# M1-F17 plateau domain audit and preregistered diagnostic controls

## Independent assessment and decision

Baseline: F16 final `e68e5a88b9feb103f05ec9d768ecbc3b85bd5f11`, with preregistration
`5fe8bf33f6dde41f3f77fa09b29a59f42f84bb4d`. Work starts on a separate branch; main is unchanged.
The baseline environment/build and all 300 tests passed, and two 200-world reproductions were
byte-identical. F14 composition, F15 raw public derivation, and F16 paired field selection remain
a useful purified internal baseline, not an operational rescue model.

The audit found a prerequisite to representation sensitivity: F16
`AddedPredicateImpliedByBaseline` starts from Regions occupied by candidates, whereas L/P
`AddedPredicateIsTop` uses the declared `RegionUniverse`. Thus the implication diagnostic can
mistake the absence of a represented candidate for a logically redundant Region restriction.
This is an evaluation-diagnostic defect; it does not alter candidate generation or safety results.

Counterexample fixed from code inspection: declared Regions `{a,b,c}`, candidates `a@10,a@11,b@12`,
L=`{a,c}`, P=`{a,b}`. Adding P to L changes no candidate, but L does not imply P because unused c
is allowed by L and rejected by P. The appropriate full-domain category is
ConstraintToCandidatePlateau, not ConstraintOverlapPlateau.

**Disclosure of prior outcome access:** during independent auditing, an agent read existing F16
results and computed an exploratory full-domain correction before this document was committed:
40 implication flags changed; 35 classifications in 19 worlds changed from constraint overlap
to constraint-to-candidate (143 -> 108 and 121 -> 156). These are known diagnostic findings,
not prospectively predicted results. Recomputing them is correction/reproduction, not a new
confirmatory experiment. No generator condition was tuned. The two domain controls below have
not been run; their conditions, hypotheses, and failures are frozen by this commit before execution.

The next question is: which F16 plateau explanations survive a consistent explicit Region domain,
and can domain-only diagnostic changes alter labels while all evidence and candidate ID sets stay
fixed? This must precede interpreting plateau migrations in a physical representation experiment.

## Frozen correction and prospective controls

Generate the original F16 worlds once per seed, using its unchanged experiment ID, generator ID,
configuration bytes, purpose streams, and seed range 0..199. Use all eight original profiles,
12 inclusion checks and nine additions. Reuse the original evaluator and retain its legacy outputs.
Do not rename generator IDs: they participate in random-stream derivation and would break pairing.

Project a truth-free diagnostic input containing only seed, declared Regions, occupied Regions,
public L/P Region predicates, T-all-candidate-intervals-pass flag, profile selected axes and candidate
ID sets, and the legacy diagnostic row. The classifier receives no world truth, truth labels,
observations, occurrences, source graph, rescue scores or inference request. It cannot change a
candidate set. Keep each original candidate ID, including interval/provenance identity.

For every one of the 1,800 additions, compute the same priority-ordered category (additional
reduction, then one-candidate saturation, top, implication, constraint-to-candidate) in three
explicit diagnostic Region domains:

| Domain | Definition | Role |
|---|---|---|
| registered | Original declared RegionUniverse | Corrected primary interpretation |
| occupied | Distinct original candidate Regions | Prospective domain restriction control |
| extended | Original declared Regions plus `m1f17-unused-region` | Prospective unused-domain extension control |

Both L/P top and Region implication use the selected domain. Implication is tested only if the
baseline selects L or P: intersect that domain with its selected Region predicates, then test
subset of the added predicate. Time addition has no Region implication. Keep F16's explicitly
registered Time-top definition (all represented candidate intervals overlap T) in every domain.
Do not silently make Time and Region stage counts comparable.

The sentinel is absent from every candidate and every L/P predicate. Only the diagnostic domain
changes. It is not a new generated Region, observation, public transition or evidence-precision
treatment. The occupied control restricts diagnostic scope; it does not declare other Regions
impossible. None of these controls measures physical Region granularity or calibrated unused-area
rates. They isolate label dependence on the domain against a fixed inference result.

## Fixed conditions and hypotheses

