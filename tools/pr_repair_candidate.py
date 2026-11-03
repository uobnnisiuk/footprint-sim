#!/usr/bin/env python3
"""R4 offline verification of a detached candidate commit; never executes it.

All Git objects and complete trees MUST be independently fetched by the trusted
adapter, not supplied by the model. This module is not an authorization source.
"""
from __future__ import annotations

import hashlib
import json
import re

SHA = re.compile(r"[0-9a-f]{40}\Z")
HASH = re.compile(r"[0-9a-f]{64}\Z")
RID = re.compile(r"[0-9a-f]{32}\Z")
PATH = re.compile(r"(?:src|tests|docs|scenarios)/(?:[A-Za-z0-9][A-Za-z0-9._-]*/)*[A-Za-z0-9][A-Za-z0-9._-]*\Z")


class CandidateHalt(ValueError):
    """Fixed failure code only: no untrusted filename or log output."""


def halt() -> None:
    raise CandidateHalt("candidate_unverified_or_out_of_scope")


def sha(value: object) -> bool:
    return type(value) is str and SHA.fullmatch(value) is not None


def digest(value: object) -> str:
    return hashlib.sha256(json.dumps(value, sort_keys=True, separators=(",", ":"),
        ensure_ascii=True, allow_nan=False).encode("ascii")).hexdigest()


def _record(obj: object, keys: set[str]) -> dict:
    if type(obj) is not dict or set(obj) != keys:
        halt()
    return obj


def verify_candidate(envelope: dict, commit: dict, before: dict,
                     after: dict, *, expected_head: str, reservation: dict,
                     permitted_paths: tuple[str, ...]) -> dict:
    """Verify a *complete* tree transition and one-parent detached commit.

    The caller must separately verify Git object hashes, tree-root identities,
    exact approved diff, and main-sourced guard in an isolated checkout. An
    envelope's self-declared manifest digest confers no permission.
    """
    _record(envelope, {"reservation_id", "kind", "parent_sha", "commit_sha",
                       "tree_sha", "changed_paths", "changes_sha256"})
    _record(commit, {"sha", "parents", "tree_sha"})
    if (not sha(expected_head) or not sha(envelope["parent_sha"])
            or not sha(envelope["commit_sha"]) or not sha(envelope["tree_sha"])
            or envelope["parent_sha"] != expected_head
            or commit["sha"] != envelope["commit_sha"]
            or commit["tree_sha"] != envelope["tree_sha"]
            or type(commit["parents"]) is not list
            or commit["parents"] != [expected_head]
            or type(reservation) is not dict
            or reservation.get("id") != envelope["reservation_id"]
            or type(envelope["reservation_id"]) is not str
            or RID.fullmatch(envelope["reservation_id"]) is None
            or envelope["kind"] not in ("minor", "substantive")
            or reservation.get("kind") != envelope["kind"]
            or reservation.get("headSha") != expected_head
            or type(permitted_paths) is not tuple
            or type(envelope["changed_paths"]) is not list):
        halt()
    paths = envelope["changed_paths"]
    if not paths or paths != sorted(set(paths)) or tuple(paths) != permitted_paths:
        halt()
    if len(paths) > (2 if envelope["kind"] == "minor" else 3):
        halt()
    if (type(before) is not dict or type(after) is not dict
            or not before or set(before) != set(after)
            or len(before) > 20000 or not set(paths).issubset(before)):
        halt()
    for path in paths:
        if (type(path) is not str or PATH.fullmatch(path) is None
                or any(part in {"bin", "obj", "results", "node_modules"} for part in path.split("/"))
                or path.endswith((".csproj", ".props", ".targets", ".sln", ".slnx"))):
            halt()
    changed = []
    for path in sorted(before):
        a = _record(before[path], {"oid", "mode"})
        b = _record(after[path], {"oid", "mode"})
        if (not sha(a["oid"]) or not sha(b["oid"]) or
                type(a["mode"]) is not str or type(b["mode"]) is not str
                or a["mode"] != b["mode"]):
            halt()
        if a != b:
            if path not in paths or a["mode"] != "100644":
                halt()
            changed.append({"path": path, "before": a["oid"], "after": b["oid"]})
    if [x["path"] for x in changed] != paths:
        halt()
    if not HASH.fullmatch(envelope["changes_sha256"] or ""):
        halt()
    manifest = {"parent_sha": expected_head, "commit_sha": commit["sha"],
                "tree_sha": commit["tree_sha"], "changes": changed}
    if envelope["changes_sha256"] != digest(manifest):
        halt()
    return {"changed_paths": tuple(paths), "changes_sha256": digest(manifest),
            "commit_sha": commit["sha"], "parent_sha": expected_head}
