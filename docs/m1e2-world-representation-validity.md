# M1-E2 synthetic-world representation validity

## Objective and baseline

M1-E2 audits whether the S5 representation of public observations, evidence states, constraints,
time, local candidate states, and public transitions is internally meaningful. It does not evaluate
whether Method C is performant, introduce an inference rule, tune candidate counts, or model rescue
performance.

The baseline is M1-E1 commit `17088f70d3de169175bca3e49bdbd43b255d49d6`, whose parent is M1-E
commit `7dfb7241d7add74a03217291747fcf899d9d90b5`. The audit covers six independent questions:

1. the truth-independent Confirmed verification fixture;
2. the 67 E1 unresolved source references;
3. observation-to-evidence-to-constraint lineage;
4. the local-neighborhood definition;
5. tick and interval semantics; and
6. public-topology completeness.

For each question, implementation fact, synthetic-world meaning, validity judgment, and any minimal
correction are kept separate below.

## Synthetic-world assumptions

S5 is an uncalibrated integer-tick world. Public candidate states and the public hazard envelope are
scenario declarations, not recovered from the realized truth path. Constraint derivations are also
declared fixtures: the simulator checks their source record, time, evidence state, and subject type,
but it does not calculate every allowed-region set from observation payload. The scenario therefore
tests declared transformations, not the empirical correctness of those transformations.

Recorded source steps are unconditional across S5 seeds; the evidence-condition cycle selects which
declared constraint transformations are active. The current six conditions therefore vary available
constraint semantics, not raw observation emission. They must not be presented as an
observation-value or minimum-observation-set experiment.

The public hazard envelope is intentionally seed-independent and includes every west/east branch in
the modeled synthetic transport route. It is treated as complete for that declared route. This is an
assumption of S5, not a claim that real public topology is complete.

## A. Truth-independent Confirmed fixture

### Implementation fact

`public-environment-west-record` is a tick-27 `ObservationRecord` reported about `person-a` in
`fork-west`. It has evidence type `public-environment-west`, state `Confirmed`, zero delay, and a
payload identifying it as a synthetic truth-independent verification record. The active
`verification-independent` definition refers to that evidence type, preserves the point time and
evidence state, and declares the transformation
`independent-environment-verification` into a tick-13–14 west-only verification constraint.

The step and constraint are unconditional scenario inputs. Neither their construction nor source
lookup receives the realized east/west truth branch. All seeds therefore receive the same record;
the constraint is selected only by the preregistered evidence-condition cycle. M1-E2 does not change
the truth branch, allowed regions, occurrence probability, evidence probability, or condition cycle.

### Meaning and validity judgment

`Confirmed` means that a record is asserted without an uncertainty qualifier at the evidence
boundary. It does **not** mean truth-matching, calibrated accuracy, high precision, or ground-truth
confirmation. The E1 verification rule may describe a candidate as independently supported or
refuted by such a record, but verification does not silently delete that candidate.

Under that record-level meaning, a truth-independent false-positive fixture is coherent: an asserted
public record can be wrong in the synthetic world. The name remains potentially easy to overread, so
the enum documentation now states the boundary explicitly. Renaming the established enum would
change a repository-wide contract without correcting the fixture; it is therefore not done.

## B. Classification of the 67 E1 unresolved references

The E1 count was per query constraint usage, not per candidate. Every unresolved row belongs to one
of two public, cutoff-available verification sources:

| E1 class | Direct source / source key | Runs | Candidate effects | E1 disposition | Cause | Judgment |
| --- | --- | ---: | ---: | --- | --- | --- |
| shared record | `source-verification-shared` / `shared-record-check` | 34 | 68 candidates, two per run | `SharedAncestryOnly` | scenario declared a public source key but emitted no corresponding record | lineage fixture defect |
| conflicting record | `source-verification-conflict` / `independent-conflicting-check` | 33 | 99 candidates, three per run | `Conflicting` | scenario declared a public source key but emitted no corresponding record | lineage fixture defect |

The shared-record runs are seeds
`1, 7, 13, 19, 25, 31, 37, 43, 49, 55, 61, 67, 73, 79, 85, 91, 97, 103, 109, 115, 121, 127, 133, 139, 145, 151, 157, 163, 169, 175, 181, 187, 193, 199`.
The conflicting-record runs are seeds
`2, 8, 14, 20, 26, 32, 38, 44, 50, 56, 62, 68, 74, 80, 86, 92, 98, 104, 110, 116, 122, 128, 134, 140, 146, 152, 158, 164, 170, 176, 182, 188, 194`.
Together these lists classify all 67 query/run references. E1 telemetry retains the per-seed query,
constraint, and generated `iobs-*` identifiers; the stable source classes above explain every row.

