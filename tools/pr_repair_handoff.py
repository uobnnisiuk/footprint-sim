#!/usr/bin/env python3
"""R5 offline review-to-repair handoff and rollout decision model.

This is a pure, fail-closed *simulation* of the protocol. It neither trusts
caller-supplied receipts nor makes GitHub, scheduler, model or filesystem calls.
A real adapter must independently authenticate every record, preserve the R3
ledger, and satisfy rollout_gate before any production action is considered.
No function in this module enables production or grants merge authority.
"""
from __future__ import annotations

from dataclasses import dataclass
import hashlib
import json
import re

POLICY = "footprint-pr-repair-handoff-v1"
SHA = re.compile(r"[0-9a-f]{40}\Z")
RID = re.compile(r"[0-9a-f]{32}\Z")
IDENT = re.compile(r"[A-Za-z0-9_.:-]{1,120}\Z")
ACTIVE = frozenset(("OBSERVED", "WAIT", "NEEDS_DECISION", "REQUESTED",
                    "RESERVED", "AUTHORING", "CANDIDATE_READY", "PUBLISHED",
                    "WAIT_CI", "WAIT_REVIEW", "ACCEPTED"))
STOPPED = frozenset(("STALE", "BLOCKED", "EXHAUSTED", "INTEGRATED"))
KINDS = frozenset(("minor", "substantive"))
CI = frozenset(("success", "failure", "unknown"))
# These are fixed, non-sensitive classes, not messages from a PR or model.
NOTICE_KINDS = frozenset(("failure", "decision", "exhausted", "published",
                           "publication_unknown", "review_passed", "completed"))
RISKS = frozenset(("test_deleted", "test_skipped", "threshold_relaxed",
                   "unknown_elided", "truth_used", "research_reinterpreted",
                   "guard_bypassed", "contract_expanded"))
ROLLOUT_CHECKS = (
    "r1_r4_integrated", "offline_pr58_cycle", "main_ci_verified",
    "trusted_github_receipts", "live_git_adapter_verified",
    "approved_write_test_target", "original_task_xml_backed_up",
    "original_enabled_state_backed_up", "runtime_sha_verified",
    "old_and_new_ledger_hashes_verified", "legacy_records_preserved",
    "windows_wsl_process_and_locks_verified", "runtime_shared_budget_verified",
    "shadow_read_only_observed", "saved_task_config_read_back",
    "reviewer_request_format_compatible", "shared_publisher_is_only_writer",
    "old_minor_direct_writer_disabled", "partial_update_absent",
    "new_target_explicitly_approved", "real_round_trip_verified",
    "human_intervention_recorded", "owner_rollout_authorized",
)

class HandoffHold(ValueError):
    """Fixed reason without rendering untrusted comments, paths or logs."""

def hold(code: str = "handoff_unverified") -> None:
    raise HandoffHold(code)

def _sha(value: object) -> bool:
    return type(value) is str and SHA.fullmatch(value) is not None

def _id(value: object) -> bool:
    return type(value) is str and IDENT.fullmatch(value) is not None

def _exact(value: object, keys: tuple[str, ...]) -> dict:
    if type(value) is not dict or set(value) != set(keys):
        hold("handoff_invalid_schema")
    return value

def _true(value: object) -> bool:
    return value is True

def _digest(value: object) -> str:
    return hashlib.sha256(json.dumps(value, sort_keys=True, separators=(",", ":"),
                                     ensure_ascii=True, allow_nan=False).encode("ascii")).hexdigest()

@dataclass(frozen=True)
class Receipt:
    """Must be read from the complete independent R3 lifetime ledger."""
    id: str
    root_issue: int
    root_pr: int
    kind: str
    head: str

@dataclass(frozen=True)
class Event:
    """Offline event only. An actor string is NOT an authentication receipt."""
    name: str
    actor: str
    head: str
    data: dict

@dataclass(frozen=True)
class Notification:
    """Non-sensitive event category plus deterministic replay fingerprint."""
    kind: str
    fingerprint: str

@dataclass(frozen=True)
class Result:
    state: str
    head: str
    attempts_consumed: int
    attempts_remaining: int
    notifications: tuple[Notification, ...]
    ci_at_head: bool
    independently_reviewed: bool
    merged_and_main_verified: bool
    production_active: bool = False

