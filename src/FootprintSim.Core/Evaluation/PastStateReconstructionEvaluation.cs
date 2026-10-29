using FootprintSim.Evaluation.Truth;
using FootprintSim.Inference.PastStates;
using FootprintSim.Scenarios;

namespace FootprintSim.Evaluation;

public sealed record PastStateMethodEvaluation
{
    public required PastStateMethod Method { get; init; }

    public required int CandidateCount { get; init; }

    public required bool? ContainsTruth { get; init; }

    public required bool? FalseExclusion { get; init; }

    public required decimal CandidateReduction { get; init; }

    public required int Steps { get; init; }

    public required int CandidateGenerationSteps { get; init; }

    public required int CandidateVerificationSteps { get; init; }

    public required int ForwardTransitionSteps { get; init; }

    public required int BackwardTransitionSteps { get; init; }

    public required int PublicRelationsInspected { get; init; }

    public required int EvidenceRecordsInspected { get; init; }

    public required int SourceGroupsInspected { get; init; }
}

public sealed record PastStateRunEvaluation
{
    public required string ScenarioId { get; init; }

    public required int Seed { get; init; }

    public required PastStateEvidenceCondition Condition { get; init; }

    public required string QueryId { get; init; }

    public required string TargetEntityId { get; init; }

    public required long TargetStartTick { get; init; }

    public required long TargetEndTick { get; init; }

    public required long EvidenceAvailabilityCutoff { get; init; }

    public string? TruthPersonRegionId { get; init; }

    public string? TruthDeviceRegionId { get; init; }

    public required string TruthOutcomeIdentifier { get; init; }

    public required int InitialCandidateCount { get; init; }

    public IReadOnlyList<PastStateMethodEvaluation> Methods { get; init; } = [];

    public required int AddressableCandidateCount { get; init; }

    public required decimal DeterministicCandidateIdentityRate { get; init; }

    public required bool? TargetCandidateRepresented { get; init; }

    public required int SupportedCandidateCount { get; init; }

    public required int UncertainCandidateCount { get; init; }

    public required int UnresolvedCandidateCount { get; init; }

    public required int ConflictedCandidateCount { get; init; }

    public required int UnsupportedCandidateCount { get; init; }

    public required int UnavailableCandidateCount { get; init; }

    public required int IndependentlySupportedCandidateCount { get; init; }

    public required int IndependentlyRefutedCandidateCount { get; init; }

    public required int ConflictingVerificationCandidateCount { get; init; }

    public required int UnresolvedVerificationCandidateCount { get; init; }

    public required int SharedAncestryOnlyCandidateCount { get; init; }

    public required int UnavailableVerificationCandidateCount { get; init; }

    public required int GenerationSourceGroupCount { get; init; }

    public required int VerificationSourceGroupCount { get; init; }

    public required int SharedAncestrySourceGroupCount { get; init; }

    public required int GenerationVerificationAncestryOverlapCount { get; init; }

    public required int PresentEvidenceSourceGroupCount { get; init; }

    public required int RedundantSameSourceRecordCount { get; init; }

    public required int ConflictingEvidenceCount { get; init; }

    public required int UncertainEvidenceCount { get; init; }

    public required int UnavailableEvidenceCount { get; init; }

    public required int ExcludedFutureEvidenceCount { get; init; }

    public required PastStateComparisonResult PublicComparison { get; init; }
}

public sealed record PastStateOutcomeCount
{
    public required string Outcome { get; init; }

    public required int Count { get; init; }
}

public sealed record PastStateMethodAggregate
{
    public required PastStateMethod Method { get; init; }

    public required int RunCount { get; init; }

    public required int ContainmentAvailableCount { get; init; }

    public required decimal ContainmentRate { get; init; }

    public required int FalseExclusionCount { get; init; }

    public required decimal MeanCandidateCount { get; init; }

    public required decimal MeanCandidateReduction { get; init; }

    public required decimal MeanSteps { get; init; }

    public required decimal MeanCandidateGenerationSteps { get; init; }

    public required decimal MeanCandidateVerificationSteps { get; init; }

    public required decimal MeanForwardTransitionSteps { get; init; }

    public required decimal MeanBackwardTransitionSteps { get; init; }

    public required decimal MeanPublicRelationsInspected { get; init; }

    public required decimal MeanEvidenceRecordsInspected { get; init; }
}

public sealed record PastStateReconstructionEvaluationReport
{
    public int SchemaVersion { get; init; } = 2;

    public required int SeedCount { get; init; }

    public required int MethodRunCount { get; init; }

    public IReadOnlyList<PastStateRunEvaluation> Runs { get; init; } = [];

    public IReadOnlyList<PastStateMethodAggregate> Methods { get; init; } = [];

    public IReadOnlyList<PastStateOutcomeCount> TruthHistoricalStateDistribution { get; init; } = [];

    public IReadOnlyList<PastStateOutcomeCount> TruthPersonDeviceOutcomeDistribution { get; init; } = [];
}

