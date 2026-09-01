# M1-F3 `later-trace-a` observed-time resolution

## Research question and scope

M1-F3 varies only the observed-time uncertainty of the existing public `later-trace-a` source. It
asks whether widening the set of possible observation ticks changes past-state candidate
discrimination under current S5, while location remains at the M1-F2 `current` payload
`{fork-east, fork-west}` and all other public/world/method inputs remain fixed.

`M1-F3` and “time resolution” are descriptive work labels. This stage adds no new inference,
physical, probabilistic, sensor, topology, or rescue model. Baseline source commit:
`31b44185395ab37395871d691844cba914710eef`.

## Current time-semantics audit

The current `later-trace-a` public interaction observation has closed exact window `[24,24]`. Its
`AvailableAtTick` is 25: availability answers when the record can be used, not when the observed
relation occurred. The query target is the closed historical interval `[13,14]`.

The two same-source generation definitions copy the source observation window into
`SourceObservedTimeWindow`, but copy their historical `ConstrainedTimeWindow` directly from the S5
scenario declaration `[13,14]`. There is no function from observed tick 24 to `[13,14]`, and the
allowed Regions `{fork-east, fork-west}` also do not vary with observed time. Thus observed time is
retained as lineage/audit information but does not parameterize the current historical constraint.

Candidate-state intervals are closed: `[8,10]`, `[13,14]`, and `[18,32]`. Query/constraint/state
overlap is inclusive (`a.start <= b.end && b.start <= a.end`), so equal endpoints overlap.

## Occurrence uncertainty is not duration

Every finite experiment window means that the observation occurred at one unknown integer tick in
the closed window:

```text
observed tick t is one member of {start, start+1, ..., end}
```

It does not mean that the relation persisted at every tick. The experiment records this explicitly
as `occursAtOnePossibleTick`. The general `InteractionTimeWindow` contract itself does not encode
the distinction between one-unknown-tick occurrence uncertainty and duration; that remains a
representation limitation outside this experiment.

## Fixed and manipulated variables

Only observed-time uncertainty changes. Fixed are subject and participant kinds, location
`{fork-east, fork-west}`, relation, availability tick 25, `Confirmed`, visibility, direct source,
ancestry `record-family-a`, truth world, trajectories, hazard realization, public topology, Region
representation, target query/cutoff, all other evidence, candidate construction, verification,
traversal, local-neighborhood selection, candidate identity, and truth scoring.

The world is generated once per seed and projected into every time level. No profile changes the
random sequence or scenario.

## Core levels fixed before result inspection

| Level | Closed window | Possible ticks | Rationale |
| --- | --- | ---: | --- |
| `current` | `[24,24]` | 1 | unchanged exact baseline |
| `narrow` | `[23,24]` | 2 | adjacent tick, no candidate-state boundary crossed |
| `later-state-broad` | `[18,24]` | 7 | reaches start of later state `[18,32]` |
| `target-crossing-broad` | `[13,24]` | 12 | crosses into target state `[13,14]` |
| `scenario-history-broad` | `[8,24]` | 17 | reaches earliest public candidate-state start |
| `unknown` | unavailable | 0 | no observed-time input and no derived constraint |

Finite windows are monotonically nested. Their end tick remains before availability tick 25. The
levels follow existing S5 state boundaries and were not selected from evaluation results.

## Uncertainty propagation

For every possible tick, the evaluator applies the existing scenario-declared exact-time mapping.
Because that mapping is independent of the tick, every possible tick yields the same historical
window `[13,14]` and allowed Regions `{fork-east, fork-west}`. The finite coarse constraint is the
union of those possible derivations, which is still `[13,14]` with the same Regions. No intersection,
midpoint, offset, hidden tick 24, truth tick, or newly invented transition rule is used.

For `unknown`, an experiment-side source record retains subject, location, relation, availability,
evidence state, and ancestry with no observed-time payload. Since the existing production
`InteractionObservation` requires a time window, the unknown projection is not emitted into that
contract and no source-derived constraint is generated. Unknown time is unavailable information,
not negative evidence.

## Preregistered metrics

