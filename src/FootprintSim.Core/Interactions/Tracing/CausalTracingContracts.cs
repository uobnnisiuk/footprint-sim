using FootprintSim.Evidence;
using FootprintSim.Interactions.CandidateWarps;

namespace FootprintSim.Interactions.Tracing;

public enum CausalTracingDirection
{
    CrossThread,
    Forward,
    Backward
}

public enum TracingConstraintRole
{
    Upstream,
    Local,
    Downstream
}

public enum TracingConstraintSourceKind
{
    CrossThreadObservation,
    PublicEnvironmentalObservation,
    DeviceOnlyObservation
}

public enum TracingStepDisposition
{
    Applied,
    Branch,
    MarkConflict,
    NoOp
}

public enum TracingStepReason
{
    Applied,
    EvidenceStateDoesNotConstrain,
    NoTemporalOverlap,
    NoRegionOverlap,
    DeviceOnlyEvidenceCannotConstrainPerson,
    PublicTransitionConsistency
}

public sealed record CausalTracingConstraint
{
    private CausalTracingConstraint(
        string constraintId,
        string scenarioId,
        int seed,
        string sourceObservationId,
        string directSourceId,
        IReadOnlyList<string> sourceAncestryIds,
        string targetEntityId,
        TracingConstraintSourceKind sourceKind,
        string relationKind,
        IReadOnlyList<InteractionParticipant> participants,
        TracingConstraintRole role,
        InteractionTimeWindow sourceObservedTimeWindow,
        InteractionTimeWindow constrainedTimeWindow,
        IReadOnlyList<string> allowedRegionIds,
        long availableAtTick,
        EvidenceState evidenceBoundaryState,
        bool isPersonEvidence,
        bool isDeviceOnlyEvidence,
        InteractionVisibility visibility,
        string derivationKind,
        IReadOnlyList<InteractionProvenance> provenance)
    {
        ConstraintId = constraintId;
        ScenarioId = scenarioId;
        Seed = seed;
        SourceObservationId = sourceObservationId;
        DirectSourceId = directSourceId;
        SourceAncestryIds = sourceAncestryIds;
        TargetEntityId = targetEntityId;
        SourceKind = sourceKind;
        RelationKind = relationKind;
        Participants = participants;
        Role = role;
        SourceObservedTimeWindow = sourceObservedTimeWindow;
        ConstrainedTimeWindow = constrainedTimeWindow;
        AllowedRegionIds = allowedRegionIds;
        AvailableAtTick = availableAtTick;
        EvidenceBoundaryState = evidenceBoundaryState;
        IsPersonEvidence = isPersonEvidence;
        IsDeviceOnlyEvidence = isDeviceOnlyEvidence;
        Visibility = visibility;
        DerivationKind = derivationKind;
        Provenance = provenance;
    }

    public string ConstraintId { get; }

    public string ScenarioId { get; }

    public int Seed { get; }

    public string SourceObservationId { get; }

    public string DirectSourceId { get; }

    public IReadOnlyList<string> SourceAncestryIds { get; }

    public string TargetEntityId { get; }

    public TracingConstraintSourceKind SourceKind { get; }

    public string RelationKind { get; }

    public IReadOnlyList<InteractionParticipant> Participants { get; }

    public TracingConstraintRole Role { get; }

    public InteractionTimeWindow SourceObservedTimeWindow { get; }

    public InteractionTimeWindow ConstrainedTimeWindow { get; }

    public IReadOnlyList<string> AllowedRegionIds { get; }

    public long AvailableAtTick { get; }

    public EvidenceState EvidenceBoundaryState { get; }

    public bool IsPersonEvidence { get; }

    public bool IsDeviceOnlyEvidence { get; }

    public InteractionVisibility Visibility { get; }

    public string DerivationKind { get; }

    public IReadOnlyList<InteractionProvenance> Provenance { get; }

