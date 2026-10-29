#!/usr/bin/env python3
"""Poll approved work hourly; reserve at most eight attempts per Japan-local day.

Opt-in controller for the existing reviewed one-shot runner. It uses the SAME
private state directory, daily lock and immutable per-Issue ledger as the old
three-hour controller. Installing this file alone changes no scheduled task.
"""
from __future__ import annotations

import argparse
from dataclasses import dataclass
from datetime import datetime
import fcntl
import hashlib
import json
import os
from pathlib import Path
import re
import stat
import subprocess
import sys
from typing import Callable

sys.dont_write_bytecode = True
import local_autodev_daily as daily

MAX_DAILY_ATTEMPTS = 8
MAX_LEDGER_ENTRIES = 10000
MAX_MARKER_BYTES = 4096
POLICY = "hourly-poll-eight-attempts-v1"


@dataclass(frozen=True)
class Budget:
    used: int
    blocked: str | None


def _unique_keys(pairs: list[tuple[str, object]]) -> dict:
    value: dict = {}
    for key, item in pairs:
        if key in value:
            raise ValueError("Duplicate ledger field")
        value[key] = item
    return value


def _invalid_constant(value: str) -> None:
    raise ValueError("Non-JSON numeric constant")


def _read_marker(path: Path) -> dict:
    # O_NOFOLLOW and fstat validate the opened object, not an earlier path lookup.
    fd = os.open(path, os.O_RDONLY | os.O_NONBLOCK | getattr(os, "O_NOFOLLOW", 0))
    with os.fdopen(fd, "rb") as stream:
        info = os.fstat(stream.fileno())
        if (not stat.S_ISREG(info.st_mode) or info.st_uid != os.getuid()
                or info.st_nlink != 1 or info.st_size > MAX_MARKER_BYTES):
            raise RuntimeError("Invalid attempt ledger entry; no execution permitted")
        data = stream.read(MAX_MARKER_BYTES + 1)
    if len(data) > MAX_MARKER_BYTES:
        raise RuntimeError("Attempt ledger entry exceeds size limit")
    try:
        value = json.loads(data.decode("utf-8"), object_pairs_hook=_unique_keys,
                           parse_constant=_invalid_constant)
    except (UnicodeError, ValueError, RecursionError):
        raise RuntimeError("Unreadable attempt ledger; no execution permitted") from None
    if not isinstance(value, dict):
        raise RuntimeError("Malformed attempt ledger; no execution permitted")
    return value


def _clock_now(clock: Callable[[], datetime]) -> datetime:
    value = clock()
    if not isinstance(value, datetime) or value.tzinfo is None or value.utcoffset() is None:
        raise ValueError("An aware clock is required")
    return value.astimezone(daily.JST)


def _canonical_date(value: object) -> str:
    if not isinstance(value, str):
        raise RuntimeError("Missing attempt date")
    try:
        canonical = datetime.strptime(value, "%Y-%m-%d").date().isoformat()
    except ValueError:
        raise RuntimeError("Invalid attempt date") from None
    if canonical != value:
        raise RuntimeError("Noncanonical attempt date")
    return value


def _reservation_identity(stem: str) -> tuple[str, str | None, int | None]:
    match = re.fullmatch(r"(\d{4}-\d{2}-\d{2})(?:-(hour|3h-slot|slot)-([0-9]+))?", stem)
    if not match:
        raise RuntimeError("Unknown attempt reservation format")
    date, kind, raw_index = match.groups()
    _canonical_date(date)
    index = int(raw_index) if raw_index is not None else None
    if kind is not None:
        if str(index) != raw_index or not 0 <= index < {"hour": 24, "3h-slot": 8, "slot": 4}[kind]:
            raise RuntimeError("Invalid attempt reservation index")
    return date, kind, index


