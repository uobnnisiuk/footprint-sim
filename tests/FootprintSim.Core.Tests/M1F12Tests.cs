using System.Text.Json;
using FootprintSim.Evidence;
using FootprintSim.Evaluation;
using FootprintSim.Inference.Derivation;
using FootprintSim.Inference.PastStates;
using FootprintSim.Interactions;
using FootprintSim.Serialization;
using Xunit;

namespace FootprintSim.Core.Tests;

public sealed class M1F12Tests
{
    private static readonly Lazy<RelationDependentFixtureEvaluationResult> Result =
        new(RunFixture);

    [Fact]
    public void ExactEncounterControlsApplicabilityAndNarrowsCandidates()
    {
        var exact = Run("exact");

        Assert.True(exact.RelationObservationPresent);
        Assert.Equal(RelationKindKnowledge.Exact, exact.RelationKnowledge);
        Assert.Equal(InteractionRelationKind.PersonPersonEncounter, exact.ExactRelationKind);
        Assert.True(exact.DerivationAudit.RelationDispatchAttempted);
        Assert.True(exact.DerivationAudit.EncounterRuleApplied);
        Assert.True(exact.ConstraintGenerated);
        Assert.Equal(["east"], exact.DerivedAllowedRegionIds);
        Assert.Equal(1, exact.CandidateCount);
        Assert.Equal(2, exact.CandidateReductionVsUnknownCount);
        Assert.Equal(2m / 3m, exact.CandidateReductionVsUnknownFraction);
        Assert.Equal(PersonPersonEncounterColocationConstraintDeriver.RuleId, exact.DerivationAudit.RuleId);
        Assert.Equal(
            EncounterParticipantLocationConstraintDeriver.RuleId,
            exact.DerivationAudit.UnderlyingConstraintRuleId);
    }

