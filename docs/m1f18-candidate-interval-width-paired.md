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

Pending registered execution. Only original F16/F17 outcomes and algebraic consequences are known.
