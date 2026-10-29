using System.Text.Json;
using FootprintSim.Evidence;
using FootprintSim.Evaluation;
using FootprintSim.Inference.Derivation;
using FootprintSim.Inference.PastStates;
using FootprintSim.Interactions;
using FootprintSim.Interactions.CandidateWarps;
using FootprintSim.Serialization;
using Xunit;

namespace FootprintSim.Core.Tests;

public sealed class M1F11Tests
{
    private static readonly Lazy<ParticipantAmbiguityPairedEvaluationResult> Result =
        new(RunEvaluation);

    [Fact]
    public void ExperimentRequiresExactRegisteredSeedRange()
    {
        var experiment = Experiment();

        ParticipantAmbiguityPairedExperimentValidator.Validate(
            experiment,
            Enumerable.Range(0, 200).ToArray());
        Assert.Throws<InvalidDataException>(() =>
            ParticipantAmbiguityPairedExperimentValidator.Validate(
                experiment,
                Enumerable.Range(0, 199).ToArray()));
        Assert.Throws<InvalidDataException>(() =>
            ParticipantAmbiguityPairedExperimentValidator.Validate(experiment, [1, 2, 3]));
    }

    [Fact]
    public void GeneratorIsDeterministicAndStaysWithinPreregisteredRanges()
    {
        var experiment = Experiment();
        var generator = new ParticipantAmbiguitySeededWorldGenerator();
        var first = generator.Generate(experiment, seed: 17);
        var second = generator.Generate(experiment, seed: 17);

        Assert.Equal(JsonDefaults.Serialize(first), JsonDefaults.Serialize(second));
        foreach (var seed in Enumerable.Range(0, 200))
        {
            var world = generator.Generate(experiment, seed);
            Assert.InRange(world.CandidateCount, 6, 14);
            Assert.InRange(world.RegionUniverseSize, 4, 8);
            Assert.InRange(world.ParticipantPoolSize, 3, 5);
            Assert.InRange(world.EncounterTick, 10, 40);
            Assert.All(world.CandidateStates, candidate =>
            {
                Assert.Equal(world.EncounterTick, candidate.Tick);
                Assert.Contains(candidate.RegionId, world.RegionUniverse);
            });
            Assert.All(world.Participants, participant =>
            {
                Assert.InRange(participant.AllowedRegionIds.Count, 1, 3);
                Assert.Equal(
                    participant.AllowedRegionIds.Count,
                    participant.AllowedRegionIds.Distinct(StringComparer.Ordinal).Count());
            });
            Assert.Single(world.ExactParticipantIds);
            Assert.Equal(2, world.NarrowParticipantIds.Count);
            Assert.Equal(world.ParticipantPoolSize, world.BroadParticipantIds.Count);
            Assert.All(world.ExactParticipantIds, participant =>
                Assert.Contains(participant, world.NarrowParticipantIds));
            Assert.All(world.NarrowParticipantIds, participant =>
                Assert.Contains(participant, world.BroadParticipantIds));
            var truthParticipant = world.Participants.Single(item => item.IsTruthParticipant);
            Assert.Contains(world.TruthRegionId, truthParticipant.AllowedRegionIds);
        }
    }

    [Fact]
    public void TwoHundredWorldsPreservePrimarySetInvariantsAndSafety()
    {
        var result = Result.Value;

        Assert.Equal(200, result.Summary.WorldCount);
        Assert.Equal(800, result.Summary.PairedProfileRunCount);
        Assert.Equal(400, result.Summary.FiniteTransitionCount);
        Assert.Equal(0, result.Summary.DerivedRegionMonotonicityViolationCount);
        Assert.Equal(0, result.Summary.CandidateSetMonotonicityViolationCount);
        Assert.Equal(0, result.Summary.FiniteDerivationFailureCount);
        Assert.Equal(0, result.Summary.FalseExclusionCount);
        Assert.Equal(0, result.Summary.SourceIndependenceViolationCount);
        Assert.Equal(0, result.Summary.UnknownEncounterAbsenceViolationCount);
        Assert.Equal(0, result.Summary.SkippedSeedCount);
        Assert.Equal(0, result.Summary.RegeneratedSeedCount);
        Assert.All(result.Runs, world =>
        {
            Assert.True(world.WorldValid);
            Assert.True(world.TruthEvaluatedAfterReconstruction);
            Assert.All(world.Profiles, profile =>
            {
                Assert.True(profile.ContainsTruth);
                Assert.False(profile.FalseExclusion);
            });
            Assert.All(world.MonotonicityAudits, audit =>
            {
                Assert.True(audit.ParticipantSetIsSubset);
                Assert.True(audit.ParticipantSetStrictlyWidened);
                Assert.True(audit.DerivedRegionSetIsSubset);
                Assert.True(audit.CandidateSetIsSubset);
            });
        });
    }

