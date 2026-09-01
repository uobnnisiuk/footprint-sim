# M1-F7 observation-semantics integration audit

## 1. Background and decision

This audit starts from M1-F6 final commit
`7c35763bb25e789c5fc3a7e510f5f892b93bc16d`. It integrates the M1-F observation-axis work with
the M1-E1 method-composition boundary and the M1-E2 artificial-world validity audit. It adds no
production semantics, new experiment, taxonomy, probability, sensor model, or generated result.

The integrated finding is asymmetric:

- **location** has one explicit information-to-constraint connection and is measurable within the
  current Region candidate layer;
- **time** has a raw value and experiment-level uncertainty wrapper but no justified
  observed-time-to-historical-window transformation;
- **other-participant identity** has exact record/addressing semantics but no ambiguity contract or
  identity-dependent narrowing rule; and
- **relation identity** has exact labels/addressing and one separate S3 co-location rule, but no S5
  semantic dispatch, ambiguity contract, or taxonomy.

No result establishes that time, participant identity, or relation identity is unnecessary or
redundant. They are currently unable to propagate into S5 candidate semantics.

### Recommended next single implementation

Implement **one minimal executable, first-class observation-to-constraint derivation slice**. Its
only initial rule and acceptance fixture should be the time-specific bounded-dependency fixture
already outlined by M1-F4.

This is **option D as the missing capability**, with option C used as its sole concrete fixture—not
two parallel projects and not a general rule engine. The deliverable should:

1. represent an observation's occurrence-time uncertainty explicitly;
2. name a truth-independent public predecessor/consequence dependency and duration/path bounds;
3. compute, rather than separately declare, the conservative predecessor constraint window;
4. preserve one source ancestry across possible ticks and paths; and
5. expose applicability, non-derivability, information loss, and usage role for audit.

After that single vertical slice, a new time-resolution experiment can validly compare exact and
wider observed occurrence windows against different derived predecessor windows. It must use a new
fixture, not reinterpret S5's `later-trace-a` or reproduce its `[13,14]` literals.

An abstract derivation interface without one executable rule would not unlock measurement. A time
fixture that still declares its output constraint independently would repeat the current defect.
The narrow vertical slice avoids both failure modes while keeping the implementation small.

## 2. Reviewed evidence

This audit directly reviewed:

- M1-F1 `later-trace-a` field ablation;
- M1-F2 location resolution;
- M1-F3 observed-time resolution;
- M1-F4 observed-time to historical-time transformation audit;
- M1-F5 participant-identity ambiguity audit;
- M1-F6 relation-semantics audit;
- M1-E1 method-composition correction;
- M1-E2 synthetic-world representation validity;
- the S5 scenario and its generation, verification, and traversal source declarations;
- interaction observation, occurrence, participant, causal constraint, candidate, query, and
  evidence-usage contracts;
- S3's explicit encounter co-location deriver;
- S5 projection, past-state reconstruction, tracing, source grouping, truth evaluation, and
  scenario validation; and
- the F2/F3 experiment-side projection contracts that currently carry finite Region sets and
  occurrence-time-window meaning.

No existing numerical result is recomputed here.

## 3. Integrated result history

### M1-F1: atomic availability, not marginal field value

F1 treated subject, time, location, and relation as an all-field eligibility conjunction. Full
emitted both same-source generation constraints; every incomplete profile emitted neither. For
reconstruction, Full produced mean 2.165 candidates and 27.83% reduction, while every incomplete
profile produced 3.000 and 0% reduction. All profiles retained 100% containment and zero false
exclusions.

That result identifies an **atomic transformation boundary**. It does not rank the four fields,
show equal value, or establish a minimum real observation set. A missing field caused constraint
non-generation because weak/partial constraints were not represented.

### M1-F2: location is connected and measurable

F2 added an experiment-side direct rule:

```text
projected Observation.ObservedRegionIds
    -> derived Constraint.AllowedRegionIds
```

