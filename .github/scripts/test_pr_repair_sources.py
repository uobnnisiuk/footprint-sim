"""Visible history coverage, synthetic GET responses only."""
from copy import deepcopy
from dataclasses import asdict
from datetime import datetime, timezone
import json
from pathlib import Path
import sys
import unittest
from unittest.mock import patch

sys.path.insert(0, str(Path(__file__).resolve().parents[2] / "tools"))
import pr_repair_adapter as adapter
import pr_repair_remote_audit as remote
import pr_repair_sources as m

NOW = datetime(2026, 11, 5, tzinfo=timezone.utc)
SINCE = datetime(2026, 10, 27, tzinfo=timezone.utc)
STAMP = "2026-11-04T01:00:00Z"
API = remote.API
SECRET = "secret-sentinel-never-print-this-body-or-error"
SHA = "a" * 40


def issue(number, *, pull=False, comments=0):
    value = {"id": 100 + number, "number": number, "url": API + f"issues/{number}",
             "user": {"id": 20}, "created_at": STAMP, "updated_at": STAMP,
             "comments": comments, "title": SECRET, "body": SECRET}
    if pull:
        value["pull_request"] = {"url": API + f"pulls/{number}"}
    return value


def pull(number):
    return {"id": 100 + number, "number": number, "url": API + f"pulls/{number}",
            "user": {"id": 20}, "created_at": STAMP, "updated_at": STAMP,
            "body": SECRET, "head": {"sha": SHA}}


def comment(identifier=300, number=1):
    return {"id": identifier, "url": API + f"issues/comments/{identifier}",
            "issue_url": API + f"issues/{number}", "user": {"id": 20},
            "created_at": STAMP, "updated_at": STAMP, "body": SECRET}


class Reader:
    def __init__(self):
        self.calls = []
        self.passes = 0
        self.change = None
        self.data = {
            "": {"id": 37, "full_name": remote.REPO},
            "issues": [issue(1, comments=1), issue(2, pull=True)],
            "pulls": [pull(2)], "issues/comments": [comment()],
            "pulls/2/reviews": [{"id": 400, "pull_request_url": API + "pulls/2",
                "user": {"id": 20}, "submitted_at": STAMP, "state": "COMMENTED",
                "commit_id": SHA, "body": SECRET}],
            "pulls/2/comments": [{"id": 500, "pull_request_url": API + "pulls/2",
                "user": {"id": 20}, "created_at": STAMP, "updated_at": STAMP,
                "body": SECRET}],
            "pulls/2/commits": [{"sha": SHA, "url": API + "commits/" + SHA,
                "author": {"id": 20}, "commit": {"message": SECRET}}],
            "issues/2/timeline": [{"id": 600, "event": "commented", "actor": {"id": 20},
                "created_at": STAMP, "body": SECRET},
                {"event": "committed", "sha": SHA, "author": {"id": 20}}],
        }

    def get(self, route, params=None):
        self.calls.append((route, deepcopy(params)))
        if route == "":
            self.passes += 1
            if self.passes == 2 and self.change:
                self.change(self)
        value = self.data[route]
        if isinstance(value, Exception):
            raise value
        if callable(value):
            value = value(params)
        elif params:
            size, page = params["per_page"], params["page"]
            value = value[(page - 1) * size:page * size]
        return deepcopy(value)


def collect(reader=None):
    return m.collect_github_sources(reader or Reader(), now=NOW, required_since=SINCE)


