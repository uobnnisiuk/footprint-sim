#!/usr/bin/env python3
"""Bounded GET inventory with explicit coverage, never execution authority.

This composes the existing pinned GitHubEvidenceReader transport. The small
page walker additionally retains partial pages and coverage, and supports
GitHub's SHA/node identities for commits/timeline records; R5a's ID-only
``_pages`` cannot express those results. No file, lock, process, credential
discovery, CI dispatch or write operation exists here. A complete *visible*
inventory cannot establish a deleted or unposted legacy execution history.
"""
from __future__ import annotations

from copy import deepcopy
from dataclasses import asdict, dataclass, replace
from datetime import datetime, timezone
import hashlib
import json
import re
from typing import ClassVar

import pr_repair_remote_audit as remote

PAGE_SIZE = remote._PAGE_SIZE
MAX_PAGES = remote._MAX_PAGES
MAX_PULLS = 200
MAX_RECORDS = 20000
MAX_SOURCE_BYTES = 32_000_000
MAX_RECORD_BYTES = 256_000
_NUMBER = re.compile(r"[1-9][0-9]*\Z")
_SHA = re.compile(r"[0-9a-f]{40}\Z")


class SourceHold(ValueError):
    """Fixed reasons only; source text and exceptions never cross this boundary."""


@dataclass(frozen=True)
class SourceRecord:
    kind: str
    record_id: str
    parent_number: int | None
    actor_id: int | None
    created_at: str | None
    updated_at: str | None
    observed_at: str
    content_sha256: str
    verification: str
    reasons: tuple[str, ...]
    repository_id: int
    endpoint: str
    # Not a dataclass field: repr/asdict/JSON diagnostics cannot expose bodies,
    # URLs, actor names or other arbitrary source strings. This is untrusted
    # input for the source-specific reconciler, not authenticated authority.
    _payload: ClassVar[dict | None] = None

    def to_dict(self):
        return asdict(self)


@dataclass(frozen=True)
class CoverageManifest:
    endpoint: str
    pages: int
    terminal: bool
    count: int
    content_sha256: str
    reread: str
    reread_pages: int
    reread_terminal: bool
    reread_count: int
    required_since: str | None
    observed_from: str | None
    observed_to: str | None
    observed_at: str
    missing: tuple[str, ...]

    def to_dict(self):
        return asdict(self)


@dataclass(frozen=True)
class SourceCollection:
    records: tuple[SourceRecord, ...]
    coverage: tuple[CoverageManifest, ...]
    holds: tuple[str, ...]
    visible_inventory_complete: bool
    legacy_execution_history_complete: bool = False
    may_publish: bool = False
    may_activate: bool = False

    def to_dict(self):
        return asdict(self)


def _require(ok, reason):
    if not ok:
        raise SourceHold(reason)


def _positive(value):
    return type(value) is int and value > 0


def _time(value):
    _require(type(value) is str and len(value) <= 40, "source_timestamp_unverified")
    try:
        parsed = datetime.fromisoformat(value.replace("Z", "+00:00"))
    except ValueError:
        raise SourceHold("source_timestamp_unverified") from None
    _require(parsed.tzinfo is not None and parsed.utcoffset() is not None,
             "source_timestamp_unverified")
    try:
        return parsed.astimezone(timezone.utc).isoformat()
    except (ValueError, OverflowError):
        raise SourceHold("source_timestamp_unverified") from None


def _canonical(value):
    try:
        content = json.dumps(value, ensure_ascii=True, sort_keys=True,
                             separators=(",", ":"), allow_nan=False).encode("ascii")
    except (TypeError, ValueError, RecursionError, UnicodeError):
        raise SourceHold("source_record_malformed") from None
    _require(len(content) <= MAX_RECORD_BYTES, "source_bounds_exceeded")
    return content


def _digest(value):
    return hashlib.sha256(value).hexdigest()


def _identity(kind, raw):
    if kind == "commit":
        value = raw.get("sha")
        _require(type(value) is str and _SHA.fullmatch(value), "source_identity_unverified")
        return value
    if _positive(raw.get("id")):
        return str(raw["id"])
    if kind == "timeline":
        # Node IDs are platform record identities, but are opaque and are never
        # copied to diagnostics. Some timeline kinds only expose a commit SHA.
        node = raw.get("node_id")
        if type(node) is str and re.fullmatch(r"[A-Za-z0-9_+/=-]{1,512}", node):
            return "node:" + _digest(node.encode("ascii"))
        sha = raw.get("sha")
        if raw.get("event") == "committed" and type(sha) is str and _SHA.fullmatch(sha):
            return "commit:" + sha
    raise SourceHold("source_identity_unverified")


