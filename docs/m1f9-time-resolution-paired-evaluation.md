# M1-F9 paired time-resolution evaluation

## 1. Objective and preregistration status

M1-F9 tests whether the M1-F8 bounded-duration rule remains monotone and safe across a family of
seed-varying synthetic candidate worlds. The observation-time profiles are paired within each
world. This is not a new time-rule design, S5 migration, physical model, or real-world calibration.

This document and
`experiments/m1f9_time_resolution_paired/experiment.json` fix the generator, profiles,
hypotheses, invalid conditions, metrics, and failure conditions before the 200-seed evaluation is
run. The results section is intentionally left pending in this preregistration revision and will be
filled without changing these choices.

## 2. Fixed M1-F8 rule and semantics

The implementation must call the unchanged M1-F8 rule:

```text
rule ID: bounded-duration-predecessor-window-v1

DerivedPast([o_min,o_max], [d_min,d_max])
  = [o_min-d_max, o_max-d_min]
```

The following remain fixed:

- public inputs only;
- inclusive integer windows;
- `CandidateGeneration` only;
- `UnknownOccurrenceTime` emits no constraint;
- `AvailableAtTick` is an access check, not a past-time operand;
- source observation, rule, dependency, duration source, and ancestry remain auditable;
- alternatives from one observation do not create independent source groups;
- constraint and candidate IDs are deterministic and truth-free; and
- truth enters only after public reconstruction returns.

No F8 derivation, observation, constraint, candidate-inclusion, or acceptance-fixture source file
may change in F9.

## 3. Preregistered world generator

Generator ID is `m1f9-simple-predecessor-layer-v1`. Every random variable uses the repository's
specified `StableRandom` SplitMix64 implementation with SHA-256-derived streams keyed by experiment
ID, generator ID, seed, and purpose. Each purpose has a separate stream so implementation changes
to one draw sequence cannot silently shift unrelated variables.

| Item | Fixed generation rule |
| --- | --- |
| seed range | every integer `0..199`; no seed filtering or replacement |
| candidate count | discrete uniform integer `5..12` |
| later-event baseline tick `L` | discrete uniform integer `30..60` |
| duration minimum `d_min` | discrete uniform integer `3..6` |
| duration width | discrete uniform integer `0..3` |
| duration maximum | `d_max = d_min + width`, hence `3..9` |
| actual truth duration | discrete uniform integer in generated `[d_min,d_max]` |
| truth slot | discrete uniform candidate slot, chosen before candidate ticks |
| truth tick | `L - actualTruthDuration`; therefore inside Exact-derived window |
| Broad-derived window | `[L-3-d_max, L+3-d_min]` |
| non-truth candidate tick range | Broad-derived window extended by 3 ticks on both sides |
| non-truth candidate ticks | discrete uniform within that extended closed range |
| tick collision | allowed; distinct candidates at one tick remain distinct Regions |
| Region assignment | unique `seed-{seed}-candidate-{slot}` identifier; no spatial semantics |
| candidate ordering | slot order in generation; canonical time/Region/ID order in output |
| dependency Region scope | all generated candidate Regions, identical for all four profiles |
| availability | `L + 3 + 2`; two ticks after the fixed Broad observation end |
| cutoff | equal to availability |

The truth slot, actual duration, and truth tick are private evaluation-side world facts. The public
dependency receives only the duration bounds and all candidate Regions. The public observation
receives only its profile window. Neither the deriver nor reconstructor receives truth slot,
actual duration, or truth ID.

Candidate counts, ticks, Regions, later tick, duration bounds, actual duration, and truth candidate
vary by seed. There is one target, one later event, one dependency, and one predecessor candidate
layer in every world.

### Duplicate and invalid-world handling

Candidate tick collisions are retained; they are not deduplicated because unique Regions identify
different candidates. Duplicate Region IDs, duplicate candidate keys, or a missing truth candidate
are invalid. The generator construction makes them impossible.

The following are invalid and must fail the evaluation rather than skip a seed:

- seed set differs from exact `0..199`;
- candidate count or generated tick lies outside its declared range;
- a negative tick or invalid duration interval;
- no candidate universe;
- truth tick is outside Exact-derived window;
- dependency scenario, target, or later-event mismatch;
- profile nesting or profile identity differs from the registered four profiles;
- a candidate Region is outside the dependency scope; or
- an observation is unavailable at the evaluation cutoff.

No result-based rejection, regeneration, or post-hoc parameter adjustment is permitted. The chosen
ranges make valid construction possible for every required seed.

## 4. Fixed and varied variables

Fixed across all seeds and profiles:

- F8 formula and rule ID;
- interval inclusivity and conservative hull semantics;
- profile construction offsets;
- candidate overlap/inclusion method;
- candidate generation work accounting;
- public dependency shape and duration-generation ranges;
- evidence state, public visibility, usage role, provenance, and source independence;
- unknown behavior and availability semantics;
- deterministic random algorithm and stream purposes; and
- post-reconstruction truth evaluation.

Varied by seed only:

- candidate count, ticks, Regions, and tick density;
- later-event baseline tick;
- duration minimum, width, maximum, and private actual duration; and
- truth candidate slot and tick.

Within one seed all four profiles share byte-identical public candidate states, duration bounds,
later-event baseline, target, availability, ancestry, and truth world. Only occurrence-time content
and the constraint derived from it differ.

## 5. Four paired profiles

For generated later-event tick `L`:

| Profile | Observation occurrence-time content | Expected derived window |
| --- | --- | --- |
| Exact | `[L,L]` | `[L-d_max,L-d_min]` |
| Medium | `[L-1,L+1]` | `[L-1-d_max,L+1-d_min]` |
| Broad | `[L-3,L+3]` | `[L-3-d_max,L+3-d_min]` |
| Unknown | event observed, occurrence time unknown | no time-derived constraint |

The offsets `0`, `1`, and `3` are fixed before evaluation. They create strictly nested,
hand-checkable windows and two finite expansion bands while keeping the candidate universe small.
They will not be widened after observing results.

## 6. Hypotheses and per-seed invariants

Primary rule-level invariant for all 200 seeds:

```text
DerivedPast(Exact) subset DerivedPast(Medium) subset DerivedPast(Broad)
```

Primary candidate-level invariant for all 200 seeds:

```text
Candidates(Exact)
  subset-or-equal Candidates(Medium)
  subset-or-equal Candidates(Broad)
  subset-or-equal Candidates(Unknown)
```

Set membership, not only counts, is checked per seed. Because generator truth is compatible with
the public duration bound, every profile is expected to contain truth and produce zero false
exclusions.

Strict candidate increases are not required. If an expanded derived window contains no additional
candidate state, the candidate set may plateau. That is representation saturation, not proof that
the observation precision is unnecessary.

## 7. Metrics and artifacts

For each profile the aggregate reports mean, median, minimum, and maximum candidate count;
containment; false exclusion; mean count and fraction reduction relative to Unknown; and mean
CandidateGeneration work.

Per-seed audits report:

- generated candidate states and canonical candidate IDs;
- truth candidate, truth tick, and private actual duration;
- later-event tick, public duration bound, candidate tick-generation range, and availability;
- every input and derived window;
- derivation success/failure, constraint ID, source group, candidate IDs and Regions;
- containment, false exclusion, generation work, and reduction versus Unknown; and
- adjacent derived-window and candidate-set inclusion.

Summary counts include Exact&lt;Medium, Medium&lt;Broad, Broad&lt;Unknown, all-finite equality, adjacent
finite plateaus despite strictly wider constraints, and Broad/Unknown saturation. Duration width is
secondary telemetry only.

Planned deterministic artifacts are aggregate/report JSON, per-seed JSONL, aggregate CSV,
per-seed monotonicity CSV, and manifest JSON.

## 8. Failure conditions

Evaluation fails before aggregate interpretation if any of these occur:

- wider observation uncertainty produces a narrower derived window;
- Exact candidate set is not contained in Medium;
- Medium is not contained in Broad;
- Broad is not contained in Unknown;
- any false exclusion;
- constraint or candidate output depends on truth identity;
- availability supplies or changes occurrence time;
- Unknown is treated as event absence;
- generator parameters or rule semantics vary by seed outside registered ranges;
- one source becomes multiple independent sources; or
- identical clean-commit runs differ byte-for-byte.

The original F8 `2 -> 3 -> 4 -> 4` acceptance result and all existing S5 behavior remain regression
requirements.

## 9. Results

Pending the preregistered `0..199` execution. This section will be filled from machine-readable
artifacts without changing Sections 2–8.

## 10. Interpretation and claim boundary

At most, F9 can establish that in this registered synthetic-world family, under the fixed public
bounded-duration rule, broader observation occurrence-time uncertainty conservatively broadens
the historical constraint and maintains or reduces candidate narrowing.

F9 cannot establish real-world effect sizes, a required tick precision, calibrated movement
durations, S5 applicability, time-versus-location importance, or proportional rescue-performance
improvement.

## 11. Next recommendation

Pending the semantic, monotonicity, safety, and representation-saturation results. The choice among
Recommend A/B/C/D/E will be based on unresolved capability gaps, not numerical neatness.
