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

Pending prospective control execution after registration. Exploratory correction access is
disclosed above and will not be relabeled as unseen confirmation.
