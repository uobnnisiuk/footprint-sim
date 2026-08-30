using System.Reflection;
using FootprintSim.Evidence;
using FootprintSim.Evaluation;
using FootprintSim.Hazards;
using FootprintSim.Inference;
using FootprintSim.Interactions;
using FootprintSim.Interactions.CandidateWarps;
using FootprintSim.Interactions.Tracing;
using FootprintSim.Interactions.Truth;
using FootprintSim.Scenarios;
using FootprintSim.Serialization;
using FootprintSim.Simulation;
using FootprintSim.Telemetry;
using Xunit;

namespace FootprintSim.Core.Tests;

public sealed class M1DTests
{
    [Fact]
    public void MultipleIndependentWeftsTraceForwardBackwardAndFormCausalSandwich()
    {
        var constraints = new[]
        {
            Constraint("up", "obs-up", "source-up", TracingConstraintRole.Upstream, 0, ["A"]),
            Constraint("local", "obs-local", "source-local", TracingConstraintRole.Local, 5, ["C"]),
            Constraint("down", "obs-down", "source-down", TracingConstraintRole.Downstream, 10, ["E"])
        };

        var result = Trace(constraints);

        Assert.Equal(["E"], result.CandidateWarps.PreferredRegionIdsAt(10));
        Assert.Equal(3, result.SourceGroups.Count);
        Assert.True(result.CausalSandwichApplied);
        Assert.Contains(result.Steps, step => step.Direction == CausalTracingDirection.Forward);
        Assert.Contains(result.Steps, step => step.Direction == CausalTracingDirection.Backward);
        Assert.Contains(result.Steps, step => step.Role == TracingConstraintRole.Local);
        Assert.All(result.SupportLineage, lineage => Assert.NotNull(lineage.SupportingSourceGroupIds));
    }

    [Fact]
    public void CausalSandwichFixtureComparesUpstreamDownstreamAndLocalConstraint()
    {
        var upstream = Constraint(
            "upstream",
            "obs-upstream",
            "source-upstream",
            TracingConstraintRole.Upstream,
            0,
            ["A"]);
        var local = Constraint(
            "local",
            "obs-local",
            "source-local",
            TracingConstraintRole.Local,
            5,
            ["C"]);
        var downstream = Constraint(
            "downstream",
            "obs-downstream",
            "source-downstream",
            TracingConstraintRole.Downstream,
            10,
            ["E"]);
        var envelope = SandwichEnvelope();

        var upstreamOnly = Trace([upstream], envelope: envelope);
        var downstreamOnly = Trace([downstream], envelope: envelope);
        var enclosing = Trace([upstream, downstream], envelope: envelope);
        var sandwich = Trace([upstream, local, downstream], envelope: envelope);

        Assert.Equal(2, upstreamOnly.CandidateWarps.PreferredRegionIdsAt(5).Count);
        Assert.Equal(2, downstreamOnly.CandidateWarps.PreferredRegionIdsAt(5).Count);
        Assert.Equal(2, enclosing.CandidateWarps.PreferredRegionIdsAt(5).Count);
        Assert.Equal(["C"], sandwich.CandidateWarps.PreferredRegionIdsAt(5));
        Assert.All(
            new[] { upstreamOnly, downstreamOnly, enclosing, sandwich },
            result => Assert.Contains("C", result.CandidateWarps.OuterPlausibleRegionIdsAt(5)));
        Assert.False(enclosing.CausalSandwichApplied);
        Assert.True(sandwich.CausalSandwichApplied);
    }

    [Fact]
    public void ConstraintOrderDoesNotChangeSemanticOutputOrCanonicalAudit()
    {
        var constraints = new[]
        {
            Constraint("up", "obs-up", "source-up", TracingConstraintRole.Upstream, 0, ["A"]),
            Constraint("local", "obs-local", "source-local", TracingConstraintRole.Local, 5, ["C"], EvidenceState.SupportedButUncertain),
            Constraint("conflict", "obs-conflict", "source-conflict", TracingConstraintRole.Local, 5, ["D"], EvidenceState.Conflicting)
        };

        var first = Trace(constraints);
        var second = Trace(constraints.Reverse());

        Assert.True(first.ConstraintOrderInvariant);
        Assert.Equal(
            JsonDefaults.Serialize(first),
            JsonDefaults.Serialize(second));
    }

