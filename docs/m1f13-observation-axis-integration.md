# M1-F13 observation-axis integration and next-study design

## 1. Background and decision

M1-F13 integrates the location, time, participant, and relation tracks after each field has been
audited and, where justified, connected to an executable public derivation. It starts from M1-F12
commit `7859c2025621c8fd2151a1343bc80ebf3cb1380b` and changes documentation only. It does not add a
rule, relation kind, world generator, multi-axis evaluator, or 200-seed experiment.

The main result is deliberately not a ranking of axes:

- location, time, and participant information each have a demonstrated single-axis path to
  historical candidates, but in different synthetic world families;
- relation has an Exact-versus-Unknown applicability slice, but no meaningful second formal kind;
- the existing shared derivation output can represent a single time-and-Region predicate, but its
  fixture reconstructor accepts at most one derived constraint;
- S5 has multiple-constraint behavior, but its source-group and conflict rules are not a general
  composition contract and its time treatment is not the F8/F9 candidate-time filter; and
- source independence does not by itself state whether two derived artifacts are conjunctions,
  alternatives, duplicates, or conflicts.

Therefore location/time/participant are **not yet ready for one executable factorial study**. The
recommended next single implementation is **C: a minimal source-aware CandidateGeneration
constraint-composition contract with one deterministic acceptance fixture**. A common synthetic
world vertical slice follows only after that contract fixes the Boolean, dimensional, provenance,
and conflict boundaries.

## 2. Reviewed milestones and implementation

This audit directly reviewed:

- M1-F1 field-level ablation;
- M1-F2 location resolution;
- M1-F4 time-transformation audit and the retained M1-F3 boundary;
- M1-F5 participant-identity audit;
- M1-F6 relation-semantics audit;
- M1-F7 observation-semantics integration;
- M1-F8 bounded-duration time vertical slice;
- M1-F9 200-world paired time evaluation;
- M1-F10 participant ambiguity vertical slice;
- M1-F11 200-world paired participant evaluation;
- M1-F12 relation-dependent vertical slice;
- M1-E1 evidence-usage correction; and
- M1-E2 artificial-world representation validity.

The code audit included the F2/F9/F11 experiment definitions and evaluators, F8/F10/F12 derivation
contracts and rules, `ObservationDerivedConstraint`,
`ObservationDerivedHistoricalCandidateReconstructor`, `PastStateReconstructor`,
`OperationalCausalTracer.GroupSources` and constraint application, evidence usage roles, candidate
state contracts, source ancestry, availability filters, and the relevant validators and tests.

## 3. Integrated axis status

### 3.1 Location

M1-F2 established an experiment-side direct path:

```text
InteractionObservation.ObservedRegionIds
    -> CausalTracingConstraint.AllowedRegionIds
    -> S5 historical Region candidates
```

The four nested profiles used Region widths 2, 3, 6, and unknown/non-generation on the same S5
world per seed. At the reconstruction method, width 2 produced mean 2.165 candidates and 27.83%
reduction; widths 3 and 6 and Unknown produced 3.000 candidates and no reduction. Containment was
100% and false exclusion was zero.

The width-2 to width-3 boundary is measurable in S5. Width 3 already contains every target-layer
candidate Region, so width 3 to width 6 changes the constraint but not the candidate set. The extra
Regions do not exist in the target candidate layer. Width 6 and Unknown also have equal candidate
counts but differ in constraint existence, work, source lineage, and evidence dispositions.

Limitations for combination work are important: the F2 rule is embedded in its evaluator rather
than the F8/F10 derivation envelope, and its historical time window remains an independent S5
scenario declaration. F2 is evidence that finite Region-set precision can propagate, not a reusable
multi-axis location contract and not physical spatial calibration.

### 3.2 Time

M1-F4 established that S5's `[24,24] -> [13,14]` transformation was not semantic. M1-F8 replaced
that invalid comparison with one explicit public rule:

