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

Existing scenario loading, simulation, CLI, batch-evaluation paths and output
schemas are unchanged. This is an opt-in programmatic bridge. A later bounded
task can connect explicit receipt events to a scenario/evaluation timeline; it
must retain receiver scoping and immutable original identities. Multiple held
records, forwarding chains, actual communication and operational rescue decisions
are outside this slice.
