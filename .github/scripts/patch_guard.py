#!/usr/bin/env python3
"""Validate a read-only agent's text patch before touching a fresh checkout.

Only the import operation is supported: the agent must never edit this guard or
the checkout used for validation/publication. No candidate code is executed here.
"""
import argparse
import json
import os
from pathlib import Path
import re
import subprocess
import sys
import tempfile

MAX_FILES = 30
MAX_PATCH_BYTES = 400_000
MAX_FILE_BYTES = 180_000
ALLOWED_PATH = re.compile(
    r"(?:src|tests|docs|scenarios)/(?:[A-Za-z0-9][A-Za-z0-9._-]*/)*"
    r"[A-Za-z0-9][A-Za-z0-9._-]*\Z"
)
PROTECTED_NAMES = {
    "agents.md", "global.json", "nuget.config", "packages.config",
    "packages.lock.json", "project.json", "project.assets.json",
    "package.json", "package-lock.json", "npm-shrinkwrap.json", "yarn.lock",
    "pnpm-lock.yaml", "dotnet-tools.json", "dotnettoolsettings.xml", "sdk.json",
}
PROTECTED_SUFFIXES = {
    ".csproj", ".fsproj", ".vbproj", ".vcxproj", ".esproj", ".proj",
    ".props", ".targets", ".sln", ".slnx", ".slnf", ".rsp",
}
GENERATED_DIRS = {"bin", "obj", "node_modules", "results"}
FAILURE_REASONS = {
    "invalid_patch", "malformed_diff", "malformed_hunk", "disallowed_path",
    "disallowed_file", "size_limit", "baseline_mismatch", "dirty_checkout",
    "patch_not_applicable", "git_failure", "invalid_encoding", "io_error",
    "internal_error",
}


class GuardRejection(ValueError):
    """Keep detailed local exceptions separate from fixed public reason codes."""

    def __init__(self, reason: str, message: str) -> None:
        super().__init__(message)
        self.reason = reason


def failure_reason(error: Exception) -> str:
    if (isinstance(error, GuardRejection) and isinstance(error.reason, str)
            and error.reason in FAILURE_REASONS):
        return error.reason
    if isinstance(error, UnicodeError):
        return "invalid_encoding"
    if isinstance(error, OSError):
        return "io_error"
    return "internal_error"


def git(*args: str, env: dict | None = None) -> bytes:
    clean_env = {k: v for k, v in os.environ.items() if not k.startswith("GIT_")}
    clean_env.update({"GIT_CONFIG_NOSYSTEM": "1", "GIT_CONFIG_GLOBAL": os.devnull})
    if env:
        clean_env.update(env)
    try:
        result = subprocess.run(
            ["git", "-c", f"core.hooksPath={os.devnull}", "-c", "core.fsmonitor=false",
             "-c", f"core.attributesFile={os.devnull}", *args],
            env=clean_env, check=True, capture_output=True,
        )
    except subprocess.CalledProcessError as error:
        reason = "patch_not_applicable" if args and args[0] == "apply" else "git_failure"
        raise GuardRejection(reason, "Git command failed during patch validation") from error
    return result.stdout


def validate_path(path: str) -> None:
    if path != "README.md" and not ALLOWED_PATH.fullmatch(path):
        raise GuardRejection("disallowed_path", f"Path is outside approved scope: {path!r}")
    parts = path.split("/")
    if (any(p.casefold() in PROTECTED_NAMES for p in parts)
            or any(p.casefold() in GENERATED_DIRS for p in parts[:-1])
            or Path(path).suffix.casefold() in PROTECTED_SUFFIXES):
        raise GuardRejection("disallowed_path", f"Protected configuration or generated path: {path!r}")
    target = Path(path)
    for item in [target, *target.parents]:
        if item.is_symlink():
            raise GuardRejection("disallowed_path", f"Symlink path or ancestor refused: {path!r}")


