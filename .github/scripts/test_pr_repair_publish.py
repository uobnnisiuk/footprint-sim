"""R4 offline fake publication tests (no network, model or candidate execution)."""
import sys
import unittest
from pathlib import Path
from dataclasses import replace
from unittest.mock import patch

sys.path.insert(0, str(Path(__file__).resolve().parents[2] / "tools"))
import pr_repair_candidate as c
import pr_repair_publish as p
import pr_repair_contract as contract
from test_pr_repair_candidate import CandidateTests, A, B, C, T, RID


class FakeAdapter:
    """Isolated fixture; no GitHub client, no shell and no live adapter."""
    def __init__(self):
        env, commit, before, after, receipt = CandidateTests().fixture()
        self.env, self.commit, self.before, self.after = env, commit, before, after
        self.receipt = {"schema": "footprint-pr-repair-ledger-v1",
                        "status": "reserved", "id": RID,
                        "kind": "substantive", "rootIssue": 56, "rootPr": 58,
                        "headSha": A, "mainSha": B,
                        "approvedTitleHash": "1"*64, "approvedBodyHash": "2"*64,
                        "expiresAtEpoch": 200}
        self.head, self.main, self.title = A, B, "1"*64
        self.body = "2"*64
        self.events = []
        self.writes = 0
        self.claims = 0
        self.timeout = False
        self.race = False
        self.no_write = False
        self.guard_pass = True

    def observe(self):
        return p.Observation(self.main, self.head, self.title, self.body,
                             True, False, "existing-pr-branch", 56, 58, (58,),
                             self.receipt.copy(), tuple(self.events), 100)

    def trusted_bundle(self, snap):
        # R2 validator is separately covered by test_pr_repair_contract.py.
        trusted = contract.TrustedFacts(
            approval_digest="0"*64, issue_title_sha256=snap.issue_title_hash,
            issue_body_sha256=snap.issue_body_hash, reservation_id=1,
            approval_result_id=2, protocol_commit=A, current_main_sha=snap.main_sha,
            current_head_sha=snap.head_sha, original_pr_base_sha=A,
            finding_id="a", reviewer_run_id="b", observed_workflow="normal",
            observed_failure_code="fixture", workflow_run_id=1,
            workflow_attempt=1, job_id=1, observed_head_sha=snap.head_sha,
            evidence_sha256="0"*64, tree_roots=(), now_epoch=snap.epoch,
            prior_changed_lines=0, prior_changed_paths=(),
            first_pr_changed_paths=(), previous_attempts=0,
            repair_reservation_id=RID, repair_expires_at=200,
            repair_policy="repair", repair_kind="substantive",
            repair_root_issue=56, repair_root_pr=58,
            repair_main_sha=B, repair_head_sha=A, repair_attempt=1)
        trees = {"current": {"entries": {k:{**v,"text":None} for k,v in self.before.items()}},
                 "candidate": {"entries": {k:{**v,"text":None} for k,v in self.after.items()},
                               "root_sha": T}}
        request = {"current_head_sha": A, "current_main_sha": B,
                   "root_pr": 58, "root_issue": 56,
                   "repair_reservation_id": RID, "kind": "substantive",
                   "paths": [{"path":"src/Test.cs"}]}
        return {}, request, trees, trusted

    def detached_objects(self, commit_sha):
        return self.commit, self.before, self.after

    def trusted_diffs(self, snap, env):
        return b"full authorized diff", b"repair authorized diff"

    def isolated_guard(self, main, head, new, full, repair):
        from hashlib import sha256
        return {"passed": self.guard_pass, "trusted_main": main, "old_head": head,
                "candidate": new, "full_sha256": sha256(full).hexdigest(),
                "repair_sha256": sha256(repair).hexdigest(),
                "isolated": True, "guard_from_main": True}

    def claim_once(self, reservation_id, old):
        if not p._pre_events(tuple(self.events)) or self.head != old:
            return False
        self.claims += 1
        self.events.append("publish_started")
        return True

    def cas_update_ref(self, branch, new, *, expected_head, force):
        self.writes += 1
        if self.race: self.head = "f"*40
        if force or self.head != expected_head: raise RuntimeError("conflict")
        if not self.no_write: self.head = new
        if self.timeout: raise TimeoutError("lost response")

    def record_once(self, reservation_id, event, head_after):
        self.events.append(event)


