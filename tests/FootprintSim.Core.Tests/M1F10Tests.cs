using System.Text.Json;
using FootprintSim.Evidence;
using FootprintSim.Evaluation;
using FootprintSim.Inference.Derivation;
using FootprintSim.Inference.PastStates;
using FootprintSim.Interactions;
using FootprintSim.Serialization;
using Xunit;

namespace FootprintSim.Core.Tests;

public sealed class M1F10Tests
{
    private static readonly Lazy<ParticipantAmbiguityFixtureEvaluationResult> Result =
        new(RunFixture);

    [Fact]
    public void AcceptanceProfilesPropagateParticipantPrecisionToRegionsAndCandidates()
    {
        var runs = Result.Value.Runs;

        Assert.Equal(["exact", "narrow", "broad", "unknown"], runs.Select(item => item.ProfileId));
        Assert.Equal(
            [
                ParticipantIdentityKnowledge.Exact,
                ParticipantIdentityKnowledge.FiniteAlternatives,
                ParticipantIdentityKnowledge.FiniteAlternatives,
                ParticipantIdentityKnowledge.UnknownIdentity
            ],
            runs.Select(item => item.IdentityKnowledge));
        Assert.Equal(
            [
                new[] { "person-b" },
                new[] { "person-b", "person-c" },
                new[] { "person-b", "person-c", "person-d" },
                Array.Empty<string>()
            ],
            runs.Select(item => item.PossibleParticipantIds));
        Assert.Equal(
            [
                new[] { "east" },
                new[] { "east", "west" },
                new[] { "east", "north", "west" },
                Array.Empty<string>()
            ],
            runs.Select(item => item.DerivedAllowedRegionIds));
        Assert.Equal([1, 2, 3, 4], runs.Select(item => item.CandidateCount));
        Assert.Equal([3, 2, 1, 0], runs.Select(item => item.CandidateReductionVsUnknownCount));
        Assert.Equal([0.75m, 0.5m, 0.25m, 0m], runs.Select(item =>
            item.CandidateReductionVsUnknownFraction));
        Assert.Equal([1, 2, 3, 0], runs.Select(item =>
            item.ParticipantAlternativesEvaluated));
    }

    [Fact]
    public void ParticipantAndCandidateSetsAreMonotone()
    {
        Assert.Equal(0, Result.Value.MonotonicityViolationCount);
        Assert.All(Result.Value.MonotonicityAudits, item =>
        {
            Assert.True(item.ParticipantInformationIsSubset);
            Assert.True(item.DerivedRegionSetIsSubset);
            Assert.True(item.CandidateSetIsSubset);
        });
    }