    [Fact]
    public void SameSourceDerivedConstraintsAreOneIndependentSourceGroup()
    {
        var first = Constraint("derived-a", "same-observation", "same-source", TracingConstraintRole.Upstream, 0, ["A"]);
        var second = Constraint("derived-b", "same-observation", "same-source", TracingConstraintRole.Local, 5, ["C"]);

        var result = Trace([first, second]);

        var group = Assert.Single(result.SourceGroups);
        Assert.Equal(2, group.ConstraintIds.Count);
        Assert.Single(result.SupportLineage
            .SelectMany(item => item.SupportingSourceGroupIds)
            .Distinct(StringComparer.Ordinal));
    }

    [Fact]
    public void ConflictAndUncertainEvidencePreserveOuterAlternatives()
    {
        var uncertain = Trace([
            Constraint("uncertain", "obs-u", "source-u", TracingConstraintRole.Local, 5, ["C"], EvidenceState.SupportedButUncertain)
        ]);
        Assert.Equal(["C"], uncertain.CandidateWarps.PreferredRegionIdsAt(5));
        Assert.Equal(["C", "D"], uncertain.CandidateWarps.OuterPlausibleRegionIdsAt(5));

        var conflict = Trace([
            Constraint("confirmed", "obs-c", "source-c", TracingConstraintRole.Local, 5, ["C"]),
            Constraint("conflict", "obs-x", "source-x", TracingConstraintRole.Local, 5, ["D"], EvidenceState.Conflicting)
        ]);
        Assert.Equal(["C", "D"], conflict.CandidateWarps.OuterPlausibleRegionIdsAt(5));
        Assert.NotEmpty(conflict.CandidateWarps.OuterPlausibleRegionIdsAt(5));
        Assert.Contains(conflict.Steps, step =>
            step.Disposition == TracingStepDisposition.MarkConflict && step.ConflictDetected);
        Assert.NotEmpty(conflict.Steps.SelectMany(step => step.ConflictingSourceGroupIds));

        var contradictoryConfirmed = Trace([
            Constraint("confirmed-c", "obs-cc", "source-cc", TracingConstraintRole.Local, 5, ["C"]),
            Constraint("confirmed-d", "obs-dd", "source-dd", TracingConstraintRole.Local, 5, ["D"])
        ]);
        Assert.Equal(["C", "D"], contradictoryConfirmed.CandidateWarps.OuterPlausibleRegionIdsAt(5));
        Assert.Equal(2, contradictoryConfirmed.Steps.Count(step =>
            step.Direction == CausalTracingDirection.CrossThread &&
            step.Disposition == TracingStepDisposition.MarkConflict));
    }

