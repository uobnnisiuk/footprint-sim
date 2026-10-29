using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using FootprintSim.Cli;
using FootprintSim.Evaluation;
using FootprintSim.Serialization;
using Xunit;

namespace FootprintSim.Core.Tests;

public sealed class M1F17Tests
{
    private static readonly int[] Seeds = Enumerable.Range(0, 200).ToArray();
    private static readonly Lazy<CommonWorldFieldCombinationEvaluationResult> Baseline = new(RunBaseline);

    [Fact]
    public void UnoccupiedCounterexampleRegionIsNotLogicalRedundancy()
    {
        // The two a candidates survive both profiles. Unoccupied c witnesses L not implying P.
        var row = new CommonWorldPlateauDomainClassifier().Audit(Counterexample());

        Assert.Equal(CommonWorldConditionalOutcomeClassification.ConstraintOverlapPlateau,
            row.Legacy.Classification);
        Assert.Equal(CommonWorldConditionalOutcomeClassification.ConstraintToCandidatePlateau,
            row.Registered.Classification);
        Assert.False(row.Registered.AddedPredicateImpliedByBaselinePredicates);
        Assert.Equal(["c"], row.Registered.ImplicationCounterexampleRegionIds);
        Assert.True(row.Occupied.AddedPredicateImpliedByBaselinePredicates);
        Assert.Equal(CommonWorldConditionalOutcomeClassification.ObservationToConstraintPlateau,
            row.Occupied.Classification);
        Assert.Equal(["c"], row.Extended.ImplicationCounterexampleRegionIds);
        Assert.Equal(["a10", "a11"], row.BaselineCandidateIds);
        Assert.Equal(row.BaselineCandidateIds, row.CombinedCandidateIds);
    }

    [Fact]
    public void ActualPredicateImplicationSurvivesDomainExtension()
    {
        var input = Counterexample() with { LocationAllowedRegionIds = ["a"] };
        var row = new CommonWorldPlateauDomainClassifier().Audit(input);

        Assert.True(row.Registered.AddedPredicateImpliedByBaselinePredicates);
        Assert.True(row.Extended.AddedPredicateImpliedByBaselinePredicates);
        Assert.Empty(row.Registered.ImplicationCounterexampleRegionIds);
        Assert.Equal(CommonWorldConditionalOutcomeClassification.ConstraintOverlapPlateau,
            row.Registered.Classification);
        Assert.Equal(row.Registered.Classification, row.Extended.Classification);
    }

    [Fact]
    public void SaturationHasPriorityWithoutHidingIndependentImplicationFailure()
    {
        var input = Counterexample();
        input = input with
        {
            BaselineCandidateIds = ["a10"],
            CombinedCandidateIds = ["a10"],
            LegacyDiagnostic = input.LegacyDiagnostic with
            {
                BaselineCandidateCount = 1,
                CombinedCandidateCount = 1,
                Classification = CommonWorldConditionalOutcomeClassification.Saturation
            }
        };
        var row = new CommonWorldPlateauDomainClassifier().Audit(input);

        Assert.Equal(CommonWorldConditionalOutcomeClassification.Saturation, row.Registered.Classification);
        Assert.Equal(CommonWorldConditionalOutcomeClassification.Saturation, row.Occupied.Classification);
        Assert.Equal(CommonWorldConditionalOutcomeClassification.Saturation, row.Extended.Classification);
        Assert.False(row.Registered.AddedPredicateImpliedByBaselinePredicates);
        Assert.Equal(["c"], row.Registered.ImplicationCounterexampleRegionIds);
    }

