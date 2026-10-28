#!/usr/bin/env python3
"""Convert bounded literal replacements to a trusted Git diff without checkout writes.

Answers up to MAX_RESPONSE_HASH_BYTES are fingerprinted even when too large to
parse. Larger regular files are rejected before reading; their size is recorded
but input_sha256 remains null. Growth beyond the read cap leaves both unknown.
"""
from __future__ import annotations

import argparse
import hashlib
import importlib.util
import json
import os
from pathlib import Path
import re
import stat
import subprocess
import sys
import tempfile

# Loading the sibling policy must never dirty the source checkout.
sys.dont_write_bytecode = True
guard = None
try:
    _spec = importlib.util.spec_from_file_location(
        "structured_edits_patch_guard", Path(__file__).with_name("patch_guard.py"))
    if _spec and _spec.loader:
        guard = importlib.util.module_from_spec(_spec)
        _spec.loader.exec_module(guard)
except Exception:
    # An incomplete installation must not leak an import traceback or local path.
    guard = None

MAX_RESPONSE_BYTES = 2_000_000
# Preserve complete fingerprints for ordinary oversized answers, but cap hashing I/O.
MAX_RESPONSE_HASH_BYTES = 8_000_000
MAX_EDITS = 100
STAGES = frozenset({"response_validation", "edit_resolution", "diff_generation"})
FAILURE_REASONS = frozenset({
    "invalid_json", "invalid_schema", "invalid_encoding", "size_limit",
    "disallowed_path", "disallowed_file", "baseline_mismatch", "dirty_checkout",
    "target_missing", "match_not_found", "ambiguous_match", "overlapping_edits",
    "no_effect", "git_failure", "io_error", "output_exists", "output_invalid",
    "invalid_patch", "internal_error",
})


class Context:
    def __init__(self) -> None:
        self.stage = "response_validation"
        self.input_sha256 = None
        self.input_bytes = None
        self.operation = None
        self.match_count = None

    def diagnostic(self, reason: str) -> dict:
        return {"schema_version": 1, "command": "structured_edits",
                "stage": self.stage, "reason": reason,
                "input_sha256": self.input_sha256, "input_bytes": self.input_bytes,
                "operation": self.operation, "match_count": self.match_count}


class ConversionError(ValueError):
    def __init__(self, reason: str, context: Context) -> None:
        super().__init__(reason)
        self.diagnostic = context.diagnostic(reason)


def reject(context: Context, reason: str) -> None:
    raise ConversionError(reason, context)


def no_symlinks(path: Path) -> bool:
    return not any(part.is_symlink() for part in (path, *path.parents))


def read_response(source: Path, context: Context) -> bytes:
    source = source.absolute()
    if not no_symlinks(source):
        reject(context, "io_error")
    descriptor = os.open(source, os.O_RDONLY | getattr(os, "O_NOFOLLOW", 0)
                         | getattr(os, "O_NONBLOCK", 0))
    digest = hashlib.sha256()
    total = 0
    chunks = []
    with os.fdopen(descriptor, "rb") as stream:
        info = os.fstat(stream.fileno())
        if not stat.S_ISREG(info.st_mode):
            reject(context, "io_error")
        if info.st_size > MAX_RESPONSE_HASH_BYTES:
            context.input_bytes = info.st_size
            reject(context, "size_limit")
        while chunk := stream.read(min(65536, MAX_RESPONSE_HASH_BYTES + 1 - total)):
            total += len(chunk)
            if total > MAX_RESPONSE_HASH_BYTES:
                # The input grew after fstat. Neither its final size nor full hash is known.
                reject(context, "size_limit")
            digest.update(chunk)
            if total <= MAX_RESPONSE_BYTES:
                chunks.append(chunk)
    context.input_bytes = total
    context.input_sha256 = digest.hexdigest()
    if total > MAX_RESPONSE_BYTES:
        reject(context, "size_limit")
    return b"".join(chunks)


