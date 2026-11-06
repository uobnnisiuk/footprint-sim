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

**CI-observed results, separate from the hypothesis.** The original PR #87
head (f780cd2) passed the ordinary and F19 research CI on 2026-11-06:
https://github.com/uobnnisiuk/footprint-sim/actions/runs/42311237213
and https://github.com/uobnnisiuk/footprint-sim/actions/runs/42311237178 .
The test verified the existing C-local output at cutoff 7 as
neither {A,B,C} (3 candidate places), first-only {A,B} (2),
second-only {B,C} (2), and both {B} (1). Available ancestry group
counts are respectively 0/1/1/2; at both, C-local actually used two
groups, generation direct source `first-original` and traversal direct
source `second-original` (the first ID is the fixture's `RecordId`).
Adding the second removes A; adding the first removes C; the
beyond-singletons exclusions and reappeared sets are empty. The
public-prior method's candidates and actually used source-group count
remain unchanged across the four cuts (zero used groups). C-local's
both-source case performed *positive* forward and backward public
transition steps, as asserted by the existing tests; these are model
work counters, not rescue-time measurements. Generation/verification,
evidence-inspection and public-relation counters are kept distinct in
the analysis output rather than rolled into a search-duration estimate.
The three other method results must be read as separate output rows,
not inferred from C-local's success; no general superiority is claimed.

The subsequent implementation head also needs its own ordinary/F19 CI
and an independent review. One further regression now checks that a
completely missing method set returns unavailable, that a method absent
from every row does not silently disappear, and that one missing
condition yields partial status. A numerical zero-candidate result is
not interchangeable with missing method data. The measured model-place
and work counts do not imply real search time, rescue cost or survival
rate. Unknown causal-relation discovery and the 24-edge search lens
remain untested, not rejected.
