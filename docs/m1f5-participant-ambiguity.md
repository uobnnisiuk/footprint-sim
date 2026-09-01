# M1-F5 `later-trace-a` participant-identity ambiguity audit

## Decision summary

This audit starts from M1-F4 final commit
`03e3c3a2ef7d46614d84385f4b516a1d6fa4258e`. It does not add a participant-dependent inference
rule, change the S5 scenario, or run a 200-seed experiment.

The primary decision is **Outcome B**: the exact identity of the other participant is present in
the `later-trace-a` observation and both derived constraints, but the current S5 transformation and
candidate reconstruction do not consult that identity when determining the historical allowed
Regions, historical time window, candidate count, containment, reduction, or work. The identity
does affect stable identifiers and audit provenance. That addressing effect is not a
participant-dependent constraint result.

There is also a supporting **Outcome C contract limitation**: the production participant model can
represent only exact `(EntityId, EntityKind, Role)` participants. It has no representation for a
candidate participant set, an identity-unknown participant, an unknown entity kind, or alternatives
derived from one source observation. Consequently, the requested narrow, broad, kind-only, and
unknown conditions cannot be constructed faithfully without inventing new semantics.

The result is therefore:

> Current S5 cannot identify the contribution of other-participant identity. It shows that the
> current transformation does not express identity-dependent constraint differences; it does not
> show that participant identity is unnecessary in general.

Per the preregistered stop rule, no ambiguity evaluator, synthetic people, new derivation rule, or
200-seed output was created.

## Inspected implementation

The audit directly inspected:

- `scenarios/s5_distributed_past_evidence/scenario.json`;
- `docs/m1f1-later-trace-field-ablation.md`, `docs/m1f2-location-resolution.md`,
  `docs/m1f3-time-resolution.md`, and `docs/m1f4-time-transformation-audit.md`;
- `Interactions/InteractionPrimitives.cs`, `Interactions/InteractionObservation.cs`, and
  `Interactions/Truth/InteractionOccurrence.cs`;
- `Interactions/CrossThreadConstraint.cs` and
  `Interactions/Tracing/CausalTracingContracts.cs`;
- `Interactions/Tracing/OperationalCausalTracer.cs` and
  `Interactions/CandidateWarps/CandidateWarpContracts.cs`;
- `Inference/PastStates/PastStateReconstructionContracts.cs` and
  `Inference/PastStates/PastStateReconstructor.cs`;
- `Scenarios/ScenarioDefinition.cs`, `Scenarios/ScenarioValidator.cs`, and
  `Simulation/SimulationEngine.cs`; and
- the F1, F2, and F3 evaluators and tests where they establish the existing source inventory and
  projection boundaries.

## Current `later-trace-a` participant semantics

| Property | Current S5 value | Meaning in the current implementation |
| --- | --- | --- |
| target/query entity | `person-a` | exact target of the historical query and both constraints |
| target participant | `person-a` | exact `Person`, role `EncounterParticipant` |
| other participant | `person-b` | exact `Person`, role `EncounterParticipant` |
| observed relation | `PersonPersonEncounter` | enum on the synthetic interaction observation |
| derived relations | `later-public-trace`; `later-public-trace-derived` | free-text constraint labels, not executable relation rules |
| constraint role | `Downstream` | tracing role on `generation-a` and `generation-a-redundant`; not a participant role |
| observed time | closed `[24,24]` | copied to the observation and constraint source lineage |
| historical window | closed `[13,14]` | separately authored on both scenario constraint definitions |
| observed/allowed Regions | `{fork-east, fork-west}` | authored on the definitions, unioned into the observation, then copied to each constraint |
| availability | tick 25 | query-usable before cutoff 28 |
| evidence boundary | `Confirmed`, person evidence, not device-only | consumed by candidate generation |
| source | `source-a` / `record-family-a` | one direct source and one shared ancestry family |

`person-c` and `device-d` also exist in the S5 world. Their existence does not make
`{person-b, person-c}` or `{person-b, device-d}` a public ambiguity set. Substituting either exact
entity for `person-b` would assert a different exact record, not represent uncertainty about the
same record.

## Participant flow and the missing semantic edge

The current runtime construction has two parallel inputs:

```text
scenario OtherEntityId = person-b
        |
        v
exact InteractionParticipant(Person, EncounterParticipant, person-b)
        |
        +--> InteractionObservation.ObservedParticipants
        |          |
        |          +--> observation stable ID and provenance reference
        |
        +--> CausalTracingConstraint.Participants
                   |
                   +--> constraint stable ID / candidate provenance

scenario AllowedRegionIds = {fork-east, fork-west} --------+
scenario ConstrainedTimeWindow = [13,14] ------------------+--> candidate generation
EvidenceState / person-device flags -----------------------+

other participant identity --------------------------------X  no semantic edge
                                                           to Regions or window
```