def _record(kind, raw, parent, repo_id, endpoint, observed):
    _require(type(raw) is dict, "source_record_malformed")
    content = _canonical(raw)
    identifier = _identity(kind, raw)
    if kind in ("issue", "pull"):
        _require(_positive(raw.get("number")), "source_parent_unverified")
        parent = raw["number"]
        noun = "issues" if kind == "issue" else "pulls"
        _require(raw.get("url") == remote.API + noun + "/" + str(parent),
                 "source_parent_unverified")
    elif kind == "issue_comment":
        url = raw.get("issue_url")
        prefix = remote.API + "issues/"
        _require(type(url) is str and url.startswith(prefix)
                 and _NUMBER.fullmatch(url[len(prefix):]), "source_parent_unverified")
        parent = int(url[len(prefix):])
        _require(raw.get("url") == remote.API + "issues/comments/" + identifier,
                 "source_parent_unverified")
    elif kind in ("review", "review_comment"):
        _require(raw.get("pull_request_url") == remote.API + "pulls/" + str(parent),
                 "source_parent_unverified")
    elif kind == "commit":
        _require(raw.get("url") == remote.API + "commits/" + identifier,
                 "source_parent_unverified")
    actor = raw.get("user", raw.get("actor", raw.get("author")))
    actor_id = actor.get("id") if type(actor) is dict else None
    reasons = ()
    if actor_id is not None:
        _require(_positive(actor_id), "source_actor_unverified")
    else:
        reasons = ("source_actor_unavailable",)
    created = raw.get("created_at", raw.get("submitted_at"))
    updated = raw.get("updated_at")
    created = _time(created) if created is not None else None
    updated = _time(updated) if updated is not None else None
    _require(not created or not updated or updated >= created, "source_timestamp_unverified")
    _require(not any(t and t > observed for t in (created, updated)), "source_time_after_cutoff")
    record = SourceRecord(kind, identifier, parent, actor_id, created, updated,
                          observed, _digest(content), "observed", reasons,
                          repo_id, endpoint)
    object.__setattr__(record, "_payload", deepcopy(raw))
    return record, len(content)


def _unverified_record(kind, raw, parent, repo_id, endpoint, observed, reason):
    # Keep evidence of a malformed/unidentified record without giving a derived
    # fingerprint the status of a platform identity, or printing arbitrary data.
    try:
        content = _canonical(raw)
    except SourceHold:
        return None
    record = SourceRecord(kind, "unidentified:" + _digest(content), parent, None,
                          None, None, observed, _digest(content), "identity_unverified",
                          (reason,), repo_id, endpoint)
    object.__setattr__(record, "_payload", deepcopy(raw))
    return record


def _key(record):
    return record.repository_id, record.kind, record.parent_number or 0, record.record_id


def _collection_digest(records):
    values = sorted((_key(r), r.content_sha256) for r in records)
    return _digest(json.dumps(values, separators=(",", ":")).encode("ascii"))


class _Scan:
    def __init__(self, reader, observed, required_since):
        self.reader = reader
        self.observed = observed
        self.required_since = required_since
        self.repo_id = 0
        self.records = []
        self.coverage = {}
        self.holds = set()
        self.bytes = 0

    def _coverage(self, endpoint, records, pages, terminal, missing):
        dates = [t for r in records for t in (r.created_at, r.updated_at) if t]
        self.coverage[endpoint] = CoverageManifest(
            endpoint, pages, terminal, len(records), _collection_digest(records),
            "not_checked", 0, False, 0, self.required_since,
            min(dates) if dates else None, max(dates) if dates else None,
            self.observed, tuple(sorted(missing)))
        self.holds.update(missing)

    def page(self, endpoint, kind, parent=None, filters=None):
        records, seen, missing = [], set(), set()
        pages, terminal = 0, False
        for number in range(1, MAX_PAGES + 1):
            params = {"per_page": PAGE_SIZE, "page": number}
            if filters:
                params.update(filters)
            try:
                raw = self.reader.get(endpoint, params)
            except Exception:
                missing.add("source_endpoint_unavailable")
                break
            pages += 1
            if type(raw) is not list or len(raw) > PAGE_SIZE:
                missing.add("source_page_malformed")
                break
            for item in raw:
                if len(self.records) >= MAX_RECORDS or self.bytes >= MAX_SOURCE_BYTES:
                    missing.add("source_bounds_exceeded")
                    break
                try:
                    record, _ = _record(kind, item, parent, self.repo_id,
                                        endpoint, self.observed)
                except SourceHold as error:
                    missing.add(str(error))
                    record = _unverified_record(kind, item, parent, self.repo_id,
                                                endpoint, self.observed, str(error))
                if record is None:
                    continue
                self.bytes += len(_canonical(item))
                if self.bytes > MAX_SOURCE_BYTES:
                    missing.add("source_bounds_exceeded")
                    break
                key = _key(record)
                if key in seen:
                    missing.add("duplicate_source_identity")
                seen.add(key)
                records.append(record)
                self.records.append(record)
            if len(raw) < PAGE_SIZE:
                terminal = True
                break
            if "source_bounds_exceeded" in missing:
                break
        if not terminal and not missing:
            missing.add("source_page_limit_reached")
        self._coverage(endpoint, records, pages, terminal, missing)
        return records

    def run(self):
        try:
            raw = self.reader.get("")
            _require(type(raw) is dict and raw.get("full_name") == remote.REPO
                     and _positive(raw.get("id")), "source_repository_unverified")
            self.repo_id = raw["id"]
            # Only stable repository identity is relevant. Mutable statistics
            # are neither evidence nor a reason to invalidate this collection.
            root = {"id": self.repo_id, "full_name": remote.REPO}
            digest = _digest(_canonical(root))
            rec = SourceRecord("repository", str(self.repo_id), None, None, None,
                               None, self.observed, digest, "observed", (), self.repo_id, "")
            object.__setattr__(rec, "_payload", root)
            self.records.append(rec)
            self._coverage("", [rec], 1, True, ())
        except Exception:
            self._coverage("", [], 0, False, ("source_repository_unavailable",))
            return self
        issues = self.page("issues", "issue", filters={"state": "all"})
        pulls = self.page("pulls", "pull", filters={"state": "all"})
        comments = self.page("issues/comments", "issue_comment")
        issue_numbers = {r.parent_number for r in issues if r.verification == "observed"}
        pull_numbers = {r.parent_number for r in pulls if r.verification == "observed"}
        if len(pull_numbers) > MAX_PULLS:
            self.holds.add("source_bounds_exceeded")
            return self
        if self.coverage["issues"].terminal and not self.coverage["issues"].missing:
            if any(r.parent_number not in issue_numbers for r in comments):
                self.holds.add("source_parent_missing")
            issue_pulls = {r.parent_number for r in issues
                           if r.verification == "observed" and "pull_request" in r._payload}
            if self.coverage["pulls"].terminal and not self.coverage["pulls"].missing:
                if issue_pulls != pull_numbers:
                    self.holds.add("source_inventory_conflict")
            counts = [r._payload.get("comments") for r in issues if r.verification == "observed"]
            if all(type(n) is int and n >= 0 for n in counts):
                if (self.coverage["issues/comments"].terminal
                        and not self.coverage["issues/comments"].missing
                        and sum(counts) != len(comments)):
                    self.holds.add("source_comment_count_conflict")
            else:
                self.holds.add("source_comment_counts_unavailable")
        for number in sorted(pull_numbers):
            self.page(f"pulls/{number}/reviews", "review", number)
            self.page(f"pulls/{number}/comments", "review_comment", number)
            self.page(f"pulls/{number}/commits", "commit", number)
            self.page(f"issues/{number}/timeline", "timeline", number)
        return self


