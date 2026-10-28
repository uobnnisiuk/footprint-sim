#!/usr/bin/env python3
"""Read-only, deterministic planning evidence snapshot. No model or GitHub writes."""
from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path
import re
import subprocess
import sys

sys.dont_write_bytecode = True

REPO = "uobnnisiuk/footprint-sim"
DOCS = (
    "AGENTS.md",
    "README.md",
    "docs/architecture.md",
    "docs/model.md",
    "docs/m1a-evaluation.md",
)
MAX_DOC_BYTES = 65536
MAX_ISSUES = 100
MAX_PRS = 50
MAX_GH_OUTPUT = 2_000_000
SHA_PATTERN = re.compile(r"^[0-9a-f]{40}$")
ACCEPTED_ORIGINS = {
    f"https://github.com/{REPO}",
    f"https://github.com/{REPO}.git",
    f"git@github.com:{REPO}.git",
    f"ssh://git@github.com/{REPO}.git",
}


def command(argv: list[str], root: Path, *, timeout: int = 60) -> str:
    result = subprocess.run(
        argv, cwd=root, text=True, capture_output=True,
        timeout=timeout, check=False,
    )
    if result.returncode != 0:
        raise ValueError(f"Read-only {argv[0]} query failed (exit {result.returncode})")
    if len(result.stdout) > MAX_GH_OUTPUT:
        raise ValueError("Query result exceeds bounded output size")
    return result.stdout.strip()


def git(root: Path, *args: str) -> str:
    return command(
        ["git", "-c", "core.hooksPath=/dev/null", "-c",
         "core.fsmonitor=false", *args], root
    )


def verify_root(root: Path) -> str:
    actual = Path(git(root, "rev-parse", "--show-toplevel")).resolve()
    if actual != root.resolve():
        raise ValueError("Planner requires the repository root")
    branch = git(root, "branch", "--show-current")
    if branch not in ("main", ""):
        raise ValueError("Planner requires a main or detached checkout")
    head = git(root, "rev-parse", "HEAD")
    base = git(root, "rev-parse", "refs/remotes/origin/main")
    if not SHA_PATTERN.fullmatch(head) or head != base:
        raise ValueError("Planner requires exact local origin/main checkout")
    status = git(root, "status", "--porcelain=v1", "--untracked-files=all")
    if status:
        raise ValueError("Planner refuses a checkout with file changes")
    for option in (("--all",), ("--push", "--all")):
        remotes = git(root, "remote", "get-url", *option, "origin").splitlines()
        if not remotes or any(item not in ACCEPTED_ORIGINS for item in remotes):
            raise ValueError("Planner requires an exact trusted GitHub origin")
    return head


def evidence_files(root: Path) -> list[dict]:
    results: list[dict] = []
    for relative in DOCS:
        file = root / relative
        if file.is_symlink() or not file.is_file():
            raise ValueError(f"Missing or symlinked planning input: {relative}")
        data = file.read_bytes()
        if not data or len(data) > MAX_DOC_BYTES:
            raise ValueError(f"Missing or oversized planning input: {relative}")
        try:
            content = data.decode("utf-8")
        except UnicodeDecodeError:
            raise ValueError(f"Invalid UTF-8 input: {relative}") from None
        results.append({
            "path": relative,
            "sha256": hashlib.sha256(data).hexdigest(),
            "content": content,
            "content_trust": "repository_text_to_review",
        })
    return results


