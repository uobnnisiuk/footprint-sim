using FootprintSim.Evaluation;
using FootprintSim.Evidence;
using FootprintSim.Hazards;
using FootprintSim.Inference;
using FootprintSim.Interactions;
using FootprintSim.Interactions.CandidateWarps;
using FootprintSim.Interactions.Tracing;
using FootprintSim.Model;
using FootprintSim.Serialization;
using Xunit;

namespace FootprintSim.Core.Tests;

public sealed class RescuerEvidenceBoundaryTests
{
    private const string ScenarioId = "rescuer-receipt-fixture";
    private const int Seed = 7;
    private const string HolderId = "held-device";
    private const string RescuerId = "rescuer-a";
    private const string TargetId = "person-a";
    private const string RecordId = "original-encounter-record";

    [Fact]
    public void ReceiptGatesInputsAndConnectsExistingCandidateAndTracingImplementations()
    {
        var record = Record();
        var before = Build(record, [Receipt(5)], currentTick: 4);
        var notReceived = Build(record, [], currentTick: 5);
        AssertNoReceivedEvidence(before);
        AssertNoReceivedEvidence(notReceived);
        Assert.DoesNotContain(record.Observation.ObservationId, JsonDefaults.Serialize(before));
        Assert.DoesNotContain(RecordId, JsonDefaults.Serialize(before));

        var received = Build(record, [Receipt(5)], currentTick: 5);
        Assert.Single(received.ReceivedRecords);
        Assert.Single(received.Input.AvailableCrossThreadConstraints!.Constraints);
        Assert.Single(received.Input.AvailableCausalTracingConstraints!.Constraints);
        Assert.Empty(received.Input.AvailableEvidence.Observations);

        var baseline = new MultiWeftTracingStrategy().Evaluate(notReceived.Input);
        var result = new MultiWeftTracingStrategy().Evaluate(received.Input);
        Assert.Equal(["C", "D"], baseline.CandidateRegionIds);
        Assert.Equal(["C"], result.CandidateRegionIds);
        Assert.Equal(["C"], result.PreferredCandidateRegionIds);
        Assert.Single(result.CausalTracingResult!.SourceGroups);
        Assert.Contains(result.CausalTracingResult.Steps, step =>
            step.Direction == CausalTracingDirection.CrossThread &&
            step.Disposition == TracingStepDisposition.Applied);
        Assert.Contains(result.CausalTracingResult.Steps, step =>
            step.Direction == CausalTracingDirection.Forward);

        // Exercise the existing historical applicator without inventing the own
        // point-position evidence required by CrossThreadConstrainedStrategy.
        var context = Context();
        var historical = PublicCandidateWarpFactory.Create(
            ScenarioId, Seed, TargetId,
            context.PublicCausalTracingModel!.CandidateStates.Take(1),
            CandidateWarpStanding.Preferred);
        var applied = new CrossThreadConstraintApplicator().Apply(
            historical,
            received.Input.AvailableCrossThreadConstraints.Constraints,
            new CandidateWarpAccessContext
            {
                ScenarioId = ScenarioId,
                Seed = Seed,
                TargetEntityId = TargetId,
                CurrentTick = 5,
                QueryContextId = "receipt-candidate-check"
            },
            context.PublicHazardEnvelope);
        Assert.Equal(["C"], applied.CandidateWarps.PreferredRegionIdsAt(5));
    }

