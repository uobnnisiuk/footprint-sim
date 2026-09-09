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

Pending registered execution. No F19 prospective density outcomes have been inspected.
