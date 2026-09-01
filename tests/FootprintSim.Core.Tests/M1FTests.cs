using System.Reflection;
using FootprintSim.Evaluation;
using FootprintSim.Inference;
using FootprintSim.Inference.PastStates;
using FootprintSim.Scenarios;
using FootprintSim.Serialization;
using FootprintSim.Simulation;
using Xunit;

namespace FootprintSim.Core.Tests;

public sealed class M1FTests
{
    private static readonly Lazy<ObservationAblationEvaluationResult> SixSeedExperiment =
        new(() => RunExperiment(Enumerable.Range(0, 6).ToArray()));

    [Fact]
    public void ExperimentPreregistersCoreProfilesAndOrdinaryObservationFamilies()
    {
        var definition = Experiment();
        var profiles = ObservationAblationEvaluationRunner.BuildProfiles(definition);

        Assert.Equal(8, definition.Families.Count);
        Assert.Equal(21, profiles.Count);
        Assert.Single(profiles, item => item.Kind == ObservationProfileKind.Full);
        Assert.Single(profiles, item => item.Kind == ObservationProfileKind.None);
        Assert.Equal(8, profiles.Count(item => item.Kind == ObservationProfileKind.SingleFamilyRemoval));
        Assert.Equal(8, profiles.Count(item => item.Kind == ObservationProfileKind.SingleFamilyPresence));
        Assert.Equal(3, profiles.Count(item => item.Kind == ObservationProfileKind.SelectedCombination));
        Assert.All(definition.Families, family =>
            Assert.Single(family.SourceObservationKeys));
        var device = SixSeedExperiment.Value.Families.Single(item => item.FamilyId == "device-fix");
        Assert.Equal(["terminal-west"], device.RecordedRegionIds);
        Assert.Equal(["fork-west"], device.AllowedConstraintRegionIds);
        Assert.Equal([PastStateEvidenceUsageRole.CandidateVerification], device.UsageRoles);
    }

    [Fact]
    public void FullProfileReproducesE2PublicMethodBehavior()
    {
        var experiment = SixSeedExperiment.Value;
        var e2 = new BatchEvaluationRunner().Run(new BatchEvaluationRequest
        {
            Scenarios = [new EvaluationScenarioInput { Scenario = Scenario(), ContentSha256 = "s5-test-hash" }],
            Seeds = Enumerable.Range(0, 6).ToArray(),
            Strategies = [new OwnEvidenceConstrainedStrategy()],
            SourceCommitSha = "m1f-test-commit",
            DirtyWorkingTree = false,
            CollectTelemetry = false
        }).PastStateReconstruction!;

        foreach (var full in experiment.Runs.Where(item => item.ProfileId == "full"))
        {
            var baseline = e2.Runs.Single(item => item.Seed == full.Seed);
            foreach (var method in full.Methods)
            {
                var expected = baseline.PublicComparison.Methods.Single(item => item.Method == method.Method);
                Assert.Equal(expected.CandidateRegionIds, method.CandidateRegionIds);
                Assert.Equal(expected.CandidateGenerationSteps, method.CandidateGenerationWork);
                Assert.Equal(expected.CandidateVerificationSteps, method.CandidateVerificationWork);
                Assert.Equal(expected.ForwardTransitionSteps, method.ForwardTraversalWork);
                Assert.Equal(expected.BackwardTransitionSteps, method.BackwardTraversalWork);
            }

            Assert.Equal(0, full.UnresolvedConstraintSourceCount);
        }
    }

    [Fact]
    public void FamilyRemovalDisablesRawObservationAndDerivedConstraintsTogether()
    {
        var full = Run(0, "full");
        var without = Run(0, "without-later-trace-a");
        var none = Run(0, "none");

        Assert.Contains("later-trace-a", full.PresentFamilyIds);
        Assert.Contains("later-trace-a", full.QueryUsableFamilyIds);
        Assert.DoesNotContain("later-trace-a", without.PresentFamilyIds);
        Assert.DoesNotContain("later-trace-a", without.QueryUsableFamilyIds);
        Assert.Equal(full.RawObservationCount - 1, without.RawObservationCount);
        Assert.Equal(full.GenerationConstraintCount - 2, without.GenerationConstraintCount);
        Assert.True(without.UnavailableEvidenceCount >= 2);
        Assert.Empty(none.QueryUsableFamilyIds);
        Assert.Equal(0, none.GenerationConstraintCount);
        Assert.Equal(0, none.VerificationConstraintCount);
        Assert.Equal(0, none.TraversalConstraintCount);
    }

