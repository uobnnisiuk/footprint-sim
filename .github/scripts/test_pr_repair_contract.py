"""R2 offline fixtures. No network, git writes, model process, or candidate execution."""
import copy
from dataclasses import replace
import importlib.util
import json
from pathlib import Path
import sys
import unittest
from unittest.mock import patch

ROOT = Path(__file__).resolve().parents[2]
SPEC = importlib.util.spec_from_file_location("pr_repair_contract", ROOT / "tools/pr_repair_contract.py")
contract = importlib.util.module_from_spec(SPEC)
sys.modules[SPEC.name] = contract
SPEC.loader.exec_module(contract)
SOURCE = "src/FootprintSim.Core/Inference/SearchProjection/PublicSearchProjection.cs"
TEST = "tests/FootprintSim.Core.Tests/PublicSearchProjectionTests.cs"
DOC = "docs/m1f20-public-search-mapping.md"
OUTSIDE = "src/FootprintSim.Core/Inference/Other.cs"
SHA = "1" * 40
DIGEST = "2" * 64


def tree(sha, contents):
    return {"root_sha":sha, "complete":True, "entries":{
        path:{"oid":contract.blob(text), "mode":"100644", "text":text}
        for path,text in contents.items()}}


def fixture():
    before = {SOURCE:"public class A { }\n", TEST:"Assert.Equal(1, 1);\n",
              DOC:"original note\n", OUTSIDE:"not touched\n"}
    current = dict(before); current[SOURCE] = "public class A { int X; }\n"
    candidate = dict(current); candidate[SOURCE] = "public class A { int X; int Y; }\n"
    trees = {"approved":tree("a"*40,before), "pr_base":tree("b"*40,before),
             "current":tree("c"*40,current), "candidate":tree("d"*40,candidate)}
    approval = {"schema":contract.SCHEMA, "root_issue":56, "root_pr":58,
                "issue_title_sha256":DIGEST, "issue_body_sha256":"3"*64,
                "reservation_id":6497211417, "approval_result_id":6497248287,
                "protocol_commit":"4"*40, "approved_main_sha":"5"*40,
                "paths":[{"path":p,"approved_oid":trees["approved"]["entries"][p]["oid"],
                          "allowed_operations":["modify"]} for p in (SOURCE,TEST,DOC)],
                "acceptance":[{"id":i,"expected_sha256":"6"*64} for i in ("cli","known_unknown")],
                "required_verifications":[{"id":"existing-tests","command_sha256":"7"*64}],
                "limits":{"max_files":3,"max_lines":600}}
    request = {"schema":contract.SCHEMA,"policy":contract.POLICIES["substantive"],
               "kind":"substantive", "root_issue":56,"root_pr":58,
               "issue_title_sha256":approval["issue_title_sha256"],
               "issue_body_sha256":approval["issue_body_sha256"],
               "reservation_id":approval["reservation_id"],
               "approval_result_id":approval["approval_result_id"],
               "protocol_commit":approval["protocol_commit"],
               "approved_main_sha":approval["approved_main_sha"],
               "current_main_sha":"8"*40,"current_head_sha":"9"*40,
               "original_pr_base_sha":"b"*40,"review_finding_id":"finding:1",
               "reviewer_run_id":"review:2","workflow":"Footprint-Sim-CI",
               "workflow_run_id":41040441468,"workflow_attempt":1,"job_id":124011624239,
               "observed_head_sha":"9"*40,"failure_code":"region_id_order_assumption",
               "evidence_sha256":"a"*64,
               "paths":[{"path":SOURCE,
                         "before_oid":trees["current"]["entries"][SOURCE]["oid"],
                         "after_oid":trees["candidate"]["entries"][SOURCE]["oid"],
                         "operation":"modify"}],
               "acceptance":copy.deepcopy(approval["acceptance"]),
               "required_verifications":copy.deepcopy(approval["required_verifications"]),
               "limits":{"max_files":3,"max_lines":600},
               "repair_reservation_id":"reservation:1", "expires_at":2000000000,"attempt":1}
    trusted = contract.TrustedFacts(
        approval_digest=contract.digest(approval), issue_title_sha256=approval["issue_title_sha256"],
        issue_body_sha256=approval["issue_body_sha256"], reservation_id=approval["reservation_id"],
        approval_result_id=approval["approval_result_id"], protocol_commit=approval["protocol_commit"],
        current_main_sha=request["current_main_sha"], current_head_sha=request["current_head_sha"],
        original_pr_base_sha=request["original_pr_base_sha"], finding_id=request["review_finding_id"],
        reviewer_run_id=request["reviewer_run_id"],
        observed_workflow=request["workflow"],
        observed_failure_code=request["failure_code"],
        workflow_run_id=request["workflow_run_id"],
        workflow_attempt=request["workflow_attempt"],job_id=request["job_id"],
        observed_head_sha=request["observed_head_sha"],evidence_sha256=request["evidence_sha256"],
        tree_roots=tuple((stage, snap["root_sha"]) for stage,snap in trees.items()),
        now_epoch=1800000000, prior_changed_lines=0, prior_changed_paths=(),
        first_pr_changed_paths=(SOURCE,), previous_attempts=0,
        repair_reservation_id=request["repair_reservation_id"],
        repair_expires_at=request["expires_at"],
        repair_policy=request["policy"], repair_kind=request["kind"],
        repair_root_issue=request["root_issue"], repair_root_pr=request["root_pr"],
        repair_main_sha=request["current_main_sha"],
        repair_head_sha=request["current_head_sha"], repair_attempt=request["attempt"])
    return approval, request, trees, trusted


