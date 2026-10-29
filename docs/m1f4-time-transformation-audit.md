# M1-F4 observed-time to historical-constraint-time audit

## Decision summary

This audit starts from M1-F3 final commit
`06c8c529a10a5f7f9b91a84dc404e8c377384620`. It does not add an inference rule, adjust a window,
or rerun the 200-seed experiment.

The primary result is **Outcome C for the current evidence**: the S5 semantics do not justify
deriving the `later-trace-a` historical constraint `[13,14]` from its observed window `[24,24]`.
The historical window is a scenario-authored fixture declaration. The observed window is copied
into constraint lineage, but it is not an input to the historical window or allowed-Region
calculation.

The supporting design result is **Outcome B for a future time experiment**: a meaningful mapping
could be defined only after adding an explicit temporal dependency such as a particular public
transition/path with bounded duration, or an explicit relation-to-target or observation-delay
contract. No value for such a contract is inferable from `later-trace-a`, and none is added here.

The recommendation is mixed:

- **Recommend B for the time track**: create a time-specific synthetic fixture whose temporal
  dependency and duration bounds are declared before choosing time-resolution levels.
- **Recommend C for immediate sequencing**: keep the current time experiment on hold and proceed
  to participant ambiguity if that independent axis is ready. Do not rerun M1-F3 as a finite-time
  precision experiment until the new fixture exists.

## Inspected implementation

The audit directly inspected:

- `docs/m1f3-time-resolution.md`, `docs/m1f2-location-resolution.md`,
  `docs/m1d-causal-tracing.md`, `docs/m1e-past-state-reconstruction.md`, and
  `docs/m1e2-world-representation-validity.md`;
- `scenarios/s5_distributed_past_evidence/scenario.json` and
  `experiments/m1f3_time_resolution/experiment.json`;
- `Evidence/ObservationContracts.cs`, `Interactions/InteractionPrimitives.cs`,
  `Interactions/InteractionObservation.cs`, and `Interactions/Truth/InteractionOccurrence.cs`;
- `Interactions/CrossThreadConstraint.cs`,
  `Interactions/Tracing/CausalTracingContracts.cs`, and
  `Interactions/Tracing/OperationalCausalTracer.cs`;
- `Interactions/CandidateWarps/CandidateWarpContracts.cs` and
  `Interactions/CandidateWarps/CrossThreadConstraintApplicator.cs`;
- `Inference/PastStates/PastStateReconstructionContracts.cs` and
  `Inference/PastStates/PastStateReconstructor.cs`;
- `Scenarios/ScenarioDefinition.cs`, `Scenarios/ScenarioValidator.cs`, and
  `Simulation/SimulationEngine.cs`;
- `Evaluation/TimeResolutionContracts.cs`, `Evaluation/TimeResolutionEvaluation.cs`, and the
  relevant F2 location projection; and
- `M1F3Tests.cs`, with the M1-D and M1-E temporal and transition tests used as boundary checks.

Git history was also inspected. The two `[13,14]` declarations for `generation-a` and
`generation-a-redundant` were introduced with the original M1-E S5 fixture in commit `1ee96518`.
They were not introduced by M1-F3.

## M1-F3 result retained as a characterization

The existing 200-seed result is reused:

| Time condition | Mean reconstruction candidates | Containment | False exclusion | Reduction |
| --- | ---: | ---: | ---: | ---: |
| `[24,24]` | 2.165 | 100% | 0/200 | 27.83% |
| `[23,24]` | 2.165 | 100% | 0/200 | 27.83% |
| `[18,24]` | 2.165 | 100% | 0/200 | 27.83% |
| `[13,24]` | 2.165 | 100% | 0/200 | 27.83% |
| `[8,24]` | 2.165 | 100% | 0/200 | 27.83% |
| unknown | 3.000 | 100% | 0/200 | 0% |

The finite-window plateau characterizes a fixed scenario declaration. It does not establish that
time precision is unnecessary. Unknown time crosses a different boundary: the F3 evaluator does
not emit the time-bearing interaction observation and therefore generates neither same-source
constraint.

