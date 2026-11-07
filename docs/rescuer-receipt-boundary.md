# Rescuer receipt boundary

Work-Key: `FP-RECEIPT-BRIDGE-V1`

Scope: [Issue #76](https://github.com/uobnnisiuk/footprint-sim/issues/76)

## Purpose

This slice connects one device-held interaction record to the existing candidate
and causal-tracing implementations after receipt by one specified rescuer.
Existence, device possession and the original evidence's availability are separate
from that rescuer's access. Previously, the public/available-at filters did not
represent that last condition.

The slice accepts an explicitly supplied `InteractionObservation` and explicit
receipt assertions. It does not simulate recording, discovery, radio transfer,
movement, delivery probability or rescue priority. It introduces no new inference
rule and uses no truth event, occurrence, final entity state or realized path.

## Contracts and use

`DeviceHeldInteractionRecord` contains a holder device identifier, a storage tick
and one `PrivateEvidence` observation. Storage cannot precede the original
`AvailableAtTick`. The existing observation constructor already requires that
availability follow the end of the observed window. Explicit `WorldEvent`
provenance is rejected at this boundary.

`InteractionRecordReceipt` identifies the scenario, seed, original record
(`SourceEvidenceIdentifier`), holder device, receiving rescuer and receipt tick.
It asserts delivery of that already identified original; it does not create an
independent evidence source. The holder must be listed in the public context's
device identifiers. No device position is promoted to a person's position.

The integration entry point is:

```csharp
var snapshot = new RescuerEvidenceInputBuilder().Build(
    publicContext,
    seed,
    currentTick,
    rescuerId,
    heldRecord,
    receipts);
var result = new MultiWeftTracingStrategy().Evaluate(snapshot.Input);
```

The returned snapshot is scoped to `RescuerId` and the input's `CurrentTick`.
Before receipt, `ReceivedRecords` and both constraint sets are empty, even if the
original record is already held and its availability tick has passed. Entries for
other runs, records, holders or rescuers cannot establish access. Future receipt
entries are excluded before validation for this query; merely having a future
entry in the ledger does not invalidate an earlier snapshot. A matching receipt
at or before the cutoff that predates storage, original availability or the
observed window is rejected.

After the earliest matching receipt, `ReceivedRecords` contains one access wrapper
around the unchanged original observation. The wrapper records its original
record ID, original observation ID, holder, rescuer and first receipt tick. It does
not recreate the original with a different visibility or availability.

The builder uses the existing `DerivePersonEncounterCoLocation` rule. When that
rule applies, it projects the derived constraint into the rescuer's input with
availability equal to the first receipt tick, and creates the corresponding
existing local-role causal-tracing constraint. Each set contains at most one
constraint. A device-only or otherwise inapplicable observation remains a received
record but produces no person constraint.

`PublicEvidence` on these constraint views means usable within the specified
rescuer's input. The original remains `PrivateEvidence`; there is no shared
evidence-store update. Callers must build a snapshot for each rescuer rather than
reuse one rescuer's input as another's. The optional public search domain is an
explicit query scope and retains the existing search-domain projection semantics.

`AvailableEvidence.Observations` stays empty: an interaction's historical time
window is not converted to a point-position `ObservationRecord`.
`CrossThreadConstrainedStrategy` still requires its separately supplied own
observation; this slice does not fabricate one. The cross-thread constraint set is
usable by the existing historical `CrossThreadConstraintApplicator`, while
`MultiWeftTracingStrategy` consumes the causal-tracing set and public candidate
model directly.

## Identity, ancestry and repeated delivery

Both projected constraints retain the original `ObservationId` as
`SourceObservationId` and the original `SourceEvidenceIdentifier` as their direct
evidence root. Their observed and constrained windows, relation, participants,
person/device flags and evidence state are unchanged. The original provenance is
retained. Evidence-record, observation and external-source provenance supply the
tracing ancestry; the shared derivation-rule identifier is not an independent
source or a reason to merge otherwise independent sources.

The cross-thread view has a deterministic identity including its access context.
Its rescuer identifier and receipt timing do not become source ancestry. Delivery
counts, holder IDs and delivery identifiers do not become independent evidence.
The earliest matching receipt determines the view. Additional receipts of the
same original, duplicate entries and reordered receipt lists therefore produce
the same input and inference output at the same query tick.

## Hand-checkable integration example

The tests declare public alternatives `{A, B}` at tick 2 and `{C, D}` at tick 5,
with public three-tick transitions `A → C` and `B → D`. The one original
interaction observation reports `A` at tick 2, is available and stored at tick 3,
and is first received by `rescuer-a` at tick 5. The public model contains both
transitions and no selected/realized transition.

| Query / receipt condition | Received originals | Constraints per set | Tick-5 result |
| --- | ---: | ---: | --- |
| No receipt, queried at tick 5 | 0 | 0 | `{C, D}` |
| Receipt at tick 5, queried at tick 4 | 0 | 0 | Receipt is unavailable |
| Confirmed receipt available at tick 5 | 1 | 1 | `{C}` |
| Same original received again | 1 | 1 | Same result at the same query tick |
| Uncertain original received | 1 | 1 | Preferred `{C}`, outer `{C, D}` |
| Conflicting original received | 1 | 1 | Conflict retained; outer `{C, D}` |
| Device-only original received | 1 | 0 | `{C, D}` for the person query |

The confirmed case narrows the historical state to `A` and follows the public
transition to `C`; it does not intersect an encounter region directly with the
current candidates. Repeated receipt leaves one independent source group.

## Verification and remaining scope

`RescuerEvidenceBoundaryTests` covers the receipt boundary, real candidate/tracing
integration, original identity and ancestry, delayed access, repeated receipt,
input ordering, recipient/run/holder/record mismatches, temporal inconsistencies,
future-ledger isolation, exact versus interval observations, uncertain/conflicting
outer alternatives, device-only evidence and original-record validation.

Run the tests with:

```bash
dotnet test FootprintSim.sln --filter FullyQualifiedName~RescuerEvidenceBoundaryTests
```

The repository-wide AGENTS build/test/format, scenario and evaluation checks
remain the handoff gates. This document specifies the contract and expected
example; execution results are recorded in the PR and CI rather than assumed here.

Existing scenario loading, simulation, batch evaluation and output schemas remain
unchanged. The programmatic bridge is also used by an opt-in receipt timeline
CLI; no new encounter rule, communication simulator, or rescue priority is added.

## Opt-in receipt timeline CLI

The next bounded slice is [Issue #78](https://github.com/uobnnisiuk/footprint-sim/issues/78).
It reads one immutable private interaction observation, one holder, one recipient,
an explicit receipt ledger and ordered query ticks from a dedicated JSON fixture.

Run the following from the repository root:

    dotnet run --project src/FootprintSim.Cli -- evaluate-rescuer-receipt-timeline --fixture experiments/rescuer_receipt_timeline/fixture.json --output results/rescuer-receipt-timeline.json

The fixture's schemaVersion 1 is deliberately separate from existing scenario
and evaluation schemas. Unknown keys, duplicate keys and unsupported enum names
are rejected. The CLI uses the existing builder and MultiWeftTracingStrategy;
it never reconstructs receipt logic, takes simulated truth, creates positional
observations from an interaction, or modifies shared evidence.

The dedicated report contains the fixture's raw byte SHA-256, ordered query
ticks, received-original counts, both constraint counts, and actual source groups
and inference results. Original record IDs, observation IDs and ancestry enter a
row only after an eligible receipt. A repeat delivery cannot add another source
group. The sample's pre-receipt tick 4 returns public {A,B}, first receipt at
tick 5 returns {C}, and tick 7 remains {C}; comparison at the same tick rather
than across ticks establishes the duplicate-delivery no-op. Public alternatives
at tick 5 without receipt remain {C,D}. Tick-7 projection is a bounded public
model convention, not a real-world position claim.

The report excludes current time, absolute paths and random identifiers. Results
for unchanged fixture bytes are deterministic. Receipt order may change raw input
SHA-256, while normalized query results remain identical. The report is published
only after all query evaluations succeed, using a temporary file in the output
directory; errors do not replace an existing complete report. This remains a
single-record synthetic CLI, not multi-record forwarding or field operation.

## One original copied to multiple holders (Issue #80)

The overload accepting `IEnumerable<DeviceHeldInteractionRecord>` models a single
immutable observation explicitly held by more than one publicly declared device.
The caller must supply each holder and storage tick; no communication, discovery,
ownership authorization, identity authentication or physical transfer is inferred.
The original source ID, observation ID, content and provenance must match across
all copies. Contradictory originals, runs and same-holder storage assertions are
rejected rather than resolved by whichever item happens to appear first.

Eligible receipt assertions must name that original, a supplied holder, the queried
rescuer and run, and be available by the query cutoff. A receipt cannot predate its
holder's storage, the original evidence availability or the observation's end.
All matching receipts at the cutoff are validated, even if another holder has a
valid earlier delivery. Other recipients, runs, unknown holders and future
deliveries do not grant this rescuer access. This is not verification that a
receipt truly happened.

The earliest valid receipt chooses the access time and the stored copy supplied
to the existing one-holder builder. When two holders have the same first receipt
tick, the representative holder ID is ordered with `StringComparer.Ordinal`.
This selection affects only the received-wrapper holder display; it does **not**
modify the original, add a new evidence source, add a holder or delivery to
ancestry, or create a new inference rule. Later arrivals and repeated receipts
cannot change the snapshot at the same query tick. Person/device evidence,
exact versus interval observations and uncertain/conflicting alternatives retain
the single-holder semantics.

For a concrete synthetic example, holder A stores an original at tick 3 and
holder B stores the *same* original at tick 4. Their receipts to one rescuer
arrive from B at tick 5 and A at tick 7:

| Query | Rescuer-held original | Cross-thread / causal constraints | Inference |
| --- | --- | --- | --- |
| tick 4, neither received | 0 | 0 / 0 | Only public alternatives |
| tick 5, B received | 1 | 1 / 1 (person evidence) | Existing model + original |
| tick 7, B received | 1 | 1 / 1 | Same tick-7 result as both |
| tick 7, B and A received | 1 | 1 / 1 | Same result, one source group |

The integration tests exercise the actual `MultiWeftTracingStrategy`, including
same-tick first-receipt ties, ordering, temporal conflicts, unrelated/future
ledger entries, uncertainty and device-only evidence. These are deterministic
contract tests with synthetic values, not evidence of network integrity,
real-world rescue accuracy or several independent original encounters. The
existing one-holder API and the opt-in timeline CLI remain unchanged.

## Two distinct originals in one query (Issue #82)

Work-Key: `FP-DISTINCT-ROOT-RECEIPT-COMPOSITION-V1`

`BuildDistinctOriginals` accepts exactly two explicitly identified original
observations, each with one declared holder, and one rescuer's receipt ledger.
Both `SourceEvidenceIdentifier` and `ObservationId` must be distinct. Empty,
null, repeated-root and conflicting same-root inputs are rejected; copies of
one original still use the existing multi-holder `Build` overload. An empty
receipt ledger is valid and grants access to neither original.

The entry point materializes the ledger once, rejects null entries, and reuses
the existing single-holder `Build` for each original. Thus every original must
match the query run and declare a public holder. All currently matching receipts
are checked, including invalid assertions alongside a valid other original.
Unrelated and future deliveries cannot grant access or narrow current candidates.
Future temporal inconsistencies are not consumed until their receipt cutoff.

Eligible received wrappers are ordered by original source ID, observation ID,
and first legal receipt tick. Each original's constraint views are combined into
one `SearchStrategyInput` before the actual `MultiWeftTracingStrategy.Evaluate`
call. The builder does not evaluate two separate candidate sets and combine those
outputs afterwards. No point-position `ObservationRecord` is fabricated.
Original bytes, visibility, observed windows, evidence states and ancestry remain
unchanged. Delivery counts and holder identities do not enter source ancestry.
Two different root IDs do not prove independent observations: the existing tracer
still joins constraints with overlapping source ancestry into one source group.

In the existing public `{A,B}` at tick 2 to `{C,D}` at tick 5 example, one
original reports `{A,B}` at tick 2 and arrives at tick 3. A second reports `{A}`
at tick 2 and arrives at tick 5. Both were available and stored at tick 3.
The following comparisons all query tick 5 and keep the public model fixed:

| Available originals | Constraints per set | Tick-5 outer candidates |
| --- | ---: | --- |
| Neither | 0 | `{C,D}` |
| Broad record only | 1 | `{C,D}` |
| Both confirmed records | 2 | `{C}` |
| Broad confirmed plus uncertain narrow record | 2 | `{C,D}` (preferred `{C}`) |
| Broad confirmed plus conflicting narrow record | 2 | `{C,D}`, conflict retained |
| Broad confirmed plus device-only record | 1 | `{C,D}` |
| Contradictory confirmed `{A}` and `{B}` records | 2 | `{C,D}`, conflict retained |

Reordered inputs and repeated delivery leave the same snapshot and inference at
the same cutoff. Swapping actual arrival times changes access times, not the
identity or ancestry of either original. Tests exercise real tracing, not a
substitute inference implementation; expected results and execution evidence are
kept distinct until the PR's CI has run.

This is a common input bridge for comparing use of multiple distributed traces.
It is not a new causal discovery method, a completed method comparison, a proof
of real rescue benefit, or authentication of original/receipt assertions. The
existing CLI fixture and report schema remain single-original and unchanged.
Following the owner's research realignment, the next priority is a bounded
same-information comparison of a simple last-seen baseline, known-relation
traversal and distributed-trace reconstruction, reusing existing M1-D/E methods.
This slice does not select a winning method or equate unimplemented hypotheses
with rejected ones.


## Receipt-gated comparison of three historical methods (Issue #84)

Work-Key: FP-RECEIPT-GATED-METHOD-COMPARISON-V1. This uses the same
two-original receipt builder as Issue #82 and the existing M1-D/E public
reconstructor and known-relation tracer. No new causal law, physical
transition, device communication, position observation or source
authentication is added.

**Question and initial concept.** When the target person's own history is
missing, can later-received records from other holders (the "weft") constrain
the target's earlier candidate locations, without allowing information
from unreceived originals or evaluation truth into the inference? The
comparison deliberately does not assume that reconstruction must win.

The synthetic world is fixed before inspecting the comparative output:
public target state at tick 2 is {A,B}; public later state at tick 5 is
{C,D}; only published A->C and B->D transitions take three ticks.
There is no directly confirmed person position. A target-window encounter
says {A} at tick 2, original available/stored at tick 3, first received
at tick 6. A distinct other-holder record says {C} at tick 5, original
available/stored at tick 6, first received at tick 7. Holders are
explicitly declared. The second original is a later trace, not a
second independent target-window observation. These observed regions are
synthetic assertions, not truth. Neither observation is a location
measurement fabricated from the event.

The comparison adapter is
src/FootprintSim.Core/Evaluation/RescuerEvidenceMethodComparison.cs.
All methods use the same BuildDistinctOriginals public projection at the
same cutoff, public candidate states, target window, published transition
model, run and rescuer. Method-specific evidence roles are explicit:

| Family | Method row | Evidence role | Intended interpretation |
| --- | --- | --- | --- |
| A | public-prior | No encounter constraints; last-confirmed unavailable | Public {A,B} target-window comparison set, **not** zero candidates |
| B | known-relation-backward | Later-window received original as traversal only | Existing backward public-relation traversal |
| C | distributed-reconstruction | Target-window received original as generation only | Existing M1-E historical candidate construction |
| C | distributed-reconstruction-local | Target-window generation; *separate* later original as traversal | Existing M1-E local state selection and real transition traversal |

The two C rows are diagnostics of one method family, not four
independent research hypotheses. A single M1-E Compare invocation
produces several methods; B and C are therefore invoked in distinct
contexts and only their selected results are compared. The target-window
generation source is not fed back to traversal pruning. There is no
independent verification source, so verification is not manufactured.

The fixed arrival series queries ticks 5, 6, 7. A separate ablation
runs at tick 7 with zero originals, only the first, only the second,
and both; withholding an already-received original is an experimental
ablation, not a statement that it was inaccessible. Thus differences
between arrival rows are changes in available information, while
differences between method rows at a fixed cutoff are changes in method.

The following table records **verified outputs** from the real method
adapter, fixed by direct assertions against all rows in
`RescuerEvidenceBoundaryTests`. On implementation head
`3d07152411dedc879ee483b9084740cefaad07ff`, [GitHub ordinary CI
run 42278720202](https://github.com/uobnnisiuk/footprint-sim/actions/runs/42278720202)
completed successfully, including build, test, formatting, S0/S1 and the
evaluation harness. This is a model-level check, not a measured rescue outcome.
All rows use the same public world and target window. The last four are
same-tick ablations at tick 7, not claims of real access.

| Cutoff / eligible originals | A public | B backward | C generation | C local | Available ancestry groups | Used groups A/B/C/C-local |
| --- | --- | --- | --- | --- | ---: | --- |
| tick 5 / none | {A,B} | {A,B} | {A,B} | {A,B} | 0 | 0/0/0/0 |
| tick 6 / target-window | {A,B} | {A,B} | {A} | {A} | 1 | 0/0/1/1 |
| tick 7 / both | {A,B} | {A} | {A} | {A} | 2 | 0/1/1/2 |
| tick 7 / ablate both | {A,B} | {A,B} | {A,B} | {A,B} | 0 | 0/0/0/0 |
| tick 7 / target only | {A,B} | {A,B} | {A} | {A} | 1 | 0/0/1/1 |
| tick 7 / later only | {A,B} | {A} | {A,B} | {A} | 1 | 0/1/0/1 |
| tick 7 / both present | {A,B} | {A} | {A} | {A} | 2 | 0/1/1/2 |

A source group is an ancestry-connected group, not a delivery count.
The query-level available count is distinct from each method's actually
used count. Public prior consumes zero encounter groups, even at tick 7
with two available originals. With a shared upstream ancestry token
on both originals, the two received originals and two constraints form
**one** available source group and C-local uses just that one group.
The same original received twice never creates another group.

The fixed work assertions below use independent counters:
`generation / forward public transitions / backward public transitions /
inspected observation records / inspected public relations`. These are
method work units in the declared synthetic model, not rescue minutes.

| Cutoff / eligible originals | B work | C generation work | C-local work |
| --- | --- | --- | --- |
| tick 5 / none | 0/1/1/0/2 | 3/0/0/0/0 | 3/1/1/0/2 |
| tick 6 / target only | 0/1/1/0/2 | 3/0/0/1/1 | 3/2/2/0/4 |
| tick 7 / both | 0/2/2/1/5 | 3/0/0/1/1 | 3/1/1/1/3 |
| tick 7 / later only | 0/2/2/1/5 | 3/0/0/0/0 | 3/2/2/1/5 |

The first model tick where the public-prior candidate set differs is
tick 6 for C/C-local and tick 7 for B. B and C-local are not assumed
superior: both use the already declared A-to-C/B-to-D public relation,
which is **not discovered**. For example, B and C-local both return
{A} at tick 7, but C-local consumes more ancestry groups.
Unknown candidate status is separate from an empty candidate set:
B and A do not publish reconstructed standing, while C with no target
generation marks both {A,B} unsupported/unknown. The fixture has no
independent verification source, so verification steps remain zero;
the confirmed target record does not prove actual historical truth.
The direct tests check candidate sets, source ID roles, used-vs-eligible
ancestry groups, stable serialization, method work, first useful tick,
uncertain/conflict/device-only boundaries and separate synthetic truth
scoring. No generic CLI/report-file generation is included.

A candidate count reduction is only reduction in an *internal synthetic
model* comparison set. Preferred is explicitly unavailable for methods
that expose only outer candidates; uncertain/conflicted dispositions
from reconstruction are separate, and empty output is not conflated
with absent direct person history. Generation work, verification work,
public forward/backward transitions, relations and inspected records
are distinct counters, not interchangeable costs. A separate
EvaluateSyntheticTruth call can find a synthetic false exclusion after
all public method outputs exist; truth never changes the inference.

**Limitations / hypotheses held open:** public transition relations and
source identities are supplied, not discovered. The 24-edge lens,
unknown-relation discovery, real-world observation validity,
geographic calibration, confirmation-place labor, rescue time, cost
and survival probability are **not evaluated**. A method with the same
candidate set or higher work remains a valid negative/equal result.
If the result indicates the two "weft" observations cannot capture the
missing own history, or methods collapse to the same operation, revisit
the minimal fixture and question through another independent scope
review rather than silently excluding the original concept. Repeating
old S5 seeds or expanding receipt API arity would not answer this
question.

The fixture and comparisons remain synthetic. The output expectations above
were checked on the cited implementation head; the subsequent documentation
commit needs its own ordinary and F19 research CI before the PR can pass
final review. Even successful tests do not establish rescue effectiveness.

## Two complementary received traces (Issue #86)

Work-Key: `FP-WEFT-COMPLEMENTARITY-V1`. The existing three method families,
not a new fourth method, receive the same public world and same-cutoff
available evidence. The new pure `AnalyzePairedContributions` API compares
`Run`'s four public ablation rows without a private observation, original
record, synthetic truth or new inference algorithm.

**Pre-registered hypothesis (fixed before implementation and CI).** At target tick 2
the public candidates are {A,B,C}; at later tick 5 they are {D,E,F},
with only public 3-tick transitions A→D, B→E, C→F (published tick 0).
Other-holder original 1 observes {A,B} at tick 2, is available/stored at
tick 3 and received tick 6. Distinct-root original 2 observes {E,F}
at tick 5, available/stored tick 6 and received tick 7. No directly
confirmed target person location. The hand prediction for the existing
C-local method at cutoff tick 7 is: zero {A,B,C}, original 1 only
{A,B}, original 2 only {B,C}, both {B}. Actual arrival ticks 5/6/7
are separately reported, not conflated with cutoff-7 ablations.

The analysis distinguishes candidate IDs and signed count changes,
the first/second source IDs and available-vs-used ancestry groups,
actual generation and traversal direct-source IDs, preferred/uncertain/
conflict availability, forward/backward public transitions, verification,
generation and inspection work. Additional exclusions are baseline
regions present in **both singletons** but absent with both records.
Reappearance means a region with both sources which is absent from at
least one singleton. Empty additional exclusions are valid, as are
candidate expansions, unchanged sets, unknown standings and conflict.
Unavailable comparisons return explicit status, not an invented zero.

Shared upstream ancestry of two otherwise distinct original IDs still
counts as one evidence group. Changing receipt order or repeating a
receipt must not change the normalized result. Truth is only available
through the separate post-inference evaluator. Any mismatch with the fixed
hypothesis requires examining declared public-transition semantics rather
than silently tuning the fixture.

**Observed model outputs, separate from the preregistered hypothesis.**
The original PR #87 implementation head (f780cd2) passed ordinary and F19 research
CI on 2026-11-06 ([ordinary](https://github.com/uobnnisiuk/footprint-sim/actions/runs/42311237213),
[F19](https://github.com/uobnnisiuk/footprint-sim/actions/runs/42311237178)).
The next implementation head (aa5647a) also passed
[ordinary](https://github.com/uobnnisiuk/footprint-sim/actions/runs/42315501307)
and [F19](https://github.com/uobnnisiuk/footprint-sim/actions/runs/42315501261)
CI on 2026-11-07. The fixed synthetic test checked C-local outer candidates
as none {A,B,C} (3 model places), first only {A,B} (2), second only
{B,C} (2), both {B} (1). Same-cutoff available ancestry group
counts are 0/1/1/2. C-local used two groups with both:
generation direct source `original-encounter-record` and traversal
direct source `second-original`. Adding the second source excludes A
relative to first-only; adding the first source excludes C relative to
second-only. Exclusions beyond the intersection of singleton candidates
and reappeared regions are both empty.

The method-specific candidate results at the same cutoff tick 7 are:

| Existing method | Neither | First only | Second only | Both |
| --- | --- | --- | --- | --- |
| public-prior | {A,B,C} | {A,B,C} | {A,B,C} | {A,B,C} |
| known-relation-backward | {A,B,C} | {A,B,C} | {B,C} | {B,C} |
| distributed-reconstruction | {A,B,C} | {A,B} | {A,B,C} | {A,B} |
| distributed-reconstruction-local | {A,B,C} | {A,B} | {B,C} | {B} |

**Actual model-work counters.** The table below is from the fixed test
`ComplementaryWeftComparisonMeasuresFourSameCutoffEvidenceConditions`,
read from the public-only
[diagnostic CI log on head 2e46e7f](https://github.com/uobnnisiuk/footprint-sim/actions/runs/42319963514).
That diagnostic run deliberately failed its placeholder snapshot assertion
to expose the counters (618 other tests passed); the subsequent code replaces
the placeholder with an **exact 16-row regression snapshot**. The final
PR head must pass its own ordinary/F19 CI before independent acceptance.
Each slash-delimited tuple is
`usedSourceGroups / generationSteps / verificationSteps / forwardTransitionSteps / backwardTransitionSteps / evidenceRecordsInspected / publicRelationsInspected`.
Counts are work operations in the simulator, not wall-clock duration.

| Method | No traces | First only | Second only | Both |
| --- | --- | --- | --- | --- |
| `public-prior` | `0/0/0/0/0/0/0` | `0/0/0/0/0/0/0` | `0/0/0/0/0/0/0` | `0/0/0/0/0/0/0` |
| `known-relation-backward` | `0/0/0/1/1/0/2` | `0/0/0/1/1/0/2` | `1/0/0/2/2/1/5` | `1/0/0/2/2/1/5` |
| `distributed-reconstruction` | `0/4/0/0/0/0/0` | `1/4/0/0/0/1/1` | `0/4/0/0/0/0/0` | `1/4/0/0/0/1/1` |
| `distributed-reconstruction-local` | `0/4/0/1/1/0/2` | `1/4/0/2/2/0/4` | `1/4/0/2/2/1/5` | `2/4/0/2/2/1/5` |

For example, the C-local **both** condition uses two source groups, 4
generation steps, 0 verification steps, 2 forward and 2 backward public
transition steps, 1 inspected evidence record and 5 inspected public
relations. By contrast, public-prior uses no source groups and has
zero work counters for all four ablations. C-local's 4 generation steps
are not the same as a four-step traversal, and two received original
records are not automatically two independently used evidence groups.
These counters and candidate sets must not be converted to uncalibrated
rescue time, cost, success or survival rates.

The final-head regression also requires missing method results to be
`unavailable` or `partial`, never a numerical zero-candidate result.
Repeated delivery, reversed receipt order, shared source ancestry,
uncertain/conflicting evidence, and device-only observations remain
separate boundary checks. The declared public transitions are supplied
by this scenario: unknown-relation discovery, a complete 24-edge
engine and field rescue effectiveness have **not** been evaluated or
rejected. This one synthetic world does not establish general superiority
of any method.

## Complementary receipt timing on identical public inputs (Issue #90)

Work-Key: `FP-COMPLEMENTARY-RECEIPT-TIMELINESS-V1`. This follows #86's
complementarity experiment without adding another inference algorithm. Two
`Run` reports are built from **the very same public context and two held
original observation objects**. Original 1 is observed at tick 2 in {A,B},
stored/available at tick 3 and received at tick 6. Original 2 is observed
at tick 5 in {E,F}, stored/available at tick 6. Only the second receipt
changes: early tick 7, late tick 8. The declared public tick-0 transition
model is A→D, B→E, C→F with 3-tick travel. There is no direct target
history. Target window is point tick 2; both `Run` calls query [5,6,7,8]
and do their separate same-cutoff ablations at tick 8.

**Pre-fixed prediction, not a #90 measurement:** C-local at tick 7
early is {B} and late is {A,B}; at tick 8 both are {B}. The simple
public-prior and generation-only candidate sets should not change
when the second receipt is delayed. Backward traversal is expected to
be {B,C} versus {A,B,C} at tick 7. These are predictions derived
from the #86 fixed ablations, not a new independent validation or
evidence of general method superiority. Predictions that fail must be
reported as failures and the scope independently reassessed, not
fixed by altering the synthetic world.

`AnalyzeReceiptDecisionTiming(early, late)` only reads each report's
published `ActualTimeline`, never its hypothetical
`SameCutoffAblations` or any original observation, truth or private
input. It returns candidate sets/count differences and inclusion,
received original and observation IDs, eligible group counts and
actually-used direct sources/groups, generation/verification and
forward/backward steps, inspected records/relations, and separate
first differences and later re-convergences across *supplied query
ticks*. Missing methods return explicit unavailable/partial with
nullable comparisons, not an invented zero candidate set.
An empty candidate set remains distinct from a missing row.
The full method work counters must not be read as elapsed rescue
time; a first differing tick is only the first of these sampled
decision queries, **not** continuous-time earliest usefulness.

**Experimental-control limit:** metadata and receipt IDs alone cannot
prove that two arbitrary reports share the same world and original
observations. The test passes the identical context and original
objects to both calls, changing the second receipt tick only. Shared
upstream ancestry still contributes one independent group. Repeated
delivery, reversed inputs, uncertain/conflicting or device-only
observations cannot strengthen an unsupported conclusion. The public
transition relation is declared, not discovered. Unknown causal
relationships, a complete 24-edge engine, real BLE delays, search
area/time, rescue success and survival rate remain untested.

**Verification status at first Draft PR publication:** the above is
a preregistered prediction and an implementation pending the PR's
ordinary and F19 CI plus a separate independent review. Neither
the expected candidate outcome nor the expected simulator work counts
are a confirmed #90 result until the final PR head's CI checks pass.

**Observed model output, separate from the preregistered prediction.**
After the initial syntax-assembly error was fixed, the
[ordinary CI on corrected head 0196789](https://github.com/uobnnisiuk/footprint-sim/actions/runs/42337998295)
passed build, 622 tests (zero failed/skipped), format, sample scenarios
and evaluation harness on 2026-11-07 JST. At query ticks 5/6/7/8,
the early schedule receives 0/1/2/2 originals and the late schedule
0/1/1/2. The fixed-public-world outputs at tick 7 are:

| Method | Early (second receipt tick 7) | Late (second receipt tick 8) |
| --- | --- | --- |
| `public-prior` | {A,B,C} | {A,B,C} |
| `known-relation-backward` | {B,C} | {A,B,C} |
| `distributed-reconstruction` | {A,B} | {A,B} |
| `distributed-reconstruction-local` | {B} | {A,B} |

Both schedules coincide at tick 8 for public candidate IDs, used
source IDs and model work. C-local excludes model-place A one sampled
query tick earlier when the second record arrives at 7; this does not
measure continuous rescue time. At tick 7, early/late C-local uses 2/1
ancestry groups, with generation direct source
`original-encounter-record` in both and traversal direct source
`second-original` only in the early condition. Its operation counts
early/late are generation 4/4, verification 0/0, forward transition
2/2, backward transition 2/2, inspected evidence records 1/0, and
inspected public relations 5/4. Backward-only uses 1/0 groups,
forward/backward steps 2/1 each, evidence inspections 1/0,
and relation inspections 5/2. Generation-only uses 1/1 groups and
4/4 generation steps without transition steps; public-prior uses
no groups or work steps. The counters use the identical #86 fixed
ablation fixture and #90 actual-timeline comparison, not a separate
real-world measurement or four independent research studies.

The public movement relation is declared, not discovered. Unknown
relationships, unmodeled travel, physical rescue time, costs, and
rescue effectiveness remain untested. The final PR head requires its
own ordinary/F19 CI and independent technical review before any
acceptance or normal merge.

## Sequential decision revision after contradictory weft (Issue #92)

Work-Key: FP-CONFLICTING-WEFT-DECISION-REVISION-V1. This uses the
original proposal's missing personal history and independent other-person
records. The challenge is not to minimize every candidate set: a late
contradictory source may require withdrawing a previous narrowing.
The public model is still declared, not discovered; this is not a new
inference method or a field rescue system.

**Fixed input, preregistered hypothesis, not a measurement.** Use the
same public object and original objects in two real Run timelines.
Public candidate states at tick 2 {A,B}, at tick 5 {C,D}, publicly
declared transitions A→C/B→D (three ticks, published tick 0).
Two independent original confirmed observations at tick 2 identify
{A} and {B}, both stored/available tick 3; neither is the target's
personal record. The {A} source is received tick 6. The {B} source
is received tick 8 in the early schedule and tick 9 in the late.
Both query ticks [5,6,7,8,9] and always evaluate past target tick 2.
Only the second receipt differs. A same-tick early/late comparison
is *separate* from adjacent queries within either timeline.

The beforehand prediction: existing MultiWeftTracingStrategy returns
outer {C} from the first source, then {C,D} and a conflict from both,
rather than treating the first hard exclusion as permanent. Actual
M1-E generation and local-traversal outputs are not assumed to
equal that tracing result; unchanged, widened or conflicting results
must be retained without post-hoc changes to world or hypothesis.

AnalyzeDecisionRevision reads only the published ActualTimeline.
For each of the four existing methods it compares adjacent sampled
decision ticks for the SAME past target window: outer IDs, excluded
and reappeared IDs, signed count difference and inclusion, null-aware
preferred/unknown/conflict standing, receipt and source-observation
IDs, direct generation and traversal sources, available/used
ancestry *group counts* and method work counters from each side.
Missing or duplicate method rows return explicit unavailable/partial.
A genuine empty outer set is not a missing result. First changed
or reappeared tick refers only to sampled decisions. The published
report cannot establish world identity or restore ancestry group IDs;
test fixtures fix original/world objects separately.

Other boundaries are direct real-code tests: receipt cutoff,
late and duplicate delivery, swapped input order, shared ancestry
not double-counted, uncertainty and device-only non-promotion,
actual MultiWeftTracingStrategy conflict and deliberate separation
from M1-E outputs. Synthetic truth B, if scored, is passed only to
EvaluateSyntheticTruth AFTER published inference.



**Observed synthetic-model output on the fixed #92 test fixture.** The
first measurement was taken from the GitHub ordinary-CI run on the
temporary measurement head, before removing the deliberately failing
diagnostic test. These counts are generated by actual Run, not inferred
from the above hypothesis. Final clean PR CI and independent review
remain necessary before this becomes a merged research result.

The sequential historical target is **tick 2**, not the declared
current-forward positions at tick 5. The table shows the result for
one original received by tick 6, and then both originals received at
the early schedule's tick 8. A delayed second receipt leaves the
one-original results at tick 8; both schedules agree at tick 9.

| Method | Early tick 6: outer / conflict | Early tick 8: outer / conflict | Reappeared from tick 6 to 8 |
| --- | --- | --- | --- |
| public-prior | {A,B} / unavailable | {A,B} / unavailable | none |
| known-relation-backward | {A,B} / unavailable | {A,B} / unavailable | none |
| distributed-reconstruction | {A} / {} | {A,B} / {A,B} | B |
| distributed-reconstruction-local | {A} / {} | {A,B} / {A,B} | B |

Preferred standing is not exposed (null) for all four method rows.
Conflict is not exposed (null) for the two baselines but an explicit
empty set in M1-E before the contradictory receipt. The latter becomes
{A,B}; uncertain remains empty in the measured M1-E rows. This is a
candidate widening/withdrawal of an earlier exclusion, **not proof that
real historical B is true**, nor proof that all methods behave alike.
The existing MultiWeftTracingStrategy separately reports its
forward/current result {C} with only the first original and {C,D}
with both, retaining conflict. Those forward sets must not be
mislabelled as the tick-2 historical outputs above.

Work is reported as simulator operations, not wall time. Each
ordered tuple is (generation, verification, forward transition,
backward transition, evidence records inspected, public relations
inspected). The first to both original change measured:

| Method | Used ancestry groups: 1st / both | First source only work | Both sources work |
| --- | --- | --- | --- |
| public-prior | 0 / 0 | (0,0,0,0,0,0) | (0,0,0,0,0,0) |
| known-relation-backward | 0 / 0 | (0,0,1,1,0,2) | (0,0,1,1,0,2) |
| distributed-reconstruction | 1 / 2 | (3,0,0,0,1,1) | (5,0,0,0,2,2) |
| distributed-reconstruction-local | 1 / 2 | (3,0,2,2,0,4) | (5,0,1,1,0,2) |

Eligible independent source groups change from 1 to 2. Generation
direct-source IDs change from the first original only to both original
IDs in the M1-E rows; traversal direct-source IDs are empty here.
The change in local traversal counters demonstrates that larger
candidate sets do not necessarily imply more of every kind of work.
A repeated receipt or shared ancestry is not a third independent group.

The **same-tick comparison** at tick 8 observes the early schedule
having the contradiction while the late schedule does not; the
**sequential comparison** observes B reappearing at tick 8 in the
early schedule, versus at tick 9 when delayed. This is an observed
difference among sampled decision ticks only, not a measured rescue
response time, physical sites avoided or unknown relation discovery.
Source authenticity and real observations remain uncalibrated.

**Initial Draft publication status:** tests and code are submitted,
but concrete measured per-method candidate sets, conflict status,
source and work counts, final-head ordinary/F19 CI, independent review
and merge are still pending. They must be recorded as observations,
including zero or adverse effects, rather than silently inferred from
the hypothesis. Model candidate-count changes are not calibrated
search-site reductions, time, cost, or rescue rates. Unknown relation
discovery, whole 24-edge engine, real-world authenticity and BLE
remain untested. If the published M1-E output differs, report the
difference and reconsider the scope; do not alter fixed inputs to
make it pass.

## Same wefts with/without published movement relations (Issue #94)

Work-Key: FP-PUBLIC-RELATION-AVAILABILITY-V1. This comparison measures the
*availability of declared public movement knowledge*, not whether any
physical route exists. The original idea is to reconstruct a missing
personal history from other devices' traces without inventing missing
relations. It builds on #84/#86's fixed four methods and two independent
original traces, not on a new relation discovery engine.

**Fixed before experiment:** identical scenario/seed/rescuer; one public
candidate model with tick 2 {A,B,C} and tick 5 {D,E,F}; two original
observations at ticks 2 {A,B} and 5 {E,F}; first/second receipts at ticks
6/7, with queries [5,6,7] always targeting past tick 2. The known
condition declares A→D, B→E and C→F with travel duration 3 ticks and
publication tick 0. Its paired condition uses the very same original
objects, receipt ledger, public candidate states and physical synthetic
world, but *does not provide the PublicHazardEnvelope to inference*.
Null means unavailable knowledge, **not** "no movement" or a closed
transition graph. The fixture checks these controls directly; the
published report alone cannot certify physical-world identity.

**Pre-registered hypothesis:** public-prior candidate sets should stay
the same; methods using public transitions may change in candidate
sets, direct-source use, internal work or uncertainty, or may become
unavailable. No difference, worsening and non-comparability are valid
outcomes; we must not adjust the fixed world after seeing results.
This contrasts with #90's evidence arrival times and #92's contrary
new evidence, and with M1-F12's uncertain encounter relation *kind*
and F21's public model coverage assumptions.

`AnalyzePublicRelationAvailability(known, unprovided)` reads only the
two actual published timelines. Per same-tick method it retains the
full existing result on each side: outer IDs/count, nullable preferred,
uncertain/conflict, direct generation/traversal/unused source IDs,
available/used ancestry-group counts, transition IDs and six separate
work counters (generation, verification, forward, backward, evidence
and relation inspections). It reports signed candidate-count difference,
set exclusions and both inclusion directions; missing/duplicate or
inconsistent rows become unavailable/partial, while real empty outer
sets and nullable standings remain distinct. Mismatched receipts,
source-observation identities, cutoffs and ancestry invalidate a
cross-condition comparison. The method neither sees truth nor runs
inference. The shared source-group *counts* do not independently verify
source authenticity or expose ancestry group identifiers.

**Implementation status:** the fixed-condition direct Run tests and
per-method recorded values require final PR CI plus independent review.
Only observed model outputs may populate measured conclusions; the
above is not a claim that missing public relations have been discovered
or that any method is superior. Unknown relationship discovery, 24-edge
hypotheses, field-device/BLE data, physical search sites, rescue time,
cost and survival effects remain untested. Revisit this interpretation
if null is treated as a closed world, source/fixture controls differ,
or the results cannot be obtained without a new inference assumption.

**Observed synthetic model results at query tick 7 (two originals received).**
These values were obtained from the actual `Run` under both fixed
public-input conditions and then pinned in a direct test, rather than
filled from the preregistered hypothesis. Candidate sets refer to the
**past target tick 2**, not current physical positions.

| Existing method | Declared transition outer | Unprovided transition outer | Used groups (both) | Direct source roles |
| --- | --- | --- | --- | --- |
| public-prior | {A,B,C} | {A,B,C} | 0 | none |
| known-relation-backward | {B,C} | {A,B,C} | 1 | traversal: second-original |
| distributed-reconstruction | {A,B} | {A,B} | 1 | generation: original-encounter-record |
| distributed-reconstruction-local | {B} | {A,B} | 2 | generation: original-encounter-record; traversal: second-original |

Both conditions retain **two eligible ancestry groups**, and all
observed methods have an output. The method's `EvidenceStatus` is
`method-executed` for the three inference methods on both sides,
even when the available public model cannot narrow the candidates.
An empty direct-source list is not evidence that the source never
existed. In both M1-E methods the exposed uncertain/conflict sets
are empty in this fixture, not null; for the two baseline methods
these standings are **unavailable (null)**. Preferred standing is
unavailable for every method.

Work counters below are measured *generation / verification / forward
transitions / backward transitions / evidence records inspected /
public relations inspected*. They are model operations, not elapsed
time.

| Existing method | Declared transition work | Unprovided transition work |
| --- | --- | --- |
| public-prior | 0/0/0/0/0/0 | 0/0/0/0/0/0 |
| known-relation-backward | 0/0/2/2/1/5 | 0/0/0/0/1/1 |
| distributed-reconstruction | 4/0/0/0/1/1 | 4/0/0/0/1/1 |
| distributed-reconstruction-local | 4/0/2/2/1/5 | 4/0/0/0/1/1 |

Thus the unprovided condition retains {A,B,C} rather than {B,C}
for backward tracing and retains {A,B} rather than {B} for local
reconstruction. Public-prior and generation-only reconstruction are
unchanged. This identifies an **input-knowledge effect within two
methods**, not an advantage from switching algorithms at the same
information level. The unchanged methods are valid results, not
discarded failures. The model does not identify unknown transit edges,
establish a true historical location, or calibrate physical search
sites, costs, time or survival effects. The snapshot originates from
a temporary measurement head and is preserved as a pinned final test;
it is subject to final-head normal/F19 CI and independent review.


**Earlier sampled queries (tick 5 and tick 6): fixed direct-Run checks.**
The tick-5 query precedes either receipt (0 original, 0 eligible groups).
At tick 6 only the first original is received (1 original, 1 eligible
group). All candidates refer to the *historical target at tick 2*.
The following values are pinned against the same `Run` inputs; the
declared-condition candidates independently match #86's no-record
and first-only controls. Final-head CI is required before treating this
new #94 test as a passed measurement.

| Query / method | Declared outer | Unprovided outer | Candidate change |
| --- | --- | --- | --- |
| 5 / public-prior | {A,B,C} | {A,B,C} | none |
| 5 / known-relation-backward | {A,B,C} | {A,B,C} | none |
| 5 / distributed-reconstruction | {A,B,C} | {A,B,C} | none |
| 5 / distributed-reconstruction-local | {A,B,C} | {A,B,C} | none |
| 6 / public-prior | {A,B,C} | {A,B,C} | none |
| 6 / known-relation-backward | {A,B,C} | {A,B,C} | none |
| 6 / distributed-reconstruction | {A,B} | {A,B} | none |
| 6 / distributed-reconstruction-local | {A,B} | {A,B} | none |

Both sides expose the same method status at each query:
public-prior is `last-confirmed-unavailable-public-prior` at both
ticks; the other three are `public-model-only` before receipt at tick 5.
At tick 6, backward-only is still `public-model-only` (no traversal
direct source), while generation and local are `method-executed`.
These statuses describe evidence use, not actual transition-work count. At tick 5, no method uses an evidence group;
at tick 6, the reconstruction methods use the first original as a
generation source. Backward-only uses no traversal source at either
tick. For known-relation backward traversal, the declared condition
performs 1 forward and 1 backward step, inspecting 2 public relations,
at each of ticks 5/6; the unprovided condition performs 0 transition
steps. C-local performs 1+1 declared transition steps at tick 5
and 2+2 at tick 6, compared with zero unprovided transition steps;
generation remains 4 steps in each condition. The direct tests pin
these model counters and source-group distinctions. They do not
represent a wall-time or rescue-benefit measurement. The first
candidate *difference* between availability conditions is at the
sampled tick 7, when the second original is received.

**Adversarial boundary discovered on 2026-11-07 (AR-1, outside
the two-condition comparison):** The finding at
[PR #95 adversarial review](https://github.com/uobnnisiuk/footprint-sim/pull/95#issuecomment-6705752274)
was checked against `OperationalCausalTracer.ApplyDirection`.
When `PublicHazardEnvelope` is entirely null, transition propagation
is skipped, as measured above. That does **not** establish safety when
only *some* branches are published. In the existing tracer, one
published branch enables hard intersection with the reachable-set
calculated from the published branches. Omitting C→F from A→D,
B→E, C→F can change known-relation-backward outer from {B,C}
to {B}; the missing transition is effectively treated as impossible
without a conflict or uncertainty warning. An unpublicized A→E
may also leave a physically plausible A excluded by local={B}.
This is a reported **unsafe limitation**, not evidence that the
system safely reconstructs missing edges. It contradicts the
missing-transition intent in `AGENTS.md` and
`docs/m1d-causal-tracing.md`; #94 does not silently approve it.

A separate diagnostic on the fixed original records/receipts pins
the current partial-public result so a later tracer fix cannot be
mistaken for a no-op. It is **a characterization of the defect**,
not a desirable acceptance property or a new third research method.
Correcting the tracer's outer-set semantics needs independently
reviewed scope because the tracer file is outside #94's three
approved files. Do not infer general monotonicity or operational
rescue safety from the null-envelope results, and do not close
AR-1 merely because this diagnostic passes.

**AR-2 source-accounting qualification:** in null-envelope results,
`TraversalDirectSourceIds` and used ancestry-group counts can name
a received source even when the forward/backward transition counters
are both zero. Those role labels mean an input was *accounted for*,
not proof that it changed candidates or improved the decision.
Future work must distinguish eligibility/assignment from measured
marginal effect; it must not score these labels as rescue utility.