def _validate_record(value: dict, identity: tuple[str, str | None, int | None],
                     role: str) -> dict:
    """Validate the actual historic schemas; return comparable identity fields."""
    date, kind, index = identity
    fields = {"dateJst", "issue", "status"}
    if kind is not None:
        fields.add("hour" if kind == "hour" else "slot")
    if kind == "hour":
        fields.add("policy")
        if role != "result":
            fields.add("attemptedAtJst")
    if role == "result":
        if ("exitCode" in value) == ("failureType" in value):
            raise RuntimeError("Invalid attempt result schema")
        fields.add("exitCode" if "exitCode" in value else "failureType")
    if set(value) != fields:
        raise RuntimeError("Unexpected attempt ledger fields")
    if (value["dateJst"] != date or type(value["issue"]) is not int
            or value["issue"] < 1):
        raise RuntimeError("Inconsistent attempt reservation")
    if kind is not None:
        field = "hour" if kind == "hour" else "slot"
        if type(value[field]) is not int or value[field] != index:
            raise RuntimeError("Inconsistent attempt reservation index")
    if kind == "hour":
        if value["policy"] != POLICY:
            raise RuntimeError("Unknown hourly ledger policy")
        if role != "result":
            timestamp = value["attemptedAtJst"]
            try:
                recorded = datetime.fromisoformat(timestamp) if isinstance(timestamp, str) else None
            except ValueError:
                recorded = None
            if (recorded is None or recorded.isoformat() != timestamp
                    or recorded.utcoffset() != daily.JST.utcoffset(recorded)
                    or recorded.date().isoformat() != date or recorded.hour != index):
                raise RuntimeError("Inconsistent hourly attempt timestamp")
    if role == "result":
        if "exitCode" in value:
            code = value["exitCode"]
            if type(code) is not int or value["status"] != ("succeeded" if code == 0 else "failed"):
                raise RuntimeError("Inconsistent attempt result")
        elif (value["status"] != "failed" or not isinstance(value["failureType"], str)
              or value["failureType"] not in {
                "OSError", "TimeoutExpired", "BlockingIOError", "ChildProcessError",
                "ConnectionError", "BrokenPipeError", "ConnectionAbortedError",
                "ConnectionRefusedError", "ConnectionResetError", "FileExistsError",
                "FileNotFoundError", "InterruptedError", "IsADirectoryError",
                "NotADirectoryError", "PermissionError", "ProcessLookupError", "TimeoutError"}):
            raise RuntimeError("Invalid attempt failure type")
    elif value["status"] != ("attempted" if role == "issue" else "reserved" if kind == "hour" else "started"):
        raise RuntimeError("Invalid attempt reservation status")
    return {key: item for key, item in value.items()
            if key not in ("status", "exitCode", "failureType")}


def _validate_log(path: Path) -> None:
    # Never read candidate output while accounting for attempts.
    fd = os.open(path, os.O_RDONLY | os.O_NONBLOCK | getattr(os, "O_NOFOLLOW", 0))
    try:
        info = os.fstat(fd)
        if not stat.S_ISREG(info.st_mode) or info.st_uid != os.getuid() or info.st_nlink != 1:
            raise RuntimeError("Invalid attempt log object")
    finally:
        os.close(fd)


