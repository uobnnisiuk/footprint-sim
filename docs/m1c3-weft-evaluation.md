# M1-C3 weft evaluation

## Purpose and adopted research boundary

M1-C3 connects the M1-C2 historical constraint applicator to a formal synthetic scenario and measures the incremental effect of available cross-thread evidence. The research question is whether a cross-entity observation can reduce a preferred candidate warp without falsely presenting an outer-plausible alternative as impossible.

The adopted concept baseline is `footprint-research-concept-baseline-v0.2.1.md`. Design also considered `causal-weave-session-note-nexus-resolution.md`, `causal-weave-full-session-research-archive.md`, and `causal-index-session-synthesis-2026-08-29-full-v2.md`. Numeric toy results in those archives are not acceptance criteria or reproduced results here; only the versioned S3 scenario, code, seed, manifest, and outputs produced by this repository are M1-C3 results.

This is not evidence that Causal Weave, 24 lenses, Index Access, or a Causal Nexus formalism is valid. It is a bounded synthetic comparison.

## S3 synthetic scenario

`scenarios/s3_cross_thread_encounter/scenario.json` contains target Person A, Person B, a target device, a person-person encounter, and a branching synthetic debris transport. The target person and device are propagated once per scenario/seed by the same stable stochastic engine used by every strategy. Strategy selection never regenerates or changes truth.

The encounter produces three distinct representations:

```text
private Encounter SimulationEvent
        -> private InteractionOccurrence

independent synthetic evidence definition
        -> InteractionObservation
        -> CrossThreadConstraint
```

The evidence-side observation deliberately omits a truth-object reference. Its relation kind, participant roles, exact tick or inclusive interval, observed window, available tick, evidence state, visibility, and provenance remain machine-readable. A missing or unavailable observation is not converted into a claim that the encounter did not occur.

The scenario assigns one of six conditions by `seed mod 6`, in declared order. This assignment is deterministic and does not alter hazard truth:

| Condition | Public search behavior |
|---|---|
| `Confirmed` | Public at tick 10; eligible for historical hard narrowing. |
| `SupportedButUncertain` | Public at tick 10; preferred branch may narrow while the outer alternative remains. |
| `Delayed` | Confirmed but unavailable until tick 16. |
| `Conflicting` | Public conflicting claim; candidates are marked conflicted rather than deleted. |
| `Unavailable` | Private evidence; not exposed to the public search context. |
| `DeviceOnly` | No person encounter constraint is derived; the ordinary device-only fix remains device evidence. |

Across successive six-seed blocks, the observed encounter alternates between an exact tick and an inclusive interval. This preserves a regression fixture for Event versus State Interval without implementing duration or continuity inference.

## Paired strategy design

The primary cumulative comparison is:

1. `last-confirmed-only`: target's last confirmed region;
2. `hazard-aware`: last-confirmed/public observations plus the public hazard envelope;
3. `own-evidence-constrained`: a public candidate warp reconstructed from target-owned evidence and propagated with the public envelope; and
4. `cross-thread-constrained`: the same own-evidence warp plus only the cross-thread constraints available at that evaluation tick.

The two existing `voluntary-reachability` and `evidence-aware-current` reference strategies remain in `all` for continuity. Only `cross-thread-constrained` receives and applies `CrossThreadConstraintSet`. Every strategy for a scenario/seed is evaluated against the same `SimulationRun` and evaluator-only truth context.

`OwnEvidence` versus `CrossThread` is the primary paired ablation. Aggregate output records `CrossThread - OwnEvidence` deltas for candidate counts, reductions, containment, time-to-guidance where both values are available, and persistence.

## Target-specific searchable denominator

Schema 3 adds optional `evaluationSearchableRegionIds`. S3 uses ten target-relevant regions and excludes Person B's private starting shelter. Auxiliary entities therefore cannot inflate apparent reduction. When the field is absent, S0/S1/S2 retain the previous denominator: every declared scenario region.

## Historical application and public forward propagation

The weft-aware strategy does not intersect the encounter region with final candidates. It builds a public candidate state at tick 9 or interval 8..9, applies an eligible constraint at that historical location, and then derives evaluation-tick candidates with `PublicHazardEnvelope` only. It never consumes the selected transition, realized terminal, `InteractionOccurrence`, random draw, or private entity trajectory.

An optional public secondary-transport branch retains both the deposited source region and the downstream possibility. Publication of a possible release is not proof that release occurred.

