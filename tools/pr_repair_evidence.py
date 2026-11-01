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


class EvidenceError(ValueError):
    """Invalid or incomplete provenance: fail closed, never infer success."""


def _object(value, required, optional=()):
    if not isinstance(value, dict):
        raise EvidenceError("expected JSON object")
    keys = set(value)
    missing = set(required) - keys
    extra = keys - set(required) - set(optional)
    if missing or extra:
        raise EvidenceError("invalid keys: missing=%s unexpected=%s" % (sorted(missing), sorted(extra)))
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


def canonical_bytes(value):
    return (json.dumps(value, sort_keys=True, ensure_ascii=False, separators=(",", ":")) + "\n").encode("utf-8")


def _workflow_run(raw, head_sha):
    _object(raw, (
        "id", "attempt", "workflow", "head_sha", "status", "conclusion",
        "jobs_complete", "jobs", "passed_tests", "failed_tests", "failures"
    ))
    run_id = _positive(raw["id"])
    attempt = _positive(raw["attempt"])
    workflow = raw["workflow"]
    if workflow not in WORKFLOWS:
        raise EvidenceError("unknown workflow")
    if _string(raw["head_sha"], SHA) != head_sha:
        raise EvidenceError("workflow belongs to a different head")
    status = raw["status"]
    conclusion = raw["conclusion"]
    if status not in STATUSES or (conclusion is not None and conclusion not in CONCLUSIONS):
        raise EvidenceError("invalid run status")
    if (status == "completed") != (conclusion is not None):
        raise EvidenceError("run completion/conclusion mismatch")
    if type(raw["jobs_complete"]) is not bool or not isinstance(raw["jobs"], list):
        raise EvidenceError("invalid jobs completeness")
    passed = _nonnegative(raw["passed_tests"])
    failed = _nonnegative(raw["failed_tests"])
    if not isinstance(raw["failures"], list) or failed != len(raw["failures"]):
        raise EvidenceError("failed test count must match individual records")

    jobs = []
    for job in raw["jobs"]:
        _object(job, ("id", "status", "conclusion", "steps"))
        job_id = _positive(job["id"])
        if job["status"] not in STATUSES:
            raise EvidenceError("invalid job status")
        if job["conclusion"] is not None and job["conclusion"] not in CONCLUSIONS:
            raise EvidenceError("invalid job conclusion")
        if not isinstance(job["steps"], list):
            raise EvidenceError("steps must be an array")
        steps = []
        for step in job["steps"]:
            _object(step, ("name", "required", "status", "conclusion"))
            if not isinstance(step["name"], str) or len(step["name"]) > 120:
                raise EvidenceError("invalid step name")
            if type(step["required"]) is not bool or step["status"] not in STATUSES:
                raise EvidenceError("invalid required step")
            if step["conclusion"] is not None and step["conclusion"] not in CONCLUSIONS:
                raise EvidenceError("invalid step conclusion")
            steps.append(dict(step))
        _unique(steps, "name", "job step")
        jobs.append({"id": job_id, "status": job["status"], "conclusion": job["conclusion"],
                     "steps": sorted(steps, key=lambda x: x["name"])})
    _unique(jobs, "id", "job ID")

    failures = []
    for item in raw["failures"]:
        _object(item, ("id", "reason_code", "evidence_sha256"))
        failures.append({
            "id": _string(item["id"]),
            "reason_code": _string(item["reason_code"]),
            "evidence_sha256": _string(item["evidence_sha256"], DIGEST)
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
        "passed_tests": passed, "failed_tests": failed,
        "failure_groups": groups, "outcome": outcome
    }


def _acceptance(raw, head_sha, runs):
    _object(raw, ("id", "status", "review_id", "evidence"))
    item_id = _string(raw["id"])
    if raw["status"] not in ACCEPTANCE:
        raise EvidenceError("invalid acceptance status")
    if not isinstance(raw["evidence"], list):
        raise EvidenceError("invalid acceptance evidence")
    review_id = raw["review_id"]
    if review_id is not None:
        _string(review_id)
    evidence = []
    successful_runs = {(run["id"], run["attempt"]) for run in runs if run["outcome"] == "pass"}
    for proof in raw["evidence"]:
        _object(proof, ("test_id", "head_sha", "run_id", "attempt"))
        if _string(proof["head_sha"], SHA) != head_sha:
            raise EvidenceError("acceptance evidence from stale head")
        evidence.append({
            "test_id": _string(proof["test_id"]), "head_sha": head_sha,
            "run_id": _positive(proof["run_id"]), "attempt": _positive(proof["attempt"])
        })
    evidence.sort(key=lambda x: (x["test_id"], x["run_id"], x["attempt"]))
    dedupe = [(x["test_id"], x["run_id"], x["attempt"]) for x in evidence]
    if len(set(dedupe)) != len(dedupe):
        raise EvidenceError("duplicate acceptance evidence")
    effective = raw["status"]
    if effective == "passed" and (not review_id or not evidence or
                                   any((x["run_id"], x["attempt"]) not in successful_runs for x in evidence)):
        effective = "needs_review"
    return {"id": item_id, "claimed_status": raw["status"], "status": effective,
            "review_id": review_id, "evidence": evidence}


def normalize(snapshot):
    """Normalize a reviewed input snapshot; never grant merge or execution authority."""
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

    if not isinstance(snapshot["heads"], list) or not snapshot["heads"]:
        raise EvidenceError("heads required")
    heads = []
    for head in snapshot["heads"]:
        _object(head, ("sha", "runs", "acceptance"))
        sha = _string(head["sha"], SHA)
        if not isinstance(head["runs"], list) or not isinstance(head["acceptance"], list):
            raise EvidenceError("invalid run/acceptance arrays")
        runs = [_workflow_run(run, sha) for run in head["runs"]]
        keys = [(run["id"], run["attempt"]) for run in runs]
        if len(set(keys)) != len(keys):
            raise EvidenceError("duplicate workflow run attempt")
        runs.sort(key=lambda x: (x["workflow"], x["id"], x["attempt"]))
        checks = [_acceptance(item, sha, runs) for item in head["acceptance"]]
        _unique(checks, "id", "acceptance requirement")
        checks.sort(key=lambda x: x["id"])
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
        elif not checks or any(item["status"] != "passed" for item in checks):
            readiness = "hold_acceptance"
        else:
            readiness = "evidence_complete_for_independent_review"
        heads.append({"sha": sha, "is_current_head": sha == current_head,
                      "ci_status": ci_status, "runs": runs, "acceptance": checks,
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
        "approval_evidence": {"reference": reference, "verified_claim": approval["verified"]},
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
            "steps": [{"name": s.get("name"), "status": s.get("status"), "conclusion": s.get("conclusion")}
                      for s in job.get("steps", [])]
        })
    return {
        "run_id": run_id, "head_sha": run.get("head_sha"),
        "workflow_name": run.get("name"), "status": run.get("status"),
        "conclusion": run.get("conclusion"), "attempt": run.get("run_attempt"),
        "jobs_complete": jobs_complete, "jobs": safe_jobs,
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
    except (EvidenceError, OSError, ValueError) as exc:
        parser.exit(2, "evidence incomplete: %s\n" % exc)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
