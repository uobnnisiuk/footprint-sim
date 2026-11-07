#!/usr/bin/env python3
"""Fixed K8 hourly observation and one preallocated GitHub result slot.

This module does not run a model, execute Issue text, create a task/comment,
repair state, or change the caller's exit status. It is intentionally tied to
one deployed checkout and one operator-owned Issue/comment.
"""
from __future__ import annotations

from datetime import datetime, timezone
import fcntl
import json
import os
from pathlib import Path
import re
import secrets
import stat
import subprocess

REPO = "uobnnisiuk/footprint-sim"
OWNER = "uobnnisiuk"
OWNER_ID = 12780770
ISSUE_NUMBER = 88
COMMENT_ID = 6700340146
ISSUE_ENDPOINT = f"repos/{REPO}/issues/{ISSUE_NUMBER}"
COMMENT_ENDPOINT = f"repos/{REPO}/issues/comments/{COMMENT_ID}"
ENABLED_MARKER = "K8-Status-Enabled: runtime-status-v1"
COMMENT_MARKER = "<!-- K8-RUNTIME-STATUS-V1: managed-result-slot -->"
EXPECTED_REPO = Path("/home/higuchi/personal/me/footprint-sim/footprint-sim-local-autodev-pr6")
EXPECTED_STATE = Path.home() / ".local/state/footprint-local-autodev/daily"
F22_POLL = Path.home() / ".local/state/footprint-f22-bridge/last-poll.log"
POWERSHELL = "/mnt/c/Windows/System32/WindowsPowerShell/v1.0/powershell.exe"
MAX_JSON_BYTES = 262144
MAX_STATE_BYTES = 4096
MAX_LOG_BYTES = 65536
COMMAND_TIMEOUT = 15
RUNTIME_UPDATED = "Skipped: runtime updated; next scheduled poll must load the reviewed version"
OUTCOMES = frozenset({
    "empty_queue", "open_pr", "daily_lock_busy", "budget_blocked",
    "candidate_runner_succeeded", "hourly_failed", "unknown",
})
RECORD_KEYS = frozenset({
    "schema_version", "kind", "started_at_utc", "hourly_returned_at_utc",
    "loaded_revision", "exit_code", "outcome",
})
WINDOWS_SCRIPT = r"""
$ErrorActionPreference = 'Stop'
[Console]::OutputEncoding = [System.Text.UTF8Encoding]::new($false)
function Utc($value) {
    if ($null -eq $value -or $value.Year -le 1900) { return $null }
    return $value.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ")
}
$task = Get-ScheduledTask -TaskName 'Footprint Local AutoDev Daily' -TaskPath '\'
$info = Get-ScheduledTaskInfo -TaskName 'Footprint Local AutoDev Daily' -TaskPath '\'
[ordered]@{
    state = $task.State.ToString()
    last_run_utc = Utc $info.LastRunTime
    last_task_result = [long]$info.LastTaskResult
    next_run_utc = Utc $info.NextRunTime
} | ConvertTo-Json -Compress
"""


class Unavailable(Exception):
    """A fixed classification, never a subprocess's raw output."""


def _utc(value: datetime) -> str:
    if not isinstance(value, datetime) or value.tzinfo is None or value.utcoffset() is None:
        raise ValueError("Aware timestamp required")
    return value.astimezone(timezone.utc).isoformat(timespec="seconds").replace("+00:00", "Z")


def _parse_time(value: object) -> datetime:
    if not isinstance(value, str) or not re.fullmatch(r"\d{4}-\d\d-\d\dT\d\d:\d\d:\d\dZ", value):
        raise ValueError("Invalid timestamp")
    return datetime.strptime(value, "%Y-%m-%dT%H:%M:%SZ").replace(tzinfo=timezone.utc)


def classify_result(result: str | None, exit_code: int) -> str:
    if exit_code != 0:
        return "hourly_failed"
    fixed = {
        "Skipped: no approved unattempted Issue": "empty_queue",
        "Skipped: open PR(s) await review/merge": "open_pr",
        "Skipped: a PR appeared during preflight": "open_pr",
        "Skipped: another daily runner is active": "daily_lock_busy",
    }
    if not isinstance(result, str):
        return "unknown"
    if result in fixed:
        return fixed[result]
    if re.fullmatch(
            r"Skipped: (?:legacy daily reservation|hour already reserved|"
            r"legacy execution window already reserved|daily attempt cap reached|"
            r"repair operation already reserved this hour|shared daily reservation cap reached)"
            r"; attempts \d{1,5}/8", result):
        return "budget_blocked"
    if re.fullmatch(r"One Issue #[1-9]\d{0,9} completed; review Draft PR and CI before next run", result):
        return "candidate_runner_succeeded"
    return "unknown"


