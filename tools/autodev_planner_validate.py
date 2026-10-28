#!/usr/bin/env python3
"""Validate at most three Issue proposals offline; never publish or run AI."""
from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path
import re
import sys
import unicodedata

sys.dont_write_bytecode = True

PROPOSAL_FIELDS = {
    "draft_id", "title", "objective", "scope", "exclusions", "paths",
    "acceptance_tests", "dependencies", "risks", "evidence",
    "target_milestone", "base_sha",
}
ALLOWED_PATH = re.compile(
    r"(?:src|tests|docs|scenarios)/(?:[A-Za-z0-9][A-Za-z0-9._-]*/)*"
    r"[A-Za-z0-9][A-Za-z0-9._-]*\Z"
)
BLOCKED_NAMES = {
    "agents.md", "global.json", "nuget.config", "packages.config",
    "packages.lock.json", "package.json", "package-lock.json", "project.json",
    "project.assets.json", "dotnet-tools.json", "sdk.json",
}
BLOCKED_SUFFIX = {
    ".csproj", ".fsproj", ".vbproj", ".sln", ".slnx", ".slnf",
    ".props", ".targets", ".proj", ".rsp",
}
GENERATED_DIRS = {"results", "bin", "obj", "node_modules"}
SHA_PATTERN = re.compile(r"^[0-9a-f]{40}$")


def read_json_file(path: Path) -> object:
    if path.is_symlink() or not path.is_file():
        raise ValueError("Missing or symlinked proposal input")
    data = path.read_bytes()
    if not data or len(data) > 2_000_000:
        raise ValueError("Empty or oversized proposal input")
    try:
        return json.loads(data.decode("utf-8"))
    except (UnicodeError, json.JSONDecodeError):
        raise ValueError("Invalid JSON/UTF-8 proposal input") from None


def meaningful_text(value: object, what: str) -> str:
    if (not isinstance(value, str) or not 8 <= len(value.strip()) <= 1000
            or any(ord(ch) < 32 and ch not in "\n\t" for ch in value)):
        raise ValueError(f"Missing, short or oversized {what}")
    return value.strip()


def strings(value: object, what: str, *, minimum: int = 1) -> list[str]:
    if not isinstance(value, list) or not minimum <= len(value) <= 20:
        raise ValueError(f"Invalid {what} collection")
    normalized = [meaningful_text(item, what) for item in value]
    if len(set(normalized)) != len(normalized):
        raise ValueError(f"Duplicate {what} entries")
    return normalized


def valid_path(path: str) -> bool:
    if path != "README.md" and not ALLOWED_PATH.fullmatch(path):
        return False
    parts = path.split("/")
    return (not any(part.casefold() in BLOCKED_NAMES for part in parts)
            and not any(part.casefold() in GENERATED_DIRS for part in parts[:-1])
            and Path(path).suffix.casefold() not in BLOCKED_SUFFIX
            and not path.startswith("tools/")
            and not path.startswith(".github/"))


def canonical_title(title: str) -> str:
    return " ".join(unicodedata.normalize("NFKC", title).casefold().split())


