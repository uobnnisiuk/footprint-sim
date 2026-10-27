#!/usr/bin/env python3
"""Choose one owner-approved Local AutoDev Issue, without scheduling AI usage.

Dry run is the default. The sole opt-in execution delegates to the existing
single-Issue runner. No retries, daemons, auto-merge, or API-key fallback.
"""
from __future__ import annotations

import argparse
import json
from pathlib import Path
import subprocess
import sys

OWNER = "uobnnisiuk"
REPO = "uobnnisiuk/footprint-sim"
LOCAL_LABEL = "local-autodev"
PAID_LABEL = "autodev"
DEFAULT_PATH = Path(__file__).resolve().parents[1]


def issue_is_eligible(issue: object) -> bool:
    if not isinstance(issue, dict):
        return False
    if issue.get("state") not in ("OPEN", "open"):
        return False  # gh issue list --state open only returns open Issues
    author = issue.get("author")
    if not isinstance(author, dict) or author.get("login") != OWNER:
        return False
    number = issue.get("number")
    if type(number) is not int or number <= 0:
        return False
    labels = issue.get("labels")
    if not isinstance(labels, list):
        return False
    names = {label.get("name") for label in labels if isinstance(label, dict)}
    return LOCAL_LABEL in names and PAID_LABEL not in names


def eligible_queue(issues: object) -> list[dict]:
    if not isinstance(issues, list):
        raise ValueError("Unexpected GitHub Issue list; refusing to act")
    ordered = sorted((i for i in issues if issue_is_eligible(i)),
                     key=lambda issue: issue["number"])
    if len({issue["number"] for issue in ordered}) != len(ordered):
        raise ValueError("Duplicate Issue numbers in response")
    return ordered


def list_issues(repo_path: Path) -> list[dict]:
    result = subprocess.run(
        ["gh", "issue", "list", "--repo", REPO, "--state", "open",
         "--label", LOCAL_LABEL, "--limit", "200", "--json",
         "number,title,author,labels,state"],
        cwd=repo_path, text=True, capture_output=True, timeout=60, check=False)
    if result.returncode:
        raise RuntimeError(f"GitHub Issue listing failed (exit={result.returncode}); "
                           "raw CLI output was withheld")
    return eligible_queue(json.loads(result.stdout))


def run_one(repo_path: Path, issue_number: int) -> int:
    # Delegate all account, branch, patch and publication safeguards to the
    # already-tested single-task runner, without direct Codex invocation here.
    result = subprocess.run(
        [sys.executable, str(repo_path / "tools" / "local_autodev.py"),
         "--repo-path", str(repo_path), "--issue", str(issue_number), "--execute"],
        cwd=repo_path, check=False)
    return result.returncode


def execute_queue(repo_path: Path, queue: list[dict], *,
                  execute: bool, acknowledge_credit_risk: bool,
                  max_issues: int) -> None:
    # Until each Draft PR is reviewed, one new candidate per invocation.
    # A larger batch or unattended timer would need a separate safety review.
    if max_issues != 1:
        raise ValueError("This MVP has a hard limit of 1 Issue per invocation")
    if execute and not acknowledge_credit_risk:
        raise ValueError("--execute also requires --acknowledge-credit-risk")
    if not queue:
        print("No eligible open owner-authored local-autodev Issues.")
        return
    first = queue[0]
    issue_number = first["number"]
    title = first.get("title") or "(untitled)"
    print(f"Eligible Issues: {len(queue)}. Next: #{issue_number} — {title}", flush=True)
    if not execute:
        print("DRY RUN: no Codex invocation or GitHub writes.", flush=True)
        print("To run one Issue, use --execute --acknowledge-credit-risk.", flush=True)
        return
    print("One ChatGPT-plan Codex execution authorized; included allowance and "
          "existing purchased credits may be consumed.", flush=True)
    status = run_one(repo_path, issue_number)
    if status:
        raise RuntimeError(f"Single-Issue runner stopped (exit={status}); "
                           "no retry or next Issue")
    print("One candidate run finished. Review the Draft PR and CI before "
          "authorizing another Issue. No auto-merge.", flush=True)


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--repo-path", type=Path, default=DEFAULT_PATH)
    parser.add_argument("--execute", action="store_true",
                        help="Explicitly authorize one Codex task")
    parser.add_argument("--acknowledge-credit-risk", action="store_true",
                        help="Confirm that existing ChatGPT credits may be consumed")
    parser.add_argument("--max-issues", type=int, default=1,
                        help="Hard-capped at 1 in this safety-first MVP")
    args = parser.parse_args()
    try:
        if args.max_issues != 1:
            raise ValueError("--max-issues must be 1 in this version")
        if args.execute and not args.acknowledge_credit_risk:
            raise ValueError("Explicit --acknowledge-credit-risk is required")
        repo_path = args.repo_path.expanduser().resolve(strict=True)
        if not repo_path.is_dir() or not (repo_path / "tools" / "local_autodev.py").is_file():
            raise ValueError("Expected trusted local-auto-dev checkout was not found")
        queue = list_issues(repo_path)
        execute_queue(repo_path, queue, execute=args.execute,
                      acknowledge_credit_risk=args.acknowledge_credit_risk,
                      max_issues=args.max_issues)
        return 0
    except (OSError, ValueError, RuntimeError, subprocess.TimeoutExpired,
            json.JSONDecodeError) as error:
        print(f"Stopped: {error}", file=sys.stderr)
        return 1


if __name__ == "__main__":
    raise SystemExit(main())
