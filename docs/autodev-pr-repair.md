# PR repair: R1 evidence (read-only)

Status: **R1 prototype / evidence only**. This does **not** activate substantive repairs, add model invocations, authorize a merge, or change the existing Windows/WSL runner. It is an offline input to R2 contract validation and independent review.

## Boundary

- Historical fixture: PR #58, Issue #56, three **distinct** head SHAs and exact normal/research run IDs recorded in Issue #59.
- Real GitHub Actions run IDs and normal job IDs were checked against GitHub. The 1 and 28 failed-test labels are **synthetic placeholders**, not verbatim test names or downloaded raw logs. The common reason codes reflect the documented independent review. The source summaries (503+1 / 503+28 / 531+0) are historical observations, not current CI status.
- A raw log, arbitrary URL, injected instruction, or text from a model is never interpreted as an action. Do not publish credentials or raw evidence logs.
- GitHub's current approval, required checks, PR protection, and independently executed review must still be checked by a separate reviewer. A verified flag in the input is an **evidence claim**, not an authority grant.

## Pure normalization

Run offline:

    python3 tools/pr_repair_evidence.py --snapshot .github/scripts/fixtures/pr58-repair-evidence.json

Run the tests (also discovered by the existing CI workflow):

    python3 -m unittest discover -s .github/scripts -p 'test_pr_repair_evidence.py' -v

The normalized JSON uses strict known keys, exact head/run/attempt association, deterministic sorting, a digest without observation time, and a grouped failure summary which still contains every individual failure record. No automatic attempt is made to interpret log content or fix code. Invalid, duplicated, truncated or mismatched records fail closed.

An acceptance item has independent status: not_implemented, tested_not_run, passed, failed, needs_review. Claiming passed is insufficient: it needs an independent review identifier and at least one test reference on a successful run for **that exact head**. CLI and automation tests are not a substitute for the project's scientific acceptance rules.

A workflow is not passed merely because a run summary says success: at least one complete successful job and a successful required step must be present. Missing run/jobs, pending, cancelled, skipped required steps and incomplete pagination never imply success. The output always says may_merge=false and may_repair=false. R2 must bind its ApprovalSnapshot to trusted sources and compare full base→candidate and head→candidate diffs; this R1 file does not create that authority.

## Independent-review corrections / fail-closed limits

- The full four-item PR #58 approval baseline is pinned in code for this **historical fixture only**. Omitting even one accepted requirement now produces `hold_acceptance` with `missing_approved_acceptance`; modifying a snapshot's own approval label does not alter the baseline. For any other PR, no baseline is inferred from Issue text, model output, or a self-declared snapshot: `normalize()` holds until the independent reviewer supplies an externally verified exact set. The CLI cannot supply or self-approve that set. This is still evidence, not authority; R2 must bind trusted provenance.
- Successful runs require a `log_evidence` state of `verified_available` with a reviewer reference. Missing, expired and unverified logs never produce a passing run. The historical final PR #58 record references its independent technical review; new GET-only adapter reads mark log availability `unverified` because GitHub run metadata alone cannot prove live logs. This field is a provenance claim to check, not proof of access to a live log.
- The historical normal/research required-step policy is pinned in code, rather than accepting the snapshot's `required=false` flag. A missing, renamed, unknown, or falsely optionalized step prevents a success result. New/changed workflows need a separately verified policy before their results can be considered complete.
- Raw arbitrary step names and failure IDs are never emitted; unknown step names are hashed and keep the CI result unknown. Failure reason codes are a fixed enum; unknown/untrusted reasons are rejected. The GET-only adapter emits hashes of workflow and step names, never their raw text. Do not forward raw CI logs or secret-bearing metadata.
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
