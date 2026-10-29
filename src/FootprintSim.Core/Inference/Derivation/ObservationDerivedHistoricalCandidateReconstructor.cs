using FootprintSim.Interactions;
using FootprintSim.Interactions.CandidateWarps;

namespace FootprintSim.Inference.Derivation;

public sealed record ObservationDerivedHistoricalCandidate
{
    public required string CandidateId { get; init; }

    public required string StateId { get; init; }

    public required InteractionTimeWindow TimeWindow { get; init; }

    public required string RegionId { get; init; }
}

public sealed record ObservationDerivedHistoricalCandidateResult
{
    public required int InitialCandidateCount { get; init; }

    public IReadOnlyList<ObservationDerivedHistoricalCandidate> Candidates { get; init; } = [];

    public required bool ConstraintApplied { get; init; }

    public required int CandidateGenerationWork { get; init; }

    public required int EvidenceRecordsInspected { get; init; }

    public required int SourceGroupsInspected { get; init; }
}

/// <summary>
/// Selects public historical candidate states using an observation-derived generation constraint.
/// This fixture-sized adapter performs no verification, traversal, or truth evaluation.
/// </summary>
public sealed class ObservationDerivedHistoricalCandidateReconstructor
{
    public ObservationDerivedHistoricalCandidateResult Reconstruct(
        string scenarioId,
        int seed,
        string targetEntityId,
        IEnumerable<PublicCandidateWarpStateDefinition> publicCandidateStates,
        ObservationDerivedConstraint? constraint)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(scenarioId);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetEntityId);
        ArgumentNullException.ThrowIfNull(publicCandidateStates);
        if (constraint is not null)
        {
            if (!string.Equals(constraint.ScenarioId, scenarioId, StringComparison.Ordinal) ||
                constraint.Seed != seed ||
                !string.Equals(constraint.TargetEntityId, targetEntityId, StringComparison.Ordinal))
            {
                throw new ArgumentException("Constraint and reconstruction identifiers do not match.");
            }

            if (constraint.UsageRole != PastStates.PastStateEvidenceUsageRole.CandidateGeneration)
            {
                throw new ArgumentException(
                    "Observation-derived reconstruction accepts CandidateGeneration constraints only.",
                    nameof(constraint));
            }
        }

        var candidateWarps = PublicCandidateWarpFactory.Create(
            scenarioId,
            seed,
            targetEntityId,
            publicCandidateStates);
        var initial = candidateWarps.Warps
            .SelectMany(item => item.States)
            .SelectMany(state => state.RegionIds.Select(regionId => CreateCandidate(state, regionId)))
            .DistinctBy(item => item.CandidateId, StringComparer.Ordinal)
            .OrderBy(item => item.TimeWindow.StartTick)
            .ThenBy(item => item.TimeWindow.EndTick)
            .ThenBy(item => item.RegionId, StringComparer.Ordinal)
            .ThenBy(item => item.CandidateId, StringComparer.Ordinal)
            .ToArray();
        var candidates = constraint is null
            ? initial
            : initial.Where(item =>
                    Overlaps(item.TimeWindow, constraint.ConstrainedTimeWindow) &&
                    constraint.AllowedRegionIds.Contains(item.RegionId, StringComparer.Ordinal))
                .ToArray();

        return new ObservationDerivedHistoricalCandidateResult
        {
            InitialCandidateCount = initial.Length,
            Candidates = candidates,
            ConstraintApplied = constraint is not null,
            CandidateGenerationWork = initial.Length + (constraint is null ? 0 : 1),
            EvidenceRecordsInspected = constraint is null ? 0 : 1,
            SourceGroupsInspected = constraint is null ? 0 : 1
        };

        ObservationDerivedHistoricalCandidate CreateCandidate(
            CandidateWarpState state,
            string regionId) => new()
            {
                CandidateId = InteractionStableIdentifier.Create(
                "ohc",
                scenarioId,
                InteractionStableIdentifier.Seed(seed),
                targetEntityId,
                state.StateId,
                regionId),
                StateId = state.StateId,
                TimeWindow = state.TimeWindow,
                RegionId = regionId
            };
    }

    private static bool Overlaps(InteractionTimeWindow left, InteractionTimeWindow right) =>
        left.StartTick <= right.EndTick && right.StartTick <= left.EndTick;
}
