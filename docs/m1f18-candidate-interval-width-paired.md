# M1-F18 candidate interval-width paired sensitivity

## Audit and research decision

Start from F17 final `58bb181593c11fd8afff5d03b057b01b5de3ab3b` on a new branch. The independent
baseline audit reproduced all 328 tests and all four F17 artifacts byte for byte; the 2,412 original
candidates map uniquely by `(seed, CandidateKey)` and by original `(start,end,Region)` coordinates.
No pointification outcome has been executed or inspected before this registration.

F17 repaired Region plateau diagnosis, not the candidate representation itself. Its interval-width
recommendation survives a fresh code audit: T admits any public candidate interval overlapping the
derived closed interval, whereas the F16 generator anchors the actual encounter to the chosen
candidate's start. Widths are sampled before truth selection. `TruthCandidateEndTick` is copied from
the selected public candidate; it is an evaluation address, not a separately simulated physical
duration or persistence endpoint. Pointification is therefore a valid bounded comparison within
this family, not a generally truth-preserving narrowing of arbitrary temporal uncertainty.

This is the next highest-value question because it directly tests the T overlap mechanism while
holding raw evidence, candidate density, starts, Regions and source graph fixed. Changing Region
counts or generator IDs would rerandomize or conflate factors. It is too early to turn descriptive
per-world minima into general minimal field requirements. The known S4 all-strategies evaluation
domain defect is independent of this F16-derived path and remains a separate prerequisite to future
cross-scenario strategy comparisons.

## Frozen protocol

The accompanying `experiments/m1f18_candidate_interval_width/experiment.json` and this design must
be committed and pushed before prospective execution. The F16 experiment bytes (SHA-256
`e717669c7178dcf8b278b831b6ee049a2aa8dae5a9c9e6366b8c32712649ca39`), IDs, purpose streams and exact
seeds 0..199 are fixed. Generate each original world once. No skip, regeneration, thinning,
replacement, outcome filtering or parameter tuning is allowed.

Paired unit: one generated world, with all eight F16 field profiles evaluated under both arms:

| Arm | Public candidate transform |
|---|---|
| original | Identity: original closed interval and temporal kind |
| point-at-start | `[s,e] -> [s,s]`, temporal kind -> EventPoint |

The transform receives a public-only candidate record containing key, public source/provenance,
Region and interval. It does not receive a generated world, truth key, truth labels, selected
participant, actual duration, observations or scores. Preserve start, Region, key, source ancestry,
availability, evidence state and candidate multiplicity. Already-point candidates remain identical.

Keep the original generated world and all truth fields byte-equivalent. After both public inference
arms return, use the original truth key to resolve the arm-specific evaluation address through the
public crosswalk. Do not mutate the original world or attach its PublicWorldId to altered candidate
content. Give each representation a deterministic content identity; record the original world ID,
arm identity and candidate universe ID separately.

The existing F15 path derives raw L/T/P inputs once for a reference check. Both primary arms use the
same public components and unchanged F14 CandidateGeneration composer. The original arm must
reproduce reference candidate IDs, sets, work, constraints and composition identity. This means
3,200 primary profile evaluations, plus reference/regression evaluations; it does not mean 400
independent worlds. No source-selected logical operator or new L/T/P interaction is added.

Small internal adapter extraction is allowed to reuse F16's original fixture construction and F17's
corrected explicit-domain classifier. No existing inference semantics or old output bytes may change
except ordinary manifest source/dirty metadata. The F17 legacy projection must not be applied to
point profiles attached to the original world: compute Time-top from the arm's actual candidate
windows, and Region top/implication from the fixed declared RegionUniverse.

## Identity, truth and fixed variables

State/candidate IDs include interval bounds, temporal kind and public provenance. Recompute native
IDs through the existing public factory/reconstructor; never overwrite them with stable keys.
Retain a bijective crosswalk `(seed,key,source,Region,original interval/kind/state ID/candidate ID,
point interval/kind/state ID/candidate ID)`. No merger is needed because F16 start/Region pairs are
unique. Width-zero candidate IDs must be identical; positive-width candidate IDs must differ.
Compare paired sets by stable keys, retaining arm-native ID sets for audit.

F14 CompositionId excludes the candidate universe. Its equality across arms is expected and does
not imply equal candidate results. Report arm and universe IDs alongside it. Within each arm,
profiles share one universe and identical raw observations/derivations/source graph.

