using System.Reflection;
using FootprintSim.Evidence;
using FootprintSim.Evaluation;
using FootprintSim.Events;
using FootprintSim.Hazards;
using FootprintSim.Interactions;
using FootprintSim.Interactions.CandidateWarps;
using FootprintSim.Interactions.Truth;
using FootprintSim.Serialization;
using FootprintSim.Simulation;
using Xunit;

namespace FootprintSim.Core.Tests;

public sealed class CandidateWarpTests
{
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

        var warp = Assert.Single(result.CandidateWarps.Warps);
        Assert.Equal(CandidateWarpStanding.Preferred, warp.Standing);
        Assert.Equal(["R3"], warp.CandidateRegionIdsAt(20));
        Assert.Equal(["R5", "R6"], warp.CandidateRegionIdsAt(25));
        var audit = Assert.Single(result.Audits);
        Assert.Equal(ConstraintApplicationDisposition.Narrow, audit.Disposition);
        Assert.Equal(ConstraintTemporalKind.ExactTick, audit.ConstraintTemporalKind);
        Assert.Equal(3, audit.CandidateCountBefore);
        Assert.Equal(1, audit.CandidateCountAfter);
        Assert.False(audit.AlternativesRetained);
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
            ["R2", "R4", "R5", "R6"],
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
    public void PublicTravelArrivalIsInclusiveWithoutRetainingTheSourceAtMaximumArrivalTick()
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

        Assert.Equal(["R5", "R6"], result.CandidateWarps.CandidateRegionIdsAt(22));
        Assert.DoesNotContain("R3", result.CandidateWarps.CandidateRegionIdsAt(22));
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