```text
Observed later occurrence window O
+ public dependency duration D
    -> DerivedPast(O,D) = [o_min-d_max, o_max-d_min]
    -> historical candidate time filter
```

F9 kept `bounded-duration-predecessor-window-v1` fixed across 200 generated worlds and 800 paired
profiles. It observed zero derived-window monotonicity violations, zero candidate-set monotonicity
violations, 100% containment, and zero false exclusions. Mean candidates were 2.190, 3.210, 5.295,
and 8.555 for Exact, Medium, Broad, and Unknown.

In 92 worlds, at least one finite derived window widened while the candidate set did not. No
candidate occupied the added tick band. Eleven worlds had equal candidate sets across all finite
profiles and ten had Broad equal to Unknown. These are discrete candidate-domain effects, not proof
that the time distinction lacks information.

F9 deliberately gave every candidate a unique Region and gave the duration dependency the complete
candidate Region set. Thus only the time predicate was discriminating inside that family. This
orthogonality was achieved by fixture construction and does not yet define cross-rule composition.

### 3.3 Participant identity

M1-F5 showed that S5 carried exact `person-b` only in identity/provenance. M1-F10 then added the
truth-free rule:

```text
PossibleParticipants P
+ each p's public same-time AllowedRegions
    -> union(AllowedRegions(p), p in P)
    -> target historical Region candidates
```

Missing evidence for any possible participant fails safely rather than producing a partial union.
Unknown identity retains the encounter assertion and produces no participant-derived constraint.
The rule is Person-only, uses exact `PersonPersonEncounter`, and is CandidateGeneration-only.

F11 fixed `encounter-participant-location-union-v1` across 200 worlds and 800 profiles. It found
zero derived-Region or candidate-set monotonicity violations, 100% containment, zero false
exclusions, zero source-independence violations, and no finite derivation failures. Mean candidates
were 4.210, 6.165, 7.995, and 9.705 for Exact, Narrow, Broad, and Unknown.

The 400 finite participant-set transitions split into:

- 78 participant-to-Region plateaus;
- 45 Region-to-candidate plateaus; and
- 277 candidate expansions.

F11 keeps all candidates at the exact encounter tick, so participant precision affects Region but
cannot reveal time interaction. In a multi-tick common world the current participant constraint
also carries its encounter window. Unknown identity makes the whole constraint non-generated. A
composition design must prevent that behavior from silently removing or adding time information
when the intended manipulated axis is participant identity.

### 3.4 Relation

M1-F12 added first-class public relation information with two justified states:

```text
Exact(PersonPersonEncounter)
    -> person-person-encounter-co-location-v1 applicable
    -> F10 Region constraint

relation assertion exists, kind unknown
    -> exact Encounter applicability not established
    -> no relation-derived constraint
```

The deterministic fixture produced one candidate for Exact and three for Unknown with containment
in both, no false exclusion, and no monotonicity violation. Relation information controls rule
applicability, not only ID/provenance.

The formal inventory still contains only `PersonPersonEncounter`. There is no repository-grounded
second kind, hierarchy, compatibility set, or conservative merge across different relation rules.
Relation is therefore not a combination-study precision axis. It is a valid fixed prerequisite for
the participant co-location rule: keep exact `PersonPersonEncounter` identical in every initial
location/time/participant profile.

## 4. Capability matrix

`Unknown` below means an assertion/source remains present while that content is unknown. In some
current paths the safe operational result is constraint non-generation because an explicit top
constraint is unavailable.