`SimulationEngine.CreatePastStateProjection` constructs one synthetic observation per
cross-thread `SourceObservationKey`. It takes the other ID from `OtherEntityId`, but takes the
observation Regions independently from the union of the source group's declared
`AllowedRegionIds`. It then constructs each `CausalTracingConstraint` by independently copying the
definition's `ConstrainedStartTick`, `ConstrainedEndTick`, and `AllowedRegionIds`. No lookup of
`person-b`, its trajectory, home, relation history, or truth state occurs.

The general `CrossThreadConstraintDeriver.DerivePersonEncounterCoLocation` has the same relevant
boundary. It requires that the **target** participant with the requested target ID exists and is a
`Person`; it also requires `PersonPersonEncounter`, person evidence, non-device-only evidence, and
at least one observed Region. It copies all participants into the result for audit, but it does not
inspect the other participant's ID to choose the Regions or time. Its only production rule is a
same-time co-location copy, not S5's later-to-historical transformation.

## Where participant identity is used

### Target identity

The target identity is semantically active and is outside this experiment's manipulation axis:

- the S5 query and both generation constraints target `person-a`;
- constraint selection requires `TargetEntityId == query.TargetEntityId`; and
- the general encounter derivation locates the exact target participant and requires it to be a
  `Person`.

The audit does not weaken, replace, or infer `person-a`.

### Other-participant identity

The exact `person-b` identity is used to:

- satisfy the exact `InteractionParticipant` record shape;
- pass scenario validation as an existing entity and keep grouped source descriptions consistent;
- form the `InteractionObservation` stable identifier;
- populate observation and constraint participant audit fields;
- form each `CausalTracingConstraint` stable identifier;
- flow into candidate-warp provenance; and
- indirectly affect source-group, candidate, reconstruction, and provenance identifiers that are
  hashes of source observation or constraint identifiers.

It is not used to:

- derive or select `{fork-east, fork-west}`;
- derive or select `[13,14]`;
- classify `EvidenceState`;
- classify the constraint as person evidence or device-only evidence;
- select a generation, verification, or traversal usage role;
- intersect candidate Regions;
- determine a candidate disposition;
- verify a candidate;
- traverse a public transition;
- calculate containment, false exclusion, reduction, or work; or
- access the truth trajectory or truth participant.

Stable identifiers changing with an exact participant are a required audit/addressing consequence.
They do not demonstrate that candidate Region semantics differ. A future ambiguity contract must
define stable addressing without retaining the hidden exact identity.

## Candidate generation and work

`PastStateReconstructor` selects generation constraints by direct source and overlap between
`ConstrainedTimeWindow` and the fixed query window. It groups them by source ancestry and then uses
only:

- `IsPersonEvidence` and `IsDeviceOnlyEvidence`;
- `EvidenceBoundaryState`; and
- `AllowedRegionIds`.

There is no read of `CausalTracingConstraint.Participants` in candidate construction or candidate
verification. `OperationalCausalTracer` likewise filters by scenario, seed, target, visibility,
availability, and constrained-window overlap, then applies evidence flags, evidence state, and
allowed Regions. Participants are copied only when it builds candidate provenance.

Therefore the current implementation contains no participant-candidate enumeration whose size
could affect generation, verification, forward, backward, evidence-inspected, or
relations-inspected work. Creating multiple exact constraints merely to manufacture such work would
be a new algorithm and would wrongly turn one uncertain source into several records.

Candidate IDs would mechanically change if an exact participant ID changed, because they include
generation evidence, source-group, and constraint IDs. Candidate **Region membership and count**
would not change through the inspected path. This distinction is why the outcome is B rather than
an identity-sensitive experiment.

## Source grouping, ancestry, and independence

The two `later-trace-a` constraints share:

- direct source `source-a`;
- declared ancestry `record-family-a`; and
- the same runtime source observation ID.

`OperationalCausalTracer.BuildSourceGroups` groups constraints from the union of
`SourceAncestryIds`, `SourceObservationId`, and `DirectSourceId`. It never groups by participant.
The source observation ID contains the exact participant indirectly, so a projected exact identity
would change the group's stable address, but not the fact that the two constraints form one shared
source group.