class PublishTests(unittest.TestCase):
    def setUp(self):
        self.a = FakeAdapter()
        self.contract_patch = patch.object(contract, "validate", return_value={
            "status": "scope_valid_for_independent_review", "may_repair": False,
            "may_merge": False})
        self.contract_patch.start()
        self.addCleanup(self.contract_patch.stop)

    def test_dry_run_no_ledger_writes(self):
        out = p.publish_once(self.a, self.a.env)
        self.assertEqual(out["state"], "dry_run")
        self.assertEqual(self.a.writes, 0)
        self.assertEqual(self.a.claims, 0)

    def test_one_write_only_then_ci_pending(self):
        out = p.publish_once(self.a, self.a.env, execute=True)
        self.assertEqual(out["state"], "ci_pending")
        self.assertFalse(out["may_merge"])
        self.assertEqual(self.a.writes, 1)
        self.assertEqual(self.a.events, ["publish_started","published"])
        with self.assertRaises(p.PublishHalt):
            p.publish_once(self.a, self.a.env, execute=True)
        self.assertEqual(self.a.writes, 1)

    def test_response_lost_new_head_readback(self):
        self.a.timeout = True
        out = p.publish_once(self.a, self.a.env, execute=True)
        self.assertEqual(out["state"], "ci_pending")
        self.assertTrue(out["response_uncertain"])
        self.assertEqual(self.a.writes, 1)

    def test_response_lost_old_head_consumed(self):
        self.a.timeout = self.a.no_write = True
        out = p.publish_once(self.a, self.a.env, execute=True)
        self.assertEqual(out["state"], "not_applied_consumed")
        self.assertEqual(self.a.writes, 1)
        self.assertEqual(self.a.events[-1], "response_unknown")

    def test_concurrent_writer_head_unknown_consumed(self):
        self.a.race = True
        out = p.publish_once(self.a, self.a.env, execute=True)
        self.assertEqual(out["state"], "unknown_consumed")
        self.assertEqual(self.a.writes, 1)

    def test_untrusted_guard_fails_before_claim(self):
        self.a.guard_pass = False
        with self.assertRaisesRegex(p.PublishHalt, "guard_unverified"):
            p.publish_once(self.a, self.a.env, execute=True)
        self.assertEqual(self.a.claims, 0)

    def test_stale_issue_and_main(self):
        self.a.title = "3"*64
        with self.assertRaises(p.PublishHalt):
            p.publish_once(self.a, self.a.env, execute=True)
        self.a.title = "1"*64
        self.a.main = "4"*40
        with self.assertRaises(p.PublishHalt):
            p.publish_once(self.a, self.a.env, execute=True)

    def test_reservation_expired_and_kind_collision(self):
        self.a.receipt["expiresAtEpoch"] = 99
        with self.assertRaises(p.PublishHalt): p.publish_once(self.a, self.a.env, execute=True)
        self.a.receipt["expiresAtEpoch"] = 200
        self.a.receipt["kind"] = "minor"
        with self.assertRaises(p.PublishHalt): p.publish_once(self.a, self.a.env, execute=True)

    def test_contract_failure_no_publish(self):
        with patch.object(contract, "validate", side_effect=contract.ContractError("bad")):
            with self.assertRaises(p.PublishHalt):
                p.publish_once(self.a, self.a.env, execute=True)
        self.assertEqual(self.a.writes, 0)

    def test_candidate_hash_or_extra_tree_fails(self):
        self.a.after["docs/NoChange.md"]["oid"] = C
        with self.assertRaises(p.PublishHalt): p.publish_once(self.a, self.a.env, execute=True)
        self.assertEqual(self.a.writes, 0)

    def test_prewrite_race_fails_closed(self):
        original = self.a.observe
        calls = [0]
        def changed():
            calls[0] += 1
            if calls[0] == 2: self.a.head = "f"*40
            return original()
        self.a.observe = changed
        with self.assertRaisesRegex(p.PublishHalt, "stale_before_publish"):
            p.publish_once(self.a, self.a.env, execute=True)
        self.assertEqual(self.a.writes, 0)


    def _observe_at(self, *epochs):
        original = self.a.observe
        clock = iter(epochs)
        def reread():
            return replace(original(), epoch=next(clock, epochs[-1]))
        self.a.observe = reread

    def test_expiry_between_first_and_second_prevents_claim(self):
        self._observe_at(100, 200)
        with self.assertRaisesRegex(p.PublishHalt, "stale_before_publish"):
            p.publish_once(self.a, self.a.env, execute=True)
        self.assertEqual(self.a.claims, 0)
        self.assertEqual(self.a.writes, 0)
        self.assertEqual(self.a.events, [])

    def test_expiry_after_claim_before_final_reread_consumes_claim(self):
        self._observe_at(100, 150, 200)
        with self.assertRaisesRegex(p.PublishHalt, "changed_after_claim"):
            p.publish_once(self.a, self.a.env, execute=True)
        self.assertEqual(self.a.claims, 1)
        self.assertEqual(self.a.writes, 0)
        self.assertEqual(self.a.events, ["publish_started"])
        with self.assertRaises(p.PublishHalt):
            p.publish_once(self.a, self.a.env, execute=True)
        self.assertEqual(self.a.claims, 1)
        self.assertEqual(self.a.writes, 0)

    def test_expiry_after_second_before_cas_is_fail_closed(self):
        self._observe_at(100, 199, 201)
        with self.assertRaisesRegex(p.PublishHalt, "changed_after_claim"):
            p.publish_once(self.a, self.a.env, execute=True)
        self.assertEqual(self.a.claims, 1)
        self.assertEqual(self.a.writes, 0)
        self.assertEqual(self.a.events, ["publish_started"])

    def test_clock_rollback_before_claim_is_fail_closed(self):
        self._observe_at(100, 99)
        with self.assertRaisesRegex(p.PublishHalt, "stale_before_publish"):
            p.publish_once(self.a, self.a.env, execute=True)
        self.assertEqual(self.a.claims, 0)
        self.assertEqual(self.a.writes, 0)

    def test_never_run_candidate_scripts(self):
        import subprocess
        with patch.object(subprocess, "run", side_effect=AssertionError("executed")):
            p.publish_once(self.a, self.a.env)
        self.assertEqual(self.a.writes, 0)

    def test_production_transport_unavailable(self):
        class NetworkLike(FakeAdapter): pass
        network = NetworkLike()
        with self.assertRaisesRegex(p.PublishHalt, "production_adapter_not_verified"):
            p.publish_once(network, network.env, execute=True)
        self.assertEqual(network.writes, 0)

    def test_module_name_spoof_cannot_enable_transport(self):
        class NetworkLike(FakeAdapter): pass
        NetworkLike.__module__ = FakeAdapter.__module__
        network = NetworkLike()
        with self.assertRaisesRegex(p.PublishHalt, "production_adapter_not_verified"):
            p.publish_once(network, network.env, execute=True)
        self.assertEqual(network.writes, 0)

    def test_candidate_generation_timeout_and_second_candidate_hold(self):
        for event in ("candidate_timeout", "candidate_rejected",
                      "failed", "response_unknown"):
            self.a.events = ["candidate_started", event]
            with self.assertRaises(p.PublishHalt):
                p.publish_once(self.a, self.a.env, execute=True)
            self.assertEqual(self.a.writes, 0)
        self.a.events = ["candidate_started", "candidate_started"]
        with self.assertRaises(p.PublishHalt):
            p.publish_once(self.a, self.a.env, execute=True)
        self.a.events = []
        with self.assertRaises(p.PublishHalt):
            p.publish_once(self.a, {}, execute=True)
        self.assertEqual(self.a.writes, 0)

    def test_one_generated_candidate_is_consumed_once(self):
        self.a.events = ["receipt_sent", "candidate_started"]
        out = p.publish_once(self.a, self.a.env, execute=True)
        self.assertEqual(out["state"], "ci_pending")
        self.assertEqual(self.a.events, ["receipt_sent", "candidate_started",
                                         "publish_started", "published"])
        with self.assertRaises(p.PublishHalt):
            p.publish_once(self.a, self.a.env, execute=True)
        self.assertEqual(self.a.writes, 1)

    def test_lost_event_persistence_never_retries_write(self):
        self.a.record_once = lambda *args: (_ for _ in ()).throw(OSError("disk"))
        with self.assertRaisesRegex(p.PublishHalt, "event_record_unverified"):
            p.publish_once(self.a, self.a.env, execute=True)
        self.assertEqual(self.a.writes, 1)
        self.assertEqual(self.a.claims, 1)

    def test_r2_contract_is_actually_validated(self):
        # Reuse the REAL R2 fixture; this test does not mock contract.validate.
        from test_pr_repair_contract import fixture, SOURCE
        approval, request, trees, trusted = fixture()
        self.contract_patch.stop()
        a = self.a
        a.main, a.head = request["current_main_sha"], request["current_head_sha"]
        a.title, a.body = approval["issue_title_sha256"], approval["issue_body_sha256"]
        a.receipt.update({"id": RID, "mainSha": a.main, "headSha": a.head,
                          "approvedTitleHash": a.title, "approvedBodyHash": a.body,
                          "expiresAtEpoch": 2000000000})
        request["repair_reservation_id"] = RID
        trusted = replace(trusted, repair_reservation_id=RID)
        a.before = {k: {"oid": v["oid"], "mode": v["mode"]}
                    for k, v in trees["current"]["entries"].items()}
        a.after = {k: {"oid": v["oid"], "mode": v["mode"]}
                   for k, v in trees["candidate"]["entries"].items()}
        new_head, root = "c"*40, trees["candidate"]["root_sha"]
        a.commit = {"sha": new_head, "parents": [a.head], "tree_sha": root}
        manifest = {"parent_sha": a.head, "commit_sha": new_head,
                    "tree_sha": root, "changes": [
                        {"path": SOURCE, "before": a.before[SOURCE]["oid"],
                         "after": a.after[SOURCE]["oid"]}]}
        a.env = {"reservation_id": RID, "kind": "substantive",
                 "parent_sha": a.head, "commit_sha": new_head, "tree_sha": root,
                 "changed_paths": [SOURCE], "changes_sha256": c.digest(manifest)}
        a.trusted_bundle = lambda snap: (approval, request, trees, trusted)
        a.observe = lambda: p.Observation(
            a.main, a.head, a.title, a.body, True, False, "existing-pr-branch",
            56, 58, (58,), a.receipt.copy(), tuple(a.events), 1800000000)
        out = p.publish_once(a, a.env)
        self.assertEqual(out["state"], "dry_run")
        self.assertEqual(a.writes, 0)
        request["acceptance"] = []
        with self.assertRaises(p.PublishHalt):
            p.publish_once(a, a.env, execute=True)
        self.assertEqual(a.writes, 0)


