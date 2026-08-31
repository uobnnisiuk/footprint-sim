using FootprintSim.Evidence;
using FootprintSim.Hazards;
using FootprintSim.Interactions;
using FootprintSim.Interactions.CandidateWarps;
using FootprintSim.Interactions.Tracing;

namespace FootprintSim.Inference.PastStates;

public enum PastStateCandidateDisposition
{
    Supported,
    Uncertain,
    Conflicting,
    Unsupported,
    Unavailable
}

public enum CandidateVerificationDisposition
{
    IndependentlySupported,
    IndependentlyRefuted,
    Conflicting,
    Unresolved,
    SharedAncestryOnly,
    Unavailable
}

public enum PastStateMethod
{
    BackwardTraversal,
    ConstraintBasedReconstruction,
    ReconstructionWithLocalTraversal
}

public sealed record PastStateQuery
{
    private PastStateQuery(
        string queryId,
        string scenarioId,
        int seed,
        string targetEntityId,
        InteractionTimeWindow targetTimeWindow,
        string queryContextId,
        long evidenceAvailabilityCutoff)
    {
        QueryId = queryId;
        ScenarioId = scenarioId;
        Seed = seed;
        TargetEntityId = targetEntityId;
        TargetTimeWindow = targetTimeWindow;
        QueryContextId = queryContextId;
        EvidenceAvailabilityCutoff = evidenceAvailabilityCutoff;
    }

    public string QueryId { get; }

    public string ScenarioId { get; }

    public int Seed { get; }

    public string TargetEntityId { get; }

    public InteractionTimeWindow TargetTimeWindow { get; }

    public string QueryContextId { get; }

    public long EvidenceAvailabilityCutoff { get; }

    public static PastStateQuery Create(
        string scenarioId,
        int seed,
        string targetEntityId,
        InteractionTimeWindow targetTimeWindow,
        string queryContextId,
        long evidenceAvailabilityCutoff)
    {
        var normalizedScenario = InteractionNormalization.RequireIdentifier(scenarioId, nameof(scenarioId));
        var normalizedTarget = InteractionNormalization.RequireIdentifier(targetEntityId, nameof(targetEntityId));
        var normalizedContext = InteractionNormalization.RequireIdentifier(queryContextId, nameof(queryContextId));
        ArgumentNullException.ThrowIfNull(targetTimeWindow);
        if (evidenceAvailabilityCutoff < targetTimeWindow.EndTick)
        {
            throw new ArgumentOutOfRangeException(
                nameof(evidenceAvailabilityCutoff),
                "The evidence cutoff cannot precede the target window.");
        }

        var queryId = InteractionStableIdentifier.Create(
            "psq",
            normalizedScenario,
            InteractionStableIdentifier.Seed(seed),
            normalizedTarget,
            InteractionStableIdentifier.Tick(targetTimeWindow.StartTick),
            InteractionStableIdentifier.Tick(targetTimeWindow.EndTick),
            normalizedContext,
            InteractionStableIdentifier.Tick(evidenceAvailabilityCutoff));
        return new PastStateQuery(
            queryId,
            normalizedScenario,
            seed,
            normalizedTarget,
            targetTimeWindow,
            normalizedContext,
            evidenceAvailabilityCutoff);
    }
}

public sealed record PublicEvidenceSet
{
    public required string EvidenceSetId { get; init; }

    public required string QueryId { get; init; }

    public IReadOnlyList<string> ObservationIds { get; init; } = [];

    public IReadOnlyList<CausalTracingConstraint> Constraints { get; init; } = [];

    public IReadOnlyList<TracingSourceGroup> SourceGroups { get; init; } = [];

    public IReadOnlyList<string> PublicRelationReferences { get; init; } = [];

    public required int ExcludedFutureEvidenceCount { get; init; }
}

public sealed record PastStateCandidate
{
    public required string CandidateId { get; init; }

    public required string QueryId { get; init; }

    public required string TargetEntityId { get; init; }

    public required InteractionTimeWindow TargetTimeWindow { get; init; }

    public required string RegionId { get; init; }

    public IReadOnlyList<string> GenerationEvidenceIds { get; init; } = [];

    public IReadOnlyList<string> GenerationSourceGroupIds { get; init; } = [];

