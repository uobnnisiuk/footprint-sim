#!/usr/bin/env python3
"""Read-only, fail-closed evidence normalization for Footprint PR repair R1.

No candidate generation, GitHub writes, retry scheduling, or merge decisions.
"""
from __future__ import annotations

import argparse
import hashlib
import json
import os
from pathlib import Path
import re
import sys
from urllib.request import Request, urlopen

SCHEMA = "footprint-pr-repair-evidence-v1"
SHA = re.compile(r"^[0-9a-f]{40}$")
DIGEST = re.compile(r"^[0-9a-f]{64}$")
IDENTIFIER = re.compile(r"^[A-Za-z0-9_.:-]{1,120}$")
WORKFLOWS = frozenset(("normal", "research"))
ACCEPTANCE = frozenset(("not_implemented", "tested_not_run", "passed", "failed", "needs_review"))
CONCLUSIONS = frozenset(("success", "failure", "cancelled", "skipped", "timed_out", "action_required", "stale"))
STATUSES = frozenset(("queued", "in_progress", "completed", "waiting", "pending"))
# These four acceptance items are pinned only for the historical, independently
# reviewed PR #58 fixture. All other PRs need an external approved baseline;
# a self-declared snapshot cannot grant one.
PR58_ACCEPTANCE = frozenset(("provenance", "cli_integration",
                             "unknown_preservation", "snapshot_integrity"))
TEST_REFERENCE_KINDS = frozenset(("synthetic", "claimed_observed"))
# Names pinned from full job-step metadata for the reviewed normal/F19
# workflow revisions (including checkout, environment and post steps).
# New or changed workflows must be independently re-pinned: fail closed.
REQUIRED_STEPS = {
    "normal": frozenset((
        "Set up job",
        "Run actions/checkout@11d5960a326750d5838078e36cf38b85af677262",
        "Check automation scripts",
        "Run actions/setup-dotnet@67a3573c9a986a3f9c594539f4ab511d57bb3ce9",
        "Check environment",
        "Restore",
        "Build",
        "Test",
        "Format",
        "Check S0 and S1",
        "Check evaluation harness",
        "Post Run actions/setup-dotnet@67a3573c9a986a3f9c594539f4ab511d57bb3ce9",
        "Post Run actions/checkout@11d5960a326750d5838078e36cf38b85af677262",
        "Complete job",
    )),
    "research": frozenset((
        "Set up job",
        "Check out the exact research revision",
        "Check out the frozen F18 research baseline separately",
        "Run actions/setup-dotnet@67a3573c9a986a3f9c594539f4ab511d57bb3ce9",
        "Run fixed research commands without credential environment variables",
        "Reject symlinks and special files before any artifact upload",
        "Retain full research results and failure evidence",
        "Link the immutable artifact in the run summary",
        "Post Run actions/setup-dotnet@67a3573c9a986a3f9c594539f4ab511d57bb3ce9",
        "Post Check out the frozen F18 research baseline separately",
        "Post Check out the exact research revision",
        "Complete job",
    )),
}
FAILURE_REFERENCE_KINDS = frozenset(("synthetic", "claimed_excerpt"))
REASON_CODES = frozenset(("fixture_provenance", "region_id_order_assumption",
                          "test_failure", "ci_failure", "unknown_failure"))
LOG_STATES = frozenset(("verified_available", "unverified", "missing", "expired"))


class EvidenceError(ValueError):
    """Invalid or incomplete provenance: fail closed, never infer success."""


def _object(value, required, optional=()):
    if not isinstance(value, dict):
        raise EvidenceError("expected JSON object")
    keys = set(value)
    missing = set(required) - keys
    extra = keys - set(required) - set(optional)
    if missing or extra:
        raise EvidenceError("invalid object structure (missing=%d unexpected=%d)" % (len(missing), len(extra)))
    return value


def _string(value, pattern=IDENTIFIER):
    if not isinstance(value, str) or not pattern.fullmatch(value):
        raise EvidenceError("invalid identifier or digest")
    return value


def _positive(value):
    if type(value) is not int or value < 1:
        raise EvidenceError("expected positive integer")
    return value