| Axis | Raw observation represented | Exact | Partial / ambiguity | Unknown | Explicit derivation | Constraint and candidate propagation | Paired-world / monotonicity / safety | Main plateau | Combination-ready |
|---|---|---|---|---|---|---|---|---|---|
| Location | yes, Region IDs on an interaction observation | yes at Region granularity; F2's narrowest set has width 2 | finite nested Region sets | source remains with no Regions; no location constraint | yes in F2 evaluator: direct Region copy; not reusable envelope | yes in S5 target layer | 200 seeds / nested profiles; 100% containment, 0 false exclusion; no F9-style general violation counter | width 3→6 constraint change is outside represented target candidates | **Ready with limitation**: proven axis, but S5-specific time and contract path must be removed |
| Time | yes, typed occurrence uncertainty in F8/F9 | point window | nested closed uncertainty windows | event remains; `UnknownOccurrenceTime`; no constraint | `bounded-duration-predecessor-window-v1` | yes, derived window filters candidate states | 200 worlds / 800 runs; 0 window and candidate violations; 100% containment, 0 false exclusion | 92 worlds with a widened window but unchanged candidates | **Ready with limitation**: single-axis rule is ready; Region scope and multi-constraint composition must be neutralized/defined |
| Participant | yes, exact/finite/unknown other-person information | exact Person ID | finite Person candidate set | encounter remains; identity unknown; no constraint | `encounter-participant-location-union-v1` | yes, participant union changes Regions then candidates | 200 worlds / 800 runs; 0 Region and candidate violations; 100% containment, 0 false exclusion | 78 input→Region and 45 Region→candidate plateaus | **Ready with limitation**: Person-only, exact relation/time, and current whole-constraint failure can couple time with identity |
| Relation | yes, independent relation information in F12 | exact one-member enum | no justified finite alternatives/category | relation assertion remains; kind unknown; no rule dispatch | `person-person-encounter-co-location-v1` applicability wrapper | yes in one deterministic Exact/Unknown fixture | vertical slice only; containment 100%, 0 false exclusion, 0 violation | taxonomy/inventory boundary before a population plateau can be studied | **Not ready** as a precision axis; **ready as a fixed Exact prerequisite** |

No cross-family numerical ordering is valid. F2, F9, and F11 use different candidate universes,
Region meanings, time layouts, density rules, and supporting public inputs. Their means cannot show
that location, time, or participant identity is generally more valuable.

## 5. Single-axis comparability versus combination readiness

| Question | Location | Time | Participant | Relation |
|---|---|---|---|---|
| Can precision be varied without truth lookup in its own family? | yes | yes | yes | Exact/Unknown only |
| Does a finite precision change reach a constraint? | yes | yes | yes | no finite series exists; Exact controls applicability |
| Can the current shared fixture adapter combine it with another constraint? | no; F2 uses S5 path | no; adapter accepts one constraint | no; adapter accepts one constraint | no; wrapper delegates one constraint |
| Can a non-manipulated constraint dimension be made explicit top today? | not first-class | Region top is emulated by enumerating the whole fixture universe | time top is not validated; F11 avoids the issue with one tick | not applicable as a fixed prerequisite |
| Can source logic distinguish joint fields from alternatives? | no general marker | no | alternatives are unioned inside one constraint | no finite alternatives |
| Immediate factorial status | not yet | not yet | not yet | hold fixed |

Location/time/participant inputs can be changed independently at their observation contracts, but
they cannot yet be safely composed on one candidate universe. The missing link is not another
single-axis derivation rule; it is an explicit rule for combining already-derived predicates while
keeping source grouping separate from logical combination.

## 6. Current constraint-composition behavior

There are two relevant implementations, and neither is the required common contract.

### 6.1 F8/F10/F12 fixture adapter

`ObservationDerivedHistoricalCandidateReconstructor` accepts zero or one
`ObservationDerivedConstraint`. It creates candidates for each public `(state window, Region)` and,
when one constraint exists, keeps a candidate only when both are true:

```text
candidate.TimeWindow overlaps constraint.ConstrainedTimeWindow
AND
candidate.RegionId is in constraint.AllowedRegionIds
```

This proves Region × Time conjunction **inside one already-justified constraint**. It does not accept
a collection, group sources, combine alternatives, detect conflicting constraints, or decide
whether two rules may be conjoined. Its derivers enforce public availability, Confirmed state, and
CandidateGeneration before construction.