    [Fact]
    public void MultipleConstraintWindowsPreserveOverlapAndNoOverlapSemantics()
    {
        var provenance = CandidateWarpProvenance.Create(
            CandidateWarpSourceKind.PersonObservation,
            "interval-fixture",
            0,
            true,
            false,
            EvidenceState.SupportedButUncertain,
            ["C", "D"]);
        var warps = PublicCandidateWarpFactory.Create(
            "tracing-fixture",
            7,
            "person-a",
            [
                new PublicCandidateWarpStateDefinition
                {
                    TimeWindow = new InteractionTimeWindow(5, 8),
                    TemporalKind = CandidateWarpTemporalKind.StateInterval,
                    RegionIds = ["C", "D"],
                    EvidenceBoundaryState = EvidenceState.SupportedButUncertain,
                    Provenance = [provenance]
                }
            ],
            CandidateWarpStanding.Preferred);
        var overlapping = Constraint(
            "overlap",
            "obs-overlap",
            "source-overlap",
            TracingConstraintRole.Local,
            6,
            ["C"],
            endTick: 7);
        var sameTick = Constraint(
            "same-tick",
            "obs-same-tick",
            "source-same-tick",
            TracingConstraintRole.Local,
            7,
            ["C"],
            endTick: 8);
        var noOverlap = Constraint(
            "no-overlap",
            "obs-no-overlap",
            "source-no-overlap",
            TracingConstraintRole.Downstream,
            10,
            ["C"]);

        var result = new OperationalCausalTracer().Trace(
            warps,
            [overlapping, sameTick, noOverlap],
            AccessContext(10),
            Envelope());

        Assert.Equal(["C"], result.CandidateWarps.OuterPlausibleRegionIdsAt(7));
        Assert.Contains(result.Steps, step =>
            step.ConstraintId == noOverlap.ConstraintId &&
            step.Reason == TracingStepReason.NoTemporalOverlap);
        Assert.Equal(2, result.Steps.Count(step =>
            step.Direction == CausalTracingDirection.CrossThread &&
            step.Disposition == TracingStepDisposition.Applied));
        Assert.Contains(result.Steps, step => step.TimeWindow == new InteractionTimeWindow(6, 7));
        Assert.Contains(result.Steps, step => step.TimeWindow == new InteractionTimeWindow(7, 8));
        Assert.Equal(new InteractionTimeWindow(5, 8), result.CandidateWarps.Warps[0].States[0].TimeWindow);

        var exactIdentity = Constraint(
            "resolution",
            "obs-resolution",
            "source-resolution",
            TracingConstraintRole.Local,
            6,
            ["C"]);
        var intervalIdentity = Constraint(
            "resolution",
            "obs-resolution",
            "source-resolution",
            TracingConstraintRole.Local,
            6,
            ["C"],
            endTick: 7);
        Assert.NotEqual(exactIdentity.ConstraintId, intervalIdentity.ConstraintId);
    }

    [Fact]
    public void AvailabilityBoundaryIsInclusiveAndDoesNotImplyAbsenceBeforeAvailability()
    {
        var delayed = Constraint(
            "delayed",
            "obs-delayed",
            "source-delayed",
            TracingConstraintRole.Local,
            5,
            ["C"],
            availableAtTick: 20);

        var before = Trace([delayed], currentTick: 19);
        var atBoundary = Trace([delayed], currentTick: 20);

        Assert.Equal(["C", "D"], before.CandidateWarps.OuterPlausibleRegionIdsAt(5));
        Assert.DoesNotContain(before.Steps, step => step.ConstraintId == delayed.ConstraintId);
        Assert.Equal(["C"], atBoundary.CandidateWarps.OuterPlausibleRegionIdsAt(5));
    }

    [Fact]
    public void ConflictingSourcesAreNotResolvedByMajorityVote()
    {
        var result = Trace([
            Constraint("support-a", "obs-a", "source-a", TracingConstraintRole.Local, 5, ["C"]),
            Constraint("support-b", "obs-b", "source-b", TracingConstraintRole.Local, 5, ["C"]),
            Constraint("conflict", "obs-x", "source-x", TracingConstraintRole.Local, 5, ["D"], EvidenceState.Conflicting)
        ]);

        Assert.Contains(result.CandidateWarps.Warps, warp => warp.Standing == CandidateWarpStanding.Conflicted);
        Assert.Contains(result.SupportLineage, lineage => lineage.CompetingSourceGroupIds.Count > 0);
    }

