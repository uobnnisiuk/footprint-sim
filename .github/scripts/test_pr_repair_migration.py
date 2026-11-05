"""Synthetic M2 contract declarations: never production approval/quiescence."""
from copy import deepcopy
from dataclasses import replace
from datetime import datetime, timedelta
from pathlib import Path
import sys
import unittest

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


if __name__ == "__main__":
    unittest.main()