### 6.2 S5 `PastStateReconstructor`

S5 first filters public/cutoff-available constraints and then selects generation constraints whose
declared time windows overlap the query target window. It groups sources by transitive overlap among
ancestry IDs, source observation IDs, and direct source IDs.

For Confirmed Person generation constraints, the Region behavior is:

```text
within each source group: union AllowedRegionIds
across source groups:      intersect the group unions
```

If any generation constraint is explicitly `Conflicting`, or if Confirmed group intersections are
empty, S5 returns the base Region candidates instead of an empty hard result and records conflict
dispositions. `SupportedButUncertain` does not hard-narrow the outer candidate set. Future/private
constraints are omitted by availability/visibility filters, not converted to negative facts.

This behavior protected F2's two same-source derived records from being counted as independent. It
is not enough for the next study:

- same-source constraints are always treated as Region alternatives, but two fields from one
  observation may be jointly true and require conjunction;
- different-source constraints are intersected if Confirmed, although source independence alone
  does not establish logical compatibility;
- time windows choose whether a constraint applies to the fixed query layer; they do not filter a
  multi-tick candidate tuple as F9 does; and
- the result candidate is a target-window Region, not a common time-and-Region hypothesis.

`OperationalCausalTracer` has a third behavior: at one overlapping state it directly intersects all
Confirmed constraints, while source grouping is primarily retained for lineage. Empty intersection
marks conflict and preserves the outer set. This differs from the reconstructor's within-group
union. The first combination experiment must not mix these method-specific policies.

## 7. Required composition semantics

The following rules are semantic preconditions, not consequences of collection APIs.

### Region × Time

For a candidate hypothesis `c = (time, Region)`, conjunction is justified only when both public
claims apply to the same target state/occurrence and are asserted as jointly required:

```text
c.Region in L
AND
c.Time overlaps T
```

F9's duration dependency must use the complete common Region domain so it adds no hidden location
precision. A location rule must use a time scope that adds no hidden time precision. Enumerating the
domain as an implicit top value must be recorded explicitly; unknown must not be confused with an
empty set.

### Region × Region

Let `L` be the target-location constraint and let `P` be the conservative participant-derived union.
If both are jointly valid hard claims about the same target occurrence/time scope, the combined
predicate is:

```text
c.Region in L AND c.Region in P
equivalently c.Region in intersection(L, P)
```

The union across possible participants happens **before** this conjunction. Intersecting individual
participant alternatives would select a hidden identity or overconstrain the result.

### Same source

Same source means “not independent evidence”; it does not mean OR. Four different relationships
must remain distinguishable:

- jointly asserted fields from one observation: conjunction may be valid;
- mutually exclusive alternative interpretations: union candidate possibilities;
- duplicate/redundant artifacts: canonicalize without extra strength or work credit; and
- inconsistent artifacts: record conflict and preserve a safe outer set.

Current source ancestry cannot identify these logical relationships by itself.

### Independent sources

Independent source groups may be counted as independent support, but may be intersected only after
their applicability to the same target proposition and their hard-constraint status are established.
Independence is neither necessary nor sufficient for conjunction. One observation's location and
time fields can be conjoined without being independent; two independent observations can still
conflict or address different times.

### Conflicts and unavailability

The purified first family should exclude conflicts. A later, separate condition should decide and
test whether an empty hard conjunction produces an explicit conflict result plus the pre-conflict
outer set, another conservative top result, or a query failure. It must not return an empty search
space, resolve by majority vote, or choose truth.

Unavailable/private/future evidence contributes no active constraint at the cutoff but remains an
audited unavailable source. Unknown content retains its observation and typed derivation failure.
Neither becomes absence.

## 8. Source independence and observation axes

An evidence field is not an evidence source. The independence unit should be the connected public
source group established by source observation ID, direct source, and common ancestry. A derived
constraint is an artifact of those sources, not an additional observation.

For a future common family, record a source graph rather than only a field list:

