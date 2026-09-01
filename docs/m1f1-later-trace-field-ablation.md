# M1-F1 `later-trace-a` field-level ablation

## Research question and boundary

M1-F identified `later-trace-a` as the only single S5 observation family that reproduced Full's
two-region reconstruction result when present alone. M1-F1 asks whether that family-level effect can
be attributed to its subject, time, location, or relation information while the paired synthetic
world, public topology, Region/tick representation, query, evidence-state semantics, inference,
verification, traversal, and scoring rules remain fixed.

`M1-F1` and “field-level ablation” are descriptive work labels. This stage adds no inference rule,
partial-constraint rule, probability, calibration, rescue behavior, sensor model, or causal
formalism. It retains the E2 gate **READY WITH EXPLICIT LIMITATIONS** and the M1-F result and
interpretation unchanged.

Baseline source commit: `8b3f33a67af9701ba019079e286af9134172f12f` (`m1/f-observation-ablation`).
The unchanged S5 scenario and M1-F experiment are inputs to a separate experiment configuration.

## Original `later-trace-a` information and lineage

For every S5 evidence condition, `SimulationEngine` constructs one public `InteractionObservation`
for source key `later-trace-a` from scenario declarations. Its current information is:

| Item | Current synthetic content |
| --- | --- |
| subject | target `person-a` and participant/source `person-b`, both typed as persons |
| observed time | closed point interval `[24,24]` |
| availability metadata | public/query-usable at tick 25, before the inclusive cutoff 28 |
| location | recorded Region set `{fork-east, fork-west}` |
| observed relation | `PersonPersonEncounter` |
| evidence state | `Confirmed`, an asserted boundary state rather than an accuracy score |
| direct source / ancestry | `source-a` / `record-family-a` |

The scenario then declares two CandidateGeneration derivations from that same observation:

| Constraint | Target window | Allowed regions | Relation / transformation | Independence |
| --- | --- | --- | --- | --- |
| `generation-a` | closed `[13,14]` | east, west | `later-public-trace` / `later-trace-to-target-window` | source-a / record-family-a |
| `generation-a-redundant` | closed `[13,14]` | east, west | `later-public-trace-derived` / `same-record-secondary-derivation` | same source and ancestry; not independent |

The source is generation-only. It is not a verification or traversal source. Removing it may still
change how a separate verification record's ancestry overlap is classified, but it does not make the
source itself verification evidence.

## Field semantics

The manipulated fields are information contents, not serializer properties:

- `subject`: the observed participant identity and entity kind (`person-a` with `person-b`);
- `time`: the observed time window `[24,24]`; availability tick 25 remains fixed cutoff metadata and
  is not silently substituted for the missing observed time;
- `location`: the recorded `{fork-east, fork-west}` Region set; and
- `relation`: the observed encounter relation together with the declared later-trace transformation
  context needed to interpret the record.

The current production `InteractionObservation` and `CausalTracingConstraint` contracts do not
represent a partially specified version of this source. More importantly, the declared transformation
needs all four contents to assert who the constraint concerns, when the source was observed, which
state set it carries, and under what relation it can be applied. If a required field is unavailable,
M1-F1 therefore retains the underlying paired world record for audit but does not emit a method-usable
source record or either derived constraint. It does not recover the missing content from truth,
scenario-hidden allowed regions, candidate state, another field, or a default value.

This is a conservative characterization of the existing atomic transformation. It does not assert
that all four fields have equal real-world value. It also does not invent weaker subject-only,
time-only, location-only, or relation-only constraints.

## Fixed and manipulated variables

Only the set of available information fields on `later-trace-a` changes. For each seed, all profiles
share the same truth realization, person/device trajectory, hazard realization, random sequence,
public topology, public candidate states, target query/window/cutoff, source ancestry,
`EvidenceState`, generation/verification/traversal usage boundary, local-neighborhood rule, candidate
identity, and truth scoring. Other M1-F observation families remain at Full availability.

The implementation reuses the same deterministic M1-F Full and `without-later-trace-a` method
executions for a seed. A profile with all required fields references Full; an incomplete profile
references `without-later-trace-a`. This prevents a profile-dependent simulation or inference rule
from entering the comparison.

## Preregistered derivation rule

```text
available subject + time + location + relation
  -> public source record eligible for derivation
  -> generation-a and generation-a-redundant
  -> existing candidate-generation behavior

any required field unavailable
  -> source record not eligible for the current transformation
  -> no source-derived evidence usable by the method
  -> no generation-a or generation-a-redundant
  -> unavailable knowledge, not negative evidence
```

