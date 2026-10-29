# M1-F6 `later-trace-a` relation semantics audit

## Decision summary

This audit starts from M1-F5 final commit
`84bc8b416584f4b5df6bb824fc668b58954e6b6c`. It does not add a relation taxonomy, a
relation-dependent historical rule, a time rule, or a performance experiment.

The primary result is **Outcome B**: S5 records `PersonPersonEncounter` on the synthetic
`later-trace-a` observation and records `later-public-trace` or
`later-public-trace-derived` on its two constraints, but none of those relation values determines
the historical allowed Regions, constrained time window, or candidate narrowing. Those fields are
separately authored in the scenario and copied into runtime constraints. Relation values affect
stable identifiers, provenance, telemetry, and audit references.

The supporting result is **Outcome C**: the current repository has no usable relation specificity
axis. `InteractionRelationKind` contains one member, there is no category hierarchy or compatibility
relation, and observation/constraint contracts cannot distinguish “a relation exists but its kind
is unknown” from missing relation metadata or an unavailable observation. Conservative alternative
derivation is undefined.

The repository does contain one genuine relation-specific rule for the S3 path:
`PersonPersonEncounter -> same-time co-location`. S5 does not call that deriver, and the rule cannot
produce S5's unexplained `[24,24] -> [13,14]` historical mapping. It therefore does not make the
requested S5 experiment valid.

The main claim is:

> Current S5 cannot identify a relation-specificity contribution. Its relation labels describe and
> address already-declared constraints; they do not select the constraint payload.

Per the stop rule, no relation profiles, new relation kinds, synthetic taxonomy, or 200-seed results
were created.

## Inspected implementation

The audit directly inspected:

- `scenarios/s5_distributed_past_evidence/scenario.json`;
- `docs/m1f1-later-trace-field-ablation.md`, `docs/m1f2-location-resolution.md`,
  `docs/m1f3-time-resolution.md`, `docs/m1f4-time-transformation-audit.md`, and
  `docs/m1f5-participant-ambiguity.md`;
- `Interactions/InteractionPrimitives.cs`, `Interactions/InteractionObservation.cs`, and
  `Interactions/Truth/InteractionOccurrence.cs`;
- `Interactions/CrossThreadConstraint.cs` and
  `Interactions/CandidateWarps/CrossThreadConstraintApplicator.cs`;
- `Interactions/Tracing/CausalTracingContracts.cs` and
  `Interactions/Tracing/OperationalCausalTracer.cs`;
- `Inference/PastStates/PastStateReconstructionContracts.cs` and
  `Inference/PastStates/PastStateReconstructor.cs`;
- `Scenarios/ScenarioDefinition.cs`, `Scenarios/ScenarioValidator.cs`, and
  `Simulation/SimulationEngine.cs`;
- relation fields in neutral telemetry; and
- the interaction, candidate-warp, M1-D, M1-F1, M1-F2, and M1-F3 tests used to confirm the
  established boundaries.

## Current relation inventory

`later-trace-a` has three different relation-like names. They are not one semantic contract.

| Layer | Value | Type | Operational meaning in S5 |
| --- | --- | --- | --- |
| observation relation kind | `PersonPersonEncounter` | `InteractionRelationKind` enum | hardcoded exact observation label; included in observation ID |
| primary constraint relation label | `later-public-trace` | required free-text string | included in `generation-a` constraint ID and audit output |
| secondary constraint relation label | `later-public-trace-derived` | required free-text string | included in `generation-a-redundant` constraint ID and audit output |
| primary derivation label | `later-trace-to-target-window` | required free-text string | provenance/identity label; no executable rule dispatch |
| secondary derivation label | `same-record-secondary-derivation` | required free-text string | provenance/identity label; no executable rule dispatch |
| constraint tracing role | `Downstream` | `TracingConstraintRole` enum | retained in tracing audit; separately scenario-authored |
| evidence usage role | `CandidateGeneration` | membership in definition-ID list | makes both constraints generation inputs; independent of relation kind |

