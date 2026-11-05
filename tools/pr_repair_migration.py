#!/usr/bin/env python3
"""M2 conditional OFFLINE contract; no authentication or execution authority.

``observations`` is a separate, externally supplied evidence transport, not a
trusted type. This module checks its declared relationships, never authenticates
its issuer. Even a perfectly forged/self-consistent bundle can prove only
conditional consistency here. Real anchor provisioning, signatures/identities,
writer fencing and lock-time reads remain outside this offline contract.

No clocks, readers, files, locks, network, subprocesses or reservation APIs are
called. R3/R4 and the PR #74 diagnostic remain unchanged and disabled.
"""
from __future__ import annotations

from datetime import datetime, timedelta, timezone
import re

import pr_repair_contract as contract
import pr_repair_history as history
import pr_repair_ledger as ledger

SCHEMA = "footprint-m2-offline-v1"
POLICY = "legacy-hold-independent-new-only-m2"
POLICY_VERSION = "M2"
REPOSITORY_ID = 1350243876
JST = timezone(timedelta(hours=9))
FRESH_SECONDS = 300
KINDS = frozenset(("initial", "minor", "substantive", "ci_rerun"))
LEGACY_HOLD = "legacy_minor_history_unverified"
SOURCE_ID = re.compile(r"(?:daily|attempts|pr-repair-ledger)/[A-Za-z0-9_.-]{1,200}\Z")


class MigrationHold(ValueError):
    """Only fixed diagnostics cross this boundary."""


def _need(condition, reason):
    if not condition:
        raise MigrationHold(reason)


def _fields(value, keys):
    return contract.fields(value, keys.split())


def _time(value):
    _need(type(value) is str and len(value) <= 40, "invalid_time")
    try:
        result = datetime.fromisoformat(value)
    except ValueError:
        raise MigrationHold("invalid_time") from None
    _need(result.tzinfo is not None and result.utcoffset() is not None,
          "invalid_time")
    return result.astimezone(JST)


def _digest(value):
    return contract.string(value, contract.DIGEST)


def _sha(value):
    return contract.string(value, contract.SHA)


def _root(value, kind):
    contract.integer(value["root_issue"])
    if kind == "initial":
        _need(value["root_pr"] is None, "invalid_root")
    else:
        contract.integer(value["root_pr"])


def _ids(values):
    contract.vector(values, low=0, high=20000)
    for value in values:
        contract.integer(value)
    contract.unique(values)


def _source(value, now, *, fresh=False):
    _fields(value, "record_id actor_id repo_id observed_at expires_at")
    _need(type(value["repo_id"]) is int and value["repo_id"] == REPOSITORY_ID, "repository_mismatch")
    contract.string(value["record_id"])
    contract.integer(value["actor_id"])
    observed, expiry = _time(value["observed_at"]), _time(value["expires_at"])
    _need(observed <= now < expiry, "evidence_expired_or_future")
    if fresh:
        _need((now - observed).total_seconds() <= FRESH_SECONDS,
              "observation_stale")
    return observed


def _writers(items):
    contract.vector(items, low=3, high=3)
    for item in items:
        _fields(item, "role id deployment_sha")
        _need(item["role"] in ("initial", "repair", "merge"), "writer_mismatch")
        contract.string(item["id"])
        _sha(item["deployment_sha"])
    _need({x["role"] for x in items} == {"initial", "repair", "merge"}
          and len({x["id"] for x in items}) == 3, "multiple_or_missing_writer")
    return contract.digest(sorted(items, key=lambda x: x["role"]))


def _policy(request, obs, now):
    p = _fields(obs["policy"], "source policy version adopted_at owner_id writers legacy_scope")
    _source(p["source"], now)
    _need(p["policy"] == POLICY == request["policy"]
          and p["version"] == request["policy_version"] == POLICY_VERSION, "policy_mismatch")
    contract.string(p["version"])
    contract.integer(p["owner_id"])
    _need(p["owner_id"] == p["source"]["actor_id"], "policy_owner_mismatch")
    _need(_time(p["adopted_at"]) <= now, "policy_not_adopted")
    scope = _fields(p["legacy_scope"], "issues prs purposes acceptance plans disposition")
    _ids(scope["issues"]); _ids(scope["prs"])
    for key in ("purposes", "acceptance", "plans"):
        contract.vector(scope[key], low=0, high=20000)
        for value in scope[key]:
            _digest(value) if key != "plans" else contract.string(value)
        contract.unique(scope[key])
    _need(scope["disposition"] == "unknown/HOLD", "legacy_history_not_preserved")
    return p, _writers(p["writers"]), contract.digest(scope)