class DurableClaimTests(unittest.TestCase):
    """Actual R3 append-only files and daily.lock, disposable local state only."""

    def setUp(self):
        from test_pr_repair_ledger import LedgerTests
        self.ledger_test = LedgerTests("test_dry_run_zero_state_or_model_writes")
        self.ledger_test.setUp()
        self.addCleanup(self.ledger_test.doCleanups)

    def test_same_pr_head_minor_substantive_compete(self):
        from test_pr_repair_ledger import at
        import pr_repair_ledger as ledger
        old = "d"*40
        minor = self.ledger_test.reserve(kind="minor", hour=10)
        self.assertTrue(p.claim_once_durable(
            self.ledger_test.state, minor["id"], 58, old,
            clock=lambda: at(10)))
        self.assertFalse(p.claim_once_durable(
            self.ledger_test.state, minor["id"], 58, old,
            clock=lambda: at(10)))
        substantive = self.ledger_test.reserve(kind="substantive", hour=11)
        self.assertFalse(p.claim_once_durable(
            self.ledger_test.state, substantive["id"], 58, old,
            clock=lambda: at(11)))
        audit = ledger.read_ledger(self.ledger_test.state, at(11))
        self.assertEqual([e["event"] for e in audit.events], ["publish_started"])
        self.assertEqual(len(audit.reservations), 2)

    def test_timeout_and_expired_reservation_never_claim(self):
        from test_pr_repair_ledger import at
        import pr_repair_ledger as ledger
        receipt = self.ledger_test.reserve(hour=10)
        ledger.append_event(self.ledger_test.state, receipt["id"],
                            "candidate_timeout", clock=lambda: at(10))
        self.assertFalse(p.claim_once_durable(
            self.ledger_test.state, receipt["id"], 58, "d"*40,
            clock=lambda: at(10)))
        self.assertFalse(p.claim_once_durable(
            self.ledger_test.state, receipt["id"], 58, "d"*40,
            clock=lambda: at(11)))
        self.assertEqual(len(ledger.read_ledger(self.ledger_test.state, at(11)).events), 1)