The two constraints are especially informative: they use different relation and derivation labels
while declaring the same source observation, direct source, ancestry, participants, evidence state,
historical window `[13,14]`, and allowed Regions `{fork-east, fork-west}`. S5 already demonstrates
that changing these labels does not require a different narrowing payload.

## Current participant, time, location, and lineage context

| Property | Current value |
| --- | --- |
| target/query participant | exact `person-a`, `Person`, `EncounterParticipant` |
| other participant | exact `person-b`, `Person`, `EncounterParticipant` |
| observed relation time | closed `[24,24]` |
| historical constraint time | closed `[13,14]` |
| location/allowed Regions | `{fork-east, fork-west}` |
| availability | tick 25, before query cutoff 28 |
| evidence state | `Confirmed`, person evidence, not device-only |
| source | `source-a` |
| source ancestry | `record-family-a` plus runtime observation/direct-source lineage |
| usage | candidate generation only, not verification or traversal |

The observation has no referenced truth `InteractionOccurrence`. Its relation kind is created from
a hardcoded S5 value, not projected from a world-side encounter event.

## Primitive decomposition of `PersonPersonEncounter`

The classifications below distinguish the concrete S5 value, the reusable public contract, the
truth-side occurrence projector, and actual use in S5 narrowing.

| Primitive | Classification | What is represented | S5 narrowing status |
| --- | --- | --- | --- |
| relation identity | **explicit** | exact enum value `PersonPersonEncounter` on the observation | **unused** as a narrowing input |
| participant count / arity | **explicit in the S5 instance; implicit in the public relation name** | S5 constructs two participants; public observation normalization requires only at least one | **unused**; no public relation-specific arity validation |
| participant roles | **explicit but merely descriptive** | both are `EncounterParticipant`; that is the only role enum value | **unused**; target lookup is by ID, not role |
| target entity kind | **explicit and conditionally semantic outside S5** | target is `Person`; the general co-location deriver checks it | S5 bypasses that deriver |
| other entity kind | **explicit in the instance; unresolved in public validation** | S5 constructs `person-b` as `Person`; the public deriver does not verify the other kind | **unused** by S5 narrowing |
| co-presence / co-location | **implicit in the name; explicit only in the separate general deriver** | the general rule copies observed Regions into a same-time constraint | S5 copies scenario Regions without invoking the rule |
| temporal concurrence | **explicit only in the general deriver** | general encounter constraint uses the same observed and constrained window | not used for S5's historical `[13,14]` window |
| temporal sequence | **unresolved** | no rule relates observation tick 24 to historical ticks 13–14 | no relation-derived sequence |
| participant directionality | **unresolved / absent** | identical participant roles and normalized participant ordering imply no directed endpoints | no effect |
| causal direction / ordering | **merely descriptive in S5** | constraint role `Downstream` and `later-*` strings are stored | no rule validates or computes downstream ordering |
| source-observation relation | **explicit label, unresolved mapping** | observation enum and constraint relation strings are both retained | no contract connects the enum to either string |
| state implication | **scenario-declared, not relation-derived** | historical Regions/window are literals | relation identity is unused |
| upstream/downstream applicability | **explicit as a separate tracing role** | both constraints are `Downstream`; usage is separately generation-only | role does not choose allowed Regions/window |
| symmetry | **implicit / unresolved** | encounter naming and equal roles suggest symmetry, but no symmetry contract exists | unused |
| existence | **coupled to exact identity** | creating an observation requires an exact enum member | cannot express existence with unknown kind |

### Truth-side semantics

`InteractionOccurrenceProjector.FromPersonPersonEncounter` is stricter than the public observation
contract. It requires a truth `Encounter` event, exactly two known persons, a Region, an exact event
tick, and assigns both participants `EncounterParticipant`. This makes the world-side occurrence an
explicit two-person relation.

That truth-side rule cannot justify S5 narrowing. `later-trace-a` has no occurrence reference, and
truth occurrence objects are deliberately excluded from observation, constraint, and inference
inputs.

### Observation-side semantics

`InteractionObservation.Create` requires one exact enum relation value, at least one exact
participant, a time window, and evidence classification. It does not validate
`PersonPersonEncounter` arity, require every participant to be a person, require the roles expected
by the relation, or require a Region. The enum currently has only one value.