    [Theory]
    [InlineData(false, CommonWorldConditionalOutcomeClassification.ConstraintToCandidatePlateau)]
    [InlineData(true, CommonWorldConditionalOutcomeClassification.ObservationToConstraintPlateau)]
    public void TimeTopDefinitionAndClassificationDoNotDependOnRegionDomain(
        bool timeTop,
        CommonWorldConditionalOutcomeClassification expected)
    {
        var input = Counterexample();
        input = input with
        {
            ComparisonId = "time-added-to-location",
            CombinedProfileId = "location-time",
            AddedAxis = CommonWorldObservationAxis.Time,
            CombinedAxes = [CommonWorldObservationAxis.Location, CommonWorldObservationAxis.Time],
            TimePredicateIsTop = timeTop,
            LegacyDiagnostic = input.LegacyDiagnostic with
            {
                ComparisonId = "time-added-to-location",
                CombinedProfileId = "location-time",
                AddedAxis = CommonWorldObservationAxis.Time,
                AddedPredicateIsTopForCandidateDomain = timeTop,
                AddedPredicateImpliedByBaselinePredicates = false,
                Classification = expected
            }
        };
        var row = new CommonWorldPlateauDomainClassifier().Audit(input);

        Assert.Equal(expected, row.Registered.Classification);
        Assert.Equal(expected, row.Occupied.Classification);
        Assert.Equal(expected, row.Extended.Classification);
        Assert.Equal(timeTop, row.Registered.AddedPredicateIsTopForCandidateDomain);
        Assert.Equal(timeTop, row.Occupied.AddedPredicateIsTopForCandidateDomain);
        Assert.Equal(timeTop, row.Extended.AddedPredicateIsTopForCandidateDomain);
        Assert.False(row.Registered.AddedPredicateImpliedByBaselinePredicates);
        Assert.Empty(row.Registered.ImplicationCounterexampleRegionIds);
    }

    [Fact]
    public void OccupiedControlCanMakeARegionPredicateTopWithoutChangingCandidates()
    {
        var input = Counterexample();
        input = input with
        {
            ComparisonId = "participant-added-to-time",
            BaselineProfileId = "time",
            CombinedProfileId = "time-participant",
            BaselineAxes = [CommonWorldObservationAxis.Time],
            CombinedAxes = [CommonWorldObservationAxis.Time, CommonWorldObservationAxis.Participant],
            LegacyDiagnostic = input.LegacyDiagnostic with
            {
                ComparisonId = "participant-added-to-time",
                BaselineProfileId = "time",
                CombinedProfileId = "time-participant",
                AddedPredicateImpliedByBaselinePredicates = false,
                Classification = CommonWorldConditionalOutcomeClassification.ConstraintToCandidatePlateau
            }
        };
        var row = new CommonWorldPlateauDomainClassifier().Audit(input);

        Assert.False(row.Registered.AddedPredicateIsTopForCandidateDomain);
        Assert.True(row.Occupied.AddedPredicateIsTopForCandidateDomain);
        Assert.False(row.Extended.AddedPredicateIsTopForCandidateDomain);
        Assert.Equal(CommonWorldConditionalOutcomeClassification.ObservationToConstraintPlateau,
            row.Occupied.Classification);
        Assert.Equal(row.BaselineCandidateIds, row.CombinedCandidateIds);
    }

    [Fact]
    public void ExtendedControlRemovesTopButPreservesLogicalImplication()
    {
        var input = Counterexample();
        input = input with
        {
            ParticipantAllowedRegionIds = ["a", "b", "c"],
            LegacyDiagnostic = input.LegacyDiagnostic with
            {
                AddedPredicateIsTopForCandidateDomain = true,
                Classification = CommonWorldConditionalOutcomeClassification.ObservationToConstraintPlateau
            }
        };
        var row = new CommonWorldPlateauDomainClassifier().Audit(input);

        Assert.True(row.Registered.AddedPredicateIsTopForCandidateDomain);
        Assert.False(row.Extended.AddedPredicateIsTopForCandidateDomain);
        Assert.True(row.Registered.AddedPredicateImpliedByBaselinePredicates);
        Assert.True(row.Extended.AddedPredicateImpliedByBaselinePredicates);
        Assert.Equal(CommonWorldConditionalOutcomeClassification.ConstraintOverlapPlateau,
            row.Extended.Classification);
        Assert.Equal(["a", "b", "c", "m1f17-unused-region"], row.Extended.DomainRegionIds);
    }

