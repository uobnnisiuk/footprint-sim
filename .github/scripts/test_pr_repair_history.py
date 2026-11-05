"""Offline UOB-6 history tests; generated records, no production state access."""
from dataclasses import FrozenInstanceError
from datetime import datetime, timezone
import json
import os
from pathlib import Path
import sys
import tempfile
import unittest
from unittest.mock import patch

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / "tools"))
import local_autodev_hourly as hourly
import pr_repair_history as history
import pr_repair_ledger as ledger


def at(hour=12, day=4):
    return datetime(2026, 11, day, hour, 5, tzinfo=hourly.daily.JST)


def write(path, value, *, canonical=False):
    path.parent.mkdir(mode=0o700, exist_ok=True)
    path.write_bytes(ledger._canonical(value) if canonical else json.dumps(value).encode())
    path.chmod(0o600)


class HistoryTests(unittest.TestCase):
    def setUp(self):
        tmp = tempfile.TemporaryDirectory()
        self.addCleanup(tmp.cleanup)
        self.root = Path(tmp.name)
        self.daily = self.root / "daily"
        self.daily.mkdir(mode=0o700)

    def initial(self, issue=12, hour=10, day=4, *, result=True, code=0, kind="hour", month=11):
        t = datetime(2026, month, day, hour, 5, tzinfo=hourly.daily.JST)
        stem = f"{t.date().isoformat()}-{kind}-{hour}"
        rec = {"dateJst": t.date().isoformat(), "issue": issue,
               "status": "reserved" if kind == "hour" else "started"}
        if kind == "hour":
            rec.update(hour=hour, policy=hourly.POLICY, attemptedAtJst=t.isoformat())
        else:
            rec["slot"] = hour
        write(self.daily / f"day-{stem}.json", rec)
        write(self.daily / f"issue-{issue}.json", {**rec, "status": "attempted"})
        if result:
            r = {k: v for k, v in rec.items() if k != "attemptedAtJst"}
            r.update(status="succeeded" if code == 0 else "failed", exitCode=code)
            write(self.daily / f"result-{stem}.json", r)
        return stem

    def manual(self, *, day=4, hour=9, result=True, exception=None, month=11):
        worktrees = [{"path": "/fixture/repo", "head": "a" * 40,
                      "status_sha256": "b" * 64, "unstaged_sha256": "c" * 64,
                      "staged_sha256": "d" * 64, "untracked": {}}]
        t = datetime(2026, month, day, hour, 5, tzinfo=hourly.daily.JST).astimezone(timezone.utc).isoformat()
        marker = {"issue": 10, "state": "attempt_consumed", "started_at": t,
                  "queue_head": 10, "argv": history.MANUAL_ARGV,
                  "worktrees_before": worktrees, "authorization": history.MANUAL_AUTHORIZATION,
                  "no_retry": True}
        write(self.root / "attempts" / "issue-10.json", marker)
        if result:
            value = {"issue": 10, "finished_at": t, "exit_code": 0,
                     "runner_invocations": [10], "exception_type": exception,
                     "worktrees_unchanged": True, "worktrees_after": worktrees,
                     "retry_permitted": False}
            write(self.root / "attempts" / "issue-10-result.json", value)

    def repair(self, *, hour=11, day=4, kind="minor", issue=56, pr=58,
               attempt=1, rid="a" * 32, events=()):
        t = at(hour, day)
        rec = {"schema": ledger.SCHEMA, "id": rid, "kind": kind,
               "rootIssue": issue, "rootPr": pr, "approvedTitleHash": "a" * 64,
               "approvedBodyHash": "b" * 64, "approvalReservationId": issue + 100,
               "approvalResultId": issue + 200, "mainSha": "c" * 40, "headSha": "d" * 40,
               "createdAtJst": t.isoformat(), "expiresAtEpoch": int(t.timestamp()) + 1000,
               "dateJst": t.date().isoformat(), "hourJst": hour, "attempt": attempt,
               "status": "reserved"}
        folder = self.root / "pr-repair-ledger"
        write(folder / f"reservation-{rid}.json", rec, canonical=True)
        for seq, name in enumerate(events, 1):
            ev = {"schema": ledger.SCHEMA, "id": rid, "sequence": seq,
                  "event": name, "atJst": t.isoformat(), "headBefore": "d" * 40,
                  "headAfter": "e" * 40 if name == "published" else None}
            write(folder / f"event-{rid}-{seq:02d}.json", ev, canonical=True)

    def test_combines_three_stores_without_metadata_double_count(self):
        self.initial()
        self.manual()
        self.repair(events=("failed",))
        before = history._snapshot(self.root)
        with patch.object(hourly, "reserve_attempt", side_effect=AssertionError), \
                patch.object(ledger, "_lock", side_effect=AssertionError), \
                patch.object(ledger, "_write_file", side_effect=AssertionError):
            audit = history.audit_history(self.root, at())
        self.assertTrue(audit.local_valid)
        self.assertEqual((audit.daily_count, audit.manual_count, audit.repair_count), (1, 1, 1))
        self.assertEqual(audit.effective_today, 3)
        self.assertEqual(audit.consumed_issues, frozenset((10, 12)))
        self.assertEqual(audit.holds, ("legacy_minor_history_unverified",))
        self.assertEqual(before, history._snapshot(self.root))
        self.assertFalse(audit.may_reserve)
        self.assertFalse(audit.may_publish)
        with self.assertRaises(FrozenInstanceError):
            audit.effective_today = 0

    def test_generated_mixed_legacy_shape_ten_daily_plus_issue_ten(self):
        # Synthetic records mirror the observed schemas/counts, not copied receipts.
        for issue, day, slot, kind in ((12, 27, 3, "slot"), (13, 28, 0, "slot"),
                (21, 28, 6, "3h-slot"), (28, 28, 7, "3h-slot"),
                (31, 29, 0, "3h-slot"), (32, 29, 1, "3h-slot"),
                (39, 29, 2, "3h-slot"), (41, 29, 3, "3h-slot"),
                (54, 29, 6, "3h-slot"), (56, 29, 22, "hour")):
            self.initial(issue, slot, day, month=10, kind=kind, code=1 if issue in (13, 21) else 0)
        self.manual(day=27, hour=10, month=10)
        audit = history.audit_history(self.root, at())
        self.assertTrue(audit.local_valid)
        self.assertEqual(audit.daily_count, 10)
        self.assertEqual(audit.manual_count, 1)
        self.assertEqual(audit.effective_today, 0)
        self.assertEqual(len(audit.consumed_issues), 11)
        self.assertEqual(audit.holds, ("legacy_minor_history_unverified",))

    def test_historical_daily_excess_is_not_erased_at_midnight(self):
        for hour in range(9):
            self.initial(100 + hour, hour, day=3)
        audit = history.audit_history(self.root, at())
        self.assertEqual(audit.effective_today, 0)
        self.assertIn("historical_shared_daily_cap_exceeded", audit.holds)

    def test_unknown_results_and_timeouts_never_refund(self):
        self.initial(result=False)
        self.manual(result=False)
        self.repair(events=("receipt_sent", "response_unknown"))
        audit = history.audit_history(self.root, at())
        self.assertTrue(audit.local_valid)
        self.assertEqual(audit.effective_today, 3)
        self.assertEqual(audit.unresolved_count, 3)
        self.assertIn("unresolved_consumed_history", audit.holds)

    def test_day_boundary_resets_date_only_not_lifetime(self):
        self.initial(hour=23, day=3)
        self.manual(day=3)
        a = history.audit_history(self.root, at(23, 3))
        b = history.audit_history(self.root, at(0, 4))
        self.assertEqual(a.effective_today, 2)
        self.assertEqual(b.effective_today, 0)
        self.assertEqual(a.consumed_issues, b.consumed_issues)
        self.assertIn("hour_already_consumed", a.holds)

    def test_manual_and_daily_duplicate_never_discounted(self):
        self.initial(issue=10)
        self.manual()
        audit = history.audit_history(self.root, at())
        self.assertFalse(audit.local_valid)
        self.assertIsNone(audit.effective_today)
        self.assertIn("duplicate_initial_history", audit.holds)

    def test_historical_cross_store_hour_conflict_holds_after_midnight(self):
        self.initial(hour=10, day=3)
        self.repair(hour=10, day=3)
        audit = history.audit_history(self.root, at())
        self.assertTrue(audit.local_valid)
        self.assertEqual(audit.effective_today, 0)
        self.assertIn("historical_hour_conflict", audit.holds)

    def test_legacy_bucket_blocks_conservatively(self):
        self.initial(hour=3, kind="3h-slot")
        self.manual(hour=10)
        audit = history.audit_history(self.root, at())
        self.assertIn("historical_hour_conflict", audit.holds)

    def test_seven_initial_plus_repair_eighth_cap(self):
        for hour in range(7):
            self.initial(issue=100 + hour, hour=hour)
        self.repair(hour=7)
        audit = history.audit_history(self.root, at())
        self.assertEqual(audit.effective_today, 8)
        self.assertIn("shared_daily_cap_reached", audit.holds)

    def test_three_repair_lifetime_cannot_be_hidden(self):
        self.repair(day=1, kind="minor", rid="a" * 32)
        self.repair(day=2, kind="substantive", rid="b" * 32)
        self.repair(day=3, kind="substantive", attempt=2, rid="c" * 32)
        audit = history.audit_history(self.root, at())
        self.assertFalse(audit.local_valid)
        self.assertIsNone(audit.effective_today)

    def test_future_receipt_clock_regression(self):
        self.repair(day=5)
        audit = history.audit_history(self.root, at())
        self.assertFalse(audit.local_valid)
        self.assertIn("history_clock_regression", audit.holds)

    def test_duplicate_claims_and_events_stay_consumed(self):
        self.repair(day=2, kind="minor", rid="a" * 32, events=("publish_started",))
        self.repair(day=3, kind="substantive", rid="b" * 32,
                    events=("publish_started", "publish_started"))
        audit = history.audit_history(self.root, at())
        self.assertTrue(audit.local_valid)
        self.assertEqual(audit.repair_count, 2)
        self.assertIn("repair_claim_conflict", audit.holds)
        self.assertIn("repair_event_conflict", audit.holds)

    def test_fabricated_published_event_is_not_success(self):
        self.repair(events=("published",))
        audit = history.audit_history(self.root, at())
        self.assertTrue(audit.local_valid)
        self.assertIn("repair_event_conflict", audit.holds)
        self.assertIn("unresolved_consumed_history", audit.holds)

    def test_ci_without_publication_is_unresolved_consumed(self):
        for kind, result in (("minor", "ci_success"), ("substantive", "ci_failed")):
            with self.subTest(kind=kind, result=result):
                self.repair(kind=kind, events=(result,))
                audit = history.audit_history(self.root, at())
                self.assertEqual(audit.repair_count, 1)
                self.assertEqual(audit.effective_today, 1)
                self.assertEqual(audit.unresolved_count, 1)
                self.assertIn("repair_event_conflict", audit.holds)
                self.assertFalse(audit.may_publish)

    def test_terminal_failure_cannot_be_rewritten_to_success(self):
        for failure in ("failed", "candidate_rejected", "candidate_timeout", "response_unknown"):
            with self.subTest(failure=failure):
                self.repair(events=(failure, "ci_success"))
                audit = history.audit_history(self.root, at())
                self.assertEqual(audit.repair_count, 1)
                self.assertEqual(audit.unresolved_count, 1)
                self.assertIn("repair_event_conflict", audit.holds)
                self.assertIn("unresolved_consumed_history", audit.holds)

    def test_valid_publication_ci_chain_and_terminal_restart(self):
        chain = ("receipt_sent", "candidate_started", "publish_started", "published", "ci_pending", "ci_success")
        self.repair(events=chain)
        audit = history.audit_history(self.root, at())
        self.assertEqual(audit.unresolved_count, 0)
        self.assertNotIn("repair_event_conflict", audit.holds)
        self.repair(events=chain + ("ci_pending",))
        audit = history.audit_history(self.root, at())
        self.assertEqual(audit.unresolved_count, 1)
        self.assertIn("repair_event_conflict", audit.holds)

    def test_ci_rerun_has_separate_pending_contract_without_publication(self):
        self.repair(kind="ci_rerun", events=("receipt_sent", "ci_pending", "ci_success"))
        audit = history.audit_history(self.root, at())
        self.assertEqual(audit.unresolved_count, 0)
        self.assertNotIn("repair_event_conflict", audit.holds)
        for chain in (("ci_success",), ("publish_started", "published", "ci_success")):
            with self.subTest(chain=chain):
                # Rebuild just this synthetic receipt's events, never production records.
                for p in (self.root / "pr-repair-ledger").glob("event-*.json"):
                    p.unlink()
                self.repair(kind="ci_rerun", events=chain)
                audit = history.audit_history(self.root, at())
                self.assertEqual(audit.unresolved_count, 1)
                self.assertIn("repair_event_conflict", audit.holds)

    def test_future_legacy_day_holds(self):
        self.initial(kind="3h-slot", hour=1, day=5)
        self.assertIn("history_clock_regression", history.audit_history(self.root, at()).holds)

    def test_orphan_manual_result_holds(self):
        self.manual()
        (self.root / "attempts" / "issue-10.json").unlink()
        audit = history.audit_history(self.root, at())
        self.assertIn("orphaned_manual_history", audit.holds)

    def test_missing_daily_is_unknown_not_empty_budget(self):
        self.daily.rmdir()
        audit = history.audit_history(self.root, at())
        self.assertFalse(audit.local_valid)
        self.assertIsNone(audit.effective_today)

    def test_unknown_manual_or_duplicate_json_keys_holds_without_echo(self):
        self.manual()
        p = self.root / "attempts" / "issue-10.json"
        p.write_bytes(b'{"issue":10,"issue":"do-not-echo-secret"}')
        audit = history.audit_history(self.root, at())
        self.assertFalse(audit.local_valid)
        self.assertNotIn("do-not-echo-secret", repr(audit))

    def test_private_permissions_symlinks_hardlinks_fifo_rejected(self):
        self.initial()
        p = self.daily / "issue-12.json"
        raw = p.read_bytes()
        p.chmod(0o644)
        self.assertFalse(history.audit_history(self.root, at()).local_valid)
        p.chmod(0o600)
        alias = self.root / "alias"
        os.link(p, alias)
        self.assertFalse(history.audit_history(self.root, at()).local_valid)
        alias.unlink()
        p.unlink()
        alias.write_bytes(raw)
        p.symlink_to(alias)
        self.assertFalse(history.audit_history(self.root, at()).local_valid)
        p.unlink()
        os.mkfifo(p, 0o600)
        self.assertFalse(history.audit_history(self.root, at()).local_valid)

    def test_symlinked_ancestor_rejected(self):
        self.initial()
        alias = self.root / "alias"
        alias.symlink_to(self.root, target_is_directory=True)
        self.assertFalse(history.audit_history(alias, at()).local_valid)

    def test_logs_are_metadata_only(self):
        stem = self.initial()
        log = self.daily / f"log-{stem}.txt"
        log.write_text("do-not-read-log-body")
        log.chmod(0o600)
        real = history._file
        def checked(path, *, content, limit):
            if path == log:
                self.assertFalse(content)
            return real(path, content=content, limit=limit)
        with patch.object(history, "_file", side_effect=checked):
            audit = history.audit_history(self.root, at())
        self.assertTrue(audit.local_valid)
        self.assertNotIn("do-not-read-log-body", repr(audit))

    def test_change_during_observation_fails_closed(self):
        self.initial()
        original = ledger.read_ledger
        calls = 0
        def raced(state, now):
            nonlocal calls
            calls += 1
            result = original(state, now)
            if calls == 2:
                self.initial(issue=13, hour=11)
            return result
        with patch.object(ledger, "read_ledger", side_effect=raced):
            audit = history.audit_history(self.root, at())
        self.assertFalse(audit.local_valid)
        self.assertIn("local_history_changed", audit.holds)

    def test_unknown_file_rejected_before_contents_are_read(self):
        p = self.root / "attempts"
        p.mkdir(mode=0o700)
        (p / "credentials.txt").write_text("never-read-this")
        real = history._file
        def checked(path, **kwargs):
            self.assertNotEqual(path.name, "credentials.txt")
            return real(path, **kwargs)
        with patch.object(history, "_file", side_effect=checked):
            self.assertFalse(history.audit_history(self.root, at()).local_valid)

    def test_manual_oversize_and_worktree_inconsistency_rejected(self):
        self.manual()
        p = self.root / "attempts" / "issue-10-result.json"
        value = json.loads(p.read_bytes())
        value["worktrees_unchanged"] = False
        write(p, value)
        self.assertFalse(history.audit_history(self.root, at()).local_valid)
        p.write_bytes(b"x" * (history.MAX_MANUAL_BYTES + 1))
        self.assertFalse(history.audit_history(self.root, at()).local_valid)


if __name__ == "__main__":
    unittest.main()