Fix all raw L observations, later-event occurrence time, duration bounds, exact encounter relation,
participant alternatives and their union, source graph, availability/cutoff, truth candidate key,
encounter/start tick, truth Region/participant, realized duration, original truth/world IDs, Region
universe, occupied/unused Regions, starts, density and eight profile definitions. Keep purified
Person-only, exact relation, truth-compatible, no missingness/conflict/misidentification. This is
candidate interval representation, not observation time precision, tick rescaling, source correlation,
Verification, Traversal, physical dwell time or rescue search cost.

## Primary hypotheses and failure criteria

All comparisons use candidate keys as well as native IDs/crosswalks; counts alone are insufficient.

1. None, L, P, L+P return identical key sets across arms (800 equality checks).
2. T, L+T, T+P, Full point key sets are subsets of the respective original sets (800 subset checks).
3. For each T profile, lost keys are exactly original survivors with start < derived T lower bound.
   Their original end reaches that lower bound, including endpoint equality; no new key appears.
4. Original and point arms each satisfy the 12 F16 Joint inclusions (4,800 checks), truth containment
   in all profiles, and no false exclusions. Truth mapping occurs after the public results exist.
5. Raw evidence, all three derived constraints (including T), source graph, components, composition
   IDs and candidate-generation work remain identical. Worlds and truth are not rewritten.
6. Region top/implication flags remain identical. Time-top can only change true -> false, never
   false -> true. Use the full registered Region domain; keep F17 saturation priority unchanged.
7. For the nine conditional additions, define delta = point additional reduction minus original
   additional reduction. T added to L/P/L+P: delta >= 0. L/P added to a baseline containing T:
   delta <= 0, because the set removed by the same Region predicate is a subset of its original
   removed set. P added to L and L added to P: delta = 0. These are algebraic checks, not a field ranking.
8. Input permutation, repeated execution and compatible truth replacement cannot change public
   transform/inference output. No truth type is reachable through transform/inference input records.

Fail before aggregate interpretation on any violated invariant, native-ID/crosswalk collision,
lost multiplicity, foreign/missing profile candidate, original-reference mismatch, missing/extra/
duplicate seed, changed protocol/baseline configuration, private/future evidence, unsupported role,
source-selected operator, nondeterministic output or result-dependent tuning. Invalid inputs fail,
never regenerate. Unexpected empirical frequencies are not failures. Implementation errors may be
fixed transparently without changing the registered transform; any design change after outcomes
requires a disclosed follow-up registration. Do not silently relabel exploratory observations as
unseen confirmation.

## Metrics and analysis plan

Primary: per-seed/profile original and point native IDs and key sets, lost/new keys and coordinate
witnesses, equality/subset checks, count delta, truth containment/false exclusion, fixed-input and
work signatures. Report profile means/medians/ranges and distributions, mean within-world changes,
changed-world counts and zero-effect counts. No significance tests or general L/T/P ranking.

Secondary: all nine conditional reductions in each arm and signed changes; per-comparison and
global 5x5 corrected plateau transition matrices with top/implication flags and affected seeds;
all 28 profile-pair equivalences per arm; minimum counts, all profiles attaining them, inclusion-
minimal field profiles and candidate-key equivalence at the minimum. Report worlds with changed
minimum counts, changed inclusion-minimal profile families, and gained/lost minimum profiles.
Full profile remains a count minimum by Joint monotonicity; its being inclusion-minimal is empirical.

Retain original widths and removed-key endpoint witnesses to explain effects. Saturation and
logical flags remain separately observable. Do not infer a general sufficient/minimal observation
set, or treat a plateau as semantic irrelevance. No frequency target or success threshold is imposed.

## Implementation, validation and artifacts

CLI: `evaluate-candidate-interval-width --experiment
experiments/m1f18_candidate_interval_width/experiment.json --baseline-experiment
experiments/m1f16_common_world_field_combination/experiment.json --seeds 0..199 --output
results/m1f18-candidate-interval-width`.

Write deterministic summary JSON, per-world JSONL, profile CSV, conditional/plateau CSV, crosswalk
CSV and manifest. Include protocol/baseline hashes, source commit/dirty flag, seeds, rule IDs,
crosswalk and representation identities, explicit truth-only evaluation context, all set checks,
profile/conditional summaries, minimum-family changes and interpretation boundary. No current time,
absolute path or random identity in artifacts; generated outputs remain ignored under `results/`.

