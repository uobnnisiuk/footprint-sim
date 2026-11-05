"""Operational read adapter: synthetic API fixtures, no credentials/network."""
import base64
from copy import deepcopy
from datetime import datetime, timezone
import io
from pathlib import Path
import sys
import unittest
from unittest.mock import patch

sys.path.insert(0, str(Path(__file__).resolve().parents[2] / "tools"))
import pr_repair_adapter as m
import pr_repair_candidate as candidate
import pr_repair_remote_audit as remote

A, B, C = "a" * 40, "b" * 40, "c" * 40
RID = "d" * 32


class Reader:
    def __init__(self):
        self.data = {}
        self.calls = []

    def get(self, route, params=None):
        self.calls.append((route, params))
        value = self.data[route]
        return deepcopy(value() if callable(value) else value)

    def blob(self, content):
        sha = m.oid("blob", content)
        self.data["git/blobs/" + sha] = {
            "sha": sha, "encoding": "base64", "size": len(content),
            "content": base64.b64encode(content).decode("ascii")}
        return sha

    def tree(self, entries):
        rows = sorted(entries, key=lambda r:
                      (r[0] + ("/" if r[1] == "040000" else "")).encode("utf-8"))
        data = b"".join((("40000" if mode == "040000" else mode) + " ").encode()
                        + name.encode() + b"\0" + bytes.fromhex(sha)
                        for name, mode, sha in rows)
        sha = m.oid("tree", data)
        types = {"040000": "tree", "100644": "blob", "100755": "blob",
                 "120000": "blob", "160000": "commit"}
        self.data["git/trees/" + sha] = {"sha": sha, "truncated": False,
            "tree": [{"path": n, "mode": mode, "sha": s, "type": types[mode]}
                     for n, mode, s in entries]}
        return sha

    def commit(self, sha, text, parents=()):
        blob = self.blob(text)
        sub = self.tree([("Test.cs", "100644", blob)])
        root = self.tree([("src", "040000", sub)])
        self.data["git/commits/" + sha] = {"sha": sha, "tree": {"sha": root},
                                         "parents": [{"sha": p} for p in parents]}
        return root