    [Fact]
    public void ReceiptPreservesOriginalIdentityAndAncestryWhileDelayingAccess()
    {
        var record = Record();
        var originalJson = JsonDefaults.Serialize(record.Observation);
        var snapshot = Build(record, [Receipt(5)]);
        var received = Assert.Single(snapshot.ReceivedRecords);
        var cross = Assert.Single(snapshot.Input.AvailableCrossThreadConstraints!.Constraints);
        var tracing = Assert.Single(snapshot.Input.AvailableCausalTracingConstraints!.Constraints);

        Assert.Same(record.Observation, received.OriginalObservation);
        Assert.Equal(record.Observation.ObservationId, received.OriginalObservationId);
        Assert.Equal(RecordId, received.OriginalRecordId);
        Assert.Equal(5, received.FirstReceivedAtTick);
        Assert.Equal(3, record.Observation.AvailableAtTick);
        Assert.Equal(InteractionVisibility.PrivateEvidence, record.Observation.Visibility);
        Assert.Equal(originalJson, JsonDefaults.Serialize(record.Observation));
        Assert.Equal(record.Observation.ObservationId, cross.SourceObservationId);
        Assert.Equal(record.Observation.ObservationId, tracing.SourceObservationId);
        Assert.Equal(RecordId, cross.SourceEvidenceIdentifier);
        Assert.Equal(RecordId, tracing.DirectSourceId);
        Assert.Equal(5, cross.AvailableAtTick);
        Assert.Equal(5, tracing.AvailableAtTick);
        Assert.Equal(InteractionVisibility.PublicEvidence, cross.Visibility);
        Assert.Contains(RecordId, tracing.SourceAncestryIds);
        Assert.Contains(record.Observation.ObservationId, tracing.SourceAncestryIds);
        Assert.Contains("upstream-evidence", tracing.SourceAncestryIds);
        Assert.DoesNotContain(HolderId, tracing.SourceAncestryIds);
        Assert.DoesNotContain(RescuerId, tracing.SourceAncestryIds);
        Assert.DoesNotContain(CrossThreadConstraintDeriver.PersonEncounterCoLocationRule, tracing.SourceAncestryIds);
        Assert.Empty(snapshot.Input.AvailableCrossThreadConstraints.PubliclyAvailableAt(4).Constraints);
        Assert.Empty(snapshot.Input.AvailableCausalTracingConstraints.PubliclyAvailableAt(4).Constraints);
    }

    [Fact]
    public void RedeliveryAndReceiptOrderingDoNotIncreaseEvidenceOrIndependentSupport()
    {
        var record = Record();
        var firstOnly = Build(record, [Receipt(5)], currentTick: 10);
        var deliveries = new[] { Receipt(9), Receipt(5), Receipt(7), Receipt(5) };
        var repeated = Build(record, deliveries, currentTick: 10);
        var reversed = Build(record, deliveries.Reverse(), currentTick: 10);

        Assert.Equal(JsonDefaults.Serialize(firstOnly), JsonDefaults.Serialize(repeated));
        Assert.Equal(JsonDefaults.Serialize(firstOnly), JsonDefaults.Serialize(reversed));
        var expected = new MultiWeftTracingStrategy().Evaluate(firstOnly.Input);
        var actual = new MultiWeftTracingStrategy().Evaluate(repeated.Input);
        Assert.Equal(JsonDefaults.Serialize(expected), JsonDefaults.Serialize(actual));
        Assert.Single(actual.CausalTracingResult!.SourceGroups);
        Assert.Single(actual.CausalTracingResult.SourceGroups[0].SourceObservationIds);
        Assert.Single(actual.CausalTracingResult.SourceGroups[0].ConstraintIds);
    }

    [Fact]
    public void FutureReceiptsDoNotAffectEarlierQueriesOrCausePrematureValidationFailure()
    {
        var record = Record(availableAtTick: 20, storedAtTick: 20);
        var withoutReceipt = Build(record, [], currentTick: 5);
        // This receipt is temporally invalid, but it is not yet in the query's ledger view.
        var earlier = Build(record, [Receipt(10)], currentTick: 5);
        AssertNoReceivedEvidence(earlier);
        Assert.Equal(JsonDefaults.Serialize(withoutReceipt), JsonDefaults.Serialize(earlier));
        Assert.Throws<ArgumentException>(() => Build(record, [Receipt(10)], currentTick: 10));
    }

