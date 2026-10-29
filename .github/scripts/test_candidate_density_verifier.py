"""Small synthetic negative tests for the independent F19 artifact verifier.

No simulator, model, network, or archived experiment output is needed. This file
belongs in .github/scripts; its sibling fallback supports isolated review copies.
"""
from contextlib import redirect_stderr
from copy import deepcopy
import hashlib
import importlib.util
from io import StringIO
import json
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest
from unittest.mock import patch

VERIFIER = Path(__file__).resolve().parents[2] / "scripts/verify-candidate-density.py"
if not VERIFIER.is_file():
    VERIFIER = Path(__file__).with_name("verify-candidate-density.py")
SPEC = importlib.util.spec_from_file_location("candidate_density_verifier", VERIFIER)
verifier = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(verifier)


class CandidateDensityVerifierTests(unittest.TestCase):
    @staticmethod
    def public_run():
        return {"seed": 7, "world": {
            "candidateBaseTick": 10, "regionUniverse": ["c", "b", "a"],
            "candidateStates": [{"candidateKey": "original", "publicSourceIdentifier": "original-model",
                                 "regionId": "a", "startTick": 10, "isTruthCandidate": True}],
            "locationObservedRegionIds": ["a", "b"],
            "participants": [
                {"participantId": "observed-a", "allowedRegionIds": ["a"], "isTruthParticipant": True},
                {"participantId": "unobserved", "allowedRegionIds": ["c"], "isTruthParticipant": False},
                {"participantId": "observed-b", "allowedRegionIds": ["b"], "isTruthParticipant": False}],
            "observedPossibleParticipantIds": ["observed-b", "observed-a"],
            "laterEventTick": 15, "durationMaximumTicks": 6, "durationMinimumTicks": 4,
            "truthCandidateKey": "original", "truthParticipantId": "observed-a"}}

    @staticmethod
    def manifest_fixture():
        sha = "a" * 40
        rows = [{"seed": seed, "originalWorldBeforeSignature": "1" * 64,
                 "rawEvidenceSignature": "2" * 64, "sourceGraphSignature": "3" * 64,
                 "support": {"supportContentSignature": "4" * 64},
                 "arms": [{"fixedConstraintSignature": "5" * 64}]}
                for seed in range(200)]
        manifest = {
            "experimentId": "m1f19-nested-point-support-density", "sourceCommitSha": sha,
            "dirtyWorkingTree": False, "protocolContentSha256": verifier.PROTOCOL_SHA,
            "baselineExperimentContentSha256": verifier.EXPERIMENT_SHA,
            "baselineFinalCommit": verifier.BASELINE_FINAL_COMMIT,
            "supportRuleId": verifier.SUPPORT_RULE, "orderingRuleId": verifier.ORDERING_RULE,
            "plateauRuleId": verifier.PLATEAU_RULE, "seeds": list(range(200)),
            "truthMappingRule": verifier.TRUTH_RULE, "outputFiles": list(verifier.OUTPUT_FILES),
        }
        fixed = [{"seed": row["seed"], "originalWorldBeforeSignature": row["originalWorldBeforeSignature"],
                  "rawEvidenceSignature": row["rawEvidenceSignature"], "sourceGraphSignature": row["sourceGraphSignature"],
                  "supportContentSignature": row["support"]["supportContentSignature"],
                  "fixedConstraintSignature": row["arms"][0]["fixedConstraintSignature"]} for row in rows]
        outputs = [{"seed": row["seed"], "support": row["support"], "arms": row["arms"]} for row in rows]
        manifest["fixedInputSignature"] = verifier.registered_signature(fixed)
        manifest["publicOutputSignature"] = verifier.registered_signature(outputs)
        return manifest, rows, sha

    def test_public_projection_uses_only_observed_participant_alternatives(self):
        projected = verifier.public_input(self.public_run())
        self.assertEqual(projected["participant"], ["a", "b"])
        self.assertEqual(projected["regions"], ["a", "b", "c"])
        self.assertEqual((projected["timeLower"], projected["timeUpper"]), (9, 11))

    def test_projection_rejects_unknown_or_empty_observed_participant_sets(self):
        for alternatives in (["missing-person"], []):
            with self.subTest(alternatives=alternatives):
                run = self.public_run()
                run["world"]["observedPossibleParticipantIds"] = alternatives
                with self.assertRaises(AssertionError):
                    verifier.public_input(run)

    def test_truth_substitution_and_participant_order_do_not_change_public_projection(self):
        run = self.public_run()
        expected = verifier.public_input(run)
        run["world"]["truthParticipantId"] = "observed-b"
        run["world"]["truthCandidateKey"] = "different-evaluation-label"
        run["world"]["candidateStates"][0]["isTruthCandidate"] = False
        for participant in run["world"]["participants"]:
            participant["isTruthParticipant"] = not participant["isTruthParticipant"]
        run["world"]["participants"].reverse()
        run["world"]["observedPossibleParticipantIds"].reverse()
        self.assertEqual(verifier.public_input(run), expected)

    def test_registered_signature_has_frozen_indented_json_bytes(self):
        value = {"ruleId": "test-rule", "seed": 7, "cells": [{"tick": 10, "valid": True}], "missing": None}
        literal = b'{\n  "ruleId": "test-rule",\n  "seed": 7,\n  "cells": [\n    {\n      "tick": 10,\n      "valid": true\n    }\n  ],\n  "missing": null\n}'
        self.assertEqual(verifier.registered_signature(value), hashlib.sha256(literal).hexdigest())

    def test_registered_signature_refuses_unsupported_serialization_forms(self):
        for value in (1.25, float("nan"), "Japanese-\u65e5", "<tag>", "a+b", 'quote"', "back\\slash", "line\n"):
            with self.subTest(value=value), self.assertRaises(AssertionError):
                verifier.registered_signature({"value": value})

    def test_support_identity_recomputes_cartesian_grid_hash(self):
        public = {"seed": 7, "baseTick": 10, "regions": ["a", "b"]}
        expected = {"ruleId": verifier.SUPPORT_RULE, "seed": 7, "baseTick": 10,
                    "registeredRegionIds": ["a", "b"], "cells": [
                        {"startTick": tick, "regionId": region} for tick, region in
                        [(7, "a"), (7, "b"), (8, "a"), (8, "b"), (9, "a"), (9, "b"),
                         (10, "a"), (10, "b"), (11, "a"), (11, "b"), (12, "a"), (12, "b"),
                         (13, "a"), (13, "b")]]}
        support = {"seed": 7, "baseTick": 10, "registeredRegionIds": ["a", "b"],
                   "supportCellCount": 14, "supportContentSignature": verifier.registered_signature(expected)}
        verifier.verify_support_identity(support, public)
        for field, value in (("seed", 8), ("baseTick", 11), ("registeredRegionIds", ["b", "a"]),
                             ("supportCellCount", 13), ("supportContentSignature", "0" * 64)):
            altered = dict(support, **{field: value})
            with self.subTest(field=field), self.assertRaises(AssertionError):
                verifier.verify_support_identity(altered, public)

    def test_point_reference_is_required_and_byte_anchored(self):
        with self.assertRaises(AssertionError):
            verifier.load_point_reference(None)
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / "point.jsonl"
            raw = b"".join(json.dumps({"seed": seed}).encode() + b"\n" for seed in range(200))
            path.write_bytes(raw)
            with patch.object(verifier, "POINT_REFERENCE_SHA", hashlib.sha256(raw).hexdigest()):
                self.assertEqual(len(verifier.load_point_reference(path)), 200)
                path.write_bytes(raw + b"\n")
                with self.assertRaisesRegex(AssertionError, "reference changed"):
                    verifier.load_point_reference(path)

    def test_point_reference_rejects_missing_duplicate_or_reordered_seeds(self):
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / "point.jsonl"
            for seeds in (list(range(199)), [0] + list(range(199)), list(reversed(range(200)))):
                raw = b"".join(json.dumps({"seed": seed}).encode() + b"\n" for seed in seeds)
                path.write_bytes(raw)
                with self.subTest(seeds=seeds[:3]), \
                     patch.object(verifier, "POINT_REFERENCE_SHA", hashlib.sha256(raw).hexdigest()), \
                     self.assertRaisesRegex(AssertionError, "seed sequence"):
                    verifier.load_point_reference(path)

    def test_clean_manifest_accepts_exact_source_rules_inputs_and_outputs(self):
        manifest, rows, sha = self.manifest_fixture()
        verifier.verify_manifest(manifest, rows, sha)

    def test_manifest_rejects_source_dirty_rules_hashes_and_artifact_list_mutations(self):
        manifest, rows, sha = self.manifest_fixture()
        mutations = [("sourceCommitSha", "b" * 40), ("dirtyWorkingTree", True), ("dirtyWorkingTree", 0),
                     ("experimentId", "different-experiment"), ("protocolContentSha256", "0" * 64),
                     ("baselineExperimentContentSha256", "0" * 64), ("baselineFinalCommit", "0" * 40),
                     ("supportRuleId", "altered"), ("orderingRuleId", "altered"), ("plateauRuleId", "altered"),
                     ("truthMappingRule", "altered"), ("seeds", list(range(199))),
                     ("outputFiles", list(reversed(verifier.OUTPUT_FILES))),
                     ("fixedInputSignature", "0" * 64), ("publicOutputSignature", "0" * 64)]
        for key, value in mutations:
            with self.subTest(key=key, value=value), self.assertRaises(AssertionError):
                verifier.verify_manifest(dict(manifest, **{key: value}), rows, sha)

    def test_manifest_rejects_unknown_fields_and_ambiguous_expected_source(self):
        manifest, rows, sha = self.manifest_fixture()
        with self.assertRaises(AssertionError):
            verifier.verify_manifest(dict(manifest, extra="ignored?"), rows, sha)
        for invalid in (None, "HEAD", "abc123", "A" * 40):
            with self.subTest(invalid=invalid), self.assertRaises(AssertionError):
                verifier.verify_manifest(manifest, rows, invalid)

    def test_manifest_rejects_changed_input_and_output_payloads(self):
        manifest, rows, sha = self.manifest_fixture()
        altered = deepcopy(rows)
        altered[0]["rawEvidenceSignature"] = "6" * 64
        with self.assertRaisesRegex(AssertionError, "fixed input"):
            verifier.verify_manifest(manifest, altered, sha)
        altered = deepcopy(rows)
        altered[0]["arms"][0]["extraPublicValue"] = "altered"
        with self.assertRaisesRegex(AssertionError, "public output"):
            verifier.verify_manifest(manifest, altered, sha)

    def test_manifest_rejects_duplicate_or_missing_world_rows(self):
        manifest, rows, sha = self.manifest_fixture()
        for altered in (rows[:-1], [rows[0]] + rows[:-1], list(reversed(rows))):
            with self.subTest(count=len(altered)), self.assertRaises(AssertionError):
                verifier.verify_manifest(manifest, altered, sha)

    def test_public_identity_hashes_reject_candidate_representation_and_provenance_drift(self):
        public = {"candidates": [{"candidateKey": "one", "state": {"provenance": [{"sourceKind": "publicModel"}]}}]}
        input_hash = verifier.registered_signature(public["candidates"])
        support_hash = "7" * 64
        constraint_hash = "8" * 64
        arm = {"armId": "half-missing", "candidateInputSignature": input_hash,
               "fixedConstraintSignature": constraint_hash,
               "representationId": verifier.registered_signature({
                   "supportContentSignature": support_hash, "armId": "half-missing", "candidateInputSignature": input_hash}),
               "candidates": [{"candidateKey": "one", "provenanceSignature":
                               verifier.registered_signature(public["candidates"][0]["state"]["provenance"])}]}
        verifier.verify_public_identity_hashes(arm, public, support_hash, constraint_hash)
        for field in ("candidateInputSignature", "representationId", "fixedConstraintSignature"):
            altered = dict(arm, **{field: "0" * 64})
            with self.subTest(field=field), self.assertRaises(AssertionError):
                verifier.verify_public_identity_hashes(altered, public, support_hash, constraint_hash)
        altered = deepcopy(arm)
        altered["candidates"][0]["provenanceSignature"] = "0" * 64
        with self.assertRaises(AssertionError):
            verifier.verify_public_identity_hashes(altered, public, support_hash, constraint_hash)

    def test_profile_membership_rejects_changed_keys_native_ids_counts_work_and_axes(self):
        expected = {"keys": ["one"], "count": 1, "work": 6}
        profile = {"candidateKeys": ["one"], "candidateIds": ["native-one"], "candidateCount": 1,
                   "candidateGenerationWork": 6, "activeAxes": ["location"]}
        ids = {"native-one": "one", "native-two": "two"}
        verifier.verify_profile_membership(profile, expected, ids, ("location",))
        for field, value in (("candidateKeys", ["two"]), ("candidateIds", ["native-two"]),
                             ("candidateIds", ["native-one", "native-one"]), ("candidateCount", 2),
                             ("candidateGenerationWork", 7), ("activeAxes", ["time"])):
            with self.subTest(field=field, value=value), self.assertRaises(AssertionError):
                verifier.verify_profile_membership(dict(profile, **{field: value}), expected, ids, ("location",))

    def test_minimum_verifier_rejects_changed_counts_or_family(self):
        expected = {"minimumCount": 1, "profilesAtMinimum": ["location", "location-time"],
                    "inclusionMinimalProfiles": ["location"]}
        actual = {"minimumCandidateCount": 1, "profilesAtMinimumCount": ["location", "location-time"],
                  "inclusionMinimalProfileIds": ["location"], "allMinimumProfilesReturnSameKeySet": True}
        verifier.verify_minimum(actual, expected)
        for field, value in (("minimumCandidateCount", 2), ("profilesAtMinimumCount", ["location"]),
                             ("inclusionMinimalProfileIds", ["time"]), ("allMinimumProfilesReturnSameKeySet", False)):
            with self.subTest(field=field), self.assertRaises(AssertionError):
                verifier.verify_minimum(dict(actual, **{field: value}), expected)

    def test_diagnostic_verifier_rejects_changed_plateau_flags_domain_and_witnesses(self):
        expected = {"classification": "constraintToCandidatePlateau", "top": False, "implied": False,
                    "regionImplicationApplicable": True, "implicationCounterexampleRegions": ["b"]}
        actual = {"domainId": "registered", "domainRegionIds": ["a", "b"],
                  "implicationDomainRegionIds": ["a", "b"], "classification": expected["classification"],
                  "addedPredicateIsTopForCandidateDomain": False, "addedPredicateImpliedByBaselinePredicates": False,
                  "regionImplicationApplicable": True, "implicationCounterexampleRegionIds": ["b"]}
        verifier.verify_diagnostic(actual, expected, ["a", "b"])
        for field, value in (("classification", "saturation"), ("addedPredicateIsTopForCandidateDomain", True),
                             ("addedPredicateImpliedByBaselinePredicates", True), ("domainRegionIds", ["a"]),
                             ("implicationDomainRegionIds", ["a"]), ("implicationCounterexampleRegionIds", [])):
            with self.subTest(field=field), self.assertRaises(AssertionError):
                verifier.verify_diagnostic(dict(actual, **{field: value}), expected, ["a", "b"])

    def test_statistics_rejects_changed_distribution_even_when_mean_is_unchanged(self):
        actual = {"mean": 2, "median": 2, "minimum": 1, "maximum": 3,
                  "distribution": [{"value": 1, "count": 1}, {"value": 2, "count": 1}, {"value": 3, "count": 1}]}
        verifier.verify_statistics(actual, [1, 2, 3])
        actual["distribution"] = [{"value": 1, "count": 2}, {"value": 3, "count": 2}]
        with self.assertRaises(AssertionError):
            verifier.verify_statistics(actual, [1, 2, 3])

    def test_actual_cli_requires_point_reference_and_expected_source_before_file_reads(self):
        args = [str(VERIFIER), "--baseline-runs", "missing.jsonl", "--output", "unused",
                "--actual-runs", "also-missing.jsonl"]
        with patch.object(sys, "argv", args), redirect_stderr(StringIO()), self.assertRaises(SystemExit) as raised:
            verifier.main()
        self.assertEqual(raised.exception.code, 2)

    def test_verifier_refuses_assertion_disabled_execution(self):
        completed = subprocess.run([sys.executable, "-O", str(VERIFIER)],
                                   capture_output=True, text=True, check=False)
        self.assertNotEqual(completed.returncode, 0)
        self.assertIn("Assertions must be enabled", completed.stderr)


if __name__ == "__main__":
    unittest.main()
