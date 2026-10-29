# M1-F10 participant ambiguity derivation

## 1. Background from F5, F8, and F9

M1-F5 established that S5 records the exact other participant `person-b`, but does not use that
identity to construct `AllowedRegionIds` or the historical time window. S5 also has no contract for
a finite identity set or identity-unknown participant. Merely hiding `person-b` in `later-trace-a`
would therefore measure neither participant specificity nor ambiguity propagation.

M1-F8 introduced a minimal observation-to-constraint envelope: an explicit rule ID, declared input
requirements and applicability, public-input references, conservative uncertainty propagation,
provenance, a failure reason, a deterministic derived-constraint ID, and a fixed evidence usage
role. M1-F9 retained that rule while varying synthetic worlds. M1-F10 reuses the generic
`ObservationDerivedConstraint` and `ObservationDerivedHistoricalCandidateReconstructor`, together
with the same audit requirements. It adds only participant-specific input and audit contracts.

This milestone is the F8-equivalent vertical slice for participant identity. It does not run a
200-seed participant experiment and does not modify S5.

## 2. Semantic basis fixed before evaluation

The rule reuses the existing S3 meaning of `PersonPersonEncounter`: the target person and the actual
other person are co-located at the reported encounter time. M1-F10 combines that fixed relation with
separate public location evidence about each possible other person:

```text
public encounter observation:
  target A encountered actual other participant P at fixed time T

public participant information:
  P is one member of PossibleParticipants

public participant-location evidence:
  for every p in PossibleParticipants, p is in AllowedRegions(p) at T

derived target constraint:
  target A is in union(AllowedRegions(p)) at T
```

The union is justified by co-location. It is not an arbitrary participant-to-Region lookup: each
Region set is a separately identified, public, time-applicable participant-location evidence record.
The participant identifier selects which public evidence can apply. The fixed relation supplies the
same-time co-location implication. The rule does not infer where a person is without such evidence.

Rule ID:

```text
encounter-participant-location-union-v1
```

The production method has no truth participant, truth occurrence, realized trajectory, private
data, or evaluation result parameter.

## 3. Participant ambiguity contract

`OtherParticipantInformation` represents the observed information about the other participant,
separately from the fixed target:

| state | entity kind | possible participant IDs | meaning |
|---|---|---|---|
| `Exact` | explicit | exactly one | the other participant identity is known |
| `FiniteAlternatives` | explicit | at least two, canonicalized | the other participant is one member of this public finite set |
| `UnknownIdentity` | explicit | empty | an encounter was observed and the other participant's identity is unknown |

The acceptance fixture is Person-only. `UnknownIdentity(Person)` therefore retains person-kind
knowledge while withholding identity. It does not represent kind unknown. A device-valued other
participant or device location evidence fails applicability; neither is promoted to person evidence.

Exact and finite alternatives are distinct states. The deriver consumes only the declared public
IDs. It has no field into which a hidden truth participant can be injected.

## 4. Public encounter and participant evidence

`PublicEncounterParticipantObservation` records:

- deterministic observation ID;
- scenario and seed;
- encounter evidence source and common ancestry;
- fixed target ID and entity kind;
- exact relation kind `PersonPersonEncounter`;
- exact fixed encounter window `[10,10]` in the acceptance fixture;
- the other-participant information state;
- availability, evidence state, visibility, and provenance.

The observation object always means that an encounter observation is present.
`UnknownIdentity` does not remove this object.

Each `PublicParticipantLocationEvidence` records:

- deterministic public input ID;
- participant ID and entity kind;
- applicable time window;
- allowed Region set;
- availability tick;
- evidence-boundary state and public/private visibility;
- usage role;
- source evidence ID, ancestry, source group, and provenance.

The acceptance fixture provides one confirmed, public, CandidateGeneration record for each of
`person-b`, `person-c`, and `person-d`. All three cover the same fixed tick. Their Region sets and all
other evidence fields remain unchanged across profiles.

## 5. Required inputs and applicability

The machine-readable derivation audit declares these required input categories:

- encounter observation;
- fixed target entity;
- other-participant information;
- exact encounter relation;
- encounter time window;
- participant-location evidence;
- evidence availability, visibility, and state;
- source ancestry;
- requested usage role.

The rule applies only when:

1. the encounter observation remains present, public, confirmed, and available by the cutoff;
2. relation kind is `PersonPersonEncounter`;
3. the target and all possible other participants are persons;
4. the other identity is exact or a non-empty finite set;
5. exactly one public location evidence record exists for every possible participant;
6. every record matches scenario and seed, is confirmed and available, and covers the complete
   encounter window;
7. every record is explicitly usable for `CandidateGeneration`;
8. the requested output usage is `CandidateGeneration`.

The initial vertical slice deliberately rejects multiple location records for one participant,
because no within-participant evidence-composition rule is defined yet. This is a conservative
contract limit, not a claim that multiple evidence sources are invalid.

## 6. Conservative union and uncertainty propagation

For a finite public identity set `P`:

```text
AllowedRegions(target) = union(AllowedRegions(p) for p in P)
ConstrainedTimeWindow = EncounterTimeWindow
```

For example:

```text
person-b -> {east}
person-c -> {west}
PossibleParticipants = {person-b, person-c}
output = {east, west}
```

The rule never intersects alternative-participant Regions and never chooses the evaluation truth
participant. If identity sets are nested and participant evidence is fixed, the derived Region set
cannot shrink as identity uncertainty grows. The reconstruction adapter consequently checks the
candidate-set inclusion relation as well.

## 7. Missing-evidence behavior

The current constraint representation has no explicit top Region value. If even one possible
participant lacks a usable same-time location record, selecting only the known participants' Regions
could exclude a valid truth state. The rule therefore generates no constraint and reports a specific
failure, including:

- missing or multiple participant-location records;
- scenario, seed, time, or entity-kind mismatch;
- non-public or unavailable evidence;
- unsupported evidence state or usage role.

This is fail-safe non-generation, not evidence that the encounter did not occur. `UnknownIdentity`
similarly returns `UnknownParticipantIdentity` and no participant-derived narrowing.

## 8. Provenance and source independence

A successful constraint traces:

- the encounter source observation and evidence record;
- `encounter-participant-location-union-v1`;
- each used participant-location public input and its evidence source;
- all contributing source ancestry IDs.

The constraint ID hashes only public derivation inputs and is deterministic. The evaluation-only
truth participant and truth candidate are absent from the derivation signature.

The possible-participant branches do not become separate constraints. Exact, narrow, and broad
profiles each produce at most one derived constraint and retain the same encounter source-group ID.
The independently sourced participant-location records keep their own source-group IDs in the
audit. Thus one encounter observation expanded to three alternatives remains one encounter source,
while the three public supporting records remain separately traceable.

## 9. Evidence usage boundary

The derived constraint is accepted only as `CandidateGeneration`. The reconstruction adapter uses it
only to select same-time candidate states whose Region is in the derived union. The rule rejects
`CandidateVerification`, `Traversal`, and `EvaluationOnly`. No automatic promotion to another usage
role occurs.

## 10. Deterministic acceptance fixture

The fixture is
`experiments/m1f10_participant_ambiguity_derivation/fixture.json`. It contains:

- target `person-a`;
- one exact `PersonPersonEncounter` at `[10,10]`;
- three possible other persons with fixed public location evidence:
  - `person-b -> {east}`;
  - `person-c -> {west}`;
  - `person-d -> {north}`;
- four same-time target candidates in `east`, `west`, `north`, and `south`;
- evaluation-only truth participant `person-b` and truth candidate `east`;
- fixed availability, evidence state, relation, time, candidate layer, and public evidence across all
  profiles.

Only the other-participant information state changes:

| profile | possible identities | derived Regions | constraint | candidates | reduction vs Unknown | containment | false exclusion |
|---|---|---|---|---:|---:|---:|---:|
| Exact | `{person-b}` | `{east}` | generated | 1 | 3 (0.75) | 1/1 | 0 |
| Narrow | `{person-b, person-c}` | `{east, west}` | generated | 2 | 2 (0.50) | 1/1 | 0 |
| Broad | `{person-b, person-c, person-d}` | `{east, north, west}` | generated | 3 | 1 (0.25) | 1/1 | 0 |
| Unknown | identity unknown; person kind retained | no derived set | not generated | 4 | 0 | 1/1 | 0 |

