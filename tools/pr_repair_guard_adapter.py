#!/usr/bin/env python3
"""Verify exact full/repair patches with the reviewed main guard in scratch Git.

No CLI, model, reservation, real ref update, or candidate-code execution exists.
The reader is the pinned GET-only evidence adapter (an injected mock in tests).
Callers must separately approve the source snapshot/content scope. Only changed
approved normal UTF-8 blobs and the reviewed guard are downloaded. Unchanged
blob bytes are neither fetched nor claimed verified: their complete inventory
and tree IDs are independently verified by GitObjects and retained in Git.

GitHub does not expose full raw signed commit bytes. Scratch HEADs are therefore
synthetic commits with the exact verified source trees, NOT the remote commits.
The explicit result is a content/guard proof, not R4 execution authority or a
drop-in fabricated live guard receipt. R4's production gate stays unchanged.
"""
from __future__ import annotations

from dataclasses import dataclass, field
import hashlib
import os
from pathlib import Path
import subprocess
import tempfile

import pr_repair_adapter as adapter
import pr_repair_contract as contract
import pr_repair_remote_audit as remote

GUARD_PATH = ".github/scripts/patch_guard.py"
# Reviewed original main guard. Updating this allowlist itself needs code review;
# a candidate/Issue/caller-provided digest cannot authorize arbitrary Python.
REVIEWED_GUARD_BLOBS = frozenset({"5f5fcd1eff954f166b28147442c3f9be380620e8"})
MAX_PATCH = 400_000
MAX_TEXT = 180_000


class GuardAdapterHold(ValueError):
    """Fixed diagnostic code, never raw Git output or candidate text."""


def _require(ok, code):
    if not ok:
        raise GuardAdapterHold(code)


@dataclass(frozen=True)
class GuardEvidence:
    main_sha: str
    base_sha: str
    head_sha: str
    candidate_sha: str
    tree_roots: tuple[tuple[str, str], ...]
    guard_blob: str
    full_diff_sha256: str
    repair_diff_sha256: str
    full_diff: bytes = field(repr=False)
    repair_diff: bytes = field(repr=False)
    status: str = "isolated_tree_guard_passed"
    synthetic_baselines: bool = True
    unchanged_blob_bytes_verified: bool = False
    candidate_code_executed: bool = False
    may_publish: bool = False
    may_activate: bool = False


def _environment(folder):
    # Do not inherit credentials, user Git config, proxy commands, Python path,
    # shell startup, arbitrary Git variables, or a candidate-controlled PATH.
    return {"PATH": "/usr/bin:/bin", "HOME": str(folder), "LANG": "C.UTF-8",
            "LC_ALL": "C.UTF-8", "TMPDIR": str(folder),
            "XDG_CONFIG_HOME": str(folder), "GIT_CONFIG_NOSYSTEM": "1",
            "GIT_CONFIG_GLOBAL": os.devnull, "GIT_ATTR_NOSYSTEM": "1",
            "GIT_TERMINAL_PROMPT": "0", "GIT_NO_REPLACE_OBJECTS": "1",
            "GIT_AUTHOR_NAME": "Offline guard", "GIT_AUTHOR_EMAIL": "guard@example.invalid",
            "GIT_COMMITTER_NAME": "Offline guard", "GIT_COMMITTER_EMAIL": "guard@example.invalid",
            "GIT_AUTHOR_DATE": "2000-01-01T00:00:00+00:00",
            "GIT_COMMITTER_DATE": "2000-01-01T00:00:00+00:00"}


def _run(command, cwd, env, data=None):
    try:
        result = subprocess.run(command, cwd=cwd, env=env, input=data,
                                capture_output=True, timeout=30, check=False)
    except (OSError, subprocess.SubprocessError):
        raise GuardAdapterHold("isolated_process_failed") from None
    _require(result.returncode == 0, "isolated_process_failed")
    return result.stdout


