#!/usr/bin/env python3
"""R5 handoff offline fixtures. No GitHub writes, model calls or deployment."""
import dataclasses
from pathlib import Path
import sys
import unittest

sys.path.insert(0, str(Path(__file__).resolve().parents[2] / "tools"))
from pr_repair_handoff import (Event, HandoffHold, Notification, Receipt, replay,
                               rollout_gate, ROLLOUT_CHECKS)

# Actual PR #58 commit identifiers are used as fixed historical anchors.
# Acceptance IDs and review receipts below are SYNTHETIC offline test data,
# NOT observed CI test names, production attestations, or a real GitHub APPROVE.
H1 = "e8927604a4fa3548dca37da9e8246afd2668c667"
H2 = "df115a35fac89184373111175b0c1c793c02ad35"
H3 = "179d60fd7022b59edb9387ba3ca1c71969551cfa"
ACCEPTANCE = ("provenance", "cli", "uncertainty", "region_order")
R1 = Receipt("1" * 32, 56, 58, "substantive", H1)
R2 = Receipt("2" * 32, 56, 58, "substantive", H2)

def ev(name, actor, head, **data):
    return Event(name, actor, head, data)

def finding(head, key):
    return ev("finding", "reviewer", head, classification="repairable",
              finding_id=key, review_run="independent_review_1",
              unchanged_contract=True, entire_diff_reviewed=True)

def reservation(r):
    return ev("reserve", "shared_manager", r.head, id=r.id, kind=r.kind,
              ledger_verified=True, shared_lock=True, shared_daily_budget=True,
              approved_root_verified=True)

def candidate(r, cand):
    return [ev("author_start", r.kind, r.head, reservation_id=r.id, kind=r.kind),
            ev("candidate_ready", r.kind, r.head, reservation_id=r.id,
               candidate_id=cand, r2_scope_valid=True, main_guard_valid=True,
               single_candidate=True, risk_flags=[])]

def publish(r, cand, next_head, readback="applied"):
    return ev("publish", "shared_publisher", r.head, reservation_id=r.id,
              candidate_id=cand, new_head=next_head, expected_old_head=r.head,
              durable_claim=True, shared_publisher=True, cas_writes=1,
              readback=readback)

def ci(head, normal="success", research="success", complete=True):
    return ev("ci_observed", "ci_observer", head, run_head=head, normal=normal,
              research=research, complete=complete, required_steps=True,
              run_receipts_verified=True)

def review(head, acceptance=None, formal=False, formal_present=False):
    return ev("review", "reviewer", head, review_head=head,
              review_run="independent_review_2",
              reviewer_independent=True, author_id="substantive",
              whole_diff_checked=True, acceptance=acceptance or dict.fromkeys(ACCEPTANCE, "pass"),
              unresolved_blockers=0, test_risks=[], main_aligned=True,
              formal_approval_required=formal, formal_approval_present=formal_present)

def merged(head):
    return ev("merge_observed", "reviewer", head, merged_head=head, method="merge",
              github_merge_verified=True, main_contains_head=True,
              main_push_ci_success=True, same_review_head=True,
              protection_satisfied=True)

def sample():
    return [
        finding(H1, "provenance_and_missing_acceptance"),
        reservation(R1), *candidate(R1, "first_candidate"),
        publish(R1, "first_candidate", H2),
        ci(H2, normal="failure", research="unknown"),
        review(H2, {"provenance":"pass", "cli":"missing",
                    "uncertainty":"unknown", "region_order":"fail"}),
        finding(H2, "region_id_order_review"),
        reservation(R2), *candidate(R2, "second_candidate"),
        publish(R2, "second_candidate", H3),
        ci(H3),
        review(H3),
        merged(H3),
    ]

def run(events, receipts=(R1,R2), acceptance=ACCEPTANCE):
    return replay(56, 58, H1, acceptance, receipts, tuple(events))