If a future contract expands one observation into several possible participants, those alternatives
must retain the same source ancestry and must not increase independent evidence count. This audit
does not implement expansion by cloning observations or constraints.

## Person/device boundary

The current exact S5 record is internally intended as person evidence:

- both participants are constructed as `InteractionEntityKind.Person`;
- `IsPersonEvidence` is true; and
- `IsDeviceOnlyEvidence` is false.

Candidate generation enforces the operational boundary through the two evidence-classification
flags, not by reading participant kinds. The general encounter derivation additionally checks only
the target participant's kind. The public observation and tracing-constraint factories do not
cross-validate every participant kind against `PersonPersonEncounter` or against the evidence
flags. The truth occurrence projector is stricter: it requires exactly two known persons.

The S5 projection also infers the other participant kind from `SourceKind`: `DeviceOnlyObservation`
becomes `Device`; all other source kinds become `Person`. `ScenarioValidator` checks that
`OtherEntityId` exists, but it does not verify that this inferred kind matches the entity registry.
These are representation-validation gaps, not permission to treat an unknown participant as a
person.

An ambiguity level whose kind is unknown cannot be represented: `InteractionEntityKind` has only
`Person` and `Device`, and `InteractionObservation.Create` requires evidence to be classified as
exactly one of person evidence or device-only evidence. Using a dummy person, `person-a`, or
`person-b` to satisfy the type would leak or invent identity. Mixing `device-d` into a person set
would violate the intended `device evidence != person evidence` boundary.

## Ambiguity contract audit

| Requested information state | Current contract support | Reason |
| --- | --- | --- |
| exact other participant | yes | one required exact `EntityId`, enum `EntityKind`, and enum `Role` |
| small participant candidate set | no | no alternative/candidate-set field or disjunctive participant relation |
| broad participant candidate set | no | same limitation; multiple records would falsely multiply one source |
| entity kind only | no | a nonempty exact `EntityId` is mandatory |
| unknown participant identity, encounter known | no | no existential `someone` participant or unknown-ID state |
| unknown entity kind | no | no unknown/union entity kind and evidence classification is binary |

`CausalTracingConstraintDefinition.OtherEntityId` is nullable, but null is not an ambiguity
contract. `SimulationEngine` replaces null with `scenario.TargetPersonId`; the two identical
participant tuples then normalize to one target participant. That one-party record is accepted by
the public observation factory even though the relation is named `PersonPersonEncounter`.
Omitting the field therefore cannot faithfully mean “someone was encountered, identity unknown.”

Similarly, removing `later-trace-a` is not an unknown-participant level. It means the source is not
available to the method. Keeping only `person-a` would no longer encode the reported two-party
relation. Neither operation is a valid substitute for existential uncertainty.

## Unknown participant is not no encounter

The propositions remain distinct:

```text
someone was encountered; their identity is unknown
no encounter occurred
the encounter record is unavailable to the query
```

The current contract can express only an exact participant record or the absence/unavailability of
that record from method input. It cannot express the first proposition. This audit therefore does
not map `NotKnown` or `NotObserved` to `Absent`, does not remove the observation, and does not infer
no encounter.

## Outcome and experiment decision

The decision tree resolves as follows:

1. **A is false for semantic constraint and candidate results.** Other-participant identity is not
   consulted to produce historical Regions or windows.
2. **B is the primary current-S5 result.** Exact identity is recorded and address-significant, but
   not constraint-semantic on this path.
3. **C is a supporting representation result.** Even if a future rule used identity, the present
   contract cannot state the requested ambiguity profiles.

No core levels are preregistered as runnable conditions. No 200-seed paired experiment is run, so
there are no participant-condition candidate counts, containment values, false exclusions,
reductions, work deltas, or measured plateau. Reporting the prior F1 “subject missing” result as an
identity-ambiguity result would be incorrect: F1 disabled the entire atomic four-field source
transformation, including identity and entity kind, instead of preserving an encounter with an
ambiguous other participant.

The only current boundary is a **representation boundary**:

```text
exact participant tuple -> representable
participant alternatives / kind-only / unknown -> not representable
```

It is not analogous to F2's measured Region-resolution threshold.

## Answers to the research questions

1. **Is exact other identity required for candidate reduction?** The current candidate algorithm
   does not read it, so it is not a computational input to the observed Region reduction. The audit
   cannot establish real-world redundancy because ambiguity cannot be represented.
2. **Can several possible persons derive the same constraint?** The present transformation would
   ignore which exact other ID was carried, but the contract cannot express several possibilities
   as one uncertain observation. No semantic claim about conservative union is implemented.
