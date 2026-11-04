#!/usr/bin/env python3
"""R5a GitHub-only, GET-only diagnostic evidence reader.

This module NEVER authorizes candidate publication, merge or rollout. In
particular, its injected reader is a *test seam*, not a trust attestation.
GitHub API records cannot establish owner approval, research acceptance,
Windows/WSL ledger integrity, or independent reviewer identity.
"""
from __future__ import annotations

import json
import re
from urllib import error, parse, request

REPO = "uobnnisiuk/footprint-sim"
API = "https://api.github.com/repos/" + REPO + "/"
SCHEMA = "footprint-r5a-github-only-v1"
_SHA = re.compile(r"[0-9a-f]{40}\Z")
_ROUTE = re.compile(
    r"(?:git/ref/heads/main|pulls/[1-9][0-9]*(?:/(?:reviews|comments))?"
    r"|actions/workflows/ci\.yml/runs|actions/runs/[1-9][0-9]*/jobs"
    r"|commits/[0-9a-f]{40}/status|branches/main/protection|rulesets)\Z"
)
_PAGE_SIZE = 100
_MAX_PAGES = 10
_MAX_JSON = 4_000_000
_SAFE_REASONS = frozenset((
    "authentication_unavailable", "disallowed_endpoint", "github_unavailable",
    "malformed_api", "pagination_incomplete", "main_unverified",
    "pr_unverified", "ci_missing", "ci_unverified", "ci_not_success",
    "ci_pr_unverified", "review_unverified", "changes_requested",
    "review_threads_unverified", "rulesets_require_independent_check",
    "protection_unverified", "formal_approval_unverified", "invalid_pr_number",
    "base_outdated", "snapshot_changed",
))


class AuditHold(ValueError):
    """Only fixed, non-sensitive reason codes leave this component."""


def _require(condition: bool, code: str) -> None:
    if not condition:
        raise AuditHold(code)


def _sha(value: object) -> bool:
    return type(value) is str and _SHA.fullmatch(value) is not None


def _object(value: object) -> dict:
    _require(type(value) is dict, "malformed_api")
    return value


def _list(value: object) -> list:
    _require(type(value) is list, "malformed_api")
    return value


def _no_duplicate_keys(pairs):
    out = {}
    for key, value in pairs:
        _require(key not in out, "malformed_api")
        out[key] = value
    return out


def _no_constant(_):
    raise AuditHold("malformed_api")


class _DenyRedirects(request.HTTPRedirectHandler):
    def redirect_request(self, req, fp, code, msg, headers, newurl):
        return None


class GitHubReader:
    """Pinned GitHub REST transport. No arbitrary URL, redirect or write."""

    def __init__(self, token: str, opener=None):
        _require(type(token) is str and bool(token.strip()),
                 "authentication_unavailable")
        self._token = token
        self._opener = opener or request.build_opener(
            request.ProxyHandler({}), _DenyRedirects())

    def _validate_endpoint(self, route: str, params: dict | None) -> dict:
        _require(type(route) is str and _ROUTE.fullmatch(route) is not None,
                 "disallowed_endpoint")
        _require(params is None or type(params) is dict, "disallowed_endpoint")
        allowed = {"per_page", "page", "head_sha"}
        params = params or {}
        _require(set(params).issubset(allowed), "disallowed_endpoint")
        for key, value in params.items():
            if key == "head_sha":
                _require(_sha(value), "disallowed_endpoint")
            else:
                _require(type(value) is int and 1 <= value <= 100,
                         "disallowed_endpoint")
        return params

    def get(self, route: str, params: dict | None = None):
        params = self._validate_endpoint(route, params)
        suffix = "?" + parse.urlencode(params) if params else ""
        url = API + route + suffix
        req = request.Request(url, headers={
            "Accept": "application/vnd.github+json",
            "Authorization": "Bearer " + self._token,
            "X-GitHub-Api-Version": "2022-11-28",
        }, method="GET")
        try:
            with self._opener.open(req, timeout=15) as response:
                _require(response.geturl() == url, "github_unavailable")
                _require(response.status == 200, "github_unavailable")
                body = response.read(_MAX_JSON + 1)
        except (error.HTTPError, error.URLError, OSError, TimeoutError):
            raise AuditHold("github_unavailable") from None
        _require(len(body) <= _MAX_JSON, "github_unavailable")
        try:
            return json.loads(body.decode("utf-8"),
                              object_pairs_hook=_no_duplicate_keys,
                              parse_constant=_no_constant)
        except (ValueError, UnicodeError, RecursionError):
            raise AuditHold("malformed_api") from None


