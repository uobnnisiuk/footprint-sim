"""R3 offline ledger tests. Fake clocks/PR facts; zero GitHub/model calls."""
from concurrent.futures import ThreadPoolExecutor
from dataclasses import replace
from datetime import datetime, timedelta
import fcntl
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
import local_autodev_hourly as hourly
import pr_repair_ledger as ledger


def at(hour=10, day=3):
    return datetime(2026, 11, day, hour, 5, tzinfo=hourly.daily.JST)


class LedgerTests(unittest.TestCase):
    def setUp(self):
        temp = tempfile.TemporaryDirectory()
        self.addCleanup(temp.cleanup)
        self.state = Path(temp.name) / "daily"

    def gate(self, hour=10, day=3, root=56, pr=58, **change):
        when = at(hour, day)
        base = ledger.Gate(
            root_issue=root, root_pr=pr, trusted_roots=((root, pr),),
            approval_reservation_id=600 + root,
            approval_result_id=700 + root,
            approved_title_hash="a" * 64,
            approved_body_hash="b" * 64,
            observed_title_hash="a" * 64,
            observed_body_hash="b" * 64,
            requested_main_sha="c" * 40,
            observed_main_sha="c" * 40,
            requested_head_sha="d" * 40,
            observed_head_sha="d" * 40,
            open_pr_numbers=(pr,),
            expires_at_epoch=int(when.timestamp()) + 1800)
        return replace(base, **change)

    def reserve(self, kind="substantive", hour=10, day=3, root=56,
                pr=58, execute=True, **change):
        return ledger.reserve(self.state, self.gate(hour, day, root, pr, **change),
                              kind, execute=execute, clock=lambda: at(hour, day))

    def test_dry_run_zero_state_or_model_writes(self):
        with patch.object(hourly.daily, "invoke_once") as model:
            result = self.reserve(execute=False)
        self.assertEqual(result["kind"], "substantive")
        self.assertFalse(self.state.exists())
        self.assertFalse((self.state.parent / "pr-repair-ledger").exists())
        model.assert_not_called()

    def test_durable_receipt_and_no_result_remains_consumed(self):
        receipt = self.reserve()
        folder = self.state.parent / "pr-repair-ledger"
        self.assertTrue((folder / f'reservation-{receipt["id"]}.json').is_file())
        self.assertEqual(ledger.read_ledger(self.state, at()).today, 1)
        self.assertEqual(hourly.read_budget(self.state, at()).used, 1)
        self.assertIn("already reserved", hourly.read_budget(self.state, at()).blocked)
        self.assertEqual(ledger.read_ledger(self.state, at(10, 4)).today, 0)
        # No event/receipt response does not free an operation or create retry.
        self.reserve(hour=11, day=4)
        with self.assertRaisesRegex(ledger.LedgerHalt, "lifetime"):
            self.reserve(hour=12, day=5)

    def test_minor_plus_substantive_lifetime_two(self):
        self.reserve("minor")
        with self.assertRaisesRegex(ledger.LedgerHalt, "shared|reserved"):
            self.reserve("substantive")
        self.reserve("substantive", 11, 4)
        with self.assertRaisesRegex(ledger.LedgerHalt, "lifetime"):
            self.reserve("substantive", 12, 5)
        with self.assertRaisesRegex(ledger.LedgerHalt, "lifetime"):
            self.reserve("minor", 13, 6)

    def test_two_substantive_then_third_rejected_across_days(self):
        self.reserve(hour=2)
        self.reserve(hour=3, day=4)
        with self.assertRaisesRegex(ledger.LedgerHalt, "lifetime"):
            self.reserve(hour=4, day=5)

    def test_substantive_daily_two_limit(self):
        self.reserve(hour=0, root=56, pr=58)
        self.reserve(hour=1, root=57, pr=59)
        with self.assertRaisesRegex(ledger.LedgerHalt, "substantive_daily_cap"):
            self.reserve(hour=2, root=60, pr=61)
        self.assertEqual(ledger.read_ledger(self.state, at(3)).substantive_today, 2)

    def test_shared_hour_across_kinds_and_legacy(self):
        hourly.daily.ensure_private_state(self.state)
        hourly.reserve_attempt(self.state, at(10), 31)
        with self.assertRaisesRegex(ledger.LedgerHalt, "hour"):
            self.reserve(hour=10)
        self.reserve(hour=11)
        budget = hourly.read_budget(self.state, at(12))
        self.assertEqual(budget.used, 2)
        self.assertIsNone(budget.blocked)

    def test_seven_legacy_plus_one_new_rejects_ninth(self):
        hourly.daily.ensure_private_state(self.state)
        for hour in range(7):
            hourly.reserve_attempt(self.state, at(hour), 100 + hour)
        self.reserve(hour=7)
        self.assertEqual(hourly.read_budget(self.state, at(8)).used, 8)
        with self.assertRaisesRegex(ledger.LedgerHalt, "cap"):
            self.reserve(hour=8, root=57, pr=59)

    def test_old_ledger_bytes_modes_never_rewritten(self):
        hourly.daily.ensure_private_state(self.state)
        hourly.reserve_attempt(self.state, at(9), 31)
        old = {p.name: (p.read_bytes(), stat.S_IMODE(p.stat().st_mode))
               for p in self.state.iterdir() if p.is_file() and p.name != "daily.lock"}
        self.reserve(hour=10)
        after = {p.name: (p.read_bytes(), stat.S_IMODE(p.stat().st_mode))
                 for p in self.state.iterdir() if p.is_file() and p.name != "daily.lock"}
        self.assertEqual(after, old)

    def test_old_orphan_stops_even_after_midnight(self):
        self.state.mkdir()
        (self.state / "issue-999.json").write_text("{}")
        with self.assertRaises(RuntimeError):
            self.reserve(hour=10, day=4)

    def test_corrupt_new_file_stops_in_later_month(self):
        r = self.reserve()
        f = self.state.parent / "pr-repair-ledger" / f'reservation-{r["id"]}.json'
        f.write_bytes(b'{"schema":')
        with self.assertRaises(ledger.LedgerHalt):
            ledger.read_ledger(self.state, at(10, 9))
        with self.assertRaises(ledger.LedgerHalt):
            hourly.read_budget(self.state, at(10, 9))

    def test_missing_event_or_response_never_releases_reservation(self):
        r = self.reserve()
        self.assertEqual(ledger.read_ledger(self.state, at()).events, ())
        with self.assertRaisesRegex(ledger.LedgerHalt, "reserved"):
            self.reserve(hour=10, root=57, pr=59)
        ledger.append_event(self.state, r["id"], "response_unknown",
                            clock=lambda: at(11))
        audit = ledger.read_ledger(self.state, at(12))
        self.assertEqual(audit.events[0]["event"], "response_unknown")
        self.assertEqual(audit.today, 1)

    def test_event_append_only_and_unknown_result_fails_closed(self):
        r = self.reserve()
        ledger.append_event(self.state, r["id"], "receipt_sent", clock=lambda: at(11))
        ledger.append_event(self.state, r["id"], "candidate_timeout",
                            clock=lambda: at(12))
        self.assertEqual(len(ledger.read_ledger(self.state, at(13)).events), 2)
        with self.assertRaises(ledger.LedgerHalt):
            ledger.append_event(self.state, r["id"], "unconsume",
                                clock=lambda: at(13))
        self.assertEqual(ledger.read_ledger(self.state, at(13)).today, 1)

    def test_orphan_event_and_unknown_file_stop(self):
        r = self.reserve()
        folder = self.state.parent / "pr-repair-ledger"
        (folder / "unexpected.json").write_text("{}")
        with self.assertRaises(ledger.LedgerHalt):
            ledger.read_ledger(self.state, at())
        (folder / "unexpected.json").unlink()
        (folder / f'reservation-{r["id"]}.json').unlink()
        (folder / f'event-{r["id"]}-01.json').write_text("{}")
        with self.assertRaises(ledger.LedgerHalt):
            ledger.read_ledger(self.state, at())

    def test_symlink_or_hardlink_records_refused(self):
        r = self.reserve()
        folder = self.state.parent / "pr-repair-ledger"
        f = folder / f'reservation-{r["id"]}.json'
        other = folder / "other"
        os.link(f, other)
        with self.assertRaises(ledger.LedgerHalt):
            ledger.read_ledger(self.state, at())
        other.unlink()
        f.unlink()
        f.symlink_to(self.state / "daily.lock")
        with self.assertRaises((OSError, ledger.LedgerHalt)):
            ledger.read_ledger(self.state, at())

    def test_changed_issue_hash_main_head_or_open_pr_stops(self):
        variants = [
            {"observed_body_hash": "f" * 64},
            {"observed_title_hash": "f" * 64},
            {"observed_head_sha": "f" * 40},
            {"observed_main_sha": "f" * 40},
            {"open_pr_numbers": (58, 99)},
            {"open_pr_numbers": ()},
            {"trusted_roots": ((56, 77),)},
            {"trusted_roots": ((56, 58), (56, 77))},
            {"trusted_roots": ((56, 58), (57, 58))},
        ]
        for variant in variants:
            with self.subTest(variant=variant):
                with self.assertRaises(ledger.LedgerHalt):
                    self.reserve(**variant)
        self.assertFalse(self.state.exists())

    def test_root_alias_or_approval_id_cannot_reset(self):
        self.reserve()
        with self.assertRaises(ledger.LedgerHalt):
            self.reserve(hour=11, root=56, pr=59)
        with self.assertRaises(ledger.LedgerHalt):
            self.reserve(hour=11, root=57, pr=58)
        with self.assertRaises(ledger.LedgerHalt):
            self.reserve(hour=11, approval_reservation_id=9999)

    def test_ci_rerun_once_per_exact_head_only_when_separately_approved(self):
        with self.assertRaisesRegex(ledger.LedgerHalt, "independent_authorization"):
            self.reserve("ci_rerun")
        self.reserve("ci_rerun", ci_rerun_approved=True)
        with self.assertRaisesRegex(ledger.LedgerHalt, "lifetime"):
            self.reserve("ci_rerun", hour=11, day=4, ci_rerun_approved=True)
        self.reserve("ci_rerun", hour=12, day=5, ci_rerun_approved=True,
                     observed_head_sha="e" * 40, requested_head_sha="e" * 40)
        self.assertEqual(len(ledger.read_ledger(self.state, at(13, 5)).reservations), 2)

    def test_lock_contention_prevents_reservation(self):
        self.state.mkdir()
        lock = self.state / "daily.lock"
        fd = os.open(lock, os.O_CREAT | os.O_RDWR, 0o600)
        try:
            fcntl.flock(fd, fcntl.LOCK_EX | fcntl.LOCK_NB)
            with self.assertRaisesRegex(ledger.LedgerHalt, "another_daily_runner"):
                self.reserve()
        finally:
            os.close(fd)
        self.assertFalse((self.state.parent / "pr-repair-ledger").exists())

    def test_concurrent_workers_only_one_receipt(self):
        def call():
            try:
                return self.reserve()
            except ledger.LedgerHalt:
                return None
        with ThreadPoolExecutor(max_workers=2) as pool:
            results = list(pool.map(lambda _: call(), range(2)))
        self.assertEqual(sum(r is not None for r in results), 1)
        self.assertEqual(ledger.read_ledger(self.state, at()).today, 1)

    def test_partial_file_crash_never_frees_slot(self):
        def interrupted(path, value):
            path.write_bytes(b'{"partial":')
            raise OSError("injected crash")
        with patch.object(ledger, "_write_file", side_effect=interrupted):
            with self.assertRaises(OSError):
                self.reserve()
        with self.assertRaises(ledger.LedgerHalt):
            self.reserve(hour=11, day=4)

    def test_legacy_incomplete_creation_stops_next_day(self):
        self.state.mkdir()
        (self.state / "day-2026-11-03-hour-7.json").write_text("{}")
        with self.assertRaises(RuntimeError):
            self.reserve(hour=10, day=4)

    def test_expired_gate_is_rejected_before_new_record(self):
        with self.assertRaisesRegex(ledger.LedgerHalt, "expired"):
            self.reserve(expires_at_epoch=int(at(10).timestamp()) - 1)
        self.assertFalse((self.state.parent / "pr-repair-ledger").exists())


if __name__ == "__main__":
    unittest.main()
