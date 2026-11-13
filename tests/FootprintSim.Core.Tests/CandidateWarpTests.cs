using System.Reflection;
using FootprintSim.Evidence;
using FootprintSim.Evaluation;
using FootprintSim.Events;
using FootprintSim.Hazards;
using FootprintSim.Interactions;
using FootprintSim.Interactions.CandidateWarps;
using FootprintSim.Interactions.Tracing;
using FootprintSim.Interactions.Truth;
using FootprintSim.Serialization;
using FootprintSim.Simulation;
using Xunit;

namespace FootprintSim.Core.Tests;

public sealed class CandidateWarpTests
{
    // #150 / AR-40/42: both receipt-tick order and a single historical
    // StateInterval must retain the later evidence's tick and its outer.
    [Fact]
    public void MultiPastConfirmedIntervalRetainsLaterTickAndUnknownPublicLinks()
    {
        PublicHazardBranch Edge(string from, string to) => new()
        {
            SourceRegionId = from,
            DestinationRegionId = to,
            MinimumTravelTicks = 1,
            MaximumTravelTicks = 1,
            TransportType = HazardTransportType.HazardDriven,
            ProvenanceId = $"public-{from}-{to}",
            PublishedAtTick = 0
        };
        var envelope = new PublicHazardEnvelope
        {
            HazardId = "ar40-public",
            SourceRegionId = "a",
            PublishedAtTick = 0,
            Branches =
            [
                Edge("a", "b"), Edge("a", "d"), Edge("c", "d"),
                Edge("b", "e"), Edge("d", "f")
            ]
        };
        var historical = Warp(new InteractionTimeWindow(0, 1),
            CandidateWarpTemporalKind.StateInterval, ["a", "b", "c", "d"]);
        var first = Constraint(EvidenceState.Confirmed,
            InteractionTimeWindow.At(0), ["a"], 2, "ar40-observation-zero");
        var second = Constraint(EvidenceState.Confirmed,
            InteractionTimeWindow.At(1), ["d"], 2, "ar40-observation-one");
        var combined = Apply(historical, [first, second], 2, envelope);
        var reversed = Apply(historical, [second, first], 2, envelope);
        Assert.Equal(["f"], combined.CandidateWarps.PreferredRegionIdsAt(2));
        Assert.Contains("f", combined.CandidateWarps.CandidateRegionIdsAt(2));
        Assert.DoesNotContain(combined.Audits, audit =>
            audit.ConstrainedTimeWindow.StartTick == 1 &&
            audit.Reason == ConstraintApplicationReason.NoTemporalOverlap);
        Assert.Contains(combined.Audits, audit =>
            audit.ConstrainedTimeWindow.StartTick == 1 &&
            audit.Disposition == ConstraintApplicationDisposition.Narrow);
        Assert.Equal(JsonDefaults.Serialize(combined), JsonDefaults.Serialize(reversed));

        var duplicated = Apply(historical, [first, first, second], 2, envelope);
        Assert.Equal(combined.CandidateWarps.PreferredRegionIdsAt(2),
            duplicated.CandidateWarps.PreferredRegionIdsAt(2));
        Assert.Contains(duplicated.Audits, audit =>
            audit.Reason == ConstraintApplicationReason.DuplicateSourceObservation);

        // c->b is not published. The later *received* P@1=b is still
        // possible through an unpublished relation; it cannot be promoted
        // to a confirmed public path, or silently eliminated from outer.
        var partial = Apply(historical,
        [
            Constraint(EvidenceState.Confirmed, InteractionTimeWindow.At(0),
                ["c"], 2, "ar40-partial-zero"),
            Constraint(EvidenceState.Confirmed, InteractionTimeWindow.At(1),
                ["b"], 2, "ar40-partial-one")
        ], 2, envelope);
        Assert.Contains("e", partial.CandidateWarps.OuterPlausibleRegionIdsAt(2));
        Assert.DoesNotContain("e", partial.CandidateWarps.PreferredRegionIdsAt(2));
        Assert.Contains(partial.Audits, audit =>
            audit.ConstrainedTimeWindow.StartTick == 1 &&
            audit.Disposition == ConstraintApplicationDisposition.RetainAlternative);

        // AR-44: the later Confirmed BLE encounter can span two ticks.
        // A published a@0 -> d@1 path remains usable inside [1,2];
        // an inclusive observation does not assert residence at both ticks.
        var intervalStart = Warp(new InteractionTimeWindow(0, 2),
            CandidateWarpTemporalKind.StateInterval, ["a", "b", "c", "d"]);
        var initialAtZero = Constraint(EvidenceState.Confirmed,
            InteractionTimeWindow.At(0), ["a"], 3, "ar44-initial");
        var laterInterval = Constraint(EvidenceState.Confirmed,
            new InteractionTimeWindow(1, 2), ["d"], 3, "ar44-later-window");
        var laterOnly = Apply(intervalStart, [laterInterval], 3, envelope);
        var intervalCombined = Apply(intervalStart,
            [initialAtZero, laterInterval], 3, envelope);
        Assert.Contains("f", laterOnly.CandidateWarps.PreferredRegionIdsAt(3));
        Assert.Contains("f", intervalCombined.CandidateWarps.PreferredRegionIdsAt(3));
        Assert.Contains(intervalCombined.Audits, audit =>
            audit.ConstrainedTimeWindow.StartTick == 1 &&
            audit.Disposition == ConstraintApplicationDisposition.Narrow &&
            audit.Reason == ConstraintApplicationReason.Applied);
        Assert.DoesNotContain(intervalCombined.Audits, audit =>
            audit.ConstrainedTimeWindow.StartTick == 1 &&
            audit.Reason == ConstraintApplicationReason.NoRegionOverlap);

        // With no published path a->d, retain the possible observed
        // encounter as outer-only; the audit must not claim empty regions.
        var missingLinkEnvelope = new PublicHazardEnvelope
        {
            HazardId = "ar44-missing-link",
            SourceRegionId = "a",
            PublishedAtTick = 0,
            Branches = envelope.Branches
                .Where(edge => edge.SourceRegionId != "a" ||
                    edge.DestinationRegionId != "d")
                .ToArray()
        };
        var unverifiedInterval = Apply(intervalStart,
            [initialAtZero, laterInterval], 3, missingLinkEnvelope);
        Assert.DoesNotContain("f",
            unverifiedInterval.CandidateWarps.PreferredRegionIdsAt(3));
        Assert.Contains("f",
            unverifiedInterval.CandidateWarps.OuterPlausibleRegionIdsAt(3));
        Assert.Contains(unverifiedInterval.Audits, audit =>
            audit.ConstrainedTimeWindow.StartTick == 1 &&
            audit.Disposition == ConstraintApplicationDisposition.RetainAlternative &&
            audit.Reason == ConstraintApplicationReason.Applied &&
            audit.CandidateCountAfter > 0);

        // AR-44 also reproduces with distinct EventPoint layers.
        var publicFixtureProvenance = CandidateWarpProvenance.Create(
            CandidateWarpSourceKind.VoluntaryReachability,
            "ar44-public-model",
            availableAtTick: 0,
            isPersonEvidence: false,
            isDeviceOnlyEvidence: false,
            EvidenceState.SupportedButUncertain,
            ["a", "b", "c", "d", "e", "f"]);
        var eventPoints = PublicCandidateWarpFactory.Create(
            ScenarioId, Seed, TargetPersonId,
            [
                new PublicCandidateWarpStateDefinition
                {
                    TimeWindow = InteractionTimeWindow.At(0),
                    TemporalKind = CandidateWarpTemporalKind.EventPoint,
                    RegionIds = ["a", "c"],
                    EvidenceBoundaryState = EvidenceState.SupportedButUncertain,
                    Provenance = [publicFixtureProvenance]
                },
                new PublicCandidateWarpStateDefinition
                {
                    TimeWindow = InteractionTimeWindow.At(1),
                    TemporalKind = CandidateWarpTemporalKind.EventPoint,
                    RegionIds = ["b", "d"],
                    EvidenceBoundaryState = EvidenceState.SupportedButUncertain,
                    Provenance = [publicFixtureProvenance]
                },
                new PublicCandidateWarpStateDefinition
                {
                    TimeWindow = InteractionTimeWindow.At(2),
                    TemporalKind = CandidateWarpTemporalKind.EventPoint,
                    RegionIds = ["e", "f"],
                    EvidenceBoundaryState = EvidenceState.SupportedButUncertain,
                    Provenance = [publicFixtureProvenance]
                }
            ]);
        var layeredInterval = Apply(eventPoints,
            [initialAtZero, laterInterval], 3, envelope);
        Assert.Contains("f", layeredInterval.CandidateWarps.PreferredRegionIdsAt(3));
        Assert.DoesNotContain(layeredInterval.Audits, audit =>
            audit.ConstrainedTimeWindow.StartTick == 1 &&
            audit.Reason == ConstraintApplicationReason.NoRegionOverlap);

        // AR-45: an uncertain query-time observation cannot turn the
        // unpublished c@0 -> b@1 sequence into a preferred history.
        var uncertainAtCurrent = Constraint(EvidenceState.SupportedButUncertain,
            InteractionTimeWindow.At(2), ["e", "f"], 2, "ar45-uncertain");
        var uncertainFollowup = Apply(historical,
        [
            Constraint(EvidenceState.Confirmed, InteractionTimeWindow.At(0),
                ["c"], 2, "ar40-partial-zero"),
            Constraint(EvidenceState.Confirmed, InteractionTimeWindow.At(1),
                ["b"], 2, "ar40-partial-one"),
            uncertainAtCurrent
        ], 2, envelope);
        Assert.DoesNotContain("e",
            uncertainFollowup.CandidateWarps.PreferredRegionIdsAt(2));
        Assert.Contains("e",
            uncertainFollowup.CandidateWarps.OuterPlausibleRegionIdsAt(2));
    }


