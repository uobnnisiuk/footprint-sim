#!/usr/bin/env python3
"""Run at most one approved Local AutoDev Issue per Japan-local calendar day.

No background scheduling is installed by this file. Execution requires two
explicit flags and a separate Windows Task Scheduler installation step.
"""
from __future__ import annotations

import argparse
from datetime import datetime
import fcntl
import json
import os
from pathlib import Path
import stat
import subprocess
import sys
from zoneinfo import ZoneInfo

# Preflight must not dirty the checkout, even when invoked without python -B.
sys.dont_write_bytecode = True
import local_autodev_queue as queue

REPO = queue.REPO
DEFAULT_PATH = Path(__file__).resolve().parents[1]
STATE = Path.home() / ".local" / "state" / "footprint-local-autodev" / "daily"
JST = ZoneInfo("Asia/Tokyo")
MAX_PR_LIST = 500


def command(args: list[str], cwd: Path, timeout: int = 60, *,
            strip: bool = True) -> str:
    result = subprocess.run(
        args, cwd=cwd, capture_output=True, text=True,
        timeout=timeout, check=False)
    if result.returncode:
        raise RuntimeError(f"Preflight command failed: {args[0]} "
                           f"(exit={result.returncode}); output withheld")
    return result.stdout.strip() if strip else result.stdout


def verify_checkout(repo_path: Path) -> None:
    if not (repo_path / "tools" / "local_autodev.py").is_file():
        raise RuntimeError("Local AutoDev checkout not found")
    root = command(["git", "rev-parse", "--show-toplevel"], repo_path)
    if Path(root).resolve() != repo_path.resolve():
        raise RuntimeError("Expected checkout root does not match")
    branch = command(["git", "branch", "--show-current"], repo_path)
    if branch not in ("main", ""):
        raise RuntimeError("Dedicated AutoDev checkout must be on main or detached")
    # A branch name alone does not prove that the checkout is the approved base.
    # Only compare local refs: preflight never fetches or updates any worktree.
    head = command(["git", "rev-parse", "--verify", "HEAD^{commit}"], repo_path)
    base = command(["git", "rev-parse", "--verify",
                    "refs/remotes/origin/main^{commit}"], repo_path)
    if head != base:
        raise RuntimeError("Dedicated AutoDev HEAD must match local origin/main; "
                           "review and update the dedicated checkout manually")
    status = command(["git", "status", "--porcelain=v1", "-z",
                      "--untracked-files=all"], repo_path, strip=False)
    for entry in status.split("\0"):
        if not entry:
            continue
        path = Path(entry[3:])
        # Tolerate only generated, untracked bytecode in these known locations.
        # Tracked edits, source inside __pycache__, and symlinks still stop us.
        if (entry.startswith("?? ") and path.suffix == ".pyc" and
                path.parent.as_posix() in ("tools/__pycache__",
                                           ".github/scripts/__pycache__") and
                stat.S_ISREG((repo_path / path).lstat().st_mode)):
            continue
        raise RuntimeError("Dedicated AutoDev checkout has uncommitted changes")


def open_pr_numbers(repo_path: Path) -> list[int]:
    raw = command([
        "gh", "pr", "list", "--repo", REPO, "--state", "open",
        "--limit", str(MAX_PR_LIST), "--json", "number"
    ], repo_path)
    payload = json.loads(raw)
    if not isinstance(payload, list) or len(payload) >= MAX_PR_LIST:
        raise RuntimeError("Cannot reliably check all open PRs; stopping")
    numbers = [p.get("number") for p in payload if isinstance(p, dict)]
    if len(numbers) != len(payload) or any(type(x) is not int for x in numbers):
        raise RuntimeError("Unexpected PR list data; stopping")
    return numbers


def ensure_private_state(path: Path) -> None:
    if path.is_symlink():
        raise RuntimeError("Symlinked daily state directory refused")
    path.mkdir(mode=0o700, parents=True, exist_ok=True)
    if not path.is_dir() or path.is_symlink():
        raise RuntimeError("Invalid daily state directory")
    if path.stat().st_uid != os.getuid():
        raise RuntimeError("Daily state directory is not owned by current user")
    path.chmod(0o700)


def create_private(path: Path, data: str) -> None:
    flags = os.O_CREAT | os.O_EXCL | os.O_WRONLY | getattr(os, "O_NOFOLLOW", 0)
    fd = os.open(path, flags, 0o600)
    with os.fdopen(fd, "w", encoding="utf-8") as stream:
        stream.write(data)
        stream.flush()
        os.fsync(stream.fileno())


def reserve_attempt(state: Path, date: str, issue_number: int) -> None:
    # The date marker is reserved *before* invoking any model. If the process
    # crashes or power is lost, that date remains consumed: never retry.
    day_file = state / f"day-{date}.json"
    issue_file = state / f"issue-{issue_number}.json"
    if day_file.exists() or day_file.is_symlink():
        raise RuntimeError("Already attempted an Issue today; no retry")
    if issue_file.exists() or issue_file.is_symlink():
        raise RuntimeError(f"Issue #{issue_number} was previously attempted; no retry")
    create_private(day_file, json.dumps({
        "dateJst": date, "issue": issue_number, "status": "started"
    }) + "\n")
    create_private(issue_file, json.dumps({
        "issue": issue_number, "dateJst": date, "status": "attempted"
    }) + "\n")