def _receipts(receipts: tuple[Receipt, ...], issue: int, pr: int) -> None:
    if type(receipts) is not tuple or len(receipts) > 2:
        hold("lifetime_limit_or_ledger_unverified")
    ids = set()
    minor = 0
    for item in receipts:
        if (type(item) is not Receipt or type(item.id) is not str
                or RID.fullmatch(item.id) is None or item.id in ids
                or type(item.root_issue) is not int or item.root_issue != issue
                or type(item.root_pr) is not int or item.root_pr != pr
                or item.kind not in KINDS or not _sha(item.head)):
            hold("lifetime_limit_or_ledger_unverified")
        ids.add(item.id)
        minor += item.kind == "minor"
    if minor > 1:
        hold("lifetime_limit_or_ledger_unverified")

def replay(root_issue: int, root_pr: int, initial_head: str,
           acceptance_ids: tuple[str, ...], receipts: tuple[Receipt, ...],
           events: tuple[Event, ...]) -> Result:
    """Replay an independently attested event history without external effects.

    The upstream adapter must read a COMPLETE immutable root Issue/PR attempt
    history (including other days and timeout/unknown receipts), approved
    acceptance contract, reviewer identity, GitHub current head and run data.
    The caller must not construct these from candidate or mutable Issue prose.
    """
    if (type(root_issue) is not int or root_issue < 1
            or type(root_pr) is not int or root_pr < 1
            or not _sha(initial_head) or type(acceptance_ids) is not tuple
            or not acceptance_ids or any(not _id(v) for v in acceptance_ids)
            or len(set(acceptance_ids)) != len(acceptance_ids)
            or type(events) is not tuple or len(events) > 100):
        hold("handoff_invalid_initial_state")
    _receipts(receipts, root_issue, root_pr)
    state = "OBSERVED"
    head = initial_head
    used = 0
    current: Receipt | None = None
    candidate_author = ""
    candidate_id = ""
    ci_success = False
    ci_failed = False
    reviewed = False
    approval_pending_digest: str | None = None
    delivered: set[str] = set()
    notices: list[Notification] = []

    def notify(kind: str, reference: str) -> None:
        # A fixed kind and an opaque fingerprint are the entire public payload.
        # Neither PR text, secret-like strings nor raw CI output may leak here.
        if kind not in NOTICE_KINDS or not _id(reference):
            hold("invalid_notification_receipt")
        key = _digest((POLICY, root_issue, root_pr, head, kind, reference))
        if key not in delivered:
            delivered.add(key)
            notices.append(Notification(kind, key))

    for event in events:
        if (type(event) is not Event or not _id(event.name)
                or not _id(event.actor) or not _sha(event.head)
                or event.head != head or type(event.data) is not dict
                or state in STOPPED):
            hold("stale_or_unverified_event")
        d = event.data
        if event.name == "poll":
            if event.actor != "observer" or d:
                hold()
            if state == "OBSERVED":
                state = "WAIT"
            # All subsequent polls preserve the observed state and notify none.
            continue
        if event.name == "finding":
            if approval_pending_digest is not None:
                hold("formal_approval_pending")
            _exact(d, ("classification", "finding_id", "review_run",
                       "unchanged_contract", "entire_diff_reviewed"))
            if (state not in ("OBSERVED", "WAIT", "NEEDS_DECISION", "WAIT_REVIEW")
                    or event.actor != "reviewer" or not _id(d["finding_id"])
                    or not _id(d["review_run"])):
                hold("independent_finding_required")
            if d["classification"] == "needs_decision":
                state = "NEEDS_DECISION"
                notify("decision", d["finding_id"])
            elif (d["classification"] == "repairable"
                  and _true(d["unchanged_contract"])
                  and _true(d["entire_diff_reviewed"])):
                # An unreserved future attempt has no ledger receipt yet.
                # Exhaustion is determined by consumed lifetime reservations;
                # the shared manager verifies the matching receipt at reserve.
                if used >= 2:
                    state = "EXHAUSTED"
                    notify("exhausted", d["finding_id"])
                else:
                    state = "REQUESTED"
                    notify("failure", d["finding_id"])
                ci_success = False
                ci_failed = False
                reviewed = False
            else:
                hold("scope_or_interpretation_not_approved")
        elif event.name == "reserve":
            _exact(d, ("id", "kind", "ledger_verified", "shared_lock",
                       "shared_daily_budget", "approved_root_verified"))
            if (state != "REQUESTED" or event.actor != "shared_manager"
                    or used >= len(receipts) or used >= 2):
                hold("lifetime_limit_or_ledger_unverified")
            item = receipts[used]
            if (event.head != item.head or d["id"] != item.id
                    or d["kind"] != item.kind
                    or not all(_true(d[k]) for k in
                        ("ledger_verified", "shared_lock", "shared_daily_budget",
                         "approved_root_verified"))):
                hold("reservation_not_independently_verified")
            used += 1
            current = item
            candidate_author = ""
            candidate_id = ""
            state = "RESERVED"
        elif event.name == "author_start":
            _exact(d, ("reservation_id", "kind"))
            if (state != "RESERVED" or current is None
                    or d["reservation_id"] != current.id or d["kind"] != current.kind
                    or event.actor != current.kind):
                hold("invalid_authoring")
            candidate_author = event.actor
            state = "AUTHORING"
        elif event.name == "candidate_ready":
            _exact(d, ("reservation_id", "candidate_id", "r2_scope_valid",
                       "main_guard_valid", "risk_flags", "single_candidate"))
            if (state != "AUTHORING" or current is None
                    or event.actor != candidate_author
                    or d["reservation_id"] != current.id or not _id(d["candidate_id"])
                    or not _true(d["r2_scope_valid"])
                    or not _true(d["main_guard_valid"])
                    or not _true(d["single_candidate"])
                    or type(d["risk_flags"]) is not list):
                hold("candidate_not_verified")
            # Unknown flags are also blockers, never a whitelist bypass.
            if d["risk_flags"]:
                hold("unsafe_candidate_requires_review")
            candidate_id = d["candidate_id"]
            state = "CANDIDATE_READY"
        elif event.name == "publish":
            _exact(d, ("reservation_id", "candidate_id", "new_head",
                       "expected_old_head", "durable_claim",
                       "shared_publisher", "cas_writes", "readback"))
            if (state != "CANDIDATE_READY" or event.actor != "shared_publisher"
                    or current is None or d["reservation_id"] != current.id
                    or d["candidate_id"] != candidate_id
                    or d["expected_old_head"] != head
                    or not _sha(d["new_head"]) or d["new_head"] == head
                    or not _true(d["durable_claim"])
                    or not _true(d["shared_publisher"])
                    or type(d["cas_writes"]) is not int or d["cas_writes"] != 1
                    or d["readback"] not in ("applied", "old_head", "unknown")):
                hold("unsafe_or_unverified_publish")
            if d["readback"] != "applied":
                state = "BLOCKED"
                notify("publication_unknown", current.id)
            else:
                head = d["new_head"]
                current = None
                state = "WAIT_CI"
                notify("published", candidate_id)
        elif event.name == "ci_observed":
            _exact(d, ("run_head", "normal", "research", "complete",
                       "required_steps", "run_receipts_verified"))
            if (state != "WAIT_CI" or event.actor != "ci_observer"
                    or d["run_head"] != head
                    or d["normal"] not in CI or d["research"] not in CI
                    or type(d["complete"]) is not bool
                    or type(d["required_steps"]) is not bool
                    or not _true(d["run_receipts_verified"])):
                hold("stale_or_unverified_ci")
            # Never turn an incomplete or missing-step snapshot into a verdict.
            # Once verified complete, a terminal failure of *either* required CI
            # is independently reviewable even if another CI is still unknown.
            # Success requires both to be final; otherwise stay on this head
            # in WAIT_CI and allow later observations without rerunning CI.
            if not d["complete"] or not d["required_steps"]:
                continue
            statuses = (d["normal"], d["research"])
            if "failure" not in statuses and "unknown" in statuses:
                continue
            ci_success = all(status == "success" for status in statuses)
            ci_failed = not ci_success
            state = "WAIT_REVIEW"
        elif event.name == "review":
            _exact(d, ("review_head", "review_run", "reviewer_independent",
                       "author_id", "whole_diff_checked", "acceptance",
                       "unresolved_blockers", "test_risks",
                       "main_aligned", "formal_approval_required",
                       "formal_approval_present"))
            resuming_approval = (state == "NEEDS_DECISION"
                                 and approval_pending_digest is not None)
            if ((state != "WAIT_REVIEW" and not resuming_approval)
                    or event.actor != "reviewer"
                    or d["review_head"] != head or not _id(d["review_run"])
                    or not _id(d["author_id"])
                    or not _true(d["reviewer_independent"])
                    or d["author_id"] == event.actor
                    or type(d["acceptance"]) is not dict
                    or set(d["acceptance"]) != set(acceptance_ids)
                    or any(v not in ("pass", "fail", "missing", "unknown")
                           for v in d["acceptance"].values())
                    or type(d["test_risks"]) is not list
                    or type(d["unresolved_blockers"]) is not int
                    or d["unresolved_blockers"] < 0
                    or type(d["formal_approval_required"]) is not bool
                    or type(d["formal_approval_present"]) is not bool):
                hold("independent_review_unverified")
            if resuming_approval:
                # Full review and head must be identical; only independently
                # observed formal approval may change on re-evaluation.
                if (not d["formal_approval_required"]
                        or not d["formal_approval_present"]
                        or _digest({**d, "formal_approval_present": False})
                           != approval_pending_digest):
                    hold("formal_approval_evidence_changed")
            technical_ok = (ci_success and _true(d["whole_diff_checked"])
                            and d["unresolved_blockers"] == 0
                            and not d["test_risks"] and _true(d["main_aligned"])
                            and all(x == "pass" for x in d["acceptance"].values()))
            approval_missing = (technical_ok and d["formal_approval_required"]
                                and not d["formal_approval_present"])
            ok = technical_ok and not approval_missing
            reviewed = ok
            if ok:
                approval_pending_digest = None
                state = "ACCEPTED"
                notify("review_passed", d["review_run"])
            elif approval_missing:
                approval_pending_digest = _digest(d)
                state = "NEEDS_DECISION"
                notify("decision", d["review_run"])
            else:
                failure = (ci_failed or any(v == "fail" for v in d["acceptance"].values())
                           or d["unresolved_blockers"] > 0 or bool(d["test_risks"]))
                # Two consumed attempts cannot be reopened with a fresh request.
                # A merely missing GitHub approval remains a decision gate, not
                # a false claim that the second repair's code failed.
                repair_unsuccessful = not ci_success or any(
                    v != "pass" for v in d["acceptance"].values())
                if used >= 2 and (repair_unsuccessful or failure):
                    state = "EXHAUSTED"
                else:
                    state = "NEEDS_DECISION"
                notify("failure" if failure else "decision", d["review_run"])
                if state == "EXHAUSTED":
                    notify("exhausted", d["review_run"])
        elif event.name == "merge_observed":
            _exact(d, ("merged_head", "method", "github_merge_verified",
                       "main_contains_head", "main_push_ci_success",
                       "same_review_head", "protection_satisfied"))
            if (state != "ACCEPTED" or event.actor != "reviewer"
                    or not reviewed or not ci_success
                    or d["merged_head"] != head or d["method"] != "merge"
                    or not all(_true(d[k]) for k in
                        ("github_merge_verified", "main_contains_head",
                         "main_push_ci_success", "same_review_head",
                         "protection_satisfied"))):
                hold("merge_evidence_unverified")
            state = "INTEGRATED"
            notify("completed", head)
        else:
            hold("unknown_handoff_event")
    return Result(state, head, used, 2 - used, tuple(notices), ci_success,
                  reviewed, state == "INTEGRATED")

