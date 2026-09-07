# M1-F16 common-world field-combination paired evaluation

## 1. Objective and preregistration boundary

M1-F15 established one deterministic end-to-end common world. M1-F16 keeps its public
Location/Time/Participant derivations and the M1-F14 composer unchanged, then varies only the
purified synthetic world over seeds `0..199`. Every world is evaluated with the same eight field-use
profiles:

```text
None, L, T, P, L+T, L+P, T+P, L+T+P
```

The generator ranges, profile definitions, primary set inclusions, conditional comparisons, and
failure conditions in this document and
`experiments/m1f16_common_world_field_combination/experiment.json` are committed before the first
200-world run. They must not be tuned after inspecting results. Generated result files remain
untracked build artifacts.

This is a field-combination baseline. It is not a field ranking, a final minimum-observation study,
a source-ablation study, a precision interaction, a real-sensor requirement, or a rescue evaluation.

## 2. Fixed F15/F14 semantics

No axis or composition rule is changed:

| Axis | Fixed public derivation |
|---|---|
| L | `m1f2-observed-regions-direct-projection`: observed Regions become `AllowedRegionIds` |
| T | `bounded-duration-predecessor-window-v1`: `[o_min-d_max, o_max-d_min]` |
| P | `encounter-participant-location-union-v1`: union public Regions across all possible persons |
| Relation | exact `PersonPersonEncounter`, used as the co-location applicability prerequisite |
| Composition | M1-F14 explicit `JointRequirement`, CandidateGeneration-only, normalized and order-independent |

Participant alternatives are unioned inside the P deriver before composition. There is no L/T/P
interaction rule. Source groups are provenance only and never select AND/OR.

All intervals remain closed/inclusive. `AvailableAtTick` controls availability only and is excluded
from the bounded-duration arithmetic.

## 3. Preregistered generator

Generator ID: `m1f16-purified-common-world-generator-v1`.

| Property | Fixed rule/range |
|---|---|
| seeds | every integer `0..199`; no skip, regeneration, or replacement |
| candidate count | discrete uniform draw over 8..16 |
| Region universe | 5..9 deterministic seed-named Regions |
| candidate base tick | 20..40 |
| candidate start offset | -3..3 from base tick |
| candidate interval width | 0..2 ticks |
| candidate coordinate collision | `(start tick, Region)` pairs sampled without replacement |
| same tick / same Region | separately allowed across candidates |
| unused Regions | allowed |
| location Region-set size | 2..5 |
| public duration lower bound | 3..6 ticks |
| duration-bound width | 0..3 ticks |
| participant pool | 2..4 Persons |
| Regions per participant evidence | 1..3 |
| availability lag | one tick |
| invalid-world policy | fail; never skip or regenerate |
| seed strategy | independent purpose-derived deterministic streams |

The smallest coordinate grid has `7 start offsets * 5 Regions = 35` cells, so the maximum 16
candidates can always be sampled without replacement. All generated ticks are non-negative. Values
are drawn through separately named stable streams, so adding a draw for one purpose does not consume
another purpose's stream.

## 4. Generation order and truth boundary

Each seed follows this fixed order:

1. generate the public Region universe and candidate universe;
2. select one existing candidate as evaluation truth;
3. generate the public L observation, including its Region;
4. generate a duration bound and one compatible realized duration, then the later event;
5. generate the Person pool and public participant-location evidence, ensuring only that the selected
   truth participant's evidence includes the truth Region;
6. derive L/T/P from the finalized public inputs;
7. select derived constraints for the eight profiles and run the unchanged composer;
8. use truth only for containment/false-exclusion evaluation.

Truth-compatible generation is not derivation-time truth access. Public derivation requests and the
composition request contain no truth field. A dedicated audit changes compatible evaluation truth
while holding public inputs fixed and requires all constraints, composition IDs, candidate sets, and
work to remain unchanged.

## 5. Varied world variables

Candidate count, Region universe size, candidate Region/start/interval placement, unused Regions,
L Region set, later-event tick, public duration lower/upper bounds, participant count and IDs,
participant Region sets and overlap, truth candidate, and truth participant vary by seed.

The rules, target (`person-a`), exact relation, Person-only boundary, candidate inclusion logic,
public/truth boundary, source semantics, interval inclusivity, profile selection, and seed algorithm
do not vary.

