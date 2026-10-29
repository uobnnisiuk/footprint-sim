# M1-F11 paired participant-ambiguity evaluation

## 1. Objective and preregistration status

M1-F11 evaluates whether the M1-F10 participant-dependent derivation remains conservative and safe
when the synthetic participant and candidate world varies by seed. Exact, Narrow, Broad, and Unknown
profiles are paired within each world. This is not a new participant rule, identity-resolution
system, kind-unknown contract, S5 migration, or real-world calibration.

This document and
`experiments/m1f11_participant_ambiguity_paired/experiment.json` fix the generator, profiles,
truth boundary, overlap generation, primary invariants, plateau classification, metrics, and failure
conditions before the 200-seed evaluation is run. Sections 2–8 are preregistered and will not be
changed after results are observed. The preregistration commit is recorded here after commit:

```text
8672c15e5a886778356a1512b559a50351930f5f
```

## 2. Fixed M1-F10 semantics

The evaluation must call the unchanged M1-F10 production rule:

```text
rule ID: encounter-participant-location-union-v1

PossibleParticipants = P
AllowedRegions(target) = union(AllowedRegions(p) for p in P)
ConstrainedTimeWindow = EncounterTimeWindow
```

The following remain fixed:

- same-time co-location under exact `PersonPersonEncounter`;
- one fixed target person and Person-only other participants;
- confirmed public/search-usable participant-location evidence only;
- every finite possible participant must have one usable evidence record;
- any missing/unusable participant evidence causes derivation failure, never partial union;
- alternatives are unioned, never intersected and never selected using hidden truth;
- identity Unknown retains the encounter observation and generates no participant constraint;
- `CandidateGeneration` is the only allowed usage role;
- Device is never promoted to Person;
- one encounter observation remains one encounter source group;
- separate participant-location evidence sources remain separately auditable;
- provenance and public-input-based deterministic IDs remain intact; and
- truth is used only for world construction and post-reconstruction evaluation.

No M1-F10 derivation, participant observation, participant evidence, derived constraint,
reconstruction, or acceptance-fixture source file may change in M1-F11.

## 3. Preregistered world generator

Generator ID is `m1f11-simple-participant-region-world-v1`. Every random variable uses the
repository's specified SplitMix64 `StableRandom` with SHA-256-derived streams keyed by experiment ID,
generator ID, seed, and purpose. Unrelated variables use distinct purpose streams.

| Item | Fixed generation rule |
|---|---|
| seed range | every integer `0..199`; no filtering, replacement, or regeneration |
| candidate count | discrete uniform integer `6..14` |
| Region universe size | discrete uniform integer `4..8` |
| Region IDs | deterministic `seed-{seed}-region-{slot}` identifiers |
| candidate Region | independent discrete-uniform draw from the generated Region universe, with collisions allowed |
| candidate identity | deterministic slot key; candidate-specific public provenance preserves duplicate same-Region candidates |
| encounter tick | discrete uniform integer `10..40` |
| candidate tick | exactly the generated encounter tick for every candidate, isolating participant/Region effects from time precision |
| participant pool size | discrete uniform integer `3..5` |
| participant IDs | deterministic `seed-{seed}-person-{slot}` identifiers |
| participant Region-set size | discrete uniform integer `1..min(3, Region universe size)` independently per participant |
| within-participant Regions | sample without replacement from the universe |
| across-participant Regions | independently sampled; overlap, identity, and disjointness are not forced |
| truth candidate slot | discrete uniform candidate slot, chosen during world generation |
| truth participant slot | discrete uniform participant slot, chosen during world generation |
| truth compatibility | truth participant evidence always contains the truth candidate Region; remaining Regions use the same registered sampling rule |
| availability | encounter tick plus one; encounter and all participant evidence use the same availability and cutoff |
| public evidence | one confirmed public CandidateGeneration record per participant, covering the exact encounter tick |
| missing evidence | excluded from the primary family; the F10 safe-failure test remains regression coverage |

The candidate Region assignment can leave a Region with no candidate, and multiple candidates can
share one Region. Both are retained. Participant evidence can be identical, partially overlapping,
or pairwise disjoint because independent finite subsets are drawn from the common universe. No
overlap category is forced or post-selected.

### Overlap classification

Each world receives one evidence-overlap classification:

- `Identical`: every participant has the same Region set;
- `CompletelyDistinct`: every pair of participant Region sets is disjoint;
- `PartiallyOverlapping`: neither of the above and at least one pair intersects.

These classifications are descriptive secondary telemetry. The generator is not adjusted to meet a
target frequency.

