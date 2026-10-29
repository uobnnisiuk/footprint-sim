# M1-F19 nested point-support density

## Audit and research choice

Start from F18 final `77611bc52976178857ac442be6e07a9caa1a2a9d`. At that clean commit,
environment/build/format checks and all 357 tests pass; all six F18 replay artifacts reproduce
byte for byte. F16/F17/F18 establish paired inference, corrected Region-domain diagnosis and
material interval-representation dependence (32/200 minimum families changed in F18).

A naive augmentation of original mixed-width candidates would require assigning new widths.
Adding points changes the width mixture; assigning widths with a new stream freezes individual
marks but still leaves their mixture coupled to support filling. Therefore this study fixes the
already-audited F18 point-at-start representation in EVERY density arm. It is not an experiment
retaining F16's original interval-native IDs, nor evidence of robustness across interval choices.
Retain the original F16 world/truth unchanged and its explicit interval-to-point identity bridge.
All F18 baseline point candidates, native IDs and provenance are retained exactly in every arm.

The next question is whether sparse occupancy of a fixed public start/Region support conceals
constraint effects and makes minimum field families depend on missing candidate witnesses.
This has higher immediate information value than another field ablation on the same sparse
representation. It also provides a bounded stopping point for abstract count sensitivity before
introducing a representation of actual search locations and actions.

The generator first defines 7 starts (base tick plus -3..3) and 5..9 registered Regions, samples
unique start/Region cells, then selects truth. Base tick is public generator support metadata;
it is not inferred from the selected truth, evidence interval, extrema of occupied candidates or
profile results. F18 point candidates already live on this grid. Support candidates are public
model hypotheses, not new observations or independent evidence. The known S4 all-strategies
domain failure is unrelated and remains outside this change.

## Frozen registration

Commit and push this document and `experiments/m1f19_nested_point_support_density/experiment.json`
BEFORE implementing/running prospective density evaluations or inspecting their outcomes. Only
F16/F17/F18 baseline results and algebraic consequences have been inspected at registration.
The F16 experiment bytes remain SHA-256
`e717669c7178dcf8b278b831b6ee049a2aa8dae5a9c9e6366b8c32712649ca39`.
Use exactly seeds 0..199, no skip/regeneration/replacement or tuning.

Paired unit: one original F16 world, one fixed public derivation of L/T/P, one F18 point baseline,
and all three density arms × eight profiles (4,800 primary evaluations, 200 independent worlds).
Legacy F15 and F18 reference evaluations are additional validation, not independent samples.

Density means the fraction of occupied `(start tick, registered Region)` cells in a FIXED finite
support. It is neither people per area, probability mass, geometric resolution nor evidence count.
For each world the support is exactly
`{baseTick-3,...,baseTick+3} × original RegionUniverse`, size `S=7*RegionCount` (35..63).
Each cell represents one EventPoint with width zero. Let `N` be the baseline point count and
`M=S-N` the number of missing cells. There are no duplicates or additional width alternatives.

| Arm | Added cells | Total candidates before fields | Density |
|---|---:|---:|---:|
| point-baseline | 0 | N | N/S |
| half-missing | floor(M/2) | N+floor(M/2) | (N+floor(M/2))/S |
| full-support | M | S | 1 |

Rank all missing cells ONCE by lower-case SHA-256 of the UTF-8 string
`m1f19-point-support-order-v1|{seed}|{baseTick}|{offset}|{RegionId}`.
Integers use invariant decimal, no padding, no plus sign, no trailing newline; offset is -3..3.
Tie-break by numeric start tick then ordinal Region ID. Prefixes define the nested additions.
This is deterministic coverage ordering, not a claim of random representative sampling. No PRNG
stream, evidence-dependent ranking, observed outcome or truth label chooses added cells.

For a missing cell use stable key `density-t{startTick}-r{regionIndex:D2}`, with the zero-based
index in ordinal Region order. PublicSourceIdentifier is
`seed-{seed:D3}-{key}-public-model`. All additions carry PublicCausalModel provenance, public
visibility, availability tick 0, Confirmed model admission, no person/device observation flag,
no source observation ID, and shared ExternalSource ancestry
`m1f19-seed-{seed:D3}-point-support-model`. Unique per-cell source identifiers are hypothesis
addresses, not independent evidence groups. Do not append ancestry to old candidates or change
their IDs. Record support content identity separately from original world and inference universe.

