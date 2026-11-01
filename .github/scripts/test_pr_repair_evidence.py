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
        self.assertEqual(normalized["current_head_readiness"], "evidence_complete_for_independent_review")
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
        self.assertEqual(next(x for x in r["heads"] if x["is_current_head"])["acceptance"][0]["status"], "needs_review")

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


if __name__ == "__main__":
    unittest.main()