    [Theory]
    [InlineData("scenario")]
    [InlineData("seed")]
    [InlineData("record")]
    [InlineData("holder")]
    [InlineData("rescuer")]
    public void UnrelatedReceiptsDoNotGrantAccess(string mismatch)
    {
        var receipt = new InteractionRecordReceipt(
            mismatch == "scenario" ? "another-scenario" : ScenarioId,
            mismatch == "seed" ? Seed + 1 : Seed,
            mismatch == "record" ? "another-record" : RecordId,
            mismatch == "holder" ? "another-device" : HolderId,
            mismatch == "rescuer" ? "rescuer-b" : RescuerId,
            5);
        var snapshot = Build(Record(), [receipt]);
        AssertNoReceivedEvidence(snapshot);
        Assert.Equal(["C", "D"], new MultiWeftTracingStrategy().Evaluate(snapshot.Input).CandidateRegionIds);
    }

    [Theory]
    [InlineData(3, 6, 5)]
    [InlineData(6, 6, 5)]
    [InlineData(2, 2, 1)]
    public void MatchingReceiptCannotPrecedeStorageAvailabilityOrObservation(
        long available, long stored, long received)
    {
        var record = Record(availableAtTick: available, storedAtTick: stored);
        Assert.Throws<ArgumentException>(() => Build(record, [Receipt(received)], currentTick: 10));
    }

    [Theory]
    [InlineData(2, 2)]
    [InlineData(1, 2)]
    public void ReceiptKeepsExactAndIntervalObservationSemantics(long start, long end)
    {
        var record = Record(window: new InteractionTimeWindow(start, end));
        var snapshot = Build(record, [Receipt(5)]);
        var cross = Assert.Single(snapshot.Input.AvailableCrossThreadConstraints!.Constraints);
        var tracing = Assert.Single(snapshot.Input.AvailableCausalTracingConstraints!.Constraints);
        Assert.Equal(record.Observation.ObservedTimeWindow, cross.SourceObservedTimeWindow);
        Assert.Equal(record.Observation.ObservedTimeWindow, cross.ConstrainedTimeWindow);
        Assert.Equal(record.Observation.ObservedTimeWindow, tracing.SourceObservedTimeWindow);
        Assert.Equal(record.Observation.ObservedTimeWindow, tracing.ConstrainedTimeWindow);
        Assert.Equal(record.Observation.ObservedRelationKind, cross.SourceRelationKind);
        Assert.Equal(record.Observation.ObservedParticipants, tracing.Participants);
    }

    [Theory]
    [InlineData(EvidenceState.SupportedButUncertain)]
    [InlineData(EvidenceState.Conflicting)]
    public void ReceiptDoesNotStrengthenUncertaintyOrDiscardConflictingAlternatives(EvidenceState state)
    {
        var snapshot = Build(Record(state: state), [Receipt(5)]);
        Assert.Equal(state, snapshot.Input.AvailableCrossThreadConstraints!.Constraints[0].EvidenceBoundaryState);
        Assert.Equal(state, snapshot.Input.AvailableCausalTracingConstraints!.Constraints[0].EvidenceBoundaryState);
        var result = new MultiWeftTracingStrategy().Evaluate(snapshot.Input);
        Assert.Equal(["C", "D"], result.OuterPlausibleCandidateRegionIds);
        if (state == EvidenceState.SupportedButUncertain)
        {
            Assert.Equal(["C"], result.PreferredCandidateRegionIds);
        }
        else
        {
            Assert.Contains(result.CausalTracingResult!.Steps, step =>
                step.Disposition == TracingStepDisposition.MarkConflict && step.ConflictDetected);
        }
    }

    [Fact]
    public void ReceivedDeviceOnlyRecordDoesNotBecomePersonEvidence()
    {
        var snapshot = Build(Record(deviceOnly: true), [Receipt(5)]);
        var received = Assert.Single(snapshot.ReceivedRecords);
        Assert.True(received.OriginalObservation.IsDeviceOnlyEvidence);
        Assert.False(received.OriginalObservation.IsPersonEvidence);
        Assert.Empty(snapshot.Input.AvailableEvidence.Observations);
        Assert.Empty(snapshot.Input.AvailableCrossThreadConstraints!.Constraints);
        Assert.Empty(snapshot.Input.AvailableCausalTracingConstraints!.Constraints);
        var result = new MultiWeftTracingStrategy().Evaluate(snapshot.Input);
        Assert.Equal(["C", "D"], result.CandidateRegionIds);
        Assert.Empty(result.CausalTracingResult!.SourceGroups);
    }