def read_budget(state: Path, now: datetime) -> Budget:
    """Read only: count reservations and validate the entire immutable ledger.

    Every historic reservation must have exactly one consistent per-Issue marker.
    Incomplete writes and orphan results/logs stop all execution, including on a
    later date; they must never make a possibly attempted Issue eligible again.
    Missing results/logs are valid (a crash can occur before either is created).
    """
    if now.tzinfo is None or now.utcoffset() is None:
        raise ValueError("An aware clock is required")
    now = now.astimezone(daily.JST)
    date, hour = now.date().isoformat(), now.hour
    if state.is_symlink():
        raise RuntimeError("Symlinked daily state directory refused")
    if not state.exists():
        return Budget(0, None)
    if not state.is_dir() or state.stat().st_uid != os.getuid():
        raise RuntimeError("Invalid daily state directory")
    entries = []
    for path in state.iterdir():
        entries.append(path)
        if len(entries) > MAX_LEDGER_ENTRIES:
            raise RuntimeError("Attempt ledger exceeds bounded entry count")
    reservations: dict[str, tuple[tuple[str, str | None, int | None], dict]] = {}
    issues: dict[int, dict] = {}
    results: dict[str, dict] = {}
    logs: set[str] = set()
    for path in entries:
        name = path.name
        if name == "daily.lock":
            continue
        match = re.fullmatch(r"issue-([1-9][0-9]*)\.json", name)
        if match:
            number = int(match[1])
            value = _read_marker(path)
            if type(value.get("issue")) is not int or value["issue"] != number:
                raise RuntimeError("Inconsistent per-Issue ledger")
            _canonical_date(value.get("dateJst"))
            issues[number] = value
            continue
        match = re.fullmatch(r"(day|result|log)-(.+)\.(json|txt)", name)
        if not match:
            raise RuntimeError("Unknown attempt ledger entry")
        role, stem, extension = match.groups()
        if extension != ("txt" if role == "log" else "json"):
            raise RuntimeError("Unknown attempt reservation format")
        identity = _reservation_identity(stem)
        if role == "log":
            _validate_log(path)
            logs.add(stem)
        else:
            value = _validate_record(_read_marker(path), identity, role)
            if role == "day":
                reservations[stem] = (identity, value)
            else:
                results[stem] = value
    if (set(results) | logs) - set(reservations):
        raise RuntimeError("Orphaned attempt result or log; review required")
    represented_issues: set[int] = set()
    used, blocked = 0, None
    for stem, (identity, value) in reservations.items():
        issue = value["issue"]
        if issue in represented_issues:
            raise RuntimeError("Duplicate Issue reservations; review required")
        represented_issues.add(issue)
        if issue not in issues:
            raise RuntimeError("Missing per-Issue reservation; review required")
        if _validate_record(issues[issue], identity, "issue") != value:
            raise RuntimeError("Inconsistent per-Issue reservation identity")
        if stem in results:
            expected_result = {key: item for key, item in value.items() if key != "attemptedAtJst"}
            if results[stem] != expected_result:
                raise RuntimeError("Inconsistent result reservation identity")
        recorded_date, kind, index = identity
        if recorded_date != date:
            continue
        used += 1
        if kind is None:
            blocked = "legacy daily reservation"
        elif kind == "hour":
            if index == hour:
                blocked = blocked or "hour already reserved"
        else:
            old_hour = index * 3 if kind == "3h-slot" else daily.LEGACY_SLOT_HOURS_JST[index]
            if hour // 3 == old_hour // 3:
                blocked = blocked or "legacy execution window already reserved"
    if set(issues) != represented_issues:
        raise RuntimeError("Orphaned per-Issue reservation; review required")
    if used >= MAX_DAILY_ATTEMPTS:
        blocked = blocked or "daily attempt cap reached"
    return Budget(used, blocked)


def reserve_attempt(state: Path, now: datetime, issue: int) -> tuple[Path, Path]:
    """Must run under daily.lock. Write-before-call; failures consume reservations."""
    if now.tzinfo is None or now.utcoffset() is None:
        raise ValueError("An aware clock is required")
    now = now.astimezone(daily.JST)
    budget = read_budget(state, now)
    if budget.blocked is not None:
        raise RuntimeError(budget.blocked)
    if type(issue) is not int or issue < 1:
        raise ValueError("Invalid Issue number")
    issue_path = state / f"issue-{issue}.json"
    if issue_path.exists() or issue_path.is_symlink():
        raise RuntimeError("Issue previously attempted; no retry")
    stem = f"{now.date().isoformat()}-hour-{now.hour}"
    data = {"policy": POLICY, "dateJst": now.date().isoformat(), "hour": now.hour,
            "issue": issue, "attemptedAtJst": now.isoformat(), "status": "reserved"}
    daily.create_private(state / f"day-{stem}.json", json.dumps(data) + "\n")
    daily.create_private(issue_path, json.dumps({**data, "status": "attempted"}) + "\n")
    # Persist directory entries before allowing a model call.
    fd = os.open(state, os.O_RDONLY | getattr(os, "O_DIRECTORY", 0))
    try:
        os.fsync(fd)
    finally:
        os.close(fd)
    return state / f"log-{stem}.txt", state / f"result-{stem}.json"


def runtime_fingerprint(repo: Path) -> tuple[tuple[str, str], ...]:
    paths = ["tools/local_autodev_hourly.py", "tools/local_autodev_daily.py",
             "tools/local_autodev_queue.py", "tools/local_autodev.py"]
    result = []
    for relative in paths:
        path = repo / relative
        if path.is_symlink() or not path.is_file():
            raise RuntimeError("Missing or symlinked runtime module")
        result.append((relative, hashlib.sha256(path.read_bytes()).hexdigest()))
    return tuple(result)