The second branch is deliberately equivalent to source-family removal for method input. The
experiment output separately records the underlying source record's existence, available and missing
fields, eligibility, evidence generation, constraint derivation, generated IDs, declared/applied
usage roles, candidate regions before/after, and the M1-F behavior used.

## Core conditions fixed before result inspection

All ten conditions are core and use seeds 0 through 199:

1. `full`: subject + time + location + relation;
2. `minus-subject`;
3. `minus-time`;
4. `minus-location`;
5. `minus-relation`;
6. `none`;
7. `subject-only`;
8. `time-only`;
9. `location-only`; and
10. `relation-only`.

There are no exploratory conditions. The four single-field removals already test every three-field
combination. Because the preregistered existing transformation requires all four fields, extra
two-field combinations cannot become derivable without adding new semantics and are not run.

## Metrics fixed before result inspection

Safety is truth containment and false exclusion. Narrowing is mean historical candidate-state count
and reduction from the three-region target layer. Work is candidate generation, candidate
verification, forward/backward public transition traversal, public relations inspected, evidence
records inspected, and source groups inspected. Evidence behavior retains source groups,
same-ancestry derivation count, conflicting/uncertain/unavailable evidence, unresolved/conflicted
candidates, and candidate-verification dispositions.

Derivability is recorded independently: available/missing fields, underlying source existence,
source-record eligibility, evidence generation, constraint-derivation success/failure, generated
constraint IDs, declared/applied usage roles, and traversal-input generation.

A profile is Full-equivalent only if every paired seed has identical method candidate sets,
containment, false exclusion, work, reconstruction candidate dispositions, and verification
dispositions. A candidate minimum means only the smallest Full-equivalent field subset among these
ten S5 profiles. It is not a Footprint-general minimum observation set.

## Preregistered failure conditions

The experiment is invalid if a hidden field remains in a derived constraint; a missing field is
filled from truth, scenario declarations, candidate state, another field, or a default; one seed's
world or topology changes across profiles; missing becomes `Absent`; verification becomes generation;
device evidence becomes person evidence; ancestry semantics change; a profile changes inference
rules; field grouping is changed after viewing results; or output is non-deterministic.

The experiment also fails if an incomplete profile emits `generation-a` or
`generation-a-redundant`, or if `later-trace-a` becomes a traversal input.

## Results

Results were added only after the preregistration above was fixed. All ten core profiles completed
seeds 0 through 199: 2,000 paired profile runs and 6,000 method runs. Each seed had exactly one
truth-world identifier and one public-topology identifier across all profiles. Every profile retained
100% truth containment and zero false exclusions.

### Full and incomplete-profile method results

All nine incomplete profiles (`minus-*`, `*-only`, and `none`) were method-equivalent because none
could instantiate the current all-field transformation. They are shown together here without
discarding their separate per-profile output records.

| Condition | Fields | Method | Mean candidates | Containment | False exclusion | Reduction | Gen | Verify | Forward | Backward |
| --- | --- | --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| Full | subject, time, location, relation | backward traversal | 2.000 | 100% | 0/200 | 33.33% | 0 | 0 | 4.000 | 4.000 |
| Full | subject, time, location, relation | reconstruction | 2.165 | 100% | 0/200 | 27.83% | 5.330 | 0.835 | 0 | 0 |
| Full | subject, time, location, relation | reconstruction + local traversal | 2.000 | 100% | 0/200 | 33.33% | 5.330 | 0 | 4.000 | 4.000 |
| each incomplete profile | profile-specific subset | backward traversal | 2.000 | 100% | 0/200 | 33.33% | 0 | 0 | 4.000 | 4.000 |
| each incomplete profile | profile-specific subset | reconstruction | 3.000 | 100% | 0/200 | 0% | 5.165 | 0.835 | 0 | 0 |
| each incomplete profile | profile-specific subset | reconstruction + local traversal | 2.000 | 100% | 0/200 | 33.33% | 5.165 | 0 | 4.000 | 4.000 |

For reconstruction, every single-field removal had the same Full-relative delta:

| Removed field | Constraint derivable | Δ candidates | Δ containment | Δ false exclusion | Δ reduction | Δ gen work | Δ verify work | Δ relations | Δ evidence records |
| --- | --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| subject | no | +0.835 | 0 | 0 | -27.83 points | -0.165 | 0 | -2 | -2 |
| time | no | +0.835 | 0 | 0 | -27.83 points | -0.165 | 0 | -2 | -2 |
| location | no | +0.835 | 0 | 0 | -27.83 points | -0.165 | 0 | -2 | -2 |
| relation | no | +0.835 | 0 | 0 | -27.83 points | -0.165 | 0 | -2 | -2 |