    [Fact]
    public void HoldingContractRejectsPublicOriginalTruthProvenanceAndPrematureStorage()
    {
        Assert.Throws<ArgumentException>(() => new DeviceHeldInteractionRecord(
            HolderId, 3, Observation(visibility: InteractionVisibility.PublicEvidence)));
        Assert.Throws<ArgumentException>(() => new DeviceHeldInteractionRecord(
            HolderId, 3, Observation(provenanceKind: InteractionProvenanceKind.WorldEvent)));
        Assert.Throws<ArgumentOutOfRangeException>(() => new DeviceHeldInteractionRecord(
            HolderId, 2, Observation()));
    }

    [Fact]
    public void QueryCannotMixRunsOrTreatAnUndeclaredHolderAsADevice()
    {
        var builder = new RescuerEvidenceInputBuilder();
        var record = Record();
        Assert.Throws<ArgumentException>(() => builder.Build(
            Context() with { ScenarioId = "another-scenario" }, Seed, 5, RescuerId, record, [Receipt(5)]));
        Assert.Throws<ArgumentException>(() => builder.Build(
            Context(), Seed + 1, 5, RescuerId, record, [Receipt(5)]));
        Assert.Throws<ArgumentException>(() => builder.Build(
            Context() with { PublicDeviceIds = [] }, Seed, 5, RescuerId, record, [Receipt(5)]));
    }


    [Fact]
    public void MultipleHoldersGrantOneOriginalOnlyAfterFirstReceipt()
    {
        var heldA = Record();
        var heldB = new DeviceHeldInteractionRecord("holder-b", 4, heldA.Observation);
        var holders = new[] { heldA, heldB };
        var receiptB = HolderReceipt("holder-b", 5);
        var receiptA = HolderReceipt(HolderId, 7);
        var before = BuildMulti(holders, [receiptB, receiptA], 4);
        AssertNoReceivedEvidence(before);

        var publicOnly = BuildMulti(holders, [], 5);
        AssertNoReceivedEvidence(publicOnly);
        Assert.Equal(["C", "D"], new MultiWeftTracingStrategy().Evaluate(publicOnly.Input).CandidateRegionIds);

        var first = BuildMulti(holders, [receiptB, receiptA], 5);
        Assert.Equal("holder-b", Assert.Single(first.ReceivedRecords).HolderDeviceId);
        Assert.Equal(5, first.ReceivedRecords[0].FirstReceivedAtTick);
        var cross = Assert.Single(first.Input.AvailableCrossThreadConstraints!.Constraints);
        var tracing = Assert.Single(first.Input.AvailableCausalTracingConstraints!.Constraints);
        Assert.Equal(heldA.Observation.ObservationId, cross.SourceObservationId);
        Assert.Equal(RecordId, tracing.DirectSourceId);
        Assert.DoesNotContain("holder-b", tracing.SourceAncestryIds);
        Assert.DoesNotContain(HolderId, tracing.SourceAncestryIds);
        Assert.DoesNotContain(RescuerId, tracing.SourceAncestryIds);
        Assert.Empty(first.Input.AvailableEvidence.Observations);
        var constrained = new MultiWeftTracingStrategy().Evaluate(first.Input);
        Assert.Equal(["C"], constrained.CandidateRegionIds);
        Assert.Single(constrained.CausalTracingResult!.SourceGroups);
        Assert.Single(constrained.CausalTracingResult.SourceGroups[0].SourceObservationIds);

        // Compare at the same query tick: B alone versus B and later A.
        var oneReceipt = BuildMulti(holders, [receiptB], 7);
        var repeated = BuildMulti(holders, [receiptB, receiptA, receiptA], 7);
        Assert.Equal(JsonDefaults.Serialize(oneReceipt), JsonDefaults.Serialize(repeated));
        Assert.Equal(
            JsonDefaults.Serialize(new MultiWeftTracingStrategy().Evaluate(oneReceipt.Input)),
            JsonDefaults.Serialize(new MultiWeftTracingStrategy().Evaluate(repeated.Input)));
    }