All F16 raw evidence, source ancestry, exact relation, Person-only semantics, intervals, truth,
availability, profiles, constraint rules, IDs, work and candidate sets remain fixed. No missingness,
conflict, misidentification, source-correlation manipulation, precision interaction or new logical
operator is introduced. Saturation retains priority and independent flags are also retained.

1. Legacy -> registered may change only implication true -> false, and the category may change
   only ConstraintOverlapPlateau -> ConstraintToCandidatePlateau. Zero-effect totals are invariant.
2. Registered -> occupied may change top/implication flags and plateau labels; no direction or
   frequency is predicted. Candidate IDs, additions and saturation remain invariant.
3. Registered -> extended preserves Region implication: the sentinel belongs to neither baseline
   Region predicate. Top can only change true -> false for L/P. Category changes can only be
   ObservationToConstraintPlateau -> ConstraintOverlapPlateau or ConstraintToCandidatePlateau.
   Time diagnostics, additional reductions and saturation are invariant. No frequency is predicted.
4. Reordering/duplicating set inputs changes neither canonical diagnostic output nor classification.

Failure blocks interpretation: missing/extra/duplicate seeds; baseline configuration hash mismatch;
undeclared candidate or L/P Region; sentinel collision; non-nested combined candidate ID set;
empty baseline/combined set in this purified family; changed evidence/inference/generator semantics;
truth/private/future data in the classifier; violation of the above migration constraints;
unexplained candidate/work/source changes; nondeterministic serialized artifacts; result-based tuning.
Invalid data fail, never skip or regenerate. Unexpected control outcomes require an explicit
follow-up registration, never adjustment of this protocol.

## Implementation, tests and outputs planned before control execution

Add a separately versioned evaluation-only classifier and CLI command
`audit-common-world-plateau-domain --experiment experiments/m1f17_plateau_domain_audit/experiment.json
--baseline-experiment experiments/m1f16_common_world_field_combination/experiment.json
--seeds 0..199 --output results/m1f17-plateau-domain-audit`.
The F16 command/schema/results and production inference retain their historical meaning and bytes
(apart from ordinary manifest commit metadata). F17 becomes the corrected interpretive reference.

Write deterministic JSON summary, per-comparison JSONL, CSV, and manifest with source commit,
dirty flag, protocol and baseline hashes, seed list, rule/domain definitions and output filenames.
Retain domain Region IDs, legacy and three new category/flag records, baseline/combined candidate
IDs, per-comparison migrations, affected seeds and full classification transition matrices.
Report original profile aggregates/safety and input/output identity signatures; no significance
tests or general field ranking. Generated artifacts stay untracked.

Test the analytic counterexample, logical overlap, saturation precedence, Time unchanged,
domain controls, malformed domains/seeds/configuration, set order invariance, and truth-free API.
Run environment, build, full tests, format verification, all six sample scenarios and relevant
README evaluations. Compare F16 payloads against the clean baseline excluding only commit/dirty
manifest metadata. Run the final clean F17 commit twice and compare every artifact byte for byte.

## Research sequence and claim boundary

F16's cross-seed variation motivates sensitivity but does not establish which representation
factor causes the observed per-world minima. Correct diagnosis comes first; D -> A is therefore
revised to diagnostic repair -> bounded representation sensitivity -> reconsider field ablation.

After this audit, first vary candidate interval width on a once-generated public candidate universe,
keeping start/Region/stable keys and all raw evidence fixed (pointification versus F16 intervals,
with explicit coordinate/ID crosswalk). This isolates temporal candidate support from observation
precision. Then study nested candidate density with a public, truth-independent construction;
naive thinning can remove truth and cannot count as an inference false exclusion. Region granularity
requires a parent/child map for candidates and evidence and cannot be approximated by changing
Region counts. Tick granularity requires explicit rounding and inclusive-bound semantics; distinguish
unit rescaling invariance from lossy coarsening. Unused-Region rate is coupled to density/support
and should be studied with fixed predicates and occupied cells. Participant overlap should follow
with a controlled P union/marginals, since changing overlap usually also changes P strength.
Each factor needs a separate frozen transform; do not combine all six in one run.