The pure density transform accepts only public support metadata and baseline point candidates,
not world/truth, observations, constraints, profiles, outcomes or scores. Reject nonpoints and
invalid/out-of-domain/colliding keys, sources, coordinates or provenance. Reuse existing native
candidate reconstruction and the unchanged F14 composer. The F18 public evaluator can evaluate
the already-point input through its identity arm; label the resulting density arm explicitly.
Retain candidate descriptors, introduction arm, stable keys/native IDs and original F16-to-F18
bridge. Universe IDs change with content; composition IDs may remain equal by existing contract.

## Fixed variables and boundaries

Generate the original world once. Fix all original raw L/T/P observations, source graph, derived
constraints, target Person/exact encounter relation, cutoff/availability, original truth fields,
Region labels/domain, base tick, seven start ticks, width zero, original point candidates and
profile definitions. Original world identity continues to identify original content only.
Resolve the fixed truth key in every primary result after ALL three public arms have returned.
Existing legacy reference checks retain their evaluation-only truth context.

Do not rerun the generator with different candidate counts. Do not rebuild evidence or constraints
per arm/profile. Do not change precision, missingness, conflict, misidentification, participant
alternatives, source correlation or logical grouping. Keep candidate-generation, independent
verification, traversal and evaluation-only purposes distinct. The L/T/P evidence source graph is
fixed; the explicitly recorded candidate-model support grows and is not secretly new evidence.

Empirical start and Region histograms, occupied ticks/Regions and unused-Region fraction can change
as empty cells are filled. These are coupled consequences of the manipulation, not independent
controls. Report them in every arm. Width histogram is identically all zero. No inference about
continuously varying density at fixed empirical marginals follows.

## Primary invariants and failure criteria

For each of the three ordered arm pairs (baseline→half, half→full, baseline→full):

1. Candidate universes and every profile's survivor key AND native-ID sets are nested. Restricting
   the denser result to the older universe recovers the older result exactly. Added survivor keys
   equal newly introduced cells satisfying that profile's unchanged public predicates.
2. All 12 Joint inclusions hold within each arm. All primary profiles contain the unchanged truth;
   false exclusion is zero. Original F18 point-reference membership/IDs/work/composition agree.
3. Reduction vs None and all nine conditional EXCLUDED key sets are nested; their absolute counts
   are nondecreasing. Any previously positive conditional effect remains positive. Retained or
   reduction fractions have no required direction. Work may grow; the composer/work rule is fixed.
4. Profile equivalence can only true→false, never false→true (all 28 pairs). At-minimum profile sets
   can only lose members because Full is a subset of every profile. Minimum candidate count and
   minimum field count are nondecreasing. Inclusion-minimal families need not be nested: a newly
   minimal profile must contain an older minimal profile. Previously necessary Full stays necessary.
5. Region top/implication flags remain fixed. Time-top only true→false. Existing nonsaturation
   cannot become one-candidate saturation. Candidate-to-constraint logic is not inferred from IDs.
6. At complete Cartesian point support, constraint-to-candidate plateaus are impossible: any
   nonredundant predicate has a represented counterexample. T interval width is at most 3 across
   seven starts spanning 6 ticks, so all T additions at full support are strictly reducing.
   These are algebraic consequences known BEFORE prospective execution, not empirical discoveries.
7. Compatible truth substitution, input permutation and repeated execution cannot change public
   transform/inference outputs. Original world and fixed public/evidence signatures remain equal.

Fail before interpretation on any violated invariant, out-of-support cell, identity collision,
modified old candidate, width drift, missing/extra/duplicate seed, changed input bytes, private/future
evidence, unsupported usage role, truth leakage, nondeterministic output or result-dependent tuning.
Do not repair an invalid world by resampling. Disclose implementation corrections; a design change
after outcomes requires a new registration, not retroactive confirmation.

## Metrics and independent validation

Primary per world/arm/profile: full key/native-ID sets, original vs introduced membership, added
survivors and excluded-key witnesses; count and reduction vs None (absolute and fraction), both
paired changes, containment/false exclusion, work, universe/support/composition signatures.
Aggregate means/medians/ranges/distributions and changed/zero-effect world counts; no significance
test, universal field ranking or target frequency.

