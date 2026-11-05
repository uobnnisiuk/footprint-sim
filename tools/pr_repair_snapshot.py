#!/usr/bin/env python3
"""Read-only evidence collection and reconciliation, never a publisher.

Source observations are not authority. In particular, prose and API success do
not create approval manifests, independent reviewer identities or acceptance
receipts. Missing old hosted runs leave consumption unknown, even with zero
observed repair reservations. No R4 execution, model, lock or migration path is
provided. Callers may serialize the safe result outside production state.
"""
from __future__ import annotations

from collections import Counter
from dataclasses import dataclass, replace
from datetime import datetime, timezone
import json
from pathlib import Path
import re

import pr_repair_adapter as adapter
import pr_repair_history as history
import pr_repair_provenance as provenance
import pr_repair_remote_audit as remote
import pr_repair_sources as sources

SCHEMA = "footprint-evidence-snapshot-v1"
_NUMBER = re.compile(r"[1-9][0-9]*\Z")
_POLICY = re.compile(r"footprint-[a-z0-9-]{1,70}\Z")
_BRANCH = re.compile(r"local-autodev/issue-([1-9][0-9]*)\Z")
_HEADER = re.compile(r"([A-Za-z][A-Za-z0-9-]*):[ \t]*(.*)\Z")
_OUTCOMES = {"success": "recorded_success", "failed": "failed",
             "failure": "failed", "interrupted": "interrupted",
             "cancelled": "interrupted", "timeout": "response_unknown",
             "unknown": "response_unknown", "response_unknown": "response_unknown"}


def _time(value):
    if not isinstance(value, datetime) or value.tzinfo is None or value.utcoffset() is None:
        raise ValueError("invalid_observation_time")
    return value.astimezone(timezone.utc)


def _timestamp(value):
    return _time(value).isoformat()


def _number(value):
    return int(value) if type(value) is str and len(value) <= 18 and _NUMBER.fullmatch(value) else None


def _ref(record):
    return "github:" + record.kind + ":" + record.record_id


def _headers(body):
    """Observe exact existing Queue headers; never interpret acceptance prose."""
    if type(body) is not str or len(body) > remote._MAX_JSON:
        return None
    lines = body.strip().splitlines()
    if not lines or not lines[0].startswith(("Queue-Approval-Reservation:", "Queue-Approval-Result:")):
        return None
    result = {}
    for line in lines:
        match = _HEADER.fullmatch(line)
        if match:
            key, value = match.groups()
            if key in result:
                return {}
            result[key] = value
    return result


@dataclass(frozen=True)
class SourceAttempt:
    """An observed relationship, not an importable reservation or retry grant."""
    source_kind: str
    root_issue: int | None
    root_pr: int | None
    reservation_sources: tuple[str, ...]
    execution_sources: tuple[str, ...]
    result_sources: tuple[str, ...]
    consumption: str
    outcome: str
    reasons: tuple[str, ...]

    def to_dict(self):
        return {"source_kind": self.source_kind, "root_issue": self.root_issue,
                "root_pr": self.root_pr, "reservation_sources": list(self.reservation_sources),
                "execution_sources": list(self.execution_sources),
                "result_sources": list(self.result_sources), "consumption": self.consumption,
                "outcome": self.outcome, "reasons": list(self.reasons)}


