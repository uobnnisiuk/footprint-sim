using FootprintSim.Evaluation;
using FootprintSim.Inference.PastStates;
using FootprintSim.Interactions;
using FootprintSim.Scenarios;
using FootprintSim.Serialization;
using FootprintSim.Simulation;
using Xunit;

namespace FootprintSim.Core.Tests;

public sealed class M1F2Tests
{
    private static readonly int[] TestSeeds = [0, 1, 2, 3, 4, 5, 24];

    private static readonly Lazy<LocationResolutionEvaluationResult> Result =
        new(() => RunExperiment(TestSeeds));

    [Fact]
    public void ExperimentPreregistersFourMonotonicallyCoarserLocationLevels()
    {
        var levels = Experiment().Levels.OrderBy(item => item.InformationOrder).ToArray();

        Assert.Equal(["current", "target-window-broad", "scenario-wide-broad", "unknown"],
            levels.Select(item => item.Id));
        Assert.Equal([2, 3, 6, 0], levels.Select(item => item.ObservedRegionIds.Count));
        Assert.All(levels[..^1], item => Assert.True(item.GenerateConstraint));
        Assert.False(levels[^1].GenerateConstraint);
        Assert.Empty(levels[^1].ObservedRegionIds);
        for (var index = 1; index < levels.Length - 1; index++)
        {
            Assert.True(levels[index].ObservedRegionIds.ToHashSet(StringComparer.Ordinal)
                .IsSupersetOf(levels[index - 1].ObservedRegionIds));
        }
    }

    [Fact]
    public void EveryFinitePayloadDirectlyDeterminesItsDerivedAllowedRegions()
    {
        foreach (var run in Result.Value.Runs)
        {
            Assert.True(run.SourceObservationResolved);
            if (run.LevelId == "unknown")
            {
                Assert.Empty(run.ObservedRegionIds);
                Assert.Empty(run.DerivedAllowedRegionIds);
                Assert.False(run.ConstraintGenerated);
                Assert.Empty(run.ConstraintIds);
                Assert.Empty(run.ConstraintDefinitionIds);
                continue;
            }

            Assert.Equal(run.ObservedRegionIds, run.DerivedAllowedRegionIds);
            Assert.True(run.ConstraintGenerated);
            Assert.Equal(2, run.ConstraintIds.Count);
            Assert.Equal(["generation-a", "generation-a-redundant"], run.ConstraintDefinitionIds);
        }
    }