def invoke_once(repo_path: Path, issue_number: int, log_path: Path) -> int:
    # Private local log; never automatically upload CLI output to GitHub.
    flags = os.O_CREAT | os.O_EXCL | os.O_WRONLY | getattr(os, "O_NOFOLLOW", 0)
    fd = os.open(log_path, flags, 0o600)
    with os.fdopen(fd, "w", encoding="utf-8") as stream:
        stream.write(f"Local AutoDev unattended Issue #{issue_number}\n")
        stream.flush()
        result = subprocess.run(
            [sys.executable, str(repo_path / "tools" / "local_autodev.py"),
             "--repo-path", str(repo_path), "--issue", str(issue_number),
             "--execute"],
            cwd=repo_path, stdout=stream, stderr=subprocess.STDOUT,
            timeout=2100, check=False)
    return result.returncode


def run_daily(repo_path: Path, state: Path, *, execute: bool,
              acknowledge_credit_risk: bool, max_issues: int,
              date: str) -> str:
    if max_issues != 1:
        raise ValueError("Hard limit is one Issue per day and per invocation")
    if execute and not acknowledge_credit_risk:
        raise ValueError("Unattended execution requires --acknowledge-credit-risk")
    verify_checkout(repo_path)
    if not execute:
        # No state writes and no model work in dry-run mode.
        prs = open_pr_numbers(repo_path)
        if prs:
            return f"DRY RUN: {len(prs)} open PR(s); would stop"
        candidates = queue.list_issues(repo_path)
        if not candidates:
            return "DRY RUN: queue empty"
        number = candidates[0]["number"]
        if ((state / f"day-{date}.json").exists() or
                (state / f"issue-{number}.json").exists()):
            return "DRY RUN: attempt already recorded; would stop"
        return f"DRY RUN: would invoke only Issue #{number} after explicit opt-in"

    ensure_private_state(state)
    # Separate daily lock protects the whole preflight and the entire model call.
    lock_path = state / "daily.lock"
    lock_fd = os.open(lock_path, os.O_CREAT | os.O_RDWR |
                      getattr(os, "O_NOFOLLOW", 0), 0o600)
    try:
        if not stat.S_ISREG(os.fstat(lock_fd).st_mode):
            raise RuntimeError("Invalid daily lock file")
        try:
            fcntl.flock(lock_fd, fcntl.LOCK_EX | fcntl.LOCK_NB)
        except BlockingIOError:
            return "Skipped: another daily runner is active"
        if (state / f"day-{date}.json").exists():
            return "Skipped: daily attempt already recorded"
        prs = open_pr_numbers(repo_path)
        if prs:
            return f"Skipped: {len(prs)} open PR(s) await review/merge"
        candidates = queue.list_issues(repo_path)
        if not candidates:
            return "Skipped: no approved open Issues"
        number = candidates[0]["number"]
        if (state / f"issue-{number}.json").exists():
            return f"Skipped: Issue #{number} was already attempted; manual review required"
        # A PR could appear during preflight; check once more before reservation.
        if open_pr_numbers(repo_path):
            return "Skipped: a PR appeared during preflight"
        reserve_attempt(state, date, number)
        print(f"Reserved Japan-local day {date} for Issue #{number}. "
              "One ChatGPT-plan Codex invocation; purchased credits may be consumed.",
              flush=True)
        try:
            code = invoke_once(repo_path, number, state / f"log-{date}.txt")
        except (OSError, subprocess.TimeoutExpired) as error:
            # Day and Issue remain reserved on any error (including uncertainty).
            create_private(state / f"result-{date}.json", json.dumps({
                "dateJst": date, "issue": number,
                "status": "failed", "failureType": type(error).__name__
            }) + "\n")
            raise RuntimeError("One-shot runner failed; no retry permitted") from None
        create_private(state / f"result-{date}.json", json.dumps({
            "dateJst": date, "issue": number, "status": "succeeded" if code == 0 else "failed",
            "exitCode": code
        }) + "\n")
        if code != 0:
            raise RuntimeError(f"Issue #{number} runner failed (exit={code}); "
                               "no automatic retry")
        return f"One Issue #{number} completed; review Draft PR and CI before next run"
    finally:
        os.close(lock_fd)


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--repo-path", type=Path, default=DEFAULT_PATH)
    parser.add_argument("--execute", action="store_true")
    parser.add_argument("--acknowledge-credit-risk", action="store_true")
    parser.add_argument("--max-issues", type=int, default=1)
    args = parser.parse_args()
    try:
        today = datetime.now(JST).date().isoformat()
        result = run_daily(
            args.repo_path.expanduser().resolve(strict=True), STATE,
            execute=args.execute, acknowledge_credit_risk=args.acknowledge_credit_risk,
            max_issues=args.max_issues, date=today)
        print(result, flush=True)
        return 0
    except (OSError, ValueError, RuntimeError, subprocess.TimeoutExpired,
            json.JSONDecodeError) as error:
        print(f"Stopped: {error}", file=sys.stderr)
        return 1


if __name__ == "__main__":
    raise SystemExit(main())
