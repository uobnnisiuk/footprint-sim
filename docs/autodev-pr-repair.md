# PR repair: R1 evidence (read-only)

Status: **R1 prototype / evidence only**. This does **not** activate substantive repairs, add model invocations, authorize a merge, or change the existing Windows/WSL runner. It is an offline input to R2 contract validation and independent review.

## Boundary

- Historical fixture: PR #58, Issue #56, three **distinct** head SHAs and exact normal/research run IDs recorded in Issue #59.
- Real GitHub Actions run IDs and normal job IDs were checked against GitHub. The 1 and 28 failed-test labels are **synthetic placeholders**, not verbatim test names or downloaded raw logs. The 29 failure entries now explicitly use `evidence_kind=synthetic` with null evidence digests. A synthetic marker with a non-null digest is invalid; a `claimed_excerpt` hash is carried separately as `claimed_evidence_sha256`, always `unverified`, never as attested evidence. The common reason codes reflect the documented independent review. The source summaries (503+1 / 503+28 / 531+0) are historical observations, not current CI status.
- A raw log, arbitrary URL, injected instruction, or text from a model is never interpreted as an action. Do not publish credentials or raw evidence logs.
- GitHub's current approval, required checks, PR protection, and independently executed review must still be checked by a separate reviewer. A verified flag in the input is an **evidence claim**, not an authority grant.

## Pure normalization

Run offline:

    python3 tools/pr_repair_evidence.py --snapshot .github/scripts/fixtures/pr58-repair-evidence.json

Run the tests (also discovered by the existing CI workflow):

    python3 -m unittest discover -s .github/scripts -p 'test_pr_repair_evidence.py' -v

The normalized JSON uses strict known keys, exact head/run/attempt association, deterministic sorting, a digest without observation time, and a grouped failure summary which still contains every individual failure record. No automatic attempt is made to interpret log content or fix code. Invalid, duplicated, truncated or mismatched records fail closed.

An acceptance item has independent status: not_implemented, tested_not_run, passed, failed, needs_review. Claiming passed is insufficient: it needs a successful exact-head run **and** independently verified provenance for the actual executed test, reviewer **and the specific approved acceptance requirement**. Each reference is explicitly `synthetic` or `claimed_observed`; neither label authenticates itself. An optional `verified_test_evidence` set can be supplied only by a separate trusted reviewer integration after verifying the exact (head, run, attempt, acceptance ID, test ID, reviewer ID) mapping. An attested test/reference cannot be reused to pass other requirements without separately verified requirement-to-test associations. The former five-field unscoped tuple is rejected. Snapshots and the CLI cannot supply these attestations. R2 must bind these to trusted observations and approved requirement mappings.

A workflow is not passed merely because a run summary says success: at least one complete successful job and a successful required step must be present. Missing run/jobs, pending, cancelled, skipped required steps and incomplete pagination never imply success. The output always says may_merge=false and may_repair=false. R2 must bind its ApprovalSnapshot to trusted sources and compare full base→candidate and head→candidate diffs; this R1 file does not create that authority.

## Independent-review corrections / fail-closed limits

- The full four-item PR #58 approval baseline is pinned in code for this **historical fixture only**. Omitting even one accepted requirement now produces `hold_acceptance` with `missing_approved_acceptance`; modifying a snapshot's own approval label does not alter the baseline. For any other PR, no baseline is inferred from Issue text, model output, or a self-declared snapshot: `normalize()` holds until the independent reviewer supplies an externally verified exact set. The CLI cannot supply or self-approve that set. This is still evidence, not authority; R2 must bind trusted provenance.
- Successful runs require a `log_evidence` state of `verified_available` with a reviewer reference. Missing, expired and unverified logs never produce a passing run. The historical final PR #58 record references its independent technical review; new GET-only adapter reads mark log availability `unverified` because GitHub run metadata alone cannot prove live logs. This field is a provenance claim to check, not proof of access to a live log.
- The **complete** normal/research GitHub Actions job step inventories are pinned in code (14 normal / 12 F19, including checkout, setup, Restore, Build, automation checks, artifact-safety, retention and post steps), rather than accepting the snapshot's `required=false` flag. The fixed fixture inventories are derived from all six actual GitHub Actions run/job records; the normal and research workflows each have exactly one job and fragmented job steps cannot be combined to appear complete. A missing, renamed, unknown, or falsely optionalized step prevents a success result. New/changed workflows need a separately verified policy before their results can be considered complete.
- Raw arbitrary step names and failure IDs are never emitted; unknown step names are hashed and keep the CI result unknown. Failure reason codes are a fixed enum; unknown/untrusted reasons are rejected. Acceptance/test/review IDs appear only as pinned neutral IDs or SHA-256 hashes. Untrusted JSON key names and detailed parse errors never reach CLI stderr. The GET-only adapter emits hashes of workflow and step names, never their raw text. Do not forward raw CI logs or secret-bearing metadata.
- The PR #58 fixture's four acceptance test labels are synthetic, **not observed CI test names**. Its final green workflow runs remain factual, but its claimed acceptance passes normalize to `needs_review` and `hold_acceptance` until real test/review provenance is verified separately. Do not treat this offline fixture as proof that its acceptance tests executed.
- Normalized output contains `may_merge=false` and `may_repair=false` in all cases, including the historical green fixture. No workflow or real repair is activated.

