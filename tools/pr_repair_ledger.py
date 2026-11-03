#!/usr/bin/env python3
"""R3 fail-closed, append-only PR-repair reservation ledger (INACTIVE).

No model, GitHub API, task scheduling, or production activation lives here.
The R4 trusted adapter must independently obtain Gate facts; a caller-provided
Issue, PR, hash or approval claim is NOT itself authorization.
"""
from __future__ import annotations

from dataclasses import dataclass
from datetime import datetime
import fcntl
import json
import os
from pathlib import Path
import re
import secrets
import stat
import sys
from typing import Callable

sys.dont_write_bytecode = True
import local_autodev_daily as daily

SCHEMA = "footprint-pr-repair-ledger-v1"
KINDS = frozenset(("minor", "substantive", "ci_rerun"))
EVENTS = frozenset(("receipt_sent", "candidate_started", "candidate_rejected",
                    "candidate_timeout", "publish_started", "published",
                    "failed", "response_unknown", "ci_pending", "ci_failed", "ci_success"))
SHA = re.compile(r"[0-9a-f]{40}\Z")
DIGEST = re.compile(r"[0-9a-f]{64}\Z")
RID = re.compile(r"[0-9a-f]{32}\Z")
MAX_FILES = 20000
MAX_BYTES = 4096
DAILY_CAP = 8
SUBSTANTIVE_DAILY_CAP = 2

RESERVATION_KEYS = frozenset(("schema", "id", "kind", "rootIssue", "rootPr",
    "approvedTitleHash", "approvedBodyHash", "approvalReservationId",
    "approvalResultId", "mainSha", "headSha", "createdAtJst", "expiresAtEpoch",
    "dateJst", "hourJst", "attempt", "status"))
EVENT_KEYS = frozenset(("schema", "id", "sequence", "event", "atJst",
                        "headBefore", "headAfter"))


class LedgerHalt(RuntimeError):
    """A fixed reason, not a raw ledger value or secret."""


@dataclass(frozen=True)
class Gate:
    """Facts from a separately VERIFIED approval/PR/GitHub adapter (R4).

    trusted_roots is a complete, independently observed map of approved root
    Issue -> original PR, NOT a map constructed from the repair request.
    """
    root_issue: int
    root_pr: int
    trusted_roots: tuple[tuple[int, int], ...]
    approval_reservation_id: int
    approval_result_id: int
    approved_title_hash: str
    approved_body_hash: str
    observed_title_hash: str
    observed_body_hash: str
    requested_main_sha: str
    observed_main_sha: str
    requested_head_sha: str
    observed_head_sha: str
    open_pr_numbers: tuple[int, ...]
    expires_at_epoch: int
    ci_rerun_approved: bool = False


@dataclass(frozen=True)
class Audit:
    reservations: tuple[dict, ...]
    events: tuple[dict, ...]
    today: int
    hour: int
    substantive_today: int


def _halt() -> None:
    raise LedgerHalt("repair_ledger_unverified_or_inconsistent")


def _now(clock: Callable[[], datetime]) -> datetime:
    t = clock()
    if not isinstance(t, datetime) or t.tzinfo is None or t.utcoffset() is None:
        _halt()
    return t.astimezone(daily.JST)


def _iso(value: object) -> datetime:
    if type(value) is not str or len(value) > 45:
        _halt()
    try:
        t = datetime.fromisoformat(value)
    except ValueError:
        _halt()
    if t.tzinfo is None or t.utcoffset() != daily.JST.utcoffset(t):
        _halt()
    if t.isoformat() != value:
        _halt()
    return t


def _sha(value: object, kind: str) -> bool:
    return type(value) is str and bool((SHA if kind == "sha" else DIGEST).fullmatch(value))


def _positive(value: object) -> bool:
    return type(value) is int and value > 0


def _canonical(value: dict) -> bytes:
    return (json.dumps(value, sort_keys=True, separators=(",", ":"),
                       ensure_ascii=True, allow_nan=False) + "\n").encode("ascii")


def _pairs(pairs: list[tuple[str, object]]) -> dict:
    out: dict = {}
    for key, value in pairs:
        if key in out:
            _halt()
        out[key] = value
    return out


def _bad_constant(_: str) -> None:
    _halt()