With all other inputs fixed, widening `{fork-east, fork-west}` to the complete target-layer set
`{alternate-route, fork-east, fork-west}` changed reconstruction from mean 2.165 candidates and
27.83% reduction to 3.000 and 0%. Containment remained 100% with zero false exclusions.

Widening from three target-layer Regions to all six searchable Regions caused no further candidate
change because the query's candidate layer contained only three Regions. This second equality is a
candidate/world representation ceiling, not evidence that broader location uncertainty is
unimportant. Unknown had the same Region candidates as the finite broad levels but different
constraint existence, work, ancestry, and verification behavior.

### M1-F3 and M1-F4: time value exists; temporal derivation does not

F3 represented finite occurrence-time uncertainty in its experiment wrapper as
`OccursAtOnePossibleTick`. Every possible observed tick still read the same separately declared
historical window `[13,14]`. All finite windows from `[24,24]` through `[8,24]` therefore produced
identical candidates and work. Unknown disabled the time-bearing source and constraints.

F4 established that `[13,14]` is not derived from tick 24, availability tick 25, relation semantics,
or public transition duration. `later-trace-a` supports only a reported co-location statement at its
observed time under the general encounter rule. It is not a valid time-resolution fixture for the
historical mapping.

### M1-F5: exact participant is carried, not consumed

The exact target `person-a` is used for query targeting. The other participant `person-b` is copied
into observation/constraint records and stable IDs, but it is not read when choosing historical
Regions, windows, candidate disposition, or work. Finite candidate participants, kind-only,
identity-unknown, and kind-unknown are not representable.

The result is current non-propagation plus an ambiguity-contract gap, not proof that participant
identity is unnecessary.

### M1-F6: relation labels are carried, not dispatched in S5

S5 hardcodes observation relation `PersonPersonEncounter`, while its two same-source constraints
carry different free-text labels (`later-public-trace` and `later-public-trace-derived`) with the
same historical Regions and window. S5 constructs those constraints directly and does not call the
repository's S3-specific `PersonPersonEncounter -> same-time co-location` deriver.

The relation enum has one member, and no hierarchy, compatible set, existential unknown relation,
or conservative alternative merge exists. The labels matter for addressing and audit, not S5
narrowing.

### M1-E1: evidence use is operation-specific

E1 separated `CandidateGeneration`, `CandidateVerification`, `Traversal`, and `EvaluationOnly`.
Verification can support or refute an already generated candidate but does not silently regenerate
or delete it and is not passed into traversal. Traversal receives only declared traversal sources.
Truth is evaluation-only and enters only after public methods return.

Every future partial-observation design must preserve this boundary. Uncertainty does not authorize
a source to cross usage roles.

### M1-E2: lineage is valid with declared-fixture limitations

E2 materialized missing public verification sources, validated source resolution and public
availability, retained person/device separation, and confirmed that truth does not enter public
methods. Its gate remains **READY WITH EXPLICIT LIMITATIONS**.

The central remaining limitation was already explicit: S5 constraint transformations and allowed
Regions are fixture declarations rather than generally computed observation semantics. M1-F2
temporarily made one such computation explicit for location; F4–F6 showed the absence of equivalent
connections for the other axes.

## 4. What measurable and not measurable mean

The following judgments are distinct:

| Code | Meaning | Required evidence |
| --- | --- | --- |
| A: unnecessary | removing the information preserves the same justified inference | a contract must permit derivation without it and paired semantics/results must remain equivalent |
| B: redundant | another independent source supplies the same constraint content | independent ancestry and equivalent derived content must be demonstrated |
| C: non-propagating | the value exists in a record but is not an input to constraint/candidate semantics | static derivation/application audit is sufficient |
| D: ambiguity unrepresentable | only exact values or source removal exist, so precision cannot be graded faithfully | contract audit is sufficient |
| E: candidate/world limited | different constraints exist but the candidate or world state space cannot expose the difference | constraint difference plus candidate-layer audit is required |

The four axes classify as follows:

| Axis | Current classification | What is established | What is not established |
| --- | --- | --- | --- |
| location | measurable for the 2→3 Region boundary; E for 3→6/unknown candidate equality; D only for a first-class unknown constraint | a finite Region superset weakens the constraint and can change candidates | location is unnecessary outside the target layer; six Regions equal unknown information |
| time | C for every finite S5 level; D in the production time-meaning/unknown contract | observed time does not parameterize `[13,14]` | time is unnecessary, redundant, or candidate-limited |
| other participant | C plus D | exact `person-b` is not read by narrowing and alternatives are not representable | identity is unnecessary, kind-only is sufficient, or ambiguity affects only work |
| relation | C plus D | S5 labels do not dispatch a narrowing rule; taxonomy/unknown are absent | exact relation is unnecessary or a broader kind would preserve the constraint |

No axis has demonstrated B. F1's four-field conjunction does not demonstrate A for any field.

## 5. Common observation-to-inference pipeline

```text
World fact / occurrence (private truth)
        |
        v  observation process; may be lossy or wrong
Observation record (public/private evidence-side assertion)
        |
        v
Observed information content
  location / time / participants / relation
        |
        +--> EvidenceState / availability / visibility / provenance
        |
        v
Explicit derivation rule  <-- incomplete as a first-class S5 contract
        |
        v
Constraint
  target / constrained time / allowed state / evidence boundary / ancestry / usage
        |
        +--> CandidateGeneration
        +--> CandidateVerification
        +--> Traversal
        |
        v
Candidate result at the query window
        |
        v  only after all public methods return
Evaluation against private truth
```

### Stage-by-stage contract audit

| Stage | Current contract | Explicit semantics | Implicit or fixture semantics | Missing semantics | Truth/public boundary |
| --- | --- | --- | --- | --- | --- |
| world fact / occurrence | `SimulationEvent`, entity states, private `InteractionOccurrence` | event kind, exact tick, participants, Region, causal parent where present | S5 truth route and synthetic topology assumptions | general uncertain occurrence and calibrated observation process | private; never accepted by reconstructor/tracer |
| observation | `ObservationRecord`, `InteractionObservation` | source ID, observed tick/window, Regions, exact participants/relation, availability, visibility | bare interval meaning; relation arity/kind intent | observation-act time, partial participant/relation, explicit uncertainty meanings | public object may reference an opaque ID, never a truth-object lookup |
| information content | lists/intervals/enums embedded in observation | finite Region list; exact participant tuple; exact relation enum; closed time interval | Region list as alternatives in F2; time interval as one-possible-tick only in F3 wrapper | common partial/unknown sum types and existential content | must be based only on available observation content |
| evidence boundary | `EvidenceState`, `AvailableAtTick`, visibility, provenance | cutoff, boundary assertion, public/private access, source references | `Confirmed` is an assertion, not calibrated accuracy | calibrated errors are out of scope; observation-act/delay distinction absent | E2 validates usable source resolution; private/future excluded |
| derivation | S3 named co-location method; S5 definition strings and construction code | S3 checks encounter/target/person/location and copies same-time Regions | S5 free-text `RelationKind`/`DerivationKind` suggest transformations | executable rule identity, required inputs, propagation, loss, failure, validation | rule must have no truth parameter or realized-path input |
| constraint | `CrossThreadConstraint`, `CausalTracingConstraint` | target, one constrained window, allowed Region set, evidence state, lineage, flags, labels | S5 output payload separately declared | disjunctive alternatives, window sets, unknown top values, typed derivation linkage | public evidence only; provenance retained |
| method use | reconstructor, tracer, source groups, E1 usage roles | Region/time intersection, evidence-state behavior, source independence, operation roles | S5 topology completeness | semantics for new weak constraints and incomplete topology | no truth; verification/generation/traversal remain disjoint |
| candidate | `PastStateCandidate`, candidate-warp states | one target/query window and Region candidate; lineage/disposition/audit | public state layers are complete fixture choices | sub-Region, participant/relation dimensions, disjoint temporal alternatives | still public inference output |
| evaluation | `PastStateTruthEvaluation`, evaluation builder | post-method containment, false exclusion, reduction, work | truth Region is the scored dimension | scoring other dimensions only after they become legitimate outputs | truth enters here, after public methods return |

