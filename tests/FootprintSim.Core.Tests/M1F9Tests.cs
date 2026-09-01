using FootprintSim.Evaluation;
using FootprintSim.Inference.Derivation;
using FootprintSim.Inference.PastStates;
using FootprintSim.Interactions;
using FootprintSim.Serialization;
using Xunit;

namespace FootprintSim.Core.Tests;

public sealed class M1F9Tests
{
    private static readonly int[] Seeds = Enumerable.Range(0, 200).ToArray();

    private static readonly Lazy<TimeResolutionPairedEvaluationResult> Result =
        new(RunExperiment);

    [Fact]
    public void GeneratorUsesPreregisteredRangesWithoutSkippingSeeds()
    {
        var experiment = Experiment();
        var generator = new TimeResolutionSeededWorldGenerator();
        var worlds = Seeds.Select(seed => generator.Generate(experiment, seed)).ToArray();

        Assert.Equal(200, worlds.Length);
        Assert.Equal(Seeds, worlds.Select(item => item.Seed));
        Assert.All(worlds, world =>
        {
            Assert.InRange(world.CandidateCount, 5, 12);
            Assert.InRange(world.LaterEventTick, 30, 60);
            Assert.InRange(world.DurationBounds.MinimumTicks, 3, 6);
            Assert.InRange(world.DurationWidthTicks, 0, 3);
            Assert.Equal(
                world.DurationBounds.MinimumTicks + world.DurationWidthTicks,
                world.DurationBounds.MaximumTicks);
            Assert.InRange(
                world.ActualTruthDurationTicks,
                world.DurationBounds.MinimumTicks,
                world.DurationBounds.MaximumTicks);
            Assert.Equal(world.CandidateCount, world.CandidateStates.Count);
            Assert.Equal(world.CandidateCount, world.CandidateStates
                .Select(item => item.RegionId).Distinct(StringComparer.Ordinal).Count());
            Assert.All(world.CandidateStates, item =>
                Assert.InRange(item.Tick, world.CandidateTickMinimum, world.CandidateTickMaximum));
            Assert.Single(world.CandidateStates, item => item.IsTruthCandidate);
            Assert.Equal(world.LaterEventTick - world.ActualTruthDurationTicks, world.TruthTick);
        });
        Assert.True(worlds.Select(item => item.CandidateCount).Distinct().Count() > 1);
        Assert.True(worlds.Select(item => item.LaterEventTick).Distinct().Count() > 1);
        Assert.Equal([0, 1, 2, 3], worlds.Select(item => item.DurationWidthTicks).Distinct().Order());
        Assert.Contains(worlds, world => world.CandidateStates
            .GroupBy(item => item.Tick).Any(group => group.Count() > 1));
    }

    [Fact]
    public void FourProfilesArePairedWithinEveryWorld()
    {
        Assert.Equal(200, Result.Value.Summary.WorldCount);
        Assert.Equal(800, Result.Value.Summary.PairedProfileRunCount);
        Assert.All(Result.Value.Runs, run =>
        {
            Assert.Equal(["exact", "medium", "broad", "unknown"],
                run.Profiles.Select(item => item.ProfileId));
            Assert.Equal(
                [
                    new InteractionTimeWindow(run.LaterEventTick, run.LaterEventTick),
                    new InteractionTimeWindow(run.LaterEventTick - 1, run.LaterEventTick + 1),
                    new InteractionTimeWindow(run.LaterEventTick - 3, run.LaterEventTick + 3),
                    null
                ],
                run.Profiles.Select(item => item.ObservedOccurrenceWindow));
            Assert.Single(run.Profiles.Select(item => item.SourceGroupId).Distinct(StringComparer.Ordinal));
            Assert.All(run.Profiles, item =>
            {
                Assert.Equal(BoundedDurationObservationConstraintDeriver.RuleId, item.RuleId);
                Assert.Equal(PastStateEvidenceUsageRole.CandidateGeneration, item.UsageRole);
            });
            Assert.Equal(run.CandidateStates.Select(item => item.CandidateId).Order(),
                Profile(run, "unknown").CandidateIds.Order());
        });
    }

    [Fact]
    public void EverySeedPreservesWindowAndCandidateSetMonotonicityAndSafety()
    {
        Assert.Equal(0, Result.Value.Summary.DerivedWindowMonotonicityViolationCount);
        Assert.Equal(0, Result.Value.Summary.CandidateSetMonotonicityViolationCount);
        Assert.Equal(0, Result.Value.Summary.FalseExclusionCount);
        Assert.All(Result.Value.Runs, run =>
        {
            Assert.All(run.MonotonicityAudits, audit =>
            {
                Assert.True(audit.DerivedWindowIsSubset);
                Assert.True(audit.CandidateSetIsSubset);
            });
            Assert.All(run.MonotonicityAudits.Take(2), audit =>
                Assert.True(audit.DerivedWindowStrictlyExpanded));
            Assert.All(run.Profiles, profile =>
            {
                Assert.True(profile.ContainsTruth);
                Assert.False(profile.FalseExclusion);
            });
        });
    }

