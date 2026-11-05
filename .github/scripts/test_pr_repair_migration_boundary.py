"""Adversarial M2 contract tests, using synthetic declarations, never live trust."""
from contextlib import ExitStack, redirect_stderr, redirect_stdout
from copy import deepcopy
from dataclasses import replace
from datetime import datetime, timedelta, timezone
import builtins
import fcntl
import io
import json
import os
from pathlib import Path
import socket
import subprocess
import sys
import unittest
from unittest.mock import patch

sys.dont_write_bytecode = True
sys.path.insert(0, str(Path(__file__).resolve().parents[2] / "tools"))
import pr_repair_migration as migration
import pr_repair_contract as contract
import pr_repair_history as history
import pr_repair_ledger as ledger
from test_pr_repair_migration import fixture, rebind_history

SECRET = "sentinel-secret-must-not-appear-in-diagnostics"
PERMISSIONS = ("may_reserve", "may_publish", "may_activate", "may_repair", "may_merge",
               "production_enabled")


class MigrationBoundaryTests(unittest.TestCase):
    def assert_no_authority(self, result):
        for key in PERMISSIONS:
            self.assertIs(result[key], False, key)
        self.assertEqual(result["authentication"], "not_established")
        self.assertEqual(result["live_eligibility"], "unverified")
        self.assertEqual(result["legacy_history"], "unknown/HOLD")

    def assert_hold(self, request, observations, now):
        output = io.StringIO()
        with redirect_stdout(output), redirect_stderr(output):
            result = migration.evaluate(request, observations, now=now)
        self.assertEqual(result["state"], "HOLD")
        self.assertIs(result["offline_contract_satisfied"], False)
        self.assert_no_authority(result)
        self.assertNotIn(SECRET, json.dumps(result) + output.getvalue())
        self.assertEqual(output.getvalue(), "")
        return result

    def test_success_and_holds_have_no_execution_or_authentication_authority(self):
        for kind in ("initial", "minor", "substantive"):
            with self.subTest(kind=kind):
                request, observations, now = fixture(kind)
                result = migration.evaluate(request, observations, now=now)
                self.assertEqual(result["state"], "CONTRACT_SATISFIED")
                self.assert_no_authority(result)
                self.assert_hold(request, None, now)

    def test_pure_evaluation_does_not_touch_io_processes_network_or_locks(self):
        request, observations, now = fixture()
        before = deepcopy((request, observations))
        calls = []

        def forbidden(*args, **kwargs):
            calls.append("side_effect")
            raise AssertionError(SECRET)

        with ExitStack() as stack:
            for owner, attribute in ((builtins, "open"), (io, "open"), (os, "open"),
                    (os, "mkdir"), (os, "rename"), (os, "replace"), (os, "unlink"),
                    (os, "remove"), (os, "system"), (subprocess, "Popen"),
                    (socket, "socket"), (socket, "create_connection"),
                    (fcntl, "flock"), (fcntl, "lockf"), (ledger, "reserve"),
                    (ledger, "append_event"), (ledger, "_lock")):
                stack.enter_context(patch.object(owner, attribute, side_effect=forbidden))
            first = migration.evaluate(request, observations, now=now)
            second = migration.evaluate(request, observations, now=now)
            self.assert_hold(request, None, now)
        self.assertEqual(first["state"], "CONTRACT_SATISFIED")
        self.assertEqual(first, second)
        self.assertEqual(calls, [])
        self.assertEqual((request, observations), before)

    def test_candidate_cannot_issue_authority_using_extra_claims(self):
        for name, value in (("verified", True), ("may_activate", True),
                            ("authentication", "verified"), ("agent_name", SECRET),
                            ("independent_reviewer", True), ("source_hash", "a" * 64)):
            with self.subTest(claim=name):
                request, observations, now = fixture()
                request[name] = value
                self.assert_hold(request, observations, now)
                request, observations, now = fixture()
                observations[name] = value
                self.assert_hold(request, observations, now)

    def test_malformed_top_level_inputs_fail_without_dumping_objects(self):
        for bad in (None, [], True, 1, SECRET, {SECRET: True}):
            with self.subTest(value_type=type(bad).__name__):
                request, observations, now = fixture()
                self.assert_hold(bad, observations, now)
                self.assert_hold(request, bad, now)

    def test_bad_schema_names_are_fixed_code_diagnostics(self):
        request, observations, now = fixture()
        request["schema"] = SECRET
        self.assert_hold(request, observations, now)
        request, observations, now = fixture()
        observations["schema"] = SECRET
        self.assert_hold(request, observations, now)

    def test_missing_or_naive_clock_is_not_current_time_or_new_credit(self):
        for bad_now in (None, SECRET, True, 0, datetime(2026, 11, 5, 0, 5)):
            with self.subTest(clock_type=type(bad_now).__name__):
                request, observations, _ = fixture()
                self.assert_hold(request, observations, bad_now)

    def test_same_head_ci_rerun_cannot_borrow_previous_acceptance_receipt(self):
        for key, value in (("workflow_run_id", 401), ("workflow_attempt", 2),
                           ("job_id", 501), ("head_sha", "9" * 40)):
            with self.subTest(mismatch=key):
                request, observations, now = fixture("substantive")
                observations["review"][key] = value
                self.assert_hold(request, observations, now)

    def test_free_form_reviewer_run_label_does_not_authenticate_run_source(self):
        request, observations, now = fixture("minor")
        observations["review"]["reviewer_run_id"] = SECRET
        self.assert_hold(request, observations, now)

    def test_current_generation_and_lineage_cannot_be_authored_by_candidate(self):
        for section in ("current", "target"):
            with self.subTest(source=section):
                request, observations, now = fixture()
                observations[section]["source"]["actor_id"] = observations["target"]["author_id"]
                self.assert_hold(request, observations, now)

    def test_fixed_record_ids_from_another_repository_cannot_be_transplanted(self):
        for section in ("policy", "boundary", "current", "target", "approval",
                        "review", "acceptance", "history_binding", "inventory", "scope"):
            with self.subTest(source=section):
                request, observations, now = fixture()
                observations[section]["source"]["repo_id"] = migration.REPOSITORY_ID + 1
                self.assert_hold(request, observations, now)
        request, observations, now = fixture()
        request["repo_id"] = migration.REPOSITORY_ID + 1
        # Self-consistent IDs in a wholly different repo are still not this contract.
        for value in observations.values():
            if type(value) is dict and "source" in value:
                value["source"]["repo_id"] = migration.REPOSITORY_ID + 1
        observations["review"]["run_source"]["repo_id"] = migration.REPOSITORY_ID + 1
        self.assert_hold(request, observations, now)

    def test_pr_must_be_observed_as_created_after_cutover_and_initial_attempt(self):
        for created in (None, "2026-11-05T15:00:00+09:00",
                        "2026-11-07T00:07:00+09:00"):
            with self.subTest(pr_created_at=created):
                request, observations, now = fixture("substantive")
                observations["target"]["pr_created_at"] = created
                self.assert_hold(request, observations, now)

    def test_current_root_cannot_reuse_an_attempt_before_cutover_or_approval(self):
        for at in ("2026-11-05T15:05:00+09:00", "2026-11-07T00:07:30+09:00"):
            with self.subTest(at=at):
                request, observations, now = fixture("minor")
                local = observations["history"]
                stamp = datetime.fromisoformat(at)
                record = replace(local.records[0], at=at, date_jst=stamp.date().isoformat(),
                                 hours_jst=(stamp.hour,))
                rebind_history(observations, replace(local, records=(record,)))
                self.assert_hold(request, observations, now)

    def test_explicit_revocation_still_rejects_self_consistent_old_generation(self):
        request, observations, now = fixture()
        request["generation"] = 2
        observations["boundary"]["generation"] = 2
        observations["current"]["generation"] = 2
        observations["current"]["highest_generation"] = 2
        self.assert_hold(request, observations, now)

    def test_same_current_hour_budget_cannot_be_reused_on_another_day(self):
        request, observations, now = fixture()
        newer = now + timedelta(days=1)
        # Refresh current controls only; the local observation is still yesterday's.
        for section in observations.values():
            if type(section) is dict:
                for key in ("source", "run_source"):
                    if key in section:
                        section[key]["observed_at"] = newer.isoformat()
                        section[key]["expires_at"] = (newer + timedelta(hours=1)).isoformat()
        observations["history_binding"]["coverage_through"] = newer.isoformat()
        self.assert_hold(request, observations, newer)

    def old_history(self, observations, result="unknown"):
        local = observations["history"]
        source = history.HistorySource("attempts/issue-10.json", "8" * 64, "schema_validated")
        evidence = [source]
        if result != "unknown":
            evidence.append(history.HistorySource("attempts/issue-10-result.json", "a" * 64,
                                                  "schema_validated"))
        record = history.HistoryRecord(source.source_id, "initial_manual", 10, None,
                    source.source_id, "2026-11-04T14:05:00+09:00", "2026-11-04", (14,),
                    result, source.source_sha256, tuple(evidence), (),
                    ("manual_result_unknown",) if result == "unknown" else ())
        audit = replace(local.audit, manual_count=1, consumed_issues=frozenset((10,)),
                        unresolved_count=int(result == "unknown"))
        rebind_history(observations, replace(local, audit=audit, records=(record,), sources=tuple(evidence)))
        return observations["history"]

    def initial_record(self, root, at, result="failed"):
        stamp = datetime.fromisoformat(at)
        stem = f"{stamp.date().isoformat()}-hour-{stamp.hour}"
        sources = [history.HistorySource("daily/day-" + stem + ".json", "a" * 64, "schema_validated"),
                   history.HistorySource(f"daily/issue-{root}.json", "b" * 64, "schema_validated")]
        if result != "unknown":
            sources.append(history.HistorySource("daily/result-" + stem + ".json", "c" * 64,
                                                  "schema_validated"))
        record = history.HistoryRecord(sources[0].source_id, "initial_daily", root, None,
                    sources[0].source_id, at, stamp.date().isoformat(), (stamp.hour,), result,
                    sources[0].source_sha256, tuple(sources), (),
                    ("result_missing",) if result == "unknown" else ())
        return record, tuple(sources)

    def repair_record(self, request, observations, at, *, kind="r3_substantive", attempt=1,
                      serial=1, root=None, pr=None, head=None):
        stamp = datetime.fromisoformat(at)
        root = request["root_issue"] if root is None else root
        pr = request["root_pr"] if pr is None else pr
        head = request["head_sha"] if head is None else head
        rid = f"{serial:032x}"
        source = history.HistorySource(f"pr-repair-ledger/reservation-{rid}.json", "a" * 64,
                                       "schema_validated")
        event_source = history.HistorySource(f"pr-repair-ledger/event-{rid}-01.json", "b" * 64,
                                             "schema_validated")
        event = history.HistoryEvent(event_source.source_id, event_source.source_sha256,
                                     "failed", at, head, None)
        approval = observations["approval"]
        record = history.HistoryRecord(source.source_id, kind, root, pr, rid, at,
                    stamp.date().isoformat(), (stamp.hour,), "failed", source.source_sha256,
                    (source, event_source), (event,), attempt=attempt, head_before=head,
                    approval_reservation_id=approval["reservation_id"] if root == request["root_issue"] else 1000 + root,
                    approval_result_id=approval["result_id"] if root == request["root_issue"] else 2000 + root)
        return record, (source, event_source)

    def append_history(self, observations, *entries):
        local = observations["history"]
        records = local.records + tuple(record for record, _ in entries)
        sources = {source.source_id: source for source in local.sources}
        for _, supplied in entries:
            for source in supplied:
                sources[source.source_id] = source
        observed = datetime.fromisoformat(local.observed_at).astimezone(migration.JST)
        audit = replace(local.audit,
                    daily_count=sum(r.kind == "initial_daily" for r in records),
                    manual_count=sum(r.kind == "initial_manual" for r in records),
                    repair_count=sum(r.kind in ("r3_minor", "r3_substantive", "ci_rerun") for r in records),
                    effective_today=sum(r.date_jst == observed.date().isoformat() for r in records),
                    effective_hour=sum(r.date_jst == observed.date().isoformat() and observed.hour in r.hours_jst
                                       for r in records),
                    consumed_issues=frozenset(r.root_issue for r in records if r.kind.startswith("initial_")),
                    unresolved_count=sum(r.result == "unknown" for r in records))
        rebind_history(observations, replace(local, audit=audit, records=records, sources=tuple(sources.values())))

    def test_legacy_failed_and_unknown_results_stay_consumed_and_unchanged(self):
        for outcome in ("failed", "unknown"):
            with self.subTest(outcome=outcome):
                request, observations, now = fixture()
                local = self.old_history(observations, outcome)
                before = deepcopy((request, observations))
                result = migration.evaluate(request, observations, now=now)
                self.assertEqual(result["state"], "CONTRACT_SATISFIED", result)
                self.assert_no_authority(result)
                self.assertEqual((request, observations), before)
                self.assertEqual(local.records[0].consumption, "consumed")
                self.assertEqual(local.records[0].result, outcome)

    def test_failed_history_cannot_be_relabelled_unused_or_drop_source(self):
        for attack in ("refund", "orphan"):
            with self.subTest(attack=attack):
                request, observations, now = fixture()
                local = self.old_history(observations, "failed")
                if attack == "refund":
                    changed = replace(local, records=(replace(local.records[0], consumption="unconsumed"),))
                else:
                    changed = replace(local, sources=())
                rebind_history(observations, changed)
                self.assert_hold(request, observations, now)

    def test_legacy_provider_exception_does_not_hide_duplicate_unrelated_initials(self):
        request, observations, now = fixture()
        local = self.old_history(observations, "failed")
        source = history.HistorySource("daily/day-2026-11-03-hour-1.json", "9" * 64,
                                      "schema_validated")
        duplicate = replace(local.records[0], source_id=source.source_id, kind="initial_daily",
                            reservation_id=source.source_id, at="2026-11-03T01:05:00+09:00",
                            date_jst="2026-11-03", hours_jst=(1,), source_sha256=source.source_sha256,
                            evidence=(source,))
        changed = replace(local, audit=replace(local.audit, daily_count=1),
                          records=local.records + (duplicate,), sources=local.sources + (source,))
        rebind_history(observations, changed)
        self.assert_hold(request, observations, now)

    def test_renumbering_and_consistent_new_hashes_do_not_hide_legacy_purpose(self):
        request, observations, now = fixture()
        old_purpose = observations["policy"]["legacy_scope"]["purposes"][0]
        for value in (request, observations["target"], observations["approval"]):
            value["purpose_sha256"] = old_purpose
        observations["review"]["approval_sha256"] = contract.digest(observations["approval"])
        result = self.assert_hold(request, observations, now)
        self.assertEqual(result["reasons"], ["legacy_target"])

    def test_mixed_work_is_rejected_even_with_consistent_lineage_hashes(self):
        request, observations, now = fixture()
        observations["target"]["lineage"]["mixed"] = True
        lineage_digest = contract.digest(observations["target"]["lineage"])
        for value in (request, observations["target"], observations["approval"], observations["review"]):
            value["lineage_sha256"] = lineage_digest
        observations["review"]["approval_sha256"] = contract.digest(observations["approval"])
        result = self.assert_hold(request, observations, now)
        self.assertEqual(result["reasons"], ["legacy_mixed_or_unknown_lineage"])

    def test_existing_research_plan_does_not_become_new_after_midnight(self):
        request, observations, now = fixture()
        observations["target"]["plan_id"] = "F22"
        result = self.assert_hold(request, observations, now)
        self.assertEqual(result["reasons"], ["legacy_target"])

    def test_shortened_quiet_day_cannot_be_approved_by_matching_ta(self):
        request, observations, now = fixture()
        start = datetime.fromisoformat(observations["boundary"]["quiet_start"])
        shortened = (start + timedelta(hours=23)).isoformat()
        observations["boundary"]["quiet_end"] = shortened
        observations["boundary"]["ta"] = shortened
        result = self.assert_hold(request, observations, now)
        self.assertEqual(result["reasons"], ["invalid_cutover_window"])

    def test_cutover_certificate_alone_cannot_advance_first_five_minute_poll(self):
        request, observations, _ = fixture()
        early = datetime.fromisoformat(observations["boundary"]["ta"]) + timedelta(minutes=4)
        for section in observations.values():
            if type(section) is dict:
                for key in ("source", "run_source"):
                    if key in section:
                        section[key]["observed_at"] = early.isoformat()
                        section[key]["expires_at"] = (early + timedelta(hours=1)).isoformat()
        result = self.assert_hold(request, observations, early)
        self.assertEqual(result["reasons"], ["first_poll_not_reached"])

    def test_seven_reservations_leave_one_daily_slot_but_eight_leave_none(self):
        for used in (7, 8):
            with self.subTest(used=used):
                request, observations, now = fixture()
                entries = [self.initial_record(200 + hour, f"2026-11-08T{hour:02d}:05:00+09:00")
                           for hour in range(used)]
                self.append_history(observations, *entries)
                result = migration.evaluate(request, observations, now=now)
                self.assert_no_authority(result)
                self.assertEqual(result["state"], "CONTRACT_SATISFIED" if used == 7 else "HOLD", result)
                if used == 8:
                    self.assertEqual(result["reasons"], ["shared_budget_exhausted"])

    def test_one_current_hour_reservation_blocks_all_operation_kinds(self):
        for kind in ("initial", "minor", "substantive", "ci_rerun"):
            with self.subTest(kind=kind):
                request, observations, now = fixture(kind)
                self.append_history(observations, self.initial_record(250, now.isoformat()))
                result = self.assert_hold(request, observations, now)
                self.assertEqual(result["reasons"], ["shared_budget_exhausted"])

    def test_false_counters_are_not_integer_zero_budget(self):
        for field in ("effective_today", "effective_hour", "daily_count", "repair_count"):
            with self.subTest(counter=field):
                request, observations, now = fixture()
                local = observations["history"]
                rebind_history(observations, replace(local, audit=replace(local.audit, **{field: False})))
                result = self.assert_hold(request, observations, now)
                self.assertEqual(result["reasons"], ["history_counts_mismatch"])

    def test_failed_initial_without_pr_does_not_restore_lifetime_attempt(self):
        request, observations, now = fixture()
        self.append_history(observations,
                            self.initial_record(request["root_issue"], "2026-11-07T01:05:00+09:00"))
        result = self.assert_hold(request, observations, now)
        self.assertEqual(result["reasons"], ["attempt_limit"])

    def test_minor_lifetime_one_and_combined_repair_two_remain_distinct(self):
        for kind in ("minor", "substantive"):
            with self.subTest(requested_kind=kind):
                request, observations, now = fixture(kind)
                self.append_history(observations, self.repair_record(request, observations,
                                    "2026-11-08T01:05:00+09:00", kind="r3_minor"))
                result = migration.evaluate(request, observations, now=now)
                self.assertEqual(result["state"], "HOLD" if kind == "minor" else "CONTRACT_SATISFIED", result)
                if kind == "minor":
                    self.assertEqual(result["reasons"], ["attempt_limit"])
        request, observations, now = fixture("substantive")
        self.append_history(observations,
            self.repair_record(request, observations, "2026-11-08T01:05:00+09:00", kind="r3_minor"),
            self.repair_record(request, observations, "2026-11-08T02:05:00+09:00", serial=2))
        result = self.assert_hold(request, observations, now)
        self.assertEqual(result["reasons"], ["attempt_limit"])

    def test_second_substantive_that_day_blocks_another_root_too(self):
        request, observations, now = fixture("substantive")
        self.append_history(observations,
            self.repair_record(request, observations, "2026-11-08T01:05:00+09:00"))
        self.assertEqual(migration.evaluate(request, observations, now=now)["state"], "CONTRACT_SATISFIED")
        self.append_history(observations,
            self.initial_record(201, "2026-11-07T02:05:00+09:00"),
            self.repair_record(request, observations, "2026-11-08T02:05:00+09:00", serial=2, root=201, pr=301))
        result = self.assert_hold(request, observations, now)
        self.assertEqual(result["reasons"], ["attempt_limit"])

    def test_failed_active_ci_rerun_still_exhausts_that_exact_head(self):
        for prior_head in ("9" * 40, "2" * 40):
            with self.subTest(prior_head_same=prior_head == "2" * 40):
                request, observations, now = fixture("ci_rerun")
                self.append_history(observations, self.repair_record(request, observations,
                    "2026-11-08T01:05:00+09:00", kind="ci_rerun", head=prior_head))
                result = migration.evaluate(request, observations, now=now)
                self.assertEqual(result["state"], "HOLD" if prior_head == request["head_sha"]
                                 else "CONTRACT_SATISFIED", result)
                if prior_head == request["head_sha"]:
                    self.assertEqual(result["reasons"], ["attempt_limit"])

    def test_two_substantive_attempts_cannot_restart_on_a_new_day(self):
        request, observations, now = fixture("substantive")
        self.append_history(observations,
            self.repair_record(request, observations, "2026-11-07T02:05:00+09:00", attempt=1),
            self.repair_record(request, observations, "2026-11-07T03:05:00+09:00", attempt=2, serial=2))
        self.assertEqual(observations["history"].audit.effective_today, 0)
        result = self.assert_hold(request, observations, now)
        self.assertEqual(result["reasons"], ["attempt_limit"])

    def test_utc_clock_keeps_jst_budget_and_cutover_semantics(self):
        request, observations, now = fixture()
        jst_result = migration.evaluate(request, observations, now=now)
        utc_result = migration.evaluate(request, observations, now=now.astimezone(timezone.utc))
        self.assertEqual(jst_result["state"], "CONTRACT_SATISFIED")
        self.assertEqual(jst_result, utc_result)

    def test_expired_current_control_cannot_borrow_unexpired_boundary(self):
        request, observations, now = fixture()
        observations["current"]["source"]["expires_at"] = now.isoformat()
        result = self.assert_hold(request, observations, now)
        self.assertEqual(result["reasons"], ["evidence_expired_or_future"])


if __name__ == "__main__":
    unittest.main()