    [Fact]
    public void MultipleHoldersAreDeterministicForEqualReceiptTimesAndReorderedInputs()
    {
        var a = Record();
        var b = new DeviceHeldInteractionRecord("holder-b", 3, a.Observation);
        var receiptA = HolderReceipt(HolderId, 5);
        var receiptB = HolderReceipt("holder-b", 5);
        var direct = BuildMulti([a, b], [receiptB, receiptA], 5);
        var reversed = BuildMulti([b, a, a], [receiptA, receiptB, receiptB], 5);
        Assert.Equal(HolderId, Assert.Single(direct.ReceivedRecords).HolderDeviceId);
        Assert.Equal(JsonDefaults.Serialize(direct), JsonDefaults.Serialize(reversed));
        Assert.Equal(
            JsonDefaults.Serialize(new MultiWeftTracingStrategy().Evaluate(direct.Input)),
            JsonDefaults.Serialize(new MultiWeftTracingStrategy().Evaluate(reversed.Input)));
    }

    [Fact]
    public void MultipleHoldersRejectInconsistentOriginalsStorageAndRuns()
    {
        var original = Record();
        var anotherCopy = new DeviceHeldInteractionRecord(
            "holder-b", 3, Observation(state: EvidenceState.Conflicting));
        Assert.Throws<ArgumentException>(() => BuildMulti([original, anotherCopy], [], 5));
        Assert.Throws<ArgumentException>(() => BuildMulti(
            [original, new DeviceHeldInteractionRecord(HolderId, 4, original.Observation)], [], 5));
        Assert.Throws<ArgumentException>(() => BuildMulti([], [], 5));

        var otherHolder = new DeviceHeldInteractionRecord("holder-b", 3, original.Observation);
        var context = MultiHolderContext();
        var builder = new RescuerEvidenceInputBuilder();
        Assert.Throws<ArgumentException>(() => builder.Build(
            context with { ScenarioId = "another-run" }, Seed, 5, RescuerId,
            new[] { original, otherHolder }, Array.Empty<InteractionRecordReceipt>()));
        Assert.Throws<ArgumentException>(() => builder.Build(
            context, Seed + 1, 5, RescuerId,
            new[] { original, otherHolder }, Array.Empty<InteractionRecordReceipt>()));
        Assert.Throws<ArgumentException>(() => builder.Build(
            context with { PublicDeviceIds = [HolderId] }, Seed, 5, RescuerId,
            new[] { original, otherHolder }, Array.Empty<InteractionRecordReceipt>()));
    }

    [Fact]
    public void InvalidMatchingReceiptCannotHideBehindValidReceiptFromAnotherHolder()
    {
        var lateHolder = Record(storedAtTick: 6);
        var otherHolder = new DeviceHeldInteractionRecord("holder-b", 3, lateHolder.Observation);
        Assert.Throws<ArgumentException>(() => BuildMulti(
            [lateHolder, otherHolder],
            [HolderReceipt("holder-b", 5), HolderReceipt(HolderId, 5)], 5));
        AssertNoReceivedEvidence(BuildMulti(
            [lateHolder, otherHolder], [HolderReceipt(HolderId, 5)], 4));
    }

