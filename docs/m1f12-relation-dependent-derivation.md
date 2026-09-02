# M1-F12 relation-dependent derivation vertical slice

## 1. Background and decision

M1-F6 found that S5 stores `PersonPersonEncounter` and free-text relation/derivation labels, but
does not dispatch from those values to historical narrowing. M1-F10/F11 then fixed the relation as
an exact encounter while validating participant-dependent Region union. M1-F12 adds one missing
link without changing S5 or inventing a relation library:

```text
public relation information
    -> relation-specific applicability
    -> existing same-time participant-location derivation
    -> AllowedRegion constraint
    -> historical candidate reconstruction
```

The vertical slice succeeds for `Exact(PersonPersonEncounter)` versus
`RelationExistsButKindUnknown`. The repository has no justified second formal relation, relation
category, or compatibility taxonomy, so finite relation alternatives and Narrow/Broad profiles are
not implemented.

## 2. Existing relation inventory

The implementation and validator audit found one formal relation kind:

| layer | representation | arity / roles | temporal and spatial meaning | executable use |
|---|---|---|---|---|
| truth occurrence | `InteractionRelationKind.PersonPersonEncounter` | exactly two known Persons; both `EncounterParticipant` | one event tick and one Region | `InteractionOccurrenceProjector.FromPersonPersonEncounter` validates an `Encounter` event |
| public interaction observation | exact `InteractionRelationKind` | one or more normalized participants; no relation-specific public arity validator | observed window and optional Regions are carried | S3 named derivation checks exact encounter before same-time co-location |
| S3 derivation | `person-person-encounter-co-location-v1` | target must be a Person | observed time and Regions become constraint time and Regions | genuine relation-specific named dispatch |
| S5 constraint | nonempty free-text `RelationKind` and `DerivationKind` | separately declared participants | historical window and Regions are scenario literals | label affects ID/provenance/audit, not narrowing payload |
| F10/F11 derivation | exact `PersonPersonEncounter` fixed | exact/alternative other Persons | same-time participant public Regions are unioned | participant-dependent constraint construction |

Formal participant roles contain only `EncounterParticipant`. The encounter is effectively
symmetric in current data: both endpoints receive the same role and no directionality contract is
defined. The truth projector is stricter than the general public observation factory. The S5
validator still treats its relation strings as nonempty labels rather than registered semantic
rules.

The formal inventory therefore contains exactly:

```text
InteractionRelationKind = { PersonPersonEncounter }
```

Names such as `later-public-trace`, `later-public-trace-derived`,
`later-trace-to-target-window`, and `same-record-secondary-derivation` remain free-text addressing,
provenance, telemetry, or audit labels. They are not alternatives to `PersonPersonEncounter` and do
not trigger this rule.

## 3. Relation information contract

`ObservedRelationInformation` separates relation existence from exact relation identity:

| state | representation | relation assertion present | exact kind available |
|---|---|---:|---:|
| Exact | `Exact(PersonPersonEncounter)` | yes | yes |
| kind unknown | `ExistsButKindUnknown()` | yes | no |

`PublicRelationObservation` carries exact target and other endpoint identities independently from
the relation information. The acceptance slice is structurally binary (`Arity = 2`) and fixes:

- target `person-a`, Person;
- other participant `person-b`, Person;
- occurrence time `[10,10]`;
- public participant-location evidence `person-b -> {east}` at `[10,10]`;
- Confirmed state, availability tick 11, and CandidateGeneration usage.

There is no `Absent` relation-information state. Kind unknown means that a binary public relation
assertion exists but its kind cannot be established. It does not mean that no relation was observed
or occurred.

Finite alternatives are deliberately absent. Adding a set containing one real relation and one
invented relation would manufacture the result. A future alternatives contract also needs
conservative behavior when any possible relation cannot establish a constraint; current top/unknown
output is represented by constraint non-generation.

## 4. Relation-specific derivation dispatch