def _nonnegative(value):
    if type(value) is not int or value < 0:
        raise EvidenceError("expected nonnegative integer")
    return value


def _unique(items, key, label):
    values = [item[key] for item in items]
    if len(values) != len(set(values)):
        raise EvidenceError("duplicate " + label)


def _safe_hash(value):
    """Opaque, deterministic identifier: never echo arbitrary source text."""
    return hashlib.sha256(value.encode("utf-8")).hexdigest()


def _public_acceptance_id(value):
    """Only pinned neutral fixture IDs can be shown verbatim."""
    return value if value in PR58_ACCEPTANCE else "acceptance-" + _safe_hash(value)


def _allowed(value, choices):
    """Test an untrusted enum without hashing malformed JSON arrays/objects."""
    return isinstance(value, str) and value in choices


def _reason(value):
    if not isinstance(value, str) or value not in REASON_CODES:
        raise EvidenceError("unapproved failure reason code")
    return value


def canonical_bytes(value):
    return (json.dumps(value, sort_keys=True, ensure_ascii=False, separators=(",", ":")) + "\n").encode("utf-8")


def _workflow_run(raw, head_sha):
    _object(raw, (
        "id", "attempt", "workflow", "head_sha", "status", "conclusion",
        "jobs_complete", "jobs", "passed_tests", "failed_tests", "failures", "log_evidence"
    ))
    run_id = _positive(raw["id"])
    attempt = _positive(raw["attempt"])
    workflow = raw["workflow"]
    if not _allowed(workflow, WORKFLOWS):
        raise EvidenceError("unknown workflow")
    if _string(raw["head_sha"], SHA) != head_sha:
        raise EvidenceError("workflow belongs to a different head")
    status = raw["status"]
    conclusion = raw["conclusion"]
    if not _allowed(status, STATUSES) or (conclusion is not None and not _allowed(conclusion, CONCLUSIONS)):
        raise EvidenceError("invalid run status")
    if (status == "completed") != (conclusion is not None):
        raise EvidenceError("run completion/conclusion mismatch")
    if type(raw["jobs_complete"]) is not bool or not isinstance(raw["jobs"], list):
        raise EvidenceError("invalid jobs completeness")
    logs = _object(raw["log_evidence"], ("state", "review_ref"))
    if not _allowed(logs["state"], LOG_STATES):
        raise EvidenceError("invalid log evidence state")
    if logs["state"] == "verified_available":
        _string(logs["review_ref"])
    elif logs["review_ref"] is not None:
        raise EvidenceError("unverified log state cannot claim a reviewer")
    passed = _nonnegative(raw["passed_tests"])
    failed = _nonnegative(raw["failed_tests"])
    if not isinstance(raw["failures"], list) or failed != len(raw["failures"]):
        raise EvidenceError("failed test count must match individual records")

    jobs = []
    observed_required = set()
    unknown_step = False
    mismatched_required = False
    for job in raw["jobs"]:
        _object(job, ("id", "status", "conclusion", "steps"))
        job_id = _positive(job["id"])
        if not _allowed(job["status"], STATUSES):
            raise EvidenceError("invalid job status")
        if job["conclusion"] is not None and not _allowed(job["conclusion"], CONCLUSIONS):
            raise EvidenceError("invalid job conclusion")
        if not isinstance(job["steps"], list):
            raise EvidenceError("steps must be an array")
        steps = []
        for step in job["steps"]:
            _object(step, ("name", "required", "status", "conclusion"))
            name = step["name"]
            if not isinstance(name, str) or not 1 <= len(name) <= 120:
                raise EvidenceError("invalid step name")
            if type(step["required"]) is not bool or not _allowed(step["status"], STATUSES):
                raise EvidenceError("invalid required step")
            if step["conclusion"] is not None and not _allowed(step["conclusion"], CONCLUSIONS):
                raise EvidenceError("invalid step conclusion")
            # Required status comes from a fixed, reviewed workflow policy, not
            # the caller's Boolean. Extra or renamed steps cannot imply success.
            policy_required = name in REQUIRED_STEPS[workflow]
            if policy_required:
                observed_required.add(name)
            else:
                unknown_step = True
            if step["required"] is not policy_required:
                mismatched_required = True
            # Job/step metadata can be attacker controlled; emit only known
            # neutral names. Hash unknown names without storing original text.
            safe_name = name if policy_required else "unverified-step-" + _safe_hash(name)
            steps.append({"name": safe_name, "required": policy_required,
                          "status": step["status"], "conclusion": step["conclusion"]})
        _unique(steps, "name", "job step")
        jobs.append({"id": job_id, "status": job["status"], "conclusion": job["conclusion"],
                     "steps": sorted(steps, key=lambda x: x["name"])})
    _unique(jobs, "id", "job ID")
    # The reviewed workflows have one job each. Combining fragments of
    # different jobs never creates a complete successful job.
    required_steps_missing = (len(jobs) != 1 or
                              not REQUIRED_STEPS[workflow].issubset(observed_required))

    failures = []
    for item in raw["failures"]:
        _object(item, ("id", "reason_code", "evidence_sha256", "evidence_kind"))
        kind = item["evidence_kind"]
        if not _allowed(kind, FAILURE_REFERENCE_KINDS):
            raise EvidenceError("invalid failure evidence kind")
        if kind == "synthetic":
            # Example failures have no actual source excerpt to hash.
            if item["evidence_sha256"] is not None:
                raise EvidenceError("synthetic failure cannot claim a digest")
            digest = None
        else:
            digest = _string(item["evidence_sha256"], DIGEST)
        failures.append({
            "id": "failure-" + _safe_hash(_string(item["id"])),
            "reason_code": _reason(item["reason_code"]),
            "evidence_kind": kind,
            "evidence_verification": "unverified",
            "evidence_sha256": None,
            "claimed_evidence_sha256": digest
        })
    _unique(failures, "id", "failure record")
    failures.sort(key=lambda x: x["id"])
    grouped = {}
    for failure in failures:
        grouped.setdefault(failure["reason_code"], []).append(failure)
    groups = [{"reason_code": reason, "count": len(items), "failures": items}
              for reason, items in sorted(grouped.items())]

    if not raw["jobs_complete"] or not jobs:
        outcome = "unknown"
    elif status != "completed":
        outcome = "wait"
    elif conclusion == "failure" or conclusion == "timed_out":
        outcome = "fail"
    elif conclusion != "success":
        outcome = "unknown"
    elif failed != 0:
        outcome = "unknown"
    elif (logs["state"] != "verified_available" or unknown_step or
          mismatched_required or required_steps_missing):
        outcome = "unknown"
    elif any(job["status"] != "completed" or job["conclusion"] != "success"
             or any(step["required"] and (step["status"] != "completed" or step["conclusion"] != "success")
                    for step in job["steps"]) for job in jobs):
        outcome = "unknown"
    elif not any(step["required"] for job in jobs for step in job["steps"]):
        outcome = "unknown"
    else:
        outcome = "pass"

    return {
        "id": run_id, "attempt": attempt, "workflow": workflow,
        "head_sha": head_sha, "status": status, "conclusion": conclusion,
        "jobs_complete": raw["jobs_complete"], "jobs": sorted(jobs, key=lambda x: x["id"]),
        "log_evidence": {"state": logs["state"],
                         "review_ref_sha256": _safe_hash(logs["review_ref"]) if logs["review_ref"] else None},
        "required_step_policy_complete": not (unknown_step or mismatched_required or required_steps_missing),
        "passed_tests": passed, "failed_tests": failed,
        "failure_groups": groups, "outcome": outcome
    }