## 6. Location observation generation

L samples a finite Region set of size 2..5 from the world Region universe and includes the truth
candidate Region. The finalized raw observation is passed to the F15 location deriver. The derived
Region set is never declared directly by the generator or profile.

Sampling L independently from candidate placement permits observed Regions with no candidate and
therefore natural constraint-to-candidate plateaus.

## 7. Time observation and duration generation

The truth candidate start is the exact encounter/predecessor tick. A public duration lower bound
`d_min` is drawn from 3..6 and width from 0..3, giving `D=[d_min,d_max]`. A compatible realized
duration is used only to make the generated later-event tick truth-compatible. The T deriver receives
the exact epistemic later-event occurrence window and public bound and computes:

```text
DerivedPast([later,later], [d_min,d_max])
  = [later-d_max, later-d_min]
```

The derived window cannot be declared in the experiment definition. Candidate state intervals pass
T when their closed interval overlaps the derived closed interval.

## 8. Participant pool and evidence generation

Each world contains 2..4 public Person IDs. Each participant independently samples 1..3 Regions.
The generated possible-participant set contains the complete pool. The selected truth participant's
public evidence includes the truth Region; other participants are sampled without outcome-based
selection. This naturally permits identical, partial-overlap, and pairwise-disjoint participant
evidence. The P deriver evaluates every possible participant and conservatively unions all Regions.

No Device, missing participant evidence, misidentification, private evidence, or participant
intersection is introduced.

## 9. Source graph and field/source distinction

Every world retains separate ancestry for:

- the L observation;
- the encounter observation carrying relation, participant identities, and encounter time;
- the later-event observation;
- the duration/dependency source; and
- one public location-evidence source per possible participant.

With `N` participants this is `N+4` independent source groups, while the source graph carries more
fields (`N+7`) because encounter and duration sources each support multiple fields. Derived artifacts
are not new independent sources. Active field count and supporting source-group count are recorded
separately for every profile.

## 10. Profiles and selection semantics

All observations and the candidate universe exist once per world. A profile changes only which
already-derived constraints are supplied to the composer:

| Profile | L | T | P |
|---|---:|---:|---:|
| None | off | off | off |
| L | on | off | off |
| T | off | on | off |
| P | off | off | on |
| L+T | on | on | off |
| L+P | on | off | on |
| T+P | off | on | on |
| L+T+P | on | on | on |

OFF means “not selected for this evaluation.” It is not absent, false, unknown, contradictory, or
an empty predicate. None therefore returns the unchanged candidate universe as NonRestricting.
Relation remains exact and invariant in all 1,600 evaluations.

## 11. Preregistered primary invariants

For every seed, candidate-ID set inclusion—not just count—is required:

```text
C(L), C(T), C(P) subset-or-equal C(None)
C(L+T) subset-or-equal C(L) and C(T)
C(L+P) subset-or-equal C(L) and C(P)
C(T+P) subset-or-equal C(T) and C(P)
C(L+T+P) subset-or-equal C(L+T), C(L+P), and C(T+P)
```

No ordering among L, T, and P singles is hypothesized. Every profile must contain truth; any false
exclusion or inclusion violation blocks aggregate interpretation.

## 12. Conditional additional reductions

Nine paired additions are fixed before evaluation:

- T added to L; P added to L;
- L added to T; P added to T;
- L added to P; T added to P; and
- P added to L+T; T added to L+P; L added to T+P.

For baseline `B` and combined profile `B+A`, the recorded quantity is
`|C(B)| - |C(B+A)|`. Each comparison reports its full per-seed value, mean/median/min/max, positive
world count, and zero-additional-effect world count. This is synthetic candidate reduction, not real
search value.

## 13. Plateau classification

When an added axis does not change the candidate ID set, the evaluator uses ordinary descriptive
categories in this order:

1. **Saturation**: the baseline already has one truth-compatible candidate.
2. **Observation-to-constraint plateau**: the added predicate is non-restricting over its registered
   candidate domain (all Regions or all candidate intervals pass).
3. **Constraint-overlap plateau**: for Region predicates, the baseline's active Region restrictions
   already imply the added Region predicate.
4. **Constraint-to-candidate plateau**: the predicate adds a logical restriction, but no surviving
   represented candidate is removed.
