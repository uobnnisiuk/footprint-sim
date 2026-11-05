#!/usr/bin/env python3
"""Offline integration tests; no source credentials, network or production state."""
from copy import deepcopy
from dataclasses import asdict, replace
from datetime import datetime, timedelta, timezone
import hashlib
import json
import os
from pathlib import Path
import sys
import tempfile
import unittest
from unittest.mock import patch

sys.path.insert(0, str(Path(__file__).resolve().parents[2] / "tools"))
import pr_repair_adapter as adapter
import pr_repair_evidence as evidence
import pr_repair_history as history
import pr_repair_provenance as provenance
import pr_repair_remote_audit as remote
import pr_repair_snapshot as snapshot
import pr_repair_sources as sources

NOW = datetime(2026, 11, 5, 0, 0, tzinfo=timezone.utc)
AT = "2026-11-04T00:00:00Z"
HEAD = "a" * 40
MAIN = "b" * 40
SECRET = "sentinel-not-for-diagnostic-output"


def issue(number, *, is_pr=False, comments=0):
    value = {"id": number + 100, "number": number, "url": remote.API + "issues/" + str(number),
             "user": {"id": 17}, "created_at": AT, "updated_at": AT,
             "comments": comments, "title": SECRET, "body": SECRET}
    if is_pr:
        value["pull_request"] = {"url": remote.API + "pulls/" + str(number)}
    return value


def pull():
    return {"id": 102, "number": 2, "url": remote.API + "pulls/2", "user": {"id": 17},
            "created_at": AT, "updated_at": AT, "state": "closed", "merged": True,
            "head": {"sha": HEAD, "ref": "local-autodev/issue-1", "repo": {"full_name": remote.REPO}},
            "base": {"sha": MAIN, "ref": "main", "repo": {"full_name": remote.REPO}},
            "body": SECRET, "title": SECRET}


def comment(identity, body, parent=19):
    return {"id": identity, "url": remote.API + "issues/comments/" + str(identity),
            "issue_url": remote.API + "issues/" + str(parent), "user": {"id": 17},
            "created_at": AT, "updated_at": AT, "body": body}


def reservation(identity=200):
    return comment(identity, "Queue-Approval-Reservation: footprint-m1b-v2\nIssue-Number: 1\n"
                   "Review-Main-SHA: " + MAIN + "\nIssue-Title-SHA256: " + "c" * 64 +
                   "\nIssue-Body-SHA256: " + "d" * 64 + "\nAcceptance: " + SECRET)


def result(identity=201, reservation_id=200, outcome="success"):
    return comment(identity, "Queue-Approval-Result: footprint-m1b-v2\nIssue-Number: 1\n"
                   f"Reservation-ID: {reservation_id}\nOutcome: {outcome}\nReview-Main-SHA: " +
                   MAIN + "\nIssue-Title-SHA256: " + "c" * 64 + "\nIssue-Body-SHA256: " + "d" * 64)


