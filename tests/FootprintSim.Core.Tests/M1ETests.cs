using System.Reflection;
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
    public async Task TelemetryVersionThreeSeparatesQueryCandidateVerificationAndMethodRecords()
    {
        var result = RunBatch([0], collectTelemetry: true);
        var telemetry = result.Telemetry!;
        Assert.Equal(3, telemetry.Manifest.TelemetrySchemaVersion);
        Assert.Single(telemetry.Evaluation.PastStateQueries);
        Assert.NotEmpty(telemetry.Evaluation.PastStateCandidates);
        Assert.NotEmpty(telemetry.Evaluation.CandidateVerifications);
        Assert.Equal(3, telemetry.Evaluation.PastStateMethods.Count);
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
