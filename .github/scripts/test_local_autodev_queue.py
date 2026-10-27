"""Offline queue-selection tests: never invoke Codex or remote GitHub."""
import importlib.util
import json
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch

REPO = Path(__file__).resolve().parents[2]
SPEC = importlib.util.spec_from_file_location("footprint_local_autodev_queue", REPO / "tools" / "local_autodev_queue.py")
assert SPEC and SPEC.loader
queue_mod = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(queue_mod)


def issue(number, *, author="uobnnisiuk", labels=("local-autodev",),
          state="OPEN"):
    return {"number": number, "title": f"Task #{number}", "state": state,
            "author": {"login": author},
            "labels": [{"name": name} for name in labels]}


class QueueTests(unittest.TestCase):
    def test_owner_only_and_separate_labels(self):
        candidates = [
            issue(5), issue(6, author="other"), issue(7, labels=("autodev",)),
            issue(8, labels=("local-autodev", "autodev")),
            issue(9, state="CLOSED"), issue(3),
        ]
        self.assertEqual([i["number"] for i in queue_mod.eligible_queue(candidates)], [3, 5])

    def test_malformed_entries_and_no_labels_do_not_authorize(self):
        values = [None, {}, {"number": True, "author": {"login": "uobnnisiuk"},
                             "labels": [{"name": "local-autodev"}]},
                  issue(4, labels=())]
        self.assertEqual(queue_mod.eligible_queue(values), [])

    def test_duplicate_issue_rejected(self):
        with self.assertRaisesRegex(ValueError, "Duplicate"):
            queue_mod.eligible_queue([issue(3), issue(3)])

    def test_invalid_source_rejected(self):
        with self.assertRaisesRegex(ValueError, "Unexpected"):
            queue_mod.eligible_queue({"issues": [issue(3)]})

    def test_dry_run_does_not_run_model_or_write(self):
        with patch.object(queue_mod, "run_one") as run:
            queue_mod.execute_queue(REPO, [issue(3)], execute=False,
                                    acknowledge_credit_risk=False, max_issues=1)
            run.assert_not_called()

    def test_explicit_authorization_required_before_execution(self):
        with patch.object(queue_mod, "run_one") as run:
            with self.assertRaisesRegex(ValueError, "acknowledge"):
                queue_mod.execute_queue(REPO, [issue(3)], execute=True,
                                        acknowledge_credit_risk=False, max_issues=1)
            run.assert_not_called()

    def test_hard_limit_rejects_multiple_tasks(self):
        with patch.object(queue_mod, "run_one") as run:
            with self.assertRaisesRegex(ValueError, "hard limit"):
                queue_mod.execute_queue(REPO, [issue(3), issue(5)],
                                        execute=True, acknowledge_credit_risk=True,
                                        max_issues=2)
            run.assert_not_called()

    def test_one_run_only_even_with_multiple_issues(self):
        with patch.object(queue_mod, "run_one", return_value=0) as run:
            queue_mod.execute_queue(REPO, [issue(3), issue(5)], execute=True,
                                    acknowledge_credit_risk=True, max_issues=1)
            run.assert_called_once_with(REPO, 3)

    def test_failed_runner_stops_without_second_issue(self):
        with patch.object(queue_mod, "run_one", return_value=1) as run:
            with self.assertRaisesRegex(RuntimeError, "no retry"):
                queue_mod.execute_queue(REPO, [issue(3), issue(5)], execute=True,
                                        acknowledge_credit_risk=True, max_issues=1)
            run.assert_called_once()

    def test_empty_queue_no_model(self):
        with patch.object(queue_mod, "run_one") as run:
            queue_mod.execute_queue(REPO, [], execute=True,
                                    acknowledge_credit_risk=True, max_issues=1)
            run.assert_not_called()

    def test_gh_listing_uses_read_only_query(self):
        class Result:
            returncode = 0
            stderr = "secret that should never be printed"
            stdout = json.dumps([issue(7), issue(2)])
        with patch.object(queue_mod.subprocess, "run", return_value=Result()) as subprocess_run:
            found = queue_mod.list_issues(REPO)
        self.assertEqual([i["number"] for i in found], [2, 7])
        cmd = subprocess_run.call_args.args[0]
        self.assertEqual(cmd[:3], ["gh", "issue", "list"])
        self.assertIn("--state", cmd)
        self.assertIn("--label", cmd)
        self.assertNotIn("edit", cmd)

    def test_gh_failure_does_not_leak_stderr(self):
        class Result:
            returncode = 1
            stderr = "SHOULD_NOT_LEAK"
            stdout = ""
        with patch.object(queue_mod.subprocess, "run", return_value=Result()):
            with self.assertRaisesRegex(RuntimeError, "raw CLI output was withheld") as raised:
                queue_mod.list_issues(REPO)
        self.assertNotIn("SHOULD_NOT_LEAK", str(raised.exception))


if __name__ == "__main__":
    unittest.main()