For reconstruction + local traversal, every removal left candidate count, containment, reduction,
verification work, public-relation work, evidence-record work, and transition work unchanged; only
reported generation work fell by 0.165. Backward traversal was unchanged. Transition identifiers
remained `east-terminal`, `release-east`, `release-west`, and `west-terminal` under every profile.

### Single-field presence and derivability

`subject-only`, `time-only`, `location-only`, and `relation-only` each generated zero
`later-trace-a` constraints in all 200 seeds. Their method results matched `none` and every
single-field removal. Conversely, Full generated both `generation-a` and
`generation-a-redundant` in every seed. All 200 underlying source-observation references resolved,
and Full recorded two stable runtime constraint IDs per seed in addition to the two definition IDs;
incomplete profiles recorded neither as applied constraints. No one-field subset reproduced Full,
and no field could be removed while retaining the current transformation.

The observed boundary is therefore a four-field conjunction at the scenario-declared derivation
step. It is not evidence that the four fields have equal marginal effects. No extra two-field profile
was needed: every three-field `minus-*` profile already failed the same declared all-field
eligibility rule, and adding a weaker two-field constraint would have changed inference semantics.

### Evidence and verification effects

Full had mean 3.830 present source groups, 1.170 same-ancestry derived-record excess, 0.330
conflicting evidence, 1.165 uncertain evidence, 0.330 unavailable evidence, 1.165 unresolved
candidates, and 0.495 conflicted candidates. Each incomplete profile had 3.000 source groups, zero
same-ancestry excess, 0.330 conflicts, 1.165 uncertain evidence, 2.330 unavailable evidence, 2.000
unresolved candidates, and 0.495 conflicted candidates.

Direct verification work stayed at mean 0.835 because `later-trace-a` is generation-only. Candidate
verification dispositions did change indirectly because generation ancestry and the third candidate
changed: Full recorded 34 independently supported, 34 independently refuted, 99 conflicting, 66
unresolved, 68 shared-ancestry-only, and 132 unavailable dispositions. Each incomplete profile
recorded 102 supported, 102 refuted, 99 conflicting, 99 unresolved, zero shared-ancestry-only, and
198 unavailable dispositions. The separate `shared-record-check` no longer overlaps generation
ancestry when `later-trace-a` is unavailable; this is not verification-to-generation leakage.

### Candidate minimum and contribution judgment

All profiles were safety-equivalent to Full, but only Full met the preregistered strict equivalence
criterion. Thus the unique smallest Full-equivalent profile among the ten tested profiles contains
all four fields. This is only a candidate minimum for current S5, current `later-trace-a`, the current
atomic derivation, and the stated criterion.

There is no separately strongest, weak, zero-contribution, or redundant field result. Each field is
structurally required by the current transformation, and removing any one disables the same two
constraints. The family-level positive contribution is attributable only to the complete four-field
input under this representation. Field-specific marginal contributions remain unidentifiable.

The six deterministic output files totaled approximately 17.35 MB in the pre-commit evaluation.
Generated outputs remain uncommitted; exact clean-commit hashes and size are reported with the final
reproducibility check.

## Interpretation rules and limitations

If every incomplete profile has the same result, M1-F1 will classify subject/time/location/relation
contributions as **not separately identifiable in the current artificial-world representation**. It
will not call every field equally valuable or unnecessary. A field can be called unnecessary only if
the current representation permits the declared transformation without it and removal preserves the
preregistered criterion; the current rule permits no such omission.

The current raw relation is generic, recorded locations are copied from scenario-declared allowed
regions, and target-window transformation is not learned from payload. Region/tick resolution cannot
express continuous precision, observation range, timing uncertainty, participant ambiguity, relation
strength, calibrated error, or alternative partial transformations. These limitations are candidates
for later representation-sensitivity work, not reasons to fabricate partial evidence here.

Telemetry schema v4 is unchanged. M1-F1 uses a dedicated deterministic experiment-output schema v1;
it does not change existing telemetry field meanings.

## Next-stage gate

The next field-level experiment should proceed only after choosing an explicit representation and
semantics for partial observations. Useful separated axes include participant ambiguity, observed-time
window width, Region-set precision, and relation specificity. Each must be varied independently of
sensor noise, topology completeness, physical realism, and rescue actions. Results here may inform
that design and later observation-limit work, but cannot determine what a real sensor or rescuer must
collect.