    [Fact]
    public void UnknownKindKeepsRelationObservationButDoesNotApplyEncounterRule()
    {
        var unknown = Run("unknown");

        Assert.True(unknown.RelationObservationPresent);
        Assert.Equal(RelationKindKnowledge.UnknownKind, unknown.RelationKnowledge);
        Assert.Null(unknown.ExactRelationKind);
        Assert.False(unknown.DerivationAudit.RelationDispatchAttempted);
        Assert.False(unknown.DerivationAudit.EncounterRuleApplied);
        Assert.False(unknown.ConstraintGenerated);
        Assert.Null(unknown.ConstraintId);
        Assert.Empty(unknown.DerivedAllowedRegionIds);
        Assert.Equal(
            RelationConstraintDerivationFailureReason.UnknownRelationKind,
            unknown.DerivationAudit.FailureReason);
        Assert.Equal(unknown.InitialCandidateCount, unknown.CandidateCount);
        Assert.Equal(1, unknown.RelationObservationSourceGroupCount);
        Assert.DoesNotContain(typeof(PublicRelationObservation).GetProperties(), property =>
            property.Name.Contains("Absent", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void UnsupportedFormalValueDoesNotReceiveEncounterSemantics()
    {
        var unsupported = Derive(
            Observation(ObservedRelationInformation.Exact((InteractionRelationKind)999)),
            [Evidence()]);

        Assert.Null(unsupported.Constraint);
        Assert.True(unsupported.Audit.RelationDispatchAttempted);
        Assert.False(unsupported.Audit.EncounterRuleApplied);
        Assert.Equal(
            RelationConstraintDerivationFailureReason.UnsupportedRelationKind,
            unsupported.Audit.FailureReason);
    }

    [Fact]
    public void MissingParticipantEvidenceFailsSafely()
    {
        var derivation = Derive(
            Observation(ObservedRelationInformation.Exact(
                InteractionRelationKind.PersonPersonEncounter)),
            []);

        Assert.Null(derivation.Constraint);
        Assert.False(derivation.Audit.EncounterRuleApplied);
        Assert.Equal(
            RelationConstraintDerivationFailureReason.MissingParticipantEvidence,
            derivation.Audit.FailureReason);
        Assert.Equal(
            ParticipantConstraintDerivationFailureReason.MissingParticipantLocationEvidence,
            derivation.Audit.UnderlyingFailureReason);
        Assert.Empty(derivation.Audit.OutputAllowedRegionIds);
    }

    [Fact]
    public void DeviceEndpointIsNotPromotedToPersonEncounterParticipant()
    {
        var observation = PublicRelationObservation.Create(
            ScenarioId,
            seed: 0,
            "public-relation-assertion",
            "person-a",
            InteractionEntityKind.Person,
            "device-b",
            InteractionEntityKind.Device,
            ObservedRelationInformation.Exact(InteractionRelationKind.PersonPersonEncounter),
            InteractionTimeWindow.At(10),
            availableAtTick: 11,
            EvidenceState.Confirmed,
            InteractionVisibility.PublicEvidence,
            ["public-relation-source-group"]);
        var derivation = Derive(observation, [PublicParticipantLocationEvidence.Create(
            ScenarioId,
            seed: 0,
            "public-location-device-b",
            "device-b",
            InteractionEntityKind.Device,
            InteractionTimeWindow.At(10),
            ["east"],
            availableAtTick: 11,
            EvidenceState.Confirmed,
            PastStateEvidenceUsageRole.CandidateGeneration)]);

        Assert.Null(derivation.Constraint);
        Assert.Equal(
            RelationConstraintDerivationFailureReason.EntityKindMismatch,
            derivation.Audit.FailureReason);
    }

    [Fact]
    public void UsageBoundaryIsCandidateGenerationOnly()
    {
        var derivation = new PersonPersonEncounterColocationConstraintDeriver().Derive(
            Observation(ObservedRelationInformation.Exact(
                InteractionRelationKind.PersonPersonEncounter)),
            [Evidence()],
            evidenceAvailabilityCutoff: 11,
            PastStateEvidenceUsageRole.CandidateVerification);

        Assert.Null(derivation.Constraint);
        Assert.Equal(
            RelationConstraintDerivationFailureReason.WrongUsageRole,
            derivation.Audit.FailureReason);
        Assert.All(Result.Value.Runs, run => Assert.Equal(
            PastStateEvidenceUsageRole.CandidateGeneration,
            run.DerivationAudit.UsageRole));
    }

    [Fact]
    public void SuccessfulConstraintHasDeterministicIdAndCompleteRelationProvenance()
    {
        var observation = Observation(ObservedRelationInformation.Exact(
            InteractionRelationKind.PersonPersonEncounter));
        var evidence = Evidence();
        var first = Derive(observation, [evidence]);
        var second = Derive(observation, [evidence]);
        var constraint = Assert.IsType<ObservationDerivedConstraint>(first.Constraint);

        Assert.Equal(constraint.ConstraintId, second.Constraint!.ConstraintId);
        Assert.Equal(EncounterParticipantLocationConstraintDeriver.RuleId, constraint.DerivationRuleId);
        Assert.Contains(observation.ObservationId, first.Audit.PublicInputIdsUsed);
        Assert.Contains(evidence.PublicInputId, first.Audit.PublicInputIdsUsed);
        Assert.Contains(constraint.Provenance, item =>
            item.Kind == InteractionProvenanceKind.InteractionObservation &&
            item.SourceIdentifier == observation.ObservationId);
        Assert.Contains(constraint.Provenance, item =>
            item.Kind == InteractionProvenanceKind.DerivationRule &&
            item.SourceIdentifier == PersonPersonEncounterColocationConstraintDeriver.RuleId);
        Assert.Contains(constraint.Provenance, item =>
            item.Kind == InteractionProvenanceKind.DerivationRule &&
            item.SourceIdentifier == EncounterParticipantLocationConstraintDeriver.RuleId);
        Assert.Contains(constraint.Provenance, item =>
            item.Kind == InteractionProvenanceKind.ExternalSource &&
            item.SourceIdentifier == evidence.PublicInputId);
        Assert.Equal(constraint.ConstraintId, first.Audit.GeneratedConstraintId);
    }

    [Fact]
    public void ExactAndUnknownShareOneRelationSourceGroup()
    {
        var exact = Run("exact");
        var unknown = Run("unknown");

        Assert.Equal(
            exact.DerivationAudit.RelationObservationSourceGroupId,
            unknown.DerivationAudit.RelationObservationSourceGroupId);
        Assert.Equal(1, exact.RelationObservationSourceGroupCount);
        Assert.Equal(1, unknown.RelationObservationSourceGroupCount);
        Assert.Equal(1, exact.ParticipantEvidenceSourceGroupCount);
        Assert.Equal(0, unknown.ParticipantEvidenceSourceGroupCount);
        Assert.Equal(1, exact.GeneratedConstraintSourceGroupCount);
        Assert.Equal(0, unknown.GeneratedConstraintSourceGroupCount);
    }

    [Fact]
    public void HiddenTruthRelationReferenceDoesNotAlterPublicOutput()
    {
        var original = RunFixture();
        var alternate = RunFixture(Fixture() with
        {
            TruthRelationReference = "evaluation-only-alternate-relation-reference"
        });

        Assert.Equal(
            original.Runs.Select(PublicDerivationSignature),
            alternate.Runs.Select(PublicDerivationSignature));
        Assert.False(original.Manifest.TruthUsedForDerivation);
        Assert.False(alternate.Manifest.TruthUsedForDerivation);
    }

    [Fact]
    public void DeriverExposesNoTruthOrFreeTextRelationDispatchInput()
    {
        var derive = typeof(PersonPersonEncounterColocationConstraintDeriver)
            .GetMethod(nameof(PersonPersonEncounterColocationConstraintDeriver.Derive));

        Assert.NotNull(derive);
        Assert.DoesNotContain(derive!.GetParameters(), parameter =>
            parameter.ParameterType.Name.Contains("Truth", StringComparison.OrdinalIgnoreCase) ||
            parameter.ParameterType.Name.Contains("Realized", StringComparison.OrdinalIgnoreCase) ||
            parameter.Name!.Contains("truth", StringComparison.OrdinalIgnoreCase) ||
            parameter.Name.Contains("hidden", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(typeof(PublicRelationObservation).GetProperties(), property =>
            property.Name.Contains("Label", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(
            [InteractionRelationKind.PersonPersonEncounter],
            Enum.GetValues<InteractionRelationKind>());
    }

    [Fact]
    public void ProfilesChangeOnlyRelationInformation()
    {
        Assert.Equal(["exact", "unknown"], Result.Value.Runs.Select(item => item.ProfileId));
        Assert.All(Result.Value.Runs, run =>
        {
            Assert.Equal("person-a", run.DerivationAudit.TargetEntityId);
            Assert.Equal("person-b", run.DerivationAudit.OtherParticipantId);
            Assert.Equal(InteractionEntityKind.Person, run.DerivationAudit.TargetEntityKind);
            Assert.Equal(InteractionEntityKind.Person, run.DerivationAudit.OtherParticipantEntityKind);
            Assert.Equal(2, run.DerivationAudit.RelationArity);
            Assert.Equal(new InteractionTimeWindow(10, 10), run.DerivationAudit.OccurrenceTimeWindow);
        });
        Assert.Equal(["east"], Result.Value.ParticipantLocationEvidence.AllowedRegionIds);
        Assert.Equal(
            new InteractionTimeWindow(10, 10),
            Result.Value.ParticipantLocationEvidence.ApplicableTimeWindow);
    }

    [Fact]
    public void CandidateSafetyAndMonotonicityHold()
    {
        Assert.All(Result.Value.Runs, run =>
        {
            Assert.True(run.ContainsTruth);
            Assert.False(run.FalseExclusion);
        });
        Assert.Equal(0, Result.Value.MonotonicityViolationCount);
        Assert.True(Result.Value.MonotonicityAudit.ExactRelationInformationPrecedesUnknown);
        Assert.True(Result.Value.MonotonicityAudit.CandidateSetIsSubset);
    }

    [Fact]
    public void FixtureCannotDeclareDerivedOutput()
    {
        var source = File.ReadAllText(FixturePath);
        var modified = source.Replace(
            "\"profiles\": [",
            "\"derivedAllowedRegionIds\": [\"east\"],\n  \"profiles\": [",
            StringComparison.Ordinal);
        var temporaryDirectory = Path.Combine(
            Path.GetTempPath(),
            $"footprint-sim-m1f12-{Guid.NewGuid():N}");
        Directory.CreateDirectory(temporaryDirectory);
        var path = Path.Combine(temporaryDirectory, "fixture.json");
        try
        {
            File.WriteAllText(path, modified);
            Assert.Throws<JsonException>(() => RelationDependentFixtureLoader.Load(path));
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

    private static RelationDependentFixtureRun Run(string profileId) =>
        Result.Value.Runs.Single(item => item.ProfileId == profileId);

    private static RelationConstraintDerivationResult Derive(
        PublicRelationObservation observation,
        IEnumerable<PublicParticipantLocationEvidence> evidence) =>
        new PersonPersonEncounterColocationConstraintDeriver().Derive(
            observation,
            evidence,
            evidenceAvailabilityCutoff: 11,
            PastStateEvidenceUsageRole.CandidateGeneration);

    private static PublicRelationObservation Observation(
        ObservedRelationInformation relationInformation) =>
        PublicRelationObservation.Create(
            ScenarioId,
            seed: 0,
            "public-relation-assertion",
            "person-a",
            InteractionEntityKind.Person,
            "person-b",
            InteractionEntityKind.Person,
            relationInformation,
            InteractionTimeWindow.At(10),
            availableAtTick: 11,
            EvidenceState.Confirmed,
            InteractionVisibility.PublicEvidence,
            ["public-relation-source-group"]);

    private static PublicParticipantLocationEvidence Evidence() =>
        PublicParticipantLocationEvidence.Create(
            ScenarioId,
            seed: 0,
            "public-location-person-b",
            "person-b",
            InteractionEntityKind.Person,
            InteractionTimeWindow.At(10),
            ["east"],
            availableAtTick: 11,
            EvidenceState.Confirmed,
            PastStateEvidenceUsageRole.CandidateGeneration,
            InteractionVisibility.PublicEvidence,
            ["participant-b-location-source-group"]);

    private static string PublicDerivationSignature(RelationDependentFixtureRun run) => string.Join(
        ';',
        run.ProfileId,
        run.SourceObservationId,
        run.ConstraintId,
        run.DerivationAudit.FailureReason,
        string.Join("|", run.DerivedAllowedRegionIds),
        run.CandidateCount,
        string.Join("|", run.CandidateIds));

    private static RelationDependentFixtureEvaluationResult RunFixture() => RunFixture(Fixture());

    private static RelationDependentFixtureEvaluationResult RunFixture(
        RelationDependentFixtureDefinition fixture) =>
        new RelationDependentFixtureEvaluationRunner().Run(
            new RelationDependentFixtureEvaluationRequest
            {
                Fixture = fixture,
                FixtureContentSha256 = "m1f12-fixture-test-hash",
                SourceCommitSha = "m1f12-test-commit",
                DirtyWorkingTree = false
            });

    private static RelationDependentFixtureDefinition Fixture() =>
        RelationDependentFixtureLoader.Load(FixturePath);

    private static string FixturePath => Path.Combine(
        RepositoryRoot,
        "experiments",
        "m1f12_relation_dependent_derivation",
        "fixture.json");

    private const string ScenarioId = "m1f12-same-time-relation-fixture";

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