    [Fact]
    public void UnknownIdentityKeepsEncounterButPerformsNoUnsafeNarrowing()
    {
        var unknown = Run("unknown");

        Assert.True(unknown.EncounterObservationPresent);
        Assert.Equal(InteractionEntityKind.Person, unknown.ParticipantEntityKind);
        Assert.Empty(unknown.PossibleParticipantIds);
        Assert.False(unknown.ConstraintGenerated);
        Assert.Null(unknown.ConstraintId);
        Assert.Empty(unknown.DerivedAllowedRegionIds);
        Assert.Equal(
            ParticipantConstraintDerivationFailureReason.UnknownParticipantIdentity,
            unknown.DerivationAudit.FailureReason);
        Assert.Equal(unknown.InitialCandidateCount, unknown.CandidateCount);
        Assert.Equal(1, unknown.EncounterSourceGroupCount);
        Assert.Equal(0, unknown.GeneratedConstraintSourceGroupCount);
        Assert.DoesNotContain(typeof(PublicEncounterParticipantObservation).GetProperties(), property =>
            property.Name.Contains("Absent", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void MissingAlternativeEvidenceFailsInsteadOfUsingAPartialUnion()
    {
        var observation = Observation(OtherParticipantInformation.FiniteAlternatives(
            ["person-b", "person-missing"]));
        var derivation = Derive(observation, [Evidence("person-b", "east")]);

        Assert.Null(derivation.Constraint);
        Assert.False(derivation.Audit.Success);
        Assert.Equal(
            ParticipantConstraintDerivationFailureReason.MissingParticipantLocationEvidence,
            derivation.Audit.FailureReason);
        Assert.Empty(derivation.Audit.OutputAllowedRegionIds);
        Assert.Contains(derivation.Audit.ParticipantBranches, branch =>
            branch.ParticipantId == "person-missing" &&
            !branch.Derivable &&
            branch.FailureReason ==
                ParticipantConstraintDerivationFailureReason.MissingParticipantLocationEvidence);
    }

    [Fact]
    public void UnavailableParticipantEvidenceFailsWithoutPartialNarrowing()
    {
        var observation = Observation(OtherParticipantInformation.FiniteAlternatives(
            ["person-b", "person-c"]));
        var derivation = Derive(
            observation,
            [Evidence("person-b", "east"), Evidence("person-c", "west", availableAtTick: 12)]);

        Assert.Null(derivation.Constraint);
        Assert.False(derivation.Audit.Success);
        Assert.Equal(
            ParticipantConstraintDerivationFailureReason.ParticipantEvidenceUnavailableAtCutoff,
            derivation.Audit.FailureReason);
        Assert.Empty(derivation.Audit.OutputAllowedRegionIds);
    }

    [Fact]
    public void DeviceEvidenceIsNotPromotedToPersonEvidence()
    {
        var personObservation = Observation(OtherParticipantInformation.Exact("person-b"));
        var deviceEvidence = Evidence(
            "person-b",
            "east",
            InteractionEntityKind.Device);
        var evidenceFailure = Derive(personObservation, [deviceEvidence]);

        Assert.Null(evidenceFailure.Constraint);
        Assert.Equal(
            ParticipantConstraintDerivationFailureReason.ParticipantEvidenceEntityKindMismatch,
            evidenceFailure.Audit.FailureReason);

        var deviceParticipant = Observation(OtherParticipantInformation.Exact(
            "device-b",
            InteractionEntityKind.Device));
        var participantFailure = Derive(deviceParticipant, [Evidence(
            "device-b",
            "east",
            InteractionEntityKind.Device)]);
        Assert.Null(participantFailure.Constraint);
        Assert.Equal(
            ParticipantConstraintDerivationFailureReason.OtherParticipantEntityKindMismatch,
            participantFailure.Audit.FailureReason);
    }

    [Fact]
    public void SuccessfulConstraintHasDeterministicIdAndCompletePublicProvenance()
    {
        var observation = Observation(OtherParticipantInformation.Exact("person-b"));
        var evidence = Evidence("person-b", "east");
        var first = Derive(observation, [evidence]);
        var second = Derive(observation, [evidence]);
        var constraint = Assert.IsType<ObservationDerivedConstraint>(first.Constraint);

        Assert.Equal(constraint.ConstraintId, second.Constraint!.ConstraintId);
        Assert.Equal(
            EncounterParticipantLocationConstraintDeriver.RuleId,
            constraint.DerivationRuleId);
        Assert.Contains(observation.ObservationId, constraint.PublicInputIds);
        Assert.Contains(evidence.PublicInputId, constraint.PublicInputIds);
        Assert.Contains(constraint.Provenance, item =>
            item.Kind == InteractionProvenanceKind.EvidenceRecord &&
            item.SourceIdentifier == "public-encounter-report");
        Assert.Contains(constraint.Provenance, item =>
            item.Kind == InteractionProvenanceKind.EvidenceRecord &&
            item.SourceIdentifier == "public-location-person-b");
        Assert.Contains(constraint.Provenance, item =>
            item.Kind == InteractionProvenanceKind.InteractionObservation &&
            item.SourceIdentifier == observation.ObservationId);
        Assert.Contains(constraint.Provenance, item =>
            item.Kind == InteractionProvenanceKind.DerivationRule &&
            item.SourceIdentifier == EncounterParticipantLocationConstraintDeriver.RuleId);
        Assert.Contains(constraint.Provenance, item =>
            item.Kind == InteractionProvenanceKind.ExternalSource &&
            item.SourceIdentifier == evidence.PublicInputId);
        Assert.Equal(first.Audit.GeneratedConstraintId, constraint.ConstraintId);
        Assert.Equal(first.Audit.PublicInputIdsUsed, constraint.PublicInputIds);
    }

    [Fact]
    public void AlternativesRemainOneEncounterSourceGroupWhilePublicEvidenceSourcesStayDistinct()
    {
        var exact = Run("exact");
        var narrow = Run("narrow");
        var broad = Run("broad");

        Assert.Equal(exact.EncounterSourceGroupId, narrow.EncounterSourceGroupId);
        Assert.Equal(narrow.EncounterSourceGroupId, broad.EncounterSourceGroupId);
        Assert.All([exact, narrow, broad], run =>
        {
            Assert.Equal(1, run.EncounterSourceGroupCount);
            Assert.Equal(1, run.GeneratedConstraintSourceGroupCount);
        });
        Assert.Equal([1, 2, 3], new[] { exact, narrow, broad }.Select(item =>
            item.ParticipantEvidenceSourceGroupCount));
        Assert.Equal([1, 2, 3], new[] { exact, narrow, broad }.Select(item =>
            item.DerivationAudit.ParticipantBranches.Count));
    }

    [Fact]
    public void UsageBoundaryIsCandidateGenerationOnly()
    {
        var observation = Observation(OtherParticipantInformation.Exact("person-b"));
        var unsupported = new EncounterParticipantLocationConstraintDeriver().Derive(
            observation,
            [Evidence("person-b", "east")],
            evidenceAvailabilityCutoff: 11,
            PastStateEvidenceUsageRole.CandidateVerification);

        Assert.Null(unsupported.Constraint);
        Assert.Equal(
            ParticipantConstraintDerivationFailureReason.UnsupportedUsageRole,
            unsupported.Audit.FailureReason);
        Assert.All(Result.Value.Runs.Where(item => item.ConstraintGenerated), item =>
            Assert.Equal(PastStateEvidenceUsageRole.CandidateGeneration, item.DerivationAudit.UsageRole));
    }

    [Fact]
    public void TruthParticipantChangesDoNotAlterPublicDerivationOrCandidates()
    {
        var original = RunFixture();
        var alternate = RunFixture(Fixture() with { TruthOtherParticipantId = "person-c" });

        Assert.Equal(
            original.Runs.Select(PublicDerivationSignature),
            alternate.Runs.Select(PublicDerivationSignature));
        Assert.Equal(
            original.Runs.SelectMany(item => item.CandidateIds),
            alternate.Runs.SelectMany(item => item.CandidateIds));
        Assert.False(original.Manifest.TruthUsedForDerivation);
        Assert.False(alternate.Manifest.TruthUsedForDerivation);
    }

    [Fact]
    public void DerivationContractExposesNoTruthOrHiddenIdentityInput()
    {
        var derive = typeof(EncounterParticipantLocationConstraintDeriver)
            .GetMethod(nameof(EncounterParticipantLocationConstraintDeriver.Derive));

        Assert.NotNull(derive);
        Assert.DoesNotContain(derive!.GetParameters(), parameter =>
            parameter.ParameterType.Name.Contains("Truth", StringComparison.OrdinalIgnoreCase) ||
            parameter.ParameterType.Name.Contains("Realized", StringComparison.OrdinalIgnoreCase) ||
            parameter.Name!.Contains("truth", StringComparison.OrdinalIgnoreCase) ||
            parameter.Name.Contains("hidden", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(typeof(ObservationDerivedConstraint).GetProperties(), property =>
            property.Name.Contains("Truth", StringComparison.OrdinalIgnoreCase) ||
            property.Name.Contains("Realized", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ProfilesChangeOnlyOtherParticipantInformation()
    {
        Assert.All(Result.Value.Runs, run =>
        {
            Assert.Equal(InteractionEntityKind.Person, run.ParticipantEntityKind);
            Assert.Equal(
                InteractionRelationKind.PersonPersonEncounter,
                run.DerivationAudit.RelationKind);
            Assert.Equal(new InteractionTimeWindow(10, 10), run.DerivationAudit.EncounterTimeWindow);
            Assert.Equal("person-a", run.DerivationAudit.TargetEntityId);
        });
        Assert.Equal(3, Result.Value.PublicParticipantLocationEvidence.Count);
        Assert.All(Result.Value.PublicParticipantLocationEvidence, item =>
        {
            Assert.Equal(new InteractionTimeWindow(10, 10), item.ApplicableTimeWindow);
            Assert.Equal(PastStateEvidenceUsageRole.CandidateGeneration, item.UsageRole);
            Assert.Equal(InteractionVisibility.PublicEvidence, item.Visibility);
        });
    }

    [Fact]
    public void FixtureContainsTruthWithoutFalseExclusion()
    {
        Assert.All(Result.Value.Runs, item =>
        {
            Assert.True(item.ContainsTruth);
            Assert.False(item.FalseExclusion);
        });
    }

    [Fact]
    public void FixtureCannotDeclareDerivedRegionsOrCandidateResults()
    {
        var source = File.ReadAllText(FixturePath);
        var modified = source.Replace(
            "\"profiles\": [",
            "\"derivedAllowedRegionIds\": [\"east\"],\n  \"profiles\": [",
            StringComparison.Ordinal);
        var temporaryDirectory = Path.Combine(
            Path.GetTempPath(),
            $"footprint-sim-m1f10-{Guid.NewGuid():N}");
        Directory.CreateDirectory(temporaryDirectory);
        var path = Path.Combine(temporaryDirectory, "fixture.json");
        try
        {
            File.WriteAllText(path, modified);
            Assert.Throws<JsonException>(() => ParticipantAmbiguityFixtureLoader.Load(path));
        }
        finally
        {
            Directory.Delete(temporaryDirectory, recursive: true);
        }
    }

    [Fact]
    public void FixtureOutputIsDeterministic()
    {
        var first = RunFixture();
        var second = RunFixture();

        Assert.Equal(JsonDefaults.Serialize(first), JsonDefaults.Serialize(second));
        Assert.Equal(
            first.Runs.Select(item => item.ConstraintId),
            second.Runs.Select(item => item.ConstraintId));
    }

    private static ParticipantAmbiguityFixtureRun Run(string profileId) =>
        Result.Value.Runs.Single(item => item.ProfileId == profileId);

    private static ParticipantConstraintDerivationResult Derive(
        PublicEncounterParticipantObservation observation,
        IEnumerable<PublicParticipantLocationEvidence> evidence) =>
        new EncounterParticipantLocationConstraintDeriver().Derive(
            observation,
            evidence,
            evidenceAvailabilityCutoff: 11,
            PastStateEvidenceUsageRole.CandidateGeneration);

    private static PublicEncounterParticipantObservation Observation(
        OtherParticipantInformation otherParticipant) =>
        PublicEncounterParticipantObservation.Create(
            "m1f10-same-time-person-encounter-fixture",
            seed: 0,
            "public-encounter-report",
            "person-a",
            InteractionEntityKind.Person,
            otherParticipant,
            InteractionRelationKind.PersonPersonEncounter,
            InteractionTimeWindow.At(10),
            availableAtTick: 11,
            EvidenceState.Confirmed,
            InteractionVisibility.PublicEvidence,
            ["public-encounter-source-group"]);

    private static PublicParticipantLocationEvidence Evidence(
        string participantId,
        string regionId,
        InteractionEntityKind entityKind = InteractionEntityKind.Person,
        long availableAtTick = 11) =>
        PublicParticipantLocationEvidence.Create(
            "m1f10-same-time-person-encounter-fixture",
            seed: 0,
            $"public-location-{participantId}",
            participantId,
            entityKind,
            InteractionTimeWindow.At(10),
            [regionId],
            availableAtTick,
            EvidenceState.Confirmed,
            PastStateEvidenceUsageRole.CandidateGeneration,
            InteractionVisibility.PublicEvidence,
            [$"{participantId}-location-source-group"]);

    private static string PublicDerivationSignature(ParticipantAmbiguityFixtureRun run) => string.Join(
        ';',
        run.ProfileId,
        run.SourceObservationId,
        run.ConstraintId,
        string.Join("|", run.DerivedAllowedRegionIds),
        run.CandidateCount,
        string.Join("|", run.CandidateIds));

    private static ParticipantAmbiguityFixtureEvaluationResult RunFixture() => RunFixture(Fixture());

    private static ParticipantAmbiguityFixtureEvaluationResult RunFixture(
        ParticipantAmbiguityFixtureDefinition fixture) =>
        new ParticipantAmbiguityFixtureEvaluationRunner().Run(
            new ParticipantAmbiguityFixtureEvaluationRequest
            {
                Fixture = fixture,
                FixtureContentSha256 = "m1f10-fixture-test-hash",
                SourceCommitSha = "m1f10-test-commit",
                DirtyWorkingTree = false
            });

    private static ParticipantAmbiguityFixtureDefinition Fixture() =>
        ParticipantAmbiguityFixtureLoader.Load(FixturePath);

    private static string FixturePath => Path.Combine(
        RepositoryRoot,
        "experiments",
        "m1f10_participant_ambiguity_derivation",
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