def rollout_gate(checks: dict) -> dict:
    """Read-only activation checklist. No production-enabling code path.

    Values must come from independent GitHub, Windows/WSL and saved-task
    readbacks; booleans from Issue/PR text or model claims prove nothing.
    """
    _exact(checks, ROLLOUT_CHECKS)
    if any(type(v) is not bool for v in checks.values()):
        hold("rollout_evidence_unverified")
    missing = tuple(key for key in ROLLOUT_CHECKS if not checks[key])
    return {
        "offline_implementation": checks["r1_r4_integrated"] and checks["offline_pr58_cycle"],
        "task_configuration_verified": (
            checks["saved_task_config_read_back"] and checks["old_minor_direct_writer_disabled"]
            and checks["shared_publisher_is_only_writer"] and checks["partial_update_absent"]),
        "machine_deployment_verified": (
            checks["runtime_sha_verified"] and checks["original_task_xml_backed_up"]
            and checks["original_enabled_state_backed_up"]
            and checks["old_and_new_ledger_hashes_verified"]
            and checks["windows_wsl_process_and_locks_verified"]
            and checks["runtime_shared_budget_verified"]),
        "real_operation_verified": checks["real_round_trip_verified"],
        "missing_gates": missing,
        "operator_gate_complete": not missing,
        "production_enabled": False,
        "may_activate": False,
    }
