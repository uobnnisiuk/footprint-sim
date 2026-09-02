using System.Text.Json;
using FootprintSim.Evaluation;
using FootprintSim.Inference.Derivation;
using FootprintSim.Interactions;
using FootprintSim.Serialization;
using Xunit;

namespace FootprintSim.Core.Tests;

public sealed class M1F16Tests
{
    private static readonly int[] Seeds = Enumerable.Range(0, 200).ToArray();

    private static readonly Lazy<CommonWorldFieldCombinationEvaluationResult> Result =
        new(RunEvaluation);

    [Fact]
    public void GeneratorUsesOnlyPreregisteredRangesWithoutSkippingOrRegeneration()
    {
        var experiment = Experiment();
        var generator = new CommonWorldFieldCombinationSeededWorldGenerator();
        var worlds = Seeds.Select(seed => generator.Generate(experiment, seed)).ToArray();

        Assert.Equal(Seeds, worlds.Select(item => item.Seed));
        Assert.All(worlds, world =>
        {
            Assert.InRange(world.CandidateCount, 8, 16);
            Assert.InRange(world.RegionUniverse.Count, 5, 9);
            Assert.InRange(world.CandidateBaseTick, 20, 40);
            Assert.InRange(world.DurationMinimumTicks, 3, 6);
            Assert.InRange(world.DurationMaximumTicks - world.DurationMinimumTicks, 0, 3);
            Assert.InRange(world.Participants.Count, 2, 4);
            Assert.Equal(world.CandidateCount, world.CandidateStates.Count);
            Assert.Equal(world.CandidateCount, world.CandidateStates
                .Select(item => (item.StartTick, item.RegionId)).Distinct().Count());
            Assert.Single(world.CandidateStates, item => item.IsTruthCandidate);
            Assert.Single(world.Participants, item => item.IsTruthParticipant);
            Assert.Contains(world.TruthRegionId, world.LocationObservedRegionIds);
            Assert.Contains(
                world.TruthRegionId,
                world.Participants.Single(item => item.IsTruthParticipant).AllowedRegionIds);
            Assert.Equal(
                world.EncounterTick + world.ActualTruthDurationTicks,
                world.LaterEventTick);
        });
        Assert.Equal(0, Result.Value.Summary.SkippedSeedCount);
        Assert.Equal(0, Result.Value.Summary.RegeneratedSeedCount);
    }

    [Fact]
    public void EveryWorldUsesTheSameEightProfilesAndFixedSemanticRules()
    {
        Assert.Equal(200, Result.Value.Summary.WorldCount);
        Assert.Equal(1600, Result.Value.Summary.ProfileEvaluationCount);
        Assert.All(Result.Value.Runs, run =>
        {
            Assert.Equal(
                [
                    "none", "location", "time", "participant", "location-time",
                    "location-participant", "time-participant", "location-time-participant"
                ],
                run.Profiles.Select(item => item.ProfileId));
            Assert.Single(run.Profiles.Select(item => item.CandidateUniverseId)
                .Distinct(StringComparer.Ordinal));
            Assert.All(run.Profiles, profile => Assert.True(profile.OmittedAxesMeanNotSelected));
            Assert.Equal(RelationKindKnowledge.Exact, run.DerivationAudit.RelationKnowledge);
            Assert.Equal(
                InteractionRelationKind.PersonPersonEncounter,
                run.DerivationAudit.ExactRelationKind);
        });
        Assert.Equal(
            ObservedRegionLocationConstraintDeriver.RuleId,
            Result.Value.Manifest.LocationRuleId);
        Assert.Equal(
            BoundedDurationObservationConstraintDeriver.RuleId,
            Result.Value.Manifest.TimeRuleId);
        Assert.Equal(
            EncounterParticipantLocationConstraintDeriver.RuleId,
            Result.Value.Manifest.ParticipantRuleId);
    }

    [Fact]
    public void JointSetInclusionSafetyAndPairingHoldPerWorld()
    {
        Assert.Equal(2400, Result.Value.Summary.SetInclusionAuditCount);
        Assert.Equal(0, Result.Value.Summary.JointSetInclusionViolationCount);
        Assert.Equal(0, Result.Value.Summary.FalseExclusionCount);
        Assert.Equal(0, Result.Value.Summary.AxisOrthogonalityViolationCount);
        Assert.Equal(0, Result.Value.Summary.CandidateUniverseInvarianceViolationCount);
        Assert.Equal(0, Result.Value.Summary.RelationInvarianceViolationCount);
        Assert.All(Result.Value.Runs, run =>
        {
            Assert.All(run.SetInclusionAudits, audit => Assert.True(audit.CandidateSetIsSubset));
            Assert.All(run.Profiles, profile =>
            {
                Assert.True(profile.ContainsTruth);
                Assert.False(profile.FalseExclusion);
            });
            Assert.True(run.AxisOrthogonalityPreserved);
            Assert.True(run.CandidateUniverseInvariant);
            Assert.True(run.RelationInvariant);
        });
    }