Secondary: all nine conditional excluded sets/counts/fractions, all three arm-pair transitions,
corrected F17 plateau 5x5 matrices with raw logical flags and affected seeds, newly detectable
effects (plateau→reduction) and any lost effects (expected zero), all 28 profile equivalences,
minimum count, all at-minimum profiles, inclusion-minimal families, lost/gained profiles and minimal
field count. Track each arm's occupied ticks/Regions, unused-Region fraction, density and full
start/Region/width histograms. Summaries must distinguish changed families from changed counts.

Write deterministic report JSON, per-world JSONL, profile/conditional CSV, candidate crosswalk CSV
and manifest with execution commit/dirty state and frozen input hashes. No times/absolute paths in
scientific artifacts. Generated results remain ignored. Independently recompute cells/hash ranking
and raw public L/T/P predicates, including full-support analytic counts, before truth checking;
compare memberships, all invariants, diagnostics, minima and aggregate tables. Native hash-factory
reimplementation is not required, but coordinates/uniqueness/preserved IDs/provenance are checked.

Run environment/build/full tests/format, all samples and relevant README commands. Meaningful tests
cover nesting/identity, malformed support, analytic predicate witnesses/minimal-family transitions,
truth substitution, permutation, common evidence, ordering and fixed protocol/seed enforcement.
Replay F16/F17/F18 to ensure no scientific output drift. Execute final clean commit twice and require
all six artifacts byte-identical. Commit/push implementation, verifier and interpreted report.

CLI: `evaluate-candidate-density --experiment experiments/m1f19_nested_point_support_density/experiment.json
--baseline-experiment experiments/m1f16_common_world_field_combination/experiment.json --seeds 0..199
--output results/m1f19-candidate-density`.

## Interpretation and next-decision rule

This measures missing-witness sensitivity on a specified point lattice. Full-support count/minima
are consequences of that lattice and its fixed predicates, not generally sufficient field sets.
Safety is structurally protected by retaining a truth-containing baseline in the purified family.
Extra model candidates are not additional people, observations, physical sites or rescue work.

After results, use the exact mechanism and changed family/equivalence frequencies to decide whether
more abstract candidate-factor studies have material information value. Do not automatically begin
field ablation or a sequence of arbitrary granularities. If support completion explains the remaining
plateaus/minima, the next priority should be an explicit public mapping from historical candidate
states to operationally checkable spatial units, including many-to-one mappings and unknown mapping,
before costs. A useful transition contract must retain outer-safe coverage, separate past state from
current location/propagation, declare Region/building/floor and action coverage, and preserve uncertainty.
Only justified spatial/action units and observations generated by actions can support wrong-area cost,
expected discovery time, personnel/equipment or dog/calling/acoustic/camera handoff. Cross-domain SAR,
closed-loop inference updates and real observations/data remain later validation, not F19 claims.

## Results

