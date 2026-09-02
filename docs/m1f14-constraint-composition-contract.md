# M1-F14 source-aware CandidateGeneration constraint composition

## 1. Background from M1-F13

M1-F13 found that location, bounded-duration time, and participant-derived Region constraints each
reach historical candidates in a dedicated synthetic family, but the shared fixture reconstructor
accepted at most one `ObservationDerivedConstraint`. S5 did accept several constraints, but used
source grouping as part of its Region Boolean policy: union within one group and intersection across
groups. That policy cannot distinguish jointly asserted fields, alternative interpretations,
equivalent support, or conflicts.

M1-F14 adds one small composition path for already-justified public CandidateGeneration predicates.
It does not replace S5, change F8/F10/F12 rules, add a general solver, or run a multi-world
performance experiment.

## 2. Source relation and logical relation are separate

The contract carries two independent structures:

- source metadata answers where an input came from: source observation, source assertion, source
  group, ancestry, public input IDs, derivation rule, and provenance;
- `CandidateConstraintLogicalRole` answers how candidate predicates in a declared group compose.

The composer never selects a logical role from source IDs. Same-source fields can be joint. Separate
sources can be alternatives. Source-group count therefore remains an audit quantity and never acts
as an AND/OR operator or as an automatic support multiplier.

## 3. Minimal composition contract

`CandidateGenerationConstraintCompositionRequest` declares:

- scenario and seed;
- target entity;
- target-state/query context ID;
- candidate-domain ID;
- evidence-availability cutoff;
- CandidateGeneration usage;
- public candidate states; and
- an explicitly structured collection of logical groups.

Each `CandidateGenerationConstraintComponent` declares the same target/context/domain plus:

- input state;
- a Region/time predicate with an explicit restricted or unrestricted state per dimension;
- component and source-constraint IDs;
- source assertion, observation, group, and ancestry;
- derivation rule and public input IDs;
- availability, evidence state, visibility, and usage; and
- normalized provenance.

The composer rejects mismatched scenario, seed, target, context, domain, usage, visibility, evidence
state, availability, predicate state, or group shape as `Invalid`. An invalid result exposes no
candidate set that could be mistaken for a valid search result.

## 4. Joint requirement

`JointRequirement` means every active member predicate in that logical group must hold for one
candidate. All top-level active groups are also jointly required. This gives ordinary conjunction
only where the caller has explicitly declared that the constraints address the same target
proposition and are simultaneously required.

Source grouping does not establish this role. The contract test includes Region and Time fields
sharing one source group: both are applied with AND while the distinct source-group count remains
one.

## 5. Alternative

`Alternative` means at least one member predicate may hold. The group evaluates OR over its
normalized branches. A contract test uses independent east and west sources and produces the union
of east and west candidates rather than their empty intersection. Thus independent sources are not
automatically ANDed.

The preferred production path remains derivation-side resolution. F10 already turns possible
participants into one conservative Region union before composition. M1-F14's acceptance profile
passes that single unioned P constraint, not separate B and C constraints.

An Alternative group that mixes an active predicate with an unknown or unavailable branch is
`Invalid`; the composer does not keep only the known branch as an unsafe partial union. The
derivation layer must first produce a complete conservative predicate or a non-restricting result.

## 6. Duplicate and equivalent support

`EquivalentSupport` requires two or more components with the same normalized predicate. The
predicate is applied once. All component/source/provenance records remain in the audit, while
`DuplicateConstraintsSuppressed` records the removed filtering work. Claiming equivalence for
different predicates is `Invalid`.

Repeated references to the exact same component ID are canonicalized even in another logical role.
They do not increase the active constraint count or the distinct source-group count.

## 7. Non-restricting, unknown, unavailable, and absence

Region and Time each have an explicit `Unrestricted` value. It is not an empty Region set, an empty
time interval, or a false predicate.

`CandidateConstraintInputState` distinguishes:

- `Unknown`: a public assertion is present but does not provide an active predicate;
- `Unavailable`: the source exists but becomes available after the cutoff;
- `ExplicitNoRestriction`: the input explicitly contributes top/no restriction; and
- `Applicable`: a confirmed, public, cutoff-available predicate is active.

These first three states belong to `NonRestricting` groups and retain their source records in the
audit. An absent input is represented by no component at all. Both no components and only valid
non-restricting components return the entire candidate universe with status `NonRestricting`.
Neither means that the observed event or relation was absent.

Marking a future component `Applicable` is invalid; it is never silently consumed. Missing or
failed derivation similarly does not create a false predicate or an empty candidate set.

## 8. Common target and candidate domain

Composition is valid only when every component matches the request's:

- scenario and seed;
- target entity;
- target-state/query context; and
- candidate domain.

The fixture domain is a deterministic collection of `(closed time interval, Region)` candidates.
Target/context/domain IDs are part of component and composition identities. Constraints for another
person, another historical proposition, or another domain are rejected rather than conjoined.