The relation applicability rule ID is the existing S3 engineering identifier:

```text
person-person-encounter-co-location-v1
```

The wrapper requires:

1. a present, public, Confirmed relation observation available by the cutoff;
2. exact public relation information equal to `PersonPersonEncounter`;
3. two exact, distinct Person endpoints;
4. an exact occurrence window;
5. public same-time location evidence for the exact other Person; and
6. `CandidateGeneration` usage.

Only after those checks pass does it construct an exact one-participant F10 observation and call the
unchanged `encounter-participant-location-union-v1` rule. With one possible participant, the F10
union is that participant's public Region set:

```text
Exact(PersonPersonEncounter)
+ exact other person B
+ public AllowedRegions(B, T)
    -> encounter rule applicable
    -> AllowedRegions(target, T) = AllowedRegions(B, T)
```

This makes relation kind operationally control rule applicability rather than merely changing an ID
or provenance string. The output constraint retains F10 as its construction rule ID and carries the
relation applicability rule in provenance. The relation audit records both rule IDs, the outer
relation observation, the internal F10 observation, used public inputs, output Regions, source
groups, and the generated constraint ID.

For kind unknown:

```text
relation assertion exists + kind unknown
    -> exact Encounter applicability cannot be established
    -> UnknownRelationKind
    -> no relation-derived constraint
```

It is not inferred that the relation was not an encounter. Encounter-specific narrowing is withheld
because public information does not justify it. An undefined enum value used by a defensive test is
reported as `UnsupportedRelationKind`; it is not added to the formal inventory and does not fire the
encounter rule.

Other failures include public visibility/availability/state failures, endpoint entity-kind mismatch,
missing participant evidence, and wrong usage role. Device endpoints or Device evidence are not
promoted to Person evidence. Missing participant evidence does not produce a partial or default
Region constraint.

## 5. Public inputs, truth boundary, and provenance

The deriver accepts only:

- `PublicRelationObservation`;
- `PublicParticipantLocationEvidence`;
- an evidence-availability cutoff; and
- an evidence usage role.

It has no truth occurrence, truth relation, realized trajectory, hidden relation, evaluation result,
or free-text relation-label input. The exact profile is exact because its public observation content
says so. The fixture's `TruthRelationReference` is evaluation-only metadata and is never passed to
the deriver. Changing that reference leaves rule applicability, observation-derived constraint ID,
Allowed Regions, and candidate IDs unchanged.

Successful constraint provenance includes:

- the public relation evidence record;
- the outer relation observation ID;
- `person-person-encounter-co-location-v1`;
- the internal F10 public observation;
- `encounter-participant-location-union-v1`; and
- the public participant-location evidence record/input ID.

Exact and Unknown profiles have different observation content and therefore different observation
IDs, but retain one shared relation source group derived from common source ancestry. Unknown does
not erase that group. The supporting participant-location record retains its own independent source
group. No relation alternative expansion exists, and one relation assertion is never counted as
multiple independent observations.

## 6. Acceptance fixture and results

The deterministic fixture is
`experiments/m1f12_relation_dependent_derivation/fixture.json`. It has three target candidates at
tick 10: `east` (evaluation truth), `west`, and `north`. The target, other participant, time,
location evidence, availability, evidence state, candidate layer, and usage role are identical in
both profiles. Only relation information changes.

| profile | public relation information | rule applied | derived Regions | candidates | reduction vs Unknown | containment | false exclusion | generation work |
|---|---|---:|---|---:|---:|---:|---:|---:|
| Exact | `Exact(PersonPersonEncounter)` | yes | `{east}` | 1 | 2 (0.6667) | 1/1 | 0 | 4 |
| Unknown | relation exists, kind unknown | no | none; constraint not generated | 3 | 0 | 1/1 | 0 | 3 |