    [Fact]
    public void UnrelatedAndFutureMultiHolderReceiptsDoNotGrantOrChangeCurrentAccess()
    {
        var a = Record();
        var b = new DeviceHeldInteractionRecord("holder-b", 3, a.Observation);
        var valid = HolderReceipt("holder-b", 5);
        var foreign = new InteractionRecordReceipt(ScenarioId, Seed, RecordId,
            "unlisted-holder", RescuerId, 4);
        var otherRescuer = new InteractionRecordReceipt(ScenarioId, Seed, RecordId,
            HolderId, "someone-else", 4);
        var otherRun = new InteractionRecordReceipt(ScenarioId, Seed + 1, RecordId,
            HolderId, RescuerId, 4);
        var future = HolderReceipt(HolderId, 10);
        var baseline = BuildMulti([a, b], [valid], 5);
        var mixed = BuildMulti([a, b], [foreign, otherRescuer, otherRun, future, valid], 5);
        Assert.Equal(JsonDefaults.Serialize(baseline), JsonDefaults.Serialize(mixed));
        AssertNoReceivedEvidence(BuildMulti(
            [a, b], [foreign, otherRescuer, otherRun, future], 5));
    }

    [Theory]
    [InlineData(EvidenceState.SupportedButUncertain)]
    [InlineData(EvidenceState.Conflicting)]
    public void MultipleHoldersPreserveUncertainAndConflictingAlternatives(EvidenceState state)
    {
        var a = Record(state: state, window: new InteractionTimeWindow(1, 2));
        var b = new DeviceHeldInteractionRecord("holder-b", 3, a.Observation);
        var single = Build(a, [Receipt(5)], 5);
        var multi = BuildMulti([a, b], [HolderReceipt("holder-b", 5)], 5);
        // PublicDeviceIds legitimately differs because the multi-holder query
        // declares an additional device; compare evidence and actual inference.
        Assert.Equal(
            JsonDefaults.Serialize(single.Input.AvailableCrossThreadConstraints),
            JsonDefaults.Serialize(multi.Input.AvailableCrossThreadConstraints));
        Assert.Equal(
            JsonDefaults.Serialize(single.Input.AvailableCausalTracingConstraints),
            JsonDefaults.Serialize(multi.Input.AvailableCausalTracingConstraints));
        Assert.Equal(
            JsonDefaults.Serialize(new MultiWeftTracingStrategy().Evaluate(single.Input)),
            JsonDefaults.Serialize(new MultiWeftTracingStrategy().Evaluate(multi.Input)));
        var actual = new MultiWeftTracingStrategy().Evaluate(multi.Input);
        Assert.Equal(["C", "D"], actual.OuterPlausibleCandidateRegionIds);
        Assert.Equal(state,
            Assert.Single(multi.Input.AvailableCrossThreadConstraints!.Constraints).EvidenceBoundaryState);
    }

    [Fact]
    public void MultipleHoldersDoNotPromoteDeviceOnlyEvidenceToPersonEvidence()
    {
        var a = Record(deviceOnly: true);
        var b = new DeviceHeldInteractionRecord("holder-b", 3, a.Observation);
        var received = BuildMulti([a, b], [HolderReceipt("holder-b", 5)], 5);
        Assert.Single(received.ReceivedRecords);
        Assert.Empty(received.Input.AvailableCrossThreadConstraints!.Constraints);
        Assert.Empty(received.Input.AvailableCausalTracingConstraints!.Constraints);
        Assert.Equal(["C", "D"],
            new MultiWeftTracingStrategy().Evaluate(received.Input).CandidateRegionIds);
    }

    private static void AssertNoReceivedEvidence(RescuerEvidenceInput snapshot)
    {
        Assert.Empty(snapshot.ReceivedRecords);
        Assert.Empty(snapshot.Input.AvailableEvidence.Observations);
        Assert.Empty(snapshot.Input.AvailableCrossThreadConstraints!.Constraints);
        Assert.Empty(snapshot.Input.AvailableCausalTracingConstraints!.Constraints);
    }

    private static RescuerEvidenceInput Build(
        DeviceHeldInteractionRecord record,
        IEnumerable<InteractionRecordReceipt> receipts,
        long currentTick = 5) => new RescuerEvidenceInputBuilder().Build(
            Context(), Seed, currentTick, RescuerId, record, receipts);


    private static RescuerEvidenceInput BuildMulti(
        IEnumerable<DeviceHeldInteractionRecord> heldRecords,
        IEnumerable<InteractionRecordReceipt> receipts,
        long currentTick) => new RescuerEvidenceInputBuilder().Build(
            MultiHolderContext(), Seed, currentTick, RescuerId, heldRecords, receipts);

