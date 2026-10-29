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
        value = json.loads(data.decode("utf-8"), object_pairs_hook=_unique_keys)
    except (UnicodeError, ValueError):
        raise RuntimeError("Unreadable attempt ledger; no execution permitted") from None
    if not isinstance(value, dict):
        raise RuntimeError("Malformed attempt ledger; no execution permitted")
    return value


def _clock_now(clock: Callable[[], datetime]) -> datetime:
    value = clock()
    if not isinstance(value, datetime) or value.tzinfo is None or value.utcoffset() is None:
        raise ValueError("An aware clock is required")
    return value.astimezone(daily.JST)


def read_budget(state: Path, now: datetime) -> Budget:
    """Read only. Count reservations, not successful runs; malformed state fails closed.

    Old four-/three-hour reservations each consume one allowance. Their entire
    original three-hour bucket is blocked conservatively during migration.
    A former one-per-day reservation still blocks the whole day. An orphaned
    same-day Issue marker also consumes allowance; never reclaim failed work.
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
    used, blocked = 0, None
    represented_issues: set[int] = set()
    prefix = f"day-{date}"
    pattern = re.compile(rf"day-{re.escape(date)}-(?:(hour|3h-slot|slot)-([0-9]+))\.json\Z")
    for path in entries:
        name = path.name
        if not name.startswith(prefix):
            continue
        value = _read_marker(path)
        issue = value.get("issue")
        if value.get("dateJst") != date or type(issue) is not int or issue < 1:
            raise RuntimeError("Inconsistent attempt reservation")
        represented_issues.add(issue)
        used += 1  # Deliberately do not deduplicate two reservation files.
        if name == f"day-{date}.json":
            blocked = "legacy daily reservation"
            continue
        match = pattern.fullmatch(name)
        if not match:
            raise RuntimeError("Unknown attempt reservation format")
        kind, raw_index = match.groups()
        index = int(raw_index)
        limit = {"hour": 24, "3h-slot": 8, "slot": 4}[kind]
        if str(index) != raw_index or not 0 <= index < limit:
            raise RuntimeError("Invalid attempt reservation index")
        field = "hour" if kind == "hour" else "slot"
        if type(value.get(field)) is not int or value[field] != index:
            raise RuntimeError("Inconsistent attempt reservation index")
        if kind == "hour":
            if index == hour:
                blocked = blocked or "hour already reserved"
        else:
            old_hour = index * 3 if kind == "3h-slot" else daily.LEGACY_SLOT_HOURS_JST[index]
            if hour // 3 == old_hour // 3:
                blocked = blocked or "legacy execution window already reserved"
    for path in entries:
        if not re.fullmatch(r"issue-[1-9][0-9]*\.json", path.name):
            continue
        value = _read_marker(path)
        issue = int(path.name[6:-5])
        if type(value.get("issue")) is not int or value["issue"] != issue:
            raise RuntimeError("Inconsistent per-Issue ledger")
        recorded_date = value.get("dateJst")
        if not isinstance(recorded_date, str):
            raise RuntimeError("Missing per-Issue attempt date")
        try:
            parsed_date = datetime.strptime(recorded_date, "%Y-%m-%d").date().isoformat()
        except ValueError:
            raise RuntimeError("Invalid per-Issue attempt date") from None
        if parsed_date != recorded_date:
            raise RuntimeError("Noncanonical per-Issue attempt date")
        if recorded_date == date and issue not in represented_issues:
            used += 1
            # With the reservation missing, the attempt's exact window is uncertain.
            blocked = blocked or "orphaned same-day Issue reservation; review required"
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
