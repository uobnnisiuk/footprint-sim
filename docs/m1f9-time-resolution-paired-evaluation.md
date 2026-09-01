# M1-F9 paired time-resolution evaluation

## 1. Objective and preregistration status

M1-F9 tests whether the M1-F8 bounded-duration rule remains monotone and safe across a family of
seed-varying synthetic candidate worlds. The observation-time profiles are paired within each
world. This is not a new time-rule design, S5 migration, physical model, or real-world calibration.

This document and
`experiments/m1f9_time_resolution_paired/experiment.json` fix the generator, profiles,
hypotheses, invalid conditions, metrics, and failure conditions before the 200-seed evaluation is
run. They were committed first as preregistration commit
`ecd2bad8d0ebd9d7ff35fb345cdc27aad4e12a2a`. Sections 2–8 below are unchanged by the result entry.

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

The exact registered seed set `0..199` produced 200 worlds and 800 paired profile runs. No seed was
skipped, rejected, regenerated, or replaced.

### Primary profile metrics

| Profile | Mean candidates | Median | Min | Max | Containment | False exclusions | Mean reduction count vs Unknown | Mean reduction fraction vs Unknown | Mean generation work |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| Exact | 2.190 | 2 | 1 | 6 | 1.000 | 0 | 6.365 | 0.734593 | 9.555 |
| Medium | 3.210 | 3 | 1 | 8 | 1.000 | 0 | 5.345 | 0.618935 | 9.555 |
| Broad | 5.295 | 5 | 1 | 11 | 1.000 | 0 | 3.260 | 0.378607 | 9.555 |
| Unknown | 8.555 | 9 | 5 | 12 | 1.000 | 0 | 0.000 | 0.000000 | 8.555 |

The reduction columns are synthetic-world candidate reduction only. Finite generation work is the
generated candidate count plus one constraint application. Unknown performs no constraint
application, so its mean work equals its mean candidate universe size.

### Per-seed monotonicity and safety

| Audit | Result |
| --- | ---: |
| derived-window monotonicity violations | 0 |
| candidate-set monotonicity violations | 0 |
| false exclusions across 800 profile runs | 0 |
| seeds with `Exact < Medium` candidate count | 122 |
| seeds with `Medium < Broad` | 175 |
| seeds with `Broad < Unknown` | 190 |
| seeds with all finite candidate sets equal | 11 |
| seeds with equal adjacent finite derived windows | 0 |
| seeds where a finite constraint widened but its candidate set did not | 92 |
| seeds where Broad already equalled Unknown | 10 |

Every finite derived window strictly expanded at both registered profile transitions. Candidate
set equality therefore never came from an unchanged rule output. It came from the absence of a
candidate state in the newly added time band.

The 11 all-finite plateau seeds are `4, 43, 44, 47, 61, 73, 80, 101, 133, 150, 172`. The 10
Broad/Unknown saturation seeds are `62, 68, 71, 85, 96, 112, 118, 124, 170, 180`. Complete lists,
including all 92 adjacent finite plateaus, are in the report JSON and each individual transition is
in the monotonicity CSV.

### Duration-bound observations

The generator exercised the complete registered duration range without making duration the primary
comparison axis:

| Duration item | Observed result |
| --- | ---: |
| `d_min` range | 3–6 |
| `d_max` range | 3–9 |
| width range | 0–3 |
| mean width | 1.605 |
| width 0 worlds | 47 |
| width 1 worlds | 42 |
| width 2 worlds | 54 |
| width 3 worlds | 57 |

No interaction effect is inferred from these frequencies. Duration is recorded so later analyses
can stratify results without changing this primary observation-time comparison.

## 10. Representation sensitivity and failure audit

The 92 seeds with at least one finite plateau demonstrate the intended representation boundary:
the derived constraint changed, but the discrete candidate layer had no state in the added band.
This is analogous to the F2 Region plateau. It is not evidence that the extra time precision is
unnecessary.

Broad equalled Unknown in 10 worlds even though Broad still generated a finite constraint. In those
worlds all generated candidates happened to fall inside the Broad-derived window. Unknown remained
semantically distinct through constraint existence, derivation failure, provenance, and one less
generation step.

All registered failure gates remained clear:

- 0 window monotonicity failures;
- 0 candidate-set inclusion failures;
- 0 false exclusions;
- all finite profiles used `bounded-duration-predecessor-window-v1` and one source group;
- every Unknown profile retained its source observation and returned `UnknownOccurrenceTime`;
- truth substitution tests left public observations, constraints, derived windows, and candidate
  IDs unchanged; and
- changing `AvailableAtTick` between two usable ticks changed neither derived windows nor candidate
  sets.

## 11. Regression and deterministic reproduction

The unchanged original M1-F8 acceptance fixture still returns candidate counts `2 -> 3 -> 4 -> 4`,
its interval formula and rule ID are unchanged, and all M1-F8 tests remain in the regression suite.
No S5 scenario, `later-trace-a`, F2/F3 experiment, or existing reconstruction contract is modified.

The F9 writer emits:

- `time-resolution-paired.json` — full report and aggregates;
- `time-resolution-paired-runs.jsonl` — 200 per-seed worlds and all profile results;
- `time-resolution-paired.csv` — primary profile table;
- `time-resolution-paired-monotonicity.csv` — 600 adjacent per-seed set audits; and
- `time-resolution-paired-manifest.json` — inputs, clean source commit, seeds, rule, and files.

Two executions from the final clean commit produce byte-identical copies of all five files. No
generated result is committed.

## 12. Interpretation and claim boundary

F9 establishes that in this registered synthetic-world family, under the fixed public
bounded-duration rule, broader observation occurrence-time uncertainty conservatively broadens the
historical constraint and maintains or reduces candidate narrowing. This held as a per-seed set
invariant, not only as an aggregate trend.

F9 cannot establish real-world effect sizes, a required tick precision, calibrated movement
durations, S5 applicability, time-versus-location importance, or proportional rescue-performance
improvement.

## 13. Next recommendation

**Recommend A:** the time-resolution track is semantically established for one explicit rule and
one preregistered seed-varying world family. The next missing cross-axis capability is a justified
participant-side observation-to-constraint slice with explicit ambiguity and conservative
alternative propagation.

This recommendation does not follow merely from ordered means. It follows from zero semantic,
monotonicity, safety, truth-boundary, or determinism defects combined with a clear account of the 92
representation plateaus. A duration-interaction study would deepen one already-working rule but not
address the participant and relation propagation gaps found by F5/F6. Candidate/world sensitivity
remains valuable after at least one second axis uses the derivation envelope. S5 migration remains
blocked by its missing public dependency, and minimal-observation conclusions remain premature.