def parse_response(data: bytes, context: Context) -> dict:
    def unique_fields(pairs: list[tuple]) -> dict:
        result = {}
        for key, value in pairs:
            if key in result:
                raise ValueError("duplicate field")
            result[key] = value
        return result

    def invalid_number(_value: str):
        raise ValueError("invalid number")

    try:
        text = data.decode("utf-8", errors="strict")
    except UnicodeError:
        reject(context, "invalid_encoding")
    try:
        result = json.loads(text, object_pairs_hook=unique_fields,
                            parse_constant=invalid_number, parse_float=invalid_number)
    except (ValueError, RecursionError):
        reject(context, "invalid_json")
    if (not isinstance(result, dict) or set(result) != {"version", "status", "edits"}
            or type(result["version"]) is not int or result["version"] != 1
            or result["status"] not in ("changes", "no_changes")
            or not isinstance(result["edits"], list)):
        reject(context, "invalid_schema")
    edits = result["edits"]
    if len(edits) > MAX_EDITS:
        reject(context, "size_limit")
    if (result["status"] == "changes") != bool(edits):
        reject(context, "invalid_schema")
    for index, edit in enumerate(edits, 1):
        context.operation = index
        if (not isinstance(edit, dict) or set(edit) != {"path", "before", "after"}
                or any(not isinstance(edit[key], str) for key in edit)):
            reject(context, "invalid_schema")
        for value in edit.values():
            if "\0" in value:
                reject(context, "invalid_schema")
            try:
                value.encode("utf-8", errors="strict")
            except UnicodeError:
                reject(context, "invalid_encoding")
        if not edit["before"]:
            reject(context, "invalid_schema")
        if max(len(edit[key].encode("utf-8")) for key in ("before", "after")) > guard.MAX_FILE_BYTES:
            reject(context, "size_limit")
    context.operation = None
    return result


def git(context: Context, *args: str, cwd: Path | None = None,
        env: dict | None = None, input_bytes: bytes | None = None) -> bytes:
    environment = {key: value for key, value in os.environ.items()
                   if not key.startswith("GIT_")}
    environment.update({"GIT_CONFIG_NOSYSTEM": "1", "GIT_CONFIG_GLOBAL": os.devnull,
                        "GIT_ATTR_NOSYSTEM": "1", "GIT_OPTIONAL_LOCKS": "0",
                        "GIT_NO_REPLACE_OBJECTS": "1", "GIT_NO_LAZY_FETCH": "1"})
    if env:
        environment.update(env)
    try:
        result = subprocess.run(
            ["git", "-c", f"core.hooksPath={os.devnull}", "-c", "core.fsmonitor=false",
             "-c", f"core.attributesFile={os.devnull}", "-c", "core.autocrlf=false",
             "-c", "core.quotePath=true", "-c", "color.ui=false", *args],
            cwd=cwd, env=environment, input=input_bytes, capture_output=True,
            check=True, timeout=30)
    except (subprocess.CalledProcessError, subprocess.TimeoutExpired):
        reject(context, "git_failure")
    return result.stdout


def file_oid(path: Path) -> tuple[str, int]:
    """Hash raw bytes without Git attributes, filters, index refresh, or symlink traversal."""
    if not no_symlinks(path):
        raise OSError("symlink")
    descriptor = os.open(path, os.O_RDONLY | getattr(os, "O_NOFOLLOW", 0)
                         | getattr(os, "O_NONBLOCK", 0))
    with os.fdopen(descriptor, "rb") as stream:
        info = os.fstat(stream.fileno())
        if not stat.S_ISREG(info.st_mode):
            raise OSError("not regular")
        digest = hashlib.sha1(f"blob {info.st_size}\0".encode("ascii"))
        total = 0
        while chunk := stream.read(65536):
            digest.update(chunk)
            total += len(chunk)
        if total != info.st_size:
            raise OSError("file changed")
        return digest.hexdigest(), info.st_mode