    [Fact]
    public void SetOrderAndDuplicateRepresentationCannotChangeAuditIdentityOrOutput()
    {
        var input = Counterexample();
        var reordered = input with
        {
            RegisteredRegionIds = ["c", "a", "b", "c"],
            OccupiedRegionIds = ["b", "a", "a"],
            LocationAllowedRegionIds = ["c", "a", "c"],
            ParticipantAllowedRegionIds = ["b", "a", "a"],
            BaselineCandidateIds = ["a11", "a10", "a11"],
            CombinedCandidateIds = ["a11", "a10", "a10"],
            BaselineAxes = [CommonWorldObservationAxis.Location, CommonWorldObservationAxis.Location],
            CombinedAxes = [CommonWorldObservationAxis.Participant, CommonWorldObservationAxis.Location,
                CommonWorldObservationAxis.Participant]
        };
        var classifier = new CommonWorldPlateauDomainClassifier();

        Assert.Equal(JsonDefaults.Serialize(classifier.Audit(input)),
            JsonDefaults.Serialize(classifier.Audit(reordered)));
    }

    [Theory]
    [InlineData("undeclared-occupied")]
    [InlineData("undeclared-location")]
    [InlineData("undeclared-participant")]
    [InlineData("sentinel-collision")]
    [InlineData("empty-baseline")]
    [InlineData("empty-combined")]
    [InlineData("non-nested-candidates")]
    [InlineData("legacy-contradiction")]
    public void InvalidDomainOrPurifiedCandidateInputsFailInsteadOfBeingRepaired(string defect)
    {
        var input = Counterexample();
        input = defect switch
        {
            "undeclared-occupied" => input with { OccupiedRegionIds = ["a", "unknown"] },
            "undeclared-location" => input with { LocationAllowedRegionIds = ["a", "unknown"] },
            "undeclared-participant" => input with { ParticipantAllowedRegionIds = ["a", "unknown"] },
            "sentinel-collision" => input with { RegisteredRegionIds = ["a", "b", "c", "m1f17-unused-region"] },
            "empty-baseline" => input with { BaselineCandidateIds = [] },
            "empty-combined" => input with { CombinedCandidateIds = [] },
            "non-nested-candidates" => input with { CombinedCandidateIds = ["a10", "absent"] },
            "legacy-contradiction" => input with
            {
                LegacyDiagnostic = input.LegacyDiagnostic with
                {
                    AddedPredicateImpliedByBaselinePredicates = false
                }
            },
            _ => throw new ArgumentOutOfRangeException(nameof(defect))
        };

        Assert.Throws<InvalidDataException>(() => new CommonWorldPlateauDomainClassifier().Audit(input));
    }

    [Fact]
    public void ClassifierInputContractContainsOnlyPublicDiagnosticValues()
    {
        var inputType = typeof(CommonWorldPlateauDiagnosticInput);
        Assert.DoesNotContain(inputType.GetProperties(), property =>
            new[] { "Truth", "Private", "Occurrence", "Observation", "SourceGraph", "Rescue" }
                .Any(term => property.Name.Contains(term, StringComparison.OrdinalIgnoreCase)));
        Assert.DoesNotContain(inputType.GetProperties(), property =>
            property.PropertyType == typeof(GeneratedCommonFieldCombinationWorld) ||
            property.PropertyType == typeof(CommonWorldFieldCombinationWorldRun));
        var audit = typeof(CommonWorldPlateauDomainClassifier).GetMethod("Audit",
            BindingFlags.Instance | BindingFlags.Public)!;
        Assert.Equal([typeof(CommonWorldPlateauDiagnosticInput), typeof(string)],
            audit.GetParameters().Select(parameter => parameter.ParameterType));
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("extra")]
    [InlineData("duplicate")]
    public void ProtocolRejectsIncompleteOrRepeatedSeeds(string defect)
    {
        var protocol = CommonWorldPlateauDomainAuditProtocolLoader.Load(ProtocolPath);
        var invalidSeeds = defect switch
        {
            "missing" => Seeds.Skip(1).ToArray(),
            "extra" => Seeds.Append(200).ToArray(),
            "duplicate" => Seeds.SkipLast(1).Append(0).ToArray(),
            _ => throw new ArgumentOutOfRangeException(nameof(defect))
        };

        Assert.Throws<InvalidDataException>(() => CommonWorldPlateauDomainAuditValidator.Validate(
            protocol, protocol.BaselineExperimentSha256, invalidSeeds));
        CommonWorldPlateauDomainAuditValidator.Validate(protocol,
            protocol.BaselineExperimentSha256, Seeds.Reverse().ToArray());
    }