    // Ready-stage Codex review: a public route to b cannot promote an
    // unverified d, even when both are allowed by one confirmed encounter.
    [Fact]
    public void MixedPublicReachabilityKeepsUnverifiedDescendantOuterOnly()
    {
        PublicHazardBranch Edge(string from, string to) => new()
        {
            SourceRegionId = from,
            DestinationRegionId = to,
            MinimumTravelTicks = 1,
            MaximumTravelTicks = 1,
            TransportType = HazardTransportType.HazardDriven,
            ProvenanceId = $"codex-public-{from}-{to}",
            PublishedAtTick = 0
        };
        var envelope = new PublicHazardEnvelope
        {
            HazardId = "codex-mixed-public-link",
            SourceRegionId = "a",
            PublishedAtTick = 0,
            Branches = [Edge("a", "b"), Edge("b", "e"), Edge("d", "f")]
        };
        var historical = Warp(new InteractionTimeWindow(0, 1),
            CandidateWarpTemporalKind.StateInterval, ["a", "b", "d"]);
        var first = Constraint(EvidenceState.Confirmed,
            InteractionTimeWindow.At(0), ["a"], 2, "codex-first");
        var mixed = Constraint(EvidenceState.Confirmed,
            InteractionTimeWindow.At(1), ["b", "d"], 2, "codex-mixed");
        var result = Apply(historical, [first, mixed], 2, envelope);
        var reversed = Apply(historical, [mixed, first], 2, envelope);

        Assert.Contains("e", result.CandidateWarps.PreferredRegionIdsAt(2));
        Assert.DoesNotContain("f", result.CandidateWarps.PreferredRegionIdsAt(2));
        Assert.Contains("f", result.CandidateWarps.OuterPlausibleRegionIdsAt(2));
        Assert.Contains(result.Audits, audit =>
            audit.ConstraintId == mixed.ConstraintId &&
            audit.Disposition == ConstraintApplicationDisposition.Narrow &&
            audit.AlternativesRetained);
        Assert.Equal(JsonDefaults.Serialize(result), JsonDefaults.Serialize(reversed));
    }

    // An exact confirmed query-time observation can support the current
    // region without a hazard envelope; earlier route uncertainty remains
    // separate from that direct evidence.
    [Fact]
    public void ExactCurrentConfirmationWithoutEnvelopeRemainsPreferred()
    {
        var historical = Warp(new InteractionTimeWindow(0, 1),
            CandidateWarpTemporalKind.StateInterval, ["a"]);
        var earlier = Constraint(EvidenceState.Confirmed,
            InteractionTimeWindow.At(0), ["a"], 1, "codex-current-earlier");
        var atQuery = Constraint(EvidenceState.Confirmed,
            InteractionTimeWindow.At(1), ["a"], 1, "codex-current-direct");
        var result = Apply(historical, [earlier, atQuery], 1);

        Assert.Contains("a", result.CandidateWarps.PreferredRegionIdsAt(1));
        Assert.Contains(result.CandidateWarps.CandidateRegionIdsAt(1),
            region => region == "a");
        Assert.Contains(result.Audits, audit =>
            audit.ConstraintId == atQuery.ConstraintId &&
            audit.Disposition == ConstraintApplicationDisposition.Narrow);
    }

    [Fact]
    public void ConfirmedExactTickNarrowsHistoricalStateThenUsesPublicForwardPropagation()
    {
        var candidates = Warp(
            InteractionTimeWindow.At(20),
            CandidateWarpTemporalKind.EventPoint,
            ["R4", "R2", "R3"]);
        var constraint = Constraint(
            EvidenceState.Confirmed,
            InteractionTimeWindow.At(20),
            ["R3"]);

        var result = Apply(candidates, [constraint], currentTick: 25, PublicEnvelope());

        var warp = Assert.Single(result.CandidateWarps.Warps,
            item => item.Standing == CandidateWarpStanding.Preferred);
        Assert.Equal(["R3"], warp.CandidateRegionIdsAt(20));
        Assert.Equal(["R5", "R6"], warp.CandidateRegionIdsAt(25));
        Assert.Equal(["R3", "R5", "R6"], result.CandidateWarps.CandidateRegionIdsAt(25));
        Assert.DoesNotContain("R3", result.CandidateWarps.PreferredRegionIdsAt(25));
        var outer = Assert.Single(result.CandidateWarps.Warps,
            item => item.Standing == CandidateWarpStanding.OuterPlausible);
        Assert.Equal(["R3"], outer.CandidateRegionIdsAt(20));
        var audit = Assert.Single(result.Audits);
        Assert.Equal(ConstraintApplicationDisposition.Narrow, audit.Disposition);
        Assert.Equal(ConstraintTemporalKind.ExactTick, audit.ConstraintTemporalKind);
        Assert.Equal(3, audit.CandidateCountBefore);
        Assert.Equal(1, audit.CandidateCountAfter);
        Assert.True(audit.AlternativesRetained);
        Assert.False(audit.ConflictDetected);
        Assert.Equal(InteractionRelationKind.PersonPersonEncounter, audit.RelationKind);
        Assert.Equal(constraint.SourceObservationId, audit.SourceObservationId);
        Assert.Contains(constraint.SourceObservationId, audit.SourceAncestryIds);
        Assert.All(audit.SourceParticipants, participant =>
            Assert.Equal(InteractionParticipantRole.EncounterParticipant, participant.Role));

        var historical = Assert.Single(warp.States, state =>
            state.TimeWindow.StartTick == 20 && state.TimeWindow.EndTick == 20);
        var crossThread = Assert.Single(historical.Provenance, provenance =>
            provenance.SourceKind == CandidateWarpSourceKind.CrossThreadObservation);
        Assert.Equal(constraint.SourceObservationId, crossThread.SourceObservationId);
        Assert.Equal(constraint.SourceRelationKind, crossThread.RelationKind);
        Assert.Equal(constraint.SourceObservedTimeWindow, crossThread.SourceTimeWindow);
        Assert.Contains(warp.States.Single(state => state.TimeWindow.StartTick == 25).Provenance,
            provenance => provenance.SourceKind == CandidateWarpSourceKind.PublicHazardEnvelope);
    }