def parse_patch(data: bytes) -> list[str]:
    """Accept only ordinary, same-path, UTF-8 unified text diffs.

    Git accepts some trailing garbage and extended headers. Parsing the small
    supported grammar first prevents silently ignoring unsupported instructions.
    Git remains responsible for checking hunk positions and original contents.
    """
    if len(data) > MAX_PATCH_BYTES:
        raise GuardRejection("size_limit", "Patch is oversized")
    if not data or b"\0" in data:
        raise GuardRejection("invalid_patch", "Patch is empty or contains NUL bytes")
    text = data.decode("utf-8", errors="strict")
    if not text.endswith("\n"):
        raise GuardRejection("malformed_diff", "Patch must end with a newline")
    lines = text[:-1].split("\n")
    paths = []
    i = 0
    while i < len(lines):
        header = re.fullmatch(r"diff --git a/(\S+) b/(\S+)", lines[i])
        if not header or header[1] != header[2]:
            raise GuardRejection("malformed_diff", "Expected a same-path git diff; renames/copies are forbidden")
        path = header[1]
        validate_path(path)
        if path in paths:
            raise GuardRejection("malformed_diff", "Duplicate paths in patch")
        if len(paths) >= MAX_FILES:
            raise GuardRejection("size_limit", "Too many files in patch")
        paths.append(path)
        i += 1
        kind = "modify"
        if i < len(lines) and lines[i] in {"new file mode 100644", "deleted file mode 100644"}:
            kind = "add" if lines[i].startswith("new") else "delete"
            i += 1
        if i < len(lines) and lines[i].startswith("index "):
            if not re.fullmatch(r"index [0-9a-f]{7,64}\.\.[0-9a-f]{7,64}(?: 100644)?", lines[i]):
                raise GuardRejection("malformed_diff", "Invalid index header or special Git mode")
            i += 1
        if i == len(lines) or lines[i].startswith("diff --git "):
            if kind == "modify":
                raise GuardRejection("malformed_diff", "Empty modification")
            continue  # Empty file addition/deletion; Git verifies the contents.
        old = "/dev/null" if kind == "add" else "a/" + path
        new = "/dev/null" if kind == "delete" else "b/" + path
        if lines[i:i + 2] != ["--- " + old, "+++ " + new]:
            raise GuardRejection("malformed_diff", "Unexpected diff headers, modes, or binary patch")
        i += 2
        hunks = 0
        while i < len(lines) and not lines[i].startswith("diff --git "):
            hunk = re.fullmatch(r"@@ -\d+(?:,(\d+))? \+\d+(?:,(\d+))? @@(?: .*)?", lines[i])
            if not hunk:
                raise GuardRejection("malformed_hunk", "Malformed hunk or trailing patch data")
            old_left = int(hunk[1]) if hunk[1] is not None else 1
            new_left = int(hunk[2]) if hunk[2] is not None else 1
            i += 1
            hunks += 1
            while old_left or new_left:
                if i >= len(lines) or not lines[i] or lines[i][0] not in " +-":
                    raise GuardRejection("malformed_hunk", "Truncated or malformed hunk")
                prefix = lines[i][0]
                old_left -= prefix in " -"
                new_left -= prefix in " +"
                if old_left < 0 or new_left < 0:
                    raise GuardRejection("malformed_hunk", "Hunk line counts do not match")
                i += 1
                if i < len(lines) and lines[i] == r"\ No newline at end of file":
                    i += 1
            if i < len(lines) and lines[i] == r"\ No newline at end of file":
                raise GuardRejection("malformed_hunk", "Misplaced newline marker")
        if not hunks:
            raise GuardRejection("malformed_hunk", "Missing text hunks")
    return paths