| Node | Proposed meaning | Independence treatment |
|---|---|---|
| `O-location` | public target-location observation | one observation/source group |
| `O-later-time` | public later-event occurrence-time observation | one source group |
| `D-duration` | public dependency and duration source | supporting model input; distinct provenance, not a second copy of `O-later-time` |
| `O-encounter` | exact relation plus other-participant information | one encounter source group regardless of participant alternatives |
| `E-location(p)` | public time-applicable location evidence for possible participant `p` | one independently traceable source per actual record, when the fixture declares them independent |
| derived constraints | outputs of location/time/participant rules | never new independent observations |

The first common family may keep the three observation families distinct to isolate rule outputs.
That design studies **axis/derivation-family combinations**, not the field minimum inside one
multi-field observation. A later same-observation variant can place time, participant, relation, and
location fields under one source ancestry and test field degradation without multiplying source
count.

## 9. Field, source, and observation-family ablation

These are separate studies:

| Study axis | Manipulation | What remains | Valid interpretation |
|---|---|---|---|
| field ablation | degrade one information field to a partial/unknown state | source assertion, other fields, availability, ancestry | marginal value of that field under the specified rule, only if a weaker constraint is preserved |
| source ablation | make one independent public source unavailable | other sources and their fields | contribution of that source group at the cutoff |
| observation-family ablation | remove/deactivate every record and artifact from one collection mechanism or family | unrelated families | family-level contribution, including all bundled fields and derivations |

M1-F1 was not a true field-level minimum: its atomic all-field transformation removed both derived
constraints whenever any field was missing. Operationally, every incomplete profile behaved like
source-family removal. Its Full-only result must not be used as evidence that all four fields are
individually necessary or equally valuable.

## 10. Minimal-observation definitions

“Minimum observation set” should not be a single unqualified label.

### Field-level minimum

The smallest set of information fields that permits a specified public derivation or meets a fixed
candidate-burden criterion while unknown fields remain explicitly unknown and other source facts are
unchanged. This requires weak constraints, not atomic deletion.

### Source-level minimum

The smallest set of independent public source groups that reaches a preregistered candidate or
search-burden criterion with required safety. Multiple fields and multiple derived artifacts from one
source still count as one source. Supporting dependency and participant-location sources must remain
visible in the source graph.

### Decision-level minimum

The smallest public information/source set that supports a specified next rescue decision under an
explicit loss and resource model. Its threshold may involve locations to check, area/volume, actions,
time, staffing, equipment, or wrong-area cost. Candidate count alone cannot define it.

The definitions can be preregistered now. None can be estimated as a general result yet. Executable
field/source minima require the common composition and common-world steps; decision-level minima
also require a rescue-workload mapping.

## 11. Plateau classification

Use ordinary stage labels and separate the observed equality from its diagnosed representation
cause:

1. **Observation-to-constraint plateau**: the observation information state changes but the derived
   constraint predicate does not. F11's 78 participant-to-Region transitions are the current clear
   example; added participants contributed no new Region.
2. **Constraint-to-candidate plateau**: the derived predicate changes but the candidate ID set does
   not. F9's 92 worlds with widened time windows and F11's 45 Region-to-candidate transitions are
   examples.
3. **Candidate-domain or resolution cause**: classify why stage 2 occurred:
   - no candidate occupies the added time/Region portion (domain occupancy or density);
   - the candidate representation aggregates distinctions into the same state/Region ID
     (resolution collapse); or
   - the finite constraint already admits the complete candidate universe (saturation).
4. **Finite-versus-Unknown saturation**: a finite constraint and non-generation baseline return the
   same candidates while remaining different in derivation success, provenance, work, and source
   state. F2's broad profiles, F9's ten Broad=Unknown worlds, and F11's 81 Broad=Unknown worlds show
   this boundary.

F2 width 3→6 is primarily a target-candidate-domain limitation: Regions outside the target layer
cannot appear. “No performance difference” must always be accompanied by the stage and cause, never
translated directly to “information unnecessary.”

