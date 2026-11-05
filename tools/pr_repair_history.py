#!/usr/bin/env python3
"""Read-only local history reconciliation for the disabled UOB-6 adapter.

This is an observation, never a reservation or an authorization. The old
daily ledger is not migrated. Issue 10's separate human-approved invocation
is retained as a consumed initial attempt, including a missing result. Live
GitHub minor-writer history must be independently reconciled by the caller;
local files cannot prove its completeness. No log body is read or returned.
"""
from __future__ import annotations

from collections import Counter
from dataclasses import dataclass
from datetime import datetime, timezone
import hashlib
import json
import os
from pathlib import Path
import re
import stat
import sys

sys.dont_write_bytecode = True
import local_autodev_hourly as hourly
import pr_repair_ledger as ledger

MAX_FILES = hourly.MAX_LEDGER_ENTRIES + ledger.MAX_FILES + 2
MAX_MANUAL_BYTES = 256 * 1024
MANUAL_KEYS = frozenset(("issue", "state", "started_at", "queue_head", "argv",
                        "worktrees_before", "authorization", "no_retry"))
RESULT_KEYS = frozenset(("issue", "finished_at", "exit_code", "runner_invocations",
                        "exception_type", "worktrees_unchanged", "worktrees_after",
                        "retry_permitted"))
MANUAL_ARGV = ["tools/local_autodev_queue.py", "--execute",
               "--acknowledge-credit-risk", "--max-issues", "1"]
MANUAL_AUTHORIZATION = "Owner explicitly approved one Issue 10 run in the current chat."
SHA = re.compile(r"[0-9a-f]{40}\Z")
DIGEST = re.compile(r"[0-9a-f]{64}\Z")


class HistoryHalt(RuntimeError):
    """Only fixed reasons are allowed across this boundary."""


@dataclass(frozen=True)
class HistoryAudit:
    local_valid: bool
    daily_count: int | None
    manual_count: int | None
    repair_count: int | None
    effective_today: int | None
    effective_hour: int | None
    consumed_issues: frozenset[int]
    unresolved_count: int | None
    holds: tuple[str, ...]
    snapshot_digest: str | None
    may_reserve: bool = False
    may_publish: bool = False


def _halt(reason="local_history_unverified"):
    raise HistoryHalt(reason)


def _metadata(s):
    # atime can change after an authorized read; it is not ledger content.
    return (s.st_dev, s.st_ino, s.st_mode, s.st_uid, s.st_gid, s.st_nlink,
            s.st_size, s.st_mtime_ns, s.st_ctime_ns)


def _directory(path: Path, *, optional=False):
    # Refuse an indirect ancestor as well as a symlink at the final component.
    for item in (path, *path.parents):
        try:
            info = item.lstat()
        except FileNotFoundError:
            if item == path and optional:
                return None
            _halt()
        if stat.S_ISLNK(info.st_mode):
            _halt()
    info = path.lstat()
    if (not stat.S_ISDIR(info.st_mode) or info.st_uid != os.getuid()
            or info.st_mode & 0o077):
        _halt()
    return _metadata(info)


def _file(path: Path, *, content: bool, limit: int):
    fd = os.open(path, os.O_RDONLY | os.O_NONBLOCK | getattr(os, "O_NOFOLLOW", 0))
    with os.fdopen(fd, "rb") as stream:
        first = os.fstat(stream.fileno())
        if (not stat.S_ISREG(first.st_mode) or first.st_uid != os.getuid()
                or first.st_nlink != 1 or first.st_mode & 0o077
                or content and first.st_size > limit):
            _halt()
        raw = stream.read(limit + 1) if content else None
        if (raw is not None and len(raw) > limit
                or _metadata(first) != _metadata(os.fstat(stream.fileno()))):
            _halt("local_history_changed")
    return _metadata(first), raw


