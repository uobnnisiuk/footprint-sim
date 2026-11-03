#!/usr/bin/env python3
"""R2 pure, fail-closed repair scope preflight; never grants execution authority.

The caller MUST independently acquire TrustedFacts and complete Git tree manifests.
No GitHub API, network, subprocess, shell, writes, clocks, or model calls here.
"""
from __future__ import annotations

from dataclasses import dataclass
import difflib
import hashlib
import json
import re

SCHEMA = "footprint-pr-repair-request-v1"
POLICIES = {"minor": "footprint-pr-repair-v1",
            "substantive": "footprint-pr-substantive-repair-v1"}
SHA = re.compile(r"[0-9a-f]{40}\Z")
DIGEST = re.compile(r"[0-9a-f]{64}\Z")
ID = re.compile(r"[A-Za-z0-9_.:-]{1,120}\Z")
PATH = re.compile(r"(?:src|tests|docs|scenarios)/(?:[A-Za-z0-9][A-Za-z0-9._-]*/)*[A-Za-z0-9][A-Za-z0-9._-]*\Z")
BLOCKED = {"agents.md", "global.json", "nuget.config", "packages.config",
           "packages.lock.json", "package.json", "package-lock.json", "project.json",
           "project.assets.json", "dotnet-tools.json", "sdk.json", "research-autodev-state.json"}
SUFFIX = {".csproj", ".fsproj", ".vbproj", ".sln", ".slnx", ".slnf",
          ".props", ".targets", ".proj", ".rsp"}
DIRS = {"results", "bin", "obj", "node_modules"}
INVARIANTS = {"candidate_set", "count", "candidate_id_order", "cli_byte_determinism"}
RISK = re.compile(r"\b(?:Assert|assert|Skip|skip|Ignore|IsComplete|SequenceEqual|OrderBy|CandidateId|threshold|TestMethod|\[Fact\])\b")


class ContractError(ValueError):
    """Only fixed, non-sensitive reason codes are exposed."""


def reject(reason):
    raise ContractError(reason)


def fields(obj, names):
    if type(obj) is not dict or set(obj) != set(names):
        reject("invalid_schema")
    return obj


def string(value, pattern=ID):
    if type(value) is not str or not pattern.fullmatch(value):
        reject("invalid_value")
    return value


def integer(value, low=1, high=2**53):
    if type(value) is not int or not low <= value <= high:
        reject("invalid_value")
    return value


def vector(value, low=1, high=100):
    if type(value) is not list or not low <= len(value) <= high:
        reject("invalid_schema")
    return value


def unique(items):
    if len(items) != len(set(items)):
        reject("duplicate_entry")


def canonical(value):
    return json.dumps(value, sort_keys=True, ensure_ascii=False, separators=(",", ":"), allow_nan=False).encode("utf-8")


def digest(value):
    return hashlib.sha256(canonical(value)).hexdigest()


def parse_json(raw):
    """Reject repeated keys, nonfinite values, oversized/non-UTF-8 text."""
    if type(raw) is not bytes or not raw or len(raw) > 500_000:
        reject("invalid_json")
    def pairs(p):
        out = {}
        for k, v in p:
            if k in out:
                reject("duplicate_key")
            out[k] = v
        return out
    def bad_constant(_):
        reject("invalid_json")
    try:
        return json.loads(raw.decode("utf-8"), object_pairs_hook=pairs,
                          parse_constant=bad_constant)
    except (ValueError, UnicodeError, RecursionError):
        reject("invalid_json")


def safe_path(value):
    if type(value) is not str or (value != "README.md" and not PATH.fullmatch(value)):
        reject("disallowed_path")
    parts = value.split("/")
    if any(p.casefold() in BLOCKED for p in parts) or any(p.casefold() in DIRS for p in parts[:-1]) or any(value.casefold().endswith(x) for x in SUFFIX):
        reject("disallowed_path")
    return value


def blob(text):
    if type(text) is not str or "\x00" in text:
        reject("invalid_encoding")
    try:
        data = text.encode("utf-8", "strict")
    except UnicodeError:
        reject("invalid_encoding")
    if len(data) > 180_000 or b"\r" in data:
        reject("size_limit")
    return hashlib.sha1(b"blob " + str(len(data)).encode() + b"\0" + data).hexdigest()


