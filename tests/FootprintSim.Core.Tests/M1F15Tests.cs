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

public sealed class M1F15Tests
{
    private static readonly Lazy<CommonWorldObservationFixtureEvaluationResult> Result =
        new(RunFixture);

    [Fact]
    public void RawPublicObservationsDriveAllThreeExistingSemanticDerivations()
    {
        var result = Result.Value;

        Assert.Equal(
            ["east", "west"],
            result.LocationObservation.ObservedRegionIds);
        Assert.Equal(
            result.LocationObservation.ObservedRegionIds,
            result.LocationConstraint.AllowedRegionIds);
        Assert.Equal(
            ObservedRegionLocationConstraintDeriver.RuleId,
            result.LocationConstraint.DerivationRuleId);

        Assert.Equal(new InteractionTimeWindow(15, 15), result.TimeObservation.OccurrenceTime.Window);
        Assert.Equal(new PublicDurationBound(4, 5), result.PublicDependency.Duration);
        Assert.Equal(new InteractionTimeWindow(10, 11), result.TimeConstraint.ConstrainedTimeWindow);
        Assert.Equal(
            BoundedDurationObservationConstraintDeriver.RuleId,
            result.TimeConstraint.DerivationRuleId);

        Assert.Equal(
            ["person-b", "person-c"],
            result.ParticipantObservation.OtherParticipant.PossibleParticipantIds);
        Assert.Equal(
            ["east", "north", "west"],
            result.ParticipantConstraint.AllowedRegionIds);
        Assert.Equal(
            EncounterParticipantLocationConstraintDeriver.RuleId,
            result.ParticipantConstraint.DerivationRuleId);
        Assert.Equal(
            InteractionRelationKind.PersonPersonEncounter,
            result.ParticipantObservation.RelationKind);
    }

    [Fact]
    public void EightProfilesSelectConstraintsWithoutChangingWorldObservations()
    {
        Assert.Equal(
            [
                ("none", 6),
                ("location", 4),
                ("time", 4),
                ("participant", 5),
                ("location-time", 2),
                ("location-participant", 4),
                ("time-participant", 3),
                ("location-time-participant", 2)
            ],
            Result.Value.Runs.Select(item => (item.ProfileId, item.CandidateCount)));
        Assert.Equal(
            CandidateConstraintCompositionStatus.NonRestricting,
            Run("none").Status);
        Assert.All(Result.Value.Runs, run =>
        {
            Assert.True(run.OmittedAxesMeanNotSelected);
            Assert.Equal(3, run.ObservationIdsPresent.Count);
            Assert.Equal(Result.Value.CandidateUniverseId, run.CandidateUniverseId);
            Assert.Equal(
                Result.Value.CandidateUniverse.Select(item => item.CandidateId)
                    .OrderBy(item => item, StringComparer.Ordinal),
                run.CandidateUniverseIds);
        });
        Assert.Empty(Run("none").ActiveConstraintIds);
        Assert.Equal(3, Run("location-time-participant").ActiveConstraintIds.Count);
        Assert.True(Result.Value.DerivationAudit.AllObservationsExistInEveryProfile);
    }

    [Fact]
    public void JointProfilesEqualIntersectionsOnTheCommonCandidateUniverse()
    {
        Assert.Equal(0, Result.Value.IntersectionViolationCount);
        Assert.All(Result.Value.IntersectionAudits, audit =>
        {
            Assert.True(audit.MatchesIntersection);
            Assert.Equal(audit.ExpectedCandidateIds, audit.ActualCandidateIds);
        });
        Assert.True(Result.Value.Manifest.CandidateUniverseInvariant);
        Assert.Equal(6, Result.Value.CandidateUniverse.Count);
    }

    [Fact]
    public void AxisInputsAreOrthogonalAndParticipantAlternativesArePreUnioned()
    {
        var audit = Result.Value.FunctionalIndependence;

        Assert.True(audit.LocationReadsOnlyLocationInputs);
        Assert.True(audit.TimeReadsOnlyTimeInputs);
        Assert.True(audit.ParticipantReadsOnlyParticipantInputs);
        Assert.Single(audit.LocationPublicInputIds);
        Assert.Equal(3, audit.TimePublicInputIds.Count);
        Assert.Equal(3, audit.ParticipantPublicInputIds.Count);
        Assert.Equal(
            ParticipantRegionUncertaintyPropagation.ConservativeUnionAcrossPossibleParticipants,
            Result.Value.DerivationAudit.Participant.UncertaintyPropagation);
        Assert.Single(Run("participant").CompositionAudit.Groups);
        Assert.Single(Run("participant").CompositionAudit.Groups[0].Components);
        Assert.Equal(0, Run("participant").CompositionAudit.AlternativeBranchesEvaluated);
    }