The Exact candidate set is a subset of Unknown, with zero monotonicity violations. Unknown retains
`RelationObservationPresent = true` and one relation source group. Exact inspects one generated
constraint in addition to the three candidate states; Unknown has no relation-derived constraint to
inspect.

Run the fixture with:

```bash
dotnet run --project src/FootprintSim.Cli -- \
  evaluate-relation-dependent-derivation \
  --fixture experiments/m1f12_relation_dependent_derivation/fixture.json \
  --output results/m1f12-relation-derivation
```

The machine-readable outputs are:

- `relation-dependent-derivation.json`;
- `relation-dependent-derivation.csv`; and
- `relation-dependent-derivation-manifest.json`.

They record the relation information state, dispatch decision, failure reason, derived Regions,
candidate IDs, safety, work, provenance audit references, and source-group counts.

## 7. Failure and safety behavior

The slice rejects these shortcuts:

- filling kind unknown with hidden `PersonPersonEncounter` truth;
- firing from an S5 free-text label;
- applying encounter semantics to an unsupported formal value;
- changing participant, time, or participant-location evidence between profiles;
- converting unknown relation kind to relation absence;
- promoting Device evidence/endpoints to Person;
- using missing evidence as a partial narrowing result;
- using the constraint for verification or traversal;
- omitting relation observation/rule provenance; or
- generating truth-dependent or nondeterministic IDs.

Focused tests cover Exact, Unknown, unsupported values, missing evidence, Device boundaries,
CandidateGeneration-only usage, provenance, source independence, hidden-truth invariance,
fixed-axis comparisons, containment, false exclusion, set monotonicity, schema rejection of directly
declared outputs, and deterministic serialization. The full suite preserves F8/F9 time behavior,
F10 `1 -> 2 -> 3 -> 4`, F11 200-world results/invariants, and existing S5 behavior.

## 8. Limitations and S5 boundary

This result establishes one semantic path, not general relation importance. It does not provide:

- a meaningful second relation;
- relation categories, hierarchy, compatibility, or finite alternatives;
- conservative merging across different relation rules;
- probabilistic relation classification;
- population or 200-seed effect sizes;
- a rule for S5's `[24,24] -> [13,14]` window; or
- comparison with location, time, participant identity, or rescue performance.

S5/later-trace-a remains unchanged. Its free-text labels still do not select historical Region/time
payloads, and this same-time fixture cannot justify S5's independent historical window.

The acceptance result supports only this claim:

> In this synthetic fixture, an exact public `PersonPersonEncounter` assertion permits the existing
> same-time co-location derivation, while a present relation assertion with unknown kind safely
> withholds that encounter-specific narrowing.

It does not show how important relation identity is in real worlds, how many relation alternatives
are sufficient, whether a taxonomy is useful, whether S5 is migratable, or how candidate reduction
maps to rescue performance.

## 9. Next recommendation

The recommendation is **Mixed B/E, led by Recommend B**:

- **B:** stop the relation-specificity track at Exact versus Unknown for now. The vertical slice is
  valid, but no repository-grounded second relation or taxonomy exists. A 200-world binary replay
  would mainly measure candidate density under one rule, not broader relation specificity.
- **E:** integrate the now-connected location, bounded-time, participant, and exact/unknown relation
  results into the next research decision. Resume relation alternatives only when artificial-world
  representation supplies a genuinely different occurrence type with explicit arity, temporal,
  spatial, and constraint semantics.

Recommend A is unsupported because there are no justified alternatives. Recommend C is lower
priority because its semantic question is currently binary and already demonstrated; world-family
sensitivity can be added later if a specific plateau question is preregistered. Recommend D is not
needed because no safety or monotonicity defect was found.

Future Buddhist-methodology comparison may examine already-separated primitives such as condition
type, concurrence, sequence, direction, support, and dependency. It must not equate
`PersonPersonEncounter` directly with a `paccaya`, introduce Buddhist terms into production names,
or assume a complete 24-condition basis.
