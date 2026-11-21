using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using FootprintSim.Evaluation;
using FootprintSim.Evaluation.Truth;
using FootprintSim.Evidence;
using FootprintSim.Inference;
using FootprintSim.Inference.PastStates;
using FootprintSim.Interactions;
using FootprintSim.Interactions.CandidateWarps;
using FootprintSim.Interactions.Tracing;
using FootprintSim.Interactions.Truth;
using FootprintSim.Scenarios;
using FootprintSim.Serialization;
using FootprintSim.Simulation;
using FootprintSim.Telemetry;
using Xunit;

namespace FootprintSim.Core.Tests;

public sealed class M1ETests
{
    private static readonly Lazy<BatchEvaluationResult> SixConditionBatch =
        new(() => RunBatch(Enumerable.Range(0, 6).ToArray(), collectTelemetry: true));

    private static readonly int[] FormerlyFailingSeeds =
        [24, 36, 78, 90, 96, 120, 132, 138, 150, 162, 180];

    private static readonly Lazy<BatchEvaluationResult> MethodCompositionBatch =
        new(() => RunBatch([0, .. FormerlyFailingSeeds], collectTelemetry: true));

    [Fact]
    public void S5ExercisesEveryDeclaredConditionAndKeepsDistributedPublicSources()
    {
        var runs = Enumerable.Range(0, 6)
            .Select(seed => new SimulationEngine().Run(Scenario(), seed))
            .ToArray();

        Assert.Equal(Enum.GetValues<PastStateEvidenceCondition>(), runs
            .Select(item => item.PastStateEvidenceCondition!.Value)
            .OrderBy(item => item));
        Assert.All(runs, item => Assert.Empty(item.GroundTruthInteractionOccurrences));
        Assert.All(runs, item => Assert.NotEmpty(item.CausalTracingConstraints!.Constraints));
        var distributed = Run(PastStateEvidenceCondition.DistributedPublicEvidence);
        Assert.True(distributed.PresentEvidenceSourceGroupCount >= 3);
        Assert.True(distributed.RedundantSameSourceRecordCount >= 1);
        Assert.True(distributed.UncertainEvidenceCount >= 1);
        Assert.True(distributed.UnresolvedCandidateCount >= 1);
    }