These were not the deliberately unavailable cases. `verification-delayed` is unavailable at tick 30,
after cutoff 28, and `verification-private` has private-evidence visibility. Neither enters the public
evidence set or the 67 used unresolved references.

### Minimal correction

S5 now emits one deterministic public `ObservationRecord` for each missing key. The shared record
reports `person-a`, preserves `Confirmed`, and declares `record-family-a` in its public payload. The
conflicting record reports `person-a` in `fork-east` and preserves `Conflicting`. Their source event
subjects remain `person-b` and `person-c` respectively. No constraint meaning or method rule changes.

Schema-v5 validation now requires every non-cross-thread public source used by the query cutoff to
resolve to exactly one deterministic recorded evidence type. It also rejects observed-time,
EvidenceState, target-person, device-only subject, or availability mismatches. Future and private
definitions remain allowed without a public record because they are not public method inputs.

## C. Observation, evidence, and constraint lineage

`CandidateGeneration`, `CandidateVerification`, and `Traversal` below are engineering usage roles,
not causal relations. Evaluation truth is not a constraint source.

| Definition | Usage | Source record or explicit synthetic source | Reported subject / source holder | Observed / available | Recorded → resulting state | Declared transformation and constraint meaning | Resolution after correction |
| --- | --- | --- | --- | --- | --- | --- | --- |
| `generation-a` | CandidateGeneration | condition-specific `InteractionObservation` for `later-trace-a` | person-a with person-b | 24 / 25 | Confirmed → Confirmed | later trace → east or west at 13–14 | resolved synthetic interaction observation |
| `generation-a-redundant` | CandidateGeneration | same `later-trace-a` observation | person-a with person-b | 24 / 25 | Confirmed → Confirmed | secondary derivation of the same source; not independent | resolved, same source group |
| `generation-b-uncertain` | CandidateGeneration | condition-specific `InteractionObservation` for `later-trace-b` | person-a with person-c | 26 / 27 | uncertain → uncertain | uncertain later trace → west at 13–14 without hard outer exclusion | resolved synthetic interaction observation |
| `generation-conflict` | CandidateGeneration | condition-specific `InteractionObservation` for `later-trace-conflict` | person-a with person-c | 25 / 26 | Conflicting → Conflicting | conflicting later trace → alternate route at 13–14 | resolved synthetic interaction observation |
| `traversal-later-state` | Traversal | `known-later-public-state` ObservationRecord | person-a / person-b | 27 / 27 | Confirmed → Confirmed | coarse later record → terminal west/east constraint at 18–32 | resolved observed fixture |
| `verification-independent` | CandidateVerification | `public-environment-west` ObservationRecord | person-a / person-b | 27 / 27 | Confirmed → Confirmed | truth-independent west check of 13–14 candidate | resolved observed fixture |
| `verification-shared` | CandidateVerification | `shared-record-check` ObservationRecord | person-a / person-b | 27 / 27 | Confirmed → Confirmed | east/west check sharing `record-family-a`; not independent | resolved after E2 fix |
| `verification-conflicting` | CandidateVerification | `independent-conflicting-check` ObservationRecord | person-a / person-c | 27 / 27 | Conflicting → Conflicting | independent conflicting east check | resolved after E2 fix |
| `verification-delayed` | CandidateVerification | explicit scenario-only future source `delayed-check` | declared person-a/person-b | 27 / 30 | Confirmed → Confirmed | west check unavailable at cutoff 28 | intentionally excluded, not a used unresolved reference |
| `verification-private` | CandidateVerification | explicit scenario-only private source `private-check` | declared person-a/person-b | 27 / 27 | Confirmed → Confirmed | east check with private-evidence visibility | intentionally excluded, not a used unresolved reference |
| `verification-device` | CandidateVerification | `device-fix` ObservationRecord | device-d / device-d | 26 / 27 | uncertain → uncertain | device terminal record → device-only west check at 13–14 | resolved; cannot support/refute person-a |

The ordinary tick-1 access observation remains present evidence but is not assigned an M1-E
constraint usage role. Truth scoring is EvaluationOnly and is passed to evaluation after all public
methods return.

### Boundary audit

