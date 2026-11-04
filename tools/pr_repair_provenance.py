#!/usr/bin/env python3
"""Read GitHub approval/review records against separately provisioned anchors.

An anchor is an operator-managed trust input, never a RepairRequest field. Its
digest or Python type is NOT authentication. Existing approval comments are
prose, not signed R2 manifests: a separately reviewed binding of that prose to
the complete R2 approval digest is required. This component verifies that
binding's source records; it does not invent it from an Issue or model answer.

The injected reader is a test seam. Production callers must use the pinned
GET-only transport and independent anchor provisioning. No token, raw comment,
review text, model call, file write, reservation or publication occurs here.
"""
from __future__ import annotations

from dataclasses import dataclass
from datetime import datetime, timezone
import hashlib
import re

import pr_repair_contract as contract
from pr_repair_remote_audit import REPO, API

_SHA = re.compile(r"[0-9a-f]{40}\Z")
_DIGEST = re.compile(r"[0-9a-f]{64}\Z")
_POLICIES = frozenset(("footprint-m1b-v1", "footprint-m1b-v2",
                       "footprint-f19-v1", "footprint-f20-v1"))
_MAX_TEXT = 200_000


class ProvenanceHold(ValueError):
    """Fixed, non-sensitive reason codes only."""


def _require(ok, reason="provenance_unverified"):
    if not ok:
        raise ProvenanceHold(reason)


def _positive(value):
    return type(value) is int and 0 < value < 2**53


def _matches(pattern, value):
    return type(value) is str and pattern.fullmatch(value) is not None


def _text(value):
    _require(type(value) is str, "source_record_unverified")
    try:
        raw = value.encode("utf-8", "strict")
    except UnicodeError:
        raise ProvenanceHold("source_record_unverified") from None
    _require(len(raw) <= _MAX_TEXT and "\x00" not in value,
             "source_record_unverified")
    return raw


def _hash_text(value):
    return hashlib.sha256(_text(value)).hexdigest()


def _instant(value):
    _require(type(value) is str and len(value) <= 40,
             "source_timestamp_unverified")
    try:
        parsed = datetime.fromisoformat(value.replace("Z", "+00:00"))
    except ValueError:
        raise ProvenanceHold("source_timestamp_unverified") from None
    _require(parsed.tzinfo is not None and parsed.utcoffset() == timezone.utc.utcoffset(parsed),
             "source_timestamp_unverified")
    return parsed


def _get(reader, route):
    try:
        value = reader.get(route)
    except Exception:
        raise ProvenanceHold("provenance_source_unavailable") from None
    _require(type(value) is dict, "source_record_unverified")
    return value


@dataclass(frozen=True)
class RecordPin:
    id: int
    author_id: int
    body_sha256: str
    created_at: str


@dataclass(frozen=True)
class ApprovalAnchor:
    root_issue: int
    root_pr: int
    ledger_issue: int
    policy: str
    protocol_commit: str
    approved_main_sha: str
    issue_title_sha256: str
    issue_body_sha256: str
    reservation: RecordPin
    result: RecordPin
    approval_review: RecordPin
    approval_digest: str


@dataclass(frozen=True)
class ApprovalEvidence:
    root_issue: int
    root_pr: int
    approval_digest: str
    reservation_id: int
    approval_result_id: int
    protocol_commit: str
    approved_main_sha: str
    status: str = "records_match_operator_anchor"
    may_publish: bool = False
    may_activate: bool = False


@dataclass(frozen=True)
class ReviewAnchor:
    root_pr: int
    review_id: int
    head_sha: str
    author_id: int
    body_sha256: str
    submitted_at: str
    reviewer_run_id: str


@dataclass(frozen=True)
class ReviewEvidence:
    root_pr: int
    review_id: int
    head_sha: str
    reviewer_run_id: str
    state: str
    formal_approval: bool
    status: str = "record_matches_operator_anchor"
    may_publish: bool = False
    may_activate: bool = False


def _pin(pin):
    _require(type(pin) is RecordPin and _positive(pin.id)
             and _positive(pin.author_id)
             and _matches(_DIGEST, pin.body_sha256), "operator_anchor_required")
    _instant(pin.created_at)