def _external_verified_refs(references):
    """R2 must independently validate actual CI tests and reviewer identities.

    Each ref: (head_sha, run_id, attempt, actual_test_id, review_id).
    Input snapshots and CLI never supply this trusted side channel.
    """
    if references is None:
        return frozenset()
    if not isinstance(references, (list, tuple, set, frozenset)):
        raise EvidenceError("invalid external evidence set")
    verified = set()
    for item in references:
        if not isinstance(item, (list, tuple)) or len(item) != 5:
            raise EvidenceError("invalid external evidence reference")
        head_sha, run_id, attempt, test_id, review_id = item
        ref = (_string(head_sha, SHA), _positive(run_id), _positive(attempt),
               _string(test_id), _string(review_id))
        if ref in verified:
            raise EvidenceError("duplicate external evidence reference")
        verified.add(ref)
    return frozenset(verified)


def _acceptance(raw, head_sha, runs, externally_verified):
    _object(raw, ("id", "status", "review_id", "evidence"))
    raw_id = _string(raw["id"])
    if not _allowed(raw["status"], ACCEPTANCE):
        raise EvidenceError("invalid acceptance status")
    if not isinstance(raw["evidence"], list):
        raise EvidenceError("invalid acceptance evidence")
    review_id = raw["review_id"]
    if review_id is not None:
        _string(review_id)
    evidence = []
    successful_runs = {(run["id"], run["attempt"]) for run in runs if run["outcome"] == "pass"}
    for proof in raw["evidence"]:
        _object(proof, ("test_id", "head_sha", "run_id", "attempt", "reference_kind"))
        if _string(proof["head_sha"], SHA) != head_sha:
            raise EvidenceError("acceptance evidence from stale head")
        test_id = _string(proof["test_id"])
        kind = proof["reference_kind"]
        if not _allowed(kind, TEST_REFERENCE_KINDS):
            raise EvidenceError("invalid test reference kind")
        run_id, attempt = _positive(proof["run_id"]), _positive(proof["attempt"])
        attested = (kind == "claimed_observed" and review_id is not None and
                    (head_sha, run_id, attempt, test_id, review_id) in externally_verified)
        evidence.append({
            "test_id_sha256": _safe_hash(test_id), "reference_kind": kind,
            "verification": "externally_verified" if attested else "unverified",
            "head_sha": head_sha, "run_id": run_id, "attempt": attempt
        })
    evidence.sort(key=lambda x: (x["test_id_sha256"], x["run_id"], x["attempt"]))
    dedupe = [(x["test_id_sha256"], x["run_id"], x["attempt"]) for x in evidence]
    if len(set(dedupe)) != len(dedupe):
        raise EvidenceError("duplicate acceptance evidence")
    effective = raw["status"]
    if effective == "passed" and (not review_id or not evidence or
                                  any(x["verification"] != "externally_verified" or
                                      (x["run_id"], x["attempt"]) not in successful_runs
                                      for x in evidence)):
        effective = "needs_review"
    return {"id": _public_acceptance_id(raw_id), "claimed_status": raw["status"],
            "status": effective,
            "review_id_sha256": _safe_hash(review_id) if review_id else None,
            "evidence": evidence}

