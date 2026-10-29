# M1-F15 common-world observation vertical slice

## 1. Background

M1-F14 established source-aware, explicitly logical CandidateGeneration constraint composition,
but its acceptance fixture received the location predicate as an already prepared value. M1-F15
closes the remaining functional gap with one deterministic common world:

```text
one public synthetic world
  -> raw Location / Time / Participant observations
  -> existing axis-specific derivations
  -> L / T / P CandidateGeneration constraints
  -> unchanged M1-F14 JointRequirement composition
  -> one invariant historical candidate universe
```

This is a functional vertical slice, not a population experiment. No seed-varying generator,
precision profiles, relation ablation, missingness, conflict, S5 migration, or rescue model is added.

## 2. Common-world definition

The purified world has:

- target `person-a`;
- actual other participant `person-b`, used only as evaluation truth;
- public possible-other-participant set `{person-b, person-c}`;
- exact public relation kind `PersonPersonEncounter`;
- exact encounter time tick 10;
- one later public event at epistemic occurrence-time window `[15,15]`;
- one public predecessor dependency with duration `[4,5]`;
- one public target-location observation with Regions `{east, west}`;
- public same-time participant-location evidence;
- six public candidate states; and
- evaluation truth candidate `(tick 10, west)`.

All evidence is public, Confirmed, available by cutoff 16, Person-only, CandidateGeneration-only,
complete, non-conflicting, and truth-compatible. The truth address is resolved only after the common
candidate universe and every public derivation/composition result have been produced.

## 3. Source graph

The world declares source relations independently from logical composition:

| Source | Fields carried | Source groups |
|---|---|---:|
| target-location observation | location Regions | 1 |
| later-event observation | occurrence-time window | 1 |
| duration/dependency source | dependency, duration bounds | 1 |
| encounter observation | encounter time, possible participant identities, exact relation kind | 1 |
| person-b location evidence | B Regions | 1 |
| person-c location evidence | C Regions | 1 |

This is nine fields in six source groups. Relation, participant identities, and encounter time are
fields of the same encounter observation and are not counted as three independent observations.
The participant-derived Region constraint is one F14 composition component but retains three
supporting groups: encounter, B location, and C location.

Derived constraints and composition artifacts are not new independent sources. Source-group counts
remain audit quantities; they do not select AND or OR.

## 4. Candidate universe

The public candidate domain is created once in the common-world runner and the same immutable
definitions and deterministic candidate IDs are supplied to all profiles.

| Tick | Region | Primary truth | Alternate truth audit |
|---:|---|---|---|
| 10 | west | yes (`person-b`) | no |
| 10 | east | no | yes (`person-c`) |
| 10 | north | no | no |
| 11 | south | no | no |
| 12 | west | no | no |
| 12 | east | no | no |

Every run records the same `CandidateUniverseId` and the same six universe candidate IDs. Profiles
never regenerate a different world, change candidate placement, or receive a profile-specific
candidate definition.

## 5. Location derivation

M1-F2 already established this meaning:

```text
public observed Region set R
  -> AllowedRegionIds = R
```

M1-F15 exposes that existing direct projection as the reusable
`m1f2-observed-regions-direct-projection` deriver. This is not a new location rule. It copies the
normalized public finite Region set into a Region-only derived constraint and carries observation,
availability, state, visibility, ancestry, rule ID, and deterministic identity. F2 behavior and S5
are unchanged.

Fixture input and output:

```text
raw observed Regions: {east, west}
derived L:            Region in {east, west}
```

The fixture has no `AllowedRegionIds` output field for L. Unknown JSON members are rejected, so a
fixture author cannot directly declare `derivedAllowedRegionIds` or candidate results.

## 6. Time derivation

The unchanged M1-F8/F9 rule is used:

```text
Rule: bounded-duration-predecessor-window-v1
DerivedPast([o_min,o_max], [d_min,d_max])
  = [o_min-d_max, o_max-d_min]
```

With occurrence uncertainty `[15,15]` and public duration `[4,5]`, T is `[10,11]`. The historical
window is not declared in the fixture. `AvailableAtTick=16` is checked for usability and is not used
in interval arithmetic. T is converted to a Time-only F14 component, so dependency Regions do not
silently add a location predicate.

## 7. Participant derivation

The unchanged M1-F10/F11 rule is used:

```text
Rule: encounter-participant-location-union-v1
person-b -> {west}
person-c -> {east, north}
P = union({west}, {east, north})
  = {east, north, west}
```