Meaningful tests: pure pointification/idempotence/order, no truth input, analytic interval crossing
and closed endpoints, no merging, crosswalk identity, original-reference equality, subset/equality
and conditional signs, corrected-domain plateau classification, minimum-family logic, compatible
truth swaps, malformed protocols/seed sets. Run environment, build, full tests, format verification,
all sample scenarios and relevant F16/F17/F18 CLI regressions. Preserve the documented pre-existing
S4 all-strategy failure separately. Independently recompute results from raw public inputs and
crosswalk; repeat final clean F18 commit twice and require every artifact byte-identical.

## Claim boundary and next-decision rule

This study isolates the computational effect of replacing candidate intervals by their starts in
the existing purified synthetic family. A reduction is not calibrated improved accuracy: removed
interval support may be needed when real event time differs from the represented start. Containment
is structurally protected by this generator's anchor convention, not evidence of real-sensor safety.

After results, reassess whether minima are sufficiently robust to become a field-ablation target.
If interval effects are material or sparse support still absorbs constraints, next study nested
candidate density with originals/evidence fixed and truth-independent additions; track unused-Region
rate as an affected property rather than pretending it is independent. Explicit Region/tick maps
remain prerequisites to geometric granularity claims. Do not automatically proceed to field ablation.

Search area/volume, buildings/floors, check locations/actions, time, personnel/equipment, wrong-area
cost and existing-method handoff still require spatial/action representations and calibration.
Closed loop requires actions that generate new public observations; cross-domain SAR and real-data
validation follow those contracts. No new Buddhist correspondence or production renaming is added.

## Results

The protocol was committed and pushed as `940d24c8a002564b3d42cf9943aee8fff7467cc2` before
either the independent Python reference or the C# implementation inspected point-arm outcomes.
Protocol SHA-256: `f8411417d008b8157e2f21c316c2183a198df271bf7b11f9630ae75e3e53d11f`.
All registered 200 worlds and eight profiles were retained. No generator, transform, seed or
analysis definition was tuned after outcomes. The first implementation run correctly records
the preregistration HEAD with a dirty working tree; it is not presented as a clean committed run.

### Paired validity and candidate sets

All 2,412 candidate keys have bijective native-ID crosswalks. The 1,609 positive-width candidates
change native ID; the 803 existing points keep it. A native-ID difference alone is consequently
not evidence of changed membership. None/L/P/L+P pass all 800 key-set equality checks, and the
four T profiles pass all 800 subset checks with no new keys. All 4,800 within-arm Joint inclusions
hold. The original arm reproduces all 1,600 legacy reference profiles, including native IDs,
composition audit and work. Both arms retain truth in all 3,200 primary evaluations, with zero
false exclusions and zero invariant violations.

The original world, fixed truth, raw observations, duration/time evidence, derived L/T/P
constraints, source graph, Regions and starts remain unchanged. Public-input signatures before
and after evaluation agree. Both primary inference arms finish before their truth addresses are
resolved. Compatible truth-replacement and public-input type-boundary tests pass. This audits
the implemented boundary; it does not convert synthetic containment into empirical sensor safety.

| Profile | Mean original -> point | Median original -> point | Range original -> point | Changed worlds / 200 | Lost memberships |
|---|---:|---:|---|---:|---:|
| None | 12.060 -> 12.060 | 12 -> 12 | 8..16 -> 8..16 | 0 | 0 |
| L | 6.550 -> 6.550 | 6 -> 6 | 2..16 -> 2..16 | 0 | 0 |
| T | 5.300 -> 4.190 | 5 -> 4 | 1..12 -> 1..11 | 116 | 222 |
| P | 8.140 -> 8.140 | 8 -> 8 | 2..16 -> 2..16 | 0 | 0 |
| L+T | 3.145 -> 2.600 | 3 -> 2 | 1..10 -> 1..9 | 81 | 109 |
| L+P | 4.965 -> 4.965 | 5 -> 5 | 1..15 -> 1..15 | 0 | 0 |
| T+P | 3.675 -> 2.960 | 3 -> 3 | 1..9 -> 1..8 | 91 | 143 |
| Full | 2.415 -> 2.045 | 2 -> 2 | 1..8 -> 1..7 | 59 | 74 |

All 548 lost profile memberships satisfy exactly `original start < T lower <= original end`.
They concern 222 distinct `(seed,key)` candidates, counted repeatedly when present in several
profiles. Of the losses, 331 have `original end == T lower` (T 138, L+T 63, T+P 87, Full 43),
so closed-interval endpoints materially participate in the mechanism. No observation bound changed.