    [Fact]
    public void ConfirmedInclusiveIntervalConstrainsOnlyOverlappingStateInterval()
    {
        var candidates = Warp(
            new InteractionTimeWindow(19, 24),
            CandidateWarpTemporalKind.StateInterval,
            ["R2", "R3", "R4"]);
        var constraint = Constraint(
            EvidenceState.Confirmed,
            new InteractionTimeWindow(20, 22),
            ["R3"],
            availableAtTick: 22);

        var result = Apply(candidates, [constraint], currentTick: 22);

        var warp = Assert.Single(result.CandidateWarps.Warps);
        Assert.Collection(
            warp.States,
            state =>
            {
                Assert.Equal(19, state.TimeWindow.StartTick);
                Assert.Equal(19, state.TimeWindow.EndTick);
                Assert.Equal(CandidateWarpTemporalKind.StateInterval, state.TemporalKind);
                Assert.Equal(["R2", "R3", "R4"], state.RegionIds);
            },
            state =>
            {
                Assert.Equal(20, state.TimeWindow.StartTick);
                Assert.Equal(22, state.TimeWindow.EndTick);
                Assert.Equal(CandidateWarpTemporalKind.StateInterval, state.TemporalKind);
                Assert.Equal(["R3"], state.RegionIds);
            });
        var audit = Assert.Single(result.Audits);
        Assert.Equal(ConstraintTemporalKind.InclusiveInterval, audit.ConstraintTemporalKind);
        Assert.Equal(20, audit.ConstrainedTimeWindow.StartTick);
        Assert.Equal(22, audit.ConstrainedTimeWindow.EndTick);
        Assert.Equal(constraint.SourceObservedTimeWindow, audit.SourceObservedTimeWindow);
    }

    [Fact]
    public void PartialIntervalOverlapPreservesTheActualInclusiveOverlap()
    {
        var candidates = Warp(
            new InteractionTimeWindow(21, 24),
            CandidateWarpTemporalKind.StateInterval,
            ["R2", "R3"]);
        var constraint = Constraint(
            EvidenceState.Confirmed,
            new InteractionTimeWindow(20, 22),
            ["R3"],
            availableAtTick: 22);

        var result = Apply(candidates, [constraint], currentTick: 22);

        var state = Assert.Single(result.CandidateWarps.Warps).States.Single();
        Assert.Equal(21, state.TimeWindow.StartTick);
        Assert.Equal(22, state.TimeWindow.EndTick);
        Assert.Equal(CandidateWarpTemporalKind.StateInterval, state.TemporalKind);
        Assert.Equal(["R3"], state.RegionIds);
    }

    [Fact]
    public void NoTemporalOverlapIsNoOpAndDoesNotTurnMissingStateIntoAbsence()
    {
        var candidates = Warp(
            InteractionTimeWindow.At(10),
            CandidateWarpTemporalKind.EventPoint,
            ["R2", "R3"]);
        var constraint = Constraint(
            EvidenceState.Confirmed,
            InteractionTimeWindow.At(20),
            ["R3"]);

        var result = Apply(candidates, [constraint], currentTick: 23);

        Assert.Equal(JsonDefaults.Serialize(candidates), JsonDefaults.Serialize(result.CandidateWarps));
        var audit = Assert.Single(result.Audits);
        Assert.Equal(ConstraintApplicationDisposition.NoOp, audit.Disposition);
        Assert.Equal(ConstraintApplicationReason.NoTemporalOverlap, audit.Reason);
        Assert.False(audit.ConflictDetected);
    }

    [Fact]
    public void ExactTickAndMultiTickIntervalRemainDifferentTemporalStructures()
    {
        var exactConstraint = Constraint(
            EvidenceState.Confirmed,
            InteractionTimeWindow.At(20),
            ["R3"],
            sourceEvidenceIdentifier: "exact-source");
        var intervalConstraint = Constraint(
            EvidenceState.Confirmed,
            new InteractionTimeWindow(20, 23),
            ["R3"],
            availableAtTick: 24,
            sourceEvidenceIdentifier: "interval-source");
        var exact = Apply(
            Warp(InteractionTimeWindow.At(20), CandidateWarpTemporalKind.EventPoint, ["R3"]),
            [exactConstraint],
            currentTick: 23);
        var interval = Apply(
            Warp(new InteractionTimeWindow(20, 23), CandidateWarpTemporalKind.StateInterval, ["R3"]),
            [intervalConstraint],
            currentTick: 24);

        Assert.Equal(ConstraintTemporalKind.ExactTick, Assert.Single(exact.Audits).ConstraintTemporalKind);
        Assert.Equal(
            ConstraintTemporalKind.InclusiveInterval,
            Assert.Single(interval.Audits).ConstraintTemporalKind);
        Assert.NotEqual(
            JsonDefaults.Serialize(exact.Audits),
            JsonDefaults.Serialize(interval.Audits));
        Assert.Equal(
            CandidateWarpTemporalKind.EventPoint,
            exact.CandidateWarps.Warps[0].States[0].TemporalKind);
        Assert.Equal(
            CandidateWarpTemporalKind.StateInterval,
            interval.CandidateWarps.Warps[0].States[0].TemporalKind);
    }

    [Fact]
    public void SupportedButUncertainBranchesAndRetainsOuterPlausibleAlternative()
    {
        var candidates = Warp(
            InteractionTimeWindow.At(20),
            CandidateWarpTemporalKind.EventPoint,
            ["R2", "R3", "R4"]);
        var constraint = Constraint(
            EvidenceState.SupportedButUncertain,
            InteractionTimeWindow.At(20),
            ["R3"]);

        var result = Apply(candidates, [constraint], currentTick: 25, PublicEnvelope());

        Assert.Equal(2, result.CandidateWarps.Warps.Count);
        Assert.Equal(["R5", "R6"], result.CandidateWarps.PreferredRegionIdsAt(25));
        Assert.Equal(
            ["R2", "R3", "R4", "R5", "R6"],
            result.CandidateWarps.OuterPlausibleRegionIdsAt(25));
        var audit = Assert.Single(result.Audits);
        Assert.Equal(ConstraintApplicationDisposition.Branch, audit.Disposition);
        Assert.True(audit.AlternativesRetained);
        Assert.False(audit.ConflictDetected);
    }

