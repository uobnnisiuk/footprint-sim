#!/usr/bin/env python3
"""GET-only operational evidence adapter; no writer, reservation or model path.

The transport is the R5a pinned-host/no-redirect reader. Its credential must
be supplied by an independently managed caller; this module never discovers,
prints or persists credentials. Injected readers are an offline test seam.
Matching GitHub records to an operator anchor is not scientific acceptance
or an authorization to publish. R4's production-write prohibition is intact.
"""
from __future__ import annotations

import base64
from dataclasses import dataclass
from datetime import datetime
import hashlib
from pathlib import Path
import re
from zoneinfo import ZoneInfo

import pr_repair_candidate as candidate
import pr_repair_evidence as evidence
import pr_repair_remote_audit as remote

_EXTRA = re.compile(
    r"(?:|issues|issues/comments|issues/[1-9][0-9]*(?:/(?:comments|timeline))?"
    r"|issues/comments/[1-9][0-9]*|pulls|pulls/[1-9][0-9]*/commits"
    r"|pulls/[1-9][0-9]*/reviews/[1-9][0-9]*"
    r"|git/(?:commits|trees|blobs)/[0-9a-f]{40}"
    r"|actions/runs/[1-9][0-9]*(?:/attempts/[1-9][0-9]*/jobs)?)\Z")
_REASONS = frozenset({
    "git_object_unverified", "git_inventory_incomplete", "git_blob_unverified",
    "git_bounds_exceeded", "git_path_unverified", "snapshot_changed",
    "ci_metadata_unverified", "evidence_unavailable", "invalid_input",
    "history_unverified", "approval_anchor_required", "approval_unverified",
    "review_anchor_required", "independent_review_unverified",
})
MAX_ENTRIES = 20000
MAX_TREES = 1000
MAX_BLOB = 1_000_000
MAX_BLOB_TOTAL = 8_000_000


class AdapterHold(ValueError):
    """Fixed non-sensitive reason; never include a source value or exception."""


def require(ok, reason):
    if not ok:
        raise AdapterHold(reason)


def oid(kind: str, content: bytes) -> str:
    return hashlib.sha1(kind.encode("ascii") + b" " +
                        str(len(content)).encode("ascii") + b"\0" + content).hexdigest()


class GitHubEvidenceReader(remote.GitHubReader):
    """Extend R5a with a closed set of repository GET routes, never log routes."""

    def _validate_endpoint(self, route, params):
        if type(route) is str and remote._ROUTE.fullmatch(route):
            return super()._validate_endpoint(route, params)
        remote._require(type(route) is str and _EXTRA.fullmatch(route) is not None,
                        "disallowed_endpoint")
        remote._require(params is None or type(params) is dict, "disallowed_endpoint")
        values = params or {}
        paged = (route in ("pulls", "issues") or route.endswith("/comments") or
                 route.endswith(("/jobs", "/commits", "/timeline")))
        allowed = {"per_page", "page"} if paged else set()
        if route in ("pulls", "issues"):
            allowed.add("state")
        remote._require(set(values) <= allowed, "disallowed_endpoint")
        for key, value in values.items():
            remote._require((key == "state" and value in ("open", "closed", "all")) or
                            (key != "state" and type(value) is int and 1 <= value <= 100),
                            "disallowed_endpoint")
        return values


@dataclass(frozen=True)
class Commit:
    sha: str
    tree_sha: str
    parents: tuple[str, ...]