    [Fact]
    public void FixedRuleProducesTheUnionForEveryFiniteProfile()
    {
        foreach (var world in Result.Value.Runs)
        {
            foreach (var profile in world.Profiles.Where(item =>
                item.IdentityKnowledge != ParticipantIdentityKnowledge.UnknownIdentity))
            {
                var expected = world.ParticipantEvidence
                    .Where(item => profile.PossibleParticipantIds.Contains(
                        item.ParticipantId,
                        StringComparer.Ordinal))
                    .SelectMany(item => item.AllowedRegionIds)
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(item => item, StringComparer.Ordinal)
                    .ToArray();
                Assert.Equal(expected, profile.DerivedAllowedRegionIds);
                Assert.True(profile.DerivationSuccess);
                Assert.Equal(
                    EncounterParticipantLocationConstraintDeriver.RuleId,
                    profile.RuleId);
                Assert.Equal(PastStateEvidenceUsageRole.CandidateGeneration, profile.UsageRole);
            }

            var unknown = Profile(world, "unknown");
            Assert.True(unknown.EncounterObservationPresent);
            Assert.False(unknown.ConstraintGenerated);
            Assert.Empty(unknown.DerivedAllowedRegionIds);
            Assert.Equal(
                ParticipantConstraintDerivationFailureReason.UnknownParticipantIdentity,
                unknown.FailureReason);
        }
    }

    [Fact]
    public void PlateauClassificationsAreExhaustiveAndMatchTheirPropagationLayer()
    {
        var finite = Result.Value.Runs.SelectMany(item => item.MonotonicityAudits)
            .Where(item => item.Classification !=
                ParticipantPropagationClassification.UnknownBaselineTransition).ToArray();

        Assert.Equal(400, finite.Length);
        Assert.Equal(400, Result.Value.Summary.PropagationFrequencies.Sum(item =>
            item.TransitionCount));
        foreach (var audit in finite)
        {
            switch (audit.Classification)
            {
                case ParticipantPropagationClassification.ParticipantToRegionPlateau:
                    Assert.True(audit.DerivedRegionSetsEqual);
                    Assert.True(audit.CandidateSetsEqual);
                    break;
                case ParticipantPropagationClassification.RegionToCandidatePlateau:
                    Assert.True(audit.DerivedRegionSetStrictlyWidened);
                    Assert.True(audit.CandidateSetsEqual);
                    break;
                case ParticipantPropagationClassification.CandidateExpansion:
                    Assert.True(audit.DerivedRegionSetStrictlyWidened);
                    Assert.False(audit.CandidateSetsEqual);
                    break;
                default:
                    throw new Xunit.Sdk.XunitException(
                        $"Unexpected finite classification {audit.Classification}.");
            }
        }
    }

    [Fact]
    public void HeadlineMetricsRemainDeterministicForTheRegisteredFamily()
    {
        var aggregates = Result.Value.Aggregates;

        Assert.Equal([4.21m, 6.165m, 7.995m, 9.705m], aggregates.Select(item =>
            item.MeanCandidateCount));
        Assert.Equal([4m, 6m, 8m, 10m], aggregates.Select(item =>
            item.MedianCandidateCount));
        Assert.Equal([1, 1, 1, 6], aggregates.Select(item => item.MinimumCandidateCount));
        Assert.Equal([12, 14, 14, 14], aggregates.Select(item => item.MaximumCandidateCount));
        Assert.Equal([2.135m, 3.41m, 4.725m, 0m], aggregates.Select(item =>
            item.MeanDerivedRegionCount));
        Assert.Equal(143, Result.Value.Summary.ExactLessThanNarrowSeedCount);
        Assert.Equal(134, Result.Value.Summary.NarrowLessThanBroadSeedCount);
        Assert.Equal(119, Result.Value.Summary.BroadLessThanUnknownSeedCount);
        Assert.Equal(8, Result.Value.Summary.FiniteCandidateSetsAllEqualSeedCount);
        Assert.Equal(77, Result.Value.Summary.ParticipantWidenedRegionUnchangedSeedCount);
        Assert.Equal(78, Result.Value.Summary.ParticipantWidenedRegionUnchangedTransitionCount);
        Assert.Equal(43, Result.Value.Summary.RegionWidenedCandidateUnchangedSeedCount);
        Assert.Equal(45, Result.Value.Summary.RegionWidenedCandidateUnchangedTransitionCount);
        Assert.Equal(277, Result.Value.Summary.CandidateExpansionTransitionCount);
        Assert.Equal(81, Result.Value.Summary.BroadUnknownSaturationSeedCount);
    }

