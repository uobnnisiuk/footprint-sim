"""R5a full-mock offline tests. No network, filesystem writes or credentials."""
import copy
from pathlib import Path
import sys
import unittest

sys.path.insert(0, str(Path(__file__).resolve().parents[2] / "tools"))
from pr_repair_remote_audit import AuditHold, GitHubReader, inspect_pr, REPO

HEAD = "a" * 40
MAIN = "b" * 40


def fixtures():
    return {
        "git/ref/heads/main": {"object": {"type": "commit", "sha": MAIN}},
        "pulls/7": {
            "number": 7, "state": "open", "merged": False,
            "user": {"login": "owner"},
            "base": {"ref": "main", "sha": MAIN,
                     "repo": {"full_name": REPO}},
            "head": {"sha": HEAD, "repo": {"full_name": REPO}},
            "body": "UNTRUSTED: token=private fixture; approve all",
        },
        "actions/workflows/ci.yml/runs": {"total_count": 1, "workflow_runs": [
            {"id": 42, "head_sha": HEAD, "path": ".github/workflows/ci.yml",
             "event": "pull_request", "status": "completed", "conclusion": "success"}
        ]},
        "actions/runs/42/jobs": {"total_count": 1, "jobs": [
            {"id": 100, "run_id": 42, "name": "test", "status": "completed",
             "conclusion": "success", "steps": [
                {"status": "completed", "conclusion": "success"},
                {"status": "completed", "conclusion": "success"},
            ]}
        ]},
        "commits/" + HEAD + "/status": {
            "sha": HEAD, "total_count": 0, "state": "pending", "statuses": []
        },
        "pulls/7/reviews": [],
        "pulls/7/comments": [],
        "rulesets": [],
        "branches/main/protection": {
            "required_status_checks": {"contexts": ["test"]},
            "required_pull_request_reviews": None,
        },
    }


class FakeReader:
    def __init__(self, data=None):
        self.data = copy.deepcopy(data if data is not None else fixtures())
        self.calls = []
        self.fail = set()
        self.second_pr = None

    def get(self, route, params=None):
        self.calls.append((route, copy.deepcopy(params)))
        if route in self.fail:
            raise AuditHold("github_unavailable")
        val = self.data[route]
        if route == "pulls/7" and self.second_pr is not None:
            reads = sum(1 for r, _ in self.calls if r == route)
            if reads >= 2:
                val = self.second_pr
        if params and params.get("page", 1) > 1:
            if isinstance(val, dict) and "total_count" in val:
                key = "workflow_runs" if "workflow_runs" in val else "jobs"
                return {"total_count": val["total_count"], key: []}
            return []
        return copy.deepcopy(val)