def _snapshot(root: Path):
    state = {".": (_directory(root), None)}
    count = 0
    for folder in ("daily", "attempts", "pr-repair-ledger"):
        path = root / folder
        info = _directory(path, optional=folder != "daily")
        state[folder] = (info, None)
        if info is None:
            continue
        for entry in path.iterdir():
            count += 1
            if count > MAX_FILES:
                _halt()
            name = entry.name
            # Do not read unknown entries that might contain unrelated secrets.
            if folder == "attempts":
                known = name in ("issue-10.json", "issue-10-result.json")
            elif folder == "pr-repair-ledger":
                known = bool(re.fullmatch(r"(?:reservation-[0-9a-f]{32}|event-[0-9a-f]{32}-[0-9]{2})\.json", name))
            else:
                known = name == "daily.lock" or bool(re.fullmatch(
                    r"(?:issue-[1-9][0-9]*\.json|(?:day|result)-[0-9-]+(?:-(?:hour|3h-slot|slot)-[0-9]+)?\.json|log-[0-9-]+(?:-(?:hour|3h-slot|slot)-[0-9]+)?\.txt)", name))
            if not known:
                _halt()
            read_content = name.endswith(".json")
            limit = MAX_MANUAL_BYTES if folder == "attempts" else ledger.MAX_BYTES
            state[f"{folder}/{name}"] = _file(entry, content=read_content, limit=limit)
    return state


def _json(raw):
    try:
        result = json.loads(raw.decode("utf-8"), object_pairs_hook=hourly._unique_keys,
                            parse_constant=hourly._invalid_constant)
    except (ValueError, UnicodeError, RecursionError):
        _halt()
    if type(result) is not dict:
        _halt()
    return result


def _time(value):
    if type(value) is not str or len(value) > 45:
        _halt()
    try:
        parsed = datetime.fromisoformat(value)
    except ValueError:
        _halt()
    # The reviewed one-shot wrapper writes canonical UTC timestamps.
    if parsed.utcoffset() != timezone.utc.utcoffset(parsed) or parsed.isoformat() != value:
        _halt()
    return parsed.astimezone(hourly.daily.JST)


def _worktrees(value):
    if type(value) is not list or not value or len(value) > 100:
        _halt()
    paths = set()
    for item in value:
        if (type(item) is not dict or set(item) != {"path", "head", "status_sha256",
                "unstaged_sha256", "staged_sha256", "untracked"}
                or type(item["path"]) is not str or not item["path"].startswith("/")
                or len(item["path"]) > 4096 or item["path"] in paths
                or type(item["head"]) is not str or not SHA.fullmatch(item["head"])
                or any(type(item[k]) is not str or not DIGEST.fullmatch(item[k])
                       for k in ("status_sha256", "unstaged_sha256", "staged_sha256"))
                or type(item["untracked"]) is not dict or len(item["untracked"]) > 1000):
            _halt()
        paths.add(item["path"])
        for name, digest in item["untracked"].items():
            if (type(name) is not str or len(name) > 4096
                    or type(digest) is not str or not DIGEST.fullmatch(digest)):
                _halt()


def _manual(snapshot):
    marker = snapshot.get("attempts/issue-10.json")
    result = snapshot.get("attempts/issue-10-result.json")
    if marker is None:
        if result is not None:
            _halt("orphaned_manual_history")
        return None, False
    record = _json(marker[1])
    if (set(record) != MANUAL_KEYS or type(record["issue"]) is not int
            or record["issue"] != 10 or type(record["queue_head"]) is not int
            or record["queue_head"] != 10 or record["state"] != "attempt_consumed"
            or record["no_retry"] is not True or record["argv"] != MANUAL_ARGV
            or record["authorization"] != MANUAL_AUTHORIZATION):
        _halt()
    started = _time(record["started_at"])
    _worktrees(record["worktrees_before"])
    if result is None:
        return started, True
    value = _json(result[1])
    if (set(value) != RESULT_KEYS or type(value["issue"]) is not int or value["issue"] != 10
            or type(value["exit_code"]) is not int
            or type(value["runner_invocations"]) is not list
            or value["runner_invocations"] not in ([], [10])
            or type(value["worktrees_unchanged"]) is not bool
            or value["retry_permitted"] is not False
            or value["exception_type"] is not None and
               (type(value["exception_type"]) is not str
                or not re.fullmatch(r"[A-Za-z][A-Za-z0-9_]{0,80}", value["exception_type"]))
            or _time(value["finished_at"]) < started):
        _halt()
    _worktrees(value["worktrees_after"])
    if value["worktrees_unchanged"] != (record["worktrees_before"] == value["worktrees_after"]):
        _halt()
    # Even exit zero is merely a runner result, never proof of a published PR.
    return started, value["exception_type"] is not None