def _fsync_directory(folder: Path) -> None:
    """Persist directory entries, including a newly created ledger directory."""
    fd = os.open(folder, os.O_RDONLY | getattr(os, "O_DIRECTORY", 0)
                 | getattr(os, "O_NOFOLLOW", 0))
    try:
        os.fsync(fd)
    finally:
        os.close(fd)


def _private_directory(folder: Path, *, create: bool) -> bool:
    if create:
        folder.mkdir(mode=0o700, exist_ok=True)
    try:
        info = folder.lstat()
    except FileNotFoundError:
        return False
    if (not stat.S_ISDIR(info.st_mode) or info.st_uid != os.getuid()
            or info.st_mode & 0o077):
        _halt()
    if create:
        # The new directory entry must survive a crash before any receipt
        # within it can be considered durable.
        _fsync_directory(folder.parent)
    return True


def _read_file(path: Path) -> dict:
    fd = os.open(path, os.O_RDONLY | os.O_NONBLOCK | getattr(os, "O_NOFOLLOW", 0))
    with os.fdopen(fd, "rb") as stream:
        meta = os.fstat(stream.fileno())
        if (not stat.S_ISREG(meta.st_mode) or meta.st_uid != os.getuid()
                or meta.st_nlink != 1 or meta.st_size > MAX_BYTES
                or meta.st_mode & 0o077):
            _halt()
        raw = stream.read(MAX_BYTES + 1)
    if len(raw) > MAX_BYTES:
        _halt()
    try:
        obj = json.loads(raw.decode("ascii"), object_pairs_hook=_pairs,
                         parse_constant=_bad_constant)
    except (ValueError, UnicodeError, RecursionError):
        _halt()
    if type(obj) is not dict or raw != _canonical(obj):
        _halt()
    return obj


def _write_file(path: Path, value: dict) -> None:
    raw = _canonical(value)
    if len(raw) > MAX_BYTES:
        _halt()
    fd = os.open(path, os.O_CREAT | os.O_EXCL | os.O_WRONLY |
                 getattr(os, "O_NOFOLLOW", 0), 0o600)
    with os.fdopen(fd, "wb") as stream:
        stream.write(raw)
        stream.flush()
        os.fsync(stream.fileno())
    _fsync_directory(path.parent)


def _folder(state: Path) -> Path:
    # Adjacent to, never inside or replacing, the old daily/issue ledger.
    return state.parent / "pr-repair-ledger"


def _validate_reservation(rec: dict, name: str) -> None:
    if (set(rec) != RESERVATION_KEYS or rec["schema"] != SCHEMA
            or type(rec["id"]) is not str or not RID.fullmatch(rec["id"])
            or name != "reservation-" + rec["id"] + ".json"
            or rec["status"] != "reserved" or type(rec["kind"]) is not str
            or rec["kind"] not in KINDS
            or not _positive(rec["rootIssue"]) or not _positive(rec["rootPr"])
            or not _positive(rec["approvalReservationId"])
            or not _positive(rec["approvalResultId"])
            or not _sha(rec["approvedTitleHash"], "digest")
            or not _sha(rec["approvedBodyHash"], "digest")
            or not _sha(rec["mainSha"], "sha") or not _sha(rec["headSha"], "sha")
            or type(rec["expiresAtEpoch"]) is not int or rec["expiresAtEpoch"] <= 0
            or type(rec["dateJst"]) is not str
            or type(rec["hourJst"]) is not int
            or not 0 <= rec["hourJst"] <= 23
            or type(rec["attempt"]) is not int or not 1 <= rec["attempt"] <= 2):
        _halt()
    start = _iso(rec["createdAtJst"])
    if (rec["dateJst"] != start.date().isoformat()
            or rec["hourJst"] != start.hour
            or rec["expiresAtEpoch"] <= int(start.timestamp())
            or rec["expiresAtEpoch"] - int(start.timestamp()) > 86400
            or rec["kind"] == "minor" and rec["attempt"] != 1
            or rec["kind"] == "ci_rerun" and rec["attempt"] != 1):
        _halt()