def _comment(reader, pin, issue):
    _pin(pin)
    record = _get(reader, "issues/comments/" + str(pin.id))
    user = record.get("user")
    _require(record.get("id") == pin.id and type(record.get("id")) is int
             and record.get("url") == API + "issues/comments/" + str(pin.id)
             and record.get("issue_url") == API + "issues/" + str(issue)
             and type(user) is dict and type(user.get("id")) is int
             and user.get("id") == pin.author_id and user.get("type") == "User"
             and record.get("created_at") == pin.created_at
             and record.get("updated_at") == pin.created_at
             and _hash_text(record.get("body")) == pin.body_sha256,
             "approval_record_changed_or_unverified")
    return record["body"]


def _headers(body, expected):
    # Only machine header fields are interpreted. Prose can never enlarge scope.
    # Duplicate required headers are rejected even if both spell the same value.
    found = {}
    for line in body.splitlines():
        if ":" not in line:
            continue
        key, value = line.split(":", 1)
        if key in expected:
            _require(key not in found, "approval_header_unverified")
            found[key] = value.strip()
    _require(found == expected, "approval_header_unverified")


def _issue(reader, anchor):
    record = _get(reader, "issues/" + str(anchor.root_issue))
    _require(type(record.get("number")) is int
             and record.get("number") == anchor.root_issue
             and "pull_request" not in record
             and record.get("url") == API + "issues/" + str(anchor.root_issue)
             and record.get("state") in ("open", "closed")
             and _hash_text(record.get("title")) == anchor.issue_title_sha256
             and _hash_text(record.get("body")) == anchor.issue_body_sha256,
             "issue_approval_drift")
    _instant(record.get("updated_at"))
    return (record["state"], record["updated_at"])


def verify_approval(reader, anchor: ApprovalAnchor, approval: dict) -> ApprovalEvidence:
    """Re-read the three unedited approval records and bind a complete R2 digest.

    The complete scope/acceptance mapping must already be independently reviewed
    and provisioned in ``anchor``. This method does not turn prose into an R2
    manifest, or create TrustedFacts/permission. A missing anchor fails closed.
    """
    _require(type(anchor) is ApprovalAnchor, "operator_anchor_required")
    for number in (anchor.root_issue, anchor.root_pr, anchor.ledger_issue):
        _require(_positive(number), "operator_anchor_required")
    _require(type(anchor.policy) is str and anchor.policy in _POLICIES
             and _matches(_SHA, anchor.protocol_commit)
             and _matches(_SHA, anchor.approved_main_sha)
             and all(_matches(_DIGEST, x) for x in (anchor.issue_title_sha256,
                      anchor.issue_body_sha256, anchor.approval_digest)),
             "operator_anchor_required")
    for pin in (anchor.reservation, anchor.result, anchor.approval_review):
        _pin(pin)
    _require(len({anchor.reservation.id, anchor.result.id, anchor.approval_review.id}) == 3,
             "operator_anchor_required")
    _require(_instant(anchor.reservation.created_at)
             <= _instant(anchor.approval_review.created_at)
             <= _instant(anchor.result.created_at), "approval_order_unverified")
    try:
        contract._approval(approval)
        digest = contract.digest(approval)
    except (ValueError, TypeError, KeyError, RecursionError):
        raise ProvenanceHold("approval_manifest_unverified") from None
    expected = {"root_issue": anchor.root_issue, "root_pr": anchor.root_pr,
                "issue_title_sha256": anchor.issue_title_sha256,
                "issue_body_sha256": anchor.issue_body_sha256,
                "reservation_id": anchor.reservation.id,
                "approval_result_id": anchor.result.id,
                "protocol_commit": anchor.protocol_commit,
                "approved_main_sha": anchor.approved_main_sha}
    _require(digest == anchor.approval_digest
             and all(approval[k] == v for k, v in expected.items()),
             "approval_manifest_unverified")
    before = _issue(reader, anchor)
    reservation = _comment(reader, anchor.reservation, anchor.ledger_issue)
    result = _comment(reader, anchor.result, anchor.ledger_issue)
    _comment(reader, anchor.approval_review, anchor.root_issue)
    shared = {"Issue-Number": str(anchor.root_issue),
              "Review-Main-SHA": anchor.approved_main_sha,
              "Protocol-Commit": anchor.protocol_commit,
              "Issue-Title-SHA256": anchor.issue_title_sha256,
              "Issue-Body-SHA256": anchor.issue_body_sha256}
    _headers(reservation, {**shared, "Queue-Approval-Reservation": anchor.policy})
    _headers(result, {**shared, "Queue-Approval-Result": anchor.policy,
                     "Reservation-ID": str(anchor.reservation.id),
                     "Approval-Review-Comment-ID": str(anchor.approval_review.id),
                     "Outcome": "success"})
    commit = _get(reader, "git/commits/" + anchor.protocol_commit)
    _require(commit.get("sha") == anchor.protocol_commit
             and commit.get("url") == API + "git/commits/" + anchor.protocol_commit
             and type(commit.get("tree")) is dict
             and _matches(_SHA, commit["tree"].get("sha")),
             "protocol_commit_unverified")
    # IDs and digests do not defeat a concurrent edit. Re-read all live sources.
    for pin, issue in ((anchor.reservation, anchor.ledger_issue),
                       (anchor.result, anchor.ledger_issue),
                       (anchor.approval_review, anchor.root_issue)):
        _comment(reader, pin, issue)
    _require(_issue(reader, anchor) == before, "approval_snapshot_changed")
    return ApprovalEvidence(anchor.root_issue, anchor.root_pr, digest,
                            anchor.reservation.id, anchor.result.id,
                            anchor.protocol_commit, anchor.approved_main_sha)


