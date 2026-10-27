"""Offline tests for the once-per-day Local AutoDev guard. No live GitHub or AI."""
import importlib.util
import json
import os
from pathlib import Path
import stat
import sys
import tempfile
import unittest
from unittest.mock import patch

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / "tools"))
SPEC = importlib.util.spec_from_file_location(
    "local_autodev_daily", ROOT / "tools" / "local_autodev_daily.py")
assert SPEC and SPEC.loader
daily = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(daily)


def one_issue(number=12):
    return [{"number": number, "title": "test issue"}]


class GuardTests(unittest.TestCase):
    def test_noninteractive_execution_requires_explicit_credit_consent(self):
        with patch.object(daily, "verify_checkout") as verify:
            with self.assertRaisesRegex(ValueError, "acknowledge"):
                daily.run_daily(ROOT, ROOT / "nonexistent-state",
                                execute=True, acknowledge_credit_risk=False,
                                max_issues=1, date="2026-10-27")
            verify.assert_not_called()

    def test_max_one_rejected_even_if_approved(self):
        with patch.object(daily, "verify_checkout") as verify:
            with self.assertRaisesRegex(ValueError, "one Issue"):
                daily.run_daily(ROOT, ROOT / "nonexistent-state",
                                execute=True, acknowledge_credit_risk=True,
                                max_issues=2, date="2026-10-27")
            verify.assert_not_called()

    def test_readonly_preview_uses_no_state_files_or_model(self):
        with tempfile.TemporaryDirectory() as temp:
            path = Path(temp) / "daily"
            with patch.object(daily, "verify_checkout"), \
                 patch.object(daily, "open_pr_numbers", return_value=[]), \
                 patch.object(daily.queue, "list_issues", return_value=one_issue()), \
                 patch.object(daily, "invoke_once") as invoke:
                msg = daily.run_daily(ROOT, path, execute=False,
                                      acknowledge_credit_risk=False,
                                      max_issues=1, date="2026-10-27")
            self.assertIn("DRY RUN", msg)
            self.assertIn("#12", msg)
            self.assertFalse(path.exists())
            invoke.assert_not_called()

    def test_open_pr_blocks_even_with_otherwise_approved_issue(self):
        with tempfile.TemporaryDirectory() as temp:
            with patch.object(daily, "verify_checkout"), \
                 patch.object(daily, "open_pr_numbers", return_value=[99]), \
                 patch.object(daily.queue, "list_issues") as list_issues, \
                 patch.object(daily, "invoke_once") as invoke:
                msg = daily.run_daily(ROOT, Path(temp) / "daily",
                                      execute=True, acknowledge_credit_risk=True,
                                      max_issues=1, date="2026-10-27")
            self.assertIn("open PR", msg)
            list_issues.assert_not_called()
            invoke.assert_not_called()

    def test_empty_queue_never_reserves_day(self):
        with tempfile.TemporaryDirectory() as temp:
            path = Path(temp) / "daily"
            with patch.object(daily, "verify_checkout"), \
                 patch.object(daily, "open_pr_numbers", return_value=[]), \
                 patch.object(daily.queue, "list_issues", return_value=[]), \
                 patch.object(daily, "invoke_once") as invoke:
                msg = daily.run_daily(ROOT, path, execute=True,
                                      acknowledge_credit_risk=True,
                                      max_issues=1, date="2026-10-27")
            self.assertIn("no approved", msg)
            self.assertFalse((path / "day-2026-10-27.json").exists())
            invoke.assert_not_called()

    def test_success_reserves_day_and_issue_without_retry(self):
        with tempfile.TemporaryDirectory() as temp:
            path = Path(temp) / "daily"
            with patch.object(daily, "verify_checkout"), \
                 patch.object(daily, "open_pr_numbers", return_value=[]), \
                 patch.object(daily.queue, "list_issues", return_value=one_issue()), \
                 patch.object(daily, "invoke_once", return_value=0) as invoke:
                first = daily.run_daily(ROOT, path, execute=True,
                                        acknowledge_credit_risk=True,
                                        max_issues=1, date="2026-10-27")
                same_day = daily.run_daily(ROOT, path, execute=True,
                                           acknowledge_credit_risk=True,
                                           max_issues=1, date="2026-10-27")
                next_day = daily.run_daily(ROOT, path, execute=True,
                                           acknowledge_credit_risk=True,
                                           max_issues=1, date="2026-10-28")
            self.assertIn("completed", first)
            self.assertIn("already recorded", same_day)
            self.assertIn("already attempted", next_day)
            invoke.assert_called_once()
            self.assertEqual(json.loads((path / "result-2026-10-27.json").read_text())["status"],
                             "succeeded")
            self.assertEqual(stat.S_IMODE(path.stat().st_mode), 0o700)
            for name in ("day-2026-10-27.json", "issue-12.json", "result-2026-10-27.json"):
                self.assertEqual(stat.S_IMODE((path / name).stat().st_mode), 0o600)

    def test_failure_stays_attempted_and_never_retries(self):
        with tempfile.TemporaryDirectory() as temp:
            path = Path(temp) / "daily"
            with patch.object(daily, "verify_checkout"), \
                 patch.object(daily, "open_pr_numbers", return_value=[]), \
                 patch.object(daily.queue, "list_issues", return_value=one_issue()), \
                 patch.object(daily, "invoke_once", return_value=1) as invoke:
                with self.assertRaisesRegex(RuntimeError, "no automatic retry"):
                    daily.run_daily(ROOT, path, execute=True,
                                    acknowledge_credit_risk=True,
                                    max_issues=1, date="2026-10-27")
                next_day = daily.run_daily(ROOT, path, execute=True,
                                           acknowledge_credit_risk=True,
                                           max_issues=1, date="2026-10-28")
            self.assertIn("already attempted", next_day)
            invoke.assert_called_once()
            self.assertEqual(json.loads((path / "result-2026-10-27.json").read_text())["status"],
                             "failed")

    def test_timeout_stays_attempted(self):
        with tempfile.TemporaryDirectory() as temp:
            path = Path(temp) / "daily"
            with patch.object(daily, "verify_checkout"), \
                 patch.object(daily, "open_pr_numbers", return_value=[]), \
                 patch.object(daily.queue, "list_issues", return_value=one_issue()), \
                 patch.object(daily, "invoke_once", side_effect=OSError("secret")) as invoke:
                with self.assertRaisesRegex(RuntimeError, "no retry permitted") as raised:
                    daily.run_daily(ROOT, path, execute=True,
                                    acknowledge_credit_risk=True,
                                    max_issues=1, date="2026-10-27")
            self.assertNotIn("secret", str(raised.exception))
            self.assertTrue((path / "issue-12.json").exists())
            invoke.assert_called_once()

    def test_invalid_pr_query_fails_closed(self):
        class Result:
            returncode = 1
            stdout = ""
            stderr = "private"
        with patch.object(daily.subprocess, "run", return_value=Result()):
            with self.assertRaisesRegex(RuntimeError, "output withheld") as raised:
                daily.open_pr_numbers(ROOT)
        self.assertNotIn("private", str(raised.exception))

    def test_refuse_symlinked_state_directory(self):
        with tempfile.TemporaryDirectory() as temp:
            target = Path(temp) / "real"
            target.mkdir()
            link = Path(temp) / "link"
            link.symlink_to(target, target_is_directory=True)
            with self.assertRaisesRegex(RuntimeError, "Symlinked"):
                daily.ensure_private_state(link)


if __name__ == "__main__":
    unittest.main()