    [Fact]
    public void AggregateAndPlateauMetricsMatchThePreregisteredRun()
    {
        var exact = Aggregate("exact");
        var medium = Aggregate("medium");
        var broad = Aggregate("broad");
        var unknown = Aggregate("unknown");

        Assert.Equal([2.19m, 3.21m, 5.295m, 8.555m],
            new[] { exact, medium, broad, unknown }.Select(item => item.MeanCandidateCount));
        Assert.Equal([2m, 3m, 5m, 9m],
            new[] { exact, medium, broad, unknown }.Select(item => item.MedianCandidateCount));
        Assert.Equal([1, 1, 1, 5],
            new[] { exact, medium, broad, unknown }.Select(item => item.MinimumCandidateCount));
        Assert.Equal([6, 8, 11, 12],
            new[] { exact, medium, broad, unknown }.Select(item => item.MaximumCandidateCount));
        Assert.Equal([6.365m, 5.345m, 3.26m, 0m],
            new[] { exact, medium, broad, unknown }
                .Select(item => item.MeanCandidateReductionCountVsUnknown));
        Assert.Equal([9.555m, 9.555m, 9.555m, 8.555m],
            new[] { exact, medium, broad, unknown }.Select(item => item.MeanCandidateGenerationWork));
        Assert.All(Result.Value.Aggregates, item =>
        {
            Assert.Equal(1m, item.ContainmentRate);
            Assert.Equal(0, item.FalseExclusionCount);
        });

        Assert.Equal(122, Result.Value.Summary.ExactLessThanMediumSeedCount);
        Assert.Equal(175, Result.Value.Summary.MediumLessThanBroadSeedCount);
        Assert.Equal(190, Result.Value.Summary.BroadLessThanUnknownSeedCount);
        Assert.Equal(11, Result.Value.Summary.FiniteProfilesAllEqualSeedCount);
        Assert.Equal(0, Result.Value.Summary.FiniteDerivedWindowEqualitySeedCount);
        Assert.Equal(92, Result.Value.Summary.ConstraintWidenedButCandidateUnchangedSeedCount);
        Assert.Equal(10, Result.Value.Summary.BroadUnknownSaturationSeedCount);
    }

    [Fact]
    public void UnknownKeepsTheEventObservationAndUsesNoTimeConstraint()
    {
        Assert.All(Result.Value.Runs, run =>
        {
            var unknown = Profile(run, "unknown");
            Assert.True(unknown.SourceObservationPresent);
            Assert.Equal(OccurrenceTimeKnowledge.Unknown, unknown.OccurrenceTimeKnowledge);
            Assert.Null(unknown.ObservedOccurrenceWindow);
            Assert.False(unknown.ConstraintGenerated);
            Assert.Null(unknown.ConstraintId);
            Assert.Null(unknown.DerivedPastWindow);
            Assert.Equal(
                ObservationConstraintDerivationFailureReason.UnknownOccurrenceTime,
                unknown.FailureReason);
            Assert.Equal(0, unknown.GeneratedConstraintSourceGroupCount);
            Assert.Equal(run.CandidateCount, unknown.CandidateCount);
        });
    }

    [Fact]
    public void TruthIdentityDoesNotChangePublicDerivationOrCandidates()
    {
        var experiment = Experiment();
        var generator = new TimeResolutionSeededWorldGenerator();
        var world = Seeds.Select(seed => generator.Generate(experiment, seed)).First(item =>
        {
            var exact = new InteractionTimeWindow(
                item.LaterEventTick - item.DurationBounds.MaximumTicks,
                item.LaterEventTick - item.DurationBounds.MinimumTicks);
            return item.CandidateStates.Any(candidate =>
                !candidate.IsTruthCandidate &&
                exact.StartTick <= candidate.Tick && candidate.Tick <= exact.EndTick);
        });
        var alternate = world.CandidateStates.First(item =>
            !item.IsTruthCandidate &&
            world.LaterEventTick - world.DurationBounds.MaximumTicks <= item.Tick &&
            item.Tick <= world.LaterEventTick - world.DurationBounds.MinimumTicks);
        var alternateStates = world.CandidateStates.Select(item => item with
        {
            IsTruthCandidate = string.Equals(
                item.CandidateKey,
                alternate.CandidateKey,
                StringComparison.Ordinal)
        }).ToArray();
        var alternateWorld = world with
        {
            TruthWorldId = "alternate-truth-world",
            ActualTruthDurationTicks = world.LaterEventTick - alternate.Tick,
            TruthCandidateKey = alternate.CandidateKey,
            TruthRegionId = alternate.RegionId,
            TruthTick = alternate.Tick,
            CandidateStates = alternateStates
        };
        var evaluator = new TimeResolutionPairedWorldEvaluator();
        var original = evaluator.Evaluate(experiment, world, "hash", "commit", false);
        var changed = evaluator.Evaluate(experiment, alternateWorld, "hash", "commit", false);

        Assert.NotEqual(original.TruthCandidateId, changed.TruthCandidateId);
        Assert.Equal(original.PublicWorldId, changed.PublicWorldId);
        Assert.Equal(
            original.Profiles.Select(PublicSignature),
            changed.Profiles.Select(PublicSignature));
    }