    [Theory]
    [InlineData(EvidenceState.Confirmed, "R9", ConstraintApplicationReason.NoRegionOverlap)]
    [InlineData(EvidenceState.Conflicting, "R3", ConstraintApplicationReason.EvidenceStateDoesNotConstrain)]
    public void ConflictNeverDeletesAllCandidateWarps(
        EvidenceState evidenceState,
        string allowedRegion,
        ConstraintApplicationReason expectedReason)
    {
        var candidates = Warp(
            InteractionTimeWindow.At(20),
            CandidateWarpTemporalKind.EventPoint,
            ["R2", "R3"]);
        var constraint = Constraint(
            evidenceState,
            InteractionTimeWindow.At(20),
            [allowedRegion]);

        var result = Apply(candidates, [constraint], currentTick: 23);

        var warp = Assert.Single(result.CandidateWarps.Warps);
        Assert.Equal(CandidateWarpStanding.Conflicted, warp.Standing);
        Assert.Equal(["R2", "R3"], warp.CandidateRegionIdsAt(23));
        var audit = Assert.Single(result.Audits);
        Assert.Equal(ConstraintApplicationDisposition.MarkConflict, audit.Disposition);
        Assert.Equal(expectedReason, audit.Reason);
        Assert.True(audit.AlternativesRetained);
        Assert.True(audit.ConflictDetected);
        Assert.Equal(audit.CandidateCountBefore, audit.CandidateCountAfter);
    }

    [Theory]
    [InlineData(EvidenceState.Indeterminate)]
    [InlineData(EvidenceState.UnsupportedForQuery)]
    public void IndeterminateAndUnsupportedConstraintsAreNoOp(EvidenceState state)
    {
        var candidates = Warp(
            InteractionTimeWindow.At(20),
            CandidateWarpTemporalKind.EventPoint,
            ["R2", "R3"]);
        var constraint = Constraint(state, InteractionTimeWindow.At(20), ["R3"]);

        var result = Apply(candidates, [constraint], currentTick: 23);

        Assert.Equal(JsonDefaults.Serialize(candidates), JsonDefaults.Serialize(result.CandidateWarps));
        Assert.Equal(ConstraintApplicationDisposition.NoOp, Assert.Single(result.Audits).Disposition);
    }

    [Fact]
    public void UnavailableAndPrivateConstraintsDoNotEnterTheSearchContext()
    {
        var candidates = Warp(
            InteractionTimeWindow.At(20),
            CandidateWarpTemporalKind.EventPoint,
            ["R2", "R3"]);
        var unavailable = Constraint(
            EvidenceState.Confirmed,
            InteractionTimeWindow.At(20),
            ["R3"],
            availableAtTick: 23);
        var privateConstraint = Constraint(
            EvidenceState.Confirmed,
            InteractionTimeWindow.At(20),
            ["R3"],
            availableAtTick: 20,
            sourceEvidenceIdentifier: "private-source",
            visibility: InteractionVisibility.PrivateEvidence);

        var before = Apply(candidates, [unavailable, privateConstraint], currentTick: 22);
        var atBoundary = Apply(candidates, [unavailable, privateConstraint], currentTick: 23);

        Assert.Empty(before.Audits);
        Assert.Equal(JsonDefaults.Serialize(candidates), JsonDefaults.Serialize(before.CandidateWarps));
        Assert.Single(atBoundary.Audits);
        Assert.Equal(ConstraintApplicationDisposition.Narrow, atBoundary.Audits[0].Disposition);
    }

    [Fact]
    public void SameSourceDuplicateIsSuppressedRatherThanAppliedAsIndependentEvidence()
    {
        var candidates = Warp(
            InteractionTimeWindow.At(20),
            CandidateWarpTemporalKind.EventPoint,
            ["R2", "R3", "R4"]);
        var constraint = Constraint(
            EvidenceState.Confirmed,
            InteractionTimeWindow.At(20),
            ["R3"]);

        var result = Apply(candidates, [constraint, constraint], currentTick: 23);

        Assert.Single(result.Audits, audit =>
            audit.Disposition == ConstraintApplicationDisposition.Narrow);
        var duplicate = Assert.Single(result.Audits, audit =>
            audit.Reason == ConstraintApplicationReason.DuplicateSourceObservation);
        Assert.Equal(ConstraintApplicationDisposition.NoOp, duplicate.Disposition);
        Assert.True(duplicate.AlternativesRetained);
        Assert.Equal(["R3"], result.CandidateWarps.CandidateRegionIdsAt(23));
    }

    [Fact]
    public void MissingConstraintDoesNotBecomeAnAbsenceClaim()
    {
        var candidates = Warp(
            InteractionTimeWindow.At(20),
            CandidateWarpTemporalKind.EventPoint,
            ["R2", "R3"]);

        var result = Apply(candidates, [], currentTick: 23);

        Assert.Empty(result.Audits);
        Assert.Equal(JsonDefaults.Serialize(candidates), JsonDefaults.Serialize(result.CandidateWarps));
    }

    [Fact]
    public void DeviceOnlyEvidenceCannotHardNarrowAPersonWarp()
    {
        var observation = InteractionObservation.Create(
            ScenarioId,
            Seed,
            "device-only-source",
            referencedInteractionId: null,
            InteractionRelationKind.PersonPersonEncounter,
            [
                Participant(TargetPersonId, InteractionEntityKind.Person),
                Participant("device-b", InteractionEntityKind.Device)
            ],
            InteractionTimeWindow.At(20),
            ["R3"],
            availableAtTick: 23,
            EvidenceState.Confirmed,
            isPersonEvidence: false,
            isDeviceOnlyEvidence: true);

        var derived = CrossThreadConstraintDeriver.DerivePersonEncounterCoLocation(
            observation,
            TargetPersonId);
        var candidates = Warp(
            InteractionTimeWindow.At(20),
            CandidateWarpTemporalKind.EventPoint,
            ["R2", "R3"]);
        var result = Apply(candidates, derived is null ? [] : [derived], currentTick: 23);

        Assert.Null(derived);
        Assert.Empty(result.Audits);
        Assert.Equal(["R2", "R3"], result.CandidateWarps.CandidateRegionIdsAt(23));
    }

    [Fact]
    public void PublicCandidateWarpApiCannotReceiveTruthOccurrenceOrRealizedRun()
    {
        var publicTypes = new[]
        {
            typeof(PublicCandidateWarpFactory),
            typeof(PublicCandidateWarpProjection),
            typeof(CrossThreadConstraintApplicator),
            typeof(CandidateWarp),
            typeof(CandidateWarpState),
            typeof(CandidateWarpSet),
            typeof(CandidateWarpApplicationResult),
            typeof(ConstraintApplicationAudit)
        };

        foreach (var type in publicTypes)
        {
            var methods = type.GetMethods(
                BindingFlags.Public |
                BindingFlags.Instance |
                BindingFlags.Static |
                BindingFlags.DeclaredOnly);
            Assert.All(methods, method =>
            {
                Assert.False(ContainsType(method.ReturnType, typeof(InteractionOccurrence)));
                Assert.False(ContainsType(method.ReturnType, typeof(SimulationRun)));
                Assert.False(ContainsType(method.ReturnType, typeof(SimulationEvent)));
                Assert.All(method.GetParameters(), parameter =>
                {
                    Assert.False(ContainsType(parameter.ParameterType, typeof(InteractionOccurrence)));
                    Assert.False(ContainsType(parameter.ParameterType, typeof(SimulationRun)));
                    Assert.False(ContainsType(parameter.ParameterType, typeof(SimulationEvent)));
                });
            });
        }

        var strategyProperties = typeof(SearchStrategyInput).GetProperties();
        Assert.Equal(8, strategyProperties.Length);
        Assert.Equal(typeof(IReadOnlyList<string>), Assert.Single(strategyProperties,
            property => property.Name == nameof(SearchStrategyInput.PublicSearchDomainRegionIds)).PropertyType);
        Assert.All(strategyProperties, property =>
        {
            Assert.False(ContainsType(property.PropertyType, typeof(InteractionOccurrence)));
            Assert.False(ContainsType(property.PropertyType, typeof(SimulationRun)));
            Assert.False(ContainsType(property.PropertyType, typeof(SimulationEvent)));
        });
    }

