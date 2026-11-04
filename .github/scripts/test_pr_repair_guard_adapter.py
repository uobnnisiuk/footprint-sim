"""Only synthetic GitHub objects and disposable local Git; no live operations."""
from copy import deepcopy
import os
from pathlib import Path
import sys
import unittest
from unittest.mock import patch

sys.dont_write_bytecode = True
sys.path.insert(0, str(Path(__file__).resolve().parents[2] / "tools"))
import pr_repair_adapter as adapter
import pr_repair_guard_adapter as guard
from test_pr_repair_adapter import Reader

MAIN, BASE, HEAD, FINAL = (x * 40 for x in "abcd")


def tree_from_paths(reader, paths):
    nested = {}
    for path, item in paths.items():
        parent = nested
        pieces = path.split("/")
        for piece in pieces[:-1]:
            parent = parent.setdefault(piece, {})
        parent[pieces[-1]] = item

    def build(folder):
        rows = []
        for name, item in folder.items():
            if type(item) is dict:
                rows.append((name, "040000", build(item)))
            else:
                mode, sha = item
                rows.append((name, mode, sha))
        return reader.tree(rows)
    return build(nested)


def fixture():
    reader = Reader()
    source = (Path(__file__).resolve().parent / "patch_guard.py").read_bytes()
    guard_oid = reader.blob(source)
    # This body must never be fetched/materialized. The authenticated tree can
    # retain its identity without interpreting a credential-looking filename.
    unknown_oid = adapter.oid("blob", b"unfetched synthetic bytes\n")
    paths = {guard.GUARD_PATH: ("100644", guard_oid),
             "unrelated.private": ("100644", unknown_oid)}
    for commit, data, parents in ((MAIN, b"before\n", ()), (BASE, b"before\n", ()),
                                  (HEAD, b"initial\n", (BASE,)), (FINAL, b"fixed\n", (HEAD,))):
        entries = {**paths, "src/Test.cs": ("100644", reader.blob(data))}
        tree = tree_from_paths(reader, entries)
        reader.data["git/commits/" + commit] = {"sha": commit, "tree": {"sha": tree},
                                               "parents": [{"sha": p} for p in parents]}
    reader.data["git/ref/heads/main"] = {"object": {"sha": MAIN, "type": "commit"}}
    return reader, unknown_oid


def run(reader, **changes):
    args = {"main_sha": MAIN, "base_sha": BASE, "head_sha": HEAD,
            "candidate_sha": FINAL, "permitted_paths": ("src/Test.cs",)}
    args.update(changes)
    return guard.validate_detached_guard(reader, **args)