### Duplicate and invalid-world handling

Candidate Region collisions are allowed. Candidate-specific provenance must keep all generated
candidate slots independently addressable. Participant Regions are unique within a participant.

The following are invalid and fail the complete evaluation; they never skip or regenerate a seed:

- the requested seed set differs from exact `0..199`;
- any generated count, tick, Region-set size, or identity lies outside registered ranges;
- candidate slot, participant ID, public input ID, or source group duplication where uniqueness is
  required;
- a candidate does not use the encounter tick;
- no candidate or participant pool;
- missing/unusable evidence for a finite-profile participant;
- truth candidate or truth participant is missing;
- truth participant evidence excludes the truth candidate Region;
- profile sets are not strictly nested as registered;
- relation, target, time, public evidence, availability, or EvidenceState differs across profiles;
- any false exclusion or monotonicity violation.

The construction makes a valid world for every registered seed. Result-based rejection, tuning,
replacement, and regeneration are forbidden.

## 4. Participant profiles

World generation first chooses the truth participant. Exact is preregistered as a correct exact
identity observation, so its public content names that person. This truth-dependent choice happens
only while constructing the public observation profile; the deriver receives the resulting public
identity information and has no truth parameter.

Narrow adds the lexicographically lowest non-truth participant ID. This distractor choice does not
inspect participant Region evidence. Broad contains the complete generated pool. Thus pool size
`3..5` gives strict membership nesting for every seed:

```text
Exact = {truth participant}                      size 1
Narrow = Exact union {lowest-ID non-truth}       size 2
Broad = complete participant pool                size 3..5
Unknown = encounter present, identity unknown    no finite identity set
```

The target, relation, encounter time, participant evidence records and their Region precision,
availability, EvidenceState, source groups, candidate world, and truth world are identical for all
four profiles of one seed.

## 5. Truth-generation and evaluation boundary

The truth candidate slot and truth participant slot are private world-generation facts. The world
generator ensures semantic compatibility by inserting the truth candidate Region into the truth
participant's public location evidence. That evidence is then an ordinary public record; it is not
tagged as truth for the deriver.

The evaluator must complete observation construction, constraint derivation, and candidate
reconstruction for all profiles before comparing candidate IDs with the truth candidate. Truth
participant, truth candidate, truth flags, and realized state are not derivation or reconstruction
parameters and do not contribute to public IDs.

A dedicated test changes only the evaluation truth participant to another participant compatible
with the same public evidence while keeping all public observation content fixed. Derived Regions,
constraint IDs, and candidate IDs must remain unchanged.

## 6. Primary invariants and safety hypothesis

For every seed, set membership—not only counts—is checked:

```text
DerivedRegions(Exact)
  subset-or-equal DerivedRegions(Narrow)
  subset-or-equal DerivedRegions(Broad)

Candidates(Exact)
  subset-or-equal Candidates(Narrow)
  subset-or-equal Candidates(Broad)
  subset-or-equal Candidates(Unknown)
```

Every finite derivation must succeed. Unknown must retain the encounter observation, return
`UnknownParticipantIdentity`, and generate no constraint. Because Exact contains the actual other
participant and that participant's public evidence contains the truth candidate Region, all four
profiles are expected to contain truth and have zero false exclusions.

Any derived-Region or candidate-set monotonicity violation, finite derivation failure, missing
encounter observation, or false exclusion blocks aggregate interpretation.

## 7. Preregistered representation-plateau analysis

Exact→Narrow and Narrow→Broad are classified independently, producing 400 finite transitions:

1. `ParticipantToRegionPlateau`: participant membership strictly widens, but the Region union is
   unchanged because the added participant evidence is identical to or contained in the prior union.
2. `RegionToCandidatePlateau`: the Region union strictly widens, but no candidate occupies an added
   Region, so the candidate set is unchanged.
3. `CandidateExpansion`: both the Region union and candidate set strictly widen.

These categories are mutually exclusive and exhaustive if the registered invariants hold. They
locate where an identity-information difference stops propagating. Per-world summaries additionally
record:

- all three finite candidate sets equal;
- any finite participant widening with unchanged Region union;
- any strict Region widening with unchanged candidate set; and
- Broad candidate set equal to Unknown.

`Broad = Unknown` means the finite Broad union already covers every represented candidate; it does
not make identity Unknown semantically equal to Broad, because only Broad generates a constraint.

## 8. Metrics, artifacts, and failure conditions