Safety is containment and false exclusion. Candidate discrimination is mean candidate count and
reduction. Temporal diagnostics are possible ticks, observed-window width, possible exact-time
derivations, unioned historical windows and widths, affected candidate-state intervals, and whether
a constraint was generated. Work is generation, verification, forward/backward transitions,
evidence records, public relations, and source groups inspected.

Per-seed reconstruction candidates are checked for the expected inclusion ordering from exact
through each wider finite window. Any violation must be retained and attributed to composition,
other evidence, or temporal semantics rather than automatically called a bug.

## Preregistered failure conditions

The experiment is invalid if a coarse window is treated as persistence; possible derivations are
intersected; midpoint/exact tick 24/truth/default time is substituted; unknown time produces a
constraint; a profile changes location, subject, relation, availability, evidence state, ancestry,
truth, trajectory, hazard, topology, query, inference roles, local traversal, or scoring; missing
time becomes absence; candidate inclusion becomes non-monotonic without an audit; levels are tuned
after results; or output is non-deterministic.

All six core levels use seeds 0 through 199: 1,200 paired profile runs and 3,600 method runs. There
are no exploratory levels.

## Results

Results are intentionally recorded only after the definitions, propagation rule, metrics, and
failure conditions above are fixed.

All six levels completed seeds 0 through 199: 1,200 paired profile runs and 3,600 method runs. Every
seed retained one truth-world identifier and one public-topology identifier across all levels. All
finite projected observations resolved; unknown retained the underlying source reference but did not
emit a time-bearing `InteractionObservation` or constraint.

The 200 current-level runs were also compared directly with the clean M1-F2 current output; paired
world identifiers, candidate evaluations, method outputs, work, and transition identifiers matched.

| Level | Window | Possible ticks | Method | Mean candidates | Containment | False exclusion | Reduction | Gen | Verify | Forward | Backward |
| --- | --- | ---: | --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| current | `[24,24]` | 1 | backward traversal | 2.000 | 100% | 0/200 | 33.33% | 0 | 0 | 4 | 4 |
| current | `[24,24]` | 1 | reconstruction | 2.165 | 100% | 0/200 | 27.83% | 5.330 | 0.835 | 0 | 0 |
| current | `[24,24]` | 1 | reconstruction + local traversal | 2.000 | 100% | 0/200 | 33.33% | 5.330 | 0 | 4 | 4 |
| narrow | `[23,24]` | 2 | backward traversal | 2.000 | 100% | 0/200 | 33.33% | 0 | 0 | 4 | 4 |
| narrow | `[23,24]` | 2 | reconstruction | 2.165 | 100% | 0/200 | 27.83% | 5.330 | 0.835 | 0 | 0 |
| narrow | `[23,24]` | 2 | reconstruction + local traversal | 2.000 | 100% | 0/200 | 33.33% | 5.330 | 0 | 4 | 4 |
| later-state-broad | `[18,24]` | 7 | backward traversal | 2.000 | 100% | 0/200 | 33.33% | 0 | 0 | 4 | 4 |
| later-state-broad | `[18,24]` | 7 | reconstruction | 2.165 | 100% | 0/200 | 27.83% | 5.330 | 0.835 | 0 | 0 |
| later-state-broad | `[18,24]` | 7 | reconstruction + local traversal | 2.000 | 100% | 0/200 | 33.33% | 5.330 | 0 | 4 | 4 |
| target-crossing-broad | `[13,24]` | 12 | backward traversal | 2.000 | 100% | 0/200 | 33.33% | 0 | 0 | 4 | 4 |
| target-crossing-broad | `[13,24]` | 12 | reconstruction | 2.165 | 100% | 0/200 | 27.83% | 5.330 | 0.835 | 0 | 0 |
| target-crossing-broad | `[13,24]` | 12 | reconstruction + local traversal | 2.000 | 100% | 0/200 | 33.33% | 5.330 | 0 | 4 | 4 |
| scenario-history-broad | `[8,24]` | 17 | backward traversal | 2.000 | 100% | 0/200 | 33.33% | 0 | 0 | 4 | 4 |
| scenario-history-broad | `[8,24]` | 17 | reconstruction | 2.165 | 100% | 0/200 | 27.83% | 5.330 | 0.835 | 0 | 0 |
| scenario-history-broad | `[8,24]` | 17 | reconstruction + local traversal | 2.000 | 100% | 0/200 | 33.33% | 5.330 | 0 | 4 | 4 |
| unknown | unavailable | 0 | backward traversal | 2.000 | 100% | 0/200 | 33.33% | 0 | 0 | 4 | 4 |
| unknown | unavailable | 0 | reconstruction | 3.000 | 100% | 0/200 | 0% | 5.165 | 0.835 | 0 | 0 |
| unknown | unavailable | 0 | reconstruction + local traversal | 2.000 | 100% | 0/200 | 33.33% | 5.165 | 0 | 4 | 4 |