The first contract does not prove that a caller's domain declaration is physically complete. That
remains a scenario/world validation responsibility.

## 9. Region × Time

`CandidateConstraintPredicate` applies:

```text
(Region unrestricted OR candidate.Region in AllowedRegionIds)
AND
(Time unrestricted OR candidate.TimeWindow overlaps ConstrainedTimeWindow)
```

Time intervals remain closed/inclusive and reuse the existing overlap rule. The acceptance L
predicate is `Region in {east, west}` with Time unrestricted. The unchanged F8 rule derives
`[10,11]`; the composition component explicitly retains Time and makes Region unrestricted.

For explicitly Joint L and T inputs, candidates must satisfy both predicates. Reversing their input
order produces the same candidate IDs, composition ID, work, and normalized provenance.

## 10. Region × Region

The location predicate is:

```text
L: Region in {east, west}
```

F10 first evaluates the two possible encounter participants:

```text
person-b -> {west}
person-c -> {north}
P = {west} union {north} = {north, west}
```

P is then represented as a Region-only component. For explicitly Joint L and P, the candidate
predicate is equivalent to `Region in {west}`. The composer never evaluates
`L AND B-region AND C-region`.

Selecting Region-only or Time-only from an existing derived constraint is an explicit conservative
projection: the other dimension becomes unrestricted and the selected predicate is included in the
component's deterministic identity. The underlying F8/F10 derivation output and rule ID are not
changed.

## 11. Source independence

The tests fix both directions of the boundary:

- same source, different Region and Time fields, `JointRequirement`: AND applies, source-group count
  remains one;
- independent east and west sources, `Alternative`: OR applies, source-group count is two.

F10's encounter observation and participant-location inputs remain traceable in the component
ancestry/provenance. The top-level component's `SourceGroupId` is not a claim that every supporting
record is one independent observation. M1-F14 deliberately does not estimate statistical evidence
independence.

## 12. Provenance

Every component audit preserves:

- component and source-constraint IDs;
- derivation rule ID;
- public input IDs;
- source assertion and observation IDs;
- source group and ancestry IDs;
- normalized provenance references;
- logical group ID and role; and
- active/non-restricting input state.

The result separately reports distinct source groups, input components, active constraints,
predicates, duplicates, and alternative branches. Three components therefore do not imply three
independent observations.

## 13. Usage boundary

The request and every active component must be `CandidateGeneration`. CandidateVerification,
Traversal, and EvaluationOnly inputs return `Invalid` and are not applied. Public visibility,
Confirmed evidence state, and cutoff availability are required for an active component.

The composer constructs and filters candidates only. It performs no verification, traversal,
truth evaluation, or score calculation. Its API contains no truth occurrence, truth participant,
realized trajectory, or evaluation result.

## 14. Conflict and unsatisfied composition

The acceptance fixture is conflict-free. The contract nevertheless distinguishes a valid active
composition that matches no candidate:

```text
Status        = Unsatisfied
FailureReason = UnsatisfiedJointRequirements
Candidates    = []
```

There is no silent fallback to base candidates. Callers must inspect the status and must not treat
an `Invalid` result as a valid empty search result. Selecting a safe outer result, stopping a query,
or resolving contradictory public assertions is intentionally not decided here; the S5 fallback is
not generalized.

## 15. Deterministic normalization

Components are normalized by component ID and groups by group ID. Region IDs, public inputs,
ancestry, provenance, source observations, source groups, and rule IDs are sorted and deduplicated.

The composed ID is derived only from:

- scenario/seed;
- target, target-state context, and candidate domain;
- usage role;
- evidence-availability cutoff; and
- normalized logical role/group structure.

It contains no truth, current time, random ID, output path, or caller input order. The fixture runs
every profile in forward and reverse constraint order and fails if ID, status, candidates, or
normalized provenance changes.

## 16. Acceptance fixture

The fixture is intentionally purified:

- one target `person-a`;
- Person-only participants `person-b` and `person-c`;
- exact fixed `PersonPersonEncounter`;
- all evidence public, Confirmed, and available;
- no missing participant evidence, private input, misidentification, or conflict;
- one F8 bounded-duration dependency;
- one F10 participant union; and
- six hand-checkable candidate states.

Candidate universe:

| Tick | Region | Truth |
|---:|---|---|
| 10 | west | yes |
| 10 | east | no |
| 10 | north | no |
| 11 | south | no |
| 12 | west | no |
| 12 | east | no |

The fixture file declares inputs and eight profile memberships, not candidate results. Unknown JSON
members are rejected so a scenario cannot directly declare the desired output.

## 17. Eight-profile functional results

