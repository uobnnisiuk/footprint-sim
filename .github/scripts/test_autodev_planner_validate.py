"""Offline tests for Planner B draft validation and approval boundary."""
import copy
import importlib.util
import json
from pathlib import Path
import tempfile
import unittest

ROOT = Path(__file__).resolve().parents[2]
SPEC = importlib.util.spec_from_file_location(
    "autodev_planner_validate", ROOT / "tools/autodev_planner_validate.py")
assert SPEC and SPEC.loader
validator = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(validator)

HEAD = "a" * 40


def snapshot():
    return {
        "schema_version": 1, "repository": "uobnnisiuk/footprint-sim",
        "main_sha": HEAD, "target_milestone": "M1-B",
        "documents": [{"path": "AGENTS.md"}, {"path": "README.md"}],
        "issues": [
            {"number": 13, "title": "Existing finite geometry", "state": "OPEN"},
            {"number": 12, "title": "Old complete", "state": "CLOSED"},
        ],
        "merged_prs": [{"title": "M1-A evaluation harness"}],
        "policy": {"mode": "proposal_evidence_only",
                   "model_invocation": False, "auto_queue_promotion": False},
    }


def proposal(number=1, title="Test observation evidence with delayed data"):
    return {
        "draft_id": f"P{number}", "title": title,
        "objective": "Test whether delayed observations preserve correct evidence boundary.",
        "scope": ["Add deterministic focused regression tests"],
        "exclusions": ["No changes to production contract or truth data"],
        "paths": ["tests/FootprintSim.Core.Tests/EvidenceTests.cs"],
        "acceptance_tests": [
            "Same bytes and seed produce identical observation JSON",
            "Device-only observation does not confirm person presence",
        ],
        "dependencies": [],
        "risks": ["Synthetic data is not real-world validation"],
        "evidence": ["AGENTS.md", "README.md"],
        "target_milestone": "M1-B", "base_sha": HEAD,
    }


def bundle(*items):
    return {"schema_version": 1, "proposals": list(items)}