## 6. Four-axis capability matrix

| Axis | Raw observation represented | Ambiguity represented | Derivation rule | Constraint changes with precision | Candidate result measurable | Main limitation |
| --- | --- | --- | --- | --- | --- | --- |
| location | yes: zero or more Region IDs | finite sets in F2; no first-class unknown/top meaning | F2 explicitly copies observed Regions; baseline S5 otherwise declares output | yes for finite F2 levels | yes within target Region layer | candidate layer hides distinctions outside its three Regions; unknown produces no constraint |
| time | yes: exact/closed interval | only F3 wrapper says one possible tick; production interval meaning is ambiguous | no observed→historical rule in S5 | no across finite F3 levels | no for time precision in current S5 | occurrence uncertainty, duration, observation act, dependency, and window union are missing |
| participant | exact `(ID, kind, role)` tuples | no candidate set, kind-only, identity unknown, or kind unknown | target matching exists; other ID has no S5 narrowing rule | no | no | ambiguity contract and identity-sensitive rule both missing |
| relation | exact one-member observation enum; arbitrary derived label | no category, compatible set, exists-kind-unknown, or existence-unknown | S3 same-time encounter rule only; no S5 dispatch | no | no | taxonomy/ambiguity and relation→historical constraint rule missing |

### Cross-cutting contract support

| Capability | Location | Time | Participant | Relation |
| --- | --- | --- | --- | --- |
| exact value | yes, as Region set of width one or more | yes, `[t,t]` | yes | yes, one enum member |
| partial value | finite Region superset | only experiment-level closed one-of-ticks meaning | no | no |
| explicit unknown while source exists | observation can carry no Regions, but no weak constraint | no production time-less observation | no | no |
| source lineage | yes | yes for finite projections | yes for exact record | yes for exact record/labels |
| same-source alternatives stay non-independent | yes for F2 Region set | F3 diagnostics preserve one ancestry, but no semantic output variation | not representable | not representable |
| person/device boundary | orthogonal and retained | orthogonal and retained | exact kinds/flags exist but are not fully cross-validated | relation arity/kinds not fully enforced publicly |
| temporal meaning | fixed historical window only | several time fields exist but dependency is missing | none specific | same-time only in S3; S5 sequence unresolved |

## 7. World fact, observation, evidence, and constraint are different propositions

### Relation example

These statements must not collapse:

```text
world: person-a and person-b actually encountered
observation: a public record asserts person-a encountered person-b
partial observation: a public record asserts person-a encountered someone
derived evidence: the reported encounter supports a co-location statement under rule R
constraint: person-a may be in Region set S during window W
```

The truth occurrence projector enforces exactly two known persons for a world-side
`PersonPersonEncounter`. The public observation factory is weaker, and S5's `later-trace-a` has no
truth occurrence reference. It must not consult truth to fill participant or relation ambiguity.

### Time example

Current time values are distinct:

| Time concept | Current support | Limitation |
| --- | --- | --- |
| actual occurrence time | private occurrence/event exact tick | unavailable for `later-trace-a`; not a public inference input |
| reported relation time | interaction observed closed window | interval meaning does not distinguish uncertainty from duration |
| ordinary recorded-event time | `ObservationRecord.ObservedTick` | exact point only |
| observation-act time | absent | cannot model capture time separately from reported occurrence |
| evidence availability time | separate point and cutoff comparison | correctly not treated as occurrence time |
| historical target time | query closed window | independently authored |
| constraint applicability time | one closed constrained window | independently authored in S5; no disjoint set of windows |

Numeric ordering alone is not an explicit dependency. Availability after an observed time does not
imply a historical state, and a later label does not supply a duration.

## 8. Observation-content ambiguity support