Candidate-generation work from the reconstruction adapter is `5, 5, 5, 4`: four initial candidates
plus one generated constraint inspection for each finite profile. The participant derivation audits
evaluate `1, 2, 3, 0` alternatives. The fixed three-record public evidence corpus is inspected once
for every finite profile; unknown identity fails before consulting participant-location records.

The acceptance artifact records participant possibilities, branch audits, public input IDs, output
Regions, candidate IDs, containment, false exclusion, work, failure, and source groups. Run it with:

```bash
dotnet run --project src/FootprintSim.Cli -- \
  evaluate-participant-ambiguity-derivation \
  --fixture experiments/m1f10_participant_ambiguity_derivation/fixture.json \
  --output results/m1f10-participant-derivation
```

The deterministic outputs are:

- `participant-ambiguity-derivation.json`;
- `participant-ambiguity-derivation.csv`;
- `participant-ambiguity-derivation-manifest.json`.

## 11. Safety and monotonicity results

The three adjacent checks all pass:

```text
Exact candidates subset Narrow candidates
Narrow candidates subset Broad candidates
Broad candidates subset Unknown candidates
```

The participant information and derived Region checks are also monotone, for zero total violations.
Every profile contains the fixed truth candidate; false exclusion is zero. Changing only the
evaluation truth participant leaves observation IDs, derived constraint IDs, Regions, and candidate
sets unchanged. Reflection tests also verify that the deriver exposes no truth, realized, or hidden
identity input.

Failure-path tests confirm that missing evidence prevents a partial union, device evidence is not
promoted, identity unknown preserves the encounter observation, non-generation is used instead of
absence, provenance is complete, IDs are deterministic, and the CandidateGeneration boundary is
enforced.

## 12. Failure conditions fixed for this slice

The following are treated as failures, not accepted shortcuts:

- choosing a hidden truth identity from an ambiguous set;
- intersecting alternative Region constraints;
- ignoring a possible participant with missing evidence;
- converting unknown participant to no encounter;
- treating device evidence as person evidence;
- counting possible identities as independent encounter observations;
- including truth or realized state in derivation inputs or IDs;
- changing relation, time, target, or participant-side Region precision with the profile;
- declaring derived Regions or candidate results in the fixture;
- using the output as verification or traversal evidence;
- omitting provenance or producing nondeterministic output.

## 13. Limitations and S5 boundary

This fixture validates one semantic path, not general identity reasoning. Current limits include:

- Person-only other participants; kind-unknown is not represented;
- deterministic finite alternatives, with no probabilities or learned resolution;
- exact fixed encounter time and exact fixed relation kind;
- one location evidence record per participant;
- fail-safe non-generation rather than an explicit top Region constraint;
- one hand-checkable candidate layer and no world-family distribution;
- no independent estimate of identity resolution cost;
- no relation ambiguity or temporal uncertainty interaction.

S5 and `later-trace-a` remain unchanged. Their existing historical narrowing still has no
participant-dependent semantic rule. Applying this rule to S5 would require new, justified
same-time participant-location evidence and would not justify S5's separately declared historical
window.

## 14. Contract reuse

The shared derived-constraint, provenance, deterministic-ID, failure, usage-role, and reconstruction
envelope can support future axes. The participant-specific information and branch audit are not
forced into F8's time-specific fields. A future relation or location slice can supply a different
input contract and conservative output construction while retaining the same public/truth and usage
boundaries.

## 15. Next recommendation

**Recommend A:** proceed to an F9-equivalent seed-varying, paired participant-ambiguity evaluation.

The semantic vertical slice, conservative union, missing-evidence safety behavior, source grouping,
truth isolation, and candidate monotonicity are now executable and tested. The next unresolved issue
is representation sensitivity across candidate worlds: how often nested identity sets change
candidate sets, and how often different participant constraints are absorbed by the candidate
layer. A preregistered small world generator can vary candidate Regions and fixed public
participant-location assignments while preserving the rule, exact time, exact relation, target,
and paired world.

The result supports the claim that this fixture can propagate public other-participant identity
precision through one explicit truth-free rule into conservative Region and candidate sets. It does
not establish real-world identity value, an optimal participant precision, population-level effect,
kind-unknown semantics, S5 applicability, relation taxonomy, or rescue-performance impact.