def _validate_event(rec: dict, name: str) -> None:
    if (set(rec) != EVENT_KEYS or rec["schema"] != SCHEMA
            or type(rec["id"]) is not str or not RID.fullmatch(rec["id"])
            or type(rec["sequence"]) is not int or not 1 <= rec["sequence"] <= 99
            or name != f'event-{rec["id"]}-{rec["sequence"]:02d}.json'
            or type(rec["event"]) is not str or rec["event"] not in EVENTS
            or not _sha(rec["headBefore"], "sha")
            or (rec["headAfter"] is not None and not _sha(rec["headAfter"], "sha"))):
        _halt()
    _iso(rec["atJst"])


def read_ledger(state: Path, now: datetime) -> Audit:
    """Read/validate ALL historic records; never repair, truncate or delete."""
    folder = _folder(state)
    if not _private_directory(folder, create=False):
        return Audit((), (), 0, 0, 0)
    files = sorted(folder.iterdir(), key=lambda p: p.name)
    if len(files) > MAX_FILES:
        _halt()
    reservations: dict[str, dict] = {}
    events: dict[str, list[dict]] = {}
    for path in files:
        name = path.name
        if re.fullmatch(r"reservation-[0-9a-f]{32}\.json", name):
            rec = _read_file(path)
            _validate_reservation(rec, name)
            reservations[rec["id"]] = rec
        elif re.fullmatch(r"event-[0-9a-f]{32}-[0-9]{2}\.json", name):
            rec = _read_file(path)
            _validate_event(rec, name)
            events.setdefault(rec["id"], []).append(rec)
        else:
            _halt()
    roots_issue: dict[int, tuple[int, int, int, str, str]] = {}
    roots_pr: dict[int, int] = {}
    approval_ids: dict[int, tuple[int, int]] = {}
    approval_results: dict[int, tuple[int, int]] = {}
    attempts: dict[tuple[int, int, str], list[int]] = {}
    per_root_repairs: dict[tuple[int, int], int] = {}
    substantive_per_date: dict[str, int] = {}
    total_per_date: dict[str, int] = {}
    rerun_heads: set[tuple[int, int, str]] = set()
    used_hours: set[tuple[str, int]] = set()
    for rec in reservations.values():
        issue, pr = rec["rootIssue"], rec["rootPr"]
        ident = (pr, rec["approvalReservationId"], rec["approvalResultId"],
                 rec["approvedTitleHash"], rec["approvedBodyHash"])
        if (issue in roots_issue and roots_issue[issue] != ident
                or pr in roots_pr and roots_pr[pr] != issue
                or rec["approvalReservationId"] in approval_ids
                   and approval_ids[rec["approvalReservationId"]] != (issue, pr)
                or rec["approvalResultId"] in approval_results
                   and approval_results[rec["approvalResultId"]] != (issue, pr)):
            _halt()
        roots_issue[issue] = ident
        roots_pr[pr] = issue
        approval_ids[rec["approvalReservationId"]] = (issue, pr)
        approval_results[rec["approvalResultId"]] = (issue, pr)
        total_per_date[rec["dateJst"]] = total_per_date.get(rec["dateJst"], 0) + 1
        if rec["kind"] == "substantive":
            substantive_per_date[rec["dateJst"]] = substantive_per_date.get(rec["dateJst"], 0) + 1
        if rec["kind"] in ("minor", "substantive"):
            root = (issue, pr)
            per_root_repairs[root] = per_root_repairs.get(root, 0) + 1
        key = (issue, pr, rec["kind"])
        attempts.setdefault(key, []).append(rec["attempt"])
        if rec["kind"] == "ci_rerun":
            h = (issue, pr, rec["headSha"])
            if h in rerun_heads:
                _halt()
            rerun_heads.add(h)
        hh = (rec["dateJst"], rec["hourJst"])
        if hh in used_hours:
            _halt()
        used_hours.add(hh)
    if (any(count > 2 for count in per_root_repairs.values())
            or any(count > SUBSTANTIVE_DAILY_CAP for count in substantive_per_date.values())
            or any(count > DAILY_CAP for count in total_per_date.values())):
        _halt()
    for (issue, pr, kind), values in attempts.items():
        if (len(values) > (2 if kind == "substantive" else 1)
                or sorted(values) != list(range(1, len(values) + 1))):
            # ci_rerun can occur on multiple distinct heads; validate separately.
            if kind != "ci_rerun" or values != [1] * len(values):
                _halt()
    result_events: list[dict] = []
    for rid, seq in events.items():
        if rid not in reservations:
            _halt()
        seq.sort(key=lambda x: x["sequence"])
        if [x["sequence"] for x in seq] != list(range(1, len(seq) + 1)):
            _halt()
        for evt in seq:
            if (evt["headBefore"] != reservations[rid]["headSha"]
                    or _iso(evt["atJst"]) < _iso(reservations[rid]["createdAtJst"])):
                _halt()
        result_events.extend(seq)
    day = now.date().isoformat()
    current = [r for r in reservations.values() if r["dateJst"] == day]
    return Audit(tuple(reservations.values()), tuple(result_events),
                 len(current), sum(r["hourJst"] == now.hour for r in current),
                 sum(r["kind"] == "substantive" for r in current))


