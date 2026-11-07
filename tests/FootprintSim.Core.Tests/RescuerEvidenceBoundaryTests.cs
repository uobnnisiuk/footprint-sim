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

        // xUnit captures Console.Out on passing tests. Write only deterministic,
        // public aggregate work counters directly to the runner for CI verification.
        var ciMetrics = new StreamWriter(Console.OpenStandardOutput()) { AutoFlush = true };
        foreach (var method in result.Methods)
        {
            foreach (var row in method.Conditions)
            {
                ciMetrics.WriteLine(
                    $"FP-WEFT86 {method.Method} {row.Label}: " +
                    $"usedGroups={row.UsedSourceGroupCount}, " +
                    $"generation={row.GenerationSteps}, verification={row.VerificationSteps}, " +
                    $"forward={row.ForwardTransitionSteps}, backward={row.BackwardTransitionSteps}, " +
                    $"evidenceInspected={row.EvidenceRecordsInspected}, " +
                    $"relationsInspected={row.PublicRelationsInspected}");
            }
        }

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
}