## Preferred, outer plausible, and conflict

Schema 3 adds distinct candidate sets:

- `preferredCandidateRegionIds`: candidates selected by the current evidence rule;
- `outerPlausibleCandidateRegionIds`: the safe union of all retained alternatives, including unresolved conflicts; and
- `conflictedCandidateRegionIds`: regions carried by a conflicted warp.

Existing `candidateRegionIds`, containment, false exclusion, and legacy guidance keep their schema-2 meaning. For schema 3 strategies, `candidateRegionIds` equals the outer-safe set. Leaving `Preferred` is not a hard exclusion while the region remains in `OuterPlausible`.

Confirmed evidence narrows the historical state. Supported-but-uncertain evidence branches into a narrowed preferred warp and an outer alternative. Conflicting evidence has no hard intersection and cannot collapse the outer set. Indeterminate/unsupported/private/unavailable evidence is a no-op. Device-only evidence cannot constrain Person A.

## Metrics and schema version 3

Schema version 3 adds, without reusing schema-2 fields:

- preferred/outer candidate counts, containment, and reduction;
- full preferred and outer-safe guidance metric sets;
- constraint/audit counts, unique source-observation count, branches, conflicts, and retained alternatives;
- evidence-condition grouping;
- paired `CrossThread - OwnEvidence` deltas; and
- target-specific denominator and person/device different-terminal summary.

Legacy `TimeToUsefulGuidance` remains based on `candidateRegionIds`. `preferredGuidance` and `outerSafeGuidance` each retain time-to-first-useful, final usefulness, useful/evaluated tick counts, persistence fraction, longest streak, and regression. A useful set contains truth and is smaller than the explicit searchable denominator. Unavailable remains distinct from measured zero.

Constraint counts and unique source-observation counts are separate. Multiple derived constraints from one source observation do not become independent evidence.

## Output and reproducibility

The `evaluate` command writes deterministic:

- `runs.jsonl` and `runs.csv`;
- `aggregate.json` and `aggregate.csv`;
- `conditions.csv`;
- `paired.csv`; and
- `manifest.json`.

JSON properties, run/scenario/strategy/seed order, candidates, provenance, audits, conditions, and CSV columns use stable order. CSV decimals use invariant culture. Reproducibility files contain no current time, absolute path, random GUID, or scheduling result.

Example:

```bash
dotnet run --project src/FootprintSim.Cli -- evaluate \
  --scenario scenarios/s3_cross_thread_encounter/scenario.json \
  --seeds 0..199 \
  --strategies all \
  --output results/m1c3-s3
```

Six registered strategies produce 1,200 runs for 200 seeds.

## Relation-resolution preservation

The audit retains relation kind, target, participants/roles, source observation, source ancestry, observed and constrained windows, exact-tick/interval classification, availability, application tick, disposition, before/after counts, conflict/alternative flags, and provenance. Candidate provenance carries cross-thread ancestry through public forward propagation.

This preservation follows the research hypothesis `Event Density != Relation Resolution`; it does not define `CausalNexus`, Nexus start/end boundaries, or a Causal Nexus Resolution score.

## Deferred work and non-goals

- **M1-T0:** neutral serialization/analysis of candidate before/after, access context, state transitions, source ancestry, and observation pipeline telemetry.
- **M1-D:** multiple simultaneous wefts, independent-evidence conflicts, upstream/local/downstream constraints, causal sandwich, and operational forward/backward Causal Tracing.
- **M1-E:** present-state bundles, dynamic past index, Reconstruct -> Jump -> Traverse, Addressability versus Verifiability, Addressing Basis, Robust Constraint Spectrum, discriminative power, persistence, and re-encoding experiments.
- **Future Causal Nexus research:** formal Nexus definition/boundaries, temporal/relation/state-transition resolution, overlap/continuity/interruption, compression, tracing accuracy, and storage trade-offs.

M1-C3 does not implement a full Causal Weave graph, graph query language, 24-lens taxonomy or experiment, Index Access, probabilistic belief engine, ATMS, observation calibration, distributed holder flow, BLE/GIS/CRA, rescue policy, active probing, Causal Shadow, Good/Bad scoring, GUI, or parallel execution.

S3 weights, topology, observations, and outcome frequencies are synthetic. They do not estimate real disaster probability, BLE performance, person location, survival, or rescue effectiveness.