def validated_issues(payload: object) -> list[dict]:
    if not isinstance(payload, list) or len(payload) >= MAX_ISSUES:
        raise ValueError("Issue listing invalid or truncated")
    results: list[dict] = []
    seen: set[int] = set()
    for item in payload:
        if not isinstance(item, dict) or type(item.get("number")) is not int:
            raise ValueError("Invalid issue metadata")
        number = item["number"]
        if number < 1 or number in seen:
            raise ValueError("Duplicate or invalid Issue number")
        seen.add(number)
        title, state = item.get("title"), item.get("state")
        author, labels = item.get("author"), item.get("labels")
        if (not isinstance(title, str) or not title.strip() or len(title) > 500
                or state not in ("OPEN", "CLOSED")
                or not isinstance(author, dict)
                or not isinstance(author.get("login"), str)
                or not isinstance(labels, list)):
            raise ValueError("Malformed Issue metadata")
        names = []
        for label in labels:
            if not isinstance(label, dict) or not isinstance(label.get("name"), str):
                raise ValueError("Invalid Issue label")
            names.append(label["name"])
        results.append({
            "number": number,
            "title": title,
            "state": state,
            "author": author["login"],
            "labels": sorted(set(names)),
            "content_trust": "untrusted_github_issue_metadata",
        })
    return sorted(results, key=lambda x: x["number"])


def validated_prs(payload: object) -> list[dict]:
    if not isinstance(payload, list) or len(payload) >= MAX_PRS:
        raise ValueError("PR listing invalid or truncated")
    seen: set[int] = set()
    results: list[dict] = []
    for item in payload:
        if (not isinstance(item, dict)
                or type(item.get("number")) is not int
                or not isinstance(item.get("title"), str)
                or not isinstance(item.get("headRefName"), str)
                or not isinstance(item.get("mergedAt"), str)):
            raise ValueError("Malformed merged PR metadata")
        number = item["number"]
        if number < 1 or number in seen:
            raise ValueError("Duplicate or invalid merged PR number")
        seen.add(number)
        results.append({
            "number": number,
            "title": item["title"],
            "head_ref": item["headRefName"],
            "merged_at": item["mergedAt"],
            "content_trust": "untrusted_github_pr_metadata",
        })
    return sorted(results, key=lambda x: x["number"])


def read_list(root: Path, kind: str, limit: int, fields: str) -> object:
    raw = command(
        ["gh", kind, "list", "--repo", REPO, "--state",
         "all" if kind == "issue" else "merged",
         "--limit", str(limit), "--json", fields],
        root,
    )
    try:
        return json.loads(raw)
    except json.JSONDecodeError:
        raise ValueError(f"Malformed read-only {kind} listing") from None


def snapshot(root: Path, milestone: str) -> dict:
    if not re.fullmatch(r"M[0-9]+-[A-Z][a-zA-Z0-9-]{0,20}", milestone):
        raise ValueError("Explicit bounded milestone (e.g. M1-B) required")
    head = verify_root(root)
    files = evidence_files(root)
    issues = validated_issues(
        read_list(root, "issue", MAX_ISSUES, "number,title,state,author,labels")
    )
    prs = validated_prs(
        read_list(root, "pr", MAX_PRS, "number,title,headRefName,mergedAt")
    )
    # Never make GitHub writes, invoke Codex, or assign a local-autodev label.
    return {
        "schema_version": 1,
        "repository": REPO,
        "main_sha": head,
        "target_milestone": milestone,
        "documents": files,
        "issues": issues,
        "merged_prs": prs,
        "policy": {
            "mode": "proposal_evidence_only",
            "max_proposals": 3,
            "auto_issue_creation": False,
            "auto_queue_promotion": False,
            "model_invocation": False,
            "api_key_billing": False,
        },
    }


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument(
        "--repo-path", type=Path, default=Path(__file__).resolve().parents[1]
    )
    parser.add_argument("--milestone", required=True)
    args = parser.parse_args()
    try:
        payload = snapshot(args.repo_path.expanduser().resolve(strict=True), args.milestone)
        print(json.dumps(payload, ensure_ascii=False, sort_keys=True,
                         separators=(",", ":")))
        return 0
    except (OSError, ValueError, subprocess.TimeoutExpired) as error:
        print(f"Planner stopped: {error}", file=sys.stderr)
        return 1


if __name__ == "__main__":
    raise SystemExit(main())
