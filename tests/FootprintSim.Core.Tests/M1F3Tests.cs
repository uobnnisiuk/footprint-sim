using FootprintSim.Evaluation;
using FootprintSim.Inference.PastStates;
using FootprintSim.Interactions;
using FootprintSim.Scenarios;
using FootprintSim.Serialization;
using FootprintSim.Simulation;
using Xunit;

namespace FootprintSim.Core.Tests;

public sealed class M1F3Tests
{
    private static readonly int[] TestSeeds = [0, 1, 2, 3, 4, 5, 24];

    private static readonly Lazy<TimeResolutionEvaluationResult> Result =
        new(() => RunExperiment(TestSeeds));

    [Fact]
    public void ExperimentPreregistersBoundaryBasedNestedTimeLevels()
    {
        var levels = Experiment().Levels.OrderBy(item => item.InformationOrder).ToArray();

        Assert.Equal(
            ["current", "narrow", "later-state-broad", "target-crossing-broad", "scenario-history-broad", "unknown"],
            levels.Select(item => item.Id));
        Assert.Equal([24L, 23L, 18L, 13L, 8L, null], levels.Select(item => item.ObservedStartTick));
        Assert.Equal([24L, 24L, 24L, 24L, 24L, null], levels.Select(item => item.ObservedEndTick));
        Assert.Equal([1, 2, 7, 12, 17, 0], Result.Value.Aggregates
            .Select(item => item.PossibleObservationTickCount));
        Assert.All(levels[..^1], item => Assert.True(item.GenerateConstraint));
        Assert.False(levels[^1].GenerateConstraint);
        for (var index = 1; index < levels.Length - 1; index++)
        {
            Assert.True(levels[index].ObservedStartTick <= levels[index - 1].ObservedStartTick);
            Assert.True(levels[index].ObservedEndTick >= levels[index - 1].ObservedEndTick);
        }
    }

    [Fact]
    public void CurrentTimeAndAvailabilityRemainDifferentSemantics()
    {
        var current = Run(0, "current");

        Assert.Equal(24, current.ObservedStartTick);
        Assert.Equal(24, current.ObservedEndTick);
        Assert.Equal([24L], current.PossibleObservationTicks);
        Assert.Equal(25, current.AvailableAtTick);
        Assert.Equal([new InteractionTimeWindow(24, 24)], current.ConstraintSourceObservedWindows);
        Assert.Equal([new InteractionTimeWindow(13, 14)], current.DerivedHistoricalWindows);
        Assert.Equal(2, current.DerivedHistoricalPossibleTickCount);
        Assert.Equal(1, current.AffectedCandidateStateIntervalCount);
    }

    [Fact]
    public void EveryPossibleTickUsesTheExistingFixedScenarioDeclaredDerivation()
    {
        Assert.False(Result.Value.CurrentTransformationUsesObservedTick);
        foreach (var run in Result.Value.Runs.Where(item => item.ObservedTimeAvailable))
        {
            Assert.Equal(run.PossibleObservationTickCount * 2, run.PossibleExactTimeDerivations.Count);
            Assert.All(run.PossibleExactTimeDerivations, item =>
            {
                Assert.Equal(new InteractionTimeWindow(13, 14), item.DerivedHistoricalWindow);
                Assert.Equal(["fork-east", "fork-west"], item.DerivedAllowedRegionIds);
            });
            Assert.Equal([new InteractionTimeWindow(13, 14)], run.DerivedHistoricalWindows);
            Assert.Equal(["fork-east", "fork-west"], run.DerivedAllowedRegionIds);
        }
    }