def verify_checkout(base: str, context: Context) -> tuple[Path, dict, Path]:
    if not isinstance(base, str) or not re.fullmatch(r"[0-9a-f]{40}", base):
        reject(context, "baseline_mismatch")
    root = Path(os.fsdecode(git(context, "rev-parse", "--show-toplevel")).strip())
    if root.resolve() != Path.cwd().resolve():
        reject(context, "baseline_mismatch")
    if git(context, "rev-parse", "HEAD").decode().strip() != base:
        reject(context, "baseline_mismatch")
    tree = {}
    for entry in git(context, "ls-tree", "-r", "-z", base).split(b"\0"):
        if not entry:
            continue
        metadata, raw_path = entry.split(b"\t", 1)
        mode, kind, oid = metadata.decode("ascii").split()
        tree[os.fsdecode(raw_path)] = (mode, kind, oid)
    index = {}
    for entry in git(context, "ls-files", "--stage", "-z").split(b"\0"):
        if not entry:
            continue
        metadata, raw_path = entry.split(b"\t", 1)
        mode, oid, stage = metadata.decode("ascii").split()
        if stage != "0":
            reject(context, "dirty_checkout")
        index[os.fsdecode(raw_path)] = (mode, oid)
    if index != {path: (value[0], value[2]) for path, value in tree.items()}:
        reject(context, "dirty_checkout")
    # Unlike status/diff, ls-files and raw hashing cannot execute clean filters.
    if git(context, "ls-files", "--others", "--exclude-standard", "-z"):
        reject(context, "dirty_checkout")
    for name, (mode, kind, oid) in tree.items():
        path = root / name
        try:
            if mode == "120000" and kind == "blob" and path.is_symlink():
                data = os.fsencode(os.readlink(path))
                actual = hashlib.sha1(f"blob {len(data)}\0".encode("ascii") + data).hexdigest()
                if actual != oid:
                    reject(context, "dirty_checkout")
            elif mode in {"100644", "100755"} and kind == "blob":
                actual, file_mode = file_oid(path)
                if actual != oid or bool(file_mode & 0o111) != (mode == "100755"):
                    reject(context, "dirty_checkout")
            elif mode == "160000":
                # Submodules require separate checkout validation and are unsupported here.
                reject(context, "disallowed_file")
            else:
                reject(context, "dirty_checkout")
        except OSError:
            reject(context, "dirty_checkout")
    objects = Path(os.fsdecode(git(context, "rev-parse", "--git-path", "objects")).strip()).resolve()
    return root, tree, objects


def resolve_edits(edits: list[dict], tree: dict, context: Context) -> dict[str, bytes]:
    originals = {}
    replacements = {}
    for number, edit in enumerate(edits, 1):
        context.operation = number
        context.match_count = None
        path = edit["path"]
        try:
            guard.validate_path(path)
        except guard.GuardRejection:
            reject(context, "disallowed_path")
        if path not in tree:
            reject(context, "target_missing")
        mode, kind, oid = tree[path]
        if mode != "100644" or kind != "blob":
            reject(context, "disallowed_file")
        if path not in originals:
            if len(originals) >= guard.MAX_FILES:
                reject(context, "size_limit")
            size = int(git(context, "cat-file", "-s", oid))
            if size > guard.MAX_FILE_BYTES:
                reject(context, "size_limit")
            data = git(context, "cat-file", "blob", oid)
            if b"\0" in data:
                reject(context, "disallowed_file")
            try:
                data.decode("utf-8", errors="strict")
            except UnicodeError:
                reject(context, "invalid_encoding")
            originals[path] = data
            replacements[path] = []
        before = edit["before"].encode("utf-8")
        after = edit["after"].encode("utf-8")
        if before == after:
            reject(context, "no_effect")
        matches = []
        start = 0
        while (found := originals[path].find(before, start)) != -1:
            matches.append(found)
            start = found + 1
        context.match_count = len(matches)
        if not matches:
            reject(context, "match_not_found")
        if len(matches) != 1:
            reject(context, "ambiguous_match")
        start = matches[0]
        end = start + len(before)
        if any(start < old_end and old_start < end
               for old_start, old_end, _ in replacements[path]):
            reject(context, "overlapping_edits")
        replacements[path].append((start, end, after))
    context.operation = None
    context.match_count = None
    modified = {}
    for path in sorted(originals):
        data = originals[path]
        for start, end, after in sorted(replacements[path], reverse=True):
            data = data[:start] + after + data[end:]
        if len(data) > guard.MAX_FILE_BYTES:
            reject(context, "size_limit")
        if data == originals[path]:
            reject(context, "no_effect")
        modified[path] = data
    return modified


