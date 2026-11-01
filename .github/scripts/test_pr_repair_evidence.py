"""Offline, no-network/no-write contract tests for the R1 evidence processor."""
import copy
import importlib.util
import io
import json
from pathlib import Path
import sys
import tempfile
import unittest
from unittest.mock import patch

ROOT = Path(__file__).resolve().parents[2]
SOURCE = ROOT / "tools" / "pr_repair_evidence.py"
SPEC = importlib.util.spec_from_file_location("pr_repair_evidence", SOURCE)
evidence = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(evidence)
FIXTURE = Path(__file__).with_name("fixtures") / "pr58-repair-evidence.json"


class EvidenceTests(unittest.TestCase):
    def setUp(self):
        self.snapshot = json.loads(FIXTURE.read_text(encoding="utf-8"))

    def test_fixed_pr58_heads_and_28_failures_preserved(self):
        normalized = evidence.normalize(self.snapshot)
        by_sha = {h["sha"]: h for h in normalized["heads"]}
        early = by_sha["e8927604a4fa3548dca37da9e8246afd2668c667"]
        middle = by_sha["df115a35fac89184373111175b0c1c793c02ad35"]
        final = by_sha["179d60fd7022b59edb9387ba3ca1c71969551cfa"]
        self.assertEqual(early["ci_status"]["normal"], "fail")
        self.assertEqual(early["runs"][0]["passed_tests"], 503)
        self.assertEqual(early["runs"][0]["failed_tests"], 1)
        self.assertEqual(middle["runs"][0]["passed_tests"], 503)
        self.assertEqual(middle["runs"][0]["failed_tests"], 28)
        self.assertEqual(middle["runs"][0]["failure_groups"][0]["count"], 28)
        self.assertEqual(len(middle["runs"][0]["failure_groups"][0]["failures"]), 28)
        self.assertEqual(final["ci_status"], {"normal": "pass", "research": "pass"})
        self.assertEqual(final["runs"][0]["passed_tests"], 531)
        self.assertEqual(final["runs"][0]["failed_tests"], 0)
        self.assertEqual(normalized["current_head_readiness"], "hold_acceptance")
        self.assertTrue(all(x["status"] == "needs_review" for x in final["acceptance"]))
        self.assertTrue(all(p["reference_kind"] == "synthetic" and p["verification"] == "unverified"
                            for x in final["acceptance"] for p in x["evidence"]))
        self.assertIs(False, normalized["may_merge"])

    def test_only_latest_head_has_current_readiness(self):
        data = copy.deepcopy(self.snapshot)
        data["current_head_sha"] = "df115a35fac89184373111175b0c1c793c02ad35"
        result = evidence.normalize(data)
        self.assertEqual(result["current_head_readiness"], "hold_ci")

    def test_ci_green_does_not_fix_unimplemented_acceptance(self):
        data = copy.deepcopy(self.snapshot)
        data["heads"][-1]["acceptance"][0]["status"] = "not_implemented"
        result = evidence.normalize(data)
        self.assertEqual(result["current_head_readiness"], "hold_acceptance")

    def test_unverified_approval_does_not_pass(self):
        data = copy.deepcopy(self.snapshot)
        data["approval"]["verified"] = False
        self.assertEqual(evidence.normalize(data)["current_head_readiness"], "hold_approval_unverified")

    def test_wrong_head_evidence_rejected(self):
        data = copy.deepcopy(self.snapshot)
        data["heads"][-1]["acceptance"][0]["evidence"][0]["head_sha"] = data["heads"][0]["sha"]
        with self.assertRaises(evidence.EvidenceError):
            evidence.normalize(data)

    def test_old_run_green_not_applied_to_current_head(self):
        data = copy.deepcopy(self.snapshot)
        data["heads"][-1]["runs"][0]["head_sha"] = data["heads"][0]["sha"]
        with self.assertRaises(evidence.EvidenceError):
            evidence.normalize(data)

    def test_required_step_skipped_is_not_success(self):
        data = copy.deepcopy(self.snapshot)
        last = data["heads"][-1]
        last["runs"][0]["jobs"][0]["steps"][0]["conclusion"] = "skipped"
        self.assertEqual(evidence.normalize(data)["current_head_readiness"], "hold_ci")

    def test_cancelled_pending_and_incomplete_are_not_success(self):
        for status, conclusion in (("completed", "cancelled"), ("in_progress", None)):
            data = copy.deepcopy(self.snapshot)
            run = data["heads"][-1]["runs"][0]
            run["status"], run["conclusion"] = status, conclusion
            self.assertEqual(evidence.normalize(data)["current_head_readiness"], "hold_ci")
        data = copy.deepcopy(self.snapshot)
        data["heads"][-1]["runs"][0]["jobs_complete"] = False
        self.assertEqual(evidence.normalize(data)["current_head_readiness"], "hold_ci")

    def test_incomplete_or_duplicate_input_rejected(self):
        for mutate in (
            lambda d: d["heads"].append(copy.deepcopy(d["heads"][0])),
            lambda d: d["heads"][-1]["runs"][0]["failures"].append({"id":"extra","reason_code":"x","evidence_sha256":"0"*64}),
            lambda d: d.update({"extra_field":"untrusted"}),
            lambda d: d["heads"][-1]["runs"][0].update({"conclusion":"nonsense"})
        ):
            data = copy.deepcopy(self.snapshot)
            mutate(data)
            with self.assertRaises(evidence.EvidenceError):
                evidence.normalize(data)

    def test_stable_with_permuted_input_order(self):
        data = copy.deepcopy(self.snapshot)
        data["heads"].reverse()
        for head in data["heads"]:
            head["runs"].reverse()
            head["acceptance"].reverse()
            for run in head["runs"]:
                run["failures"].reverse()
        self.assertEqual(evidence.canonical_bytes(evidence.normalize(self.snapshot)),
                         evidence.canonical_bytes(evidence.normalize(data)))

    def test_claimed_pass_without_independent_review_is_hold(self):
        data = copy.deepcopy(self.snapshot)
        data["heads"][-1]["acceptance"][0]["review_id"] = None
        r = evidence.normalize(data)
        self.assertEqual(r["current_head_readiness"], "hold_acceptance")
        self.assertEqual(next(item for item in next(h for h in r["heads"] if h["is_current_head"])["acceptance"] if item["id"] == "provenance")["status"], "needs_review")

    def test_read_only_adapter_gets_only_fixed_api_urls(self):
        urls = []
        def fake_get(url):
            urls.append(url)
            if url.endswith("page=1"):
                return {"total_count":1, "jobs":[{"id":7,"status":"completed","conclusion":"success","steps":[]}]}
            return {"id":13, "head_sha":"a"*40, "status":"completed",
                    "conclusion":"success","run_attempt":1, "name":"Footprint Sim CI"}
        result = evidence.fetch_run_readonly("uobnnisiuk/footprint-sim", 13, get_json=fake_get)
        self.assertTrue(result["read_only"])
        self.assertTrue(result["acceptance_unverified"])
        self.assertEqual(len(urls), 2)
        self.assertTrue(all("https://api.github.com/repos/" in u for u in urls))
        with self.assertRaises(evidence.EvidenceError):
            evidence.fetch_run_readonly("host.invalid/../private", 13, get_json=fake_get)

    def test_incomplete_job_pagination_stays_unverified(self):
        def fake(url):
            if "jobs?" in url:
                return {"total_count":101, "jobs":[{"id":1, "steps":[]}]}
            return {"id":13}
        r = evidence.fetch_run_readonly("uobnnisiuk/footprint-sim", 13, get_json=fake)
        self.assertFalse(r["jobs_complete"])

    def test_cli_snapshot_never_touches_network_or_writes(self):
        with tempfile.TemporaryDirectory() as tmp:
            path = Path(tmp) / "case.json"
            path.write_text(json.dumps(self.snapshot), encoding="utf-8")
            with patch.object(evidence, "_api_json", side_effect=AssertionError("network")), \
                 patch.object(evidence, "fetch_run_readonly", side_effect=AssertionError("fetch")), \
                 patch("sys.stdout", new_callable=lambda: io.TextIOWrapper(io.BytesIO(), encoding="utf-8")):
                self.assertEqual(evidence.main(["--snapshot", str(path)]), 0)
            self.assertEqual(sorted(p.name for p in Path(tmp).iterdir()), ["case.json"])


    def test_missing_approved_acceptance_is_hold_even_when_ci_green(self):
        data = copy.deepcopy(self.snapshot)
        data["heads"][-1]["acceptance"] = [
            item for item in data["heads"][-1]["acceptance"]
            if item["id"] != "unknown_preservation"
        ]
        normalized = evidence.normalize(data)
        self.assertEqual(normalized["current_head_readiness"], "hold_acceptance")
        current = next(head for head in normalized["heads"] if head["is_current_head"])
        self.assertEqual(current["missing_approved_acceptance"], ["unknown_preservation"])
        # Even a snapshot that changes its own claimed approval cannot amend
        # the independently pinned PR58 acceptance baseline.
        data["approval"]["reference"] = "issue-56-shortened"
        self.assertEqual(evidence.normalize(data)["current_head_readiness"], "hold_acceptance")

    def test_non_historical_pr_needs_external_approved_baseline(self):
        data = copy.deepcopy(self.snapshot)
        data["pr_number"] = 67
        self.assertEqual(evidence.normalize(data)["current_head_readiness"], "hold_acceptance")
        self.assertEqual(evidence.normalize(
            data, approved_acceptance_ids=["provenance", "cli_integration",
                                           "unknown_preservation", "snapshot_integrity"]
        )["current_head_readiness"], "hold_acceptance")

    def test_expired_missing_and_unverified_logs_fail_closed(self):
        for state in ("expired", "missing", "unverified"):
            data = copy.deepcopy(self.snapshot)
            data["heads"][-1]["runs"][0]["log_evidence"] = {
                "state": state, "review_ref": None
            }
            normalized = evidence.normalize(data)
            self.assertEqual(normalized["current_head_readiness"], "hold_ci")
            current = next(head for head in normalized["heads"] if head["is_current_head"])
            self.assertEqual(current["ci_status"]["normal"], "unknown")
        data = copy.deepcopy(self.snapshot)
        data["heads"][-1]["runs"][0]["log_evidence"]["review_ref"] = None
        with self.assertRaises(evidence.EvidenceError):
            evidence.normalize(data)

    def test_required_step_cannot_be_declassified_by_snapshot(self):
        data = copy.deepcopy(self.snapshot)
        data["heads"][-1]["runs"][0]["jobs"][0]["steps"][0]["required"] = False
        normalized = evidence.normalize(data)
        self.assertEqual(normalized["current_head_readiness"], "hold_ci")
        current = next(head for head in normalized["heads"] if head["is_current_head"])
        self.assertFalse(current["runs"][0]["required_step_policy_complete"])
        data = copy.deepcopy(self.snapshot)
        data["heads"][-1]["runs"][0]["jobs"][0]["steps"].pop(0)
        self.assertEqual(evidence.normalize(data)["current_head_readiness"], "hold_ci")

    def test_untrusted_names_and_reason_codes_never_echo(self):
        secret_step = "token=ghp_NotARealCredential_TEST"
        data = copy.deepcopy(self.snapshot)
        data["heads"][-1]["runs"][0]["jobs"][0]["steps"].append(
            {"name": secret_step, "required": False,
             "status": "completed", "conclusion": "success"})
        result = evidence.normalize(data)
        self.assertEqual(result["current_head_readiness"], "hold_ci")
        self.assertNotIn(secret_step, evidence.canonical_bytes(result).decode("utf-8"))
        secret_reason = "ghp_NotARealCredential_TEST"
        data = copy.deepcopy(self.snapshot)
        data["heads"][1]["runs"][0]["failures"][0]["reason_code"] = secret_reason
        with self.assertRaises(evidence.EvidenceError) as caught:
            evidence.normalize(data)
        self.assertNotIn(secret_reason, str(caught.exception))

    def test_adapter_redacts_remote_untrusted_step_names(self):
        secret = "ghp_NotARealCredential_TEST"
        def fake(url):
            if "jobs?" in url:
                return {"total_count": 1, "jobs": [
                    {"id": 7, "status": "completed", "conclusion": "success",
                     "steps": [{"name": secret, "status": "completed", "conclusion": "success"}]}
                ]}
            return {"id": 13, "name": secret, "run_attempt": 1,
                    "status": "completed", "conclusion": "success", "head_sha": "a" * 40}
        result = evidence.fetch_run_readonly("uobnnisiuk/footprint-sim", 13, get_json=fake)
        self.assertEqual(result["log_evidence"]["state"], "unverified")
        self.assertFalse(result["required_steps_verified"])
        self.assertNotIn(secret, evidence.canonical_bytes(result).decode("utf-8"))


    def test_green_run_does_not_attest_invented_test_or_review(self):
        data = copy.deepcopy(self.snapshot)
        item = data["heads"][-1]["acceptance"][0]
        item["evidence"][0]["test_id"] = "made_up_test_not_in_ci"
        item["evidence"][0]["reference_kind"] = "claimed_observed"
        item["review_id"] = "claimed_review_without_proof"
        result = evidence.normalize(data)
        current = next(h for h in result["heads"] if h["is_current_head"])
        proof = next(x for x in current["acceptance"] if x["id"] == "provenance")["evidence"][0]
        self.assertEqual(proof["verification"], "unverified")
        self.assertEqual(result["current_head_readiness"], "hold_acceptance")

    def test_external_evidence_requires_exact_observed_ref(self):
        data = copy.deepcopy(self.snapshot)
        item = data["heads"][-1]["acceptance"][0]
        proof = item["evidence"][0]
        proof["test_id"] = "example_only_not_a_claim_of_actual_ci"
        proof["reference_kind"] = "claimed_observed"
        ref = (data["current_head_sha"], proof["run_id"], proof["attempt"],
               proof["test_id"], item["review_id"])
        passed = evidence.normalize(data, verified_test_evidence=[ref])
        current = next(h for h in passed["heads"] if h["is_current_head"])
        item_result = next(x for x in current["acceptance"] if x["id"] == "provenance")
        self.assertEqual(item_result["status"], "passed")
        self.assertEqual(passed["current_head_readiness"], "hold_acceptance")
        wrong = (ref[0], ref[1], ref[2] + 1, ref[3], ref[4])
        mismatched = evidence.normalize(data, verified_test_evidence=[wrong])
        current = next(h for h in mismatched["heads"] if h["is_current_head"])
        self.assertEqual(next(x for x in current["acceptance"] if x["id"] == "provenance")["status"], "needs_review")
        proof["reference_kind"] = "synthetic"
        synthetic = evidence.normalize(data, verified_test_evidence=[ref])
        current = next(h for h in synthetic["heads"] if h["is_current_head"])
        self.assertEqual(next(x for x in current["acceptance"] if x["id"] == "provenance")["status"], "needs_review")

    def test_secret_shaped_identifiers_never_echo(self):
        secret = "ghp_NotARealCredential_TEST"
        for field in ("id", "review_id"):
            data = copy.deepcopy(self.snapshot)
            data["heads"][-1]["acceptance"][0][field] = secret
            self.assertNotIn(secret, evidence.canonical_bytes(evidence.normalize(data)).decode("utf-8"))
        data = copy.deepcopy(self.snapshot)
        data["heads"][-1]["acceptance"][0]["evidence"][0]["test_id"] = secret
        self.assertNotIn(secret, evidence.canonical_bytes(evidence.normalize(data)).decode("utf-8"))
        data = copy.deepcopy(self.snapshot)
        data["pr_number"] = 67
        data["heads"][-1]["acceptance"][0]["id"] = secret
        trusted_baseline = [secret, "cli_integration", "unknown_preservation", "snapshot_integrity"]
        public = evidence.canonical_bytes(evidence.normalize(
            data, approved_acceptance_ids=trusted_baseline)).decode("utf-8")
        self.assertNotIn(secret, public)

    def test_secret_shaped_unknown_keys_never_echo_in_cli_errors(self):
        secret = "ghp_NotARealCredential_TEST"
        data = copy.deepcopy(self.snapshot)
        data["heads"][-1]["acceptance"][0][secret] = True
        with self.assertRaises(evidence.EvidenceError) as caught:
            evidence.normalize(data)
        self.assertNotIn(secret, str(caught.exception))
        with tempfile.TemporaryDirectory() as tmp:
            path = Path(tmp) / "untrusted.json"
            path.write_text(json.dumps(data), encoding="utf-8")
            stderr = io.StringIO()
            with patch("sys.stderr", stderr), self.assertRaises(SystemExit) as caught_exit:
                evidence.main(["--snapshot", str(path)])
            self.assertEqual(caught_exit.exception.code, 2)
            self.assertNotIn(secret, stderr.getvalue())

if __name__ == "__main__":
    unittest.main()