| Axis / information state | Current support | Missing contract | Required future contract |
| --- | --- | --- | --- |
| location exact/narrow set | supported | no distinction between alternatives and multi-location persistence in base type | state that the set is conservative alternatives at the relevant time |
| location broad set | supported | candidate layer may ignore out-of-layer Regions | retain set semantics and audit affected candidate domain |
| location unknown | empty observation content possible; no constraint | unknown/top not distinguished from no usable location constraint | existential source content plus explicit non-informative/top derivation result or failure reason |
| exact occurrence time | `[t,t]` supported | occurrence versus observation-act meaning not typed | temporal semantic kind |
| occurrence-time uncertainty | F3 wrapper only | not in production observation; no historical rule | one-of-time/window-set type plus conservative rule application |
| observation-time uncertainty | unsupported | observation-act time absent | separate observation-act window if a future model needs it |
| duration/persistence | bare interval only, meaning ambiguous | no persistence kind or dwell semantics | typed duration/state interval and applicability rules |
| time unknown | not representable in production interaction observation | required window cannot be omitted | explicit unknown occurrence-time content distinct from absent source |
| exact participant | supported | public relation invariants incomplete | keep exact tuple and validate relation-specific arity/kinds/roles |
| finite participant set | unsupported | no alternative set | typed candidate endpoints sharing one source ancestry |
| participant kind only | unsupported | exact nonempty ID required | existential participant with known kind |
| identity unknown | unsupported | no existential endpoint | relation endpoint exists without exact ID |
| kind unknown | unsupported | enum has no union/unknown | typed person/device alternatives without default promotion |
| exact relation | only `PersonPersonEncounter` observation kind | no competing kind for comparison | validated relation descriptor with explicit primitive requirements |
| compatible relation set | unsupported | no compatibility model or merge | finite alternatives with per-kind derivation and conservative merge |
| broader relation category | unsupported | no taxonomy | only a repository-justified minimal hierarchy, if needed |
| relation exists, kind unknown | unsupported | existence coupled to exact enum | existential relation assertion separate from identity |
| relation existence unknown | unsupported | source absence is the only practical state | epistemic existence state distinct from no occurrence |

These future representations should prefer finite sets, closed intervals or interval sets, and
explicit alternatives. No probability model is required for the next stage.

## 9. The derivation-rule gap

A first-class observation-to-constraint rule is needed. The current S5 definition stores source and
output fields together, while runtime construction copies the output fields independently of most
observation content. Free-text labels document intent but cannot validate it.

The minimum useful contract is not a broad policy engine. One derivation instance should contain:

| Contract item | Minimum responsibility |
| --- | --- |
| stable rule identity/version | address the public transformation without a hidden truth value |
| source requirements | declare required location/time/participant/relation content and accepted uncertainty meanings |
| applicability | validate source kind, target, relation/participant prerequisites, availability, and public dependency inputs |
| output constructor | compute allowed Regions/window/alternatives from the declared inputs rather than repeat output literals |
| uncertainty propagation | define conservative union across possible values and prohibit unjustified intersection/default filling |
| information loss | report which distinctions the output cannot retain |
| failure result | distinguish unsupported content, unknown content, unavailable source, and invalid configuration |
| provenance | retain source observation, rule ID, public dependency/path IDs, and shared ancestry |
| usage role | bind the output to generation, verification, traversal, or evaluation without leakage |
| truth boundary | expose no truth occurrence, realized transition, scored Region, or PRNG draw |

Validation should reject a scenario that independently supplies an output inconsistent with the
rule. Stable output IDs should depend on the public uncertainty statement and rule inputs, never on
a hidden exact value. Deterministic ordering is required for alternative sets.

The existing S3 same-time co-location method and F2 Region-copy evaluator are useful reference
instances. They are not a complete generic derivation architecture.

## 10. Weak constraints

Information precision should be able to weaken a constraint instead of only deleting it.