## Temporal concepts and current contracts

The current implementation contains several different time concepts. They must not be treated as
one interchangeable `time` value.

| Concept | Current representation and S5 meaning | Operational use | Limitation |
| --- | --- | --- | --- |
| world occurrence time | `InteractionOccurrence.OccurrenceTimeWindow`, projected from a truth `Encounter` event; the current projector produces an exact tick | private truth and evaluation only | S5 `later-trace-a` has no truth occurrence and no occurrence reference |
| ordinary record observed time | exact `ObservationRecord.ObservedTick`; the generator sets it to the source world-event tick | ordering and ordinary evidence inference | cannot represent an uncertain tick, an observation-act window, or a distinction between event time and capture time |
| interaction observed time | required `InteractionObservation.ObservedTimeWindow`; `[t,t]` is exact, a wider window is merely a closed interval in the base contract | source identity, ordering, availability validation, lineage, and F3 projection | the base contract does not say whether the interval is persistence, one unknown occurrence tick, epistemic uncertainty about an exact occurrence, or the time of the observation act |
| relation time | truth occurrences have `OccurrenceTimeWindow`; public interaction observations have `ObservedTimeWindow`; tracing constraints retain it as `SourceObservedTimeWindow` | relation/source audit only in S5 reconstruction | S5's free-text `RelationKind`, `Role`, and `DerivationKind` define no temporal dependency or offset |
| state interval | `CandidateWarpTemporalKind.StateInterval` plus a closed `InteractionTimeWindow`; S5 declares `[8,10]`, `[13,14]`, and `[18,32]` | a Region set is available at every contained tick; transition consistency compares declared layers | no dwell-time or mandatory-departure contract explains how long an entity may remain in one Region |
| target historical window | `PastStateQuery.TargetTimeWindow`; S5 declares `[13,14]` | selects the public candidate layer and defines the requested historical scope | chosen independently of all observation times |
| constraint applicability window | `CausalTracingConstraint.ConstrainedTimeWindow`; S5 declares `[13,14]` for both `later-trace-a` generation definitions | a constraint is selected when this window inclusively overlaps the query or a candidate state | no derivation from `SourceObservedTimeWindow` is required or represented |
| availability time | `ObservationRecord.AvailableTick` or evidence-side `AvailableAtTick`; `later-trace-a` uses 25 | evidence is usable only once this point is reached | availability is not occurrence time, observed-relation time, or a historical target |
| query cutoff | `PastStateQuery.EvidenceAvailabilityCutoff`; S5 uses 28 | public evidence with availability `<= 28` is selected | does not infer any missing observed or occurrence time |

All intervals and overlap tests on this path are closed and inclusive:

```text
overlap([a,b], [c,d]) iff a <= d and c <= b
```

An equal endpoint therefore overlaps. This boundary behavior is consistent across the reconstructor,
tracer, applicator, and F3 diagnostics.

### Occurrence uncertainty, observation uncertainty, and observation-act time

M1-F3 adds an experiment-level enum value, `OccursAtOnePossibleTick`, and enumerates each integer
tick in a finite window. This states that one relation occurrence tick is possible; it avoids
treating the whole window as persistence. That meaning is not carried by
`InteractionObservation` itself.

The public contracts cannot distinguish these propositions:

```text
the relation occurrence itself is modeled as lying somewhere in [a,b]
the relation occurred at one exact truth tick, but the observer knows only [a,b]
the observation act occurred sometime in [a,b]
the relation or state persisted throughout [a,b]
```

`ObservationRecord` represents only one exact `ObservedTick`. `InteractionTimeWindow` contains only
start and end. `InteractionOccurrence` could retain a private truth window in another scenario, but
S5 deliberately has observations without truth occurrences, and the inference boundary cannot use
the truth object to resolve this ambiguity. `AvailableAtTick` does not fill this gap.

## Current S5 time flow

There are two different source paths. Ordinary recorded evidence follows a world event:

```text
world event at tick e
        |
        +-- ObservationRecord.ObservedTick = e
        |
        +-- declared/random reporting delay d
                |
                +-- ObservationRecord.AvailableTick = e + d
```