The within-world reduction distributions below give `removed count: number of worlds`; each row
sums to 200. The four non-T profiles each have `0:200`. Full count reduction is 0.370 on average
(15.3% of the original mean), not a measured improvement in reconstruction accuracy.

| Profile | Original count minus point count distribution |
|---|---|
| T | 0:84, 1:44, 2:45, 3:21, 4:5, 5:1 |
| L+T | 0:119, 1:58, 2:18, 3:5 |
| T+P | 0:109, 1:53, 2:28, 3:6, 4:4 |
| Full | 0:141, 1:47, 2:9, 3:3 |

Full marginal count distributions are original `1:69, 2:52, 3:39, 4:19, 5:14, 6:3, 7:3, 8:1`
and point `1:86, 2:56, 3:35, 4:13, 5:7, 6:2, 7:1`. The deterministic report retains full marginal
distributions for every profile and every conditional comparison, including zero effects.

### Conditional reduction and corrected F17 plateau transitions

Additional reduction means baseline count minus combined count within the same arm. Delta means
point additional reduction minus original additional reduction. All 1,800 registered sign checks
hold. T gains more exclusions after pointification, while a Region field added after T often has
fewer remaining candidates to remove. This does not rank the intrinsic value of L, T or P.

| Addition | Mean original | Mean point | Mean delta | Worlds with changed reduction | Changed diagnostic labels |
|---|---:|---:|---:|---:|---:|
| T to L | 3.405 | 3.950 | +0.545 | 81 | 3 |
| P to L | 1.585 | 1.585 | 0 | 0 | 0 |
| L to T | 2.155 | 1.590 | -0.565 | 78 | 16 |
| P to T | 1.625 | 1.230 | -0.395 | 61 | 17 |
| L to P | 3.175 | 3.175 | 0 | 0 | 0 |
| T to P | 4.465 | 5.180 | +0.715 | 91 | 0 |
| P to L+T | 0.730 | 0.555 | -0.175 | 30 | 18 |
| T to L+P | 2.550 | 2.920 | +0.370 | 59 | 4 |
| L to T+P | 1.260 | 0.915 | -0.345 | 55 | 25 |

The complete aggregate transition matrix uses the corrected F17 full Region domain. Rows are
original, columns point. Reduction denotes additional candidate reduction; the other four outcomes
are plateaus. Saturation keeps F17's priority over other plateau explanations.

| Original / point | Reduction | Observation-to-constraint | Constraint overlap | Constraint-to-candidate | One-candidate saturation |
|---|---:|---:|---:|---:|---:|
| Reduction | 1315 | 0 | 0 | 30 | 26 |
| Observation-to-constraint | 0 | 74 | 0 | 0 | 4 |
| Constraint overlap | 0 | 0 | 103 | 0 | 5 |
| Constraint-to-candidate | 7 | 0 | 0 | 138 | 11 |
| One-candidate saturation | 0 | 0 | 0 | 0 | 87 |
| Point total | 1322 | 74 | 103 | 168 | 133 |

Plateaus increase from 429 to 478, despite lower T-profile counts. There are 83 label transitions
across 47 worlds. Region top/implication and Time-top flags have zero changes; the moved labels
come from membership and saturation, not changed public predicate logic. Seven candidate plateaus
become reductions, while 56 reductions become plateaus. Per-comparison 5x5 matrices, raw flags,
affected seeds and per-world rows are retained, so saturation does not erase the logical audit.

### Minimum profiles and the F16 conclusions

The minimum count changes in 59/200 worlds (29.5%). The entire family of inclusion-minimal profiles
changes in 32/200 worlds (16%); 168 families remain identical. These are sets of field profiles,
not merely the count of fields or one arbitrarily selected winner. All 28 profile-pair key-set
equivalences in each arm are retained and independently checked.

| Profile | Worlds attaining minimum: original -> point | Worlds inclusion-minimal: original -> point |
|---|---:|---:|
| None | 0 -> 0 | 0 -> 0 |
| L | 4 -> 2 | 4 -> 2 |
| T | 20 -> 31 | 20 -> 31 |
| P | 0 -> 0 | 0 -> 0 |
| L+T | 116 -> 124 | 92 -> 91 |
| L+P | 24 -> 20 | 20 -> 18 |
| T+P | 70 -> 89 | 50 -> 58 |
| Full | 200 -> 200 | 42 -> 31 |

