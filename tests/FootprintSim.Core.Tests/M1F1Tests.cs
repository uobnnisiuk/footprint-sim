using System.Reflection;
using FootprintSim.Evaluation;
using FootprintSim.Inference.PastStates;
using FootprintSim.Scenarios;
using FootprintSim.Serialization;
using FootprintSim.Simulation;
using Xunit;

namespace FootprintSim.Core.Tests;

public sealed class M1F1Tests
{
    private static readonly int[] TestSeeds = [0, 1, 2, 3, 4, 5, 24];

    private static readonly Lazy<LaterTraceFieldAblationEvaluationResult> Result =
        new(() => RunExperiment(TestSeeds));

    [Fact]
    public void ExperimentPreregistersTheFourInformationFieldsAndTenCoreProfiles()
    {
        var experiment = Experiment();

        Assert.Equal(["location", "relation", "subject", "time"], experiment.Fields
            .Select(item => item.Id).OrderBy(item => item, StringComparer.Ordinal));
        Assert.Equal(experiment.Fields.Select(item => item.Id).OrderBy(item => item, StringComparer.Ordinal),
            experiment.RequiredFieldIds.OrderBy(item => item, StringComparer.Ordinal));
        Assert.Equal(10, experiment.Profiles.Count);
        Assert.All(experiment.Profiles, profile => Assert.True(profile.IsCore));
        Assert.Single(experiment.Profiles, profile => profile.Kind == LaterTraceFieldProfileKind.Full);
        Assert.Single(experiment.Profiles, profile => profile.Kind == LaterTraceFieldProfileKind.None);
        Assert.Equal(4, experiment.Profiles.Count(profile =>
            profile.Kind == LaterTraceFieldProfileKind.SingleFieldRemoval));
        Assert.Equal(4, experiment.Profiles.Count(profile =>
            profile.Kind == LaterTraceFieldProfileKind.SingleFieldPresence));
    }

    [Fact]
    public void SourceInventoryPreservesTheOriginalLaterTraceMeaningAndGenerationBoundary()
    {
        var inventory = Result.Value.SourceInventory;

        Assert.Equal("later-trace-a", inventory.SourceObservationKey);
        Assert.Equal(["generation-a", "generation-a-redundant"], inventory.ConstraintDefinitionIds);
        Assert.Equal(["source-a"], inventory.DirectSourceIds);
        Assert.Equal(["record-family-a"], inventory.SourceAncestryIds);
        Assert.Equal(["person-a"], inventory.ObservedSubjectIds);
        Assert.Equal(["person-b"], inventory.ObserverOrParticipantIds);
        Assert.Equal([new Interactions.InteractionTimeWindow(24, 24)], inventory.ObservedTimeWindows);
        Assert.Equal([25L], inventory.AvailableAtTicks);
        Assert.Equal(["fork-east", "fork-west"], inventory.RecordedRegionIds);
        Assert.Equal(["PersonPersonEncounter"], inventory.ObservedRelationKinds);
        Assert.Equal([PastStateEvidenceUsageRole.CandidateGeneration], inventory.DeclaredUsageRoles);
    }

    [Fact]
    public void OnlyFullFieldsPermitTheCurrentAtomicTransformation()
    {
        foreach (var profile in Result.Value.ProfileAggregates)
        {
            if (profile.ProfileKind == LaterTraceFieldProfileKind.Full)
            {
                Assert.Equal(TestSeeds.Length, profile.DerivationSuccessCount);
                Assert.Equal(2m, profile.MeanGeneratedConstraintCount);
            }
            else
            {
                Assert.Equal(0, profile.DerivationSuccessCount);
                Assert.Equal(0m, profile.MeanGeneratedConstraintCount);
            }
        }

        Assert.All(Result.Value.FieldContributions, item =>
        {
            Assert.True(item.RemovalPreventsDerivation);
            Assert.False(item.PresenceEnablesDerivation);
        });
    }