| Profile | Explicit predicates | Candidates | Count | Reduction vs None | Generation work |
|---|---|---|---:|---:|---:|
| None | none | all six | 6 | 0 | 6 |
| L | Region `{east, west}` | `(10,west)`, `(10,east)`, `(12,west)`, `(12,east)` | 4 | 2 | 12 |
| T | Time `[10,11]` | `(10,west)`, `(10,east)`, `(10,north)`, `(11,south)` | 4 | 2 | 12 |
| P | Region `{north, west}` | `(10,west)`, `(10,north)`, `(12,west)` | 3 | 3 | 12 |
| L + T | L AND T | `(10,west)`, `(10,east)` | 2 | 4 | 18 |
| L + P | L AND P | `(10,west)`, `(12,west)` | 2 | 4 | 18 |
| T + P | T AND P | `(10,west)`, `(10,north)` | 2 | 4 | 18 |
| L + T + P | L AND T AND P | `(10,west)` | 1 | 5 | 24 |

All four joint profiles exactly equal the set intersection of their single-axis candidate sets.
This equality is expected only because every component was preregistered as a Joint requirement for
the same target proposition. It is not a rule to intersect arbitrary evidence.

## 18. Safety and work telemetry

The evaluation-only truth candidate `(10,west)` is selected after the None candidate universe is
constructed. It is not passed to a deriver, component factory, composer, or reconstructor.
Changing only that evaluation address leaves component predicates, composition IDs, candidate IDs,
work, and composition audits unchanged.

Results:

- containment: 8/8 profiles;
- false exclusions: 0;
- intersection expectation violations: 0; and
- input-order violations: 0.

Work is defined for this fixture as candidate-universe generation plus exhaustive normalized
candidate-predicate checks. The audit separately reports:

- input components received;
- active constraints received;
- normalized predicates applied;
- candidate-predicate checks;
- duplicate constraints suppressed;
- alternative branches evaluated; and
- distinct source groups.

It excludes F8/F10 derivation work and is not calibrated search or rescue effort.

## 19. Failure and contract tests

The focused tests cover:

- Region × Time and Region × Region Joint behavior;
- F10 union before P composition;
- independent-source Alternative union;
- same-source Joint conjunction without an extra source count;
- equivalent support and exact repeated-artifact suppression with provenance retained;
- unknown, unavailable, explicit top, and no-component behavior;
- explicit `Unsatisfied` with no fallback;
- target, state-context, candidate-domain, usage, visibility, and availability rejection;
- false equivalence rejection;
- mixed known/unknown Alternative rejection rather than partial union;
- truth-free API shape;
- fixture output non-declaration;
- order-independent identity, candidates, and provenance; and
- deterministic JSON/CSV/JSONL/manifest writing.

## 20. Output artifacts and reproduction

Run:

```bash
dotnet run --project src/FootprintSim.Cli -- evaluate-constraint-composition \
  --fixture experiments/m1f14_constraint_composition/fixture.json \
  --output results/m1f14-constraint-composition
```

The output directory contains:

- `constraint-composition-summary.json`;
- `constraint-composition-profiles.csv`;
- `constraint-composition-audit.jsonl`; and
- `constraint-composition-manifest.json`.

No generated result is committed. Clean-commit reproduction runs write to separate temporary
directories and compare all four artifacts byte-for-byte.

## 21. Regressions and unchanged boundaries

M1-F14 retains:

- F8 acceptance `2 -> 3 -> 4 -> 4`;
- F9 200-world monotonicity and safety tests;
- F10 acceptance `1 -> 2 -> 3 -> 4`;
- F11 200-world monotonicity and safety tests;
- F12 Exact-versus-Unknown relation applicability;
- S0-S5 sample execution; and
- the existing full suite.

S5's reconstructor and tracer are unchanged. This contract makes no claim that S5's source-group
policy is generally valid.

## 22. Limitations and claim boundary

M1-F14 can claim only that this purified deterministic fixture safely composes explicitly declared
Joint Location, Time, and participant-derived Region predicates, while separately representing
Alternative, EquivalentSupport, and NonRestricting behavior.

It cannot establish:

- general relative value of L/T/P;
- a minimum field or independent-source set;
- performance under missingness, source correlation, or conflicts;
- final conflict fallback policy;
- realistic Region/tick/candidate distributions;
- sensor requirements or calibration;
- rescue search time or success; or
- general validity of S5 multi-group composition.

## 23. Next recommendation

**Recommend A:** build one deterministic common-world vertical slice in which Location, Time, and
Participant information can be independently changed while using this composition contract.

The semantic gap identified by F13 is closed for the purified CandidateGeneration path: common
target/domain validation, explicit dimension tops, logical roles, source-independent Boolean
selection, provenance, usage rejection, order normalization, and unsatisfied status are executable.
The remaining limitations are deliberately staged conditions rather than blockers for one
truth-compatible common-world slice. A 200-seed multi-axis experiment should still wait until that
next vertical slice passes.