def import_patch(source: Path, base: str) -> None:
    if not re.fullmatch(r"[0-9a-f]{40}", base):
        raise GuardRejection("baseline_mismatch", "Expected a full trusted baseline commit SHA")
    if source.is_symlink() or not source.is_file():
        raise GuardRejection("invalid_patch", "Patch is missing or symlinked")
    if source.stat().st_size > MAX_PATCH_BYTES:
        raise GuardRejection("size_limit", "Patch is oversized")
    data = source.read_bytes()
    # Codex's output-last-message is a text response and may omit its final LF.
    # Normalize only that transport detail; all diff/hunk syntax stays strict.
    if data and not data.endswith(b"\n"):
        data += b"\n"
    paths = parse_patch(data)
    root = Path(git("rev-parse", "--show-toplevel").decode().strip())
    if root.resolve() != Path.cwd().resolve():
        raise GuardRejection("baseline_mismatch", "Run the guard from the repository root")
    if git("rev-parse", "HEAD").decode().strip() != base:
        raise GuardRejection("baseline_mismatch", "Checkout HEAD differs from the trusted baseline")
    if git("status", "--porcelain=v1", "--untracked-files=all"):
        raise GuardRejection("dirty_checkout", "The checkout and index must be clean; keep artifacts outside it")
    objects = str(Path(git("rev-parse", "--git-path", "objects").decode().strip()).resolve())
    with tempfile.TemporaryDirectory(prefix="autodev-guard-") as temp:
        temporary = Path(temp)
        git_dir = temporary / "repo.git"
        patch = temporary / "changes.patch"
        patch.write_bytes(data)
        git("init", "--bare", "--template=", str(git_dir))
        isolated = {"GIT_DIR": str(git_dir), "GIT_ALTERNATE_OBJECT_DIRECTORIES": objects}
        git("read-tree", base, env=isolated)
        git("apply", "--cached", "--check", str(patch), env=isolated)
        git("apply", "--cached", str(patch), env=isolated)
        entries = git("diff", "--cached", "--raw", "--no-abbrev", "--no-renames", "-z", base,
                      env=isolated).split(b"\0")
        changed = []
        for metadata, raw_path in zip(entries[0:-1:2], entries[1:-1:2]):
            old_mode, new_mode, old_oid, new_oid, status = metadata.split()
            path = raw_path.decode("utf-8", errors="strict")
            validate_path(path)
            if (old_mode not in {b":000000", b":100644"}
                    or new_mode not in {b"000000", b"100644"}
                    or status not in {b"A", b"M", b"D"}):
                raise GuardRejection("disallowed_file", "Special file modes, renames, and copies are forbidden")
            for mode, oid in [(old_mode[1:], old_oid), (new_mode, new_oid)]:
                if mode == b"000000":
                    continue
                size = int(git("cat-file", "-s", oid.decode(), env=isolated))
                if size > MAX_FILE_BYTES:
                    raise GuardRejection("size_limit", f"File exceeds the size limit: {path}")
                blob = git("cat-file", "blob", oid.decode(), env=isolated)
                if b"\0" in blob:
                    raise GuardRejection("disallowed_file", f"Binary file refused: {path}")
                blob.decode("utf-8", errors="strict")
            changed.append(path)
        if set(changed) != set(paths):
            raise GuardRejection("invalid_patch", "Applied changes do not exactly match the patch paths")
        # Only now may the real index and worktree change. --index also verifies
        # that original worktree contents still match the trusted index.
        git("apply", "--index", "--check", str(patch))
        git("apply", "--index", str(patch))
    print(f"Applied {len(paths)} approved files from baseline {base}")


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("operation", choices=["import"])
    parser.add_argument("patch", type=Path)
    parser.add_argument("--base", required=True, help="Trusted full checkout commit SHA")
    parser.add_argument("--diagnostic-json", action="store_true",
                        help="On failure emit only a fixed reason code; omit raw exception text")
    args = parser.parse_args()
    if not args.diagnostic_json:
        import_patch(args.patch, args.base)
        return
    try:
        import_patch(args.patch, args.base)
    except Exception as error:
        # Paths, patch text, Git output, and exception messages may contain secrets.
        # This interface deliberately carries only a version and allowlisted code.
        print(json.dumps({"schema_version": 1, "command": "patch_guard",
                          "reason": failure_reason(error)}), file=sys.stderr)
        raise SystemExit(1) from None


if __name__ == "__main__":
    main()