def generate_diff(base: str, objects: Path, modified: dict[str, bytes], context: Context) -> bytes:
    with tempfile.TemporaryDirectory(prefix="autodev-structured-edits-") as directory:
        temporary = Path(directory)
        repository = temporary / "repo.git"
        git(context, "init", "--bare", "--template=", str(repository), cwd=temporary)
        (repository / "info").mkdir(exist_ok=True)
        (repository / "info/attributes").write_text(
            "* diff -text -filter -ident -working-tree-encoding\n", encoding="utf-8")
        isolated = {"GIT_DIR": str(repository), "GIT_ALTERNATE_OBJECT_DIRECTORIES": str(objects)}
        git(context, "read-tree", base, cwd=temporary, env=isolated)
        for path, contents in sorted(modified.items()):
            oid = git(context, "hash-object", "-w", "--stdin", "--no-filters", cwd=temporary,
                      env=isolated, input_bytes=contents).decode("ascii").strip()
            git(context, "update-index", "--cacheinfo", f"100644,{oid},{path}",
                cwd=temporary, env=isolated)
        data = git(context, "diff", "--cached", "--no-ext-diff", "--no-textconv", "--no-renames",
                   "--text", "--full-index", "--src-prefix=a/", "--dst-prefix=b/", "--no-color",
                   "--unified=3", "--diff-algorithm=myers", "--no-indent-heuristic", base, "--",
                   *sorted(modified), cwd=temporary, env=isolated)
        if len(data) > guard.MAX_PATCH_BYTES:
            reject(context, "size_limit")
        try:
            paths = guard.parse_patch(data)
        except (guard.GuardRejection, UnicodeError):
            reject(context, "invalid_patch")
        if set(paths) != set(modified):
            reject(context, "invalid_patch")
        return data


def validate_output(output: Path, root: Path, context: Context) -> Path:
    output = output.absolute()
    if not no_symlinks(output.parent):
        reject(context, "output_invalid")
    if output.exists() or output.is_symlink():
        reject(context, "output_exists")
    if not output.parent.is_dir():
        reject(context, "output_invalid")
    # Resolve '..' aliases only after refusing symlink traversal.
    output = output.parent.resolve(strict=True) / output.name
    protected = [root.resolve()]
    for option in ("--git-dir", "--git-common-dir", "--git-path"):
        args = (option, "objects") if option == "--git-path" else (option,)
        protected.append(Path(os.fsdecode(git(context, "rev-parse", *args)).strip()).resolve())
    if any(output.is_relative_to(directory) for directory in protected):
        reject(context, "output_invalid")
    return output


def write_output(output: Path, data: bytes, context: Context) -> None:
    descriptor = None
    try:
        descriptor = os.open(output, os.O_WRONLY | os.O_CREAT | os.O_EXCL
                             | getattr(os, "O_NOFOLLOW", 0), 0o600)
    except FileExistsError:
        reject(context, "output_exists")
    info = os.fstat(descriptor)
    try:
        with os.fdopen(descriptor, "wb") as stream:
            if not stat.S_ISREG(info.st_mode):
                reject(context, "output_invalid")
            stream.write(data)
            stream.flush()
            os.fsync(stream.fileno())
    except BaseException:
        try:
            current = output.lstat()
            if (current.st_dev, current.st_ino) == (info.st_dev, info.st_ino):
                output.unlink()
        except OSError:
            pass
        raise


def convert_response(source: Path, base: str, output: Path) -> dict:
    context = Context()
    try:
        if guard is None:
            reject(context, "internal_error")
        response = parse_response(read_response(source, context), context)
        context.stage = "edit_resolution"
        root, tree, objects = verify_checkout(base, context)
        output = validate_output(output, root, context)
        if response["status"] == "changes":
            modified = resolve_edits(response["edits"], tree, context)
            context.stage = "diff_generation"
            data = generate_diff(base, objects, modified, context)
            verify_checkout(base, context)
            write_output(output, data, context)
        return {"status": response["status"], "input_sha256": context.input_sha256,
                "input_bytes": context.input_bytes}
    except ConversionError:
        raise
    except UnicodeError:
        reject(context, "invalid_encoding")
    except OSError:
        reject(context, "io_error")
    except Exception:
        reject(context, "internal_error")


class SafeParser(argparse.ArgumentParser):
    def error(self, message: str) -> None:
        reject(Context(), "invalid_schema")


def main() -> int:
    try:
        parser = SafeParser(description=__doc__)
        parser.add_argument("response", type=Path)
        parser.add_argument("--base", required=True)
        parser.add_argument("--output", type=Path, required=True)
        parser.add_argument("--diagnostic-json", action="store_true")
        args = parser.parse_args()
        result = convert_response(args.response, args.base, args.output)
        print(json.dumps(result, sort_keys=True))
        return 0
    except ConversionError as error:
        print(json.dumps(error.diagnostic, sort_keys=True), file=sys.stderr)
        return 1
    except Exception:
        print(json.dumps(Context().diagnostic("internal_error"), sort_keys=True), file=sys.stderr)
        return 1


if __name__ == "__main__":
    raise SystemExit(main())