def _boundary(request, obs, p, writers, legacy, now):
    b = _fields(obs["boundary"], "source policy_version generation ts tq quiet_start quiet_end ta writers_sha256 prerequisites_sha256 legacy_scope_sha256 local_state cloud_state pending_dispatch pending_writes locks old_activity_at observation_gaps unknown_days unknown_hours")
    c = _fields(obs["current"], "source policy_version generation highest_generation revoked_generations writers_sha256 prerequisites_sha256 legacy_scope_sha256 active_old_writers pending_old_writes old_activity_at observation_gaps post_ta_history unknown_days unknown_hours")
    observed = _source(b["source"], now)
    current_observed = _source(c["source"], now, fresh=True)
    _need(current_observed >= observed, "current_precedes_boundary")
    for item in (b, c):
        contract.integer(item["generation"])
        _need(item["policy_version"] == p["version"], "policy_mismatch")
        _digest(item["prerequisites_sha256"])
        _need(item["writers_sha256"] == writers and item["legacy_scope_sha256"] == legacy,
              "writer_or_legacy_scope_changed")
    contract.integer(c["highest_generation"])
    _ids(c["revoked_generations"])
    _need(all(generation <= c["highest_generation"] for generation in c["revoked_generations"]),
          "generation_history_contradiction")
    _need(request["generation"] == b["generation"] == c["generation"]
          == c["highest_generation"] and c["generation"] not in c["revoked_generations"],
          "generation_stale_revoked_or_rolled_back")
    _need(b["prerequisites_sha256"] == c["prerequisites_sha256"], "prerequisites_changed")
    _need(b["source"]["actor_id"] == p["owner_id"], "boundary_owner_mismatch")
    _need(c["source"]["actor_id"] == p["owner_id"], "current_generation_owner_mismatch")
    for key in ("pending_dispatch", "pending_writes"):
        _need(type(b[key]) is int and b[key] == 0, "quiescence_unverified")
    _need(b["local_state"] == "quiescent" and b["locks"] == "observed_exclusive"
          and b["cloud_state"] in ("terminated", "write_fenced")
          and c["active_old_writers"] == [] and type(c["pending_old_writes"]) is int
          and c["pending_old_writes"] == 0, "quiescence_unverified")
    _need(b["old_activity_at"] is None and c["old_activity_at"] is None,
          "old_activity_recurred")
    _need(b["observation_gaps"] == [] and c["observation_gaps"] == [],
          "observation_gap")
    _need(c["post_ta_history"] == "complete", "forward_history_incomplete")
    ts, tq, start, end, ta = (_time(b[x]) for x in ("ts", "tq", "quiet_start", "quiet_end", "ta"))
    next_day = (tq + timedelta(days=1)).replace(hour=0, minute=0, second=0, microsecond=0)
    _need(_time(p["adopted_at"]) <= ts <= tq < start == next_day
          and end == start + timedelta(days=1) and ta == end <= observed <= now,
          "invalid_cutover_window")
    _need(now >= ta + timedelta(minutes=5), "first_poll_not_reached")
    # Unknown windows prohibit usage; they are NOT synthetic consumed receipts.
    for item in (b, c):
        contract.vector(item["unknown_days"], low=0, high=20000)
        contract.vector(item["unknown_hours"], low=0, high=20000)
        for day in item["unknown_days"]:
            _need(type(day) is str and len(day) == 10, "invalid_time")
            stamp = _time(day + "T00:00:00+09:00")
            _need(stamp.date().isoformat() == day, "invalid_time")
        for hour in item["unknown_hours"]:
            stamp = _time(hour)
            _need(stamp.minute == stamp.second == stamp.microsecond == 0, "invalid_time")
        _need(now.date().isoformat() not in item["unknown_days"]
              and not any(_time(h).date() == now.date() for h in item["unknown_hours"]),
              "budget_window_unknown")
    _need(not ts.date() <= now.date() <= tq.date(), "budget_window_unknown")
    return ta