class Reader:
    def __init__(self, comments=(), *, ci=False):
        self.comments = list(comments)
        self.ci = ci
        self.calls = []
        self.rerun = False
        self.run_reads = 0
        self.list_reads = 0
        self.edit_on_pass = False
        self.inventory_reads = 0

    def get(self, route, params=None):
        self.calls.append((route, params))
        page = (params or {}).get("page", 1)
        if route == "":
            return {"id": 99, "full_name": remote.REPO}
        if route == "git/ref/heads/main":
            return {"object": {"sha": MAIN, "type": "commit"}}
        if route == "pulls/2":
            return pull()
        if route == "issues":
            return [issue(1), issue(2, is_pr=True, comments=sum(c["issue_url"].endswith("/2") for c in self.comments)),
                    issue(19, comments=sum(c["issue_url"].endswith("/19") for c in self.comments))] if page == 1 else []
        if route == "pulls":
            return [pull()] if page == 1 else []
        if route == "issues/comments":
            self.inventory_reads += 1
            items = deepcopy(self.comments)
            if self.edit_on_pass and self.inventory_reads == 2 and items:
                items[0]["body"] += " edited"
            return items if page == 1 else []
        if route == "pulls/2/commits":
            return [{"sha": HEAD, "url": remote.API + "commits/" + HEAD,
                     "author": {"id": 17}, "commit": {"message": SECRET}}] if page == 1 else []
        if route in ("pulls/2/reviews", "pulls/2/comments", "issues/2/timeline"):
            return []
        if route == "actions/workflows/ci.yml/runs":
            self.list_reads += 1
            return {"total_count": 1, "workflow_runs": [self.run()] if self.ci and page == 1 else []}
        if route == "actions/runs/300":
            self.run_reads += 1
            value = self.run()
            if self.rerun and self.run_reads > 1:
                value["run_attempt"] = 2
            return value
        if route == "actions/runs/300/attempts/1/jobs":
            return {"total_count": 1, "jobs": [{"id": 400, "name": "test", "run_id": 300,
                     "head_sha": HEAD, "run_attempt": 1, "status": "completed", "conclusion": "success",
                     "steps": [{"name": n, "status": "completed", "conclusion": "success"}
                               for n in evidence.REQUIRED_STEPS["normal"]]}] if page == 1 else []}
        raise OSError(SECRET)

    def run(self):
        return {"id": 300, "head_sha": HEAD, "head_branch": "local-autodev/issue-1",
                "event": "pull_request", "path": ".github/workflows/ci.yml", "run_attempt": 1,
                "status": "completed", "conclusion": "success", "repository": {"full_name": remote.REPO},
                "head_repository": {"full_name": remote.REPO},
                "pull_requests": [{"number": 2, "head": {"sha": HEAD, "ref": "local-autodev/issue-1", "repo": {"url": remote.API.rstrip("/")}},
                                   "base": {"ref": "main", "repo": {"url": remote.API.rstrip("/")}}}]}


def collect(reader, **kwargs):
    return snapshot.collect_evidence(reader, clock=lambda: NOW, **kwargs)


