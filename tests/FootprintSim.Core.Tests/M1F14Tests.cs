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

public sealed class M1F14Tests
{
    private static readonly Lazy<ConstraintCompositionFixtureEvaluationResult> Result =
        new(RunFixture);

    [Fact]
    public void EightProfilesApplyThePreregisteredJointPredicates()
    {
        Assert.Equal(
            [
                ("none", 6),
                ("location", 4),
                ("time", 4),
                ("participant", 3),
                ("location-time", 2),
                ("location-participant", 2),
                ("time-participant", 2),
                ("location-time-participant", 1)
            ],
            Result.Value.Runs.Select(item => (item.ProfileId, item.CandidateCount)));
        Assert.Equal(
            CandidateConstraintCompositionStatus.NonRestricting,
            Run("none").Status);
        Assert.All(
            Result.Value.Runs.Where(item => item.ProfileId != "none"),
            item => Assert.Equal(CandidateConstraintCompositionStatus.Applied, item.Status));
    }

    [Fact]
    public void JointProfilesEqualIntersectionsOfSingleAxisResults()
    {
        Assert.Equal(0, Result.Value.IntersectionViolationCount);
        Assert.All(Result.Value.IntersectionAudits, audit =>
        {
            Assert.True(audit.MatchesIntersection);
            Assert.Equal(audit.ExpectedCandidateIds, audit.ActualCandidateIds);
        });
        Assert.All(Result.Value.Runs, run => Assert.True(
            run.CandidateIds.All(candidateId =>
                Run("none").CandidateIds.Contains(candidateId, StringComparer.Ordinal))));
    }

    [Fact]
    public void FixtureKeepsDimensionsExplicitAndParticipantAlternativesPreUnioned()
    {
        Assert.Equal(
            CandidateConstraintDimensionRestriction.Restricted,
            Result.Value.LocationComponent.Predicate.RegionRestriction);
        Assert.Equal(
            CandidateConstraintDimensionRestriction.Unrestricted,
            Result.Value.LocationComponent.Predicate.TimeRestriction);
        Assert.Equal(["east", "west"], Result.Value.LocationComponent.Predicate.AllowedRegionIds);

        Assert.Equal(
            CandidateConstraintDimensionRestriction.Unrestricted,
            Result.Value.TimeComponent.Predicate.RegionRestriction);
        Assert.Equal(
            CandidateConstraintDimensionRestriction.Restricted,
            Result.Value.TimeComponent.Predicate.TimeRestriction);
        Assert.Equal(
            new InteractionTimeWindow(10, 11),
            Result.Value.TimeComponent.Predicate.ConstrainedTimeWindow);

        Assert.Equal(
            EncounterParticipantLocationConstraintDeriver.RuleId,
            Result.Value.ParticipantComponent.DerivationRuleId);
        Assert.Equal(
            CandidateConstraintDimensionRestriction.Restricted,
            Result.Value.ParticipantComponent.Predicate.RegionRestriction);
        Assert.Equal(
            CandidateConstraintDimensionRestriction.Unrestricted,
            Result.Value.ParticipantComponent.Predicate.TimeRestriction);
        Assert.Equal(
            ["north", "west"],
            Result.Value.ParticipantComponent.Predicate.AllowedRegionIds);
    }

    [Fact]
    public void TruthSafetyHoldsForEveryFunctionalProfile()
    {
        Assert.Equal(8, Result.Value.ContainmentCount);
        Assert.Equal(0, Result.Value.FalseExclusionCount);
        Assert.All(Result.Value.Runs, run =>
        {
            Assert.True(run.ContainsTruth);
            Assert.False(run.FalseExclusion);
        });
        Assert.False(Result.Value.Manifest.TruthUsedForComposition);
        Assert.Equal(InteractionRelationKind.PersonPersonEncounter, Result.Value.Manifest.FixedRelationKind);
    }