    [Fact]
    public void PublicCandidateWarpRejectsPrivateEvidenceProvenance()
    {
        var privateProvenance = CandidateWarpProvenance.Create(
            CandidateWarpSourceKind.PersonObservation,
            "private-observation",
            availableAtTick: 20,
            isPersonEvidence: true,
            isDeviceOnlyEvidence: false,
            EvidenceState.Confirmed,
            ["R3"],
            visibility: InteractionVisibility.PrivateEvidence);

        Assert.Throws<ArgumentException>(() => PublicCandidateWarpFactory.Create(
            ScenarioId,
            Seed,
            TargetPersonId,
            [
                new PublicCandidateWarpStateDefinition
                {
                    TimeWindow = InteractionTimeWindow.At(20),
                    TemporalKind = CandidateWarpTemporalKind.EventPoint,
                    RegionIds = ["R3"],
                    EvidenceBoundaryState = EvidenceState.Confirmed,
                    Provenance = [privateProvenance]
                }
            ]));
    }

    [Fact]
    public void PublicTravelArrivalKeepsOnlyDestinationsPreferredWhileUnknownStopStaysOuter()
    {
        var candidates = Warp(
            InteractionTimeWindow.At(20),
            CandidateWarpTemporalKind.EventPoint,
            ["R3"]);
        var constraint = Constraint(
            EvidenceState.Confirmed,
            InteractionTimeWindow.At(20),
            ["R3"],
            availableAtTick: 20);

        var result = Apply(candidates, [constraint], currentTick: 22, PublicEnvelope());

        Assert.Equal(["R5", "R6"], result.CandidateWarps.PreferredRegionIdsAt(22));
        Assert.Equal(["R3", "R5", "R6"], result.CandidateWarps.CandidateRegionIdsAt(22));
        Assert.DoesNotContain("R3", result.CandidateWarps.PreferredRegionIdsAt(22));
    }


    [Fact]
    public void SharedForwardProjectionKeepsUndeclaredStopOuterOnly()
    {
        var candidates = PublicCandidateWarpFactory.Create(
            ScenarioId,
            Seed,
            TargetPersonId,
            [
                new PublicCandidateWarpStateDefinition
                {
                    TimeWindow = InteractionTimeWindow.At(2),
                    TemporalKind = CandidateWarpTemporalKind.EventPoint,
                    RegionIds = ["X"],
                    EvidenceBoundaryState = EvidenceState.Confirmed,
                    Provenance =
                    [
                        CandidateWarpProvenance.Create(
                            CandidateWarpSourceKind.LastConfirmed,
                            "owned-x",
                            2,
                            true,
                            false,
                            EvidenceState.Confirmed,
                            ["X"])
                    ]
                }
            ],
            CandidateWarpStanding.Preferred);
        var envelope = new PublicHazardEnvelope
        {
            HazardId = "shared-forward-stop",
            SourceRegionId = "X",
            PublishedAtTick = 0,
            Branches =
            [
                new PublicHazardBranch
                {
                    SourceRegionId = "X",
                    DestinationRegionId = "Y",
                    MinimumTravelTicks = 3,
                    MaximumTravelTicks = 3,
                    TransportType = HazardTransportType.HazardDriven,
                    ProvenanceId = "published-x-y",
                    PublishedAtTick = 0
                }
            ]
        };

        var projected = PublicCandidateWarpProjection.ForwardTo(candidates, 10, envelope);
        Assert.Equal(["Y"], projected.PreferredRegionIdsAt(10));
        Assert.Equal(["X", "Y"], projected.CandidateRegionIdsAt(10));
        var preferred = Assert.Single(projected.Warps,
            warp => warp.Standing == CandidateWarpStanding.Preferred);
        Assert.Contains(preferred.States.Single(state => state.TimeWindow.StartTick == 10).Provenance,
            item => item.SourceKind == CandidateWarpSourceKind.PublicHazardEnvelope &&
                item.SourceIdentifier == "published-x-y");
        var alternative = Assert.Single(projected.Warps,
            warp => warp.Standing == CandidateWarpStanding.OuterPlausible);
        Assert.Equal(["X", "Y"], alternative.CandidateRegionIdsAt(10));
        Assert.DoesNotContain(alternative.States.Single(state => state.TimeWindow.StartTick == 10)
                .Provenance,
            item => item.SourceKind == CandidateWarpSourceKind.PublicHazardEnvelope &&
                item.RegionIds.Contains("X", StringComparer.Ordinal));
        Assert.Equal(["X"], PublicCandidateWarpProjection.ForwardTo(
            candidates, 10, null).CandidateRegionIdsAt(10));
        Assert.Equal(JsonDefaults.Serialize(projected),
            JsonDefaults.Serialize(PublicCandidateWarpProjection.ForwardTo(
                candidates, 10, envelope)));
    }

    // #98 / AR-6: a published X->Y is a possible movement, not an observed departure.
    [Fact]
    public void PublishedHazardRouteRetainsUndeclaredStopOnlyInOuter()
    {
        var candidates = Warp(InteractionTimeWindow.At(2),
            CandidateWarpTemporalKind.EventPoint, ["X"]);
        var confirmed = Constraint(EvidenceState.Confirmed,
            InteractionTimeWindow.At(2), ["X"], availableAtTick: 2);
        var envelope = new PublicHazardEnvelope
        {
            HazardId = "published-possible-departure",
            SourceRegionId = "X",
            PublishedAtTick = 0,
            Branches =
            [
                new PublicHazardBranch
                {
                    SourceRegionId = "X", DestinationRegionId = "Y",
                    MinimumTravelTicks = 3, MaximumTravelTicks = 3,
                    TransportType = HazardTransportType.HazardDriven,
                    ProvenanceId = "public-x-y", PublishedAtTick = 0
                }
            ]
        };

        var atObservation = Apply(candidates, [confirmed], 2, envelope);
        var atQuery = Apply(candidates, [confirmed], 10, envelope);
        var missingPublicModel = Apply(candidates, [confirmed], 10, null);
        Assert.Equal(["X"], atObservation.CandidateWarps.CandidateRegionIdsAt(2));
        Assert.Equal(["Y"], atQuery.CandidateWarps.PreferredRegionIdsAt(10));
        Assert.Equal(["X", "Y"], atQuery.CandidateWarps.CandidateRegionIdsAt(10));
        Assert.Equal(["X", "Y"], atQuery.CandidateWarps.OuterPlausibleRegionIdsAt(10));
        Assert.Equal(["X"], missingPublicModel.CandidateWarps.CandidateRegionIdsAt(10));
        var preferred = Assert.Single(atQuery.CandidateWarps.Warps,
            item => item.Standing == CandidateWarpStanding.Preferred);
        Assert.Contains(preferred.States.Single(state => state.TimeWindow.StartTick == 10).Provenance,
            item => item.SourceKind == CandidateWarpSourceKind.PublicHazardEnvelope &&
                    item.SourceIdentifier == "public-x-y");
        var outer = Assert.Single(atQuery.CandidateWarps.Warps,
            item => item.Standing == CandidateWarpStanding.OuterPlausible);
        Assert.Equal(["X"], outer.CandidateRegionIdsAt(2));
        Assert.Equal(ConstraintApplicationDisposition.Narrow,
            Assert.Single(atQuery.Audits).Disposition);
        Assert.Equal(JsonDefaults.Serialize(atQuery),
            JsonDefaults.Serialize(Apply(candidates, [confirmed], 10, envelope)));
    }