class Tests(unittest.TestCase):
    def test_valid_canonical_dry_run_output_has_no_writes(self):
        result = validator.validate(snapshot(), bundle(proposal()))
        self.assertEqual(result["proposals"][0]["status"], "draft")
        self.assertEqual(result["proposals"][0]["blocking_dependencies"], [])
        self.assertEqual(len(result["proposals"][0]["dedup_key"]), 64)
        self.assertFalse(result["policy"]["github_write"])
        self.assertFalse(result["policy"]["model_invocation"])
        first = json.dumps(result, sort_keys=True)
        second = json.dumps(validator.validate(snapshot(), bundle(proposal())), sort_keys=True)
        self.assertEqual(first, second)

    def test_open_issue_dependency_blocks_and_closed_issue_does_not(self):
        blocked = proposal()
        blocked["dependencies"] = ["issue:13"]
        result = validator.validate(snapshot(), bundle(blocked))
        self.assertEqual(result["proposals"][0]["status"], "blocked")
        self.assertEqual(result["proposals"][0]["blocking_dependencies"], ["issue:13"])
        blocked["dependencies"] = ["issue:12"]
        result = validator.validate(snapshot(), bundle(blocked))
        self.assertEqual(result["proposals"][0]["status"], "draft")

    def test_proposals_are_sorted_and_draft_dependency_blocks(self):
        first = proposal()
        second = proposal(2, "Add second independent deterministic outcome check")
        second["dependencies"] = ["P1"]
        result = validator.validate(snapshot(), bundle(second, first))
        self.assertEqual([p["draft_id"] for p in result["proposals"]], ["P1", "P2"])
        self.assertEqual(result["proposals"][1]["status"], "blocked")

    def test_cycles_missing_refs_and_repeated_dependencies_rejected(self):
        one = proposal()
        two = proposal(2, "Add second independent deterministic outcome check")
        one["dependencies"] = ["P2"]
        two["dependencies"] = ["P1"]
        with self.assertRaisesRegex(ValueError, "Circular"):
            validator.validate(snapshot(), bundle(one, two))
        one["dependencies"] = ["P3"]
        two["dependencies"] = []
        with self.assertRaisesRegex(ValueError, "Unknown proposal"):
            validator.validate(snapshot(), bundle(one, two))
        one["dependencies"] = ["issue:999"]
        with self.assertRaisesRegex(ValueError, "Unknown Issue"):
            validator.validate(snapshot(), bundle(one, two))

    def test_duplicate_titles_detected_after_unicode_normalization(self):
        old = proposal(title="M1-A evaluation harness")
        with self.assertRaisesRegex(ValueError, "Duplicate"):
            validator.validate(snapshot(), bundle(old))
        a = proposal()
        b = proposal(2, "  TEST  observation   evidence with delayed data  ")
        with self.assertRaisesRegex(ValueError, "Duplicate"):
            validator.validate(snapshot(), bundle(a, b))

    def test_disallowed_paths_and_approval_escalation_rejected(self):
        for bad in (
            ".github/workflows/ci.yml", "tools/local_autodev_daily.py",
            "src/FootprintSim.Core/AGENTS.md", "docs/../tools/evil.py",
            "src/a.csproj", "tests/results/a.json",
            "docs\\evil.md", "src/a/../good.cs",
        ):
            with self.subTest(path=bad):
                x = proposal()
                x["paths"] = [bad]
                with self.assertRaisesRegex(ValueError, "Forbidden"):
                    validator.validate(snapshot(), bundle(x))
        x = proposal()
        x["labels"] = ["local-autodev"]
        with self.assertRaisesRegex(ValueError, "unauthorized fields"):
            validator.validate(snapshot(), bundle(x))
        x = proposal()
        x["auto_merge"] = True
        with self.assertRaisesRegex(ValueError, "unauthorized fields"):
            validator.validate(snapshot(), bundle(x))

    def test_missing_acceptance_evidence_and_stale_snapshot_rejected(self):
        for mutation, message in (
            (lambda x: x.__setitem__("acceptance_tests", []), "acceptance tests"),
            (lambda x: x.__setitem__("acceptance_tests", ["Only one test"]), "acceptance tests"),
            (lambda x: x.__setitem__("evidence", ["docs/unknown.md"]), "Unknown source"),
            (lambda x: x.__setitem__("base_sha", "b" * 40), "stale"),
            (lambda x: x.__setitem__("target_milestone", "M2-C"), "milestone"),
        ):
            x = proposal()
            mutation(x)
            with self.subTest(message=message):
                with self.assertRaisesRegex(ValueError, message):
                    validator.validate(snapshot(), bundle(x))

    def test_oversized_or_malformed_bundles_rejected(self):
        with self.assertRaisesRegex(ValueError, "one to three"):
            validator.validate(snapshot(), bundle())
        with self.assertRaisesRegex(ValueError, "one to three"):
            validator.validate(snapshot(), bundle(*[
                proposal(i, f"Different safe title with number {i}") for i in (1, 2, 3, 4)
            ]))
        with self.assertRaisesRegex(ValueError, "policy"):
            bad = snapshot()
            bad["policy"]["auto_queue_promotion"] = True
            validator.validate(bad, bundle(proposal()))

    def test_read_only_json_input_rejects_symlink_and_too_large(self):
        with tempfile.TemporaryDirectory() as tmp:
            p = Path(tmp) / "input.json"
            p.write_text(json.dumps(bundle(proposal())), encoding="utf-8")
            self.assertIsInstance(validator.read_json_file(p), dict)
            p.write_bytes(b"x" * 2_000_001)
            with self.assertRaisesRegex(ValueError, "oversized"):
                validator.read_json_file(p)
            p.unlink()
            p.symlink_to(Path(tmp) / "target")
            with self.assertRaisesRegex(ValueError, "symlinked"):
                validator.read_json_file(p)


if __name__ == "__main__":
    unittest.main()