    [Fact]
    public void DeviceOnlyUnavailableAndUnsupportedConstraintsDoNotCreateAbsenceOrHardNarrowing()
    {
        var device = Constraint(
            "device",
            "obs-device",
            "source-device",
            TracingConstraintRole.Local,
            5,
            ["C"],
            EvidenceState.Confirmed,
            isPersonEvidence: false,
            isDeviceOnly: true);
        var unavailable = Constraint(
            "private",
            "obs-private",
            "source-private",
            TracingConstraintRole.Local,
            5,
            ["C"],
            EvidenceState.Confirmed,
            visibility: InteractionVisibility.PrivateEvidence);
        var unsupported = Constraint(
            "unsupported",
            "obs-unsupported",
            "source-unsupported",
            TracingConstraintRole.Local,
            5,
            ["C"],
            EvidenceState.UnsupportedForQuery);

        var result = Trace([device, unavailable, unsupported]);

        Assert.Equal(["C", "D"], result.CandidateWarps.OuterPlausibleRegionIdsAt(5));
        Assert.DoesNotContain(result.Steps, step => step.ConstraintId == unavailable.ConstraintId);
        Assert.Contains(result.Steps, step =>
            step.ConstraintId == device.ConstraintId && step.Disposition == TracingStepDisposition.NoOp);
        Assert.DoesNotContain(result.GetType().GetProperties(), property =>
            property.Name.Contains("Absent", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void PublicTracingContractCannotReceiveTruthObjects()
    {
        var method = typeof(OperationalCausalTracer).GetMethod(nameof(OperationalCausalTracer.Trace));
        Assert.NotNull(method);
        Assert.DoesNotContain(method!.GetParameters(), parameter =>
            Contains(parameter.ParameterType, typeof(InteractionOccurrence)) ||
            Contains(parameter.ParameterType, typeof(SimulationRun)));
        Assert.DoesNotContain(typeof(SearchStrategyInput).GetProperties(), property =>
            Contains(property.PropertyType, typeof(InteractionOccurrence)) ||
            Contains(property.PropertyType, typeof(SimulationRun)));
    }

    [Fact]
    public void S4KeepsTruthFixedAcrossAblationAndExercisesEveryCondition()
    {
        var scenario = Scenario();
        var engine = new SimulationEngine();
        var conditions = Enumerable.Range(0, 10)
            .Select(seed => engine.Run(scenario, seed))
            .ToArray();

        Assert.Equal(Enum.GetValues<CausalTracingCondition>(), conditions
            .Select(run => run.CausalTracingCondition!.Value)
            .OrderBy(item => item));
        Assert.Contains(conditions, run => run.CausalTracingConstraints!.Constraints
            .Any(item => item.IsDeviceOnlyEvidence));
        Assert.Contains(conditions, run => run.CausalTracingConstraints!.Constraints
            .Any(item => item.Visibility == InteractionVisibility.PrivateEvidence));
        Assert.Contains(conditions, run => run.CausalTracingConstraints!.Constraints
            .Any(item => item.EvidenceBoundaryState == EvidenceState.Conflicting));
        Assert.All(conditions, run => Assert.Equal(2, run.GroundTruthInteractionOccurrences.Count));
        var deviceOnly = conditions.Single(run =>
            run.CausalTracingCondition == CausalTracingCondition.DeviceOnlyConstraint);
        Assert.Empty(deviceOnly.InteractionObservations!.Observations);
        Assert.Single(deviceOnly.CausalTracingConstraints!.Constraints);
        var sameSource = conditions.Single(run =>
            run.CausalTracingCondition == CausalTracingCondition.TwoSameSourceDerivedConstraints);
        var sameSourceConstraints = sameSource.CausalTracingConstraints!.Constraints;
        Assert.Equal(2, sameSourceConstraints.Count);
        Assert.Single(sameSourceConstraints.Select(item => item.SourceObservationId).Distinct());
        Assert.Equal([6L, 9L], sameSourceConstraints.Select(item => item.AvailableAtTick).Order().ToArray());
        Assert.Single(sameSourceConstraints.Select(item => item.SourceObservedTimeWindow).Distinct());
        Assert.Equal(6, Assert.Single(sameSource.InteractionObservations!.Observations).AvailableAtTick);

        var batch = RunBatch([0]);
        var truthIds = batch.Runs.Select(run => run.TruthOutcomeIdentifier).Distinct().ToArray();
        Assert.Single(truthIds);
        Assert.Equal(3, batch.Runs.Count);
        Assert.NotNull(batch.CausalTracing);
    }

    [Fact]
    public async Task S4BatchAndTracingTelemetryAreByteDeterministic()
    {
        var first = RunBatch(Enumerable.Range(0, 10).ToArray(), collectTelemetry: true);
        var second = RunBatch(Enumerable.Range(0, 10).Reverse().ToArray(), collectTelemetry: true);
        Assert.Equal(JsonDefaults.Serialize(first.CausalTracing), JsonDefaults.Serialize(second.CausalTracing));
        Assert.Equal(JsonDefaults.Serialize(first.Telemetry), JsonDefaults.Serialize(second.Telemetry));
        Assert.Equal(2, first.Telemetry!.Manifest.TelemetrySchemaVersion);
        Assert.NotEmpty(first.Telemetry.Evidence.TracingConstraints);
        Assert.NotEmpty(first.Telemetry.Evaluation.CausalTracingSteps);
        Assert.All(first.Telemetry.Evaluation.CausalTracingSteps, step =>
        {
            Assert.False(string.IsNullOrWhiteSpace(step.RelationKind));
            Assert.NotNull(step.SourceAncestryIds);
        });

        var firstDirectory = Path.Combine(Path.GetTempPath(), $"m1d-first-{Guid.NewGuid():N}");
        var secondDirectory = Path.Combine(Path.GetTempPath(), $"m1d-second-{Guid.NewGuid():N}");
        try
        {
            await new EvaluationOutputWriter().WriteAsync(firstDirectory, first);
            await new EvaluationOutputWriter().WriteAsync(secondDirectory, second);
            Assert.Equal(
                await File.ReadAllBytesAsync(Path.Combine(firstDirectory, EvaluationOutputNames.CausalTracingJson)),
                await File.ReadAllBytesAsync(Path.Combine(secondDirectory, EvaluationOutputNames.CausalTracingJson)));
        }
        finally
        {
            if (Directory.Exists(firstDirectory)) Directory.Delete(firstDirectory, recursive: true);
            if (Directory.Exists(secondDirectory)) Directory.Delete(secondDirectory, recursive: true);
        }
    }

    private static CausalTracingResult Trace(
        IEnumerable<CausalTracingConstraint> constraints,
        long currentTick = 10,
        PublicHazardEnvelope? envelope = null)
    {
        var provenance = CandidateWarpProvenance.Create(
            CandidateWarpSourceKind.PersonObservation,
            "public-fixture",
            0,
            true,
            false,
            EvidenceState.SupportedButUncertain,
            ["A", "B", "C", "D", "E", "F"]);
        var warps = PublicCandidateWarpFactory.Create(
            "tracing-fixture",
            7,
            "person-a",
            [
                State(0, ["A", "B"], provenance),
                State(5, ["C", "D"], provenance),
                State(10, ["E", "F"], provenance)
            ],
            CandidateWarpStanding.Preferred);
        return new OperationalCausalTracer().Trace(
            warps,
            constraints,
            AccessContext(currentTick),
            envelope ?? Envelope());
    }

    private static CandidateWarpAccessContext AccessContext(long currentTick) => new()
    {
        ScenarioId = "tracing-fixture",
        Seed = 7,
        TargetEntityId = "person-a",
        CurrentTick = currentTick,
        QueryContextId = "test-public-query"
    };

    private static PublicCandidateWarpStateDefinition State(
        long tick,
        IReadOnlyList<string> regions,
        CandidateWarpProvenance provenance) => new()
        {
            TimeWindow = InteractionTimeWindow.At(tick),
            TemporalKind = CandidateWarpTemporalKind.EventPoint,
            RegionIds = regions,
            EvidenceBoundaryState = EvidenceState.SupportedButUncertain,
            Provenance = [provenance]
        };

    private static CausalTracingConstraint Constraint(
        string id,
        string observation,
        string directSource,
        TracingConstraintRole role,
        long tick,
        IReadOnlyList<string> regions,
        EvidenceState state = EvidenceState.Confirmed,
        bool isPersonEvidence = true,
        bool isDeviceOnly = false,
        InteractionVisibility visibility = InteractionVisibility.PublicEvidence,
        long? availableAtTick = null,
        long? endTick = null)
    {
        var windowEnd = endTick ?? tick;
        return CausalTracingConstraint.Create(
            "tracing-fixture",
            7,
            observation,
            directSource,
            [directSource],
            "person-a",
            isDeviceOnly
                ? TracingConstraintSourceKind.DeviceOnlyObservation
                : TracingConstraintSourceKind.CrossThreadObservation,
            $"fixture-relation-{id}",
            [
                new InteractionParticipant("person-a", InteractionEntityKind.Person, InteractionParticipantRole.EncounterParticipant),
                new InteractionParticipant(isDeviceOnly ? "device-d" : $"person-{id}", isDeviceOnly ? InteractionEntityKind.Device : InteractionEntityKind.Person, InteractionParticipantRole.EncounterParticipant)
            ],
            role,
            new InteractionTimeWindow(tick, windowEnd),
            new InteractionTimeWindow(tick, windowEnd),
            regions,
            availableAtTick ?? windowEnd,
            state,
            isPersonEvidence,
            isDeviceOnly,
            visibility,
            "fixture-derivation",
            [new InteractionProvenance(InteractionProvenanceKind.ExternalSource, directSource)]);
    }

    private static PublicHazardEnvelope Envelope() => new()
    {
        HazardId = "public-fixture-model",
        SourceRegionId = "A",
        PublishedAtTick = 0,
        Regions = [],
        Branches =
        [
            Branch("A", "C"), Branch("B", "D"), Branch("C", "E"), Branch("D", "F")
        ]
    };

    private static PublicHazardEnvelope SandwichEnvelope() => new()
    {
        HazardId = "public-sandwich-model",
        SourceRegionId = "A",
        PublishedAtTick = 0,
        Regions = [],
        Branches =
        [
            Branch("A", "C"), Branch("A", "D"), Branch("B", "D"),
            Branch("C", "E"), Branch("D", "E"), Branch("D", "F")
        ]
    };

    private static PublicHazardBranch Branch(string source, string destination) => new()
    {
        SourceRegionId = source,
        DestinationRegionId = destination,
        MinimumTravelTicks = 5,
        MaximumTravelTicks = 5,
        TransportType = HazardTransportType.HazardDriven,
        ProvenanceId = $"public-{source}-{destination}",
        PublishedAtTick = 0
    };

    private static BatchEvaluationResult RunBatch(
        IReadOnlyList<int> seeds,
        bool collectTelemetry = false)
    {
        var scenario = Scenario();
        return new BatchEvaluationRunner().Run(new BatchEvaluationRequest
        {
            Scenarios = [new EvaluationScenarioInput
            {
                Scenario = scenario,
                ContentSha256 = "s4-test-content-hash"
            }],
            Seeds = seeds,
            Strategies =
            [
                new OwnEvidenceConstrainedStrategy(),
                new SingleWeftStrategy(),
                new MultiWeftTracingStrategy()
            ],
            SourceCommitSha = "m1d-test-commit",
            DirtyWorkingTree = false,
            CollectTelemetry = collectTelemetry
        });
    }

    private static ScenarioDefinition Scenario() => ScenarioLoader.Load(ScenarioPath);

    private static string ScenarioPath => Path.Combine(
        RepositoryRoot,
        "scenarios",
        "s4_multi_weft_tracing",
        "scenario.json");

    private static string RepositoryRoot
    {
        get
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "FootprintSim.sln")))
            {
                directory = directory.Parent;
            }

            return directory?.FullName ?? throw new DirectoryNotFoundException("Repository root not found.");
        }
    }

    private static bool Contains(Type candidate, Type target)
    {
        if (candidate == target) return true;
        if (candidate.IsArray) return Contains(candidate.GetElementType()!, target);
        return candidate.IsGenericType && candidate.GetGenericArguments().Any(argument => Contains(argument, target));
    }
}