Thus `PersonPersonEncounter` is an exact observation-side label with some intended meaning, but the
public record factory does not fully enforce that meaning.

### General derived-constraint semantics

`CrossThreadConstraintDeriver.DerivePersonEncounterCoLocation` is a real, narrow semantic rule:

```text
PersonPersonEncounter
+ target participant exists and is Person
+ person evidence and not device-only
+ at least one observed Region
    -> same observed time window
    -> same observed Regions
    -> PersonEncounterCoLocation constraint
```

The method checks the observed relation enum before deriving. It is explicitly called by the S3
projection. This is the repository's one relation-specific derivation dispatch, although it is a
named method rather than a taxonomy/registry dispatcher and there is no second relation kind for a
specificity comparison.

### S5 derived-constraint semantics

S5 follows a different path:

```text
hardcoded observation relation = PersonPersonEncounter
        |
        +---------------------------> observation ID / audit
        |
        X  CrossThreadConstraintDeriver is not called

scenario RelationKind string ----------------------------> constraint ID / audit
scenario DerivationKind string --------------------------> constraint ID / provenance
scenario AllowedRegionIds = {east,west} -----------------+
scenario ConstrainedTimeWindow = [13,14] ----------------+--> candidate narrowing
scenario EvidenceState / person-device flags ------------+
```

`SimulationEngine.CreatePastStateProjection` constructs `CausalTracingConstraint` directly. It
does not dispatch on `InteractionObservation.ObservedRelationKind`, `RelationKind`, or
`DerivationKind`. It copies historical Regions and windows from each definition.

## Relation use in constraint application and reconstruction

`CausalTracingConstraint.Create` requires a nonempty relation string and includes it in the stable
constraint ID. It does not interpret the string. Its `SemanticIdentity()` is the resulting ID, so a
label change creates a differently addressed constraint even when every narrowing field is equal.

`PastStateReconstructor` uses relation labels only to build public relation references and usage
audits. Candidate generation selects constraints by direct source and constrained-window overlap,
groups source ancestry, and applies:

- person/device evidence flags;
- `EvidenceBoundaryState`; and
- `AllowedRegionIds`.

Candidate verification follows the same pattern. Neither path branches on relation kind.

`OperationalCausalTracer` copies relation labels to step audits and lineage. Constraint application
uses target, visibility, availability, constrained-window overlap, evidence state, person/device
flags, and allowed Regions. It does not branch on relation. Its separate
`public-causal-transition` label describes a transition-consistency audit step; the label itself
does not select the public transition.

The general `CrossThreadConstraintApplicator` also receives an already-derived constraint and
applies its time window, evidence state, and allowed Regions. It preserves the source relation enum
in provenance and audits but does not redispatch on it.

## Stable IDs, provenance, and source independence

Relation values affect addressing and auditability:

- the observation enum is part of `InteractionObservation.ObservationId`;
- the free-text constraint relation is part of `CausalTracingConstraint.ConstraintId`;
- the derivation string is also part of the constraint ID and provenance;
- evidence-set, tracing-session, step, candidate, reconstruction, and candidate-warp IDs can change
  transitively because they include observation or constraint IDs; and
- telemetry and relation-reference lists retain relation values.

These effects are not historical candidate discrimination. Region membership, candidate count,
containment, reduction, and work counters are calculated from other fields.

Source grouping uses `SourceAncestryIds`, `SourceObservationId`, and `DirectSourceId`, not relation
identity. Because the observation ID contains its exact relation enum, an exact enum change could
change a group address. The two S5 relation-label variants still form one source group through the
same observation, `source-a`, and `record-family-a`.

A future set of possible relations from one observation must retain that one source group. Cloning
each alternative into independent evidence would be invalid and is not implemented here.

## Validator audit

Current validation is split and incomplete:

- the truth occurrence projector enforces `Encounter`, exactly two known persons, and a Region;
- the interaction enum type prevents an arbitrary observation string but has only
  `PersonPersonEncounter`;
- the public observation factory does not enforce relation-specific arity, roles, entity kinds, or
  location;