def normalize(snapshot, approved_acceptance_ids=None, verified_test_evidence=None):
    """Normalize evidence; only an independently supplied baseline can cover new PRs.

    External baselines and test attestations must come from independently verified
    review/CI sources, never from the snapshot or model. CLI cannot supply them.
    """
    _object(snapshot, ("schema_version", "repository", "pr_number", "issue_number",
                       "main_sha", "current_head_sha", "approval", "heads"))
    if snapshot["schema_version"] != SCHEMA:
        raise EvidenceError("unsupported evidence schema")
    repository = snapshot["repository"]
    if not isinstance(repository, str) or not re.fullmatch(r"[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+", repository):
        raise EvidenceError("invalid repository")
    pr_number = _positive(snapshot["pr_number"])
    issue_number = _positive(snapshot["issue_number"])
    main_sha = _string(snapshot["main_sha"], SHA)
    current_head = _string(snapshot["current_head_sha"], SHA)
    approval = _object(snapshot["approval"], ("reference", "verified"))
    reference = _string(approval["reference"])
    if type(approval["verified"]) is not bool:
        raise EvidenceError("approval verified must be boolean")
    if approved_acceptance_ids is not None:
        if not isinstance(approved_acceptance_ids, (list, tuple, frozenset)) or not approved_acceptance_ids:
            raise EvidenceError("invalid external approval baseline")
        expected = [_string(x) for x in approved_acceptance_ids]
        if len(expected) != len(set(expected)):
            raise EvidenceError("duplicate external acceptance ID")
        expected_ids = frozenset(expected)
    elif (repository, pr_number, issue_number) == ("uobnnisiuk/footprint-sim", 58, 56):
        expected_ids = PR58_ACCEPTANCE
    else:
        expected_ids = None  # Not reviewable as complete without an external baseline.

    externally_verified = _external_verified_refs(verified_test_evidence)

    if not isinstance(snapshot["heads"], list) or not snapshot["heads"]:
        raise EvidenceError("heads required")
    heads = []
    # GitHub run IDs are global within the repository and keep their identity
    # across attempts. Never attribute one run ID to another head or workflow.
    run_owners = {}
    for head in snapshot["heads"]:
        _object(head, ("sha", "runs", "acceptance"))
        sha = _string(head["sha"], SHA)
        if not isinstance(head["runs"], list) or not isinstance(head["acceptance"], list):
            raise EvidenceError("invalid run/acceptance arrays")
        runs = [_workflow_run(run, sha) for run in head["runs"]]
        keys = [(run["id"], run["attempt"]) for run in runs]
        if len(set(keys)) != len(keys):
            raise EvidenceError("duplicate workflow run attempt")
        for run in runs:
            owner = (sha, run["workflow"])
            previous_owner = run_owners.setdefault(run["id"], owner)
            if previous_owner != owner:
                raise EvidenceError("workflow run ID has conflicting head or workflow")
        runs.sort(key=lambda x: (x["workflow"], x["id"], x["attempt"]))
        checks = [_acceptance(item, sha, runs, externally_verified)
                  for item in head["acceptance"]]
        _unique(checks, "id", "acceptance requirement")
        checks.sort(key=lambda x: x["id"])
        actual_ids = {_string(item["id"]) for item in head["acceptance"]}
        missing_acceptance = sorted(_public_acceptance_id(x) for x in expected_ids - actual_ids) if expected_ids is not None else []
        unapproved_acceptance = sorted(_public_acceptance_id(x) for x in actual_ids - expected_ids) if expected_ids is not None else []
        kinds = {kind: [r for r in runs if r["workflow"] == kind] for kind in WORKFLOWS}
        ci_status = {}
        for kind in sorted(WORKFLOWS):
            selected = kinds[kind]
            if len(selected) != 1:
                ci_status[kind] = "unknown"
            else:
                ci_status[kind] = selected[0]["outcome"]
        if not approval["verified"]:
            readiness = "hold_approval_unverified"
        elif any(value != "pass" for value in ci_status.values()):
            readiness = "hold_ci"
        elif (expected_ids is None or missing_acceptance or unapproved_acceptance or
              not checks or any(item["status"] != "passed" for item in checks)):
            readiness = "hold_acceptance"
        else:
            readiness = "evidence_complete_for_independent_review"
        heads.append({"sha": sha, "is_current_head": sha == current_head,
                      "ci_status": ci_status, "runs": runs, "acceptance": checks,
                      "missing_approved_acceptance": missing_acceptance,
                      "unapproved_acceptance": unapproved_acceptance,
                      "acceptance_baseline_known": expected_ids is not None,
                      "readiness": readiness})
    _unique(heads, "sha", "head SHA")
    heads.sort(key=lambda x: x["sha"])
    if sum(head["is_current_head"] for head in heads) != 1:
        raise EvidenceError("current head absent or ambiguous")
    current = next(x for x in heads if x["is_current_head"])
    result = {
        "schema_version": SCHEMA, "repository": repository,
        "pr_number": pr_number, "issue_number": issue_number,
        "main_sha": main_sha, "current_head_sha": current_head,
        "approval_evidence": {"reference_sha256": _safe_hash(reference),
                              "verified_claim": approval["verified"],
                              "acceptance_baseline_sha256": _safe_hash("|".join(sorted(expected_ids)))
                              if expected_ids is not None else None},
        "heads": heads, "current_head_readiness": current["readiness"],
        "may_merge": False, "may_repair": False
    }
    # The digest excludes collection times and any raw logs. It is content-bound.
    result["sha256"] = hashlib.sha256(canonical_bytes(result)).hexdigest()
    return result