class GuardAdapterTests(unittest.TestCase):
    def test_real_guard_on_two_synthetic_exact_tree_baselines(self):
        reader, unknown = fixture()
        result = run(reader)
        self.assertEqual(result.status, "isolated_tree_guard_passed")
        self.assertTrue(result.synthetic_baselines)
        self.assertFalse(result.unchanged_blob_bytes_verified)
        self.assertFalse(result.candidate_code_executed)
        self.assertFalse(result.may_publish)
        self.assertFalse(result.may_activate)
        self.assertIn(b"-before", result.full_diff)
        self.assertIn(b"-initial", result.repair_diff)
        self.assertNotIn(("git/blobs/" + unknown, None), reader.calls)
        self.assertNotIn("fixed", repr(result))

    def test_candidate_script_is_text_only_and_never_executed(self):
        reader, _ = fixture()
        # Change only an approved .py text file; it is never imported or run.
        for commit, text, parents in ((MAIN, b"# original\n", ()), (BASE, b"# original\n", ()),
                                     (HEAD, b"# initial\n", (BASE,)),
                                     (FINAL, b'raise RuntimeError("candidate-executed")\n', (HEAD,))):
            old = adapter.GitObjects(reader).inventory(commit)["entries"]
            entries = {p: (v["mode"], v["oid"]) for p, v in old.items() if p != "src/Test.cs"}
            entries["src/Trap.py"] = ("100644", reader.blob(text))
            tree = tree_from_paths(reader, entries)
            reader.data["git/commits/" + commit] = {"sha": commit, "tree": {"sha": tree},
                                                    "parents": [{"sha": p} for p in parents]}
        self.assertEqual(run(reader, permitted_paths=("src/Trap.py",)).status,
                         "isolated_tree_guard_passed")

    def test_unreviewed_main_guard_never_runs(self):
        reader, _ = fixture()
        items = adapter.GitObjects(reader).inventory(MAIN)["entries"]
        entries = {p: (v["mode"], v["oid"]) for p, v in items.items()}
        entries[guard.GUARD_PATH] = ("100644", reader.blob(b'raise RuntimeError("not-reviewed")\n'))
        reader.data["git/commits/" + MAIN]["tree"]["sha"] = tree_from_paths(reader, entries)
        with patch.object(guard.subprocess, "run") as subprocess_mock:
            with self.assertRaisesRegex(guard.GuardAdapterHold, "main_guard_not_reviewed"):
                run(reader)
        subprocess_mock.assert_not_called()

    def test_candidate_cannot_change_guard_or_other_unapproved_file(self):
        for path in (guard.GUARD_PATH, "unrelated.private"):
            reader, _ = fixture()
            items = adapter.GitObjects(reader).inventory(FINAL)["entries"]
            entries = {p: (v["mode"], v["oid"]) for p, v in items.items()}
            entries[path] = ("100644", reader.blob(b"changed\n"))
            reader.data["git/commits/" + FINAL]["tree"]["sha"] = tree_from_paths(reader, entries)
            with self.subTest(path=path), self.assertRaisesRegex(guard.GuardAdapterHold, "guard_scope_unverified"):
                run(reader)

    def test_added_deleted_and_mode_changes_hold(self):
        for kind in ("add", "delete", "mode"):
            reader, _ = fixture()
            items = adapter.GitObjects(reader).inventory(FINAL)["entries"]
            entries = {p: (v["mode"], v["oid"]) for p, v in items.items()}
            if kind == "add": entries["src/Extra.cs"] = ("100644", reader.blob(b"extra\n"))
            elif kind == "delete": del entries["unrelated.private"]
            else: entries["src/Test.cs"] = ("100755", entries["src/Test.cs"][1])
            reader.data["git/commits/" + FINAL]["tree"]["sha"] = tree_from_paths(reader, entries)
            with self.subTest(kind=kind), self.assertRaises(guard.GuardAdapterHold):
                run(reader)

    def test_non_utf8_nul_cr_and_oversize_hold(self):
        for data in (b"\xff", b"x\0\n", b"x\r\n", b"x" * (guard.MAX_TEXT + 1)):
            reader, _ = fixture()
            items = adapter.GitObjects(reader).inventory(FINAL)["entries"]
            entries = {p: (v["mode"], v["oid"]) for p, v in items.items()}
            entries["src/Test.cs"] = ("100644", reader.blob(data))
            reader.data["git/commits/" + FINAL]["tree"]["sha"] = tree_from_paths(reader, entries)
            with self.subTest(size=len(data)), self.assertRaisesRegex(guard.GuardAdapterHold, "guard_text_unverified"):
                run(reader)

    def test_wrong_parent_and_unapproved_path_fail_before_process(self):
        reader, _ = fixture()
        reader.data["git/commits/" + FINAL]["parents"] = [{"sha": BASE}]
        with patch.object(guard.subprocess, "run") as subprocess_mock:
            with self.assertRaisesRegex(guard.GuardAdapterHold, "guard_candidate_parent_unverified"):
                run(reader)
        subprocess_mock.assert_not_called()
        reader, _ = fixture()
        with self.assertRaisesRegex(guard.GuardAdapterHold, "guard_scope_unverified"):
            run(reader, permitted_paths=("../outside",))

    def test_clean_environment_does_not_inherit_sensitive_or_git_values(self):
        with patch.dict(os.environ, {"PRIVATE_MARKER": "must-not-copy", "GIT_SSH_COMMAND": "untrusted"}):
            env = guard._environment(Path("/tmp/synthetic"))
        self.assertNotIn("PRIVATE_MARKER", env)
        self.assertNotIn("GIT_SSH_COMMAND", env)
        self.assertEqual(env["PATH"], "/usr/bin:/bin")
        self.assertEqual(env["GIT_CONFIG_GLOBAL"], os.devnull)

    def test_main_drift_and_unavailable_source_hold(self):
        reader, _ = fixture()
        reader.data["git/ref/heads/main"]["object"]["sha"] = BASE
        with self.assertRaisesRegex(guard.GuardAdapterHold, "guard_main_changed"):
            run(reader)
        reader, _ = fixture()
        del reader.data["git/commits/" + BASE]
        with self.assertRaisesRegex(guard.GuardAdapterHold, "guard_source_unverified"):
            run(reader)

    def test_source_error_and_process_error_do_not_emit_raw_content(self):
        reader, _ = fixture()
        with patch.object(guard.subprocess, "run", side_effect=OSError("private-value")):
            with self.assertRaises(guard.GuardAdapterHold) as error:
                run(reader)
        self.assertEqual(str(error.exception), "isolated_process_failed")

    def test_arbitrary_reader_error_is_sanitized(self):
        reader, _ = fixture()
        with patch.object(reader, "get", side_effect=RuntimeError("private-response-value")):
            with self.assertRaises(guard.GuardAdapterHold) as error:
                run(reader)
        self.assertEqual(str(error.exception), "guard_source_unverified")

    def test_main_change_after_both_guard_runs_holds(self):
        reader, _ = fixture()
        calls = [0]
        def observed_main():
            calls[0] += 1
            return {"object": {"sha": MAIN if calls[0] == 1 else HEAD, "type": "commit"}}
        reader.data["git/ref/heads/main"] = observed_main
        with self.assertRaisesRegex(guard.GuardAdapterHold, "guard_main_changed"):
            run(reader)


if __name__ == "__main__":
    unittest.main()
