"""Offline contracts for the fixed K8 report slot. No GitHub/Windows/model calls."""
from contextlib import ExitStack
from copy import deepcopy
from datetime import datetime, timedelta, timezone
import fcntl
import json
import os
from pathlib import Path
import stat
import subprocess
import sys
import tempfile
import unittest
from unittest.mock import patch

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / "tools"))
import local_autodev_status as status


REVISION = "a" * 40
SECRET = "DO_NOT_PUBLISH_this_secret_or_raw_log"


def record():
    return {"schema_version": 1, "kind": "hourly_return",
            "started_at_utc": "2026-11-06T00:00:00Z",
            "hourly_returned_at_utc": "2026-11-06T00:01:00Z",
            "loaded_revision": REVISION, "exit_code": 0, "outcome": "empty_queue"}


def task(state="Running"):
    return {"state": state, "last_run_utc": "2026-11-06T00:00:00Z",
            "last_task_result": 267009, "next_run_utc": "2026-11-06T01:00:00Z"}


class StatusTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.base = Path(self.temp.name)
        self.repo = self.base / "repo"
        self.repo.mkdir()
        self.module_source = self.repo / "tools/local_autodev_status.py"
        self.module_source.parent.mkdir()
        self.module_source.write_text("# Offline provenance fixture\n")
        self.state = self.base / "state/daily"
        self.state.mkdir(parents=True)
        self.private = self.state.parent / "runtime-status"
        self.log = self.base / "f22/last-poll.log"
        self.log.parent.mkdir()
        self.issue = {"number": 88, "html_url": "https://github.com/uobnnisiuk/footprint-sim/issues/88",
                      "state": "open", "body": "Reviewed fixed diagnostic\n" + status.ENABLED_MARKER,
                      "user": {"login": status.OWNER, "id": status.OWNER_ID}}
        self.comment = {"id": status.COMMENT_ID,
                        "issue_url": "https://api.github.com/" + status.ISSUE_ENDPOINT,
                        "user": deepcopy(self.issue["user"]),
                        "body": status.COMMENT_MARKER + "\nawaiting_runtime_report"}
        self.calls = []
        self.stack = ExitStack()
        self.addCleanup(self.stack.close)
        for name, value in (("EXPECTED_REPO", self.repo), ("EXPECTED_STATE", self.state),
                            ("F22_POLL", self.log), ("__file__", str(self.module_source))):
            self.stack.enter_context(patch.object(status, name, value))
        self.gh = self.stack.enter_context(patch.object(status, "_github", side_effect=self.github))
        # Any unexpected subprocess is a failing test, never a real external call.
        self.process = self.stack.enter_context(patch.object(status.subprocess, "run",
                                                            side_effect=AssertionError("Unexpected subprocess")))
        self.windows = self.stack.enter_context(patch.object(status, "_windows", return_value={
            "status": "observed", **task(), "current_task_completion": "unverified"}))

    def github(self, repo, method, endpoint, payload=None):
        self.assertEqual(repo, self.repo)
        self.calls.append((method, endpoint, deepcopy(payload)))
        if method == "GET" and endpoint == status.ISSUE_ENDPOINT:
            return deepcopy(self.issue)
        if method == "GET" and endpoint == status.COMMENT_ENDPOINT:
            return deepcopy(self.comment)
        if method == "PATCH" and endpoint == status.COMMENT_ENDPOINT:
            self.assertEqual(set(payload), {"body"})
            self.comment["body"] = payload["body"]
            return deepcopy(self.comment)
        self.fail("Unexpected GitHub operation")

    def poll(self, result="Skipped: no approved unattempted Issue", exit_code=0, **changes):
        values = dict(repo=self.repo, state=self.state, result=result, exit_code=exit_code,
                      started_at=datetime.now(timezone.utc) - timedelta(seconds=2), loaded_revision=REVISION)
        values.update(changes)
        return status.report_poll(**values)

    def published(self):
        return json.loads(self.comment["body"].split("```json\n", 1)[1].split("\n```", 1)[0])

    def private_record(self, data=None, mode=0o600):
        self.private.mkdir(mode=0o700, exist_ok=True)
        path = self.private / "last-poll.json"
        path.write_bytes(json.dumps(record()).encode() if data is None else data)
        path.chmod(mode)
        return path

    def test_report_records_two_hourly_returns_without_touching_existing_state(self):
        legacy = self.state / "day-2026-11-06-hour-9.json"
        legacy.write_text(SECRET)
        lock_paths = [self.state / "daily.lock", self.state.parent / "runner.lock",
                      self.state.parent / "controller.lock"]
        for path in lock_paths:
            path.write_text(SECRET)
        before = {path: (path.read_bytes(), path.stat().st_mtime_ns) for path in [legacy, *lock_paths]}
        self.log.write_text(SECRET + "\nalready-published\n")
        self.assertEqual(self.poll(), "published")
        first = self.published()
        self.assertIsNone(first["previous_hourly"])
        self.assertEqual(first["current_hourly"]["outcome"], "empty_queue")
        self.assertEqual(first["f22_poll"]["marker"], "already_published")
        self.assertEqual(first["f22_poll"]["same_poll_correlation"], "unverified")
        self.assertEqual(first["windows_task"]["current_task_completion"], "unverified")
        self.assertEqual(first["trigger_origin"], "unverified")
        self.assertEqual(first["reporter_model_calls"], 0)
        self.assertEqual(set(first["locks"].values()), {"available_at_observation"})
        self.assertEqual(self.poll("Skipped: open PR(s) await review/merge"), "published")
        second = self.published()
        self.assertEqual(second["previous_hourly"], first["current_hourly"])
        self.assertEqual(second["current_hourly"]["outcome"], "open_pr")
        self.assertEqual(json.loads((self.private / "last-poll.json").read_text()), second["current_hourly"])
        self.assertEqual(stat.S_IMODE(self.private.stat().st_mode), 0o700)
        self.assertEqual(stat.S_IMODE((self.private / "last-poll.json").stat().st_mode), 0o600)
        self.assertEqual(stat.S_IMODE((self.private / "status.lock").stat().st_mode), 0o600)
        self.assertEqual(before, {path: (path.read_bytes(), path.stat().st_mtime_ns) for path in before})
        self.assertNotIn(SECRET, self.comment["body"])
        self.assertEqual(sum(method == "PATCH" for method, _, _ in self.calls), 2)

    def test_failure_and_unknown_are_enums_never_raw_messages(self):
        self.assertEqual(self.poll(SECRET, exit_code=1), "published")
        self.assertEqual(self.published()["current_hourly"]["outcome"], "hourly_failed")
        self.assertEqual(self.poll(SECRET), "published")
        self.assertEqual(self.published()["current_hourly"]["outcome"], "unknown")
        self.assertNotIn(SECRET, self.comment["body"])
        self.assertNotIn(SECRET, (self.private / "last-poll.json").read_text())

    def test_other_clone_other_state_and_mixed_runtime_do_no_io(self):
        with patch.object(status, "_directory", side_effect=AssertionError("Unexpected path access")):
            self.assertEqual(self.poll(repo=self.base), "scope_mismatch")
            self.assertEqual(self.poll(state=self.base), "scope_mismatch")
            self.assertEqual(self.poll(status.RUNTIME_UPDATED), "runtime_updated")
        self.gh.assert_not_called()
        self.windows.assert_not_called()
        self.assertFalse(self.private.exists())

    def test_foreign_actual_module_does_not_access_state_or_github(self):
        foreign_module = ROOT / "tools/local_autodev_status.py"
        with patch.object(status, "__file__", str(foreign_module)):
            with patch.object(status, "_directory", wraps=status._directory) as directory:
                self.assertEqual(self.poll(), "scope_mismatch")
        self.assertEqual([call.args[0] for call in directory.call_args_list],
                         [self.repo, self.repo / "tools"])
        self.gh.assert_not_called()
        self.windows.assert_not_called()
        self.assertFalse(self.private.exists())

    def test_symlinked_deployed_source_cannot_claim_provenance(self):
        self.module_source.unlink()
        self.module_source.symlink_to(ROOT / "tools/local_autodev_status.py")
        self.assertEqual(self.poll(), "scope_mismatch")
        self.gh.assert_not_called()
        self.windows.assert_not_called()
        self.assertFalse(self.private.exists())

    def test_disabled_or_foreign_issue_and_slot_do_not_sample_or_save(self):
        original_issue, original_comment = deepcopy(self.issue), deepcopy(self.comment)
        variants = [({"state": "closed"}, {}), ({"body": "not enabled"}, {}),
                    ({"body": status.ENABLED_MARKER + "\n" + status.ENABLED_MARKER}, {}),
                    ({"number": 89}, {}), ({"user": {"login": "other", "id": 1}}, {}),
                    ({"pull_request": {}}, {}), ({}, {"id": 1}),
                    ({}, {"body": "prefix " + status.COMMENT_MARKER + "\n"}),
                    ({}, {"issue_url": "https://api.github.com/repos/other/repo/issues/88"})]
        for issue_change, slot_change in variants:
            with self.subTest(issue=issue_change, slot=slot_change):
                self.issue = {**original_issue, **issue_change}
                self.comment = {**original_comment, **slot_change}
                self.assertEqual(self.poll(), "disabled")
        self.windows.assert_not_called()
        self.assertFalse(self.private.exists())
        self.assertFalse(any(method == "PATCH" for method, _, _ in self.calls))

    def test_disabling_during_observation_prevents_patch(self):
        self.windows.side_effect = lambda _: self.issue.update(state="closed") or {"status": "unavailable"}
        self.assertEqual(self.poll(), "disabled")
        self.assertTrue((self.private / "last-poll.json").is_file())
        self.assertFalse(any(method == "PATCH" for method, _, _ in self.calls))

    def test_gh_failure_has_no_raw_exception_output_or_legacy_mutation(self):
        self.gh.side_effect = status.Unavailable(SECRET)
        self.assertEqual(self.poll(), "unavailable")
        self.assertFalse(self.private.exists())
        self.windows.assert_not_called()

    def test_patch_timeout_uses_same_slot_readback_without_retry(self):
        def timed_out(repo, method, endpoint, payload=None):
            result = self.github(repo, method, endpoint, payload)
            if method == "PATCH":
                raise status.Unavailable(SECRET)
            return result
        self.gh.side_effect = timed_out
        self.assertEqual(self.poll(), "published")
        self.assertEqual(sum(method == "PATCH" for method, _, _ in self.calls), 1)
        self.assertEqual(self.calls[-1][:2], ("GET", status.COMMENT_ENDPOINT))

    def test_uncommitted_patch_timeout_does_not_claim_published(self):
        def timed_out(repo, method, endpoint, payload=None):
            if method == "PATCH":
                self.calls.append((method, endpoint, payload))
                raise status.Unavailable(SECRET)
            return self.github(repo, method, endpoint, payload)
        self.gh.side_effect = timed_out
        self.assertEqual(self.poll(), "publish_unconfirmed")
        self.assertEqual(sum(method == "PATCH" for method, _, _ in self.calls), 1)

    def test_unsafe_private_record_is_preserved_and_never_published(self):
        examples = [b"not json", b'{"schema_version":1,"schema_version":1}',
                    b"x" * (status.MAX_STATE_BYTES + 1),
                    json.dumps({**record(), "extra": SECRET}).encode(),
                    json.dumps({**record(), "outcome": SECRET}).encode(),
                    json.dumps({**record(), "exit_code": True}).encode(),
                    json.dumps({**record(), "loaded_revision": SECRET}).encode(),
                    json.dumps({**record(), "started_at_utc": "2026-11-07T00:00:00Z"}).encode()]
        for data in examples:
            with self.subTest(data=data[:90]):
                path = self.private_record(data)
                self.assertEqual(self.poll(), "state_invalid")
                self.assertEqual(path.read_bytes(), data)
        path = self.private_record(mode=0o644)
        self.assertEqual(self.poll(), "state_invalid")
        self.assertEqual(stat.S_IMODE(path.stat().st_mode), 0o644)
        self.assertFalse(any(method == "PATCH" for method, _, _ in self.calls))

    def test_private_record_symlink_and_hardlink_are_preserved(self):
        path = self.private_record()
        target = self.base / "outside.json"
        path.rename(target)
        path.symlink_to(target)
        original = target.read_bytes()
        self.assertEqual(self.poll(), "state_invalid")
        self.assertTrue(path.is_symlink())
        path.unlink()
        os.link(target, path)
        self.assertEqual(self.poll(), "state_invalid")
        self.assertEqual(target.read_bytes(), original)
        self.assertFalse(any(method == "PATCH" for method, _, _ in self.calls))

    def test_symlinked_private_directory_and_insecure_mode_fail_closed(self):
        outside = self.base / "outside"
        outside.mkdir(mode=0o700)
        self.private.symlink_to(outside, target_is_directory=True)
        self.assertEqual(self.poll(), "unavailable")
        self.assertEqual(list(outside.iterdir()), [])
        self.private.unlink()
        self.private.mkdir(mode=0o755)
        self.assertEqual(self.poll(), "state_invalid")
        self.assertFalse((self.private / "last-poll.json").exists())

    def test_symlinked_deployment_or_state_is_rejected_before_remote_access(self):
        moved = self.base / "moved"
        self.repo.rename(moved)
        self.repo.symlink_to(moved, target_is_directory=True)
        self.assertEqual(self.poll(), "unavailable")
        self.repo.unlink()
        moved.rename(self.repo)
        self.state.rmdir()
        self.state.symlink_to(self.repo, target_is_directory=True)
        self.assertEqual(self.poll(), "unavailable")
        self.gh.assert_not_called()

    def test_private_lock_busy_does_not_save_or_publish(self):
        self.private.mkdir(mode=0o700)
        fd = os.open(self.private / "status.lock", os.O_CREAT | os.O_RDWR, 0o600)
        try:
            fcntl.flock(fd, fcntl.LOCK_EX | fcntl.LOCK_NB)
            self.assertEqual(self.poll(), "report_busy")
            self.assertFalse((self.private / "last-poll.json").exists())
        finally:
            os.close(fd)
        self.assertFalse(any(method == "PATCH" for method, _, _ in self.calls))

    def test_atomic_write_failure_preserves_previous_and_cleans_pending_file(self):
        path = self.private_record()
        original = path.read_bytes()
        with patch.object(status.os, "replace", side_effect=OSError(SECRET)):
            self.assertEqual(self.poll(), "unavailable")
        self.assertEqual(path.read_bytes(), original)
        self.assertEqual(list(self.private.glob("pending-*")), [])
        self.assertFalse(any(method == "PATCH" for method, _, _ in self.calls))

    def test_missing_daily_directory_is_not_created(self):
        self.state.rmdir()
        self.assertEqual(self.poll(), "published")
        self.assertFalse(self.state.exists())
        self.assertEqual(self.published()["locks"]["daily"], "missing")

    def test_bad_revision_and_naive_timestamp_cannot_be_published(self):
        self.assertEqual(self.poll(loaded_revision=SECRET), "unavailable")
        self.assertEqual(self.poll(started_at=datetime(2026, 11, 6)), "unavailable")
        self.gh.assert_not_called()
        self.assertFalse(self.private.exists())

    def test_existing_lock_probe_is_nonblocking_never_reads_or_creates(self):
        path = self.state / "daily.lock"
        self.assertEqual(status._lock(path), "missing")
        self.assertFalse(path.exists())
        path.write_text(SECRET)
        with patch.object(status.os, "read", side_effect=AssertionError("Lock contents read")):
            self.assertEqual(status._lock(path), "available_at_observation")
            with path.open("rb") as stream:
                fcntl.flock(stream, fcntl.LOCK_EX | fcntl.LOCK_NB)
                self.assertEqual(status._lock(path), "busy")
        self.assertEqual(path.read_text(), SECRET)
        link = self.state / "link.lock"
        link.symlink_to(path)
        self.assertEqual(status._lock(link), "unavailable")
        link.unlink()
        os.link(path, link)
        self.assertEqual(status._lock(path), "unavailable")

    def test_f22_only_returns_exact_bounded_marker_and_never_raw_text(self):
        self.assertEqual(status._f22(), {"status": "missing", "marker": "unknown"})
        for data, expected in [(SECRET.encode(), "unknown"), (b"not-already-published", "unknown"),
                               (b"already-published-more", "unknown"),
                               (b"result: already-published\n" + SECRET.encode(), "already_published")]:
            self.log.write_bytes(data)
            observed = status._f22()
            self.assertEqual(observed["marker"], expected)
            self.assertNotIn(SECRET, json.dumps(observed))
            self.assertEqual(observed["same_poll_correlation"], "unverified")
        self.log.write_bytes(b"x" * (status.MAX_LOG_BYTES + 1))
        self.assertEqual(status._f22()["status"], "unavailable")
        self.log.unlink()
        self.log.symlink_to(self.base / "absent")
        self.assertEqual(status._f22()["status"], "unavailable")