def _approval(a):
    fields(a, ("schema", "root_issue", "root_pr", "issue_title_sha256", "issue_body_sha256",
               "reservation_id", "approval_result_id", "protocol_commit", "approved_main_sha",
               "paths", "acceptance", "required_verifications", "limits"))
    if a["schema"] != SCHEMA:
        reject("unknown_schema")
    integer(a["root_issue"]); integer(a["root_pr"])
    for k in ("issue_title_sha256", "issue_body_sha256"):
        string(a[k], DIGEST)
    for k in ("reservation_id", "approval_result_id"):
        integer(a[k])
    for k in ("protocol_commit", "approved_main_sha"):
        string(a[k], SHA)
    for p in vector(a["paths"], high=100):
        fields(p, ("path", "approved_oid", "allowed_operations"))
        safe_path(p["path"]); string(p["approved_oid"], SHA)
        if p["allowed_operations"] != ["modify"]:
            reject("disallowed_operation")
    unique([p["path"].casefold() for p in a["paths"]])
    for x in vector(a["acceptance"]):
        fields(x, ("id", "expected_sha256"))
        string(x["id"]); string(x["expected_sha256"], DIGEST)
    unique([x["id"] for x in a["acceptance"]])
    for x in vector(a["required_verifications"]):
        fields(x, ("id", "command_sha256"))
        string(x["id"]); string(x["command_sha256"], DIGEST)
    unique([x["id"] for x in a["required_verifications"]])
    fields(a["limits"], ("max_files", "max_lines"))
    integer(a["limits"]["max_files"], 1, 30)
    integer(a["limits"]["max_lines"], 1, 600)


def _request(r):
    fields(r, ("schema", "policy", "kind", "root_issue", "root_pr", "issue_title_sha256",
               "issue_body_sha256", "reservation_id", "approval_result_id", "protocol_commit",
               "approved_main_sha", "current_main_sha", "current_head_sha", "original_pr_base_sha",
               "review_finding_id", "reviewer_run_id", "workflow", "workflow_run_id", "workflow_attempt",
               "job_id", "observed_head_sha", "failure_code", "evidence_sha256", "paths",
               "acceptance", "required_verifications", "limits", "repair_reservation_id",
               "expires_at", "attempt"))
    if r["schema"] != SCHEMA or r["kind"] not in POLICIES or r["policy"] != POLICIES[r["kind"]]:
        reject("unknown_schema")
    for k in ("root_issue", "root_pr", "reservation_id", "approval_result_id", "workflow_run_id", "workflow_attempt", "job_id", "attempt"):
        integer(r[k])
    if r["attempt"] > (1 if r["kind"] == "minor" else 2):
        reject("attempt_limit")
    for k in ("issue_title_sha256", "issue_body_sha256", "evidence_sha256"):
        string(r[k], DIGEST)
    for k in ("protocol_commit", "approved_main_sha", "current_main_sha", "current_head_sha", "original_pr_base_sha", "observed_head_sha"):
        string(r[k], SHA)
    for k in ("review_finding_id", "reviewer_run_id", "workflow", "failure_code", "repair_reservation_id"):
        string(r[k])
    if type(r["expires_at"]) is not int or r["expires_at"] < 1:
        reject("invalid_value")
    fields(r["limits"], ("max_files", "max_lines"))
    integer(r["limits"]["max_files"], 1, 3); integer(r["limits"]["max_lines"], 1, 600)
    for x in vector(r["paths"], high=3):
        fields(x, ("path", "before_oid", "after_oid", "operation"))
        safe_path(x["path"]); string(x["before_oid"], SHA); string(x["after_oid"], SHA)
        if x["operation"] != "modify" or x["before_oid"] == x["after_oid"]:
            reject("disallowed_operation")
    unique([x["path"].casefold() for x in r["paths"]])
    for key in ("acceptance", "required_verifications"):
        for x in vector(r[key]):
            fields(x, ("id", "expected_sha256") if key == "acceptance" else ("id", "command_sha256"))
            string(x["id"]); string(x["expected_sha256" if key == "acceptance" else "command_sha256"], DIGEST)
        unique([x["id"] for x in r[key]])