def _queue_attempts(records):
    """Keep approval-budget receipts separate from model/repair consumption."""
    reservations, results, invalid = {}, [], []
    comment_ids = {r.record_id: r for r in records if r.kind == "issue_comment" and r.verification == "observed"}
    issue_numbers = {r.parent_number for r in records if r.kind == "issue" and r.verification == "observed"}
    for record in records:
        if record.kind != "issue_comment" or record.verification != "observed":
            continue
        headers = _headers(record._payload.get("body"))
        if headers is None:
            continue
        issue = _number(headers.get("Issue-Number"))
        key = "Queue-Approval-Reservation" if "Queue-Approval-Reservation" in headers else "Queue-Approval-Result"
        policy = headers.get(key)
        if issue is None or type(policy) is not str or not _POLICY.fullmatch(policy):
            invalid.append(SourceAttempt("queue_approval", issue, None, (), (), (_ref(record),),
                                         "approval_only", "unknown", ("approval_receipt_malformed",)))
        elif key == "Queue-Approval-Reservation":
            reservations.setdefault(record.record_id, []).append((record, headers, issue))
        else:
            results.append((record, headers, issue))
    out = list(invalid)
    used_results = set()
    root_reservations = Counter(x[0][2] for x in reservations.values())
    for identity, versions in sorted(reservations.items()):
        record, headers, issue = versions[0]
        paired = [(r, h, n) for r, h, n in results if h.get("Reservation-ID") == identity]
        reasons = {"approval_scope_not_authenticated"}
        if issue not in issue_numbers:
            reasons.add("approval_issue_missing")
        if root_reservations[issue] > 1:
            reasons.add("duplicate_approval_reservation")
        outcome = "unknown"
        if len(versions) != 1:
            reasons.add("approval_reservation_conflict")
        if not paired:
            reasons.add("approval_result_missing")
        if len(paired) > 1:
            reasons.add("duplicate_approval_result")
        review_refs = set()
        for result, h, n in paired:
            used_results.add(id(result))
            if (n != issue or result.parent_number != record.parent_number or
                    h.get("Queue-Approval-Result") != headers["Queue-Approval-Reservation"] or
                    any(h.get(k) != headers.get(k) for k in
                        ("Review-Main-SHA", "Issue-Title-SHA256", "Issue-Body-SHA256"))):
                reasons.add("approval_result_conflict")
            if (record.created_at and result.created_at and result.created_at < record.created_at):
                reasons.add("approval_result_conflict")
            observed = _OUTCOMES.get(h.get("Outcome"), "unknown")
            review = comment_ids.get(h.get("Approval-Review-Comment-ID"))
            if review is None:
                reasons.add("approval_review_missing")
            elif review.parent_number != issue:
                reasons.add("approval_review_parent_conflict")
            else:
                review_refs.add(_ref(review))
            if outcome != "unknown" and observed != outcome:
                reasons.add("approval_result_conflict")
            outcome = observed
        if any("conflict" in r or "duplicate" in r for r in reasons):
            outcome = "conflict"
        out.append(SourceAttempt("queue_approval", issue, None,
                                 tuple(_ref(x[0]) for x in versions), (),
                                 tuple(_ref(x[0]) for x in paired) + tuple(sorted(review_refs)), "approval_only", outcome,
                                 tuple(sorted(reasons))))
    for record, _, issue in results:
        if id(record) not in used_results:
            out.append(SourceAttempt("queue_approval", issue, None, (), (), (_ref(record),),
                                     "approval_only", "unknown", ("orphan_approval_result",)))
    return out


def reconcile_sources(collection, local=None):
    """Preserve evidence whose old protocol cannot be authenticated as unknown.

    Git commits and prose cannot tell how many model invocations happened.
    They therefore form an unclassified intervention group, never inferred
    minor reservations. The unavailable old prompt is not replaced by a new
    invented parser or by R3's later protocol.
    """
    attempts = _queue_attempts(collection.records)
    pulls = {}
    for record in collection.records:
        if record.kind == "pull" and record.verification == "observed":
            pulls.setdefault(record.parent_number, record)
    for number, pull in sorted(pulls.items()):
        related = [r for r in collection.records if r.parent_number == number]
        commits = [r for r in related if r.kind == "commit"]
        marker = [r for r in related if r.verification == "observed"
                  and r.kind in ("issue_comment", "review", "review_comment")
                  and "footprint-pr-repair-v1" in str(r._payload.get("body", ""))]
        branch = pull._payload.get("head", {}).get("ref")
        match = _BRANCH.fullmatch(branch) if type(branch) is str else None
        if commits and match:
            attempts.append(SourceAttempt(
                "unclassified_git_intervention", int(match[1]), number, (),
                tuple(_ref(r) for r in commits),
                tuple(_ref(r) for r in related if r.kind in ("issue_comment", "review")),
                "unknown", "unknown", ("git_commits_are_not_invocations", "original_root_unverified")))
        for record in marker:
            attempts.append(SourceAttempt("legacy_minor_claim", None, number, (), (), (_ref(record),),
                                         "unknown", "unknown", ("legacy_protocol_unavailable",)))
    if local is not None:
        for record in local.records:
            evidence = tuple("local:" + x.source_id for x in record.evidence)
            event_refs = tuple("local:" + x.source_id for x in record.events)
            attempts.append(SourceAttempt(
                record.kind, record.root_issue, record.root_pr, ("local:" + record.source_id,),
                event_refs, tuple(x for x in evidence if x != "local:" + record.source_id),
                "consumed", record.result, tuple(record.reasons)))
    return tuple(attempts)