    [Fact]
    public void MissingFieldNeverLeaksTheScenarioDeclaredConstraintValues()
    {
        foreach (var run in Result.Value.Runs.Where(item => item.ProfileKind != LaterTraceFieldProfileKind.Full))
        {
            Assert.True(run.UnderlyingSourceObservationExists);
            Assert.False(run.SourceRecordEligibleForDerivation);
            Assert.False(run.EvidenceGenerated);
            Assert.False(run.ConstraintDerivationSucceeded);
            Assert.NotEmpty(run.MissingFieldIds);
            Assert.NotNull(run.DerivationFailureReason);
            Assert.Empty(run.GeneratedConstraintIds);
            Assert.Empty(run.GeneratedConstraintDefinitionIds);
            Assert.Empty(run.AppliedUsageRoles);
            Assert.False(run.TraversalInputGenerated);
            Assert.Equal("without-later-trace-a", run.SourceBehaviorReferenceProfileId);
        }
    }

    [Fact]
    public void FullProfileEmitsBothSameSourceGenerationConstraintsAndNoTraversalInput()
    {
        foreach (var run in Result.Value.Runs.Where(item => item.ProfileKind == LaterTraceFieldProfileKind.Full))
        {
            Assert.True(run.SourceRecordEligibleForDerivation);
            Assert.True(run.EvidenceGenerated);
            Assert.True(run.ConstraintDerivationSucceeded);
            Assert.True(run.SourceObservationResolved);
            Assert.NotNull(run.SourceObservationId);
            Assert.Null(run.DerivationFailureReason);
            Assert.Equal(2, run.GeneratedConstraintIds.Count);
            Assert.Equal(["generation-a", "generation-a-redundant"], run.GeneratedConstraintDefinitionIds);
            Assert.Equal([PastStateEvidenceUsageRole.CandidateGeneration], run.AppliedUsageRoles);
            Assert.False(run.TraversalInputGenerated);
        }
    }

    [Fact]
    public void EveryFieldProfileKeepsTheSameTruthWorldAndPublicTopologyForItsSeed()
    {
        foreach (var seedGroup in Result.Value.Runs.GroupBy(item => item.Seed))
        {
            Assert.Single(seedGroup.Select(item => item.TruthWorldIdentifier).Distinct(StringComparer.Ordinal));
            Assert.Single(seedGroup.Select(item => item.PublicTopologyIdentifier).Distinct(StringComparer.Ordinal));
            Assert.Single(seedGroup.Select(item => item.TruthPersonRegionId).Distinct(StringComparer.Ordinal));
            Assert.All(seedGroup.SelectMany(item => item.Methods), method =>
            {
                Assert.Equal(["alternate-route", "fork-east", "fork-west"], method.CandidateRegionIdsBefore);
                Assert.True(method.Method != PastStateMethod.ReconstructionWithLocalTraversal ||
                    method.TransitionIds.Count > 0);
            });
        }
    }

    [Fact]
    public void IncompleteProfilesAreMethodEquivalentBecauseTheCurrentRepresentationIsAtomic()
    {
        foreach (var seed in TestSeeds)
        {
            var incomplete = Result.Value.Runs
                .Where(item => item.Seed == seed && item.ProfileKind != LaterTraceFieldProfileKind.Full)
                .ToArray();
            var expected = MethodSignature(incomplete[0]);
            Assert.All(incomplete, item => Assert.Equal(expected, MethodSignature(item)));
        }
    }

    [Fact]
    public void RemovingLaterTraceGenerationDoesNotChangeVerificationOrTraversalUsageRoles()
    {
        foreach (var seed in TestSeeds)
        {
            var full = Run(seed, "full");
            var removed = Run(seed, "minus-location");
            var fullReconstruction = full.Methods.Single(item =>
                item.Method == PastStateMethod.ConstraintBasedReconstruction);
            var removedReconstruction = removed.Methods.Single(item =>
                item.Method == PastStateMethod.ConstraintBasedReconstruction);
            var fullLocal = full.Methods.Single(item =>
                item.Method == PastStateMethod.ReconstructionWithLocalTraversal);
            var removedLocal = removed.Methods.Single(item =>
                item.Method == PastStateMethod.ReconstructionWithLocalTraversal);

            Assert.Equal(fullReconstruction.CandidateVerificationWork, removedReconstruction.CandidateVerificationWork);
            Assert.Equal(fullLocal.ForwardTraversalWork, removedLocal.ForwardTraversalWork);
            Assert.Equal(fullLocal.BackwardTraversalWork, removedLocal.BackwardTraversalWork);
            Assert.Equal(fullLocal.TransitionIds, removedLocal.TransitionIds);
        }
    }