    [Fact]
    public void MultiplePublishedBranchesRetainStopAndEveryReachableDestination()
    {
        var candidates = Warp(InteractionTimeWindow.At(2),
            CandidateWarpTemporalKind.EventPoint, ["X"]);
        var confirmed = Constraint(EvidenceState.Confirmed,
            InteractionTimeWindow.At(2), ["X"], availableAtTick: 2);
        var branches = new[]
        {
            new PublicHazardBranch
            {
                SourceRegionId = "X", DestinationRegionId = "Z",
                MinimumTravelTicks = 2, MaximumTravelTicks = 4,
                TransportType = HazardTransportType.HazardDriven,
                ProvenanceId = "public-x-z", PublishedAtTick = 0
            },
            new PublicHazardBranch
            {
                SourceRegionId = "X", DestinationRegionId = "Y",
                MinimumTravelTicks = 3, MaximumTravelTicks = 3,
                TransportType = HazardTransportType.HazardDriven,
                ProvenanceId = "public-x-y", PublishedAtTick = 0
            }
        };
        var publicModel = new PublicHazardEnvelope
        {
            HazardId = "published-fork",
            SourceRegionId = "X",
            PublishedAtTick = 0,
            Branches = branches
        };
        var reversed = publicModel with { Branches = branches.Reverse().ToArray() };
        var result = Apply(candidates, [confirmed], 10, publicModel);
        Assert.Equal(["Y", "Z"], result.CandidateWarps.PreferredRegionIdsAt(10));
        Assert.Equal(["X", "Y", "Z"], result.CandidateWarps.CandidateRegionIdsAt(10));
        Assert.Equal(JsonDefaults.Serialize(result),
            JsonDefaults.Serialize(Apply(candidates, [confirmed], 10, reversed)));
    }

    [Fact]
    public void CandidateWarpConstraintAndAuditOrderingAreDeterministic()
    {
        var firstWarp = Warp(
            InteractionTimeWindow.At(20),
            CandidateWarpTemporalKind.EventPoint,
            ["R4", "R2", "R3"],
            reverseProvenance: false);
        var secondWarp = Warp(
            InteractionTimeWindow.At(20),
            CandidateWarpTemporalKind.EventPoint,
            ["R3", "R4", "R2"],
            reverseProvenance: true);
        var firstConstraint = Constraint(
            EvidenceState.SupportedButUncertain,
            InteractionTimeWindow.At(20),
            ["R3"],
            sourceEvidenceIdentifier: "source-b");
        var secondConstraint = Constraint(
            EvidenceState.Indeterminate,
            InteractionTimeWindow.At(20),
            ["R2"],
            sourceEvidenceIdentifier: "source-a");

        var first = Apply(
            firstWarp,
            [firstConstraint, secondConstraint],
            currentTick: 25,
            PublicEnvelope(reverse: false));
        var second = Apply(
            secondWarp,
            [secondConstraint, firstConstraint],
            currentTick: 25,
            PublicEnvelope(reverse: true));

        Assert.Equal(JsonDefaults.Serialize(first), JsonDefaults.Serialize(second));
    }


    // #96: positive published reachability cannot prove unavailable paths absent.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PartialPublishedCausalPathsKeepUnknownOuterInBothDirections(bool interval)
    {
        var earlyWindow = interval
            ? new InteractionTimeWindow(2, 3) : InteractionTimeWindow.At(2);
        var laterWindow = interval
            ? new InteractionTimeWindow(5, 6) : InteractionTimeWindow.At(5);
        var provenance = CandidateWarpProvenance.Create(
            CandidateWarpSourceKind.VoluntaryReachability, "public-path-fixture",
            0, false, false, EvidenceState.SupportedButUncertain, ["A", "B", "C", "D", "E", "F"]);
        var warps = PublicCandidateWarpFactory.Create(ScenarioId, Seed, TargetPersonId,
        [
            new PublicCandidateWarpStateDefinition
            {
                TimeWindow = earlyWindow,
                TemporalKind = interval ? CandidateWarpTemporalKind.StateInterval : CandidateWarpTemporalKind.EventPoint,
                RegionIds = ["A", "B", "C"],
                EvidenceBoundaryState = EvidenceState.SupportedButUncertain,
                Provenance = [provenance]
            },
            new PublicCandidateWarpStateDefinition
            {
                TimeWindow = laterWindow,
                TemporalKind = interval ? CandidateWarpTemporalKind.StateInterval : CandidateWarpTemporalKind.EventPoint,
                RegionIds = ["D", "E", "F"],
                EvidenceBoundaryState = EvidenceState.SupportedButUncertain,
                Provenance = [provenance]
            }
        ]);
        var envelope = new PublicHazardEnvelope
        {
            HazardId = "partial-public-tracing",
            SourceRegionId = "A",
            PublishedAtTick = 0,
            Branches =
            [
                new PublicHazardBranch
                {
                    SourceRegionId = "A", DestinationRegionId = "D",
                    MinimumTravelTicks = 3, MaximumTravelTicks = 3,
                    TransportType = HazardTransportType.HazardDriven,
                    ProvenanceId = "published-a-d", PublishedAtTick = 0
                }
            ]
        };
        var access = new CandidateWarpAccessContext
        {
            ScenarioId = ScenarioId,
            Seed = Seed,
            TargetEntityId = TargetPersonId,
            CurrentTick = 6,
            QueryContextId = "partial-public-direct-tracing"
        };
        var tracer = new OperationalCausalTracer();
        var partial = tracer.Trace(warps, [], access, envelope);
        var missing = tracer.Trace(warps, [], access, null);

        Assert.Equal(["A", "B", "C"], partial.CandidateWarps.OuterPlausibleRegionIdsAt(2));
        Assert.Equal(["D", "E", "F"], partial.CandidateWarps.OuterPlausibleRegionIdsAt(5));
        Assert.Equal(missing.CandidateWarps.OuterPlausibleRegionIdsAt(2),
            partial.CandidateWarps.OuterPlausibleRegionIdsAt(2));
        Assert.Equal(missing.CandidateWarps.OuterPlausibleRegionIdsAt(5),
            partial.CandidateWarps.OuterPlausibleRegionIdsAt(5));
        Assert.Contains(partial.Steps, step =>
            step.Direction == CausalTracingDirection.Forward && step.RealTransitionTraversalOccurred);
        Assert.Contains(partial.Steps, step =>
            step.Direction == CausalTracingDirection.Backward && step.RealTransitionTraversalOccurred);
        Assert.DoesNotContain(partial.Steps, step => step.ConflictDetected);
        Assert.Contains("B", partial.CandidateWarps.OuterPlausibleRegionIdsAt(2));
        Assert.Equal(JsonDefaults.Serialize(partial),
            JsonDefaults.Serialize(tracer.Trace(warps, [], access, envelope)));
    }