def _api_json(url):
    if not url.startswith("https://api.github.com/repos/"):
        raise EvidenceError("unexpected API origin")
    headers = {"Accept": "application/vnd.github+json", "User-Agent": "footprint-evidence-readonly"}
    token = os.environ.get("GITHUB_TOKEN")
    if token:
        headers["Authorization"] = "Bearer " + token
    request = Request(url, headers=headers, method="GET")
    with urlopen(request, timeout=20) as response:
        return json.load(response)


def fetch_run_readonly(repository, run_id, get_json=_api_json):
    """Read-only API adapter. Does not infer acceptance or fetch/execute job logs."""
    if not re.fullmatch(r"[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+", repository):
        raise EvidenceError("invalid repository")
    _positive(run_id)
    prefix = "https://api.github.com/repos/" + repository + "/actions/runs/" + str(run_id)
    run = get_json(prefix)
    if type(run) is not dict or run.get("id") != run_id:
        raise EvidenceError("run ID mismatch")
    jobs_payload = get_json(prefix + "/jobs?per_page=100&page=1")
    jobs = jobs_payload.get("jobs") if isinstance(jobs_payload, dict) else None
    total = jobs_payload.get("total_count") if isinstance(jobs_payload, dict) else None
    if type(total) is not int or not isinstance(jobs, list):
        raise EvidenceError("job listing is unavailable")
    # No hidden pagination: if >100 jobs, report missing coverage; never claim success.
    jobs_complete = total == len(jobs)
    safe_jobs = []
    for job in jobs:
        safe_jobs.append({
            "id": job.get("id"), "status": job.get("status"), "conclusion": job.get("conclusion"),
            "steps": [{"name_sha256": _safe_hash(str(s.get("name", ""))),
                       "status": s.get("status") if _allowed(s.get("status"), STATUSES) else "unverified",
                       "conclusion": s.get("conclusion") if _allowed(s.get("conclusion"), CONCLUSIONS) else None}
                      for s in job.get("steps", [])]
        })
    return {
        "run_id": run_id, "head_sha": run.get("head_sha"),
        "workflow_name_sha256": _safe_hash(str(run.get("name", ""))),
        "status": run.get("status") if _allowed(run.get("status"), STATUSES) else "unverified",
        "conclusion": run.get("conclusion") if _allowed(run.get("conclusion"), CONCLUSIONS) else None,
        "attempt": run.get("run_attempt"),
        "jobs_complete": jobs_complete, "jobs": safe_jobs,
        "log_evidence": {"state": "unverified", "review_ref": None},
        "required_steps_verified": False,
        "acceptance_unverified": True, "read_only": True
    }