The exact relation kind and same-time public evidence establish co-location applicability. Both
participant branches are evaluated before composition. P reaches F14 as one already-unioned
Region-only constraint; composition never evaluates `L AND B-regions AND C-regions`.

## 8. Exact relation role

`PersonPersonEncounter` is exact and invariant in all profiles. The F10 derivation explicitly checks
this relation kind before applying same-time co-location semantics, preserving the applicability
boundary established in M1-F12. Relation is a common prerequisite rather than an ablated precision
axis. The repository still has no second meaningful formal relation or taxonomy.

Exact relation information does not imply exact participant identity. The public observation can
therefore state exact Encounter while retaining `{person-b, person-c}` as possible endpoints.

## 9. Axis orthogonality

All L/T/P derivations run once before profile selection. Their explicit public-input audits prove:

- L reads only the location observation ID;
- T reads only the later-event observation, public dependency, and duration source;
- P reads only the encounter observation and B/C location-evidence inputs.

Turning one axis OFF does not change or reconstruct another axis's observation, evidence, rule
inputs, constraint ID, or output. It only omits that already-derived constraint from the F14 request.

## 10. Profile-selection semantics

The eight conditions are field-use selections:

1. None
2. L
3. T
4. P
5. L + T
6. L + P
7. T + P
8. L + T + P

Every raw observation exists in every profile. `OmittedAxesMeanNotSelected=true` records that OFF is
neither a false predicate nor absence of the world event/observation. No empty Region set,
NonRestricting placeholder, unknown profile, or contradiction is inserted. None sends zero groups
to the composer and returns the full candidate universe as `NonRestricting`.

## 11. F14 composition usage

Each selected L/T/P component is placed in an explicit `JointRequirement` group for the same:

- scenario/seed;
- target `person-a`;
- target-state context;
- candidate domain; and
- CandidateGeneration usage.

The production `CandidateGenerationConstraintComposer` is used directly. There is no
common-world-specific intersection implementation. Groups are also passed in reverse order; the run
fails unless composition ID, status, candidate IDs, normalized provenance, and work are identical.

## 12. Eight-condition results

| Profile | Derived predicates selected | Candidate coordinates | Count | Reduction vs None | Work |
|---|---|---|---:|---:|---:|
| None | none | all six | 6 | 0 | 6 |
| L | Region `{east,west}` | `10/west`, `10/east`, `12/west`, `12/east` | 4 | 2 | 12 |
| T | Tick overlaps `[10,11]` | `10/west`, `10/east`, `10/north`, `11/south` | 4 | 2 | 12 |
| P | Region `{east,north,west}` | all except `11/south` | 5 | 1 | 12 |
| L + T | L AND T | `10/west`, `10/east` | 2 | 4 | 18 |
| L + P | L AND P | same four candidates as L | 4 | 2 | 18 |
| T + P | T AND P | `10/west`, `10/east`, `10/north` | 3 | 3 | 18 |
| L + T + P | L AND T AND P | `10/west`, `10/east` | 2 | 4 | 24 |

The plateau `L+P = L` and `L+T+P = L+T` is retained. P is a superset of L over this candidate
representation, so its additional valid predicate performs checks but does not remove another
candidate. Values were not changed to manufacture a staircase.

## 13. Intersection and safety

Because all selected constraints are explicitly joint requirements for the same target state:

```text
Candidates(L+T)   = Candidates(L) intersect Candidates(T)
Candidates(L+P)   = Candidates(L) intersect Candidates(P)
Candidates(T+P)   = Candidates(T) intersect Candidates(P)
Candidates(L+T+P) = Candidates(L) intersect Candidates(T) intersect Candidates(P)
```

Observed results:

- intersection expectation violations: 0/4;
- containment: 8/8;
- false exclusion: 0/8;
- input-order violations: 0; and
- candidate-universe or relation-invariance violations: 0.

These equations are not a general instruction to intersect arbitrary evidence. They hold because
the purified fixture declares each predicate as a simultaneous hard requirement.

## 14. Derivation and composition telemetry

Derivation audit records:

- L observation ID, input Regions, output Regions, source group, rule and constraint IDs;
- T event observation, occurrence window, dependency and duration source, duration bounds, derived
  historical window, rule and constraint IDs; and
- P encounter observation, relation, possible participant IDs, branch evidence/source groups,
  conservative output union, alternatives evaluated, rule and constraint IDs.

Each profile records active/omitted axes, active constraint IDs, all observation IDs, common universe
ID/IDs, composed ID/status, result candidate IDs, reduction, containment, top-level component source
groups, supporting source groups, fields represented, input components, active predicates,
candidate-predicate checks, and generation work.