    [Fact]
    public void InputOrderDoesNotChangeIdentityCandidatesOrNormalizedProvenance()
    {
        var location = Component(CandidateConstraintPredicate.Region(["east", "west"]), "location");
        var time = Component(CandidateConstraintPredicate.Time(new InteractionTimeWindow(10, 11)), "time");
        var participant = Component(CandidateConstraintPredicate.Region(["north", "west"]), "participant");
        var forward = Compose(
            Joint(location),
            Joint(time),
            Joint(participant));
        var reverse = Compose(
            Joint(participant),
            Joint(time),
            Joint(location));

        Assert.Equal(forward.CompositionId, reverse.CompositionId);
        Assert.Equal(
            forward.Candidates.Select(item => item.CandidateId),
            reverse.Candidates.Select(item => item.CandidateId));
        Assert.Equal(forward.Audit.SourceObservationIds, reverse.Audit.SourceObservationIds);
        Assert.Equal(forward.Audit.SourceAncestryIds, reverse.Audit.SourceAncestryIds);
        Assert.True(Result.Value.Manifest.InputOrderIndependent);
    }

    [Fact]
    public void AlternativeUsesOrAcrossIndependentSources()
    {
        var east = Component(CandidateConstraintPredicate.Region(["east"]), "east", "source-east");
        var west = Component(CandidateConstraintPredicate.Region(["west"]), "west", "source-west");
        var alternative = CandidateGenerationConstraintGroup.Create(
            CandidateConstraintLogicalRole.Alternative,
            [west, east]);
        var result = Compose(alternative);

        Assert.Equal(4, result.Candidates.Count);
        Assert.Equal(["east", "west"], result.Candidates
            .Select(item => item.RegionId)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(item => item, StringComparer.Ordinal));
        Assert.Equal(2, result.Audit.DistinctSourceGroupCount);
        Assert.Equal(2, result.Audit.AlternativeBranchesEvaluated);
        Assert.Equal(CandidateConstraintLogicalRole.Alternative, result.Audit.Groups[0].LogicalRole);
    }

    [Fact]
    public void SameSourceJointFieldsUseAndWithoutIncreasingSourceCount()
    {
        var location = Component(
            CandidateConstraintPredicate.Region(["east", "west"]),
            "location",
            "shared-source");
        var time = Component(
            CandidateConstraintPredicate.Time(new InteractionTimeWindow(10, 11)),
            "time",
            "shared-source");
        var group = CandidateGenerationConstraintGroup.Create(
            CandidateConstraintLogicalRole.JointRequirement,
            [location, time]);
        var result = Compose(group);

        Assert.Equal(2, result.Candidates.Count);
        Assert.Equal(1, result.Audit.DistinctSourceGroupCount);
        Assert.Equal(2, result.Audit.PredicatesApplied);
        Assert.Equal(CandidateConstraintLogicalRole.JointRequirement, result.Audit.Groups[0].LogicalRole);
    }

    [Fact]
    public void EquivalentSupportAppliesOnePredicateAndKeepsBothProvenances()
    {
        var first = Component(
            CandidateConstraintPredicate.Region(["east", "west"]),
            "first",
            "source-one");
        var second = Component(
            CandidateConstraintPredicate.Region(["west", "east"]),
            "second",
            "source-two");
        var group = CandidateGenerationConstraintGroup.Create(
            CandidateConstraintLogicalRole.EquivalentSupport,
            [first, second]);
        var result = Compose(group);

        Assert.Equal(4, result.Candidates.Count);
        Assert.Equal(1, result.Audit.PredicatesApplied);
        Assert.Equal(1, result.Audit.DuplicateConstraintsSuppressed);
        Assert.Equal(2, result.Audit.Groups[0].Components.Count);
        Assert.Equal(2, result.Audit.SourceObservationIds.Count);
        Assert.Equal(2, result.Audit.DistinctSourceGroupCount);
        Assert.All(result.Audit.Groups[0].Components, item => Assert.NotEmpty(item.Provenance));
    }