`later-trace-a` does not follow that path. It is a condition-specific synthetic
`InteractionObservation` with `ReferencedInteractionId = null`, and S5 has no corresponding
`InteractionOccurrence`:

```text
scenario observed window [24,24]
        |
        v
synthetic InteractionObservation.ObservedTimeWindow [24,24]
        |
        +-------------------------------> constraint.SourceObservedTimeWindow
        |                                  (lineage / identity / audit)
        |
        X  no temporal transformation
        |
scenario constrained window [13,14] --------------------+
scenario allowed Regions {fork-west,fork-east} ---------+---+
scenario relation/derivation strings -------------------+   |
                                                            v
                                             CausalTracingConstraint
                                             .ConstrainedTimeWindow [13,14]
                                             .AllowedRegionIds {west,east}
                                                            |
AvailableAtTick 25 -- usable by cutoff 28 -----------------+
                                                            v
query target window [13,14] -- inclusive overlap --> candidate generation
                                                            |
                                                            v
                                        historical candidate Region narrowing
```

The two `?` transformations in the proposed conceptual flow do not exist:

```text
world occurrence time
        X  no S5 occurrence for later-trace-a
observed relation time
        X  no rule using observed tick
historical constraint time
```

Public transition consistency runs only after candidate-state layers and constraint windows have
already been supplied. It does not fill either missing edge.

## Exact `[13,14]` provenance

`[13,14]` is declared independently in three places in S5:

- the query target is `targetStartTick: 13`, `targetEndTick: 14`;
- `generation-a` declares `constrainedStartTick: 13`, `constrainedEndTick: 14`; and
- `generation-a-redundant` repeats the same constrained window.

The runtime flow is direct:

1. `ScenarioLoader` deserializes the literals into `CausalTracingConstraintDefinition`.
2. `SimulationEngine.CreatePastStateProjection` constructs the synthetic observation from
   `ObservedStartTick` / `ObservedEndTick`.
3. The same method separately constructs `ConstrainedTimeWindow` from
   `ConstrainedStartTick` / `ConstrainedEndTick`.
4. `TimeResolutionEvaluation.ProjectSource` repeats that separation for every F3 finite level.
5. `PastStateReconstructor` considers the constraint because its **constrained** window overlaps
   the query target.

No code subtracts 10 or 11 ticks, follows a path from tick 24, reads a transition duration, or
consults a relation rule to produce `[13,14]`. The strings `later-trace-to-target-window` and
`same-record-secondary-derivation` are identifiers and provenance labels, not executable rules.

`ScenarioValidator` verifies nonnegative, ordered windows and checks
`AvailableAtTick >= ObservedEndTick`. It does not require an ordering, duration, dependency, or
derivation relation between the observed and constrained windows. Git blame confirms that the
literals were authored as part of the original M1-E fixture in `1ee96518`.

Therefore `[13,14]` is **not** derived from observed time, public transition timing, or formal
relation semantics. It is a scenario-authored fixture declaration.

## Where the observed tick is and is not used

For finite F3 profiles, the observed tick/window is used to:

- create the synthetic `InteractionObservation` and its stable identifier;
- enforce that evidence availability is not before the observed-window end;
- order interaction observations;
- populate `CausalTracingConstraint.SourceObservedTimeWindow` and constraint identity;
- preserve source-time provenance in candidate and telemetry audits;
- enumerate F3 possible ticks and report possible-derivation diagnostics; and
- decide the F3 unknown-time boundary, where no time-bearing observation or constraint is emitted.

It is not used to:

- calculate `ConstrainedTimeWindow`;
- calculate `AllowedRegionIds`;
- choose the query target window;
- select the public candidate-state interval;
- determine whether a generation or verification constraint temporally applies;
- compute transition reachability in `OperationalCausalTracer`;
- calculate availability or the query cutoff; or
- score historical truth.

The reconstructor and tracer use `ConstrainedTimeWindow`, not `SourceObservedTimeWindow`, for all
historical overlap checks. Public transition traversal uses candidate-state windows. The source
observed window is preserved as evidence lineage but never becomes a transition anchor in S5.