| Axis | Weak constraint meaning | Current support | Missing piece |
| --- | --- | --- | --- |
| location | a superset of allowed Regions | yes for finite F2 profiles | first-class unknown/top result and base derivation linkage |
| time | a conservative union of predecessor windows reachable from every possible observed occurrence time/path | no | typed occurrence uncertainty, explicit dependency/duration rule, and possibly an interval-set output |
| participant | union of legitimate constraints for every candidate endpoint, with one source lineage | no | endpoint alternatives plus an identity-sensitive rule; no dummy person/device promotion |
| relation | union of constraints derived by every compatible relation alternative, preserving unsupported alternatives | no | relation alternatives/taxonomy, per-kind rules, and merge semantics |

`EvidenceState.SupportedButUncertain` is not a substitute for imprecise content. Evidence-boundary
state and information-set width are independent dimensions.

A single `CausalTracingConstraint` currently has one closed time window. If a future derivation
produces disjoint windows, widening them into one convex interval may lose information; emitting
multiple constraints may falsely multiply work or evidence. The chosen temporal vertical slice must
decide whether its first fixture guarantees a single conservative interval or needs a same-source
interval-set representation. It must not silently clone independent evidence.

## 11. Unknown is not absent

The common contract principle is:

```text
unknown content = a source/assertion exists but a field's value is not known
absent source    = no usable source assertion is available to the query
negative fact    = an explicit assertion that the world proposition did not hold
```

Axis-specific consequences are:

- location unknown is not nowhere;
- time unknown is not that the event never occurred;
- participant unknown is not nobody;
- relation-kind unknown is not no relation; and
- a missing public transition is not an impossible transition unless the public model explicitly
  declares completeness for that scope.

F2/F3 unknown profiles often withheld the derived constraint because production contracts could not
carry the missing content. That behavior is an experiment boundary, not the desired general
representation of unknown. Future contracts must retain source existence, field availability, and
derivation failure separately.

## 12. Provenance, source independence, and usage roles

Every alternative produced from one observation must retain:

- one source observation identity;
- common direct source and ancestry;
- one source group for independence counting;
- the derivation rule and public dependency inputs used; and
- alternative-specific output lineage without claiming independent corroboration.

This applies equally to Region alternatives, possible occurrence ticks, participant candidates, and
relation candidates. Expanding one source into N constraints may be an internal representation, but
it must not create N independent source groups or N independent verification votes.

E1's operation boundary is invariant under partial observations:

```text
CandidateGeneration  != CandidateVerification
CandidateVerification != Traversal
Traversal             != EvaluationOnly
```

An ambiguity expansion inherits the source's declared usage role. A verification alternative does
not enter generation; a generation alternative does not become traversal input; truth remains
evaluation-only. Scenario validation should continue rejecting accidental cross-role source reuse
unless an explicit per-constraint multi-role contract is later justified.

## 13. Candidate and world representation limitations

| Axis | Observation limitation | Constraint limitation | Candidate limitation | World limitation |
| --- | --- | --- | --- | --- |
| location | base set meaning not typed | finite Region set works; unknown/top not explicit | only three Regions exist at target layer, hiding width-3 vs width-6 distinctions | aggregate named Regions, no sub-Region or physical sensor resolution |
| time | bare interval conflates meanings | one independently declared closed window; no dependency or disjoint windows | fixed coarse state intervals and one query window | integer ticks, declared dwell/gaps, no calibrated duration or observation act |
| participant | exact tuples only | alternatives/kind-only cannot be retained; other ID not consumed | candidate result has target Region, not participant dimension | persons/devices exist separately, but S5 has no identity-dependent public rule |
| relation | one exact enum plus free strings | no taxonomy, alternatives, or semantic dispatch | candidate result has no relation dimension; relation matters only if it changes state/time constraints | only encounter occurrence kind is formalized; `later-trace-a` has no occurrence |

An observation refinement is performance-measurable only if all links are capable of carrying its
difference. A changed stable ID is not enough. A constraint difference hidden outside the target
candidate domain is also not enough.

For participant or relation precision to affect the current Region-based performance metrics, a
legitimate public rule must ultimately derive different allowed state/time alternatives. Otherwise
only provenance or work can change, which is not candidate discrimination.