Columns need not sum to 200 because a world can have several minimal profiles. Full attaining the
count minimum is guaranteed by Joint inclusion, not empirical evidence of its necessity. Full is
inclusion-minimal in 42 original versus 31 point worlds; one world gains this status and twelve
lose it. T-only becomes inclusion-minimal in eleven more worlds. Even L/L+P minimum status can
change while their own candidate sets stay fixed, because the minimum elsewhere becomes smaller.

F16's fixed-family containment, Joint monotonicity, all no-T memberships, and the existence of
both additional reductions and plateaus survive this manipulation. F16's numerical candidate
burden, conditional magnitudes, plateau frequencies, profile equivalences and exact minimum
families depend on interval representation. F17's correction remains necessary in both arms; it
does not make a plateau label independent of candidate representation.

### Validation and reproduction

Build succeeds without warnings or errors; all 357 tests pass (328 baseline plus 29 F18 cases).
Environment check and `dotnet format FootprintSim.sln --verify-no-changes --no-restore` pass.
All six sample scenarios and 13 additional supported legacy evaluation commands pass, including
200-seed S3/S4/S5 telemetry evaluations, F8/F9/F10/F11/F12/F14/F15 and S0/S1/S2 all-strategy runs.
Separate F16/F17 replay checks compare the legacy artifacts, permitting only source-commit and
dirty-tree manifest metadata to differ. The protocol and baseline experiment bytes remain frozen.

The independent Python reference starts from the immutable original F16 per-world artifact
(SHA-256 `6c114058f7e9479ae29e3d16030f10843ddfb71b8b8bc861d13f8840c0cd4c81`). It recomputes both
arms from raw L Regions, raw P alternatives and raw later-event/duration evidence before checking
truth. It agrees on 3,200 profile memberships/work, 3,600 diagnostics, 11,200 equivalences,
400 minimum families, 2,412 crosswalks, 1,800 conditional deltas and all 548 loss witnesses, plus
the complete reported aggregate statistics and transition matrices. It checks native coordinates,
uniqueness and crosswalk behavior; it does not independently reimplement the native hash factory.

```bash
python3 scripts/verify-candidate-interval-width.py \
  --baseline-runs results/m1f17-baseline-audit/f16-a/common-world-field-combination-runs.jsonl \
  --actual-runs results/m1f18-final-a/candidate-interval-width-runs.jsonl \
  --output results/m1f18-final-independent
```

For clean-commit reproduction, run the registered F18 CLI twice with outputs `results/m1f18-final-a`
and `results/m1f18-final-b`, and compare all six artifacts byte for byte. Each manifest records the
actual execution commit and dirty flag; outputs and validation evidence remain ignored. The tracked
code, frozen inputs, report and independent verifier are the durable reproduction package.

Post-registration implementation review tightened rejection of inconsistent temporal kind/width
and named distribution frequencies `Count`, since the global conditional distribution counts
comparisons rather than worlds. The independent verifier initially compared gained minimum-profile
sets in profile order; it now compares set content and separately requires unique ordinal output
order. These corrections changed neither the registered design nor any prospective outcome.

The generic README `--scenarios scenarios --strategies all` command still fails on the known S4
evaluation-domain mismatch (`person-b-home`, exit 2). Its three documented strategies succeed.
This predates F17 and is independent of the F18 candidate-support experiment, so it is not silently
repaired here. Repair it in a separate change before cross-scenario all-strategy comparisons.

### Research decision after F16/F17/F18

Do not yet promote per-world minimal field combinations into general field requirements or
automatically begin field ablation. The mechanism of this interval manipulation is now understood
exactly, but its 32 changed minimum families and sparse-support plateaus show that minima are still
properties of the representation as well as the evidence. The next highest-value factor is nested
candidate density: retain original candidates and raw evidence, predefine public space/time support,
and add candidates without consulting truth. Region occupancy/unused-Region rate must be reported
as a coupled consequence of filling that support. It is not an independently held factor. This
follow-up needs its own semantic audit and preregistration; no density outcomes are generated here.

The distant Footprint goal still needs a justified mapping from represented candidate states to
Region/building/floor, area or volume and actual check locations/actions. Only then can observations
from dogs, calling, acoustic/vibration sensing or cameras support calibrated action cost, expected
time, personnel/equipment and wrong-area cost. Nothing in the present candidate-count change
estimates those quantities. Physical spatial/tick mapping, action-generated observations, inference
updates, cross-domain SAR and real-data validation remain subsequent stages. F18 advances the
auditability of synthetic support and its minimum-profile claims; it establishes no operational
rescue performance or general guarantee that pointification safely discards temporal uncertainty.