### Finite-window non-identifiability

Every finite window produced the same possible exact-time derivation union: historical window
`[13,14]`, two historical ticks, `{fork-east,fork-west}`, and one affected candidate-state interval.
Candidate sets, containment, reduction, verification dispositions, evidence/source counts, work,
and transition IDs were identical to current for every paired seed. All 800 adjacent finite-level
monotonicity checks passed with zero violations; equality, rather than candidate expansion, held.

`CurrentTransformationUsesObservedTick` is therefore false. The coarsest tested finite level that is
candidate-output and strict-work equivalent to Full/current is `scenario-history-broad [8,24]`.
This equivalence is a limitation of the fixed scenario declaration, not evidence that a 17-tick
uncertainty is sufficient in another representation.

The target-crossing and scenario-history levels also show that the current declaration does not
validate whether every possible source tick is temporally later than the target window. The
experiment records the mechanical current behavior but does not treat this as a justified general
causal derivation. A future temporal-representation study must define or calibrate a tick-dependent
mapping before time-resolution contribution can be measured.

### Unknown-time boundary

Unknown is the only level where the current transformation cannot be instantiated. It removes the
two same-source generation constraints without creating negative evidence. Reconstruction increases
by 0.835 mean candidates, reduction falls by 27.83 percentage points, generation work falls by
0.165, and evidence/public-relation inspection falls by two each. Containment remains 100% and false
exclusion remains zero.

This is an availability boundary between a fully instantiated fixed transformation and no
time-derived constraint. It is not a measured precision threshold among finite windows.

### Threshold, plateau, and evidence behavior

There is no finite precision threshold: crossing the later-state, target-state, and earliest-state
boundaries never changes an output. There is a complete finite-level performance/work plateau from
`[24,24]` through `[8,24]`. A smooth knee is neither present nor computed.

Finite levels retain mean 3.830 source groups, 1.170 same-ancestry derived-record excess, 0.330
unavailable evidence, 1.165 unresolved candidates, and 0.495 conflicted candidates. Unknown has
3.000 source groups, zero same-ancestry excess, 2.330 unavailable evidence, 2.000 unresolved
candidates, and the same 0.495 conflicted candidates. These changes arise from constraint
non-generation, not from a finer-to-coarser tick mapping.

The existing neutral-telemetry schema remains version 4. M1-F3 writes a dedicated time-resolution
output schema version 1 with per-run possible ticks, exact-time derivations, historical windows,
lineage, work, paired-world identifiers, and monotonicity audits.

## Relationship to M1-F2

M1-F2 could vary a directly consumed Region set, and candidate discrimination disappeared when
`alternate-route` entered that set. M1-F3 cannot make an analogous time comparison because observed
time is not an input to the historical-window declaration. The experiments therefore differ in
measurability; their numerical contributions must not be ranked.

## Interpretation boundary and next axes

If all finite windows are equivalent, that does not establish that time precision is unimportant.
It would show that the current scenario-declared transformation cannot express time-resolution
contribution. Results remain limited to current S5, its Region/tick/state intervals,
`later-trace-a`, and current inference rules.

M1-F2 found a measurable location-set boundary when `alternate-route` became allowed. M1-F3 does
not rank time against location because their units and mappings differ. Subsequent independent axes
remain participant ambiguity, relation specificity, location × time interaction only after the
single-axis studies, and sensitivity to the temporal representation itself.