    public IReadOnlyList<string> SourceAncestryIds { get; init; } = [];

    public IReadOnlyList<string> AppliedConstraintIds { get; init; } = [];

    public IReadOnlyList<string> UnresolvedConstraintIds { get; init; } = [];

    public IReadOnlyList<string> ConflictingConstraintIds { get; init; } = [];

    public IReadOnlyList<string> PublicRelationReferences { get; init; } = [];

    public required PastStateCandidateDisposition Disposition { get; init; }
}

public static class PastStateCandidateIdentity
{
    public static string Create(
        PastStateQuery query,
        string regionId,
        IEnumerable<string> generationEvidenceIds,
        IEnumerable<string> generationSourceGroupIds,
        IEnumerable<string> generationConstraintIds)
    {
        ArgumentNullException.ThrowIfNull(query);
        var normalizedRegion = InteractionNormalization.RequireIdentifier(regionId, nameof(regionId));
        return InteractionStableIdentifier.Create(
            "psc",
            query.QueryId,
            query.TargetEntityId,
            InteractionStableIdentifier.Tick(query.TargetTimeWindow.StartTick),
            InteractionStableIdentifier.Tick(query.TargetTimeWindow.EndTick),
            normalizedRegion,
            Joined(generationEvidenceIds),
            Joined(generationSourceGroupIds),
            Joined(generationConstraintIds));
    }

    private static string Joined(IEnumerable<string> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        var joined = string.Join(
            "|",
            values
                .Select(item => InteractionNormalization.RequireIdentifier(item, nameof(values)))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(item => item, StringComparer.Ordinal));
        return joined.Length == 0 ? "none" : joined;
    }
}

public sealed record CandidateVerification
{
    public required string CandidateId { get; init; }

    public IReadOnlyList<string> VerificationEvidenceIds { get; init; } = [];

    public IReadOnlyList<string> VerificationSourceGroupIds { get; init; } = [];

    public IReadOnlyList<string> SharedAncestrySourceGroupIds { get; init; } = [];

    public IReadOnlyList<string> SupportingConstraintIds { get; init; } = [];

    public IReadOnlyList<string> RefutingConstraintIds { get; init; } = [];

    public IReadOnlyList<string> ConflictingConstraintIds { get; init; } = [];

    public IReadOnlyList<string> GenerationVerificationAncestryOverlap { get; init; } = [];

    public required CandidateVerificationDisposition Disposition { get; init; }
}

public sealed record PastStateReconstructionResult
{
    public required string ReconstructionId { get; init; }

    public required PastStateQuery Query { get; init; }

    public required PublicEvidenceSet EvidenceSet { get; init; }

    public required int InitialCandidateCount { get; init; }

    public IReadOnlyList<PastStateCandidate> Candidates { get; init; } = [];

    public IReadOnlyList<CandidateVerification> Verification { get; init; } = [];

    public required int ReconstructionSteps { get; init; }

    public required int EvidenceRecordsInspected { get; init; }

    public required int SourceGroupsInspected { get; init; }

    public required int PublicRelationsInspected { get; init; }
}

public sealed record PastStateMethodResult
{
    public required PastStateMethod Method { get; init; }

    public IReadOnlyList<string> CandidateRegionIds { get; init; } = [];

    public required int Steps { get; init; }

    public required int PublicRelationsInspected { get; init; }

    public required int EvidenceRecordsInspected { get; init; }

    public required int SourceGroupsInspected { get; init; }

    public string? TracingSessionId { get; init; }
}

public sealed record PastStateComparisonResult
{
    public required PastStateQuery Query { get; init; }

    public required PastStateReconstructionResult Reconstruction { get; init; }

    public IReadOnlyList<PastStateMethodResult> Methods { get; init; } = [];
}

public sealed record PastStatePublicContext
{
    public required PastStateQuery Query { get; init; }

    public IReadOnlyList<PublicCandidateWarpStateDefinition> PublicCandidateStates { get; init; } = [];

    public IReadOnlyList<string> GenerationDirectSourceIds { get; init; } = [];

    public IReadOnlyList<string> VerificationDirectSourceIds { get; init; } = [];

    public required int LocalTraversalWindowPaddingTicks { get; init; }

    public PublicHazardEnvelope? PublicHazardEnvelope { get; init; }
}