def merge_budget(state: Path, now: datetime, legacy: object) -> object:
    """Called by hourly.read_budget so old initial runs see ALL repair reservations."""
    audit = read_ledger(state, now)
    used = legacy.used + audit.today
    reason = legacy.blocked
    if audit.hour:
        reason = reason or "repair operation already reserved this hour"
    if used >= DAILY_CAP:
        reason = reason or "shared daily reservation cap reached"
    return type(legacy)(used, reason)


def _validate_gate(g: Gate) -> None:
    if not isinstance(g, Gate):
        _halt()
    if (not _positive(g.root_issue) or not _positive(g.root_pr)
            or not _positive(g.approval_reservation_id)
            or not _positive(g.approval_result_id)
            or not all(_sha(x, "digest") for x in (
                g.approved_title_hash, g.approved_body_hash,
                g.observed_title_hash, g.observed_body_hash))
            or not all(_sha(x, "sha") for x in (
                g.requested_main_sha, g.observed_main_sha,
                g.requested_head_sha, g.observed_head_sha))
            or g.approved_title_hash != g.observed_title_hash
            or g.approved_body_hash != g.observed_body_hash
            or g.requested_main_sha != g.observed_main_sha
            or g.requested_head_sha != g.observed_head_sha
            or type(g.trusted_roots) is not tuple
            or not g.trusted_roots
            or any(type(k) is not int or type(v) is not int or k <= 0 or v <= 0
                   for k, v in g.trusted_roots)
            or len(set(g.trusted_roots)) != len(g.trusted_roots)
            or len({k for k, _ in g.trusted_roots}) != len(g.trusted_roots)
            or len({v for _, v in g.trusted_roots}) != len(g.trusted_roots)
            or (g.root_issue, g.root_pr) not in g.trusted_roots
            or type(g.open_pr_numbers) is not tuple
            or g.open_pr_numbers != (g.root_pr,)
            or type(g.expires_at_epoch) is not int
            or type(g.ci_rerun_approved) is not bool):
        _halt()


def _lock(state: Path) -> int:
    daily.ensure_private_state(state)
    fd = os.open(state / "daily.lock", os.O_CREAT | os.O_RDWR |
                 getattr(os, "O_NOFOLLOW", 0), 0o600)
    info = os.fstat(fd)
    if (not stat.S_ISREG(info.st_mode) or info.st_uid != os.getuid()
            or info.st_nlink != 1 or info.st_mode & 0o077):
        os.close(fd)
        _halt()
    try:
        fcntl.flock(fd, fcntl.LOCK_EX | fcntl.LOCK_NB)
    except BlockingIOError:
        os.close(fd)
        raise LedgerHalt("another_daily_runner_active") from None
    return fd