@dataclass(frozen=True)
class EvidenceSnapshot:
    observed_at: str
    finished_at: str
    source_collection: object
    attempts: tuple[SourceAttempt, ...]
    local_history: object | None
    approval_state: str
    review_state: str
    formal_approval: bool
    ci: dict
    diagnostics: tuple[tuple[str, str], ...]

    def to_dict(self):
        counts = Counter(a.source_kind for a in self.attempts)
        local = self.local_history.to_dict() if self.local_history is not None else None
        stale = any(r in ("local_observation_not_current_day", "observation_clock_regression",
                          "local_history_changed_during_collection") for _, r in self.diagnostics)
        if local is not None:
            local["counts_apply_to_observation_only"] = True
            local["current_day_counts_verified"] = not stale and local["audit"]["local_valid"]
            if stale:
                local["audit"]["effective_today"] = None
                local["audit"]["effective_hour"] = None
        legacy_unknown = counts["legacy_minor_claim"] > 0 or not self.source_collection.visible_inventory_complete
        return {"schema": SCHEMA, "state": "HOLD", "observed_at": self.observed_at,
                "finished_at": self.finished_at, "sources": self.source_collection.to_dict(),
                "attempts": [a.to_dict() for a in self.attempts],
                "observed_by_kind": dict(sorted(counts.items())),
                "legacy_minor": {"observed_reservations": None if legacy_unknown else 0,
                                 "unclassified_claims": counts["legacy_minor_claim"], "consumption": "unknown",
                                 "execution_history_complete": False},
                "local_history": local,
                "original_approval": self.approval_state, "review": self.review_state,
                "formal_approval": self.formal_approval, "ci": dict(self.ci),
                "semantic_acceptance": "unverified",
                "diagnostics": [{"category": kind, "reason": reason} for kind, reason in self.diagnostics],
                "may_reserve": False, "may_publish": False, "may_activate": False,
                "production_enabled": False, "r4_connection": "not_connected"}


def validate_evidence_bundle(snapshot: EvidenceSnapshot, *, now: datetime):
    """Recheck time at consumption; never convert a snapshot to TrustedFacts."""
    if type(snapshot) is not EvidenceSnapshot:
        raise ValueError("invalid_evidence_snapshot")
    observed = _time(datetime.fromisoformat(snapshot.observed_at))
    current = _time(now)
    reasons = set(snapshot.diagnostics)
    if current < observed:
        reasons.add(("conflict", "observation_clock_regression"))
    elif current.astimezone(history.hourly.daily.JST).date() != observed.astimezone(history.hourly.daily.JST).date():
        reasons.add(("unverified", "local_observation_not_current_day"))
    return replace(snapshot, diagnostics=tuple(sorted(reasons)))