class GitObjects:
    """Authenticated commit metadata, recomputed tree and blob object IDs.

    Commit identity is asserted by GitHub's authenticated Git-commit endpoint;
    the API does not supply every raw signed-commit byte. Do not call this a
    local rehash/signature verification of the commit. Every tree is fetched
    non-recursively and rehashed, so truncated or omitted leaves cannot pass.
    """

    def __init__(self, reader):
        self.reader = reader
        self._commits = {}
        self._trees = {}
        self._blobs = {}
        self._blob_bytes = 0

    def commit(self, sha: str) -> Commit:
        require(remote._sha(sha), "invalid_input")
        if sha not in self._commits:
            obj = self.reader.get("git/commits/" + sha)
            require(type(obj) is dict and obj.get("sha") == sha,
                    "git_object_unverified")
            tree = obj.get("tree")
            parents = obj.get("parents")
            require(type(tree) is dict and remote._sha(tree.get("sha")) and
                    type(parents) is list and len(parents) <= 16 and
                    all(type(p) is dict and remote._sha(p.get("sha")) for p in parents),
                    "git_object_unverified")
            ps = tuple(p["sha"] for p in parents)
            require(len(set(ps)) == len(ps), "git_object_unverified")
            self._commits[sha] = Commit(sha, tree["sha"], ps)
        return self._commits[sha]

    def _tree(self, sha):
        if sha in self._trees:
            return self._trees[sha]
        require(len(self._trees) < MAX_TREES, "git_bounds_exceeded")
        obj = self.reader.get("git/trees/" + sha)
        require(type(obj) is dict and obj.get("sha") == sha and
                obj.get("truncated") is False and type(obj.get("tree")) is list,
                "git_inventory_incomplete")
        rows = obj["tree"]
        require(0 < len(rows) <= MAX_ENTRIES, "git_bounds_exceeded")
        seen = set()
        normalized = []
        kinds = {"040000": "tree", "100644": "blob", "100755": "blob",
                 "120000": "blob", "160000": "commit"}
        for row in rows:
            require(type(row) is dict, "git_inventory_incomplete")
            name, mode, sha1 = row.get("path"), row.get("mode"), row.get("sha")
            require(type(name) is str and 0 < len(name.encode("utf-8")) <= 255 and
                    name.casefold() not in {".", "..", ".git"} and
                    not any(c in name for c in "/\\\0\r\n") and
                    name.casefold() not in seen, "git_path_unverified")
            seen.add(name.casefold())
            require(type(mode) is str and mode in kinds and row.get("type") == kinds[mode]
                    and remote._sha(sha1), "git_object_unverified")
            normalized.append((name, mode, sha1))
        # Git sorts trees as if directory names have a trailing slash.
        ordered = sorted(normalized, key=lambda r: (r[0] +
                         ("/" if r[1] == "040000" else "")).encode("utf-8"))
        raw = b"".join((("40000" if mode == "040000" else mode) + " ").encode("ascii")
                       + name.encode("utf-8") + b"\0" + bytes.fromhex(sha1)
                       for name, mode, sha1 in ordered)
        require(oid("tree", raw) == sha, "git_object_unverified")
        self._trees[sha] = tuple(ordered)
        return self._trees[sha]

    def inventory(self, commit_sha):
        root = self.commit(commit_sha).tree_sha
        leaves = {}
        folded = set()
        walked = 0

        def walk(sha, prefix, ancestors):
            nonlocal walked
            require(sha not in ancestors and len(ancestors) < 32, "git_bounds_exceeded")
            walked += 1
            require(walked <= MAX_TREES, "git_bounds_exceeded")
            for name, mode, child in self._tree(sha):
                path = prefix + name
                require(path.casefold() not in folded and len(path) <= 4096,
                        "git_path_unverified")
                folded.add(path.casefold())
                if mode == "040000":
                    walk(child, path + "/", ancestors + (sha,))
                else:
                    require(len(leaves) < MAX_ENTRIES, "git_bounds_exceeded")
                    leaves[path] = {"oid": child, "mode": mode, "text": None}
        walk(root, "", ())
        require(bool(leaves), "git_inventory_incomplete")
        return {"root_sha": root, "entries": dict(sorted(leaves.items())), "complete": True}

    def blob(self, sha):
        require(remote._sha(sha), "invalid_input")
        if sha not in self._blobs:
            obj = self.reader.get("git/blobs/" + sha)
            require(type(obj) is dict and obj.get("sha") == sha and
                    obj.get("encoding") == "base64" and
                    type(obj.get("size")) is int and 0 <= obj["size"] <= MAX_BLOB and
                    type(obj.get("content")) is str and
                    len(obj["content"]) <= MAX_BLOB * 2, "git_blob_unverified")
            try:
                data = base64.b64decode(obj["content"].replace("\n", ""), validate=True)
            except (ValueError, UnicodeError):
                raise AdapterHold("git_blob_unverified") from None
            require(len(data) == obj["size"] and oid("blob", data) == sha,
                    "git_blob_unverified")
            self._blob_bytes += len(data)
            require(self._blob_bytes <= MAX_BLOB_TOTAL, "git_bounds_exceeded")
            self._blobs[sha] = data
        return self._blobs[sha]

    def contract_trees(self, stages: dict, text_paths: tuple[str, ...]):
        """R2-shaped complete inventories, with text only at explicit safe paths."""
        require(type(stages) is dict and set(stages) ==
                {"approved", "pr_base", "current", "candidate"} and
                type(text_paths) is tuple and len(text_paths) <= 20 and
                all(type(p) is str and candidate.PATH.fullmatch(p) for p in text_paths),
                "invalid_input")
        result = {}
        for stage, sha in stages.items():
            tree = self.inventory(sha)
            for path in text_paths:
                meta = tree["entries"].get(path)
                require(meta is not None and meta["mode"] == "100644",
                        "git_path_unverified")
                raw = self.blob(meta["oid"])
                try:
                    text = raw.decode("utf-8")
                except UnicodeError:
                    raise AdapterHold("git_blob_unverified") from None
                require("\0" not in text, "git_blob_unverified")
                meta["text"] = text
            result[stage] = tree
        return result

    def detached_candidate(self, envelope, receipt, permitted_paths):
        """Feed independently read inventories into the existing R4 verifier."""
        require(type(envelope) is dict and type(receipt) is dict and
                type(permitted_paths) is tuple, "invalid_input")
        new = self.commit(envelope.get("commit_sha"))
        before = self.inventory(receipt.get("headSha"))
        after = self.inventory(new.sha)
        compact = lambda t: {p: {k: m[k] for k in ("oid", "mode")}
                             for p, m in t["entries"].items()}
        return candidate.verify_candidate(
            envelope, {"sha": new.sha, "parents": list(new.parents), "tree_sha": new.tree_sha},
            compact(before), compact(after), expected_head=receipt.get("headSha"),
            reservation=receipt, permitted_paths=permitted_paths)


