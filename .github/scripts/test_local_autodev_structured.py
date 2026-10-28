"""Offline end-to-end runner tests: fake model, real converter and patch guard."""
from contextlib import redirect_stdout
import hashlib
import importlib.util
from io import StringIO
import json
from pathlib import Path
import shutil
import sys
import tempfile
import unittest
from unittest.mock import patch

ROOT = Path(__file__).resolve().parents[2]
spec = importlib.util.spec_from_file_location("runner_structured_tests", ROOT / "tools/local_autodev.py")
runner = importlib.util.module_from_spec(spec)
spec.loader.exec_module(runner)


class StructuredRunnerTests(unittest.TestCase):
    def exercise(self, outcome):
        real_cmd = runner.cmd
        with tempfile.TemporaryDirectory(prefix="structured-runner-test-") as temp:
            home = Path(temp)
            repo = home / "repository"
            repo.mkdir()
            real_cmd(["git", "init", "--quiet", "--template="], cwd=repo)
            (repo / "README.md").write_bytes(b"alpha\nbeta\nrepeat repeat\n")
            scripts = repo / ".github/scripts"
            scripts.mkdir(parents=True)
            for name in ("patch_guard.py", "structured_edits.py", "local_autodev_response.schema.json"):
                shutil.copyfile(ROOT / ".github/scripts" / name, scripts / name)
            real_cmd(["git", "add", "."], cwd=repo)
            real_cmd(["git", "-c", "user.name=Test", "-c", "user.email=test@example.invalid",
                      "-c", "commit.gpgsign=false", "commit", "--quiet", "-m", "base"], cwd=repo)
            base = real_cmd(["git", "rev-parse", "HEAD"], cwd=repo)
            real_cmd(["git", "update-ref", "refs/remotes/origin/main", base], cwd=repo)
            payload = {"version": 1, "status": "changes", "edits": [
                {"path": "README.md", "before": "alpha", "after": "日本語 PRIVATE_SOURCE"}]}
            if outcome == "ambiguous":
                payload["edits"][0]["before"] = "repeat"
            if outcome == "missing_match":
                payload["edits"][0]["before"] = "PRIVATE_MISSING"
            if outcome == "no_changes":
                payload.update(status="no_changes", edits=[])
            if outcome == "overlap":
                payload["edits"].append({"path": "README.md", "before": "alpha\nbeta", "after": "changed"})
            response = json.dumps(payload, ensure_ascii=False).encode("utf-8")
            if outcome == "invalid_json":
                response = b'{"PRIVATE_SECRET":'
            if outcome == "invalid_encoding":
                response = b'\xffPRIVATE_SECRET'
            calls, inspected = [], {}

            def fake(argv, **kwargs):
                calls.append(argv)
                if argv[:2] == ["gh", "api"]:
                    return json.dumps({"number": 3, "state": "open", "title": "Synthetic test",
                                       "body": "PRIVATE_PROMPT", "user": {"login": "uobnnisiuk"},
                                       "labels": [{"name": "local-autodev"}]})
                if argv[:2] == ["git", "fetch"]:
                    return ""
                if argv[:2] == ["codex", "exec"]:
                    self.assertIn("--ignore-user-config", argv)
                    self.assertIn("--ignore-rules", argv)
                    self.assertIn("--ephemeral", argv)
                    self.assertEqual(argv[argv.index("--sandbox") + 1], "read-only")
                    for option in ('model_provider="openai"', 'forced_login_method="chatgpt"',
                                   'approval_policy="never"', 'web_search="disabled"'):
                        self.assertIn(option, argv)
                    self.assertNotIn("GH_TOKEN", kwargs["env"])
                    self.assertNotIn("OPENAI_API_KEY", kwargs["env"])
                    schema = Path(argv[argv.index("--output-schema") + 1])
                    self.assertEqual(json.loads(schema.read_text())["type"], "object")
                    self.assertIn("original baseline", kwargs["input_text"])
                    if outcome == "model_failure":
                        return real_cmd([sys.executable, "-c",
                                         "import sys;sys.stderr.write('PRIVATE_TOKEN HTTP 429');sys.exit(1)"],
                                        **kwargs)
                    Path(argv[argv.index("--output-last-message") + 1]).write_bytes(response)
                    if outcome == "dirty":
                        (kwargs["cwd"] / "README.md").write_text("PRIVATE_DIRTY", encoding="utf-8")
                    return ""
                if "structured_edits.py" in " ".join(argv):
                    self.assertIn("-B", argv)
                    result = real_cmd(argv, **kwargs)
                    if outcome == "guard_rejection":
                        Path(argv[argv.index("--output") + 1]).write_bytes(
                            b"diff --git a/README.md b/README.md\n--- a/README.md\n+++ b/README.md\n"
                            b"@@ -1,999 +1 @@\n-alpha\n+PRIVATE_PATCH\n")
                    return result
                if "patch_guard.py" in " ".join(argv):
                    self.assertIn("-B", argv)
                    result = real_cmd(argv, **kwargs)
                    inspected["applied"] = (kwargs["cwd"] / "README.md").read_bytes()
                    self.assertTrue(real_cmd(["git", "diff", "--cached", "--name-only"], cwd=kwargs["cwd"]))
                    return result
                if argv[:2] == ["git", "push"]:
                    self.assertIn("--force-with-lease=refs/heads/local-autodev/issue-3:", argv)
                    return ""
                if argv[:3] == ["gh", "pr", "create"]:
                    self.assertIn("--draft", argv)
                    return "https://github.com/example/fixture/pull/1"
                if argv[:3] == ["git", "worktree", "remove"]:
                    target = Path(argv[-1])
                    inspected["before_cleanup"] = (target / "README.md").read_bytes()
                    inspected["index_before_cleanup"] = real_cmd(
                        ["git", "diff", "--cached", "--name-only"], cwd=target)
                if argv[0] != "git":
                    raise AssertionError("Unexpected external/candidate execution")
                return real_cmd(argv, **kwargs)

            with patch.object(runner, "ensure_auth"), patch.object(runner, "verify_no_candidate") as candidate_check, \
                 patch.object(runner.Path, "home", return_value=home), \
                 patch.object(runner, "cmd", side_effect=fake), redirect_stdout(StringIO()) as progress:
                if outcome == "success":
                    runner.run(repo, 3, execute=True)
                else:
                    with self.assertRaises(RuntimeError) as raised:
                        runner.run(repo, 3, execute=True)
                    self.assertNotIn("PRIVATE", str(raised.exception))
            self.assertNotIn("PRIVATE", progress.getvalue())
            self.assertEqual(candidate_check.call_count, 2 if outcome == "success" else 1)
            self.assertEqual(sum(a[:2] == ["codex", "exec"] for a in calls), 1)
            self.assertEqual(calls[-1][:3], ["git", "worktree", "remove"])
            self.assertEqual(real_cmd(["git", "rev-parse", "HEAD"], cwd=repo), base)
            self.assertEqual(real_cmd(["git", "status", "--porcelain"], cwd=repo), "")
            publishes = [a for a in calls if a[:2] == ["git", "push"] or a[:3] == ["gh", "pr", "create"]]
            diagnostics = list((home / ".local/state/footprint-local-autodev/diagnostics").glob("*.json"))
            if outcome == "success":
                self.assertEqual(len(publishes), 2)
                self.assertIn("日本語 PRIVATE_SOURCE".encode(), inspected["applied"])
                self.assertFalse(diagnostics)
            else:
                self.assertFalse(publishes)
                self.assertFalse(any("commit" in a or a[:2] == ["git", "switch"] for a in calls))
                self.assertEqual(inspected["index_before_cleanup"], "")
                self.assertEqual(inspected["before_cleanup"],
                                 b"PRIVATE_DIRTY" if outcome == "dirty" else b"alpha\nbeta\nrepeat repeat\n")
                if outcome == "no_changes":
                    self.assertFalse(diagnostics)
                    self.assertFalse(any("patch_guard.py" in " ".join(a) for a in calls))
                    return
                self.assertEqual(len(diagnostics), 1)
                text = diagnostics[0].read_text(encoding="utf-8")
                self.assertNotIn("PRIVATE", text)
                diagnostic = json.loads(text)
                self.assertEqual(diagnostic["issue"], 3)
                self.assertEqual(diagnostic["base_sha"], base)
                self.assertEqual(diagnostics[0].stat().st_mode & 0o777, 0o600)
                stages = {"model_failure": "model_execution", "invalid_json": "response_validation",
                          "invalid_encoding": "response_validation", "guard_rejection": "patch_validation"}
                self.assertEqual(diagnostic["stage"], stages.get(outcome, "edit_resolution"))
                self.assertRegex(diagnostic["input_sha256"], r"^[0-9a-f]{64}$")
                self.assertGreater(diagnostic["input_bytes"], 0)
                if outcome not in ("model_failure", "guard_rejection"):
                    self.assertEqual(diagnostic["input_sha256"], hashlib.sha256(response).hexdigest())
                    self.assertEqual(diagnostic["input_bytes"], len(response))

    def test_model_file_through_converter_and_real_guard_then_publish(self):
        self.exercise("success")

    def test_invalid_json_never_publishes(self):
        self.exercise("invalid_json")

    def test_invalid_encoding_never_publishes(self):
        self.exercise("invalid_encoding")

    def test_missing_match_never_publishes(self):
        self.exercise("missing_match")

    def test_ambiguous_match_never_publishes(self):
        self.exercise("ambiguous")

    def test_overlapping_edits_never_publish(self):
        self.exercise("overlap")

    def test_dirty_candidate_stops_before_application(self):
        self.exercise("dirty")

    def test_real_guard_failure_prevents_publication(self):
        self.exercise("guard_rejection")

    def test_model_failure_has_correlated_private_diagnostic(self):
        self.exercise("model_failure")

    def test_structured_no_changes_stops_without_guard_or_publication(self):
        self.exercise("no_changes")

    def test_converter_protocol_rejects_secret_fields_and_invalid_types(self):
        valid = {"schema_version": 1, "command": "structured_edits", "stage": "edit_resolution",
                 "reason": "ambiguous_match", "input_sha256": "a" * 64, "input_bytes": 10,
                 "operation": 1, "match_count": 2}
        self.assertEqual(runner.safe_edit_failure(json.dumps(valid))["reason"], "ambiguous_match")
        for bad in ({**valid, "secret": "PRIVATE"}, {**valid, "reason": "PRIVATE"},
                    {**valid, "stage": "PRIVATE"}, {**valid, "operation": True},
                    {**valid, "input_sha256": "PRIVATE"}, {**valid, "input_bytes": -1}):
            self.assertEqual(runner.safe_edit_failure(json.dumps(bad))["reason"], "diagnostic_unavailable")
        self.assertEqual(runner.safe_edit_failure('{"command":"PRIVATE","command":"structured_edits"}')["reason"],
                         "diagnostic_unavailable")

    def test_converter_protocol_constants_match_the_producer(self):
        converter_spec = importlib.util.spec_from_file_location("converter_protocol_test", ROOT / ".github/scripts/structured_edits.py")
        converter = importlib.util.module_from_spec(converter_spec)
        converter_spec.loader.exec_module(converter)
        self.assertEqual(runner.EDIT_STAGES, converter.STAGES)
        self.assertEqual(runner.EDIT_REASONS, converter.FAILURE_REASONS)


if __name__ == "__main__":
    unittest.main()