5. **Additional candidate reduction**: at least one candidate is removed.

This refines the F13 pipeline distinction without asserting that a plateau makes an axis unnecessary.
Candidate density, Region/tick granularity, interval width, unused Regions, and participant-evidence
overlap can all affect where information is absorbed.

## 14. Profile equivalence and per-world minima

All 28 unordered profile pairs are compared by candidate IDs. For each world the output also records:

- minimum candidate count among the eight profiles;
- every profile attaining that count;
- inclusion-minimal profiles among those profiles; and
- whether all count-minimum profiles return the same candidate set.

These are only per-world minimal field combinations within this synthetic family. They do not define
a general minimal observation or a minimum source set.

## 15. Work and audit telemetry

Per profile: active fields, constraints, source groups, components received, predicates applied,
candidate-predicate checks, duplicate suppression, alternative branches, candidate IDs, reduction,
containment, and composition identity.

Per world: raw L Regions; T event, bounds, and derived window; participant pool and public Regions;
source graph; candidate universe; truth labels; L/T/P constraint IDs and outputs; all set-inclusion,
conditional-reduction, equivalence, plateau, and minimum-profile audits.

Participant alternatives evaluated are derivation work. CandidateGeneration work remains the F14
candidate-universe generation plus predicate checks and is not calibrated rescue/search cost.

## 16. Preregistered failure conditions

The run fails on truth leakage, per-profile world or universe changes, OFF-to-absent/false/unknown/
empty conversion, source-selected operators, an L/T/P interaction rule, joint set-inclusion failure,
false exclusion, private/future evidence use, use of AvailableAtTick in time arithmetic, participant
alternative intersection, result-dependent tuning, seed skip/regeneration, or nondeterministic IDs/
outputs.

The family is purified: no missing evidence, conflict, Device, private evidence, relation ambiguity,
precision profiles, or source-correlation treatment is mixed into the primary axis.

## 17. Planned artifacts and deterministic reproduction

The CLI writes:

- `common-world-field-combination.json`;
- `common-world-field-combination-runs.jsonl`;
- `common-world-field-combination-profiles.csv`;
- `common-world-field-combination-conditional-reductions.csv`;
- `common-world-field-combination-plateau-audit.csv`;
- `common-world-field-combination-set-inclusion.csv`;
- `common-world-field-combination-profile-equivalence.csv`; and
- `common-world-field-combination-manifest.json`.

After this preregistration is committed, run:

```bash
dotnet run --project src/FootprintSim.Cli -- \
  evaluate-common-world-field-combination \
  --experiment experiments/m1f16_common_world_field_combination/experiment.json \
  --seeds 0..199 \
  --output results/m1f16-common-world-field-combination
```

The final clean commit is evaluated twice into separate directories; every registered artifact must
be byte-identical.

## 18. Results

The preregistered seed range completed without skip or regeneration:

- worlds: 200;
- profile evaluations: 1,600;
- primary candidate-ID set-inclusion audits: 2,400;
- joint set-inclusion violations: 0;
- containment: 1,600/1,600;
- false exclusions: 0;
- axis-orthogonality violations: 0;
- candidate-universe invariance violations: 0; and
- relation-invariance violations: 0.

### 18.1 Candidate results and work

| Profile | Mean | Median | Min-max | Containment | False exclusions | Mean reduction vs None | Mean CandidateGeneration work |
|---|---:|---:|---:|---:|---:|---:|---:|
| None | 12.060 | 12 | 8-16 | 100% | 0 | 0.000 | 12.060 |
| L | 6.550 | 6 | 2-16 | 100% | 0 | 5.510 | 24.120 |
| T | 5.300 | 5 | 1-12 | 100% | 0 | 6.760 | 24.120 |
| P | 8.140 | 8 | 2-16 | 100% | 0 | 3.920 | 24.120 |
| L+T | 3.145 | 3 | 1-10 | 100% | 0 | 8.915 | 36.180 |
| L+P | 4.965 | 5 | 1-15 | 100% | 0 | 7.095 | 36.180 |
| T+P | 3.675 | 3 | 1-9 | 100% | 0 | 8.385 | 36.180 |
| L+T+P | 2.415 | 2 | 1-8 | 100% | 0 | 9.645 | 48.240 |

