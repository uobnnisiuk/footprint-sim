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
        Assert.Equal(["A"], local.CandidateRegionIds);
        Assert.True(local.ForwardTransitionSteps >= 1);
        Assert.True(local.BackwardTransitionSteps >= 1);
        Assert.NotEmpty(local.TransitionIds);
        Assert.Equal(2, local.Diagnostics!.LocalStates.Count);
        Assert.Contains(local.Diagnostics.TracingSteps, item =>
            item.RealTransitionTraversalOccurred && item.TransitionIds.Count > 0);
        Assert.Contains(local.Diagnostics.TracingSteps, item =>
            item.Direction == CausalTracingDirection.Backward &&
            item.CandidateInputRegionIds.SequenceEqual(["A", "X"]) &&
            item.CandidateOutputRegionIds.SequenceEqual(["A"]));
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

        Assert.Equal(["A"], comparison.Reconstruction.Candidates.Select(item => item.RegionId));
        Assert.Contains(comparison.Reconstruction.EvidenceSet.Constraints, item =>
            item.AvailableAtTick == query.EvidenceAvailabilityCutoff);
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