## 12. Common synthetic family necessity

A common family is required before comparing combination effects. F2, F9, and F11 differ in exactly
the dimensions that drive candidate reduction:

- F2 has a fixed three-Region target layer and S5's separately declared historical window;
- F9 has 5–12 candidates with unique Region IDs and variable ticks, so Region is deliberately
  non-discriminating;
- F11 has 6–14 candidates with Region collisions and every candidate at one encounter tick, so time
  is deliberately non-discriminating; and
- participant Region overlap, candidate density, Region universe size, and interval layout are drawn
  under different generators.

Putting their aggregate means in one table would measure world construction as much as observation
precision. One paired common universe must be generated once per seed and shared by all axis
conditions.

## 13. Common-world minimum requirements

After composition is defined, begin with one hand-checkable deterministic vertical slice. Its minimum
requirements are:

- one target Person;
- one explicit target historical proposition and candidate universe whose IDs distinguish relevant
  time-window and Region membership;
- multiple candidate ticks and multiple Regions, including preregistered candidates in incremental
  time bands and Region sets so propagation can be inspected without result-driven tuning;
- Exact/finite/Unknown location content with a direct public Region derivation;
- Exact/Medium/Broad/Unknown occurrence-time content using the unchanged F8 duration rule;
- Exact/Narrow/Broad/Unknown other-Person identity using the unchanged F10 conservative union;
- fixed exact `PersonPersonEncounter` in every profile;
- fixed public participant-location evidence and one public bounded-duration dependency;
- a declared source graph distinguishing common ancestry, independent observations, participant
  evidence, and duration input;
- all public evidence Confirmed, available, Person-only, truth-compatible, and CandidateGeneration;
- no conflict, missing evidence, private evidence, Device, traversal, or verification input;
- unknown field content retained independently from source absence;
- truth candidate selected/generated before public derivation and used only after reconstruction;
  and
- deterministic output with per-constraint, per-source-group, per-candidate, and plateau audits.

The initial combination conditions should use one fixed informative profile per axis and Unknown for
the disabled axis:

```text
none
Location
Time
Participant
Location + Time
Location + Participant
Time + Participant
Location + Time + Participant
```

“None” means all three public assertions remain present with the selected content unknown and no
axis-derived narrowing, not that no event or observation occurred. Finer precision levels can be
added only after the binary composition slice is validated.

The composition design must also resolve a current temporal coupling: F10's participant constraint
carries the encounter time, and Unknown identity removes the whole constraint. On a multi-tick
candidate universe that can alter time information along with participant identity. The common
fixture must either provide a typed, fixed non-discriminating temporal scope for participant
evidence, factor the fixed temporal premise from the participant-derived Region predicate, or reject
the profile as non-orthogonal. It must not simply ignore the constraint's time window.

## 14. Orthogonality analysis

| Manipulation pair | Classification | Reason / condition for a valid common study |
|---|---|---|
| Location input vs time input | independent manipulation possible | use the full Region domain for the time rule and a fixed/non-discriminating time scope for location; neither profile may rewrite the other's public inputs |
| Location input vs participant identity | input manipulation possible; outputs intentionally share Region | participant evidence stays fixed; union possible participants first, then intersect with location only when both claims address the same target state |
| Time input vs participant identity | **coupled by current implementation; semantic concurrence remains** | F10 carries same-time encounter applicability and Unknown removes a combined time/Region constraint; a dimension-aware composition boundary is required |
| Relation vs participant identity | inherently coupled as applicability, not as a studied axis | exact `PersonPersonEncounter` is required for the participant co-location rule and must remain fixed |
| Source ancestry vs field precision | independently representable | widening a field must retain its observation/source group; source removal is a separate condition |
| Candidate density/resolution vs observation precision | independently generatable, but affects observability | fix one candidate universe within a seed; vary representation only in a later sensitivity family |