@dataclass(frozen=True)
class TrustedFacts:
    """Must originate from *independent* verified GitHub/approval/ledger reads.

    Constructing this object from the RepairRequest itself is forbidden. This
    class is a transport contract, not an authentication mechanism.
    """
    approval_digest: str
    issue_title_sha256: str
    issue_body_sha256: str
    reservation_id: int
    approval_result_id: int
    protocol_commit: str
    current_main_sha: str
    current_head_sha: str
    original_pr_base_sha: str
    finding_id: str
    reviewer_run_id: str
    workflow_run_id: int
    workflow_attempt: int
    job_id: int
    observed_head_sha: str
    evidence_sha256: str
    tree_roots: tuple  # ((stage, independently verified Git tree SHA), ...)
    now_epoch: int
    prior_changed_lines: int
    prior_changed_paths: tuple
    first_pr_changed_paths: tuple
    previous_attempts: int
    reviewer_semantic_delta_sha256: str | None = None
    reviewer_invariants: tuple = ()


def _tree(tree, expected):
    fields(tree, ("root_sha", "entries", "complete"))
    if tree["complete"] is not True or tree["root_sha"] != expected or type(tree["entries"]) is not dict:
        reject("incomplete_tree")
    entries = tree["entries"]
    if not entries or len(entries) > 20000:
        reject("incomplete_tree")
    for path, meta in entries.items():
        if type(path) is not str or not path or path.startswith("/") or ".." in path.split("/") or "\\" in path:
            reject("invalid_tree")
        fields(meta, ("oid", "mode", "text"))
        string(meta["oid"], SHA)
        if meta["mode"] not in ("100644", "100755", "120000", "160000"):
            reject("invalid_tree")
        if meta["text"] is not None and blob(meta["text"]) != meta["oid"]:
            reject("blob_mismatch")
    unique([p.casefold() for p in entries])
    return entries


def _change_counts(before, after):
    b = before.splitlines(keepends=True); a = after.splitlines(keepends=True)
    adds = removals = 0; removed = []; added = []
    for op, a1, a2, b1, b2 in difflib.SequenceMatcher(None, b, a, autojunk=False).get_opcodes():
        if op in ("replace", "delete"):
            removals += a2-a1; removed.extend(b[a1:a2])
        if op in ("replace", "insert"):
            adds += b2-b1; added.extend(a[b1:b2])
    return adds+removals, removed, added