    [Fact]
    public void AvailableAtTickDoesNotAlterDerivedWindowsOrCandidateSets()
    {
        var experiment = Experiment();
        var world = new TimeResolutionSeededWorldGenerator().Generate(experiment, seed: 0);
        var earlierAvailability = world with { AvailableAtTick = world.AvailableAtTick - 1 };
        var evaluator = new TimeResolutionPairedWorldEvaluator();
        var original = evaluator.Evaluate(experiment, world, "hash", "commit", false);
        var changed = evaluator.Evaluate(experiment, earlierAvailability, "hash", "commit", false);

        Assert.NotEqual(original.AvailableAtTick, changed.AvailableAtTick);
        Assert.Equal(
            original.Profiles.Select(item =>
                (item.ProfileId, item.DerivedPastWindow, string.Join("|", item.CandidateIds))),
            changed.Profiles.Select(item =>
                (item.ProfileId, item.DerivedPastWindow, string.Join("|", item.CandidateIds))));
    }

    [Fact]
    public void OriginalF8AcceptanceFixtureRemainsTwoThreeFourFour()
    {
        var fixture = BoundedDurationTimeFixtureLoader.Load(Path.Combine(
            RepositoryRoot,
            "experiments",
            "m1f8_observation_constraint_derivation",
            "fixture.json"));
        var result = new BoundedDurationTimeFixtureEvaluationRunner().Run(
            new BoundedDurationTimeFixtureEvaluationRequest
            {
                Fixture = fixture,
                FixtureContentSha256 = "f8-regression-hash",
                SourceCommitSha = "f9-test-commit",
                DirtyWorkingTree = false
            });

        Assert.Equal([2, 3, 4, 4], result.Runs.Select(item => item.CandidateCount));
        Assert.Equal(BoundedDurationObservationConstraintDeriver.RuleId, result.Runs[0].DerivationAudit.RuleId);
        Assert.Equal("[o_min - d_max, o_max - d_min]", result.IntervalFormula);
    }

    [Fact]
    public async Task EvaluationAndArtifactsAreDeterministic()
    {
        var first = RunExperiment();
        var second = RunExperiment();
        Assert.Equal(JsonDefaults.Serialize(first), JsonDefaults.Serialize(second));

        var root = Path.Combine(Path.GetTempPath(), $"footprint-sim-m1f9-{Guid.NewGuid():N}");
        var left = Path.Combine(root, "left");
        var right = Path.Combine(root, "right");
        try
        {
            var writer = new TimeResolutionPairedOutputWriter();
            await writer.WriteAsync(left, first);
            await writer.WriteAsync(right, second);
            Assert.All(TimeResolutionPairedOutputNames.All, name =>
                Assert.Equal(File.ReadAllBytes(Path.Combine(left, name)),
                    File.ReadAllBytes(Path.Combine(right, name))));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void EvaluationRejectsAnySeedSelectionOtherThanZeroThrough199()
    {
        var request = Request() with { Seeds = Seeds[..^1] };
        Assert.Throws<InvalidDataException>(() =>
            new TimeResolutionPairedEvaluationRunner().Run(request));
    }

    private static TimeResolutionPairedProfileAggregate Aggregate(string profileId) =>
        Result.Value.Aggregates.Single(item => item.ProfileId == profileId);

    private static TimeResolutionPairedProfileRun Profile(
        TimeResolutionPairedWorldRun run,
        string profileId) => run.Profiles.Single(item => item.ProfileId == profileId);

    private static string PublicSignature(TimeResolutionPairedProfileRun profile) => string.Join(
        ';',
        profile.ProfileId,
        profile.SourceObservationId,
        profile.ConstraintId,
        profile.DerivedPastWindow?.StartTick,
        profile.DerivedPastWindow?.EndTick,
        string.Join("|", profile.CandidateIds));

    private static TimeResolutionPairedEvaluationResult RunExperiment() =>
        new TimeResolutionPairedEvaluationRunner().Run(Request());

    private static TimeResolutionPairedEvaluationRequest Request() => new()
    {
        Experiment = Experiment(),
        Seeds = Seeds,
        ExperimentContentSha256 = "m1f9-experiment-test-hash",
        SourceCommitSha = "m1f9-test-commit",
        DirtyWorkingTree = false
    };

    private static TimeResolutionPairedExperimentDefinition Experiment() =>
        TimeResolutionPairedExperimentLoader.Load(Path.Combine(
            RepositoryRoot,
            "experiments",
            "m1f9_time_resolution_paired",
            "experiment.json"));

    private static string RepositoryRoot
    {
        get
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory is not null &&
                !File.Exists(Path.Combine(directory.FullName, "FootprintSim.sln")))
            {
                directory = directory.Parent;
            }

            return directory?.FullName ??
                throw new DirectoryNotFoundException("Repository root not found.");
        }
    }
}