def _pages(reader, route: str, key: str | None = None, filters=None) -> list:
    """Require complete pagination, including the page after a full 100."""
    gathered = []
    total = None
    ids = set()
    for page in range(1, _MAX_PAGES + 1):
        params = {"per_page": _PAGE_SIZE, "page": page}
        if filters:
            params.update(filters)
        raw = reader.get(route, params)
        if key:
            data = _object(raw)
            count = data.get("total_count")
            _require(type(count) is int and count >= 0, "pagination_incomplete")
            if total is None:
                total = count
            _require(total == count, "pagination_incomplete")
            items = _list(data.get(key))
        else:
            items = _list(raw)
        _require(len(items) <= _PAGE_SIZE, "pagination_incomplete")
        for item in items:
            obj = _object(item)
            identifier = obj.get("id")
            _require(type(identifier) is int and identifier > 0
                     and identifier not in ids, "pagination_incomplete")
            ids.add(identifier)
            gathered.append(obj)
        if total is not None:
            _require(len(gathered) <= total, "pagination_incomplete")
            if len(gathered) == total:
                return gathered
        if len(items) < _PAGE_SIZE:
            _require(total is None or len(gathered) == total,
                     "pagination_incomplete")
            return gathered
    raise AuditHold("pagination_incomplete")


def _main(reader) -> str:
    value = _object(reader.get("git/ref/heads/main"))
    obj = _object(value.get("object"))
    head = obj.get("sha")
    _require(obj.get("type") == "commit" and _sha(head),
             "main_unverified")
    return head


def _pr(reader, number: int) -> tuple[str, str, str, str]:
    p = _object(reader.get("pulls/" + str(number)))
    base = _object(p.get("base"))
    head = _object(p.get("head"))
    _require(p.get("number") == number and p.get("state") == "open"
             and p.get("merged") is False, "pr_unverified")
    _require(base.get("ref") == "main"
             and _object(base.get("repo")).get("full_name") == REPO
             and _object(head.get("repo")).get("full_name") == REPO,
             "pr_unverified")
    head_sha, base_sha = head.get("sha"), base.get("sha")
    _require(_sha(head_sha) and _sha(base_sha), "pr_unverified")
    author = _object(p.get("user")).get("login")
    branch = head.get("ref")
    _require(type(author) is str and bool(author)
             and type(branch) is str and bool(branch)
             and len(branch) <= 255 and not branch.startswith("refs/"),
             "pr_unverified")
    return head_sha, base_sha, author, branch


def _run_for_pr(run: dict, number: int, head: str, branch: str) -> bool:
    # The same head SHA may appear in runs for different PRs. A SHA alone,
    # the workflow path, or the run's event do NOT bind CI to the target PR.
    if (run.get("head_sha") != head
            or run.get("path") != ".github/workflows/ci.yml"
            or run.get("event") != "pull_request"):
        return False
    _require(run.get("head_branch") == branch
             and _object(run.get("head_repository")).get("full_name") == REPO,
             "ci_pr_unverified")
    links = _list(run.get("pull_requests"))
    _require(len(links) == 1, "ci_pr_unverified")
    linked = _object(links[0])
    if linked.get("number") != number:
        return False
    linked_head = _object(linked.get("head"))
    linked_base = _object(linked.get("base"))
    _require(linked_head.get("sha") == head
             and linked_head.get("ref") == branch
             and _object(linked_head.get("repo")).get("url")
             == "https://api.github.com/repos/" + REPO
             and linked_base.get("ref") == "main"
             and _object(linked_base.get("repo")).get("url")
             == "https://api.github.com/repos/" + REPO,
             "ci_pr_unverified")
    return True


def _ci(reader, number: int, head: str, branch: str) -> int:
    runs = _pages(reader, "actions/workflows/ci.yml/runs",
                  "workflow_runs", {"head_sha": head})
    matched = [r for r in runs if _run_for_pr(r, number, head, branch)]
    _require(bool(matched), "ci_missing")
    # A newer failing/pending run supersedes an older successful run.
    latest = max(matched, key=lambda r: r.get("id", 0))
    run_id = latest.get("id")
    _require(type(run_id) is int and run_id > 0, "ci_unverified")
    _require(latest.get("status") == "completed"
             and latest.get("conclusion") == "success", "ci_not_success")
    jobs = _pages(reader, "actions/runs/" + str(run_id) + "/jobs", "jobs")
    _require(bool(jobs) and any(j.get("name") == "test" for j in jobs),
             "ci_unverified")
    for job in jobs:
        _require(job.get("run_id") == run_id
                 and job.get("status") == "completed"
                 and job.get("conclusion") == "success", "ci_unverified")
        steps = _list(job.get("steps"))
        _require(bool(steps), "ci_unverified")
        for step in steps:
            _require(type(step) is dict and step.get("status") == "completed"
                     and step.get("conclusion") == "success", "ci_unverified")
    legacy = _object(reader.get("commits/" + head + "/status"))
    _require(legacy.get("sha") == head, "ci_unverified")
    count = legacy.get("total_count")
    _require(type(count) is int and count >= 0, "ci_unverified")
    statuses = _list(legacy.get("statuses"))
    # Combined commit statuses are not GitHub Actions check runs.
    # No legacy statuses is normal, but an incomplete or failing set is not.
    _require(len(statuses) == count, "ci_unverified")
    if count:
        _require(legacy.get("state") == "success"
                 and all(s.get("state") == "success" for s in statuses),
                 "ci_not_success")
    return run_id