def collect_evidence(reader, *, state_root: Path | None = None,
                     root_issue: int | None = None, pr_number: int | None = None,
                     approval_anchor=None, approval=None, review_anchor=None,
                     required_since: datetime | None = None, clock=None) -> EvidenceSnapshot:
    """Compose existing readers with coverage and safe source relationships.

    Authentication belongs to the separately managed reader. No credential
    discovery, archive restoration, live guard invocation or authority
    provisioning is attempted. The clock is injectable for offline fixtures.
    """
    clock = clock or (lambda: datetime.now(timezone.utc))
    started = _time(clock())
    for value in (root_issue, pr_number):
        if value is not None and (type(value) is not int or value < 1):
            raise ValueError("invalid_target_identity")
    diagnostics = {("unavailable", "legacy_execution_history_unavailable"),
                   ("unavailable", "legacy_prompt_history_unavailable"),
                   ("unavailable", "acceptance_receipts_unavailable")}
    main = None
    try:
        main = remote._main(reader)
    except (remote.AuditHold, OSError, ValueError, TypeError, KeyError):
        diagnostics.add(("unavailable", "main_unverified"))
    collection = sources.collect_github_sources(reader, now=started, required_since=required_since)
    for reason in collection.holds:
        category = "conflict" if any(x in reason for x in ("changed", "duplicate", "conflict")) else "unavailable"
        diagnostics.add((category, reason))
    local = None
    if state_root is not None:
        local = history.audit_history_records(state_root, started)
        diagnostics.update(("unverified", reason) for reason in local.audit.holds)
    else:
        diagnostics.add(("unavailable", "local_history_not_observed"))
    approval_state, review_state, formal = "unverified", "unverified", False
    ci = {"state": "unobserved", "semantic_acceptance": "unverified"}
    selected = next((r for r in collection.records if r.kind == "pull" and
                     r.verification == "observed" and r.parent_number == pr_number), None)
    if selected is not None:
        raw = selected._payload
        head = raw.get("head", {})
        if (remote._sha(head.get("sha")) and type(head.get("ref")) is str and
                type(raw.get("user", {}).get("id")) is int):
            try:
                ci = adapter.read_ci(reader, pr_number, head["sha"], head["ref"])
            except (adapter.AdapterHold, remote.AuditHold, OSError, ValueError, TypeError, KeyError):
                diagnostics.add(("unverified", "ci_metadata_unverified_or_changed"))
        else:
            diagnostics.add(("unverified", "selected_pr_unverified"))
    elif pr_number is not None:
        diagnostics.add(("unavailable", "selected_pr_not_observed"))
    else:
        diagnostics.add(("unverified", "ci_target_not_selected"))
    if approval_anchor is None:
        diagnostics.add(("unverified", "approval_anchor_required"))
    else:
        try:
            if (approval_anchor.root_issue != root_issue or approval_anchor.root_pr != pr_number):
                raise ValueError("anchor_scope_mismatch")
            verified = provenance.verify_approval(reader, approval_anchor, approval)
            approval_state = verified.status
        except (provenance.ProvenanceHold, OSError, ValueError, TypeError, KeyError, AttributeError):
            diagnostics.add(("unverified", "approval_provenance_mismatch"))
    if review_anchor is None:
        diagnostics.add(("unverified", "review_anchor_required"))
    else:
        try:
            if (selected is None or review_anchor.root_pr != pr_number or
                    review_anchor.head_sha != selected._payload["head"]["sha"]):
                raise ValueError("anchor_scope_mismatch")
            review = provenance.verify_review(reader, review_anchor,
                                               pr_author_id=selected._payload["user"]["id"])
            review_state, formal = review.state, review.formal_approval
            if review_state == "CHANGES_REQUESTED":
                diagnostics.add(("conflict", "changes_requested"))
        except (provenance.ProvenanceHold, OSError, ValueError, TypeError, KeyError, AttributeError):
            diagnostics.add(("unverified", "review_provenance_mismatch"))
    try:
        if main is not None and remote._main(reader) != main:
            diagnostics.add(("conflict", "main_changed_during_collection"))
        if selected is not None:
            fresh = reader.get("pulls/" + str(pr_number))
            if any(fresh.get(k) != selected._payload.get(k) for k in ("head", "base", "state", "updated_at")):
                diagnostics.add(("conflict", "selected_pr_changed_during_collection"))
    except (remote.AuditHold, OSError, ValueError, TypeError, KeyError, AttributeError):
        diagnostics.add(("unavailable", "final_readback_unavailable"))
    finished = _time(clock())
    if local is not None:
        again = history.audit_history_records(state_root, finished)
        if local.snapshot_digest != again.snapshot_digest or local.audit.local_valid != again.audit.local_valid:
            diagnostics.add(("conflict", "local_history_changed_during_collection"))
        # Keep the first observation and its time, never silently use its counts
        # for a different day. Source records remain even if counts expire.
    attempts = reconcile_sources(collection, local)
    for attempt in attempts:
        for reason in attempt.reasons:
            category = "conflict" if any(x in reason for x in ("conflict", "duplicate", "orphan")) else "unverified"
            diagnostics.add((category, reason))
    snapshot = EvidenceSnapshot(_timestamp(started), _timestamp(finished), collection,
                                attempts, local, approval_state, review_state, formal, ci,
                                tuple(sorted(diagnostics)))
    return validate_evidence_bundle(snapshot, now=finished)


def render_diagnostic(snapshot: EvidenceSnapshot) -> str:
    """Serialize only the explicitly safe output, never dataclass internals."""
    return json.dumps(snapshot.to_dict(), ensure_ascii=True, sort_keys=True, separators=(",", ":")) + "\n"
