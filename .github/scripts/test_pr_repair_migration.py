"""Synthetic M2 contract declarations: never production approval/quiescence."""
from copy import deepcopy
from contextlib import redirect_stderr, redirect_stdout
from dataclasses import replace
from datetime import datetime, timedelta
from pathlib import Path
import io
import json
import sys
import unittest
from unittest.mock import patch

sys.dont_write_bytecode = True
sys.path.insert(0, str(Path(__file__).resolve().parents[2] / "tools"))
import pr_repair_contract as contract
import pr_repair_history as history
import pr_repair_migration as migration


def fixture(kind="initial"):
    """Return independent-shaped but ENTIRELY SYNTHETIC declarations."""
    now = datetime(2026, 11, 8, 12, 5, tzinfo=migration.JST)
    def source(name, actor=101, at=None):
        return {"record_id": name, "actor_id": actor, "repo_id": migration.REPOSITORY_ID,
                "observed_at": (at or now).isoformat(),
                "expires_at": (now + timedelta(hours=1)).isoformat()}
    acceptance = [{"id": "a1", "expected_sha256": "a" * 64, "command_sha256": "b" * 64}]
    lineage = {"state": "independent_new", "ancestor_issues": [], "ancestor_prs": [],
               "prior_plans": [], "mixed": False, "source_refs": ["origin-one"]}
    pr = None if kind == "initial" else 102
    writers = [{"role": role, "id": role + "-writer", "deployment_sha": "c" * 40}
               for role in ("initial", "repair", "merge")]
    legacy = {"issues": [10, 56], "prs": [58], "purposes": ["f" * 64],
              "acceptance": ["e" * 64], "plans": ["F22"], "disposition": "unknown/HOLD"}
    writer_digest = contract.digest(sorted(writers, key=lambda x: x["role"]))
    request = {"schema": migration.SCHEMA, "repo_id": migration.REPOSITORY_ID, "policy": migration.POLICY,
               "policy_version": "M2", "generation": 3, "kind": kind,
               "root_issue": 101, "root_pr": pr, "purpose_sha256": "1" * 64,
               "acceptance_sha256": contract.digest(acceptance),
               "lineage_sha256": contract.digest(lineage), "head_sha": "2" * 40,
               "main_sha": "3" * 40,
               "writer_id": ("initial" if kind == "initial" else "repair") + "-writer",
               "deployment_sha": "c" * 40}
    policy = {"source": source("policy"), "policy": migration.POLICY, "version": "M2",
              "adopted_at": "2026-11-05T12:00:00+09:00", "owner_id": 101,
              "writers": writers, "legacy_scope": legacy}
    boundary = {"source": source("boundary"), "policy_version": "M2", "generation": 3,
                "ts": "2026-11-05T13:00:00+09:00", "tq": "2026-11-05T14:00:00+09:00",
                "quiet_start": "2026-11-06T00:00:00+09:00", "quiet_end": "2026-11-07T00:00:00+09:00",
                "ta": "2026-11-07T00:00:00+09:00", "writers_sha256": writer_digest,
                "prerequisites_sha256": "4" * 64, "legacy_scope_sha256": contract.digest(legacy),
                "local_state": "quiescent", "cloud_state": "terminated", "pending_dispatch": 0,
                "pending_writes": 0, "locks": "observed_exclusive", "old_activity_at": None,
                "observation_gaps": [], "unknown_days": ["2026-11-05"], "unknown_hours": []}
    current = {"source": source("current"), "policy_version": "M2", "generation": 3,
               "highest_generation": 3, "revoked_generations": [1, 2],
               "writers_sha256": writer_digest, "prerequisites_sha256": "4" * 64,
               "legacy_scope_sha256": contract.digest(legacy), "active_old_writers": [],
               "pending_old_writes": 0, "old_activity_at": None, "observation_gaps": [],
               "post_ta_history": "complete", "unknown_days": [], "unknown_hours": []}
    target = {"source": source("target", 202), "root_issue": 101, "root_pr": pr,
              "author_id": 303, "purpose_sha256": request["purpose_sha256"],
              "acceptance_sha256": request["acceptance_sha256"], "lineage_sha256": request["lineage_sha256"],
              "needed_at": "2026-11-07T00:06:00+09:00", "issue_created_at": "2026-11-07T00:07:00+09:00",
              "approved_at": "2026-11-07T00:08:00+09:00",
              "pr_created_at": None if pr is None else "2026-11-07T01:10:00+09:00",
              "issue_title_sha256": "8" * 64, "issue_body_sha256": "9" * 64,
              "plan_id": None, "lineage": lineage}
    approval = {"source": source("approval"), "root_issue": 101, "root_pr": pr,
                "purpose_sha256": request["purpose_sha256"], "lineage_sha256": request["lineage_sha256"],
                "acceptance": acceptance, "paths": ["src/Example.cs"], "max_files": 3,
                "max_lines": 600, "approved_at": target["approved_at"],
                "reservation_id": 700, "result_id": 701,
                "issue_title_sha256": "8" * 64, "issue_body_sha256": "9" * 64}
    review = {"source": source("review", 202), "run_source": source("review-run", 202),
              "root_issue": 101, "root_pr": pr, "head_sha": request["head_sha"],
              "approval_sha256": contract.digest(approval), "lineage_sha256": request["lineage_sha256"],
              "reviewer_run_id": "review-run", "state": "COMMENTED",
              "workflow_run_id": 400, "workflow_attempt": 1, "job_id": 500}
    receipt = {"source": source("acceptance-run", 202), "head_sha": request["head_sha"],
               "workflow_run_id": 400, "workflow_attempt": 1, "job_id": 500, "reviewer_id": 202,
               "phase": "baseline" if kind == "initial" else "current_head",
               "results": [{"acceptance_id": "a1", "test_id": "test-a1", "expected_sha256": "a" * 64,
                            "command_sha256": "b" * 64, "status": "failed", "evidence_sha256": "5" * 64}]}
    records = ()
    sources = ()
    if kind != "initial":
        record_source = history.HistorySource("daily/day-2026-11-07-hour-1.json", "6" * 64, "schema_validated")
        marker_source = history.HistorySource("daily/issue-101.json", "a" * 64, "schema_validated")
        result_source = history.HistorySource("daily/result-2026-11-07-hour-1.json", "b" * 64, "schema_validated")
        record = history.HistoryRecord(record_source.source_id, "initial_daily", 101, None,
                   record_source.source_id, "2026-11-07T01:05:00+09:00", "2026-11-07", (1,),
                   "recorded_success", record_source.source_sha256, (record_source, marker_source, result_source), ())
        records, sources = (record,), (record_source, marker_source, result_source)
    audit = history.HistoryAudit(True, len(records), 0, 0, 0, 0,
               frozenset(r.root_issue for r in records), 0, (migration.LEGACY_HOLD,), "7" * 64)
    local = history.HistoryRecords(audit, records, sources, now.isoformat(), "7" * 64)
    binding = {"source": source("history"), "snapshot_sha256": "7" * 64,
               "records_sha256": contract.digest(local.to_dict()["records"]),
               "record_ids": [r.source_id for r in records], "coverage_from": boundary["ta"],
               "coverage_through": now.isoformat(), "lifetime_complete": True}
    inventory = {"source": source("inventory"), "complete": True, "open_prs": [] if pr is None else [pr],
                 "root_map": [] if pr is None else [[101, pr]], "head_sha": request["head_sha"],
                 "main_sha": request["main_sha"]}
    scope = {"source": source("scope", 202), "root_issue": 101, "root_pr": pr,
             "head_sha": request["head_sha"], "approval_sha256": contract.digest(approval),
             "paths": ["src/Example.cs"], "changed_lines": 20,
             "initial_paths": ["src/Example.cs"], "ci_rerun_authorization": None}
    if kind == "ci_rerun":
        scope["ci_rerun_authorization"] = {"source": source("ci-grant"), "head_sha": request["head_sha"]}
    return request, {"policy": policy, "boundary": boundary, "current": current,
                     "target": target, "approval": approval, "review": review, "acceptance": receipt,
                     "history": local, "history_binding": binding, "inventory": inventory, "scope": scope}, now