def collect_github_sources(reader, *, now: datetime,
                           required_since: datetime | None = None) -> SourceCollection:
    """Read every supported visible source twice, retaining partial evidence.

    ``now`` is an explicit observation cutoff, not an inferred execution time.
    Coverage observed_from/to describe source timestamps actually present in
    the returned records. No timestamp-free activity is assigned to a day.
    An injected reader is a test/connector seam, never proof of authentication.
    """
    try:
        _require(isinstance(now, datetime) and now.tzinfo is not None
                 and now.utcoffset() is not None, "invalid_observation_time")
        observed = now.astimezone(timezone.utc).isoformat()
        since = None
        if required_since is not None:
            _require(isinstance(required_since, datetime) and required_since.tzinfo is not None
                     and required_since.utcoffset() is not None and required_since <= now,
                     "invalid_required_period")
            since = required_since.astimezone(timezone.utc).isoformat()
    except (SourceHold, ValueError, TypeError, OverflowError):
        return SourceCollection((), (), ("invalid_observation_period",), False)
    first = _Scan(reader, observed, since).run()
    second = _Scan(reader, observed, since).run()
    holds = first.holds | second.holds | {"legacy_execution_history_unavailable"}
    if since is None:
        holds.add("legacy_required_period_unknown")
    coverage = []
    for endpoint in sorted(first.coverage.keys() | second.coverage.keys()):
        before, after = first.coverage.get(endpoint), second.coverage.get(endpoint)
        if before is None or after is None:
            current = before or after
            coverage.append(replace(current, reread="endpoint_set_changed",
                                    missing=tuple(sorted(set(current.missing) |
                                                         {"source_changed_during_collection"}))))
            holds.add("source_changed_during_collection")
            continue
        stable = before.content_sha256 == after.content_sha256
        complete = before.terminal and after.terminal and not before.missing and not after.missing
        status = "unchanged" if stable and complete else "incomplete" if not complete else "changed"
        missing = set(before.missing) | set(after.missing)
        if not stable:
            missing.add("source_changed_during_collection")
            holds.add("source_changed_during_collection")
        coverage.append(replace(before, reread=status, reread_pages=after.pages,
                                reread_terminal=after.terminal, reread_count=after.count,
                                missing=tuple(sorted(missing))))
    # Retain both observations of an edited/deleted source, not only the newest
    # result. Exact repeated observations are the only safe deduplication here.
    unique = {}
    for record in first.records + second.records:
        unique.setdefault((_key(record), record.content_sha256), record)
    records = tuple(unique[k] for k in sorted(unique))
    inventory_holds = holds - {"legacy_execution_history_unavailable", "legacy_required_period_unknown"}
    complete = bool(coverage) and not inventory_holds and all(
        c.terminal and c.reread_terminal and c.reread == "unchanged" and not c.missing for c in coverage)
    return SourceCollection(records, tuple(coverage), tuple(sorted(holds)), complete)