    public static CausalTracingConstraint Create(
        string scenarioId,
        int seed,
        string sourceObservationId,
        string directSourceId,
        IEnumerable<string> sourceAncestryIds,
        string targetEntityId,
        TracingConstraintSourceKind sourceKind,
        string relationKind,
        IEnumerable<InteractionParticipant> participants,
        TracingConstraintRole role,
        InteractionTimeWindow sourceObservedTimeWindow,
        InteractionTimeWindow constrainedTimeWindow,
        IEnumerable<string> allowedRegionIds,
        long availableAtTick,
        EvidenceState evidenceBoundaryState,
        bool isPersonEvidence,
        bool isDeviceOnlyEvidence,
        InteractionVisibility visibility,
        string derivationKind,
        IEnumerable<InteractionProvenance> provenance)
    {
        var normalizedScenario = InteractionNormalization.RequireIdentifier(
            scenarioId,
            nameof(scenarioId));
        var normalizedObservation = InteractionNormalization.RequireIdentifier(
            sourceObservationId,
            nameof(sourceObservationId));
        var normalizedDirectSource = InteractionNormalization.RequireIdentifier(
            directSourceId,
            nameof(directSourceId));
        var normalizedTarget = InteractionNormalization.RequireIdentifier(
            targetEntityId,
            nameof(targetEntityId));
        var normalizedRelation = InteractionNormalization.RequireIdentifier(
            relationKind,
            nameof(relationKind));
        var normalizedDerivation = InteractionNormalization.RequireIdentifier(
            derivationKind,
            nameof(derivationKind));
        ArgumentNullException.ThrowIfNull(sourceObservedTimeWindow);
        ArgumentNullException.ThrowIfNull(constrainedTimeWindow);
        if (availableAtTick < sourceObservedTimeWindow.EndTick)
        {
            throw new ArgumentOutOfRangeException(
                nameof(availableAtTick),
                "Tracing evidence cannot become available before its observed window ends.");
        }

        if (isPersonEvidence && isDeviceOnlyEvidence)
        {
            throw new ArgumentException(
                "Tracing evidence cannot be both person and device-only evidence.");
        }

        if (visibility == InteractionVisibility.PrivateTruth)
        {
            throw new ArgumentException(
                "Tracing constraints cannot use private-truth visibility.",
                nameof(visibility));
        }

        var ancestry = sourceAncestryIds
            .Append(normalizedDirectSource)
            .Append(normalizedObservation)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        var normalizedParticipants = InteractionNormalization.Participants(participants);
        var regions = InteractionNormalization.RegionIds(allowedRegionIds, requireAtLeastOne: true);
        var normalizedProvenance = InteractionNormalization.Provenance(provenance);
        var constraintId = InteractionStableIdentifier.Create(
            "trc",
            normalizedScenario,
            InteractionStableIdentifier.Seed(seed),
            normalizedObservation,
            normalizedDirectSource,
            normalizedTarget,
            sourceKind.ToString(),
            normalizedRelation,
            role.ToString(),
            InteractionStableIdentifier.Tick(sourceObservedTimeWindow.StartTick),
            InteractionStableIdentifier.Tick(sourceObservedTimeWindow.EndTick),
            InteractionStableIdentifier.Tick(constrainedTimeWindow.StartTick),
            InteractionStableIdentifier.Tick(constrainedTimeWindow.EndTick),
            InteractionStableIdentifier.Tick(availableAtTick),
            evidenceBoundaryState.ToString(),
            isPersonEvidence.ToString(),
            isDeviceOnlyEvidence.ToString(),
            visibility.ToString(),
            normalizedDerivation,
            string.Join("|", ancestry),
            string.Join("|", regions),
            string.Join("|", normalizedParticipants.Select(ParticipantIdentity)),
            string.Join("|", normalizedProvenance.Select(ProvenanceIdentity)));

        return new CausalTracingConstraint(
            constraintId,
            normalizedScenario,
            seed,
            normalizedObservation,
            normalizedDirectSource,
            ancestry,
            normalizedTarget,
            sourceKind,
            normalizedRelation,
            normalizedParticipants,
            role,
            sourceObservedTimeWindow,
            constrainedTimeWindow,
            regions,
            availableAtTick,
            evidenceBoundaryState,
            isPersonEvidence,
            isDeviceOnlyEvidence,
            visibility,
            normalizedDerivation,
            normalizedProvenance);
    }

    internal string SemanticIdentity() => ConstraintId;

    private static string ParticipantIdentity(InteractionParticipant participant) =>
        $"{participant.EntityId}:{participant.EntityKind}:{participant.Role}";

    private static string ProvenanceIdentity(InteractionProvenance provenance) =>
        $"{provenance.Kind}:{provenance.SourceIdentifier}";
}

