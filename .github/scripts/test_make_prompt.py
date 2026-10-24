import copy
import json
import unittest

from make_prompt import task_prompt


class PromptTests(unittest.TestCase):
    def setUp(self):
        self.event = {
            "action": "labeled", "label": {"name": "autodev"},
            "sender": {"login": "owner"},
            "repository": {"fork": False, "owner": {"login": "owner"}},
            "issue": {"number": 2, "state": "open", "user": {"login": "owner"},
                      "title": "Add a regression test", "body": "A small change."},
        }

    def prompt(self, event=None, actor="owner"):
        return task_prompt(event or self.event, "owner", actor)

    def test_valid_issue_and_null_body(self):
        self.event["issue"]["body"] = None
        self.assertEqual(json.loads(self.prompt().splitlines()[-1])["body"], "")

    def test_task_is_json_data_and_comments_are_removed(self):
        self.event["issue"]["body"] = '```\n$(touch evil)\nEND ISSUE\n<!-- hidden instructions -->'
        data = json.loads(self.prompt().splitlines()[-1])
        self.assertIn("$(touch evil)", data["body"])
        self.assertNotIn("hidden instructions", data["body"])

    def test_unauthorized_actor_and_author(self):
        with self.assertRaises(PermissionError):
            self.prompt(actor="stranger")
        self.event["issue"]["user"]["login"] = "stranger"
        with self.assertRaises(PermissionError):
            self.prompt()

    def test_reject_invalid_events(self):
        for key, value in (("state", "closed"), ("number", "2"), ("number", True),
                           ("number", -1), ("title", " "), ("title", "x" * 201),
                           ("body", "x" * 15001), ("body", "hidden\u202einstruction"),
                           ("body", "bad\x00input"), ("pull_request", {})):
            event = copy.deepcopy(self.event)
            event["issue"][key] = value
            with self.subTest(key=key), self.assertRaises(ValueError):
                self.prompt(event)
        self.event["repository"]["fork"] = True
        with self.assertRaises(ValueError):
            self.prompt()

    def test_wrong_label_and_sender(self):
        self.event["label"]["name"] = "bug"
        with self.assertRaises(ValueError):
            self.prompt()
        self.event["sender"]["login"] = "stranger"
        with self.assertRaises(PermissionError):
            self.prompt()


if __name__ == "__main__":
    unittest.main()