## Primitive temporal relations

The current `later-trace-a` story can be decomposed without introducing a new research term:

| Primitive statement | Status in current S5 | Evidence |
| --- | --- | --- |
| a person-person encounter relation is reported for person-a and person-b | explicit | runtime `InteractionObservation.ObservedRelationKind = PersonPersonEncounter` |
| the reported relation has observed window `[24,24]` | explicit | scenario source window copied to the interaction observation |
| the reported participants are co-located in one of the observed Regions at that relation time | available in the general encounter derivation semantics | `CrossThreadConstraintDeriver.DerivePersonEncounterCoLocation` copies observation time and Regions to a same-time constraint |
| the evidence becomes query-usable at tick 25 | explicit | `AvailableAtTick = 25` |
| evidence use happens after the observed-window end | explicit structural invariant | creation and validation require availability at or after the end |
| the target query asks about person-a during `[13,14]` | explicit and independent | `PastStateQuery.TargetTimeWindow` |
| person-a was in west or east during `[13,14]` | explicit fixture claim | `ConstrainedTimeWindow` and `AllowedRegionIds` literals |
| the tick-24 relation is a consequence of the tick-13–14 state | undefined | no dependency or path reference |
| the relation occurred after the target state in a semantic rather than merely numeric sense | undefined | `Downstream` and `later-*` are labels; no ordering rule validates them |
| a particular transition connects the target state to the relation | undefined | no transition/path identifier on the observation or constraint |
| the elapsed time from target state to relation is bounded | undefined | no relation-to-target duration contract |
| the observation act followed the relation occurrence by a bounded delay | undefined | only availability is represented; observation-act time is absent |
| a broad observed window is occurrence uncertainty, epistemic uncertainty, or duration | defined only by the F3 experiment wrapper | not represented in `InteractionObservation` |

The only existing production derivation for a person-person encounter is same-time co-location:

```text
observed encounter window O and observed Regions R
        -> constrained co-location window O and allowed Regions R
```

It does not transform `[24,24]` into `[13,14]`.

## Public transition timing and derivability

### What the tracer can derive

For a supplied source state window `S = [s1,s2]` and a particular public path with total travel
duration `D = [dmin,dmax]`, M1-D checks whether the reachable arrival range

```text
[s1 + dmin, s2 + dmax]
```

overlaps a separately supplied destination state window. Reverse tracing calls the same reachability
test in the opposite state-layer direction. It filters candidate Regions for consistency; it does
not emit a newly derived historical window.

If a future contract explicitly states that an observed occurrence in
`O = [o1,o2]` is the arrival/consequence of a named predecessor transition or path with duration
`[dmin,dmax]`, the conservative predecessor window would be:

```text
P = [max(0, o1 - dmax), o2 - dmin]
```

For multiple eligible paths, the result must retain the union of path-specific possibilities rather
than intersecting them. This formula is valid only when the predecessor/consequence relationship and
duration semantics are explicit. It is not a rule that can be attached to an arbitrary later
observation.

Similarly, if `O` were the time of an observation act rather than the observed occurrence, an
occurrence window could be inverted from a declared observation-delay bound `[lmin,lmax]` as
`[max(0, o1-lmax), o2-lmin]`. S5 has no observation-act timestamp or such delay contract for
`later-trace-a`. `AvailableAtTick - observed tick` must not be substituted for it.

### Why S5 cannot derive `[13,14]`

S5 publishes these path durations:

- release to fork: 2–4 ticks;
- fork to corresponding terminal: 3–6 ticks.

`later-trace-a` reports fork-east or fork-west at tick 24. If that point were explicitly declared
as an arrival on release-to-fork, the predecessor departure window would be `[20,22]`, not
`[13,14]`. If a tick-24 observation were instead at a terminal and explicitly linked to the
fork-to-terminal transition, the predecessor window would be `[18,21]`, again not `[13,14]`.
Neither hypothetical relation is what the current fixture declares.

