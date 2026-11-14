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
    // A published route is not proof of departure, and a destination with a
    // later minimum arrival cannot be used as a positive current location.
    [Theory]
    [InlineData(2, false)]
    [InlineData(3, false)]
    [InlineData(4, true)]
    [InlineData(5, true)]
    public void ForwardProjectionBeforeMinimumArrivalKeepsUnknownOuter(
        long queryTick, bool destinationAvailable)
    {
        var source = MinimumArrivalSource();
        var envelope = MinimumArrivalEnvelope();
        var projected = PublicCandidateWarpProjection.ForwardTo(
            source, queryTick, envelope);

        Assert.Contains("a", projected.CandidateRegionIdsAt(0));
        Assert.Contains("a", projected.OuterPlausibleRegionIdsAt(queryTick));
        if (destinationAvailable)
        {
            Assert.Contains("b", projected.PreferredRegionIdsAt(queryTick));
        }
        else
        {
            Assert.DoesNotContain("b", projected.CandidateRegionIdsAt(queryTick));
            Assert.Empty(projected.PreferredRegionIdsAt(queryTick));
        }

        Assert.Equal(JsonDefaults.Serialize(projected),
            JsonDefaults.Serialize(PublicCandidateWarpProjection.ForwardTo(
                source, queryTick, envelope)));
        Assert.All(projected.Warps, warp =>
            Assert.Contains(warp.States.SelectMany(state => state.Provenance),
                sourceProvenance => sourceProvenance.SourceIdentifier ==
                    "minimum-arrival-source"));
    }

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    public void ReceivedConfirmationBeforeMinimumArrivalDoesNotThrowOrPreferStop(
        long queryTick)
    {
        var candidates = Warp(InteractionTimeWindow.At(0),
            CandidateWarpTemporalKind.EventPoint, ["a"]);
        var confirmed = Constraint(EvidenceState.Confirmed,
            InteractionTimeWindow.At(0), ["a"], 0, "minimum-arrival-receipt");
        var envelope = MinimumArrivalEnvelope();

        var applied = Apply(candidates, [confirmed], queryTick, envelope);
        Assert.Contains("a", applied.CandidateWarps.CandidateRegionIdsAt(0));
        Assert.Contains("a", applied.CandidateWarps.OuterPlausibleRegionIdsAt(queryTick));
        Assert.DoesNotContain("b",
            applied.CandidateWarps.CandidateRegionIdsAt(queryTick));
        Assert.DoesNotContain("a",
            applied.CandidateWarps.PreferredRegionIdsAt(queryTick));
        Assert.Equal(JsonDefaults.Serialize(applied.CandidateWarps),
            JsonDefaults.Serialize(Apply(candidates,
                [confirmed, confirmed], queryTick, envelope).CandidateWarps));
    }

    [Fact]
    public void UnreachableDestinationDoesNotEraseOtherPublishedBranch()
    {
        var projected = PublicCandidateWarpProjection.ForwardTo(
            MinimumArrivalSource(), 2,
            MinimumArrivalEnvelope(includeReachableAlternative: true));

        Assert.Contains("c", projected.PreferredRegionIdsAt(2));
        Assert.Contains("c", projected.CandidateRegionIdsAt(2));
        Assert.Contains("a", projected.OuterPlausibleRegionIdsAt(2));
        Assert.DoesNotContain("b", projected.CandidateRegionIdsAt(2));
        Assert.DoesNotContain("a", projected.PreferredRegionIdsAt(2));
    }

    [Fact]
    public void UnpublishedMinimumArrivalBranchCannotStrengthenEarlyQuery()
    {
        var source = MinimumArrivalSource();
        var envelope = MinimumArrivalEnvelope(branchPublishedAtTick: 3);
        var early = PublicCandidateWarpProjection.ForwardTo(source, 2, envelope);
        var later = PublicCandidateWarpProjection.ForwardTo(source, 4, envelope);

        Assert.DoesNotContain("b", early.CandidateRegionIdsAt(2));
        Assert.Contains("b", later.PreferredRegionIdsAt(4));
        Assert.Equal(JsonDefaults.Serialize(early),
            JsonDefaults.Serialize(PublicCandidateWarpProjection.ForwardTo(
                source, 2, envelope)));
    }

    private static CandidateWarpSet MinimumArrivalSource() =>
        PublicCandidateWarpFactory.Create(
            ScenarioId, Seed, TargetPersonId,
            [
                new PublicCandidateWarpStateDefinition
                {
                    TimeWindow = InteractionTimeWindow.At(0),
                    TemporalKind = CandidateWarpTemporalKind.EventPoint,
                    RegionIds = ["a"],
                    EvidenceBoundaryState = EvidenceState.Confirmed,
                    Provenance =
                    [
                        CandidateWarpProvenance.Create(
                            CandidateWarpSourceKind.LastConfirmed,
                            "minimum-arrival-source", 0, true, false,
                            EvidenceState.Confirmed, ["a"])
                    ]
                }
            ], CandidateWarpStanding.Preferred);

    private static PublicHazardEnvelope MinimumArrivalEnvelope(
        bool includeReachableAlternative = false,
        long branchPublishedAtTick = 0)
    {
        var branches = new List<PublicHazardBranch>
        {
            new()
            {
                SourceRegionId = "a", DestinationRegionId = "b",
                MinimumTravelTicks = 1, MaximumTravelTicks = 1,
                TransportType = HazardTransportType.HazardDriven,
                ProvenanceId = "minimum-arrival-a-b",
                PublishedAtTick = branchPublishedAtTick
            }
        };
        if (includeReachableAlternative)
        {
            branches.Add(new PublicHazardBranch
            {
                SourceRegionId = "a",
                DestinationRegionId = "c",
                MinimumTravelTicks = 1,
                MaximumTravelTicks = 1,
                TransportType = HazardTransportType.HazardDriven,
                ProvenanceId = "minimum-arrival-a-c",
                PublishedAtTick = 0
            });
        }

        return new PublicHazardEnvelope
        {
            HazardId = "minimum-arrival-public-model",
            SourceRegionId = "a",
            PublishedAtTick = 0,
            Regions =
            [
                new PublicHazardRegion
                {
                    RegionId = "a", PublishedAtTick = 0,
                    MinimumArrivalTick = 0, MaximumArrivalTick = 0
                },
                new PublicHazardRegion
                {
                    RegionId = "b", PublishedAtTick = 0,
                    MinimumArrivalTick = 4, MaximumArrivalTick = 4
                }
            ],
            Branches = branches
        };
    }

    // AR-62 derivative: a partly shared region is not a mandatory
    // simultaneous witness when one encounter covers an inclusive window.
    [Theory]
    [InlineData(3, false, EvidenceState.Confirmed)]
    [InlineData(4, true, EvidenceState.Confirmed)]
    [InlineData(5, false, EvidenceState.Confirmed)]
    [InlineData(3, false, EvidenceState.SupportedButUncertain)]
    public void PartiallySharedIntervalAndExactKeepDistinctWitnesses(
        int exactTick, bool withEnvelope, EvidenceState exactEvidence)
    {
        var initial = Warp(new InteractionTimeWindow(0, 5),
            CandidateWarpTemporalKind.StateInterval, ["a", "b", "c"]);
        var interval = Constraint(EvidenceState.Confirmed,
            new InteractionTimeWindow(2, exactTick == 5 ? 5 : 4),
            ["a", "b"], 6, "ar62-derivative-interval");
        var exact = Constraint(exactEvidence, InteractionTimeWindow.At(exactTick),
            ["b", "c"], 6, "ar62-derivative-exact");
        var envelope = withEnvelope ? new PublicHazardEnvelope
        {
            HazardId = "ar62-derivative-route",
            SourceRegionId = "a",
            PublishedAtTick = 0,
            Branches =
            [
                new PublicHazardBranch
                {
                    SourceRegionId = "a", DestinationRegionId = "c",
                    MinimumTravelTicks = 1, MaximumTravelTicks = 1,
                    TransportType = HazardTransportType.HazardDriven,
                    ProvenanceId = "ar62-derivative-a-c", PublishedAtTick = 0
                }
            ]
        } : null;
        var result = Apply(initial, [interval, exact], 6, envelope);
        Assert.Contains("a", result.CandidateWarps.CandidateRegionIdsAt(2));
        Assert.Contains("c", result.CandidateWarps.CandidateRegionIdsAt(exactTick));
        Assert.Contains("c", result.CandidateWarps.CandidateRegionIdsAt(6));
        if (!withEnvelope)
        {
            Assert.DoesNotContain("c",
                result.CandidateWarps.PreferredRegionIdsAt(6));
        }
        if (exactEvidence == EvidenceState.SupportedButUncertain)
        {
            Assert.Contains("a", result.CandidateWarps.CandidateRegionIdsAt(exactTick));
        }
        Assert.DoesNotContain(result.Audits, audit => audit.ConflictDetected);
        Assert.Equal(JsonDefaults.Serialize(result),
            JsonDefaults.Serialize(Apply(initial, [exact, interval], 6, envelope)));
        Assert.Equal(JsonDefaults.Serialize(result.CandidateWarps),
            JsonDefaults.Serialize(Apply(initial,
                [interval, exact, exact], 6, envelope).CandidateWarps));
    }

    [Theory]
    [InlineData(EvidenceState.Confirmed)]
    [InlineData(EvidenceState.SupportedButUncertain)]
    public void PartiallySharedExactAtInclusiveStartKeepsLaterWitness(
        EvidenceState intervalEvidence)
    {
        var initial = Warp(new InteractionTimeWindow(0, 5),
            CandidateWarpTemporalKind.StateInterval, ["a", "b", "c"]);
        var exact = Constraint(EvidenceState.Confirmed,
            InteractionTimeWindow.At(2), ["a", "b"], 6,
            "ar62-derivative-first-exact");
        var interval = Constraint(intervalEvidence,
            new InteractionTimeWindow(2, 4), ["b", "c"], 6,
            "ar62-derivative-later-interval");
        var result = Apply(initial, [exact, interval], 6);
        Assert.Contains("a", result.CandidateWarps.CandidateRegionIdsAt(2));
        Assert.Contains("c", result.CandidateWarps.CandidateRegionIdsAt(3));
        Assert.Contains("c", result.CandidateWarps.CandidateRegionIdsAt(6));
        Assert.DoesNotContain(result.Audits, audit => audit.ConflictDetected);
        Assert.Equal(JsonDefaults.Serialize(result),
            JsonDefaults.Serialize(Apply(initial, [interval, exact], 6)));
    }

    // Ready-stage Codex P2: merging sparse outer histories must not erase
    // a branch whose last preceding state differs from another branch.
    [Fact]
    public void SparseOuterUnionPreservesEachBranchTemporalFallback()
    {
        var publicSource = CandidateWarpProvenance.Create(
            CandidateWarpSourceKind.VoluntaryReachability,
            "outer-fallback-public", 0, false, false,
            EvidenceState.SupportedButUncertain, ["a", "b"]);
        var historical = PublicCandidateWarpFactory.Create(
            ScenarioId, Seed, TargetPersonId,
            [
                new PublicCandidateWarpStateDefinition
                {
                    TimeWindow = InteractionTimeWindow.At(0),
                    TemporalKind = CandidateWarpTemporalKind.EventPoint,
                    RegionIds = ["a"],
                    EvidenceBoundaryState = EvidenceState.SupportedButUncertain,
                    Provenance = [publicSource]
                },
                new PublicCandidateWarpStateDefinition
                {
                    TimeWindow = InteractionTimeWindow.At(2),
                    TemporalKind = CandidateWarpTemporalKind.EventPoint,
                    RegionIds = ["b"],
                    EvidenceBoundaryState = EvidenceState.SupportedButUncertain,
                    Provenance = [publicSource]
                }
            ]);
        var confirmed = Constraint(EvidenceState.Confirmed,
            InteractionTimeWindow.At(0), ["a"], 4, "outer-fallback-a");
        var uncertain = Constraint(EvidenceState.SupportedButUncertain,
            InteractionTimeWindow.At(2), ["b"], 4, "outer-fallback-b");
        var result = Apply(historical, [confirmed, uncertain], 4);

        // The uncertain b@2 can be false, so a@3 is still possible;
        // b@3 also remains possible if it was true. These are not a
        // combined preferred path or independent copies of one source.
        Assert.Contains("a", result.CandidateWarps.OuterPlausibleRegionIdsAt(3));
        Assert.Contains("b", result.CandidateWarps.OuterPlausibleRegionIdsAt(3));
        Assert.Contains("a", result.CandidateWarps.CandidateRegionIdsAt(4));
        Assert.Contains("b", result.CandidateWarps.CandidateRegionIdsAt(4));
        Assert.DoesNotContain("b", result.CandidateWarps.PreferredRegionIdsAt(3));
        Assert.Equal(JsonDefaults.Serialize(result),
            JsonDefaults.Serialize(Apply(historical,
                [uncertain, confirmed], 4)));
        var copied = Apply(historical,
            [confirmed, uncertain, uncertain], 4);
        Assert.Equal(JsonDefaults.Serialize(result.CandidateWarps),
            JsonDefaults.Serialize(copied.CandidateWarps));
    }

    // AR-59: an inclusive encounter is true at SOME tick, not every tick.
    // A separate exact confirmation inside its window must not be discarded.
    [Theory]
    [InlineData(EvidenceState.Confirmed)]
    [InlineData(EvidenceState.SupportedButUncertain)]
    public void InclusiveEncounterCanCoexistWithDifferentExactConfirmation(
        EvidenceState intervalState)
    {
        PublicHazardBranch Edge(string from, string to) => new()
        {
            SourceRegionId = from,
            DestinationRegionId = to,
            MinimumTravelTicks = 1,
            MaximumTravelTicks = 1,
            TransportType = HazardTransportType.HazardDriven,
            ProvenanceId = $"ar59-{from}-{to}",
            PublishedAtTick = 0
        };
        var envelope = new PublicHazardEnvelope
        {
            HazardId = "ar59-public",
            SourceRegionId = "a",
            PublishedAtTick = 0,
            Branches =
            [
                Edge("a", "b"), Edge("b", "c"), Edge("b", "y"),
                Edge("c", "d"), Edge("y", "d")
            ]
        };
        var initial = Warp(new InteractionTimeWindow(0, 1),
            CandidateWarpTemporalKind.StateInterval, ["a", "b"]);
        var first = Constraint(EvidenceState.Confirmed,
            InteractionTimeWindow.At(0), ["a"], 5, "ar59-a");
        var inclusive = Constraint(intervalState,
            new InteractionTimeWindow(2, 4), ["y"], 5, "ar59-y");
        var exact = Constraint(EvidenceState.Confirmed,
            InteractionTimeWindow.At(3), ["d"], 5, "ar59-d");
        var result = Apply(initial, [first, inclusive, exact], 5, envelope);
        Assert.Contains("d", result.CandidateWarps.CandidateRegionIdsAt(3));
        Assert.Contains("d", result.CandidateWarps.CandidateRegionIdsAt(5));
        Assert.DoesNotContain(result.Audits, audit =>
            audit.SourceObservationId == exact.SourceObservationId &&
            audit.ConflictDetected);
        Assert.DoesNotContain(result.CandidateWarps.Warps, warp =>
            warp.Standing == CandidateWarpStanding.Conflicted);
        Assert.Equal(JsonDefaults.Serialize(result),
            JsonDefaults.Serialize(Apply(initial,
                [exact, first, inclusive], 5, envelope)));
    }

    [Fact]
    public void InclusiveEncounterWithoutPublishedRouteKeepsLaterExactOuter()
    {
        var initial = Warp(new InteractionTimeWindow(0, 4),
            CandidateWarpTemporalKind.StateInterval, ["a", "b", "c", "d", "y"]);
        var inclusive = Constraint(EvidenceState.Confirmed,
            new InteractionTimeWindow(2, 4), ["y"], 5, "ar59-direct-y");
        var exact = Constraint(EvidenceState.Confirmed,
            InteractionTimeWindow.At(3), ["d"], 5, "ar59-direct-d");
        var result = Apply(initial, [inclusive, exact], 5);
        Assert.Contains("d", result.CandidateWarps.CandidateRegionIdsAt(3));
        Assert.Contains("d", result.CandidateWarps.CandidateRegionIdsAt(5));
        Assert.DoesNotContain(result.Audits, audit =>
            audit.SourceObservationId == exact.SourceObservationId &&
            audit.ConflictDetected);
    }

    // AR-61: two interval receipts can have different witness ticks even
    // when their windows overlap (or share just one endpoint).
    [Theory]
    [InlineData(false, false, EvidenceState.Confirmed)]
    [InlineData(false, true, EvidenceState.Confirmed)]
    [InlineData(true, false, EvidenceState.Confirmed)]
    [InlineData(true, true, EvidenceState.Confirmed)]
    [InlineData(false, false, EvidenceState.SupportedButUncertain)]
    [InlineData(false, true, EvidenceState.SupportedButUncertain)]
    [InlineData(true, false, EvidenceState.SupportedButUncertain)]
    [InlineData(true, true, EvidenceState.SupportedButUncertain)]
    public void OverlappingInclusiveReceiptsRetainDistinctWitnessTicks(
        bool sharedEndpoint, bool publishedRoute, EvidenceState laterState)
    {
        var initial = Warp(new InteractionTimeWindow(0, 5),
            CandidateWarpTemporalKind.StateInterval, ["y", "z"]);
        var publicEnvelope = new PublicHazardEnvelope
        {
            HazardId = "ar61-public-y-z",
            SourceRegionId = "y",
            PublishedAtTick = 0,
            Branches =
            [
                new PublicHazardBranch
                {
                    SourceRegionId = "y", DestinationRegionId = "z",
                    MinimumTravelTicks = 1, MaximumTravelTicks = 1,
                    TransportType = HazardTransportType.HazardDriven,
                    ProvenanceId = "ar61-y-z", PublishedAtTick = 0
                }
            ]
        };
        var laterWindow = sharedEndpoint
            ? new InteractionTimeWindow(4, 6)
            : new InteractionTimeWindow(3, 5);
        for (var idOrder = 0; idOrder < 2; idOrder++)
        {
            var early = Constraint(EvidenceState.Confirmed,
                new InteractionTimeWindow(2, 4), ["y"], 6,
                idOrder == 0 ? "ar61-a-y" : "ar61-z-y");
            var later = Constraint(laterState, laterWindow, ["z"], 6,
                idOrder == 0 ? "ar61-z-z" : "ar61-a-z");
            var model = publishedRoute ? publicEnvelope : null;
            var result = Apply(initial, [early, later], 6, model);
            Assert.Contains("y", result.CandidateWarps.CandidateRegionIdsAt(2));
            Assert.Contains("z", result.CandidateWarps.CandidateRegionIdsAt(
                sharedEndpoint ? 5 : 3));
            Assert.Contains("z", result.CandidateWarps.CandidateRegionIdsAt(6));
            Assert.DoesNotContain(result.CandidateWarps.Warps, warp =>
                warp.Standing == CandidateWarpStanding.Conflicted);
            Assert.DoesNotContain(result.Audits, audit => audit.ConflictDetected);
            Assert.Contains(result.Audits, audit =>
                audit.SourceObservationId == later.SourceObservationId &&
                audit.Disposition == ConstraintApplicationDisposition.RetainAlternative);
            Assert.Contains(result.CandidateWarps.Warps
                .SelectMany(warp => warp.States)
                .SelectMany(state => state.Provenance), source =>
                    source.SourceObservationId == later.SourceObservationId &&
                    source.SourceTimeWindow == later.ConstrainedTimeWindow);
            Assert.Equal(JsonDefaults.Serialize(result),
                JsonDefaults.Serialize(Apply(initial, [later, early], 6, model)));
            var copy = Apply(initial, [early, later, later], 6, model);
            Assert.Equal(JsonDefaults.Serialize(result.CandidateWarps),
                JsonDefaults.Serialize(copy.CandidateWarps));
        }
    }

    // AR-62: overlapping inclusive receipts may each be witnessed at
    // different ticks, even if their allowed region sets share one region.
    // The common b is not proof of continuous residence there.
    [Theory]
    [InlineData(false, false, EvidenceState.Confirmed)]
    [InlineData(false, true, EvidenceState.Confirmed)]
    [InlineData(true, false, EvidenceState.Confirmed)]
    [InlineData(true, true, EvidenceState.Confirmed)]
    [InlineData(false, false, EvidenceState.SupportedButUncertain)]
    [InlineData(false, true, EvidenceState.SupportedButUncertain)]
    [InlineData(true, false, EvidenceState.SupportedButUncertain)]
    [InlineData(true, true, EvidenceState.SupportedButUncertain)]
    public void PartiallySharedInclusiveRegionsPreserveDistinctWitnesses(
        bool sameWindow, bool publishedRoute, EvidenceState laterState)
    {
        var initial = Warp(new InteractionTimeWindow(0, 5),
            CandidateWarpTemporalKind.StateInterval, ["a", "b", "c"]);
        var publicEnvelope = new PublicHazardEnvelope
        {
            HazardId = "ar62-public-a-c",
            SourceRegionId = "a",
            PublishedAtTick = 0,
            Branches =
            [
                new PublicHazardBranch
                {
                    SourceRegionId = "a", DestinationRegionId = "c",
                    MinimumTravelTicks = 1, MaximumTravelTicks = 1,
                    TransportType = HazardTransportType.HazardDriven,
                    ProvenanceId = "ar62-a-c", PublishedAtTick = 0
                }
            ]
        };
        var laterWindow = sameWindow
            ? new InteractionTimeWindow(2, 4)
            : new InteractionTimeWindow(3, 5);
        for (var idOrder = 0; idOrder < 2; idOrder++)
        {
            var first = Constraint(EvidenceState.Confirmed,
                new InteractionTimeWindow(2, 4), ["a", "b"], 6,
                idOrder == 0 ? "ar62-a-first" : "ar62-z-first");
            var second = Constraint(laterState, laterWindow, ["b", "c"], 6,
                idOrder == 0 ? "ar62-z-second" : "ar62-a-second");
            var model = publishedRoute ? publicEnvelope : null;
            var result = Apply(initial, [first, second], 6, model);
            Assert.Contains("a", result.CandidateWarps.CandidateRegionIdsAt(2));
            Assert.Contains("c", result.CandidateWarps.CandidateRegionIdsAt(
                sameWindow ? 4 : 5));
            Assert.Contains("c", result.CandidateWarps.CandidateRegionIdsAt(6));
            // A weak interval can be processed before the independent
            // Confirmed one. A published a->c path may then support c
            // independently; without that path, c must remain outer-only.
            if (laterState == EvidenceState.Confirmed || !publishedRoute)
            {
                Assert.DoesNotContain("c", result.CandidateWarps.PreferredRegionIdsAt(6));
            }
            Assert.DoesNotContain(result.CandidateWarps.Warps, warp =>
                warp.Standing == CandidateWarpStanding.Conflicted);
            Assert.DoesNotContain(result.Audits, audit => audit.ConflictDetected);
            // Same-window observations can apply in either source-ID order.
            // The conservative retention may be audited on either receipt.
            Assert.Contains(result.Audits, audit =>
                (audit.SourceObservationId == first.SourceObservationId ||
                 audit.SourceObservationId == second.SourceObservationId) &&
                audit.Disposition == ConstraintApplicationDisposition.RetainAlternative);
            Assert.Contains(result.CandidateWarps.Warps
                .SelectMany(warp => warp.States)
                .SelectMany(state => state.Provenance), source =>
                    source.SourceObservationId == second.SourceObservationId &&
                    source.SourceTimeWindow == second.ConstrainedTimeWindow);
            Assert.Equal(JsonDefaults.Serialize(result),
                JsonDefaults.Serialize(Apply(initial, [second, first], 6, model)));
            var withCopy = Apply(initial, [first, second, second], 6, model);
            Assert.Equal(JsonDefaults.Serialize(result.CandidateWarps),
                JsonDefaults.Serialize(withCopy.CandidateWarps));
        }
    }

    // AR-59 reverse boundary: d@2 followed by y@[2,4] may mean y@3.
    // Neither a missing public edge nor a changed receipt/source ordering can
    // convert an existential interval into a same-tick hard contradiction.
    [Theory]
    [InlineData(EvidenceState.Confirmed, true)]
    [InlineData(EvidenceState.Confirmed, false)]
    [InlineData(EvidenceState.SupportedButUncertain, true)]
    [InlineData(EvidenceState.SupportedButUncertain, false)]
    public void ExactAtInclusiveStartRetainsLaterWitnessAsOuter(
        EvidenceState intervalState, bool publishedRoute)
    {
        var initial = Warp(new InteractionTimeWindow(0, 2),
            CandidateWarpTemporalKind.StateInterval, ["d"]);
        var envelope = new PublicHazardEnvelope
        {
            HazardId = "ar59-reverse-public",
            SourceRegionId = "d",
            PublishedAtTick = 0,
            Branches =
            [
                new PublicHazardBranch
                {
                    SourceRegionId = "d",
                    DestinationRegionId = "y",
                    MinimumTravelTicks = 1,
                    MaximumTravelTicks = 1,
                    TransportType = HazardTransportType.HazardDriven,
                    ProvenanceId = "ar59-reverse-d-y",
                    PublishedAtTick = 0
                }
            ]
        };
        for (var idOrder = 0; idOrder < 2; idOrder++)
        {
            var exact = Constraint(EvidenceState.Confirmed,
                InteractionTimeWindow.At(2), ["d"], 5,
                idOrder == 0 ? "ar59-reverse-a-exact" : "ar59-reverse-z-exact");
            var inclusive = Constraint(intervalState,
                new InteractionTimeWindow(2, 4), ["y"], 5,
                idOrder == 0 ? "ar59-reverse-z-inclusive" : "ar59-reverse-a-inclusive");
            var publicModel = publishedRoute ? envelope : null;
            var result = Apply(initial, [exact, inclusive], 5, publicModel);
            Assert.Contains("d", result.CandidateWarps.CandidateRegionIdsAt(2));
            Assert.Contains("y", result.CandidateWarps.CandidateRegionIdsAt(3));
            Assert.Contains("y", result.CandidateWarps.CandidateRegionIdsAt(5));
            Assert.DoesNotContain("y", result.CandidateWarps.PreferredRegionIdsAt(5));
            Assert.DoesNotContain(result.CandidateWarps.Warps, warp =>
                warp.Standing == CandidateWarpStanding.Conflicted);
            Assert.All(result.Audits, audit => Assert.False(audit.ConflictDetected));
            Assert.Contains(result.Audits, audit =>
                audit.SourceObservationId == inclusive.SourceObservationId &&
                audit.Disposition == ConstraintApplicationDisposition.RetainAlternative);
            Assert.Contains(result.CandidateWarps.Warps.SelectMany(warp => warp.States)
                .SelectMany(state => state.Provenance), source =>
                    source.SourceObservationId == inclusive.SourceObservationId &&
                    source.SourceTimeWindow == inclusive.ConstrainedTimeWindow);
            Assert.Equal(JsonDefaults.Serialize(result),
                JsonDefaults.Serialize(Apply(initial, [inclusive, exact], 5, publicModel)));
            var withCopy = Apply(initial, [exact, inclusive, inclusive], 5, publicModel);
            Assert.Equal(JsonDefaults.Serialize(result.CandidateWarps),
                JsonDefaults.Serialize(withCopy.CandidateWarps));
        }
    }

    [Fact]
    public void TwoDifferentExactConfirmationsAtInclusiveStartStillConflict()
    {
        var initial = Warp(new InteractionTimeWindow(0, 2),
            CandidateWarpTemporalKind.StateInterval, ["d", "y"]);
        var exactD = Constraint(EvidenceState.Confirmed,
            InteractionTimeWindow.At(2), ["d"], 5, "ar59-exact-d");
        var exactY = Constraint(EvidenceState.Confirmed,
            InteractionTimeWindow.At(2), ["y"], 5, "ar59-exact-y");
        var result = Apply(initial, [exactD, exactY], 5);
        Assert.All(result.CandidateWarps.Warps, warp =>
            Assert.Equal(CandidateWarpStanding.Conflicted, warp.Standing));
        Assert.Contains(result.Audits, audit => audit.ConflictDetected);
    }

    // AR-60: source IDs may decide processing order, but not which of two
    // opposing exact confirmations remains in the conservative candidate set.
    [Theory]
    [InlineData(EvidenceState.Confirmed)]
    [InlineData(EvidenceState.SupportedButUncertain)]
    public void ContradictorySameTickConfirmedKeepBothRegionsAcrossSourceIds(
        EvidenceState thirdState)
    {
        var initial = Warp(new InteractionTimeWindow(0, 2),
            CandidateWarpTemporalKind.StateInterval, ["a", "b"]);
        for (var suffix = 0; suffix < 6; suffix++)
        {
            var first = Constraint(EvidenceState.Confirmed,
                InteractionTimeWindow.At(0), ["a"], 2, $"ar60-a-{suffix}");
            var second = Constraint(EvidenceState.Confirmed,
                InteractionTimeWindow.At(0), ["b"], 2, $"ar60-b-{suffix}");
            var third = Constraint(thirdState,
                InteractionTimeWindow.At(0), ["a"], 2, $"ar60-third-{suffix}");
            var result = Apply(initial, [first, second, third], 2);
            Assert.Contains("a", result.CandidateWarps.CandidateRegionIdsAt(0));
            Assert.Contains("b", result.CandidateWarps.CandidateRegionIdsAt(0));
            Assert.Contains("a", result.CandidateWarps.CandidateRegionIdsAt(2));
            Assert.Contains("b", result.CandidateWarps.CandidateRegionIdsAt(2));
            Assert.Empty(result.CandidateWarps.PreferredRegionIdsAt(2));
            Assert.All(result.CandidateWarps.Warps, warp =>
                Assert.Equal(CandidateWarpStanding.Conflicted, warp.Standing));
            Assert.Contains(result.Audits, audit => audit.ConflictDetected);
            Assert.Equal(JsonDefaults.Serialize(result),
                JsonDefaults.Serialize(Apply(initial,
                    [third, second, first], 2)));
            var duplicate = Apply(initial, [first, second, third, third], 2);
            Assert.Equal(result.CandidateWarps.CandidateRegionIdsAt(2),
                duplicate.CandidateWarps.CandidateRegionIdsAt(2));
        }
    }

    // Codex P2 / AR-58: an inclusive receipt synthesized in a historical
    // gap must not be mistaken for its own predecessor. Published edges are
    // needed to promote an independent later region into preferred.
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void GapReceiptCannotVerifyItsOwnUnpublishedTransition(
        bool hasEnvelope, bool inclusive)
    {
        var initial = Warp(new InteractionTimeWindow(0, 1),
            CandidateWarpTemporalKind.StateInterval, ["a", "b"]);
        var first = Constraint(EvidenceState.Confirmed,
            InteractionTimeWindow.At(0), ["a"], 5, "ar58-first");
        var later = Constraint(EvidenceState.Confirmed,
            inclusive ? new InteractionTimeWindow(2, 3) :
                InteractionTimeWindow.At(2), ["x"], 5, "ar58-later");
        PublicHazardEnvelope? envelope = hasEnvelope
            ? new PublicHazardEnvelope
            {
                HazardId = "ar58-no-path-to-x",
                SourceRegionId = "a",
                PublishedAtTick = 0,
                Branches =
                [
                    new PublicHazardBranch
                    {
                        SourceRegionId = "a",
                        DestinationRegionId = "b",
                        MinimumTravelTicks = 1,
                        MaximumTravelTicks = 1,
                        TransportType = HazardTransportType.HazardDriven,
                        ProvenanceId = "ar58-a-b",
                        PublishedAtTick = 0
                    }
                ]
            }
            : null;
        var result = Apply(initial, [first, later], 5, envelope);
        Assert.DoesNotContain("x", result.CandidateWarps.PreferredRegionIdsAt(5));
        Assert.Contains("x", result.CandidateWarps.CandidateRegionIdsAt(5));
        Assert.Equal(JsonDefaults.Serialize(result),
            JsonDefaults.Serialize(Apply(initial, [later, first], 5, envelope)));
    }

    // AR-57: an uncertain, possibly wrong interval encounter must not
    // convert a later independent Confirmed fact into a global conflict.
    // The exact-tick form must not silently ignore the later receipt either.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void WeakGapCannotConflictWithLaterIndependentConfirmed(bool inclusive)
    {
        var initial = Warp(new InteractionTimeWindow(0, 1),
            CandidateWarpTemporalKind.StateInterval, ["a", "b"]);
        PublicHazardBranch Edge(string from, string to) => new()
        {
            SourceRegionId = from,
            DestinationRegionId = to,
            MinimumTravelTicks = 1,
            MaximumTravelTicks = 1,
            TransportType = HazardTransportType.HazardDriven,
            ProvenanceId = $"ar57-{from}-{to}",
            PublishedAtTick = 0
        };
        var envelope = new PublicHazardEnvelope
        {
            HazardId = "ar57-published-path",
            SourceRegionId = "a",
            PublishedAtTick = 0,
            Branches = [Edge("a", "b"), Edge("b", "c"), Edge("c", "d")]
        };
        var first = Constraint(EvidenceState.Confirmed,
            InteractionTimeWindow.At(0), ["a"], 5, "ar57-first");
        var weak = Constraint(EvidenceState.SupportedButUncertain,
            inclusive ? new InteractionTimeWindow(2, 4) :
                InteractionTimeWindow.At(2), ["y"], 5, "ar57-weak");
        var last = Constraint(EvidenceState.Confirmed,
            InteractionTimeWindow.At(3), ["d"], 5, "ar57-last");
        var result = Apply(initial, [first, weak, last], 5, envelope);
        Assert.Contains("d", result.CandidateWarps.CandidateRegionIdsAt(3));
        Assert.Contains("d", result.CandidateWarps.CandidateRegionIdsAt(5));
        Assert.Contains(result.CandidateWarps.Warps, warp =>
            warp.Standing == CandidateWarpStanding.OuterPlausible &&
            warp.CandidateRegionIdsAt(3).Contains("d", StringComparer.Ordinal));
        Assert.DoesNotContain(result.Audits, audit =>
            audit.ConstraintId == last.ConstraintId && audit.ConflictDetected);
        Assert.Equal(JsonDefaults.Serialize(result),
            JsonDefaults.Serialize(Apply(initial, [last, weak, first], 5, envelope)));
    }

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


    // AR-55: a received encounter outside the original history window must
    // be checked against a public path, rather than discarded as a generic gap.
    [Fact]
    public void SparseHistoricalGapDistinguishesReachableAndUnreachableReceipts()
    {
        PublicHazardBranch Edge(string from, string to) => new()
        {
            SourceRegionId = from,
            DestinationRegionId = to,
            MinimumTravelTicks = 1,
            MaximumTravelTicks = 1,
            TransportType = HazardTransportType.HazardDriven,
            ProvenanceId = $"ar55-{from}-{to}",
            PublishedAtTick = 0
        };
        var envelope = new PublicHazardEnvelope
        {
            HazardId = "ar55-public",
            SourceRegionId = "a",
            PublishedAtTick = 0,
            Branches = [Edge("a", "b"), Edge("b", "c")]
        };
        var historical = Warp(new InteractionTimeWindow(0, 1),
            CandidateWarpTemporalKind.StateInterval, ["a", "b"]);
        var first = Constraint(EvidenceState.Confirmed,
            InteractionTimeWindow.At(0), ["a"], 5, "ar55-first");
        var reachable = Constraint(EvidenceState.Confirmed,
            InteractionTimeWindow.At(2), ["c"], 5, "ar55-reachable");
        var missing = Constraint(EvidenceState.Confirmed,
            InteractionTimeWindow.At(2), ["x"], 5, "ar55-missing");

        var baseline = Apply(historical, [first], 5, envelope);
        var supported = Apply(historical, [first, reachable], 5, envelope);
        var unknown = Apply(historical, [first, missing], 5, envelope);
        Assert.Contains("c", baseline.CandidateWarps.PreferredRegionIdsAt(5));
        Assert.Contains("c", supported.CandidateWarps.PreferredRegionIdsAt(2));
        Assert.Contains("c", supported.CandidateWarps.PreferredRegionIdsAt(5));
        Assert.Contains(supported.Audits, audit =>
            audit.SourceObservationId == reachable.SourceObservationId &&
            audit.Reason == ConstraintApplicationReason.Applied &&
            audit.Disposition == ConstraintApplicationDisposition.Narrow);
        Assert.DoesNotContain("x", unknown.CandidateWarps.PreferredRegionIdsAt(2));
        Assert.Contains("x", unknown.CandidateWarps.OuterPlausibleRegionIdsAt(2));
        Assert.DoesNotContain("c", unknown.CandidateWarps.PreferredRegionIdsAt(5));
        Assert.NotEqual(JsonDefaults.Serialize(supported.CandidateWarps),
            JsonDefaults.Serialize(unknown.CandidateWarps));
        Assert.Equal(JsonDefaults.Serialize(supported),
            JsonDefaults.Serialize(Apply(historical, [reachable, first], 5, envelope)));
        var duplicate = Apply(historical, [first, reachable, reachable], 5, envelope);
        Assert.Equal(supported.CandidateWarps.PreferredRegionIdsAt(5),
            duplicate.CandidateWarps.PreferredRegionIdsAt(5));
        Assert.Contains(duplicate.Audits, audit =>
            audit.Reason == ConstraintApplicationReason.DuplicateSourceObservation);
    }

    // AR-56: learning of a secondary-transport edge at cutoff 5 does NOT
    // imply that its destination with public minimum arrival 4 was reached at 1.
    [Fact]
    public void HistoricalArrivalCannotPrecedePublishedDestinationMinimum()
    {
        var envelope = new PublicHazardEnvelope
        {
            HazardId = "ar56-secondary",
            SourceRegionId = "a",
            PublishedAtTick = 0,
            Regions =
            [
                new PublicHazardRegion
                {
                    RegionId = "a",
                    PublishedAtTick = 0,
                    MinimumArrivalTick = 0,
                    MaximumArrivalTick = 0
                },
                new PublicHazardRegion
                {
                    RegionId = "b",
                    PublishedAtTick = 4,
                    MinimumArrivalTick = 4,
                    MaximumArrivalTick = 4,
                    TransportTypes = [HazardTransportType.SecondaryTransport]
                }
            ],
            Branches =
            [
                new PublicHazardBranch
                {
                    SourceRegionId = "a",
                    DestinationRegionId = "b",
                    MinimumTravelTicks = 1,
                    MaximumTravelTicks = 1,
                    TransportType = HazardTransportType.SecondaryTransport,
                    ProvenanceId = "ar56-a-b",
                    PublishedAtTick = 4
                }
            ]
        };
        var historical = Warp(new InteractionTimeWindow(0, 1),
            CandidateWarpTemporalKind.StateInterval, ["a", "b"]);
        var first = Constraint(EvidenceState.Confirmed,
            InteractionTimeWindow.At(0), ["a"], 5, "ar56-first");
        var early = Constraint(EvidenceState.Confirmed,
            InteractionTimeWindow.At(1), ["b"], 5, "ar56-too-early");
        var result = Apply(historical, [first, early], 5, envelope);
        Assert.DoesNotContain("b", result.CandidateWarps.PreferredRegionIdsAt(1));
        Assert.Contains("b", result.CandidateWarps.OuterPlausibleRegionIdsAt(1));
        Assert.Equal(JsonDefaults.Serialize(result),
            JsonDefaults.Serialize(Apply(historical, [early, first], 5, envelope)));
    }

    // The query's public-information cutoff is not the historical tick being
    // reconstructed. With no Regions/onset information, this fixture assumes
    // an already-existing route merely learned later; it cannot prove that
    // such a route physically existed before publication. The same route
    // published after the query cannot contribute to this conditional model.
    [Fact]
    public void HistoricalPublishedRouteUsesReceiptCutoffForVerificationAndTails()
    {
        PublicHazardEnvelope PublicAt(long publishedAtTick) => new()
        {
            HazardId = "historical-publication-cutoff",
            SourceRegionId = "a",
            PublishedAtTick = 0,
            Branches =
            [
                new PublicHazardBranch
                {
                    SourceRegionId = "a",
                    DestinationRegionId = "b",
                    MinimumTravelTicks = 1,
                    MaximumTravelTicks = 1,
                    TransportType = HazardTransportType.HazardDriven,
                    ProvenanceId = "later-published-a-b",
                    PublishedAtTick = publishedAtTick
                }
            ]
        };

        var historical = Warp(new InteractionTimeWindow(0, 1),
            CandidateWarpTemporalKind.StateInterval, ["a", "b"]);
        var first = Constraint(EvidenceState.Confirmed,
            InteractionTimeWindow.At(0), ["a"], 3, "late-public-first");
        var second = Constraint(EvidenceState.Confirmed,
            InteractionTimeWindow.At(1), ["b"], 3, "late-public-second");

        // Physically traversable by tick 1, published at tick 2, and known
        // to the rescuer by cutoff 3. Do not use the encounter tick as cutoff.
        var available = Apply(historical, [first, second], 3, PublicAt(2));
        Assert.Contains("b", available.CandidateWarps.PreferredRegionIdsAt(1));
        Assert.Contains("b", available.CandidateWarps.PreferredRegionIdsAt(3));
        Assert.Equal(JsonDefaults.Serialize(available),
            JsonDefaults.Serialize(Apply(historical,
                [second, first], 3, PublicAt(2))));

        // The same physical route is not available to a cutoff-3 query if
        // it is published only at tick 4. The encounter remains outer-only.
        var futurePublication = Apply(historical,
            [first, second], 3, PublicAt(4));
        Assert.DoesNotContain("b",
            futurePublication.CandidateWarps.PreferredRegionIdsAt(1));
        Assert.Contains("b",
            futurePublication.CandidateWarps.OuterPlausibleRegionIdsAt(1));

        // The raw historical StateInterval tail must use the same evidence
        // cutoff: otherwise it remains an unknown outer possibility.
        var tailVerified = Apply(historical, [first], 3, PublicAt(2));
        var tailUnknown = Apply(historical, [first], 3, PublicAt(4));
        Assert.Contains("b", tailVerified.CandidateWarps.PreferredRegionIdsAt(1));
        Assert.DoesNotContain("b",
            tailUnknown.CandidateWarps.PreferredRegionIdsAt(1));
        Assert.Contains("b",
            tailUnknown.CandidateWarps.OuterPlausibleRegionIdsAt(1));
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


    // Ready-stage Codex: an unrelated later interval cannot survive at the
    // query tick alongside propagation from the newly confirmed earlier state.
    [Fact]
    public void NonOverlappingLaterIntervalIsClippedBeforeQueryProjection()
    {
        var publicSource = CandidateWarpProvenance.Create(
            CandidateWarpSourceKind.VoluntaryReachability,
            "ar49-public-model", 0, false, false,
            EvidenceState.SupportedButUncertain, ["a", "b", "c"]);
        var historical = PublicCandidateWarpFactory.Create(
            ScenarioId, Seed, TargetPersonId,
            [
                new PublicCandidateWarpStateDefinition
                {
                    TimeWindow = InteractionTimeWindow.At(0),
                    TemporalKind = CandidateWarpTemporalKind.EventPoint,
                    RegionIds = ["a"],
                    EvidenceBoundaryState = EvidenceState.SupportedButUncertain,
                    Provenance = [publicSource]
                },
                new PublicCandidateWarpStateDefinition
                {
                    TimeWindow = new InteractionTimeWindow(1, 3),
                    TemporalKind = CandidateWarpTemporalKind.StateInterval,
                    RegionIds = ["b", "c"],
                    EvidenceBoundaryState = EvidenceState.SupportedButUncertain,
                    Provenance = [publicSource]
                }
            ], CandidateWarpStanding.Preferred);
        var envelope = new PublicHazardEnvelope
        {
            HazardId = "ar49-clipping",
            SourceRegionId = "a",
            PublishedAtTick = 0,
            Branches =
            [
                new PublicHazardBranch
                {
                    SourceRegionId = "a", DestinationRegionId = "b",
                    MinimumTravelTicks = 1, MaximumTravelTicks = 1,
                    TransportType = HazardTransportType.HazardDriven,
                    ProvenanceId = "ar49-a-b", PublishedAtTick = 0
                }
            ]
        };
        var result = Apply(historical,
            [Constraint(EvidenceState.Confirmed, InteractionTimeWindow.At(0),
                ["a"], 3, "ar49-confirmed")], 3, envelope);
        Assert.DoesNotContain("c", result.CandidateWarps.PreferredRegionIdsAt(3));
        Assert.Contains("b", result.CandidateWarps.PreferredRegionIdsAt(3));
        Assert.DoesNotContain(result.CandidateWarps.Warps
            .Where(w => w.Standing == CandidateWarpStanding.Preferred)
            .SelectMany(w => w.States), s =>
            s.TimeWindow.EndTick >= 3 && s.RegionIds.Contains("c"));
    }

    // Arrival at tick 2 does not verify a location at tick 1 or authorize a
    // two-tick trip from that location to finish at query tick 3.
    [Fact]
    public void InclusiveEncounterKeepsPublicArrivalTickBound()
    {
        var historical = Warp(new InteractionTimeWindow(0, 2),
            CandidateWarpTemporalKind.StateInterval, ["a", "d", "f"]);
        var envelope = new PublicHazardEnvelope
        {
            HazardId = "ar50-time-bounded",
            SourceRegionId = "a",
            PublishedAtTick = 0,
            Branches =
            [
                new PublicHazardBranch
                {
                    SourceRegionId = "a", DestinationRegionId = "d",
                    MinimumTravelTicks = 2, MaximumTravelTicks = 2,
                    TransportType = HazardTransportType.HazardDriven,
                    ProvenanceId = "ar50-a-d", PublishedAtTick = 0
                },
                new PublicHazardBranch
                {
                    SourceRegionId = "d", DestinationRegionId = "f",
                    MinimumTravelTicks = 2, MaximumTravelTicks = 2,
                    TransportType = HazardTransportType.HazardDriven,
                    ProvenanceId = "ar50-d-f", PublishedAtTick = 0
                }
            ]
        };
        var result = Apply(historical,
        [
            Constraint(EvidenceState.Confirmed, InteractionTimeWindow.At(0),
                ["a"], 3, "ar50-at-zero"),
            Constraint(EvidenceState.Confirmed, new InteractionTimeWindow(1, 2),
                ["d"], 3, "ar50-later-window")
        ], 3, envelope);
        Assert.DoesNotContain("f", result.CandidateWarps.PreferredRegionIdsAt(3));
        Assert.Contains("d", result.CandidateWarps.PreferredRegionIdsAt(3));
        Assert.Contains("f", result.CandidateWarps.OuterPlausibleRegionIdsAt(3));
    }

    // Independent review: b is on a historical EventPoint only at tick 1,
    // although the declared a->b route first reaches b at tick 2. Checking
    // regions and arrival times separately must neither throw nor certify
    // an impossible preferred historical encounter; unknown travel stays outer.
    [Fact]
    public void InclusiveEncounterNeedsRegionAndArrivalInTheSameState()
    {
        var publicSource = CandidateWarpProvenance.Create(
            CandidateWarpSourceKind.VoluntaryReachability,
            "ar51-public-model", 0, false, false,
            EvidenceState.SupportedButUncertain, ["a", "b", "c"]);
        var historical = PublicCandidateWarpFactory.Create(
            ScenarioId, Seed, TargetPersonId,
            [
                new PublicCandidateWarpStateDefinition
                {
                    TimeWindow = InteractionTimeWindow.At(0),
                    TemporalKind = CandidateWarpTemporalKind.EventPoint,
                    RegionIds = ["a"],
                    EvidenceBoundaryState = EvidenceState.SupportedButUncertain,
                    Provenance = [publicSource]
                },
                new PublicCandidateWarpStateDefinition
                {
                    TimeWindow = InteractionTimeWindow.At(1),
                    TemporalKind = CandidateWarpTemporalKind.EventPoint,
                    RegionIds = ["b"],
                    EvidenceBoundaryState = EvidenceState.SupportedButUncertain,
                    Provenance = [publicSource]
                },
                new PublicCandidateWarpStateDefinition
                {
                    TimeWindow = InteractionTimeWindow.At(2),
                    TemporalKind = CandidateWarpTemporalKind.EventPoint,
                    RegionIds = ["c"],
                    EvidenceBoundaryState = EvidenceState.SupportedButUncertain,
                    Provenance = [publicSource]
                }
            ]);
        var envelope = new PublicHazardEnvelope
        {
            HazardId = "ar51-late-arrival",
            SourceRegionId = "a",
            PublishedAtTick = 0,
            Branches =
            [
                new PublicHazardBranch
                {
                    SourceRegionId = "a", DestinationRegionId = "b",
                    MinimumTravelTicks = 2, MaximumTravelTicks = 2,
                    TransportType = HazardTransportType.HazardDriven,
                    ProvenanceId = "ar51-a-b", PublishedAtTick = 0
                }
            ]
        };
        var first = Constraint(EvidenceState.Confirmed,
            InteractionTimeWindow.At(0), ["a"], 3, "ar51-first");
        var later = Constraint(EvidenceState.Confirmed,
            new InteractionTimeWindow(1, 2), ["b"], 3, "ar51-later");

        var result = Apply(historical, [first, later], 3, envelope);
        var reversed = Apply(historical, [later, first], 3, envelope);
        Assert.Equal(JsonDefaults.Serialize(result), JsonDefaults.Serialize(reversed));
        Assert.DoesNotContain("b", result.CandidateWarps.PreferredRegionIdsAt(3));
        Assert.Contains("b", result.CandidateWarps.OuterPlausibleRegionIdsAt(3));
        Assert.Contains(result.Audits, audit =>
            audit.ConstraintId == later.ConstraintId &&
            audit.Disposition == ConstraintApplicationDisposition.RetainAlternative &&
            audit.Reason == ConstraintApplicationReason.Applied);
    }

    // Two direct sightings at the same region are consistent even without
    // a published hazard graph. An unpublished move to a different region
    // remains outer-only, never a fabricated preferred pedestrian route.
    [Fact]
    public void RepeatedSameRegionWithNoEnvelopeKeepsItsPreferredCandidate()
    {
        var historical = Warp(new InteractionTimeWindow(0, 1),
            CandidateWarpTemporalKind.StateInterval, ["a", "b"]);
        var first = Constraint(EvidenceState.Confirmed,
            InteractionTimeWindow.At(0), ["a"], 2, "ar47-first");
        var second = Constraint(EvidenceState.Confirmed,
            InteractionTimeWindow.At(1), ["a"], 2, "ar47-second");
        var noEnvelope = Apply(historical, [first, second], 2);
        var irrelevant = new PublicHazardEnvelope
        {
            HazardId = "ar47-irrelevant-public",
            SourceRegionId = "x",
            PublishedAtTick = 0,
            Branches =
            [
                new PublicHazardBranch
                {
                    SourceRegionId = "x", DestinationRegionId = "y",
                    MinimumTravelTicks = 1, MaximumTravelTicks = 1,
                    TransportType = HazardTransportType.HazardDriven,
                    ProvenanceId = "ar47-x-y", PublishedAtTick = 0
                }
            ]
        };
        var irrelevantGraph = Apply(historical, [first, second], 2, irrelevant);
        Assert.Equal(["a"], noEnvelope.CandidateWarps.PreferredRegionIdsAt(2));
        Assert.Equal(noEnvelope.CandidateWarps.PreferredRegionIdsAt(2),
            irrelevantGraph.CandidateWarps.PreferredRegionIdsAt(2));

        var uncertainSecond = Apply(historical,
        [
            first, Constraint(EvidenceState.SupportedButUncertain,
                InteractionTimeWindow.At(1), ["a"], 2, "ar47-uncertain")
        ], 2);
        Assert.Contains("a", uncertainSecond.CandidateWarps.PreferredRegionIdsAt(2));
        var unpublishedMove = Apply(historical,
        [
            first, Constraint(EvidenceState.Confirmed,
                InteractionTimeWindow.At(1), ["b"], 2, "ar47-walk")
        ], 2);
        Assert.DoesNotContain("b", unpublishedMove.CandidateWarps.PreferredRegionIdsAt(2));
        Assert.Contains("b", unpublishedMove.CandidateWarps.OuterPlausibleRegionIdsAt(2));
    }

    // Query-time direct confirmation must not turn an unverified past
    // relationship into a preferred historical trajectory.
    [Fact]
    public void DirectCurrentAnchorDoesNotPromoteUnverifiedPast()
    {
        var historical = Warp(new InteractionTimeWindow(0, 2),
            CandidateWarpTemporalKind.StateInterval, ["b", "c", "e"]);
        var envelope = new PublicHazardEnvelope
        {
            HazardId = "ar48-unpublished-c-b",
            SourceRegionId = "c",
            PublishedAtTick = 0,
            Branches =
            [
                new PublicHazardBranch
                {
                    SourceRegionId = "b", DestinationRegionId = "e",
                    MinimumTravelTicks = 1, MaximumTravelTicks = 1,
                    TransportType = HazardTransportType.HazardDriven,
                    ProvenanceId = "ar48-b-e", PublishedAtTick = 0
                }
            ]
        };
        var result = Apply(historical,
        [
            Constraint(EvidenceState.Confirmed, InteractionTimeWindow.At(0),
                ["c"], 2, "ar48-zero"),
            Constraint(EvidenceState.Confirmed, InteractionTimeWindow.At(1),
                ["b"], 2, "ar48-one"),
            Constraint(EvidenceState.Confirmed, InteractionTimeWindow.At(2),
                ["e"], 2, "ar48-direct-current")
        ], 2, envelope);
        Assert.Contains("e", result.CandidateWarps.PreferredRegionIdsAt(2));
        Assert.DoesNotContain("c", result.CandidateWarps.PreferredRegionIdsAt(0));
        Assert.DoesNotContain("b", result.CandidateWarps.PreferredRegionIdsAt(1));
        Assert.Contains("c", result.CandidateWarps.OuterPlausibleRegionIdsAt(0));
    }

    // AR-49: a preferred hypothetical weak encounter is still a waypoint.
    // If its link is unpublished, neither it nor later Confirmed receipts
    // can turn the entire historical sequence into a verified route.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void UncertainIntermediateEncounterCannotBypassMissingPublicLink(
        bool oneWayPublicEdge)
    {
        var publicSource = CandidateWarpProvenance.Create(
            CandidateWarpSourceKind.VoluntaryReachability,
            "ar49-public-baseline", 0, false, false,
            EvidenceState.SupportedButUncertain, ["a", "b"]);
        var historical = PublicCandidateWarpFactory.Create(
            ScenarioId, Seed, TargetPersonId,
            Enumerable.Range(0, 3)
                .Select(tick => new PublicCandidateWarpStateDefinition
                {
                    TimeWindow = InteractionTimeWindow.At(tick),
                    TemporalKind = CandidateWarpTemporalKind.EventPoint,
                    RegionIds = ["a", "b"],
                    EvidenceBoundaryState = EvidenceState.SupportedButUncertain,
                    Provenance = [publicSource]
                })
                .ToArray());
        PublicHazardEnvelope? envelope = oneWayPublicEdge
            ? new PublicHazardEnvelope
            {
                HazardId = "ar49-one-way",
                SourceRegionId = "a",
                PublishedAtTick = 0,
                Branches =
                [
                    new PublicHazardBranch
                    {
                        SourceRegionId = "a",
                        DestinationRegionId = "b",
                        MinimumTravelTicks = 1,
                        MaximumTravelTicks = 1,
                        TransportType = HazardTransportType.HazardDriven,
                        ProvenanceId = "ar49-a-b",
                        PublishedAtTick = 0
                    }
                ]
            }
            : null;
        var first = Constraint(EvidenceState.Confirmed,
            InteractionTimeWindow.At(0), ["a"], 3, "ar49-first");
        var middleWeak = Constraint(EvidenceState.SupportedButUncertain,
            InteractionTimeWindow.At(1), ["b"], 3, "ar49-middle-weak");
        var middleStrong = Constraint(EvidenceState.Confirmed,
            InteractionTimeWindow.At(1), ["b"], 3, "ar49-middle-strong");
        var last = Constraint(EvidenceState.Confirmed,
            InteractionTimeWindow.At(2),
            oneWayPublicEdge ? ["a"] : ["a", "b"], 3, "ar49-last");

        var weak = Apply(historical, [first, middleWeak, last], 3, envelope);
        var strong = Apply(historical, [first, middleStrong, last], 3, envelope);
        Assert.Empty(weak.CandidateWarps.PreferredRegionIdsAt(3));
        Assert.Empty(strong.CandidateWarps.PreferredRegionIdsAt(3));
        Assert.Contains("b", weak.CandidateWarps.OuterPlausibleRegionIdsAt(1));
        Assert.Contains("a", weak.CandidateWarps.OuterPlausibleRegionIdsAt(2));
        Assert.Equal(JsonDefaults.Serialize(weak),
            JsonDefaults.Serialize(Apply(historical,
                [last, middleWeak, first], 3, envelope)));
    }

    // AR-51: a weak encounter at an unpublished location can be wrong.
    // Its unverified path must keep the original outer possibility, rather
    // than describing an alternative in the audit and actually discarding it.
    [Fact]
    public void WeakUnverifiedEncounterRetainsUnconstrainedOuterHistory()
    {
        var historical = Warp(new InteractionTimeWindow(0, 2),
            CandidateWarpTemporalKind.StateInterval, ["a", "b", "c"]);
        var first = Constraint(EvidenceState.Confirmed,
            InteractionTimeWindow.At(0), ["b"], 2, "ar51-first");
        var weak = Constraint(EvidenceState.SupportedButUncertain,
            InteractionTimeWindow.At(1), ["c"], 2, "ar51-weak");
        var result = Apply(historical, [first, weak], 2);

        Assert.Contains("b", result.CandidateWarps.OuterPlausibleRegionIdsAt(1));
        Assert.Contains("b", result.CandidateWarps.OuterPlausibleRegionIdsAt(2));
        Assert.Contains(result.Audits, audit =>
            audit.ConstraintId == weak.ConstraintId &&
            audit.Disposition == ConstraintApplicationDisposition.Branch &&
            audit.AlternativesRetained);
        Assert.Equal(JsonDefaults.Serialize(result),
            JsonDefaults.Serialize(Apply(historical, [weak, first], 2)));
    }

    // AR-51: after branching on an erroneous weak encounter, a later
    // independent Confirmed receipt must still find the genuine public path.
    [Fact]
    public void WrongWeakEncounterDoesNotRemoveSubsequentConfirmedPublicPath()
    {
        var historical = Warp(new InteractionTimeWindow(0, 2),
            CandidateWarpTemporalKind.StateInterval, ["a", "b", "c"]);
        var envelope = new PublicHazardEnvelope
        {
            HazardId = "ar51-published-b-a",
            SourceRegionId = "b",
            PublishedAtTick = 0,
            Branches =
            [
                new PublicHazardBranch
                {
                    SourceRegionId = "b",
                    DestinationRegionId = "a",
                    MinimumTravelTicks = 1,
                    MaximumTravelTicks = 1,
                    TransportType = HazardTransportType.HazardDriven,
                    ProvenanceId = "ar51-b-a",
                    PublishedAtTick = 0
                }
            ]
        };
        var first = Constraint(EvidenceState.Confirmed,
            InteractionTimeWindow.At(0), ["b"], 2, "ar51-first");
        var weak = Constraint(EvidenceState.SupportedButUncertain,
            InteractionTimeWindow.At(1), ["c"], 2, "ar51-a-weak");
        var later = Constraint(EvidenceState.Confirmed,
            InteractionTimeWindow.At(1), ["a"], 2, "ar51-z-confirmed");
        var result = Apply(historical, [first, weak, later], 2, envelope);

        Assert.Contains("a", result.CandidateWarps.CandidateRegionIdsAt(1));
        Assert.Contains("a", result.CandidateWarps.CandidateRegionIdsAt(2));
        Assert.Equal(JsonDefaults.Serialize(result),
            JsonDefaults.Serialize(Apply(historical, [later, weak, first], 2,
                envelope)));
    }

    // AR-52: an uncertain interval overlapping the query tick still has an
    // earlier portion. Direct current confirmation must not erase the older
    // independently received Confirmed location from history.
    [Fact]
    public void CurrentAnchorPreservesConfirmedBeforeWeakIntervalEndingAtQuery()
    {
        var historical = Warp(new InteractionTimeWindow(0, 3),
            CandidateWarpTemporalKind.StateInterval, ["a", "b", "c", "d"]);
        var envelope = new PublicHazardEnvelope
        {
            HazardId = "ar52-incomplete-public-path",
            SourceRegionId = "a",
            PublishedAtTick = 0,
            Branches =
            [
                new PublicHazardBranch
                {
                    SourceRegionId = "a",
                    DestinationRegionId = "b",
                    MinimumTravelTicks = 1,
                    MaximumTravelTicks = 1,
                    TransportType = HazardTransportType.HazardDriven,
                    ProvenanceId = "ar52-a-b",
                    PublishedAtTick = 0
                },
                new PublicHazardBranch
                {
                    SourceRegionId = "b",
                    DestinationRegionId = "c",
                    MinimumTravelTicks = 1,
                    MaximumTravelTicks = 1,
                    TransportType = HazardTransportType.HazardDriven,
                    ProvenanceId = "ar52-b-c",
                    PublishedAtTick = 0
                }
            ]
        };
        var early = Constraint(EvidenceState.Confirmed,
            InteractionTimeWindow.At(0), ["c"], 3, "ar52-early");
        var weak = Constraint(EvidenceState.SupportedButUncertain,
            new InteractionTimeWindow(2, 3), ["a", "b"], 3, "ar52-weak");
        var current = Constraint(EvidenceState.Confirmed,
            InteractionTimeWindow.At(3), ["b"], 3, "ar52-current");
        var result = Apply(historical, [early, weak, current], 3, envelope);

        Assert.Contains("c", result.CandidateWarps.CandidateRegionIdsAt(0));
        Assert.DoesNotContain("c", result.CandidateWarps.PreferredRegionIdsAt(0));
        Assert.Contains("b", result.CandidateWarps.CandidateRegionIdsAt(3));
        Assert.Equal(JsonDefaults.Serialize(result),
            JsonDefaults.Serialize(Apply(historical, [current, weak, early],
                3, envelope)));
    }

    // AR-50: stay-at-the-last-confirmed-region is a preferred modelling
    // assumption, not new location evidence. Unpublished walking remains
    // possible in outer, so this preference must not become a hard exclusion.
    [Fact]
    public void AmbiguousLaterEncounterPrefersStayButPreservesUnknownMove()
    {
        var historical = Warp(new InteractionTimeWindow(0, 1),
            CandidateWarpTemporalKind.StateInterval, ["a", "b"]);
        var later = Constraint(EvidenceState.Confirmed,
            InteractionTimeWindow.At(1), ["a", "b"], 2, "ar50-later");
        var laterOnly = Apply(historical, [later], 2);
        var withEarlier = Apply(historical,
        [
            Constraint(EvidenceState.Confirmed, InteractionTimeWindow.At(0),
                ["a"], 2, "ar50-earlier"),
            later
        ], 2);

        Assert.Contains("b", laterOnly.CandidateWarps.PreferredRegionIdsAt(2));
        Assert.Equal(["a"], withEarlier.CandidateWarps.PreferredRegionIdsAt(2));
        Assert.Contains("b", withEarlier.CandidateWarps.OuterPlausibleRegionIdsAt(2));
    }

    // Ready-stage Codex P1: a raw post-encounter StateInterval cannot
    // carry regions with no public arrival into preferred historical ticks.
    // The original interval must remain outer-only for later receipts.
    [Fact]
    public void UnverifiedHistoricalTailStaysOuterUntilPubliclyReachable()
    {
        var historical = Warp(new InteractionTimeWindow(0, 2),
            CandidateWarpTemporalKind.StateInterval, ["a", "b", "d"]);
        var first = Constraint(EvidenceState.Confirmed,
            InteractionTimeWindow.At(0), ["a"], 3, "tail-first");
        var envelope = new PublicHazardEnvelope
        {
            HazardId = "tail-limited-public",
            SourceRegionId = "a",
            PublishedAtTick = 0,
            Branches =
            [
                new PublicHazardBranch
                {
                    SourceRegionId = "a",
                    DestinationRegionId = "b",
                    MinimumTravelTicks = 1,
                    MaximumTravelTicks = 1,
                    TransportType = HazardTransportType.HazardDriven,
                    ProvenanceId = "tail-a-b",
                    PublishedAtTick = 0
                }
            ]
        };

        var result = Apply(historical, [first], 3, envelope);
        Assert.Contains("b", result.CandidateWarps.PreferredRegionIdsAt(1));
        Assert.DoesNotContain("d", result.CandidateWarps.PreferredRegionIdsAt(1));
        Assert.DoesNotContain("d", result.CandidateWarps.PreferredRegionIdsAt(2));
        Assert.Contains("d", result.CandidateWarps.OuterPlausibleRegionIdsAt(1));
        Assert.Contains("d", result.CandidateWarps.OuterPlausibleRegionIdsAt(2));
        Assert.Contains("b", result.CandidateWarps.PreferredRegionIdsAt(3));

        // A second valid receipt must still be able to use the verified
        // historical b@1 candidate, not lose it while demoting the raw tail.
        var second = Constraint(EvidenceState.Confirmed,
            InteractionTimeWindow.At(1), ["b"], 3, "tail-second");
        var combined = Apply(historical, [first, second], 3, envelope);
        Assert.Contains("b", combined.CandidateWarps.PreferredRegionIdsAt(1));
        Assert.DoesNotContain("d", combined.CandidateWarps.PreferredRegionIdsAt(1));
        // The second Confirmed b@1 excludes d at that exact tick. This
        // is not a hard exclusion from the *first* receipt alone.
        Assert.DoesNotContain("d", combined.CandidateWarps.CandidateRegionIdsAt(1));
        Assert.Equal(JsonDefaults.Serialize(combined),
            JsonDefaults.Serialize(Apply(historical, [second, first], 3, envelope)));
    }

    [Fact]
    public void NoPublishedHistoricalMoveCannotBecomePreferredTail()
    {
        var historical = Warp(new InteractionTimeWindow(0, 2),
            CandidateWarpTemporalKind.StateInterval, ["a", "b", "d"]);
        var first = Constraint(EvidenceState.Confirmed,
            InteractionTimeWindow.At(0), ["a"], 3, "tail-no-public");
        var result = Apply(historical, [first], 3);
        Assert.DoesNotContain("d", result.CandidateWarps.PreferredRegionIdsAt(1));
        Assert.DoesNotContain("b", result.CandidateWarps.PreferredRegionIdsAt(2));
        Assert.Contains("d", result.CandidateWarps.OuterPlausibleRegionIdsAt(2));
        Assert.Contains("a", result.CandidateWarps.PreferredRegionIdsAt(1));
    }

    // Ready-stage Codex P2: a later EventPoint is a possible historical
    // location, not evidence that every candidate was publicly reached.
    // Treat its unverified alternatives exactly like a raw interval tail:
    // preserve them in outer, never inside a preferred history.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void UnverifiedHistoricalEventPointStaysOuter(bool publishLink)
    {
        var source = CandidateWarpProvenance.Create(
            CandidateWarpSourceKind.VoluntaryReachability,
            "tail-point-public-model", 0, false, false,
            EvidenceState.SupportedButUncertain, ["a", "b", "d"]);
        var historical = PublicCandidateWarpFactory.Create(
            ScenarioId, Seed, TargetPersonId,
            [
                new PublicCandidateWarpStateDefinition
                {
                    TimeWindow = InteractionTimeWindow.At(0),
                    TemporalKind = CandidateWarpTemporalKind.EventPoint,
                    RegionIds = ["a"],
                    EvidenceBoundaryState = EvidenceState.SupportedButUncertain,
                    Provenance = [source]
                },
                new PublicCandidateWarpStateDefinition
                {
                    TimeWindow = InteractionTimeWindow.At(1),
                    TemporalKind = CandidateWarpTemporalKind.EventPoint,
                    RegionIds = ["b", "d"],
                    EvidenceBoundaryState = EvidenceState.SupportedButUncertain,
                    Provenance = [source]
                }
            ]);
        var envelope = new PublicHazardEnvelope
        {
            HazardId = "tail-point-published-a-b",
            SourceRegionId = "a",
            PublishedAtTick = 0,
            Branches =
            [
                new PublicHazardBranch
                {
                    SourceRegionId = "a", DestinationRegionId = "b",
                    MinimumTravelTicks = 1, MaximumTravelTicks = 1,
                    TransportType = HazardTransportType.HazardDriven,
                    ProvenanceId = "tail-point-a-b", PublishedAtTick = 0
                }
            ]
        };
        var confirmed = Constraint(EvidenceState.Confirmed,
            InteractionTimeWindow.At(0), ["a"], 2, "tail-point-confirmed");
        var result = Apply(historical, [confirmed], 2,
            publishLink ? envelope : null);

        Assert.Contains("a", result.CandidateWarps.PreferredRegionIdsAt(0));
        Assert.DoesNotContain("d", result.CandidateWarps.PreferredRegionIdsAt(1));
        Assert.Contains("d", result.CandidateWarps.OuterPlausibleRegionIdsAt(1));
        Assert.Contains("d", result.CandidateWarps.CandidateRegionIdsAt(1));
        if (publishLink)
        {
            Assert.Contains("b", result.CandidateWarps.PreferredRegionIdsAt(1));
        }
        else
        {
            Assert.DoesNotContain("b", result.CandidateWarps.PreferredRegionIdsAt(1));
        }
    }

    // AR-46: both a single public chain and a chain with an unverified
    // alternative must remain polynomial in observations, not 2^n warps.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ConfirmedEncounterChainsDoNotExplode(bool mixed)
    {
        const int n = 16;
        string G(int tick) => $"g{tick}";
        string U(int tick) => $"u{tick}";
        var allRegions = Enumerable.Range(0, n + 1)
            .SelectMany(tick => mixed
                ? new[] { G(tick), U(tick) }
                : new[] { G(tick) })
            .ToArray();
        var historical = Warp(new InteractionTimeWindow(0, n),
            CandidateWarpTemporalKind.StateInterval, allRegions);
        var branches = Enumerable.Range(0, n)
            .Select(tick => new PublicHazardBranch
            {
                SourceRegionId = G(tick),
                DestinationRegionId = G(tick + 1),
                MinimumTravelTicks = 1,
                MaximumTravelTicks = 1,
                TransportType = HazardTransportType.HazardDriven,
                ProvenanceId = $"ar46-{tick}",
                PublishedAtTick = 0
            }).ToArray();
        var envelope = new PublicHazardEnvelope
        {
            HazardId = "ar46-chain",
            SourceRegionId = G(0),
            PublishedAtTick = 0,
            Branches = branches
        };
        var received = Enumerable.Range(0, n + 1)
            .Select(tick => Constraint(
                EvidenceState.Confirmed, InteractionTimeWindow.At(tick),
                mixed ? [G(tick), U(tick)] : [G(tick)],
                n + 1, $"ar46-encounter-{tick}"))
            .ToArray();
        var result = Apply(historical, received, n + 1, envelope);
        Assert.Contains(G(n), result.CandidateWarps.PreferredRegionIdsAt(n + 1));
        Assert.Contains(G(n), result.CandidateWarps.CandidateRegionIdsAt(n + 1));
        if (mixed)
        {
            Assert.Contains(U(n), result.CandidateWarps.OuterPlausibleRegionIdsAt(n + 1));
        }

        Assert.True(result.CandidateWarps.Warps.Count <= 32);
        Assert.True(result.Audits.Count <= 200);
        Assert.True(JsonDefaults.Serialize(result).Length < 2_000_000);
        Assert.Equal(JsonDefaults.Serialize(result),
            JsonDefaults.Serialize(Apply(historical, received.Reverse(), n + 1,
                envelope)));
    }

    // Ready-stage Codex P2: two distinct confirmed encounters can overlap
    // in time without certifying any unpublished transition between them.
    [Fact]
    public void OverlappingConfirmedWindowKeepsEarlierReceiptAndUnknownLinkOuter()
    {
        var historical = Warp(new InteractionTimeWindow(0, 1),
            CandidateWarpTemporalKind.StateInterval, ["a", "b"]);
        var earlier = Constraint(EvidenceState.Confirmed,
            InteractionTimeWindow.At(0), ["a"], 2, "overlap-earlier-a");
        var inclusive = Constraint(EvidenceState.Confirmed,
            new InteractionTimeWindow(0, 1), ["b"], 2, "overlap-later-b");
        var result = Apply(historical, [earlier, inclusive], 2);
        Assert.Contains("a", result.CandidateWarps.OuterPlausibleRegionIdsAt(0));
        Assert.Contains("b", result.CandidateWarps.OuterPlausibleRegionIdsAt(2));
        Assert.DoesNotContain("b", result.CandidateWarps.PreferredRegionIdsAt(2));
        Assert.Contains(result.Audits, audit =>
            audit.SourceObservationId == inclusive.SourceObservationId &&
            audit.Disposition == ConstraintApplicationDisposition.RetainAlternative);
        Assert.Equal(JsonDefaults.Serialize(result),
            JsonDefaults.Serialize(Apply(historical, [inclusive, earlier], 2)));
        var duplicate = Apply(historical, [earlier, earlier, inclusive], 2);
        Assert.Equal(result.CandidateWarps.CandidateRegionIdsAt(2),
            duplicate.CandidateWarps.CandidateRegionIdsAt(2));
    }

    [Fact]
    public void OverlappingConfirmedWindowAllowsPublishedOneTickLink()
    {
        var historical = Warp(new InteractionTimeWindow(0, 1),
            CandidateWarpTemporalKind.StateInterval, ["a", "b"]);
        var envelope = new PublicHazardEnvelope
        {
            HazardId = "overlap-public-one-tick",
            SourceRegionId = "a",
            PublishedAtTick = 0,
            Branches =
            [
                new PublicHazardBranch
                {
                    SourceRegionId = "a", DestinationRegionId = "b",
                    MinimumTravelTicks = 1, MaximumTravelTicks = 1,
                    TransportType = HazardTransportType.HazardDriven,
                    ProvenanceId = "overlap-public-a-b", PublishedAtTick = 0
                }
            ]
        };
        var earlier = Constraint(EvidenceState.Confirmed,
            InteractionTimeWindow.At(0), ["a"], 1, "overlap-public-earlier");
        var inclusive = Constraint(EvidenceState.Confirmed,
            new InteractionTimeWindow(0, 1), ["b"], 1, "overlap-public-later");
        var result = Apply(historical, [earlier, inclusive], 1, envelope);
        Assert.Contains("a", result.CandidateWarps.PreferredRegionIdsAt(0));
        Assert.Contains("b", result.CandidateWarps.PreferredRegionIdsAt(1));
        Assert.Equal(JsonDefaults.Serialize(result),
            JsonDefaults.Serialize(Apply(historical, [inclusive, earlier], 1,
                envelope)));
    }

    [Fact]
    public void SameTickDisjointConfirmedReceiptsRemainConflicted()
    {
        var historical = Warp(InteractionTimeWindow.At(0),
            CandidateWarpTemporalKind.EventPoint, ["a", "b"]);
        var first = Constraint(EvidenceState.Confirmed,
            InteractionTimeWindow.At(0), ["a"], 1, "same-tick-a");
        var other = Constraint(EvidenceState.Confirmed,
            InteractionTimeWindow.At(0), ["b"], 1, "same-tick-b");
        var result = Apply(historical, [first, other], 1);
        Assert.Contains(result.CandidateWarps.Warps, warp =>
            warp.Standing == CandidateWarpStanding.Conflicted);
        Assert.Contains(result.Audits, audit => audit.ConflictDetected);
        Assert.Equal(JsonDefaults.Serialize(result),
            JsonDefaults.Serialize(Apply(historical, [other, first], 1)));
    }

    // Ready-after-independent Codex P2: an unverified later transition must
    // not turn two contradictory Confirmed receipts into an outer-safe route.
    // The existing conflict remains visible even when the last receipt is weak.
    [Theory]
    [InlineData(EvidenceState.Confirmed)]
    [InlineData(EvidenceState.SupportedButUncertain)]
    public void UnverifiedLaterLinkCannotClearEarlierConfirmedConflict(
        EvidenceState laterEvidence)
    {
        var historical = Warp(new InteractionTimeWindow(0, 2),
            CandidateWarpTemporalKind.StateInterval, ["a", "b", "c"]);
        var first = Constraint(EvidenceState.Confirmed,
            InteractionTimeWindow.At(0), ["a"], 2, "sticky-conflict-a");
        var incompatible = Constraint(EvidenceState.Confirmed,
            InteractionTimeWindow.At(0), ["b"], 2, "sticky-conflict-b");
        var later = Constraint(laterEvidence,
            InteractionTimeWindow.At(1), ["c"], 2, "sticky-conflict-c");

        var initialConflict = Apply(historical, [first, incompatible], 2);
        Assert.Contains(initialConflict.CandidateWarps.Warps,
            warp => warp.Standing == CandidateWarpStanding.Conflicted);

        var result = Apply(historical, [first, incompatible, later], 2);
        Assert.All(result.CandidateWarps.Warps,
            warp => Assert.Equal(CandidateWarpStanding.Conflicted, warp.Standing));
        Assert.Contains(result.Audits, audit =>
            audit.ConstraintId == later.ConstraintId && audit.ConflictDetected);
        Assert.Equal(JsonDefaults.Serialize(result),
            JsonDefaults.Serialize(Apply(historical, [later, incompatible, first], 2)));
    }

    // Ready-stage Codex: an inclusive middle encounter represents alternatives,
    // not a mandatory choice of its last historical state. b@1 can reach d@3
    // even when c@2 (the other middle possibility) has no published route to d.
    [Fact]
    public void InclusivePreviousEncounterChecksAllTimeLocationAlternatives()
    {
        var source = CandidateWarpProvenance.Create(
            CandidateWarpSourceKind.VoluntaryReachability,
            "ar52-public-states", 0, false, false,
            EvidenceState.SupportedButUncertain, ["a", "b", "c", "d"]);
        var historical = PublicCandidateWarpFactory.Create(
            ScenarioId, Seed, TargetPersonId,
            Enumerable.Range(0, 4)
                .Select((tick) => new PublicCandidateWarpStateDefinition
                {
                    TimeWindow = InteractionTimeWindow.At(tick),
                    TemporalKind = CandidateWarpTemporalKind.EventPoint,
                    RegionIds = [tick switch
                    {
                        0 => "a", 1 => "b", 2 => "c", _ => "d"
                    }],
                    EvidenceBoundaryState = EvidenceState.SupportedButUncertain,
                    Provenance = [source]
                }));
        var envelope = new PublicHazardEnvelope
        {
            HazardId = "ar52-public-alternatives",
            SourceRegionId = "a",
            PublishedAtTick = 0,
            Branches =
            [
                new PublicHazardBranch
                {
                    SourceRegionId = "a", DestinationRegionId = "b",
                    MinimumTravelTicks = 1, MaximumTravelTicks = 1,
                    TransportType = HazardTransportType.HazardDriven,
                    ProvenanceId = "ar52-a-b", PublishedAtTick = 0
                },
                new PublicHazardBranch
                {
                    SourceRegionId = "a", DestinationRegionId = "c",
                    MinimumTravelTicks = 2, MaximumTravelTicks = 2,
                    TransportType = HazardTransportType.HazardDriven,
                    ProvenanceId = "ar52-a-c", PublishedAtTick = 0
                },
                new PublicHazardBranch
                {
                    SourceRegionId = "b", DestinationRegionId = "d",
                    MinimumTravelTicks = 2, MaximumTravelTicks = 2,
                    TransportType = HazardTransportType.HazardDriven,
                    ProvenanceId = "ar52-b-d", PublishedAtTick = 0
                }
            ]
        };
        var first = Constraint(EvidenceState.Confirmed,
            InteractionTimeWindow.At(0), ["a"], 4, "ar52-first");
        var middle = Constraint(EvidenceState.Confirmed,
            new InteractionTimeWindow(1, 2), ["b", "c"], 4, "ar52-middle");
        var last = Constraint(EvidenceState.Confirmed,
            InteractionTimeWindow.At(3), ["d"], 4, "ar52-last");
        var result = Apply(historical, [first, middle, last], 4, envelope);
        Assert.Contains("d", result.CandidateWarps.PreferredRegionIdsAt(4));
        Assert.Contains("d", result.CandidateWarps.CandidateRegionIdsAt(4));
        Assert.Equal(JsonDefaults.Serialize(result),
            JsonDefaults.Serialize(Apply(historical,
                [last, middle, first], 4, envelope)));
    }

    // Ready-stage Codex: a new direct current anchor is independently
    // confirmed, but it must not erase one already received historical fact.
    [Fact]
    public void DirectCurrentAnchorRetainsSingleHistoricalConfirmedOuter()
    {
        var historical = Warp(new InteractionTimeWindow(0, 2),
            CandidateWarpTemporalKind.StateInterval, ["a", "b"]);
        var earlier = Constraint(EvidenceState.Confirmed,
            InteractionTimeWindow.At(0), ["a"], 2, "ar53-earlier");
        var uncertain = Constraint(EvidenceState.SupportedButUncertain,
            InteractionTimeWindow.At(1), ["b"], 2, "ar53-uncertain");
        var current = Constraint(EvidenceState.Confirmed,
            InteractionTimeWindow.At(2), ["b"], 2, "ar53-current");
        var result = Apply(historical, [earlier, uncertain, current], 2);
        Assert.Contains("b", result.CandidateWarps.PreferredRegionIdsAt(2));
        Assert.DoesNotContain("a", result.CandidateWarps.PreferredRegionIdsAt(0));
        Assert.Contains("a", result.CandidateWarps.OuterPlausibleRegionIdsAt(0));
        Assert.Equal(JsonDefaults.Serialize(result),
            JsonDefaults.Serialize(Apply(historical,
                [current, uncertain, earlier], 2)));
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