Field precision, source-level ablation/independence, missingness/delay/conflict/misidentification,
and S5 migration remain later stages. Candidate keys are historical hypotheses, not buildings,
floors, square metres, check locations, actions or rescue time. Search burden requires an explicit
spatial/action model, effort and wrong-area cost before existing-method handoff, closed loop,
cross-domain SAR or real observations. No general minimum field/source set, real rescue performance,
field importance ordering or direct Buddhist paccaya mapping follows from this audit.

## Results

Registration commit: `fe630c20ed0398e2a76ad334a457a174691b8fd2`, pushed before the prospective
controls. Protocol file SHA-256:
`52c1a24b6b1ef3e84c7b59c219765ec2e1793d14c1c1728f8f2644a6dd0f54bc`.
The registered protocol and F16 generator/configuration were not modified after outcome access.
An independent Python implementation first ran the prospective controls from the original clean
F16 JSONL at registration HEAD with implementation changes in the working tree. The C# F17 CLI then
recomputed the same fixed worlds. All 1,800 comparison records, 7,200 domain records (including
legacy), Region witnesses, candidate IDs, flags, transitions and aggregate counts agreed exactly.
The known legacy correction remains exploratory correction/reproduction, as disclosed above.

### Candidate results and safety

The unchanged evaluator generated 200 worlds and 1,600 profile evaluations once per complete run.
The three new diagnostic domains account for 5,400 classifications; they are not 600 independent
worlds or new inference experiments. Containment is 1,600/1,600, false exclusion 0, and all 2,400
candidate-ID inclusion checks pass. The profile mean counts remain
`12.060 / 6.550 / 5.300 / 8.140 / 3.145 / 4.965 / 3.675 / 2.415` in F16 order. Full-profile
saturation remains 69 worlds and inclusion-minimal full profile remains 42 worlds. There are still
1,371 positive additions and 429 plateaus, with identical candidate IDs, per-world minima,
derivation/composition identities, source graph and work.

The manifest's public-inference input/output signatures are snapshots **before and after running
the diagnostics**, not names for different inference pipeline stages. The full legacy result is
also hashed before/after to detect mutation. Those checks establish non-mutation; separately,
re-running the F16 command reproduces all eight baseline artifacts, with only source-commit/dirty
manifest metadata differing. No production derivation, composer, generator, scenario or legacy
F16 evaluation implementation was edited.

### Corrected explanations and prospective controls

| Classification | F16 legacy | Registered domain (corrected) | Occupied domain control | Extended domain control |
|---|---:|---:|---:|---:|
| Additional candidate reduction | 1,371 | 1,371 | 1,371 | 1,371 |
| Observation-to-constraint plateau | 78 | 78 | 84 | 0 |
| Constraint overlap plateau | 143 | 108 | 139 | 159 |
| Constraint-to-candidate plateau | 121 | 156 | 119 | 183 |
| One-candidate saturation | 87 | 87 | 87 | 87 |

The correction changes 35 classifications in 19 worlds, all overlap -> constraint-to-candidate:
P added to L: 9; L added to P: 11; P added to L+T: 6; L added to T+P: 9. Forty implication flags
change; five are hidden by the intentionally higher-priority saturation category. Every changed
implication has a concrete declared Region allowed by the baseline predicate but rejected by the
added predicate, with no represented candidate occupying it. This corrects the reason for zero
effect without treating an unavailable candidate as measured absence in the world.

The registered -> occupied control changes 37 labels in 19 worlds: 31 constraint-to-candidate ->
overlap, and six constraint-to-candidate -> observation-to-constraint. Six top flags and 40
implication flags change. Restricting the diagnostic domain can therefore hide both non-universality
and non-implication while every candidate result stays fixed.

The registered -> extended control changes 78 labels in 27 worlds: 51 observation-to-constraint ->
overlap, and 27 observation-to-constraint -> constraint-to-candidate. Eighty-four top flags change;
six are hidden by saturation. Region implication never changes. Adding a sentinel outside all
L/P predicates removes their universality over the extended domain. Time diagnostics do not change
and no Time predicate was top in these worlds. All preregistered migration constraints hold.