## 14. Prioritized missing capabilities

| Priority | Capability | Why now/later |
| ---: | --- | --- |
| 1 | minimal executable observation→constraint derivation contract, instantiated only by a bounded-duration time fixture | fixes the common semantic break and immediately unlocks one valid new precision experiment |
| 2 | extract/reuse the proven derivation pattern for other fixtures and migrate location as a conformance regression | broadens reuse only after the first rule demonstrates the contract is sufficient |
| 3 | participant ambiguity contract plus an identity-sensitive synthetic rule | ambiguity alone would not affect current narrowing; person/device and source grouping need careful design |
| 4 | relation existence/alternatives and only the minimal justified taxonomy | taxonomy alone has no value until per-kind derivations exist; greatest ontology risk |
| 5 | candidate/world refinement | needed for finer spatial sensitivity, but current location already has one measured boundary and other axes are blocked earlier |

The ranking is about research dependency, not implementation ease.

## 15. Recommendation assessment

| Candidate | Semantic correctness | Conceptual size / assumptions | Truth-leak risk | Cross-axis reuse | Immediate measurement | Decision |
| --- | --- | --- | --- | --- | --- | --- |
| A participant ambiguity | incomplete without an identity-sensitive rule | several new endpoint states and merge rules | medium if truth identity fills alternatives | medium | no, contract alone changes no Regions/window | later |
| B relation ambiguity/taxonomy | incomplete without semantic dispatch | largest ontology choice; taxonomy currently unjustified | low/medium | medium | no, labels remain non-semantic | later |
| C time fixture alone | correct only if output is computed from explicit dependency | small if bounds are public and preregistered | low | mostly temporal | yes, but risks another separately declared mapping | use only as D's acceptance fixture |
| D explicit derivation contract | addresses the common break | small if limited to one rule shape, large if generalized prematurely | lowest with no truth inputs | high across all axes | yes when delivered with one executable temporal instance | **chosen** |
| E candidate/world refinement | semantically orthogonal | introduces more state granularity and possible topology assumptions | low | medium | yes for additional location levels only | later |

### Exact implementation boundary

The next branch should implement one derivation vertical slice, not two frameworks:

```text
typed later occurrence-time uncertainty
        + named public dependency/path and duration bounds
        |
        v
minimal explicit rule contract
        |
        v
conservative predecessor time constraint
        |
        v
existing Region/time candidate reconstruction
```

It should not modify `later-trace-a`, implement participant/relation ambiguity, create a generic
rule-language, refine spatial Regions, or run a 24-condition engine.

## 16. Performance experiment unlocked afterward

Once the vertical slice is implemented and validated, a dedicated paired time experiment can vary
only the later occurrence-time information:

1. exact occurrence tick;
2. narrow possible-tick interval;
3. wider interval crossing a preregistered predecessor candidate boundary; and
4. occurrence time unknown, while event/relation existence remains represented if supported by the
   new contract.

For each possible later tick and public path, the rule should invert declared duration bounds,
conservatively union predecessor windows, and retain one source ancestry. The experiment can then
measure derived-window width/alternatives, candidate count, containment, false exclusion, reduction,
and generation/traversal work. Paired worlds and truth-free public inputs remain mandatory.

This would measure temporal information value for that explicit synthetic dependency. It would not
validate real travel times or rescue performance.

## 17. Roadmap impact

The integrated roadmap is:

```text
purified artificial world
    -> causal tracing / past-state reconstruction
    -> observation-information evaluation
    -> representation gaps discovered (F1-F6)
    -> minimal executable observation→constraint derivation slice (next)
    -> resume time precision on a dedicated fixture
    -> add participant or relation weak-observation contracts one at a time
    -> vary artificial-world/candidate representation and test sensitivity
    -> increase temporal, relational, spatial, and observation complexity separately
    -> introduce real observation models only after synthetic semantics remain traceable
    -> study search workload and handoff to existing rescue means
    -> study rescue action -> new observation -> re-estimation closed loops
```

The later program still includes:

- minimum observation-set research after partial observations yield legitimate weak constraints;
- search workload rather than candidate count alone;
- handoff from inference output to existing rescue tools and teams;
- rescue actions, active probing, and the new-observation feedback loop; and
- case/representation sensitivity before any claim of real rescue performance.

No current F result moves directly to sensors, rescue action, or operational validation.

## 18. Artificial-world validity implications

The E2 gate remains **READY WITH EXPLICIT LIMITATIONS**, not fully validated. This integration does
not reopen corrected lineage defects. It sharpens the largest remaining threat: a scenario label and
output literal can look like a derivation without being one.

The chosen next slice improves internal validity by requiring a public, executable dependency and by
making uncertainty propagation auditable. It still will not establish:

- calibrated observation accuracy;
- physical realism of duration bounds;
- complete real topology;
- real participant/relation taxonomies;
- continuous space/time adequacy; or
- rescue effectiveness.

Future artificial-world sensitivity should independently vary candidate granularity, public
topology completeness, interval resolution, and fixture assumptions after the semantic connection is
working. It must not tune world resolution merely to create an effect.

## 19. Minimum-observation research implications

F1's Full-only minimum is not a field minimum; it is the smallest profile accepted by an atomic
all-field rule. Minimum-observation research becomes meaningful only when partial content can produce
correspondingly weak constraints and when candidate representation can expose their distinctions.

After the temporal vertical slice, time information can be included in minimum-set analysis for that
fixture without equating unknown with absent. Participant and relation must remain excluded from
field-value ranking until their ambiguity and semantic-dispatch gaps are separately resolved.

Source independence and E1 usage roles must be part of any minimum-set criterion. Multiple
alternatives from one record do not count as multiple observations, and verification records do not
substitute for generation observations merely because they support the same candidate.

## 20. Future Buddhist-methodology comparison

Only after modern primitives are explicit, a separate comparison may examine:

- condition type: what kind of modeled condition links an observation to a state constraint;
- temporal relation: concurrence versus explicit sequence;
- support versus dependency: evidence supporting a candidate is not the same as the world relation
  that generated it;
- alternatives and unknown: conservative possible-state sets versus absence;
- forward implication and converse boundary: a rule's output does not automatically validate its
  inverse; and
- direct and reverse examination of a declared dependency.

`Paccaya` and other Abhidhamma/Paṭṭhāna material may be future comparison sources after this
primitive audit, but no current rule is identified one-to-one with a canonical condition. The
twenty-four conditions are not assumed to be a complete basis, no new term is coined, and no
Buddhist term enters production naming.

## 21. Claim boundaries

This integration can claim that:

- location precision propagated through a direct Region-set rule and changed current S5 candidates;
- the F2 3→6 Region plateau is candidate-representation-limited;
- finite observed-time precision did not propagate to S5 historical windows;
- other-participant and relation identities do not parameterize current S5 narrowing;
- participant and relation ambiguity are not currently representable;
- F1 measured an atomic transformation boundary rather than marginal field values;
- E1 usage roles and E2 truth/lineage boundaries remain required invariants; and
- a minimal executable derivation contract with one temporal fixture is the highest-leverage next
  capability.

It cannot claim that:

- time, participant, or relation information is unnecessary or redundant;
- location is always the most valuable observation axis;
- a wider finite time interval is harmless outside the current fixture;
- participant kind-only or broader relation categories are sufficient;
- candidate equality implies information equivalence;
- a new temporal fixture will improve performance;
- current aggregate Regions/ticks are adequate for real observations; or
- any synthetic result predicts rescue workload, rescue success, or a doctrinal correspondence.

## 22. Validation scope

This branch changes documentation only. No scenario, production contract, inference rule,
candidate/world representation, experiment configuration, telemetry schema, truth path, or generated
result changes. No 200-seed rerun is justified.

Validation is limited to:

- verifying the clean M1-F6 starting commit;
- direct review of F1–F6, E1, E2, S5, and the relevant contracts and implementations;
- staged documentation review; and
- `git diff --check`.