def _event_conflict(record, events):
    """Validate causal order without turning later records into a refund.

    A repair's CI is about its published candidate; a rerun has no candidate
    publication and must first record CI pending. Failure or uncertainty is
    terminal for this reservation. A later correction needs independent
    reconciliation, not an automatic transition back to success.
    """
    phase = "reserved"
    receipt_sent = False
    rerun = record["kind"] == "ci_rerun"
    for event in events:
        name = event["event"]
        if phase == "terminal":
            return True
        if name == "receipt_sent":
            if phase != "reserved" or receipt_sent:
                return True
            receipt_sent = True
        elif name == "candidate_started":
            if rerun or phase != "reserved":
                return True
            phase = "authoring"
        elif name == "publish_started":
            if rerun or phase not in ("reserved", "authoring"):
                return True
            phase = "claimed"
        elif name == "published":
            if (rerun or phase != "claimed" or event["headAfter"] is None
                    or event["headAfter"] == event["headBefore"]):
                return True
            phase = "published"
        elif name == "ci_pending":
            if phase not in (("reserved", "ci") if rerun else ("published", "ci")):
                return True
            phase = "ci"
        elif name in ("ci_success", "ci_failed"):
            if phase not in (("ci",) if rerun else ("published", "ci")):
                return True
            phase = "terminal"
        elif name in ("failed", "response_unknown", "candidate_timeout", "candidate_rejected"):
            if rerun and name in ("candidate_timeout", "candidate_rejected"):
                return True
            phase = "terminal"
        else:
            return True
    return False