    [Fact]
    public void RepeatedArtifactIsNotCountedAsIndependentEvidence()
    {
        var location = Component(
            CandidateConstraintPredicate.Region(["east", "west"]),
            "location",
            "one-source");
        var group = CandidateGenerationConstraintGroup.Create(
            CandidateConstraintLogicalRole.JointRequirement,
            [location, location]);
        var result = Compose(group);

        Assert.Equal(4, result.Candidates.Count);
        Assert.Equal(1, result.Audit.ConstraintsReceived);
        Assert.Equal(1, result.Audit.DistinctSourceGroupCount);
        Assert.Equal(1, result.Audit.DuplicateConstraintsSuppressed);
    }

    [Fact]
    public void UnknownUnavailableAndExplicitTopRemainNonRestrictingButAuditable()
    {
        var unknown = NonRestricting(CandidateConstraintInputState.Unknown, "unknown", 10);
        var unavailable = NonRestricting(CandidateConstraintInputState.Unavailable, "future", 17);
        var explicitTop = NonRestricting(
            CandidateConstraintInputState.ExplicitNoRestriction,
            "top",
            10);
        var group = CandidateGenerationConstraintGroup.Create(
            CandidateConstraintLogicalRole.NonRestricting,
            [unknown, unavailable, explicitTop]);
        var result = Compose(group);

        Assert.Equal(CandidateConstraintCompositionStatus.NonRestricting, result.Status);
        Assert.Equal(6, result.Candidates.Count);
        Assert.Equal(0, result.Audit.ConstraintsReceived);
        Assert.Equal(0, result.Audit.PredicatesApplied);
        Assert.Equal(3, result.Audit.InputComponentsReceived);
        Assert.Equal(
            [
                CandidateConstraintInputState.Unknown,
                CandidateConstraintInputState.Unavailable,
                CandidateConstraintInputState.ExplicitNoRestriction
            ],
            result.Audit.Groups[0].Components
                .Select(item => item.InputState)
                .OrderBy(item => item));
        Assert.All(result.Audit.Groups[0].Components, item => Assert.True(
            item.Predicate.IsNonRestricting));
    }

    [Fact]
    public void NoConstraintDoesNotMeanFalsePredicate()
    {
        var result = Compose();

        Assert.Equal(CandidateConstraintCompositionStatus.NonRestricting, result.Status);
        Assert.Equal(result.InitialCandidateCount, result.Candidates.Count);
        Assert.Empty(result.Audit.Groups);
        Assert.Empty(result.Audit.SourceObservationIds);
    }

    [Fact]
    public void EmptyJointResultIsExplicitlyUnsatisfiedWithoutFallback()
    {
        var east = Component(CandidateConstraintPredicate.Region(["east"]), "east");
        var north = Component(CandidateConstraintPredicate.Region(["north"]), "north");
        var result = Compose(Joint(east), Joint(north));

        Assert.Equal(CandidateConstraintCompositionStatus.Unsatisfied, result.Status);
        Assert.Empty(result.Candidates);
        Assert.NotEqual(result.InitialCandidateCount, result.Candidates.Count);
        Assert.Equal(
            CandidateConstraintCompositionFailureReason.UnsatisfiedJointRequirements,
            result.FailureReason);
    }

    [Fact]
    public void DifferentTargetAndWrongUsageAreRejectedAsInvalid()
    {
        var otherTarget = Component(
            CandidateConstraintPredicate.Region(["east"]),
            "other-target",
            targetEntityId: "person-z");
        var wrongUsage = Component(
            CandidateConstraintPredicate.Region(["east"]),
            "wrong-usage",
            usageRole: PastStateEvidenceUsageRole.CandidateVerification);

        var targetResult = Compose(Joint(otherTarget));
        var usageResult = Compose(Joint(wrongUsage));

        Assert.Equal(CandidateConstraintCompositionStatus.Invalid, targetResult.Status);
        Assert.Equal(CandidateConstraintCompositionFailureReason.TargetMismatch, targetResult.FailureReason);
        Assert.Empty(targetResult.Candidates);
        Assert.Equal(CandidateConstraintCompositionStatus.Invalid, usageResult.Status);
        Assert.Equal(
            CandidateConstraintCompositionFailureReason.UnsupportedUsageRole,
            usageResult.FailureReason);
        Assert.Empty(usageResult.Candidates);
    }

