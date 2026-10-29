# M1-F19 reconciliation and clean execution verification

## Scope and prior outcome exposure

This record supplements the frozen [F19 plan and original results](m1f19-nested-point-support-density.md).
Registration remains `9039417314df4c11fe5bc088825bf5f25eaf4d98`; its protocol, F16 input,
seeds 0..199, three density arms, eight profiles, support, ordering and predicates are unchanged.

The original research checkout already contained an uncommitted F19 implementation, generated
results and interpretation. Those outcomes were inspected during reconciliation. The implementation
was preserved and reused; this is a reproduction and validation of previously observed outcomes,
not a new prospective experiment. Additional verifier checks and the separate S4 domain correction
were made after outcome exposure. Neither changes the registered F19 manipulation.

## Execution and validation

Both complete F19 executions used clean source commit
`621949087bf1c94fad0e7118c9803befde5bfa26`. Their manifests record `dirtyWorkingTree: false`.
The independent verifier records the same source, verifies the clean source manifest, and returns
`allVerified: true` for individual results, aggregates and every CSV column.
This document records that execution revision; it does not attribute its outputs to a later commit.

The credential-isolated local validation completed:

- Environment check, build and formatting verification.
- 217 Python tests and 457 .NET tests.
- 1,120 scenario/seed/strategy evaluations: seven scenario definitions × 20 seeds × eight strategies.
  These are regression runs, not additional independent F19 samples.
- F16, F17 and F18 replays: all 18 artifacts preserve their scientific contents; differences are
  limited to recorded source commit/dirty metadata.
- Two full F19 runs, with all six complete files byte-identical, including their manifests.

The S4 all-strategies failure was reproduced on the old F18 source and corrected separately.
Its original 11-region evaluation domain and full public topology are retained. A predeclared
public search scope now consistently bounds reported strategy results; scope omission is not
logical exclusion by evidence. The existing three S4 tracing strategies retain their scientific
outputs. F19 uses its unchanged F16-derived lattice and predicates.

Evidence directory names are `f19-a`, `f19-b` and `f19-independent`; the latter contains
`summary.json` and the independent per-world calculations. Generated results remain untracked.
At the initial local-report snapshot, CI, PR publication/merge and operational deployment
were pending. The later verified publication/deployment record below supersedes that status.

### Reviewed publication, CI artifact and deployment