def _audit(root: Path, now: datetime) -> HistoryAudit:
    before = _snapshot(root)
    state = root / "daily"
    budget = hourly.read_budget(state, now)  # Reuse full old + R3 validation.
    repairs = ledger.read_ledger(state, now)
    manual_at, manual_unknown = _manual(before)
    daily = []
    consumed = set()
    unresolved = int(manual_unknown)
    intervals = []
    dates = Counter()
    for key, (_, raw) in before.items():
        if not key.startswith("daily/day-"):
            continue
        record = _json(raw)
        stem = key[len("daily/day-"):-5]
        date, kind, index = hourly._reservation_identity(stem)
        if date > now.date().isoformat():
            _halt("history_clock_regression")
        daily.append(record)
        consumed.add(record["issue"])
        dates[date] += 1
        # Old 3-hour buckets block conservatively even without an exact start.
        if kind == "hour":
            hours = frozenset((index,))
            if datetime.fromisoformat(record["attemptedAtJst"]) > now:
                _halt("history_clock_regression")
        elif kind is None:
            hours = frozenset(range(24))
        else:
            hour = index * 3 if kind == "3h-slot" else hourly.daily.LEGACY_SLOT_HOURS_JST[index]
            hours = frozenset(range(hour // 3 * 3, hour // 3 * 3 + 3))
        intervals.append((date, hours))
        if f"daily/result-{stem}.json" not in before:
            unresolved += 1
    holds = {"legacy_minor_history_unverified"}
    if manual_at is not None:
        if manual_at > now:
            _halt("history_clock_regression")
        if 10 in consumed:
            _halt("duplicate_initial_history")
        consumed.add(10)
        dates[manual_at.date().isoformat()] += 1
        intervals.append((manual_at.date().isoformat(), frozenset((manual_at.hour,))))
    claimed_heads = set()
    for record in repairs.reservations:
        created = ledger._iso(record["createdAtJst"])
        if created > now:
            _halt("history_clock_regression")
        dates[record["dateJst"]] += 1
        intervals.append((record["dateJst"], frozenset((record["hourJst"],))))
        # Missing/ambiguous events remain consumed. They do not yield a retry.
        events = [e for e in repairs.events if e["id"] == record["id"]]
        event_conflict = _event_conflict(record, events)
        if event_conflict:
            holds.add("repair_event_conflict")
        previous_time = created
        event_counts = Counter()
        for event in events:
            event_time = ledger._iso(event["atJst"])
            if event_time < previous_time or event_time > now:
                _halt("history_clock_regression")
            previous_time = event_time
            event_counts[event["event"]] += 1
            if event["event"] in ("receipt_sent", "candidate_started", "publish_started", "published"):
                if event_counts[event["event"]] > 1:
                    holds.add("repair_event_conflict")
            if event["event"] == "publish_started":
                key = (record["rootPr"], record["headSha"])
                if key in claimed_heads:
                    holds.add("repair_claim_conflict")
                claimed_heads.add(key)
            if event["event"] == "published" and (
                    event_counts["publish_started"] != 1 or event["headAfter"] is None
                    or event["headAfter"] == event["headBefore"]):
                holds.add("repair_event_conflict")
        if (event_conflict or not events
                or any(e["event"] in ("response_unknown", "candidate_timeout") for e in events)
                or events[-1]["event"] not in ("ci_success", "ci_failed", "failed", "candidate_rejected")):
            unresolved += 1
    # Cross-store overlaps were not checked by R3's per-store reader.
    occupied = set()
    for date, hours in intervals:
        slots = {(date, hour) for hour in hours}
        if occupied & slots:
            holds.add("historical_hour_conflict")
        occupied |= slots
    if any(count > ledger.DAILY_CAP for count in dates.values()):
        holds.add("historical_shared_daily_cap_exceeded")
    today = now.date().isoformat()
    effective_today = dates[today]
    effective_hour = sum(date == today and now.hour in hours for date, hours in intervals)
    # Compare independent aggregation with the reused existing accounting path.
    manual_today = int(manual_at is not None and manual_at.date().isoformat() == today)
    if effective_today != budget.used + manual_today:
        _halt("history_accounting_mismatch")
    if effective_today >= ledger.DAILY_CAP:
        holds.add("shared_daily_cap_reached")
    if effective_hour:
        holds.add("hour_already_consumed")
    if unresolved:
        holds.add("unresolved_consumed_history")
    after = _snapshot(root)
    if before != after:
        _halt("local_history_changed")
    # Digest opaque file content and selected metadata; never expose raw records.
    digest = hashlib.sha256()
    for name, (metadata, raw) in sorted(before.items()):
        digest.update(name.encode("utf-8"))
        digest.update(repr(metadata).encode("ascii"))
        if raw is not None:
            digest.update(hashlib.sha256(raw).digest())
    return HistoryAudit(True, len(daily), int(manual_at is not None),
                        len(repairs.reservations), effective_today, effective_hour,
                        frozenset(consumed), unresolved, tuple(sorted(holds)), digest.hexdigest())


def audit_history(state_root: Path, now: datetime) -> HistoryAudit:
    """Read all supported local stores without acquiring locks or writing.

    An invalid/missing store yields unknown counts, never zero availability.
    This non-atomic observation cannot authorize a later reservation; the live
    manager must eventually repeat validation under its authorized shared lock.
    """
    try:
        if (not isinstance(state_root, Path) or not state_root.is_absolute()
                or not isinstance(now, datetime) or now.tzinfo is None
                or now.utcoffset() is None):
            _halt()
        return _audit(state_root, now.astimezone(hourly.daily.JST))
    except HistoryHalt as error:
        reason = str(error)
    except (OSError, RuntimeError, ValueError, TypeError, KeyError, RecursionError):
        reason = "local_history_unverified"
    return HistoryAudit(False, None, None, None, None, None, frozenset(), None,
                        (reason, "legacy_minor_history_unverified"), None)