def _reviews(reader, number: int, head: str, author: str) -> str:
    reviews = _pages(reader, "pulls/" + str(number) + "/reviews")
    latest = {}
    for r in reviews:
        actor = _object(r.get("user")).get("login")
        state = r.get("state")
        _require(type(actor) is str and bool(actor)
                 and state in ("APPROVED", "CHANGES_REQUESTED",
                               "COMMENTED", "DISMISSED", "PENDING"),
                 "review_unverified")
        if state in ("APPROVED", "CHANGES_REQUESTED", "DISMISSED"):
            latest[actor] = (state, r.get("commit_id"))
    _require(not any(state == "CHANGES_REQUESTED"
                     for state, _ in latest.values()), "changes_requested")
    # Formal APPROVED is still not proof of independent acceptance review.
    present = any(actor != author and state == "APPROVED"
                  and commit == head
                  for actor, (state, commit) in latest.items())
    comments = _pages(reader, "pulls/" + str(number) + "/comments")
    # REST inline comment APIs cannot establish the resolved state of
    # threads. Even a resolved GitHub thread is HOLD until independently
    # authenticated by a reader that can establish its current state.
    _require(not comments, "review_threads_unverified")
    return "current_head_formal_observed" if present else "not_observed"


def _protection(reader, formal: str) -> None:
    rulesets = _list(reader.get("rulesets"))
    _require(not rulesets, "rulesets_require_independent_check")
    p = _object(reader.get("branches/main/protection"))
    checks = _object(p.get("required_status_checks"))
    contexts = checks.get("contexts")
    _require(type(contexts) is list and "test" in contexts,
             "protection_unverified")
    reviews = p.get("required_pull_request_reviews")
    if reviews is not None:
        count = _object(reviews).get("required_approving_review_count")
        _require(type(count) is int and count >= 0,
                 "protection_unverified")
        if count:
            # A single observation is NOT sufficient for multiple approvals.
            _require(count == 1 and formal == "current_head_formal_observed",
                     "formal_approval_unverified")


def inspect_pr(reader, pr_number: int) -> dict:
    """Sanitized diagnostic. Never returns any operational permission."""
    out = {
        "schema": SCHEMA, "repository": REPO,
        "pr": pr_number if type(pr_number) is int else None,
        "state": "HOLD", "reason": "unverified",
        "head": None, "main": None, "ci": "unknown",
        "ci_run_id": None, "formal_review": "unknown",
        "review_threads": "unknown", "protection": "unknown",
        "original_approval": "unknown", "research_acceptance": "unknown",
        "saved_tasks": "unknown", "windows_wsl": "unknown",
        "shared_ledger": "unknown", "shadow": "unverified",
        "may_publish": False, "may_activate": False,
        "production_enabled": False,
    }
    try:
        _require(type(pr_number) is int and pr_number > 0,
                 "invalid_pr_number")
        main = _main(reader)
        head, base, author, branch = _pr(reader, pr_number)
        out.update(main=main, head=head)
        _require(base == main, "base_outdated")
        out["ci_run_id"] = _ci(reader, pr_number, head, branch)
        out["ci"] = "head_ci_success"
        out["formal_review"] = _reviews(reader, pr_number, head, author)
        out["review_threads"] = "no_inline_threads_observed"
        _protection(reader, out["formal_review"])
        out["protection"] = "read_only_policy_observed"
        # A change while reading invalidates the entire snapshot.
        _require(_main(reader) == main
                 and _pr(reader, pr_number) == (head, base, author, branch),
                 "snapshot_changed")
        out["state"] = "GITHUB_ONLY_SNAPSHOT"
        out["reason"] = "production_and_acceptance_not_verified"
    except AuditHold as exc:
        # An injected reader may raise an AuditHold with arbitrary text.
        # Never echo its message unless it is one of our fixed reason codes.
        reason = str(exc)
        out["reason"] = reason if reason in _SAFE_REASONS else "unverified"
    except (TypeError, ValueError, KeyError, IndexError, AttributeError):
        out["reason"] = "malformed_api"
    except (OSError, TimeoutError):
        out["reason"] = "github_unavailable"
    return out
