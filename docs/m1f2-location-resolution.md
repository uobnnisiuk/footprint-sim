# M1-F2 `later-trace-a` location resolution

## Research question and scope

M1-F1 could not isolate subject, time, location, and relation contributions because the current
scenario-declared transformation treated all four as an atomic input: removing any one withheld both
`later-trace-a` generation constraints. M1-F2 keeps subject, observed time, relation, availability,
evidence state, and ancestry available while changing only the Region set carried by the location
payload. It asks how coarse that payload can become before current S5 past-state reconstruction
changes.

`M1-F2` and “location resolution” are descriptive work labels. This stage adds no inference
algorithm, confidence model, sensor model, metric-distance conversion, topology change, or rescue
behavior. Baseline source commit:
`2e11fd8934db5b324c1986af9d5ece7c4742c0b0`.

## Why location is isolated first

M1-F showed a family-level candidate-reduction contribution from `later-trace-a`. Location is the
first partial-observation axis because the existing Region representation can express nested
allowed-state sets without changing the candidate-generation algorithm. Time-window width,
participant ambiguity, and relation specificity require different representations and remain
separate later stages.

## Fixed and manipulated variables

The only manipulated variable is the available location Region set on the `later-trace-a` public
interaction observation. Fixed across profiles are subject (`person-a` with `person-b`), entity kinds,
observed closed interval `[24,24]`, `PersonPersonEncounter`, available tick 25, `Confirmed`, public
visibility, direct source `source-a`, ancestry `record-family-a`, constrained closed interval
`[13,14]`, truth world, trajectory, hazard realization, random sequence, public topology, target
query/cutoff, Region/tick representation, all other evidence, inference/verification/traversal rules,
local neighborhood, candidate identity, and truth scoring.

The simulator generates the world once per seed. Location profiles are evidence-side projections of
that one run; they never regenerate or branch truth.

## Location levels fixed before result inspection

The levels use existing S5 sets rather than result-selected regions:

| Level | Order | Location payload / derived allowed regions | Width | Constraint |
| --- | ---: | --- | ---: | --- |
| `current` | 0, most informative | `{fork-east, fork-west}` | 2 | generated |
| `target-window-broad` | 1 | `{alternate-route, fork-east, fork-west}`: every target-layer candidate | 3 | generated |
| `scenario-wide-broad` | 2 | all six evaluation-searchable S5 Regions | 6 | generated |
| `unknown` | 3, least informative | no location payload | 0/unknown | not generated |

The finite levels are monotonically nested:

```text
{fork-east, fork-west}
  subset of {alternate-route, fork-east, fork-west}
  subset of {release, alternate-route, fork-east, fork-west, terminal-east, terminal-west}
  subset of unknown/no location restriction
```

A fixed east-only or west-only level is not introduced: it would be more precise than the current
fixture while arbitrarily favoring one truth branch. The medium set is the complete fixed target
candidate layer. The broad set is the complete fixed evaluation-searchable Region set. These choices
are fixed independently of output quality.

## Partial-observation and derivation semantics

For every finite level, a new public evidence-side `InteractionObservation` copies the original
subject, participant kinds, observed time, relation, availability, evidence state, visibility, and
non-record provenance, replacing only `ObservedRegionIds`. The two existing scenario-declared
generation transformations are then instantiated with:

```text
derived AllowedRegionIds = projected Observation.ObservedRegionIds
```

Both `generation-a` and `generation-a-redundant` retain their direct source, semantic ancestry,
target entity/window, roles, relation kinds, evidence state, and derivation kinds. Their stable
runtime IDs change because the public payload changes. They remain one independent source group.

For `unknown`, the projected public observation retains subject/time/relation and an empty location
payload, but no location-derived constraint is generated. Empty location is unavailable information,
not an `Absent` claim. No exact Region is recovered from truth, the original fixture, a candidate,
topology, or a default.

## Preregistered metrics

Safety is truth containment and false exclusion. Narrowing is mean historical candidate count and
reduction from the three-Region target layer. Work is candidate generation, candidate verification,
forward/backward traversal, evidence records inspected, public relations inspected, and source groups
inspected.

Location-specific diagnostics are observed Region count, projected payload, derived allowed Region
count/set, constraint existence, definition/runtime constraint IDs, source-observation resolution,
and reconstruction candidate improvement relative to `unknown`. Candidate sets and verification
dispositions are retained per seed.

Full-equivalent means identical paired-seed candidate sets, containment, false exclusion, candidate
and verification dispositions, and work. Candidate-output equivalence is also reported separately so
that an outcome plateau with different inspection work is not hidden.

## Preregistered failure conditions

The experiment is invalid if coarse payload Regions differ from derived allowed Regions; exact
Regions leak from the original fixture or truth; a profile changes truth, trajectory, hazard,
topology, subject, time, relation, availability, evidence state, ancestry, inference semantics,
verification/traversal roles, person/device boundaries, or local traversal; unknown becomes negative
evidence; Regions are changed after inspecting results; or output is non-deterministic.

All four core levels use seeds 0 through 199: 800 paired profile runs and 2,400 method runs. There are
no exploratory profiles.

## Results

Results were added only after the definitions, ordering, metrics, and failure conditions above were
fixed. All four levels completed the same seeds 0 through 199: 800 paired profile runs and 2,400
method runs. Every seed had one truth-world identifier and one public-topology identifier across all
levels. All 800 source observations resolved.