def validate(approval, request, trees, trusted):
    """Return an offline scope assessment, NEVER a GitHub/merge/repair permission.

    Fail closed without calling out. The four trees are full inventories from a
    trusted source; their root SHAs must be independently verified by the caller.
    """
    if type(trusted) is not TrustedFacts:
        reject("untrusted_provenance")
    _approval(approval); _request(request)
    if (digest(approval) != trusted.approval_digest
            or approval["issue_title_sha256"] != trusted.issue_title_sha256
            or approval["issue_body_sha256"] != trusted.issue_body_sha256
            or approval["reservation_id"] != trusted.reservation_id
            or approval["approval_result_id"] != trusted.approval_result_id
            or approval["protocol_commit"] != trusted.protocol_commit):
        reject("untrusted_provenance")
    shared = ("root_issue", "root_pr", "issue_title_sha256", "issue_body_sha256",
              "reservation_id", "approval_result_id", "protocol_commit", "approved_main_sha")
    if any(request[k] != approval[k] for k in shared):
        reject("approval_mismatch")
    if (request["current_main_sha"] != trusted.current_main_sha or
            request["current_head_sha"] != trusted.current_head_sha or
            request["original_pr_base_sha"] != trusted.original_pr_base_sha or
            request["observed_head_sha"] != request["current_head_sha"] or
            request["observed_head_sha"] != trusted.observed_head_sha or
            request["review_finding_id"] != trusted.finding_id or
            request["reviewer_run_id"] != trusted.reviewer_run_id or
            request["workflow_run_id"] != trusted.workflow_run_id or
            request["workflow_attempt"] != trusted.workflow_attempt or
            request["job_id"] != trusted.job_id or
            request["evidence_sha256"] != trusted.evidence_sha256):
        reject("stale_or_unverified_evidence")
    if (request["acceptance"] != approval["acceptance"] or
            request["required_verifications"] != approval["required_verifications"]):
        reject("acceptance_changed")
    if type(trusted.now_epoch) is not int or request["expires_at"] <= trusted.now_epoch:
        reject("expired_reservation")
    if (type(trusted.previous_attempts) is not int or trusted.previous_attempts < 0
            or request["attempt"] != trusted.previous_attempts+1
            or request["attempt"] > (1 if request["kind"] == "minor" else 2)):
        reject("attempt_limit")
    if type(trusted.prior_changed_lines) is not int or trusted.prior_changed_lines < 0:
        reject("untrusted_provenance")
    caps = (2,80) if request["kind"] == "minor" else (3,600)
    file_cap = min(approval["limits"]["max_files"], request["limits"]["max_files"], caps[0])
    line_cap = min(approval["limits"]["max_lines"], request["limits"]["max_lines"], caps[1])
    if type(trees) is not dict or set(trees) != {"approved", "pr_base", "current", "candidate"}:
        reject("incomplete_tree")
    roots = dict(trusted.tree_roots)
    if set(roots) != set(trees) or len(roots) != len(trusted.tree_roots):
        reject("untrusted_provenance")
    maps = {stage:_tree(data, roots[stage]) for stage,data in trees.items()}
    approved, base, current, candidate = (maps[k] for k in ("approved", "pr_base", "current", "candidate"))
    if set(base) != set(current) or set(base) != set(candidate):
        reject("file_add_delete")
    if any(base[p]["mode"] != current[p]["mode"] or base[p]["mode"] != candidate[p]["mode"] for p in base):
        reject("special_mode_or_rename")
    changed_initial = {p for p in base if base[p]["oid"] != current[p]["oid"]}
    changed_full = {p for p in base if base[p]["oid"] != candidate[p]["oid"]}
    changed_repair = {p for p in base if current[p]["oid"] != candidate[p]["oid"]}
    approved_paths = {p["path"]:p["approved_oid"] for p in approval["paths"]}
    requested_paths = {p["path"]:p for p in request["paths"]}
    if (not changed_repair or changed_repair != set(requested_paths) or
            len(changed_repair) > file_cap or
            not changed_initial.issubset(approved_paths) or
            not changed_full.issubset(approved_paths) or
            not changed_repair.issubset(approved_paths)):
        reject("out_of_scope")
    if request["kind"] == "minor" and not changed_repair.issubset(set(trusted.first_pr_changed_paths)):
        reject("out_of_scope")
    if (len(set(trusted.prior_changed_paths) | changed_repair) > file_cap or
            any(p not in approved for p in approved_paths)):
        reject("out_of_scope")
    for p, oid in approved_paths.items():
        if approved[p]["oid"] != oid or approved[p]["mode"] != "100644":
            reject("approval_blob_mismatch")
    cumulative = trusted.prior_changed_lines
    risky = False
    for p in changed_repair:
        safe_path(p)
        old, new = current[p], candidate[p]
        if (old["mode"] != "100644" or new["mode"] != "100644" or
                old["text"] is None or new["text"] is None):
            reject("invalid_encoding")
        if requested_paths[p]["before_oid"] != old["oid"] or requested_paths[p]["after_oid"] != new["oid"]:
            reject("before_after_mismatch")
        if request["kind"] == "substantive" and p not in changed_initial and current[p]["oid"] != approved_paths[p]:
            reject("approval_blob_mismatch")
        delta, removed, added = _change_counts(old["text"], new["text"])
        cumulative += delta
        if any("IsComplete" in line and re.search(r"\btrue\b", line, re.I)
               for line in added):
            reject("prohibited_semantic_change")
        if any(RISK.search(line) for line in removed):
            risky = True
    if cumulative > line_cap:
        reject("line_limit")
    # A string created from a request is not an independent code review.
    # A narrow independently verified attestation is required for risky edits.
    delta_proof = digest({p:(current[p]["oid"], candidate[p]["oid"]) for p in sorted(changed_repair)})
    if risky:
        if (trusted.reviewer_semantic_delta_sha256 != delta_proof or
                set(trusted.reviewer_invariants) != INVARIANTS):
            reject("hold_semantic_review")
    return {"status":"scope_valid_for_independent_review", "may_repair":False,
            "may_merge":False, "requires_independent_review":True,
            "delta_sha256":delta_proof, "changed_files":len(changed_repair),
            "cumulative_changed_lines":cumulative, "semantic_attestation_required":risky}