Thus orthogonal public-input manipulation is feasible in principle, but not completely encoded by the
current constraint/reconstructor contracts. The largest immediate defect is composition metadata,
not the absence of another axis-specific 200-world result.

## 15. Purified-family and conflict plan

The initial common-world study should use the purified family proposed in the request:

- all public evidence truth-compatible;
- no missing evidence;
- no conflicting assertions;
- no Device participants/evidence;
- exact fixed encounter relation;
- one target;
- simple public candidate layer; and
- CandidateGeneration only.

This is appropriate because the first question is whether independently manipulated, correctly
derived predicates compose as intended. Mixing calibration errors, missingness, conflict resolution,
source correlation, topology incompleteness, or traversal would make a failure hard to locate and
would reuse policies that differ between the S5 reconstructor and tracer.

After the purified common-world invariants pass, add separate staged studies in this order:

1. source sharing/correlation while keeping claims compatible;
2. missing and unavailable observations;
3. conflicting public assertions with an explicit safe-outer policy;
4. candidate/world representation sensitivity;
5. verification and traversal as separately declared methods; and
6. calibrated observation processes and rescue decisions.

## 16. Research questions unlocked by a common world

The requested questions become valid under one paired candidate universe and explicit composition:

1. candidate reduction from Location only;
2. candidate reduction from Time only;
3. candidate reduction from Participant only;
4. conditional additional reduction from each two-axis pair;
5. reduction from all three axes;
6. loss when removing one axis from the three-axis condition;
7. which combinations saturate at observation→constraint or constraint→candidate stages;
8. where stricter composed predicates leave candidate IDs unchanged; and
9. whether different combinations reach the same candidate set and work through different
   provenance/source graphs.

These are synthetic-family candidate-reduction questions. Equal candidate counts should be checked
as set equality, and “additional value” should be reported conditionally rather than as a global
importance rank. A 200-world experiment is justified only after the deterministic composition and
common-world vertical slices pass.

## 17. Next single implementation

Choose **Recommend C: constraint-composition contract first**.

Keep it small: extend the fixture-sized CandidateGeneration path with a collection-based,
truth-free, deterministic composition envelope and one acceptance fixture. It should explicitly
represent:

- the common target proposition and candidate time/Region domain;
- jointly required predicates versus same-source alternatives and duplicates;
- Region × Time and Region × Region candidate predicates;
- source group and logical-combination relationship as independent metadata;
- explicit non-restricting/top scope per dimension or a declared complete fixture domain;
- unavailable/unknown non-participation without erasing the source assertion;
- safe conflict audit without empty false exclusion;
- CandidateGeneration-only usage; and
- order-invariant provenance, IDs, work, and candidate sets.

The acceptance cases should include one independent Region × Time conjunction, one compatible
Region × Region conjunction, same-source alternatives that union rather than intersect, same-source
joint fields that remain conjunctive without gaining an independence count, duplicate
canonicalization, unavailable input, and a conflict that preserves a safe outer result. This is not
a general rule engine.

### Why not the other candidates now

| Candidate | Decision | Reason |
|---|---|---|
| A common-world vertical slice | next after C | creating it now would silently choose AND/OR, top-dimension, same-source, and conflict semantics inside an evaluator |
| B direct common-world 200-seed generator | reject now | would scale an unresolved composition policy and make failures difficult to diagnose |
| D representation sensitivity first | defer | representation plateaus are real, but current simple candidate domains can still test composition; semantic combination precedes resolution tuning |
| E direct minimal-observation study | reject now | F1 was atomic removal, the single-axis families differ, and no current path produces all eight combinations on one paired universe |

After C, implement A as a deterministic common-world vertical slice. Only after both pass should the
eight paired combinations be expanded to a preregistered seed-varying family. That experiment can
estimate conditional synthetic-world candidate reduction, full-minus-one-axis loss, work, safety,
source counts, and stage-specific plateaus.

## 18. Artificial-world validity handoff