def _git(folder, env, *args, data=None):
    return _run(["/usr/bin/git", "-c", "core.hooksPath=/dev/null", "-c",
                 "core.fsmonitor=false", "-c", "core.attributesFile=/dev/null",
                 "-c", "protocol.allow=never", *args], folder, env, data)


def _tree_objects(objects, sha, folder, env, imported):
    if sha in imported:
        return
    rows = objects._tree(sha)  # Reuses the complete, independently rehashed inventory.
    for _, mode, child in rows:
        if mode == "040000":
            _tree_objects(objects, child, folder, env, imported)
    raw = b"".join((("40000" if mode == "040000" else mode) + " ").encode("ascii")
                   + name.encode("utf-8") + b"\0" + bytes.fromhex(child)
                   for name, mode, child in rows)
    actual = _git(folder, env, "hash-object", "-t", "tree", "-w", "--stdin", data=raw)
    _require(actual.strip().decode("ascii") == sha, "scratch_tree_mismatch")
    imported.add(sha)


def _patch(objects, before, after, contents, active, script, temp):
    folder = temp / "repo"
    folder.mkdir(mode=0o700)
    env = _environment(temp)
    _git(folder, env, "init", "--template=", ".")
    imported = set()
    for tree in (before, after):
        _tree_objects(objects, tree["root_sha"], folder, env, imported)
    for sha, data in contents.items():
        actual = _git(folder, env, "hash-object", "-w", "--stdin", data=data)
        _require(actual.strip().decode("ascii") == sha, "scratch_blob_mismatch")
    _git(folder, env, "read-tree", before["root_sha"])
    # Keep every unchanged entry in the index/tree without downloading its body.
    inactive = sorted(set(before["entries"]) - set(active))
    if inactive:
        _git(folder, env, "update-index", "--skip-worktree", "-z", "--stdin",
             data=b"".join(p.encode("utf-8") + b"\0" for p in inactive))
    for path in active:
        target = folder / path
        target.parent.mkdir(parents=True, exist_ok=True, mode=0o700)
        target.write_bytes(contents[before["entries"][path]["oid"]])
        target.chmod(0o644)
    synthetic = _git(folder, env, "commit-tree", before["root_sha"],
                     data=b"Synthetic scratch baseline; not a remote commit.\n").strip().decode("ascii")
    _git(folder, env, "update-ref", "HEAD", synthetic)
    _require(not _git(folder, env, "status", "--porcelain", "--untracked-files=all"),
             "scratch_checkout_not_clean")
    patch = _git(folder, env, "diff", "--no-ext-diff", "--no-textconv", "--no-renames",
                 "--full-index", "--src-prefix=a/", "--dst-prefix=b/",
                 before["root_sha"], after["root_sha"], "--")
    _require(0 < len(patch) <= MAX_PATCH, "guard_patch_bounds")
    patch_file = temp / "changes.patch"
    patch_file.write_bytes(patch)
    guard_file = temp / "verified_main_guard.py"
    guard_file.write_bytes(script)
    _run(["/usr/bin/python3", "-I", "-B", str(guard_file), "import", str(patch_file),
          "--base", synthetic, "--diagnostic-json"], folder, env)
    actual = _git(folder, env, "write-tree", "--missing-ok").strip().decode("ascii")
    _require(actual == after["root_sha"], "guard_result_tree_mismatch")
    for path in active:
        raw = (folder / path).read_bytes()
        _require(adapter.oid("blob", raw) == after["entries"][path]["oid"],
                 "guard_result_blob_mismatch")
    return patch


