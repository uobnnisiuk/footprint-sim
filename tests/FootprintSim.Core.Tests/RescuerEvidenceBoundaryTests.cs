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

    [Fact]
    public void DistinctOriginalsEnterOneInferenceOnlyWhenReceived()
    {
        var narrow = Record();
        var broad = DistinctRecord(["A", "B"]);
        var originals = new[] { narrow, broad };
        var first = DistinctReceipt(3);
        var second = Receipt(5);
        var before = BuildDistinct(originals, [first, second], 2);
        AssertNoReceivedEvidence(before);
        Assert.DoesNotContain(RecordId, JsonDefaults.Serialize(before));
        Assert.DoesNotContain(broad.OriginalRecordId, JsonDefaults.Serialize(before));

        var atFirstCutoff = BuildDistinct(originals, [first, second], 4);
        Assert.Equal(broad.OriginalRecordId, Assert.Single(atFirstCutoff.ReceivedRecords).OriginalRecordId);
        Assert.Single(atFirstCutoff.Input.AvailableCausalTracingConstraints!.Constraints);
        Assert.DoesNotContain(narrow.Observation.ObservationId, JsonDefaults.Serialize(atFirstCutoff));

        // Hold query time and public model fixed when testing information gain.
        var none = BuildDistinct(originals, [], 5);
        var onlyBroad = BuildDistinct(originals, [first], 5);
        var both = BuildDistinct(originals, [first, second], 5);
        Assert.Equal(2, both.ReceivedRecords.Count);
        Assert.Equal(2, both.Input.AvailableCrossThreadConstraints!.Constraints.Count);
        Assert.Equal(2, both.Input.AvailableCausalTracingConstraints!.Constraints.Count);
        Assert.Empty(both.Input.AvailableEvidence.Observations);
        Assert.Equal(["C", "D"], new MultiWeftTracingStrategy().Evaluate(none.Input).CandidateRegionIds);
        Assert.Equal(["C", "D"], new MultiWeftTracingStrategy().Evaluate(onlyBroad.Input).CandidateRegionIds);
        var result = new MultiWeftTracingStrategy().Evaluate(both.Input);
        Assert.Equal(["C"], result.CandidateRegionIds);
        Assert.Equal(["C"], result.PreferredCandidateRegionIds);
        Assert.Equal(2, result.CausalTracingResult!.SourceGroups.Count);
        Assert.Contains(result.CausalTracingResult.Steps, step =>
            step.Direction == CausalTracingDirection.Forward);
        Assert.All(both.Input.AvailableCausalTracingConstraints.Constraints, constraint =>
            Assert.Contains(result.CausalTracingResult.Steps, step =>
                step.ConstraintId == constraint.ConstraintId &&
                step.Direction == CausalTracingDirection.CrossThread &&
                step.Disposition == TracingStepDisposition.Applied));
    }

    [Fact]
    public void DistinctOriginalOrderingAndRedeliveryAreDeterministic()
    {
        var narrow = Record();
        var broad = DistinctRecord(["A", "B"]);
        var originals = new[] { narrow, broad };
        var receipts = new[] { DistinctReceipt(3), Receipt(5) };
        var expected = BuildDistinct(originals, receipts, 7);
        var actual = BuildDistinct(originals.Reverse(),
            new[] { Receipt(7), Receipt(5), DistinctReceipt(6), DistinctReceipt(3), Receipt(5) }, 7);
        Assert.Equal(JsonDefaults.Serialize(expected), JsonDefaults.Serialize(actual));
        Assert.Equal(
            JsonDefaults.Serialize(new MultiWeftTracingStrategy().Evaluate(expected.Input)),
            JsonDefaults.Serialize(new MultiWeftTracingStrategy().Evaluate(actual.Input)));
        Assert.Equal(new[] { 5L, 3L }, actual.ReceivedRecords.Select(record => record.FirstReceivedAtTick));

        var invertedArrival = BuildDistinct(originals, [Receipt(3), DistinctReceipt(5)], 5);
        var atSecond = new MultiWeftTracingStrategy().Evaluate(invertedArrival.Input);
        Assert.Equal(["C"], atSecond.CandidateRegionIds);
        Assert.Equal(2, atSecond.CausalTracingResult!.SourceGroups.Count);
    }

    [Theory]
    [InlineData(false, 2)]
    [InlineData(true, 1)]
    public void DistinctRootsPreserveOriginalsAndSharedAncestry(bool sharedAncestry, int expectedGroups)
    {
        var first = Record();
        var second = DistinctRecord(["A"], ancestry: sharedAncestry ? "upstream-evidence" : "other-upstream");
        var firstBytes = JsonDefaults.Serialize(first.Observation);
        var secondBytes = JsonDefaults.Serialize(second.Observation);
        var snapshot = BuildDistinct([first, second], [Receipt(5), DistinctReceipt(5)], 5);
        var trace = new MultiWeftTracingStrategy().Evaluate(snapshot.Input).CausalTracingResult!;
        Assert.Equal(expectedGroups, trace.SourceGroups.Count);
        Assert.Equal(2, trace.SourceGroups.Sum(group => group.SourceObservationIds.Count));
        Assert.Equal(2, trace.SourceGroups.Sum(group => group.ConstraintIds.Count));
        Assert.Same(first.Observation, snapshot.ReceivedRecords[0].OriginalObservation);
        Assert.Same(second.Observation, snapshot.ReceivedRecords[1].OriginalObservation);
        Assert.Equal(firstBytes, JsonDefaults.Serialize(first.Observation));
        Assert.Equal(secondBytes, JsonDefaults.Serialize(second.Observation));
        Assert.All(snapshot.ReceivedRecords, record =>
            Assert.Equal(InteractionVisibility.PrivateEvidence, record.OriginalObservation.Visibility));
        Assert.All(snapshot.Input.AvailableCausalTracingConstraints!.Constraints, constraint =>
        {
            Assert.DoesNotContain(HolderId, constraint.SourceAncestryIds);
            Assert.DoesNotContain("holder-b", constraint.SourceAncestryIds);
            Assert.DoesNotContain(RescuerId, constraint.SourceAncestryIds);
            Assert.Equal(InteractionTimeWindow.At(2), constraint.ConstrainedTimeWindow);
        });
    }

    [Theory]
    [InlineData(EvidenceState.SupportedButUncertain)]
    [InlineData(EvidenceState.Conflicting)]
    public void DistinctUncertainOrConflictingOriginalDoesNotHardExclude(EvidenceState state)
    {
        var broad = DistinctRecord(["A", "B"]);
        var narrow = Record(state: state, window: new InteractionTimeWindow(1, 2));
        var snapshot = BuildDistinct([narrow, broad], [Receipt(5), DistinctReceipt(3)], 5);
        var constraint = snapshot.Input.AvailableCausalTracingConstraints!.Constraints
            .Single(item => item.DirectSourceId == RecordId);
        Assert.Equal(state, constraint.EvidenceBoundaryState);
        Assert.Equal(new InteractionTimeWindow(1, 2), constraint.SourceObservedTimeWindow);
        Assert.Equal(new InteractionTimeWindow(1, 2), constraint.ConstrainedTimeWindow);
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
    public void ContradictoryConfirmedOriginalsRetainOuterAlternativesAndConflict()
    {
        var snapshot = BuildDistinct([Record(), DistinctRecord(["B"])],
            [Receipt(5), DistinctReceipt(5)], 5);
        var result = new MultiWeftTracingStrategy().Evaluate(snapshot.Input);
        Assert.Equal(["C", "D"], result.OuterPlausibleCandidateRegionIds);
        Assert.Equal(2, result.CausalTracingResult!.SourceGroups.Count);
        Assert.Contains(result.CausalTracingResult.Steps, step =>
            step.Disposition == TracingStepDisposition.MarkConflict && step.ConflictDetected);
    }

    [Fact]
    public void DistinctDeviceOnlyOriginalAddsNoPersonConstraint()
    {
        var snapshot = BuildDistinct([Record(deviceOnly: true), DistinctRecord(["A", "B"])],
            [Receipt(5), DistinctReceipt(3)], 5);
        Assert.Equal(2, snapshot.ReceivedRecords.Count);
        Assert.True(snapshot.ReceivedRecords[0].OriginalObservation.IsDeviceOnlyEvidence);
        Assert.Single(snapshot.Input.AvailableCrossThreadConstraints!.Constraints);
        Assert.Single(snapshot.Input.AvailableCausalTracingConstraints!.Constraints);
        var result = new MultiWeftTracingStrategy().Evaluate(snapshot.Input);
        Assert.Equal(["C", "D"], result.CandidateRegionIds);
        Assert.Single(result.CausalTracingResult!.SourceGroups);
    }

    [Theory]
    [InlineData("scenario")]
    [InlineData("seed")]
    [InlineData("record")]
    [InlineData("holder")]
    [InlineData("rescuer")]
    public void UnrelatedReceiptCannotAddSecondOriginal(string mismatch)
    {
        var records = new[] { Record(), DistinctRecord(["A", "B"]) };
        var unrelated = new InteractionRecordReceipt(
            mismatch == "scenario" ? "other-run" : ScenarioId,
            mismatch == "seed" ? Seed + 1 : Seed,
            mismatch == "record" ? "unknown-record" : RecordId,
            mismatch == "holder" ? "holder-b" : HolderId,
            mismatch == "rescuer" ? "other-rescuer" : RescuerId, 5);
        var expected = BuildDistinct(records, [DistinctReceipt(3)], 5);
        var actual = BuildDistinct(records, [DistinctReceipt(3), unrelated, Receipt(10)], 5);
        Assert.Equal(JsonDefaults.Serialize(expected), JsonDefaults.Serialize(actual));
        Assert.Equal(["C", "D"], new MultiWeftTracingStrategy().Evaluate(actual.Input).CandidateRegionIds);
    }

    [Theory]
    [InlineData(3, 6, 5)]
    [InlineData(6, 6, 5)]
    [InlineData(2, 2, 1)]
    public void InvalidMatchingReceiptCannotHideBehindAnotherValidOriginal(long available, long stored, long received)
    {
        var late = Record(availableAtTick: available, storedAtTick: stored);
        var other = DistinctRecord(["A", "B"]);
        Assert.Throws<ArgumentException>(() => BuildDistinct([late, other],
            [DistinctReceipt(3), Receipt(received)], 10));
        // A future invalid assertion is not consumed by the earlier query.
        var future = Record(availableAtTick: 20, storedAtTick: 20);
        var expected = BuildDistinct([future, other], [DistinctReceipt(3)], 5);
        var actual = BuildDistinct([future, other], [DistinctReceipt(3), Receipt(10)], 5);
        Assert.Equal(JsonDefaults.Serialize(expected), JsonDefaults.Serialize(actual));
    }

    [Fact]
    public void DistinctCompositionRejectsInvalidOriginalCollectionsAndNullReceipts()
    {
        var first = Record();
        var second = DistinctRecord(["A"]);
        Assert.Throws<ArgumentNullException>(() => BuildDistinct(null!, [], 5));
        Assert.Throws<ArgumentNullException>(() => BuildDistinct([first, second], null!, 5));
        Assert.Throws<ArgumentException>(() => BuildDistinct([], [], 5));
        Assert.Throws<ArgumentException>(() => BuildDistinct([first], [], 5));
        Assert.Throws<ArgumentException>(() => BuildDistinct([first, second, first], [], 5));
        Assert.Throws<ArgumentException>(() => BuildDistinct([first, null!], [], 5));
        Assert.Throws<ArgumentException>(() => BuildDistinct([first, first], [], 5));
        Assert.Throws<ArgumentException>(() => BuildDistinct(
            [first, new DeviceHeldInteractionRecord("holder-b", 3, first.Observation)], [], 5));
        Assert.Throws<ArgumentException>(() => BuildDistinct(
            [first, Record(state: EvidenceState.Conflicting)], [], 5));
        Assert.Throws<ArgumentException>(() => BuildDistinct([first, second], [null!], 5));
    }

    [Fact]
    public void DistinctCompositionValidatesEveryOriginalRunAndHolder()
    {
        var first = Record();
        var otherRun = DistinctRecord(["A"], seed: Seed + 1);
        Assert.Throws<ArgumentException>(() => BuildDistinct([first, otherRun], [Receipt(5)], 5));
        var undeclared = new DeviceHeldInteractionRecord("unlisted-holder", 3,
            DistinctRecord(["A"]).Observation);
        Assert.Throws<ArgumentException>(() => BuildDistinct([first, undeclared], [Receipt(5)], 5));
        Assert.Throws<ArgumentOutOfRangeException>(() => BuildDistinct(
            [first, DistinctRecord(["A"])], [], -1));
    }

    private static RescuerEvidenceInput BuildDistinct(
        IEnumerable<DeviceHeldInteractionRecord> records,
        IEnumerable<InteractionRecordReceipt> receipts,
        long currentTick) => new RescuerEvidenceInputBuilder().BuildDistinctOriginals(
            MultiHolderContext(), Seed, currentTick, RescuerId, records, receipts);

    private static InteractionRecordReceipt DistinctReceipt(long tick) =>
        new(ScenarioId, Seed, "second-original", "holder-b", RescuerId, tick);

    private static DeviceHeldInteractionRecord DistinctRecord(
        IReadOnlyList<string> regions,
        string ancestry = "other-upstream",
        int seed = Seed) => new("holder-b", 3, InteractionObservation.Create(
            ScenarioId, seed, "second-original", null,
            InteractionRelationKind.PersonPersonEncounter,
            [
                new InteractionParticipant(TargetId, InteractionEntityKind.Person,
                    InteractionParticipantRole.EncounterParticipant),
                new InteractionParticipant("person-c", InteractionEntityKind.Person,
                    InteractionParticipantRole.EncounterParticipant)
            ],
            InteractionTimeWindow.At(2), regions, 3, EvidenceState.Confirmed,
            isPersonEvidence: true, isDeviceOnlyEvidence: false,
            InteractionVisibility.PrivateEvidence,
            [new InteractionProvenance(InteractionProvenanceKind.EvidenceRecord, ancestry)]));

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

    [Fact]
    public void ReceiptGatedComparisonUsesTheSamePublicWorldAndSeparatesTimelineFromAblations()
    {
        var target = Record();
        var later = LaterComparisonRecord();
        var receipts = new[] { Receipt(6), LaterComparisonReceipt(7) };
        var report = RescuerEvidenceMethodComparison.Run(
            MultiHolderContext(), Seed, RescuerId, [target, later], receipts,
            InteractionTimeWindow.At(2), [5, 6, 7], 7);
        Assert.Equal(3, report.ActualTimeline.Count);
        Assert.Equal(4, report.SameCutoffAblations.Count);
        Assert.Equal([0, 1, 2], report.ActualTimeline.Select(row => row.ReceivedOriginalCount));
        Assert.Equal([0, 1, 1, 2],
            report.SameCutoffAblations.Select(row => row.ReceivedOriginalCount));
        Assert.All(report.ActualTimeline, row =>
        {
            Assert.Equal(4, row.Methods.Count);
            Assert.Equal(["A", "B"], row.Methods[0].OuterCandidateRegionIds);
            Assert.Equal(2, row.Methods[0].BaselineCandidateCount);
            Assert.Equal(row.ReceivedOriginalCount, row.AvailableConstraintCount);
        });
        Assert.Empty(report.ActualTimeline[0].ReceivedOriginalIds);
        Assert.DoesNotContain(target.Observation.ObservationId,
            JsonDefaults.Serialize(report.ActualTimeline[0]));
        Assert.DoesNotContain(later.Observation.ObservationId,
            JsonDefaults.Serialize(report.ActualTimeline[1]));
        var first = report.ActualTimeline[1];
        Assert.Equal(["A", "B"], first.Methods[1].OuterCandidateRegionIds);
        Assert.Equal(["A"], first.Methods[2].OuterCandidateRegionIds);
        Assert.Equal(1, first.Methods[2].CandidateReduction);
        Assert.Equal([RecordId], first.Methods[2].GenerationDirectSourceIds);
        Assert.Empty(first.Methods[2].TraversalDirectSourceIds);
        Assert.Empty(first.Methods[1].TraversalDirectSourceIds);
        Assert.Equal(6, report.FirstDifferences.Single(
            item => item.Method == "distributed-reconstruction").FirstDifferentTick);

        var second = report.ActualTimeline[2];
        Assert.Equal(2, second.AvailableSourceGroupCount);
        Assert.Equal(["second-original"], second.Methods[1].TraversalDirectSourceIds);
        Assert.Equal([RecordId], second.Methods[3].GenerationDirectSourceIds);
        Assert.Equal(["second-original"], second.Methods[3].TraversalDirectSourceIds);
        Assert.True(second.Methods[3].ForwardTransitionSteps +
            second.Methods[3].BackwardTransitionSteps > 0);
        Assert.Contains(second.Methods[3].PublicTransitionIds,
            id => !string.IsNullOrWhiteSpace(id));
        Assert.Equal(JsonDefaults.Serialize(second.Methods),
            JsonDefaults.Serialize(report.SameCutoffAblations[3].Methods));
        Assert.Equal(JsonDefaults.Serialize(report),
            JsonDefaults.Serialize(RescuerEvidenceMethodComparison.Run(
                MultiHolderContext(), Seed, RescuerId, [later, target],
                [LaterComparisonReceipt(7), Receipt(6), Receipt(6)],
                InteractionTimeWindow.At(2), [7, 5, 6], 7)));
    }


    [Fact]
    public void ReceiptGatedComparisonPinsAllMethodOutcomesAndWorkAtFixedCutoffs()
    {
        var report = RescuerEvidenceMethodComparison.Run(
            MultiHolderContext(), Seed, RescuerId,
            [Record(), LaterComparisonRecord()],
            [Receipt(6), LaterComparisonReceipt(7)],
            InteractionTimeWindow.At(2), [5, 6, 7], 7);

        // A = public-prior, B = known-relation-backward,
        // C = distributed-reconstruction, C-local = local traversal.
        // Work string is generation / forward / backward /
        // inspected observation records / inspected public relations.
        static void AssertRows(
            RescuerEvidenceMethodQueryRow row, int availableGroups,
            string[] outer, int[] usedGroups, string[] work)
        {
            Assert.Equal(availableGroups, row.AvailableSourceGroupCount);
            Assert.Equal(outer, row.Methods.Select(item =>
                string.Join(",", item.OuterCandidateRegionIds)));
            Assert.Equal(usedGroups, row.Methods.Select(item => item.UsedSourceGroupCount));
            Assert.All(row.Methods, item =>
                Assert.Equal(availableGroups, item.AvailableSourceGroupCount));
            Assert.Equal(outer.Select(item => item == "A,B" ? 0 : 1),
                row.Methods.Select(item => item.CandidateReduction));
            Assert.Equal(work, row.Methods.Select(item =>
                $"{item.GenerationSteps}/{item.ForwardTransitionSteps}/" +
                $"{item.BackwardTransitionSteps}/{item.EvidenceRecordsInspected}/" +
                $"{item.PublicRelationsInspected}"));
            Assert.Null(row.Methods[0].UncertainCandidateRegionIds);
            Assert.Null(row.Methods[1].UncertainCandidateRegionIds);
            Assert.All(row.Methods, item => Assert.Equal(0, item.VerificationSteps));
        }

        AssertRows(report.ActualTimeline[0], 0,
            ["A,B", "A,B", "A,B", "A,B"], [0, 0, 0, 0],
            ["0/0/0/0/0", "0/1/1/0/2", "3/0/0/0/0", "3/1/1/0/2"]);
        AssertRows(report.ActualTimeline[1], 1,
            ["A,B", "A,B", "A", "A"], [0, 0, 1, 1],
            ["0/0/0/0/0", "0/1/1/0/2", "3/0/0/1/1", "3/2/2/0/4"]);
        AssertRows(report.ActualTimeline[2], 2,
            ["A,B", "A", "A", "A"], [0, 1, 1, 2],
            ["0/0/0/0/0", "0/2/2/1/5", "3/0/0/1/1", "3/1/1/1/3"]);

        // Same query tick: varying evidence, not later availability.
        AssertRows(report.SameCutoffAblations[0], 0,
            ["A,B", "A,B", "A,B", "A,B"], [0, 0, 0, 0],
            ["0/0/0/0/0", "0/1/1/0/2", "3/0/0/0/0", "3/1/1/0/2"]);
        AssertRows(report.SameCutoffAblations[1], 1,
            ["A,B", "A,B", "A", "A"], [0, 0, 1, 1],
            ["0/0/0/0/0", "0/1/1/0/2", "3/0/0/1/1", "3/2/2/0/4"]);
        AssertRows(report.SameCutoffAblations[2], 1,
            ["A,B", "A", "A,B", "A"], [0, 1, 0, 1],
            ["0/0/0/0/0", "0/2/2/1/5", "3/0/0/0/0", "3/2/2/1/5"]);
        AssertRows(report.SameCutoffAblations[3], 2,
            ["A,B", "A", "A", "A"], [0, 1, 1, 2],
            ["0/0/0/0/0", "0/2/2/1/5", "3/0/0/1/1", "3/1/1/1/3"]);

        var both = report.ActualTimeline[2].Methods;
        Assert.Equal(["second-original"], both[1].TraversalDirectSourceIds);
        Assert.Equal([RecordId], both[2].GenerationDirectSourceIds);
        Assert.Equal([RecordId], both[3].GenerationDirectSourceIds);
        Assert.Equal(["second-original"], both[3].TraversalDirectSourceIds);
        Assert.Empty(both[0].GenerationDirectSourceIds);
        Assert.Empty(both[0].TraversalDirectSourceIds);
        Assert.Equal(2, both[0].UnusedDirectSourceIds.Count);
        Assert.Equal(["A", "B"], report.ActualTimeline[0].Methods[2].UncertainCandidateRegionIds);
        Assert.Empty(both[2].UncertainCandidateRegionIds!);
        Assert.All(both, item => Assert.Empty(item.ConflictedCandidateRegionIds ?? []));
        Assert.Equal(7, report.FirstDifferences.Single(
            item => item.Method == "known-relation-backward").FirstDifferentTick);
        Assert.Equal(6, report.FirstDifferences.Single(
            item => item.Method == "distributed-reconstruction-local").FirstDifferentTick);
    }

    [Fact]
    public void ReceiptGatedComparisonDoesNotDoubleCountSharedAncestry()
    {
        var report = RescuerEvidenceMethodComparison.Run(
            MultiHolderContext(), Seed, RescuerId,
            [Record(), LaterComparisonRecord("upstream-evidence")],
            [Receipt(6), LaterComparisonReceipt(7), LaterComparisonReceipt(7)],
            InteractionTimeWindow.At(2), [5, 6, 7], 7);
        var row = report.ActualTimeline[2];
        Assert.Equal(2, row.ReceivedOriginalCount);
        Assert.Equal(2, row.AvailableConstraintCount);
        Assert.Equal(1, row.AvailableSourceGroupCount);
        Assert.Equal([0, 1, 1, 1],
            row.Methods.Select(item => item.UsedSourceGroupCount));
        Assert.All(row.Methods, item => Assert.Equal(1, item.AvailableSourceGroupCount));
        Assert.Equal(JsonDefaults.Serialize(row.Methods),
            JsonDefaults.Serialize(report.SameCutoffAblations[3].Methods));
    }

    [Fact]
    public void ReceiptGatedMethodComparisonKeepsUncertaintyConflictAndDeviceBoundary()
    {
        foreach (var state in new[] {
            EvidenceState.SupportedButUncertain, EvidenceState.Conflicting })
        {
            var record = Record(state: state);
            var snapshot = BuildDistinct(
                [record, LaterComparisonRecord()],
                [Receipt(6), LaterComparisonReceipt(7)], 7);
            var result = RescuerEvidenceMethodComparison.Compare(
                snapshot, InteractionTimeWindow.At(2));
            Assert.Equal(["A", "B"],
                result.Methods.Single(item => item.Method == "distributed-reconstruction")
                    .OuterCandidateRegionIds);
            Assert.Equal(2, result.ReceivedOriginalCount);
            Assert.All(result.Methods, method =>
                Assert.True(method.CandidateCount >= 0));
        }

        var device = BuildDistinct(
            [Record(deviceOnly: true), LaterComparisonRecord()],
            [Receipt(6), LaterComparisonReceipt(7)], 7);
        var deviceResult = RescuerEvidenceMethodComparison.Compare(
            device, InteractionTimeWindow.At(2));
        Assert.Empty(deviceResult.Methods[2].GenerationDirectSourceIds);
        Assert.Equal(["A", "B"], deviceResult.Methods[2].OuterCandidateRegionIds);
    }

    [Fact]
    public void MethodComparisonNeverUsesFutureUnreceivedPrivateOrOtherRescuerEvidence()
    {
        var originals = new[] { Record(), LaterComparisonRecord() };
        var before = BuildDistinct(originals,
            [Receipt(6), LaterComparisonReceipt(7)], 5);
        var withUnrelated = BuildDistinct(originals,
            [Receipt(6), LaterComparisonReceipt(7),
                new InteractionRecordReceipt(ScenarioId, Seed, RecordId, HolderId,
                    "another-rescuer", 4)], 5);
        var prior = RescuerEvidenceMethodComparison.Compare(
            before, InteractionTimeWindow.At(2));
        Assert.Equal(JsonDefaults.Serialize(prior),
            JsonDefaults.Serialize(RescuerEvidenceMethodComparison.Compare(
                withUnrelated, InteractionTimeWindow.At(2))));
        Assert.Empty(prior.ReceivedSourceObservationIds);
        Assert.Equal(0, prior.AvailableSourceGroupCount);
        Assert.All(prior.Methods, method => Assert.Equal(["A", "B"],
            method.OuterCandidateRegionIds));
        Assert.Throws<ArgumentException>(() => RescuerEvidenceMethodComparison.Run(
            MultiHolderContext(), Seed, RescuerId, originals,
            [Receipt(6), LaterComparisonReceipt(9)],
            InteractionTimeWindow.At(2), [5, 6, 7], 7));
    }

    [Fact]
    public void SyntheticTruthIsEvaluatedOnlyAfterThePublicComparison()
    {
        var report = RescuerEvidenceMethodComparison.Run(
            MultiHolderContext(), Seed, RescuerId,
            [Record(), LaterComparisonRecord()],
            [Receipt(6), LaterComparisonReceipt(7)],
            InteractionTimeWindow.At(2), [5, 6, 7], 7);
        var originalJson = JsonDefaults.Serialize(report);
        var truthA = RescuerEvidenceMethodComparison.EvaluateSyntheticTruth(report, "A");
        var truthB = RescuerEvidenceMethodComparison.EvaluateSyntheticTruth(report, "B");
        Assert.Contains(truthA.Evaluations, item =>
            item.QueryTick == 6 && item.Method == "distributed-reconstruction" &&
            item.ContainsSyntheticTruth && !item.FalseExclusion);
        Assert.Contains(truthB.Evaluations, item =>
            item.QueryTick == 6 && item.Method == "distributed-reconstruction" &&
            !item.ContainsSyntheticTruth && item.FalseExclusion);
        Assert.Equal(originalJson, JsonDefaults.Serialize(report));
        Assert.DoesNotContain("SyntheticTruth", originalJson);
    }

    private static DeviceHeldInteractionRecord LaterComparisonRecord(
        string ancestry = "other-upstream") =>
        new("holder-b", 6, InteractionObservation.Create(
            ScenarioId, Seed, "second-original", null,
            InteractionRelationKind.PersonPersonEncounter,
            [
                new InteractionParticipant(TargetId, InteractionEntityKind.Person,
                    InteractionParticipantRole.EncounterParticipant),
                new InteractionParticipant("person-c", InteractionEntityKind.Person,
                    InteractionParticipantRole.EncounterParticipant)
            ],
            InteractionTimeWindow.At(5), ["C"], 6, EvidenceState.Confirmed,
            isPersonEvidence: true, isDeviceOnlyEvidence: false,
            InteractionVisibility.PrivateEvidence,
            [new InteractionProvenance(InteractionProvenanceKind.EvidenceRecord,
                ancestry)]));

    private static InteractionRecordReceipt LaterComparisonReceipt(long tick) =>
        new(ScenarioId, Seed, "second-original", "holder-b", RescuerId, tick);


    private static RescueContext ComplementaryPublicContext()
    {
        var context = MultiHolderContext();
        return context with
        {
            PublicRegions = new[] { "A", "B", "C", "D", "E", "F" }
                .Select((id, index) => new RegionDefinition
                {
                    Id = id,
                    Type = RegionType.Outdoor,
                    Center = new Position(index, 0, 0, id)
                }).ToArray(),
            PublicCausalTracingModel = new PublicCausalTracingModel
            {
                CandidateStates = [State(2, ["A", "B", "C"]), State(5, ["D", "E", "F"])]
            },
            PublicHazardEnvelope = new PublicHazardEnvelope
            {
                HazardId = "declared-public-model",
                SourceRegionId = "A",
                PublishedAtTick = 0,
                Branches = [Branch("A", "D"), Branch("B", "E"), Branch("C", "F")]
            }
        };
    }

    private static DeviceHeldInteractionRecord ComplementaryOriginal(
        string originalId, string holder, long atTick, IReadOnlyList<string> regions,
        long available, string upstream, EvidenceState state = EvidenceState.Confirmed,
        bool deviceOnly = false) => new(holder, available, InteractionObservation.Create(
            ScenarioId, Seed, originalId, null,
            InteractionRelationKind.PersonPersonEncounter,
            [
                new InteractionParticipant(deviceOnly ? holder : TargetId,
                    deviceOnly ? InteractionEntityKind.Device : InteractionEntityKind.Person,
                    InteractionParticipantRole.EncounterParticipant),
                new InteractionParticipant("person-b", InteractionEntityKind.Person,
                    InteractionParticipantRole.EncounterParticipant)
            ],
            InteractionTimeWindow.At(atTick), regions, available, state,
            isPersonEvidence: !deviceOnly, isDeviceOnlyEvidence: deviceOnly,
            InteractionVisibility.PrivateEvidence,
            [new InteractionProvenance(InteractionProvenanceKind.EvidenceRecord, upstream)]));

    private static RescuerEvidenceMethodReport ComplementaryRun(
        EvidenceState firstState = EvidenceState.Confirmed,
        bool deviceOnly = false,
        string secondAncestry = "weft-b",
        bool reversed = false)
    {
        var first = ComplementaryOriginal(
            RecordId, HolderId, 2, ["A", "B"], 3, "weft-a", firstState, deviceOnly);
        var second = ComplementaryOriginal(
            "second-original", "holder-b", 5, ["E", "F"], 6, secondAncestry);
        var records = reversed ? new[] { second, first } : new[] { first, second };
        var receipts = reversed
            ? new[] { LaterComparisonReceipt(7), Receipt(6), Receipt(6) }
            : new[] { Receipt(6), LaterComparisonReceipt(7) };
        return RescuerEvidenceMethodComparison.Run(
            ComplementaryPublicContext(), Seed, RescuerId, records, receipts,
            InteractionTimeWindow.At(2), reversed ? [7, 5, 6] : [5, 6, 7], 7);
    }

    [Fact]
    public void ComplementaryWeftComparisonMeasuresFourSameCutoffEvidenceConditions()
    {
        var report = ComplementaryRun();
        var result = RescuerEvidenceMethodComparison.AnalyzePairedContributions(report);
        Assert.Equal("available", result.Status);
        Assert.Equal(7, result.QueryTick);
        Assert.Equal([RecordId, "second-original"], result.OriginalIds);
        Assert.Equal(4, result.Methods.Count);
        Assert.All(result.Methods, method =>
        {
            Assert.Equal("available", method.Status);
            Assert.Equal(["neither", "first-only", "second-only", "both"],
                method.Conditions.Select(row => row.Label));
            Assert.Equal([0, 1, 1, 2],
                method.Conditions.Select(row => row.ReceivedOriginalCount));
            Assert.Equal([0, 1, 1, 2],
                method.Conditions.Select(row => row.AvailableSourceGroupCount));
            Assert.All(method.Conditions, row => Assert.Equal(7, row.QueryTick));
        });
        var local = result.Methods.Single(method =>
            method.Method == "distributed-reconstruction-local");
        Assert.Equal(["A", "B", "C"], local.Conditions[0].OuterCandidateRegionIds);
        // Fixed hand-calculation hypotheses, not outcomes adjusted after a test.
        Assert.Equal(["A", "B"], local.Conditions[1].OuterCandidateRegionIds);
        Assert.Equal(["B", "C"], local.Conditions[2].OuterCandidateRegionIds);
        Assert.Equal(["B"], local.Conditions[3].OuterCandidateRegionIds);
        Assert.True(local.BothStrictlyNarrowerThanEachSingle);
        Assert.Equal(["A"], local.ExcludedWhenAddingSecond);
        Assert.Equal(["C"], local.ExcludedWhenAddingFirst);
        Assert.Empty(local.AdditionalExclusionsBeyondSingles!);
        Assert.Empty(local.ReappearedComparedWithEitherSingle!);
        Assert.Equal(1, local.CandidateReductionFromFirst);
        Assert.Equal(1, local.CandidateReductionFromSecond);
        Assert.Equal([RecordId], local.Conditions[3].GenerationDirectSourceIds);
        Assert.Equal(["second-original"], local.Conditions[3].TraversalDirectSourceIds);
        Assert.Equal(2, local.Conditions[3].UsedSourceGroupCount);
        Assert.True(local.Conditions[3].ForwardTransitionSteps > 0);
        Assert.True(local.Conditions[3].BackwardTransitionSteps > 0);
        var backward = result.Methods.Single(method =>
            method.Method == "known-relation-backward");
        Assert.Equal(["A", "B", "C"], backward.Conditions[0].OuterCandidateRegionIds);
        Assert.Equal(["A", "B", "C"], backward.Conditions[1].OuterCandidateRegionIds);
        Assert.Equal(["B", "C"], backward.Conditions[2].OuterCandidateRegionIds);
        Assert.Equal(["B", "C"], backward.Conditions[3].OuterCandidateRegionIds);
        var reconstructed = result.Methods.Single(method =>
            method.Method == "distributed-reconstruction");
        Assert.Equal(["A", "B", "C"], reconstructed.Conditions[0].OuterCandidateRegionIds);
        Assert.Equal(["A", "B"], reconstructed.Conditions[1].OuterCandidateRegionIds);
        Assert.Equal(["A", "B", "C"], reconstructed.Conditions[2].OuterCandidateRegionIds);
        Assert.Equal(["A", "B"], reconstructed.Conditions[3].OuterCandidateRegionIds);

        // Snapshot of public simulator work counters from the fixed two-weft world.
        // Keep separate from wall-clock runtime and from actual rescue outcomes.
        var metricsSnapshot = string.Join("\n", result.Methods.SelectMany(method =>
            method.Conditions.Select(row =>
                $"FP-WEFT86 {method.Method} {row.Label}: " +
                $"usedGroups={row.UsedSourceGroupCount}, " +
                $"generation={row.GenerationSteps}, verification={row.VerificationSteps}, " +
                $"forward={row.ForwardTransitionSteps}, backward={row.BackwardTransitionSteps}, " +
                $"evidenceInspected={row.EvidenceRecordsInspected}, " +
                $"relationsInspected={row.PublicRelationsInspected}")));
        const string expectedMetricsSnapshot = """
            FP-WEFT86 distributed-reconstruction neither: usedGroups=0, generation=4, verification=0, forward=0, backward=0, evidenceInspected=0, relationsInspected=0
            FP-WEFT86 distributed-reconstruction first-only: usedGroups=1, generation=4, verification=0, forward=0, backward=0, evidenceInspected=1, relationsInspected=1
            FP-WEFT86 distributed-reconstruction second-only: usedGroups=0, generation=4, verification=0, forward=0, backward=0, evidenceInspected=0, relationsInspected=0
            FP-WEFT86 distributed-reconstruction both: usedGroups=1, generation=4, verification=0, forward=0, backward=0, evidenceInspected=1, relationsInspected=1
            FP-WEFT86 distributed-reconstruction-local neither: usedGroups=0, generation=4, verification=0, forward=1, backward=1, evidenceInspected=0, relationsInspected=2
            FP-WEFT86 distributed-reconstruction-local first-only: usedGroups=1, generation=4, verification=0, forward=2, backward=2, evidenceInspected=0, relationsInspected=4
            FP-WEFT86 distributed-reconstruction-local second-only: usedGroups=1, generation=4, verification=0, forward=2, backward=2, evidenceInspected=1, relationsInspected=5
            FP-WEFT86 distributed-reconstruction-local both: usedGroups=2, generation=4, verification=0, forward=2, backward=2, evidenceInspected=1, relationsInspected=5
            FP-WEFT86 known-relation-backward neither: usedGroups=0, generation=0, verification=0, forward=1, backward=1, evidenceInspected=0, relationsInspected=2
            FP-WEFT86 known-relation-backward first-only: usedGroups=0, generation=0, verification=0, forward=1, backward=1, evidenceInspected=0, relationsInspected=2
            FP-WEFT86 known-relation-backward second-only: usedGroups=1, generation=0, verification=0, forward=2, backward=2, evidenceInspected=1, relationsInspected=5
            FP-WEFT86 known-relation-backward both: usedGroups=1, generation=0, verification=0, forward=2, backward=2, evidenceInspected=1, relationsInspected=5
            FP-WEFT86 public-prior neither: usedGroups=0, generation=0, verification=0, forward=0, backward=0, evidenceInspected=0, relationsInspected=0
            FP-WEFT86 public-prior first-only: usedGroups=0, generation=0, verification=0, forward=0, backward=0, evidenceInspected=0, relationsInspected=0
            FP-WEFT86 public-prior second-only: usedGroups=0, generation=0, verification=0, forward=0, backward=0, evidenceInspected=0, relationsInspected=0
            FP-WEFT86 public-prior both: usedGroups=0, generation=0, verification=0, forward=0, backward=0, evidenceInspected=0, relationsInspected=0
            """;
        Assert.Equal(expectedMetricsSnapshot, metricsSnapshot);

        var prior = result.Methods.Single(method => method.Method == "public-prior");
        Assert.False(prior.BothStrictlyNarrowerThanEachSingle);
        Assert.All(prior.Conditions, row =>
        {
            Assert.Empty(row.GenerationDirectSourceIds);
            Assert.Empty(row.TraversalDirectSourceIds);
            Assert.Equal(0, row.UsedSourceGroupCount);
        });
        Assert.Equal(JsonDefaults.Serialize(result),
            JsonDefaults.Serialize(RescuerEvidenceMethodComparison.AnalyzePairedContributions(
                ComplementaryRun(reversed: true))));
        Assert.Equal(JsonDefaults.Serialize(report), JsonDefaults.Serialize(ComplementaryRun(reversed: true)));
    }

    [Fact]
    public void ComplementaryWeftComparisonMarksAbsentMethodResultsUnavailable()
    {
        var report = ComplementaryRun();
        var missingAll = report with
        {
            SameCutoffAblations = report.SameCutoffAblations
                .Select(row => row with { Methods = [] }).ToArray()
        };
        var noMethods = RescuerEvidenceMethodComparison.AnalyzePairedContributions(missingAll);
        Assert.Equal("unavailable", noMethods.Status);
        Assert.Equal(4, noMethods.Methods.Count);
        Assert.All(noMethods.Methods, method =>
        {
            Assert.Equal("unavailable", method.Status);
            Assert.Empty(method.Conditions);
            Assert.Null(method.CandidateReductionFromFirst);
        });

        var absentEverywhere = report with
        {
            SameCutoffAblations = report.SameCutoffAblations
                .Select(row => row with
                {
                    Methods = row.Methods.Where(method =>
                        method.Method != "known-relation-backward").ToArray()
                }).ToArray()
        };
        var missingMethod = RescuerEvidenceMethodComparison.AnalyzePairedContributions(
            absentEverywhere);
        Assert.Equal("partial", missingMethod.Status);
        Assert.Equal("unavailable", missingMethod.Methods.Single(method =>
            method.Method == "known-relation-backward").Status);
        Assert.Equal(3, missingMethod.Methods.Count(method =>
            method.Status == "available"));

        var missingOne = report with
        {
            SameCutoffAblations = report.SameCutoffAblations
                .Select((row, index) => index == 2 ? row with
                {
                    Methods = row.Methods.Where(method =>
                        method.Method != "distributed-reconstruction-local").ToArray()
                } : row).ToArray()
        };
        var partial = RescuerEvidenceMethodComparison.AnalyzePairedContributions(missingOne);
        Assert.Equal("partial", partial.Status);
        Assert.Equal("unavailable", partial.Methods.Single(method =>
            method.Method == "distributed-reconstruction-local").Status);
        Assert.Null(partial.Methods.Single(method =>
            method.Method == "distributed-reconstruction-local").BothStrictlyNarrowerThanEachSingle);
    }

    [Fact]
    public void ComplementaryWeftComparisonRetainsSharedAncestryAndUncertainty()
    {
        var unavailable = RescuerEvidenceMethodComparison.AnalyzePairedContributions(
            ComplementaryRun() with { SameCutoffAblations = [] });
        Assert.Equal("unavailable", unavailable.Status);
        Assert.Null(unavailable.QueryTick);
        Assert.Empty(unavailable.Methods);

        var shared = RescuerEvidenceMethodComparison.AnalyzePairedContributions(
            ComplementaryRun(secondAncestry: "weft-a"));
        Assert.Equal("available", shared.Status);
        Assert.All(shared.Methods, item =>
            Assert.Equal(1, item.Conditions[3].AvailableSourceGroupCount));
        Assert.Equal(1, shared.Methods.Single(item =>
            item.Method == "distributed-reconstruction-local")
            .Conditions[3].UsedSourceGroupCount);
        foreach (var state in new[]
            { EvidenceState.SupportedButUncertain, EvidenceState.Conflicting })
        {
            var analysis = RescuerEvidenceMethodComparison.AnalyzePairedContributions(
                ComplementaryRun(firstState: state));
            Assert.Equal("available", analysis.Status);
            var method = analysis.Methods.Single(item =>
                item.Method == "distributed-reconstruction");
            Assert.NotNull(method.Conditions[3].UncertainCandidateRegionIds);
            Assert.NotNull(method.Conditions[3].ConflictedCandidateRegionIds);
        }
        var device = RescuerEvidenceMethodComparison.AnalyzePairedContributions(
            ComplementaryRun(deviceOnly: true));
        Assert.Equal("available", device.Status);
        Assert.Empty(device.Methods.Single(item =>
            item.Method == "distributed-reconstruction").Conditions[3]
            .GenerationDirectSourceIds);
    }

    private static (RescuerEvidenceMethodReport Early, RescuerEvidenceMethodReport Late)
        ComplementaryTimingRuns(
            bool reverse = false,
            string ancestry = "weft-b",
            EvidenceState firstState = EvidenceState.Confirmed,
            bool deviceOnly = false)
    {
        var context = ComplementaryPublicContext();
        var first = ComplementaryOriginal(
            RecordId, HolderId, 2, ["A", "B"], 3, "weft-a", firstState, deviceOnly);
        var second = ComplementaryOriginal(
            "second-original", "holder-b", 5, ["E", "F"], 6, ancestry);
        var originals = reverse ? new[] { second, first } : new[] { first, second };
        var receipt1 = Receipt(6);
        var early2 = LaterComparisonReceipt(7);
        var late2 = LaterComparisonReceipt(8);
        // Same exact objects/world/observations; only second receipt tick differs.
        Assert.Equal(early2.OriginalRecordId, late2.OriginalRecordId);
        Assert.Equal(early2.HolderDeviceId, late2.HolderDeviceId);
        Assert.Equal(early2.RescuerId, late2.RescuerId);
        Assert.Equal(7, early2.ReceivedAtTick);
        Assert.Equal(8, late2.ReceivedAtTick);
        var earlyReceipts = reverse
            ? new[] { early2, receipt1, receipt1 }
            : new[] { receipt1, early2 };
        var lateReceipts = reverse
            ? new[] { late2, receipt1, receipt1 }
            : new[] { receipt1, late2 };
        RescuerEvidenceMethodReport Run(InteractionRecordReceipt[] receipts) =>
            RescuerEvidenceMethodComparison.Run(
                context, Seed, RescuerId, originals, receipts,
                InteractionTimeWindow.At(2), reverse ? [8, 7, 6, 5] : [5, 6, 7, 8], 8);
        return (Run(earlyReceipts), Run(lateReceipts));
    }

    [Fact]
    public void ComplementaryReceiptTimingSeparatesTemporaryLossFromFinalConvergence()
    {
        var (early, late) = ComplementaryTimingRuns();
        var comparison = RescuerEvidenceMethodComparison.AnalyzeReceiptDecisionTiming(early, late);
        Assert.Equal("available", comparison.Status);
        Assert.Equal([5L, 6L, 7L, 8L], comparison.QueryTicks);
        Assert.Equal(4, comparison.Methods.Count);
        Assert.Equal([0, 1, 2, 2], early.ActualTimeline.Select(row => row.ReceivedOriginalCount));
        Assert.Equal([0, 1, 1, 2], late.ActualTimeline.Select(row => row.ReceivedOriginalCount));
        Assert.Equal(JsonDefaults.Serialize(early.ActualTimeline[0]),
            JsonDefaults.Serialize(late.ActualTimeline[0]));
        Assert.Equal(JsonDefaults.Serialize(early.ActualTimeline[1]),
            JsonDefaults.Serialize(late.ActualTimeline[1]));
        Assert.DoesNotContain("second-original", late.ActualTimeline[2].ReceivedOriginalIds);
        Assert.Equal(JsonDefaults.Serialize(early.ActualTimeline[3]),
            JsonDefaults.Serialize(late.ActualTimeline[3]));
        Assert.Equal(JsonDefaults.Serialize(early.SameCutoffAblations),
            JsonDefaults.Serialize(late.SameCutoffAblations));

        var prior = comparison.Methods.Single(row => row.Method == "public-prior");
        Assert.Null(prior.FirstCandidateDifferentTick);
        Assert.All(prior.Timeline, row => Assert.True(row.SameOuterCandidates));
        var generation = comparison.Methods.Single(row =>
            row.Method == "distributed-reconstruction");
        Assert.Null(generation.FirstCandidateDifferentTick);
        var local = comparison.Methods.Single(row =>
            row.Method == "distributed-reconstruction-local");
        Assert.Equal(7, local.FirstCandidateDifferentTick);
        Assert.Equal(8, local.FirstCandidateConvergenceTick);
        Assert.Equal(7, local.FirstMethodResultDifferentTick);
        Assert.Equal(8, local.FirstMethodResultConvergenceTick);
        var tick7 = local.Timeline.Single(row => row.QueryTick == 7);
        Assert.Equal(["B"], tick7.EarlyMethod!.OuterCandidateRegionIds);
        Assert.Equal(["A", "B"], tick7.LateMethod!.OuterCandidateRegionIds);
        Assert.Equal(["A"], tick7.LateOnlyCandidateRegionIds);
        Assert.Empty(tick7.EarlyOnlyCandidateRegionIds!);
        Assert.Equal(-1, tick7.EarlyMinusLateCandidateCount);
        Assert.True(tick7.EarlyCandidatesSubsetOfLate);
        Assert.False(tick7.LateCandidatesSubsetOfEarly);
        Assert.Equal([RecordId, "second-original"], tick7.EarlyReceivedOriginalIds);
        Assert.Equal([RecordId], tick7.LateReceivedOriginalIds);
        Assert.Equal(2, tick7.EarlyAvailableSourceGroupCount);
        Assert.Equal(1, tick7.LateAvailableSourceGroupCount);
        Assert.Equal(2, tick7.EarlyMethod.UsedSourceGroupCount);
        Assert.Equal(1, tick7.LateMethod.UsedSourceGroupCount);
        Assert.Equal([RecordId], tick7.EarlyMethod.GenerationDirectSourceIds);
        Assert.Equal(["second-original"], tick7.EarlyMethod.TraversalDirectSourceIds);
        Assert.Empty(tick7.LateMethod.TraversalDirectSourceIds);
        Assert.Equal(4, tick7.EarlyMethod.GenerationSteps);
        Assert.Equal(2, tick7.EarlyMethod.ForwardTransitionSteps);
        Assert.Equal(2, tick7.EarlyMethod.BackwardTransitionSteps);
        Assert.Equal(1, tick7.EarlyMethod.EvidenceRecordsInspected);
        Assert.Equal(5, tick7.EarlyMethod.PublicRelationsInspected);
        Assert.Equal(0, tick7.LateMethod.EvidenceRecordsInspected);
        Assert.Equal(4, tick7.LateMethod.PublicRelationsInspected);
        var backward = comparison.Methods.Single(row =>
            row.Method == "known-relation-backward");
        Assert.Equal(["B", "C"], backward.Timeline[2].EarlyMethod!.OuterCandidateRegionIds);
        Assert.Equal(["A", "B", "C"], backward.Timeline[2].LateMethod!.OuterCandidateRegionIds);
        Assert.Equal(7, backward.FirstCandidateDifferentTick);
        Assert.Equal(8, backward.FirstCandidateConvergenceTick);
        Assert.All(comparison.Methods, item =>
            Assert.True(item.Timeline[3].SameMethodResult));
        Assert.DoesNotContain("SyntheticTruth", JsonDefaults.Serialize(comparison));
    }

    [Fact]
    public void ComplementaryReceiptTimingIsDeterministicAndPreservesAncestryAndUncertainty()
    {
        var (a, b) = ComplementaryTimingRuns();
        var (reversedA, reversedB) = ComplementaryTimingRuns(reverse: true);
        Assert.Equal(JsonDefaults.Serialize(
                RescuerEvidenceMethodComparison.AnalyzeReceiptDecisionTiming(a, b)),
            JsonDefaults.Serialize(
                RescuerEvidenceMethodComparison.AnalyzeReceiptDecisionTiming(
                    reversedA, reversedB)));
        var (sharedEarly, sharedLate) = ComplementaryTimingRuns(ancestry: "weft-a");
        var shared = RescuerEvidenceMethodComparison.AnalyzeReceiptDecisionTiming(
            sharedEarly, sharedLate);
        var sharedLocal = shared.Methods.Single(row =>
            row.Method == "distributed-reconstruction-local").Timeline[2];
        Assert.Equal(1, sharedLocal.EarlyAvailableSourceGroupCount);
        Assert.Equal(1, sharedLocal.EarlyMethod!.UsedSourceGroupCount);
        foreach (var state in new[] { EvidenceState.SupportedButUncertain, EvidenceState.Conflicting })
        {
            var (uncertainEarly, uncertainLate) = ComplementaryTimingRuns(firstState: state);
            var result = RescuerEvidenceMethodComparison.AnalyzeReceiptDecisionTiming(
                uncertainEarly, uncertainLate);
            Assert.Equal("available", result.Status);
            var row = result.Methods.Single(item =>
                item.Method == "distributed-reconstruction").Timeline[3];
            Assert.NotNull(row.EarlyMethod!.UncertainCandidateRegionIds);
            Assert.NotNull(row.EarlyMethod.ConflictedCandidateRegionIds);
        }
        var (deviceEarly, deviceLate) = ComplementaryTimingRuns(deviceOnly: true);
        var device = RescuerEvidenceMethodComparison.AnalyzeReceiptDecisionTiming(
            deviceEarly, deviceLate);
        Assert.Empty(device.Methods.Single(item =>
            item.Method == "distributed-reconstruction").Timeline[2]
            .EarlyMethod!.GenerationDirectSourceIds);
    }

    [Fact]
    public void ComplementaryReceiptTimingMarksMissingAndIncompatibleRowsUnavailable()
    {
        var (early, late) = ComplementaryTimingRuns();
        var tick7 = late.ActualTimeline[2];
        var missing = late with
        {
            ActualTimeline = late.ActualTimeline.Select((row, index) =>
                index == 2 ? row with
                {
                    Methods = row.Methods.Where(method =>
                        method.Method != "distributed-reconstruction-local").ToArray()
                } : row).ToArray()
        };
        var result = RescuerEvidenceMethodComparison.AnalyzeReceiptDecisionTiming(
            early, missing);
        Assert.Equal("partial", result.Status);
        var local = result.Methods.Single(method =>
            method.Method == "distributed-reconstruction-local");
        Assert.Equal("partial", local.Status);
        Assert.Equal("unavailable", local.Timeline[2].Status);
        Assert.Null(local.Timeline[2].EarlyMinusLateCandidateCount);
        Assert.Null(local.Timeline[2].SameOuterCandidates);
        Assert.Null(local.Timeline[2].EarlyMethod);
        Assert.Null(local.Timeline[2].LateMethod);
        Assert.Equal("unavailable",
            RescuerEvidenceMethodComparison.AnalyzeReceiptDecisionTiming(
                early, late with { Seed = late.Seed + 1 }).Status);
        Assert.Equal("unavailable",
            RescuerEvidenceMethodComparison.AnalyzeReceiptDecisionTiming(
                early, late with
                {
                    ActualTimeline = late.ActualTimeline
                        .Where(row => row.QueryTick != 7).ToArray()
                }).Status);
        var emptySet = tick7 with
        {
            Methods = tick7.Methods.Select(item =>
                item.Method == "public-prior"
                    ? item with { OuterCandidateRegionIds = [], CandidateCount = 0 }
                    : item).ToArray()
        };
        var withEmpty = late with
        {
            ActualTimeline = late.ActualTimeline.Select((row, index) =>
                index == 2 ? emptySet : row).ToArray()
        };
        var zero = RescuerEvidenceMethodComparison.AnalyzeReceiptDecisionTiming(
            early, withEmpty).Methods.Single(row => row.Method == "public-prior").Timeline[2];
        Assert.Equal("available", zero.Status);
        Assert.Equal(0, zero.LateMethod!.CandidateCount);
        Assert.Equal(3, zero.EarlyMethod!.CandidateCount);
    }

    // #92: preserve both original objects/world; vary only second receipt tick.
    private static (RescuerEvidenceMethodReport Early, RescuerEvidenceMethodReport Late)
        ContradictoryRevisionRuns(
            bool reversed = false,
            string secondAncestry = "other-upstream",
            EvidenceState firstState = EvidenceState.Confirmed,
            bool deviceOnly = false)
    {
        var context = MultiHolderContext();
        var first = Record(state: firstState, deviceOnly: deviceOnly);
        var second = DistinctRecord(["B"], ancestry: secondAncestry);
        var originals = reversed ? new[] { second, first } : new[] { first, second };
        var firstReceipt = Receipt(6);
        var earlySecond = DistinctReceipt(8);
        var lateSecond = DistinctReceipt(9);
        Assert.Equal(earlySecond.OriginalRecordId, lateSecond.OriginalRecordId);
        Assert.Equal(earlySecond.HolderDeviceId, lateSecond.HolderDeviceId);
        Assert.Equal(earlySecond.RescuerId, lateSecond.RescuerId);
        Assert.Equal(8, earlySecond.ReceivedAtTick);
        Assert.Equal(9, lateSecond.ReceivedAtTick);
        var early = reversed ? new[] { earlySecond, firstReceipt, firstReceipt } :
            new[] { firstReceipt, earlySecond };
        var late = reversed ? new[] { lateSecond, firstReceipt, firstReceipt } :
            new[] { firstReceipt, lateSecond };
        RescuerEvidenceMethodReport Run(InteractionRecordReceipt[] ledger) =>
            RescuerEvidenceMethodComparison.Run(context, Seed, RescuerId, originals, ledger,
                InteractionTimeWindow.At(2),
                reversed ? [9, 8, 7, 6, 5] : [5, 6, 7, 8, 9], 9);
        return (Run(early), Run(late));
    }

    [Fact]
    public void ContradictoryWeftsCompareSequentialActualDecisionsSeparateFromSameTickControl()
    {
        var (early, late) = ContradictoryRevisionRuns();
        var revised = RescuerEvidenceMethodComparison.AnalyzeDecisionRevision(early);
        var delayed = RescuerEvidenceMethodComparison.AnalyzeDecisionRevision(late);
        var sameTick = RescuerEvidenceMethodComparison.AnalyzeReceiptDecisionTiming(early, late);
        Assert.Equal("available", revised.Status);
        Assert.Equal("available", delayed.Status);
        Assert.Equal("available", sameTick.Status);
        Assert.Equal(InteractionTimeWindow.At(2), revised.TargetWindow);
        Assert.Equal([5L, 6L, 7L, 8L, 9L], revised.QueryTicks);
        Assert.Equal([0, 1, 1, 2, 2],
            early.ActualTimeline.Select(x => x.ReceivedOriginalCount));
        Assert.Equal([0, 1, 1, 1, 2],
            late.ActualTimeline.Select(x => x.ReceivedOriginalCount));
        Assert.Equal(4, revised.Methods.Count);
        Assert.All(revised.Methods, method =>
        {
            Assert.Equal("available", method.Status);
            Assert.Equal(4, method.Timeline.Count);
            Assert.All(method.Timeline, change =>
            {
                Assert.True(change.BeforeQueryTick < change.AfterQueryTick);
                Assert.Equal(change.BeforeMethod!.OuterCandidateRegionIds.Except(
                    change.AfterMethod!.OuterCandidateRegionIds, StringComparer.Ordinal)
                    .OrderBy(id => id, StringComparer.Ordinal), change.ExcludedCandidateRegionIds);
                Assert.Equal(change.AfterMethod.OuterCandidateRegionIds.Except(
                    change.BeforeMethod.OuterCandidateRegionIds, StringComparer.Ordinal)
                    .OrderBy(id => id, StringComparer.Ordinal), change.ReappearedCandidateRegionIds);
                Assert.Equal(change.AfterMethod.CandidateCount - change.BeforeMethod.CandidateCount,
                    change.AfterMinusBeforeCandidateCount);
            });
        });
        var change8 = revised.Methods.Single(x => x.Method == "distributed-reconstruction")
            .Timeline.Single(x => x.AfterQueryTick == 8);
        Assert.Equal([RecordId], change8.BeforeReceivedOriginalIds);
        Assert.Equal([RecordId, "second-original"], change8.AfterReceivedOriginalIds);
        Assert.Equal(1, change8.BeforeAvailableSourceGroupCount);
        Assert.Equal(2, change8.AfterAvailableSourceGroupCount);
        // Pinned observations from the real fixed-world Run at past target tick 2.
        Assert.Equal(["A"], change8.BeforeMethod!.OuterCandidateRegionIds);
        Assert.Equal(["A", "B"], change8.AfterMethod!.OuterCandidateRegionIds);
        Assert.Equal(["B"], change8.ReappearedCandidateRegionIds);
        Assert.Empty(change8.ExcludedCandidateRegionIds!);
        Assert.Equal(1, change8.AfterMinusBeforeCandidateCount);
        Assert.True(change8.BeforeCandidatesSubsetOfAfter);
        Assert.False(change8.AfterCandidatesSubsetOfBefore);
        Assert.Equal(["A", "B"], change8.AfterMethod.ConflictedCandidateRegionIds);
        Assert.Empty(change8.BeforeMethod.ConflictedCandidateRegionIds!);
        Assert.Equal(3, change8.BeforeMethod.GenerationSteps);
        Assert.Equal(5, change8.AfterMethod.GenerationSteps);
        Assert.Equal(1, change8.BeforeMethod.EvidenceRecordsInspected);
        Assert.Equal(2, change8.AfterMethod.EvidenceRecordsInspected);
        Assert.Equal([RecordId, "second-original"],
            change8.AfterMethod.GenerationDirectSourceIds);
        Assert.Equal(8, revised.Methods.Single(x =>
            x.Method == "distributed-reconstruction").FirstReappearedTick);
        Assert.Equal(9, delayed.Methods.Single(x =>
            x.Method == "distributed-reconstruction").FirstReappearedTick);
        var localChange = revised.Methods.Single(x =>
            x.Method == "distributed-reconstruction-local").Timeline.Single(x =>
                x.AfterQueryTick == 8);
        Assert.Equal(["A"], localChange.BeforeMethod!.OuterCandidateRegionIds);
        Assert.Equal(["A", "B"], localChange.AfterMethod!.OuterCandidateRegionIds);
        Assert.Equal(["B"], localChange.ReappearedCandidateRegionIds);
        Assert.Equal(["A", "B"], localChange.AfterMethod.ConflictedCandidateRegionIds);
        Assert.Equal(3, localChange.BeforeMethod.GenerationSteps);
        Assert.Equal(5, localChange.AfterMethod.GenerationSteps);
        Assert.Equal(2, localChange.BeforeMethod.ForwardTransitionSteps);
        Assert.Equal(1, localChange.AfterMethod.ForwardTransitionSteps);
        Assert.Equal(2, localChange.BeforeMethod.BackwardTransitionSteps);
        Assert.Equal(1, localChange.AfterMethod.BackwardTransitionSteps);
        Assert.All(revised.Methods.Where(x => x.Method is
            "public-prior" or "known-relation-backward"),
            method => Assert.Null(method.FirstReappearedTick));

        Assert.DoesNotContain("second-original", change8.BeforeMethod!.GenerationDirectSourceIds);
        Assert.DoesNotContain("second-original", change8.BeforeMethod.TraversalDirectSourceIds);
        Assert.Null(change8.SamePreferredCandidates);
        Assert.NotNull(change8.SameUncertainCandidates);
        Assert.NotNull(change8.SameConflictedCandidates);
        var late8 = delayed.Methods.Single(x => x.Method == "distributed-reconstruction")
            .Timeline.Single(x => x.AfterQueryTick == 8);
        Assert.Equal([RecordId], late8.AfterReceivedOriginalIds);
        Assert.NotEqual(JsonDefaults.Serialize(early.ActualTimeline[3]),
            JsonDefaults.Serialize(late.ActualTimeline[3]));
        Assert.Equal(JsonDefaults.Serialize(early.ActualTimeline[4]),
            JsonDefaults.Serialize(late.ActualTimeline[4]));
        Assert.Equal(2, sameTick.Methods.Single(x =>
            x.Method == "distributed-reconstruction").Timeline[3].EarlyReceivedOriginalIds.Count);
        Assert.Single(sameTick.Methods.Single(x =>
            x.Method == "distributed-reconstruction").Timeline[3].LateReceivedOriginalIds);
        Assert.DoesNotContain("SyntheticTruth", JsonDefaults.Serialize(revised));
        Assert.NotEmpty(RescuerEvidenceMethodComparison.EvaluateSyntheticTruth(early, "B").Evaluations);
    }

    [Fact]
    public void ContradictoryWeftsReopenOuterInExistingTracingWithoutAssumingM1EAgreement()
    {
        var context = MultiHolderContext();
        var originals = new[] { Record(), DistinctRecord(["B"]) };
        var before = new RescuerEvidenceInputBuilder().BuildDistinctOriginals(
            context, Seed, 8, RescuerId, originals, [Receipt(6)]);
        var after = new RescuerEvidenceInputBuilder().BuildDistinctOriginals(
            context, Seed, 8, RescuerId, originals, [Receipt(6), DistinctReceipt(8)]);
        var narrow = new MultiWeftTracingStrategy().Evaluate(before.Input);
        var conflicting = new MultiWeftTracingStrategy().Evaluate(after.Input);
        Assert.Equal(["C"], narrow.OuterPlausibleCandidateRegionIds);
        Assert.Equal(["C", "D"], conflicting.OuterPlausibleCandidateRegionIds);
        Assert.Equal(2, conflicting.CausalTracingResult!.SourceGroups.Count);
        Assert.Contains(conflicting.CausalTracingResult.Steps, step =>
            step.Disposition == TracingStepDisposition.MarkConflict && step.ConflictDetected);
        Assert.DoesNotContain("second-original", JsonDefaults.Serialize(before));
    }

    [Fact]
    public void DecisionRevisionPreservesAncestryOrderingUncertaintyAndDeviceBoundary()
    {
        var (early, late) = ContradictoryRevisionRuns();
        var (ordered, delayed) = ContradictoryRevisionRuns(reversed: true);
        Assert.Equal(JsonDefaults.Serialize(early), JsonDefaults.Serialize(ordered));
        Assert.Equal(JsonDefaults.Serialize(late), JsonDefaults.Serialize(delayed));
        Assert.Equal(JsonDefaults.Serialize(
            RescuerEvidenceMethodComparison.AnalyzeDecisionRevision(early)),
            JsonDefaults.Serialize(
                RescuerEvidenceMethodComparison.AnalyzeDecisionRevision(ordered)));
        var (shared, _) = ContradictoryRevisionRuns(secondAncestry: "upstream-evidence");
        var sharedChange = RescuerEvidenceMethodComparison.AnalyzeDecisionRevision(shared)
            .Methods.Single(x => x.Method == "distributed-reconstruction")
            .Timeline.Single(x => x.AfterQueryTick == 8);
        Assert.Equal(1, sharedChange.AfterAvailableSourceGroupCount);
        foreach (var state in new[] { EvidenceState.SupportedButUncertain, EvidenceState.Conflicting })
        {
            var (soft, _) = ContradictoryRevisionRuns(firstState: state);
            Assert.Equal("available",
                RescuerEvidenceMethodComparison.AnalyzeDecisionRevision(soft).Status);
        }
        var (device, _) = ContradictoryRevisionRuns(deviceOnly: true);
        var deviceChange = RescuerEvidenceMethodComparison.AnalyzeDecisionRevision(device)
            .Methods.Single(x => x.Method == "distributed-reconstruction")
            .Timeline.Single(x => x.AfterQueryTick == 6);
        Assert.Empty(deviceChange.AfterMethod!.GenerationDirectSourceIds);
    }

    [Fact]
    public void DecisionRevisionDistinguishesMissingAmbiguousAndRealEmptyCandidateSet()
    {
        var (early, _) = ContradictoryRevisionRuns();
        var absent = early with
        {
            ActualTimeline = early.ActualTimeline.Select((row, index) => index == 3
                ? row with
                {
                    Methods = row.Methods.Where(x =>
                        x.Method != "distributed-reconstruction-local").ToArray()
                }
                : row).ToArray()
        };
        var partial = RescuerEvidenceMethodComparison.AnalyzeDecisionRevision(absent);
        Assert.Equal("partial", partial.Status);
        var missing = partial.Methods.Single(x =>
            x.Method == "distributed-reconstruction-local").Timeline.Single(x =>
                x.AfterQueryTick == 8);
        Assert.Equal("unavailable", missing.Status);
        Assert.Null(missing.AfterMinusBeforeCandidateCount);
        Assert.Null(missing.AfterMethod);
        Assert.Null(missing.ReappearedCandidateRegionIds);
        var tick8 = early.ActualTimeline[3];
        var explicitEmpty = early with
        {
            ActualTimeline = early.ActualTimeline.Select((row, index) => index == 3
                ? row with
                {
                    Methods = row.Methods.Select(x => x.Method == "public-prior"
                        ? x with { OuterCandidateRegionIds = [], CandidateCount = 0 }
                        : x).ToArray()
                } : row).ToArray()
        };
        var emptyStep = RescuerEvidenceMethodComparison.AnalyzeDecisionRevision(explicitEmpty)
            .Methods.Single(x => x.Method == "public-prior").Timeline.Single(x =>
                x.AfterQueryTick == 8);
        Assert.Equal("available", emptyStep.Status);
        Assert.Empty(emptyStep.AfterMethod!.OuterCandidateRegionIds);
        Assert.NotNull(emptyStep.ExcludedCandidateRegionIds);
        Assert.Equal("unavailable",
            RescuerEvidenceMethodComparison.AnalyzeDecisionRevision(early with
            { ActualTimeline = [.. early.ActualTimeline, tick8] }).Status);
        Assert.Equal("unavailable",
            RescuerEvidenceMethodComparison.AnalyzeDecisionRevision(early with
            { ActualTimeline = early.SameCutoffAblations }).Status);
    }


    // #94: One synthetic world and the SAME two originals / receipts / ticks.
    // Null means no public transition declaration was supplied to the rescuer.
    private static (RescuerEvidenceMethodReport Known, RescuerEvidenceMethodReport Unprovided)
        PublicRelationAvailabilityRuns(
            bool reversed = false,
            string secondAncestry = "weft-b",
            EvidenceState firstState = EvidenceState.Confirmed,
            bool deviceOnly = false)
    {
        var context = ComplementaryPublicContext();
        var unprovided = context with { PublicHazardEnvelope = null };
        Assert.NotNull(context.PublicHazardEnvelope);
        Assert.Null(unprovided.PublicHazardEnvelope);
        Assert.Equal(JsonDefaults.Serialize(context.PublicCausalTracingModel),
            JsonDefaults.Serialize(unprovided.PublicCausalTracingModel));
        Assert.Equal(JsonDefaults.Serialize(context.PublicRegions),
            JsonDefaults.Serialize(unprovided.PublicRegions));
        var first = ComplementaryOriginal(
            RecordId, HolderId, 2, ["A", "B"], 3, "weft-a", firstState, deviceOnly);
        var second = ComplementaryOriginal(
            "second-original", "holder-b", 5, ["E", "F"], 6, secondAncestry);
        var originals = reversed ? new[] { second, first } : new[] { first, second };
        var receipts = reversed
            ? new[] { LaterComparisonReceipt(7), Receipt(6), Receipt(6) }
            : new[] { Receipt(6), LaterComparisonReceipt(7) };
        RescuerEvidenceMethodReport Run(RescueContext publicContext) =>
            RescuerEvidenceMethodComparison.Run(
                publicContext, Seed, RescuerId, originals, receipts,
                InteractionTimeWindow.At(2), reversed ? [7, 6, 5] : [5, 6, 7], 7);
        return (Run(context), Run(unprovided));
    }

    [Fact]
    public void MissingPublicTransitionsCompareTheSameReceivedWeftsWithoutClosingUnknownPaths()
    {
        var (known, unprovided) = PublicRelationAvailabilityRuns();
        var comparison = RescuerEvidenceMethodComparison.AnalyzePublicRelationAvailability(
            known, unprovided);
        Assert.Equal("available", comparison.Status);
        Assert.Equal([5L, 6L, 7L], comparison.QueryTicks);
        Assert.Equal(InteractionTimeWindow.At(2), comparison.TargetWindow);
        Assert.Equal(4, comparison.Methods.Count);
        Assert.Equal([0, 1, 2], known.ActualTimeline.Select(row => row.ReceivedOriginalCount));
        Assert.Equal([0, 1, 2], unprovided.ActualTimeline.Select(row => row.ReceivedOriginalCount));
        Assert.All(comparison.Methods, method =>
        {
            Assert.Equal("available", method.Status);
            Assert.Equal(3, method.Timeline.Count);
            Assert.All(method.Timeline, tick =>
            {
                Assert.Equal("available", tick.Status);
                Assert.NotNull(tick.KnownRelationMethod);
                Assert.NotNull(tick.UnprovidedRelationMethod);
                Assert.Equal(tick.ReceivedOriginalCount, tick.ReceivedOriginalIds.Count);
                Assert.Equal(tick.KnownRelationMethod!.CandidateCount -
                    tick.UnprovidedRelationMethod!.CandidateCount,
                    tick.KnownMinusUnprovidedCandidateCount);
                Assert.Equal(tick.KnownRelationMethod.OuterCandidateRegionIds.Except(
                    tick.UnprovidedRelationMethod.OuterCandidateRegionIds, StringComparer.Ordinal)
                    .OrderBy(id => id, StringComparer.Ordinal), tick.KnownOnlyCandidateRegionIds);
                Assert.Equal(tick.UnprovidedRelationMethod.OuterCandidateRegionIds.Except(
                    tick.KnownRelationMethod.OuterCandidateRegionIds, StringComparer.Ordinal)
                    .OrderBy(id => id, StringComparer.Ordinal), tick.UnprovidedOnlyCandidateRegionIds);
                Assert.Equal(tick.KnownOnlyCandidateRegionIds!.Count == 0,
                    tick.KnownCandidatesSubsetOfUnprovided);
                Assert.Equal(tick.UnprovidedOnlyCandidateRegionIds!.Count == 0,
                    tick.UnprovidedCandidatesSubsetOfKnown);
                Assert.True(tick.KnownRelationMethod.AvailableSourceGroupCount >=
                    tick.KnownRelationMethod.UsedSourceGroupCount);
                Assert.True(tick.UnprovidedRelationMethod.AvailableSourceGroupCount >=
                    tick.UnprovidedRelationMethod.UsedSourceGroupCount);
            });
        });
        var prior = comparison.Methods.Single(m => m.Method == "public-prior");
        Assert.All(prior.Timeline, tick =>
        {
            Assert.True(tick.SameOuterCandidates);
            Assert.Equal(["A", "B", "C"], tick.KnownRelationMethod!.OuterCandidateRegionIds);
            Assert.Equal(["A", "B", "C"], tick.UnprovidedRelationMethod!.OuterCandidateRegionIds);
            Assert.Empty(tick.KnownRelationMethod.GenerationDirectSourceIds);
            Assert.Empty(tick.UnprovidedRelationMethod.TraversalDirectSourceIds);
            Assert.Null(tick.SameConflictedCandidates);
        });
        var final = comparison.Methods.Single(m =>
            m.Method == "distributed-reconstruction-local").Timeline[2];
        Assert.Equal([RecordId, "second-original"], final.ReceivedOriginalIds);
        Assert.Equal(2, final.AvailableSourceGroupCount);
        Assert.Equal(["A", "B"], final.KnownRelationMethod!.OuterCandidateRegionIds);
        Assert.NotNull(final.UnprovidedRelationMethod!.UncertainCandidateRegionIds);
        Assert.Equal(4, final.KnownRelationMethod.GenerationSteps);
        Assert.DoesNotContain("SyntheticTruth", JsonDefaults.Serialize(comparison));
        Assert.Equal(JsonDefaults.Serialize(comparison), JsonDefaults.Serialize(
            RescuerEvidenceMethodComparison.AnalyzePublicRelationAvailability(
                PublicRelationAvailabilityRuns(reversed: true).Known,
                PublicRelationAvailabilityRuns(reversed: true).Unprovided)));
    }

    // #94: measured Run inputs, not the hypothetical same-cutoff ablations.
    // Existing #86's fixed 'none'/'first-only' results provide an independent
    // cross-check for the declared condition. Null is separately asserted.
    [Fact]
    public void PublicRelationAvailabilityTicksFiveAndSixPinTheActualOutcomes()
    {
        var (known, unprovided) = PublicRelationAvailabilityRuns();
        var comparison = RescuerEvidenceMethodComparison.AnalyzePublicRelationAvailability(
            known, unprovided);
        Assert.Equal("available", comparison.Status);
        var expectations = new[]
        {
            (Method: "public-prior", Tick5: new[] { "A", "B", "C" },
                Tick6: new[] { "A", "B", "C" }),
            (Method: "known-relation-backward", Tick5: new[] { "A", "B", "C" },
                Tick6: new[] { "A", "B", "C" }),
            (Method: "distributed-reconstruction", Tick5: new[] { "A", "B", "C" },
                Tick6: new[] { "A", "B" }),
            (Method: "distributed-reconstruction-local", Tick5: new[] { "A", "B", "C" },
                Tick6: new[] { "A", "B" })
        };
        foreach (var (methodName, tick5, tick6) in expectations)
        {
            var method = comparison.Methods.Single(m => m.Method == methodName);
            foreach (var (tick, expected, received) in new[]
            {
                (Tick: method.Timeline[0], Expected: tick5, Received: 0),
                (Tick: method.Timeline[1], Expected: tick6, Received: 1)
            })
            {
                Assert.Equal("available", tick.Status);
                Assert.Equal(received, tick.ReceivedOriginalCount);
                Assert.Equal(received, tick.AvailableSourceGroupCount);
                Assert.Equal(expected, tick.KnownRelationMethod!.OuterCandidateRegionIds);
                Assert.Equal(expected, tick.UnprovidedRelationMethod!.OuterCandidateRegionIds);
                Assert.True(tick.SameOuterCandidates);
                Assert.Equal(0, tick.KnownMinusUnprovidedCandidateCount);
                Assert.Empty(tick.KnownOnlyCandidateRegionIds!);
                Assert.Empty(tick.UnprovidedOnlyCandidateRegionIds!);
                var expectedStatus = methodName == "public-prior"
                    ? "last-confirmed-unavailable-public-prior"
                    : received == 0 || methodName == "known-relation-backward"
                        ? "public-model-only" : "method-executed";
                Assert.Equal(expectedStatus, tick.KnownRelationMethod.EvidenceStatus);
                Assert.Equal(tick.KnownRelationMethod.EvidenceStatus,
                    tick.UnprovidedRelationMethod.EvidenceStatus);
            }
        }

        var backward = comparison.Methods.Single(m =>
            m.Method == "known-relation-backward").Timeline;
        var local = comparison.Methods.Single(m =>
            m.Method == "distributed-reconstruction-local").Timeline;
        var generation = comparison.Methods.Single(m =>
            m.Method == "distributed-reconstruction").Timeline;
        Assert.All(backward.Take(2), tick =>
        {
            Assert.Equal(1, tick.KnownRelationMethod!.ForwardTransitionSteps);
            Assert.Equal(1, tick.KnownRelationMethod.BackwardTransitionSteps);
            Assert.Equal(2, tick.KnownRelationMethod.PublicRelationsInspected);
            Assert.Equal(0, tick.UnprovidedRelationMethod!.ForwardTransitionSteps);
            Assert.Equal(0, tick.UnprovidedRelationMethod.BackwardTransitionSteps);
        });
        Assert.Equal(1, local[0].KnownRelationMethod!.ForwardTransitionSteps);
        Assert.Equal(0, local[0].UnprovidedRelationMethod!.ForwardTransitionSteps);
        Assert.Equal(2, local[1].KnownRelationMethod!.ForwardTransitionSteps);
        Assert.Equal(0, local[1].UnprovidedRelationMethod!.ForwardTransitionSteps);
        Assert.Equal(0, local[0].KnownRelationMethod.UsedSourceGroupCount);
        Assert.Equal(1, local[1].KnownRelationMethod.UsedSourceGroupCount);
        Assert.Equal(1, local[1].UnprovidedRelationMethod.UsedSourceGroupCount);
        Assert.Equal(4, local[1].KnownRelationMethod.GenerationSteps);
        Assert.Equal(4, local[1].UnprovidedRelationMethod.GenerationSteps);
        Assert.Equal(4, generation[1].KnownRelationMethod!.GenerationSteps);
        Assert.Equal(4, generation[1].UnprovidedRelationMethod!.GenerationSteps);
        Assert.Equal([RecordId], generation[1].KnownRelationMethod.GenerationDirectSourceIds);
        Assert.Equal([RecordId], generation[1].UnprovidedRelationMethod.GenerationDirectSourceIds);
        Assert.Empty(backward[1].KnownRelationMethod!.TraversalDirectSourceIds);
        Assert.Empty(backward[1].UnprovidedRelationMethod!.TraversalDirectSourceIds);
        Assert.All(comparison.Methods, method => Assert.Equal(0,
            method.Timeline[0].KnownRelationMethod!.UsedSourceGroupCount));
    }

    // #96: preserve the original AR-1 fixture but assert the corrected
    // open-world behavior instead of the former unsafe candidate reduction.
    [Fact]
    public void PartiallyPublishedTransitionsDoNotHardExcludeUnknownOuterCandidates()
    {
        var context = ComplementaryPublicContext();
        var partial = context with
        {
            PublicHazardEnvelope = context.PublicHazardEnvelope! with
            {
                Branches = [Branch("A", "D"), Branch("B", "E")]
            }
        };
        var first = ComplementaryOriginal(
            RecordId, HolderId, 2, ["A", "B"], 3, "weft-a");
        var second = ComplementaryOriginal(
            "second-original", "holder-b", 5, ["E", "F"], 6, "weft-b");
        var records = new[] { first, second };
        var receipts = new[] { Receipt(6), LaterComparisonReceipt(7) };
        RescuerEvidenceMethodReport Run(RescueContext publicContext) =>
            RescuerEvidenceMethodComparison.Run(
                publicContext, Seed, RescuerId, records, receipts,
                InteractionTimeWindow.At(2), [5, 6, 7], 7);

        var declared = Run(context).ActualTimeline[2].Methods;
        var incomplete = Run(partial).ActualTimeline[2].Methods;
        var declaredBackward = declared.Single(m => m.Method == "known-relation-backward");
        var partialBackward = incomplete.Single(m => m.Method == "known-relation-backward");
        var partialLocal = incomplete.Single(m =>
            m.Method == "distributed-reconstruction-local");
        Assert.Equal(["A", "B", "C"], declaredBackward.OuterCandidateRegionIds);
        Assert.Equal(["A", "B", "C"], partialBackward.OuterCandidateRegionIds);
        Assert.Equal(["A", "B"], partialLocal.OuterCandidateRegionIds);
        Assert.Equal("method-executed", partialBackward.EvidenceStatus);
        Assert.Equal("method-executed", partialLocal.EvidenceStatus);
        Assert.Empty(partialLocal.ConflictedCandidateRegionIds!);
        Assert.Empty(partialLocal.UncertainCandidateRegionIds!);
        Assert.True(declaredBackward.OuterCandidateRegionIds.All(
            id => partialBackward.OuterCandidateRegionIds.Contains(id)));
        // An unprovided physical A->E path would make A plausible.
        // Neither synthetic truth nor that unprovided path is passed to Run.
        Assert.Contains("A", partialLocal.OuterCandidateRegionIds);
    }

    // AR-4: use the former unsafe zero/one/two-receipt fixture
    // as a direct safe-outer regression, without changing the inputs.
    [Fact]
    public void PartialPublicBranchesCannotNarrowWithoutEvidenceOrDiscardConfirmedRegions()
    {
        var context = ComplementaryPublicContext();
        var first = ComplementaryOriginal(
            RecordId, HolderId, 2, ["A", "B"], 3, "weft-a");
        var second = ComplementaryOriginal(
            "second-original", "holder-b", 5, ["E", "F"], 6, "weft-b");
        var originals = new[] { first, second };
        var receipts = new[] { Receipt(6), LaterComparisonReceipt(7) };
        RescuerEvidenceMethodReport Run(RescueContext publicContext) =>
            RescuerEvidenceMethodComparison.Run(
                publicContext, Seed, RescuerId, originals, receipts,
                InteractionTimeWindow.At(2), [5, 6, 7], 7);
        RescuerEvidenceMethodRow At(
            RescuerEvidenceMethodReport report, int index, string method) =>
            report.ActualTimeline[index].Methods.Single(row => row.Method == method);

        var twoBranches = Run(context with
        {
            PublicHazardEnvelope = context.PublicHazardEnvelope! with
            {
                Branches = [Branch("A", "D"), Branch("B", "E")]
            }
        });
        var aOnly = Run(context with
        {
            PublicHazardEnvelope = context.PublicHazardEnvelope! with
            {
                Branches = [Branch("A", "D")]
            }
        });
        var aAndC = Run(context with
        {
            PublicHazardEnvelope = context.PublicHazardEnvelope! with
            {
                Branches = [Branch("A", "D"), Branch("C", "F")]
            }
        });

        // No originals have arrived at tick 5: public paths cannot invent
        // evidence or produce a false 'public-model-only' reduction.
        Assert.Empty(twoBranches.ActualTimeline[0].ReceivedOriginalIds);
        Assert.Equal(0, twoBranches.ActualTimeline[0].AvailableSourceGroupCount);
        foreach (var method in new[]
        {
            "known-relation-backward",
            "distributed-reconstruction-local"
        })
        {
            var beforeReceipt = At(twoBranches, 0, method);
            Assert.Equal(["A", "B", "C"], beforeReceipt.OuterCandidateRegionIds);
            Assert.Equal(3, beforeReceipt.BaselineCandidateCount);
            Assert.Equal(0, beforeReceipt.CandidateReduction);
            Assert.Equal("public-model-only", beforeReceipt.EvidenceStatus);
            Assert.Equal(0, beforeReceipt.UsedSourceGroupCount);
            var oneBranchBeforeReceipt = At(aOnly, 0, method);
            Assert.Equal(["A", "B", "C"], oneBranchBeforeReceipt.OuterCandidateRegionIds);
            Assert.Equal(0, oneBranchBeforeReceipt.CandidateReduction);
            Assert.Equal("public-model-only", oneBranchBeforeReceipt.EvidenceStatus);
        }

        // Tick-2 {A,B} is a confirmed received observation at query tick 6;
        // missing published B routes cannot safely rule B out.
        Assert.Equal(1, aOnly.ActualTimeline[1].ReceivedOriginalCount);
        var localAtSix = At(aOnly, 1, "distributed-reconstruction-local");
        Assert.Equal(["A", "B"], localAtSix.OuterCandidateRegionIds);
        Assert.Contains("B", localAtSix.OuterCandidateRegionIds);
        Assert.Empty(localAtSix.ConflictedCandidateRegionIds!);
        var alternateLocalAtSix = At(aAndC, 1, "distributed-reconstruction-local");
        Assert.Equal(["A", "B"], alternateLocalAtSix.OuterCandidateRegionIds);
        Assert.Contains("B", alternateLocalAtSix.OuterCandidateRegionIds);
        Assert.Empty(alternateLocalAtSix.ConflictedCandidateRegionIds!);

        // Do not mistake a reversal of the prior false exclusion for
        // meaningful information arriving with the second original.
        Assert.Equal(2, aOnly.ActualTimeline[2].ReceivedOriginalCount);
        var backwardAtSix = At(aOnly, 1, "known-relation-backward");
        var backwardAtSeven = At(aOnly, 2, "known-relation-backward");
        Assert.Equal(["A", "B", "C"], backwardAtSix.OuterCandidateRegionIds);
        Assert.Equal(["A", "B", "C"], backwardAtSeven.OuterCandidateRegionIds);
        Assert.Equal(backwardAtSix.CandidateCount, backwardAtSeven.CandidateCount);
    }

    [Fact]
    public void MissingPublicTransitionsRemainSeparateFromReceiptAndAncestryBoundaries()
    {
        var (known, unprovided) = PublicRelationAvailabilityRuns(secondAncestry: "weft-a");
        var shared = RescuerEvidenceMethodComparison.AnalyzePublicRelationAvailability(
            known, unprovided);
        Assert.Equal("available", shared.Status);
        Assert.All(shared.Methods, method =>
            Assert.Equal(1, method.Timeline[2].AvailableSourceGroupCount));
        Assert.All(shared.Methods, method =>
            Assert.True(method.Timeline[2].KnownRelationMethod!.UsedSourceGroupCount <= 1));
        foreach (var state in new[] { EvidenceState.SupportedButUncertain, EvidenceState.Conflicting })
        {
            var (softKnown, softUnprovided) = PublicRelationAvailabilityRuns(firstState: state);
            var result = RescuerEvidenceMethodComparison.AnalyzePublicRelationAvailability(
                softKnown, softUnprovided);
            Assert.Equal("available", result.Status);
            Assert.NotNull(result.Methods.Single(method =>
                method.Method == "distributed-reconstruction").Timeline[2]
                .UnprovidedRelationMethod!.ConflictedCandidateRegionIds);
        }
        var (deviceKnown, deviceUnprovided) = PublicRelationAvailabilityRuns(deviceOnly: true);
        var device = RescuerEvidenceMethodComparison.AnalyzePublicRelationAvailability(
            deviceKnown, deviceUnprovided);
        Assert.Empty(device.Methods.Single(method =>
            method.Method == "distributed-reconstruction").Timeline[2]
            .KnownRelationMethod!.GenerationDirectSourceIds);
        Assert.Empty(device.Methods.Single(method =>
            method.Method == "distributed-reconstruction").Timeline[2]
            .UnprovidedRelationMethod!.GenerationDirectSourceIds);
        Assert.Empty(device.Methods[0].Timeline[0].ReceivedOriginalIds);
        Assert.DoesNotContain("second-original", JsonDefaults.Serialize(
            device.Methods[0].Timeline[1].ReceivedOriginalIds));
    }

    [Fact]
    public void MissingPublicRelationsDoNotTurnAbsentResultsIntoEmptyCandidateSets()
    {
        var (known, unprovided) = PublicRelationAvailabilityRuns();
        var missing = unprovided with
        {
            ActualTimeline = unprovided.ActualTimeline.Select((row, index) =>
                index == 2 ? row with
                {
                    Methods = row.Methods.Where(m =>
                        m.Method != "known-relation-backward").ToArray()
                } : row).ToArray()
        };
        var partial = RescuerEvidenceMethodComparison.AnalyzePublicRelationAvailability(
            known, missing);
        Assert.Equal("partial", partial.Status);
        var absent = partial.Methods.Single(m => m.Method == "known-relation-backward")
            .Timeline[2];
        Assert.Equal("unavailable", absent.Status);
        Assert.Null(absent.KnownRelationMethod);
        Assert.Null(absent.KnownMinusUnprovidedCandidateCount);
        Assert.Null(absent.KnownOnlyCandidateRegionIds);
        var duplicated = unprovided with
        {
            ActualTimeline = unprovided.ActualTimeline.Select((row, index) => index == 1
                ? row with { Methods = [.. row.Methods, row.Methods[0]] } : row).ToArray()
        };
        Assert.Equal("partial", RescuerEvidenceMethodComparison
            .AnalyzePublicRelationAvailability(known, duplicated).Status);
        var empty = unprovided with
        {
            ActualTimeline = unprovided.ActualTimeline.Select((row, index) =>
                index == 2 ? row with
                {
                    Methods = row.Methods.Select(m => m.Method == "public-prior"
                        ? m with
                        {
                            OuterCandidateRegionIds = [],
                            CandidateCount = 0,
                            CandidateReduction = m.BaselineCandidateCount
                        } : m).ToArray()
                } : row).ToArray()
        };
        var genuineEmpty = RescuerEvidenceMethodComparison.AnalyzePublicRelationAvailability(
            known, empty).Methods.Single(m => m.Method == "public-prior").Timeline[2];
        Assert.Equal("available", genuineEmpty.Status);
        Assert.Empty(genuineEmpty.UnprovidedRelationMethod!.OuterCandidateRegionIds);
        Assert.NotNull(genuineEmpty.KnownOnlyCandidateRegionIds);
        Assert.Equal("unavailable", RescuerEvidenceMethodComparison
            .AnalyzePublicRelationAvailability(known, unprovided with { Seed = Seed + 1 }).Status);
        Assert.Equal("unavailable", RescuerEvidenceMethodComparison
            .AnalyzePublicRelationAvailability(known, unprovided with
            {
                ActualTimeline = unprovided.ActualTimeline.Skip(1).ToArray()
            }).Status);
        var changedReceipt = unprovided with
        {
            ActualTimeline = unprovided.ActualTimeline.Select((row, index) => index == 2
                ? row with { ReceivedOriginalIds = [RecordId, "not-the-same-record"] }
                : row).ToArray()
        };
        Assert.Equal("unavailable", RescuerEvidenceMethodComparison
            .AnalyzePublicRelationAvailability(known, changedReceipt).Status);
    }


    // #96: same #94 Run inputs, corrected tracer; old measurements remain
    // separately in the research document. Counters are not rescue metrics.
    [Fact]
    public void PublicRelationAvailabilityDiagnosticSnapshot()
    {
        var (known, unprovided) = PublicRelationAvailabilityRuns();
        static string Standing(IReadOnlyList<string>? items) =>
            items is null ? "null" : string.Join(",", items);
        var measured = string.Join("\n", new[]
        {
            (Label: "known", Report: known),
            (Label: "unprovided", Report: unprovided)
        }.SelectMany(condition => condition.Report.ActualTimeline
            .Where(row => row.QueryTick == 7)
            .SelectMany(row => row.Methods.Select(method =>
                $"FP94 {condition.Label} {method.Method}: " +
                $"outer={Standing(method.OuterCandidateRegionIds)}; " +
                $"status={method.EvidenceStatus}; " +
                $"uncertain={Standing(method.UncertainCandidateRegionIds)}; " +
                $"conflict={Standing(method.ConflictedCandidateRegionIds)}; " +
                $"genSources={Standing(method.GenerationDirectSourceIds)}; " +
                $"traversalSources={Standing(method.TraversalDirectSourceIds)}; " +
                $"groups={row.AvailableSourceGroupCount}/{method.UsedSourceGroupCount}; " +
                $"work={method.GenerationSteps}/{method.VerificationSteps}/" +
                $"{method.ForwardTransitionSteps}/{method.BackwardTransitionSteps}/" +
                $"{method.EvidenceRecordsInspected}/{method.PublicRelationsInspected}"))));
        const string expected = """
            FP94 known public-prior: outer=A,B,C; status=last-confirmed-unavailable-public-prior; uncertain=null; conflict=null; genSources=; traversalSources=; groups=2/0; work=0/0/0/0/0/0
            FP94 known known-relation-backward: outer=A,B,C; status=method-executed; uncertain=null; conflict=null; genSources=; traversalSources=second-original; groups=2/1; work=0/0/2/2/1/5
            FP94 known distributed-reconstruction: outer=A,B; status=method-executed; uncertain=; conflict=; genSources=original-encounter-record; traversalSources=; groups=2/1; work=4/0/0/0/1/1
            FP94 known distributed-reconstruction-local: outer=A,B; status=method-executed; uncertain=; conflict=; genSources=original-encounter-record; traversalSources=second-original; groups=2/2; work=4/0/2/2/1/5
            FP94 unprovided public-prior: outer=A,B,C; status=last-confirmed-unavailable-public-prior; uncertain=null; conflict=null; genSources=; traversalSources=; groups=2/0; work=0/0/0/0/0/0
            FP94 unprovided known-relation-backward: outer=A,B,C; status=method-executed; uncertain=null; conflict=null; genSources=; traversalSources=second-original; groups=2/1; work=0/0/0/0/1/1
            FP94 unprovided distributed-reconstruction: outer=A,B; status=method-executed; uncertain=; conflict=; genSources=original-encounter-record; traversalSources=; groups=2/1; work=4/0/0/0/1/1
            FP94 unprovided distributed-reconstruction-local: outer=A,B; status=method-executed; uncertain=; conflict=; genSources=original-encounter-record; traversalSources=second-original; groups=2/2; work=4/0/0/0/1/1
            """;
        Assert.Equal(expected, measured);
    }
}
