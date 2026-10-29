#!/usr/bin/env python3
"""Synthetic, offline tests for structured edits and the real patch guard.

These fixtures are not a reconstruction of the lost Issue #21 response.
No model, network service, or candidate program is executed by these tests.
"""
import hashlib
import importlib.util
import json
import os
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest
from unittest.mock import patch as mock_patch

SCRIPTS = Path(__file__).resolve().parent
CONVERTER = SCRIPTS / "structured_edits.py"
GUARD = SCRIPTS / "patch_guard.py"
SPEC = importlib.util.spec_from_file_location("structured_test_guard", GUARD)
guard = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(guard)
CONVERTER_SPEC = importlib.util.spec_from_file_location("structured_edits_under_test", CONVERTER)
converter = importlib.util.module_from_spec(CONVERTER_SPEC)
CONVERTER_SPEC.loader.exec_module(converter)


class StructuredEditsTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory(prefix="structured-edits-test-")
        self.addCleanup(self.temp.cleanup)
        self.area = Path(self.temp.name)
        self.repo = self.area / "repo"
        self.repo.mkdir()
        self.response = self.area / "response.json"
        self.output = self.area / "candidate.patch"
        self.env = {key: value for key, value in os.environ.items()
                    if not key.startswith("GIT_")}
        self.env.update({"GIT_CONFIG_GLOBAL": os.devnull, "GIT_CONFIG_NOSYSTEM": "1",
                         "GIT_TERMINAL_PROMPT": "0"})
        self.git("init", "--quiet", "--template=")
        self.git("config", "user.name", "Structured edit test")
        self.git("config", "user.email", "structured@example.invalid")
        self.write("src/code.cs", b"old\n")
        self.write("README.md", b"baseline\n")
        self.commit()

    def git(self, *args, input_data=None):
        result = subprocess.run(
            ["git", "--no-optional-locks", "-c", "core.hooksPath=/dev/null",
             "-c", "core.fsmonitor=false", *args], cwd=self.repo, env=self.env,
            input=input_data, capture_output=True, check=False)
        self.assertEqual(result.returncode, 0, result.stderr.decode(errors="replace"))
        return result.stdout

    def write(self, relative, data):
        path = self.repo / relative
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_bytes(data)

    def commit(self):
        self.git("add", "--all")
        self.git("commit", "--quiet", "--allow-empty", "-m", "trusted fixture")
        self.base = self.git("rev-parse", "HEAD").decode().strip()

    def snapshot(self):
        files = {}
        for path in self.repo.rglob("*"):
            if ".git" in path.relative_to(self.repo).parts:
                continue
            name = str(path.relative_to(self.repo))
            if path.is_symlink():
                files[name] = ("symlink", os.readlink(path))
            elif path.is_file():
                files[name] = ("file", path.read_bytes(), path.stat().st_mode)
        return {"files": files, "index": (self.repo / ".git/index").read_bytes(),
                "head": self.git("rev-parse", "HEAD"),
                "objects": sorted(str(path.relative_to(self.repo / ".git/objects"))
                                  for path in (self.repo / ".git/objects").rglob("*")
                                  if path.is_file())}

    @staticmethod
    def edit(before="old", after="new", path="src/code.cs"):
        return {"path": path, "before": before, "after": after}

    @staticmethod
    def proposal(*edits):
        return {"version": 1, "status": "changes", "edits": list(edits)}

    def test_existing_research_handoff_state_cannot_be_promoted_by_model(self):
        path = "docs/research-autodev-state.json"
        self.write(path, b'{"phase":"integrating"}\n')
        self.commit()
        self.reject(self.proposal(self.edit("integrating", "f19_complete", path)),
                    reason="disallowed_path")

    def invoke(self, value=None, *, raw=None, base=None, env=None, output=None):
        if raw is None:
            raw = json.dumps(value, ensure_ascii=False).encode("utf-8")
        self.response.write_bytes(raw)
        return subprocess.run(
            [sys.executable, "-B", str(CONVERTER), str(self.response), "--base",
             self.base if base is None else base, "--output", str(output or self.output),
             "--diagnostic-json"], cwd=self.repo, env=env or self.env,
            capture_output=True, check=False)

    def success(self, value, *, expected_status="changes"):
        before = self.snapshot()
        result = self.invoke(value)
        self.assertEqual(result.returncode, 0, result.stderr.decode(errors="replace"))
        self.assertEqual(result.stderr, b"")
        self.assertEqual(json.loads(result.stdout), {
            "status": expected_status,
            "input_sha256": hashlib.sha256(self.response.read_bytes()).hexdigest(),
            "input_bytes": self.response.stat().st_size,
        })
        self.assertEqual(before, self.snapshot(), "Conversion mutated the actual repository")
        if expected_status == "changes":
            self.assertTrue(self.output.is_file())
            self.assertTrue(self.output.read_bytes().startswith(b"diff --git "))
        else:
            self.assertFalse(self.output.exists())
        return result

    def reject(self, value=None, *, raw=None, stage=None, base=None, reason=None):
        before = self.snapshot()
        result = self.invoke(value, raw=raw, base=base)
        self.assertEqual(result.returncode, 1)
        self.assertEqual(result.stdout, b"")
        diagnostic = json.loads(result.stderr)
        self.assertEqual(set(diagnostic), {
            "schema_version", "command", "stage", "reason", "input_sha256",
            "input_bytes", "operation", "match_count"})
        self.assertEqual(diagnostic["schema_version"], 1)
        self.assertEqual(diagnostic["command"], "structured_edits")
        self.assertIn(diagnostic["stage"], {
            "response_validation", "edit_resolution", "diff_generation"})
        if stage is not None:
            self.assertEqual(diagnostic["stage"], stage)
        if reason is not None:
            self.assertEqual(diagnostic["reason"], reason)
        self.assertIsInstance(diagnostic["reason"], str)
        self.assertRegex(diagnostic["reason"], r"^[a-z][a-z0-9_]*$")
        self.assertEqual(diagnostic["input_sha256"],
                         hashlib.sha256(self.response.read_bytes()).hexdigest())
        self.assertEqual(diagnostic["input_bytes"], self.response.stat().st_size)
        for field in ("operation", "match_count"):
            self.assertTrue(diagnostic[field] is None or type(diagnostic[field]) is int)
        self.assertNotIn(b"PRIVATE", result.stderr)
        self.assertNotIn(b"Traceback", result.stderr)
        self.assertNotIn(str(self.repo).encode(), result.stderr)
        self.assertFalse(self.output.exists(), "Rejected response left a usable patch")
        self.assertEqual(before, self.snapshot(), "Rejected response mutated the repository")
        return diagnostic

    def apply_guard(self, *, expected_code=0, patch=None):
        if patch is not None:
            self.output.write_bytes(patch)
        result = subprocess.run(
            [sys.executable, "-B", str(GUARD), "import", str(self.output),
             "--base", self.base, "--diagnostic-json"], cwd=self.repo,
            env=self.env, capture_output=True, check=False)
        self.assertEqual(result.returncode, expected_code,
                         result.stderr.decode(errors="replace"))
        return result

    def test_single_replacement_passes_actual_guard(self):
        self.success(self.proposal(self.edit()))
        self.apply_guard()
        self.assertEqual((self.repo / "src/code.cs").read_bytes(), b"new\n")
        self.assertEqual(self.git("diff", "--cached", "--name-only"), b"src/code.cs\n")

    def test_multiple_files_pass_actual_guard(self):
        self.success(self.proposal(self.edit(), self.edit("baseline", "documentation", "README.md")))
        self.apply_guard()
        self.assertEqual((self.repo / "src/code.cs").read_bytes(), b"new\n")
        self.assertEqual((self.repo / "README.md").read_bytes(), b"documentation\n")

    def test_disjoint_edits_resolve_against_original_file(self):
        self.write("src/code.cs", b"alpha\nbeta\ngamma\n")
        self.commit()
        self.success(self.proposal(self.edit("alpha", "gamma"), self.edit("gamma", "omega")))
        self.apply_guard()
        self.assertEqual((self.repo / "src/code.cs").read_bytes(), b"gamma\nbeta\nomega\n")

    def test_order_independent_and_repeatable_patch_bytes(self):
        self.write("src/code.cs", b"alpha\nbeta\ngamma\n")
        self.commit()
        edits = [self.edit("alpha", "first"), self.edit("gamma", "last")]
        self.success(self.proposal(*edits))
        first = self.output.read_bytes()
        self.output.unlink()
        self.success(self.proposal(*edits))
        self.assertEqual(first, self.output.read_bytes())
        self.output.unlink()
        self.success(self.proposal(*reversed(edits)))
        self.assertEqual(first, self.output.read_bytes())

    def test_unicode_quotes_slashes_blank_lines_and_diff_text(self):
        original = '日本語 "quoted" \\path\n\n@@ literal\ndiff --git a/fake b/fake\nend\n'
        replacement = '日本語の変更 "quoted" \\path\n\n@@ -9,99 +0,1 @@\ndiff --git a/source b/source\nend\n'
        self.write("src/code.cs", original.encode())
        self.commit()
        self.success(self.proposal(self.edit(original, replacement)))
        self.apply_guard()
        self.assertEqual((self.repo / "src/code.cs").read_bytes(), replacement.encode())

    def test_final_newline_is_preserved_or_changed_exactly(self):
        for original, replacement in [(b"old\n", b"new"), (b"old", b"new\n"), (b"old", b"new")]:
            with self.subTest(original=original, replacement=replacement):
                self.write("src/code.cs", original)
                self.commit()
                self.output.unlink(missing_ok=True)
                self.success(self.proposal(self.edit(original.decode(), replacement.decode())))
                self.apply_guard()
                self.assertEqual((self.repo / "src/code.cs").read_bytes(), replacement)

    def test_crlf_exact_bytes_pass_guard_without_normalization(self):
        self.write("src/code.cs", b"first\r\nsecond\r\n")
        self.commit()
        self.success(self.proposal(self.edit("first\r\n", "changed\r\n")))
        self.apply_guard()
        self.assertEqual((self.repo / "src/code.cs").read_bytes(), b"changed\r\nsecond\r\n")

    def test_crlf_cannot_be_matched_with_normalized_lf(self):
        self.write("src/code.cs", b"first\r\nsecond\r\n")
        self.commit()
        diagnostic = self.reject(self.proposal(self.edit("first\n", "changed\n")),
                                 stage="edit_resolution")
        self.assertEqual(diagnostic["match_count"], 0)

    def test_structured_no_changes_stops_without_patch(self):
        self.success({"version": 1, "status": "no_changes", "edits": []},
                     expected_status="no_changes")

    def test_inconsistent_status_and_edits_rejected(self):
        for proposal in [self.proposal(),
                         {"version": 1, "status": "no_changes", "edits": [self.edit()]},
                         {"version": 1, "status": "NO_CHANGES", "edits": []}]:
            with self.subTest(proposal=proposal):
                self.reject(proposal, stage="response_validation")

    def test_absent_before_is_not_fuzzily_matched(self):
        diagnostic = self.reject(self.proposal(self.edit("PRIVATE absent old ", "new")),
                                 stage="edit_resolution")
        self.assertEqual(diagnostic["match_count"], 0)

    def test_multiple_matches_rejected(self):
        self.write("src/code.cs", b"old old\n")
        self.commit()
        diagnostic = self.reject(self.proposal(self.edit()), stage="edit_resolution")
        self.assertEqual(diagnostic["match_count"], 2)

    def test_overlapping_occurrences_count_as_multiple(self):
        self.write("src/code.cs", b"aaa\n")
        self.commit()
        diagnostic = self.reject(self.proposal(self.edit("aa", "x")), stage="edit_resolution")
        self.assertEqual(diagnostic["match_count"], 2)

    def test_duplicate_edits_are_rejected(self):
        self.reject(self.proposal(self.edit(), self.edit()), stage="edit_resolution")

    def test_overlapping_edit_ranges_are_rejected(self):
        self.write("src/code.cs", b"abcdef\n")
        self.commit()
        self.reject(self.proposal(self.edit("abcd", "x"), self.edit("cdef", "y")),
                    stage="edit_resolution")

    def test_inserted_text_never_becomes_later_match_target(self):
        diagnostic = self.reject(self.proposal(self.edit("old", "inserted"),
                                                self.edit("inserted", "again")),
                                 stage="edit_resolution")
        self.assertEqual(diagnostic["match_count"], 0)

    def test_second_edit_failure_leaves_first_edit_unapplied(self):
        self.reject(self.proposal(self.edit(), self.edit("PRIVATE absent", "PRIVATE after", "README.md")),
                    stage="edit_resolution")
        self.assertEqual((self.repo / "src/code.cs").read_bytes(), b"old\n")

    def test_empty_or_identical_before_after_rejected(self):
        for edit in [self.edit("", "x"), self.edit("old", "old")]:
            with self.subTest(edit=edit):
                self.reject(self.proposal(edit))

    def test_empty_after_can_remove_exact_text_without_deleting_file(self):
        self.success(self.proposal(self.edit("old", "")))
        self.apply_guard()
        self.assertEqual((self.repo / "src/code.cs").read_bytes(), b"\n")

    def test_invalid_json_and_trailing_documents_rejected(self):
        for raw in [b"", b"PRIVATE not JSON", b"{", b"{} {}", b"[]", b"null",
                    b'{"version":NaN,"status":"changes","edits":[]}',
                    b'{"version":Infinity,"status":"changes","edits":[]}']:
            with self.subTest(raw=raw):
                self.reject(raw=raw, stage="response_validation")

    def test_duplicate_json_keys_rejected_at_all_levels(self):
        for raw in [
            b'{"version":1,"version":1,"status":"changes","edits":[]}',
            b'{"version":1,"status":"changes","edits":[{"path":"src/code.cs","before":"old","before":"old","after":"new"}]}',
        ]:
            with self.subTest(raw=raw):
                self.reject(raw=raw, stage="response_validation")

    def test_unknown_fields_rejected(self):
        proposal = self.proposal(self.edit())
        proposal["PRIVATE_unknown"] = "PRIVATE value"
        self.reject(proposal, stage="response_validation")
        proposal = self.proposal(self.edit())
        proposal["edits"][0]["operation"] = "delete"
        self.reject(proposal, stage="response_validation")

    def test_required_fields_and_exact_types_rejected(self):
        for key, value in [("version", True), ("version", 1.0), ("status", None),
                           ("edits", {}), ("edits", [None])]:
            proposal = self.proposal(self.edit())
            proposal[key] = value
            with self.subTest(key=key, value=value):
                self.reject(proposal, stage="response_validation")
        for key in ("version", "status", "edits"):
            proposal = self.proposal(self.edit())
            del proposal[key]
            with self.subTest(missing=key):
                self.reject(proposal, stage="response_validation")
        for key, value in [("path", 3), ("before", []), ("after", None)]:
            proposal = self.proposal(self.edit())
            proposal["edits"][0][key] = value
            with self.subTest(key=key, value=value):
                self.reject(proposal, stage="response_validation")

    def test_invalid_encoding_nul_and_surrogates_rejected(self):
        valid = json.dumps(self.proposal(self.edit())).encode()
        for raw in [b"\xff" + valid, b"\xef\xbb\xbf" + valid,
                    valid.replace(b'"new"', b'"PRIVATE\\u0000"'),
                    valid.replace(b'"new"', b'"PRIVATE\\ud800"'),
                    b"\0" + valid]:
            with self.subTest(raw=raw[:25]):
                self.reject(raw=raw, stage="response_validation")

    def test_excessive_nesting_rejected_without_traceback(self):
        self.reject(raw=b"[" * 2000 + b"0" + b"]" * 2000, stage="response_validation")

    def test_oversized_response_rejected_with_hash_and_size(self):
        self.reject(raw=b" " * 4_000_001, stage="response_validation")

    def test_response_above_hash_cap_is_rejected_without_reading_contents(self):
        size = converter.MAX_RESPONSE_HASH_BYTES + 1
        with self.response.open("wb") as stream:
            stream.truncate(size)
        real_fdopen = converter.os.fdopen

        class NoRead:
            def __init__(self, descriptor, mode):
                self.stream = real_fdopen(descriptor, mode)

            def __enter__(self):
                return self

            def __exit__(self, *args):
                return self.stream.__exit__(*args)

            def fileno(self):
                return self.stream.fileno()

            def read(self, _size):
                raise AssertionError("Input above the hard cap must not be read")

        context = converter.Context()
        before = self.snapshot()
        with mock_patch.object(converter.os, "fdopen", side_effect=NoRead), \
             self.assertRaises(converter.ConversionError) as caught:
            converter.read_response(self.response, context)
        self.assertEqual(caught.exception.diagnostic["reason"], "size_limit")
        self.assertEqual(caught.exception.diagnostic["stage"], "response_validation")
        self.assertEqual(caught.exception.diagnostic["input_bytes"], size)
        self.assertIsNone(caught.exception.diagnostic["input_sha256"])
        self.assertEqual(before, self.snapshot())
        self.assertFalse(self.output.exists())

    def test_oversized_replacement_rejected(self):
        self.reject(self.proposal(self.edit("old", "x" * (guard.MAX_FILE_BYTES + 1))))

    def test_too_many_operations_rejected(self):
        self.reject(self.proposal(*[self.edit() for _ in range(1001)]))

    def test_too_many_distinct_files_rejected(self):
        edits = []
        for number in range(guard.MAX_FILES + 1):
            path = f"docs/file{number}.md"
            self.write(path, b"original\n")
            edits.append(self.edit("original", "replacement", path))
        self.commit()
        self.reject(self.proposal(*edits))

    def test_oversized_generated_diff_leaves_no_patch_or_real_changes(self):
        old = "a" * 110000 + "\n"
        new = "b" * 110000 + "\n"
        self.write("src/code.cs", old.encode())
        self.write("src/second.cs", old.encode())
        self.commit()
        self.reject(self.proposal(self.edit(old, new), self.edit(old, new, "src/second.cs")),
                    stage="diff_generation")

    def test_forbidden_paths_reuse_guard_policy(self):
        for path in ["../README.md", "/src/code.cs", "src/../README.md", "src//code.cs",
                     "src\\code.cs", ".github/workflows/PRIVATE.yml", "tools/runner.py",
                     "src/AGENTS.md", "docs/AgEnTs.Md", "src/global.json",
                     "src/project.csproj", "src/Directory.Build.targets", "src/NuGet.Config",
                     "src/.git/config", "src/obj/code.cs", "src/space name.cs"]:
            with self.subTest(path=path):
                self.reject(self.proposal(self.edit(path=path)))

    def test_nonexistent_file_cannot_be_added_by_replacement(self):
        self.reject(self.proposal(self.edit(path="tests/new.cs")), stage="edit_resolution")

    def test_symlink_target_and_ancestor_rejected(self):
        (self.repo / "src/link.cs").symlink_to("code.cs")
        (self.repo / "src/linked").symlink_to("../src", target_is_directory=True)
        self.commit()
        for path in ("src/link.cs", "src/linked/code.cs"):
            with self.subTest(path=path):
                self.reject(self.proposal(self.edit(path=path)))

    def test_executable_file_mode_rejected(self):
        (self.repo / "src/code.cs").chmod(0o755)
        self.commit()
        self.reject(self.proposal(self.edit()), stage="edit_resolution")

    def test_gitlink_mode_rejected(self):
        self.git("update-index", "--add", "--cacheinfo", "160000," + self.base + ",src/module")
        self.git("commit", "--quiet", "-m", "trusted gitlink fixture")
        self.base = self.git("rev-parse", "HEAD").decode().strip()
        self.reject(self.proposal(self.edit(path="src/module")), stage="edit_resolution")

    def test_binary_and_invalid_utf8_baseline_rejected(self):
        for data in (b"old\nPRIVATE\0\n", b"old\nPRIVATE\xff\n"):
            with self.subTest(data=data):
                self.write("src/code.cs", data)
                self.commit()
                self.reject(self.proposal(self.edit()), stage="edit_resolution")

    def test_oversized_baseline_rejected(self):
        self.write("src/code.cs", b"old\n" + b"x" * guard.MAX_FILE_BYTES)
        self.commit()
        self.reject(self.proposal(self.edit()), stage="edit_resolution")

    def test_old_or_invalid_base_rejected_without_mutation(self):
        old = self.base
        self.write("README.md", b"second revision\n")
        self.commit()
        for base in (old, "0" * 40, "HEAD", "PRIVATE bad baseline"):
            with self.subTest(base=base):
                self.reject(self.proposal(self.edit()), base=base)

    def test_replace_refs_cannot_substitute_the_fixed_base_contents(self):
        original_base = self.base
        self.write("src/code.cs", b"PRIVATE substituted baseline\n")
        self.commit()
        replacement = self.base
        self.git("switch", "--detach", original_base)
        self.base = original_base
        self.git("replace", original_base, replacement)
        # Ordinary Git now reads the replacement object; the converter must not.
        self.assertEqual(self.git("show", original_base + ":src/code.cs"),
                         b"PRIVATE substituted baseline\n")
        self.success(self.proposal(self.edit()))
        self.assertIn(b"-old\n+new\n", self.output.read_bytes())
        self.assertNotIn(b"PRIVATE", self.output.read_bytes())

    def test_dirty_tree_index_and_untracked_files_rejected(self):
        self.write("README.md", b"PRIVATE unstaged\n")
        self.reject(self.proposal(self.edit()))
        self.git("add", "README.md")
        self.reject(self.proposal(self.edit()))
        self.commit()
        self.write("docs/PRIVATE.md", b"PRIVATE untracked\n")
        self.reject(self.proposal(self.edit()))

    def test_guard_rejects_valid_generated_diff_if_real_tree_changes(self):
        self.success(self.proposal(self.edit()))
        self.write("README.md", b"PRIVATE local edit\n")
        before = self.snapshot()
        result = self.apply_guard(expected_code=1)
        self.assertEqual(json.loads(result.stderr)["reason"], "dirty_checkout")
        self.assertEqual(before, self.snapshot())

    def test_guard_still_rejects_tampered_generated_hunks(self):
        self.success(self.proposal(self.edit()))
        original = self.output.read_bytes()
        tampered = original.replace(b"@@ -1 +1 @@", b"@@ -1,2 +1 @@")
        self.assertNotEqual(original, tampered)
        before = self.snapshot()
        result = self.apply_guard(expected_code=1, patch=tampered)
        self.assertEqual(json.loads(result.stderr)["reason"], "malformed_hunk")
        self.assertEqual(before, self.snapshot())

    def test_existing_output_is_not_overwritten(self):
        self.output.write_bytes(b"PRIVATE existing artifact")
        before = self.snapshot()
        result = self.invoke(self.proposal(self.edit()))
        self.assertEqual(result.returncode, 1)
        self.assertNotIn(b"PRIVATE", result.stderr)
        self.assertEqual(self.output.read_bytes(), b"PRIVATE existing artifact")
        self.assertEqual(before, self.snapshot())

    def test_response_symlink_rejected(self):
        target = self.area / "target.json"
        target.write_text(json.dumps(self.proposal(self.edit())))
        self.response.symlink_to(target)
        before = self.snapshot()
        result = subprocess.run(
            [sys.executable, "-B", str(CONVERTER), str(self.response), "--base", self.base,
             "--output", str(self.output), "--diagnostic-json"],
            cwd=self.repo, env=self.env, capture_output=True, check=False)
        self.assertEqual(result.returncode, 1)
        self.assertFalse(self.output.exists())
        self.assertEqual(before, self.snapshot())

    def test_output_symlink_rejected_without_touching_target(self):
        target = self.area / "PRIVATE.txt"
        target.write_bytes(b"PRIVATE artifact")
        self.output.symlink_to(target)
        before = self.snapshot()
        result = self.invoke(self.proposal(self.edit()))
        self.assertEqual(result.returncode, 1)
        self.assertEqual(target.read_bytes(), b"PRIVATE artifact")
        self.assertEqual(before, self.snapshot())

    def test_output_inside_real_worktree_is_rejected(self):
        before = self.snapshot()
        result = self.invoke(self.proposal(self.edit()), output=self.repo / "candidate.patch")
        self.assertEqual(result.returncode, 1)
        self.assertEqual(before, self.snapshot())

    def test_output_parent_alias_cannot_escape_real_worktree_check(self):
        (self.repo / "nested").mkdir()
        before = self.snapshot()
        target = self.repo / "nested" / ".." / "candidate.patch"
        result = self.invoke(self.proposal(self.edit()), output=target)
        self.assertEqual(result.returncode, 1)
        self.assertEqual(json.loads(result.stderr)["reason"], "output_invalid")
        self.assertFalse(target.exists())
        self.assertEqual(before, self.snapshot())

    def test_output_in_git_metadata_and_object_store_is_rejected(self):
        for target in (self.repo / ".git/PRIVATE.patch", self.repo / ".git/objects/PRIVATE.patch"):
            with self.subTest(target=target):
                before = self.snapshot()
                result = self.invoke(self.proposal(self.edit()), output=target)
                self.assertEqual(result.returncode, 1)
                self.assertEqual(json.loads(result.stderr)["reason"], "output_invalid")
                self.assertFalse(target.exists())
                self.assertEqual(before, self.snapshot())

    def test_linked_worktree_cannot_write_shared_git_metadata(self):
        linked = self.area / "linked-checkout"
        self.git("worktree", "add", "--detach", str(linked), self.base)
        self.response.write_text(json.dumps(self.proposal(self.edit())), encoding="utf-8")
        metadata = self.repo / ".git/worktrees" / linked.name
        before = self.snapshot()
        index = (metadata / "index").read_bytes()
        for target in (self.repo / ".git/PRIVATE.patch", metadata / "PRIVATE.patch",
                       self.repo / ".git/objects/PRIVATE.patch"):
            with self.subTest(target=target):
                result = subprocess.run(
                    [sys.executable, "-B", str(CONVERTER), str(self.response), "--base", self.base,
                     "--output", str(target), "--diagnostic-json"],
                    cwd=linked, env=self.env, capture_output=True, check=False)
                self.assertEqual(result.returncode, 1)
                self.assertEqual(json.loads(result.stderr)["reason"], "output_invalid")
                self.assertFalse(target.exists())
                self.assertEqual(before, self.snapshot())
                self.assertEqual(index, (metadata / "index").read_bytes())
                self.assertEqual((linked / "src/code.cs").read_bytes(), b"old\n")

    def test_final_output_io_failure_removes_partial_patch(self):
        self.response.write_text(json.dumps(self.proposal(self.edit())), encoding="utf-8")
        before = self.snapshot()
        previous = Path.cwd()
        try:
            os.chdir(self.repo)
            with mock_patch.object(converter.os, "fsync", side_effect=OSError("PRIVATE disk failure")), \
                 self.assertRaises(converter.ConversionError) as caught:
                converter.convert_response(self.response, self.base, self.output)
        finally:
            os.chdir(previous)
        diagnostic = caught.exception.diagnostic
        self.assertEqual(diagnostic["stage"], "diff_generation")
        self.assertEqual(diagnostic["reason"], "io_error")
        self.assertNotIn("PRIVATE", json.dumps(diagnostic))
        self.assertFalse(self.output.exists(), "I/O failure left a partially written patch")
        self.assertEqual(before, self.snapshot())

    def test_source_policy_attributes_filters_and_hooks_do_not_execute(self):
        marker = self.area / "executed"
        script = self.area / "PRIVATE-helper.sh"
        script.write_text("#!/bin/sh\nprintf triggered > '" + str(marker) + "'\nexit 1\n")
        script.chmod(0o755)
        self.write(".gitattributes", b"*.cs diff=private filter=private\n")
        self.commit()
        self.git("config", "diff.external", str(script))
        self.git("config", "diff.private.command", str(script))
        self.git("config", "diff.private.textconv", str(script))
        self.git("config", "filter.private.clean", str(script))
        self.git("config", "filter.private.smudge", str(script))
        self.git("config", "filter.private.required", "true")
        hooks = self.area / "hooks"
        hooks.mkdir()
        for name in ("post-index-change", "pre-commit", "post-checkout"):
            (hooks / name).symlink_to(script)
        self.git("config", "core.hooksPath", str(hooks))
        self.success(self.proposal(self.edit()))
        self.assertFalse(marker.exists(), "Conversion executed a configured helper")
        # The converter's bytes are ordinary raw text despite baseline attributes.
        self.assertIn(b"-old\n+new\n", self.output.read_bytes())


if __name__ == "__main__":
    unittest.main()