These averages describe only the preregistered generator. In particular, T having a smaller mean
than L or P is not evidence of a general importance ordering.

Each single profile receives one component and applies one predicate; pair profiles receive/apply
two; the full profile receives/applies three. With mean universe size 12.060, mean predicate checks
are 12.060, 24.120, and 36.180 for singles, pairs, and the full profile respectively. The None work
is universe generation only. Duplicate suppression and raw composition-alternative evaluation are
zero, as expected: P reaches composition as one pre-unioned component.

### 18.2 Conditional additional reductions

| Added axis / baseline | Mean reduction | Median | Min-max | Positive worlds | No-change worlds |
|---|---:|---:|---:|---:|---:|
| T added to L | 3.405 | 3 | 0-12 | 189 | 11 |
| P added to L | 1.585 | 1 | 0-9 | 123 | 77 |
| L added to T | 2.155 | 2 | 0-8 | 167 | 33 |
| P added to T | 1.625 | 1 | 0-8 | 139 | 61 |
| L added to P | 3.175 | 3 | 0-12 | 165 | 35 |
| T added to P | 4.465 | 4 | 0-13 | 198 | 2 |
| P added to L+T | 0.730 | 0 | 0-5 | 84 | 116 |
| T added to L+P | 2.550 | 2 | 0-12 | 176 | 24 |
| L added to T+P | 1.260 | 1 | 0-8 | 130 | 70 |

Across the 1,800 fixed comparisons, 1,371 produced additional candidate reduction and 429 did not.
The no-change cases split into 78 observation-to-constraint plateaus, 143 constraint-overlap
plateaus, 121 constraint-to-candidate plateaus, and 87 one-candidate saturation cases. The detailed
per-comparison split is:

| Addition | Observation-to-constraint | Constraint overlap | Constraint-to-candidate | Saturation |
|---|---:|---:|---:|---:|
| T to L | 0 | 0 | 11 | 0 |
| P to L | 21 | 56 | 0 | 0 |
| L to T | 6 | 0 | 20 | 7 |
| P to T | 21 | 0 | 33 | 7 |
| L to P | 7 | 28 | 0 | 0 |
| T to P | 0 | 0 | 2 | 0 |
| P to L+T | 18 | 39 | 22 | 37 |
| T to L+P | 0 | 0 | 12 | 12 |
| L to T+P | 5 | 20 | 21 | 24 |

Thus a zero result has four traceable meanings in this family. It never licenses the conclusion that
the added field is semantically unnecessary.

### 18.3 Profile equivalence

Requested direct equivalence counts are:

| Candidate sets compared | Equal worlds |
|---|---:|
| L vs L+T | 11 |
| L vs L+P | 77 |
| T vs L+T | 33 |
| T vs T+P | 61 |
| P vs L+P | 35 |
| P vs T+P | 2 |
| L+T vs L+T+P | 116 |
| L+P vs L+T+P | 24 |
| T+P vs L+T+P | 70 |

All 28 unordered profile pairs are retained in the CSV/JSONL artifacts. Among the three singles, all
candidate sets differed in 186 worlds. A single-axis result was a proper subset of both other
single-axis results in 24 worlds. At least one two-axis addition plateaued in 133 worlds, while at
least one third-axis addition still reduced candidates in 197 worlds.

### 18.4 Per-world minimum profiles

The mean per-world minimum was 2.415 candidates. The full profile attained the minimum count in all
200 worlds by Joint monotonicity, but it was inclusion-minimal among count-minimum profiles in only
42. Other profile frequencies were:

| Profile | Worlds attaining minimum count | Worlds inclusion-minimal at minimum |
|---|---:|---:|
| None | 0 | 0 |
| L | 4 | 4 |
| T | 20 | 20 |
| P | 0 | 0 |
| L+T | 116 | 92 |
| L+P | 24 | 20 |
| T+P | 70 | 50 |
| L+T+P | 200 | 42 |

The most frequent inclusion-minimal sets were L+T alone (69 worlds), full L+T+P alone (42), T+P
alone (28), T alone (20), and the tied alternatives L+T plus T+P (17). The remaining 24 worlds used
L+P alone (12) or smaller mixed/tied patterns. Every world's count-minimum profiles returned the same
candidate ID set, not merely the same count. These remain per-world descriptive results, not a
general minimum field set.