class GitObjectTests(unittest.TestCase):
    def fixture(self):
        r = Reader()
        r.commit(A, b"before\n")
        r.commit(B, b"after\n", (A,))
        return r, m.GitObjects(r)

    def test_complete_tree_and_exact_blob_content(self):
        r, g = self.fixture()
        trees = g.contract_trees({"approved": A, "pr_base": A,
                                  "current": A, "candidate": B}, ("src/Test.cs",))
        self.assertTrue(trees["candidate"]["complete"])
        self.assertEqual("after\n", trees["candidate"]["entries"]["src/Test.cs"]["text"])
        self.assertFalse(any(p and "recursive" in p for _, p in r.calls))

    def test_truncated_tree_never_counted_complete(self):
        r, g = self.fixture()
        root = r.data["git/commits/" + A]["tree"]["sha"]
        r.data["git/trees/" + root]["truncated"] = True
        with self.assertRaises(m.AdapterHold):
            g.inventory(A)

    def test_missing_or_injected_entry_fails_tree_hash(self):
        for mutation in ("missing", "extra", "wrong_oid", "wrong_mode"):
            with self.subTest(mutation=mutation):
                r, g = self.fixture()
                root = r.data["git/commits/" + A]["tree"]["sha"]
                rows = r.data["git/trees/" + root]["tree"]
                if mutation == "missing": rows.clear()
                if mutation == "extra": rows.append(dict(rows[0], path="evil"))
                if mutation == "wrong_oid": rows[0]["sha"] = C
                if mutation == "wrong_mode": rows[0]["mode"] = "100644"
                with self.assertRaises(m.AdapterHold): g.inventory(A)

    def test_untrusted_paths_and_case_collisions(self):
        for name in ("../outside", ".git", "bad\nname", "bad\\name", "/abs"):
            r, g = self.fixture()
            root = r.data["git/commits/" + A]["tree"]["sha"]
            r.data["git/trees/" + root]["tree"][0]["path"] = name
            with self.assertRaises(m.AdapterHold): g.inventory(A)
        r, g = self.fixture()
        root = r.data["git/commits/" + A]["tree"]["sha"]
        rows = r.data["git/trees/" + root]["tree"]
        rows.append(dict(rows[0], path="SRC"))
        with self.assertRaises(m.AdapterHold): g.inventory(A)

    def test_blob_hash_size_encoding_verified(self):
        for field, value in (("content", "c2VjcmV0"), ("size", True),
                             ("size", 999), ("encoding", "utf-8")):
            r, g = self.fixture()
            sha = r.blob(b"hello")
            r.data["git/blobs/" + sha][field] = value
            with self.assertRaises(m.AdapterHold): g.blob(sha)

    def test_blob_bounds_and_non_utf8(self):
        r, g = self.fixture()
        r.commit(C, b"\xff")
        with self.assertRaises(m.AdapterHold):
            g.contract_trees(dict.fromkeys(("approved", "pr_base", "current", "candidate"), C),
                             ("src/Test.cs",))
        with patch.object(m, "MAX_BLOB_TOTAL", 1):
            with self.assertRaises(m.AdapterHold): g.blob(r.blob(b"long"))

    def test_contract_content_paths_are_explicit(self):
        r, g = self.fixture()
        for path in ("../auth.json", ".env", "tools/secret.py"):
            with self.assertRaises(m.AdapterHold):
                g.contract_trees(dict.fromkeys(("approved", "pr_base", "current", "candidate"), A),
                                 (path,))

    def test_candidate_feeds_existing_r4_verifier(self):
        r, g = self.fixture()
        before = g.inventory(A)["entries"]["src/Test.cs"]["oid"]
        after = g.inventory(B)["entries"]["src/Test.cs"]["oid"]
        tree = g.commit(B).tree_sha
        digest = candidate.digest({"parent_sha": A, "commit_sha": B, "tree_sha": tree,
                                   "changes": [{"path": "src/Test.cs", "before": before,
                                                "after": after}]})
        envelope = {"reservation_id": RID, "kind": "substantive", "parent_sha": A,
                    "commit_sha": B, "tree_sha": tree, "changed_paths": ["src/Test.cs"],
                    "changes_sha256": digest}
        receipt = {"id": RID, "headSha": A, "kind": "substantive"}
        self.assertEqual(B, g.detached_candidate(envelope, receipt, ("src/Test.cs",))["commit_sha"])
        r.data["git/commits/" + B]["parents"].append({"sha": C})
        with self.assertRaises(candidate.CandidateHalt):
            m.GitObjects(r).detached_candidate(envelope, receipt, ("src/Test.cs",))

    def test_commit_identity_and_bad_types(self):
        r, g = self.fixture()
        r.data["git/commits/" + A]["sha"] = B
        with self.assertRaises(m.AdapterHold): g.commit(A)
        with self.assertRaises(m.AdapterHold): g.commit(None)

    def test_tree_walk_bounds(self):
        r, g = self.fixture()
        with patch.object(m, "MAX_TREES", 1):
            with self.assertRaises(m.AdapterHold): g.inventory(A)


def ci_fixture():
    r = Reader()
    repo = {"url": "https://api.github.com/repos/" + remote.REPO}
    run = {"id": 15, "run_attempt": 2, "head_sha": A, "head_branch": "work",
           "head_repository": {"full_name": remote.REPO},
           "path": ".github/workflows/ci.yml", "event": "pull_request",
           "status": "completed", "conclusion": "success",
           "pull_requests": [{"number": 9,
               "head": {"sha": A, "ref": "work", "repo": repo},
               "base": {"ref": "main", "repo": repo}}]}
    r.data["actions/workflows/ci.yml/runs"] = {"total_count": 1, "workflow_runs": [run]}
    r.data["actions/runs/15"] = run
    job = {"id": 30, "name": "test", "run_id": 15, "run_attempt": 2, "head_sha": A,
           "status": "completed", "conclusion": "success",
           "steps": [{"name": name, "status": "completed", "conclusion": "success"}
                     for name in sorted(m.evidence.REQUIRED_STEPS["normal"])]}
    r.data["actions/runs/15/attempts/2/jobs"] = {"total_count": 1, "jobs": [job]}
    return r