def run_hourly(repo: Path, state: Path, *, execute: bool, acknowledge_credit_risk: bool,
               max_issues: int = 1, clock: Callable[[], datetime] | None = None) -> str:
    if type(max_issues) is not int or max_issues != 1:
        raise ValueError("Hard limit is one Issue per invocation")
    if execute and not acknowledge_credit_risk:
        raise ValueError("Unattended execution requires explicit credit consent")
    clock = clock or (lambda: datetime.now(daily.JST))
    daily.verify_checkout(repo, allow_behind=True)
    if not execute:
        budget = read_budget(state, _clock_now(clock))
        if budget.blocked:
            return f"DRY RUN: {budget.blocked}; attempts {budget.used}/{MAX_DAILY_ATTEMPTS}"
        if daily.open_pr_numbers(repo):
            return "DRY RUN: open PR(s); would stop"
        candidates = daily.queue.list_issues(repo)
        issue = daily.first_unattempted_issue(candidates, state)
        if issue is None:
            return "DRY RUN: no approved unattempted Issue"
        return (f"DRY RUN: would invoke only Issue #{issue}; "
                f"attempts {budget.used}/{MAX_DAILY_ATTEMPTS}; no reservation or model call")
    daily.ensure_private_state(state)
    fd = os.open(state / "daily.lock", os.O_CREAT | os.O_RDWR | getattr(os, "O_NOFOLLOW", 0), 0o600)
    try:
        info = os.fstat(fd)
        if not stat.S_ISREG(info.st_mode) or info.st_uid != os.getuid() or info.st_nlink != 1:
            raise RuntimeError("Invalid daily lock")
        try:
            fcntl.flock(fd, fcntl.LOCK_EX | fcntl.LOCK_NB)
        except BlockingIOError:
            return "Skipped: another daily runner is active"
        budget = read_budget(state, _clock_now(clock))
        if budget.blocked:
            return f"Skipped: {budget.blocked}; attempts {budget.used}/{MAX_DAILY_ATTEMPTS}"
        before = runtime_fingerprint(repo)
        daily.refresh_checkout(repo)
        if runtime_fingerprint(repo) != before:
            return "Skipped: runtime updated; next scheduled poll must load the reviewed version"
        if daily.open_pr_numbers(repo):
            return "Skipped: open PR(s) await review/merge"
        candidates = daily.queue.list_issues(repo)
        issue = daily.first_unattempted_issue(candidates, state)
        if issue is None:
            return "Skipped: no approved unattempted Issue"
        if daily.open_pr_numbers(repo):
            return "Skipped: a PR appeared during preflight"
        # Refresh the JST date/hour AFTER network operations, still under lock.
        now = _clock_now(clock)
        budget = read_budget(state, now)
        if budget.blocked:
            return f"Skipped: {budget.blocked}; attempts {budget.used}/{MAX_DAILY_ATTEMPTS}"
        log_path, result_path = reserve_attempt(state, now, issue)
        data = {"policy": POLICY, "dateJst": now.date().isoformat(),
                "hour": now.hour, "issue": issue}
        print(f"Reserved Issue #{issue}; JST daily attempts {budget.used + 1}/{MAX_DAILY_ATTEMPTS}. "
              "One ChatGPT-plan call; purchased credits may be consumed.", flush=True)
        try:
            code = daily.invoke_once(repo, issue, log_path)
        except (OSError, subprocess.TimeoutExpired) as error:
            daily.create_private(result_path, json.dumps({**data, "status": "failed",
                                 "failureType": type(error).__name__}) + "\n")
            raise RuntimeError("One-shot runner failed; no retry permitted") from None
        daily.create_private(result_path, json.dumps({**data, "exitCode": code,
                             "status": "succeeded" if code == 0 else "failed"}) + "\n")
        if code != 0:
            raise RuntimeError(f"Issue #{issue} runner failed (exit={code}); no automatic retry")
        return f"One Issue #{issue} completed; review Draft PR and CI before next run"
    finally:
        os.close(fd)


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--repo-path", type=Path, default=daily.DEFAULT_PATH)
    parser.add_argument("--execute", action="store_true")
    parser.add_argument("--acknowledge-credit-risk", action="store_true")
    parser.add_argument("--max-issues", type=int, default=1)
    args = parser.parse_args()
    try:
        print(run_hourly(args.repo_path.expanduser().resolve(strict=True), daily.STATE,
                         execute=args.execute, acknowledge_credit_risk=args.acknowledge_credit_risk,
                         max_issues=args.max_issues), flush=True)
        return 0
    except (OSError, ValueError, RuntimeError, subprocess.TimeoutExpired) as error:
        print(f"Stopped: {error}", file=sys.stderr)
        return 1


if __name__ == "__main__":
    raise SystemExit(main())