def validate_detached_guard(reader, *, main_sha: str, base_sha: str, head_sha: str,
                            candidate_sha: str, permitted_paths: tuple[str, ...],
                            approved_paths: tuple[str, ...] | None = None) -> GuardEvidence:
    """Read exact objects, then test the two patches only inside private temp Git.

    Only an independently approved invocation may use real remote sources. This
    release's regression tests use synthetic reader data. Permission to inspect
    GitHub metadata is not permission to read arbitrary repository blob bodies.
    """
    _require(os.name == "posix" and Path("/usr/bin/git").is_file()
             and Path("/usr/bin/python3").is_file(), "isolated_runtime_unavailable")
    _require(all(remote._sha(s) for s in (main_sha, base_sha, head_sha, candidate_sha)),
             "guard_input_unverified")
    if approved_paths is None:
        approved_paths = permitted_paths
    for paths, limit in ((permitted_paths, 3), (approved_paths, 30)):
        _require(type(paths) is tuple and 0 < len(paths) <= limit
                 and all(type(p) is str for p in paths) and len(paths) == len(set(paths)),
                 "guard_scope_unverified")
        try:
            for path in paths:
                contract.safe_path(path)
        except (ValueError, TypeError):
            raise GuardAdapterHold("guard_scope_unverified") from None
    _require(set(permitted_paths) <= set(approved_paths), "guard_scope_unverified")
    try:
        _require(remote._main(reader) == main_sha, "guard_main_changed")
        objects = adapter.GitObjects(reader)
        main = objects.inventory(main_sha)
        guard = main["entries"].get(GUARD_PATH)
        _require(guard is not None and guard["mode"] == "100644"
                 and guard["oid"] in REVIEWED_GUARD_BLOBS, "main_guard_not_reviewed")
        script = objects.blob(guard["oid"])
        trees = {stage: objects.inventory(sha) for stage, sha in
                 (("base", base_sha), ("head", head_sha), ("candidate", candidate_sha))}
        _require(objects.commit(candidate_sha).parents == (head_sha,),
                 "guard_candidate_parent_unverified")
        base, head, final = (trees[x] for x in ("base", "head", "candidate"))
        names = set(base["entries"])
        _require(names == set(head["entries"]) == set(final["entries"]),
                 "guard_inventory_changed")
        for path in names:
            _require(base["entries"][path]["mode"] == head["entries"][path]["mode"]
                     == final["entries"][path]["mode"], "guard_mode_changed")
        full = {p for p in names if base["entries"][p]["oid"] != final["entries"][p]["oid"]}
        repair = {p for p in names if head["entries"][p]["oid"] != final["entries"][p]["oid"]}
        initial = {p for p in names if base["entries"][p]["oid"] != head["entries"][p]["oid"]}
        _require(bool(full) and bool(repair) and (full | initial) <= set(approved_paths)
                 and repair <= set(permitted_paths), "guard_scope_unverified")
        active = tuple(sorted(full | repair | initial))
        contents = {}
        for tree in trees.values():
            for path in active:
                meta = tree["entries"][path]
                _require(meta["mode"] == "100644", "guard_non_normal_file")
                raw = objects.blob(meta["oid"])
                _require(len(raw) <= MAX_TEXT and b"\0" not in raw and b"\r" not in raw,
                         "guard_text_unverified")
                try:
                    raw.decode("utf-8", "strict")
                except UnicodeError:
                    raise GuardAdapterHold("guard_text_unverified") from None
                contents[meta["oid"]] = raw
        with tempfile.TemporaryDirectory(prefix="footprint-main-guard-") as scratch:
            root = Path(scratch)
            for name in ("full", "repair"):
                (root / name).mkdir(mode=0o700)
            full_diff = _patch(objects, base, final, contents, active, script, root / "full")
            repair_diff = _patch(objects, head, final, contents, active, script, root / "repair")
        _require(remote._main(reader) == main_sha, "guard_main_changed")
    except GuardAdapterHold:
        raise
    except Exception:
        # A reader/test seam must not leak an arbitrary API body or exception.
        raise GuardAdapterHold("guard_source_unverified") from None
    return GuardEvidence(main_sha, base_sha, head_sha, candidate_sha,
             tuple((stage, trees[stage]["root_sha"]) for stage in ("base", "head", "candidate")),
             guard["oid"], hashlib.sha256(full_diff).hexdigest(),
             hashlib.sha256(repair_diff).hexdigest(), full_diff, repair_diff)