    [Fact]
    public void DifferentDomainContextAndPrivateInputAreRejected()
    {
        var domain = Component(
            CandidateConstraintPredicate.Region(["east"]),
            "other-domain",
            candidateDomainId: "different-domain");
        var context = Component(
            CandidateConstraintPredicate.Region(["east"]),
            "other-context",
            targetStateContextId: "different-context");
        var privateInput = Component(
            CandidateConstraintPredicate.Region(["east"]),
            "private",
            visibility: InteractionVisibility.PrivateTruth);

        Assert.Equal(
            CandidateConstraintCompositionFailureReason.CandidateDomainMismatch,
            Compose(Joint(domain)).FailureReason);
        Assert.Equal(
            CandidateConstraintCompositionFailureReason.TargetStateContextMismatch,
            Compose(Joint(context)).FailureReason);
        Assert.Equal(
            CandidateConstraintCompositionFailureReason.PrivateInput,
            Compose(Joint(privateInput)).FailureReason);
    }

    [Fact]
    public void UnavailableConstraintCannotBeMarkedApplicable()
    {
        var unavailable = Component(
            CandidateConstraintPredicate.Region(["east"]),
            "future-active",
            availableAtTick: 17);
        var result = Compose(Joint(unavailable));

        Assert.Equal(CandidateConstraintCompositionStatus.Invalid, result.Status);
        Assert.Equal(
            CandidateConstraintCompositionFailureReason.ApplicableInputUnavailable,
            result.FailureReason);
        Assert.Empty(result.Candidates);
    }

    [Fact]
    public void NonEquivalentPredicatesCannotClaimEquivalentSupport()
    {
        var east = Component(CandidateConstraintPredicate.Region(["east"]), "east");
        var west = Component(CandidateConstraintPredicate.Region(["west"]), "west");
        var result = Compose(CandidateGenerationConstraintGroup.Create(
            CandidateConstraintLogicalRole.EquivalentSupport,
            [east, west]));

        Assert.Equal(CandidateConstraintCompositionStatus.Invalid, result.Status);
        Assert.Equal(
            CandidateConstraintCompositionFailureReason.EquivalentPredicateMismatch,
            result.FailureReason);
    }

    [Fact]
    public void AlternativeWithUnknownBranchCannotUseUnsafePartialUnion()
    {
        var east = Component(CandidateConstraintPredicate.Region(["east"]), "east");
        var unknown = NonRestricting(CandidateConstraintInputState.Unknown, "unknown-branch", 10);
        var result = Compose(CandidateGenerationConstraintGroup.Create(
            CandidateConstraintLogicalRole.Alternative,
            [east, unknown]));

        Assert.Equal(CandidateConstraintCompositionStatus.Invalid, result.Status);
        Assert.Equal(
            CandidateConstraintCompositionFailureReason.InvalidLogicalGroup,
            result.FailureReason);
        Assert.Empty(result.Candidates);
    }