Per-profile aggregates report mean, median, minimum, and maximum candidate count; mean and median
derived-Region count; containment; false exclusion; mean candidate reduction count and fraction
relative to Unknown; participant alternative count; alternatives evaluated; derivation success and
failure; mean CandidateGeneration work; and encounter, participant-evidence, and generated-constraint
source-group counts.

Per-seed output records the complete candidate universe, truth candidate, participant pool, truth
participant, every participant's public Regions and source group, all four participant sets, derived
Regions, candidate IDs, containment, work, source ancestry, adjacent monotonicity audits, and plateau
classification.

Planned deterministic artifacts are:

- aggregate/report JSON;
- one-world-per-line JSONL;
- profile aggregate CSV;
- per-transition monotonicity/plateau CSV; and
- manifest JSON.

The evaluation treats each of the following as failure:

- truth participant selection inside derivation;
- intersection of alternative Region constraints;
- wider participant information producing narrower Regions or candidates;
- any false exclusion;
- unsafe partial union for missing evidence;
- Unknown converted to no encounter;
- Device promoted to Person;
- participant alternatives counted as independent encounter sources;
- profile-specific changes to participant evidence, relation, time, or location precision;
- result-dependent world tuning;
- nondeterministic IDs or output; or
- CandidateGeneration evidence leaking into verification or traversal.

M1-F10 `1 -> 2 -> 3 -> 4`, missing-evidence failure, hidden-identity invariance, Device rejection,
source grouping, usage boundary, and Unknown behavior remain regressions. M1-F8 `2 -> 3 -> 4 -> 4`,
M1-F9 monotonicity, all existing tests, and unchanged S5 behavior also remain required.

## 9. Results

The exact registered seed set `0..199` produced 200 valid worlds and 800 paired profile runs. No
seed was skipped, rejected, regenerated, replaced, or tuned after observation.

### Primary profile metrics

| Profile | Mean candidates | Median | Min–Max | Mean derived Regions | Median derived Regions | Containment | False exclusions | Mean reduction count vs Unknown | Mean reduction fraction vs Unknown | Mean generation work |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| Exact | 4.210 | 4 | 1–12 | 2.135 | 2 | 1.000 | 0 | 5.495 | 0.555574 | 10.705 |
| Narrow | 6.165 | 6 | 1–14 | 3.410 | 3 | 1.000 | 0 | 3.540 | 0.362608 | 10.705 |
| Broad | 7.995 | 8 | 1–14 | 4.725 | 5 | 1.000 | 0 | 1.710 | 0.172987 | 10.705 |
| Unknown | 9.705 | 10 | 6–14 | no constraint | no constraint | 1.000 | 0 | 0.000 | 0.000000 | 9.705 |

The Unknown derived-Region telemetry is serialized as count zero because no constraint exists; it
does not mean an empty/nowhere Region constraint. Unknown retained the encounter observation in all
200 worlds and returned the expected `UnknownParticipantIdentity` failure.

All 600 finite runs derived successfully. The 200 Unknown runs intentionally did not derive. Mean
participant alternatives and alternatives evaluated were respectively `1`, `2`, `3.970`, and `0`
for Exact, Narrow, Broad, and Unknown. The Broad mean is the generated participant-pool mean.

Every finite profile scanned the same fixed per-world public evidence corpus, giving mean evidence
records inspected `3.970`; identity precision changes branch evaluation, not which public corpus
exists. CandidateGeneration work is the candidate universe plus one constraint application for
finite profiles. Unknown performs no constraint application.

### Monotonicity and safety

| Audit | Result |
|---|---:|
| derived-Region set monotonicity violations | 0 |
| candidate-set monotonicity violations | 0 |
| finite derivation failures | 0 |
| false exclusions across 800 runs | 0 |
| source-independence violations | 0 |
| Unknown encounter-presence violations | 0 |
| seeds with `Exact < Narrow` candidates | 143 |
| seeds with `Narrow < Broad` candidates | 134 |
| seeds with `Broad < Unknown` candidates | 119 |
| seeds with all finite candidate sets equal | 8 |
| seeds where participant widened but Regions were unchanged | 77 |
| seeds where Regions widened but candidates were unchanged | 43 |
| seeds where Broad candidates equalled Unknown | 81 |

All primary relations were checked by candidate ID and Region ID set inclusion in every world, not
inferred from counts or aggregate means.

### Region-evidence overlap

| Whole-world participant evidence overlap | Worlds |
|---|---:|
| identical for every participant | 0 |
| completely pairwise disjoint | 11 |
| partially overlapping | 189 |