    // An actual received Confirmed constraint still narrows a historical outer.
    [Fact]
    public void PartialPublishedPathsDoNotUndoConfirmedHistoricalOuter()
    {
        var provenance = CandidateWarpProvenance.Create(
            CandidateWarpSourceKind.VoluntaryReachability, "confirmed-path-fixture",
            0, false, false, EvidenceState.SupportedButUncertain, ["A", "B", "C", "D", "E", "F"]);
        var warps = PublicCandidateWarpFactory.Create(ScenarioId, Seed, TargetPersonId,
        [
            new PublicCandidateWarpStateDefinition
            {
                TimeWindow = InteractionTimeWindow.At(2),
                TemporalKind = CandidateWarpTemporalKind.EventPoint,
                RegionIds = ["A", "B", "C"],
                EvidenceBoundaryState = EvidenceState.SupportedButUncertain,
                Provenance = [provenance]
            },
            new PublicCandidateWarpStateDefinition
            {
                TimeWindow = InteractionTimeWindow.At(5),
                TemporalKind = CandidateWarpTemporalKind.EventPoint,
                RegionIds = ["D", "E", "F"],
                EvidenceBoundaryState = EvidenceState.SupportedButUncertain,
                Provenance = [provenance]
            }
        ]);
        var confirmed = CausalTracingConstraint.Create(
            ScenarioId, Seed, "source-observation", "original-source", ["original-source"],
            TargetPersonId, TracingConstraintSourceKind.CrossThreadObservation, "person-person",
            [
                Participant(TargetPersonId, InteractionEntityKind.Person),
                Participant("person-b", InteractionEntityKind.Person)
            ],
            TracingConstraintRole.Local, InteractionTimeWindow.At(2),
            InteractionTimeWindow.At(2), ["A", "B"], 3, EvidenceState.Confirmed,
            true, false, InteractionVisibility.PublicEvidence, "confirmed-derivation",
            [new InteractionProvenance(InteractionProvenanceKind.ExternalSource, "original-source")]);
        var access = new CandidateWarpAccessContext
        {
            ScenarioId = ScenarioId,
            Seed = Seed,
            TargetEntityId = TargetPersonId,
            CurrentTick = 5,
            QueryContextId = "confirmed-partial-path"
        };
        var envelope = new PublicHazardEnvelope
        {
            HazardId = "partial-confirmed-model",
            SourceRegionId = "A",
            PublishedAtTick = 0,
            Branches =
            [
                new PublicHazardBranch
                {
                    SourceRegionId = "A", DestinationRegionId = "D",
                    MinimumTravelTicks = 3, MaximumTravelTicks = 3,
                    TransportType = HazardTransportType.HazardDriven,
                    ProvenanceId = "known-a-d", PublishedAtTick = 0
                }
            ]
        };
        var result = new OperationalCausalTracer().Trace(warps, [confirmed], access, envelope);
        Assert.Equal(["A", "B"], result.CandidateWarps.OuterPlausibleRegionIdsAt(2));
        Assert.Equal(["D", "E", "F"], result.CandidateWarps.OuterPlausibleRegionIdsAt(5));
        Assert.Contains(result.Steps, step =>
            step.Direction == CausalTracingDirection.CrossThread &&
            step.Disposition == TracingStepDisposition.Applied);
        Assert.DoesNotContain(result.Steps, step =>
            step.Direction != CausalTracingDirection.CrossThread && step.ConflictDetected);

        // AR-7: A->D is the sole published path. E/F stay outer-possible,
        // but must never inherit g1's positive candidate-level support.
        var confirmedGroup = Assert.Single(result.SourceGroups).SourceGroupId;
        Assert.Contains(result.SupportLineage, item =>
            item.RegionId == "D" && item.SupportingSourceGroupIds.Contains(confirmedGroup));
        Assert.All(result.SupportLineage.Where(item => item.RegionId is "E" or "F"), item =>
        {
            Assert.DoesNotContain(confirmedGroup, item.SupportingSourceGroupIds);
            Assert.DoesNotContain(confirmed.ConstraintId, item.ConstraintIds);
        });

        // A second independent conflicting record must remain visible in
        // competing source lineage after a positive published transition.
        var competing = CausalTracingConstraint.Create(
            ScenarioId, Seed, "competing-observation", "competing-source", ["competing-source"],
            TargetPersonId, TracingConstraintSourceKind.CrossThreadObservation, "person-person",
            [
                Participant(TargetPersonId, InteractionEntityKind.Person),
                Participant("person-c", InteractionEntityKind.Person)
            ],
            TracingConstraintRole.Local, InteractionTimeWindow.At(2),
            InteractionTimeWindow.At(2), ["C"], 3, EvidenceState.Conflicting,
            true, false, InteractionVisibility.PublicEvidence, "competing-derivation",
            [new InteractionProvenance(InteractionProvenanceKind.ExternalSource, "competing-source")]);
        var withConflict = new OperationalCausalTracer().Trace(
            warps, [confirmed, competing], access, envelope);
        var competingGroup = Assert.Single(withConflict.SourceGroups, group =>
            group.ConstraintIds.Contains(competing.ConstraintId)).SourceGroupId;
        Assert.Equal(["D", "E", "F"], withConflict.CandidateWarps.OuterPlausibleRegionIdsAt(5));
        Assert.Contains(withConflict.SupportLineage, item =>
            item.RegionId == "D" && item.CompetingSourceGroupIds.Contains(competingGroup));
        Assert.All(withConflict.SupportLineage.Where(item => item.RegionId is "E" or "F"), item =>
            Assert.DoesNotContain(confirmedGroup, item.SupportingSourceGroupIds));
    }