def validate(snapshot: object, drafts: object) -> dict:
    if not isinstance(snapshot, dict) or snapshot.get("schema_version") != 1:
        raise ValueError("Invalid planning snapshot")
    if (snapshot.get("repository") != "uobnnisiuk/footprint-sim"
            or not isinstance(snapshot.get("main_sha"), str)
            or not SHA_PATTERN.fullmatch(snapshot["main_sha"])
            or not isinstance(snapshot.get("target_milestone"), str)
            or not isinstance(snapshot.get("documents"), list)
            or not isinstance(snapshot.get("issues"), list)
            or not isinstance(snapshot.get("merged_prs"), list)):
        raise ValueError("Incomplete trusted planning snapshot")
    policy = snapshot.get("policy")
    if (not isinstance(policy, dict)
            or policy.get("mode") != "proposal_evidence_only"
            or policy.get("model_invocation") is not False
            or policy.get("auto_queue_promotion") is not False):
        raise ValueError("Planning snapshot policy mismatch")
    if (not isinstance(drafts, dict) or set(drafts) != {"schema_version", "proposals"}
            or type(drafts["schema_version"]) is not int
            or drafts["schema_version"] != 1
            or not isinstance(drafts["proposals"], list)
            or not 1 <= len(drafts["proposals"]) <= 3):
        raise ValueError("Expected one to three proposal drafts")
    evidence_paths = {
        item["path"] for item in snapshot["documents"]
        if isinstance(item, dict) and isinstance(item.get("path"), str)
    }
    existing_titles: set[str] = set()
    issue_states: dict[int, str] = {}
    for item in snapshot["issues"]:
        if (not isinstance(item, dict) or type(item.get("number")) is not int
                or not isinstance(item.get("title"), str)
                or item.get("state") not in ("OPEN", "CLOSED")):
            raise ValueError("Invalid Issue evidence")
        if item["number"] in issue_states:
            raise ValueError("Duplicate Issue evidence")
        issue_states[item["number"]] = item["state"]
        existing_titles.add(canonical_title(item["title"]))
    for item in snapshot["merged_prs"]:
        if not isinstance(item, dict) or not isinstance(item.get("title"), str):
            raise ValueError("Invalid PR evidence")
        existing_titles.add(canonical_title(item["title"]))

    proposed: dict[str, dict] = {}
    titles: set[str] = set()
    for item in drafts["proposals"]:
        if not isinstance(item, dict) or set(item) != PROPOSAL_FIELDS:
            raise ValueError("Proposal has missing or unauthorized fields")
        ident = item["draft_id"]
        if not isinstance(ident, str) or not re.fullmatch(r"P[1-3]", ident):
            raise ValueError("Invalid proposal draft ID")
        if ident in proposed:
            raise ValueError("Duplicate proposal draft ID")
        title = meaningful_text(item["title"], "title")
        key = canonical_title(title)
        if key in existing_titles or key in titles:
            raise ValueError("Duplicate or already-tracked proposal title")
        titles.add(key)
        meaningful_text(item["objective"], "objective")
        scope = strings(item["scope"], "scope")
        exclusions = strings(item["exclusions"], "exclusions")
        acceptance = strings(item["acceptance_tests"], "acceptance tests", minimum=2)
        risks = strings(item["risks"], "risks")
        paths = strings(item["paths"], "paths")
        if len(paths) > 10 or any(not valid_path(p) for p in paths):
            raise ValueError("Forbidden or excessive proposal file paths")
        evidence = strings(item["evidence"], "source evidence")
        if any(p not in evidence_paths for p in evidence):
            raise ValueError("Unknown source evidence path")
        dependencies = item["dependencies"]
        if (not isinstance(dependencies, list) or len(dependencies) > 10
                or any(not isinstance(dep, str) for dep in dependencies)
                or len(set(dependencies)) != len(dependencies)):
            raise ValueError("Invalid dependencies")
        if item["base_sha"] != snapshot["main_sha"]:
            raise ValueError("Proposal based on stale main SHA")
        if item["target_milestone"] != snapshot["target_milestone"]:
            raise ValueError("Proposal target milestone mismatch")
        proposed[ident] = {
            "draft_id": ident,
            "title": title,
            "objective": item["objective"].strip(),
            "scope": scope, "exclusions": exclusions, "paths": paths,
            "acceptance_tests": acceptance, "risks": risks, "evidence": evidence,
            "dependencies": dependencies,
            "target_milestone": item["target_milestone"],
            "base_sha": item["base_sha"],
            "dedup_key": hashlib.sha256(
                (item["target_milestone"] + "\0" + key).encode("utf-8")
            ).hexdigest(),
        }

    visiting: set[str] = set()
    visited: set[str] = set()

    def visit(ident: str) -> None:
        if ident in visiting:
            raise ValueError("Circular proposal dependencies")
        if ident in visited:
            return
        visiting.add(ident)
        for dependency in proposed[ident]["dependencies"]:
            if re.fullmatch(r"P[1-3]", dependency):
                if dependency not in proposed:
                    raise ValueError("Unknown proposal dependency")
                visit(dependency)
            else:
                match = re.fullmatch(r"issue:([1-9][0-9]*)", dependency)
                if not match or int(match.group(1)) not in issue_states:
                    raise ValueError("Unknown Issue dependency")
        visiting.remove(ident)
        visited.add(ident)

    for ident in sorted(proposed):
        visit(ident)

    result = []
    for ident in sorted(proposed):
        item = proposed[ident]
        blocking = [
            dependency for dependency in item["dependencies"]
            if dependency in proposed or (
                dependency.startswith("issue:") and
                issue_states[int(dependency[6:])] == "OPEN"
            )
        ]
        result.append({**item, "status": "blocked" if blocking else "draft",
                       "blocking_dependencies": blocking})
    return {
        "schema_version": 1,
        "repository": snapshot["repository"],
        "main_sha": snapshot["main_sha"],
        "proposals": result,
        "policy": {
            "dry_run_only": True,
            "github_write": False,
            "model_invocation": False,
            "auto_queue_promotion": False,
        },
    }


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--snapshot", type=Path, required=True)
    parser.add_argument("--drafts", type=Path, required=True)
    args = parser.parse_args()
    try:
        result = validate(
            read_json_file(args.snapshot.expanduser()),
            read_json_file(args.drafts.expanduser()),
        )
        print(json.dumps(result, ensure_ascii=False, sort_keys=True,
                         separators=(",", ":")))
        return 0
    except (ValueError, OSError) as error:
        print(f"Planner proposal rejected: {error}", file=sys.stderr)
        return 1


if __name__ == "__main__":
    raise SystemExit(main())
