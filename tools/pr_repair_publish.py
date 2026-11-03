#!/usr/bin/env python3
"""R4 PR repair publication gate: offline fake-adapter only (NOT deployed).

The independent adapter is responsible for verifying original approval,
GitHub/Issue/current head, R1 evidence, R3 receipt and Git objects. Never
construct its trusted facts from a candidate. No CLI or live transport is
provided: production remains disabled until R5 integration and verification.
"""
from __future__ import annotations

from dataclasses import dataclass
import hashlib
import re

import pr_repair_candidate as candidate
import pr_repair_contract as contract

SHA = re.compile(r"[0-9a-f]{40}\Z")
HASH = re.compile(r"[0-9a-f]{64}\Z")


class PublishHalt(RuntimeError):
    """Fixed reason, never expose raw GitHub/CI/candidate content."""


def halt(reason="repair_publish_hold"):
    raise PublishHalt(reason)


@dataclass(frozen=True)
class Observation:
    main_sha: str
    head_sha: str
    issue_title_hash: str
    issue_body_hash: str
    pr_open: bool
    pr_merged: bool
    branch: str
    root_issue: int
    root_pr: int
    open_prs: tuple[int, ...]
    reservation: dict
    events: tuple[str, ...]
    epoch: int


def _sha(x):
    return type(x) is str and SHA.fullmatch(x) is not None


def _hash(x):
    return type(x) is str and HASH.fullmatch(x) is not None


def _same(a: Observation, b: Observation) -> bool:
    # Events can grow only through our own durable claim.
    return (a.main_sha, a.head_sha, a.issue_title_hash, a.issue_body_hash,
            a.pr_open, a.pr_merged, a.branch, a.root_issue, a.root_pr,
            a.open_prs, a.reservation) == (
            b.main_sha, b.head_sha, b.issue_title_hash, b.issue_body_hash,
            b.pr_open, b.pr_merged, b.branch, b.root_issue, b.root_pr,
            b.open_prs, b.reservation)


def _check(snapshot: Observation, envelope: dict) -> None:
    if (type(snapshot) is not Observation or not _sha(snapshot.main_sha)
            or not _sha(snapshot.head_sha)
            or not _hash(snapshot.issue_title_hash) or not _hash(snapshot.issue_body_hash)
            or snapshot.pr_open is not True or snapshot.pr_merged is not False
            or type(snapshot.root_issue) is not int or snapshot.root_issue < 1
            or type(snapshot.root_pr) is not int or snapshot.root_pr < 1
            or type(snapshot.branch) is not str
            or not snapshot.branch or snapshot.branch.startswith("refs/")
            or type(snapshot.open_prs) is not tuple
            or snapshot.open_prs != (snapshot.root_pr,)
            or type(snapshot.events) is not tuple or snapshot.events
            or type(snapshot.epoch) is not int
            or type(snapshot.reservation) is not dict):
        halt()
    receipt = snapshot.reservation
    if (receipt.get("schema") != "footprint-pr-repair-ledger-v1"
            or receipt.get("status") != "reserved"
            or receipt.get("id") != envelope.get("reservation_id")
            or receipt.get("rootIssue") != snapshot.root_issue
            or receipt.get("rootPr") != snapshot.root_pr
            or receipt.get("headSha") != snapshot.head_sha
            or receipt.get("mainSha") != snapshot.main_sha
            or receipt.get("approvedTitleHash") != snapshot.issue_title_hash
            or receipt.get("approvedBodyHash") != snapshot.issue_body_hash
            or type(receipt.get("expiresAtEpoch")) is not int
            or receipt["expiresAtEpoch"] <= snapshot.epoch):
        halt("untrusted_or_consumed_reservation")


