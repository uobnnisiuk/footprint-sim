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