    [Fact]
    public void ReconstructionContractCannotReceiveTruthObjectsOrStoreTruthStatus()
    {
        var compare = typeof(PastStateReconstructor).GetMethod(nameof(PastStateReconstructor.Compare));
        Assert.NotNull(compare);
        Assert.DoesNotContain(compare!.GetParameters(), parameter =>
            Contains(parameter.ParameterType, typeof(InteractionOccurrence)) ||
            Contains(parameter.ParameterType, typeof(SimulationRun)) ||
            Contains(parameter.ParameterType, typeof(PastStateTruthEvaluation)));
        Assert.DoesNotContain(typeof(PastStateCandidate).GetProperties(), property =>
            property.Name.Contains("Truth", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(typeof(PastStateComparisonResult).GetProperties(), property =>
            Contains(property.PropertyType, typeof(PastStateTruthEvaluation)));
        var verify = typeof(PastStateReconstructor).GetMethod(nameof(PastStateReconstructor.VerifyCandidates));
        Assert.NotNull(verify);
        Assert.DoesNotContain(verify!.GetParameters(), parameter =>
            Contains(parameter.ParameterType, typeof(PastStateTruthEvaluation)) ||
            Contains(parameter.ParameterType, typeof(InteractionOccurrence)));
    }

    [Fact]
    public void PublicEvidenceSetEnforcesAvailabilityCutoffAndVisibility()
    {
        var delayed = Run(PastStateEvidenceCondition.DelayedVerification);
        var unavailable = Run(PastStateEvidenceCondition.UnavailableVerification);

        Assert.True(delayed.ExcludedFutureEvidenceCount >= 1);
        Assert.DoesNotContain(
            delayed.PublicComparison.Reconstruction.EvidenceSet.Constraints,
            item => item.AvailableAtTick > delayed.EvidenceAvailabilityCutoff);
        Assert.DoesNotContain(
            unavailable.PublicComparison.Reconstruction.EvidenceSet.Constraints,
            item => item.Visibility != InteractionVisibility.PublicEvidence);
        Assert.True(delayed.UnavailableVerificationCandidateCount > 0);
        Assert.True(unavailable.UnavailableVerificationCandidateCount > 0);
    }

    [Fact]
    public void SameSourceDerivationsAreOneGroupAndSharedAncestryIsNotIndependentVerification()
    {
        var distributed = Run(PastStateEvidenceCondition.DistributedPublicEvidence);
        var generationGroups = distributed.PublicComparison.Reconstruction.Candidates
            .SelectMany(item => item.GenerationSourceGroupIds)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        Assert.True(distributed.RedundantSameSourceRecordCount >= 1);
        Assert.True(generationGroups.Length < distributed.PublicComparison.Reconstruction.Candidates[0]
            .GenerationEvidenceIds.Count + 1);

        var shared = Run(PastStateEvidenceCondition.SharedAncestryVerification);
        Assert.True(shared.SharedAncestryOnlyCandidateCount > 0);
        Assert.True(shared.GenerationVerificationAncestryOverlapCount > 0);
        Assert.All(shared.PublicComparison.Reconstruction.Verification, verification =>
        {
            Assert.Empty(verification.VerificationSourceGroupIds);
            Assert.NotEmpty(verification.SharedAncestrySourceGroupIds);
            Assert.Empty(verification.SupportingConstraintIds);
        });
    }

    [Fact]
    public void IndependentVerificationCanSupportAndRefuteDifferentCandidates()
    {
        var run = Run(PastStateEvidenceCondition.DistributedPublicEvidence);
        var reconstruction = run.PublicComparison.Reconstruction;
        var byRegion = reconstruction.Candidates.ToDictionary(item => item.RegionId, StringComparer.Ordinal);
        var verification = reconstruction.Verification.ToDictionary(item => item.CandidateId, StringComparer.Ordinal);

        Assert.Equal(
            CandidateVerificationDisposition.IndependentlySupported,
            verification[byRegion["fork-west"].CandidateId].Disposition);
        Assert.Equal(
            CandidateVerificationDisposition.IndependentlyRefuted,
            verification[byRegion["fork-east"].CandidateId].Disposition);
        Assert.DoesNotContain(
            verification.SelectMany(item => item.Value.VerificationEvidenceIds),
            reconstruction.Candidates.SelectMany(item => item.GenerationEvidenceIds).Contains);
    }

    [Fact]
    public void ConflictingEvidenceIsPreservedWithoutBooleanCollapse()
    {
        var run = Run(PastStateEvidenceCondition.ConflictingVerification);

        Assert.True(run.ConflictingEvidenceCount > 0);
        Assert.True(run.ConflictedCandidateCount > 0);
        Assert.True(run.ConflictingVerificationCandidateCount > 0);
        Assert.Contains(run.PublicComparison.Reconstruction.Candidates, item =>
            item.Disposition == PastStateCandidateDisposition.Conflicting &&
            item.ConflictingConstraintIds.Count > 0);
    }

    [Fact]
    public void DeviceEvidenceDoesNotBecomePersonEvidence()
    {
        var run = Run(PastStateEvidenceCondition.DeviceOnlyEvidence);
        var constraints = run.PublicComparison.Reconstruction.EvidenceSet.Constraints;
        Assert.Contains(constraints, item => item.IsDeviceOnlyEvidence && !item.IsPersonEvidence);
        Assert.All(run.PublicComparison.Reconstruction.Verification, verification =>
        {
            Assert.Empty(verification.SupportingConstraintIds);
            Assert.Empty(verification.RefutingConstraintIds);
        });
        Assert.True(run.UnresolvedVerificationCandidateCount > 0);
    }

    [Fact]
    public void MissingObservationDoesNotMeanAbsentAndAllEvidenceStatesRemainRepresentable()
    {
        Assert.DoesNotContain(typeof(PublicEvidenceSet).GetProperties(), property =>
            property.Name.Contains("Absent", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(typeof(PastStateCandidate).GetProperties(), property =>
            property.PropertyType == typeof(bool));
        Assert.Equal(
            [
                EvidenceState.Confirmed,
                EvidenceState.SupportedButUncertain,
                EvidenceState.Conflicting,
                EvidenceState.Indeterminate,
                EvidenceState.UnsupportedForQuery
            ],
            Enum.GetValues<EvidenceState>());
        var unavailable = Run(PastStateEvidenceCondition.UnavailableVerification);
        Assert.NotEmpty(unavailable.PublicComparison.Reconstruction.Candidates);
        Assert.All(unavailable.PublicComparison.Reconstruction.Verification, item =>
            Assert.Equal(CandidateVerificationDisposition.Unavailable, item.Disposition));
    }

    [Theory]
    [InlineData(EvidenceState.Confirmed, PastStateCandidateDisposition.Supported, 1)]
    [InlineData(EvidenceState.SupportedButUncertain, PastStateCandidateDisposition.Uncertain, 2)]
    [InlineData(EvidenceState.Conflicting, PastStateCandidateDisposition.Conflicting, 2)]
    [InlineData(EvidenceState.Indeterminate, PastStateCandidateDisposition.Unsupported, 2)]
    [InlineData(EvidenceState.UnsupportedForQuery, PastStateCandidateDisposition.Unsupported, 2)]
    public void ReconstructionPreservesEvidenceStateBoundaries(
        EvidenceState evidenceState,
        PastStateCandidateDisposition expectedForRegionA,
        int expectedCandidateCount)
    {
        var reconstruction = ReconstructFixture(evidenceState);
        Assert.Equal(expectedCandidateCount, reconstruction.Candidates.Count);
        Assert.Equal(
            expectedForRegionA,
            reconstruction.Candidates.Single(item => item.RegionId == "region-a").Disposition);
    }

    [Fact]
    public void NoAvailableGenerationEvidenceProducesUnavailableCandidatesNotAbsence()
    {
        var reconstruction = ReconstructFixture(evidenceState: null);
        Assert.Equal(2, reconstruction.Candidates.Count);
        Assert.All(reconstruction.Candidates, item =>
            Assert.Equal(PastStateCandidateDisposition.Unavailable, item.Disposition));
    }

    [Fact]
    public void SharedVerificationDoesNotChangeGenerationGroupsOrCandidateAddresses()
    {
        var withoutVerification = ReconstructFixture(EvidenceState.Confirmed);
        var withVerification = ReconstructFixture(
            EvidenceState.Confirmed,
            includeSharedVerification: true);

        Assert.Equal(
            withoutVerification.Candidates.Select(item => item.CandidateId),
            withVerification.Candidates.Select(item => item.CandidateId));
        Assert.Equal(
            withoutVerification.Candidates.SelectMany(item => item.GenerationSourceGroupIds),
            withVerification.Candidates.SelectMany(item => item.GenerationSourceGroupIds));
        Assert.DoesNotContain(
            withVerification.Candidates.SelectMany(item => item.SourceAncestryIds),
            item => item.Contains("verification", StringComparison.Ordinal));
        Assert.All(withVerification.Verification, item =>
            Assert.Equal(CandidateVerificationDisposition.SharedAncestryOnly, item.Disposition));
    }

    [Fact]
    public void ObservationAndOccurrenceAreExaminedInBothDirections()
    {
        var observedWithoutOccurrence = new SimulationEngine().Run(Scenario(), 0);
        Assert.NotEmpty(observedWithoutOccurrence.InteractionObservations!.Observations);
        Assert.Empty(observedWithoutOccurrence.GroundTruthInteractionOccurrences);

        var occurredWithoutObservation = new SimulationEngine().Run(S4Scenario(), 9);
        Assert.NotEmpty(occurredWithoutObservation.GroundTruthInteractionOccurrences);
        Assert.Empty(occurredWithoutObservation.InteractionObservations!.Observations);
    }

    [Fact]
    public void GenerationVerificationAndTraversalRemainSeparateOperations()
    {
        var run = Run(PastStateEvidenceCondition.DistributedPublicEvidence);
        var comparison = run.PublicComparison;
        Assert.Equal(
            [
                PastStateMethod.BackwardTraversal,
                PastStateMethod.ConstraintBasedReconstruction,
                PastStateMethod.ReconstructionWithLocalTraversal
            ],
            comparison.Methods.Select(item => item.Method).OrderBy(item => item));
        Assert.Null(comparison.Methods.Single(item =>
            item.Method == PastStateMethod.ConstraintBasedReconstruction).TracingSessionId);
        Assert.NotNull(comparison.Methods.Single(item =>
            item.Method == PastStateMethod.BackwardTraversal).TracingSessionId);
        Assert.NotNull(comparison.Methods.Single(item =>
            item.Method == PastStateMethod.ReconstructionWithLocalTraversal).TracingSessionId);
        Assert.All(comparison.Reconstruction.Verification, verification =>
            Assert.DoesNotContain(
                verification.VerificationEvidenceIds,
                comparison.Reconstruction.Candidates.Single(candidate =>
                    candidate.CandidateId == verification.CandidateId).GenerationEvidenceIds.Contains));

        var usage = comparison.EvidenceUsage;
        Assert.Contains(usage, item => item.UsageRoles.SequenceEqual(
            [PastStateEvidenceUsageRole.CandidateGeneration]));
        Assert.Contains(usage, item => item.UsageRoles.SequenceEqual(
            [PastStateEvidenceUsageRole.CandidateVerification]));
        Assert.Contains(usage, item => item.UsageRoles.SequenceEqual(
            [PastStateEvidenceUsageRole.Traversal]));
        Assert.All(
            comparison.Methods.Single(item =>
                item.Method == PastStateMethod.ReconstructionWithLocalTraversal)
                .Diagnostics!.ConstraintUsages,
            item => Assert.Equal([PastStateEvidenceUsageRole.Traversal], item.UsageRoles));
    }

    [Fact]
    public void Seed24RecordsTheLegacyCompositionDefectAndUsesTheCorrectedContract()
    {
        var run = RunBySeed(24);
        var comparison = run.PublicComparison;
        var reconstruction = comparison.Methods.Single(item =>
            item.Method == PastStateMethod.ConstraintBasedReconstruction);
        var local = comparison.Methods.Single(item =>
            item.Method == PastStateMethod.ReconstructionWithLocalTraversal);
        var verificationConstraint = comparison.Reconstruction.EvidenceSet.Constraints.Single(item =>
            string.Equals(item.DirectSourceId, "source-verification-west", StringComparison.Ordinal));

        Assert.Equal("fork-east", run.TruthPersonRegionId);
        Assert.Equal(["fork-east", "fork-west"], reconstruction.CandidateRegionIds);
        Assert.Equal(EvidenceState.Confirmed, verificationConstraint.EvidenceBoundaryState);
        Assert.Equal(["fork-west"], verificationConstraint.AllowedRegionIds);

        // Historical characterization: the former method passed this verification-only
        // constraint to the tracer, so its confirmed intersection produced only west.
        Assert.Equal(
            ["fork-west"],
            reconstruction.CandidateRegionIds
                .Intersect(verificationConstraint.AllowedRegionIds, StringComparer.Ordinal)
                .OrderBy(item => item, StringComparer.Ordinal)
                .ToArray());

        Assert.Equal(["fork-east", "fork-west"], local.CandidateRegionIdsBefore);
        Assert.Equal(["fork-east", "fork-west"], local.CandidateRegionIds);
        Assert.True(local.ForwardTransitionSteps >= 1);
        Assert.True(local.BackwardTransitionSteps >= 1);
        Assert.NotEmpty(local.TransitionIds);
        Assert.DoesNotContain(local.Diagnostics!.ConstraintUsages, item =>
            item.UsageRoles.Contains(PastStateEvidenceUsageRole.CandidateVerification));

        var east = comparison.Reconstruction.Candidates.Single(item => item.RegionId == "fork-east");
        Assert.Equal(
            CandidateVerificationDisposition.IndependentlyRefuted,
            comparison.Reconstruction.Verification.Single(item => item.CandidateId == east.CandidateId).Disposition);
    }

    [Theory]
    [InlineData(24)]
    [InlineData(36)]
    [InlineData(78)]
    [InlineData(90)]
    [InlineData(96)]
    [InlineData(120)]
    [InlineData(132)]
    [InlineData(138)]
    [InlineData(150)]
    [InlineData(162)]
    [InlineData(180)]
    public void FormerFalseExclusionsRemainContainedAfterUsageSeparation(int seed)
    {
        var run = RunBySeed(seed);
        var reconstruction = run.PublicComparison.Methods.Single(item =>
            item.Method == PastStateMethod.ConstraintBasedReconstruction);
        var local = run.PublicComparison.Methods.Single(item =>
            item.Method == PastStateMethod.ReconstructionWithLocalTraversal);

        Assert.Equal(PastStateEvidenceCondition.DistributedPublicEvidence, run.Condition);
        Assert.Equal("fork-east", run.TruthPersonRegionId);
        Assert.Contains(run.TruthPersonRegionId, reconstruction.CandidateRegionIds);
        Assert.Contains(run.TruthPersonRegionId, local.CandidateRegionIds);
        Assert.True(local.ForwardTransitionSteps + local.BackwardTransitionSteps >= 1);
        Assert.DoesNotContain(local.Diagnostics!.ConstraintUsages, item =>
            item.UsageRoles.Contains(PastStateEvidenceUsageRole.CandidateVerification));
    }

    [Fact]
    public void TruthWestUsesTheSamePublicMethodContractAsTruthEast()
    {
        var west = RunBySeed(0);
        var east = RunBySeed(24);
        var westSimulation = new SimulationEngine().Run(Scenario(), 0);
        var eastSimulation = new SimulationEngine().Run(Scenario(), 24);
        var westLocal = west.PublicComparison.Methods.Single(item =>
            item.Method == PastStateMethod.ReconstructionWithLocalTraversal);
        var eastLocal = east.PublicComparison.Methods.Single(item =>
            item.Method == PastStateMethod.ReconstructionWithLocalTraversal);

        Assert.Equal("fork-west", west.TruthPersonRegionId);
        Assert.Equal("fork-east", east.TruthPersonRegionId);
        Assert.Equal(westLocal.CandidateRegionIds, eastLocal.CandidateRegionIds);
        Assert.Equal(westLocal.ForwardTransitionSteps, eastLocal.ForwardTransitionSteps);
        Assert.Equal(westLocal.BackwardTransitionSteps, eastLocal.BackwardTransitionSteps);
        Assert.Equal(
            westLocal.Diagnostics!.ConstraintUsages.Select(item => item.UsageRoles),
            eastLocal.Diagnostics!.ConstraintUsages.Select(item => item.UsageRoles));
        Assert.Equal(
            JsonDefaults.Serialize(westSimulation.ObservationSet.Observations
                .OrderBy(item => item.EvidenceType, StringComparer.Ordinal)
                .ThenBy(item => item.ObservedTick)
                .Select(item => item with { ObservationId = "seed-independent-for-test" })),
            JsonDefaults.Serialize(eastSimulation.ObservationSet.Observations
                .OrderBy(item => item.EvidenceType, StringComparer.Ordinal)
                .ThenBy(item => item.ObservedTick)
                .Select(item => item with { ObservationId = "seed-independent-for-test" })));
    }

    [Fact]
    public void VerificationOnlyReportsDispositionWithoutMutatingCandidates()
    {
        var comparison = VerificationOnlyFixture();
        var reconstruction = comparison.Methods.Single(item =>
            item.Method == PastStateMethod.ConstraintBasedReconstruction);
        var local = comparison.Methods.Single(item =>
            item.Method == PastStateMethod.ReconstructionWithLocalTraversal);

        Assert.Equal(["east", "west"], reconstruction.CandidateRegionIds);
        Assert.Equal(reconstruction.CandidateRegionIds, local.CandidateRegionIds);
        Assert.Equal(0, local.ForwardTransitionSteps);
        Assert.Equal(0, local.BackwardTransitionSteps);
        var candidates = comparison.Reconstruction.Candidates.ToDictionary(
            item => item.RegionId,
            StringComparer.Ordinal);
        Assert.Equal(
            CandidateVerificationDisposition.IndependentlyRefuted,
            comparison.Reconstruction.Verification.Single(item =>
                item.CandidateId == candidates["east"].CandidateId).Disposition);
        Assert.Equal(
            CandidateVerificationDisposition.IndependentlySupported,
            comparison.Reconstruction.Verification.Single(item =>
                item.CandidateId == candidates["west"].CandidateId).Disposition);
        var evaluationOnly = comparison.EvidenceUsage.Single(item =>
            string.Equals(item.DirectSourceId, "unused-source", StringComparison.Ordinal));
        Assert.Equal([PastStateEvidenceUsageRole.EvaluationOnly], evaluationOnly.UsageRoles);
        Assert.Equal(PastStateEvidenceUsageDisposition.NotUsedForMethod, evaluationOnly.Disposition);
    }

    [Fact]
    public void LocalTraversalUsesPublicTransitionsWithoutTruthParents()
    {
        var comparison = TransitionFixture();
        var local = comparison.Methods.Single(item =>
            item.Method == PastStateMethod.ReconstructionWithLocalTraversal);

        Assert.Equal(["A", "X"], local.CandidateRegionIdsBefore);
        // A published traversal is positive support, not proof that an
        // unlisted route to X is impossible.
        Assert.Equal(["A", "X"], local.CandidateRegionIds);
        Assert.True(local.ForwardTransitionSteps >= 1);
        Assert.True(local.BackwardTransitionSteps >= 1);
        Assert.NotEmpty(local.TransitionIds);
        Assert.Equal(2, local.Diagnostics!.LocalStates.Count);
        Assert.Contains(local.Diagnostics.TracingSteps, item =>
            item.RealTransitionTraversalOccurred && item.TransitionIds.Count > 0);
        Assert.Contains(local.Diagnostics.TracingSteps, item =>
            item.Direction == CausalTracingDirection.Backward &&
            item.RealTransitionTraversalOccurred &&
            item.TransitionIds.Count > 0 &&
            item.CandidateInputRegionIds.SequenceEqual(["A", "X"]) &&
            item.CandidateOutputRegionIds.SequenceEqual(["A", "X"]) &&
            item.AlternativesRetained && !item.ConflictDetected);
        Assert.DoesNotContain(typeof(CausalTracingStepAudit).GetProperties(), property =>
            property.Name.Contains("Truth", StringComparison.OrdinalIgnoreCase) ||
            property.Name.Contains("Parent", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void S5UsedConstraintSourcesResolveToObservationsWithPreservedEvidenceState()
    {
        var comparison = RunBySeed(24).PublicComparison;
        var simulation = new SimulationEngine().Run(Scenario(), 24);
        var environmental = comparison.EvidenceUsage.Single(item =>
            string.Equals(item.DirectSourceId, "source-verification-west", StringComparison.Ordinal));
        var traversal = comparison.EvidenceUsage.Single(item =>
            string.Equals(item.DirectSourceId, "source-known-later-state", StringComparison.Ordinal));

        Assert.True(environmental.SourceObservationResolved);
        Assert.True(traversal.SourceObservationResolved);
        var environmentalObservation = simulation.ObservationSet.Observations.Single(item =>
            string.Equals(item.EvidenceType, "public-environment-west", StringComparison.Ordinal));
        Assert.Equal(environmentalObservation.ObservationId, environmental.SourceObservationId);
        Assert.Equal(EvidenceState.Confirmed, environmentalObservation.State);
        Assert.Equal("fork-west", environmentalObservation.ReportedRegionId);
        var generated = simulation.GroundTruthEvents.Single(item =>
            item.Kind == FootprintSim.Events.EventKind.ObservationGenerated &&
            item.Payload.TryGetValue("observationId", out var observationId) &&
            string.Equals(
                observationId.GetString(),
                environmentalObservation.ObservationId,
                StringComparison.Ordinal));
        Assert.Contains(simulation.GroundTruthEvents, item =>
            string.Equals(item.Id, generated.CauseEventId, StringComparison.Ordinal) &&
            item.Kind == FootprintSim.Events.EventKind.EvidenceExchanged);
        Assert.Equal("fork-east", RunBySeed(24).TruthPersonRegionId);
        Assert.All(
            SixConditionBatch.Value.Telemetry!.Evaluation.PastStateQueries
                .SelectMany(item => item.EvidenceUsage),
            item => Assert.True(item.SourceObservationResolved));

        var shared = new SimulationEngine().Run(Scenario(), 1);
        var conflicting = new SimulationEngine().Run(Scenario(), 2);
        Assert.Contains(shared.ObservationSet.Observations, item =>
            item.EvidenceType == "shared-record-check" &&
            item.SubjectIds.SequenceEqual(["person-a"]) &&
            item.State == EvidenceState.Confirmed);
        Assert.Contains(conflicting.ObservationSet.Observations, item =>
            item.EvidenceType == "independent-conflicting-check" &&
            item.SubjectIds.SequenceEqual(["person-a"]) &&
            item.State == EvidenceState.Conflicting);
    }

    [Fact]
    public void UnresolvedSourceReferenceRemainsExplicitWithoutBecomingAbsence()
    {
        var resolved = VerificationOnlyFixture();
        var unresolved = VerificationOnlyFixture(sourceObservationResolved: false);
        var usage = unresolved.EvidenceUsage.Single(item =>
            item.UsageRoles.Contains(PastStateEvidenceUsageRole.CandidateVerification));

        Assert.False(usage.SourceObservationResolved);
        Assert.Equal(
            resolved.Reconstruction.Candidates.Select(item => item.RegionId),
            unresolved.Reconstruction.Candidates.Select(item => item.RegionId));
        Assert.Equal(
            resolved.Reconstruction.Verification.Select(item => item.Disposition),
            unresolved.Reconstruction.Verification.Select(item => item.Disposition));
        Assert.DoesNotContain(typeof(PastStateConstraintUsage).GetProperties(), property =>
            property.Name.Contains("Absent", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void S5ValidationRejectsMissingOrSemanticallyMismatchedRecordedSources()
    {
        Assert.Throws<InvalidDataException>(() => MutatedScenario(root =>
        {
            var steps = root["steps"]!.AsArray();
            var source = steps.Single(item => item!["id"]!.GetValue<string>() == "shared-record-check-source");
            steps.Remove(source);
        }));
        Assert.Throws<InvalidDataException>(() => MutatedScenario(root =>
            root["steps"]!.AsArray()
                .Single(item => item!["id"]!.GetValue<string>() == "public-environment-west-record")!["observation"]!["state"] =
                    "supportedButUncertain"));
        Assert.Throws<InvalidDataException>(() => MutatedScenario(root =>
            root["steps"]!.AsArray()
                .Single(item => item!["id"]!.GetValue<string>() == "device-later-fix")!["observation"]!["reportedSubjectIds"] =
                    new JsonArray("person-a")));
    }

    [Fact]
    public void ClosedIntervalsIncludeEqualBoundariesAndCutoffEquality()
    {
        const string scenarioId = "closed-interval-fixture";
        const int seed = 17;
        var query = PastStateQuery.Create(
            scenarioId,
            seed,
            "person-a",
            new InteractionTimeWindow(10, 12),
            "closed-interval-query",
            20);
        var atBoundary = CausalTracingConstraint.Create(
            scenarioId,
            seed,
            "boundary-observation",
            "boundary-source",
            ["boundary-ancestry"],
            "person-a",
            TracingConstraintSourceKind.CrossThreadObservation,
            "boundary-relation",
            [new InteractionParticipant(
                "person-a",
                InteractionEntityKind.Person,
                InteractionParticipantRole.EncounterParticipant)],
            TracingConstraintRole.Local,
            InteractionTimeWindow.At(20),
            new InteractionTimeWindow(12, 13),
            ["A"],
            20,
            EvidenceState.Confirmed,
            isPersonEvidence: true,
            isDeviceOnlyEvidence: false,
            InteractionVisibility.PublicEvidence,
            "boundary-derivation",
            [new InteractionProvenance(InteractionProvenanceKind.ExternalSource, "boundary-source")]);

        var comparison = new PastStateReconstructor().Compare(
            new PastStatePublicContext
            {
                Query = query,
                PublicCandidateStates = [PublicState(10, 12, ["A", "B"])],
                GenerationDirectSourceIds = ["boundary-source"],
                AvailableObservationIds = ["boundary-observation"],
                LocalTraversalWindowPaddingTicks = 0
            },
            new ObservationSet { ScenarioId = scenarioId, Seed = seed, Observations = [] },
            new CausalTracingConstraintSet(scenarioId, seed, [atBoundary]));

        Assert.Equal(["A", "B"], comparison.Reconstruction.Candidates
            .Select(item => item.RegionId).OrderBy(item => item, StringComparer.Ordinal));
        Assert.Equal(["A", "B"], comparison.Methods.Single(item =>
            item.Method == PastStateMethod.ReconstructionWithLocalTraversal).CandidateRegionIds);
        Assert.All(comparison.Reconstruction.Candidates, item =>
            Assert.DoesNotContain(atBoundary.ConstraintId, item.AppliedConstraintIds));
        Assert.Contains(comparison.Reconstruction.EvidenceSet.Constraints, item =>
            item.AvailableAtTick == query.EvidenceAvailabilityCutoff);
    }

    [Theory]
    [InlineData("P1", false, "a,b,c", "a,b,c")]
    [InlineData("P1", true, "a,b,c", "a,b,c")]
    [InlineData("P3", false, "a,b", "a,b")]
    [InlineData("P3", true, "a,b", "a,b")]
    [InlineData("P4", false, "a,b", "a,b")]
    [InlineData("P4", true, "a,b", "a,b")]
    [InlineData("F1", false, "a,b", "a,b")]
    [InlineData("S1", false, "a,b,c", "a")]
    [InlineData("S1", true, "a,b,c", "a")]
    [InlineData("S2", false, "a,b,c", "b")]
    [InlineData("P5-window", false, "a,b,c", "c")]
    [InlineData("P5-window", true, "a,b,c", "c")]
    public void Ar43GenerationProjectsPossibleTimesWithoutInventingSupport(
        string example, bool eventPoints, string expected, string supported)
    {
        var x = Ar43TemporalEvidence("x", 5, ["a"]);
        var y = Ar43TemporalEvidence("y", 6, ["b"]);
        CausalTracingConstraint[] facts = example switch
        {
            "P1" => [Ar43TemporalEvidence("x", 5, ["a", "c"]),
                Ar43TemporalEvidence("y", 6, ["b", "c"])],
            "P3" => [x, y],
            "P4" or "F1" => [x, Ar43TemporalEvidence("y", 6, ["a", "b"])],
            "S1" => [x],
            "S2" => [y],
            "P5-window" => [Ar43TemporalEvidence("x", 6, ["a", "c"]),
                Ar43TemporalEvidence("y", 6, ["b", "c"])],
            _ => throw new InvalidOperationException(example)
        };
        var comparison = Ar43TemporalFixture(facts, eventPoints: eventPoints,
            regions: example == "F1" ? ["a", "b", "c", "d"] : null);
        var candidates = comparison.Reconstruction.Candidates;
        Assert.Equal(expected.Split(','), candidates.Select(item => item.RegionId)
            .OrderBy(item => item, StringComparer.Ordinal));
        Assert.Equal(expected.Split(','), comparison.Methods.Single(item =>
            item.Method == PastStateMethod.ReconstructionWithLocalTraversal).CandidateRegionIds);
        Assert.Equal(supported.Split(','), candidates
            .Where(item => item.Disposition == PastStateCandidateDisposition.Supported)
            .Select(item => item.RegionId).OrderBy(item => item, StringComparer.Ordinal));
        Assert.All(candidates, candidate =>
        {
            Assert.Empty(candidate.ConflictingConstraintIds);
            Assert.NotEmpty(candidate.GenerationEvidenceIds);
            Assert.NotEmpty(candidate.SourceAncestryIds);
            if (!supported.Split(',').Contains(candidate.RegionId, StringComparer.Ordinal))
            {
                Assert.Equal(PastStateCandidateDisposition.Unsupported, candidate.Disposition);
                Assert.Empty(candidate.AppliedConstraintIds);
                Assert.NotEmpty(candidate.UnresolvedConstraintIds);
            }
        });

        // These are post-comparison synthetic truths, not inference inputs.
        string[] possibleTruth = example == "P5-window" ? ["a", "c"] : ["a", "b"];
        Assert.All(possibleTruth, region => Assert.Contains(candidates, item => item.RegionId == region));
    }

    [Fact]
    public void Ar43PointIntersectionAndSameTickConflictsRemainDistinctFromWindowUnion()
    {
        var x = Ar43TemporalEvidence("x", 6, ["a", "c"]);
        var y = Ar43TemporalEvidence("y", 6, ["b", "c"]);
        var point = Ar43TemporalFixture([x, y], startTick: 6);
        Assert.Equal(["c"], point.Reconstruction.Candidates.Select(item => item.RegionId));
        var conflict = Ar43TemporalFixture([
            Ar43TemporalEvidence("x", 6, ["a"]), Ar43TemporalEvidence("y", 6, ["b"])]);
        Assert.Equal(["a", "b", "c"], conflict.Reconstruction.Candidates
            .Select(item => item.RegionId).OrderBy(item => item, StringComparer.Ordinal));
        Assert.All(conflict.Reconstruction.Candidates, candidate =>
        {
            Assert.Equal(PastStateCandidateDisposition.Conflicting, candidate.Disposition);
            Assert.Equal(2, candidate.ConflictingConstraintIds.Count);
        });
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void Ar43VerificationRequiresRefutationAtEveryPossibleTime(
        bool eventPoints, bool generateAtFive)
    {
        var x = Ar43TemporalEvidence("x", 5, ["a"]);
        var y = Ar43TemporalEvidence("y", 6, ["b"]);
        var comparison = Ar43TemporalFixture(generateAtFive ? [x] : [], [y], eventPoints);
        var a = comparison.Reconstruction.Candidates.Single(item => item.RegionId == "a");
        var verification = comparison.Reconstruction.Verification.Single(item => item.CandidateId == a.CandidateId);
        Assert.Equal(CandidateVerificationDisposition.Unresolved, verification.Disposition);
        Assert.Empty(verification.RefutingConstraintIds);
        Assert.Empty(verification.ConflictingConstraintIds);
        Assert.Contains(y.SourceObservationId, verification.VerificationEvidenceIds);
        var withoutVerification = Ar43TemporalFixture(generateAtFive ? [x] : [], eventPoints: eventPoints);
        Assert.Equal(JsonDefaults.Serialize(withoutVerification.Reconstruction.Candidates),
            JsonDefaults.Serialize(comparison.Reconstruction.Candidates));

        var both = Ar43TemporalFixture([], [x, y], eventPoints);
        foreach (var candidate in both.Reconstruction.Candidates)
        {
            var check = both.Reconstruction.Verification.Single(item => item.CandidateId == candidate.CandidateId);
            Assert.Equal(candidate.RegionId == "c"
                ? CandidateVerificationDisposition.IndependentlyRefuted
                : CandidateVerificationDisposition.IndependentlySupported, check.Disposition);
            Assert.Empty(check.ConflictingConstraintIds);
            if (candidate.RegionId != "c") Assert.Empty(check.RefutingConstraintIds);
        }
    }

    [Fact]
    public void Ar43VerificationPreservesSameTickRefutationAndIndependentConflict()
    {
        var x = Ar43TemporalEvidence("x", 6, ["a"]);
        var y = Ar43TemporalEvidence("y", 6, ["b"]);
        var point = Ar43TemporalFixture([], [y], startTick: 6);
        var a = point.Reconstruction.Candidates.Single(item => item.RegionId == "a");
        Assert.Equal(CandidateVerificationDisposition.IndependentlyRefuted,
            point.Reconstruction.Verification.Single(item => item.CandidateId == a.CandidateId).Disposition);
        var generated = Ar43TemporalFixture([x], [y], startTick: 6);
        Assert.Equal(CandidateVerificationDisposition.IndependentlyRefuted,
            Assert.Single(generated.Reconstruction.Verification).Disposition);
        var contradiction = Ar43TemporalFixture([], [x, y]);
        foreach (var candidate in contradiction.Reconstruction.Candidates.Where(item => item.RegionId != "c"))
        {
            var check = contradiction.Reconstruction.Verification.Single(item => item.CandidateId == candidate.CandidateId);
            Assert.Equal(CandidateVerificationDisposition.Conflicting, check.Disposition);
            Assert.NotEmpty(check.ConflictingConstraintIds);
        }
    }

    [Fact]
    public void Ar43TemporalGenerationHonorsCopiesWeakDeviceCutoffAndSharedVerification()
    {
        var x = Ar43TemporalEvidence("x", 5, ["a"]);
        var y = Ar43TemporalEvidence("y", 6, ["b"]);
        var baseline = Ar43TemporalFixture([x, y]);
        var copied = Ar43TemporalFixture([y, Ar43TemporalEvidence("x", 5, ["a"]), x]);
        Assert.Equal(JsonDefaults.Serialize(baseline), JsonDefaults.Serialize(copied));
        var sameSource = Ar43TemporalFixture([x, Ar43TemporalEvidence("x", 6, ["b"])]);
        Assert.Equal(["a", "b"], sameSource.Reconstruction.Candidates
            .Select(item => item.RegionId).OrderBy(item => item, StringComparer.Ordinal));
        Assert.Single(sameSource.Reconstruction.Candidates.SelectMany(item => item.GenerationSourceGroupIds)
            .Distinct(StringComparer.Ordinal));
        foreach (var limited in new[]
        {
            Ar43TemporalEvidence("y", 6, ["b"], availableTick: 21),
            Ar43TemporalEvidence("y", 6, ["b"], state: EvidenceState.SupportedButUncertain),
            Ar43TemporalEvidence("y", 6, ["b"], deviceOnly: true)
        })
        {
            var comparison = Ar43TemporalFixture([x, limited]);
            Assert.Equal(["a", "b", "c"], comparison.Reconstruction.Candidates
                .Select(item => item.RegionId).OrderBy(item => item, StringComparer.Ordinal));
        }

        var shared = Ar43TemporalFixture([x], [Ar43TemporalEvidence("y", 6, ["b"], ancestry: "origin-x")]);
        Assert.All(shared.Reconstruction.Verification, item =>
        {
            Assert.Equal(CandidateVerificationDisposition.SharedAncestryOnly, item.Disposition);
            Assert.Empty(item.RefutingConstraintIds);
            Assert.Empty(item.SupportingConstraintIds);
        });
        var fewer = Ar43TemporalFixture([x]).Reconstruction.Candidates.Select(item => item.RegionId).ToHashSet();
        var more = Ar43TemporalFixture([x, Ar43TemporalEvidence("y", 6, ["a", "b"])])
            .Reconstruction.Candidates.Select(item => item.RegionId).ToHashSet();
        Assert.True(more.IsSubsetOf(fewer));
    }

    [Theory]
    [InlineData(true, true, false, false, "a,b")]
    [InlineData(true, true, false, true, "a,b")]
    [InlineData(false, true, false, false, "a,b,c")]
    [InlineData(true, true, true, false, "a,b")]
    [InlineData(true, false, false, false, "a,b,c")]
    public void Ar43WindowUnionKeepsEveryReceivedLocation(
        bool atFive, bool atSix, bool eventPoints, bool publicEdge,
        string expected)
    {
        var compared = Ar43WindowFixture(atFive, atSix, eventPoints, publicEdge);
        var local = compared.Methods.Single(item =>
            item.Method == PastStateMethod.ReconstructionWithLocalTraversal);
        Assert.Equal(expected.Split(','), local.CandidateRegionIds);
        Assert.Equal(["a", "b", "c"], local.CandidateRegionIdsBefore);
    }

    [Fact]
    public void Ar43WindowUnionHonorsCutoffAndSourceCopies()
    {
        var before = Ar43WindowFixture(true, true, delayedB: true)
            .Methods.Single(item =>
                item.Method == PastStateMethod.ReconstructionWithLocalTraversal);
        var after = Ar43WindowFixture(true, true)
            .Methods.Single(item =>
                item.Method == PastStateMethod.ReconstructionWithLocalTraversal);
        var duplicated = Ar43WindowFixture(true, true, reverseCopy: true)
            .Methods.Single(item =>
                item.Method == PastStateMethod.ReconstructionWithLocalTraversal);
        Assert.Equal(["a", "b", "c"], before.CandidateRegionIds);
        Assert.Equal(["a", "b"], after.CandidateRegionIds);
        Assert.Equal(after.CandidateRegionIds, duplicated.CandidateRegionIds);
        Assert.Equal(JsonDefaults.Serialize(after.Diagnostics),
            JsonDefaults.Serialize(duplicated.Diagnostics));
        Assert.DoesNotContain(before.Diagnostics!.ConstraintUsages,
            item => item.SourceObservationId == "ar43-obs-b");
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public void Ar43TraversalTelemetryResolvesPartitionedStateIds(
        bool eventPoints, bool publishedMove)
    {
        var compared = Ar43WindowFixture(
            atFive: true, atSix: true, eventPoints: eventPoints, edge: publishedMove);
        var local = compared.Methods.Single(item =>
            item.Method == PastStateMethod.ReconstructionWithLocalTraversal);
        var diagnostics = local.Diagnostics!;
        var memberships = diagnostics.LocalStates.ToDictionary(
            item => item.StateId, StringComparer.Ordinal);

        // The exact-tick evidence partitions [5,6] into two independently
        // identifiable slices. The telemetry join must retain both.
        Assert.Equal(
            [InteractionTimeWindow.At(5), InteractionTimeWindow.At(6)],
            diagnostics.LocalStates
                .Where(item => item.IsTargetWindowState)
                .Select(item => item.TimeWindow)
                .Distinct()
                .OrderBy(item => item.StartTick)
                .ToArray());
        var touched = diagnostics.TracingSteps
            .Where(step => step.ConstraintId is not null)
            .SelectMany(step => new[] { step.SourceStateId, step.DestinationStateId })
            .Where(id => id is not null)
            .Select(id => id!)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        Assert.NotEmpty(touched);
        foreach (var id in touched)
        {
            Assert.True(memberships.TryGetValue(id, out var membership),
                "Missing tracing state from M1-E local telemetry membership: " + id);
            Assert.NotEmpty(membership!.CandidateIds);
            Assert.All(membership.CandidateIds, candidateId =>
                Assert.Contains(compared.Reconstruction.Candidates,
                    candidate => candidate.CandidateId == candidateId));
        }
    }

    [Fact]
    public void Ar43OverlappingSourceWindowsDoNotInventLocalStateMembership()
    {
        const string scenario = "ar43-overlapping-source-windows";
        const int seed = 43;
        var exact = CausalTracingConstraint.Create(
            scenario, seed, "ar43-overlap-observation", "ar43-overlap-source",
            ["ar43-overlap-ancestry"], "person-a",
            TracingConstraintSourceKind.CrossThreadObservation, "person-person",
            [new InteractionParticipant("person-a", InteractionEntityKind.Person,
                InteractionParticipantRole.EncounterParticipant)],
            TracingConstraintRole.Local, InteractionTimeWindow.At(1),
            InteractionTimeWindow.At(1), ["shared"], 1,
            EvidenceState.Confirmed, true, false,
            InteractionVisibility.PublicEvidence, "ar43-overlap-derivation",
            [new InteractionProvenance(InteractionProvenanceKind.ExternalSource,
                "ar43-overlap-ancestry")]);
        var comparison = new PastStateReconstructor().Compare(
            new PastStatePublicContext
            {
                Query = PastStateQuery.Create(scenario, seed, "person-a",
                    new InteractionTimeWindow(0, 3), "ar43-overlap-query", 3),
                PublicCandidateStates =
                [
                    PublicState(0, 3, ["a", "shared"]),
                    PublicState(1, 2, ["b", "shared"])
                ],
                TraversalDirectSourceIds = ["ar43-overlap-source"],
                AvailableObservationIds = ["ar43-overlap-observation"],
                LocalTraversalWindowPaddingTicks = 0
            },
            new ObservationSet { ScenarioId = scenario, Seed = seed, Observations = [] },
            new CausalTracingConstraintSet(scenario, seed, [exact]));
        var diagnostics = comparison.Methods.Single(item =>
            item.Method == PastStateMethod.ReconstructionWithLocalTraversal).Diagnostics!;
        var memberships = diagnostics.LocalStates;
        // BuildLocalStates intentionally expands both target states to the
        // union of candidate regions. Their provenance and source identities
        // still differ. Only A@[0], A@[1], A@[2,3], B@[1], B@[2] may exist:
        // B@[2] must not create an extra A@[2] audit membership.
        Assert.Equal(
            [
                "0-0",
                "1-1",
                "1-1",
                "2-2",
                "2-3"
            ],
            memberships
                .Select(item => $"{item.TimeWindow.StartTick}-{item.TimeWindow.EndTick}")
                .OrderBy(item => item, StringComparer.Ordinal)
                .ToArray());
        Assert.All(memberships, item =>
            Assert.Equal(["a", "b", "shared"], item.RegionIds));
        Assert.Equal(5, memberships.Select(item => item.StateId)
            .Distinct(StringComparer.Ordinal).Count());
        Assert.All(memberships, item => Assert.NotEmpty(item.CandidateIds));
        var witnessedIds = diagnostics.TracingSteps
            .Where(step => step.ConstraintId == exact.ConstraintId)
            .SelectMany(step => new[] { step.SourceStateId, step.DestinationStateId })
            .Where(id => id is not null)
            .Select(id => id!)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        Assert.NotEmpty(witnessedIds);
        Assert.All(witnessedIds, id =>
            Assert.Contains(memberships, item => item.StateId == id));
    }

    [Fact]
    public void Ar43RecreatedCopiesAreIdempotentAcrossPublicComparison()
    {
        var original = Ar43WindowFixture(true, true);
        var reproduced = Ar43WindowFixture(true, true, recreatedCopy: true);

        Assert.Equal(JsonDefaults.Serialize(original), JsonDefaults.Serialize(reproduced));
        Assert.Equal(2, reproduced.Reconstruction.EvidenceSet.Constraints.Count);
        Assert.Equal(2, reproduced.EvidenceUsage.Count);
        foreach (var method in reproduced.Methods)
        {
            var baselineMethod = original.Methods.Single(item => item.Method == method.Method);
            Assert.Equal(JsonDefaults.Serialize(baselineMethod.Diagnostics),
                JsonDefaults.Serialize(method.Diagnostics));
            Assert.Equal(method.Diagnostics!.ConstraintUsages.Count,
                method.Diagnostics.ConstraintUsages
                    .Select(item => item.ConstraintId)
                    .Distinct(StringComparer.Ordinal)
                    .Count());
        }
    }

    [Fact]
    public void Ar43SameSourceCanSupplyDifferentValidClaims()
    {
        var compared = Ar43WindowFixture(true, false, sameSourceSecondClaim: true);
        var evidence = compared.Reconstruction.EvidenceSet.Constraints;

        Assert.Equal(2, evidence.Count);
        Assert.Equal(2, evidence.Select(item => item.ConstraintId)
            .Distinct(StringComparer.Ordinal).Count());
        Assert.All(evidence, item =>
        {
            Assert.Equal("ar43-source-a", item.DirectSourceId);
            Assert.Contains("ar43-origin-a", item.SourceAncestryIds);
        });
        Assert.Equal(2, compared.EvidenceUsage.Count);
        var local = compared.Methods.Single(item =>
            item.Method == PastStateMethod.ReconstructionWithLocalTraversal);
        Assert.Equal(2, local.Diagnostics!.ConstraintUsages.Count);
        Assert.Equal(["a"], local.CandidateRegionIds);
    }

    [Fact]
    public void LocalNeighborhoodSelectionIsChronologicalAndDeterministic()
    {
        var ordered = LocalNeighborhoodFixture(
        [
            PublicState(0, 8, ["old"]),
            PublicState(9, 9, ["previous"]),
            PublicState(10, 12, ["A", "B"]),
            PublicState(13, 13, ["next"]),
            PublicState(14, 20, ["later"])
        ]);
        var reversed = LocalNeighborhoodFixture(
        [
            PublicState(14, 20, ["later"]),
            PublicState(13, 13, ["next"]),
            PublicState(10, 12, ["A", "B"]),
            PublicState(9, 9, ["previous"]),
            PublicState(0, 8, ["old"])
        ]);
        var expected = new[]
        {
            new InteractionTimeWindow(9, 9),
            new InteractionTimeWindow(10, 12),
            new InteractionTimeWindow(13, 13)
        };

        Assert.Equal(expected, ordered);
        Assert.Equal(expected, reversed);
    }

    [Fact]
    public void NoPublishedPublicEdgeLeavesCandidatesUnknownRatherThanAbsent()
    {
        var comparison = TransitionFixture(includePublicTransition: false);
        var local = comparison.Methods.Single(item =>
            item.Method == PastStateMethod.ReconstructionWithLocalTraversal);

        Assert.Equal(["A", "X"], local.CandidateRegionIds);
        Assert.Equal(0, local.ForwardTransitionSteps);
        Assert.Equal(0, local.BackwardTransitionSteps);
        Assert.Empty(local.TransitionIds);
        Assert.DoesNotContain(typeof(PastStateMethodResult).GetProperties(), property =>
            property.Name.Contains("Absent", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void S5PublicTopologyIsSeedIndependentScenarioDataWithoutRealizedSelections()
    {
        var scenario = Scenario();
        var envelope = FootprintSim.Hazards.PublicHazardEnvelopeBuilder.Create(
            scenario.StochasticHazard!);
        var serialized = JsonDefaults.Serialize(envelope);

        Assert.Equal(
            scenario.StochasticHazard!.Transitions.Count(item => item.PubliclyVisible),
            envelope.Branches.Count);
        Assert.Equal(4, envelope.Branches.Count);
        Assert.Contains(envelope.Branches, item =>
            item.SourceRegionId == "release" && item.DestinationRegionId == "fork-east");
        Assert.Contains(envelope.Branches, item =>
            item.SourceRegionId == "release" && item.DestinationRegionId == "fork-west");
        Assert.DoesNotContain("truth", serialized, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("realized", serialized, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("random", serialized, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("parent", serialized, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void PairedBoundaryContractsDoNotEncodeTheirConverses()
    {
        var run = Run(PastStateEvidenceCondition.DistributedPublicEvidence);
        var reconstruction = run.PublicComparison.Reconstruction;
        var reconstructionMethod = run.PublicComparison.Methods.Single(item =>
            item.Method == PastStateMethod.ConstraintBasedReconstruction);
        var traversalMethod = run.PublicComparison.Methods.Single(item =>
            item.Method == PastStateMethod.BackwardTraversal);

        Assert.NotEmpty(reconstruction.Candidates);
        Assert.Null(reconstructionMethod.TracingSessionId);
        Assert.NotNull(traversalMethod.TracingSessionId);
        Assert.DoesNotContain(typeof(PastStateMethodResult).GetProperties(), property =>
            Contains(property.PropertyType, typeof(PastStateCandidate)));

        Assert.NotEqual(CausalTracingDirection.Forward, CausalTracingDirection.Backward);
        Assert.DoesNotContain(typeof(ObservationRecord).GetProperties(), property =>
            Contains(property.PropertyType, typeof(CausalTracingConstraint)));
        Assert.DoesNotContain(typeof(CausalTracingConstraint).GetProperties(), property =>
            Contains(property.PropertyType, typeof(InteractionOccurrence)));

        Assert.All(reconstruction.Verification, verification =>
        {
            var candidate = reconstruction.Candidates.Single(item =>
                item.CandidateId == verification.CandidateId);
            Assert.DoesNotContain(
                verification.VerificationEvidenceIds,
                candidate.GenerationEvidenceIds.Contains);
        });
    }

    [Fact]
    public void CandidateAddressesAndBatchTelemetryAreDeterministic()
    {
        var first = RunBatch(Enumerable.Range(0, 6).ToArray(), collectTelemetry: true);
        var second = RunBatch(Enumerable.Range(0, 6).Reverse().ToArray(), collectTelemetry: true);

        Assert.Equal(
            JsonDefaults.Serialize(first.PastStateReconstruction),
            JsonDefaults.Serialize(second.PastStateReconstruction));
        Assert.Equal(JsonDefaults.Serialize(first.Telemetry), JsonDefaults.Serialize(second.Telemetry));
        Assert.All(first.PastStateReconstruction!.Runs, item =>
            Assert.Equal(1m, item.DeterministicCandidateIdentityRate));
    }

    [Fact]
    public void ChangingEvaluationTruthDoesNotChangePublicCandidateAddresses()
    {
        var run = Run(PastStateEvidenceCondition.DistributedPublicEvidence);
        var comparison = run.PublicComparison;
        var west = PastStateReconstructionEvaluationBuilder.Evaluate(
            comparison,
            new PastStateTruthEvaluation
            {
                PersonRegionId = "fork-west",
                DeviceRegionId = "fork-west",
                OutcomeIdentifier = "evaluation-west"
            },
            run.Condition,
            0);
        var east = PastStateReconstructionEvaluationBuilder.Evaluate(
            comparison,
            new PastStateTruthEvaluation
            {
                PersonRegionId = "fork-east",
                DeviceRegionId = "fork-east",
                OutcomeIdentifier = "evaluation-east"
            },
            run.Condition,
            0);

        Assert.Equal(
            west.PublicComparison.Reconstruction.Candidates.Select(item => item.CandidateId),
            east.PublicComparison.Reconstruction.Candidates.Select(item => item.CandidateId));
        Assert.NotNull(west.TargetCandidateRepresented);
        Assert.NotNull(east.TargetCandidateRepresented);
    }

    [Fact]
    public async Task TelemetryVersionFourAuditsEvidenceUsageAndLocalTraversal()
    {
        var result = RunBatch([0], collectTelemetry: true);
        var telemetry = result.Telemetry!;
        Assert.Equal(4, telemetry.Manifest.TelemetrySchemaVersion);
        Assert.Single(telemetry.Evaluation.PastStateQueries);
        Assert.NotEmpty(telemetry.Evaluation.PastStateCandidates);
        Assert.NotEmpty(telemetry.Evaluation.CandidateVerifications);
        Assert.Equal(3, telemetry.Evaluation.PastStateMethods.Count);
        Assert.Contains(telemetry.Evaluation.PastStateQueries.Single().EvidenceUsage, item =>
            item.UsageRoles.Contains(PastStateEvidenceUsageRole.CandidateVerification));
        var local = telemetry.Evaluation.PastStateMethods.Single(item =>
            item.Method == PastStateMethod.ReconstructionWithLocalTraversal);
        Assert.True(local.ForwardTransitionSteps >= 1);
        Assert.True(local.BackwardTransitionSteps >= 1);
        Assert.NotEmpty(local.LocalStates);
        Assert.NotEmpty(local.TransitionIds);
        Assert.Equal(
            local.Steps,
            telemetry.Evaluation.CausalTracingSteps.Count(item =>
                string.Equals(item.MethodId, local.MethodId, StringComparison.Ordinal)));
        Assert.Contains(telemetry.Evaluation.CausalTracingSteps, item =>
            item.PastStateMethod == PastStateMethod.ReconstructionWithLocalTraversal &&
            item.RealTransitionTraversalOccurred);
        Assert.DoesNotContain(typeof(PastStateCandidateTelemetryRecord).GetProperties(), property =>
            property.Name.Contains("Truth", StringComparison.OrdinalIgnoreCase));

        var directory = Path.Combine(Path.GetTempPath(), $"m1e-telemetry-{Guid.NewGuid():N}");
        try
        {
            await new TelemetryOutputWriter().WriteAsync(
                Path.Combine(directory, TelemetryOutputNames.RootDirectory),
                telemetry);
            Assert.All(
                new[]
                {
                    TelemetryOutputNames.PastStateQueries,
                    TelemetryOutputNames.PastStateCandidates,
                    TelemetryOutputNames.CandidateVerifications,
                    TelemetryOutputNames.PastStateMethods
                },
                relativePath => Assert.True(File.Exists(Path.Combine(
                    directory,
                    TelemetryOutputNames.RootDirectory,
                    relativePath.Replace('/', Path.DirectorySeparatorChar)))));
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void TelemetryCanRemainDisabledForS5()
    {
        var result = RunBatch([0], collectTelemetry: false);
        Assert.Null(result.Telemetry);
        Assert.NotNull(result.PastStateReconstruction);
    }

    private static PastStateRunEvaluation Run(PastStateEvidenceCondition condition) =>
        SixConditionBatch.Value.PastStateReconstruction!.Runs.Single(item => item.Condition == condition);

    private static PastStateRunEvaluation RunBySeed(int seed) =>
        MethodCompositionBatch.Value.PastStateReconstruction!.Runs.Single(item => item.Seed == seed);


    [Fact]
    public void Ar82IntervalConflictCannotNarrowOuterMoreThanPointConflict()
    {
        var x = Ar43TemporalEvidence("x", 5, ["a"]);
        var y = Ar43TemporalEvidence("y", 6, ["a"]);
        var interval = Ar43TemporalEvidence("z", 5, ["b"],
            state: EvidenceState.Conflicting, endTick: 6);
        var point = Ar43TemporalEvidence("z", 5, ["b"], state: EvidenceState.Conflicting);
        var noConflict = Ar43TemporalFixture([x, y]);
        Assert.Equal(["a"], noConflict.Reconstruction.Candidates.Select(item => item.RegionId));

        var widened = Ar43TemporalFixture([x, y, interval]);
        var pointConflict = Ar43TemporalFixture([x, y, point]);
        Assert.Equal(["a", "b", "c"], widened.Reconstruction.Candidates
            .Select(item => item.RegionId).OrderBy(item => item, StringComparer.Ordinal));
        Assert.Equal(pointConflict.Reconstruction.Candidates.Select(item => item.RegionId)
                .OrderBy(item => item, StringComparer.Ordinal),
            widened.Reconstruction.Candidates.Select(item => item.RegionId)
                .OrderBy(item => item, StringComparer.Ordinal));
        Assert.All(widened.Reconstruction.Candidates, candidate =>
        {
            Assert.Equal(PastStateCandidateDisposition.Conflicting, candidate.Disposition);
            Assert.Contains(interval.ConstraintId, candidate.ConflictingConstraintIds);
            Assert.DoesNotContain(interval.ConstraintId, candidate.UnresolvedConstraintIds);
        });

        // P1: a query at tick 5 also overlaps a broader [4,6] conflict.
        var partial = Ar43TemporalFixture(
            [x, Ar43TemporalEvidence("z", 4, ["b"],
                state: EvidenceState.Conflicting, endTick: 6)],
            startTick: 5, endTick: 5);
        Assert.Contains(partial.Reconstruction.Candidates, item => item.RegionId == "b");
        Assert.All(partial.Reconstruction.Candidates, candidate =>
            Assert.Equal(PastStateCandidateDisposition.Conflicting, candidate.Disposition));

        // P7/P8: the public model is already narrower at tick 6.
        PublicCandidateWarpStateDefinition[] publicStates =
            [PublicState(5, ["a", "b", "c"]), PublicState(6, ["a"])];
        var splitInterval = Ar43TemporalFixture([x, interval], publicStates: publicStates);
        var splitPoint = Ar43TemporalFixture([x, point], publicStates: publicStates);
        Assert.Contains(splitInterval.Reconstruction.Candidates, item => item.RegionId == "b");
        Assert.Equal(splitPoint.Reconstruction.Candidates.Select(item => item.RegionId)
                .OrderBy(item => item, StringComparer.Ordinal),
            splitInterval.Reconstruction.Candidates.Select(item => item.RegionId)
                .OrderBy(item => item, StringComparer.Ordinal));

        // No query-time use of a future receipt or device-only record.
        var future = Ar43TemporalEvidence("late", 5, ["b"],
            availableTick: 21, state: EvidenceState.Conflicting, endTick: 6);
        var device = Ar43TemporalEvidence("device", 5, ["b"],
            state: EvidenceState.Conflicting, deviceOnly: true, endTick: 6);
        var privateConflict = Ar43TemporalEvidence("private", 5, ["b"],
            state: EvidenceState.Conflicting, endTick: 6,
            visibility: InteractionVisibility.PrivateEvidence);
        var otherPerson = Ar43TemporalEvidence("other", 5, ["b"],
            state: EvidenceState.Conflicting, endTick: 6, targetEntityId: "person-b");
        var outside = Ar43TemporalEvidence("outside", 7, ["b"],
            state: EvidenceState.Conflicting, endTick: 8);
        var excluded = Ar43TemporalFixture([x, y, future, device, privateConflict, otherPerson, outside]);
        Assert.Equal(["a"], excluded.Reconstruction.Candidates.Select(item => item.RegionId));
        Assert.DoesNotContain(excluded.Reconstruction.EvidenceSet.Constraints,
            item => item.ConstraintId == privateConflict.ConstraintId ||
                item.ConstraintId == future.ConstraintId);
        Assert.Equal(JsonDefaults.Serialize(widened),
            JsonDefaults.Serialize(Ar43TemporalFixture([interval, y, x, interval])));
    }


    [Theory]
    [InlineData("G0")]
    [InlineData("G1")]
    [InlineData("G2")]
    [InlineData("G3")]
    public void Ar82GenerationRetainsIntervalConflictAcrossDistinctTemporalSlices(string example)
    {
        var z = example == "G2"
            ? Ar43TemporalEvidence("z", 4, ["b"],
                state: EvidenceState.Conflicting, endTick: 6)
            : Ar43TemporalEvidence("z", 5, ["b"],
                state: EvidenceState.Conflicting, endTick: 6);
        CausalTracingConstraint[] constraints = example switch
        {
            "G0" => [z],
            "G1" or "G2" => [Ar43TemporalEvidence("x", 5, ["a"]), z],
            "G3" => [Ar43TemporalEvidence("y", 6, ["a"]), z],
            _ => throw new InvalidOperationException(example)
        };
        var comparison = Ar43TemporalFixture(constraints);
        Assert.Equal(["a", "b", "c"], comparison.Reconstruction.Candidates
            .Select(item => item.RegionId).OrderBy(item => item, StringComparer.Ordinal));
        Assert.All(comparison.Reconstruction.Candidates, candidate =>
        {
            Assert.Equal(PastStateCandidateDisposition.Conflicting, candidate.Disposition);
            Assert.Contains(z.ConstraintId, candidate.ConflictingConstraintIds);
            Assert.DoesNotContain(z.ConstraintId, candidate.UnresolvedConstraintIds);
        });
    }

    [Fact]
    public void Ar82GenerationAndIndependentVerificationPreserveIntervalConflictLineage()
    {
        var x = Ar43TemporalEvidence("x", 5, ["a"]);
        var z = Ar43TemporalEvidence("z", 5, ["b"],
            state: EvidenceState.Conflicting, endTick: 6);
        var generated = Ar43TemporalFixture([x, z]);
        Assert.All(generated.Reconstruction.Candidates, candidate =>
        {
            Assert.Contains(z.ConstraintId, candidate.ConflictingConstraintIds);
            Assert.Equal(PastStateCandidateDisposition.Conflicting, candidate.Disposition);
        });

        var verified = Ar43TemporalFixture([x], [z]);
        Assert.All(verified.Reconstruction.Verification, check =>
        {
            Assert.Equal(CandidateVerificationDisposition.Conflicting, check.Disposition);
            Assert.Contains(z.ConstraintId, check.ConflictingConstraintIds);
            Assert.Empty(check.RefutingConstraintIds);
        });
        var shared = Ar43TemporalFixture([x], [Ar43TemporalEvidence("z", 5, ["b"],
            state: EvidenceState.Conflicting, ancestry: "origin-x", endTick: 6)]);
        Assert.All(shared.Reconstruction.Verification, check =>
        {
            Assert.Equal(CandidateVerificationDisposition.SharedAncestryOnly, check.Disposition);
            Assert.Empty(check.ConflictingConstraintIds);
        });
        var partial = Ar43TemporalFixture([x, Ar43TemporalEvidence("z", 4, ["b"],
            state: EvidenceState.Conflicting, endTick: 6)]);
        Assert.All(partial.Reconstruction.Candidates, candidate =>
            Assert.NotEmpty(candidate.ConflictingConstraintIds));
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void Ar82S5SyntheticReceiptPathKeepsConflictedTruthRegionInOuter(
        bool conflictAtSingleTick, bool keepOriginalRegions)
    {
        var source = JsonNode.Parse(File.ReadAllText(Path.Combine(RepositoryRoot,
            "scenarios", "s5_distributed_past_evidence", "scenario.json")))!.AsObject();
        var definitions = source["pastStateReconstruction"]!["constraints"]!.AsArray();
        foreach (var definition in definitions)
        {
            if (definition is not JsonObject item) continue;
            var id = item["id"]?.GetValue<string>();
            if (id is "generation-a" or "generation-a-redundant")
            {
                if (!keepOriginalRegions) item["allowedRegionIds"] = new JsonArray("fork-east");
                item["constrainedStartTick"] = id == "generation-a" ? 14 : 13;
                item["constrainedEndTick"] = id == "generation-a" ? 14 : 13;
            }
            else if (id == "generation-conflict")
            {
                if (!keepOriginalRegions) item["allowedRegionIds"] = new JsonArray("fork-west");
                if (conflictAtSingleTick)
                {
                    item["constrainedStartTick"] = 13;
                    item["constrainedEndTick"] = 13;
                }
            }
        }

        // Use the same public scenario-loader/validator and batch-evaluation path
        // as the CLI; never modify the checked-in S5 fixture or pass truth to Compare.
        var temporaryPath = Path.Combine(Path.GetTempPath(), $"ar82-s5-{Guid.NewGuid():N}.json");
        try
        {
            File.WriteAllText(temporaryPath, source.ToJsonString());
            var scenario = ScenarioLoader.Load(temporaryPath);
            var batch = new BatchEvaluationRunner().Run(new BatchEvaluationRequest
            {
                Scenarios = [new EvaluationScenarioInput
                {
                    Scenario = scenario,
                    ContentSha256 = "ar82-s5-synthetic-variant"
                }],
                Seeds = [2],
                Strategies =
                [
                    new OwnEvidenceConstrainedStrategy(),
                    new SingleWeftStrategy(),
                    new MultiWeftTracingStrategy()
                ],
                SourceCommitSha = "ar82-s5-test",
                DirtyWorkingTree = false,
                CollectTelemetry = false
            });
            var run = Assert.Single(batch.PastStateReconstruction!.Runs);
            var candidates = run.PublicComparison.Reconstruction.Candidates;
            Assert.Contains(candidates, item => item.RegionId == "fork-west");
            Assert.Contains(candidates, item => item.RegionId == "fork-east");
            if (keepOriginalRegions)
                Assert.Contains(candidates, item => item.RegionId == "alternate-route");
            Assert.True(run.ConflictedCandidateCount > 0);
            Assert.All(candidates, item => Assert.NotEmpty(item.ConflictingConstraintIds));
            var evaluated = PastStateReconstructionEvaluationBuilder.Evaluate(
                run.PublicComparison,
                new PastStateTruthEvaluation
                {
                    PersonRegionId = "fork-west",
                    DeviceRegionId = "fork-west",
                    OutcomeIdentifier = "ar82-synthetic-truth-after-inference"
                },
                run.Condition, 2);
            Assert.Equal(true, evaluated.TargetCandidateRepresented);
            Assert.Equal(PastStateEvidenceCondition.ConflictingVerification, run.Condition);
            // Generation-only containment cannot prove either method retains truth.
            // Check the method-level safety outputs for interval, point and tick-split controls.
            foreach (var method in new[]
            {
                PastStateMethod.ConstraintBasedReconstruction,
                PastStateMethod.ReconstructionWithLocalTraversal
            })
            {
                var metrics = evaluated.Methods.Single(item => item.Method == method);
                Assert.Equal(true, metrics.ContainsTruth);
                Assert.Equal(false, metrics.FalseExclusion);
            }
        }
        finally
        {
            if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
        }
    }


    [Fact]
    public void Ar84ConfirmedIntervalsConflictOnlyWhenEveryPossibleTickIsExcluded()
    {
        var x = Ar43TemporalEvidence("x", 5, ["a"]);
        var y = Ar43TemporalEvidence("y", 6, ["a"]);
        var broad = Ar43TemporalEvidence("w", 5, ["b"], endTick: 6);
        var point = Ar43TemporalEvidence("w", 5, ["b"]);
        var clear = Ar43TemporalFixture([x, y]);
        Assert.Equal(["a"], clear.Reconstruction.Candidates.Select(item => item.RegionId));

        // C1/C1p: neither tick admits b, but the broad witness is not
        // equivalent to observing b at both ticks.
        var conflict = Ar43TemporalFixture([x, y, broad]);
        var pointConflict = Ar43TemporalFixture([x, y, point]);
        Assert.Equal(["a", "b", "c"], conflict.Reconstruction.Candidates
            .Select(item => item.RegionId).OrderBy(item => item, StringComparer.Ordinal));
        Assert.Equal(pointConflict.Reconstruction.Candidates.Select(item => item.RegionId)
                .OrderBy(item => item, StringComparer.Ordinal),
            conflict.Reconstruction.Candidates.Select(item => item.RegionId)
                .OrderBy(item => item, StringComparer.Ordinal));
        Assert.All(conflict.Reconstruction.Candidates, candidate =>
        {
            Assert.Equal(PastStateCandidateDisposition.Conflicting, candidate.Disposition);
            Assert.Contains(broad.ConstraintId, candidate.ConflictingConstraintIds);
            Assert.DoesNotContain(broad.ConstraintId, candidate.UnresolvedConstraintIds);
            Assert.Contains("origin-w", candidate.SourceAncestryIds);
        });

        // C2: [4,6] could have happened at 4 or 6. Do not invent a conflict
        // or undo the valid exact-tick narrowing at 5.
        var wide = Ar43TemporalEvidence("wide", 4, ["b"], endTick: 6);
        var compatible = Ar43TemporalFixture([x, wide], startTick: 5, endTick: 5);
        Assert.Equal(["a"], compatible.Reconstruction.Candidates.Select(item => item.RegionId));
        Assert.All(compatible.Reconstruction.Candidates, candidate =>
        {
            Assert.DoesNotContain(wide.ConstraintId, candidate.ConflictingConstraintIds);
            Assert.Equal(PastStateCandidateDisposition.Supported, candidate.Disposition);
        });
        var c2Point = Ar43TemporalFixture([x, Ar43TemporalEvidence("wide", 5, ["b"])],
            startTick: 5, endTick: 5);
        Assert.Contains(c2Point.Reconstruction.Candidates, candidate =>
            candidate.RegionId == "b" && candidate.Disposition == PastStateCandidateDisposition.Conflicting);

        // C2t / C2wide: an interval can be compatible at a different tick,
        // and unknown public time cannot be used as an impossibility proof.
        var c2t = Ar43TemporalFixture([x, Ar43TemporalEvidence("next", 6, ["b"]), wide]);
        Assert.Equal(["a", "b"], c2t.Reconstruction.Candidates
            .Select(item => item.RegionId).OrderBy(item => item, StringComparer.Ordinal));
        Assert.All(c2t.Reconstruction.Candidates, candidate =>
            Assert.DoesNotContain(wide.ConstraintId, candidate.ConflictingConstraintIds));
        var c2wide = Ar43TemporalFixture([x, wide]);
        Assert.All(c2wide.Reconstruction.Candidates, candidate =>
            Assert.DoesNotContain(wide.ConstraintId, candidate.ConflictingConstraintIds));

        // C3: at 5 the exact record excludes b; at 6 the declared public
        // model excludes b. No unmodelled alternative tick remains.
        PublicCandidateWarpStateDefinition[] publicStates =
            [PublicState(5, ["a", "b", "c"]), PublicState(6, ["a"])];
        var c3 = Ar43TemporalFixture([x, broad], publicStates: publicStates);
        Assert.Contains(c3.Reconstruction.Candidates, candidate => candidate.RegionId == "b");
        Assert.All(c3.Reconstruction.Candidates, candidate =>
            Assert.Contains(broad.ConstraintId, candidate.ConflictingConstraintIds));

        Assert.Equal(JsonDefaults.Serialize(conflict),
            JsonDefaults.Serialize(Ar43TemporalFixture([broad, y, x, broad])));
    }

    [Fact]
    public void Ar84IndependentConfirmedIntervalConflictRespectsReceiptAndAncestry()
    {
        var x = Ar43TemporalEvidence("x", 5, ["a"]);
        var y = Ar43TemporalEvidence("y", 6, ["a"]);
        var w = Ar43TemporalEvidence("w", 5, ["b"], endTick: 6);
        var verified = Ar43TemporalFixture([x, y], [w]);
        Assert.All(verified.Reconstruction.Verification, check =>
        {
            Assert.Equal(CandidateVerificationDisposition.Conflicting, check.Disposition);
            Assert.Contains(w.ConstraintId, check.ConflictingConstraintIds);
            Assert.Empty(check.RefutingConstraintIds);
        });

        var shared = Ar43TemporalFixture([x, y],
            [Ar43TemporalEvidence("w", 5, ["b"], ancestry: "origin-x", endTick: 6)]);
        Assert.All(shared.Reconstruction.Verification, check =>
            Assert.Empty(check.ConflictingConstraintIds));

        var ineligible = new[]
        {
            Ar43TemporalEvidence("future", 5, ["b"], availableTick: 21, endTick: 6),
            Ar43TemporalEvidence("device", 5, ["b"], deviceOnly: true, endTick: 6),
            Ar43TemporalEvidence("private", 5, ["b"],
                endTick: 6, visibility: InteractionVisibility.PrivateEvidence),
            Ar43TemporalEvidence("other", 5, ["b"],
                endTick: 6, targetEntityId: "person-b"),
            Ar43TemporalEvidence("outside", 7, ["b"], endTick: 8)
        };
        var excluded = Ar43TemporalFixture([x, y, .. ineligible]);
        Assert.Equal(["a"], excluded.Reconstruction.Candidates.Select(item => item.RegionId));
        Assert.All(excluded.Reconstruction.Candidates, candidate =>
            Assert.Empty(candidate.ConflictingConstraintIds));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Ar84S5ConfirmedIntervalAndPointContrastsKeepTruthForBothMethods(bool pointConflict)
    {
        var source = JsonNode.Parse(File.ReadAllText(Path.Combine(RepositoryRoot,
            "scenarios", "s5_distributed_past_evidence", "scenario.json")))!.AsObject();
        foreach (var node in source["pastStateReconstruction"]!["constraints"]!.AsArray())
        {
            if (node is not JsonObject item) continue;
            var id = item["id"]?.GetValue<string>();
            if (id is "generation-a" or "generation-a-redundant")
            {
                item["allowedRegionIds"] = new JsonArray("fork-east");
                item["constrainedStartTick"] = id == "generation-a" ? 14 : 13;
                item["constrainedEndTick"] = id == "generation-a" ? 14 : 13;
            }
            else if (id == "generation-conflict")
            {
                item["evidenceState"] = "confirmed";
                item["allowedRegionIds"] = new JsonArray("fork-west");
                item["constrainedStartTick"] = 13;
                item["constrainedEndTick"] = pointConflict ? 13 : 14;
            }
        }

        var path = Path.Combine(Path.GetTempPath(), $"ar84-s5-{Guid.NewGuid():N}.json");
        try
        {
            File.WriteAllText(path, source.ToJsonString());
            var scenario = ScenarioLoader.Load(path);
            var batch = new BatchEvaluationRunner().Run(new BatchEvaluationRequest
            {
                Scenarios = [new EvaluationScenarioInput
                {
                    Scenario = scenario,
                    ContentSha256 = "ar84-confirmed-interval-test"
                }],
                Seeds = [2],
                Strategies =
                [
                    new OwnEvidenceConstrainedStrategy(),
                    new SingleWeftStrategy(),
                    new MultiWeftTracingStrategy()
                ],
                SourceCommitSha = "ar84-s5-synthetic-test",
                DirtyWorkingTree = false,
                CollectTelemetry = false
            });
            var run = Assert.Single(batch.PastStateReconstruction!.Runs);
            var result = run.PublicComparison;
            Assert.Contains(result.Reconstruction.Candidates, item =>
                item.RegionId == "fork-west");
            Assert.Contains(result.Reconstruction.Candidates, item =>
                item.RegionId == "alternate-route");
            Assert.Contains(result.Reconstruction.Candidates, item =>
                item.ConflictingConstraintIds.Count > 0);
            var evaluated = PastStateReconstructionEvaluationBuilder.Evaluate(
                result, new PastStateTruthEvaluation
                {
                    PersonRegionId = "fork-west",
                    DeviceRegionId = "fork-west",
                    OutcomeIdentifier = "ar84-evaluation-only-truth"
                }, run.Condition, 2);
            Assert.Equal(true, evaluated.TargetCandidateRepresented);
            foreach (var method in new[]
            {
                PastStateMethod.ConstraintBasedReconstruction,
                PastStateMethod.ReconstructionWithLocalTraversal
            })
            {
                var metrics = evaluated.Methods.Single(item => item.Method == method);
                Assert.Equal(true, metrics.ContainsTruth);
                Assert.Equal(false, metrics.FalseExclusion);
            }
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    private static CausalTracingConstraint Ar43TemporalEvidence(
        string source, long tick, IReadOnlyList<string> regions,
        long availableTick = 20, EvidenceState state = EvidenceState.Confirmed,
        bool deviceOnly = false, string? ancestry = null, long? endTick = null,
        string targetEntityId = "person-a",
        InteractionVisibility visibility = InteractionVisibility.PublicEvidence) => CausalTracingConstraint.Create(
            "ar43-temporal-generation", 43, $"obs-{source}-{tick}", $"source-{source}",
            [ancestry ?? $"origin-{source}"], targetEntityId,
            deviceOnly ? TracingConstraintSourceKind.DeviceOnlyObservation : TracingConstraintSourceKind.CrossThreadObservation,
            "person-person", [new InteractionParticipant(targetEntityId, InteractionEntityKind.Person,
                InteractionParticipantRole.EncounterParticipant)], TracingConstraintRole.Local,
            InteractionTimeWindow.At(tick), new InteractionTimeWindow(tick, endTick ?? tick), regions,
            availableTick, state, !deviceOnly, deviceOnly, visibility,
            "ar43-temporal-generation", [new InteractionProvenance(InteractionProvenanceKind.ExternalSource, source)]);

    private static PastStateComparisonResult Ar43TemporalFixture(
        IReadOnlyList<CausalTracingConstraint> generation,
        IReadOnlyList<CausalTracingConstraint>? verification = null,
        bool eventPoints = false, IReadOnlyList<string>? regions = null,
        long startTick = 5, long endTick = 6,
        IReadOnlyList<PublicCandidateWarpStateDefinition>? publicStates = null)
    {
        verification ??= [];
        regions ??= ["a", "b", "c"];
        return new PastStateReconstructor().Compare(
            new PastStatePublicContext
            {
                Query = PastStateQuery.Create("ar43-temporal-generation", 43, "person-a",
                    new InteractionTimeWindow(startTick, endTick), "ar43-temporal-generation-query", 20),
                PublicCandidateStates = publicStates ?? (eventPoints && startTick < endTick
                    ? [PublicState(startTick, regions), PublicState(endTick, regions)]
                    : [PublicState(startTick, endTick, regions)]),
                GenerationDirectSourceIds = generation.Select(item => item.DirectSourceId)
                    .Distinct(StringComparer.Ordinal).ToArray(),
                VerificationDirectSourceIds = verification.Select(item => item.DirectSourceId)
                    .Distinct(StringComparer.Ordinal).ToArray(),
                AvailableObservationIds = generation.Concat(verification).Select(item => item.SourceObservationId)
                    .Distinct(StringComparer.Ordinal).OrderBy(item => item, StringComparer.Ordinal).ToArray(),
                LocalTraversalWindowPaddingTicks = 0
            },
            new ObservationSet { ScenarioId = "ar43-temporal-generation", Seed = 43, Observations = [] },
            new CausalTracingConstraintSet("ar43-temporal-generation", 43, generation.Concat(verification).ToArray()));
    }

    private static PastStateComparisonResult Ar43WindowFixture(
        bool atFive, bool atSix, bool eventPoints = false, bool edge = false,
        bool reverseCopy = false, bool delayedB = false,
        bool recreatedCopy = false, bool sameSourceSecondClaim = false)
    {
        const string scenario = "ar43-query-window";
        const int seed = 43;
        CausalTracingConstraint Evidence(string region, long tick) =>
            CausalTracingConstraint.Create(
                scenario, seed, "ar43-obs-" + region, "ar43-source-" + region,
                ["ar43-origin-" + region], "person-a",
                TracingConstraintSourceKind.CrossThreadObservation, "person-person",
                [new InteractionParticipant("person-a", InteractionEntityKind.Person,
                    InteractionParticipantRole.EncounterParticipant)],
                TracingConstraintRole.Local, InteractionTimeWindow.At(tick),
                InteractionTimeWindow.At(tick), [region],
                region == "b" && delayedB ? 7 : tick,
                EvidenceState.Confirmed, true, false,
                InteractionVisibility.PublicEvidence, "ar43-evidence",
                [new InteractionProvenance(InteractionProvenanceKind.ExternalSource,
                    "ar43-origin-" + region)]);
        var facts = new List<CausalTracingConstraint>();
        if (atFive) facts.Add(Evidence("a", 5));
        if (atSix) facts.Add(Evidence("b", 6));
        if (sameSourceSecondClaim) facts.Add(Evidence("a", 6));
        if (recreatedCopy) facts.Add(Evidence("a", 5));
        if (reverseCopy)
        {
            facts.Reverse();
            if (facts.Count > 0) facts.Add(facts[0]);
        }

        return new PastStateReconstructor().Compare(
            new PastStatePublicContext
            {
                Query = PastStateQuery.Create(scenario, seed, "person-a",
                    new InteractionTimeWindow(5, 6), "ar43-query", 6),
                PublicCandidateStates = eventPoints
                    ? [PublicState(5, ["a", "b", "c"]),
                       PublicState(6, ["a", "b", "c"])]
                    : [PublicState(5, 6, ["a", "b", "c"])],
                TraversalDirectSourceIds = ["ar43-source-a", "ar43-source-b"],
                AvailableObservationIds = ["ar43-obs-a", "ar43-obs-b"],
                LocalTraversalWindowPaddingTicks = 0,
                PublicHazardEnvelope = edge
                    ? new FootprintSim.Hazards.PublicHazardEnvelope
                    {
                        HazardId = "ar43-published-move",
                        SourceRegionId = "a",
                        PublishedAtTick = 0,
                        Branches =
                        [
                            new FootprintSim.Hazards.PublicHazardBranch
                            {
                                SourceRegionId = "a", DestinationRegionId = "b",
                                MinimumTravelTicks = 1, MaximumTravelTicks = 1,
                                TransportType = FootprintSim.Hazards.HazardTransportType.HazardDriven,
                                ProvenanceId = "ar43-public-a-b", PublishedAtTick = 0
                            }
                        ]
                    }
                    : null
            },
            new ObservationSet { ScenarioId = scenario, Seed = seed, Observations = [] },
            new CausalTracingConstraintSet(scenario, seed, facts));
    }

    private static PastStateComparisonResult VerificationOnlyFixture(
        bool sourceObservationResolved = true)
    {
        const string scenarioId = "verification-only-fixture";
        const int seed = 5;
        var query = PastStateQuery.Create(
            scenarioId,
            seed,
            "person-a",
            InteractionTimeWindow.At(5),
            "verification-only-query",
            10);
        var verification = CausalTracingConstraint.Create(
            scenarioId,
            seed,
            "verification-observation",
            "verification-source",
            ["verification-ancestry"],
            "person-a",
            TracingConstraintSourceKind.PublicEnvironmentalObservation,
            "fixture-verification",
            [new InteractionParticipant(
                "person-a",
                InteractionEntityKind.Person,
                InteractionParticipantRole.EncounterParticipant)],
            TracingConstraintRole.Local,
            InteractionTimeWindow.At(8),
            InteractionTimeWindow.At(5),
            ["west"],
            9,
            EvidenceState.Confirmed,
            isPersonEvidence: true,
            isDeviceOnlyEvidence: false,
            InteractionVisibility.PublicEvidence,
            "fixture-verification-derivation",
            [new InteractionProvenance(
                InteractionProvenanceKind.ExternalSource,
                "verification-source")]);

        return new PastStateReconstructor().Compare(
            new PastStatePublicContext
            {
                Query = query,
                PublicCandidateStates = [PublicState(5, ["east", "west"])],
                VerificationDirectSourceIds = ["verification-source"],
                AvailableObservationIds = sourceObservationResolved ? ["verification-observation"] : [],
                LocalTraversalWindowPaddingTicks = 0
            },
            new ObservationSet { ScenarioId = scenarioId, Seed = seed, Observations = [] },
            new CausalTracingConstraintSet(
                scenarioId,
                seed,
                [
                    verification,
                    FixtureConstraint(
                        scenarioId,
                        seed,
                        "unused-observation",
                        "unused-source",
                        EvidenceState.UnsupportedForQuery)
                ]));
    }

    private static PastStateComparisonResult TransitionFixture(bool includePublicTransition = true)
    {
        const string scenarioId = "transition-only-fixture";
        const int seed = 7;
        var query = PastStateQuery.Create(
            scenarioId,
            seed,
            "person-a",
            InteractionTimeWindow.At(0),
            "transition-only-query",
            10);
        return new PastStateReconstructor().Compare(
            new PastStatePublicContext
            {
                Query = query,
                PublicCandidateStates =
                [
                    PublicState(0, ["A", "X"]),
                    PublicState(5, ["B"])
                ],
                LocalTraversalWindowPaddingTicks = 0,
                PublicHazardEnvelope = new FootprintSim.Hazards.PublicHazardEnvelope
                {
                    HazardId = "public-transition-fixture",
                    SourceRegionId = "A",
                    PublishedAtTick = 0,
                    Branches = includePublicTransition
                        ?
                        [
                        new FootprintSim.Hazards.PublicHazardBranch
                        {
                            SourceRegionId = "A",
                            DestinationRegionId = "B",
                            MinimumTravelTicks = 5,
                            MaximumTravelTicks = 5,
                            TransportType = FootprintSim.Hazards.HazardTransportType.HazardDriven,
                            ProvenanceId = "public-a-b",
                            PublishedAtTick = 0
                        }
                        ]
                        : []
                }
            },
            new ObservationSet { ScenarioId = scenarioId, Seed = seed, Observations = [] },
            new CausalTracingConstraintSet(scenarioId, seed, []));
    }

    private static PublicCandidateWarpStateDefinition PublicState(
        long tick,
        IReadOnlyList<string> regions) => new()
        {
            TimeWindow = InteractionTimeWindow.At(tick),
            TemporalKind = CandidateWarpTemporalKind.EventPoint,
            RegionIds = regions,
            EvidenceBoundaryState = EvidenceState.SupportedButUncertain,
            Provenance =
            [
                CandidateWarpProvenance.Create(
                    CandidateWarpSourceKind.PublicCausalModel,
                    $"fixture-public-state-{tick}",
                    availableAtTick: 0,
                    isPersonEvidence: true,
                    isDeviceOnlyEvidence: false,
                    EvidenceState.SupportedButUncertain,
                    regions)
            ]
        };

    private static PublicCandidateWarpStateDefinition PublicState(
        long startTick,
        long endTick,
        IReadOnlyList<string> regions) => new()
        {
            TimeWindow = new InteractionTimeWindow(startTick, endTick),
            TemporalKind = CandidateWarpTemporalKind.StateInterval,
            RegionIds = regions,
            EvidenceBoundaryState = EvidenceState.SupportedButUncertain,
            Provenance =
            [
                CandidateWarpProvenance.Create(
                    CandidateWarpSourceKind.PublicCausalModel,
                    $"fixture-public-state-{startTick}-{endTick}",
                    availableAtTick: 0,
                    isPersonEvidence: true,
                    isDeviceOnlyEvidence: false,
                    EvidenceState.SupportedButUncertain,
                    regions)
            ]
        };

    private static IReadOnlyList<InteractionTimeWindow> LocalNeighborhoodFixture(
        IReadOnlyList<PublicCandidateWarpStateDefinition> states)
    {
        const string scenarioId = "local-neighborhood-fixture";
        const int seed = 19;
        var comparison = new PastStateReconstructor().Compare(
            new PastStatePublicContext
            {
                Query = PastStateQuery.Create(
                    scenarioId,
                    seed,
                    "person-a",
                    new InteractionTimeWindow(10, 12),
                    "local-neighborhood-query",
                    20),
                PublicCandidateStates = states,
                LocalTraversalWindowPaddingTicks = 1
            },
            new ObservationSet { ScenarioId = scenarioId, Seed = seed, Observations = [] },
            new CausalTracingConstraintSet(scenarioId, seed, []));
        return comparison.Methods.Single(item =>
                item.Method == PastStateMethod.ReconstructionWithLocalTraversal)
            .Diagnostics!.LocalStates
            .Select(item => item.TimeWindow)
            .ToArray();
    }

    private static ScenarioDefinition MutatedScenario(Action<JsonObject> mutate)
    {
        var root = JsonNode.Parse(File.ReadAllText(Path.Combine(
            RepositoryRoot,
            "scenarios",
            "s5_distributed_past_evidence",
            "scenario.json")))!.AsObject();
        mutate(root);
        var scenario = JsonSerializer.Deserialize<ScenarioDefinition>(
            root.ToJsonString(),
            JsonDefaults.Options)!;
        ScenarioValidator.Validate(scenario);
        return scenario;
    }

    private static PastStateReconstructionResult ReconstructFixture(
        EvidenceState? evidenceState,
        bool includeSharedVerification = false)
    {
        const string scenarioId = "past-state-evidence-fixture";
        const int seed = 11;
        var query = PastStateQuery.Create(
            scenarioId,
            seed,
            "person-a",
            new InteractionTimeWindow(5, 5),
            "fixture-query",
            10);
        var constraints = new List<CausalTracingConstraint>();
        if (evidenceState is not null)
        {
            constraints.Add(FixtureConstraint(
                scenarioId,
                seed,
                "fixture-observation",
                "fixture-generation-source",
                evidenceState.Value));
        }

        if (includeSharedVerification)
        {
            constraints.Add(FixtureConstraint(
                scenarioId,
                seed,
                "fixture-verification-observation",
                "fixture-verification-source",
                EvidenceState.Confirmed));
        }

        return new PastStateReconstructor().Compare(
            new PastStatePublicContext
            {
                Query = query,
                PublicCandidateStates =
                [
                    new PublicCandidateWarpStateDefinition
                    {
                        TimeWindow = InteractionTimeWindow.At(5),
                        TemporalKind = CandidateWarpTemporalKind.EventPoint,
                        RegionIds = ["region-a", "region-b"],
                        EvidenceBoundaryState = EvidenceState.SupportedButUncertain,
                        Provenance =
                        [
                            CandidateWarpProvenance.Create(
                                CandidateWarpSourceKind.PublicCausalModel,
                                "fixture-public-candidates",
                                availableAtTick: 0,
                                isPersonEvidence: true,
                                isDeviceOnlyEvidence: false,
                                EvidenceState.SupportedButUncertain,
                                ["region-a", "region-b"])
                        ]
                    }
                ],
                GenerationDirectSourceIds = ["fixture-generation-source"],
                VerificationDirectSourceIds = includeSharedVerification
                    ? ["fixture-verification-source"]
                    : [],
                LocalTraversalWindowPaddingTicks = 0
            },
            new ObservationSet { ScenarioId = scenarioId, Seed = seed, Observations = [] },
            new CausalTracingConstraintSet(scenarioId, seed, constraints)).Reconstruction;
    }

    private static CausalTracingConstraint FixtureConstraint(
        string scenarioId,
        int seed,
        string observationId,
        string directSourceId,
        EvidenceState evidenceState) => CausalTracingConstraint.Create(
            scenarioId,
            seed,
            observationId,
            directSourceId,
            ["fixture-ancestry"],
            "person-a",
            TracingConstraintSourceKind.CrossThreadObservation,
            "fixture-relation",
            [new InteractionParticipant(
                "person-a",
                InteractionEntityKind.Person,
                InteractionParticipantRole.EncounterParticipant)],
            TracingConstraintRole.Local,
            InteractionTimeWindow.At(8),
            InteractionTimeWindow.At(5),
            ["region-a"],
            9,
            evidenceState,
            isPersonEvidence: true,
            isDeviceOnlyEvidence: false,
            InteractionVisibility.PublicEvidence,
            "fixture-derivation",
            [new InteractionProvenance(
                InteractionProvenanceKind.ExternalSource,
                directSourceId)]);

    private static BatchEvaluationResult RunBatch(
        IReadOnlyList<int> seeds,
        bool collectTelemetry)
    {
        return new BatchEvaluationRunner().Run(new BatchEvaluationRequest
        {
            Scenarios = [new EvaluationScenarioInput
            {
                Scenario = Scenario(),
                ContentSha256 = "s5-test-content-hash"
            }],
            Seeds = seeds,
            Strategies =
            [
                new OwnEvidenceConstrainedStrategy(),
                new SingleWeftStrategy(),
                new MultiWeftTracingStrategy()
            ],
            SourceCommitSha = "m1e-test-commit",
            DirtyWorkingTree = false,
            CollectTelemetry = collectTelemetry
        });
    }

    private static ScenarioDefinition Scenario() => ScenarioLoader.Load(Path.Combine(
        RepositoryRoot,
        "scenarios",
        "s5_distributed_past_evidence",
        "scenario.json"));

    private static ScenarioDefinition S4Scenario() => ScenarioLoader.Load(Path.Combine(
        RepositoryRoot,
        "scenarios",
        "s4_multi_weft_tracing",
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