## Read-only remote adapter

    python3 tools/pr_repair_evidence.py --fetch-run 41041420429 --repo uobnnisiuk/footprint-sim

This uses two bounded GET requests for the run and its first page of jobs. It **never downloads logs**, publishes records, executes model code, starts CI, writes GitHub, or creates a reservation. More than 100 jobs is explicitly marked jobs_complete=false, not silently treated as fully checked. Set GITHUB_TOKEN only if authenticated reads are necessary. Never print or commit it.

## Provenance

- Issue R1: https://github.com/uobnnisiuk/footprint-sim/issues/59
- PR #58 and original Issue: https://github.com/uobnnisiuk/footprint-sim/pull/58 ; https://github.com/uobnnisiuk/footprint-sim/issues/56
- Initial normal run: https://github.com/uobnnisiuk/footprint-sim/actions/runs/40878956092
- Intermediate normal run: https://github.com/uobnnisiuk/footprint-sim/actions/runs/41040441468
- Final normal run: https://github.com/uobnnisiuk/footprint-sim/actions/runs/41041420429
- Research runs: 40878956054, 41040441429, 41041420353, respectively.

R2/3/4/5 must land through **separate reviewed infrastructure PRs in dependency order**. Do not label these Issues local-autodev/autodev and do not activate the production repair worker before R5's real-world rollout checks.

## R2: immutable approval/repair contract (offline preflight only)

Issue: https://github.com/uobnnisiuk/footprint-sim/issues/60