    [Fact]
    public void ParticipantAlternativesRemainPreUnionedAndSourceGroupsDoNotSelectOperators()
    {
        Assert.All(Result.Value.Runs, run =>
        {
            var participant = run.Profiles.Single(item => item.ProfileId == "participant");
            Assert.Single(participant.CompositionAudit.Groups);
            Assert.Single(participant.CompositionAudit.Groups[0].Components);
            Assert.Equal(0, participant.CompositionAudit.AlternativeBranchesEvaluated);
            Assert.Equal(
                run.World.Participants.SelectMany(item => item.AllowedRegionIds)
                    .Distinct(StringComparer.Ordinal).OrderBy(item => item, StringComparer.Ordinal),
                run.DerivedParticipantAllowedRegionIds);
            Assert.True(participant.SelectedSupportingSourceGroupCount >= 3);
        });
        Assert.DoesNotContain(
            typeof(CommonWorldProfileDefinition).GetProperties(),
            property => property.Name.Contains("Source", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void PublicCompatibleTruthChangesDoNotChangeDerivationCompositionOrCandidateSets()
    {
        var experiment = Experiment();
        var generator = new CommonWorldFieldCombinationSeededWorldGenerator();
        var candidateWorld = Seeds.Select(seed => generator.Generate(experiment, seed)).First(world =>
        {
            var participantRegions = world.Participants.Single(item => item.IsTruthParticipant)
                .AllowedRegionIds
                .ToHashSet(StringComparer.Ordinal);
            return world.CandidateStates.Any(candidate =>
                !candidate.IsTruthCandidate &&
                candidate.StartTick == world.EncounterTick &&
                world.LocationObservedRegionIds.Contains(candidate.RegionId, StringComparer.Ordinal) &&
                participantRegions.Contains(candidate.RegionId));
        });
        var alternateCandidate = candidateWorld.CandidateStates.First(candidate =>
            !candidate.IsTruthCandidate &&
            candidate.StartTick == candidateWorld.EncounterTick &&
            candidateWorld.LocationObservedRegionIds.Contains(candidate.RegionId, StringComparer.Ordinal) &&
            candidateWorld.Participants.Single(item => item.IsTruthParticipant).AllowedRegionIds
                .Contains(candidate.RegionId, StringComparer.Ordinal));
        var candidateChanged = candidateWorld with
        {
            TruthWorldId = candidateWorld.TruthWorldId + "-alternate-candidate",
            TruthCandidateKey = alternateCandidate.CandidateKey,
            TruthCandidateStartTick = alternateCandidate.StartTick,
            TruthCandidateEndTick = alternateCandidate.EndTick,
            TruthRegionId = alternateCandidate.RegionId,
            CandidateStates = candidateWorld.CandidateStates.Select(item => item with
            {
                IsTruthCandidate = item.CandidateKey == alternateCandidate.CandidateKey
            }).ToArray()
        };
        var candidateOriginal = Evaluate(experiment, candidateWorld);
        var candidateAlternate = Evaluate(experiment, candidateChanged);
        Assert.Equal(PublicSignature(candidateOriginal), PublicSignature(candidateAlternate));
        Assert.NotEqual(candidateOriginal.TruthCandidateId, candidateAlternate.TruthCandidateId);

        var participantWorld = Seeds.Select(seed => generator.Generate(experiment, seed)).First(world =>
            world.Participants.Any(item =>
                !item.IsTruthParticipant &&
                item.AllowedRegionIds.Contains(world.TruthRegionId, StringComparer.Ordinal)));
        var alternateParticipant = participantWorld.Participants.First(item =>
            !item.IsTruthParticipant &&
            item.AllowedRegionIds.Contains(participantWorld.TruthRegionId, StringComparer.Ordinal));
        var participantChanged = participantWorld with
        {
            TruthWorldId = participantWorld.TruthWorldId + "-alternate-participant",
            TruthParticipantId = alternateParticipant.ParticipantId,
            Participants = participantWorld.Participants.Select(item => item with
            {
                IsTruthParticipant = item.ParticipantId == alternateParticipant.ParticipantId
            }).ToArray()
        };
        var participantOriginal = Evaluate(experiment, participantWorld);
        var participantAlternate = Evaluate(experiment, participantChanged);
        Assert.Equal(PublicSignature(participantOriginal), PublicSignature(participantAlternate));
        Assert.NotEqual(
            participantOriginal.World.TruthParticipantId,
            participantAlternate.World.TruthParticipantId);
    }

    [Fact]
    public void EvaluationAndAllRegisteredArtifactsAreDeterministic()
    {
        var first = RunEvaluation();
        var second = RunEvaluation();

        Assert.Equal(JsonDefaults.Serialize(first), JsonDefaults.Serialize(second));
    }

    [Fact]
    public async Task WriterEmitsRegisteredArtifactShapes()
    {
        var firstDirectory = Path.Combine(
            Path.GetTempPath(),
            $"footprint-sim-m1f16-first-{Guid.NewGuid():N}");
        var secondDirectory = Path.Combine(
            Path.GetTempPath(),
            $"footprint-sim-m1f16-second-{Guid.NewGuid():N}");
        try
        {
            var writer = new CommonWorldFieldCombinationPairedOutputWriter();
            await writer.WriteAsync(firstDirectory, Result.Value);
            await writer.WriteAsync(secondDirectory, Result.Value);
            Assert.All(CommonWorldFieldCombinationOutputNames.All, file =>
                Assert.Equal(
                    File.ReadAllBytes(Path.Combine(firstDirectory, file)),
                    File.ReadAllBytes(Path.Combine(secondDirectory, file))));
            Assert.Equal(200, File.ReadLines(Path.Combine(
                firstDirectory,
                CommonWorldFieldCombinationOutputNames.RunsJsonLines)).Count());
            Assert.Equal(1801, File.ReadLines(Path.Combine(
                firstDirectory,
                CommonWorldFieldCombinationOutputNames.ConditionalReductionsCsv)).Count());
            Assert.Equal(2401, File.ReadLines(Path.Combine(
                firstDirectory,
                CommonWorldFieldCombinationOutputNames.SetInclusionCsv)).Count());
        }
        finally
        {
            if (Directory.Exists(firstDirectory)) Directory.Delete(firstDirectory, recursive: true);
            if (Directory.Exists(secondDirectory)) Directory.Delete(secondDirectory, recursive: true);
        }
    }

    [Fact]
    public void ExperimentRejectsUnregisteredResultExpectations()
    {
        var source = File.ReadAllText(ExperimentPath);
        var modified = source.Replace(
            "\"profiles\": [",
            "\"expectedMeanCandidates\": 1,\n  \"profiles\": [",
            StringComparison.Ordinal);
        var directory = Path.Combine(
            Path.GetTempPath(),
            $"footprint-sim-m1f16-schema-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            var path = Path.Combine(directory, "experiment.json");
            File.WriteAllText(path, modified);
            Assert.Throws<JsonException>(() => CommonWorldFieldCombinationExperimentLoader.Load(path));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static CommonWorldFieldCombinationWorldRun Evaluate(
        CommonWorldFieldCombinationExperimentDefinition experiment,
        GeneratedCommonFieldCombinationWorld world) =>
        new CommonWorldFieldCombinationWorldEvaluator().Evaluate(
            experiment,
            world,
            "m1f16-truth-isolation-test",
            dirtyWorkingTree: false);

    private static string PublicSignature(CommonWorldFieldCombinationWorldRun run) => string.Join(
        ';',
        run.World.PublicWorldId,
        run.CandidateUniverseId,
        string.Join("|", run.DerivedLocationAllowedRegionIds),
        run.DerivedHistoricalTimeWindow,
        string.Join("|", run.DerivedParticipantAllowedRegionIds),
        string.Join("|", run.Profiles.Select(item => string.Join(
            ':', item.CompositionId, string.Join("+", item.CandidateIds), item.CandidateGenerationWork))));

    private static CommonWorldFieldCombinationEvaluationResult RunEvaluation() =>
        new CommonWorldFieldCombinationEvaluationRunner().Run(
            new CommonWorldFieldCombinationEvaluationRequest
            {
                Experiment = Experiment(),
                Seeds = Seeds,
                ExperimentContentSha256 = "m1f16-experiment-test-hash",
                SourceCommitSha = "m1f16-test-commit",
                DirtyWorkingTree = false
            });

    private static CommonWorldFieldCombinationExperimentDefinition Experiment() =>
        CommonWorldFieldCombinationExperimentLoader.Load(ExperimentPath);

    private static string ExperimentPath => Path.Combine(
        RepositoryRoot,
        "experiments",
        "m1f16_common_world_field_combination",
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