Thus even the pipeline-stage label is conditional on an explicitly declared domain and classification
priority. The extended control does not show that real evidence deteriorated, nor that physical
unused space increased search burden. It changed the scope of a diagnostic question only.

### Validation and audit limitations

Ubuntu 24.04 / .NET SDK 8.0.424: environment check and build pass with no warnings/errors; all 328
tests pass (300 existing plus 28 new test cases). `dotnet format --verify-no-changes` passes. Tests
cover the analytic unused-Region counterexample, saturation with independent flags, both domain
controls, Time invariance, set ordering/duplicates, invalid domains and seeds, baseline file/object
drift, unknown protocol fields, truth-free input types and compatible truth/participant swaps.

All six sample simulations and the scenario-specific 200-seed evaluations pass: S0/S1/S2/S3/S5
with all strategies, S4 with its documented three-strategy set. F8/F9/F10/F11/F12/F14/F15 registered
CLI regressions pass, as does F16. These checks were repeated with the rebuilt application.

One **pre-existing failure remains**: the generic README all-scenario/all-strategy command and S4
with all strategies fail with `Strategy 'cross-thread-constrained' classified unknown region
'person-b-home'`. S4 contains the public witness-home Regions but excludes them from its
evaluation-searchable domain (`scenario.json`, `evaluationSearchableRegionIds`). Its missing weft
definition triggers the cross-thread strategy's HazardAware fallback; early normal-movement
candidates include the nearby homes. `PerRunEvaluator` then rejects valid public Regions outside
the evaluation domain. This was reproduced on the original F16 binaries and the rebuilt application;
it is not a passing regression and is documented in README. Fix the evaluation-domain/fallback
contract separately before using all-strategy S4 comparisons. Changing that contract during F17
would alter unrelated historical comparison semantics, so this audit records it without silently
changing the scenario or treating those Regions as impossible.

The final clean commit is the reproduction target. Run the F17 command twice into distinct output
directories and compare all four files with `diff -qr`. Commit-independent row and CSV hashes are:

- `plateau-domain-audit-runs.jsonl`:
  `eba6a5addc7788d2b56488b9f6a443fccaf5029acee503ceb88c741add067b8f`;
- `plateau-domain-audit.csv`:
  `c4f294e061657705e6ab5778e2b9ab0c549d662f38ee5fbdbeeea8ac1286f2f5`.

Summary/manifest hashes depend on the final source SHA and clean status and are therefore recorded
by final validation rather than embedded recursively into their own source commit. Generated files
remain under ignored `results/`; no outputs, SDK or build artifacts are committed.

### Revised research judgment

The audit supports keeping the F14/F15/F16 public inference baseline. It also demonstrates a
specific diagnostic dependence on unoccupied declared Regions, and corrects an overstatement in
F16's causal explanation of plateaus. It does **not** establish robustness of field-combination
effects across physical representations: those effects were deliberately fixed here. Source ancestry
partitions identify provenance groups; they do not establish statistical independence.

Next choose a preregistered **candidate interval-width** comparison before field ablation. Generate
F16 once, compare original intervals against pointification at each existing start tick, retain all
candidate keys/Regions and raw L/T/P evidence, and keep a key-to-candidate-ID crosswalk. Test equality
for profiles without T and subset containment by stable keys for profiles with T; inspect conditional
deltas, corrected-domain plateau transitions and changed minimum profiles. The F16 truth starts
at the candidate start, so this family allows truth-compatible pointification without consulting
truth during transformation. This is candidate representation sensitivity, not observation precision.
The exact transform, hypotheses and failure criteria still require their own registration before
execution; no F18 outcomes were inspected in this task.

Then assess nested candidate density and explicit spatial/temporal mappings as described above.
Do not promote per-world minima into a general minimal field study after this diagnostic control
alone. Repair the separate S4 evaluation-domain defect before cross-scenario strategy comparisons.

Relative to the distant Footprint goal, the work now has a more trustworthy explanation of where
information stops reducing synthetic historical hypotheses. It still has no calibrated mapping to
Region/building/floor search, area/volume, check locations, actions, expected discovery time,
personnel/equipment, wrong-area cost or existing rescue-method handoff. Those representations and
an observation-generating action model are prerequisites for closed-loop and real-data claims.