class CITests(unittest.TestCase):
    def test_metadata_not_semantic_acceptance(self):
        r = ci_fixture()
        result = m.read_ci(r, 9, A, "work")
        self.assertEqual("success_metadata_only", result["state"])
        self.assertEqual("unverified", result["semantic_acceptance"])
        self.assertFalse(any("/logs" in route for route, _ in r.calls))

    def test_failed_and_pending_are_not_success(self):
        for state, conclusion, expected in (("completed", "failure", "failure_metadata_only"),
                                            ("in_progress", None, "unknown_or_pending")):
            r = ci_fixture()
            r.data["actions/runs/15"].update(status=state, conclusion=conclusion)
            self.assertEqual(expected, m.read_ci(r, 9, A, "work")["state"])

    def test_wrong_attempt_head_or_empty_steps_hold(self):
        for field, value in (("run_attempt", 1), ("head_sha", B), ("steps", [])):
            r = ci_fixture()
            r.data["actions/runs/15/attempts/2/jobs"]["jobs"][0][field] = value
            with self.assertRaises(m.AdapterHold): m.read_ci(r, 9, A, "work")

    def test_incomplete_job_page_hold(self):
        r = ci_fixture()
        r.data["actions/runs/15/attempts/2/jobs"]["total_count"] = 2
        with self.assertRaises(remote.AuditHold): m.read_ci(r, 9, A, "work")

    def test_missing_or_duplicate_required_step_is_not_green(self):
        for duplicate in (False, True):
            r = ci_fixture()
            steps = r.data["actions/runs/15/attempts/2/jobs"]["jobs"][0]["steps"]
            steps.pop()
            if duplicate:
                steps.append(deepcopy(steps[0]))
            with self.assertRaises(m.AdapterHold): m.read_ci(r, 9, A, "work")

    def test_rerun_during_observation_hold(self):
        r = ci_fixture()
        first = deepcopy(r.data["actions/runs/15"])
        count = [0]
        def changes():
            count[0] += 1
            return dict(first, run_attempt=2 if count[0] == 1 else 3)
        r.data["actions/runs/15"] = changes
        with self.assertRaises(m.AdapterHold): m.read_ci(r, 9, A, "work")

    def test_new_run_supersedes_unchanged_success(self):
        r = ci_fixture()
        original = r.get
        def get(route, params=None):
            result = original(route, params)
            if route.endswith("/jobs"):
                newer = dict(r.data["actions/runs/15"], id=16, status="queued", conclusion=None)
                r.data["actions/workflows/ci.yml/runs"] = {
                    "total_count": 2, "workflow_runs": [r.data["actions/runs/15"], newer]}
            return result
        r.get = get
        with self.assertRaisesRegex(m.AdapterHold, "snapshot_changed"):
            m.read_ci(r, 9, A, "work")