Minimum candidate-count distribution was: 1 in 69 worlds, 2 in 52, 3 in 39, 4 in 19, 5 in 14, 6 in
3, 7 in 3, and 8 in 1.

### 18.5 Representation and source observations

The generator realized its full registered candidate range 8..16 and Region range 5..9. It produced:

- unused Regions in 131 worlds;
- same-Region candidate collisions in all 200 worlds;
- same-start-tick candidate collisions in all 200 worlds;
- non-point state intervals in all 200 worlds;
- participant evidence classified as identical in 3 worlds, partially overlapping in 144, and
  completely distinct in 53; and
- participant pools of 2/3/4 Persons in 66/63/71 worlds.

Mean L predicate size was 3.435 Regions (range 2..5), mean P union size was 4.410 (range 1..8), and
mean derived T width was 1.465 ticks (range 0..3). Duration-bound widths 0/1/2/3 occurred in
49/51/58/42 worlds. These representation choices help explain why a valid extra constraint can be
absorbed at different layers.

Available source-group count ranged from 6 to 8 (mean 7.025). Mean active supporting groups were 1
for L, 2 for T, and 4.025 for P, whereas mean selected field counts were 1, 3, and 6.025. The full
profile therefore averaged 7.025 source groups but 10.025 fields. This confirms that field count,
source count, and composition component count remain different quantities.

### 18.6 Truth, pairing, and source/operator audits

The manifest records `TruthUsedForDerivationOrComposition=false`. Focused tests replace a compatible
truth candidate and, separately, a compatible truth participant while preserving public inputs;
derived L/T/P, composition IDs, candidate sets, and work remain invariant. Every profile retains the
same raw observations, source graph, relation, candidate universe, and truth labels for its seed.

The common evaluator invokes the unchanged F14 composer and has no source-based shortcut or L/T/P
interaction rule. Same-source fields can be Joint; independently sourced participant alternatives
remain unioned within P. All profiles use CandidateGeneration only.

### 18.7 Deterministic reproduction and regressions

The initial clean preregistration run used commit
`5fe8bf33f6dde41f3f77fa09b29a59f42f84bb4d`. The final validation records the final commit and
repeats the complete run twice; byte-identity and full regression status are reported with that
commit rather than changing any preregistered definition.

## 19. Claim boundaries

At most, the completed study can claim that paired field-combination effects and pipeline plateaus
were measured within this preregistered purified family using truth-free constraints on a common
candidate universe. It cannot rank L/T/P generally, establish a general minimum observation or
source set, specify real sensors/precision, generalize to missingness/conflict/correlation or S5, or
infer rescue time/success.

## 20. Next-decision criteria and handoffs

The next recommendation will consider semantic validity, safety, plateau location, representation
sensitivity, unresolved source dependence, and whether the field baseline is sufficient for an
explicit per-world field-ablation study. Candidate representation sensitivity is handed off through
Region/tick granularity, density, interval width, unused Regions, and participant overlap.

Source-level ablation, source correlation, missingness/conflict, field precision interactions, and
S5 migration remain separate staged studies. Rescue work must later map candidates to Regions,
buildings/floors, area/volume, check locations, actions, time, personnel/equipment, wrong-area cost,
and existing-method handoff. Multiple joint conditions may later be decomposed for neutral comparison
with Buddhist methodological categories such as concurrence, sequence, dependency, and forward/
reverse examination; no production naming or direct paccaya mapping is introduced here.

## 21. Recommendation

**Recommend D, then A.** The field-combination path is semantically valid and safe, so a field-level
study is now implementable. However, 429/1,800 additions plateaued and 131/200 worlds contained
unused Regions; every world also had Region/tick collisions and interval candidates. The inclusion-
minimal profile pattern therefore visibly depends on the registered Region/tick/density/overlap
representation. A small, preregistered sensitivity study that changes one representation factor at a
time is the largest remaining prerequisite before elevating the secondary per-world minima into an
explicit minimal-field study.

Recommend A follows that sensitivity check, using the current per-world minimum schema as its
starting point. Recommend C (source-level ablation) remains later because field and source counts are
now correctly separated but source presence/correlation was intentionally fixed. Recommend B
(precision interaction) and E (missingness/conflict/correlation) also remain later to avoid mixing
two new axes before the field baseline's representation dependence is understood.