[PR #48](https://github.com/uobnnisiuk/footprint-sim/pull/48) integrated the research baseline
at `2f182064fa0b9e6eae7319a8cffdcd29505ca702`;
[PR #49](https://github.com/uobnnisiuk/footprint-sim/pull/49) integrated the reused F19 work
at `77f7951e1f9bca3beb46f75dffd087e6f13edd37`.
[PR #50](https://github.com/uobnnisiuk/footprint-sim/pull/50) added the separately reviewed
fixed research CI and state-file protection at `ab88780d872f958520a619d3e23f795081622aad`.
All three passed independent agent review and their exact-head GitHub CI; no human GitHub
approval is implied by that description.

The [full research CI run](https://github.com/uobnnisiuk/footprint-sim/actions/runs/40807614373)
executed clean source `1e6c8acf8bafc3c164d34c44530de939edd054cd`. It passed 241 Python tests,
457 .NET tests, build/format, both sources' 18 reference artifacts, two full F19 runs and
independent verification. Its [artifact](https://github.com/uobnnisiuk/footprint-sim/actions/runs/40807614373/artifacts/12869051226)
was downloaded and checked: all 68 recorded file hashes match, `complete` is true, and ZIP
SHA256 is `a5f5b778a49b2d0009b73385454165b646435f1f62651815e43395da7b3224ca`.
The six CI outputs match the local scientific outputs above; the report and manifest differ
only in their truthful execution-source SHA. The two CI runs are themselves byte-identical.
GitHub retention ends on 2026-11-28 at 02:41:30 UTC; the checked ZIP is also preserved locally
outside Git. The artifact contains full results, independent calculations, logs and hashes.

Production checkout deployment was verified at `ab88780d...`: all 230 tracked source files
matched that reviewed version, including runner, converter, schema, guard, verifier and CI.
Detached HEAD and the two known ordinary `.pyc` files were retained. The original research
worktree and the other original worktrees, all 1,568 original result files, and all 50 prior
execution-state files remained unchanged. No failed attempt was removed or retried.

The Windows-to-Ubuntu/higuchi login-shell dry-run returned `DRY RUN: queue empty`, exit zero,
with no model invocation or slot reservation. At 2026-10-29 11:47 JST the existing Daily task
was restored to Ready with its original eight triggers, ordinary interactive privileges,
IgnoreNew, zero automatic retries, no wake, and 40-minute limit. Monitor's definition was
unchanged. Task Scheduler reported the next run as 2026-10-29 12:05 JST at that observation;
this timestamp is historical evidence, not a forecast for future readers.

The [state file](research-autodev-state.json) records `f19_complete`, the real integrated
ancestor, runtime hashes and evidence. Final docs-only bookkeeping and checkout identity
are recorded in [Issue #47](https://github.com/uobnnisiuk/footprint-sim/issues/47).
There is no remaining F19 implementation to enqueue. Issue #44 stays held; #13/#21 remain
unlabelled historical failures with incomplete finite-input acceptance, not completed work.
The next mapping contract still needs separate preregistration and approval. No production
model success was tested, and no manual model call was made in this work.

## Immutable inputs and output identity

| Input or signature | SHA-256 |
|---|---|
| Frozen F19 experiment | `f0669251ab8b747fbd4b7517e169d70723f052ae0465d33cfac6d5fe355c6bbd` |
| Frozen F16 experiment | `e717669c7178dcf8b278b831b6ee049a2aa8dae5a9c9e6366b8c32712649ca39` |
| Original F16 reference JSONL | `6c114058f7e9479ae29e3d16030f10843ddfb71b8b8bc861d13f8840c0cd4c81` |
| Original F18 point reference JSONL | `c5b62f9bbad305e1ee3176b86105d1a631d4e3ebeb6868703d41d5c76c0d4d81` |
| Fixed input signature | `673e6a5a9785c782ccebb1571465ddc48d2602f46350f39fdcd4b319be34cc9c` |
| Public output signature | `3b7a8b49be6258616bdbfda05a3551f39d0283edd3c815173f4dc0f46343026a` |

The F18 reference is now mandatory when verifying actual F19 results and its hash is pinned.
The original protocol's baseline final commit remains
`77611bc52976178857ac442be6e07a9caa1a2a9d`.

The following hashes were independently read from both output directories and match pairwise:

| File | SHA-256, identical in both runs |
|---|---|
| `candidate-density.json` | `bd24212c69afa2e867a3ba8275a09a68bce4cd56f8b17f2803efad195b8c6d7a` |
| `candidate-density-runs.jsonl` | `f2427db96d118afa5214d5e264de6b3c343a8fff4feb2ccc0011329a03891a3a` |
| `candidate-density-profiles.csv` | `82157963b7b14366089346a669480d0dd49de97ec5e61ab2a1780e4d1b258950` |
| `candidate-density-conditionals.csv` | `cd373e3615b18d51caf6e1f0ed6dea6fe9e6bbac7b8877b68f6f9a86031be414` |
| `candidate-density-crosswalk.csv` | `345c5a6c4462062e12e0426131e5dfb42b1f60c4cfce837511e599c7a64942c5` |
| `candidate-density-manifest.json` | `7651e77360e2f463587dafddfd236d42b69da1e29c88e7ce1461abf3b092ef2e` |

## Independent checks and reproduced outcomes

The independent unit is one original world: 200 worlds yield 4,800 primary profile evaluations.
The 4,800 reference profile evaluations and three paired arm contrasts are not independent samples.
All primary evaluations retain truth; false exclusions and invariant violations are zero.

The verifier independently checks 7,367 missing-cell coordinates and hash ranks, 2,412 original
interval-to-point bridges, 9,779 final crosswalk entries, 4,800 profile memberships, 5,400 conditional
diagnostics, 16,800 within-arm profile equivalences, 600 arm minima/occupancies, 600 paired-world
comparisons, 7,200 Joint inclusions and 1,600 full-support analytic profile counts. It also checks
content signatures and all 4,800 / 5,400 / 9,779 profile / conditional / crosswalk CSV rows.
Native candidate hash factories are not independently reimplemented; coordinates, uniqueness,
provenance and preserved IDs are checked against the fixed references. Integer memberships/counts
are exact; decimal fraction comparisons use tolerance `1e-12`.

| Metric | Point baseline | Half missing | Full support |
|---|---:|---:|---:|
| Mean universe count | 12.060 | 30.235 | 48.895 |
| Mean Full-profile candidate count | 2.045 | 3.465 | 5.015 |
| Mean Full reduction fraction | 82.76% | 88.19% | 89.28% |
| Constraint-to-candidate plateaus | 168 | 44 | 0 |
| Worlds requiring Full within the eight profiles | 31 | 91 | 114 |
| Mean minimum field count | 1.990 | 2.425 | 2.565 |

Baseline to full support changes the minimum family in 115/200 worlds (57.5%), the minimum field
count in 106 worlds, and the minimum candidate count in 176 worlds. These are distinct measures.
It exposes 266 previously zero conditional effects: 213 appear at half filling and 53 appear only
after completion. No positive effect is lost. There are 369 lost profile equivalences across
127 worlds and no newly equivalent pairs. Region-top, implication and Time-top flags remain
unchanged in this cohort.

At full support the 212 remaining plateaus are 84 observation-to-constraint, 127 overlap and one
single-candidate saturation case. Removing all constraint-to-candidate plateaus is a preregistered
algebraic consequence of complete Cartesian support, not an empirical discovery of better rescue
performance. Full candidate burden increases even while its relative reduction increases.

The numerical tables and primary conclusions match the previously inspected original results.
The changes to the validation record are the completed clean replay, stronger immutable-reference
verification, expanded regression suite and repaired S4 domain boundary, not changed F19 outcomes.

## Limits and next research decision

These results measure missing candidate witnesses on one fixed point lattice. They do not establish
general field requirements, real-world safety, calibrated probabilities or search costs. Empirical
tick/Region marginals change with support completion; density is not independently varied at fixed
marginals. Safety here is structurally protected by keeping a truth-containing baseline. Additional
model candidates are neither additional observations nor additional people or physical search sites.

The evidence supports ending arbitrary lattice-density refinements for this question. The next
priority is a separate, explicit public mapping from historical candidate states through declared
temporal transitions to possible current locations and action coverage. It must support many-to-one
and one-to-many mappings, retain unknown/unmapped possibilities, and test outer-safe coverage without
truth-based selection. Counts of mapped locations and covered actions should precede cost estimates.
Action-generated observations, inference updates, calibrated resource/time costs and real-data
validation remain subsequent work. No such successor experiment is registered or executed here.