    [Fact]
    public void SourceGraphSeparatesFieldsSourcesAndCompositionComponents()
    {
        Assert.Equal(9, Result.Value.WorldFieldCount);
        Assert.Equal(6, Result.Value.WorldIndependentSourceGroupCount);
        Assert.Equal(
            [0, 1, 2, 3, 3, 4, 5, 6],
            Result.Value.Runs.Select(item => item.SelectedSupportingSourceGroupCount));
        Assert.Equal(
            [0, 1, 3, 5, 4, 6, 8, 9],
            Result.Value.Runs.Select(item => item.SelectedFieldCount));

        var encounter = Result.Value.SourceGraph.Single(item =>
            item.SourceKind == "encounter-observation");
        Assert.Equal(
            ["encounter.time", "participant.identities", "relation.kind"],
            encounter.FieldIds);
        Assert.Single(encounter.PublicInputIds);
        Assert.Equal(1, Run("participant").CompositionAudit.DistinctSourceGroupCount);
        Assert.Equal(3, Run("participant").SelectedSupportingSourceGroupCount);
    }

    [Fact]
    public void PurifiedTruthIsContainedAndDoesNotEnterDerivationOrComposition()
    {
        Assert.Equal(8, Result.Value.ContainmentCount);
        Assert.Equal(0, Result.Value.FalseExclusionCount);
        Assert.All(Result.Value.Runs, run =>
        {
            Assert.True(run.ContainsTruth);
            Assert.False(run.FalseExclusion);
        });
        Assert.False(Result.Value.DerivationAudit.TruthUsedForDerivation);
        Assert.False(Result.Value.Manifest.TruthUsedForDerivationOrComposition);
        Assert.DoesNotContain(
            typeof(CandidateGenerationConstraintCompositionRequest).GetProperties(),
            property => property.Name.Contains("Truth", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(
            typeof(PublicObservedRegionObservation).GetProperties(),
            property => property.Name.Contains("Truth", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void CompatibleTruthCandidateAndParticipantChangesLeavePublicOutputsUnchanged()
    {
        var original = RunFixture();
        var alternate = RunFixture(Fixture() with
        {
            TruthOtherParticipantId = "person-c",
            TruthCandidateStartTick = 10,
            TruthCandidateEndTick = 10,
            TruthCandidateRegionId = "east"
        });

        Assert.Equal(original.LocationConstraint.ConstraintId, alternate.LocationConstraint.ConstraintId);
        Assert.Equal(original.TimeConstraint.ConstraintId, alternate.TimeConstraint.ConstraintId);
        Assert.Equal(original.ParticipantConstraint.ConstraintId, alternate.ParticipantConstraint.ConstraintId);
        Assert.Equal(
            original.Runs.Select(PublicSignature),
            alternate.Runs.Select(PublicSignature));
        Assert.NotEqual(original.TruthCandidateId, alternate.TruthCandidateId);
        Assert.NotEqual(original.TruthOtherParticipantId, alternate.TruthOtherParticipantId);
    }

    [Fact]
    public void RelationAndCandidateUniverseAreFixedAcrossProfiles()
    {
        Assert.Equal(RelationKindKnowledge.Exact, Result.Value.DerivationAudit.RelationKnowledge);
        Assert.Equal(
            InteractionRelationKind.PersonPersonEncounter,
            Result.Value.DerivationAudit.ExactRelationKind);
        Assert.True(Result.Value.Manifest.RelationInvariant);
        Assert.DoesNotContain(
            typeof(CommonWorldProfileDefinition).GetProperties(),
            property => property.Name.Contains("Relation", StringComparison.OrdinalIgnoreCase) ||
                property.Name.Contains("CandidateState", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void LocationDeriverRejectsWrongUsageAndPublicContractRejectsPrivateTruth()
    {
        var observation = PublicObservedRegionObservation.Create(
            "m1f15-location-test",
            0,
            "location-source",
            "person-a",
            ["west"],
            10,
            EvidenceState.Confirmed);
        var wrongUsage = new ObservedRegionLocationConstraintDeriver().Derive(
            observation,
            10,
            PastStateEvidenceUsageRole.CandidateVerification);

        Assert.Null(wrongUsage.Constraint);
        Assert.Equal(
            LocationConstraintDerivationFailureReason.UnsupportedUsageRole,
            wrongUsage.Audit.FailureReason);
        Assert.Throws<ArgumentException>(() => PublicObservedRegionObservation.Create(
            "m1f15-location-test",
            0,
            "private-location-source",
            "person-a",
            ["west"],
            10,
            EvidenceState.Confirmed,
            InteractionVisibility.PrivateTruth));
    }

    [Fact]
    public void CommonTargetAndDomainRemainEnforcedByTheF14Composer()
    {
        var fixture = Fixture();
        var observation = PublicObservedRegionObservation.Create(
            fixture.ScenarioId,
            fixture.Seed,
            "other-target-source",
            "person-z",
            ["west"],
            10,
            EvidenceState.Confirmed);
        var constraint = new ObservedRegionLocationConstraintDeriver().Derive(
            observation,
            fixture.EvidenceAvailabilityCutoff,
            PastStateEvidenceUsageRole.CandidateGeneration).Constraint!;
        var wrongTarget = CandidateGenerationConstraintComponent.FromLocationDerivedConstraint(
            constraint,
            fixture.TargetStateContextId,
            fixture.CandidateDomainId);
        var wrongDomain = CandidateGenerationConstraintComponent.FromLocationDerivedConstraint(
            Result.Value.LocationConstraint,
            fixture.TargetStateContextId,
            "different-domain");

        Assert.Equal(
            CandidateConstraintCompositionFailureReason.TargetMismatch,
            Compose(fixture, wrongTarget).FailureReason);
        Assert.Equal(
            CandidateConstraintCompositionFailureReason.CandidateDomainMismatch,
            Compose(fixture, wrongDomain).FailureReason);
    }

    [Fact]
    public void FixtureCannotDeclareDerivedOutputsOrPerProfileWorlds()
    {
        var source = File.ReadAllText(FixturePath);
        var modified = source.Replace(
            "\"profiles\": [",
            "\"derivedAllowedRegionIds\": [\"west\"],\n  \"profiles\": [",
            StringComparison.Ordinal);
        var temporaryDirectory = Path.Combine(
            Path.GetTempPath(),
            $"footprint-sim-m1f15-loader-{Guid.NewGuid():N}");
        Directory.CreateDirectory(temporaryDirectory);
        try
        {
            var path = Path.Combine(temporaryDirectory, "fixture.json");
            File.WriteAllText(path, modified);
            Assert.Throws<JsonException>(() => CommonWorldObservationFixtureLoader.Load(path));
        }
        finally
        {
            Directory.Delete(temporaryDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task EvaluationAndAllFiveArtifactsAreDeterministic()
    {
        var first = RunFixture();
        var second = RunFixture();
        Assert.Equal(JsonDefaults.Serialize(first), JsonDefaults.Serialize(second));
        Assert.True(first.Manifest.InputOrderIndependent);

        var firstDirectory = Path.Combine(
            Path.GetTempPath(),
            $"footprint-sim-m1f15-first-{Guid.NewGuid():N}");
        var secondDirectory = Path.Combine(
            Path.GetTempPath(),
            $"footprint-sim-m1f15-second-{Guid.NewGuid():N}");
        try
        {
            var writer = new CommonWorldObservationFixtureOutputWriter();
            await writer.WriteAsync(firstDirectory, first);
            await writer.WriteAsync(secondDirectory, second);
            foreach (var name in CommonWorldObservationFixtureOutputNames.All)
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

    private static CommonWorldProfileRun Run(string id) =>
        Result.Value.Runs.Single(item => item.ProfileId == id);

    private static CandidateGenerationConstraintCompositionResult Compose(
        CommonWorldObservationFixtureDefinition fixture,
        CandidateGenerationConstraintComponent component) =>
        new CandidateGenerationConstraintComposer().Compose(
            new CandidateGenerationConstraintCompositionRequest
            {
                ScenarioId = fixture.ScenarioId,
                Seed = fixture.Seed,
                TargetEntityId = fixture.TargetEntityId,
                TargetStateContextId = fixture.TargetStateContextId,
                CandidateDomainId = fixture.CandidateDomainId,
                EvidenceAvailabilityCutoff = fixture.EvidenceAvailabilityCutoff,
                UsageRole = PastStateEvidenceUsageRole.CandidateGeneration,
                PublicCandidateStates = fixture.CandidateStates.Select(item =>
                    new PublicCandidateWarpStateDefinition
                    {
                        TimeWindow = new InteractionTimeWindow(item.StartTick, item.EndTick),
                        TemporalKind = item.TemporalKind,
                        RegionIds = item.RegionIds,
                        EvidenceBoundaryState = EvidenceState.Confirmed,
                        Provenance =
                        [
                            CandidateWarpProvenance.Create(
                                CandidateWarpSourceKind.PublicCausalModel,
                                "m1f15-test-domain",
                                0,
                                false,
                                false,
                                EvidenceState.Confirmed,
                                item.RegionIds)
                        ]
                    }).ToArray(),
                ConstraintGroups =
                [
                    CandidateGenerationConstraintGroup.Create(
                        CandidateConstraintLogicalRole.JointRequirement,
                        [component])
                ]
            });

    private static string PublicSignature(CommonWorldProfileRun run) => string.Join(
        ';',
        run.ProfileId,
        run.CompositionId,
        run.Status,
        string.Join("|", run.CandidateIds),
        run.CandidateGenerationWork,
        JsonDefaults.Serialize(run.CompositionAudit));

    private static CommonWorldObservationFixtureEvaluationResult RunFixture() =>
        RunFixture(Fixture());

    private static CommonWorldObservationFixtureDefinition Fixture() =>
        CommonWorldObservationFixtureLoader.Load(FixturePath);

    private static CommonWorldObservationFixtureEvaluationResult RunFixture(
        CommonWorldObservationFixtureDefinition fixture) =>
        new CommonWorldObservationFixtureEvaluationRunner().Run(
            new CommonWorldObservationFixtureEvaluationRequest
            {
                Fixture = fixture,
                FixtureContentSha256 = "m1f15-fixture-test-hash",
                SourceCommitSha = "m1f15-test-commit",
                DirtyWorkingTree = false
            });

    private static string FixturePath => Path.Combine(
        RepositoryRoot,
        "experiments",
        "m1f15_common_world_observation",
        "fixture.json");

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