def reserve(state: Path, gate: Gate, kind: str, *,
            clock: Callable[[], datetime] | None = None, execute: bool = False) -> dict:
    """Offline ledger operation; default dry-run makes NO changes or model calls.

    execute only authorizes a durable reservation (not a model, GitHub write,
    PR modification, or automation scheduling). Re-read time UNDER the lock.
    """
    _validate_gate(gate)
    if type(kind) is not str or kind not in KINDS:
        _halt()
    if kind == "ci_rerun" and not gate.ci_rerun_approved:
        raise LedgerHalt("active_ci_rerun_requires_independent_authorization")
    clock = clock or (lambda: datetime.now(daily.JST))
    fd = _lock(state) if execute else None
    try:
        now = _now(clock)
        if not int(now.timestamp()) < gate.expires_at_epoch <= int(now.timestamp()) + 86400:
            raise LedgerHalt("repair_reservation_expired_or_unverified")
        # Old ledger is validated even on a dry run. It must not be rewritten.
        import local_autodev_hourly as hourly
        budget = hourly.read_budget(state, now)
        audit = read_ledger(state, now)
        if budget.blocked:
            raise LedgerHalt(budget.blocked)
        if budget.used >= DAILY_CAP or audit.hour:
            raise LedgerHalt("shared_reservation_cap_reached")
        if kind == "substantive" and audit.substantive_today >= SUBSTANTIVE_DAILY_CAP:
            raise LedgerHalt("substantive_daily_cap_reached")
        history = [r for r in audit.reservations
                   if r["rootIssue"] == gate.root_issue
                   or r["rootPr"] == gate.root_pr
                   or r["approvalReservationId"] == gate.approval_reservation_id
                   or r["approvalResultId"] == gate.approval_result_id]
        for prior in history:
            if (prior["rootIssue"] != gate.root_issue
                    or prior["rootPr"] != gate.root_pr
                    or prior["approvalReservationId"] != gate.approval_reservation_id
                    or prior["approvalResultId"] != gate.approval_result_id
                    or prior["approvedTitleHash"] != gate.approved_title_hash
                    or prior["approvedBodyHash"] != gate.approved_body_hash):
                _halt()
        repair = [r for r in history if r["kind"] in ("minor", "substantive")]
        minors = [r for r in repair if r["kind"] == "minor"]
        substantives = [r for r in repair if r["kind"] == "substantive"]
        reruns = [r for r in history if r["kind"] == "ci_rerun"
                  and r["headSha"] == gate.observed_head_sha]
        if ((kind in ("minor", "substantive") and len(repair) >= 2)
                or (kind == "minor" and minors)
                or (kind == "substantive" and len(substantives) >= 2)
                or (kind == "ci_rerun" and reruns)):
            raise LedgerHalt("repair_lifetime_limit_reached")
        attempt = 1 + (len(substantives) if kind == "substantive" else 0)
        record = {
            "schema": SCHEMA, "id": secrets.token_hex(16) if execute else "0" * 32,
            "kind": kind, "rootIssue": gate.root_issue, "rootPr": gate.root_pr,
            "approvedTitleHash": gate.approved_title_hash,
            "approvedBodyHash": gate.approved_body_hash,
            "approvalReservationId": gate.approval_reservation_id,
            "approvalResultId": gate.approval_result_id,
            "mainSha": gate.observed_main_sha, "headSha": gate.observed_head_sha,
            "createdAtJst": now.isoformat(), "expiresAtEpoch": gate.expires_at_epoch,
            "dateJst": now.date().isoformat(), "hourJst": now.hour,
            "attempt": attempt, "status": "reserved",
        }
        if execute:
            folder = _folder(state)
            _private_directory(folder, create=True)
            _write_file(folder / ("reservation-" + record["id"] + ".json"), record)
        return record
    finally:
        if fd is not None:
            os.close(fd)


def append_event(state: Path, reservation_id: str, event: str, *,
                 head_after: str | None = None,
                 clock: Callable[[], datetime] | None = None) -> None:
    """Append once under the shared lock. Never unconsume a reservation."""
    if (type(reservation_id) is not str or not RID.fullmatch(reservation_id)
            or event not in EVENTS
            or (head_after is not None and not _sha(head_after, "sha"))):
        _halt()
    fd = _lock(state)
    try:
        now = _now(clock or (lambda: datetime.now(daily.JST)))
        audit = read_ledger(state, now)
        found = [r for r in audit.reservations if r["id"] == reservation_id]
        if len(found) != 1:
            _halt()
        record = found[0]
        previous = [e for e in audit.events if e["id"] == reservation_id]
        if len(previous) >= 99 or now < _iso(record["createdAtJst"]):
            _halt()
        seq = len(previous) + 1
        data = {"schema": SCHEMA, "id": reservation_id, "sequence": seq,
                "event": event, "atJst": now.isoformat(),
                "headBefore": record["headSha"], "headAfter": head_after}
        _write_file(_folder(state) / f"event-{reservation_id}-{seq:02d}.json", data)
    finally:
        os.close(fd)