class SnapshotTests(unittest.TestCase):
    def local_record(self, **changes):
        src = history.HistorySource("pr-repair-ledger/reservation-" + "a" * 32 + ".json", "c" * 64, "observed")
        record = history.HistoryRecord(src.source_id, "r3_minor", 1, 2, "a" * 32,
                    NOW.isoformat(), "2026-11-05", (9,), "unknown", src.source_sha256,
                    (src,), (), approval_reservation_id=200, approval_result_id=201)
        return replace(record, **changes)

    def test_observed_zero_is_not_unknown_consumption_or_authority(self):
        value = collect(Reader()).to_dict()
        self.assertTrue(value["sources"]["visible_inventory_complete"])
        self.assertEqual(value["legacy_minor"]["observed_reservations"], 0)
        self.assertEqual(value["legacy_minor"]["consumption"], "unknown")
        self.assertFalse(value["legacy_minor"]["execution_history_complete"])
        for name in ("may_reserve", "may_publish", "may_activate", "production_enabled"):
            self.assertIs(value[name], False)
        self.assertEqual(value["r4_connection"], "not_connected")

    def test_queue_results_are_linked_but_not_model_reservations(self):
        value = collect(Reader([reservation(), result()]))
        queue = [a for a in value.attempts if a.source_kind == "queue_approval"]
        self.assertEqual(len(queue), 1)
        self.assertEqual(queue[0].reservation_sources, ("github:issue_comment:200",))
        self.assertEqual(queue[0].result_sources, ("github:issue_comment:201",))
        self.assertEqual(queue[0].outcome, "recorded_success")
        self.assertEqual(queue[0].consumption, "approval_only")
        self.assertEqual(value.approval_state, "unverified")

    def test_orphan_failed_unknown_and_duplicate_results_survive(self):
        value = collect(Reader([reservation(), result(outcome="failed"),
                                result(202, outcome="unknown"), result(203, 999)]))
        queue = [a for a in value.attempts if a.source_kind == "queue_approval"]
        self.assertEqual(len(queue), 2)
        self.assertEqual(queue[0].outcome, "conflict")
        self.assertEqual(len(queue[0].result_sources), 2)
        self.assertIn("duplicate_approval_result", queue[0].reasons)
        self.assertIn("orphan_approval_result", queue[1].reasons)

    def test_reserved_without_result_stays_unknown(self):
        value = collect(Reader([reservation()]))
        queue = next(a for a in value.attempts if a.source_kind == "queue_approval")
        self.assertEqual(queue.outcome, "unknown")
        self.assertIn("approval_result_missing", queue.reasons)

    def test_distinct_reservations_for_same_issue_are_not_a_fresh_attempt(self):
        value = collect(Reader([reservation(), reservation(205)]))
        queue = [a for a in value.attempts if a.source_kind == "queue_approval"]
        self.assertEqual(len(queue), 2)
        self.assertTrue(all("duplicate_approval_reservation" in a.reasons for a in queue))
        self.assertTrue(all(a.consumption == "approval_only" for a in queue))

    def test_approval_review_link_must_resolve_to_original_issue(self):
        receipt = result()
        receipt["body"] += "\nApproval-Review-Comment-ID: 210"
        value = collect(Reader([reservation(), receipt, comment(210, SECRET, 2)]))
        queue = next(a for a in value.attempts if a.source_kind == "queue_approval")
        self.assertIn("approval_review_parent_conflict", queue.reasons)
        self.assertEqual(queue.outcome, "conflict")

    def test_changed_receipt_is_conflict_and_both_versions_retained(self):
        reader = Reader([reservation(), result()]); reader.edit_on_pass = True
        value = collect(reader)
        self.assertFalse(value.source_collection.visible_inventory_complete)
        self.assertEqual(sum(r.record_id == "200" for r in value.source_collection.records), 2)
        queue = next(a for a in value.attempts if a.source_kind == "queue_approval")
        self.assertEqual(queue.outcome, "conflict")

    def test_unclassified_legacy_marker_is_not_new_reservation_schema(self):
        value = collect(Reader([comment(209, "footprint-pr-repair-v1\n" + SECRET, 2)]))
        claim = next(a for a in value.attempts if a.source_kind == "legacy_minor_claim")
        self.assertEqual(claim.consumption, "unknown")
        self.assertEqual(claim.reservation_sources, ())
        self.assertIsNone(value.to_dict()["legacy_minor"]["observed_reservations"])

    def test_commits_do_not_infer_model_calls_or_manual_success(self):
        value = collect(Reader())
        intervention = next(a for a in value.attempts if a.source_kind == "unclassified_git_intervention")
        self.assertEqual(intervention.consumption, "unknown")
        self.assertEqual(intervention.outcome, "unknown")

    def test_secret_payloads_and_exception_text_not_serialized(self):
        value = collect(Reader([reservation(), result()]), root_issue=1, pr_number=2)
        self.assertNotIn(SECRET, snapshot.render_diagnostic(value))
        self.assertNotIn(SECRET, repr(value))
        self.assertNotIn(SECRET, repr(asdict(value)))
        self.assertNotIn("Acceptance:", snapshot.render_diagnostic(value))

    def test_green_ci_is_not_acceptance(self):
        value = collect(Reader(ci=True), root_issue=1, pr_number=2)
        self.assertEqual(value.ci["state"], "success_metadata_only")
        self.assertEqual(value.to_dict()["semantic_acceptance"], "unverified")
        self.assertIn(("unavailable", "acceptance_receipts_unavailable"), value.diagnostics)

    def test_same_head_new_attempt_invalidates_ci(self):
        reader = Reader(ci=True); reader.rerun = True
        value = collect(reader, root_issue=1, pr_number=2)
        self.assertEqual(value.ci["state"], "unobserved")
        self.assertIn(("unverified", "ci_metadata_unverified_or_changed"), value.diagnostics)

    def test_same_account_comment_does_not_authenticate_review(self):
        anchor = provenance.ReviewAnchor(2, 900, HEAD, 17, "c" * 64, AT, "claimed-agent-run")
        value = collect(Reader(), root_issue=1, pr_number=2, review_anchor=anchor)
        self.assertEqual(value.review_state, "unverified")
        self.assertFalse(value.formal_approval)
        self.assertIn(("unverified", "review_provenance_mismatch"), value.diagnostics)

    def test_free_text_and_caller_manifest_do_not_make_approval(self):
        value = collect(Reader([reservation(), result()]), root_issue=1, pr_number=2,
                        approval={"approved": True, "acceptance": SECRET})
        self.assertEqual(value.approval_state, "unverified")
        self.assertIn(("unverified", "approval_anchor_required"), value.diagnostics)

    def test_reused_provenance_validator_is_called_for_supplied_anchor(self):
        anchor = provenance.ApprovalAnchor(1, 2, 19, "footprint-f20-v1", MAIN, MAIN, "c" * 64,
                     "d" * 64, None, None, None, "e" * 64)
        with patch.object(provenance, "verify_approval", side_effect=provenance.ProvenanceHold(SECRET)) as check:
            value = collect(Reader(), root_issue=1, pr_number=2, approval_anchor=anchor, approval={})
        check.assert_called_once()
        self.assertIn(("unverified", "approval_provenance_mismatch"), value.diagnostics)
        self.assertNotIn(SECRET, snapshot.render_diagnostic(value))

    def test_no_files_locks_processes_or_network_for_injected_collection(self):
        with patch("builtins.open", side_effect=AssertionError("unexpected file access")), \
             patch("os.open", side_effect=AssertionError("unexpected fd access")), \
             patch("subprocess.Popen", side_effect=AssertionError("unexpected process")), \
             patch("socket.socket", side_effect=AssertionError("unexpected socket")), \
             patch("fcntl.flock", side_effect=AssertionError("unexpected lock")):
            value = collect(Reader([reservation(), result()]))
        self.assertEqual(value.to_dict()["state"], "HOLD")

    def test_stale_day_hides_counts_but_preserves_source_evidence(self):
        audit = history.HistoryAudit(True, 1, 0, 0, 1, 1, frozenset({1}), 1, (), "c" * 64)
        local = history.HistoryRecords(audit, (), (history.HistorySource("daily/day-2026-11-05-hour-09.json", "d" * 64, "observed"),), NOW.isoformat(), "c" * 64)
        value = replace(collect(Reader()), local_history=local)
        value = snapshot.validate_evidence_bundle(value, now=NOW + timedelta(days=1))
        output = value.to_dict()
        self.assertIsNone(output["local_history"]["audit"]["effective_today"])
        self.assertFalse(output["local_history"]["observation_day_matches_validation"])
        self.assertFalse(output["local_history"]["counts_are_available_credit"])
        self.assertEqual(len(output["local_history"]["sources"]), 1)

    def test_local_reader_integration_never_opens_for_write_or_acquires_lock(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            (root / "daily").mkdir(mode=0o700)
            (root / "daily" / "daily.lock").touch(mode=0o600)
            before = {str(p): (p.stat().st_mtime_ns, p.stat().st_ctime_ns, p.stat().st_size)
                      for p in (root, root / "daily", root / "daily" / "daily.lock")}
            original = os.open
            def readonly(path, flags, *args, **kwargs):
                self.assertEqual(flags & (os.O_WRONLY | os.O_RDWR | os.O_CREAT | os.O_TRUNC | os.O_APPEND), 0)
                return original(path, flags, *args, **kwargs)
            with patch("os.open", side_effect=readonly), \
                 patch("fcntl.flock", side_effect=AssertionError("unexpected lock")), \
                 patch("subprocess.Popen", side_effect=AssertionError("unexpected subprocess")), \
                 patch("socket.socket", side_effect=AssertionError("unexpected socket")):
                value = collect(Reader(), state_root=root)
            self.assertTrue(value.local_history.audit.local_valid)
            self.assertEqual(value.local_history.audit.effective_today, 0)
            self.assertEqual(value.to_dict()["legacy_minor"]["consumption"], "unknown")
            after = {str(p): (p.stat().st_mtime_ns, p.stat().st_ctime_ns, p.stat().st_size)
                     for p in (root, root / "daily", root / "daily" / "daily.lock")}
            self.assertEqual(before, after)

    def test_clock_regression_does_not_refund_or_forget_sources(self):
        audit = history.HistoryAudit(True, 1, 0, 0, 1, 1, frozenset({1}), 1, (), "c" * 64)
        local = history.HistoryRecords(audit, (), (history.HistorySource("daily/issue-1.json", "d" * 64, "observed"),), NOW.isoformat(), "c" * 64)
        value = snapshot.validate_evidence_bundle(replace(collect(Reader()), local_history=local),
                                                  now=NOW - timedelta(seconds=1))
        self.assertIsNone(value.to_dict()["local_history"]["audit"]["effective_today"])
        self.assertEqual(len(value.local_history.sources), 1)

    def test_previous_hour_count_is_not_reused(self):
        audit = history.HistoryAudit(True, 1, 0, 0, 1, 1, frozenset({1}), 1, (), "c" * 64)
        local = history.HistoryRecords(audit, (), (), NOW.isoformat(), "c" * 64)
        value = snapshot.validate_evidence_bundle(replace(collect(Reader()), local_history=local),
                                                  now=NOW + timedelta(hours=1))
        self.assertIsNone(value.to_dict()["local_history"]["audit"]["effective_hour"])
        self.assertEqual(value.to_dict()["local_history"]["audit"]["effective_today"], 1)

    def test_invalid_target_does_not_enter_reader(self):
        reader = Reader()
        with self.assertRaisesRegex(ValueError, "invalid_target_identity"):
            collect(reader, pr_number=True)
        self.assertEqual(reader.calls, [])

    def test_malformed_pr_nested_objects_hold_and_preserve_record(self):
        for key in ("head", "base", "user"):
            for malformed in (None, [], SECRET):
                with self.subTest(key=key, malformed=type(malformed).__name__):
                    reader = Reader(ci=True)
                    real = reader.get
                    def get(route, params=None):
                        value = real(route, params)
                        if route == "pulls":
                            for item in value:
                                item[key] = malformed
                        elif route == "pulls/2":
                            value[key] = malformed
                        return value
                    reader.get = get
                    value = collect(reader, root_issue=1, pr_number=2)
                    self.assertIn(("unverified", "selected_pr_unverified"), value.diagnostics)
                    self.assertEqual(value.ci["state"], "unobserved")
                    self.assertTrue(any(r.kind == "pull" for r in value.source_collection.records))
                    self.assertNotIn(SECRET, snapshot.render_diagnostic(value))

    def test_local_roots_and_approval_links_are_checked_against_github(self):
        observed = sources.collect_github_sources(Reader([reservation(), result()]), now=NOW)
        audit = history.HistoryAudit(True, 0, 0, 1, 1, 1, frozenset(), 1, (), "d" * 64)
        local = history.HistoryRecords(audit, (self.local_record(),), (), NOW.isoformat(), "d" * 64)
        linked = next(a for a in snapshot.reconcile_sources(observed, local) if a.source_kind == "r3_minor")
        self.assertIn("github:issue_comment:200", linked.related_sources)
        self.assertIn("github:issue_comment:201", linked.related_sources)
        self.assertIn("local_approval_authority_unverified", linked.reasons)
        self.assertFalse(any("missing" in r or "conflict" in r for r in linked.reasons))
        bad = replace(local, records=(self.local_record(root_issue=999, root_pr=998,
                        approval_reservation_id=1234, approval_result_id=1235),))
        missing = next(a for a in snapshot.reconcile_sources(observed, bad) if a.source_kind == "r3_minor")
        self.assertIn("local_root_issue_missing", missing.reasons)
        self.assertIn("local_root_pr_missing", missing.reasons)
        self.assertIn("local_approval_source_missing", missing.reasons)
        partial = replace(observed, visible_inventory_complete=False)
        unknown = next(a for a in snapshot.reconcile_sources(partial, bad) if a.source_kind == "r3_minor")
        self.assertIn("local_root_issue_unverified", unknown.reasons)
        self.assertNotIn("local_root_issue_missing", unknown.reasons)
        self.assertEqual(unknown.consumption, "consumed")

    def test_local_approval_result_wrong_reservation_is_conflict(self):
        observed = sources.collect_github_sources(Reader([reservation(), result(reservation_id=999)]), now=NOW)
        audit = history.HistoryAudit(True, 0, 0, 1, 1, 1, frozenset(), 1, (), "d" * 64)
        local = history.HistoryRecords(audit, (self.local_record(),), (), NOW.isoformat(), "d" * 64)
        linked = next(a for a in snapshot.reconcile_sources(observed, local) if a.source_kind == "r3_minor")
        self.assertIn("local_approval_link_conflict", linked.reasons)
        self.assertEqual(linked.consumption, "consumed")

    def test_connector_exceptions_are_sanitized_at_every_boundary(self):
        for fail_at in ("main", "ci", "final"):
            reader = Reader(ci=True)
            real = reader.get
            main_count = 0
            def get(route, params=None):
                nonlocal main_count
                if route == "git/ref/heads/main":
                    main_count += 1
                if ((fail_at == "main" and route == "git/ref/heads/main") or
                    (fail_at == "ci" and route.startswith("actions/")) or
                    (fail_at == "final" and route == "git/ref/heads/main" and main_count == 2)):
                    raise RuntimeError(SECRET)
                return real(route, params)
            reader.get = get
            value = collect(reader, root_issue=1, pr_number=2)
            self.assertTrue(value.source_collection.records)
            self.assertEqual(value.to_dict()["state"], "HOLD")
            self.assertNotIn(SECRET, snapshot.render_diagnostic(value))


if __name__ == "__main__":
    unittest.main()