R2 adds a **pure preflight** at \`tools/pr_repair_contract.py\` and the closed,
versioned shape \`.github/scripts/pr_repair_request.schema.json\`. The JSON
document contains two distinct records: \`approval_snapshot\` (independent
approval baseline) and \`repair_request\` (narrower proposed repair). The
request **cannot issue its own approval**. Do not construct \`TrustedFacts\`
from the request, model text, mutable Issue body, or an unverified manifest.
A separate independent integration must read and verify the original Issue
title/body SHA-256, approval reservation/result GitHub records and protocol
commit, reviewer identity/run and finding, exact observed CI run/attempt/job,
the current main/head and original PR base, complete Git tree inventories,
and the persistent attempt ledger. \`approval_digest\` is SHA-256 over
canonical sorted compact UTF-8 JSON and verifies equality only; it is
**not a signature** and does not establish independent trust.

The four full \`trees\` inventories are named \`approved\`, \`pr_base\`,
\`current\`, \`candidate\`. Each requires a root tree SHA and \`complete=true\`,
with all paths and their Git blob OID, mode, and optional UTF-8 contents.
The independent adapter MUST verify these root SHAs against the real Git
trees and ensure inventories are complete **before** constructing
\`TrustedFacts\`; a flag or hash merely copied from the candidate is not
proof. Contents for every actually modified file are mandatory and checked
against Git SHA-1 blob OIDs; unchanged files may omit contents. R2 targets
this repository's SHA-1 Git object format. Missing inventory, wrong blob,
unknown schema/key, repeated JSON key, bad UTF-8, unverified provenance,
expired reservation, stale head, absent reviewer-run identity or missing
CI mapping causes a fixed-code rejection, not automatic approval.

The independent CI evidence adapter must also bind **both** the normalized
workflow identity and the failure classification to the same verified
run ID, attempt, job ID, observed PR head and evidence digest. In R2,
`TrustedFacts.observed_workflow` and `TrustedFacts.observed_failure_code`
must come from that independently verified run/failure receipt, not from the
candidate's `repair_request.workflow`, `failure_code`, mutable Issue text
or an unverified classification. An unmatched or missing pair fails closed
with `stale_or_unverified_evidence`, even if all other provenance and tree
checks succeed. The workflow token must be mapped from a verified workflow
identity (for example R1's reviewed `normal` / `research` class); a
display name, copied SHA-256 or model-chosen failure label alone is not
independent attestation. When multiple failures exist, the adapter must
select the independently reviewed finding and its evidence, not let the
candidate decide which failure it is repairing. R2 supplies this strict
interface only; actual trusted GitHub/R1 receipt acquisition is a separate
downstream adapter and is not claimed to be implemented here.

The trusted adapter must read the persistent **repair reservation** from
an independent ledger and verify its ID, recorded expiry, policy and kind,
root Issue/PR, current main/head, and attempt before filling the matching
fields in `TrustedFacts`. The request itself is NOT a trusted reservation
source. Forging a new ID or extending the deadline, even while preserving
all valid CI facts, must fail closed. The R3 ledger adapter is not included
in R2; without its receipt, R2 cannot approve the request.

The checker compares the **entire original PR base → current head**, the
**entire original PR base → candidate**, and **current head → candidate**.
Both original and proposed changes must lie within the previously approved
exact paths. The requested before/after OIDs must match those changes.
No additions, deletions, renames, special modes, globbed paths, generated
artifacts, or protected configuration are allowed in the repair. Every
file present in the approval must have its approval-time OID rechecked.
**Each approved exact-path blob must also match the same path in the
independently verified original PR base** (`pr_base`); missing files, mode
differences or differing Git blob OIDs fail closed as
`approval_blob_mismatch`, even when base→current, base→candidate and
current→candidate diffs only touch approved paths. A newer approval
snapshot does not retroactively authorize a drifted original PR base;
legitimate baseline changes require separately verified explicit
reauthorization through a new approved contract, not an exception
claimed by a repair request.
Substantive repair is limited to the minimum of original approval, request,
and 3 distinct existing files/600 cumulative added+removed lines, including
previous trusted attempts. Minor repair retains the existing 2 files/80
cumulative lines/one lifetime attempt and initial-PR-changed-file limit.
An existing test allowed at original approval but untouched by the initial
PR is eligible only for an independently requested substantive repair.
The downstream R3/R4 adapter must also run the **unchanged** general
\`patch_guard.py\` and preserve the legacy immutable ledger.

Acceptance IDs, their expected-evidence digests, and mandatory verification
command digests must exactly match the independently pinned approval; no
new acceptance, expected-result change, removal, or silent weakening is
authorized. **All edits in `tests/` require independent semantic
attestation**, even changes that only add control flow or skip attributes
and leave the original asserts untouched. Suspected assertion/skip/order
changes elsewhere also require the exact-delta review and separate
verification of candidate-set membership, count, CandidateId order, and
CLI byte determinism. Adding \`IsComplete=true\` is rejected. This heuristic is
only an alarm: passing it is **not** proof that tests or semantics have
been preserved. Actual code review is still required.

Run R2 offline tests using:

    python3 -m unittest discover -s .github/scripts -p 'test_pr_repair_contract.py' -v

The test matrix uses synthetic old-PR #58 source/test/docs paths and covers
valid narrowed contracts, approval/source tampering, unsafe extra files,
both full-diff boundaries, before/after mismatch, changed modes, missing
tree data, expiry, attempt limits, missing attestation, test expectation
changes and the order-assert safety boundary. Synthetic paths and OIDs
are **not** claimed to be historical GitHub CI test names or actual
attested approvals. R2 invokes no subprocess, GitHub calls, models, timers,
reservations, or branch writes. Every successful preflight still returns
\`may_repair=false\`, \`may_merge=false\`, and requires a future independent
review; only R5 may evaluate actual activation after Windows/WSL and
shadow/runtime evidence.


## R3: shared reservation accounting (INACTIVE, offline contract)

Issue: https://github.com/uobnnisiuk/footprint-sim/issues/61

The R3 module \`tools/pr_repair_ledger.py\` implements an **append-only** repair
reservation ledger in a private sibling directory of the old daily state:
\`<old-state-parent>/pr-repair-ledger\`. It neither edits nor migrates old
\`day-*.json\`, \`issue-*.json\`, result, log, or \`daily.lock\` records.
Records are written with exclusive create, file sync and directory sync.
Partial/truncated/malformed records, unexpected entries, symlinks, orphan
events, approval/root aliases or missing old reservation counterparts are
fatal on **all future days**, not silently dropped as unused capacity.

\`local_autodev_hourly.read_budget()\` now reads both ledgers. The same
\`daily.lock\` is required for the original initial Issue runner and new
repair reservations, with fresh Japan-local time **inside** the lock.
Initial implementation still has one irreversible Issue-lifetime attempt.
For repair, the maximum is two combined minor/substantive reservations per
original root Issue+PR, at most one minor, at most two substantive, and
two substantive reservations per JST day. Initial + minor + substantive +
explicit active CI reruns share **eight** JST daily reservations and **one
start per hour**. Active CI reruns require a separately verified explicit authorization
(`Gate.ci_rerun_approved=True`) and are limited to one per exact head.
Normal CI/research CI automatically triggered after a candidate commit
are not booked a second time. Read-only polling consumes no reservation.

\`Gate\` is an **input boundary, not a proof of trust**: the downstream R4
adapter must independently construct its full approved Issue→root PR map,
original immutable approval IDs, Issue title/body hashes, current GitHub
main/head, open-PR inventory and expiry, and verify these against the
approved R2 contract and live sources **before** calling \`reserve()\`.
The ledger will reject drift, aliases, a second Open PR, expired receipts,
repeated head CI reruns and an unrecognized root. No Issue body, proposed
repair request, model response or user-supplied root text is trusted
just because it can be formatted as a \`Gate\`. There is currently **no
production trusted adapter** and no external action is authorized.

Use \`reserve(state, gate, kind, execute=False)\` for a read-only preview.
\`execute=True\` creates **only** the persisted reservation and never
calls a model, GitHub, a command or a scheduler. Follow-up lifecycle
observations are separate immutable \`append_event\` records. No result
event, lost external receipt, generation timeout, response unknown,
failed write, stale expiry or crash consumes anything *less* than the
original reservation. Never generate a second candidate or resend
an uncertain write: the R4 adapter must read back the external state,
record an unambiguous result or stop.

### Required R5 rollout and rollback gates

R1–R4 **do not enable PR repair**. The existing Local AutoDev executable,
old minor-correction task, Windows/WSL state and scheduled execution remain
unchanged by this PR. Production activation requires a separately approved
R5 integration and the following verified conditions:

1. Read and archive exact SHA-256 and mode of the old daily/issue/results,
   active scheduler/task settings and runtime module fingerprint *without
   modifying old data*. Validate no damaged/orphan markers.
2. Confirm the deployed hourly runtime actually includes the new shared
   \`read_budget\` and \`pr_repair_ledger.py\`; any older runner that ignores
   the new ledger **must be stopped before repair reservations can begin**.
   An unverified or old 3-hour/daily/one-shot controller may not run
   concurrently; a GitHub commit alone does not deploy local Windows files.
3. Ensure old minor-fix tasks cannot write in parallel with the new
   repair controller, and the owner-approved initial Issue timer shares
   the same local \`daily.lock\` and eight-per-day accounting.
4. Perform offline mixed-legacy/repair, corruption, concurrency, UTC↔JST
   boundary, expiry and crash-injection tests; then shadow-observe without
   consuming reservations, before an explicitly approved single production
   attempt with verified trusted facts and independent review.
5. Do **not** simply roll back to a runtime that ignores the new ledger.
   Stop all writers first; reconcile every outstanding reservation, result,
   unknown response and shared budget with independent evidence.
   Restart only a verified compatible version. Never delete/rewrite old
   markers, new receipts or unresolved failures as a rollback technique.

The R3 offline tests are included in the existing required \`test\` job:
\`python3 -m unittest discover -s .github/scripts -p 'test_pr_repair_ledger.py' -v\`
and the existing hourly tests remain intact. The R2 preflight, R4
publication adapter, automatic review/merge and real-machine deployment
remain separate gates. F22 research is not authorized by this change.


## R4: detached candidate publication gate (offline only)

Issue: https://github.com/uobnnisiuk/footprint-sim/issues/62

R4 adds `tools/pr_repair_candidate.py` and `tools/pr_repair_publish.py`
with two offline test modules under `.github/scripts/`. **No live GitHub
publisher, scheduling, model call, or production activation exists yet.**
A candidate must be a detached, one-parent commit whose sole parent is the
PR's *currently observed* head. The model may describe that candidate but
cannot update the branch. A separate trusted adapter must GET and verify
its Git commit/tree/blob objects and complete inventories; a SHA or digest
in a model response proves nothing.

The offline gate requires a durable R3 reservation, independent R2 contract
facts, exact-blob approval, checked original base→candidate and
head→candidate diffs, and a verified *main-derived* guard run within
disposable Git. The guard must independently validate the **entire** patch,
and changes must remain in existing normal UTF-8 files. The new candidate
checker rejects additional paths, changed modes, multiple parents,
new/deleted files, unsupported paths, and manifest mismatch. It never runs
candidate code or tests on an owner's credential-bearing computer.

One atomic persistent `claim_once` operation is a required adapter contract:
under the **same R3 ledger lock**, confirm no other repair has claimed the
reservation or PR/head and durably append `publish_started` before CAS.
The current R3 `append_event` API alone is **not sufficient** as this
claim-and-check primitive. This is an explicit production integration
blocker, not an invitation to patch the R3 file outside Issue #62 scope.

After initial validation, the controller checks a newly observed, non-backward
clock against the reservation deadline before claiming. After claim, it
rereads the reservation and time once more, failing closed when the deadline
has passed; that claim stays consumed and there is no retry. The trusted
adapter must independently resample time on each observation, rather than
reuse the first snapshot. The controller then performs at most **one**
expected-old-head, `force=false` ref update to the **existing** branch.
Timeout and ambiguous writes are never retried. Readback distinguishes
new head, old head and unknown; all outcomes leave the reservation spent.
The deadline is a strict **pre-CAS write gate**, not a post-write evidence
gate: after a valid CAS attempt, independently verified readback may occur
after expiry and still establish a known new or old head. A missing or
backward clock, changed Issue/main/PR/reservation, or third-party head still
produces an unknown result without reattempting the write. Regression tests
cover lost responses on both known heads after expiry and clock rollback.
Even a verified publication enters CI *pending*, never a preapproved merge.
The controller does not create Issues/PRs, make a PR Ready, or merge.

Offline tests use a fake Git adapter to inject stale main/Issue/head,
approval or contract rejection, extra tree changes, guard refusal,
reservation competition, timeout, and lost CAS responses. Run via:

    python3 -m unittest discover -s .github/scripts -p 'test_pr_repair_*.py' -v

**Remaining R4 verification:** the current GitHub connection supports Git
commit/tree/ref operations, including `update_ref(expected_sha,force=false)`,
but this is only an interface-level observation, **not** proof of safe
detached-object retrieval or a transactional, verified reservation claim.
No real repository was used as a write-test target. A production-capable
adapter needs an independently approved test target and R5 runtime/ledger
checks; without them live publication stays disabled. The fake adapter
must not be used as authority over live GitHub data. R5 must prevent
the old minor writer and new substantive writer from ever writing the same
PR/head independently.

### R4 independent-review remediation (offline tests only)

The first Draft R4 head failed the Python check because the simulated write
accepted a subclass sharing the test module's name. The offline simulation
now permits only the exact `FakeAdapter` class object; a subclass with a
forged `__module__` string fails before any write. This identity check is
**not** a live authentication boundary and must never authorize production.

The R4 tests now exercise the actual R2 validator on the R2 synthetic
approval/tree fixture (including a tampered acceptance failure), a disposable
local Git repository with a detached single-parent candidate and
compare-and-swap `update-ref` semantics, candidate timeout/repeated generation
failures, and R3's persistent receipt/event storage. An R4 offline
`claim_once_durable` helper takes R3's same `daily.lock`, checks the
receipt and all historical claims for the same PR/head, and durably appends
one `publish_started` event. It refuses to append events when the observed
clock is earlier than the reservation's durable `createdAtJst`, preserving
R3's ledger-read and shared-budget invariants after a clock rollback.
The minor/substantive competition fixture
verifies only one publisher wins. No candidate code or test is run inside
that Git repository, and no GitHub/Windows state is modified by these tests.

**Not established:** a production-grade independent source of trusted R1/R2
facts, an independently verified live Git-object/ref adapter, an approved
GitHub write-test target, and the deployed Windows/WSL controller with the
old minor publisher retired. The R3 `append_event` method by itself remains
insufficient as an atomic claim. None of this offline evidence enables
production. Final exact-head GitHub CI and independent review are required.


## R5: review-to-repair handoff and guarded rollout (OFFLINE ONLY)

Issue: https://github.com/uobnnisiuk/footprint-sim/issues/63

`tools/pr_repair_handoff.py` is the **read-only, event-replay handoff
contract**, not a live orchestrator. Its offline tests use PR #58's three fixed
historical Git heads, but all acceptance IDs, reviewer receipts and authoring
records are explicitly **synthetic**. The simulation models the first
provenance repair, the independent determination that the RegionId-ordering
problem remains, the second bounded repair, new-head CI, full acceptance
review, normal merge and main readback. These fixtures do not prove that any
historical acceptance test actually ran or that a GitHub reviewer APPROVED.

The implementation has no network client, file write, scheduler integration,
model call, public mutator or production permission switch. It implements
the state transition contract:

```
OBSERVED -> WAIT / REQUESTED / NEEDS_DECISION
WAIT -> WAIT / REQUESTED / NEEDS_DECISION
REQUESTED -> RESERVED -> AUTHORING -> CANDIDATE_READY
CANDIDATE_READY -> PUBLISHED -> WAIT_CI -> WAIT_REVIEW
WAIT_REVIEW -> ACCEPTED / NEEDS_DECISION
WAIT_REVIEW -> EXHAUSTED immediately if second repair's CI or acceptance fails
ACCEPTED -> INTEGRATED (only after actual normal merge and main CI readback)
any uncertain publication -> BLOCKED (reservation remains spent)
two lifetime repair reservations consumed -> zero remaining; never reserve a third
```

A repairable independent finding enters REQUESTED when fewer than two lifetime
reservations have been consumed, even if the complete R3 ledger has no receipt
for the **future** attempt. REQUESTED does not reserve credit or permit code
generation. Only the shared manager can create and independently verify the
matching current-head R3 receipt before accepting the RESERVE transition.
A missing future receipt is not EXHAUSTED; a missing receipt **at reserve time**
fails closed. Once two reservations are consumed, any further request remains
EXHAUSTED, including after failed or uncertain outcomes.

The first normal read-only poll moves OBSERVED to WAIT; further polls preserve
the state and issue no notices. New independent findings may move WAIT into
REQUESTED or NEEDS_DECISION. Each notification is a fixed, non-sensitive
`Notification(kind, fingerprint)`: the kind distinguishes new failure,
required decision, published repair, exhausted budget and completed integration;
the fingerprint deterministically deduplicates the exact root/head/event
reference. No PR text or raw CI output is returned in a notification. Offline
replays of identical event histories return identical typed notifications.
`Result.attempts_remaining` is zero after the second reservation. Only a verified final CI result moves WAIT_CI to WAIT_REVIEW: pending,
unknown, missing required steps or incomplete snapshots stay in WAIT_CI,
allowing a later independently checked result on the same head without
spending a third reservation. A final CI failure or failed acceptance on
the second consumed repair enters EXHAUSTED after independent review.
If the technical review passes but required formal GitHub approval has not
arrived, NEEDS_DECISION preserves its exact evidence for a same-head
recheck. Only the independently observed approval bit may change before
ACCEPTED, without another repair reservation or self-APPROVE.
This is **not a durable notification database**: production integration must
persist delivered keys and reconcile them with existing task history across
restarts; replay-local deduplication alone cannot prevent repeated delivery.

### Handoff boundaries

- The **initial runner** retains its original one-issue/one-attempt policy and
  does not receive R5 repair authorization. A separately authorized independent
  reviewer classifies current-head failure evidence from R1, checks the original
  approval/acceptance criteria in R2 and issues a bounded request. A reviewer
  finding that requires a new expected result, interpretation or research
  authority produces NEEDS_DECISION, not an automatic repair.
- The **shared manager** alone owns R3 reservation/budget/lock state. Minor
  `footprint-pr-repair-v1` and substantive
  `footprint-pr-substantive-repair-v1` are distinct kinds; never dispatch the
  same reservation to both. A minor exhausted attempt is not an implicit
  upgrade to substantive. Lifetime two combined repair attempts per original
  Issue+PR, one minor maximum, two substantive maximum, daily substantive two,
  shared JST daily eight and at most one operation started per hour still apply.
  Neither an issue clone, a new day nor a different request ID restores credit.
- A **repair author** may prepare exactly one candidate on the reserved head
  and cannot Ready, approve, merge, add Issue approvals, or write a branch ref.
  Unknown semantic risk, deleted/skipped tests, relaxed assertions, removal of
  unknown results, private truth use, altered research interpretation or
  extended approval scope fails closed. R2 preflight, verified complete trees,
  original main-sourced isolated guard and full base→candidate diff are still
  required.
- The **shared publisher** must be the *only writer* for both repair kinds.
  It uses R4's durable once-only claim, fresh reservation/expiry and
  non-backward-clock checks, one expected-old-head, force=false CAS, and
  independent readback. An uncertain write is consumed, never retransmitted.
  Incomplete or missing-step same-head CI snapshots remain in WAIT_CI;
  success plus unknown also waits for the remaining CI. With a complete,
  independently verified required-step receipt, one confirmed CI failure
  may proceed to independent failure review even when the other CI is unknown.
  This never permits merge or treats unknown as successful.
  Only after new-head CI succeeds and the independent reviewer checks every
  original acceptance and unresolved finding may the separate review/merge
  task apply GitHub's actual protection conditions. Same-account COMMENT is
  not a distinct-account APPROVE if GitHub requires the latter.

The `Receipt` and `Event` inputs to `replay()` are **not authentication**.
A boolean such as `ledger_verified=True` or `reviewer_independent=True`
originating from a model or mutable PR body does not grant authority.
Before production, a real adapter must independently read and reconcile
R1 evidence, original approval, exact R2 manifests, the complete R3
persistent old/new history, current GitHub PR/main/CI/review records,
R4 detached Git objects, and scheduler/runtime state. No such trusted
production adapter or safe live write test exists in R5's offline code.
Do not treat state INTEGRATED in the simulator as a real merge.

### Saved automation configuration and rollout boundary

The R5 `rollout_gate()` returns separate offline, saved-config, machine and
real-operation statuses and a list of missing checks. It **always returns
`production_enabled=false` and `may_activate=false`**, even if a synthetic
input marks every check true. It does not edit task settings. A saved task
update is not an observed Windows deployment, and an offline merge is neither
a successful real repair nor proof of unattended completion.

The required rollout sequence remains:

1. Verify all R1–R4 normal PR integrations, final-head reviews, main CI,
   original authorized scope and the R5 latest-head CI/review. Confirm every
   existing active task by its *persisted ID and exact configuration*, not by
   display name. If task reads fail, STOP. Do not create a replacement timer.
2. On the Windows/WSL owner runtime, archive original task XML and enabled
   state, exact executable/runtime SHA, old daily/issue/results and new repair
   ledger SHA-256 plus modes, and process/lock state **before modification**.
   Verify the deployed hourly code actually reads R3 shared budget; a
   legacy runner that cannot see repair reservations may not remain active.
   All backups are outside the repository; no old ledger rewrites/deletions.
3. Observe the existing tasks and shared ledgers in a read-only shadow phase,
   across meaningful hourly and JST-day boundaries. Polling must not reserve
   model credits, trigger extra models or generate repair attempts.
4. Under the existing shared lock and confirmed process quiescence, migrate
   the **old minor writer** from direct ref changes to the same verified R4
   gateway and reconcile the independent review request format with both
   repair kinds. A partially saved/failed task reconfiguration leaves the
   substantive worker **disabled** and must not leave two writers enabled.
   Preserve the old configuration and old accounting records.
5. Only with explicit owner-approved target and separately verified live
   adapter may an operator consider a bounded attempt. Record reservation,
   detached candidate, single CAS, latest head CI, full independent review,
   ordinary merge and main CI. A historical closed PR #58 is **never**
   reopened as a live target. Log any human action; do not report it as
   unattended success. A normal merge by itself does not complete Issue #63.

Rollback must stop **all writers** and inspect every reserved, timed-out or
unknown attempt; never restore an old controller that ignores the R3 ledger,
discard reservations, reset the day counter, or start a parallel timer. If
verification fails, keep new substantive repair disabled and record the
specific missing receipt. Do not activate F22, relax research or CI contracts,
or add paid model invocations.

Offline checks (also included in the required existing Python CI step):

```
python3 -m unittest discover -s .github/scripts -p 'test_pr_repair_handoff.py' -v
python3 -m unittest discover -s .github/scripts -p 'test_pr_repair_*.py' -v
```

**Evidence update (2026-11-04):** R1–R5 and R5a have completed independent
technical review, normal main integration and post-merge CI. See the final
records on Issues #63 and #71; earlier proposal/dependency wording is not a
current blocker. K8 read-only machine inspection is recorded in UOB-6.
Saved-task rewiring, the complete production publisher, safe live write test,
real-time boundary shadow and a new approved real end-to-end cycle remain
**unverified**. Technical COMMENT is not formal GitHub APPROVED.


## R5a: GitHub-only read-only audit (Issue #71)

The standalone \`tools/pr_repair_remote_audit.py\` provides a pinned-repository,
**GET-only** GitHub REST reader and \`inspect_pr(reader, pr_number)\`. It checks
a stable main/PR head, same-repository main target, latest-head Actions CI
run and successful job/step records, distinct legacy commit statuses, submitted
reviews, REST inline review comments and branch protection/ruleset reads.
It uses bounded, complete API pagination and fails closed on missing pages,
403/401, stale head, red or pending checks, review objections or unknown
GitHub rules. REST cannot establish whether nonempty inline review threads
are resolved, so their existence remains a hold rather than assumed closure.
The transport refuses redirects, arbitrary URLs and methods other than GET.
No token, untrusted PR/Issue/review text or raw job logs enter diagnostic output.

Only the fixed, sanitized GitHub-only diagnostic state can be observed.
This **does not** authenticate independent technical acceptance, original Issue
approval, R1/R2 scope, R3 persistent ledgers, R4 publisher, saved automations,
Windows/WSL, shared locks, shadow observation, or real operations. Even an
apparently complete GitHub snapshot returns
\`may_publish=false\`, \`may_activate=false\`,
\`production_enabled=false\`. A future independently authorized operator must
validate every other R5 gate; this module is not a production adapter.
The full-mock tests live at
\`.github/scripts/test_pr_repair_remote_audit.py\`.

Run offline: \`python3 -m unittest discover -s .github/scripts -p 'test_pr_repair_remote_audit.py' -v\`.
The preexisting CI workflow runs these tests through its unmodified
\`test_*.py\` discovery. No Windows task, GitHub write API, model call,
automatic repair run or R5 rollout is performed by this module.

## UOB-6: operational evidence and complete local history, writer disabled

This infrastructure change implements the **read side** of the operational
adapter. It does not claim that the R4 production publisher or rollout is
finished. No activation switch is added; R4 still rejects every real adapter
on its execution path. No scheduled task imports the new modules.

- `pr_repair_adapter.GitHubEvidenceReader` extends the existing R5a pinned
  repository, GET-only, no-redirect transport. It accepts no log, write,
  arbitrary URL or arbitrary query route. Authentication is supplied by a
  separately managed caller; no token discovery, authentication-file access,
  environment dump or credential persistence exists in the adapter.
- `GitObjects` retrieves commit identities from authenticated GitHub Git
  endpoints, walks non-recursive trees, recomputes each tree OID and verifies
  exact blob bytes/OIDs. Bounds, truncated/missing entries, path collisions,
  empty subtrees, special changes and malformed encodings fail closed.
  GitHub's commit metadata is not a local rehash or signature verification of
  every raw signed-commit byte. R2-shaped inventories and R4 candidate checks
  use independently retrieved objects rather than the candidate's manifest.
- CI observations bind the PR, head, workflow run, attempt and jobs, and
  reread both the run and latest-run list. A newer pending run invalidates an
  older green snapshot. Job/step success is metadata, not proof that all
  independently approved acceptance requirements executed. No logs are read.
- `pr_repair_provenance` rereads original Issue/approval records and an exact
  submitted review against **separately provisioned operator anchors**.
  Source IDs, numeric actors, unedited bodies, timestamps, original Issue
  content, protocol commit and the complete R2 approval digest must agree.
  A Python object, digest, mutable Issue or request cannot authenticate its
  own anchor. Existing prose approvals do not automatically become R2 scope
  manifests. Missing independent bindings are explicit holds.
- An exact COMMENT and an APPROVED review remain different observations.
  CHANGES_REQUESTED is retained as a hold. Same-account technical-review
  independence needs a separately verified agent-run source; it is not
  inferred from a claimed reviewer name or run ID.
- `pr_repair_guard_adapter.validate_detached_guard()` connects the reviewed
  main guard to complete independently verified trees. It fetches only the
  approved changed blobs and main's guard, verifies the guard against a
  code-reviewed allowlist, and applies full and repair patches inside private
  scratch Git with a fixed credential-free process environment. Candidate
  code is never run. Exact post-application tree/blob checks are required.
  Scratch commits are explicitly synthetic anchors for the verified trees;
  they are not presented as the original remote commits. Unchanged blob
  bodies are not fetched or claimed verified. This is an explicit isolated
  validation operation, not part of read-only `inspect()` or shadow polling,
  and has only been exercised on synthetic sources in this change.
- `pr_repair_history.audit_history(state_root, aware_time)` reads old
  `daily`, separate `attempts` and adjacent `pr-repair-ledger` without
  acquiring locks or writing. It reuses R3/hourly validation and checks
  cross-store duplicates, overlapping slots, shared daily limits, event
  transitions, clock rollback and before/after content/metadata snapshots.
  Unknown/missing results stay consumed. An invalid store returns unknown
  counts, never zero available credit. Logs are statted, not read.

### Existing K8 records and Monitor cause

UOB-6's real read-only check found ten daily reservations with ten matching
Issue markers, plus a separate consumed Issue #10 manual invocation. The
new repair directory did not yet exist. The new history reader reconciled
all eleven initial Issues, with zero reservations for the observation day
and unchanged snapshots. Daily results contain eight `succeeded` and two
`failed` labels; these are runner records, not independent PR-success proof.
Issue #10's record is a consumed historical invocation, not fresh authority
derived from the record's authorization prose.

The old minor writer's complete GitHub reservation/result history is not
established by those local files. `legacy_minor_history_unverified` remains
an explicit hold. No history is copied into a new reservation, discounted,
deleted, overwritten or re-enabled. Local observation is not an atomic
reservation barrier; future authorized execution must reconcile under the
existing shared lock.

The deployed Monitor pins `local_autodev.py` to PR #6's final content.
PR #20 changed that content on October 28; PR #35 subsequently introduced
the current structured-edits runner. The Windows and WSL copies of
`common.py` match each other. The cause is stale Monitor pinning against a
checkout that follows main, not transfer corruption. Its check fails before
the queue is read. Fixed-schema state records show October 28 at 09:00 was
okay, then October 29 through November 4 reported `preflight_failed` with
no AI invocation. Those records do not identify every historical failure's
sole cause.

Daily does not import that Monitor wrapper and uses its own runtime
fingerprint. The pin mismatch itself therefore blocks the Monitor, not
Daily. The Monitor only checks the old queue and cannot verify the R3 shared
budget or repair success even if repaired. Its dry-run also writes state,
locks and ACLs. It was not executed or modified. Any future pin update needs
a review of the wrapper's current semantics, not blind hash replacement or
restoration of an obsolete runner.

### Verification boundary and remaining integration

Offline tests use synthetic GitHub records and temporary private state.
They exercise missing/edited sources, caller-self-approval, malformed trees,
exact blobs, same-head reruns/new runs, history races, duplicate attempts,
conflicting publication/CI events and consumed unknown outcomes. The full
existing Python suite and repository CI remain required.

The fixed `inspect()` result always has `may_publish=false`,
`may_activate=false` and `production_enabled=false`. Remaining integration
must provide independently authorized anchor provisioning, reviewer-run and
CI-failure/acceptance classification, complete old minor history and the R4
trusted bundle. R1/R2 booleans or a caller-built `TrustedFacts` cannot fill
those gaps. A reviewed source guard and verified Git data are separate from
an approved repair decision.

There is no live CAS transport in this change. GitHub REST reference updates
document `sha` and `force`, not a transactional expected-old-head parameter:
[GitHub reference API](https://docs.github.com/en/rest/git/refs#update-a-reference).
A connector argument alone does not prove server-side CAS. Implementing or
testing a writer must not silently replace the expected-head contract with
GET-then-PATCH. A safe approved target, conflict/timeout/readback validation,
single-writer cutover and durable notification delivery are still required.

Keep the existing daily eight/hourly one limits and lifetime caps. Preserve
all three local stores and old minor records on rollback; never restore a
controller that ignores R3 reservations. The owner has authorized this
implementation PR and independent review, not merge, task/Automation changes,
model calls, production reservations, write experiments or production rollout.
