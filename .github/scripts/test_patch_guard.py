#!/usr/bin/env python3
"""Regression tests for the AutoDev patch trust boundary (stdlib only)."""
import importlib.util
from pathlib import Path
import os
import subprocess
import tempfile
import unittest

SPEC = importlib.util.spec_from_file_location("patch_guard", Path(__file__).with_name("patch_guard.py"))
guard = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(guard)


class PatchGuardTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory(prefix="autodev-test-")
        self.root = Path(self.temp.name) / "repo"
        self.root.mkdir()
        self.previous = Path.cwd()
        os.chdir(self.root)
        self.git("init", "--quiet", "--template=")
        self.git("config", "user.name", "Guard test")
        self.git("config", "user.email", "guard@example.invalid")
        self.write("README.md", b"baseline\n")
        self.write("src/code.cs", b"old\n")
        self.write("docs/remove.md", b"remove me\n")
        self.git("add", ".")
        self.git("commit", "--quiet", "-m", "trusted baseline")
        self.base = self.git("rev-parse", "HEAD").decode().strip()
        self.patch_file = Path(self.temp.name) / "changes.patch"

    def tearDown(self):
        os.chdir(self.previous)
        self.temp.cleanup()

    def git(self, *args):
        return guard.git(*args)

    def write(self, path, data):
        target = self.root / path
        target.parent.mkdir(parents=True, exist_ok=True)
        target.write_bytes(data)

    def make_patch(self):
        self.git("add", "--all")
        patch = self.git("diff", "--cached", "--binary", "--no-renames", self.base)
        self.git("reset", "--hard", "--quiet", self.base)
        return patch

    def snapshot(self):
        files = {}
        for path in self.root.rglob("*"):
            if ".git" in path.parts:
                continue
            if path.is_symlink():
                files[str(path.relative_to(self.root))] = ("link", os.readlink(path))
            elif path.is_file():
                files[str(path.relative_to(self.root))] = ("file", path.read_bytes(), path.stat().st_mode)
        return self.git("write-tree"), self.git("rev-parse", "HEAD"), files

    def apply(self, patch, base=None):
        self.patch_file.write_bytes(patch)
        guard.import_patch(self.patch_file, base or self.base)

    def reject(self, patch, base=None):
        before = self.snapshot()
        with self.assertRaises((ValueError, subprocess.CalledProcessError)):
            self.apply(patch, base)
        self.assertEqual(before, self.snapshot(), "Rejected patch changed the real checkout/index")

    def test_add_modify_delete_and_unicode_contents(self):
        self.write("src/code.cs", "変更済み\n".encode())
        self.write("tests/new.cs", b"new regression\n")
        (self.root / "docs/remove.md").unlink()
        patch = self.make_patch()
        self.apply(patch)
        self.assertEqual((self.root / "src/code.cs").read_text(), "変更済み\n")
        self.assertTrue((self.root / "tests/new.cs").exists())
        self.assertFalse((self.root / "docs/remove.md").exists())
        self.assertEqual(set(self.git("diff", "--cached", "--name-only").decode().splitlines()),
                         {"src/code.cs", "tests/new.cs", "docs/remove.md"})

    def test_empty_file_addition(self):
        self.write("docs/empty.md", b"")
        self.apply(self.make_patch())
        self.assertEqual((self.root / "docs/empty.md").read_bytes(), b"")

    def test_no_final_newline_in_source(self):
        self.write("src/code.cs", b"new without newline")
        self.apply(self.make_patch())
        self.assertEqual((self.root / "src/code.cs").read_bytes(), b"new without newline")

    def test_codex_response_without_final_newline(self):
        self.write("src/code.cs", b"changed\n")
        self.apply(self.make_patch().removesuffix(b"\n"))
        self.assertEqual((self.root / "src/code.cs").read_bytes(), b"changed\n")

    def test_trusted_base_must_match_head(self):
        self.write("src/code.cs", b"changed\n")
        self.reject(self.make_patch(), "0" * 40)

    def test_base_must_be_full_sha(self):
        self.write("src/code.cs", b"changed\n")
        self.reject(self.make_patch(), "HEAD")

    def test_dirty_worktree_or_index_or_untracked_file(self):
        self.write("src/code.cs", b"changed\n")
        patch = self.make_patch()
        for kind in ["worktree", "index", "untracked"]:
            with self.subTest(kind=kind):
                path = "docs/untracked.md" if kind == "untracked" else "README.md"
                self.write(path, b"local work\n")
                if kind == "index":
                    self.git("add", path)
                self.reject(patch)
                self.git("reset", "--hard", "--quiet", self.base)
                if kind == "untracked":
                    (self.root / path).unlink()

    def test_outside_paths_and_nested_configuration(self):
        paths = [
            ".github/workflows/pwn.yml", "AGENTS.md", "scripts/pwn.py",
            "src/AGENTS.md", "docs/aGeNtS.Md", "tests/global.json",
            "src/sub/Directory.Build.props", "tests/custom.targets",
            "src/project.CSPROJ", "docs/tool.sln", "docs/other.slnx",
            "src/NuGet.Config", "src/packages.lock.json", "src/package-lock.json",
            "src/.git/config", "docs/.hidden", "src/.config/tool.json",
            "src/obj/code.cs", "tests/bin/code.cs", "scenarios/results/data.json",
            "src/../README.md", "/src/absolute.cs", "src//code.cs",
            "src\\code.cs", "src/space name.cs",
        ]
        for path in paths:
            with self.subTest(path=path):
                patch = (f"diff --git a/{path} b/{path}\nnew file mode 100644\n"
                         f"--- /dev/null\n+++ b/{path}\n@@ -0,0 +1 @@\n+bad\n").encode()
                self.reject(patch)

    def test_symlink_file_and_symlink_ancestor(self):
        (self.root / "src/link").symlink_to("../docs", target_is_directory=True)
        self.git("add", "src/link")
        self.git("commit", "--quiet", "-m", "trusted symlink")
        self.base = self.git("rev-parse", "HEAD").decode().strip()
        patch = (b"diff --git a/src/link/added.md b/src/link/added.md\n"
                 b"new file mode 100644\n--- /dev/null\n+++ b/src/link/added.md\n"
                 b"@@ -0,0 +1 @@\n+bad\n")
        self.reject(patch)
        (self.root / "src/link").unlink()
        self.reject(self.make_patch())

    def test_new_symlink_and_executable_and_gitlink_modes(self):
        patch = (b"diff --git a/src/evil b/src/evil\nnew file mode MODE\n"
                 b"--- /dev/null\n+++ b/src/evil\n@@ -0,0 +1 @@\n+target\n")
        for mode in [b"120000", b"100755", b"160000"]:
            with self.subTest(mode=mode):
                self.reject(patch.replace(b"MODE", mode))

    def test_existing_executable_deletion(self):
        (self.root / "src/code.cs").chmod(0o755)
        self.git("add", "src/code.cs")
        self.git("commit", "--quiet", "-m", "executable baseline")
        self.base = self.git("rev-parse", "HEAD").decode().strip()
        (self.root / "src/code.cs").unlink()
        self.reject(self.make_patch())

    def test_rename_and_copy_and_mode_change_headers(self):
        for extra in [
            "similarity index 100%\nrename from src/code.cs\nrename to src/new.cs\n",
            "similarity index 100%\ncopy from src/code.cs\ncopy to src/new.cs\n",
            "old mode 100644\nnew mode 100755\n",
        ]:
            with self.subTest(extra=extra):
                self.reject(("diff --git a/src/code.cs b/src/code.cs\n" + extra).encode())
        self.reject(b"diff --git a/src/code.cs b/src/new.cs\n")

    def test_binary_and_invalid_utf8_and_nul_patches(self):
        self.write("src/binary.dat", b"a\0b")
        self.reject(self.make_patch())
        for data in [b"\xff\n", b"\0\n"]:
            self.reject(data)

    def test_binary_or_invalid_utf8_in_unchanged_blob_context(self):
        for bad in [b"\0", b"\xff"]:
            with self.subTest(bad=bad):
                self.write("src/code.cs", b"first\nsecond\nthird\n" + bad + b"\n")
                self.git("add", "src/code.cs")
                self.git("commit", "--quiet", "-m", "unusual trusted file")
                self.base = self.git("rev-parse", "HEAD").decode().strip()
                patch = (b"diff --git a/src/code.cs b/src/code.cs\n"
                         b"--- a/src/code.cs\n+++ b/src/code.cs\n"
                         b"@@ -1,2 +1,2 @@\n-first\n+changed\n second\n")
                self.reject(patch)

    def test_file_size_limit(self):
        self.write("src/large.cs", b"a" * (guard.MAX_FILE_BYTES + 1) + b"\n")
        self.reject(self.make_patch())

    def test_patch_size_limit(self):
        self.reject(b"x" * (guard.MAX_PATCH_BYTES + 1))

    def test_file_count_limit(self):
        for number in range(guard.MAX_FILES + 1):
            self.write(f"docs/file{number}.md", b"small\n")
        self.reject(self.make_patch())

    def test_malformed_and_trailing_data_and_duplicate_paths(self):
        self.write("src/code.cs", b"changed\n")
        patch = self.make_patch()
        for malformed in [
            patch + b"unexpected trailing text\n",
            b"\x60\x60\x60diff\n" + patch + b"\x60\x60\x60\n",
            patch + patch,
            patch.replace(b"@@ -1 +1 @@", b"@@ -1,2 +1 @@"),
            patch.replace(b"-old\n", b"-wrong original\n"),
            patch.replace(b"+++ b/src/code.cs", b"+++ b/README.md"),
        ]:
            with self.subTest(patch=malformed[:100]):
                self.reject(malformed)

    def test_patch_file_symlink(self):
        self.write("src/code.cs", b"changed\n")
        target = Path(self.temp.name) / "target.patch"
        target.write_bytes(self.make_patch())
        self.patch_file.symlink_to(target)
        before = self.snapshot()
        with self.assertRaises(ValueError):
            guard.import_patch(self.patch_file, self.base)
        self.assertEqual(before, self.snapshot())

    def test_no_patch(self):
        self.reject(b"")


if __name__ == "__main__":
    unittest.main()
