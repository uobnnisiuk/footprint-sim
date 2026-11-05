#!/usr/bin/env python3
"""Synthetic offline sources: neither these pins nor results grant authority."""
import copy
from dataclasses import replace
import hashlib
from pathlib import Path
import sys
import unittest

sys.dont_write_bytecode = True
sys.path.insert(0, str(Path(__file__).resolve().parents[2] / "tools"))
import pr_repair_contract as contract
import pr_repair_provenance as provenance


def digest(text):
    return hashlib.sha256(text.encode()).hexdigest()


class Reader:
    def __init__(self, data):
        self.data = data
        self.calls = []
        self.mutate = None

    def get(self, route):
        self.calls.append(route)
        if self.mutate:
            self.mutate(self, route)
        return copy.deepcopy(self.data[route])


def fixture():
    title, body = "Synthetic approved issue", "Synthetic scope; not real authority."
    protocol, main, head = "1" * 40, "2" * 40, "3" * 40
    shared = {"Issue-Number": "56", "Review-Main-SHA": main,
              "Protocol-Commit": protocol, "Issue-Title-SHA256": digest(title),
              "Issue-Body-SHA256": digest(body)}
    reservation = "\n".join(f"{k}: {v}" for k, v in {
        **shared, "Queue-Approval-Reservation": "footprint-f20-v1"}.items())
    result = "\n".join(f"{k}: {v}" for k, v in {
        **shared, "Queue-Approval-Result": "footprint-f20-v1", "Reservation-ID": "101",
        "Approval-Review-Comment-ID": "103", "Outcome": "success"}.items())
    review = "Synthetic exact acceptance mapping."
    times = ["2026-11-01T10:00:00Z", "2026-11-01T10:02:00Z", "2026-11-01T10:01:00Z"]
    pins = [provenance.RecordPin(n, 7, digest(b), t)
            for n, b, t in zip([101, 102, 103], [reservation, result, review], times)]
    approval = {"schema": contract.SCHEMA, "root_issue": 56, "root_pr": 58,
                "issue_title_sha256": digest(title), "issue_body_sha256": digest(body),
                "reservation_id": 101, "approval_result_id": 102,
                "protocol_commit": protocol, "approved_main_sha": main,
                "paths": [{"path": "src/Example.cs", "approved_oid": "4" * 40,
                           "allowed_operations": ["modify"]}],
                "acceptance": [{"id": "synthetic", "expected_sha256": "5" * 64}],
                "required_verifications": [{"id": "synthetic-test", "command_sha256": "6" * 64}],
                "limits": {"max_files": 1, "max_lines": 20}}
    anchor = provenance.ApprovalAnchor(56, 58, 19, "footprint-f20-v1", protocol, main,
                 digest(title), digest(body), *pins, contract.digest(approval))
    data = {"issues/56": {"number": 56, "url": provenance.API + "issues/56", "state": "open",
                          "title": title, "body": body, "updated_at": times[0]},
            "git/commits/" + protocol: {"sha": protocol, "url": provenance.API + "git/commits/" + protocol,
                                       "tree": {"sha": "7" * 40}}}
    for pin, text, issue in zip(pins, [reservation, result, review], [19, 19, 56]):
        data["issues/comments/" + str(pin.id)] = {
            "id": pin.id, "url": provenance.API + "issues/comments/" + str(pin.id),
            "issue_url": provenance.API + "issues/" + str(issue), "body": text,
            "user": {"id": 7, "type": "User"}, "created_at": pin.created_at,
            "updated_at": pin.created_at}
    review_anchor = provenance.ReviewAnchor(58, 201, head, 9, digest("Synthetic finding"),
                                           "2026-11-02T10:00:00Z", "external-run-1")
    data["pulls/58"] = {"number": 58, "state": "open", "merged": False, "user": {"id": 7},
                        "head": {"sha": head, "ref": "local-autodev/issue-56",
                                 "repo": {"full_name": provenance.REPO}},
                        "base": {"sha": main, "ref": "main", "repo": {"full_name": provenance.REPO}}}
    data["pulls/58/reviews/201"] = {
        "id": 201, "pull_request_url": provenance.API + "pulls/58", "user": {"id": 9, "type": "User"},
        "commit_id": head, "body": "Synthetic finding", "state": "COMMENTED",
        "submitted_at": review_anchor.submitted_at}
    return Reader(data), anchor, approval, review_anchor