The common experiment must report how these representation choices affect observability:

- **Region granularity:** broad named Regions can collapse distinct locations or make added Regions
  irrelevant outside the candidate layer;
- **integer tick granularity:** expanded windows can contain no new representable tick/state, and
  interval hulls can hide disjoint alternatives;
- **candidate density:** sparse candidates create constraint-to-candidate plateaus; dense artificial
  candidates can exaggerate precision value;
- **participant evidence overlap:** identical/subset Region sets create legitimate
  observation-to-constraint plateaus;
- **candidate/state intervals:** overlap can admit states that only touch a boundary and can merge
  distinctions that point candidates would expose; and
- **candidate identity shape:** a simple `(window, Region)` hypothesis has no participant or relation
  output dimension, so those axes matter only through valid state/time predicates.

Do not tune density or granularity after viewing combination results. First preregister one simple
domain, then rerun the same semantic experiment under separately registered representation variants.
No immediate move to 2D/2.5D is required.

## 19. Staged complexity and rescue roadmap

The resulting sequence is:

```text
single-axis semantic paths (F2/F8-F12)
    -> minimal source-aware composition contract (next)
    -> deterministic common-world Location/Time/Participant slice
    -> preregistered common-world paired evaluation
    -> field/source/observation-family minimum studies
    -> candidate/world representation sensitivity
    -> missingness, conflicts, source correlation, topology limits
    -> calibrated and progressively realistic observations
    -> search-burden and rescue-decision models
    -> rescue action, new observation, and re-estimation loop
```

Candidate count remains an intermediate measure. Decision-level research must eventually translate
candidate hypotheses into candidate Regions/buildings/floors, search area or volume, check
locations, action count, expected search time, rescuers/equipment, wrong-area cost, and handoff to
existing rescue methods. Later closed-loop work must model rescue action → new observation → revised
estimate. None of those quantities can be inferred from current candidate counts.

## 20. Future Buddhist-methodology comparison

Only after the engineering primitives are explicit, a separate methodological comparison may ask
about conditions acting jointly, concurrence versus sequence, modeled dependency, several
conditions on one target, and forward versus reverse examination. Terms such as the 24 `paccaya`,
`anuloma`, or `paṭiloma` may be comparison sources, not production identifiers.

No multi-axis evidence combination is equated with a particular `paccaya`; the 24 conditions are
not assumed to be a complete basis; and no Buddhist terminology or newly coined category is added to
the simulator.

## 21. Claim boundaries

This integration can claim that:

- location, bounded-duration time, and participant identity each have one public, truth-free path
  capable of changing historical candidates in their own synthetic family;
- F9 and F11 preserve per-world monotonicity and safety under their fixed rules;
- F2, F9, and F11 all expose representation plateaus at identifiable pipeline stages;
- exact Encounter information can gate the existing co-location rule, while unknown kind safely
  withholds it;
- relation is suitable as a fixed prerequisite but not as a graded precision axis;
- the existing generic adapter does not compose multiple derived constraints; and
- current S5 composition cannot be adopted as a general multi-axis rule without additional logical
  metadata and candidate-time semantics.

It cannot claim that:

- one observation axis is generally more important than another;
- any F2/F9/F11 aggregate difference is comparable across world families;
- the eight axis combinations currently have valid measured effects;
- a field, source, or observation-family minimum has been found;
- a particular sensor, spatial accuracy, time accuracy, or participant count is sufficient;
- current Region/tick/candidate resolution is realistic;
- candidate reduction predicts discovery time or rescue success; or
- a doctrinal classification explains the engineering results.

## 22. Validation scope

This branch is documentation-only. It does not modify production semantics, experiments, scenarios,
tests, generated results, relation inventory, or the F8/F10/F12 rules. Validation consists of direct
source and milestone review, documentation link inspection, `git diff --check`, and the repository's
standard build/test/format/sample checks before handoff. No 200-seed evaluation is rerun because no
executable behavior changes.
