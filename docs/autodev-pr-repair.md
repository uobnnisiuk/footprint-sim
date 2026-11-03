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