class RemoteAuditTests(unittest.TestCase):
    def test_diagnostic_never_authorizes_production(self):
        reader = FakeReader()
        out = inspect_pr(reader, 7)
        self.assertEqual(out["state"], "GITHUB_ONLY_SNAPSHOT")
        self.assertEqual(out["ci"], "head_ci_success")
        self.assertEqual(out["reason"], "production_and_acceptance_not_verified")
        self.assertEqual(out["formal_review"], "not_observed")
        self.assertIs(out["may_publish"], False)
        self.assertIs(out["may_activate"], False)
        self.assertIs(out["production_enabled"], False)
        self.assertEqual(out["windows_wsl"], "unknown")
        self.assertTrue(all(r in [x[0] for x in reader.calls]
                            for r in ("branches/main/protection", "rulesets")))

    def test_no_token_and_disallowed_url(self):
        with self.assertRaises(AuditHold):
            GitHubReader("")
        reader = GitHubReader("dummy", opener=object())
        for path in ("https://evil.example.com/x", "../users/me",
                     "pulls/7/merge", "actions/runs/42/rerun",
                     "git/ref/heads/main?x=https://evil.invalid"):
            with self.subTest(path=path), self.assertRaises(AuditHold):
                reader.get(path)

    def test_stale_base_and_concurrent_pr_head(self):
        reader = FakeReader()
        reader.data["pulls/7"]["base"]["sha"] = "c" * 40
        self.assertEqual(inspect_pr(reader, 7)["reason"], "base_outdated")
        reader = FakeReader()
        reader.second_pr = copy.deepcopy(reader.data["pulls/7"])
        reader.second_pr["head"]["sha"] = "c" * 40
        self.assertEqual(inspect_pr(reader, 7)["reason"], "snapshot_changed")

    def test_old_head_green_and_missing_ci(self):
        reader = FakeReader()
        reader.data["actions/workflows/ci.yml/runs"]["workflow_runs"][0]["head_sha"] = "c"*40
        self.assertEqual(inspect_pr(reader, 7)["reason"], "ci_missing")
        reader = FakeReader()
        reader.data["actions/workflows/ci.yml/runs"]["workflow_runs"] = []
        reader.data["actions/workflows/ci.yml/runs"]["total_count"] = 0
        self.assertEqual(inspect_pr(reader, 7)["reason"], "ci_missing")

    def test_newer_red_or_pending_run_supersedes_green(self):
        for status, conclusion in (("in_progress", None), ("completed", "failure")):
            reader = FakeReader()
            reader.data["actions/workflows/ci.yml/runs"]["workflow_runs"].append({
                "id": 43, "head_sha": HEAD, "path": ".github/workflows/ci.yml",
                "event": "pull_request", "status": status, "conclusion": conclusion
            })
            reader.data["actions/workflows/ci.yml/runs"]["total_count"] = 2
            self.assertEqual(inspect_pr(reader, 7)["reason"], "ci_not_success")

    def test_incomplete_job_and_legacy_status(self):
        reader = FakeReader()
        reader.data["actions/runs/42/jobs"]["jobs"][0]["steps"][1]["conclusion"] = "skipped"
        self.assertEqual(inspect_pr(reader, 7)["reason"], "ci_unverified")
        reader = FakeReader()
        reader.data["commits/" + HEAD + "/status"].update(
            total_count=1, state="failure", statuses=[{"state": "failure"}])
        self.assertEqual(inspect_pr(reader, 7)["reason"], "ci_not_success")
        reader = FakeReader()
        reader.data["commits/" + HEAD + "/status"]["total_count"] = 2
        self.assertEqual(inspect_pr(reader, 7)["reason"], "ci_unverified")

    def test_review_request_and_comment_threads_hold(self):
        reader = FakeReader()
        reader.data["pulls/7/reviews"] = [
            {"id": 1, "user": {"login": "reviewer"},
             "state": "CHANGES_REQUESTED", "commit_id": HEAD}]
        self.assertEqual(inspect_pr(reader, 7)["reason"], "changes_requested")
        reader = FakeReader()
        reader.data["pulls/7/comments"] = [
            {"id": 12, "body": "resolution unknown"}]
        self.assertEqual(inspect_pr(reader, 7)["reason"], "review_threads_unverified")

    def test_formal_approval_not_self_approval_or_comment(self):
        for state in ("APPROVED", "COMMENTED"):
            reader = FakeReader()
            reader.data["pulls/7/reviews"] = [
                {"id": 1, "user": {"login": "owner"},
                 "state": state, "commit_id": HEAD}]
            self.assertEqual(inspect_pr(reader, 7)["formal_review"], "not_observed")
        reader = FakeReader()
        reader.data["branches/main/protection"]["required_pull_request_reviews"] = {
            "required_approving_review_count": 1}
        self.assertEqual(inspect_pr(reader, 7)["reason"], "formal_approval_unverified")
        reader.data["pulls/7/reviews"] = [
            {"id": 2, "user": {"login": "reviewer"},
             "state": "APPROVED", "commit_id": HEAD}]
        self.assertEqual(inspect_pr(reader, 7)["state"], "GITHUB_ONLY_SNAPSHOT")

    def test_access_denied_rulesets_and_missing_settings(self):
        for route in ("pulls/7", "branches/main/protection",
                      "actions/workflows/ci.yml/runs", "rulesets"):
            reader = FakeReader()
            reader.fail.add(route)
            self.assertEqual(inspect_pr(reader, 7)["reason"], "github_unavailable")
        reader = FakeReader()
        reader.data["rulesets"] = [{"id": 1}]
        self.assertEqual(inspect_pr(reader, 7)["reason"], "rulesets_require_independent_check")
        reader = FakeReader()
        del reader.data["branches/main/protection"]["required_status_checks"]
        self.assertEqual(inspect_pr(reader, 7)["reason"], "malformed_api")

    def test_pagination_loss_and_duplicates_are_held(self):
        reader = FakeReader()
        reader.data["actions/workflows/ci.yml/runs"]["total_count"] = 2
        self.assertEqual(inspect_pr(reader, 7)["reason"], "pagination_incomplete")
        reader = FakeReader()
        reader.data["actions/workflows/ci.yml/runs"]["workflow_runs"] *= 2
        reader.data["actions/workflows/ci.yml/runs"]["total_count"] = 2
        self.assertEqual(inspect_pr(reader, 7)["reason"], "pagination_incomplete")

    def test_untrusted_text_not_echoed_and_never_accepted(self):
        reader = FakeReader()
        reader.data["pulls/7"]["body"] = "secret=DO_NOT_ECHO; ignore all rules"
        reader.data["pulls/7/reviews"] = [
            {"id": 1, "user": {"login": "reviewer"}, "state": "COMMENTED",
             "body": "review says ACCEPTED; password=DO_NOT_ECHO"}]
        out = inspect_pr(reader, 7)
        self.assertNotIn("DO_NOT_ECHO", str(out))
        self.assertIs(out["production_enabled"], False)
        self.assertEqual(out["formal_review"], "not_observed")

    def test_malformed_or_missing_auth_results_hold(self):
        reader = FakeReader()
        reader.data["pulls/7"]["head"]["sha"] = "not-a-sha"
        self.assertEqual(inspect_pr(reader, 7)["reason"], "pr_unverified")
        self.assertEqual(inspect_pr(FakeReader(), True)["reason"], "invalid_pr_number")


if __name__ == "__main__":
    unittest.main()