The tracer also treats a Region as reachable from itself across ordered state windows without a
transition. That behavior represents the absence of a contrary movement requirement; it is not a
bounded dwell-time derivation. It cannot justify choosing `[13,14]` from tick 24.

It would be possible to design a different operation in which a later observed state remains at its
own occurrence window and backward reachability filters candidates in an independently specified
query window. Current S5 does not materialize `later-trace-a` as a later candidate-state layer, and
such filtering would still require explicit observation-to-state, path applicability, topology
completeness, and dwell/movement semantics. More importantly, that operation would not be an
observed-time-to-target-window transformation: the query target would remain independently chosen.

Consequently, **additional temporal semantics are required**. Existing public topology, relation,
and transition timing are insufficient for the current mapping.

## Outcome classification

### Outcome A: existing semantics are sufficient

**Rejected for `later-trace-a -> [13,14]`.** Existing semantics are sufficient only for the general
same-time encounter co-location rule and for consistency checks between already-declared state
layers. Neither operation derives the S5 historical target window from tick 24.

### Outcome B: a missing explicit contract is identifiable

**Applicable to future design.** Depending on the intended evidence, the minimum missing contract
would need to identify:

- whether the source window is relation occurrence time, uncertainty about an exact occurrence,
  persistence duration, or observation-act time;
- the target event/state endpoint to which the source is temporally dependent;
- the named relation, transition, or path that makes the dependency valid;
- bounded duration or delay semantics, including how paths compose and alternatives union;
- whether movement is mandatory, optional, or allows unbounded dwell in a Region; and
- validation rules that reject incompatible observed, constrained, availability, and query windows.

No numeric offset or duration can be supplied from the current evidence.

### Outcome C: the mapping is conceptually invalid for this evidence

**Primary current result.** `later-trace-a` is a synthetic reported encounter at fork Regions. Its
formal semantics establish co-location at the reported relation time, not a historical state at
`[13,14]`. The strings `later-public-trace` and `later-trace-to-target-window` do not add the missing
dependency. Treating `[13,14]` as derived from tick 24 would therefore overstate the current model.

`later-trace-a` was suitable for testing the behavior of an atomic scenario-declared evidence
transformation and for M1-F2's direct Region-set projection. It is not a valid fixture for measuring
finite observed-time resolution effects on a historical window.

## Relationship to M1-F2

M1-F2 had a direct transformation:

```text
projected InteractionObservation.ObservedRegionIds
        -> CausalTracingConstraint.AllowedRegionIds
```

The code performs that copy for every finite location profile, so widening the observed Region set
can directly admit more historical candidate Regions. This made the location precision effect
measurable under the existing declared historical window.

M1-F3 instead performs:

```text
projected InteractionObservation.ObservedTimeWindow
        -> CausalTracingConstraint.SourceObservedTimeWindow

scenario CausalTracingConstraintDefinition.ConstrainedTimeWindow
        -> CausalTracingConstraint.ConstrainedTimeWindow
```

There is no corresponding `ObservedTimeInformation -> HistoricalConstraintTime` operation. The F2
ablation form must not be copied to time until that semantic gap is resolved. M1-F2 also does not
validate the separate historical-window declaration; its result is specifically about the direct
Region-set projection under that fixed fixture.

## Representation limitations and artificial-world validity

The following items should be sent to artificial-world representation validation before a new time
experiment:

1. **Temporal representation**: add an explicit semantic kind for occurrence point, occurrence-time
   uncertainty, state duration, and observation-act time instead of relying on a bare interval.
2. **State intervals**: state whether a Region set holds throughout an interval, and define dwell,
   departure, and gaps between aggregate state layers.
3. **Transition duration**: define path-duration composition, alternative-path union, and whether a
   transition is optional or mandatory for the target entity.
4. **Event ordering**: distinguish numeric `earlier/later` from an explicit dependency between two
   events or states.
5. **Observation delay**: represent observation-act time and delay bounds only if they are part of
   the evidence model; keep them distinct from availability.