    [Fact]
    public void EvidenceOverlapClassificationMatchesGeneratedRegionSets()
    {
        foreach (var world in Result.Value.Runs)
        {
            Assert.Equal(ClassifyOverlap(world.ParticipantEvidence), world.EvidenceOverlapKind);
        }

        Assert.Equal(
            200,
            Result.Value.Summary.OverlapFrequencies.Sum(item => item.WorldCount));
        Assert.Equal(
            0,
            Result.Value.Summary.OverlapFrequencies.Single(item =>
                item.Kind == ParticipantEvidenceOverlapKind.Identical).WorldCount);
        Assert.Equal(
            11,
            Result.Value.Summary.OverlapFrequencies.Single(item =>
                item.Kind == ParticipantEvidenceOverlapKind.CompletelyDistinct).WorldCount);
        Assert.Equal(
            189,
            Result.Value.Summary.OverlapFrequencies.Single(item =>
                item.Kind == ParticipantEvidenceOverlapKind.PartiallyOverlapping).WorldCount);
    }

    [Fact]
    public void EncounterAlternativesRemainOneSourceWhileParticipantSourcesRemainDistinct()
    {
        Assert.All(Result.Value.Runs, world =>
        {
            Assert.Equal(
                world.ParticipantPoolSize,
                world.ParticipantEvidence.Select(item => item.SourceGroupId)
                    .Distinct(StringComparer.Ordinal).Count());
            Assert.Single(world.Profiles.Select(item => item.EncounterSourceGroupId)
                .Distinct(StringComparer.Ordinal));
            Assert.All(world.Profiles.Take(3), profile =>
            {
                Assert.Equal(1, profile.EncounterSourceGroupCount);
                Assert.Equal(1, profile.GeneratedConstraintSourceGroupCount);
                Assert.Equal(
                    profile.ParticipantAlternativeCount,
                    profile.ParticipantEvidenceSourceGroupCount);
            });
        });
    }

    [Fact]
    public void ChangingOnlyCompatibleTruthParticipantCannotChangePublicOutputs()
    {
        var world = Result.Value.Runs.First(run => run.ParticipantEvidence.Count(item =>
            item.AllowedRegionIds.Contains(run.TruthRegionId, StringComparer.Ordinal)) >= 2);
        var originalTruth = world.TruthParticipantId;
        var alternateTruth = world.ParticipantEvidence.First(item =>
            !string.Equals(item.ParticipantId, originalTruth, StringComparison.Ordinal) &&
            item.AllowedRegionIds.Contains(world.TruthRegionId, StringComparer.Ordinal)).ParticipantId;
        var originalEvaluationView = world with { TruthParticipantId = originalTruth };
        var alternateEvaluationView = world with { TruthParticipantId = alternateTruth };

        Assert.NotEqual(originalTruth, alternateTruth);
        Assert.Equal(
            PublicSignature(originalEvaluationView),
            PublicSignature(alternateEvaluationView));
        Assert.Equal(
            originalEvaluationView.Profiles.Select(item => item.DerivedAllowedRegionIds),
            alternateEvaluationView.Profiles.Select(item => item.DerivedAllowedRegionIds));
        Assert.Equal(
            originalEvaluationView.Profiles.Select(item => item.ConstraintId),
            alternateEvaluationView.Profiles.Select(item => item.ConstraintId));
        Assert.Equal(
            originalEvaluationView.Profiles.Select(item => item.CandidateIds),
            alternateEvaluationView.Profiles.Select(item => item.CandidateIds));
        Assert.DoesNotContain(
            typeof(EncounterParticipantLocationConstraintDeriver)
                .GetMethod(nameof(EncounterParticipantLocationConstraintDeriver.Derive))!
                .GetParameters(),
            parameter => parameter.Name!.Contains("truth", StringComparison.OrdinalIgnoreCase));
        Assert.False(Result.Value.Manifest.TruthUsedForDerivation);
    }

    [Fact]
    public void OutputAndWorldIdsAreDeterministic()
    {
        var first = RunEvaluation();
        var second = RunEvaluation();

        Assert.Equal(JsonDefaults.Serialize(first), JsonDefaults.Serialize(second));
        Assert.Equal(
            first.Runs.Select(item => item.PublicWorldId),
            second.Runs.Select(item => item.PublicWorldId));
        Assert.Equal(
            first.Runs.SelectMany(item => item.Profiles).Select(item => item.ConstraintId),
            second.Runs.SelectMany(item => item.Profiles).Select(item => item.ConstraintId));
    }