The registered independent subset sampler happened to produce no world in which every participant
had an identical complete set. This category remained possible and is reported explicitly as zero;
the family was not regenerated to force an example. Partial overlap dominated because three to five
participants draw one to three Regions from universes of size four to eight.

### Propagation and plateau classification

The 400 Exact→Narrow and Narrow→Broad transitions were classified exhaustively:

| Propagation class | Transitions | Interpretation |
|---|---:|---|
| `ParticipantToRegionPlateau` | 78 | participant set widened, but the added person's Regions were already contained in the prior union |
| `RegionToCandidatePlateau` | 45 | Region union widened, but no historical candidate occupied an added Region |
| `CandidateExpansion` | 277 | participant widening propagated through Region constraint into candidate membership |

The 78 first-layer plateaus occurred in 77 unique seeds; one seed plateaued at both finite
transitions. The 45 candidate-representation plateaus occurred in 43 unique seeds; two seeds had
two such transitions. The 277 candidate expansions exactly match the 143 Exact&lt;Narrow plus 134
Narrow&lt;Broad strict increases.

Eight worlds had equal candidate sets across all three finite profiles: seeds
`39, 47, 52, 53, 59, 74, 76, 169`. Their equality is not treated as evidence that participant
precision is unnecessary; their per-transition records identify whether the difference stopped at
the participant-to-Region or Region-to-candidate boundary.

Broad equalled Unknown in 81 worlds. In those worlds the complete finite participant evidence union
already covered every Region occupied by the represented candidates. Broad remained semantically
different from Unknown through successful constraint existence, provenance, source groups, and one
additional generation step.

Complete seed lists and every one of the 600 adjacent transition audits are preserved in the JSON
and monotonicity CSV artifacts.

### Source independence and truth boundary

Every profile retained one encounter source group. Finite participant-evidence source-group counts
equalled their possible participant counts: means `1`, `2`, and `3.970`. Every successful derived
constraint remained one constraint source group, while the used participant evidence ancestry was
listed separately. Unknown had one encounter group, no used participant-location group, and no
derived constraint.

The evaluator completed all four public derivations and reconstructions before resolving the truth
candidate ID. The production derivation method still exposes no truth parameter. A dedicated audit
changed only the evaluation truth participant to another public-evidence-compatible participant;
derived Regions, constraint IDs, and candidate IDs remained unchanged.

All generated worlds were truth-compatible: Exact named the generated truth participant, that
person's public evidence included the truth candidate Region, and all profiles contained the truth
candidate.

## 10. Deterministic reproduction

The writer emits:

- `participant-ambiguity-paired.json` — complete report, aggregates, and summary;
- `participant-ambiguity-paired-runs.jsonl` — 200 generated worlds with all public and evaluation
  details;
- `participant-ambiguity-paired.csv` — primary profile aggregates;
- `participant-ambiguity-paired-monotonicity.csv` — 600 adjacent set and plateau audits; and
- `participant-ambiguity-paired-manifest.json` — experiment hash, clean source commit, seeds, rule,
  and output inventory.

Two executions from the final clean commit produced byte-identical copies of all five files.
Generated result files are ignored and are not committed.

## 11. Claim boundaries and next recommendation

M1-F11 establishes that in this preregistered synthetic family, under the unchanged public
same-time co-location union rule, broader other-participant identity uncertainty conservatively
maintained or widened both derived Region constraints and historical candidate sets. This held for
all 200 worlds as direct per-seed set inclusion, with zero false exclusion.

It also establishes where finite identity differences became unobservable in this representation:
78/400 transitions stopped before Region expansion, 45/400 reached a wider Region constraint but
stopped at the candidate representation, and 277/400 changed candidate membership. These are
synthetic-world propagation counts, not real-world identity values.

M1-F11 does not establish real-world identity importance, a sufficient number of identified people,
identity acquisition technology, kind-only or kind-unknown semantics, S5 applicability, relative
importance against time/location, or rescue-performance effects.

**Recommend A:** the participant identity track now has both a semantic vertical slice and a paired
world-family evaluation. The largest remaining observation-axis gap is relation semantics: F6 found
no relation-specific dispatch or ambiguity taxonomy, and no relation vertical slice comparable to
F8/F10 exists. The next step should create one minimal, semantically justified relation-dependent
derivation fixture before any relation performance experiment. Participant-world representation
sensitivity remains valuable later—especially given 81 Broad/Unknown saturations—but it is no
longer the earliest missing semantic link. Kind-only/kind-unknown should also remain later because it
would add new participant semantics rather than validate the fixed identity rule.