public static class PastStateReconstructionEvaluationBuilder
{
    public static PastStateRunEvaluation Evaluate(
        PastStateComparisonResult comparison,
        PastStateTruthEvaluation truth,
        PastStateEvidenceCondition condition,
        int unavailableEvidenceCount)
    {
        ArgumentNullException.ThrowIfNull(comparison);
        ArgumentNullException.ThrowIfNull(truth);
        var reconstruction = comparison.Reconstruction;
        var candidates = reconstruction.Candidates;
        var verification = reconstruction.Verification;
        var methods = comparison.Methods.Select(method => new PastStateMethodEvaluation
        {
            Method = method.Method,
            CandidateCount = method.CandidateRegionIds.Count,
            ContainsTruth = truth.PersonRegionId is null
                ? null
                : method.CandidateRegionIds.Contains(truth.PersonRegionId, StringComparer.Ordinal),
            FalseExclusion = truth.PersonRegionId is null
                ? null
                : !method.CandidateRegionIds.Contains(truth.PersonRegionId, StringComparer.Ordinal),
            CandidateReduction = reconstruction.InitialCandidateCount == 0
                ? 0m
                : 1m - ((decimal)method.CandidateRegionIds.Count / reconstruction.InitialCandidateCount),
            Steps = method.Steps,
            CandidateGenerationSteps = method.CandidateGenerationSteps,
            CandidateVerificationSteps = method.CandidateVerificationSteps,
            ForwardTransitionSteps = method.ForwardTransitionSteps,
            BackwardTransitionSteps = method.BackwardTransitionSteps,
            PublicRelationsInspected = method.PublicRelationsInspected,
            EvidenceRecordsInspected = method.EvidenceRecordsInspected,
            SourceGroupsInspected = method.SourceGroupsInspected
        }).OrderBy(item => item.Method).ToArray();
        var deterministicCandidateIds = candidates.Count(item => string.Equals(
            item.CandidateId,
            PastStateCandidateIdentity.Create(
                comparison.Query,
                item.RegionId,
                item.GenerationEvidenceIds,
                item.GenerationSourceGroupIds,
                item.AppliedConstraintIds
                    .Concat(item.UnresolvedConstraintIds)
                    .Concat(item.ConflictingConstraintIds)),
            StringComparison.Ordinal));

        return new PastStateRunEvaluation
        {
            ScenarioId = comparison.Query.ScenarioId,
            Seed = comparison.Query.Seed,
            Condition = condition,
            QueryId = comparison.Query.QueryId,
            TargetEntityId = comparison.Query.TargetEntityId,
            TargetStartTick = comparison.Query.TargetTimeWindow.StartTick,
            TargetEndTick = comparison.Query.TargetTimeWindow.EndTick,
            EvidenceAvailabilityCutoff = comparison.Query.EvidenceAvailabilityCutoff,
            TruthPersonRegionId = truth.PersonRegionId,
            TruthDeviceRegionId = truth.DeviceRegionId,
            TruthOutcomeIdentifier = truth.OutcomeIdentifier,
            InitialCandidateCount = reconstruction.InitialCandidateCount,
            Methods = methods,
            AddressableCandidateCount = candidates
                .Select(item => item.CandidateId)
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .Distinct(StringComparer.Ordinal)
                .Count(),
            DeterministicCandidateIdentityRate = candidates.Count == 0
                ? 0m
                : (decimal)deterministicCandidateIds / candidates.Count,
            TargetCandidateRepresented = truth.PersonRegionId is null
                ? null
                : candidates.Any(item => string.Equals(item.RegionId, truth.PersonRegionId, StringComparison.Ordinal)),
            SupportedCandidateCount = candidates.Count(item => item.Disposition == PastStateCandidateDisposition.Supported),
            UncertainCandidateCount = candidates.Count(item => item.Disposition == PastStateCandidateDisposition.Uncertain),
            UnresolvedCandidateCount = candidates.Count(item => item.UnresolvedConstraintIds.Count > 0),
            ConflictedCandidateCount = candidates.Count(item => item.Disposition == PastStateCandidateDisposition.Conflicting),
            UnsupportedCandidateCount = candidates.Count(item => item.Disposition == PastStateCandidateDisposition.Unsupported),
            UnavailableCandidateCount = candidates.Count(item => item.Disposition == PastStateCandidateDisposition.Unavailable),
            IndependentlySupportedCandidateCount = verification.Count(item =>
                item.Disposition == CandidateVerificationDisposition.IndependentlySupported),
            IndependentlyRefutedCandidateCount = verification.Count(item =>
                item.Disposition == CandidateVerificationDisposition.IndependentlyRefuted),
            ConflictingVerificationCandidateCount = verification.Count(item =>
                item.Disposition == CandidateVerificationDisposition.Conflicting),
            UnresolvedVerificationCandidateCount = verification.Count(item =>
                item.Disposition == CandidateVerificationDisposition.Unresolved),
            SharedAncestryOnlyCandidateCount = verification.Count(item =>
                item.Disposition == CandidateVerificationDisposition.SharedAncestryOnly),
            UnavailableVerificationCandidateCount = verification.Count(item =>
                item.Disposition == CandidateVerificationDisposition.Unavailable),
            GenerationSourceGroupCount = candidates
                .SelectMany(item => item.GenerationSourceGroupIds)
                .Distinct(StringComparer.Ordinal)
                .Count(),
            VerificationSourceGroupCount = verification
                .SelectMany(item => item.VerificationSourceGroupIds)
                .Distinct(StringComparer.Ordinal)
                .Count(),
            SharedAncestrySourceGroupCount = verification
                .SelectMany(item => item.SharedAncestrySourceGroupIds)
                .Distinct(StringComparer.Ordinal)
                .Count(),
            GenerationVerificationAncestryOverlapCount = verification
                .SelectMany(item => item.GenerationVerificationAncestryOverlap)
                .Distinct(StringComparer.Ordinal)
                .Count(),
            PresentEvidenceSourceGroupCount = reconstruction.EvidenceSet.SourceGroups.Count,
            RedundantSameSourceRecordCount = Math.Max(
                0,
                reconstruction.EvidenceSet.Constraints.Count - reconstruction.EvidenceSet.SourceGroups.Count),
            ConflictingEvidenceCount = reconstruction.EvidenceSet.Constraints.Count(item =>
                item.EvidenceBoundaryState == Evidence.EvidenceState.Conflicting),
            UncertainEvidenceCount = reconstruction.EvidenceSet.Constraints.Count(item =>
                item.EvidenceBoundaryState == Evidence.EvidenceState.SupportedButUncertain),
            UnavailableEvidenceCount = unavailableEvidenceCount,
            ExcludedFutureEvidenceCount = reconstruction.EvidenceSet.ExcludedFutureEvidenceCount,
            PublicComparison = comparison
        };
    }