6. **Query-time semantics**: keep target historical scope and evidence cutoff independent. Decide
   whether an experiment varies an anchor occurrence window while querying a fixed past window, or
   genuinely derives a predecessor occurrence window.
7. **Relation semantics**: replace suggestive free-text derivation labels with an explicit validated
   contract when a transformation is intended.
8. **Public topology assumptions**: state completeness and persistence assumptions; same-Region
   reachability must not silently stand in for a calibrated dwell bound.

These limitations concern the artificial model's internal meaning. They do not authorize truth-side
timing, new probabilities, or real sensor physics.

## Proposed time-specific synthetic fixture

A future fixture can make time resolution identifiable without arbitrary offsets:

1. Declare a predecessor event/state `A` and a later observed event `B` as distinct primitives.
2. Declare that `B` is an arrival or consequence of `A` through a particular public transition or
   allowed path set.
3. Declare path travel duration bounds in the public model before evaluation.
4. Represent the evidence as uncertainty about `B`'s occurrence time, not as an ambiguous generic
   interval and not as `AvailableAtTick`.
5. Derive each possible `A` window by the conservative inverse-duration formula above and retain the
   union across possible `B` ticks and eligible paths.
6. Keep evidence availability and the query cutoff fixed and independent.
7. Construct candidate-state boundaries so widening `B`'s window can naturally cross reachable
   predecessor-state boundaries; preregister them before looking at results.
8. Validate both safe containment and whether the representation distinguishes the levels.

The fixture should test a public temporal contract, not reproduce the S5 `[13,14]` numbers. It need
not be implemented in this audit.

## Buddhist methodology comparison boundary

Only already-documented comparison candidates are retained, after primitive decomposition:

| Modern operation | Primitive relation | Possible future comparison | Boundary |
| --- | --- | --- | --- |
| forward interval propagation | an explicitly related predecessor can reach a consequence after a bounded duration | `anuloma` as an ordered-examination analogy | not a production name or claim of formal identity |
| backward interval inversion | an explicitly related consequence permits a set of possible predecessor times | `paṭiloma` as a reverse-examination analogy | reverse calculation does not establish the true predecessor |
| dependency and duration audit | one event/state conditions another under declared bounds | `paccaya` / Paṭṭhāna as a broad conditional-analysis comparison candidate | no one-to-one mapping and no twenty-four-condition engine |
| testing implication and converse separately | observation, occurrence, availability, and historical state do not imply their converses | Yamaka as an operational paired-questioning analogy | no terminology transfer to code or exhaustive doctrinal correspondence |

The sequence remains: modern operation, primitive temporal relation, then possible Buddhist
analogue. This audit introduces no Buddhist-derived production identifier and claims no complete
match.

## Implementation and validation decision

No implementation change is justified. In particular, this audit does not:

- add an offset from 24 to 13 or 14;
- use the truth path or truth occurrence;
- reinterpret `AvailableAtTick = 25` as an occurrence constraint;
- add a probability, physics rule, dwell rule, or sensor-delay model;
- change the scenario, validator, inference, F3 evaluator, or test expectations; or
- rerun the 200-seed evaluation.

Because only this audit document changes, the existing M1-F3 result is reused. Repository diff and
whitespace validation are sufficient for this change; build, test, format, determinism, and sample
evaluation outputs are unaffected.

## Claims after this audit

This audit can claim that:

- the F3 finite-window plateau follows from a tick-independent scenario declaration;
- `[13,14]` is a scenario-authored constraint and query fixture, not a derived time window;
- `AvailableAtTick` and the query cutoff remain separate and are used only for evidence access;
- current contracts preserve several time values but do not represent their required dependency;
- public transition durations support consistency checks and could support a future explicit
  inverse-duration rule; and
- `later-trace-a` is not a meaningful finite time-resolution fixture under current semantics.

It cannot claim that:

- time precision is unimportant;
- `[24,24]` causally implies `[13,14]`;
- the current public topology implicitly validates that implication;
- unknown observed time can be recovered from availability;
- a specific missing offset, delay, or duration value has been identified;
- a new time fixture will improve candidate performance; or
- any Buddhist term is an exact description of the implementation.