    [Fact]
    public void MissingInformationDoesNotBecomeAbsenceOrEvaluationInput()
    {
        Assert.DoesNotContain(typeof(LaterTraceFieldAblationRun).GetProperties(), property =>
            property.Name.Contains("Absent", StringComparison.OrdinalIgnoreCase));
        var compare = typeof(PastStateReconstructor).GetMethod(nameof(PastStateReconstructor.Compare));
        Assert.NotNull(compare);
        Assert.DoesNotContain(compare!.GetParameters(), parameter =>
            Contains(parameter.ParameterType, typeof(SimulationRun)) ||
            parameter.ParameterType.Name.Contains("Truth", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void TruthEastAndWestDoNotChangeThePublicFieldProfileMethodOutput()
    {
        var west = Run(0, "full");
        var east = Run(24, "full");

        Assert.Equal(PastStateEvidenceCondition.DistributedPublicEvidence, west.EvidenceCondition);
        Assert.Equal(west.EvidenceCondition, east.EvidenceCondition);
        Assert.Equal("fork-west", west.TruthPersonRegionId);
        Assert.Equal("fork-east", east.TruthPersonRegionId);
        Assert.Equal(MethodSignature(west), MethodSignature(east));
    }

    [Fact]
    public void FieldProfileOutputAndCandidateIdentifiersAreDeterministic()
    {
        var forward = RunExperiment(TestSeeds);
        var reverse = RunExperiment(TestSeeds.Reverse().ToArray());

        Assert.Equal(JsonDefaults.Serialize(forward), JsonDefaults.Serialize(reverse));
        Assert.Equal(
            forward.Runs.SelectMany(item => item.CandidateEvaluations).Select(item => item.CandidateId),
            reverse.Runs.SelectMany(item => item.CandidateEvaluations).Select(item => item.CandidateId));
    }

    private static LaterTraceFieldAblationRun Run(int seed, string profileId) =>
        Result.Value.Runs.Single(item => item.Seed == seed && item.ProfileId == profileId);

    private static string MethodSignature(LaterTraceFieldAblationRun run) => JsonDefaults.Serialize(run.Methods
        .OrderBy(item => item.Method)
        .Select(item => new
        {
            item.Method,
            item.CandidateRegionIdsAfter,
            item.CandidateGenerationWork,
            item.CandidateVerificationWork,
            item.ForwardTraversalWork,
            item.BackwardTraversalWork,
            item.PublicRelationsInspected,
            item.EvidenceRecordsInspected,
            item.SourceGroupsInspected,
            item.TransitionIds
        }).ToArray());

    private static LaterTraceFieldAblationEvaluationResult RunExperiment(IReadOnlyList<int> seeds) =>
        new LaterTraceFieldAblationEvaluationRunner().Run(new LaterTraceFieldAblationEvaluationRequest
        {
            Experiment = Experiment(),
            BaseExperiment = BaseExperiment(),
            Scenario = Scenario(),
            Seeds = seeds,
            ExperimentContentSha256 = "m1f1-experiment-test-hash",
            BaseExperimentContentSha256 = "m1f-base-experiment-test-hash",
            ScenarioContentSha256 = "m1f1-scenario-test-hash",
            SourceCommitSha = "m1f1-test-commit",
            DirtyWorkingTree = false
        });

    private static LaterTraceFieldAblationExperimentDefinition Experiment() =>
        LaterTraceFieldAblationExperimentLoader.Load(Path.Combine(
            RepositoryRoot,
            "experiments",
            "m1f1_later_trace_field_ablation",
            "experiment.json"));

    private static ObservationAblationExperimentDefinition BaseExperiment() =>
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