def read_ci(reader, number, head, branch):
    """Exact PR/head/run/attempt metadata only; no logs or semantic claims."""
    runs = remote._pages(reader, "actions/workflows/ci.yml/runs",
                         "workflow_runs", {"head_sha": head})
    matched = [r for r in runs if remote._run_for_pr(r, number, head, branch)]
    require(bool(matched), "ci_metadata_unverified")
    chosen = max(matched, key=lambda r: r["id"])
    run = reader.get("actions/runs/" + str(chosen["id"]))
    require(type(run) is dict and run.get("id") == chosen["id"] and
            remote._run_for_pr(run, number, head, branch) and
            type(run.get("run_attempt")) is int and run["run_attempt"] > 0,
            "ci_metadata_unverified")
    jobs = remote._pages(reader, "actions/runs/" + str(run["id"]) +
                         "/attempts/" + str(run["run_attempt"]) + "/jobs", "jobs")
    # Reuse R1's independently pinned normal-workflow inventory. A successful
    # subset of steps cannot represent the full workflow.
    require(len(jobs) == 1 and jobs[0].get("name") == "test", "ci_metadata_unverified")
    statuses = []
    for job in jobs:
        require(job.get("run_id") == run["id"] and job.get("head_sha") == head and
                job.get("run_attempt") == run["run_attempt"] and
                type(job.get("steps")) is list and bool(job["steps"]),
                "ci_metadata_unverified")
        for step in job["steps"]:
            require(type(step) is dict, "ci_metadata_unverified")
        names = [step.get("name") for step in job["steps"]]
        require(all(type(name) is str for name in names) and
                len(names) == len(evidence.REQUIRED_STEPS["normal"]) and
                set(names) == evidence.REQUIRED_STEPS["normal"], "ci_metadata_unverified")
        statuses.extend((step.get("status"), step.get("conclusion")) for step in job["steps"])
        statuses.append((job.get("status"), job.get("conclusion")))
    statuses.append((run.get("status"), run.get("conclusion")))
    if all(s == "completed" and c == "success" for s, c in statuses):
        state = "success_metadata_only"
    elif any(s == "completed" and c == "failure" for s, c in statuses):
        state = "failure_metadata_only"
    else:
        state = "unknown_or_pending"
    latest = reader.get("actions/runs/" + str(run["id"]))
    require(type(latest) is dict and latest.get("head_sha") == head and
            latest.get("run_attempt") == run["run_attempt"] and
            latest.get("status") == run.get("status") and
            latest.get("conclusion") == run.get("conclusion"), "snapshot_changed")
    # A *new* run can supersede the observed run without changing it.
    refreshed = remote._pages(reader, "actions/workflows/ci.yml/runs",
                              "workflow_runs", {"head_sha": head})
    matched_again = [r for r in refreshed if remote._run_for_pr(r, number, head, branch)]
    require(bool(matched_again), "snapshot_changed")
    newest = max(matched_again, key=lambda r: r["id"])
    require(newest["id"] == run["id"] and newest.get("run_attempt") == run["run_attempt"],
            "snapshot_changed")
    return {"state": state, "run_id": run["id"], "attempt": run["run_attempt"],
            "jobs": len(jobs), "semantic_acceptance": "unverified"}