def verify_review(reader, anchor: ReviewAnchor, *, pr_author_id: int) -> ReviewEvidence:
    """Verify an exact submitted GitHub review; COMMENT never becomes APPROVED.

    A distinct numeric GitHub identity is required here. Same-account technical
    review needs a separate verified agent-run identity integration, which this
    method does not infer from body text or self-declared reviewer_run_id.
    The run identifier is only an external anchor reference, not run proof.
    """
    _require(type(anchor) is ReviewAnchor and _positive(anchor.root_pr)
             and _positive(anchor.review_id) and _positive(anchor.author_id)
             and _positive(pr_author_id) and anchor.author_id != pr_author_id
             and _matches(_SHA, anchor.head_sha)
             and _matches(_DIGEST, anchor.body_sha256)
             and type(anchor.reviewer_run_id) is str
             and contract.ID.fullmatch(anchor.reviewer_run_id) is not None,
             "independent_review_anchor_required")
    _instant(anchor.submitted_at)
    route = "pulls/" + str(anchor.root_pr) + "/reviews/" + str(anchor.review_id)

    def pr():
        record = _get(reader, "pulls/" + str(anchor.root_pr))
        user, head, base = record.get("user"), record.get("head"), record.get("base")
        _require(type(record.get("number")) is int and record["number"] == anchor.root_pr
                 and record.get("state") == "open" and record.get("merged") is False
                 and type(user) is dict and type(user.get("id")) is int
                 and user.get("id") == pr_author_id
                 and type(head) is dict and head.get("sha") == anchor.head_sha
                 and type(head.get("repo")) is dict and head["repo"].get("full_name") == REPO
                 and type(base) is dict and base.get("ref") == "main"
                 and type(base.get("repo")) is dict and base["repo"].get("full_name") == REPO,
                 "review_pr_unverified")
        return (head["sha"], head.get("ref"), base.get("sha"), user["id"])

    def read():
        record = _get(reader, route)
        user = record.get("user")
        _require(type(record.get("id")) is int and record["id"] == anchor.review_id
                 and record.get("pull_request_url") == API + "pulls/" + str(anchor.root_pr)
                 and type(user) is dict and type(user.get("id")) is int
                 and user.get("id") == anchor.author_id and user.get("type") == "User"
                 and record.get("commit_id") == anchor.head_sha
                 and record.get("submitted_at") == anchor.submitted_at
                 and record.get("state") in ("COMMENTED", "APPROVED", "CHANGES_REQUESTED")
                 and _hash_text(record.get("body")) == anchor.body_sha256,
                 "review_record_changed_or_unverified")
        return record["state"]

    before = pr()
    state = read()
    _require(read() == state, "review_snapshot_changed")
    _require(pr() == before, "review_snapshot_changed")
    return ReviewEvidence(anchor.root_pr, anchor.review_id, anchor.head_sha,
                          anchor.reviewer_run_id, state, state == "APPROVED")
