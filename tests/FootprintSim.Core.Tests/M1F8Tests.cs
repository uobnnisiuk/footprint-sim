using System.Text.Json;
using FootprintSim.Evidence;
using FootprintSim.Evaluation;
using FootprintSim.Inference.Derivation;
using FootprintSim.Inference.PastStates;
using FootprintSim.Interactions;
using FootprintSim.Serialization;
using Xunit;

namespace FootprintSim.Core.Tests;

public sealed class M1F8Tests
{
    private static readonly Lazy<BoundedDurationTimeFixtureEvaluationResult> Result =
        new(RunFixture);

    [Fact]
    public void AcceptanceProfilesDeriveExpectedWindowsAndCandidateCounts()
    {
        var runs = Result.Value.Runs;

        Assert.Equal(["exact", "medium", "broad", "unknown"], runs.Select(item => item.ProfileId));
        Assert.Equal(
            [
                new InteractionTimeWindow(10, 11),
                new InteractionTimeWindow(10, 12),
                new InteractionTimeWindow(10, 13),
                null
            ],
            runs.Select(item => item.DerivedPastWindow));
        Assert.Equal([2, 3, 4, 4], runs.Select(item => item.CandidateCount));
        Assert.Equal([0.5m, 0.25m, 0m, 0m], runs.Select(item => item.Reduction));
        Assert.Equal([5, 5, 5, 4], runs.Select(item => item.CandidateGenerationWork));
        Assert.Equal("[o_min - d_max, o_max - d_min]", Result.Value.IntervalFormula);
    }

    [Fact]
    public void ObservationAndDurationUncertaintyPropagateMonotonically()
    {
        Assert.Equal(0, Result.Value.MonotonicityViolationCount);
        Assert.All(Result.Value.MonotonicityAudits, item =>
        {
            Assert.True(item.DerivedWindowIsSubset);
            Assert.True(item.CandidateSetIsSubset);
        });

        var observation = Observation(ObservedOccurrenceTime.Known(new InteractionTimeWindow(15, 15)));
        var narrow = Derive(observation, Dependency(4, 5));
        var wider = Derive(observation, Dependency(3, 6));

        Assert.Equal(new InteractionTimeWindow(10, 11), narrow.Constraint!.ConstrainedTimeWindow);
        Assert.Equal(new InteractionTimeWindow(9, 12), wider.Constraint!.ConstrainedTimeWindow);
        Assert.True(wider.Constraint.ConstrainedTimeWindow.StartTick <=
            narrow.Constraint.ConstrainedTimeWindow.StartTick);
        Assert.True(narrow.Constraint.ConstrainedTimeWindow.EndTick <=
            wider.Constraint.ConstrainedTimeWindow.EndTick);
    }