def change(trees, request, path, content, *, include_request=True):
    snap=trees["candidate"]["entries"][path]
    snap["text"]=content
    snap["oid"]=contract.blob(content)
    if include_request:
        entry=next((x for x in request["paths"] if x["path"]==path),None)
        if entry is None:
            request["paths"].append({"path":path,"before_oid":trees["current"]["entries"][path]["oid"],
                                     "after_oid":snap["oid"],"operation":"modify"})
        else: entry["after_oid"]=snap["oid"]


class ContractTests(unittest.TestCase):
    def setUp(self):
        self.a,self.r,self.t,self.v=fixture()

    def check(self,reason):
        with self.assertRaises(contract.ContractError) as context:
            contract.validate(self.a,self.r,self.t,self.v)
        self.assertEqual(str(context.exception),reason)

    def test_pr58_scope_is_only_preflight_never_merge_or_repair(self):
        result=contract.validate(self.a,self.r,self.t,self.v)
        self.assertEqual(result["status"],"scope_valid_for_independent_review")
        self.assertIs(result["may_merge"],False)
        self.assertIs(result["may_repair"],False)
        self.assertEqual(result["changed_files"],1)

    def test_approval_sha_cannot_be_invented_by_request_or_manifest(self):
        self.r["issue_body_sha256"]="0"*64; self.check("approval_mismatch")
        self.a,self.r,self.t,self.v=fixture()
        self.a["limits"]["max_lines"]=601; self.check("invalid_value")
        self.a,self.r,self.t,self.v=fixture()
        self.a["paths"][0]["path"]=OUTSIDE; self.check("untrusted_provenance")
        self.a,self.r,self.t,self.v=fixture()
        self.v=replace(self.v,issue_body_sha256="0"*64); self.check("untrusted_provenance")

    def test_original_pr_base_drift_from_exact_approval_blob_is_rejected(self):
        # The initial PR changed SOURCE. Even with all three diffs confined
        # to approved paths, its original base B must match approved blob A.
        old_base = "public class A { int PreExistingUnapproved; }\n"
        self.t["pr_base"]["entries"][SOURCE]["text"] = old_base
        self.t["pr_base"]["entries"][SOURCE]["oid"] = contract.blob(old_base)
        self.assertNotEqual(self.t["approved"]["entries"][SOURCE]["oid"],
                            self.t["pr_base"]["entries"][SOURCE]["oid"])
        self.check("approval_blob_mismatch")

    def test_every_approved_path_must_match_original_base_even_if_not_repaired(self):
        # A path omitted from this repair still carries an exact-blob approval.
        altered_test_base = "Assert.Equal(0, 1);\n"
        self.t["pr_base"]["entries"][TEST]["text"] = altered_test_base
        self.t["pr_base"]["entries"][TEST]["oid"] = contract.blob(altered_test_base)
        self.check("approval_blob_mismatch")
        # Missing originally approved files must also fail closed.
        self.a,self.r,self.t,self.v=fixture()
        for stage in ("pr_base", "current", "candidate"):
            del self.t[stage]["entries"][DOC]
        self.check("approval_blob_mismatch")

    def test_full_diff_guard_rejects_unapproved_original_change(self):
        # Initial PR already touched an unapproved file; even when repair only touches source.
        self.t["current"]["entries"][OUTSIDE]["text"]="unauthorized\n"
        self.t["current"]["entries"][OUTSIDE]["oid"]=contract.blob("unauthorized\n")
        self.t["candidate"]["entries"][OUTSIDE]=copy.deepcopy(self.t["current"]["entries"][OUTSIDE])
        self.check("out_of_scope")

    def test_full_diff_guard_rejects_unapproved_candidate_edit(self):
        change(self.t,self.r,OUTSIDE,"unauthorized\n",include_request=False)
        self.check("out_of_scope")

    def test_manifest_cannot_expand_file_scope_even_under_general_src_guard(self):
        change(self.t,self.r,OUTSIDE,"unauthorized\n")
        self.check("out_of_scope")

    def test_reject_more_than_three_repair_files(self):
        for idx in range(3):
            path=f"src/FootprintSim.Core/Another{idx}.cs"
            content="public class C { }\n"
            for snap in self.t.values(): snap["entries"][path]={"oid":contract.blob(content),"mode":"100644","text":content}
            change(self.t,self.r,path,"public class C { int x; }\n")
        self.check("invalid_schema")  # paths max 3

    def test_before_and_after_blob_mismatch(self):
        self.r["paths"][0]["before_oid"]="0"*40; self.check("before_after_mismatch")
        self.a,self.r,self.t,self.v=fixture()
        self.t["candidate"]["entries"][SOURCE]["oid"]="e"*40; self.check("blob_mismatch")

    def test_added_deleted_mode_and_encoding(self):
        del self.t["candidate"]["entries"][TEST]; self.check("file_add_delete")
        self.a,self.r,self.t,self.v=fixture()
        self.t["candidate"]["entries"][SOURCE]["mode"]="100755"; self.check("special_mode_or_rename")
        self.a,self.r,self.t,self.v=fixture()
        self.t["candidate"]["entries"][SOURCE]["text"]="invalid\0"; self.check("invalid_encoding")

    def test_modified_existing_unedited_test_allowed_only_substantive(self):
        change(self.t,self.r,TEST,"Assert.Equal(1, 1);\nAssert.True(true);\n")
        self.check("hold_semantic_review")
        proof=contract.digest({p:(self.t["current"]["entries"][p]["oid"],
                                  self.t["candidate"]["entries"][p]["oid"])
                               for p in sorted([SOURCE,TEST])})
        self.v=replace(self.v,reviewer_semantic_delta_sha256=proof,
                       reviewer_invariants=tuple(sorted(contract.INVARIANTS)))
        result=contract.validate(self.a,self.r,self.t,self.v)
        self.assertEqual(result["changed_files"],2)
        self.assertTrue(result["semantic_attestation_required"])
        self.r["kind"]="minor";self.r["policy"]=contract.POLICIES["minor"]
        self.v=replace(self.v,repair_kind="minor",repair_policy=self.r["policy"])
        self.check("out_of_scope")

    def test_minor_policy_two_files_80_lines_and_one_attempt(self):
        self.r["kind"]="minor";self.r["policy"]=contract.POLICIES["minor"]
        self.v=replace(self.v,repair_kind="minor",repair_policy=self.r["policy"])
        self.assertEqual(contract.validate(self.a,self.r,self.t,self.v)["changed_files"],1)
        self.r["attempt"]=2;self.check("attempt_limit")
        self.a,self.r,self.t,self.v=fixture()
        self.r["kind"]="minor";self.r["policy"]=contract.POLICIES["minor"]
        self.v=replace(self.v,repair_kind="minor",repair_policy=self.r["policy"],
                       prior_changed_lines=79)
        self.check("line_limit")

    def test_approval_body_change_after_reservation(self):
        self.v=replace(self.v,issue_title_sha256="0"*64)
        self.check("untrusted_provenance")

    def test_missing_run_review_and_incomplete_fetch_hold(self):
        self.r["reviewer_run_id"]="owner-written";self.check("stale_or_unverified_evidence")
        self.a,self.r,self.t,self.v=fixture()
        self.r["workflow_run_id"]=1;self.check("stale_or_unverified_evidence")
        self.a,self.r,self.t,self.v=fixture()
        self.t["candidate"]["complete"]=False;self.check("incomplete_tree")

    def test_workflow_and_failure_classification_are_independently_bound(self):
        # Do not accept a candidate-selected CI workflow or failure cause.
        self.r["workflow"] = "Unrelated-CI"
        self.check("stale_or_unverified_evidence")
        self.a,self.r,self.t,self.v=fixture()
        self.r["failure_code"] = "candidate_selected_error"
        self.check("stale_or_unverified_evidence")
        # The trusted receipt must also match exactly; it cannot be absent,
        # incomplete, or from another independently verified run.
        self.a,self.r,self.t,self.v=fixture()
        self.v=replace(self.v,observed_workflow="Other-CI")
        self.check("stale_or_unverified_evidence")
        self.a,self.r,self.t,self.v=fixture()
        self.v=replace(self.v,observed_failure_code="different_failure")
        self.check("stale_or_unverified_evidence")
        self.a,self.r,self.t,self.v=fixture()
        self.v=replace(self.v,observed_workflow=None)
        self.check("stale_or_unverified_evidence")
        self.a,self.r,self.t,self.v=fixture()
        self.v=replace(self.v,observed_failure_code="")
        self.check("stale_or_unverified_evidence")
        # The fixture's independently supplied pair remains valid.
        self.a,self.r,self.t,self.v=fixture()
        self.assertEqual(contract.validate(self.a,self.r,self.t,self.v)["status"],
                         "scope_valid_for_independent_review")

    def test_expired_or_replayed_reservation_hold(self):
        self.r["expires_at"]=self.v.now_epoch;self.check("expired_reservation")
        self.a,self.r,self.t,self.v=fixture()
        self.v=replace(self.v,previous_attempts=1);self.check("attempt_limit")

    def test_reservation_id_expiry_and_binding_not_self_authorized(self):
        self.r["repair_reservation_id"]="reservation:forged"
        self.check("untrusted_reservation")
        self.a,self.r,self.t,self.v=fixture()
        self.r["expires_at"]+=3600
        self.check("untrusted_reservation")
        self.a,self.r,self.t,self.v=fixture()
        self.v=replace(self.v,repair_root_pr=999)
        self.check("untrusted_reservation")
        self.a,self.r,self.t,self.v=fixture()
        self.v=replace(self.v,repair_head_sha="f"*40)
        self.check("untrusted_reservation")
        self.a,self.r,self.t,self.v=fixture()
        self.v=replace(self.v,repair_policy=contract.POLICIES["minor"])
        self.check("untrusted_reservation")
        self.a,self.r,self.t,self.v=fixture()
        self.v=replace(self.v,repair_attempt=2)
        self.check("untrusted_reservation")

    def test_inserted_early_return_and_skip_in_test_hold(self):
        # An inserted early return leaves the original assertion untouched.
        change(self.t,self.r,TEST,"return;\nAssert.Equal(1, 1);\n")
        self.check("hold_semantic_review")
        proof=contract.digest({p:(self.t["current"]["entries"][p]["oid"],
                                  self.t["candidate"]["entries"][p]["oid"])
                               for p in sorted([SOURCE,TEST])})
        self.v=replace(self.v,reviewer_semantic_delta_sha256=proof,
                       reviewer_invariants=tuple(sorted(contract.INVARIANTS)))
        result=contract.validate(self.a,self.r,self.t,self.v)
        self.assertTrue(result["semantic_attestation_required"])
        self.a,self.r,self.t,self.v=fixture()
        change(self.t,self.r,TEST,'[Fact(Skip = "disabled")]\nAssert.Equal(1, 1);\n')
        self.check("hold_semantic_review")

    def test_acceptance_and_protected_checks_cannot_disappear(self):
        self.r["acceptance"].pop();self.check("acceptance_changed")
        self.a,self.r,self.t,self.v=fixture()
        self.r["required_verifications"]=[];self.check("invalid_schema")
        self.a,self.r,self.t,self.v=fixture()
        self.r["required_verifications"][0]["command_sha256"]="f"*64
        self.check("acceptance_changed")

    def test_assert_order_change_requires_all_four_independent_invariants(self):
        change(self.t,self.r,TEST,"Assert.Equal(2, 1);\n")
        self.check("hold_semantic_review")
        proof=contract.digest({p:(self.t["current"]["entries"][p]["oid"],
                                  self.t["candidate"]["entries"][p]["oid"])
                               for p in sorted([SOURCE,TEST])})
        self.v=replace(self.v,reviewer_semantic_delta_sha256=proof,
                       reviewer_invariants=("candidate_set","count","candidate_id_order"))
        self.check("hold_semantic_review")
        self.v=replace(self.v,reviewer_invariants=tuple(sorted(contract.INVARIANTS)))
        self.assertTrue(contract.validate(self.a,self.r,self.t,self.v)["semantic_attestation_required"])

    def test_automatic_IsComplete_filling_is_forbidden(self):
        change(self.t,self.r,SOURCE,"public class A { int X; int Y; bool IsComplete = true; }\n")
        self.check("prohibited_semantic_change")

    def test_new_unapproved_and_unknown_fields_rejected(self):
        self.r["allow_publish"]=True;self.check("invalid_schema")
        self.a,self.r,self.t,self.v=fixture()
        self.r["paths"][0]["path"]="src/**/*.cs";self.check("disallowed_path")
        self.a,self.r,self.t,self.v=fixture()
        self.r["schema"]="version-next";self.check("unknown_schema")

    def test_input_json_rejects_duplicate_keys_nan_and_bad_bytes(self):
        for raw in (b'{"a":1,"a":2}',b'{"a":NaN}',b'\xff'):
            with self.assertRaises(contract.ContractError):contract.parse_json(raw)
        self.assertEqual(contract.parse_json(b'{"ok":true}'),{"ok":True})

    def test_schema_contains_strict_root_and_required_fields(self):
        s=json.loads((ROOT/".github/scripts/pr_repair_request.schema.json").read_text())
        self.assertIs(s["additionalProperties"],False)
        self.assertEqual(set(s["required"]),{"approval_snapshot","repair_request"})
        self.assertEqual(s["$defs"]["approval"]["additionalProperties"],False)
        self.assertEqual(s["$defs"]["request"]["additionalProperties"],False)
        self.assertIn("reviewer_run_id",s["$defs"]["request"]["required"])

    def test_validation_is_pure_no_external_calls(self):
        with patch("subprocess.run",side_effect=AssertionError("process")),\
             patch("socket.socket",side_effect=AssertionError("network")):
            a=contract.validate(self.a,self.r,self.t,self.v)
        self.assertFalse(a["may_repair"])


if __name__=="__main__": unittest.main()