    [Fact]
    public async Task WriterEmitsRegisteredArtifactShapes()
    {
        var temporaryDirectory = Path.Combine(
            Path.GetTempPath(),
            $"footprint-sim-m1f11-{Guid.NewGuid():N}");
        try
        {
            await new ParticipantAmbiguityPairedOutputWriter().WriteAsync(
                temporaryDirectory,
                Result.Value);
            Assert.All(ParticipantAmbiguityPairedOutputNames.All, file =>
                Assert.True(File.Exists(Path.Combine(temporaryDirectory, file))));
            Assert.Equal(
                200,
                File.ReadLines(Path.Combine(
                    temporaryDirectory,
                    ParticipantAmbiguityPairedOutputNames.RunsJsonLines)).Count());
            Assert.Equal(
                601,
                File.ReadLines(Path.Combine(
                    temporaryDirectory,
                    ParticipantAmbiguityPairedOutputNames.MonotonicityCsv)).Count());
        }
        finally
        {
            if (Directory.Exists(temporaryDirectory))
            {
                Directory.Delete(temporaryDirectory, recursive: true);
            }
        }
    }

    [Fact]
    public void ExperimentRejectsUnregisteredOutputFields()
    {
        var source = File.ReadAllText(ExperimentPath);
        var modified = source.Replace(
            "\"profiles\": [",
            "\"expectedMeanCandidates\": 1,\n  \"profiles\": [",
            StringComparison.Ordinal);
        var temporaryDirectory = Path.Combine(
            Path.GetTempPath(),
            $"footprint-sim-m1f11-schema-{Guid.NewGuid():N}");
        Directory.CreateDirectory(temporaryDirectory);
        var path = Path.Combine(temporaryDirectory, "experiment.json");
        try
        {
            File.WriteAllText(path, modified);
            Assert.Throws<JsonException>(() => ParticipantAmbiguityPairedExperimentLoader.Load(path));
        }
        finally
        {
            Directory.Delete(temporaryDirectory, recursive: true);
        }
    }

    private static ParticipantAmbiguityPairedProfileRun Profile(
        ParticipantAmbiguityPairedWorldRun world,
        string profileId) => world.Profiles.Single(item => item.ProfileId == profileId);

    private static ParticipantEvidenceOverlapKind ClassifyOverlap(
        IReadOnlyList<ParticipantAmbiguityPairedEvidenceRun> participants)
    {
        var first = participants[0].AllowedRegionIds;
        if (participants.Skip(1).All(item => first.SequenceEqual(item.AllowedRegionIds)))
        {
            return ParticipantEvidenceOverlapKind.Identical;
        }

        for (var left = 0; left < participants.Count; left++)
        {
            for (var right = left + 1; right < participants.Count; right++)
            {
                if (participants[left].AllowedRegionIds.Any(region =>
                    participants[right].AllowedRegionIds.Contains(region, StringComparer.Ordinal)))
                {
                    return ParticipantEvidenceOverlapKind.PartiallyOverlapping;
                }
            }
        }

        return ParticipantEvidenceOverlapKind.CompletelyDistinct;
    }

    private static string PublicSignature(ParticipantAmbiguityPairedWorldRun world) => string.Join(
        ';',
        world.PublicWorldId,
        string.Join("|", world.Profiles.Select(item => item.SourceObservationId)),
        string.Join("|", world.Profiles.Select(item => item.ConstraintId)),
        string.Join("|", world.Profiles.SelectMany(item => item.DerivedAllowedRegionIds)),
        string.Join("|", world.Profiles.SelectMany(item => item.CandidateIds)));

    private static ParticipantAmbiguityPairedEvaluationResult RunEvaluation() =>
        new ParticipantAmbiguityPairedEvaluationRunner().Run(
            new ParticipantAmbiguityPairedEvaluationRequest
            {
                Experiment = Experiment(),
                Seeds = Enumerable.Range(0, 200).ToArray(),
                ExperimentContentSha256 = "m1f11-experiment-test-hash",
                SourceCommitSha = "m1f11-test-commit",
                DirtyWorkingTree = false
            });

    private static ParticipantAmbiguityPairedExperimentDefinition Experiment() =>
        ParticipantAmbiguityPairedExperimentLoader.Load(ExperimentPath);

    private static string ExperimentPath => Path.Combine(
        RepositoryRoot,
        "experiments",
        "m1f11_participant_ambiguity_paired",
        "experiment.json");

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