public sealed record CausalTracingConstraintSet
{
    public CausalTracingConstraintSet(
        string scenarioId,
        int seed,
        IEnumerable<CausalTracingConstraint> constraints)
    {
        ScenarioId = InteractionNormalization.RequireIdentifier(scenarioId, nameof(scenarioId));
        Seed = seed;
        ArgumentNullException.ThrowIfNull(constraints);
        Constraints = constraints
            .OrderBy(item => item.AvailableAtTick)
            .ThenBy(item => item.ConstrainedTimeWindow.StartTick)
            .ThenBy(item => item.ConstrainedTimeWindow.EndTick)
            .ThenBy(item => item.SourceObservationId, StringComparer.Ordinal)
            .ThenBy(item => item.ConstraintId, StringComparer.Ordinal)
            .ToArray();
        if (Constraints.Any(item =>
            !string.Equals(item.ScenarioId, ScenarioId, StringComparison.Ordinal) ||
            item.Seed != Seed))
        {
            throw new ArgumentException("Tracing constraint identifiers do not match their set.");
        }
    }

    public string ScenarioId { get; }

    public int Seed { get; }

    public IReadOnlyList<CausalTracingConstraint> Constraints { get; }

    public CausalTracingConstraintSet PubliclyAvailableAt(long tick) => new(
        ScenarioId,
        Seed,
        Constraints.Where(item =>
            item.Visibility == InteractionVisibility.PublicEvidence &&
            item.AvailableAtTick <= tick));
}

public sealed record TracingSourceGroup
{
    public required string SourceGroupId { get; init; }

    public IReadOnlyList<string> SourceObservationIds { get; init; } = [];

    public IReadOnlyList<string> SourceAncestryIds { get; init; } = [];

    public IReadOnlyList<string> ConstraintIds { get; init; } = [];
}

public sealed record CandidateSupportLineage
{
    public required string CandidateStateId { get; init; }

    public required string RegionId { get; init; }

    public IReadOnlyList<string> SupportingSourceGroupIds { get; init; } = [];

    public IReadOnlyList<string> CompetingSourceGroupIds { get; init; } = [];

    public IReadOnlyList<string> ConstraintIds { get; init; } = [];

    public IReadOnlyList<string> RelationKinds { get; init; } = [];

    public IReadOnlyList<InteractionTimeWindow> ConstrainedTimeWindows { get; init; } = [];

    public IReadOnlyList<EvidenceState> EvidenceStates { get; init; } = [];
}

public sealed record CausalTracingStepAudit
{
    public required string StepId { get; init; }

    public required string SessionId { get; init; }

    public required string QueryContextId { get; init; }

    public required int StepNumber { get; init; }

    public required CausalTracingDirection Direction { get; init; }

    public required TracingConstraintRole Role { get; init; }

    public required InteractionTimeWindow TimeWindow { get; init; }

    public required string TargetEntityId { get; init; }

    public string? ConstraintId { get; init; }

    public string? SourceObservationId { get; init; }

    public string? SourceGroupId { get; init; }

    public required string RelationKind { get; init; }

    public required EvidenceState EvidenceState { get; init; }

    public required TracingStepDisposition Disposition { get; init; }

    public required TracingStepReason Reason { get; init; }

    public IReadOnlyList<string> CandidateInputRegionIds { get; init; } = [];

    public IReadOnlyList<string> CandidateOutputRegionIds { get; init; } = [];

    public IReadOnlyList<string> AppliedConstraintIds { get; init; } = [];

    public IReadOnlyList<string> SkippedConstraintIds { get; init; } = [];

    public IReadOnlyList<string> SupportingSourceGroupIds { get; init; } = [];

    public IReadOnlyList<string> ConflictingSourceGroupIds { get; init; } = [];

    public required bool AlternativesRetained { get; init; }

    public required bool ConflictDetected { get; init; }

    public IReadOnlyList<string> SourceAncestryIds { get; init; } = [];

    public IReadOnlyList<InteractionProvenance> Provenance { get; init; } = [];
}

public sealed record CausalTracingResult
{
    public required string SessionId { get; init; }

    public required string QueryContextId { get; init; }

    public required CandidateWarpSet CandidateWarps { get; init; }

    public IReadOnlyList<TracingSourceGroup> SourceGroups { get; init; } = [];

    public IReadOnlyList<CandidateSupportLineage> SupportLineage { get; init; } = [];

    public IReadOnlyList<CausalTracingStepAudit> Steps { get; init; } = [];

    public required bool CausalSandwichApplied { get; init; }

    public required bool ConstraintOrderInvariant { get; init; }
}