    [Fact]
    public void LocationIsTheOnlySourceFieldThatChangesAcrossProfiles()
    {
        foreach (var seedGroup in Result.Value.Runs.GroupBy(item => item.Seed))
        {
            Assert.Single(seedGroup.Select(item => string.Join("|", item.ObservedSubjectIds))
                .Distinct(StringComparer.Ordinal));
            Assert.Single(seedGroup.Select(item => item.ObservedTimeWindow).Distinct());
            Assert.Single(seedGroup.Select(item => item.ObservedRelationKind).Distinct());
            Assert.Single(seedGroup.Select(item => item.AvailableAtTick).Distinct());
            Assert.Single(seedGroup.Select(item => item.EvidenceState).Distinct());
            Assert.Single(seedGroup.Select(item => string.Join("|", item.SourceAncestryIds))
                .Distinct(StringComparer.Ordinal));
            Assert.Equal(["person-a", "person-b"], seedGroup.First().ObservedSubjectIds);
            Assert.Equal(new InteractionTimeWindow(24, 24), seedGroup.First().ObservedTimeWindow);
            Assert.Equal(25, seedGroup.First().AvailableAtTick);
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
    public void CurrentLevelReproducesTheUnchangedM1FFullMethodBehavior()
    {
        var baseline = new ObservationAblationEvaluationRunner().Run(new ObservationAblationEvaluationRequest
        {
            Experiment = M1FExperiment(),
            Scenario = Scenario(),
            Seeds = TestSeeds,
            ExperimentContentSha256 = "m1f-test-hash",
            ScenarioContentSha256 = "s5-test-hash",
            SourceCommitSha = "m1f2-test-commit",
            DirtyWorkingTree = false
        });

        foreach (var current in Result.Value.Runs.Where(item => item.LevelId == "current"))
        {
            var full = baseline.Runs.Single(item => item.Seed == current.Seed && item.ProfileId == "full");
            Assert.Equal(
                full.Methods.Select(MethodSignature),
                current.Methods.Select(MethodSignature));
            Assert.Equal(
                full.CandidateEvaluations.Select(item =>
                    (item.CandidateId, item.RegionId, item.CandidateDisposition, item.VerificationDisposition)),
                current.CandidateEvaluations.Select(item =>
                    (item.CandidateId, item.RegionId, item.CandidateDisposition, item.VerificationDisposition)));
        }
    }

    [Fact]
    public void CoarserFinitePayloadsRetainAmbiguityInsteadOfLeakingCurrentRegions()
    {
        var targetBroad = Run(0, "target-window-broad");
        var scenarioBroad = Run(0, "scenario-wide-broad");
        var reconstruction = PastStateMethod.ConstraintBasedReconstruction;

        Assert.Equal(["alternate-route", "fork-east", "fork-west"],
            targetBroad.Methods.Single(item => item.Method == reconstruction).CandidateRegionIdsAfter);
        Assert.Equal(["alternate-route", "fork-east", "fork-west"],
            scenarioBroad.Methods.Single(item => item.Method == reconstruction).CandidateRegionIdsAfter);
        Assert.Contains("alternate-route", targetBroad.DerivedAllowedRegionIds);
        Assert.Contains("release", scenarioBroad.DerivedAllowedRegionIds);
        Assert.Contains("terminal-east", scenarioBroad.DerivedAllowedRegionIds);
    }

    [Fact]
    public void UnknownLocationRemainsPublicButDoesNotBecomeNegativeEvidence()
    {
        var current = Run(0, "current");
        var unknown = Run(0, "unknown");

        Assert.True(unknown.SourceObservationResolved);
        Assert.False(unknown.ConstraintGenerated);
        Assert.Equal(current.UnavailableEvidenceCount + 2, unknown.UnavailableEvidenceCount);
        Assert.DoesNotContain(typeof(LocationResolutionRun).GetProperties(), property =>
            property.Name.Contains("Absent", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(["alternate-route", "fork-east", "fork-west"], unknown.Methods.Single(item =>
            item.Method == PastStateMethod.ConstraintBasedReconstruction).CandidateRegionIdsAfter);
    }

    [Fact]
    public void VerificationAndPublicTopologyTraversalRemainSeparateFromLocationResolution()
    {
        foreach (var seed in TestSeeds)
        {
            var current = Run(seed, "current");
            foreach (var level in Result.Value.Runs.Where(item => item.Seed == seed))
            {
                var currentReconstruction = current.Methods.Single(item =>
                    item.Method == PastStateMethod.ConstraintBasedReconstruction);
                var reconstruction = level.Methods.Single(item =>
                    item.Method == PastStateMethod.ConstraintBasedReconstruction);
                var local = level.Methods.Single(item =>
                    item.Method == PastStateMethod.ReconstructionWithLocalTraversal);
                Assert.Equal(currentReconstruction.CandidateVerificationWork, reconstruction.CandidateVerificationWork);
                Assert.True(local.ForwardTraversalWork >= 1);
                Assert.True(local.BackwardTraversalWork >= 1);
                Assert.NotEmpty(local.TransitionIds);
            }
        }
    }

    [Fact]
    public void TruthEastAndWestDoNotChangeAnyPublicLocationProfileOutput()
    {
        foreach (var levelId in Experiment().Levels.Select(item => item.Id))
        {
            var west = Run(0, levelId);
            var east = Run(24, levelId);
            Assert.Equal(PastStateEvidenceCondition.DistributedPublicEvidence, west.EvidenceCondition);
            Assert.Equal(west.EvidenceCondition, east.EvidenceCondition);
            Assert.Equal("fork-west", west.TruthPersonRegionId);
            Assert.Equal("fork-east", east.TruthPersonRegionId);
            Assert.Equal(west.ObservedRegionIds, east.ObservedRegionIds);
            Assert.Equal(west.Methods.Select(MethodSignature), east.Methods.Select(MethodSignature));
        }
    }

    [Fact]
    public void LocationProfilesDoNotAddTruthToPastStateInferenceInputs()
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
    public void LocationResolutionOutputAndCandidateIdentifiersAreDeterministic()
    {
        var forward = RunExperiment(TestSeeds);
        var reverse = RunExperiment(TestSeeds.Reverse().ToArray());

        Assert.Equal(JsonDefaults.Serialize(forward), JsonDefaults.Serialize(reverse));
        Assert.Equal(
            forward.Runs.SelectMany(item => item.CandidateEvaluations).Select(item => item.CandidateId),
            reverse.Runs.SelectMany(item => item.CandidateEvaluations).Select(item => item.CandidateId));
    }

    private static LocationResolutionRun Run(int seed, string levelId) =>
        Result.Value.Runs.Single(item => item.Seed == seed && item.LevelId == levelId);

    private static string MethodSignature(ObservationAblationMethodRun item) => string.Join(
        ";",
        item.Method,
        string.Join("|", item.CandidateRegionIds),
        item.CandidateGenerationWork,
        item.CandidateVerificationWork,
        item.ForwardTraversalWork,
        item.BackwardTraversalWork,
        item.PublicRelationsInspected,
        item.EvidenceRecordsInspected,
        item.SourceGroupsInspected,
        string.Join("|", item.TransitionIds));

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

    private static LocationResolutionEvaluationResult RunExperiment(IReadOnlyList<int> seeds) =>
        new LocationResolutionEvaluationRunner().Run(new LocationResolutionEvaluationRequest
        {
            Experiment = Experiment(),
            Scenario = Scenario(),
            Seeds = seeds,
            ExperimentContentSha256 = "m1f2-experiment-test-hash",
            ScenarioContentSha256 = "m1f2-scenario-test-hash",
            SourceCommitSha = "m1f2-test-commit",
            DirtyWorkingTree = false
        });

    private static LocationResolutionExperimentDefinition Experiment() =>
        LocationResolutionExperimentLoader.Load(Path.Combine(
            RepositoryRoot,
            "experiments",
            "m1f2_location_resolution",
            "experiment.json"));

    private static ObservationAblationExperimentDefinition M1FExperiment() =>
        ObservationAblationExperimentLoader.Load(Path.Combine(
            RepositoryRoot,
            "experiments",
            "m1f_observation_ablation",
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