def rebind_history(obs, local):
    obs["history"] = local
    obs["history_binding"]["records_sha256"] = contract.digest(local.to_dict()["records"])
    obs["history_binding"]["record_ids"] = [r.source_id for r in local.records]


class MigrationTests(unittest.TestCase):
    def evaluate(self, kind="initial", changes=None):
        request, obs, now = fixture(kind)
        if changes:
            changes(request, obs)
        return migration.evaluate(request, obs, now=now)

    def test_all_kinds_are_conditional_only(self):
        for kind in migration.KINDS:
            with self.subTest(kind=kind):
                result = self.evaluate(kind)
                self.assertEqual(result["state"], "CONTRACT_SATISFIED", result)
                self.assertEqual(result["authentication"], "not_established")
                self.assertEqual(result["legacy_history"], "unknown/HOLD")
                self.assertEqual(result["acceptance_success"], "not_asserted")
                for key in ("may_reserve", "may_publish", "may_activate", "may_repair", "may_merge", "production_enabled"):
                    self.assertIs(result[key], False)

    def test_old_and_mixed_work_rejected(self):
        for mutate in (
            lambda r, o: o["target"].update(plan_id="F22"),
            lambda r, o: o["target"].update(needed_at="2026-11-01T00:00:00+09:00"),
            lambda r, o: o["target"]["lineage"].update(mixed=True),
            lambda r, o: o["target"]["lineage"].update(state="unknown"),
            lambda r, o: o["target"]["lineage"].update(ancestor_issues=[56]),
        ):
            self.assertEqual(self.evaluate(changes=mutate)["state"], "HOLD")

    def test_stopped_setting_is_not_quiescence(self):
        for key, value in (("local_state", "disabled"), ("cloud_state", "quiet"),
                           ("pending_dispatch", 1), ("pending_writes", 1), ("locks", "file_exists")):
            result = self.evaluate(changes=lambda r, o: o["boundary"].update({key: value}))
            self.assertEqual(result["state"], "HOLD")

    def test_generation_recurrence_and_prerequisites(self):
        for key, value in (("generation", 4), ("highest_generation", 4),
                           ("revoked_generations", [1, 2, 3]), ("old_activity_at", "2026-11-08T00:00:00+09:00"),
                           ("observation_gaps", ["gap"]), ("active_old_writers", ["legacy"]),
                           ("prerequisites_sha256", "8" * 64), ("post_ta_history", "unknown")):
            result = self.evaluate(changes=lambda r, o: o["current"].update({key: value}))
            self.assertEqual(result["state"], "HOLD")

    def test_unknown_windows_are_never_free(self):
        for key, value in (("unknown_days", ["2026-11-08"]),
                           ("unknown_hours", ["2026-11-08T12:00:00+09:00"])):
            result = self.evaluate(changes=lambda r, o: o["current"].update({key: value}))
            self.assertEqual(result["reasons"], ["budget_window_unknown"])

    def test_unknown_hour_anywhere_on_jst_day_blocks_daily_budget(self):
        for section in ("boundary", "current"):
            for unknown in ("2026-11-08T10:00:00+09:00", "2026-11-08T18:00:00+09:00",
                            "2026-11-07T15:00:00+00:00", "2026-11-08T01:00:00+00:00"):
                with self.subTest(section=section, hour=unknown):
                    request, obs, now = fixture()
                    obs[section]["unknown_hours"] = [unknown]
                    before = deepcopy(obs)
                    result = migration.evaluate(request, obs, now=now)
                    self.assertEqual(result["reasons"], ["budget_window_unknown"])
                    self.assertEqual(obs, before)
                    for key in ("may_reserve", "may_publish", "may_activate", "may_repair", "may_merge", "production_enabled"):
                        self.assertIs(result[key], False)

    def test_unknown_hour_on_different_jst_day_remains_quarantined(self):
        for section in ("boundary", "current"):
            request, obs, now = fixture()
            # This UTC date is today, but the actual JST date is tomorrow.
            obs[section]["unknown_hours"] = ["2026-11-08T15:00:00+00:00", "2026-11-07T10:00:00+09:00"]
            before = deepcopy(obs)
            result = migration.evaluate(request, obs, now=now)
            self.assertEqual(result["state"], "CONTRACT_SATISFIED")
            self.assertEqual(result["legacy_history"], "unknown/HOLD")
            self.assertEqual(obs, before)

    def test_known_revoked_generation_cannot_exceed_high_water(self):
        request, obs, now = fixture()
        obs["current"]["revoked_generations"] = [1, 2, 4]
        before = deepcopy(obs)
        result = migration.evaluate(request, obs, now=now)
        self.assertEqual(result["reasons"], ["generation_history_contradiction"])
        self.assertEqual(obs, before)
        for key in ("may_reserve", "may_publish", "may_activate", "may_repair", "may_merge", "production_enabled"):
            self.assertIs(result[key], False)

    def test_global_open_inventory(self):
        for kind in ("initial", "minor"):
            for change in ({"complete": False}, {"open_prs": [58]}, {"open_prs": [102, 58]}):
                result = self.evaluate(kind, lambda r, o: o["inventory"].update(change))
                self.assertEqual(result["state"], "HOLD")

    def test_missing_acceptance_or_self_review(self):
        for mutate in (
            lambda r, o: o["review"]["source"].update(actor_id=303),
            lambda r, o: o["approval"]["source"].update(actor_id=303),
            lambda r, o: o["acceptance"]["results"].clear(),
            lambda r, o: o["acceptance"]["results"][0].update(status="not_run"),
            lambda r, o: o["acceptance"].update(workflow_attempt=2),
            lambda r, o: o["acceptance"]["results"][0].update(expected_sha256="9" * 64),
        ):
            self.assertEqual(self.evaluate(changes=mutate)["state"], "HOLD")

    def test_local_corruption_is_not_provider_exception(self):
        request, obs, now = fixture()
        local = obs["history"]
        rebind_history(obs, replace(local, audit=replace(local.audit, holds=("orphaned_manual_history",))))
        self.assertEqual(migration.evaluate(request, obs, now=now)["reasons"], ["local_history_invalid"])

    def test_minor_scope_limits(self):
        for mutation in ({"changed_lines": 81}, {"paths": ["src/New.cs"]}, {"initial_paths": []}):
            self.assertEqual(self.evaluate("minor", lambda r, o: o["scope"].update(mutation))["state"], "HOLD")

    def test_expiry_does_not_issue_new_boundary(self):
        request, obs, now = fixture()
        obs["boundary"]["source"]["expires_at"] = now.isoformat()
        before = deepcopy(obs)
        self.assertEqual(migration.evaluate(request, obs, now=now)["state"], "HOLD")
        self.assertEqual(obs, before)

    def test_old_repair_ci_cannot_skip_publication_causal_order(self):
        request, obs, now = fixture("substantive")
        rid = "a" * 32
        src = history.HistorySource(f"pr-repair-ledger/reservation-{rid}.json", "a" * 64, "schema_validated")
        event_src = history.HistorySource(f"pr-repair-ledger/event-{rid}-01.json", "b" * 64, "schema_validated")
        event = history.HistoryEvent(event_src.source_id, event_src.source_sha256, "ci_success",
                                     "2026-11-07T02:06:00+09:00", request["head_sha"], None)
        bad = history.HistoryRecord(src.source_id, "r3_substantive", 101, 102, rid,
                   "2026-11-07T02:05:00+09:00", "2026-11-07", (2,), "recorded_success",
                   src.source_sha256, (src, event_src), (event,), (), 1, request["head_sha"], None, 700, 701)
        local = obs["history"]
        rebind_history(obs, replace(local, records=local.records + (bad,), sources=local.sources + (src, event_src),
                                   audit=replace(local.audit, repair_count=1)))
        result = migration.evaluate(request, obs, now=now)
        self.assertEqual(result["reasons"], ["history_event_conflict"])

    def test_two_old_reservations_cannot_claim_same_pr_head(self):
        request, obs, now = fixture()
        records, sources = [], []
        for attempt, hour in ((1, 2), (2, 3)):
            rid = str(attempt) * 32
            src = history.HistorySource(f"pr-repair-ledger/reservation-{rid}.json", "a" * 64, "schema_validated")
            event_sources = tuple(history.HistorySource(f"pr-repair-ledger/event-{rid}-{seq:02d}.json",
                                  str(seq) * 64, "schema_validated") for seq in (1, 2))
            events = tuple(history.HistoryEvent(s.source_id, s.source_sha256, name,
                                  f"2026-11-04T{hour:02d}:06:00+09:00", "b" * 40, None)
                           for s, name in zip(event_sources, ("publish_started", "failed")))
            records.append(history.HistoryRecord(src.source_id, "r3_substantive", 201, 301, rid,
                           f"2026-11-04T{hour:02d}:05:00+09:00", "2026-11-04", (hour,), "failed",
                           src.source_sha256, (src, *event_sources), events, (), attempt, "b" * 40, None, 800, 801))
            sources.extend((src, *event_sources))
        local = obs["history"]
        rebind_history(obs, replace(local, records=tuple(records), sources=tuple(sources),
                                   audit=replace(local.audit, repair_count=2)))
        before = deepcopy(obs)
        result = migration.evaluate(request, obs, now=now)
        self.assertEqual(result["reasons"], ["repair_claim_conflict"])
        self.assertEqual(obs, before)
        for key in ("may_reserve", "may_publish", "may_activate", "may_repair", "may_merge", "production_enabled"):
            self.assertIs(result[key], False)

    def test_quiet_day_is_a_complete_jst_day_not_elapsed_timeout(self):
        for change in ({"quiet_start": "2026-11-05T15:00:00+09:00"},
                       {"quiet_end": "2026-11-06T01:00:00+09:00"},
                       {"ta": "2026-11-06T00:00:00+09:00"}):
            result = self.evaluate(changes=lambda r, o: o["boundary"].update(change))
            self.assertEqual(result["reasons"], ["invalid_cutover_window"])

    def test_scope_observer_cannot_be_candidate_author(self):
        result = self.evaluate(changes=lambda r, o: o["scope"]["source"].update(actor_id=303))
        self.assertEqual(result["reasons"], ["scope_review_unverified"])

    def test_scope_cannot_be_transplanted_from_another_root_head_or_approval(self):
        for key, value in (("root_issue", 999), ("root_pr", 999),
                           ("head_sha", "f" * 40), ("approval_sha256", "e" * 64)):
            with self.subTest(field=key):
                result = self.evaluate("minor", lambda r, o: o["scope"].update({key: value}))
                self.assertEqual(result["reasons"], ["scope_binding_mismatch"])

    def test_receipt_numeric_ids_reject_bool_and_float_equivalence(self):
        for key in ("workflow_run_id", "workflow_attempt", "job_id", "reviewer_id"):
            for malformed in (True, "float"):
                with self.subTest(field=key, malformed=malformed):
                    request, obs, now = fixture()
                    # Use exact equal numbers, so equality alone would accept.
                    if malformed is True:
                        if key == "reviewer_id":
                            obs["review"]["source"]["actor_id"] = 1
                            obs["review"]["run_source"]["actor_id"] = 1
                            obs["target"]["source"]["actor_id"] = 1
                            obs["scope"]["source"]["actor_id"] = 1
                            obs["acceptance"]["source"]["actor_id"] = 1
                        else:
                            obs["review"][key] = 1
                        obs["acceptance"][key] = True
                    else:
                        obs["acceptance"][key] = float(obs["acceptance"][key])
                    result = migration.evaluate(request, obs, now=now)
                    self.assertEqual(result["state"], "HOLD")
                    self.assertEqual(result["reasons"], ["invalid_or_incomplete_contract"])
                    for permission in ("may_reserve", "may_publish", "may_activate", "may_repair", "may_merge", "production_enabled"):
                        self.assertIs(result[permission], False)

    def test_joined_root_ids_reject_bool_and_float_equivalence(self):
        for section in ("target", "approval", "review", "scope"):
            for key in ("root_issue", "root_pr"):
                for malformed in (True, "float"):
                    with self.subTest(section=section, field=key, malformed=malformed):
                        request, obs, now = fixture("initial" if key == "root_issue" else "minor")
                        if malformed is True:
                            request[key] = 1
                            for other in ("target", "approval", "review", "scope"):
                                obs[other][key] = 1
                            if key == "root_pr":
                                obs["inventory"]["root_map"] = [[101, 1]]
                                obs["inventory"]["open_prs"] = [1]
                            obs["review"]["approval_sha256"] = contract.digest(obs["approval"])
                            obs["scope"]["approval_sha256"] = contract.digest(obs["approval"])
                            obs[section][key] = True
                        else:
                            obs[section][key] = float(obs[section][key])
                        result = migration.evaluate(request, obs, now=now)
                        self.assertEqual(result["state"], "HOLD")
                        self.assertEqual(result["reasons"], ["invalid_or_incomplete_contract"])
                        for permission in ("may_reserve", "may_publish", "may_activate", "may_repair", "may_merge", "production_enabled"):
                            self.assertIs(result[permission], False)

    def test_consumed_issue_set_rejects_boolean_alias_for_issue_one(self):
        request, obs, now = fixture("minor")
        request["root_issue"] = 1
        for section in ("target", "approval", "review", "scope"):
            obs[section]["root_issue"] = 1
        obs["inventory"]["root_map"] = [[1, 102]]
        obs["review"]["approval_sha256"] = contract.digest(obs["approval"])
        obs["scope"]["approval_sha256"] = contract.digest(obs["approval"])
        local = obs["history"]
        converted = tuple(replace(s, source_id="daily/issue-1.json")
                          if s.source_id == "daily/issue-101.json" else s for s in local.sources)
        original = local.records[0]
        record = replace(original, root_issue=1, evidence=converted)
        rebind_history(obs, replace(local, records=(record,), sources=converted,
                                   audit=replace(local.audit, consumed_issues=frozenset((True,)))))
        result = migration.evaluate(request, obs, now=now)
        self.assertEqual(result["reasons"], ["history_counts_mismatch"])
        for permission in ("may_reserve", "may_publish", "may_activate", "may_repair", "may_merge", "production_enabled"):
            self.assertIs(result[permission], False)

    def test_invalid_legacy_reservation_name_returns_safe_hold(self):
        request, obs, now = fixture("minor")
        local = obs["history"]
        malformed = "daily/day-2026-11-07-hour-99.json"
        old = local.records[0]
        converted = tuple(replace(s, source_id=malformed) if s.source_id == old.source_id else s
                          for s in local.sources)
        record = replace(old, source_id=malformed, reservation_id=malformed, evidence=converted)
        rebind_history(obs, replace(local, records=(record,), sources=converted))
        before = deepcopy((request, obs))
        output = io.StringIO()
        with redirect_stdout(output), redirect_stderr(output):
            result = migration.evaluate(request, obs, now=now)
        self.assertEqual(result["reasons"], ["local_history_invalid"])
        self.assertEqual((request, obs), before)
        self.assertEqual(output.getvalue(), "")
        self.assertNotIn(malformed, json.dumps(result))
        for permission in ("may_reserve", "may_publish", "may_activate", "may_repair", "may_merge", "production_enabled"):
            self.assertIs(result[permission], False)

    def test_legacy_parser_runtime_error_text_never_leaks(self):
        request, obs, now = fixture("minor")
        before = deepcopy((request, obs))
        sentinel = "secret-sentinel-from-existing-parser"
        output = io.StringIO()
        with patch.object(history.hourly, "_reservation_identity", side_effect=RuntimeError(sentinel)), \
                redirect_stdout(output), redirect_stderr(output):
            result = migration.evaluate(request, obs, now=now)
        self.assertEqual(result["reasons"], ["local_history_invalid"])
        self.assertNotIn(sentinel, json.dumps(result) + output.getvalue())
        self.assertEqual(output.getvalue(), "")
        self.assertEqual((request, obs), before)
        for permission in ("may_reserve", "may_publish", "may_activate", "may_repair", "may_merge", "production_enabled"):
            self.assertIs(result[permission], False)

    def test_unexpected_side_effect_assertions_are_not_swallowed(self):
        request, obs, now = fixture("minor")
        with patch.object(history.hourly, "_reservation_identity", side_effect=AssertionError("forbidden IO")):
            with self.assertRaises(AssertionError):
                migration.evaluate(request, obs, now=now)


if __name__ == "__main__":
    unittest.main()