3. **Does knowing only that the other entity is a person have value?** The operational person/device
   flags have value, but a kind-only participant record is not representable. This fixture cannot
   isolate identity from kind, so kind-only sufficiency is unproven.
4. **What is lost when the participant is unknown?** The current contract loses the ability to
   construct a faithful observation at all; it cannot preserve the existential encounter while
   hiding the exact ID. Source addressing and participant audit lineage also need a new design.
5. **Does identity ambiguity affect only work?** No such conclusion is available. The current code
   has no ambiguity enumeration, so neither result nor work effects are measured.
6. **Can current S5 express identity-dependent differences?** No. Regions and historical windows
   are scenario declarations independent of `OtherEntityId`.
7. **Is there an F2-like discrimination boundary?** Not in current S5. The observable boundary is
   exact-only contract support versus unrepresentable ambiguity, not a performance threshold.

## Fixed variables and truth boundary

Because no profiles were constructed, nothing in the world or inference path was changed. The
audited baseline retains:

- location at F2 current `{fork-east, fork-west}`;
- observed time `[24,24]` and historical fixture window `[13,14]`;
- `PersonPersonEncounter`, availability tick 25, `Confirmed`, and the source ancestry;
- the same truth world, trajectories, hazard realization, topology, Region/tick representation,
  query, inference, verification, traversal, local neighborhood, and scoring; and
- the existing person/device separation.

No hidden truth occurrence or truth participant lookup is available on the `later-trace-a` path.
The synthetic observation has no referenced truth interaction. The audited construction reads
`person-b` from the public scenario definition, not from realized truth. Because no ambiguity
projection was made, no derived object can accidentally retain a hidden profile truth identity.

## Claims supported and not supported

The audit supports these claims:

- `later-trace-a` currently reports exact `person-a`–`person-b` person participants;
- target identity matters for query/constraint targeting;
- other-participant identity changes identifiers and provenance;
- other-participant identity does not determine the current historical Region/window payload;
- candidate generation and verification do not inspect constraint participants;
- source independence is based on source lineage, not participant count; and
- current contracts cannot represent the requested participant ambiguity states.

The audit does **not** support these claims:

- participant identity is generally unnecessary;
- `person-b` and `person-c` have equal evidential meaning;
- person kind alone is sufficient;
- an unknown participant should produce the same candidates or work as an exact participant;
- ambiguity affects only work;
- no encounter occurred when identity is unknown; or
- a five-level identity threshold or plateau exists.

## Follow-on work

### Relation specificity

Keep `who` and `what relation` separate. The relation enum is a separate field, but its participant
arity/kind requirements are not fully enforced on public observations and its S5 derived relation
strings are not executable semantics. A relation-specificity step should formalize relation
preconditions without using relation changes as a proxy for participant ambiguity.

### Time-specific fixture

M1-F4 remains unchanged: `later-trace-a` is unsuitable for a time-resolution experiment because
`[24,24] -> [13,14]` is not a represented temporal derivation. A dedicated temporal dependency and
fixture are still required. No time code or scenario window is changed here.

### Artificial-world representation validation

Before a participant experiment, design and validate a public uncertainty contract that:

- represents exact IDs, finite alternative IDs, kind-only existence, unknown identity, and unknown
  kind without dummy entities;
- represents “someone was encountered” independently from record absence;
- keeps target identity exact;
- carries possible derivations conservatively without consulting a truth participant;
- preserves one observation/source ancestry across alternatives;
- keeps person and device alternatives typed and non-interchangeable;
- gives stable identifiers to uncertainty statements rather than hidden exact values;
- cross-validates participant kinds, relation arity, entity registry membership, source kind, and
  person/device evidence flags; and
- defines an identity-sensitive transformation or fixture before performance levels are chosen.

Only then can a paired experiment determine whether alternatives yield the same constraint, a union
of legitimate constraints, no usable constraint, or additional work.

### Minimum observation set

F1 established only that the current atomic source transformation is disabled when its combined
subject field is unavailable. It did not isolate other-participant identity from entity kind or
from source eligibility. Revisit the minimum observation set after the ambiguity contract and an
identity-sensitive fixture exist; do not treat F1 source removal as an unknown-participant profile.

## Validation scope

This branch changes documentation only. No production code, scenario, experiment definition,
truth path, inference rule, or generated result changes. In accordance with the B/C stop rule, the
full build/test/scenario suite and 200-seed determinism reruns are not required. Validation is
limited to repository cleanliness at the baseline, focused static code tracing, review of the
documentation diff, and whitespace validation with `git diff --check`.