    private static RescueContext MultiHolderContext() =>
        Context() with { PublicDeviceIds = [HolderId, "holder-b"] };

    private static InteractionRecordReceipt HolderReceipt(string holderId, long tick) =>
        new(ScenarioId, Seed, RecordId, holderId, RescuerId, tick);

    private static InteractionRecordReceipt Receipt(long tick) =>
        new(ScenarioId, Seed, RecordId, HolderId, RescuerId, tick);

    private static DeviceHeldInteractionRecord Record(
        EvidenceState state = EvidenceState.Confirmed,
        bool deviceOnly = false,
        long availableAtTick = 3,
        long storedAtTick = 3,
        InteractionTimeWindow? window = null) => new(
            HolderId,
            storedAtTick,
            Observation(state, deviceOnly, availableAtTick, window));

    private static InteractionObservation Observation(
        EvidenceState state = EvidenceState.Confirmed,
        bool deviceOnly = false,
        long availableAtTick = 3,
        InteractionTimeWindow? window = null,
        InteractionVisibility visibility = InteractionVisibility.PrivateEvidence,
        InteractionProvenanceKind provenanceKind = InteractionProvenanceKind.EvidenceRecord) =>
        InteractionObservation.Create(
            ScenarioId, Seed, RecordId, null,
            InteractionRelationKind.PersonPersonEncounter,
            [
                new InteractionParticipant(
                    deviceOnly ? HolderId : TargetId,
                    deviceOnly ? InteractionEntityKind.Device : InteractionEntityKind.Person,
                    InteractionParticipantRole.EncounterParticipant),
                new InteractionParticipant("person-b", InteractionEntityKind.Person,
                    InteractionParticipantRole.EncounterParticipant)
            ],
            window ?? InteractionTimeWindow.At(2),
            ["A"],
            availableAtTick,
            state,
            isPersonEvidence: !deviceOnly,
            isDeviceOnlyEvidence: deviceOnly,
            visibility,
            [new InteractionProvenance(provenanceKind, "upstream-evidence")]);

    private static RescueContext Context() => new()
    {
        ScenarioId = ScenarioId,
        TargetPersonId = TargetId,
        NormalWalkingRadius = 0,
        PublicDeviceIds = [HolderId],
        PublicRegions = new[] { "A", "B", "C", "D" }.Select((id, index) => new RegionDefinition
        {
            Id = id,
            Type = RegionType.Outdoor,
            Center = new Position(index, 0, 0, id)
        }).ToArray(),
        PublicCausalTracingModel = new PublicCausalTracingModel
        {
            CandidateStates = [State(2, ["A", "B"]), State(5, ["C", "D"])]
        },
        PublicHazardEnvelope = new PublicHazardEnvelope
        {
            HazardId = "declared-public-model",
            SourceRegionId = "A",
            PublishedAtTick = 0,
            Branches = [Branch("A", "C"), Branch("B", "D")]
        }
    };

    private static PublicCandidateWarpStateDefinition State(long tick, IReadOnlyList<string> regions) => new()
    {
        TimeWindow = InteractionTimeWindow.At(tick),
        TemporalKind = CandidateWarpTemporalKind.EventPoint,
        RegionIds = regions,
        EvidenceBoundaryState = EvidenceState.SupportedButUncertain,
        Provenance = [CandidateWarpProvenance.Create(
            CandidateWarpSourceKind.PublicCausalModel, "declared-public-model", 0,
            false, false, EvidenceState.SupportedButUncertain, regions)]
    };

    private static PublicHazardBranch Branch(string source, string destination) => new()
    {
        SourceRegionId = source,
        DestinationRegionId = destination,
        MinimumTravelTicks = 3,
        MaximumTravelTicks = 3,
        TransportType = HazardTransportType.HazardDriven,
        ProvenanceId = $"declared-{source}-{destination}",
        PublishedAtTick = 0
    };
}