    // A shared descendant must not reflect an A-only observation into B.
    // This tests positive support and outer possibility separately.
    [Fact]
    public void ConvergingPublishedPathsCannotReverseHistoricalObservationSupport()
    {
        var provenance = CandidateWarpProvenance.Create(
            CandidateWarpSourceKind.VoluntaryReachability, "converging-public-fixture",
            0, false, false, EvidenceState.SupportedButUncertain, ["A", "B", "D"]);
        var warps = PublicCandidateWarpFactory.Create(ScenarioId, Seed, TargetPersonId,
        [
            new PublicCandidateWarpStateDefinition
            {
                TimeWindow = InteractionTimeWindow.At(2),
                TemporalKind = CandidateWarpTemporalKind.EventPoint,
                RegionIds = ["A", "B"],
                EvidenceBoundaryState = EvidenceState.SupportedButUncertain,
                Provenance = [provenance]
            },
            new PublicCandidateWarpStateDefinition
            {
                TimeWindow = InteractionTimeWindow.At(5),
                TemporalKind = CandidateWarpTemporalKind.EventPoint,
                RegionIds = ["D"],
                EvidenceBoundaryState = EvidenceState.SupportedButUncertain,
                Provenance = [provenance]
            }
        ]);
        var observation = CausalTracingConstraint.Create(
            ScenarioId, Seed, "observed-a", "source-a", ["source-a"],
            TargetPersonId, TracingConstraintSourceKind.CrossThreadObservation, "person-person",
            [
                Participant(TargetPersonId, InteractionEntityKind.Person),
                Participant("person-b", InteractionEntityKind.Person)
            ],
            TracingConstraintRole.Local, InteractionTimeWindow.At(2),
            InteractionTimeWindow.At(2), ["A"], 3, EvidenceState.SupportedButUncertain,
            true, false, InteractionVisibility.PublicEvidence, "converging-derivation",
            [new InteractionProvenance(InteractionProvenanceKind.ExternalSource, "source-a")]);
        var access = new CandidateWarpAccessContext
        {
            ScenarioId = ScenarioId,
            Seed = Seed,
            TargetEntityId = TargetPersonId,
            CurrentTick = 5,
            QueryContextId = "converging-published-paths"
        };
        var envelope = new PublicHazardEnvelope
        {
            HazardId = "converging-model",
            SourceRegionId = "A",
            PublishedAtTick = 0,
            Branches =
            [
                new PublicHazardBranch
                {
                    SourceRegionId = "A", DestinationRegionId = "D",
                    MinimumTravelTicks = 3, MaximumTravelTicks = 3,
                    TransportType = HazardTransportType.HazardDriven,
                    ProvenanceId = "a-d", PublishedAtTick = 0
                },
                new PublicHazardBranch
                {
                    SourceRegionId = "B", DestinationRegionId = "D",
                    MinimumTravelTicks = 3, MaximumTravelTicks = 3,
                    TransportType = HazardTransportType.HazardDriven,
                    ProvenanceId = "b-d", PublishedAtTick = 0
                }
            ]
        };

        var tracer = new OperationalCausalTracer();
        var result = tracer.Trace(warps, [observation], access, envelope);
        var groupId = Assert.Single(result.SourceGroups).SourceGroupId;
        var atA = Assert.Single(result.SupportLineage, entry => entry.RegionId == "A");
        var atB = Assert.Single(result.SupportLineage, entry => entry.RegionId == "B");
        var atD = Assert.Single(result.SupportLineage, entry => entry.RegionId == "D");
        Assert.Contains(groupId, atA.SupportingSourceGroupIds);
        Assert.Contains(observation.ConstraintId, atA.ConstraintIds);
        Assert.Empty(atB.SupportingSourceGroupIds);
        Assert.Empty(atB.ConstraintIds);
        Assert.Contains(groupId, atD.SupportingSourceGroupIds);
        Assert.Contains(observation.ConstraintId, atD.ConstraintIds);
        Assert.Equal(["A", "B"], result.CandidateWarps.OuterPlausibleRegionIdsAt(2));
        Assert.Equal(["D"], result.CandidateWarps.OuterPlausibleRegionIdsAt(5));
        var outer = Assert.Single(result.CandidateWarps.Warps, warp =>
            warp.Standing == CandidateWarpStanding.OuterPlausible);
        var historical = Assert.Single(outer.States, state => state.TimeWindow.StartTick == 2);
        Assert.DoesNotContain(historical.Provenance, item =>
            item.SourceKind == CandidateWarpSourceKind.CrossThreadObservation);
        Assert.Equal(JsonDefaults.Serialize(result),
            JsonDefaults.Serialize(tracer.Trace(warps, [observation], access, envelope)));
    }

    private static CandidateWarpSet Warp(
        InteractionTimeWindow timeWindow,
        CandidateWarpTemporalKind temporalKind,
        IReadOnlyList<string> regions,
        bool reverseProvenance = false)
    {
        var provenance = new[]
        {
            CandidateWarpProvenance.Create(
                CandidateWarpSourceKind.VoluntaryReachability,
                "public-voluntary-model",
                availableAtTick: 0,
                isPersonEvidence: false,
                isDeviceOnlyEvidence: false,
                EvidenceState.SupportedButUncertain,
                regions),
            CandidateWarpProvenance.Create(
                CandidateWarpSourceKind.LastConfirmed,
                "public-last-confirmed",
                availableAtTick: 0,
                isPersonEvidence: true,
                isDeviceOnlyEvidence: false,
                EvidenceState.Confirmed,
                regions)
        };
        return PublicCandidateWarpFactory.Create(
            ScenarioId,
            Seed,
            TargetPersonId,
            [
                new PublicCandidateWarpStateDefinition
                {
                    TimeWindow = timeWindow,
                    TemporalKind = temporalKind,
                    RegionIds = regions,
                    EvidenceBoundaryState = EvidenceState.SupportedButUncertain,
                    Provenance = reverseProvenance ? provenance.Reverse().ToArray() : provenance
                }
            ]);
    }

    private static CrossThreadConstraint Constraint(
        EvidenceState state,
        InteractionTimeWindow timeWindow,
        IReadOnlyList<string> regionIds,
        long availableAtTick = 23,
        string sourceEvidenceIdentifier = "encounter-source",
        InteractionVisibility visibility = InteractionVisibility.PublicEvidence)
    {
        var observation = InteractionObservation.Create(
            ScenarioId,
            Seed,
            sourceEvidenceIdentifier,
            referencedInteractionId: null,
            InteractionRelationKind.PersonPersonEncounter,
            [
                Participant(TargetPersonId, InteractionEntityKind.Person),
                Participant("person-b", InteractionEntityKind.Person)
            ],
            timeWindow,
            regionIds,
            availableAtTick,
            state,
            isPersonEvidence: true,
            isDeviceOnlyEvidence: false,
            visibility,
            [
                new InteractionProvenance(
                    InteractionProvenanceKind.ExternalSource,
                    "fixture-witness")
            ]);
        return Assert.IsType<CrossThreadConstraint>(
            CrossThreadConstraintDeriver.DerivePersonEncounterCoLocation(
                observation,
                TargetPersonId));
    }

    private static CandidateWarpApplicationResult Apply(
        CandidateWarpSet candidateWarps,
        IEnumerable<CrossThreadConstraint> constraints,
        long currentTick,
        PublicHazardEnvelope? envelope = null) =>
        new CrossThreadConstraintApplicator().Apply(
            candidateWarps,
            constraints,
            new CandidateWarpAccessContext
            {
                ScenarioId = ScenarioId,
                Seed = Seed,
                TargetEntityId = TargetPersonId,
                CurrentTick = currentTick,
                QueryContextId = "fixture-public-search"
            },
            envelope);

    private static PublicHazardEnvelope PublicEnvelope(bool reverse = false)
    {
        var branches = new[]
        {
            new PublicHazardBranch
            {
                SourceRegionId = "R3",
                DestinationRegionId = "R6",
                MinimumTravelTicks = 2,
                MaximumTravelTicks = 2,
                TransportType = HazardTransportType.HazardDriven,
                ProvenanceId = "public-r3-r6",
                PublishedAtTick = 0
            },
            new PublicHazardBranch
            {
                SourceRegionId = "R3",
                DestinationRegionId = "R5",
                MinimumTravelTicks = 2,
                MaximumTravelTicks = 2,
                TransportType = HazardTransportType.HazardDriven,
                ProvenanceId = "public-r3-r5",
                PublishedAtTick = 0
            }
        };
        return new PublicHazardEnvelope
        {
            HazardId = "public-fixture-hazard",
            SourceRegionId = "R3",
            PublishedAtTick = 0,
            Branches = reverse ? branches.Reverse().ToArray() : branches
        };
    }

    private static InteractionParticipant Participant(
        string entityId,
        InteractionEntityKind kind) => new(
            entityId,
            kind,
            InteractionParticipantRole.EncounterParticipant);

    private static bool ContainsType(Type candidate, Type prohibited)
    {
        if (candidate == prohibited)
        {
            return true;
        }

        return candidate.IsGenericType && candidate.GetGenericArguments().Any(argument =>
            ContainsType(argument, prohibited));
    }

    private const string ScenarioId = "candidate-warp-fixture";
    private const int Seed = 17;
    private const string TargetPersonId = "person-a";
}