def main(argv=None):
    parser = argparse.ArgumentParser(description="Read-only PR repair evidence; never creates approvals")
    actions = parser.add_mutually_exclusive_group(required=True)
    actions.add_argument("--snapshot", type=Path, help="Reviewed JSON snapshot to normalize")
    actions.add_argument("--fetch-run", type=int, help="Inspect one GitHub Actions run via GET only")
    parser.add_argument("--repo", help="Required with --fetch-run: owner/repo")
    args = parser.parse_args(argv)
    try:
        if args.snapshot:
            # Duplicate keys in JSON would make source provenance ambiguous.
            def reject_duplicates(pairs):
                result = {}
                for key, value in pairs:
                    if key in result:
                        raise EvidenceError("duplicate JSON key")
                    result[key] = value
                return result
            data = json.loads(args.snapshot.read_text(encoding="utf-8"), object_pairs_hook=reject_duplicates)
            result = normalize(data)
        else:
            if not args.repo:
                parser.error("--repo required with --fetch-run")
            result = fetch_run_readonly(args.repo, args.fetch_run)
        sys.stdout.buffer.write(canonical_bytes(result))
    except (EvidenceError, OSError, ValueError):
        # Never echo raw JSON keys, file paths, or parser details.
        parser.exit(2, "evidence incomplete: invalid or unverified input\n")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