class SourcesTests(unittest.TestCase):
    def test_complete_visible_inventory_is_not_complete_execution_history(self):
        value = collect()
        self.assertTrue(value.visible_inventory_complete)
        self.assertFalse(value.legacy_execution_history_complete)
        self.assertFalse(value.may_publish)
        self.assertFalse(value.may_activate)
        self.assertEqual(("legacy_execution_history_unavailable",), value.holds)
        self.assertTrue(all(c.terminal and c.reread == "unchanged" for c in value.coverage))
        self.assertEqual(10, len(value.records))

    def test_all_state_is_explicit_and_exact_full_page_has_terminal_page(self):
        reader = Reader()
        with patch.object(m, "PAGE_SIZE", 2):
            value = collect(reader)
        self.assertTrue(value.visible_inventory_complete)
        issues = next(c for c in value.coverage if c.endpoint == "issues")
        self.assertEqual(2, issues.pages)
        self.assertEqual(2, issues.reread_pages)
        self.assertTrue(any(route == "issues" and args == {"per_page": 2, "page": 2, "state": "all"}
                            for route, args in reader.calls))

    def test_partial_page_failure_retains_observed_records_and_safe_reason(self):
        reader = Reader()
        originals = reader.data["issues"]
        def page(args):
            if args["page"] > 1:
                raise RuntimeError(SECRET)
            return originals
        reader.data["issues"] = page
        with patch.object(m, "PAGE_SIZE", 2):
            value = collect(reader)
        self.assertFalse(value.visible_inventory_complete)
        self.assertIn("source_endpoint_unavailable", value.holds)
        self.assertEqual(2, len([r for r in value.records if r.kind == "issue"]))
        coverage = next(c for c in value.coverage if c.endpoint == "issues")
        self.assertFalse(coverage.terminal)
        self.assertEqual("incomplete", coverage.reread)
        self.assertNotIn(SECRET, json.dumps(asdict(value)))

    def test_edit_between_passes_keeps_both_versions(self):
        reader = Reader()
        reader.change = lambda r: r.data["issues/comments"][0].update(body="changed")
        value = collect(reader)
        records = [r for r in value.records if r.kind == "issue_comment"]
        self.assertEqual(2, len(records))
        self.assertEqual(1, len({r.record_id for r in records}))
        self.assertEqual(2, len({r.content_sha256 for r in records}))
        self.assertIn("source_changed_during_collection", value.holds)
        self.assertFalse(value.visible_inventory_complete)

    def test_deleted_record_remains_with_changed_coverage(self):
        reader = Reader()
        def change(r):
            r.data["issues/comments"] = []
            r.data["issues"][0]["comments"] = 0
        reader.change = change
        value = collect(reader)
        self.assertEqual(1, len([r for r in value.records if r.kind == "issue_comment"]))
        c = next(c for c in value.coverage if c.endpoint == "issues/comments")
        self.assertEqual((1, 0, "changed"), (c.count, c.reread_count, c.reread))

    def test_new_pull_changes_endpoint_set(self):
        reader = Reader()
        def change(r):
            r.data["issues"].append(issue(3, pull=True))
            r.data["pulls"].append(pull(3))
            for suffix in ("reviews", "comments", "commits"):
                r.data[f"pulls/3/{suffix}"] = []
            r.data["issues/3/timeline"] = []
        reader.change = change
        value = collect(reader)
        self.assertIn("source_changed_during_collection", value.holds)
        self.assertTrue(any(c.reread == "endpoint_set_changed" for c in value.coverage))

    def test_duplicate_identity_does_not_disappear_as_clean_snapshot(self):
        reader = Reader()
        reader.data["issues/comments"].append(comment())
        reader.data["issues"][0]["comments"] = 2
        value = collect(reader)
        self.assertIn("duplicate_source_identity", value.holds)
        self.assertFalse(value.visible_inventory_complete)
        self.assertEqual(2, next(c for c in value.coverage if c.endpoint == "issues/comments").count)

    def test_same_body_different_ids_are_distinct_observations(self):
        reader = Reader()
        reader.data["issues/comments"].append(comment(301))
        reader.data["issues"][0]["comments"] = 2
        value = collect(reader)
        self.assertTrue(value.visible_inventory_complete)
        self.assertEqual(2, len([r for r in value.records if r.kind == "issue_comment"]))

    def test_same_global_comment_id_cannot_bind_to_two_issues(self):
        reader = Reader()
        reader.data["issues/comments"].append(comment(number=2))
        reader.data["issues"][1]["comments"] = 1
        value = collect(reader)
        self.assertFalse(value.visible_inventory_complete)
        self.assertIn("source_inventory_conflict", value.holds)
        self.assertEqual({1, 2}, {r.parent_number for r in value.records
                                 if r.kind == "issue_comment" and r.record_id == "300"})
        self.assertIn("source_inventory_conflict",
                      next(c for c in value.coverage if c.endpoint == "issues/comments").missing)

    def test_reparented_global_comment_is_retained_as_conflict(self):
        reader = Reader()
        def change(r):
            r.data["issues/comments"] = [comment(number=2)]
            r.data["issues"][0]["comments"] = 0
            r.data["issues"][1]["comments"] = 1
        reader.change = change
        value = collect(reader)
        self.assertIn("source_changed_during_collection", value.holds)
        self.assertIn("source_inventory_conflict", value.holds)
        self.assertEqual(2, len([r for r in value.records if r.kind == "issue_comment"]))

    def test_platform_id_bindings_cover_issue_pull_review_and_timeline(self):
        for kind in ("issue", "pull", "review", "review_comment", "timeline", "timeline_node"):
            with self.subTest(kind=kind):
                reader = Reader()
                reader.data["issues"].append(issue(3, pull=True))
                reader.data["pulls"].append(pull(3))
                for suffix in ("reviews", "comments", "commits"):
                    reader.data[f"pulls/3/{suffix}"] = []
                reader.data["issues/3/timeline"] = []
                if kind == "issue":
                    reader.data["issues"][-1]["id"] = reader.data["issues"][0]["id"]
                elif kind == "pull":
                    reader.data["pulls"][-1]["id"] = reader.data["pulls"][0]["id"]
                elif kind in ("review", "review_comment"):
                    suffix = "reviews" if kind == "review" else "comments"
                    duplicate = deepcopy(reader.data[f"pulls/2/{suffix}"][0])
                    duplicate["pull_request_url"] = API + "pulls/3"
                    reader.data[f"pulls/3/{suffix}"] = [duplicate]
                elif kind == "timeline":
                    reader.data["issues/3/timeline"] = [deepcopy(reader.data["issues/2/timeline"][0])]
                else:
                    event = {"node_id": "SharedEventNode", "event": "reviewed"}
                    reader.data["issues/2/timeline"] = [event]
                    reader.data["issues/3/timeline"] = [deepcopy(event)]
                value = collect(reader)
                self.assertIn("source_inventory_conflict", value.holds)
                self.assertFalse(value.visible_inventory_complete)

    def test_shared_commit_sha_in_multiple_prs_is_not_parent_conflict(self):
        reader = Reader()
        reader.data["issues"].append(issue(3, pull=True))
        reader.data["pulls"].append(pull(3))
        reader.data["pulls/3/reviews"] = []
        reader.data["pulls/3/comments"] = []
        reader.data["pulls/3/commits"] = deepcopy(reader.data["pulls/2/commits"])
        timeline = {"node_id": "SharedCommitNode", "event": "committed", "sha": SHA}
        reader.data["issues/2/timeline"] = [timeline]
        reader.data["issues/3/timeline"] = [deepcopy(timeline)]
        value = collect(reader)
        self.assertTrue(value.visible_inventory_complete)
        self.assertNotIn("source_inventory_conflict", value.holds)
        self.assertEqual(2, len([r for r in value.records if r.kind == "commit"]))

    def test_orphan_comment_and_missing_pull_pair_are_conflicts(self):
        for kind in ("orphan", "missing_pull"):
            reader = Reader()
            if kind == "orphan": reader.data["issues/comments"] = [comment(number=99)]
            else: reader.data["pulls"] = []
            value = collect(reader)
            self.assertFalse(value.visible_inventory_complete)
            self.assertIn("source_parent_missing" if kind == "orphan" else "source_inventory_conflict",
                          value.holds)

    def test_cross_repository_parent_is_not_followed(self):
        reader = Reader()
        reader.data["issues/comments"][0]["issue_url"] = "https://evil.invalid/" + SECRET
        value = collect(reader)
        self.assertIn("source_parent_unverified", value.holds)
        self.assertFalse(any("evil" in route for route, _ in reader.calls))
        self.assertNotIn(SECRET, json.dumps(value.to_dict()))

    def test_timeline_without_identity_is_retained_unverified(self):
        reader = Reader()
        reader.data["issues/2/timeline"] = [{"event": "unknown", "body": SECRET}]
        value = collect(reader)
        record = next(r for r in value.records if r.kind == "timeline")
        self.assertEqual("identity_unverified", record.verification)
        self.assertTrue(record.record_id.startswith("unidentified:"))
        self.assertIn("source_identity_unverified", value.holds)

    def test_node_id_is_retained_as_opaque_hash_not_arbitrary_string(self):
        reader = Reader()
        reader.data["issues/2/timeline"] = [{"node_id": "StableNode123", "event": "committed"}]
        value = collect(reader)
        record = next(r for r in value.records if r.kind == "timeline")
        self.assertTrue(record.record_id.startswith("node:"))
        self.assertNotIn("StableNode123", json.dumps(value.to_dict()))

    def test_source_text_never_reaches_repr_asdict_or_json(self):
        value = collect()
        self.assertNotIn(SECRET, repr(value))
        self.assertNotIn(SECRET, repr(asdict(value)))
        self.assertNotIn(SECRET, json.dumps(value.to_dict()))
        record = next(r for r in value.records if r.kind == "issue_comment")
        self.assertEqual(SECRET, record._payload["body"])
        self.assertNotIn("_payload", asdict(record))

    def test_source_payload_is_detached_from_injected_reader(self):
        reader = Reader()
        value = collect(reader)
        reader.data["issues/comments"][0]["body"] = "later mutation"
        self.assertEqual(SECRET, next(r for r in value.records if r.kind == "issue_comment")._payload["body"])

    def test_source_future_time_does_not_become_current_day_usage(self):
        reader = Reader()
        reader.data["issues/comments"][0]["created_at"] = "2026-11-06T00:00:00Z"
        reader.data["issues/comments"][0]["updated_at"] = "2026-11-06T00:00:00Z"
        value = collect(reader)
        self.assertIn("source_time_after_cutoff", value.holds)
        self.assertFalse(value.visible_inventory_complete)

    def test_malformed_timestamp_and_boolean_identity_are_holds(self):
        for key, bad in (("created_at", "0001-01-01T00:00:00+23:00"),
                         ("updated_at", SECRET), ("id", True)):
            reader = Reader()
            reader.data["issues/comments"][0][key] = bad
            value = collect(reader)
            self.assertFalse(value.visible_inventory_complete)
            self.assertNotIn(SECRET, repr(value))

    def test_missing_repository_stops_without_fallback_or_exception_text(self):
        reader = Reader()
        reader.data[""] = RuntimeError(SECRET)
        value = collect(reader)
        self.assertEqual([("", None), ("", None)], reader.calls)
        self.assertIn("source_repository_unavailable", value.holds)
        self.assertNotIn(SECRET, repr(value))

    def test_invalid_input_period_is_fixed_hold(self):
        for now, since in ((datetime(2026, 11, 5), None), (NOW, datetime(2026, 11, 6, tzinfo=timezone.utc)),
                           (NOW, "secret")):
            reader = Reader()
            value = m.collect_github_sources(reader, now=now, required_since=since)
            self.assertEqual(("invalid_observation_period",), value.holds)
            self.assertEqual([], reader.calls)

    def test_unknown_start_and_empty_inventory_do_not_prove_no_executions(self):
        reader = Reader()
        for route in ("issues", "pulls", "issues/comments"):
            reader.data[route] = []
        value = m.collect_github_sources(reader, now=NOW)
        self.assertTrue(value.visible_inventory_complete)
        self.assertFalse(value.legacy_execution_history_complete)
        self.assertIn("legacy_required_period_unknown", value.holds)
        self.assertIsNone(next(c for c in value.coverage if c.endpoint == "issues").observed_from)

    def test_page_and_record_bounds_are_holds(self):
        with patch.object(m, "MAX_PAGES", 1), patch.object(m, "PAGE_SIZE", 2):
            value = collect()
            self.assertIn("source_page_limit_reached", value.holds)
        with patch.object(m, "MAX_RECORDS", 2):
            value = collect()
            self.assertIn("source_bounds_exceeded", value.holds)
        with patch.object(m, "MAX_SOURCE_BYTES", 20):
            value = collect()
            self.assertIn("source_bounds_exceeded", value.holds)

    def test_no_files_processes_or_network_when_reader_is_injected(self):
        with patch("builtins.open", side_effect=AssertionError("file")), \
             patch("os.open", side_effect=AssertionError("file")), \
             patch("subprocess.Popen", side_effect=AssertionError("process")), \
             patch("socket.socket", side_effect=AssertionError("network")):
            self.assertTrue(collect().visible_inventory_complete)


class SourceTransportTests(unittest.TestCase):
    def test_closed_inventory_routes_reuse_existing_reader(self):
        reader = adapter.GitHubEvidenceReader("synthetic")
        for route, params in (("", None), ("issues", {"state": "all", "page": 1, "per_page": 100}),
                              ("pulls", {"state": "closed"}), ("issues/comments", {"page": 1}),
                              ("pulls/2/commits", {"page": 1}), ("issues/2/timeline", {"page": 1})):
            self.assertEqual(params or {}, reader._validate_endpoint(route, params))
        for route, params in (("https://evil.invalid", None), ("actions/runs/2/logs", None),
                              ("issues", {"state": "secret"}), ("", {"page": 1}),
                              ("issues", {"since": SECRET}), ("issues/comments", {"page": True})):
            with self.assertRaises(remote.AuditHold): reader._validate_endpoint(route, params)


if __name__ == "__main__":
    unittest.main()