Work is the F14 candidate-universe generation plus exhaustive predicate checks. It excludes
derivation work and is not calibrated search or rescue cost.

## 15. Truth isolation

No truth property appears in location/time/participant observation inputs or the F14 composition
request. The actual other participant and historical candidate are consulted only for final
containment evaluation.

A test changes evaluation truth from `person-b / 10-west` to public-compatible
`person-c / 10-east` while retaining the same public observations, possible participants, evidence,
dependency, relation, and candidate universe. L/T/P constraint IDs, composition IDs, candidate IDs,
work, and normalized audits remain unchanged.

## 16. Failure boundaries and regressions

Focused tests verify:

- different target and candidate domain are `Invalid` in F14 composition;
- non-CandidateGeneration location derivation fails;
- private-truth location input cannot be constructed;
- profile OFF does not create a false predicate;
- fixture JSON cannot declare a derived output;
- participant alternatives are unioned before composition;
- candidate universe and exact relation cannot vary by profile;
- order normalization is deterministic; and
- five output artifacts are byte-identical for identical inputs/metadata.

F8/F9, F10/F11, F12, F14, S0-S5, and the full suite remain regression requirements. F2 and S5
production behavior are not replaced.

## 17. Deterministic reproduction

Run:

```bash
dotnet run --project src/FootprintSim.Cli -- evaluate-common-world-observation \
  --fixture experiments/m1f15_common_world_observation/fixture.json \
  --output results/m1f15-common-world-observation
```

Artifacts:

- `common-world-observation-summary.json`;
- `common-world-observation-profiles.csv`;
- `common-world-derivation-audit.json`;
- `common-world-composition-audit.jsonl`; and
- `common-world-observation-manifest.json`.

They use normalized IDs/order, no current time, no absolute paths, and no random IDs. Final
validation executes the command twice from the same clean commit and compares all five files byte
for byte.

## 18. Limitations and claim boundary

This work establishes only that one purified common world can generate L/T/P through existing
truth-free public derivations, selectively compose them as explicit Joint requirements, and compare
eight field-use profiles on one invariant candidate universe.

It does not establish:

- a general ranking of Location, Time, and Participant;
- a minimum field set or minimum independent-source set;
- population-level interaction effects;
- performance under precision variation, missingness, conflict, or correlated errors;
- required sensors or real location/time/identity precision;
- S5 applicability; or
- rescue success, search time, staffing, or equipment effects.

The displayed candidate reductions are functional checks for one fixture, not performance estimates.

## 19. Next recommendation

**Recommend A:** build a preregistered seed-varying common-world generator, then evaluate the same
eight selection profiles as paired conditions over 200 worlds.

The reasons are:

- axis derivations are explicit and orthogonal at their public-input boundary;
- relation and candidate universe are invariant;
- F14 composition supplies order-independent Joint semantics;
- provenance and field/source counts remain separate;
- truth isolation and safety hold; and
- observed plateaus can be attributed to predicate/candidate-set relations rather than a broken
  end-to-end path.

The next generator may vary, under preregistered fixed rules:

- candidate count;
- candidate Region distribution;
- candidate tick/state distribution;
- location-observation Region-set size;
- later-event tick and bounded duration;
- participant pool size and participant-evidence Region overlap; and
- truth candidate/participant, selected during world construction but excluded from derivation.

It must keep the three axis profiles orthogonal within a seed, exact Encounter fixed, all primary
evidence truth-compatible/available, the same world across eight conditions, and per-seed set
inclusion/composition audits. Missingness, conflict, source correlation, and precision profiles
should remain later stages.

## 20. Research-roadmap handoff

This fixture is a **field-combination functional baseline**, not the final minimal-observation study.
Later work must separately vary:

- field presence;
- field precision;
- observation/source presence;
- source independence/correlation; and
- a decision threshold tied to a downstream action.

Artificial-world sensitivity must test Region/tick granularity, candidate density, state-interval
representation, and participant evidence overlap before general importance claims.

Candidate count must eventually be mapped to candidate Regions/buildings/floors, search area or
volume, check locations, actions, expected search time, personnel/equipment, wrong-area cost, and
handoff to existing rescue methods. M1-F15 does not perform that mapping.

After primitive decomposition, joint conditions, concurrence/sequence, dependency, and forward or
reverse examination may be compared with existing Buddhist/Abhidhamma methodology. Production
naming remains ordinary engineering terminology; no direct paccaya identification or complete-basis
claim is made.