def _target(request, obs, p, ta, now):
    t = _fields(obs["target"], "source root_issue root_pr author_id purpose_sha256 acceptance_sha256 lineage_sha256 needed_at issue_created_at approved_at pr_created_at issue_title_sha256 issue_body_sha256 plan_id lineage")
    _source(t["source"], now)
    _root(t, request["kind"])
    contract.integer(t["author_id"])
    _digest(t["issue_title_sha256"]); _digest(t["issue_body_sha256"])
    _need(t["source"]["actor_id"] != t["author_id"], "lineage_independence_unverified")
    for key in ("root_issue", "root_pr", "purpose_sha256", "acceptance_sha256", "lineage_sha256"):
        _need(t[key] == request[key], "target_binding_mismatch")
    _need(ta < _time(t["needed_at"]) <= _time(t["issue_created_at"])
          <= _time(t["approved_at"]) <= now, "pre_boundary_or_unapproved_work")
    if request["kind"] == "initial":
        _need(t["pr_created_at"] is None, "initial_pr_already_exists")
    else:
        _need(ta < _time(t["approved_at"]) <= _time(t["pr_created_at"]) <= now,
              "pre_boundary_or_unapproved_pr")
    scope = p["legacy_scope"]
    _need(t["root_issue"] not in scope["issues"] and t["root_pr"] not in scope["prs"]
          and t["purpose_sha256"] not in scope["purposes"]
          and t["acceptance_sha256"] not in scope["acceptance"]
          and (t["plan_id"] is None or t["plan_id"] not in scope["plans"]), "legacy_target")
    if t["plan_id"] is not None:
        contract.string(t["plan_id"])
    line = _fields(t["lineage"], "state ancestor_issues ancestor_prs prior_plans mixed source_refs")
    _need(line["state"] == "independent_new" and line["ancestor_issues"] == []
          and line["ancestor_prs"] == [] and line["prior_plans"] == []
          and line["mixed"] is False, "legacy_mixed_or_unknown_lineage")
    contract.vector(line["source_refs"], low=1)
    for value in line["source_refs"]:
        contract.string(value)
    contract.unique(line["source_refs"])
    _need(contract.digest(line) == t["lineage_sha256"], "lineage_binding_mismatch")
    return t


def _evidence(request, obs, p, t, now):
    a = _fields(obs["approval"], "source root_issue root_pr purpose_sha256 lineage_sha256 acceptance paths max_files max_lines approved_at reservation_id result_id issue_title_sha256 issue_body_sha256")
    _source(a["source"], now)
    _root(a, request["kind"])
    _need(a["source"]["actor_id"] == p["owner_id"] != t["author_id"], "self_approval")
    for key in ("root_issue", "root_pr", "purpose_sha256", "lineage_sha256"):
        _need(a[key] == request[key], "approval_binding_mismatch")
    _need(a["approved_at"] == t["approved_at"], "approval_binding_mismatch")
    contract.integer(a["reservation_id"]); contract.integer(a["result_id"])
    _need(a["reservation_id"] != a["result_id"], "approval_record_alias")
    for key in ("issue_title_sha256", "issue_body_sha256"):
        _digest(a[key])
        _need(a[key] == t[key], "approval_issue_changed")
    contract.vector(a["acceptance"], low=1)
    for item in a["acceptance"]:
        _fields(item, "id expected_sha256 command_sha256")
        contract.string(item["id"]); _digest(item["expected_sha256"]); _digest(item["command_sha256"])
    contract.unique([x["id"] for x in a["acceptance"]])
    _need(contract.digest(a["acceptance"]) == request["acceptance_sha256"], "acceptance_binding_mismatch")
    contract.vector(a["paths"], low=1)
    for path in a["paths"]:
        contract.safe_path(path)
    contract.unique(a["paths"])
    contract.integer(a["max_files"], 1, 30); contract.integer(a["max_lines"], 1, 600)
    r = _fields(obs["review"], "source run_source root_issue root_pr head_sha approval_sha256 lineage_sha256 reviewer_run_id state workflow_run_id workflow_attempt job_id")
    _source(r["source"], now); _source(r["run_source"], now)
    _root(r, request["kind"]); _sha(r["head_sha"])
    _need(r["source"]["actor_id"] == r["run_source"]["actor_id"]
          and r["source"]["actor_id"] != t["author_id"], "review_independence_unverified")
    _need(r["source"]["record_id"] != r["run_source"]["record_id"], "review_run_missing")
    contract.string(r["reviewer_run_id"])
    _need(r["reviewer_run_id"] == r["run_source"]["record_id"], "review_run_mismatch")
    _need(t["source"]["actor_id"] in (p["owner_id"], r["source"]["actor_id"]),
          "lineage_independence_unverified")
    _need(r["state"] in ("COMMENTED", "APPROVED"), "review_objection")
    for key in ("root_issue", "root_pr", "head_sha", "lineage_sha256"):
        _need(r[key] == request[key], "review_binding_mismatch")
    _need(r["approval_sha256"] == contract.digest(a), "review_approval_mismatch")
    for key in ("workflow_run_id", "workflow_attempt", "job_id"):
        contract.integer(r[key])
    receipt = _fields(obs["acceptance"], "source head_sha workflow_run_id workflow_attempt job_id reviewer_id phase results")
    _source(receipt["source"], now)
    for key in ("workflow_run_id", "workflow_attempt", "job_id", "reviewer_id"):
        contract.integer(receipt[key])
    _sha(receipt["head_sha"])
    for key in ("head_sha", "workflow_run_id", "workflow_attempt", "job_id"):
        _need(receipt[key] == r[key], "acceptance_run_mismatch")
    _need(receipt["reviewer_id"] == receipt["source"]["actor_id"] == r["source"]["actor_id"],
          "acceptance_reviewer_mismatch")
    _need(receipt["phase"] == ("baseline" if request["kind"] == "initial" else "current_head"),
          "acceptance_phase_mismatch")
    contract.vector(receipt["results"], low=1)
    required = {x["id"]: x for x in a["acceptance"]}
    seen = []
    for item in receipt["results"]:
        _fields(item, "acceptance_id test_id expected_sha256 command_sha256 status evidence_sha256")
        key = item["acceptance_id"]
        _need(type(key) is str and key in required, "acceptance_evidence_missing")
        contract.string(item["test_id"]); _digest(item["evidence_sha256"])
        _need(item["expected_sha256"] == required[key]["expected_sha256"]
              and item["command_sha256"] == required[key]["command_sha256"], "acceptance_requirement_mismatch")
        _need(item["status"] in ("passed", "failed"), "acceptance_not_executed")
        seen.append(key)
    _need(len(set(seen)) == len(seen) and set(seen) == set(required), "acceptance_evidence_missing")
    # This is evidence of the observed baseline/head, never proof of future work.
    return a