- Recorded person sources report `person-a`; the device source reports only `device-d`.
- `IsDeviceOnlyEvidence` remains true and `IsPersonEvidence` false for `verification-device`.
- An absent source record is represented by `SourceObservationResolved=false`; no `Absent` field or
  false world proposition is created.
- Tick-27 observations become evidence for earlier windows only through named, scenario-declared
  transformations. They are not copied into tick 13–14 as direct world facts.
- Verification sources are not promoted into generation or traversal usage.
- Truth and EvaluationOnly fields are not accepted by the reconstructor or tracer contracts.

The remaining limitation is important: the transformation rule and allowed regions are declared by
the fixture rather than computed from the observation payload. M1-E2 validates the lineage boundary,
not the empirical validity of those transformations.

## D. Local-neighborhood semantics

The corrected local neighborhood is a deterministic chronological selection over public candidate
state layers:

1. every state whose closed interval overlaps the target window;
2. every state whose closed interval overlaps the target window expanded by the declared padding;
3. the immediately preceding public state layer; and
4. the immediately following public state layer.

S5 declares target 13–14 and padding one tick. Its three selected layers are 8–10, 13–14, and 18–32.
The predecessor and successor are included to make actual transition traversal possible; they were
fixed before E1 results. Selection uses only the query and scenario-declared public candidate states.
It does not inspect truth, private trajectory, realized parent, seed outcome, or candidate containment.

“Local” here means a bounded chronological neighborhood of public state layers, not geometric
distance. The rule is reusable for scenarios that expose the same ordered aggregate-state contract,
but it is not a general graph-neighborhood definition. A long predecessor or successor interval is
selected as one whole layer, and overlapping branch-specific state lists could select more states
than intended. These are documented limitations; windows are not tuned in M1-E2.

Tests fix deterministic selection under reversed input order and the inclusion of exact padding
boundaries.

## E. Temporal semantics

All M1-E2 times use integer ticks and closed intervals.

| Item | Representation and boundary behavior |
| --- | --- |
| ObservationRecord | point time `ObservedTick`; availability is a separate point `AvailableTick` |
| InteractionObservation | exact point when start=end, otherwise closed observed interval |
| candidate state | declared event point or closed state interval `[start,end]` |
| target | closed interval `[13,14]` |
| padding | closed expanded interval `[target.start-padding,target.end+padding]` |
| constraint | closed observed window and closed constrained window |
| cutoff | inclusive: `AvailableAtTick <= cutoff` is usable |
| public transition | integer min/max travel range; propagated arrival range must overlap the closed destination interval |
| later-known state | observation point 27 transformed by declaration to closed state interval 18–32 |
| environment record | observation point 27 transformed by declaration to closed target interval 13–14 |

Overlap is `left.start <= right.end && right.start <= left.end`; equal endpoints overlap. Candidate
lookup also includes both endpoints. Public transition enumeration is deterministically sorted by
destination and provenance. No half-open interval is used. The resolution is one simulation tick;
M1-E2 does not refine it to improve a result.

Tests cover equal-boundary overlap, cutoff equality, and deterministic local-state selection. No
off-by-one defect was found in the audited S5 path.

## F. Public-topology completeness

The public candidate states come directly from `pastStateReconstruction.publicCandidateStates`.
Public branches come from the scenario stochastic-hazard transitions filtered by
`publiclyVisible=true`; they are not the realized selected truth transitions and carry no random draw
or realized parent. S5 publishes release→west/east and west/east→terminal branches, so both possible
truth routes are present for every seed.

The scenario intentionally treats this declared west/east topology as complete for its synthetic
transport model. `alternate-route` is a conflicting candidate with no public hazard path and is
therefore documented as topology-inconsistent in S5. This is a scenario assumption, not evidence
that lack of an edge in an incomplete real map makes movement impossible.

Current tracer behavior has two boundaries:

- if no public branch is available from the local source set, it performs no transition step and
  leaves candidates unchanged;
- in a partially connected model with at least one known path, candidates without a known path can
  be removed by transition consistency.

The second behavior is valid for S5 only under its completeness assumption. Applying it to incomplete
observed topology would require a separate inference-semantics decision. M1-E2 adds a characterization
test for the no-edge case and does not change the tracer.

## Discovered defects and corrections

Two lineage defects were found: the shared and conflicting public verification definitions had no
materialized source record, accounting for all 67 E1 unresolved used references. They were corrected
without changing truth, regions, evidence states, probabilities, condition selection, candidate
windows, constraints, or inference rules. Scenario validation and invariant tests prevent recurrence.