    [Fact]
    public void UnknownTimeKeepsTheObservedEventButGeneratesNoConstraint()
    {
        var unknown = Run("unknown");

        Assert.True(unknown.SourceObservationPresent);
        Assert.Equal(OccurrenceTimeKnowledge.Unknown, unknown.OccurrenceTimeKnowledge);
        Assert.Null(unknown.ObservedOccurrenceWindow);
        Assert.False(unknown.ConstraintGenerated);
        Assert.Null(unknown.ConstraintId);
        Assert.Null(unknown.DerivedPastWindow);
        Assert.False(unknown.DerivationAudit.Success);
        Assert.Equal(
            ObservationConstraintDerivationFailureReason.UnknownOccurrenceTime,
            unknown.DerivationAudit.FailureReason);
        Assert.Equal(4, unknown.CandidateCount);
        Assert.Equal(1, unknown.ObservationSourceGroupCount);
        Assert.Equal(0, unknown.GeneratedConstraintSourceGroupCount);
        Assert.DoesNotContain(typeof(BoundedDurationTimeFixtureRun).GetProperties(), property =>
            property.Name.Contains("Absent", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void AvailabilityDoesNotSupplyOrAlterOccurrenceTime()
    {
        var time = ObservedOccurrenceTime.Known(new InteractionTimeWindow(15, 16));
        var at18 = Derive(Observation(time, availableAtTick: 18), Dependency());
        var at20 = Derive(Observation(time, availableAtTick: 20), Dependency());

        Assert.Equal(
            at18.Constraint!.ConstrainedTimeWindow,
            at20.Constraint!.ConstrainedTimeWindow);
        Assert.Equal(new InteractionTimeWindow(10, 12), at18.Constraint.ConstrainedTimeWindow);

        var unknown = Derive(Observation(ObservedOccurrenceTime.Unknown()), Dependency());
        Assert.Null(unknown.Constraint);
        Assert.Null(unknown.Audit.InputTimeWindow);
        Assert.Null(unknown.Audit.OutputTimeWindow);
    }

    [Fact]
    public void TruthChangesOnlyPostReconstructionEvaluation()
    {
        var original = RunFixture();
        var alternate = RunFixture(Fixture() with { TruthRegionId = "predecessor-11" });

        Assert.Equal(
            original.Runs.Select(PublicDerivationSignature),
            alternate.Runs.Select(PublicDerivationSignature));
        Assert.Equal(
            original.Runs.SelectMany(item => item.Candidates).Select(item => item.CandidateId),
            alternate.Runs.SelectMany(item => item.Candidates).Select(item => item.CandidateId));
    }

    [Fact]
    public void ConstraintIdentifiersAndOutputAreDeterministic()
    {
        var first = RunFixture();
        var second = RunFixture();

        Assert.Equal(JsonDefaults.Serialize(first), JsonDefaults.Serialize(second));
        Assert.Equal(
            first.Runs.Select(item => item.ConstraintId),
            second.Runs.Select(item => item.ConstraintId));
        Assert.Equal(
            first.PublicDependency.PublicInputId,
            second.PublicDependency.PublicInputId);
    }

    [Fact]
    public void SuccessfulConstraintHasCompletePublicProvenance()
    {
        var exact = Run("exact");
        var constraint = Assert.IsType<ObservationDerivedConstraint>(
            Derive(
                Observation(ObservedOccurrenceTime.Known(new InteractionTimeWindow(15, 15))),
                Dependency()).Constraint);

        Assert.True(exact.DerivationAudit.Success);
        Assert.Equal(BoundedDurationObservationConstraintDeriver.RuleId, exact.DerivationAudit.RuleId);
        Assert.Equal(PastStateEvidenceUsageRole.CandidateGeneration, exact.DerivationAudit.UsageRole);
        Assert.Equal(exact.ConstraintId, exact.DerivationAudit.GeneratedConstraintId);
        Assert.Contains(exact.SourceObservationId, exact.DerivationAudit.PublicInputIdsUsed);
        Assert.Contains(exact.DerivationAudit.PublicDependencyInputId, exact.DerivationAudit.PublicInputIdsUsed);
        Assert.Contains(exact.DerivationAudit.DurationSourceId, exact.DerivationAudit.PublicInputIdsUsed);
        Assert.Contains(constraint.Provenance, item =>
            item.Kind == InteractionProvenanceKind.EvidenceRecord &&
            item.SourceIdentifier == "later-event-observation");
        Assert.Contains(constraint.Provenance, item =>
            item.Kind == InteractionProvenanceKind.InteractionObservation &&
            item.SourceIdentifier == constraint.SourceObservationId);
        Assert.Contains(constraint.Provenance, item =>
            item.Kind == InteractionProvenanceKind.DerivationRule &&
            item.SourceIdentifier == BoundedDurationObservationConstraintDeriver.RuleId);
        Assert.Contains(constraint.Provenance, item =>
            item.Kind == InteractionProvenanceKind.ExternalSource &&
            item.SourceIdentifier == "public-transition-duration-bound");
    }

    [Fact]
    public void OneObservationRemainsOneSourceGroupAcrossDerivedAlternatives()
    {
        var observation = Observation(
            ObservedOccurrenceTime.Known(new InteractionTimeWindow(15, 15)));
        var first = Derive(observation, Dependency(4, 5)).Constraint!;
        var second = Derive(observation, Dependency(3, 6)).Constraint!;

        Assert.NotEqual(first.ConstraintId, second.ConstraintId);
        Assert.Equal(first.SourceGroupId, second.SourceGroupId);
        Assert.Equal(first.SourceAncestryIds, second.SourceAncestryIds);
        Assert.All(Result.Value.Runs.Where(item => item.ConstraintGenerated), item =>
            Assert.Equal(1, item.GeneratedConstraintSourceGroupCount));
    }

    [Fact]
    public void UsageBoundaryIsCandidateGenerationOnly()
    {
        var observation = Observation(
            ObservedOccurrenceTime.Known(new InteractionTimeWindow(15, 15)));
        var unsupported = new BoundedDurationObservationConstraintDeriver().Derive(
            observation,
            Dependency(),
            evidenceAvailabilityCutoff: 20,
            PastStateEvidenceUsageRole.CandidateVerification);

        Assert.Null(unsupported.Constraint);
        Assert.Equal(
            ObservationConstraintDerivationFailureReason.UnsupportedUsageRole,
            unsupported.Audit.FailureReason);
        Assert.All(Result.Value.Runs.Where(item => item.ConstraintGenerated), item =>
            Assert.Equal(PastStateEvidenceUsageRole.CandidateGeneration, item.DerivationAudit.UsageRole));
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
    public void FixtureCannotDeclareAComputedOutputWindow()
    {
        var source = File.ReadAllText(FixturePath);
        var modified = source.Replace(
            "\"profiles\": [",
            "\"derivedPastWindow\": { \"startTick\": 1, \"endTick\": 2 },\n  \"profiles\": [",
            StringComparison.Ordinal);
        var temporaryDirectory = Path.Combine(
            Path.GetTempPath(),
            $"footprint-sim-m1f8-{Guid.NewGuid():N}");
        Directory.CreateDirectory(temporaryDirectory);
        var path = Path.Combine(temporaryDirectory, "fixture.json");
        try
        {
            File.WriteAllText(path, modified);
            Assert.Throws<JsonException>(() => BoundedDurationTimeFixtureLoader.Load(path));
        }
        finally
        {
            Directory.Delete(temporaryDirectory, recursive: true);
        }
    }

    [Fact]
    public void DerivationAndReconstructionContractsExposeNoTruthInput()
    {
        var derive = typeof(BoundedDurationObservationConstraintDeriver)
            .GetMethod(nameof(BoundedDurationObservationConstraintDeriver.Derive));
        var reconstruct = typeof(ObservationDerivedHistoricalCandidateReconstructor)
            .GetMethod(nameof(ObservationDerivedHistoricalCandidateReconstructor.Reconstruct));

        Assert.NotNull(derive);
        Assert.NotNull(reconstruct);
        Assert.DoesNotContain(derive!.GetParameters(), parameter =>
            parameter.ParameterType.Name.Contains("Truth", StringComparison.OrdinalIgnoreCase) ||
            parameter.ParameterType.Name.Contains("Realized", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(reconstruct!.GetParameters(), parameter =>
            parameter.ParameterType.Name.Contains("Truth", StringComparison.OrdinalIgnoreCase) ||
            parameter.ParameterType.Name.Contains("Realized", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(typeof(ObservationDerivedConstraint).GetProperties(), property =>
            property.Name.Contains("Truth", StringComparison.OrdinalIgnoreCase) ||
            property.Name.Contains("Realized", StringComparison.OrdinalIgnoreCase));
    }

    private static BoundedDurationTimeFixtureRun Run(string profileId) =>
        Result.Value.Runs.Single(item => item.ProfileId == profileId);

    private static ObservationConstraintDerivationResult Derive(
        PublicEventObservation observation,
        PublicBoundedDurationDependency dependency) =>
        new BoundedDurationObservationConstraintDeriver().Derive(
            observation,
            dependency,
            evidenceAvailabilityCutoff: 20,
            PastStateEvidenceUsageRole.CandidateGeneration);

    private static PublicEventObservation Observation(
        ObservedOccurrenceTime time,
        long availableAtTick = 20) => PublicEventObservation.Create(
            "m1f8-bounded-duration-time-fixture",
            seed: 0,
            "later-event-observation",
            "later-checkpoint-event",
            "person-a",
            time,
            availableAtTick,
            EvidenceState.Confirmed,
            InteractionVisibility.PublicEvidence,
            ["later-event-observation-family"]);

    private static PublicBoundedDurationDependency Dependency(
        long minimumDurationTicks = 4,
        long maximumDurationTicks = 5) => PublicBoundedDurationDependency.Create(
            "m1f8-bounded-duration-time-fixture",
            "predecessor-to-later-checkpoint",
            "predecessor-state",
            "later-checkpoint-event",
            "person-a",
            new PublicDurationBound(minimumDurationTicks, maximumDurationTicks),
            "public-transition-duration-bound",
            ["predecessor-10", "predecessor-11", "predecessor-12", "predecessor-13"]);

    private static string PublicDerivationSignature(BoundedDurationTimeFixtureRun run) => string.Join(
        ';',
        run.ProfileId,
        run.SourceObservationId,
        run.ConstraintId,
        run.DerivedPastWindow?.StartTick,
        run.DerivedPastWindow?.EndTick,
        run.CandidateCount,
        string.Join("|", run.Candidates.Select(item => item.CandidateId)));

    private static BoundedDurationTimeFixtureEvaluationResult RunFixture() => RunFixture(Fixture());

    private static BoundedDurationTimeFixtureEvaluationResult RunFixture(
        BoundedDurationTimeFixtureDefinition fixture) =>
        new BoundedDurationTimeFixtureEvaluationRunner().Run(
            new BoundedDurationTimeFixtureEvaluationRequest
            {
                Fixture = fixture,
                FixtureContentSha256 = "m1f8-fixture-test-hash",
                SourceCommitSha = "m1f8-test-commit",
                DirtyWorkingTree = false
            });

    private static BoundedDurationTimeFixtureDefinition Fixture() =>
        BoundedDurationTimeFixtureLoader.Load(FixturePath);

    private static string FixturePath => Path.Combine(
        RepositoryRoot,
        "experiments",
        "m1f8_observation_constraint_derivation",
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