| Level | Regions | Constraint runs | Method | Mean candidates | Containment | False exclusion | Reduction | Gen | Verify | Forward | Backward |
| --- | ---: | ---: | --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| current | 2 | 200 | backward traversal | 2.000 | 100% | 0/200 | 33.33% | 0 | 0 | 4 | 4 |
| current | 2 | 200 | reconstruction | 2.165 | 100% | 0/200 | 27.83% | 5.330 | 0.835 | 0 | 0 |
| current | 2 | 200 | reconstruction + local traversal | 2.000 | 100% | 0/200 | 33.33% | 5.330 | 0 | 4 | 4 |
| target-window-broad | 3 | 200 | backward traversal | 2.000 | 100% | 0/200 | 33.33% | 0 | 0 | 4 | 4 |
| target-window-broad | 3 | 200 | reconstruction | 3.000 | 100% | 0/200 | 0% | 6.165 | 0.835 | 0 | 0 |
| target-window-broad | 3 | 200 | reconstruction + local traversal | 2.000 | 100% | 0/200 | 33.33% | 6.165 | 0 | 4 | 4 |
| scenario-wide-broad | 6 | 200 | backward traversal | 2.000 | 100% | 0/200 | 33.33% | 0 | 0 | 4 | 4 |
| scenario-wide-broad | 6 | 200 | reconstruction | 3.000 | 100% | 0/200 | 0% | 6.165 | 0.835 | 0 | 0 |
| scenario-wide-broad | 6 | 200 | reconstruction + local traversal | 2.000 | 100% | 0/200 | 33.33% | 6.165 | 0 | 4 | 4 |
| unknown | unknown | 0 | backward traversal | 2.000 | 100% | 0/200 | 33.33% | 0 | 0 | 4 | 4 |
| unknown | unknown | 0 | reconstruction | 3.000 | 100% | 0/200 | 0% | 5.165 | 0.835 | 0 | 0 |
| unknown | unknown | 0 | reconstruction + local traversal | 2.000 | 100% | 0/200 | 33.33% | 5.165 | 0 | 4 | 4 |

Relative to current, every coarser level changed reconstruction by +0.835 mean candidates and
-27.83 percentage points reduction, without changing containment or false exclusion. At the
target-window and scenario-wide finite levels, generation work increased by 0.835 because the two
confirmed same-source constraints remained present and were evaluated against the additional
candidate. At unknown, generation work fell by 0.165 and reconstruction inspected two fewer evidence
records and two fewer public relations because the two constraints were not generated. Verification work
remained 0.835 under every level. Backward/local transition counts and transition IDs were unchanged.

### Threshold and plateau

A discrete candidate-reconstruction threshold is visible between the current two-Region set and the
three-Region target-layer set. Current excludes `alternate-route`; once that Region is admitted, the
location source no longer reduces the fixed three-Region target layer. This is a boundary in current
S5 set intersection, not a smooth knee and not a distance threshold.

There is a candidate-count/containment/reduction plateau from `target-window-broad` through
`scenario-wide-broad` and `unknown`: all return three reconstruction candidates with 100%
containment. The finite width-3 and width-6 levels are also equal in work and verification
dispositions, demonstrating that Regions outside the target layer cannot affect current candidate
space. Unknown has the same candidate metrics but different work, source groups, ancestry overlap,
and verification dispositions; it is not fully behavior-equivalent to the finite plateau.

No coarser level is Full-equivalent under either the candidate-output or strict criterion. The
coarsest tested level retaining current performance is therefore `current` itself. Constraints remain
derivable at both finite coarse levels and become non-derivable only at `unknown`.

### Evidence behavior

Current, target-window-broad, and scenario-wide-broad retain mean 3.830 source groups and 1.170
same-ancestry derived-record excess. Unknown falls to 3.000 and zero because the source-derived
constraints are unavailable. Mean unavailable evidence rises from 0.330 to 2.330 only at unknown.
Mean unresolved reconstruction candidates are 1.165 at current and 2.000 at all coarser levels;
mean conflicted candidates remain 0.495.

The finite coarse levels preserve shared ancestry but change candidate verification because the
additional candidate is retained. Unknown additionally removes the generation ancestry overlap.
These are recorded separately and are not treated as verification-to-generation leakage.

The existing neutral-telemetry schema remains version 4 and no existing field meaning changes.
This experiment writes a dedicated location-resolution output schema version 1 with per-run payload,
derived Region, source-resolution, candidate, work, and paired-world identifiers.

### Contribution and representation judgment

The observable contribution is the current payload's exclusion of `alternate-route`, worth 0.835
mean reconstruction candidates relative to unknown in this evidence-condition cycle. No additional
candidate contribution is observable when moving from the target-layer set to the six-Region set or
to unknown.

The width-3 to width-6 equality is representation-limited: the fixed query has only the three target
Regions, so release and terminal Regions cannot become historical candidates at `[13,14]`. It does
not show that broader location distinctions are generally useless. Equality between finite broad and
unknown candidate counts also does not make their information equivalent because constraint
existence, work, ancestry, and verification records differ.

## Interpretation boundary

Any observed threshold is limited to current S5, its coarse Region representation,
`later-trace-a`, and current inference rules. Equal results across levels can mean that the candidate
space cannot express their difference. They do not establish that real location precision is
unnecessary. No Region is converted to metres or to BLE/GPS performance.

## Next independent axes

After this stage, separate candidates are observed-time window width, participant ambiguity,
relation specificity, and—only if justified after single-axis results—location × time interaction.
Topology incompleteness, physical space, sensor calibration, rescue actions, and active probing remain
outside this experiment.