class HandoffTests(unittest.TestCase):
    def test_pr58_two_repairs_then_review_and_main(self):
        events = sample()
        self.assertEqual(run(events[:7]).state, "NEEDS_DECISION")
        self.assertEqual(run(events[:8]).state, "REQUESTED")
        self.assertEqual(run(events[:12]).state, "WAIT_CI")
        outcome = run(events)
        self.assertEqual(outcome.state, "INTEGRATED")
        self.assertEqual(outcome.head, H3)
        self.assertEqual(outcome.attempts_consumed, 2)
        self.assertTrue(outcome.independently_reviewed)
        self.assertTrue(outcome.merged_and_main_verified)
        self.assertFalse(outcome.production_active)

    def test_initial_observation_enters_wait_then_independent_finding(self):
        polls = [ev("poll", "observer", H1), ev("poll", "observer", H1)]
        waiting = run(polls)
        self.assertEqual(waiting.state, "WAIT")
        self.assertEqual(waiting.notifications, ())
        requested = run(polls + [finding(H1, "initial_repair_finding")])
        direct = run([finding(H1, "initial_repair_finding")])
        self.assertEqual(requested.state, "REQUESTED")
        self.assertEqual(requested.notifications, direct.notifications)
        self.assertEqual([n.kind for n in requested.notifications], ["failure"])
        self.assertEqual(requested.attempts_remaining, 2)

    def test_unreserved_future_attempt_is_not_lifetime_exhaustion(self):
        # R3's complete ledger contains only reservations already persisted.
        # The independent finding precedes the shared manager's reservation.
        first = finding(H1, "initial_repair_finding")
        requested = run([first], receipts=())
        self.assertEqual(requested.state, "REQUESTED")
        self.assertEqual(requested.attempts_consumed, 0)
        self.assertEqual(requested.attempts_remaining, 2)
        self.assertEqual([n.kind for n in requested.notifications], ["failure"])
        # A request is not permission to author: no matching R3 receipt.
        with self.assertRaises(HandoffHold):
            run([first, reservation(R1)], receipts=())

        # After one completed repair, the next independent finding must
        # remain requestable even before the second reservation exists.
        before_second_reservation = sample()[:8]
        requested_again = run(before_second_reservation, receipts=(R1,))
        self.assertEqual(requested_again.state, "REQUESTED")
        self.assertEqual(requested_again.attempts_consumed, 1)
        self.assertEqual(requested_again.attempts_remaining, 1)
        with self.assertRaises(HandoffHold):
            run(before_second_reservation + [reservation(R2)], receipts=(R1,))
        # Only after a valid, independently supplied matching reservation may
        # the shared manager transition the request into RESERVED.
        reserved = run(before_second_reservation + [reservation(R2)],
                       receipts=(R1, R2))
        self.assertEqual(reserved.state, "RESERVED")
        self.assertEqual(reserved.attempts_consumed, 2)
        self.assertEqual(reserved.attempts_remaining, 0)
        with self.assertRaises(HandoffHold):
            run(before_second_reservation + [reservation(
                dataclasses.replace(R2, id="f" * 32))], receipts=(R1, R2))

    def test_second_failed_ci_or_acceptance_exhausts_immediately(self):
        history = sample()
        for events in (
            history[:-3] + [ci(H3, normal="failure"), review(H3)],
            history[:-2] + [review(H3, {"provenance": "pass", "cli": "missing",
                                       "uncertainty": "pass", "region_order": "pass"})],
        ):
            with self.subTest(reason=events[-2:]):
                result = run(events)
                self.assertEqual(result.state, "EXHAUSTED")
                self.assertEqual(result.attempts_consumed, 2)
                self.assertEqual(result.attempts_remaining, 0)
                self.assertIn("exhausted", [n.kind for n in result.notifications])
                with self.assertRaises(HandoffHold):
                    run(events + [finding(H3, "third_request")])
        # A successful full second review remains eligible for integration.
        self.assertEqual(run(history).state, "INTEGRATED")

    def test_structured_notifications_stable_and_non_sensitive(self):
        result = run(sample())
        self.assertEqual(result.notifications, run(sample()).notifications)
        kinds = [note.kind for note in result.notifications]
        self.assertIn("failure", kinds)
        self.assertIn("published", kinds)
        self.assertEqual(kinds[-1], "completed")
        for note in result.notifications:
            self.assertIsInstance(note, Notification)
            self.assertRegex(note.fingerprint, r"^[0-9a-f]{64}$")
            self.assertNotIn("region_id_order_review", str(note))
        decision = ev("finding", "reviewer", H1, classification="needs_decision",
                      finding_id="new_policy", review_run="independent_review_1",
                      unchanged_contract=False, entire_diff_reviewed=True)
        notice = run([decision]).notifications
        self.assertEqual([n.kind for n in notice], ["decision"])
        self.assertEqual(notice, run([ev("poll", "observer", H1), decision]).notifications)
        self.assertEqual(run([decision]).state, "NEEDS_DECISION")
        self.assertEqual(result.attempts_remaining, 0)

    def test_green_ci_alone_does_not_approve_missing_acceptance(self):
        events = sample()[:5] + [ci(H2), review(H2, {
            "provenance":"pass", "cli":"missing",
            "uncertainty":"unknown", "region_order":"pass"})]
        result = run(events)
        self.assertEqual(result.state, "NEEDS_DECISION")
        self.assertTrue(result.ci_at_head)
        self.assertFalse(result.independently_reviewed)

    def test_result_not_approved_if_normal_or_research_not_success(self):
        for norm, research in (("failure","success"),("success","failure")):
            with self.subTest(norm=norm, research=research):
                prefix = sample()[:5]
                result = run(prefix + [ci(H2, norm, research), review(H2)])
                self.assertEqual(result.state, "NEEDS_DECISION")
                self.assertFalse(result.independently_reviewed)

    def test_pending_ci_reobserved_on_same_head(self):
        prefix = sample()[:-3]  # Published second repair, before CI/review.
        incomplete_steps = dataclasses.replace(
            ci(H3), data={**ci(H3).data, "required_steps": False})
        for waiting in (ci(H3, normal="unknown"), ci(H3, research="unknown"),
                        ci(H3, complete=False), incomplete_steps,
                        ci(H3, normal="failure", complete=False)):
            with self.subTest(waiting=waiting.data):
                pending = run(prefix + [waiting])
                self.assertEqual(pending.state, "WAIT_CI")
                self.assertFalse(pending.ci_at_head)
                self.assertFalse(pending.independently_reviewed)
                self.assertEqual(pending.attempts_remaining, 0)
                self.assertEqual(pending.notifications,
                                 run(prefix + [waiting, waiting]).notifications)
                with self.assertRaises(HandoffHold):
                    run(prefix + [waiting, review(H3)])
                self.assertEqual(run(prefix + [waiting, ci(H3), review(H3),
                                                merged(H3)]).state, "INTEGRATED")

    def test_second_round_pending_ci_then_final_failure_exhausts(self):
        prefix = sample()[:-3]
        waiting = ci(H3, normal="unknown")
        result = run(prefix + [waiting])
        self.assertEqual(result.state, "WAIT_CI")
        self.assertEqual(result.attempts_consumed, 2)
        with self.assertRaises(HandoffHold):
            run(prefix + [waiting, finding(H3, "third_request")])
        final = prefix + [waiting, ci(H3, normal="failure"), review(H3)]
        result = run(final)
        self.assertEqual(result.state, "EXHAUSTED")
        self.assertEqual(result.attempts_remaining, 0)
        self.assertIn("exhausted", [n.kind for n in result.notifications])
        with self.assertRaises(HandoffHold):
            run(final + [finding(H3, "third_request")])

    def test_verified_ci_failure_decidable_while_other_ci_unknown(self):
        # PR #58's first repair had a confirmed normal CI failure while
        # the independent research run was not yet known. Its failure can
        # be reviewed without ever treating the missing run as successful.
        first_round = sample()[:5]
        failed = ci(H2, normal="failure", research="unknown")
        outcome = run(first_round + [failed])
        self.assertEqual(outcome.state, "WAIT_REVIEW")
        self.assertFalse(outcome.ci_at_head)
        self.assertEqual(run(first_round + [failed, review(H2)]).state,
                         "NEEDS_DECISION")
        with self.assertRaises(HandoffHold):
            run(first_round + [failed, merged(H2)])

        # A confirmed failure in the other required CI is symmetric.
        second_round = sample()[:-3]
        for status in (ci(H3, normal="failure", research="unknown"),
                       ci(H3, normal="unknown", research="failure")):
            with self.subTest(status=status.data):
                outcome = run(second_round + [status])
                self.assertEqual(outcome.state, "WAIT_REVIEW")
                self.assertFalse(outcome.ci_at_head)
                exhausted = run(second_round + [status, review(H3)])
                self.assertEqual(exhausted.state, "EXHAUSTED")
                self.assertEqual(exhausted.attempts_remaining, 0)
                with self.assertRaises(HandoffHold):
                    run(second_round + [status, review(H3),
                                        finding(H3, "third_request")])

    def test_incomplete_failure_evidence_remains_pending(self):
        prefix = sample()[:-3]
        terminal_failure = ci(H3, normal="failure", research="unknown")
        for evidence in (
            dataclasses.replace(terminal_failure,
                data={**terminal_failure.data, "complete": False}),
            dataclasses.replace(terminal_failure,
                data={**terminal_failure.data, "required_steps": False}),
            ci(H3, normal="success", research="unknown"),
        ):
            with self.subTest(evidence=evidence.data):
                self.assertEqual(run(prefix + [evidence]).state, "WAIT_CI")
                with self.assertRaises(HandoffHold):
                    run(prefix + [evidence, review(H3)])
                self.assertEqual(run(prefix + [evidence, ci(H3),
                                               review(H3), merged(H3)]).state,
                                 "INTEGRATED")
                with self.assertRaises(HandoffHold):
                    run(prefix + [evidence, ci(H2)])

    def test_stale_ci_and_review_fail_closed(self):
        events = sample()[:5]
        with self.assertRaises(HandoffHold):
            run(events + [ci(H1)])
        with self.assertRaises(HandoffHold):
            run(events + [review(H2)])
        events += [ci(H2)]
        stale = dataclasses.replace(review(H2), data={**review(H2).data, "review_head": H1})
        with self.assertRaises(HandoffHold):
            run(events + [stale])

    def test_revision_needs_same_original_acceptance(self):
        events = sample()[:-1]
        old = review(H3)
        expanded = dataclasses.replace(old, data={**old.data,
             "acceptance": {**old.data["acceptance"], "new_objective": "pass"}})
        with self.assertRaises(HandoffHold):
            run(events[:-1] + [expanded])
        missing = dataclasses.replace(old, data={**old.data,
             "acceptance": {"provenance":"pass"}})
        with self.assertRaises(HandoffHold):
            run(events[:-1] + [missing])

    def test_no_third_attempt_even_other_day_or_identity(self):
        with self.assertRaises(HandoffHold):
            run(sample(), receipts=(R1,R2,Receipt("3"*32,56,58,"minor",H3)))
        with self.assertRaises(HandoffHold):
            run(sample(), receipts=(R1,Receipt("2"*32,57,59,"substantive",H2)))
        extra = sample()[:-1] + [finding(H3, "third_attempt")]
        with self.assertRaises(HandoffHold):
            run(extra)  # accepted state may not start a third repair

    def test_minor_and_substantive_share_one_budget(self):
        bad = (R1, dataclasses.replace(R2, kind="minor"),
               Receipt("3"*32,56,58,"substantive",H3))
        with self.assertRaises(HandoffHold):
            run((), receipts=bad)
        duplicate = (dataclasses.replace(R1, kind="minor"),
                     dataclasses.replace(R2, kind="minor"))
        with self.assertRaises(HandoffHold):
            run((), receipts=duplicate)

    def test_reservation_requires_independent_shared_budget_receipt(self):
        e = sample()[:2]
        bad = dataclasses.replace(e[1], data={**e[1].data, "shared_daily_budget": False})
        with self.assertRaises(HandoffHold):
            run([e[0], bad])
        with self.assertRaises(HandoffHold):
            run(e, receipts=(R2,R1))

    def test_unsafe_candidate_no_publish_and_no_scope_escalation(self):
        e = sample()[:4]
        for risk in ("test_deleted", "test_skipped", "threshold_relaxed",
                     "unknown_elided", "truth_used", "research_reinterpreted",
                     "contract_expanded", "other_unrecognized_risk"):
            bad = dataclasses.replace(e[-1],
                data={**e[-1].data, "risk_flags":[risk]})
            with self.subTest(risk=risk), self.assertRaises(HandoffHold):
                run(e[:-1] + [bad])
        with self.assertRaises(HandoffHold):
            run(e[:-1] + [dataclasses.replace(e[-1],
                data={**e[-1].data, "single_candidate":False})])

    def test_unknown_ref_write_consumed_no_retry(self):
        e = sample()[:4] + [publish(R1, "first_candidate", H2, readback="unknown")]
        outcome = run(e)
        self.assertEqual(outcome.state, "BLOCKED")
        self.assertEqual(outcome.attempts_consumed, 1)
        with self.assertRaises(HandoffHold):
            run(e + [publish(R1, "first_candidate", H2)])
        bad = dataclasses.replace(e[-1], data={**e[-1].data, "cas_writes": 2})
        with self.assertRaises(HandoffHold):
            run(e[:-1] + [bad])

    def test_only_shared_publisher_and_exact_kind_can_author(self):
        e = sample()[:4]
        with self.assertRaises(HandoffHold):
            run(e + [dataclasses.replace(sample()[4], actor="substantive")])
        with self.assertRaises(HandoffHold):
            run(e[:2] + [dataclasses.replace(e[2], actor="minor")])
        with self.assertRaises(HandoffHold):
            run(e[:2] + [reservation(R1)])  # cannot reserve again

    def test_review_independence_and_formal_approval(self):
        prefix = sample()[:-2]
        bad = dataclasses.replace(review(H3), data={
            **review(H3).data, "reviewer_independent": False})
        with self.assertRaises(HandoffHold):
            run(prefix + [bad])
        awaiting = review(H3, formal=True, formal_present=False)
        approved = review(H3, formal=True, formal_present=True)
        result = run(prefix + [awaiting])
        self.assertEqual(result.state, "NEEDS_DECISION")
        self.assertFalse(result.independently_reviewed)
        self.assertEqual(result.attempts_remaining, 0)
        with self.assertRaises(HandoffHold):
            run(prefix + [merged(H3)])
        with self.assertRaises(HandoffHold):
            run(prefix + [awaiting, merged(H3)])
        with self.assertRaises(HandoffHold):
            run(prefix + [awaiting, finding(H3, "unneeded_repair")])
        # Simulated approval is not a real GitHub APPROVED: trusted adapter
        # must independently recheck the unchanged head and actual protection.
        self.assertEqual(run(prefix + [awaiting, approved]).state, "ACCEPTED")
        self.assertEqual(run(prefix + [awaiting, approved, merged(H3)]).state,
                         "INTEGRATED")
        altered = dataclasses.replace(approved, data={
            **approved.data, "review_run": "changed_review"})
        with self.assertRaises(HandoffHold):
            run(prefix + [awaiting, altered])
        with self.assertRaises(HandoffHold):
            run(prefix + [awaiting, review(H2, formal=True, formal_present=True)])

    def test_poll_does_not_repeat_notifications(self):
        e = sample()[:5]
        x = run(e)
        y = run(e + [ev("poll", "observer", H2)])
        self.assertEqual(x.notifications, y.notifications)
        self.assertEqual(y.state, "WAIT_CI")

    def test_classification_cannot_authorize_research_change(self):
        f = finding(H1, "not_existing")
        f = dataclasses.replace(f, data={**f.data, "unchanged_contract":False})
        with self.assertRaises(HandoffHold):
            run([f])
        f = ev("finding","reviewer",H1,classification="needs_decision",
              finding_id="new_policy",review_run="independent_review_1",
              unchanged_contract=False,entire_diff_reviewed=True)
        self.assertEqual(run([f]).state, "NEEDS_DECISION")

    def test_rollout_fail_closed_even_when_every_checkbox_true(self):
        blank = dict.fromkeys(ROLLOUT_CHECKS, False)
        report = rollout_gate(blank)
        self.assertFalse(report["production_enabled"])
        self.assertFalse(report["operator_gate_complete"])
        partial = {**blank, "r1_r4_integrated":True,
                   "offline_pr58_cycle":True,
                   "saved_task_config_read_back":True}
        self.assertFalse(rollout_gate(partial)["task_configuration_verified"])
        complete = dict.fromkeys(ROLLOUT_CHECKS, True)
        self.assertTrue(rollout_gate(complete)["operator_gate_complete"])
        self.assertFalse(rollout_gate(complete)["may_activate"])
        self.assertFalse(rollout_gate(complete)["production_enabled"])

    def test_rollout_does_not_accept_missing_or_claimed_receipts(self):
        blank = dict.fromkeys(ROLLOUT_CHECKS, False)
        with self.assertRaises(HandoffHold):
            rollout_gate({**blank, "extra_permission":True})
        with self.assertRaises(HandoffHold):
            rollout_gate({k:v for k,v in blank.items() if k != "runtime_sha_verified"})
        with self.assertRaises(HandoffHold):
            rollout_gate({**blank, "runtime_sha_verified":"yes"})

    def test_actual_r4_offline_fake_publisher_is_not_merge_authority(self):
        # Exercises the already-reviewed R4 implementation with its existing
        # isolated fake; R2 acceptance is mocked ONLY for this offline case.
        import pr_repair_contract as contract
        import pr_repair_publish as publisher
        from test_pr_repair_publish import FakeAdapter
        from unittest.mock import patch
        adapter = FakeAdapter()
        with patch.object(contract, "validate", return_value={
            "status": "scope_valid_for_independent_review",
            "may_repair": False, "may_merge": False}):
            result = publisher.publish_once(adapter, adapter.env, execute=True)
        self.assertEqual(result["state"], "ci_pending")
        self.assertFalse(result["may_merge"])
        self.assertEqual(adapter.writes, 1)
        self.assertEqual(adapter.events, ["publish_started", "published"])

if __name__ == "__main__":
    unittest.main()