def _history(request, obs, ta, now):
    h = obs["history"]
    _need(type(h) is history.HistoryRecords and type(h.audit) is history.HistoryAudit,
          "local_history_unverified")
    bind = _fields(obs["history_binding"], "source snapshot_sha256 records_sha256 record_ids coverage_from coverage_through lifetime_complete")
    _source(bind["source"], now, fresh=True)
    _need(h.audit.local_valid is True and type(h.audit.holds) is tuple
          and set(h.audit.holds) <= {LEGACY_HOLD, "unresolved_consumed_history",
                                    "shared_daily_cap_reached", "hour_already_consumed"}
          and h.audit.may_reserve is False
          and h.audit.may_publish is False, "local_history_invalid")
    _need(h.snapshot_digest == h.audit.snapshot_digest == bind["snapshot_sha256"], "history_snapshot_mismatch")
    _digest(bind["snapshot_sha256"])
    _need(_time(h.observed_at).date() == now.date() and _time(h.observed_at).hour == now.hour
          and 0 <= (now - _time(h.observed_at)).total_seconds() <= FRESH_SECONDS,
          "history_not_current")
    _need(_time(bind["coverage_from"]) <= ta and _time(bind["coverage_through"]) == _time(h.observed_at) == now
          and bind["lifetime_complete"] is True, "history_coverage_incomplete")
    _need(type(h.records) is tuple and type(h.sources) is tuple, "local_history_invalid")
    _need(contract.digest(h.to_dict()["records"]) == bind["records_sha256"], "history_records_mismatch")
    _need(type(bind["record_ids"]) is list and bind["record_ids"] == [r.source_id for r in h.records],
          "history_records_missing")
    sources = {s.source_id: s.source_sha256 for s in h.sources
               if type(s) is history.HistorySource and s.verification == "schema_validated"}
    _need(len(sources) == len(h.sources), "history_sources_unverified")
    for s in h.sources:
        contract.string(s.source_id, SOURCE_ID); _digest(s.source_sha256)
    ids, reservations, slots, days, used_sources = set(), set(), set(), {}, set()
    initials, repairs, minors, substantives, reruns = [], [], [], [], []
    total_today = total_hour = substantive_today = 0
    all_initials = set()
    claimed_heads = set()
    root_identity, approval_owners, pr_owners, attempts, repair_totals, substantive_days, rerun_heads = {}, {}, {}, {}, {}, {}, set()
    for r in h.records:
        _need(type(r) is history.HistoryRecord, "local_history_invalid")
        _need(r.source_id in sources and sources[r.source_id] == r.source_sha256
              and r.source_id not in ids and r.reservation_id not in reservations,
              "history_duplicate_or_orphan")
        ids.add(r.source_id); reservations.add(r.reservation_id)
        contract.string(r.source_id, SOURCE_ID)
        contract.string(r.reservation_id, SOURCE_ID if r.kind.startswith("initial_") else ledger.RID)
        _digest(r.source_sha256)
        _need(type(r.evidence) is tuple and bool(r.evidence) and type(r.events) is tuple,
              "history_source_links_invalid")
        for s in r.evidence:
            _need(type(s) is history.HistorySource and s.source_id in sources
                  and sources[s.source_id] == s.source_sha256 and s.verification == "schema_validated",
                  "history_source_links_invalid")
            used_sources.add(s.source_id)
        _need(r.source_id in {s.source_id for s in r.evidence}, "history_source_links_invalid")
        contract.integer(r.root_issue)
        if r.root_pr is not None:
            contract.integer(r.root_pr)
        _need(r.kind in ("initial_daily", "initial_manual", "r3_minor", "r3_substantive", "ci_rerun")
              and r.consumption == "consumed" and type(r.reasons) is tuple
              and set(r.reasons) <= {"result_missing", "manual_result_unknown", "repair_result_unknown"}
              and r.result in ("unknown", "failed", "recorded_success"), "local_history_invalid")
        _need(type(r.date_jst) is str and len(r.date_jst) == 10, "local_history_invalid")
        date = _time(r.date_jst + "T00:00:00+09:00").date()
        _need(r.result != "unknown" or date < ta.date(), "forward_result_unknown")
        _need(date <= now.date() and type(r.hours_jst) is tuple and bool(r.hours_jst)
              and len(set(r.hours_jst)) == len(r.hours_jst)
              and all(type(x) is int and 0 <= x <= 23 for x in r.hours_jst), "local_history_invalid")
        if r.at is not None:
            at = _time(r.at)
            _need(at <= now and at.date() == date and at.hour in r.hours_jst, "local_history_invalid")
        if r.kind.startswith("initial_"):
            _need(r.root_issue not in all_initials and r.root_pr is None and r.attempt is None
                  and not r.events, "historical_initial_duplicate")
            all_initials.add(r.root_issue)
            if r.kind == "initial_daily":
                _need(r.source_id.startswith("daily/day-") and r.source_id.endswith(".json"), "local_history_invalid")
                day, style, index = history.hourly._reservation_identity(r.source_id[len("daily/day-"):-5])
                expected_hours = (tuple(range(24)) if style is None else (index,) if style == "hour"
                                  else tuple(range((index * 3 if style == "3h-slot" else history.hourly.daily.LEGACY_SLOT_HOURS_JST[index]) // 3 * 3,
                                                   (index * 3 if style == "3h-slot" else history.hourly.daily.LEGACY_SLOT_HOURS_JST[index]) // 3 * 3 + 3)))
                _need(day == r.date_jst and r.hours_jst == expected_hours
                      and f"daily/issue-{r.root_issue}.json" in {s.source_id for s in r.evidence},
                      "history_counterpart_missing")
                result_source = r.source_id.replace("daily/day-", "daily/result-")
            else:
                _need(r.root_issue == 10 and r.source_id == "attempts/issue-10.json", "local_history_invalid")
                result_source = "attempts/issue-10-result.json"
            _need(r.result == "unknown" or result_source in {s.source_id for s in r.evidence},
                  "history_result_source_missing")
        else:
            contract.integer(r.root_pr); contract.integer(r.attempt, 1, 2)
            contract.integer(r.approval_reservation_id); contract.integer(r.approval_result_id)
            _sha(r.head_before)
            if r.head_after is not None:
                _sha(r.head_after)
            _need(r.at is not None, "repair_time_missing")
            _need(r.source_id == f"pr-repair-ledger/reservation-{r.reservation_id}.json", "local_history_invalid")
            root = (r.root_issue, r.root_pr)
            identity = (r.root_pr, r.approval_reservation_id, r.approval_result_id)
            _need(root_identity.get(r.root_issue, identity) == identity
                  and pr_owners.get(r.root_pr, r.root_issue) == r.root_issue, "history_root_alias")
            root_identity[r.root_issue] = identity
            pr_owners[r.root_pr] = r.root_issue
            for key in (("reservation", r.approval_reservation_id), ("result", r.approval_result_id)):
                _need(approval_owners.get(key, root) == root, "history_approval_alias")
                approval_owners[key] = root
            attempts.setdefault((root, r.kind), []).append(r.attempt)
            if r.kind in ("r3_minor", "r3_substantive"):
                repair_totals[root] = repair_totals.get(root, 0) + 1
            if r.kind == "r3_substantive":
                substantive_days[r.date_jst] = substantive_days.get(r.date_jst, 0) + 1
            if r.kind == "ci_rerun":
                key = (root, r.head_before)
                _need(key not in rerun_heads, "historical_rerun_conflict")
                rerun_heads.add(key)
        previous = _time(r.at) if r.at is not None else None
        event_objects = []
        for sequence, e in enumerate(r.events, 1):
            _need(type(e) is history.HistoryEvent and e.source_id in sources
                  and sources[e.source_id] == e.source_sha256
                  and e.event in ledger.EVENTS, "history_event_invalid")
            used_sources.add(e.source_id)
            _sha(e.head_before)
            if e.head_after is not None:
                _sha(e.head_after)
            instant = _time(e.at)
            _need(previous is not None and previous <= instant <= now, "history_event_time_invalid")
            previous = instant
            _need(e.head_before == r.head_before
                  and e.source_id == f"pr-repair-ledger/event-{r.reservation_id}-{sequence:02d}.json",
                  "history_event_invalid")
            if e.event == "publish_started":
                claim = (r.root_pr, r.head_before)
                _need(claim not in claimed_heads, "repair_claim_conflict")
                claimed_heads.add(claim)
            event_objects.append({"event": e.event, "headBefore": e.head_before, "headAfter": e.head_after})
        if not r.kind.startswith("initial_"):
            _need(not history._event_conflict({"kind": r.kind.removeprefix("r3_")}, event_objects),
                  "history_event_conflict")
            terminal = event_objects[-1]["event"] if event_objects else None
            outcome = ("recorded_success" if terminal == "ci_success" else
                       "failed" if terminal in ("ci_failed", "failed", "candidate_rejected") else "unknown")
            after = next((e["headAfter"] for e in reversed(event_objects) if e["event"] == "published"), None)
            _need(r.result == outcome and r.head_after == after, "history_outcome_mismatch")
        for hour in r.hours_jst:
            slot = (r.date_jst, hour)
            _need(slot not in slots, "history_slot_conflict")
            slots.add(slot)
        days[r.date_jst] = days.get(r.date_jst, 0) + 1
        _need(days[r.date_jst] <= ledger.DAILY_CAP, "historical_budget_conflict")
        today = date == now.date()
        total_today += today
        total_hour += today and now.hour in r.hours_jst
        substantive_today += today and r.kind == "r3_substantive"
        if (r.root_issue == request["root_issue"] or r.root_pr == request["root_pr"] and r.root_pr is not None
                or r.approval_reservation_id == obs["approval"]["reservation_id"]
                or r.approval_result_id == obs["approval"]["result_id"]):
            _need(r.root_issue == request["root_issue"] and r.root_pr in (None, request["root_pr"]),
                  "history_root_alias")
            _need(r.at is not None and ta < _time(obs["target"]["approved_at"]) <= _time(r.at),
                  "target_history_predates_approval")
            if r.kind.startswith("initial_"):
                _need(request["kind"] != "initial", "attempt_limit")
                _need(obs["target"]["pr_created_at"] is not None
                      and _time(r.at) <= _time(obs["target"]["pr_created_at"]), "pr_initial_link_invalid")
                initials.append(r)
            elif r.kind.startswith("r3_"):
                _need(r.approval_reservation_id == obs["approval"]["reservation_id"]
                      and r.approval_result_id == obs["approval"]["result_id"], "history_approval_alias")
                repairs.append(r)
                (minors if r.kind == "r3_minor" else substantives).append(r)
            elif r.head_before == request["head_sha"]:
                reruns.append(r)
    _need(used_sources == set(sources), "history_orphan_source")
    _need(all(n <= 2 for n in repair_totals.values())
          and all(n <= ledger.SUBSTANTIVE_DAILY_CAP for n in substantive_days.values()),
          "historical_lifetime_conflict")
    for (_, kind), values in attempts.items():
        expected = [1] * len(values) if kind == "ci_rerun" else list(range(1, len(values) + 1))
        _need(sorted(values) == expected and (kind == "ci_rerun" or len(values) <= (2 if kind == "r3_substantive" else 1)),
              "historical_lifetime_conflict")
    counts = {"daily_count": sum(r.kind == "initial_daily" for r in h.records),
              "manual_count": sum(r.kind == "initial_manual" for r in h.records),
              "repair_count": sum(r.kind in ("r3_minor", "r3_substantive", "ci_rerun") for r in h.records),
              "unresolved_count": sum(r.result == "unknown" for r in h.records),
              "effective_today": total_today, "effective_hour": total_hour}
    _need(all(type(getattr(h.audit, k)) is int and getattr(h.audit, k) == v for k, v in counts.items())
          and type(h.audit.consumed_issues) is frozenset
          and all(type(issue) is int and issue > 0 for issue in h.audit.consumed_issues)
          and h.audit.consumed_issues == all_initials,
          "history_counts_mismatch")
    _need(("shared_daily_cap_reached" not in h.audit.holds or total_today >= ledger.DAILY_CAP)
          and ("hour_already_consumed" not in h.audit.holds or total_hour > 0), "history_counts_mismatch")
    _need(total_today < ledger.DAILY_CAP and total_hour == 0, "shared_budget_exhausted")
    kind = request["kind"]
    _need(len(initials) <= 1 and len(repairs) <= 2 and len(minors) <= 1
          and len(substantives) <= 2 and len(reruns) <= 1, "historical_lifetime_conflict")
    _need(not (kind == "initial" and initials)
          and not (kind != "initial" and len(initials) != 1)
          and not (kind in ("minor", "substantive") and len(repairs) >= 2)
          and not (kind == "minor" and minors)
          and not (kind == "substantive" and (len(substantives) >= 2
                    or substantive_today >= ledger.SUBSTANTIVE_DAILY_CAP))
          and not (kind == "ci_rerun" and reruns), "attempt_limit")


def _inventory_and_scope(request, obs, approval, now):
    inv = _fields(obs["inventory"], "source complete open_prs root_map head_sha main_sha")
    _source(inv["source"], now, fresh=True)
    _ids(inv["open_prs"])
    _need(inv["complete"] is True, "open_pr_inventory_incomplete")
    _need(inv["head_sha"] == request["head_sha"] and inv["main_sha"] == request["main_sha"],
          "git_head_changed")
    _need(inv["open_prs"] == ([] if request["kind"] == "initial" else [request["root_pr"]]),
          "global_open_pr_exclusion")
    if request["kind"] != "initial":
        roots = inv["root_map"]
        _need(type(roots) is list and all(type(x) is list and len(x) == 2 for x in roots), "root_map_invalid")
        # Reuse R3's exact root identity, aliases and global single-open-PR check.
        ledger._validate_gate(ledger.Gate(
            request["root_issue"], request["root_pr"], tuple(tuple(x) for x in roots),
            approval["reservation_id"], approval["result_id"],
            approval["issue_title_sha256"], approval["issue_body_sha256"],
            obs["target"]["issue_title_sha256"], obs["target"]["issue_body_sha256"],
            request["main_sha"], inv["main_sha"], request["head_sha"], inv["head_sha"],
            tuple(inv["open_prs"]), int(now.timestamp()) + 1,
            request["kind"] == "ci_rerun"))
    else:
        _need(inv["root_map"] == [], "root_map_invalid")
    scope = _fields(obs["scope"], "source root_issue root_pr head_sha approval_sha256 paths changed_lines initial_paths ci_rerun_authorization")
    _source(scope["source"], now)
    _root(scope, request["kind"]); _sha(scope["head_sha"]); _digest(scope["approval_sha256"])
    _need(scope["source"]["actor_id"] == obs["review"]["source"]["actor_id"], "scope_review_unverified")
    _need(all(scope[k] == request[k] for k in ("root_issue", "root_pr", "head_sha"))
          and scope["approval_sha256"] == contract.digest(approval)
          == obs["review"]["approval_sha256"], "scope_binding_mismatch")
    contract.vector(scope["paths"], low=0)
    contract.vector(scope["initial_paths"], low=0)
    for path in scope["paths"] + scope["initial_paths"]:
        contract.safe_path(path)
    contract.unique(scope["paths"]); contract.unique(scope["initial_paths"])
    contract.integer(scope["changed_lines"], 0, 100000)
    _need(set(scope["paths"]) <= set(approval["paths"]), "scope_expansion")
    files, lines = approval["max_files"], approval["max_lines"]
    if request["kind"] == "minor":
        files, lines = min(files, 2), min(lines, 80)
        _need(set(scope["paths"]) <= set(scope["initial_paths"]), "minor_outside_initial_paths")
    elif request["kind"] == "substantive":
        files, lines = min(files, 3), min(lines, 600)
    _need(len(scope["paths"]) <= files and scope["changed_lines"] <= lines, "cumulative_scope_limit")
    if request["kind"] == "ci_rerun":
        grant = _fields(scope["ci_rerun_authorization"], "source head_sha")
        _source(grant["source"], now)
        _need(grant["source"]["actor_id"] == obs["policy"]["owner_id"]
              and grant["head_sha"] == request["head_sha"], "ci_rerun_not_authorized")


def evaluate(request, observations=None, *, now):
    """Compare declared offline inputs; NEVER authenticate or grant authority.

    A synthetic consistent fixture may satisfy this conditional contract. Its
    hashes, IDs, dataclasses and matching claims still do not prove real-world
    independence, quiescence, approval, acceptance or history completeness.
    """
    reason = None
    try:
        _need(type(now) is datetime and now.tzinfo is not None and now.utcoffset() is not None,
              "invalid_time")
        now = now.astimezone(JST)
        r = _fields(request, "schema repo_id policy policy_version generation kind root_issue root_pr purpose_sha256 acceptance_sha256 lineage_sha256 head_sha main_sha writer_id deployment_sha")
        _need(type(r["repo_id"]) is int and r["repo_id"] == REPOSITORY_ID, "repository_mismatch")
        _need(r["schema"] == SCHEMA and type(r["kind"]) is str and r["kind"] in KINDS,
              "unsupported_contract")
        contract.integer(r["generation"]); contract.integer(r["root_issue"])
        _need(r["root_pr"] is None if r["kind"] == "initial" else type(r["root_pr"]) is int and r["root_pr"] > 0,
              "invalid_root")
        for k in ("purpose_sha256", "acceptance_sha256", "lineage_sha256"):
            _digest(r[k])
        for k in ("head_sha", "main_sha", "deployment_sha"):
            _sha(r[k])
        contract.string(r["writer_id"])
        obs = _fields(observations, "policy boundary current target approval review acceptance history history_binding inventory scope")
        source_refs = [obs[k]["source"]["record_id"] for k in ("policy", "boundary", "current", "target", "approval", "review", "acceptance", "history_binding", "inventory", "scope")]
        source_refs.append(obs["review"]["run_source"]["record_id"])
        _need(len(set(source_refs)) == len(source_refs), "source_record_reused")
        p, writers, legacy = _policy(r, obs, now)
        role = "initial" if r["kind"] == "initial" else "repair"
        _need(any(x["role"] == role and x["id"] == r["writer_id"]
                  and x["deployment_sha"] == r["deployment_sha"] for x in p["writers"]), "writer_mismatch")
        ta = _boundary(r, obs, p, writers, legacy, now)
        target = _target(r, obs, p, ta, now)
        approval = _evidence(r, obs, p, target, now)
        _history(r, obs, ta, now)
        _inventory_and_scope(r, obs, approval, now)
    except MigrationHold as exc:
        reason = str(exc)
    except (contract.ContractError, ledger.LedgerHalt, ValueError, TypeError, KeyError,
            AttributeError, OverflowError, RecursionError):
        reason = "invalid_or_incomplete_contract"
    return {"schema": SCHEMA, "state": "HOLD" if reason else "CONTRACT_SATISFIED",
            "offline_contract_satisfied": reason is None,
            "reasons": [] if reason is None else [reason],
            "authentication": "not_established", "live_eligibility": "unverified",
            "legacy_history": "unknown/HOLD", "acceptance_success": "not_asserted",
            "may_reserve": False, "may_publish": False, "may_activate": False,
            "may_repair": False, "may_merge": False, "production_enabled": False,
            "r4_connection": "not_connected"}