Reconciliation disclosure (Issue #47, 2026-10-29): the following results and the
original implementation/validation narrative were already present in the local
uncommitted research worktree at `9039417314df4c11fe5bc088825bf5f25eaf4d98`.
They were outcome-exposed before the current integration audit. They are retained
as prior-work evidence, not a claim of a first unseen prospective run or of final
clean-source validation. The current audit reuses that implementation, strengthens
the independent verifier, and requires fresh full runs on one clean integrated
source revision. Its verified evidence is recorded separately in Issue #47 and
`docs/research-autodev-state.json`; historical test counts below describe the prior run.

Registration was committed and pushed as `9039417314df4c11fe5bc088825bf5f25eaf4d98` before
prospective computation. The frozen protocol SHA-256 is
`f0669251ab8b747fbd4b7517e169d70723f052ae0465d33cfac6d5fe355c6bbd`.
The first independent Python computation occurred after that push. No support, ordering, density
level, seed, generator or field definition was tuned in response to outcomes.

The independent public-predicate reference and C# implementation agree on all per-world results,
paired changes and aggregate tables. The first C# execution records preregistration HEAD with a
dirty working tree; it is not presented as a clean committed execution.

### Paired validity, truth isolation and safety

All 4,800 primary profile evaluations retain truth, with zero false exclusions and zero invariant
violations. All 4,800 paired profile checks satisfy both key/native-ID nesting and exact recovery
when restricted to the older universe; all 7,200 within-arm Joint inclusions hold. The 2,412
original interval-to-point bridges agree with F18, and the 9,779 full-support candidates each have
a unique stable/native-ID crosswalk with introduction arm. Old point descriptors and provenance
are unchanged. Added survivor/exclusion keys match the fixed public predicates exactly.

The original world/truth and raw evidence, source graph, L/T/P constraints and composition rules
are fixed. All three public density arms finish before primary truth addresses are resolved.
Compatible truth-candidate and truth-participant substitutions leave public results unchanged;
input permutation, invalid public support/provenance and usage boundaries are tested. Candidate
model ancestry is explicitly recorded separately from the fixed L/T/P evidence sources. Work
increases with the universe according to the unchanged composer rule `N * (1 + active axis count)`;
it is computational work, not rescue actions or time.

### Support and profile effects

The 2,412 F18 baseline point candidates occupy an average 25.6125% of the fixed support. The
half-missing arm occupies 62.2803% on average; it fills half the missing cells, not half the entire
grid. Full support contains 9,779 candidates across the same 200 worlds. Every width remains zero.

| Property (mean across worlds) | Point baseline | Half missing | Full support |
|---|---:|---:|---:|
| Candidate universe | 12.060 | 30.235 | 48.895 |
| Occupied start ticks | 6.145 | 7.000 | 7.000 |
| Occupied Regions | 5.970 | 6.980 | 6.985 |
| Unused Region fraction | 13.6135% | 0.0556% | 0% |
| Worlds with any unused Region | 131 | 1 | 0 |

These occupancy changes are coupled to support filling; no new Region, tick, observation or person
was introduced. Full-start and Region histograms are retained per world. The width histogram is
all zero in every arm, removing the new-width-mixture problem found during the design audit.

| Profile | Mean candidates: baseline | Half | Full | Mean reduction fraction vs None: baseline -> full | Changed worlds: baseline -> full |
|---|---:|---:|---:|---:|---:|
| None | 12.060 | 30.235 | 48.895 | 0% -> 0% | 200 |
| L | 6.550 | 15.170 | 24.045 | 45.38% -> 48.73% | 200 |
| T | 4.190 | 9.495 | 15.090 | 65.35% -> 69.57% | 200 |
| P | 8.140 | 19.385 | 30.870 | 32.72% -> 35.21% | 200 |
| L+T | 2.600 | 4.870 | 7.280 | 78.21% -> 84.73% | 193 |
| L+P | 4.965 | 10.995 | 16.975 | 58.50% -> 63.11% | 200 |
| T+P | 2.960 | 6.015 | 9.335 | 75.58% -> 80.70% | 198 |
| Full | 2.045 | 3.465 | 5.015 | 82.76% -> 89.28% | 176 |

Fractions are averages of within-world fractions, not ratios of aggregate means. Their direction
is empirical, not a nesting theorem. Full candidates increase while the fraction removed increases:
absolute candidate burden and relative reduction answer different questions. Neither is calibrated
search work. Exact sets, count changes, distributions, original/introduced memberships and exclusion
witnesses are retained rather than inferring membership changes from counts alone.
Full medians are 2/3/4 and ranges 1..7 / 1..11 / 1..15. Its candidate set remains unchanged in
24 baseline-to-full worlds despite the larger universe. The report JSON includes means, medians,
ranges, complete distributions, and zero-effect frequencies for all profiles and paired changes.

### Conditional effects and plateau transitions

| Addition | Mean reduction: baseline | Half | Full | Newly positive effects, baseline -> full |
|---|---:|---:|---:|---:|
| T to L | 3.950 | 10.300 | 16.765 | 8 |
| P to L | 1.585 | 4.175 | 7.070 | 9 |
| L to T | 1.590 | 4.625 | 7.810 | 41 |
| P to T | 1.230 | 3.480 | 5.755 | 54 |
| L to P | 3.175 | 8.390 | 13.895 | 11 |
| T to P | 5.180 | 13.370 | 21.535 | 2 |
| P to L+T | 0.555 | 1.405 | 2.265 | 56 |
| T to L+P | 2.920 | 7.530 | 11.960 | 20 |
| L to T+P | 0.915 | 2.550 | 4.320 | 65 |

The nine comparisons per world total 1,800 comparisons per arm. All previously positive effects
remain positive. Half filling reveals 213 effects; completing the remaining support reveals 53
more. The 266 new effects are represented counterexamples to prior candidate-set equality, not new
independent evidence or changes to the public constraints. No effect becomes undetectable.

| Corrected F17 classification | Baseline | Half | Full |
|---|---:|---:|---:|
| Additional candidate reduction | 1322 | 1535 | 1588 |
| Observation-to-constraint plateau | 74 | 82 | 84 |
| Constraint overlap plateau | 103 | 119 | 127 |
| Constraint-to-candidate plateau | 168 | 44 | 0 |
| One-candidate saturation | 133 | 20 | 1 |
| All plateaus | 478 | 265 | 212 |

Baseline-to-full changes 300 classifications across 137 worlds. The complete matrix below separates
newly positive effects from saturation losing priority. Per-comparison matrices, both adjacent-arm
matrices, flags and affected seeds are also retained.

| Baseline / full | Reduction | Observation | Overlap | Candidate plateau | Saturation |
|---|---:|---:|---:|---:|---:|
| Reduction | 1322 | 0 | 0 | 0 | 0 |
| Observation | 0 | 74 | 0 | 0 | 0 |
| Overlap | 0 | 0 | 103 | 0 | 0 |
| Candidate plateau | 168 | 0 | 0 | 0 | 0 |
| Saturation | 98 | 10 | 24 | 0 | 1 |

The disappearance of candidate plateaus at full Cartesian support was preregistered as an
algebraic consequence. Its occurrence is a contract check, not a newly discovered performance
benefit. The empirical findings are how many sparse-world effects were hidden, where half filling
is sufficient, and which minima depend on those witnesses. Observation/overlap labels increase
because unchanged logic becomes visible after saturation ceases to take priority.
All recorded Region top/implication and Time-top flags are unchanged in this cohort; allowed
Time-top true-to-false transitions occur zero times. Thus these observed label changes are fully
explained by membership and saturation, not altered diagnostic domains or public constraints.

### Equivalence and minimum families

| Paired change | Worlds with changed minimum count | Worlds with changed minimal family | Lost equivalences / affected worlds |
|---|---:|---:|---:|
| Baseline -> half | 133 | 95 | 303 / 111 |
| Half -> full | 143 | 30 | 66 / 36 |
| Baseline -> full | 176 | 115 | 369 / 127 |

The three contrasts reuse the same worlds; their counts must not be pooled as independent samples.
Existing nonequivalence never disappears. At-minimum profile sets only lose members, but the
inclusion-minimal family can acquire a larger profile when a smaller one ceases to attain Full's
result. In 85 worlds the entire baseline minimum family survives to full support unchanged; in
115 worlds (57.5%) it does not.

| Profile | At-minimum worlds: baseline / half / full | Inclusion-minimal worlds: baseline / half / full |
|---|---:|---:|
| None | 0 / 0 / 0 | 0 / 0 / 0 |
| L | 2 / 0 / 0 | 2 / 0 / 0 |
| T | 31 / 6 / 1 | 31 / 6 / 1 |
| P | 0 / 0 / 0 | 0 / 0 / 0 |
| L+T | 124 / 87 / 68 | 91 / 81 / 67 |
| L+P | 20 / 0 / 0 | 18 / 0 / 0 |
| T+P | 89 / 36 / 24 | 58 / 30 / 23 |
| Full | 200 / 200 / 200 | 31 / 91 / 114 |

Multiple profiles can be minimal in one world, so columns need not sum to 200. The smallest field
count attaining the minimum has distributions (one/two/three fields): baseline 33/136/31,
half 6/103/91, full 1/85/114. Means are 1.990/2.425/2.565. Full attaining the minimum always
follows from Joint inclusion; Full being necessary within these eight profiles does not.
The smallest field count changes in 106 baseline-to-full worlds, compared with 115 changed
families and 176 changed candidate minima. These metrics are not interchangeable.

### Research decision after F16-F19

Do not elevate these minima to general field requirements or proceed automatically to field
ablation. F18 changed 16% of families by interval representation; this fixed-point support completion
changes 57.5%. The figures refer to different manipulations/baselines and are not comparable causal
effect sizes. What survives is paired safety in the purified family, Joint monotonicity and fixed
public evidence semantics. Counts, equivalence and descriptive field sufficiency remain dependent
on the represented candidate support.

At the same time, further arbitrary density levels or tick/Region label counts have diminishing
value here. At complete point support the result is exactly the product of surviving registered
Regions and surviving start ticks, and every remaining plateau is accounted for by top/overlap or
saturation. Abstract candidate-count stability is not the objective. The next priority is an explicit
public spatial/temporal mapping toward search units, rather than another uncalibrated scalar factor.

The transition should proceed only with a contract that maps a historical candidate to possible
current search locations using declared public transitions, then maps locations to action coverage.
It must support many-to-one and one-to-many mappings, retain unknown/unmapped possibilities, and
test outer-safe coverage without selecting by truth. A historical Region must not silently become
a current building, floor or check location. Compare candidate counts with mapped location counts
and action coverage before adding times or costs. This makes the next uncertainty concrete: what
does each surviving hypothesis require a rescuer to check, and what public mapping justifies it?

Action-generated observations and inference updates can then support a closed loop. Expected
discovery time, personnel/equipment, wrong-area cost and dog/calling/acoustic/vibration/camera handoff
need explicit action models and subsequent calibration; cross-domain SAR and real-data validation
remain later. No current result estimates those quantities or validates actual rescue safety.

### Implementation and validation record

The implementation adds a public-only support transform, typed evaluation/audit records, a runner
reusing F18 public reconstruction and F14 composition, a six-artifact writer and CLI. No old core
inference, generator or frozen experiment file is changed. The protocol loader and runner enforce
the exact registration, file hashes and seed set. A compile-only test typo was corrected from the
nonexistent enum `Unknown` to the existing `Indeterminate`; four canonical-support boundary tests
were added. Neither correction changes the registered manipulation or any computed outcome.

Environment/build/format verification pass; build reports zero warnings/errors. All 403 tests pass
(357 baseline plus 46 F19 cases). F16/F17/F18 replays preserve all 18 scientific artifacts: only
manifest source commit/dirty metadata may differ. The supported sample/legacy command regression
set and known S4 failure are recorded separately in ignored validation artifacts.

The independent verifier uses immutable original F16 rows (SHA-256
`6c114058f7e9479ae29e3d16030f10843ddfb71b8b8bc861d13f8840c0cd4c81`) and optionally anchors native
point identities to the original F18 rows (SHA-256
`c5b62f9bbad305e1ee3176b86105d1a631d4e3ebeb6868703d41d5c76c0d4d81`). It independently reconstructs
7,367 missing cells and their ranking, computes all public memberships before truth checking, and
verifies 4,800 profile results, 5,400 conditional diagnostics, 16,800 profile equivalences, all
600 arm minima/occupancies and all 600 paired-world comparisons. Full-support analytic counts
agree in all 1,600 profile checks. Native coordinates, provenance, old IDs and crosswalks agree;
native ID hashing itself is not independently reimplemented. Integer sets/counts/distributions
must match exactly; only decimal fraction arithmetic uses tolerance `1e-12` for Python/C# numeric
representation. Every aggregate distribution/matrix and every CSV column is checked.

```bash
python3 scripts/verify-candidate-density.py \
  --baseline-runs results/m1f17-baseline-audit/f16-a/common-world-field-combination-runs.jsonl \
  --point-reference-runs results/m1f18-final-a/candidate-interval-width-runs.jsonl \
  --actual-runs results/m1f19-final-a/candidate-density-runs.jsonl \
  --expected-source-sha "$(git rev-parse HEAD)" \
  --output results/m1f19-final-independent
```

For clean-commit reproduction run the registered CLI twice with output directories
`results/m1f19-final-a` and `results/m1f19-final-b`; require all six artifacts byte-identical and
run the independent verifier on the clean output. The manifest records the actual execution
commit and dirty state. Profile/conditional/crosswalk CSV contain 4,800 / 5,400 / 9,779 rows.
Generated outputs and validation logs remain ignored; frozen inputs, code, verifier and this
interpreted report are the committed reproduction package.

The pre-existing S4 `--strategies all` evaluation-domain mismatch is retained independently;
the three documented S4 strategies succeed. Its repair is a prerequisite to future cross-scenario
all-strategy comparisons, not to this F16-derived experiment. No cross-scenario claim is made here.