def inspect(reader, *, state_root: Path, root_issue: int, pr_number: int,
            approval_anchor=None, approval=None, review_anchor=None, clock=None):
    """Combine local history, authenticated metadata and external anchor checks.

    This intentionally cannot supply R2 TrustedFacts from request fields.
    Missing independently provisioned manifests/reviewer CI classifications,
    live guard/CAS validation and rollout records remain explicit holds.
    """
    import pr_repair_history as history
    import pr_repair_provenance as provenance

    out = {"schema": "footprint-operational-evidence-v1", "state": "HOLD",
           "local_history": "unknown", "github": "unknown", "ci": "unknown",
           "original_approval": "unknown", "review": "unknown",
           "review_state": "unknown", "formal_approval": False,
           "may_publish": False, "may_activate": False, "production_enabled": False}
    holds = {"independent_ci_classification_unverified", "isolated_main_guard_unverified",
             "live_cas_unverified", "saved_configuration_and_shadow_unverified"}
    try:
        now = (clock or (lambda: datetime.now(ZoneInfo("Asia/Tokyo"))))()
        require(type(root_issue) is int and root_issue > 0 and
                type(pr_number) is int and pr_number > 0, "invalid_input")
        local = history.audit_history(state_root, now)
        out["local_history"] = "consistent" if local.local_valid else "hold"
        out["shared_today"] = local.effective_today
        out["shared_hour"] = local.effective_hour
        holds.update(local.holds)
        main = remote._main(reader)
        head, base, author, branch = remote._pr(reader, pr_number)
        prs = remote._pages(reader, "pulls", filters={"state": "open"})
        require([p.get("number") for p in prs] == [pr_number], "snapshot_changed")
        objects = GitObjects(reader)
        objects.inventory(main)
        objects.inventory(head)
        objects.inventory(base)
        out["github"] = "complete_git_inventories_observed"
        out["ci"] = read_ci(reader, pr_number, head, branch)["state"]
        if approval_anchor is None:
            holds.add("approval_anchor_required")
        else:
            require(approval_anchor.root_issue == root_issue and
                    approval_anchor.root_pr == pr_number, "approval_unverified")
            provenance.verify_approval(reader, approval_anchor, approval)
            out["original_approval"] = "records_match_operator_anchor"
        if review_anchor is None:
            holds.add("review_anchor_required")
        else:
            require(review_anchor.root_pr == pr_number and review_anchor.head_sha == head,
                    "independent_review_unverified")
            pr = reader.get("pulls/" + str(pr_number))
            review = provenance.verify_review(reader, review_anchor,
                                               pr_author_id=pr["user"]["id"])
            out["review"] = "record_matches_operator_anchor"
            out["review_state"] = review.state
            out["formal_approval"] = review.formal_approval
            if review.state == "CHANGES_REQUESTED":
                holds.add("changes_requested")
        require(remote._main(reader) == main and remote._pr(reader, pr_number) ==
                (head, base, author, branch), "snapshot_changed")
        again = history.audit_history(state_root, now)
        require(again == local, "snapshot_changed")
    except (AdapterHold, remote.AuditHold) as exc:
        reason = str(exc)
        holds.add(reason if reason in _REASONS | remote._SAFE_REASONS
                  else "evidence_unavailable")
    except provenance.ProvenanceHold:
        holds.add("approval_or_review_unverified")
    except (OSError, ValueError, KeyError, TypeError, AttributeError, RecursionError):
        holds.add("evidence_unavailable")
    out["holds"] = sorted(holds)
    return out