    [Fact]
    public void FiniteWindowsAreEmittedAndPropagatedWithoutUsingTheMidpoint()
    {
        foreach (var run in Result.Value.Runs.Where(item => item.ObservedTimeAvailable))
        {
            var expected = new InteractionTimeWindow(run.ObservedStartTick!.Value, run.ObservedEndTick!.Value);
            Assert.True(run.ProjectedSourceObservationEmitted);
            Assert.True(run.ProjectedSourceObservationResolved);
            Assert.NotNull(run.ProjectedSourceObservationId);
            Assert.True(run.ConstraintGenerated);
            Assert.Equal(2, run.ConstraintIds.Count);
            Assert.Equal([expected], run.ConstraintSourceObservedWindows);
            Assert.Equal(
                Enumerable.Range(0, run.PossibleObservationTickCount)
                    .Select(offset => run.ObservedStartTick.Value + offset),
                run.PossibleObservationTicks);
        }
    }

    [Fact]
    public void UnknownTimeKeepsTheUnderlyingSourceButDoesNotEmitOrDerive()
    {
        var current = Run(0, "current");
        var unknown = Run(0, "unknown");

        Assert.True(unknown.UnderlyingSourceObservationResolved);
        Assert.False(unknown.ObservedTimeAvailable);
        Assert.Null(unknown.ObservedStartTick);
        Assert.Null(unknown.ObservedEndTick);
        Assert.Empty(unknown.PossibleObservationTicks);
        Assert.Empty(unknown.PossibleExactTimeDerivations);
        Assert.False(unknown.ProjectedSourceObservationEmitted);
        Assert.False(unknown.ProjectedSourceObservationResolved);
        Assert.Null(unknown.ProjectedSourceObservationId);
        Assert.False(unknown.ConstraintGenerated);
        Assert.Empty(unknown.ConstraintIds);
        Assert.Empty(unknown.DerivedHistoricalWindows);
        Assert.Equal(current.UnavailableEvidenceCount + 2, unknown.UnavailableEvidenceCount);
        Assert.DoesNotContain(typeof(TimeResolutionRun).GetProperties(), property =>
            property.Name.Contains("Absent", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ObservedTimeIsTheOnlySourceFieldThatChangesAcrossProfiles()
    {
        foreach (var seedGroup in Result.Value.Runs.GroupBy(item => item.Seed))
        {
            Assert.Single(seedGroup.Select(item => string.Join("|", item.ObservedSubjectIds))
                .Distinct(StringComparer.Ordinal));
            Assert.Single(seedGroup.Select(item => string.Join("|", item.ObservedRegionIds))
                .Distinct(StringComparer.Ordinal));
            Assert.Single(seedGroup.Select(item => item.ObservedRelationKind).Distinct());
            Assert.Single(seedGroup.Select(item => item.AvailableAtTick).Distinct());
            Assert.Single(seedGroup.Select(item => item.EvidenceState).Distinct());
            Assert.Single(seedGroup.Select(item => string.Join("|", item.SourceAncestryIds))
                .Distinct(StringComparer.Ordinal));
            Assert.Equal(["fork-east", "fork-west"], seedGroup.First().ObservedRegionIds);
            Assert.Equal(["person-a", "person-b"], seedGroup.First().ObservedSubjectIds);
            Assert.Equal(["record-family-a"], seedGroup.First().SourceAncestryIds);
        }
    }

    [Fact]
    public void EveryLevelSharesOneTruthWorldAndOnePublicTopologyPerSeed()
    {
        foreach (var seedGroup in Result.Value.Runs.GroupBy(item => item.Seed))
        {
            Assert.Single(seedGroup.Select(item => item.TruthWorldIdentifier).Distinct(StringComparer.Ordinal));
            Assert.Single(seedGroup.Select(item => item.PublicTopologyIdentifier).Distinct(StringComparer.Ordinal));
            Assert.Single(seedGroup.Select(item => item.TruthPersonRegionId).Distinct(StringComparer.Ordinal));
        }
    }

    [Fact]
    public void CurrentLevelReproducesTheUnchangedM1F2CurrentBehavior()
    {
        var baseline = new LocationResolutionEvaluationRunner().Run(new LocationResolutionEvaluationRequest
        {
            Experiment = M1F2Experiment(),
            Scenario = Scenario(),
            Seeds = TestSeeds,
            ExperimentContentSha256 = "m1f2-test-hash",
            ScenarioContentSha256 = "s5-test-hash",
            SourceCommitSha = "m1f3-test-commit",
            DirtyWorkingTree = false
        });

        foreach (var current in Result.Value.Runs.Where(item => item.LevelId == "current"))
        {
            var locationCurrent = baseline.Runs.Single(item =>
                item.Seed == current.Seed && item.LevelId == "current");
            Assert.Equal(locationCurrent.Methods.Select(MethodSignature), current.Methods.Select(MethodSignature));
            Assert.Equal(
                locationCurrent.CandidateEvaluations.Select(item =>
                    (item.CandidateId, item.RegionId, item.CandidateDisposition, item.VerificationDisposition)),
                current.CandidateEvaluations.Select(item =>
                    (item.CandidateId, item.RegionId, item.CandidateDisposition, item.VerificationDisposition)));
        }
    }

    [Fact]
    public void EveryFiniteWindowIsMethodEquivalentUnderTheCurrentTransformation()
    {
        Assert.Equal(["scenario-history-broad"], Result.Value.CoarsestCandidateOutputEquivalentLevelIds);
        Assert.Equal(["scenario-history-broad"], Result.Value.CoarsestBaselineEquivalentLevelIds);
        foreach (var seed in TestSeeds)
        {
            var finite = Result.Value.Runs.Where(item => item.Seed == seed && item.ObservedTimeAvailable)
                .OrderBy(item => item.InformationOrder).ToArray();
            var expected = finite[0].Methods.Select(MethodSignature).ToArray();
            Assert.All(finite, item => Assert.Equal(expected, item.Methods.Select(MethodSignature)));
        }
    }

    [Fact]
    public void ReconstructionCandidateOrderingIsMonotonicForEveryFinitePair()
    {
        Assert.Equal(0, Result.Value.MonotonicityViolationCount);
        Assert.NotEmpty(Result.Value.MonotonicityAudits);
        Assert.All(Result.Value.MonotonicityAudits, item => Assert.True(item.IsSubset));
    }

    [Fact]
    public void BroadWindowMeansOnePossibleOccurrenceTickNotPersistence()
    {
        Assert.Equal(TimeObservationWindowMeaning.OccursAtOnePossibleTick, Experiment().TimeWindowMeaning);
        var broad = Run(0, "scenario-history-broad");
        Assert.Equal(17, broad.PossibleObservationTickCount);
        Assert.Equal(34, broad.PossibleExactTimeDerivations.Count);
        Assert.DoesNotContain(typeof(TimeResolutionRun).GetProperties(), property =>
            property.Name.Contains("Persist", StringComparison.OrdinalIgnoreCase) ||
            property.Name.Contains("Duration", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void VerificationAndTraversalRemainSeparateFromTimeResolution()
    {
        foreach (var seed in TestSeeds)
        {
            var current = Run(seed, "current");
            foreach (var run in Result.Value.Runs.Where(item => item.Seed == seed))
            {
                Assert.Equal(
                    current.Methods.Single(item => item.Method == PastStateMethod.ConstraintBasedReconstruction)
                        .CandidateVerificationWork,
                    run.Methods.Single(item => item.Method == PastStateMethod.ConstraintBasedReconstruction)
                        .CandidateVerificationWork);
                var local = run.Methods.Single(item => item.Method == PastStateMethod.ReconstructionWithLocalTraversal);
                Assert.True(local.ForwardTraversalWork >= 1);
                Assert.True(local.BackwardTraversalWork >= 1);
                Assert.NotEmpty(local.TransitionIds);
            }
        }
    }

    [Fact]
    public void TruthEastAndWestDoNotChangeAnyPublicTimeProfileOutput()
    {
        foreach (var levelId in Experiment().Levels.Select(item => item.Id))
        {
            var west = Run(0, levelId);
            var east = Run(24, levelId);
            Assert.Equal(PastStateEvidenceCondition.DistributedPublicEvidence, west.EvidenceCondition);
            Assert.Equal(west.EvidenceCondition, east.EvidenceCondition);
            Assert.Equal("fork-west", west.TruthPersonRegionId);
            Assert.Equal("fork-east", east.TruthPersonRegionId);
            Assert.Equal(west.PossibleObservationTicks, east.PossibleObservationTicks);
            Assert.Equal(west.Methods.Select(MethodSignature), east.Methods.Select(MethodSignature));
        }
    }

    [Fact]
    public void TimeProfilesDoNotAddTruthToPastStateInferenceInputs()
    {
        var compare = typeof(PastStateReconstructor).GetMethod(nameof(PastStateReconstructor.Compare));
        Assert.NotNull(compare);
        Assert.DoesNotContain(compare!.GetParameters(), parameter =>
            Contains(parameter.ParameterType, typeof(SimulationRun)) ||
            parameter.ParameterType.Name.Contains("Truth", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(typeof(PastStatePublicContext).GetProperties(), property =>
            property.Name.Contains("Truth", StringComparison.OrdinalIgnoreCase) ||
            property.Name.Contains("Realized", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void TimeResolutionOutputAndCandidateIdentifiersAreDeterministic()
    {
        var forward = RunExperiment(TestSeeds);
        var reverse = RunExperiment(TestSeeds.Reverse().ToArray());

        Assert.Equal(JsonDefaults.Serialize(forward), JsonDefaults.Serialize(reverse));
        Assert.Equal(
            forward.Runs.SelectMany(item => item.CandidateEvaluations).Select(item => item.CandidateId),
            reverse.Runs.SelectMany(item => item.CandidateEvaluations).Select(item => item.CandidateId));
    }

    private static TimeResolutionRun Run(int seed, string levelId) =>
        Result.Value.Runs.Single(item => item.Seed == seed && item.LevelId == levelId);

    private static string MethodSignature(LaterTraceFieldMethodRun item) => string.Join(
        ";",
        item.Method,
        string.Join("|", item.CandidateRegionIdsAfter),
        item.CandidateGenerationWork,
        item.CandidateVerificationWork,
        item.ForwardTraversalWork,
        item.BackwardTraversalWork,
        item.PublicRelationsInspected,
        item.EvidenceRecordsInspected,
        item.SourceGroupsInspected,
        string.Join("|", item.TransitionIds));

    private static TimeResolutionEvaluationResult RunExperiment(IReadOnlyList<int> seeds) =>
        new TimeResolutionEvaluationRunner().Run(new TimeResolutionEvaluationRequest
        {
            Experiment = Experiment(),
            Scenario = Scenario(),
            Seeds = seeds,
            ExperimentContentSha256 = "m1f3-experiment-test-hash",
            ScenarioContentSha256 = "m1f3-scenario-test-hash",
            SourceCommitSha = "m1f3-test-commit",
            DirtyWorkingTree = false
        });

    private static TimeResolutionExperimentDefinition Experiment() =>
        TimeResolutionExperimentLoader.Load(Path.Combine(
            RepositoryRoot,
            "experiments",
            "m1f3_time_resolution",
            "experiment.json"));

    private static LocationResolutionExperimentDefinition M1F2Experiment() =>
        LocationResolutionExperimentLoader.Load(Path.Combine(
            RepositoryRoot,
            "experiments",
            "m1f2_location_resolution",
            "experiment.json"));

    private static ScenarioDefinition Scenario() => ScenarioLoader.Load(Path.Combine(
        RepositoryRoot,
        "scenarios",
        "s5_distributed_past_evidence",
        "scenario.json"));

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
