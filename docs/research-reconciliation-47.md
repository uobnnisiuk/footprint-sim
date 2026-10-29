# Research reconciliation for Issue #47

This records the 2026-10-29 integration of the existing M1-B through M1-F18 research and the registered F19 plan with reviewed automation main. The controlling request is [Issue #47](https://github.com/uobnnisiuk/footprint-sim/issues/47), under `footprint-research-reconnect-v1`. This document is an integration evidence record; it does not declare F19 complete, authorize another unattended research task, or claim production deployment.

## Source identities and preserved work

| Role | Commit |
|---|---|
| Common ancestor used for reconciliation | `33f2755546125ca8ade71cf46168f4366707e33a` |
| Main when local work started | `30af71e5b6280b7e65792fbd840601de6b806442` |
| Research branch and frozen F19 registration | `9039417314df4c11fe5bc088825bf5f25eaf4d98` |
| Completed F18 source used for baseline reproduction | `77611bc52976178857ac442be6e07a9caa1a2a9d` |
| Reviewed main update from PR #46, retained during integration | `ee3f3ff4c8a73586a4d007fd173797a3604f7553` |
| Reconciled research/main merge | `9779f9de80335272931cf1ada259643db6631100` |
| Separate S4 public-domain correction | `6c65bd6022263c0161b3e2daf5ddb2ba5c27984f` |
| Integration source validated below | `bc5251157eecf5afae42d1a41a43808a1338bb0a` |
| Separate reconciled F19 implementation candidate | `621949087bf1c94fad0e7118c9803befde5bfa26` |

The original research worktree was not reset, cleaned, stashed, or overwritten. It contained nine modified or untracked files, comprising three tracked edits and six new files:

- `README.md`
- `docs/m1f19-nested-point-support-density.md`
- `src/FootprintSim.Cli/CliApplication.cs`
- `scripts/verify-candidate-density.py`
- `src/FootprintSim.Core/Evaluation/CandidateDensityContracts.cs`
- `src/FootprintSim.Core/Evaluation/CandidateDensityOutputWriter.cs`
- `src/FootprintSim.Core/Evaluation/CandidateDensityRunner.cs`
- `src/FootprintSim.Core/Evaluation/CandidateDensityTransform.cs`
- `tests/FootprintSim.Core.Tests/M1F19Tests.cs`

These implement the F19 transform, contracts, runner, output writer, CLI, tests, independent verifier, and report additions. They were preserved and reconciled for reuse rather than treated as missing work. Existing result directories and written F19 outcome claims were also present and inspected. This continuation is therefore outcome-exposed reconciliation and reproduction, not a previously unseen first prospective experiment. The registered design, seeds, support ordering, and evidence/truth boundaries remain fixed.

Private preservation evidence is stored outside Git under `$HOME/.local/state/footprint-research-reconnect/20261029T1046/`. `local-source/` contains the nine source files, `registered/` contains frozen protocol copies, and `research-changes.patch` records the tracked local changes. The manifest records per-file SHA256, size, and mode, worktree identities, and execution-state hashes. It inventories 1,568 prior result files totaling 5,612,833,822 bytes (about 5.61 GB).

| Preservation artifact | Bytes | SHA256 |
|---|---:|---|
| `preservation-manifest.json` | 329,586 | `95449dda9c8d035b382f4d6e98e6d1852a00d3225ccef3df6428f91bc959ad7c` |
| `prior-results-complete.tar.gz` | 305,370,140 | `270279a65c0f8bcff77a9851cd326cb96ba6697702e4f7aff121c8687450ac03` |

The complete archive hash was checked against the manifest. The older `prior-results.tar.gz` in the same directory is not the complete archive identified here. Raw results, archives, and private execution state are not added to this repository. Windows task-definition backups and a copy of the preservation manifest are also retained in the Work workspace's `work/research-reconnect-records/` directory.

The frozen F19 inputs were hashed from their Git blob bytes at `9039417314df4c11fe5bc088825bf5f25eaf4d98`, separately from later local report additions:

| Registered path | SHA256 |
|---|---|
| `docs/m1f19-nested-point-support-density.md` | `ea27087640df341c43af4fafc3dadc21987bb60548d91a8cf470e53cb12e43a6` |
| `experiments/m1f19_nested_point_support_density/experiment.json` | `f0669251ab8b747fbd4b7517e169d70723f052ae0465d33cfac6d5fe355c6bbd` |

## Integration decisions

Integration used a separate worktree and branch, `integration/research-reconnect-47`, created from then-current main. The source histories were compared at the common ancestor before resolving the merge. No blanket ours/theirs selection or old-main rollback was used. PR #46 was left to its existing review process; its merged main commit was then brought into the integration branch without replacing its work.

| Area | Resolution |
|---|---|
| `AGENTS.md` | Retain every original safety rule and the research additions covering public hazard structure, stable random streams, interaction truth boundaries, historical constraints, candidate standings, telemetry, tracing, and independent past-state verification. Main had no competing change from the common ancestor. |
| `ScenarioValidator` | Retain research schema 1 through 5 and its stochastic, weft, tracing, and past-state checks. Also retain main's finite radius, passability, and travel-cost checks and precise diagnostics. Finite negative travel costs retain their prior accepted behavior. |
| `SimulationContractTests` | Keep main's determinism, input-order ties, nonfinite topology, and observation-delay regressions. The original research branch had no changes to this file. The later PR #46 additions are also retained. |
| `EvaluationHarnessTests` | Resolve the sole textual merge conflict by retaining both the intermittent-S2 regression and the S1 first-useful/final-usefulness regression as separate tests. Keep research provenance fixtures and all main identifier/availability checks. Assert the exact eight strategy identities and exercise the full registry. |
| Public input contract tests | The S4 fix adds one public domain field, bringing the input property count from seven to eight. The two additional reflection assertions in `CandidateWarpTests` and `InteractionModelTests` were updated to recognize that field while retaining their truth-boundary checks. |
| Evaluation schemas and CLI | Preserve the research implementation and dedicated experiment commands. Ordinary evaluation records and manifests use schema 3; schema 1 and 2 documentation is historical. Existing field meanings, outer-safe candidate semantics, and preferred/outer distinctions remain intact. Scenario schema numbers and experiment-specific schemas are separate contracts. |

Both S2 scenarios remain with their existing full IDs and paths. `s2-intermittent-person-evidence` at `scenarios/s2_intermittent_person_evidence/scenario.json` is a schema-1 fixture: fixed S1 physical truth with intermittent target-person evidence, including a 0.5 drop probability at transport start. `s2-branching-debris-transport` at `scenarios/s2_branching_debris_transport/scenario.json` is the schema-2 research fixture with stochastic physical branching and person/device separation. They are distinct experiments, not alternative copies of one scenario. Their names, bytes, and scientific roles are not collapsed into a generic “S2.”

Manifest source identity is kept truthful. A source-commit or dirty-tree change is recorded as provenance, not concealed by rewriting a manifest and not automatically counted as a scientific-output change.

## Pre-existing S4 mismatch and bounded correction

The completed F18 source was reproduced in a credential-isolated environment. Its build had zero warnings and errors, all 357 .NET tests passed, and the known S4 all-strategies seed-0 evaluation exited 2 with:

```text
Strategy 'cross-thread-constrained' classified unknown region 'person-b-home'.
```

Evidence is in `sandboxes/f18-baseline-v4/execution.json` and `execution.log` under the preservation directory. This is the same pre-existing mismatch disclosed in [the F18 report](m1f18-candidate-interval-width-paired.md), not a newly introduced integration regression.

S4 declares an 11-region target evaluation domain but provides a larger public map. Its radius-24 geometric baseline can include `person-b-home` and `person-c-home`, both about 22.36 units from the confirmed target-home position. The old strategy result/timeline could classify these public but out-of-domain regions, while the evaluator correctly rejected classifications outside its declared denominator.

The separate correction fixes the public query domain from scenario metadata before simulation and passes that same domain to every strategy tick. Full public topology, observations, historical candidate states, and tracing internals remain available. A projection scopes reported candidates, preferred/outer/conflicted standings, confirmed regions, explicit exclusions, and candidate provenance. Regions omitted by query scope are not added to logical exclusions. Unknown regions absent from the full public map are rejected rather than silently removed. A null scope retains legacy behavior; an explicit empty scope remains empty; no-op projection preserves the original result and ordering.

The final weft/tracing standing sets are projected after their specialized overlays as well as in the common result path. Tests cover each reported field, invalid domains and invalid map regions, full-topology reasoning from an observation outside the query domain, and all eight S4 strategies across its ten conditions. The three documented tracing strategies are checked against their unscoped results at every evaluated tick. The adjacent pre-existing nonfallback input-identity validation concern is outside this bounded correction.

An independent output comparison additionally ran S4's documented `own-evidence-constrained`, `single-weft`, and `multi-weft-tracing` strategies for seeds 0 through 199 on F18 source `77611bc...` and integration source `bc525115...`. All nine output artifacts were compared. Eight are byte-identical: `aggregate.csv`, `aggregate.json`, `causal-tracing.csv`, `causal-tracing.json`, `conditions.csv`, `paired.csv`, `runs.csv`, and `runs.jsonl`. The only difference in `manifest.json` is its exact `sourceCommitSha`; the dirty flag did not change. Scientific bytes are therefore unchanged for this registered comparison. Per-file original and actual hashes and the exact metadata difference are recorded in `s4-compatibility.json`.

## Validation snapshot and remaining gates

The following completed isolated run identifies source `bc5251157eecf5afae42d1a41a43808a1338bb0a` in `sandboxes/integration-v2/execution.json`; the execution exit code is zero and its log ends with `INTEGRATION_CHECKS_PASSED`.

| Check | Observed result |
|---|---|
| Python automation regression suite | 197 passed |
| .NET Release build | Succeeded, zero warnings and errors |
| .NET tests | 411 passed, zero failed or skipped |
| Format | `dotnet format --verify-no-changes --no-restore` passed |
| S0 and S1 sample runs | Completed at seed 42 |
| All checked-in scenarios | 1,120 evaluations: 7 scenarios, seeds 0 through 19, all 8 strategies |
| Documented S4 comparison | 600 evaluations: 200 seeds and 3 strategies; scientific-output compatibility verified as above |

The earlier integration run had 409 total tests: 407 passed and two public-contract reflection assertions failed after adding the domain field. Those failures were not suppressed: the specific assertions were corrected in `bc525115...`; PR #46 supplied two further tests, and the 411-test rerun above supersedes that failing result. This local evidence does not substitute for the integration PR's independent review and required CI, which remain publication/merge gates at this document snapshot.

The separate F19 candidate reuses the preserved local implementation. Full F16/F17/F18 replay, the two clean-source F19 runs, independent verification, six-artifact comparison, and interpretation are tracked with that candidate and its subsequent PR. An experiment process completing or a test count alone is not F19 completion. Those F19 completion gates do not prevent reviewing and merging this separately validated baseline integration; conversely, baseline integration does not satisfy them.

## Automation and concurrent-work boundary

Relative to reviewed main `ee3f3ff4c8a73586a4d007fd173797a3604f7553`, this integration leaves `tools/` and `.github/` unchanged. The runner still accepts only exact replacements in existing ordinary UTF-8 files under its current path, mode, and size guard. It does not gain new-file support, access to `experiments/` or `scripts/`, registered research-experiment CI, artifact-upload support, or permission to modify guard/workflow code. Candidate code is not run on the credential-holding owner host. The isolated research validation route is distinct from the unattended model runner.

The existing Daily task was idle before being temporarily disabled under the owner's migration authorization. Its definition was backed up to prevent concurrent implementation while Work owns reconciliation, integration, and F19 validation. No in-flight model task was interrupted and no production model was manually started. The Monitor task, credentials, eight daily trigger definitions, usage ceilings, and immutable attempt ledgers are retained. Issue #44 remains on migration hold; failed Issue #13/#21 attempts are not deleted, retried, or duplicated under new issue numbers. See the [local-start status comment](https://github.com/uobnnisiuk/footprint-sim/issues/47#issuecomment-6485646657).

`docs/research-autodev-state.json` is intentionally deferred to the later F19 handoff PR so that `integrated_main_sha` can name a real reviewed integration baseline on main. No future self-referential SHA is invented. Until evidence and deployment checks are complete, do not advertise `research_ready`, replenish unrelated old-main chores, or enqueue overlapping F19 work. The existing runner does not enforce this state file or research dependencies; a manifest or label cannot expand its capabilities. The final handoff must record the remaining scope explicitly, retain an empty automatic research path allowance while Work owns it, and independently verify review/CI/deployment evidence before any subsequent promotion.

Production deployment, final source-byte and ledger-preservation checks, queue inspection, and Daily task restoration with a dry-run and actual next-run time remain explicit end-of-transition gates. The task must retain its original eight JST times and normal privileges. No manual production model invocation is needed to demonstrate readiness. This research remains synthetic and supplies no claim of real-world rescue accuracy.
