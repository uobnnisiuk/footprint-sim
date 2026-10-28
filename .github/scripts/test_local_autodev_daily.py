"""Offline tests for the eight-slot JST Local AutoDev guard. No live GitHub or AI."""
import importlib.util
import json
import os
from pathlib import Path
import shutil
import stat
import subprocess
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


class CheckoutTests(unittest.TestCase):
    """Exercise real Git status/ref output, without GitHub or a model CLI."""

    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.repo = Path(self.temp.name) / "repo"
        self.repo.mkdir()
        (self.repo / "tools").mkdir()
        (self.repo / ".github/scripts").mkdir(parents=True)
        (self.repo / "tools/local_autodev.py").write_text(
            'raise AssertionError("Runner must never execute in this test")\n')
        (self.repo / "source.py").write_text("original = True\n")
        self.git("init", "-q", "--initial-branch=main")
        self.commit_base()

    def git(self, *args):
        return subprocess.run(
            ["git", "-c", "core.hooksPath=/dev/null", "-c", "commit.gpgsign=false",
             "-c", "user.name=Test", "-c", "user.email=test@example.invalid", *args],
            cwd=self.repo, check=True, text=True, capture_output=True).stdout.strip()

    def commit_base(self):
        self.git("add", ".")
        self.git("commit", "-qm", "test fixture")
        self.git("update-ref", "refs/remotes/origin/main", "HEAD")

    def assert_dirty(self):
        with self.assertRaisesRegex(RuntimeError, "uncommitted changes"):
            daily.verify_checkout(self.repo)

    def test_main_and_detached_at_origin_main_are_accepted(self):
        daily.verify_checkout(self.repo)
        self.git("checkout", "--detach")
        daily.verify_checkout(self.repo)
        self.assertEqual(self.git("branch", "--show-current"), "")

    def test_named_research_branch_even_at_origin_main_is_rejected(self):
        self.git("checkout", "-b", "research")
        with self.assertRaisesRegex(RuntimeError, "main or detached"):
            daily.verify_checkout(self.repo)

    def test_main_and_detached_ahead_or_behind_origin_main_are_rejected(self):
        old = self.git("rev-parse", "HEAD")
        self.git("commit", "--allow-empty", "-qm", "local change")
        new = self.git("rev-parse", "HEAD")
        for branch in ("main", ""):
            if not branch:
                self.git("checkout", "--detach")
            with self.assertRaisesRegex(RuntimeError, "must match local origin/main"):
                daily.verify_checkout(self.repo)
        self.git("update-ref", "refs/remotes/origin/main", new)
        self.git("checkout", "--detach", old)
        with self.assertRaisesRegex(RuntimeError, "must match local origin/main"):
            daily.verify_checkout(self.repo)
        self.git("branch", "-f", "main", old)
        self.git("checkout", "main")
        with self.assertRaisesRegex(RuntimeError, "must match local origin/main"):
            daily.verify_checkout(self.repo)
        self.assertEqual(self.git("rev-parse", "HEAD"), old)
        self.assertEqual(self.git("rev-parse", "origin/main"), new)

    def test_missing_origin_main_is_rejected(self):
        self.git("update-ref", "-d", "refs/remotes/origin/main")
        with self.assertRaises(RuntimeError):
            daily.verify_checkout(self.repo)

    def test_only_known_untracked_bytecode_is_tolerated_without_deletion(self):
        self.git("checkout", "--detach")
        for directory in ("tools", ".github/scripts"):
            cache = self.repo / directory / "__pycache__"
            cache.mkdir()
            (cache / "module.cpython-312.pyc").write_bytes(b"cached bytecode")
        before = self.git("status", "--porcelain", "--untracked-files=all")
        daily.verify_checkout(self.repo)
        self.assertEqual(self.git("status", "--porcelain", "--untracked-files=all"), before)

    def test_staged_and_unstaged_source_edits_are_rejected(self):
        (self.repo / "source.py").write_text("modified = True\n")
        self.assert_dirty()
        self.git("add", "source.py")
        self.assert_dirty()

    def test_tracked_deletion_and_rename_to_cache_are_rejected(self):
        (self.repo / "source.py").unlink()
        self.assert_dirty()
        self.git("restore", "source.py")
        cache = self.repo / "tools/__pycache__"
        cache.mkdir()
        self.git("mv", "source.py", "tools/__pycache__/source.pyc")
        self.assert_dirty()

    def test_tracked_bytecode_edits_are_rejected(self):
        cache = self.repo / "tools/__pycache__"
        cache.mkdir()
        bytecode = cache / "module.pyc"
        bytecode.write_bytes(b"original")
        self.commit_base()
        bytecode.write_bytes(b"changed")
        self.assert_dirty()
        self.git("add", ".")
        self.assert_dirty()

    def test_untracked_source_and_other_files_are_rejected(self):
        for name in ("new.py", "tools/__pycache__/new.py",
                     ".github/scripts/__pycache__/notes.txt",
                     "elsewhere/__pycache__/module.pyc", "tools/module.pyc",
                     "tools/__pycache__/nested/module.pyc",
                     "tools/__pycache__/line\nbreak.py"):
            with self.subTest(name=name):
                path = self.repo / name
                path.parent.mkdir(parents=True, exist_ok=True)
                path.write_text("user content")
                try:
                    self.assert_dirty()
                finally:
                    path.unlink()

    def test_cache_symlink_is_rejected(self):
        cache = self.repo / "tools/__pycache__"
        cache.mkdir()
        (cache / "module.pyc").symlink_to(self.repo / "source.py")
        self.assert_dirty()

    def test_dirty_checkout_stops_before_queries_state_or_model(self):
        (self.repo / "source.py").write_text("modified = True\n")
        state = Path(self.temp.name) / "daily"
        with patch.object(daily, "open_pr_numbers") as prs, \
             patch.object(daily.queue, "list_issues") as issues, \
             patch.object(daily, "invoke_once") as invoke:
            for execute in (False, True):
                with self.assertRaisesRegex(RuntimeError, "uncommitted changes"):
                    daily.run_daily(self.repo, state, execute=execute,
                                    acknowledge_credit_risk=True, max_issues=1,
                                    date="2026-10-27")
            prs.assert_not_called()
            issues.assert_not_called()
            invoke.assert_not_called()
        self.assertFalse(state.exists())


    def test_behind_origin_main_can_be_checked_and_refreshed_safely(self):
        baseline = self.git("rev-parse", "HEAD")
        self.git("commit", "--allow-empty", "-qm", "new main commit")
        latest = self.git("rev-parse", "HEAD")
        self.git("update-ref", "refs/remotes/origin/main", latest)
        self.git("checkout", "--detach", baseline)
        self.git("remote", "add", "origin",
                 "https://github.com/uobnnisiuk/footprint-sim.git")
        cache = self.repo / "tools/__pycache__"
        cache.mkdir()
        (cache / "module.pyc").write_bytes(b"retained")
        with self.assertRaisesRegex(RuntimeError, "must match"):
            daily.verify_checkout(self.repo)
        daily.verify_checkout(self.repo, allow_behind=True)
        original = daily.command
        def mock_fetch(args, cwd, timeout=60, *, strip=True):
            if "fetch" in args:
                self.assertIn("--no-tags", args)
                return ""
            return original(args, cwd, timeout=timeout, strip=strip)
        with patch.object(daily, "command", side_effect=mock_fetch):
            daily.refresh_checkout(self.repo)
        self.assertEqual(self.git("rev-parse", "HEAD"), latest)
        self.assertEqual((cache / "module.pyc").read_bytes(), b"retained")
        daily.verify_checkout(self.repo)

    def test_refresh_rejects_untrusted_remote_before_fetch_or_merge(self):
        self.git("remote", "add", "origin", "https://example.invalid/evil.git")
        with patch.object(daily, "command", wraps=daily.command) as command:
            with self.assertRaisesRegex(RuntimeError, "Untrusted Git remote"):
                daily.refresh_checkout(self.repo)
            self.assertFalse(any("fetch" in call.args[0] for call in command.call_args_list))

    def test_refresh_refuses_diverging_commits_and_user_changes(self):
        self.git("remote", "add", "origin",
                 "https://github.com/uobnnisiuk/footprint-sim.git")
        original = self.git("rev-parse", "HEAD")
        self.git("commit", "--allow-empty", "-qm", "local fork")
        self.git("update-ref", "refs/remotes/origin/main", original)
        with patch.object(daily, "command", wraps=daily.command) as command:
            with self.assertRaisesRegex(RuntimeError, "not an ancestor"):
                daily.refresh_checkout(self.repo)
            self.assertFalse(any("fetch" in call.args[0] for call in command.call_args_list))
        self.git("checkout", "--detach", original)
        (self.repo / "source.py").write_text("user modification\n")
        with self.assertRaisesRegex(RuntimeError, "uncommitted changes"):
            daily.refresh_checkout(self.repo)

    def test_fresh_dry_run_without_bytecode_flag_keeps_checkout_clean(self):
        for name in ("local_autodev_daily.py", "local_autodev_queue.py"):
            shutil.copyfile(ROOT / "tools" / name, self.repo / "tools" / name)
        self.commit_base()
        self.git("checkout", "--detach")
        fake_bin = Path(self.temp.name) / "bin"
        fake_bin.mkdir()
        gh = fake_bin / "gh"
        gh.write_text('#!/bin/sh\ncase "$1 $2" in\n'
                      '  "pr list"|"issue list") printf "[]\\n" ;;\n'
                      '  *) exit 99 ;;\nesac\n')
        gh.chmod(0o700)
        env = {k: v for k, v in os.environ.items()
               if k not in ("PYTHONDONTWRITEBYTECODE", "PYTHONPYCACHEPREFIX", "PYTHONPATH")}
        env["PATH"] = f"{fake_bin}{os.pathsep}{os.defpath}"
        env["HOME"] = str(Path(self.temp.name) / "home")
        result = subprocess.run(
            [sys.executable, str(self.repo / "tools/local_autodev_daily.py"),
             "--repo-path", str(self.repo)], env=env, cwd=self.repo,
            text=True, capture_output=True, timeout=30, check=True)
        self.assertIn("DRY RUN: queue empty", result.stdout)
        self.assertEqual(list(self.repo.rglob("__pycache__")), [])
        self.assertEqual(self.git("status", "--porcelain"), "")
        self.assertFalse((Path(env["HOME"]) / ".local").exists())


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
                 patch.object(daily, "refresh_checkout"), \
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
                 patch.object(daily, "refresh_checkout"), \
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
                 patch.object(daily, "refresh_checkout"), \
                 patch.object(daily, "open_pr_numbers", return_value=[]), \
                 patch.object(daily.queue, "list_issues", return_value=[]), \
                 patch.object(daily, "invoke_once") as invoke:
                msg = daily.run_daily(ROOT, path, execute=True,
                                      acknowledge_credit_risk=True,
                                      max_issues=1, date="2026-10-27")
            self.assertIn("no approved", msg)
            self.assertFalse((path / "day-2026-10-27-3h-slot-0.json").exists())
            invoke.assert_not_called()

    def test_success_reserves_day_and_issue_without_retry(self):
        with tempfile.TemporaryDirectory() as temp:
            path = Path(temp) / "daily"
            with patch.object(daily, "verify_checkout"), \
                 patch.object(daily, "refresh_checkout"), \
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
            self.assertIn("all approved open Issues already attempted", next_day)
            invoke.assert_called_once()
            self.assertEqual(json.loads((path / "result-2026-10-27-3h-slot-0.json").read_text())["status"],
                             "succeeded")
            self.assertEqual(stat.S_IMODE(path.stat().st_mode), 0o700)
            for name in ("day-2026-10-27-3h-slot-0.json", "issue-12.json", "result-2026-10-27-3h-slot-0.json"):
                self.assertEqual(stat.S_IMODE((path / name).stat().st_mode), 0o600)

    def test_failure_stays_attempted_and_never_retries(self):
        with tempfile.TemporaryDirectory() as temp:
            path = Path(temp) / "daily"
            with patch.object(daily, "verify_checkout"), \
                 patch.object(daily, "refresh_checkout"), \
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
            self.assertIn("all approved open Issues already attempted", next_day)
            invoke.assert_called_once()
            self.assertEqual(json.loads((path / "result-2026-10-27-3h-slot-0.json").read_text())["status"],
                             "failed")

    def test_timeout_stays_attempted(self):
        with tempfile.TemporaryDirectory() as temp:
            path = Path(temp) / "daily"
            with patch.object(daily, "verify_checkout"), \
                 patch.object(daily, "refresh_checkout"), \
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



    def test_real_execution_refreshes_before_issue_discovery(self):
        with tempfile.TemporaryDirectory() as temp:
            state = Path(temp) / "daily"
            call_order = []
            def refresh(_):
                call_order.append("refresh")
            def prs(_):
                call_order.append("prs")
                return [17]
            with patch.object(daily, "verify_checkout"), \
                 patch.object(daily, "refresh_checkout", side_effect=refresh), \
                 patch.object(daily, "open_pr_numbers", side_effect=prs), \
                 patch.object(daily.queue, "list_issues") as issues, \
                 patch.object(daily, "invoke_once") as invoke:
                result = daily.run_daily(ROOT, state, execute=True,
                                         acknowledge_credit_risk=True, max_issues=1,
                                         date="2026-10-28", slot=0)
            self.assertIn("open PR", result)
            self.assertEqual(call_order, ["refresh", "prs"])
            issues.assert_not_called()
            invoke.assert_not_called()

    def test_every_three_hours_has_a_distinct_slot(self):
        self.assertEqual(daily.SLOT_HOURS_JST, (0, 3, 6, 9, 12, 15, 18, 21))
        self.assertIsNone(daily.slot_for_hour(-1))
        self.assertIsNone(daily.slot_for_hour(24))
        for hour, slot in ((0, 0), (2, 0), (3, 1), (5, 1),
                           (6, 2), (8, 2), (9, 3), (11, 3),
                           (12, 4), (14, 4), (15, 5), (17, 5),
                           (18, 6), (20, 6), (21, 7), (23, 7)):
            with self.subTest(hour=hour):
                self.assertEqual(daily.slot_for_hour(hour), slot)

    def test_next_slot_can_run_another_approved_issue_after_merge(self):
        with tempfile.TemporaryDirectory() as temp:
            path = Path(temp) / "daily"
            with patch.object(daily, "verify_checkout"), \
                 patch.object(daily, "refresh_checkout"), \
                 patch.object(daily, "open_pr_numbers", return_value=[]), \
                 patch.object(daily.queue, "list_issues",
                              side_effect=[one_issue(12), one_issue(13)]), \
                 patch.object(daily, "invoke_once", return_value=0) as invoke:
                first = daily.run_daily(ROOT, path, execute=True,
                                        acknowledge_credit_risk=True,
                                        max_issues=1, date="2026-10-28", slot=0)
                second = daily.run_daily(ROOT, path, execute=True,
                                         acknowledge_credit_risk=True,
                                         max_issues=1, date="2026-10-28", slot=1)
            self.assertIn("#12 completed", first)
            self.assertIn("#13 completed", second)
            self.assertEqual(invoke.call_count, 2)
            self.assertTrue((path / "day-2026-10-28-3h-slot-0.json").exists())
            self.assertTrue((path / "day-2026-10-28-3h-slot-1.json").exists())
            self.assertTrue((path / "issue-12.json").exists())
            self.assertTrue((path / "issue-13.json").exists())

    def test_same_issue_remains_blocked_in_later_slot(self):
        with tempfile.TemporaryDirectory() as temp:
            path = Path(temp) / "daily"
            with patch.object(daily, "verify_checkout"), \
                 patch.object(daily, "refresh_checkout"), \
                 patch.object(daily, "open_pr_numbers", return_value=[]), \
                 patch.object(daily.queue, "list_issues", return_value=one_issue(12)), \
                 patch.object(daily, "invoke_once", return_value=0) as invoke:
                daily.run_daily(ROOT, path, execute=True,
                                acknowledge_credit_risk=True, max_issues=1,
                                date="2026-10-28", slot=0)
                skipped = daily.run_daily(ROOT, path, execute=True,
                                          acknowledge_credit_risk=True, max_issues=1,
                                          date="2026-10-28", slot=1)
            self.assertIn("all approved open Issues already attempted", skipped)
            invoke.assert_called_once()

    def test_attempted_issue_is_skipped_for_later_approved_issue(self):
        with tempfile.TemporaryDirectory() as temp:
            state = Path(temp) / "daily"
            daily.ensure_private_state(state)
            daily.create_private(state / "issue-13.json", '{"status":"attempted"}\n')
            candidates = [one_issue(13)[0], one_issue(21)[0]]
            with patch.object(daily, "verify_checkout"), \
                 patch.object(daily, "refresh_checkout"), \
                 patch.object(daily, "open_pr_numbers", return_value=[]), \
                 patch.object(daily.queue, "list_issues", return_value=candidates), \
                 patch.object(daily, "invoke_once", return_value=0) as invoke:
                preview = daily.run_daily(ROOT, state, execute=False,
                                          acknowledge_credit_risk=False, max_issues=1,
                                          date="2026-10-28", slot=1)
                executed = daily.run_daily(ROOT, state, execute=True,
                                           acknowledge_credit_risk=True, max_issues=1,
                                           date="2026-10-28", slot=1)
            self.assertIn("#21", preview)
            self.assertIn("#21 completed", executed)
            invoke.assert_called_once()
            self.assertEqual(invoke.call_args.args[1], 21)
            self.assertTrue((state / "issue-13.json").exists())
            self.assertTrue((state / "issue-21.json").exists())

    def test_only_attempted_issues_remain_stopped_with_no_new_attempt(self):
        with tempfile.TemporaryDirectory() as temp:
            state = Path(temp) / "daily"
            daily.ensure_private_state(state)
            daily.create_private(state / "issue-13.json", '{"status":"failed"}\n')
            with patch.object(daily, "verify_checkout"), \
                 patch.object(daily, "refresh_checkout"), \
                 patch.object(daily, "open_pr_numbers", return_value=[]), \
                 patch.object(daily.queue, "list_issues", return_value=one_issue(13)), \
                 patch.object(daily, "invoke_once") as invoke:
                result = daily.run_daily(ROOT, state, execute=True,
                                         acknowledge_credit_risk=True, max_issues=1,
                                         date="2026-10-28", slot=2)
            self.assertIn("all approved open Issues already attempted", result)
            self.assertFalse((state / "day-2026-10-28-3h-slot-2.json").exists())
            invoke.assert_not_called()

    def test_legacy_four_slot_marker_blocks_matching_three_hour_window(self):
        with tempfile.TemporaryDirectory() as temp:
            state = Path(temp) / "daily"
            daily.ensure_private_state(state)
            daily.create_private(state / "day-2026-10-28-slot-0.json",
                                 '{"issue":13,"status":"started"}\n')
            self.assertEqual(
                daily.legacy_slot_markers(state, "2026-10-28", 3),
                [state / "day-2026-10-28-slot-0.json"])
            with patch.object(daily, "verify_checkout"), \
                 patch.object(daily, "refresh_checkout"), \
                 patch.object(daily, "open_pr_numbers", return_value=[]), \
                 patch.object(daily.queue, "list_issues", return_value=one_issue(21)), \
                 patch.object(daily, "invoke_once", return_value=0) as invoke:
                blocked = daily.run_daily(ROOT, state, execute=True,
                                          acknowledge_credit_risk=True, max_issues=1,
                                          date="2026-10-28", slot=3)
                allowed = daily.run_daily(ROOT, state, execute=True,
                                          acknowledge_credit_risk=True, max_issues=1,
                                          date="2026-10-28", slot=4)
            self.assertIn("slot attempt already recorded", blocked)
            self.assertIn("#21 completed", allowed)
            invoke.assert_called_once()
            self.assertTrue((state / "day-2026-10-28-slot-0.json").exists())
            self.assertTrue((state / "day-2026-10-28-3h-slot-4.json").exists())

    def test_legacy_date_marker_blocks_today_even_in_new_slot(self):
        with tempfile.TemporaryDirectory() as temp:
            path = Path(temp) / "daily"
            daily.ensure_private_state(path)
            daily.create_private(path / "day-2026-10-28.json", '{"status":"started"}\n')
            with patch.object(daily, "verify_checkout"), \
                 patch.object(daily, "refresh_checkout"), \
                 patch.object(daily, "open_pr_numbers", return_value=[]), \
                 patch.object(daily.queue, "list_issues", return_value=one_issue(13)), \
                 patch.object(daily, "invoke_once") as invoke:
                skipped = daily.run_daily(ROOT, path, execute=True,
                                          acknowledge_credit_risk=True, max_issues=1,
                                          date="2026-10-28", slot=1)
            self.assertIn("legacy daily attempt", skipped)
            invoke.assert_not_called()

    def test_out_of_range_slot_rejected_before_any_model_work(self):
        with patch.object(daily, "verify_checkout") as verify:
            for slot in (-1, 8, True):
                with self.subTest(slot=slot):
                    with self.assertRaisesRegex(ValueError, "slot"):
                        daily.run_daily(ROOT, ROOT / "nonexistent-state", execute=True,
                                        acknowledge_credit_risk=True, max_issues=1,
                                        date="2026-10-28", slot=slot)
            verify.assert_not_called()

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