    [Fact]
    public void EnabledSourcesResolveAndDisabledSourcesDoNotBecomeAbsentClaims()
    {
        Assert.All(
            SixSeedExperiment.Value.Runs.Where(item => item.ProfileId == "full"),
            item => Assert.Equal(0, item.UnresolvedConstraintSourceCount));
        Assert.DoesNotContain(typeof(ObservationAblationRun).GetProperties(), property =>
            property.Name.Contains("Absent", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(typeof(PublicEvidenceSet).GetProperties(), property =>
            property.Name.Contains("Absent", StringComparison.OrdinalIgnoreCase));
        Assert.All(
            SixSeedExperiment.Value.Runs.Where(item => item.ProfileId == "none"),
            item => Assert.NotEmpty(item.Methods));
    }

    [Fact]
    public void EveryProfileSharesTheSameTruthWorldAndPublicTopologyForItsSeed()
    {
        foreach (var seedGroup in SixSeedExperiment.Value.Runs.GroupBy(item => item.Seed))
        {
            Assert.Single(seedGroup.Select(item => item.TruthWorldIdentifier).Distinct(StringComparer.Ordinal));
            Assert.Single(seedGroup.Select(item => item.PublicTopologyIdentifier).Distinct(StringComparer.Ordinal));
            Assert.Single(seedGroup.Select(item => item.TruthPersonRegionId).Distinct(StringComparer.Ordinal));
            Assert.Single(seedGroup.Select(item => item.TruthDeviceRegionId).Distinct(StringComparer.Ordinal));
        }
    }

    [Fact]
    public void VerificationFamilyRemovalDoesNotMutateGeneratedCandidates()
    {
        var full = Run(0, "full");
        var without = Run(0, "without-public-environment-west");

        Assert.Equal(
            full.CandidateEvaluations.Select(item => (item.CandidateId, item.RegionId, item.CandidateDisposition)),
            without.CandidateEvaluations.Select(item => (item.CandidateId, item.RegionId, item.CandidateDisposition)));
        Assert.Equal(
            full.Methods.Single(item => item.Method == PastStateMethod.ConstraintBasedReconstruction).CandidateRegionIds,
            without.Methods.Single(item => item.Method == PastStateMethod.ConstraintBasedReconstruction).CandidateRegionIds);
        Assert.Equal(full.GenerationConstraintCount, without.GenerationConstraintCount);
        Assert.Equal(full.VerificationConstraintCount - 1, without.VerificationConstraintCount);
        Assert.NotEqual(
            full.CandidateEvaluations.Select(item => item.VerificationDisposition),
            without.CandidateEvaluations.Select(item => item.VerificationDisposition));
    }

    [Fact]
    public void TruthEastAndWestKeepTheSameFullProfilePublicMethodOutput()
    {
        var result = RunExperiment([0, 24]);
        var west = result.Runs.Single(item => item.Seed == 0 && item.ProfileId == "full");
        var east = result.Runs.Single(item => item.Seed == 24 && item.ProfileId == "full");

        Assert.Equal(PastStateEvidenceCondition.DistributedPublicEvidence, west.EvidenceCondition);
        Assert.Equal(west.EvidenceCondition, east.EvidenceCondition);
        Assert.Equal("fork-west", west.TruthPersonRegionId);
        Assert.Equal("fork-east", east.TruthPersonRegionId);
        Assert.Equal(west.QueryUsableFamilyIds, east.QueryUsableFamilyIds);
        Assert.Equal(west.GenerationConstraintCount, east.GenerationConstraintCount);
        Assert.Equal(west.VerificationConstraintCount, east.VerificationConstraintCount);
        Assert.All(west.Methods, method => Assert.Equal(
            method.CandidateRegionIds,
            east.Methods.Single(item => item.Method == method.Method).CandidateRegionIds));
    }

    [Fact]
    public void DeviceFamilyRemovalDoesNotPromoteDeviceEvidenceToPersonEvidence()
    {
        var full = Run(5, "full");
        var without = Run(5, "without-device-fix");

        Assert.Equal(
            full.CandidateEvaluations.Select(item => (item.CandidateId, item.RegionId, item.CandidateDisposition)),
            without.CandidateEvaluations.Select(item => (item.CandidateId, item.RegionId, item.CandidateDisposition)));
        Assert.Equal(full.GenerationConstraintCount, without.GenerationConstraintCount);
        Assert.Equal(full.VerificationConstraintCount - 1, without.VerificationConstraintCount);
        Assert.All(full.Methods, method => Assert.Equal(
            method.CandidateRegionIds,
            without.Methods.Single(item => item.Method == method.Method).CandidateRegionIds));
    }

    [Fact]
    public void SharedAncestryGroupingAndAvailabilityCutoffRemainExplicit()
    {
        var sharedFull = Run(1, "full");
        var sharedRemoved = Run(1, "without-shared-record-check");
        var delayed = Run(3, "full");

        Assert.True(sharedFull.SameAncestryDerivedRecordCount > sharedRemoved.SameAncestryDerivedRecordCount);
        Assert.True(sharedFull.SharedAncestryOnlyCandidateCount > 0);
        Assert.Equal(0, sharedRemoved.SharedAncestryOnlyCandidateCount);
        Assert.True(delayed.UnavailableEvidenceCount > 0);
        Assert.DoesNotContain("delayed-check", delayed.PresentFamilyIds);
        Assert.DoesNotContain("delayed-check", delayed.QueryUsableFamilyIds);
    }

    [Fact]
    public void RecordExistencePublicAvailabilityAndQueryUseRemainSeparate()
    {
        var full = Run(0, "full");

        Assert.Equal(full.RawObservationCount, full.RecordedObservationIds.Count);
        Assert.Equal(full.PublicObservationCount, full.PublicObservationIds.Count);
        Assert.Equal(
            full.AvailableByCutoffObservationCount,
            full.AvailableByCutoffObservationIds.Count);
        Assert.Equal(
            full.QueryUsableSourceObservationCount,
            full.QueryUsableSourceObservationIds.Count);
        Assert.True(full.RecordedObservationIds.Count >= full.PublicObservationIds.Count);
        Assert.True(full.PublicObservationIds.Count >= full.AvailableByCutoffObservationIds.Count);
        Assert.True(full.AvailableByCutoffObservationIds.Count >= full.QueryUsableSourceObservationIds.Count);
    }

    [Fact]
    public void ObservationProfilesNeverRemoveTheFixedPublicTopology()
    {
        foreach (var run in SixSeedExperiment.Value.Runs)
        {
            var local = run.Methods.Single(item =>
                item.Method == PastStateMethod.ReconstructionWithLocalTraversal);
            Assert.True(local.ForwardTraversalWork >= 1);
            Assert.True(local.BackwardTraversalWork >= 1);
            Assert.NotEmpty(local.TransitionIds);
        }
    }

    [Fact]
    public void ProfileCandidateIdentifiersAndEvaluationOutputAreDeterministic()
    {
        var forward = RunExperiment(Enumerable.Range(0, 6).ToArray());
        var reverse = RunExperiment(Enumerable.Range(0, 6).Reverse().ToArray());

        Assert.Equal(JsonDefaults.Serialize(forward), JsonDefaults.Serialize(reverse));
        Assert.Equal(
            forward.Runs.SelectMany(item => item.CandidateEvaluations).Select(item => item.CandidateId),
            reverse.Runs.SelectMany(item => item.CandidateEvaluations).Select(item => item.CandidateId));
    }

    [Fact]
    public void AblationRunnerDoesNotChangePastStateInferenceTruthBoundary()
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

    private static ObservationAblationRun Run(int seed, string profileId) =>
        SixSeedExperiment.Value.Runs.Single(item => item.Seed == seed && item.ProfileId == profileId);

    private static ObservationAblationEvaluationResult RunExperiment(IReadOnlyList<int> seeds) =>
        new ObservationAblationEvaluationRunner().Run(new ObservationAblationEvaluationRequest
        {
            Experiment = Experiment(),
            Scenario = Scenario(),
            Seeds = seeds,
            ExperimentContentSha256 = "m1f-experiment-test-hash",
            ScenarioContentSha256 = "m1f-scenario-test-hash",
            SourceCommitSha = "m1f-test-commit",
            DirtyWorkingTree = false
        });

    private static ObservationAblationExperimentDefinition Experiment() =>
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