- S5 scenario validation only requires nonempty `RelationKind` and `DerivationKind` strings;
- S5 validation does not resolve either string through a rule registry or taxonomy;
- it does not require a relation-specific allowed-Region or time transformation;
- it does not compare the observation enum with the derived relation strings; and
- source-group consistency deliberately omits relation and derivation strings, permitting the two
  differently labeled constraints from the same observation.

There is no S5 validator rule equivalent to:

```text
PersonPersonEncounter + declared inputs
    -> allowed Regions R
    -> constrained time window W
```

## Separation from participant, time, and location

### Participant boundary

Relation and participant fields are syntactically separate, so S5 can store the same exact
`person-a` and `person-b` under its fixed relation value. Their semantics are not fully separable:
the only relation name contains participant kinds, while public validation does not enforce the
other participant's kind or arity. M1-F5's candidate-set and unknown-participant gaps remain.

This audit does not alter participant identity, kind, count, or roles.

### Time boundary

The general encounter rule means concurrence only: it copies the observation window to the
co-location constraint. It supplies no sequence, offset, duration, or historical dependency.
S5's `[24,24] -> [13,14]` mapping remains an independent scenario fixture as established by M1-F4.

No relation condition can legitimately choose a new historical window until a temporal dependency
contract exists. This audit adds none.

### Location boundary

The general encounter rule uses observed location by copying it; relation kind is an eligibility
condition, while the actual allowed Regions come from the observation. In S5 even that eligibility
check is bypassed: F2-current `{fork-east, fork-west}` is copied directly from scenario definitions.

Current S5 therefore cannot isolate relation value from location value. Its measured narrowing is
connected to location payload and scenario declarations, not to relation specificity. This audit
does not change location.

## Ambiguity and taxonomy contract audit

| Requested relation state | Current support | Limitation |
| --- | --- | --- |
| exact observation relation | partial | exact `PersonPersonEncounter` exists, but it is the only enum member |
| exact derived relation label | syntactic only | arbitrary nonempty string; no semantic registry |
| broader relation category | no | no taxonomy or parent category |
| compatible relation set | no | no set/union or compatibility relation |
| relation exists, kind unknown | no | exact enum is required; existence is not separate |
| unknown relation kind | no | no unknown enum state with epistemic semantics |
| relation unavailable | only as source unavailability | cannot retain relation existence while withholding kind |
| no relation occurred | world/event absence only | distinct from unknown and unavailable |

`CandidateWarpProvenance.RelationKind` is nullable, but null means that relation metadata is absent
from that provenance item. It does not carry “a relation exists, exact kind unknown.” The S5
constraint relation string is non-null and nonempty, but inventing the literal `unknown` would be a
new convention without derivation semantics.

There is no machine-readable relation taxonomy, hierarchy, abstraction function, compatibility
set, existential relation marker, or conservative alternative merge. Therefore no meaningful
exact/broad/set/unknown core conditions can be preregistered.

## Unknown relation is not no relation

The following propositions remain distinct:

```text
a relation exists and its exact kind is known
a relation exists but its kind is unknown
the relation observation is unavailable to this query
no relation occurred
```

The current interaction observation contract can represent the first. Source filtering can produce
the third. Truth world construction can represent the fourth by having no occurrence. It cannot
represent the second.

Removing `later-trace-a`, omitting all derived constraints, using null provenance, or inventing an
`unknown` string would not be a valid unknown-kind condition. No `NotKnown` or `NotObserved` state is
converted to `Absent` in this audit.

## Outcome and experiment decision

The decision tree resolves as follows:

1. **Outcome A does not hold for current S5.** The relation-specific S3 co-location rule is not used
   in the S5 path and cannot produce its historical window.
2. **Outcome B is the primary result.** S5 relation values are record, identity, provenance, and
   audit fields while narrowing payloads remain scenario-declared.
3. **Outcome C also holds.** Specificity levels and unknown relation existence cannot be expressed,
   and alternative-derivation merge semantics are absent.

A performance experiment is not justified. No core conditions, relation-candidate counts,
derivable-constraint counts, alternative-preservation results, derivation-failure profiles, or
200-seed safety/work metrics are produced.

