"""R4 candidate verification tests: no GitHub access or subprocess calls."""
import sys
import unittest
from pathlib import Path
from copy import deepcopy

sys.path.insert(0, str(Path(__file__).resolve().parents[2] / "tools"))
import pr_repair_candidate as m

A, B, C, T = ("a"*40, "b"*40, "c"*40, "d"*40)
RID = "e"*32


class CandidateTests(unittest.TestCase):
    def fixture(self):
        before = {"src/Test.cs": {"oid": A, "mode": "100644"},
                  "docs/NoChange.md": {"oid": B, "mode": "100644"}}
        after = deepcopy(before)
        after["src/Test.cs"]["oid"] = C
        commit = {"sha": C, "parents": [A], "tree_sha": T}
        env = {"reservation_id": RID, "kind": "substantive",
               "parent_sha": A, "commit_sha": C, "tree_sha": T,
               "changed_paths": ["src/Test.cs"], "changes_sha256": ""}
        manifest = {"parent_sha": A, "commit_sha": C, "tree_sha": T,
                    "changes": [{"path": "src/Test.cs", "before": A, "after": C}]}
        env["changes_sha256"] = m.digest(manifest)
        receipt = {"id": RID, "kind": "substantive", "headSha": A}
        return env, commit, before, after, receipt

    def verify(self, parts):
        env, commit, before, after, receipt = parts
        return m.verify_candidate(env, commit, before, after,
                                  expected_head=A, reservation=receipt,
                                  permitted_paths=("src/Test.cs",))

    def test_valid_deterministic(self):
        f = self.fixture()
        self.assertEqual(self.verify(f), self.verify(f))

    def test_single_parent_required(self):
        f = list(self.fixture()); f[1]["parents"] = [A, B]
        with self.assertRaises(m.CandidateHalt): self.verify(f)

    def test_no_other_parent(self):
        f = list(self.fixture()); f[1]["parents"] = [B]
        with self.assertRaises(m.CandidateHalt): self.verify(f)

    def test_reject_extra_tree_change(self):
        f = list(self.fixture()); f[3]["docs/NoChange.md"]["oid"] = C
        with self.assertRaises(m.CandidateHalt): self.verify(f)

    def test_reject_added_deleted_and_mode(self):
        f = list(self.fixture()); f[3]["new.cs"] = {"oid": C, "mode":"100644"}
        with self.assertRaises(m.CandidateHalt): self.verify(f)
        f = list(self.fixture()); f[3]["src/Test.cs"]["mode"] = "100755"
        with self.assertRaises(m.CandidateHalt): self.verify(f)

    def test_reject_manifest_tamper(self):
        f = list(self.fixture()); f[0]["changes_sha256"] = "0"*64
        with self.assertRaises(m.CandidateHalt): self.verify(f)

    def test_reject_hash_tree_commit(self):
        f = list(self.fixture()); f[1]["tree_sha"] = B
        with self.assertRaises(m.CandidateHalt): self.verify(f)

    def test_reject_expanded_or_unknown_paths(self):
        f = list(self.fixture()); f[0]["changed_paths"] = ["src/Test.cs", "docs/NoChange.md"]
        with self.assertRaises(m.CandidateHalt): self.verify(f)

    def test_reject_wrong_kind_reservation(self):
        f = list(self.fixture()); f[4]["kind"] = "minor"
        with self.assertRaises(m.CandidateHalt): self.verify(f)

    def test_reject_unsupported_location(self):
        f = list(self.fixture()); f[0]["changed_paths"] = ["tools/secret.py"]
        with self.assertRaises(m.CandidateHalt): self.verify(f)