    public static PastStateReconstructionEvaluationReport? Build(
        IEnumerable<PastStateRunEvaluation> inputs)
    {
        ArgumentNullException.ThrowIfNull(inputs);
        var runs = inputs
            .OrderBy(item => item.ScenarioId, StringComparer.Ordinal)
            .ThenBy(item => item.Seed)
            .ToArray();
        if (runs.Length == 0)
        {
            return null;
        }

        var methodRows = runs.SelectMany(item => item.Methods).ToArray();
        var methodAggregates = methodRows
            .GroupBy(item => item.Method)
            .OrderBy(item => item.Key)
            .Select(group =>
            {
                var available = group.Where(item => item.ContainsTruth.HasValue).ToArray();
                return new PastStateMethodAggregate
                {
                    Method = group.Key,
                    RunCount = group.Count(),
                    ContainmentAvailableCount = available.Length,
                    ContainmentRate = available.Length == 0
                        ? 0m
                        : (decimal)available.Count(item => item.ContainsTruth == true) / available.Length,
                    FalseExclusionCount = available.Count(item => item.FalseExclusion == true),
                    MeanCandidateCount = Mean(group.Select(item => item.CandidateCount)),
                    MeanCandidateReduction = Mean(group.Select(item => item.CandidateReduction)),
                    MeanSteps = Mean(group.Select(item => item.Steps)),
                    MeanCandidateGenerationSteps = Mean(group.Select(item => item.CandidateGenerationSteps)),
                    MeanCandidateVerificationSteps = Mean(group.Select(item => item.CandidateVerificationSteps)),
                    MeanForwardTransitionSteps = Mean(group.Select(item => item.ForwardTransitionSteps)),
                    MeanBackwardTransitionSteps = Mean(group.Select(item => item.BackwardTransitionSteps)),
                    MeanPublicRelationsInspected = Mean(group.Select(item => item.PublicRelationsInspected)),
                    MeanEvidenceRecordsInspected = Mean(group.Select(item => item.EvidenceRecordsInspected))
                };
            }).ToArray();
        var historical = runs
            .GroupBy(item => item.TruthPersonRegionId ?? "unavailable", StringComparer.Ordinal)
            .OrderBy(item => item.Key, StringComparer.Ordinal)
            .Select(group => new PastStateOutcomeCount { Outcome = group.Key, Count = group.Count() })
            .ToArray();
        var personDevice = runs
            .GroupBy(item => item.TruthOutcomeIdentifier, StringComparer.Ordinal)
            .OrderBy(item => item.Key, StringComparer.Ordinal)
            .Select(group => new PastStateOutcomeCount { Outcome = group.Key, Count = group.Count() })
            .ToArray();

        return new PastStateReconstructionEvaluationReport
        {
            SeedCount = runs.Select(item => item.Seed).Distinct().Count(),
            MethodRunCount = methodRows.Length,
            Runs = runs,
            Methods = methodAggregates,
            TruthHistoricalStateDistribution = historical,
            TruthPersonDeviceOutcomeDistribution = personDevice
        };
    }

    private static decimal Mean(IEnumerable<int> values)
    {
        var array = values.ToArray();
        return array.Length == 0 ? 0m : (decimal)array.Sum() / array.Length;
    }

    private static decimal Mean(IEnumerable<decimal> values)
    {
        var array = values.ToArray();
        return array.Length == 0 ? 0m : array.Sum() / array.Length;
    }
}