The F1 “relation field missing” profile is not a substitute. It disables the complete atomic source
transformation and therefore compares source availability, not a known-existing relation with less
specific identity.

## Answers to the research questions

1. **Is exact relation kind necessary for candidate narrowing?** It is not read by current S5
   narrowing. This does not establish general redundancy because no alternative semantics exist.
2. **Can a broader relation category derive the same constraint?** There is no broader category or
   abstraction rule, so this is untestable.
3. **Can relation-kind unknown preserve a valid constraint?** The state and its conservative
   derivation semantics are not representable.
4. **Does current S5 express relation differences?** It expresses different labels and IDs, not
   relation-dependent Region/time results.
5. **Do relation labels have meaning?** They have addressing, provenance, telemetry, and human-audit
   meaning. The S5 free-text labels have no executable narrowing meaning.
6. **Can specificity be isolated from location/time/participants?** Not experimentally with current
   S5. Fields are syntactically separate, but the required semantic mappings and uncertainty
   contracts are missing.
7. **Is a relation taxonomy needed?** It is needed before testing broad/compatible/unknown levels,
   but a taxonomy alone is insufficient; each supported abstraction also needs derivation and
   conservative merge semantics.

## Artificial-world representation handoff

Future representation work should distinguish four layers:

| Layer | Required distinction |
| --- | --- |
| world-side fact | occurrence relation with enforced arity, roles, entity kinds, time, location, and visibility |
| observation-side assertion | possibly uncertain public claim about relation existence and kind, without truth leakage |
| derived evidence type | evidence statement produced from the observation, preserving source ancestry and alternatives |
| constraint derivation rule | executable mapping from supported primitives to allowed state/time payloads |

Before resuming the relation experiment, define and validate:

- relation existence independently from exact relation identity;
- a small repository-justified taxonomy or compatibility model, if one is genuinely needed;
- relation-specific arity, endpoint kind, role, symmetry/directionality, and temporal requirements;
- whether location is an argument to the relation or independent observed payload;
- rule registration/dispatch from relation semantics to constraint semantics;
- explicit failure behavior for unsupported or unknown relation kinds;
- conservative union/intersection behavior for same-source alternatives;
- stable IDs for uncertainty statements that do not contain a hidden exact relation; and
- source grouping that counts one observation as one source regardless of relation alternatives.

The design must not infer world-side relation truth from an observation label and must not use a
truth occurrence to fill missing public relation identity.

## Future Buddhist-methodology comparison

Only after the primitives above are explicit, a separate methodology comparison may examine
whether its vocabulary offers useful questions about:

- condition type rather than a single opaque relation name;
- symmetric or directed relation structure;
- concurrence versus temporal sequence; and
- evidential support versus modeled dependency.

No current code relation is equated with a specific `paccaya`. In particular,
`PersonPersonEncounter` is not mapped directly to any canonical category. Such a comparison would
be an external conceptual analysis, not production naming and not a 24-condition engine.

## Next recommendation

The recommendation is **Mixed: C now, then B and D before any relation performance experiment**.

- **Recommend C now:** pause the relation performance track and integrate the observation-axis
  findings: F2 has a direct location-to-Region connection; F4 time, F5 other-participant identity,
  and F6 S5 relation specificity do not have equivalent semantic connections.
- **Recommend B before resumption:** design only the minimal relation existence, taxonomy or
  compatibility, ambiguity, and alternative-merge contract justified by a future fixture.
- **Recommend D in parallel:** complete the artificial-world representation boundary and any
  temporal dependency needed by a relation whose meaning includes sequence. Do not repair relation
  specificity by inventing a time offset or participant ambiguity convention.

Recommend A is not justified on current S5.

## Validation scope

This branch changes documentation only. No production code, scenario, taxonomy, inference rule,
truth path, experiment definition, or generated result changes. In accordance with the B/C stop
rule, the full build/test/scenario suite and 200-seed determinism reruns are not required.
Validation is limited to clean-baseline verification, focused static code tracing, staged-diff
review, and `git diff --check`.