def _object(data: bytes) -> dict:
    def unique(pairs):
        result = {}
        for key, value in pairs:
            if key in result:
                raise ValueError("Duplicate JSON field")
            result[key] = value
        return result

    def invalid(_):
        raise ValueError("Non-JSON constant")

    value = json.loads(data.decode("utf-8"), object_pairs_hook=unique, parse_constant=invalid)
    if not isinstance(value, dict):
        raise ValueError("Expected object")
    return value


def _directory(path: Path, *, owned: bool = True) -> int:
    """Traverse with directory descriptors; reject symlinks in every component."""
    if not path.is_absolute() or ".." in path.parts:
        raise ValueError("Expected absolute fixed path")
    flags = os.O_RDONLY | os.O_DIRECTORY | os.O_NOFOLLOW
    fd = os.open("/", flags)
    try:
        for part in path.parts[1:]:
            next_fd = os.open(part, flags, dir_fd=fd)
            os.close(fd)
            fd = next_fd
        if owned and os.fstat(fd).st_uid != os.getuid():
            raise ValueError("Unexpected directory owner")
        return fd
    except BaseException:
        os.close(fd)
        raise


def _regular(info: os.stat_result, *, private: bool = False) -> None:
    if (not stat.S_ISREG(info.st_mode) or info.st_uid != os.getuid()
            or info.st_nlink != 1 or private and stat.S_IMODE(info.st_mode) != 0o600):
        raise ValueError("Unsafe file")


def _read_at(directory: int, name: str, limit: int, *, private: bool = False) -> tuple[bytes, os.stat_result]:
    fd = os.open(name, os.O_RDONLY | os.O_NONBLOCK | os.O_NOFOLLOW, dir_fd=directory)
    try:
        before = os.fstat(fd)
        _regular(before, private=private)
        if before.st_size > limit:
            raise ValueError("Oversized file")
        chunks, remaining = [], limit + 1
        while remaining:
            chunk = os.read(fd, remaining)
            if not chunk:
                break
            chunks.append(chunk)
            remaining -= len(chunk)
        data = b"".join(chunks)
        after = os.fstat(fd)
        if (len(data) > limit or before.st_size != len(data)
                or (before.st_size, before.st_mtime_ns, before.st_ctime_ns)
                != (after.st_size, after.st_mtime_ns, after.st_ctime_ns)):
            raise ValueError("Changing or oversized file")
        return data, after
    finally:
        os.close(fd)


def _record(value: dict) -> dict:
    if (set(value) != RECORD_KEYS or type(value["schema_version"]) is not int
            or value["schema_version"] != 1 or value["kind"] != "hourly_return"
            or type(value["exit_code"]) is not int or not 0 <= value["exit_code"] <= 255
            or not isinstance(value["outcome"], str) or value["outcome"] not in OUTCOMES
            or value["loaded_revision"] is not None
            and (not isinstance(value["loaded_revision"], str)
                 or not re.fullmatch(r"[a-f0-9]{40}", value["loaded_revision"]))):
        raise ValueError("Invalid status record")
    if _parse_time(value["started_at_utc"]) > _parse_time(value["hourly_returned_at_utc"]):
        raise ValueError("Inconsistent status timestamps")
    if (value["exit_code"] != 0) != (value["outcome"] == "hourly_failed"):
        raise ValueError("Inconsistent status outcome")
    return value


def _save_record(directory: int, value: dict) -> None:
    data = (json.dumps(_record(value), sort_keys=True, separators=(",", ":")) + "\n").encode("utf-8")
    name = "pending-" + secrets.token_hex(8) + ".tmp"
    fd = os.open(name, os.O_CREAT | os.O_EXCL | os.O_WRONLY | os.O_NOFOLLOW, 0o600, dir_fd=directory)
    try:
        _regular(os.fstat(fd), private=True)
        with os.fdopen(fd, "wb", closefd=False) as stream:
            stream.write(data)
            stream.flush()
            os.fsync(fd)
        os.replace(name, "last-poll.json", src_dir_fd=directory, dst_dir_fd=directory)
        os.fsync(directory)
    finally:
        os.close(fd)
        try:
            os.unlink(name, dir_fd=directory)
        except FileNotFoundError:
            pass