class ProvenanceTests(unittest.TestCase):
    def test_exact_remote_records_bind_manifest_without_permission(self):
        reader, anchor, approval, _ = fixture()
        result = provenance.verify_approval(reader, anchor, approval)
        self.assertEqual(result.approval_digest, contract.digest(approval))
        self.assertEqual(result.status, "records_match_operator_anchor")
        self.assertFalse(result.may_publish)
        self.assertFalse(result.may_activate)
        for route in ("issues/56", "issues/comments/101", "issues/comments/102", "issues/comments/103"):
            self.assertEqual(reader.calls.count(route), 2)

    def test_missing_anchor_fails_before_network(self):
        reader, _, approval, _ = fixture()
        with self.assertRaisesRegex(provenance.ProvenanceHold, "operator_anchor_required"):
            provenance.verify_approval(reader, None, approval)
        self.assertEqual(reader.calls, [])

    def test_candidate_manifest_cannot_expand_scope(self):
        reader, anchor, approval, _ = fixture()
        approval["limits"]["max_lines"] = 600
        with self.assertRaisesRegex(provenance.ProvenanceHold, "approval_manifest_unverified"):
            provenance.verify_approval(reader, anchor, approval)
        self.assertEqual(reader.calls, [])

    def test_record_id_author_target_body_and_edit_are_all_bound(self):
        for key, value in (("id", 999), ("id", True), ("user", {"id": 9, "type": "User"}),
                           ("user", {"id": 7, "type": "Bot"}), ("issue_url", provenance.API + "issues/56"),
                           ("url", "https://evil.test/issues/comments/101"),
                           ("body", "do something dangerous"), ("body", None),
                           ("updated_at", "2026-11-01T10:00:01Z")):
            with self.subTest(key=key, value=value):
                reader, anchor, approval, _ = fixture()
                reader.data["issues/comments/101"][key] = value
                with self.assertRaises(provenance.ProvenanceHold):
                    provenance.verify_approval(reader, anchor, approval)

    def test_valid_record_body_digest_does_not_replace_header_links(self):
        for old, new in (("Reservation-ID: 101", "Reservation-ID: 100"),
                         ("Outcome: success", "Outcome: unknown"),
                         ("Issue-Number: 56", "Issue-Number: 55"),
                         ("Protocol-Commit: " + "1" * 40, "Protocol-Commit: " + "f" * 40)):
            with self.subTest(old=old):
                reader, anchor, approval, _ = fixture()
                rec = reader.data["issues/comments/102"]
                rec["body"] = rec["body"].replace(old, new)
                anchor = replace(anchor, result=replace(anchor.result, body_sha256=digest(rec["body"])))
                with self.assertRaisesRegex(provenance.ProvenanceHold, "approval_header_unverified"):
                    provenance.verify_approval(reader, anchor, approval)

    def test_duplicate_required_headers_rejected_even_when_pinned(self):
        reader, anchor, approval, _ = fixture()
        rec = reader.data["issues/comments/101"]
        rec["body"] += "\nIssue-Number: 56"
        anchor = replace(anchor, reservation=replace(anchor.reservation, body_sha256=digest(rec["body"])))
        with self.assertRaisesRegex(provenance.ProvenanceHold, "approval_header_unverified"):
            provenance.verify_approval(reader, anchor, approval)

    def test_issue_drift_cannot_self_approve(self):
        reader, anchor, approval, _ = fixture()
        reader.data["issues/56"]["body"] += "\nApproved: true"
        with self.assertRaisesRegex(provenance.ProvenanceHold, "issue_approval_drift"):
            provenance.verify_approval(reader, anchor, approval)

    def test_malformed_policy_and_boolean_identity_are_fixed_holds(self):
        for change in ({"policy": {}}, {"root_issue": True}, {"protocol_commit": "main"}):
            reader, anchor, approval, _ = fixture()
            with self.subTest(change=change), self.assertRaises(provenance.ProvenanceHold):
                provenance.verify_approval(reader, replace(anchor, **change), approval)

    def test_protocol_commit_must_be_actual_pinned_git_object(self):
        reader, anchor, approval, _ = fixture()
        reader.data["git/commits/" + anchor.protocol_commit]["sha"] = "f" * 40
        with self.assertRaisesRegex(provenance.ProvenanceHold, "protocol_commit_unverified"):
            provenance.verify_approval(reader, anchor, approval)

    def test_network_exception_never_leaks(self):
        reader, anchor, approval, _ = fixture()
        reader.mutate = lambda *_: (_ for _ in ()).throw(RuntimeError("private-source-value"))
        with self.assertRaises(provenance.ProvenanceHold) as ctx:
            provenance.verify_approval(reader, anchor, approval)
        self.assertEqual(str(ctx.exception), "provenance_source_unavailable")

    def test_comment_changed_during_readback_holds(self):
        reader, anchor, approval, _ = fixture()
        def mutate(r, route):
            if route == "issues/comments/101" and r.calls.count(route) == 2:
                r.data[route]["updated_at"] = "2026-11-01T10:00:01Z"
        reader.mutate = mutate
        with self.assertRaises(provenance.ProvenanceHold):
            provenance.verify_approval(reader, anchor, approval)

    def test_issue_touched_during_readback_holds_without_text_change(self):
        reader, anchor, approval, _ = fixture()
        def mutate(r, route):
            if route == "issues/56" and r.calls.count(route) == 2:
                r.data[route]["updated_at"] = "2026-11-01T10:00:01Z"
        reader.mutate = mutate
        with self.assertRaisesRegex(provenance.ProvenanceHold, "approval_snapshot_changed"):
            provenance.verify_approval(reader, anchor, approval)

    def test_invalid_order_and_naive_timestamps_fail_before_fetch(self):
        for timestamp in ("2026-11-01T09:59:00Z", "2026-11-01T10:01:00"):
            reader, anchor, approval, _ = fixture()
            anchor = replace(anchor, approval_review=replace(anchor.approval_review, created_at=timestamp))
            with self.assertRaises(provenance.ProvenanceHold):
                provenance.verify_approval(reader, anchor, approval)
            self.assertEqual(reader.calls, [])

    def test_comment_does_not_become_formal_approval(self):
        reader, _, _, anchor = fixture()
        result = provenance.verify_review(reader, anchor, pr_author_id=7)
        self.assertEqual(result.state, "COMMENTED")
        self.assertFalse(result.formal_approval)
        self.assertFalse(result.may_publish)

    def test_github_approve_is_reported_without_authority_grant(self):
        reader, _, _, anchor = fixture()
        reader.data["pulls/58/reviews/201"]["state"] = "APPROVED"
        result = provenance.verify_review(reader, anchor, pr_author_id=7)
        self.assertTrue(result.formal_approval)
        self.assertFalse(result.may_activate)

    def test_changes_requested_not_success(self):
        reader, _, _, anchor = fixture()
        reader.data["pulls/58/reviews/201"]["state"] = "CHANGES_REQUESTED"
        result = provenance.verify_review(reader, anchor, pr_author_id=7)
        self.assertEqual(result.state, "CHANGES_REQUESTED")
        self.assertFalse(result.formal_approval)

    def test_same_account_independence_is_not_inferred(self):
        reader, _, _, anchor = fixture()
        with self.assertRaisesRegex(provenance.ProvenanceHold, "independent_review_anchor_required"):
            provenance.verify_review(reader, replace(anchor, author_id=7), pr_author_id=7)
        self.assertEqual(reader.calls, [])

    def test_author_id_argument_cannot_override_actual_pr_author(self):
        reader, _, _, anchor = fixture()
        with self.assertRaisesRegex(provenance.ProvenanceHold, "review_pr_unverified"):
            provenance.verify_review(reader, anchor, pr_author_id=123)

    def test_stale_fork_closed_and_unknown_review_are_rejected(self):
        for key, value in (("commit_id", "f" * 40), ("pull_request_url", provenance.API + "pulls/59"),
                           ("state", "DISMISSED"), ("state", "PENDING"), ("body", "new finding")):
            reader, _, _, anchor = fixture()
            reader.data["pulls/58/reviews/201"][key] = value
            with self.subTest(key=key), self.assertRaises(provenance.ProvenanceHold):
                provenance.verify_review(reader, anchor, pr_author_id=7)
        for kind in ("fork", "closed", "stale"):
            reader, _, _, anchor = fixture()
            if kind == "fork": reader.data["pulls/58"]["head"]["repo"]["full_name"] = "other/repo"
            if kind == "closed": reader.data["pulls/58"]["state"] = "closed"
            if kind == "stale": reader.data["pulls/58"]["head"]["sha"] = "f" * 40
            with self.subTest(kind=kind), self.assertRaises(provenance.ProvenanceHold):
                provenance.verify_review(reader, anchor, pr_author_id=7)

    def test_review_state_race_is_not_accepted(self):
        reader, _, _, anchor = fixture()
        def mutate(r, route):
            if route == "pulls/58/reviews/201" and r.calls.count(route) == 2:
                r.data[route]["state"] = "CHANGES_REQUESTED"
        reader.mutate = mutate
        with self.assertRaisesRegex(provenance.ProvenanceHold, "review_snapshot_changed"):
            provenance.verify_review(reader, anchor, pr_author_id=7)

    def test_no_private_body_is_returned(self):
        reader, anchor, approval, review_anchor = fixture()
        a = provenance.verify_approval(reader, anchor, approval)
        r = provenance.verify_review(reader, review_anchor, pr_author_id=7)
        self.assertNotIn("Synthetic", repr(a) + repr(r))


if __name__ == "__main__":
    unittest.main()