    [Fact]
    public void ExistingTimeAndParticipantConstraintsEnterCompositionWithoutTruthInputs()
    {
        Assert.Equal(
            BoundedDurationObservationConstraintDeriver.RuleId,
            Result.Value.TimeComponent.DerivationRuleId);
        Assert.NotNull(Result.Value.TimeComponent.ConstraintId);
        Assert.Equal(
            EncounterParticipantLocationConstraintDeriver.RuleId,
            Result.Value.ParticipantComponent.DerivationRuleId);
        Assert.NotNull(Result.Value.ParticipantComponent.ConstraintId);
        Assert.DoesNotContain(
            typeof(CandidateGenerationConstraintCompositionRequest).GetProperties(),
            property => property.Name.Contains("Truth", StringComparison.OrdinalIgnoreCase) ||
                property.Name.Contains("Realized", StringComparison.OrdinalIgnoreCase));
        var compose = typeof(CandidateGenerationConstraintComposer)
            .GetMethod(nameof(CandidateGenerationConstraintComposer.Compose));
        Assert.DoesNotContain(compose!.GetParameters(), parameter =>
            parameter.Name!.Contains("truth", StringComparison.OrdinalIgnoreCase) ||
            parameter.ParameterType.Name.Contains("Truth", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void EvaluationTruthAddressDoesNotChangePublicComposition()
    {
        var original = RunFixture();
        var alternate = RunFixture(Fixture() with
        {
            TruthCandidateStartTick = 10,
            TruthCandidateEndTick = 10,
            TruthCandidateRegionId = "east"
        });

        Assert.Equal(
            original.Runs.Select(PublicSignature),
            alternate.Runs.Select(PublicSignature));
        Assert.NotEqual(original.Runs[0].TruthCandidateId, alternate.Runs[0].TruthCandidateId);
        Assert.False(original.Manifest.TruthUsedForComposition);
        Assert.False(alternate.Manifest.TruthUsedForComposition);
    }

    [Fact]
    public void FixtureCannotDeclareCandidateResults()
    {
        var source = File.ReadAllText(FixturePath);
        var modified = source.Replace(
            "\"profiles\": [",
            "\"candidateResults\": [\"west\"],\n  \"profiles\": [",
            StringComparison.Ordinal);
        var temporaryDirectory = Path.Combine(
            Path.GetTempPath(),
            $"footprint-sim-m1f14-loader-{Guid.NewGuid():N}");
        Directory.CreateDirectory(temporaryDirectory);
        try
        {
            var path = Path.Combine(temporaryDirectory, "fixture.json");
            File.WriteAllText(path, modified);
            Assert.Throws<JsonException>(() => ConstraintCompositionFixtureLoader.Load(path));
        }
        finally
        {
            Directory.Delete(temporaryDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task FixtureAndArtifactsAreDeterministic()
    {
        var first = RunFixture();
        var second = RunFixture();
        Assert.Equal(JsonDefaults.Serialize(first), JsonDefaults.Serialize(second));

        var firstDirectory = Path.Combine(
            Path.GetTempPath(),
            $"footprint-sim-m1f14-first-{Guid.NewGuid():N}");
        var secondDirectory = Path.Combine(
            Path.GetTempPath(),
            $"footprint-sim-m1f14-second-{Guid.NewGuid():N}");
        try
        {
            var writer = new ConstraintCompositionFixtureOutputWriter();
            await writer.WriteAsync(firstDirectory, first);
            await writer.WriteAsync(secondDirectory, second);
            foreach (var name in ConstraintCompositionFixtureOutputNames.All)
            {
                Assert.Equal(
                    await File.ReadAllBytesAsync(Path.Combine(firstDirectory, name)),
                    await File.ReadAllBytesAsync(Path.Combine(secondDirectory, name)));
            }
        }
        finally
        {
            Directory.Delete(firstDirectory, recursive: true);
            Directory.Delete(secondDirectory, recursive: true);
        }
    }

    private static ConstraintCompositionFixtureRun Run(string profileId) =>
        Result.Value.Runs.Single(item => item.ProfileId == profileId);

    private static CandidateGenerationConstraintCompositionResult Compose(
        params CandidateGenerationConstraintGroup[] groups) =>
        new CandidateGenerationConstraintComposer().Compose(new CandidateGenerationConstraintCompositionRequest
        {
            ScenarioId = ScenarioId,
            Seed = 0,
            TargetEntityId = "person-a",
            TargetStateContextId = TargetStateContextId,
            CandidateDomainId = CandidateDomainId,
            EvidenceAvailabilityCutoff = 16,
            UsageRole = PastStateEvidenceUsageRole.CandidateGeneration,
            PublicCandidateStates = CandidateStates(),
            ConstraintGroups = groups
        });

    private static CandidateGenerationConstraintGroup Joint(
        CandidateGenerationConstraintComponent component) =>
        CandidateGenerationConstraintGroup.Create(
            CandidateConstraintLogicalRole.JointRequirement,
            [component]);

    private static CandidateGenerationConstraintComponent Component(
        CandidateConstraintPredicate predicate,
        string id,
        string? sourceGroupId = null,
        string targetEntityId = "person-a",
        long availableAtTick = 10,
        PastStateEvidenceUsageRole usageRole = PastStateEvidenceUsageRole.CandidateGeneration,
        string targetStateContextId = TargetStateContextId,
        string candidateDomainId = CandidateDomainId,
        InteractionVisibility visibility = InteractionVisibility.PublicEvidence) =>
        CandidateGenerationConstraintComponent.PublicPredicate(
            ScenarioId,
            seed: 0,
            targetEntityId,
            targetStateContextId,
            candidateDomainId,
            predicate,
            $"assertion-{id}",
            $"observation-{id}",
            sourceGroupId ?? $"source-{id}",
            [sourceGroupId ?? $"source-{id}"],
            $"rule-{id}",
            [$"input-{id}"],
            availableAtTick,
            EvidenceState.Confirmed,
            visibility,
            usageRole,
            [new InteractionProvenance(InteractionProvenanceKind.EvidenceRecord, $"input-{id}")]);

    private static CandidateGenerationConstraintComponent NonRestricting(
        CandidateConstraintInputState state,
        string id,
        long availableAtTick) => CandidateGenerationConstraintComponent.NonRestricting(
            ScenarioId,
            seed: 0,
            "person-a",
            TargetStateContextId,
            CandidateDomainId,
            state,
            $"assertion-{id}",
            $"observation-{id}",
            $"source-{id}",
            [$"source-{id}"],
            $"rule-{id}",
            [$"input-{id}"],
            availableAtTick,
            state == CandidateConstraintInputState.Unknown
                ? EvidenceState.Indeterminate
                : EvidenceState.Confirmed,
            InteractionVisibility.PublicEvidence,
            PastStateEvidenceUsageRole.CandidateGeneration);

    private static PublicCandidateWarpStateDefinition[] CandidateStates() =>
    [
        State(10, "west"),
        State(10, "east"),
        State(10, "north"),
        State(11, "south"),
        State(12, "west"),
        State(12, "east")
    ];

    private static PublicCandidateWarpStateDefinition State(long tick, string regionId) => new()
    {
        TimeWindow = InteractionTimeWindow.At(tick),
        TemporalKind = CandidateWarpTemporalKind.EventPoint,
        RegionIds = [regionId],
        EvidenceBoundaryState = EvidenceState.Confirmed,
        Provenance =
        [
            CandidateWarpProvenance.Create(
                CandidateWarpSourceKind.PublicCausalModel,
                "m1f14-test-domain",
                availableAtTick: 0,
                isPersonEvidence: false,
                isDeviceOnlyEvidence: false,
                EvidenceState.Confirmed,
                [regionId])
        ]
    };

    private static string PublicSignature(ConstraintCompositionFixtureRun run) => string.Join(
        ';',
        run.ProfileId,
        run.CompositionId,
        run.Status,
        string.Join("|", run.CandidateIds),
        run.CandidateGenerationWork,
        JsonDefaults.Serialize(run.CompositionAudit));

    private static ConstraintCompositionFixtureEvaluationResult RunFixture() => RunFixture(Fixture());

    private static ConstraintCompositionFixtureDefinition Fixture() =>
        ConstraintCompositionFixtureLoader.Load(FixturePath);

    private static ConstraintCompositionFixtureEvaluationResult RunFixture(
        ConstraintCompositionFixtureDefinition fixture) =>
        new ConstraintCompositionFixtureEvaluationRunner().Run(
            new ConstraintCompositionFixtureEvaluationRequest
            {
                Fixture = fixture,
                FixtureContentSha256 = "m1f14-fixture-test-hash",
                SourceCommitSha = "m1f14-test-commit",
                DirtyWorkingTree = false
            });

    private static string FixturePath => Path.Combine(
        RepositoryRoot,
        "experiments",
        "m1f14_constraint_composition",
        "fixture.json");

    private const string ScenarioId = "m1f14-composition-test";
    private const string TargetStateContextId = "historical-state-context";
    private const string CandidateDomainId = "time-region-domain";

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