class TransportBoundaryTests(unittest.TestCase):
    def test_extended_routes_do_not_extend_old_reader(self):
        extended = m.GitHubEvidenceReader("offline-dummy")
        old = remote.GitHubReader("offline-dummy")
        for route in ("git/trees/" + A, "issues/comments/19", "pulls/9/reviews/20"):
            self.assertEqual({}, extended._validate_endpoint(route, None))
            with self.assertRaises(remote.AuditHold): old._validate_endpoint(route, None)

    def test_no_arbitrary_urls_logs_query_or_write_route(self):
        reader = m.GitHubEvidenceReader("offline-dummy")
        for route in ("https://evil.invalid", "actions/runs/1/logs", "../issues/1",
                      "git/refs/heads/main", "issues/1?secret=x", "git/blobs/" + A + "/"):
            with self.assertRaises(remote.AuditHold): reader._validate_endpoint(route, {})
        for params in ({"recursive": 1}, {"page": True}, {"state": "invalid"}):
            with self.assertRaises(remote.AuditHold): reader._validate_endpoint("pulls", params)

    def test_actual_transport_only_get_fixed_host_and_sanitizes_error(self):
        class Opener:
            def open(self, req, timeout):
                self.req = req
                raise OSError("dummy-private-message")
        opener = Opener()
        r = m.GitHubEvidenceReader("offline-dummy", opener=opener)
        with self.assertRaisesRegex(remote.AuditHold, "^github_unavailable$"):
            r.get("git/trees/" + A)
        self.assertEqual("GET", opener.req.get_method())
        self.assertEqual(remote.API + "git/trees/" + A, opener.req.full_url)

    def test_existing_live_publish_prohibition_unchanged(self):
        # Import after test discovery: the existing R2 test module loads its
        # contract via importlib, so an earlier publisher import would retain
        # the superseded class identity and poison unrelated R4 fixtures.
        import pr_repair_publish as publish
        with self.assertRaisesRegex(publish.PublishHalt, "production_adapter_not_verified"):
            publish.publish_once(m.GitObjects(Reader()), {}, execute=True)


class AggregateTests(unittest.TestCase):
    def test_missing_anchors_never_promote_to_authority(self):
        from types import SimpleNamespace
        r = ci_fixture()
        r.commit(A, b"same\n")
        r.data["git/ref/heads/main"] = {"object": {"type": "commit", "sha": A}}
        r.data["pulls/9"] = {"number": 9, "state": "open", "merged": False,
            "base": {"ref": "main", "sha": A, "repo": {"full_name": remote.REPO}},
            "head": {"ref": "work", "sha": A, "repo": {"full_name": remote.REPO}},
            "user": {"id": 1, "login": "author"}}
        r.data["pulls"] = [{"id": 90, "number": 9}]
        local = SimpleNamespace(local_valid=True, effective_today=0, effective_hour=0,
                                holds=("legacy_minor_history_unverified",))
        with patch("pr_repair_history.audit_history", return_value=local):
            result = m.inspect(r, state_root=Path("unused"), root_issue=8, pr_number=9)
        self.assertIn("approval_anchor_required", result["holds"])
        self.assertIn("review_anchor_required", result["holds"])
        self.assertIn("legacy_minor_history_unverified", result["holds"])
        self.assertFalse(result["may_publish"])
        self.assertFalse(result["may_activate"])
        self.assertFalse(result["production_enabled"])

    def test_changes_requested_stays_visible(self):
        from types import SimpleNamespace
        r = ci_fixture()
        r.commit(A, b"same\n")
        r.data["git/ref/heads/main"] = {"object": {"type": "commit", "sha": A}}
        r.data["pulls/9"] = {"number": 9, "state": "open", "merged": False,
            "base": {"ref": "main", "sha": A, "repo": {"full_name": remote.REPO}},
            "head": {"ref": "work", "sha": A, "repo": {"full_name": remote.REPO}},
            "user": {"id": 1, "login": "author"}}
        r.data["pulls"] = [{"id": 90, "number": 9}]
        local = SimpleNamespace(local_valid=True, effective_today=0, effective_hour=0, holds=())
        anchor = SimpleNamespace(root_pr=9, head_sha=A)
        review = SimpleNamespace(state="CHANGES_REQUESTED", formal_approval=False)
        with patch("pr_repair_history.audit_history", return_value=local), \
             patch("pr_repair_provenance.verify_review", return_value=review):
            result = m.inspect(r, state_root=Path("unused"), root_issue=8,
                               pr_number=9, review_anchor=anchor)
        self.assertEqual("CHANGES_REQUESTED", result["review_state"])
        self.assertIn("changes_requested", result["holds"])
        self.assertFalse(result["formal_approval"])

    def test_sensitive_reader_failure_not_emitted(self):
        with patch("pr_repair_history.audit_history", side_effect=OSError("private-content")):
            result = m.inspect(Reader(), state_root=Path("unused"), root_issue=1, pr_number=2)
        self.assertNotIn("private-content", repr(result))
        self.assertEqual("HOLD", result["state"])


if __name__ == "__main__":
    unittest.main()