    [Fact]
    public void ProtocolRejectsChangedBaselineHashDomainRulesAndUndeclaredOutputs()
    {
        var protocol = CommonWorldPlateauDomainAuditProtocolLoader.Load(ProtocolPath);
        var hash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(ExperimentPath))).ToLowerInvariant();
        CommonWorldPlateauDomainAuditValidator.Validate(protocol, hash, Seeds);
        Assert.Throws<InvalidDataException>(() => CommonWorldPlateauDomainAuditValidator.Validate(
            protocol, new string('0', 64), Seeds));
        Assert.Throws<InvalidDataException>(() => CommonWorldPlateauDomainAuditValidator.Validate(
            protocol with { Domains = ["registered", "extended", "occupied"] }, hash, Seeds));
        Assert.Throws<InvalidDataException>(() => CommonWorldPlateauDomainAuditValidator.Validate(
            protocol with { TimeTopRule = "all-ticks-in-declared-range" }, hash, Seeds));
        Assert.Throws<InvalidDataException>(() => CommonWorldPlateauDomainAuditValidator.Validate(
            protocol with { LegacyCorrectionOutcomeAlreadyInspected = false }, hash, Seeds));
        var declaredResult = File.ReadAllText(ProtocolPath).Replace(
            "\"schemaVersion\": 1,", "\"schemaVersion\": 1,\n  \"expectedPlateauCount\": 35,",
            StringComparison.Ordinal);
        Assert.Throws<JsonException>(() => CommonWorldPlateauDomainAuditProtocolLoader.Parse(declaredResult));
    }

    [Fact]
    public void ValidHashCannotAuthorizeAnAlteredInMemoryGeneratorOrExperimentIdentity()
    {
        var request = AuditRequest();
        var renamed = request with
        {
            BaselineExperiment = request.BaselineExperiment with { Id = "m1f17-renamed-baseline" }
        };
        var resized = request with
        {
            BaselineExperiment = request.BaselineExperiment with
            {
                Generator = request.BaselineExperiment.Generator with { CandidateCountMaximum = 17 }
            }
        };

        Assert.Equal(request.BaselineExperimentContentSha256, renamed.BaselineExperimentContentSha256);
        Assert.Throws<InvalidDataException>(() => new CommonWorldPlateauDomainAuditRunner().Run(renamed));
        Assert.Throws<InvalidDataException>(() => new CommonWorldPlateauDomainAuditRunner().Run(resized));
    }

    [Theory]
    [InlineData("0..198")]
    [InlineData("0..200")]
    [InlineData("0..199,0")]
    public async Task CliRejectsMissingExtraAndDuplicateSeedsBeforeCreatingOutput(string seeds)
    {
        var output = Path.Combine(Path.GetTempPath(), $"footprint-m1f17-invalid-cli-{Guid.NewGuid():N}");
        var exitCode = await CliApplication.RunAsync(
        [
            "audit-common-world-plateau-domain", "--experiment", ProtocolPath,
            "--baseline-experiment", ExperimentPath, "--seeds", seeds, "--output", output
        ]);

        Assert.NotEqual(0, exitCode);
        Assert.False(Directory.Exists(output));
    }

    [Fact]
    public void ProjectionAndDiagnosticsAreUnchangedWhenCompatibleEvaluationTruthChanges()
    {
        var original = Baseline.Value.Runs.First(run => run.World.CandidateStates.Any(candidate =>
            !candidate.IsTruthCandidate && candidate.StartTick == run.World.EncounterTick &&
            run.World.LocationObservedRegionIds.Contains(candidate.RegionId, StringComparer.Ordinal) &&
            run.World.Participants.Single(participant => participant.IsTruthParticipant).AllowedRegionIds
                .Contains(candidate.RegionId, StringComparer.Ordinal)));
        var world = original.World;
        var alternate = world.CandidateStates.First(candidate =>
            !candidate.IsTruthCandidate && candidate.StartTick == world.EncounterTick &&
            world.LocationObservedRegionIds.Contains(candidate.RegionId, StringComparer.Ordinal) &&
            world.Participants.Single(participant => participant.IsTruthParticipant).AllowedRegionIds
                .Contains(candidate.RegionId, StringComparer.Ordinal));
        var changedWorld = world with
        {
            TruthWorldId = world.TruthWorldId + "-alternate-evaluation-truth",
            TruthCandidateKey = alternate.CandidateKey,
            TruthCandidateStartTick = alternate.StartTick,
            TruthCandidateEndTick = alternate.EndTick,
            TruthRegionId = alternate.RegionId,
            CandidateStates = world.CandidateStates.Select(candidate => candidate with
            {
                IsTruthCandidate = candidate.CandidateKey == alternate.CandidateKey
            }).ToArray()
        };
        var changed = new CommonWorldFieldCombinationWorldEvaluator().Evaluate(
            Experiment(), changedWorld, "m1f17-test", dirtyWorkingTree: false);

        Assert.NotEqual(original.TruthCandidateId, changed.TruthCandidateId);
        AssertProjectionEqual(original, changed);

        var participantOriginal = Baseline.Value.Runs.First(run => run.World.Participants.Any(participant =>
            !participant.IsTruthParticipant &&
            participant.AllowedRegionIds.Contains(run.World.TruthRegionId, StringComparer.Ordinal)));
        var participantWorld = participantOriginal.World;
        var alternateParticipant = participantWorld.Participants.First(participant =>
            !participant.IsTruthParticipant &&
            participant.AllowedRegionIds.Contains(participantWorld.TruthRegionId, StringComparer.Ordinal));
        var participantChangedWorld = participantWorld with
        {
            TruthWorldId = participantWorld.TruthWorldId + "-alternate-participant-truth",
            TruthParticipantId = alternateParticipant.ParticipantId,
            Participants = participantWorld.Participants.Select(participant => participant with
            {
                IsTruthParticipant = participant.ParticipantId == alternateParticipant.ParticipantId
            }).ToArray()
        };
        var participantChanged = new CommonWorldFieldCombinationWorldEvaluator().Evaluate(
            Experiment(), participantChangedWorld, "m1f17-test", dirtyWorkingTree: false);

        Assert.NotEqual(participantOriginal.World.TruthParticipantId, participantChanged.World.TruthParticipantId);
        AssertProjectionEqual(participantOriginal, participantChanged);
    }

    [Fact]
    public void FullRegisteredFamilyReproducesDisclosedCorrectionWithoutChangingAnyCandidateSet()
    {
        var rows = Baseline.Value.Runs.SelectMany(run => run.ConditionalReductions.Select(comparison =>
            new CommonWorldPlateauDomainClassifier().Audit(
                CommonWorldPlateauDiagnosticProjection.FromLegacy(run, comparison)))).ToArray();

        Assert.Equal(1800, rows.Length);
        Assert.Equal(Seeds, rows.Select(row => row.Seed).Distinct().Order());
        Assert.Equal(35, rows.Count(row => row.Legacy.Classification != row.Registered.Classification));
        Assert.Equal(40, rows.Count(row => row.Legacy.AddedPredicateImpliedByBaselinePredicates !=
            row.Registered.AddedPredicateImpliedByBaselinePredicates));
        Assert.Equal(19, rows.Where(row => row.Legacy.Classification != row.Registered.Classification)
            .Select(row => row.Seed).Distinct().Count());
        Assert.Equal(108, rows.Count(row => row.Registered.Classification ==
            CommonWorldConditionalOutcomeClassification.ConstraintOverlapPlateau));
        Assert.Equal(156, rows.Count(row => row.Registered.Classification ==
            CommonWorldConditionalOutcomeClassification.ConstraintToCandidatePlateau));
        Assert.Equal(429, rows.Count(row => row.BaselineCandidateIds.SequenceEqual(row.CombinedCandidateIds)));
        Assert.All(rows, row =>
        {
            var source = Baseline.Value.Runs.Single(run => run.Seed == row.Seed);
            Assert.Equal(source.Profiles.Single(profile => profile.ProfileId == row.BaselineProfileId).CandidateIds,
                row.BaselineCandidateIds);
            Assert.Equal(source.Profiles.Single(profile => profile.ProfileId == row.CombinedProfileId).CandidateIds,
                row.CombinedCandidateIds);
            Assert.All(row.CombinedCandidateIds, candidate => Assert.Contains(candidate, row.BaselineCandidateIds));
            if (row.Legacy.Classification != row.Registered.Classification)
            {
                Assert.Equal(CommonWorldConditionalOutcomeClassification.ConstraintOverlapPlateau,
                    row.Legacy.Classification);
                Assert.Equal(CommonWorldConditionalOutcomeClassification.ConstraintToCandidatePlateau,
                    row.Registered.Classification);
                Assert.NotEmpty(row.Registered.ImplicationCounterexampleRegionIds);
            }

            Assert.Equal(row.Registered.AddedPredicateImpliedByBaselinePredicates,
                row.Extended.AddedPredicateImpliedByBaselinePredicates);
            Assert.False(!row.Registered.AddedPredicateIsTopForCandidateDomain &&
                row.Extended.AddedPredicateIsTopForCandidateDomain);
        });
        Assert.Equal(0, Baseline.Value.Summary.FalseExclusionCount);
        Assert.Equal(0, Baseline.Value.Summary.JointSetInclusionViolationCount);
    }

    [Fact]
    public void FullRunnerIsDeterministicUnderSeedReorderingAndPreservesPublicInference()
    {
        var request = AuditRequest();
        var runner = new CommonWorldPlateauDomainAuditRunner();
        var first = runner.Run(request);
        var second = runner.Run(request with { Seeds = Seeds.Reverse().ToArray() });

        Assert.Equal(1800, first.Rows.Count);
        Assert.Equal(Seeds, first.Rows.Select(row => row.Seed).Distinct().Order());
        Assert.Equal(first.Manifest.PublicInferenceInputSignature, first.Manifest.PublicInferenceOutputSignature);
        Assert.Equal(JsonDefaults.Serialize(Baseline.Value), JsonDefaults.Serialize(first.BaselineResult));
        Assert.Equal(200, first.Summary.WorldCount);
        Assert.Equal(1800, first.Summary.ComparisonCount);
        Assert.Equal(429, first.Summary.ZeroAdditionalEffectCount);
        Assert.Equal(1371, first.Summary.PositiveAdditionalEffectCount);
        Assert.Equal(0, first.Summary.MigrationViolationCount);
        Assert.All(first.Summary.Migrations, migration =>
        {
            Assert.Equal(migration.ComparisonId == "all" ? 1800 : 200, migration.ComparisonCount);
            Assert.Equal(25, migration.ClassificationTransitions.Count);
            Assert.Equal(migration.ComparisonCount,
                migration.ClassificationTransitions.Sum(transition => transition.ComparisonCount));
        });
        Assert.All(first.Summary.DomainClassifications, domain =>
            Assert.Equal(domain.ComparisonId == "all" ? 1800 : 200,
                domain.ClassificationFrequencies.Sum(frequency => frequency.ComparisonCount)));
        Assert.Equal(JsonDefaults.Serialize(first), JsonDefaults.Serialize(second));
    }

    private static void AssertProjectionEqual(
        CommonWorldFieldCombinationWorldRun original,
        CommonWorldFieldCombinationWorldRun changed)
    {
        var classifier = new CommonWorldPlateauDomainClassifier();
        foreach (var comparison in original.ConditionalReductions)
        {
            var changedComparison = changed.ConditionalReductions.Single(item =>
                item.ComparisonId == comparison.ComparisonId);
            var first = CommonWorldPlateauDiagnosticProjection.FromLegacy(original, comparison);
            var second = CommonWorldPlateauDiagnosticProjection.FromLegacy(changed, changedComparison);
            Assert.Equal(JsonDefaults.Serialize(first), JsonDefaults.Serialize(second));
            Assert.Equal(JsonDefaults.Serialize(classifier.Audit(first)),
                JsonDefaults.Serialize(classifier.Audit(second)));
        }
    }

    private static CommonWorldPlateauDiagnosticInput Counterexample() => new()
    {
        Seed = 0,
        ComparisonId = "participant-added-to-location",
        BaselineProfileId = "location",
        CombinedProfileId = "location-participant",
        AddedAxis = CommonWorldObservationAxis.Participant,
        RegisteredRegionIds = ["a", "b", "c"],
        OccupiedRegionIds = ["a", "b"],
        LocationAllowedRegionIds = ["a", "c"],
        ParticipantAllowedRegionIds = ["a", "b"],
        BaselineCandidateIds = ["a10", "a11"],
        CombinedCandidateIds = ["a10", "a11"],
        BaselineAxes = [CommonWorldObservationAxis.Location],
        CombinedAxes = [CommonWorldObservationAxis.Location, CommonWorldObservationAxis.Participant],
        TimePredicateIsTop = false,
        LegacyDiagnostic = new CommonWorldConditionalReductionAudit
        {
            Seed = 0,
            ComparisonId = "participant-added-to-location",
            BaselineProfileId = "location",
            CombinedProfileId = "location-participant",
            AddedAxis = CommonWorldObservationAxis.Participant,
            BaselineCandidateCount = 2,
            CombinedCandidateCount = 2,
            AdditionalCandidateReduction = 0,
            CandidateSetsEqual = true,
            AddedPredicateIsTopForCandidateDomain = false,
            AddedPredicateImpliedByBaselinePredicates = true,
            Classification = CommonWorldConditionalOutcomeClassification.ConstraintOverlapPlateau
        }
    };

    private static CommonWorldFieldCombinationEvaluationResult RunBaseline() =>
        new CommonWorldFieldCombinationEvaluationRunner().Run(new CommonWorldFieldCombinationEvaluationRequest
        {
            Experiment = Experiment(),
            Seeds = Seeds,
            ExperimentContentSha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(ExperimentPath)))
                .ToLowerInvariant(),
            SourceCommitSha = "m1f17-test",
            DirtyWorkingTree = false
        });

    private static CommonWorldFieldCombinationExperimentDefinition Experiment() =>
        CommonWorldFieldCombinationExperimentLoader.Load(ExperimentPath);

    private static string HashFile(string path) =>
        Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();

    private static CommonWorldPlateauDomainAuditRequest AuditRequest() => new()
    {
        Protocol = CommonWorldPlateauDomainAuditProtocolLoader.Load(ProtocolPath),
        ProtocolContentSha256 = HashFile(ProtocolPath),
        BaselineExperiment = Experiment(),
        BaselineExperimentContentSha256 = HashFile(ExperimentPath),
        Seeds = Seeds,
        SourceCommitSha = "m1f17-test",
        DirtyWorkingTree = false
    };

    private static string ExperimentPath => Path.Combine(RepositoryRoot,
        "experiments", "m1f16_common_world_field_combination", "experiment.json");

    private static string ProtocolPath => Path.Combine(RepositoryRoot,
        "experiments", "m1f17_plateau_domain_audit", "experiment.json");

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
}