No temporal-boundary bug, person/device promotion, truth leakage, verification-role leakage, local
selection nondeterminism, or missing-to-absence conversion was found. No telemetry field was needed;
schema version 4 already represents source resolution and usage roles, so its meaning and version
remain unchanged. Scenario schema version 5 also remains unchanged; validation is stricter for its
record-backed, cutoff-usable sources.

## Before/after result impact

The semantic change is source materialization, so the corrected 200-seed S5 comparison is rerun even
though no method contract changed. The comparison below reports the result without treating numerical
improvement as the objective.

| Measure | E1 baseline | E2 after lineage correction | Difference |
| --- | ---: | ---: | ---: |
| backward containment / false exclusions | 100% / 0 | 100% / 0 | none |
| reconstruction containment / false exclusions | 100% / 0 | 100% / 0 | none |
| local containment / false exclusions | 100% / 0 | 100% / 0 | none |
| backward mean candidates / reduction | 2.000 / 33.33% | 2.000 / 33.33% | none |
| reconstruction mean candidates / reduction | 2.165 / 27.83% | 2.165 / 27.83% | none |
| local mean candidates / reduction | 2.000 / 33.33% | 2.000 / 33.33% | none |
| generation usage resolved | 633 / 633 | 633 / 633 | none |
| traversal usage resolved | 200 / 200 | 200 / 200 | none |
| verification usage resolved | 100 / 167 | 167 / 167 | 67 lineage defects resolved |
| reconstruction mean evidence records inspected | 8 | 10 | two materialized public records |

Generation work remains 5.330 per reconstruction/local run. Verification work remains 0.835 for
reconstruction and zero additional work for local traversal. Local forward/backward work remains
4/4 per run. Candidate verification dispositions remain 34 independently supported, 34 independently
refuted, 99 conflicting, 66 unresolved, 68 shared-ancestry-only, and 132 unavailable. The four public
transition references are unchanged:

- `ptr-1e6108de3493d505`;
- `ptr-6d1c157cb358ae99`;
- `ptr-b8572e88c7e6a5a1`; and
- `ptr-f9521f5361d6cbd3`.

The unchanged method outcomes are expected because source records now ground already-existing
constraints; neither constraints nor inference semantics changed.

## Remaining validity threats

- Constraint allowed regions and transformations are declared fixtures, not learned or calibrated
  mappings from observation payload.
- `Confirmed` is an evidence-boundary assertion, not an accuracy estimate.
- The public hazard topology is artificially complete for the modeled route.
- A partially known real topology cannot safely inherit S5's topology-consistency interpretation.
- Recorded source validation currently requires deterministic materialization for cutoff-usable
  schema-v5 sources. A later observation-emission experiment must make constraint availability depend
  on actual source availability rather than orphan a constraint.
- Source records are unconditional while the condition cycle activates transformations. A future
  observation-limit experiment must couple record availability and derived constraints rather than
  reuse the present cycle as if it varied observations.
- `PublicEnvironmentalObservation` is an inherited broad source-kind label that also carries synthetic
  public record checks; M1-E2 does not introduce a replacement taxonomy.
- Integer-tick resolution, declared interval widths, and aggregate public state layers remain
  synthetic abstractions.

## What this stage does not establish

M1-E2 does not establish Method C superiority, real-world observation accuracy, real topology
completeness, physical realism, minimum observation sets, rescue workload, rescue handoff, closed-loop
rescue behavior, predictive validity, a belief system, a new causal formalism, or a Buddhist-method
identity.

## Gate for the next stage

The gate categories are `READY`, `READY WITH EXPLICIT LIMITATIONS`, and `NOT READY`. Passing requires
meaningful artificial observation semantics, traceable public lineage, classified unresolved sources,
explicit time and local-neighborhood semantics, a stated topology assumption, no truth/evaluation
leakage, and documented remaining assumptions.

The gate decision is **READY WITH EXPLICIT LIMITATIONS**. Current S5 observation meanings are stated,
every cutoff-usable source resolves, all former unresolved usages are classified, time and local-state
boundaries are explicit, public topology is truth-independent, and truth/evaluation-only information
does not enter a method.

An observation-limit experiment may proceed only while keeping the declared transformation and
topology assumptions explicit. Varying observation emission or delay additionally requires
constraints to be conditioned on actual source availability; otherwise it would recreate an
orphan-source defect. A result under incomplete public topology must not reuse S5's completeness
assumption without an explicit inference-semantics decision.