def _command_json(argv: list[str], repo: Path, payload: dict | None = None) -> dict:
    # Existing gh authentication is reused, never discovered, printed or stored.
    env = dict(os.environ)
    env.pop("GH_DEBUG", None)
    env.update(GH_PROMPT_DISABLED="1", GH_NO_UPDATE_NOTIFIER="1", NO_COLOR="1")
    data = None if payload is None else json.dumps(payload, ensure_ascii=True).encode("utf-8")
    try:
        completed = subprocess.run(argv, cwd=repo, input=data, stdout=subprocess.PIPE,
                                   stderr=subprocess.DEVNULL, timeout=COMMAND_TIMEOUT,
                                   check=False, env=env)
        if completed.returncode or len(completed.stdout) > MAX_JSON_BYTES:
            raise Unavailable("command_unavailable")
        return _object(completed.stdout)
    except (OSError, subprocess.TimeoutExpired, ValueError, RecursionError):
        raise Unavailable("command_unavailable") from None


def _github(repo: Path, method: str, endpoint: str, payload: dict | None = None) -> dict:
    if (method == "GET" and endpoint in (ISSUE_ENDPOINT, COMMENT_ENDPOINT) and payload is None):
        pass
    elif (method == "PATCH" and endpoint == COMMENT_ENDPOINT and isinstance(payload, dict)
          and set(payload) == {"body"} and isinstance(payload["body"], str)
          and payload["body"].startswith(COMMENT_MARKER + "\n")
          and len(payload["body"].encode("utf-8")) <= 16384):
        pass
    else:
        raise ValueError("Unsupported fixed status request")
    argv = ["gh", "api", "--hostname", "github.com", "--method", method, endpoint]
    if payload is not None:
        argv.extend(["--input", "-"])
    return _command_json(argv, repo, payload)


def _owner(value: object) -> bool:
    return (isinstance(value, dict) and value.get("login") == OWNER
            and type(value.get("id")) is int and value["id"] == OWNER_ID)


def _slot(value: dict) -> bool:
    return (type(value.get("id")) is int and value["id"] == COMMENT_ID
            and value.get("issue_url") == f"https://api.github.com/{ISSUE_ENDPOINT}"
            and _owner(value.get("user")) and isinstance(value.get("body"), str)
            and value["body"].startswith(COMMENT_MARKER + "\n"))


def _target(repo: Path) -> dict | None:
    issue = _github(repo, "GET", ISSUE_ENDPOINT)
    if (type(issue.get("number")) is not int or issue["number"] != ISSUE_NUMBER
            or issue.get("html_url") != f"https://github.com/{REPO}/issues/{ISSUE_NUMBER}"
            or issue.get("state") != "open" or not _owner(issue.get("user"))
            or "pull_request" in issue or not isinstance(issue.get("body"), str)
            or issue["body"].splitlines().count(ENABLED_MARKER) != 1):
        return None
    comment = _github(repo, "GET", COMMENT_ENDPOINT)
    return comment if _slot(comment) else None


def _windows(repo: Path) -> dict:
    try:
        value = _command_json(
            [POWERSHELL, "-NoLogo", "-NoProfile", "-NonInteractive", "-Command", WINDOWS_SCRIPT], repo)
        if (set(value) != {"state", "last_run_utc", "last_task_result", "next_run_utc"}
                or value["state"] not in ("Unknown", "Disabled", "Queued", "Ready", "Running")
                or type(value["last_task_result"]) is not int
                or not 0 <= value["last_task_result"] <= 2**32 - 1):
            raise ValueError("Invalid task observation")
        for name in ("last_run_utc", "next_run_utc"):
            if value[name] is not None:
                _parse_time(value[name])
        return {"status": "observed", **value, "current_task_completion": "unverified"}
    except (Unavailable, ValueError, TypeError):
        return {"status": "unavailable", "current_task_completion": "unverified"}


def _f22() -> dict:
    directory = None
    try:
        directory = _directory(F22_POLL.parent)
        data, info = _read_at(directory, F22_POLL.name, MAX_LOG_BYTES)
        marker = ("already_published" if re.search(
            rb"(?<![A-Za-z0-9_-])already-published(?![A-Za-z0-9_-])", data) else "unknown")
        return {"status": "observed", "marker": marker,
                "file_modified_at_utc": _utc(datetime.fromtimestamp(info.st_mtime, timezone.utc)),
                "same_poll_correlation": "unverified"}
    except FileNotFoundError:
        return {"status": "missing", "marker": "unknown"}
    except (OSError, ValueError, OverflowError):
        return {"status": "unavailable", "marker": "unknown"}
    finally:
        if directory is not None:
            os.close(directory)


def _lock(path: Path) -> str:
    directory = fd = None
    try:
        directory = _directory(path.parent)
        fd = os.open(path.name, os.O_RDONLY | os.O_NONBLOCK | os.O_NOFOLLOW, dir_fd=directory)
        _regular(os.fstat(fd))
        try:
            fcntl.flock(fd, fcntl.LOCK_EX | fcntl.LOCK_NB)
        except BlockingIOError:
            return "busy"
        fcntl.flock(fd, fcntl.LOCK_UN)
        return "available_at_observation"
    except FileNotFoundError:
        return "missing"
    except (OSError, ValueError):
        return "unavailable"
    finally:
        if fd is not None:
            os.close(fd)
        if directory is not None:
            os.close(directory)