class BoundaryTests(unittest.TestCase):
    def test_result_classification_is_an_explicit_allowlist(self):
        values = [(None, 0, "unknown"), (SECRET, 0, "unknown"), (SECRET, 1, "hourly_failed"),
                  ("Skipped: another daily runner is active", 0, "daily_lock_busy"),
                  ("Skipped: a PR appeared during preflight", 0, "open_pr"),
                  ("Skipped: shared daily reservation cap reached; attempts 8/8", 0, "budget_blocked"),
                  ("Skipped: hour already reserved; attempts 1/8", 0, "budget_blocked"),
                  ("One Issue #67 completed; review Draft PR and CI before next run", 0, "candidate_runner_succeeded")]
        for result, code, expected in values:
            self.assertEqual(status.classify_result(result, code), expected)

    def test_github_adapter_allows_only_fixed_get_patch(self):
        repo = Path("/offline")
        body = status.COMMENT_MARKER + "\nfixed report"
        with patch.object(status, "_command_json", return_value={}) as command:
            status._github(repo, "GET", status.ISSUE_ENDPOINT)
            status._github(repo, "GET", status.COMMENT_ENDPOINT)
            status._github(repo, "PATCH", status.COMMENT_ENDPOINT, {"body": body})
            self.assertEqual(command.call_count, 3)
            self.assertEqual(command.call_args.args[0], ["gh", "api", "--hostname", "github.com",
                             "--method", "PATCH", status.COMMENT_ENDPOINT, "--input", "-"])
            invalid = [("POST", status.COMMENT_ENDPOINT, {"body": body}),
                       ("PATCH", status.ISSUE_ENDPOINT, {"body": body}),
                       ("GET", "repos/other/repo/issues/88", None),
                       ("GET", status.ISSUE_ENDPOINT, {}),
                       ("PATCH", status.COMMENT_ENDPOINT, {"body": body, "extra": SECRET}),
                       ("PATCH", status.COMMENT_ENDPOINT, {"body": SECRET}),
                       ("PATCH", status.COMMENT_ENDPOINT, {"body": body + "x" * 16384})]
            for method, endpoint, payload in invalid:
                with self.assertRaises(ValueError):
                    status._github(repo, method, endpoint, payload)
            self.assertEqual(command.call_count, 3)

    def test_subprocess_timeout_stderr_and_output_are_bounded_and_never_surface_secrets(self):
        result = subprocess.CompletedProcess(["mock"], 0, stdout=b'{"ok":true}')
        with patch.object(status.subprocess, "run", return_value=result) as run:
            with patch.dict(os.environ, {"GH_DEBUG": "api", "GH_TOKEN": SECRET}):
                self.assertEqual(status._command_json(["mock"], ROOT, {"body": "fixed"}), {"ok": True})
            options = run.call_args.kwargs
            self.assertEqual(options["timeout"], status.COMMAND_TIMEOUT)
            self.assertEqual(options["stderr"], subprocess.DEVNULL)
            self.assertEqual(options["env"]["GH_TOKEN"], SECRET)  # Existing auth stays process-local.
            self.assertNotIn("GH_DEBUG", options["env"])
            self.assertNotIn(SECRET.encode(), options["input"])
            for output in (b"not json", b'{"x":1,"x":2}', b'{"x":NaN}', b"[]",
                           b"x" * (status.MAX_JSON_BYTES + 1)):
                run.return_value = subprocess.CompletedProcess(["mock"], 0, stdout=output)
                with self.assertRaises(status.Unavailable) as raised:
                    status._command_json(["mock"], ROOT)
                self.assertNotIn(SECRET, str(raised.exception))
            run.side_effect = subprocess.TimeoutExpired([SECRET], status.COMMAND_TIMEOUT, output=SECRET)
            with self.assertRaises(status.Unavailable) as raised:
                status._command_json(["mock"], ROOT)
            self.assertEqual(str(raised.exception), "command_unavailable")

    def test_windows_observation_has_only_four_allowed_values_and_cannot_prove_completion(self):
        with patch.object(status, "_command_json", return_value=task()) as command:
            value = status._windows(ROOT)
            self.assertEqual(value["state"], "Running")
            self.assertEqual(value["current_task_completion"], "unverified")
            argv = command.call_args.args[0]
            self.assertEqual(argv[0], status.POWERSHELL)
            self.assertIn("Footprint Local AutoDev Daily", argv[-1])
            self.assertNotIn("Start-ScheduledTask", argv[-1])
            variants = [{**task(), "extra": SECRET}, {**task(), "state": SECRET},
                        {**task(), "last_task_result": True}, {**task(), "last_task_result": -1},
                        {**task(), "last_run_utc": SECRET}, {**task(), "next_run_utc": []}]
            for invalid in variants:
                command.return_value = invalid
                self.assertEqual(status._windows(ROOT), {"status": "unavailable", "current_task_completion": "unverified"})
            command.side_effect = status.Unavailable(SECRET)
            self.assertEqual(status._windows(ROOT)["status"], "unavailable")


if __name__ == "__main__":
    unittest.main()