def _preflight(adapter, snap: Observation, envelope: dict) -> dict:
    # trusted_bundle() must be derived from independently verified external
    # reads (not the candidate or mutable Issue/PR prose).
    approval, request, trees, trusted = adapter.trusted_bundle(snap)
    if (type(trusted) is not contract.TrustedFacts
            or trusted.current_main_sha != snap.main_sha
            or trusted.current_head_sha != snap.head_sha
            or trusted.issue_title_sha256 != snap.issue_title_hash
            or trusted.issue_body_sha256 != snap.issue_body_hash
            or trusted.repair_reservation_id != snap.reservation["id"]
            or trusted.repair_kind != envelope["kind"]
            or trusted.repair_expires_at != snap.reservation["expiresAtEpoch"]
            or trusted.repair_root_issue != snap.root_issue
            or trusted.repair_root_pr != snap.root_pr
            or trusted.now_epoch != snap.epoch):
        halt("untrusted_contract")
    try:
        verdict = contract.validate(approval, request, trees, trusted)
    except (ValueError, TypeError, KeyError):
        halt("contract_rejected")
    if (type(verdict) is not dict or
            verdict.get("status") != "scope_valid_for_independent_review"
            or verdict.get("may_repair") is not False
            or verdict.get("may_merge") is not False):
        halt("contract_rejected")
    if (request["current_head_sha"] != snap.head_sha
            or request["current_main_sha"] != snap.main_sha
            or request["root_pr"] != snap.root_pr
            or request["root_issue"] != snap.root_issue
            or request["repair_reservation_id"] != snap.reservation["id"]
            or request["kind"] != envelope["kind"]):
        halt("untrusted_contract")
    # Candidate came from a detached, independently GET-fetched Git object.
    obj, before, after = adapter.detached_objects(envelope["commit_sha"])
    try:
        proof = candidate.verify_candidate(
            envelope, obj, before, after, expected_head=snap.head_sha,
            reservation=snap.reservation,
            permitted_paths=tuple(sorted(x["path"] for x in request["paths"])))
    except (ValueError, TypeError, KeyError):
        halt("candidate_rejected")
    actual_before = trees["current"]["entries"]
    actual_after = trees["candidate"]["entries"]
    if (set(before) != set(actual_before) or set(after) != set(actual_after)
            or any(before[p] != {k: actual_before[p][k] for k in ("oid", "mode")}
                   or after[p] != {k: actual_after[p][k] for k in ("oid", "mode")}
                   for p in before)
            or trees["candidate"]["root_sha"] != envelope["tree_sha"]):
        halt("detached_tree_mismatch")
    # Both full-base and repair diffs are generated by a verified adapter.
    # It must use *main-derived* unchanged patch_guard in disposable Git.
    full_diff, repair_diff = adapter.trusted_diffs(snap, envelope)
    if (type(full_diff) is not bytes or type(repair_diff) is not bytes
            or not full_diff or not repair_diff):
        halt("untrusted_diff")
    full_digest = hashlib.sha256(full_diff).hexdigest()
    repair_digest = hashlib.sha256(repair_diff).hexdigest()
    guard = adapter.isolated_guard(snap.main_sha, snap.head_sha,
                                   envelope["commit_sha"], full_diff, repair_diff)
    if (type(guard) is not dict or set(guard) !=
            {"passed", "trusted_main", "old_head", "candidate", "full_sha256",
             "repair_sha256", "isolated", "guard_from_main"}
            or guard != {"passed": True, "trusted_main": snap.main_sha,
                         "old_head": snap.head_sha, "candidate": envelope["commit_sha"],
                         "full_sha256": full_digest, "repair_sha256": repair_digest,
                         "isolated": True, "guard_from_main": True}):
        halt("guard_unverified")
    return {"reservation_id": snap.reservation["id"], "old_head": snap.head_sha,
            "new_head": proof["commit_sha"], "changes_sha256": proof["changes_sha256"],
            "full_diff_sha256": full_digest, "repair_diff_sha256": repair_digest,
            "checks": ("independent_contract", "complete_git_tree",
                       "detached_single_parent", "full_diff", "main_guard",
                       "reservation", "expected_head")}


def publish_once(adapter, envelope: dict, *, execute: bool = False) -> dict:
    """One candidate, at most one CAS ref write. No retries, no model execution.

    The current release permits writes only to an explicitly supplied offline
    fake adapter. A real adapter must be validated separately and integrated
    through R5; setting flags on a network adapter cannot enable production.
    """
    if type(envelope) is not dict or type(execute) is not bool:
        halt()
    if execute and type(adapter).__module__ != "test_pr_repair_publish":
        halt("production_adapter_not_verified")
    first = adapter.observe()
    _check(first, envelope)
    result = _preflight(adapter, first, envelope)
    second = adapter.observe()
    if not _same(first, second) or second.events != ():
        halt("stale_before_publish")
    if not execute:
        return {**result, "state": "dry_run", "ci": "not_run",
                "may_merge": False, "write_attempted": False}
    # claim_once MUST be atomic with the persistent R3 ledger lock. If the
    # same reservation/head was claimed by any minor/substantive publisher,
    # it must return false and NEVER release the reservation for retry.
    if adapter.claim_once(first.reservation["id"], first.head_sha) is not True:
        halt("reservation_already_claimed")
    third = adapter.observe()
    if not _same(first, third) or third.events != ("publish_started",):
        halt("changed_after_claim")
    error = None
    try:
        adapter.cas_update_ref(first.branch, result["new_head"],
                               expected_head=first.head_sha, force=False)
    except Exception:
        error = "update_response_unknown"
    # A timeout is not a reason to resend. Read back even if CAS returned.
    try:
        final = adapter.observe()
    except Exception:
        final = None
    valid = (type(final) is Observation and final.main_sha == first.main_sha
             and final.issue_title_hash == first.issue_title_hash
             and final.issue_body_hash == first.issue_body_hash
             and final.root_issue == first.root_issue and final.root_pr == first.root_pr
             and final.branch == first.branch and final.pr_open is True
             and final.pr_merged is False and final.open_prs == first.open_prs)
    if valid and final.head_sha == result["new_head"]:
        state, event = "ci_pending", "published"
    elif valid and final.head_sha == first.head_sha:
        state, event = "not_applied_consumed", "response_unknown"
    else:
        state, event = "unknown_consumed", "response_unknown"
    # Durable receipt already consumed, including on timeout, guard rejection,
    # post-write competition, or unsuccessful event persistence.
    adapter.record_once(first.reservation["id"], event,
                        result["new_head"] if state == "ci_pending" else None)
    return {**result, "state": state, "ci": "pending" if state == "ci_pending" else
            "unverified", "may_merge": False, "write_attempted": True,
            "response_uncertain": error is not None}
