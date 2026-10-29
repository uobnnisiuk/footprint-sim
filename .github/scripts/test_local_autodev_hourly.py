"""Offline hourly controller contracts. Never call GitHub, Codex or Task Scheduler."""
from contextlib import ExitStack
from datetime import datetime, timezone
import fcntl
import importlib.util
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
sys.path.insert(0, str(ROOT / 'tools'))
SPEC = importlib.util.spec_from_file_location('local_autodev_hourly', ROOT / 'tools/local_autodev_hourly.py')
assert SPEC and SPEC.loader
hourly = importlib.util.module_from_spec(SPEC)
sys.modules[SPEC.name] = hourly
SPEC.loader.exec_module(hourly)


def at(hour=10, day=29):
    return datetime(2026, 10, day, hour, 5, tzinfo=hourly.daily.JST)


class HourlyTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.state = Path(self.temp.name) / 'daily'

    def marker(self, kind, index, issue, day=29):
        self.state.mkdir(exist_ok=True)
        date = f'2026-10-{day}'
        name = f'day-{date}.json' if kind == 'daily' else f'day-{date}-{kind}-{index}.json'
        data = {'dateJst': date, 'issue': issue, 'status': 'started'}
        if kind != 'daily': data['hour' if kind == 'hour' else 'slot'] = index
        (self.state / name).write_text(json.dumps(data), encoding='utf-8')
        return self.state / name

    def issue_marker(self, issue, day=29):
        self.state.mkdir(exist_ok=True)
        path = self.state / f'issue-{issue}.json'
        path.write_text(json.dumps({'issue': issue, 'dateJst': f'2026-10-{day}', 'status': 'attempted'}))
        return path

    def run_controller(self, *, execute=True, now=None, issues=(56,), prs=(), code=0, clock=None,
                       refresh=None, fingerprint=None):
        with ExitStack() as stack:
            verify = stack.enter_context(patch.object(hourly.daily, 'verify_checkout'))
            update = stack.enter_context(patch.object(hourly.daily, 'refresh_checkout', side_effect=refresh))
            query = stack.enter_context(patch.object(hourly.daily, 'open_pr_numbers',
                                                  side_effect=list(prs) if prs else [[], []]))
            listing = stack.enter_context(patch.object(hourly.daily.queue, 'list_issues',
                                                      return_value=[{'number': n} for n in issues]))
            invoke = stack.enter_context(patch.object(hourly.daily, 'invoke_once', return_value=code))
            stack.enter_context(patch.object(hourly, 'runtime_fingerprint',
                side_effect=fingerprint if fingerprint is not None else [('same',), ('same',)]))
            result = hourly.run_hourly(ROOT, self.state, execute=execute,
                         acknowledge_credit_risk=execute, clock=clock or (lambda: now or at()))
            return result, invoke.call_count, update.call_count, listing.call_count

    def test_missing_state_preview_is_read_only(self):
        result, calls, updates, _ = self.run_controller(execute=False)
        self.assertIn('#56', result)
        self.assertEqual((calls, updates), (0, 0))
        self.assertFalse(self.state.exists())

    def test_empty_queue_does_not_reserve(self):
        result, calls, _, _ = self.run_controller(issues=())
        self.assertIn('no approved', result)
        self.assertEqual(calls, 0)
        self.assertFalse(list(self.state.glob('day-*')))

    def test_open_pr_prevents_reservation(self):
        result, calls, _, _ = self.run_controller(prs=([55],))
        self.assertIn('open PR', result)
        self.assertEqual(calls, 0)
        self.assertFalse(list(self.state.glob('issue-*')))

    def test_pr_appearing_at_final_check_prevents_reservation(self):
        result, calls, _, _ = self.run_controller(prs=([], [57]))
        self.assertIn('appeared', result)
        self.assertEqual(calls, 0)
        self.assertFalse(list(self.state.glob('day-*')))

    def test_one_success_reserves_hour_and_issue(self):
        result, calls, _, _ = self.run_controller()
        self.assertIn('#56 completed', result)
        self.assertEqual(calls, 1)
        self.assertEqual(hourly.read_budget(self.state, at()).used, 1)
        self.assertTrue((self.state/'day-2026-10-29-hour-10.json').is_file())
        self.assertTrue((self.state/'issue-56.json').is_file())
        result = json.loads((self.state/'result-2026-10-29-hour-10.json').read_text())
        self.assertEqual(result['status'], 'succeeded')
        for path in self.state.glob('*.json'):
            self.assertEqual(stat.S_IMODE(path.stat().st_mode), 0o600)

    def test_same_hour_never_calls_second_issue(self):
        self.run_controller()
        result, calls, _, _ = self.run_controller(issues=(57,))
        self.assertIn('hour already reserved', result)
        self.assertEqual(calls, 0)
        self.assertFalse((self.state/'issue-57.json').exists())

    def test_consecutive_hours_work_within_cap(self):
        self.run_controller()
        result, calls, _, _ = self.run_controller(now=at(11), issues=(56, 57))
        self.assertIn('#57 completed', result)
        self.assertEqual(calls, 1)
        self.assertEqual(hourly.read_budget(self.state, at(12)).used, 2)

    def test_eight_then_ninth_never_calls_model(self):
        for hour in range(8):
            _, calls, _, _ = self.run_controller(now=at(hour), issues=(100+hour,))
            self.assertEqual(calls, 1)
        result, calls, _, _ = self.run_controller(now=at(8), issues=(200,))
        self.assertIn('daily attempt cap reached', result)
        self.assertEqual(calls, 0)
        self.assertEqual(len(list(self.state.glob('day-*'))), 8)

    def test_nonzero_consumes_allowance_and_has_failure_result(self):
        with self.assertRaisesRegex(RuntimeError, 'no automatic retry'):
            self.run_controller(code=1)
        self.assertEqual(hourly.read_budget(self.state, at(11)).used, 1)
        result, calls, _, _ = self.run_controller(now=at(11), issues=(56,))
        self.assertIn('no approved unattempted', result)
        self.assertEqual(calls, 0)
        self.assertEqual(json.loads((self.state/'result-2026-10-29-hour-10.json').read_text())['status'], 'failed')

    def test_timeout_consumes_allowance(self):
        with patch.object(hourly.daily, 'verify_checkout'), patch.object(hourly.daily,'refresh_checkout'), \
             patch.object(hourly,'runtime_fingerprint', return_value=()), \
             patch.object(hourly.daily,'open_pr_numbers', return_value=[]), \
             patch.object(hourly.daily.queue,'list_issues', return_value=[{'number':56}]), \
             patch.object(hourly.daily,'invoke_once', side_effect=subprocess.TimeoutExpired('redacted',1)):
            with self.assertRaisesRegex(RuntimeError, 'no retry'):
                hourly.run_hourly(ROOT,self.state,execute=True,acknowledge_credit_risk=True,clock=lambda:at())
        self.assertEqual(hourly.read_budget(self.state,at(11)).used,1)

    def test_old_three_hour_reservations_consume_daily_limit(self):
        for index in range(7): self.marker('3h-slot',index,100+index)
        _, calls, _, _ = self.run_controller(now=at(21))
        self.assertEqual(calls,1)
        result,calls,_,_=self.run_controller(now=at(22),issues=(57,))
        self.assertIn('daily attempt cap reached',result)
        self.assertEqual(calls,0)

    def test_old_bucket_is_conservatively_blocked(self):
        self.marker('3h-slot',3,10)
        self.assertIsNotNone(hourly.read_budget(self.state,at(10)).blocked)
        self.assertIsNone(hourly.read_budget(self.state,at(12)).blocked)

    def test_four_slot_and_hourly_records_share_cap(self):
        for index in range(4): self.marker('slot',index,index+10)
        for index in range(4): self.marker('hour',index,index+20)
        self.assertEqual(hourly.read_budget(self.state,at(23)).used,8)
        self.assertIsNotNone(hourly.read_budget(self.state,at(23)).blocked)

    def test_original_day_marker_blocks_entire_day(self):
        self.marker('daily',0,10)
        self.assertEqual(hourly.read_budget(self.state,at(23)).blocked,'legacy daily reservation')

    def test_failed_reservations_are_not_deduplicated(self):
        self.marker('hour',1,10)
        self.marker('3h-slot',1,10)
        self.assertEqual(hourly.read_budget(self.state,at(10)).used,2)

    def test_matching_issue_marker_not_counted_twice(self):
        self.marker('hour',1,10); self.issue_marker(10)
        self.assertEqual(hourly.read_budget(self.state,at()).used,1)

    def test_orphan_issue_marker_fails_closed(self):
        self.issue_marker(10)
        budget=hourly.read_budget(self.state,at())
        self.assertEqual(budget.used,1)
        self.assertIn('orphaned',budget.blocked)

    def test_partial_reservation_write_cannot_release_allowance(self):
        self.state.mkdir()
        (self.state/'day-2026-10-29-hour-10.json').write_text('')
        with self.assertRaisesRegex(RuntimeError,'Unreadable'):
            self.run_controller()

    def test_malformed_duplicate_or_wrong_day_marker_rejected(self):
        p=self.marker('hour',1,10)
        for content in ('[]','{"dateJst":"2026-10-29","dateJst":"2026-10-29"}',
                        '{"dateJst":"2026-10-28","issue":10,"hour":1}'):
            with self.subTest(content=content):
                p.write_text(content)
                with self.assertRaises(RuntimeError): hourly.read_budget(self.state,at())

    def test_unknown_reservation_namespace_rejected(self):
        p=self.marker('hour',1,10); p.rename(self.state/'day-2026-10-29-weird.json')
        with self.assertRaisesRegex(RuntimeError,'Unknown'):
            hourly.read_budget(self.state,at())

    def test_invalid_index_rejected(self):
        for kind,index in (('hour',24),('3h-slot',8),('slot',4)):
            with self.subTest(kind=kind):
                p=self.marker(kind,index,10)
                with self.assertRaises(RuntimeError): hourly.read_budget(self.state,at())
                p.unlink()

    def test_symlink_state_and_reservations_rejected(self):
        target=Path(self.temp.name)/'real';target.mkdir()
        self.state.symlink_to(target,target_is_directory=True)
        with self.assertRaises(RuntimeError): hourly.read_budget(self.state,at())
        self.state.unlink();self.state.mkdir()
        p=self.state/'day-2026-10-29-hour-0.json';p.symlink_to(target/'missing')
        with self.assertRaises((RuntimeError, OSError)): hourly.read_budget(self.state,at())

    def test_hardlink_reservation_rejected(self):
        p=self.marker('hour',1,10);os.link(p,Path(self.temp.name)/'link')
        with self.assertRaises(RuntimeError): hourly.read_budget(self.state,at())

    def test_jst_day_resets_budget_but_not_issue_history(self):
        for i in range(8): self.marker('hour',i,100+i,day=28)
        self.issue_marker(56,day=28)
        result,calls,_,_=self.run_controller(now=at(0),issues=(56,57))
        self.assertIn('#57 completed',result);self.assertEqual(calls,1)
        self.assertEqual(hourly.read_budget(self.state,at(1)).used,1)

    def test_date_hour_recomputed_after_slow_preflight(self):
        moments=iter([at(23,28),at(0,29)])
        result,calls,_,_=self.run_controller(clock=lambda:next(moments))
        self.assertEqual(calls,1)
        self.assertTrue((self.state/'day-2026-10-29-hour-0.json').exists())
        self.assertFalse(list(self.state.glob('day-2026-10-28*')))

    def test_utc_clock_converted_to_jst(self):
        now=datetime(2026,10,28,15,5,tzinfo=timezone.utc)
        self.run_controller(now=now)
        self.assertTrue((self.state/'day-2026-10-29-hour-0.json').exists())

    def test_naive_clock_rejected(self):
        with self.assertRaises(ValueError): self.run_controller(now=datetime(2026,10,29))

    def test_same_daily_lock_prevents_concurrency(self):
        self.state.mkdir();fd=os.open(self.state/'daily.lock',os.O_CREAT|os.O_RDWR,0o600)
        try:
            fcntl.flock(fd,fcntl.LOCK_EX|fcntl.LOCK_NB)
            result,calls,_,_=self.run_controller()
            self.assertIn('another daily runner',result);self.assertEqual(calls,0)
        finally:os.close(fd)

    def test_runtime_refresh_change_defers_without_reservation(self):
        result,calls,_,_=self.run_controller(fingerprint=[('old',),('new',)])
        self.assertIn('runtime updated',result);self.assertEqual(calls,0)
        self.assertFalse(list(self.state.glob('day-*')))

    def test_unsafe_checkout_never_queries_or_creates_state(self):
        with patch.object(hourly.daily,'verify_checkout',side_effect=RuntimeError('dirty')), \
             patch.object(hourly.daily,'open_pr_numbers') as query:
            with self.assertRaisesRegex(RuntimeError,'dirty'):
                hourly.run_hourly(ROOT,self.state,execute=True,acknowledge_credit_risk=True)
            query.assert_not_called()
        self.assertFalse(self.state.exists())

    def test_consent_and_single_issue_limit_required(self):
        for maximum in (0,2,True):
            with self.subTest(maximum=maximum), self.assertRaises(ValueError):
                hourly.run_hourly(ROOT,self.state,execute=True,acknowledge_credit_risk=True,max_issues=maximum)
        with self.assertRaises(ValueError):
            hourly.run_hourly(ROOT,self.state,execute=True,acknowledge_credit_risk=False)
        self.assertFalse(self.state.exists())

    def test_failed_issue_is_skipped_not_retried(self):
        self.marker('hour',1,56);self.issue_marker(56)
        result,calls,_,_=self.run_controller(issues=(56,57))
        self.assertIn('#57 completed',result);self.assertEqual(calls,1)

    def test_crash_between_reservation_files_leaves_consumed_hour(self):
        self.state.mkdir()
        original=hourly.daily.create_private
        def second_write_fails(path,data):
            if path.name.startswith('issue-'):raise OSError('simulated filesystem failure')
            original(path,data)
        with patch.object(hourly.daily,'create_private',side_effect=second_write_fails):
            with self.assertRaises(OSError): hourly.reserve_attempt(self.state,at(),56)
        budget=hourly.read_budget(self.state,at())
        self.assertEqual(budget.used,1);self.assertIsNotNone(budget.blocked)


if __name__=='__main__':
    unittest.main()