def _publish(repo: Path, document: dict) -> str:
    fence = chr(96) * 3
    body = (COMMENT_MARKER + "\n## K8 hourlyからの運用観測\n\n"
            "この記録はhourly制御処理が戻った後の観測です。Windowsタスク全体の終了・"
            "自然起動の出所・長期安定性は、この報告だけでは確定しません。"
            "F22は既存ログの限定marker、lockは個別時点の観測です。\n\n"
            + fence + "json\n" + json.dumps(document, ensure_ascii=True, sort_keys=True, indent=2)
            + "\n" + fence + "\n")
    target = _target(repo)  # Recheck the activation and destination immediately before writing.
    if target is None:
        return "disabled"
    if target["body"] == body:
        return "published"
    try:
        _github(repo, "PATCH", COMMENT_ENDPOINT, {"body": body})
    except Unavailable:
        pass  # A timeout may follow a committed PATCH. Read this same slot; never POST/retry blindly.
    try:
        observed = _github(repo, "GET", COMMENT_ENDPOINT)
        return "published" if _slot(observed) and observed["body"] == body else "publish_unconfirmed"
    except Unavailable:
        return "publish_unconfirmed"


def report_poll(repo: Path, state: Path, result: str | None, exit_code: int,
                started_at: datetime, loaded_revision: str | None) -> str:
    """Observe only the fixed deployment; caller must preserve its original exit."""
    if repo != EXPECTED_REPO or state != EXPECTED_STATE:
        return "scope_mismatch"
    if result == RUNTIME_UPDATED:
        return "runtime_updated"
    parent = directory = lock_fd = None
    try:
        repo_fd = _directory(repo)
        os.close(repo_fd)
        source_directory = _directory(repo / "tools")
        os.close(source_directory)
        try:
            if Path(__file__).resolve(strict=True) != repo / "tools/local_autodev_status.py":
                return "scope_mismatch"
        except (OSError, RuntimeError):
            return "scope_mismatch"
        parent = _directory(state.parent)
        # Missing daily state is observable, but a symlink/non-directory is never followed.
        try:
            state_fd = _directory(state)
            os.close(state_fd)
        except FileNotFoundError:
            pass
        current = _record({
            "schema_version": 1, "kind": "hourly_return", "started_at_utc": _utc(started_at),
            "hourly_returned_at_utc": _utc(datetime.now(timezone.utc)),
            "loaded_revision": loaded_revision, "exit_code": exit_code,
            "outcome": classify_result(result, exit_code),
        })
        if _target(repo) is None:
            return "disabled"
        try:
            os.mkdir("runtime-status", 0o700, dir_fd=parent)
        except FileExistsError:
            pass
        directory = os.open("runtime-status", os.O_RDONLY | os.O_DIRECTORY | os.O_NOFOLLOW, dir_fd=parent)
        info = os.fstat(directory)
        if info.st_uid != os.getuid() or stat.S_IMODE(info.st_mode) != 0o700:
            return "state_invalid"
        lock_fd = os.open("status.lock", os.O_CREAT | os.O_RDWR | os.O_NONBLOCK | os.O_NOFOLLOW,
                          0o600, dir_fd=directory)
        _regular(os.fstat(lock_fd), private=True)
        try:
            fcntl.flock(lock_fd, fcntl.LOCK_EX | fcntl.LOCK_NB)
        except BlockingIOError:
            return "report_busy"
        try:
            previous = _record(_object(_read_at(directory, "last-poll.json", MAX_STATE_BYTES, private=True)[0]))
        except FileNotFoundError:
            previous = None
        except (OSError, ValueError, RecursionError):
            return "state_invalid"  # Preserve suspicious state rather than resetting it.
        _save_record(directory, current)
        document = {
            "schema_version": 1, "kind": "k8_runtime_status_v1",
            "current_hourly": current, "previous_hourly": previous,
            "windows_task": _windows(repo), "f22_poll": _f22(),
            "locks": {"daily": _lock(state / "daily.lock"),
                      "runner": _lock(state.parent / "runner.lock"),
                      "controller": _lock(state.parent / "controller.lock")},
            "trigger_origin": "unverified", "lock_observations_atomic": False,
            "reporter_model_calls": 0,
        }
        return _publish(repo, document)
    except (Unavailable, OSError, ValueError, RecursionError):
        return "unavailable"
    finally:
        for fd in (lock_fd, directory, parent):
            if fd is not None:
                os.close(fd)
