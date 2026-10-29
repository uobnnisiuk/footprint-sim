using FootprintSim.Evidence;
using FootprintSim.Inference.PastStates;
using FootprintSim.Interactions;

namespace FootprintSim.Inference.Derivation;

public enum OccurrenceTimeKnowledge
{
    KnownWindow,
    Unknown
}

public enum ObservationTimeWindowKind
{
    OccurrenceTimeUncertainty
}

public enum ObservationConstraintInputKind
{
    SourceObservation,
    OccurrenceTimeWindow,
    PublicDependency,
    DurationBounds,
    TargetEntity,
    EvidenceAvailability,
    EvidenceVisibility,
    EvidenceState,
    SourceAncestry,
    UsageRole
}

public enum DerivationApplicabilityCondition
{
    PublicEvidence,
    AvailableByCutoff,
    ConfirmedEvidenceBoundary,
    MatchingScenario,
    MatchingTarget,
    MatchingLaterEvent,
    KnownOccurrenceTime,
    CandidateGenerationOnly,
    NonNegativeDerivedWindow
}

public enum ObservationConstraintDerivationFailureReason
{
    None,
    UnknownOccurrenceTime,
    ObservationNotPublic,
    ObservationUnavailableAtCutoff,
    UnsupportedEvidenceState,
    ScenarioMismatch,
    TargetMismatch,
    LaterEventMismatch,
    UnsupportedUsageRole,
    DerivedWindowBeforeZero,
    ArithmeticOverflow
}

public enum IntervalUncertaintyPropagation
{
    ConservativeInverseDurationHull
}

public sealed record ObservedOccurrenceTime
{
    private ObservedOccurrenceTime(
        OccurrenceTimeKnowledge knowledge,
        ObservationTimeWindowKind? windowKind,
        InteractionTimeWindow? window)
    {
        Knowledge = knowledge;
        WindowKind = windowKind;
        Window = window;
    }

    public OccurrenceTimeKnowledge Knowledge { get; }

    public ObservationTimeWindowKind? WindowKind { get; }

    public InteractionTimeWindow? Window { get; }

    public static ObservedOccurrenceTime Known(InteractionTimeWindow window)
    {
        ArgumentNullException.ThrowIfNull(window);
        return new ObservedOccurrenceTime(
            OccurrenceTimeKnowledge.KnownWindow,
            ObservationTimeWindowKind.OccurrenceTimeUncertainty,
            window);
    }

    public static ObservedOccurrenceTime Unknown() => new(
        OccurrenceTimeKnowledge.Unknown,
        windowKind: null,
        window: null);

    internal string StableIdentity() => Knowledge == OccurrenceTimeKnowledge.Unknown
        ? "unknown-occurrence-time"
        : string.Join(
            ':',
            WindowKind,
            InteractionStableIdentifier.Tick(Window!.StartTick),
            InteractionStableIdentifier.Tick(Window.EndTick));
}

public sealed record PublicEventObservation
{
    private PublicEventObservation(
        string observationId,
        string scenarioId,
        int seed,
        string sourceEvidenceIdentifier,
        string eventKind,
        string targetEntityId,
        ObservedOccurrenceTime occurrenceTime,
        long availableAtTick,
        EvidenceState evidenceBoundaryState,
        InteractionVisibility visibility,
        IReadOnlyList<string> sourceAncestryIds,
        IReadOnlyList<InteractionProvenance> provenance)
    {
        ObservationId = observationId;
        ScenarioId = scenarioId;
        Seed = seed;
        SourceEvidenceIdentifier = sourceEvidenceIdentifier;
        EventKind = eventKind;
        TargetEntityId = targetEntityId;
        OccurrenceTime = occurrenceTime;
        AvailableAtTick = availableAtTick;
        EvidenceBoundaryState = evidenceBoundaryState;
        Visibility = visibility;
        SourceAncestryIds = sourceAncestryIds;
        Provenance = provenance;
    }

    public string ObservationId { get; }

    public string ScenarioId { get; }

    public int Seed { get; }

    public string SourceEvidenceIdentifier { get; }

    public string EventKind { get; }

    public string TargetEntityId { get; }

    public ObservedOccurrenceTime OccurrenceTime { get; }

    public long AvailableAtTick { get; }

    public EvidenceState EvidenceBoundaryState { get; }

    public InteractionVisibility Visibility { get; }

    public IReadOnlyList<string> SourceAncestryIds { get; }

    public IReadOnlyList<InteractionProvenance> Provenance { get; }

    public static PublicEventObservation Create(
        string scenarioId,
        int seed,
        string sourceEvidenceIdentifier,
        string eventKind,
        string targetEntityId,
        ObservedOccurrenceTime occurrenceTime,
        long availableAtTick,
        EvidenceState evidenceBoundaryState,
        InteractionVisibility visibility = InteractionVisibility.PublicEvidence,
        IEnumerable<string>? sourceAncestryIds = null,
        IEnumerable<InteractionProvenance>? additionalProvenance = null)
    {
        var normalizedScenario = InteractionNormalization.RequireIdentifier(
            scenarioId,
            nameof(scenarioId));
        var normalizedSource = InteractionNormalization.RequireIdentifier(
            sourceEvidenceIdentifier,
            nameof(sourceEvidenceIdentifier));
        var normalizedEvent = InteractionNormalization.RequireIdentifier(eventKind, nameof(eventKind));
        var normalizedTarget = InteractionNormalization.RequireIdentifier(
            targetEntityId,
            nameof(targetEntityId));
        ArgumentNullException.ThrowIfNull(occurrenceTime);
        if (availableAtTick < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(availableAtTick));
        }

        if (occurrenceTime.Window is not null && availableAtTick < occurrenceTime.Window.EndTick)
        {
            throw new ArgumentOutOfRangeException(
                nameof(availableAtTick),
                "Evidence cannot become available before the observed occurrence window ends.");
        }

        if (visibility == InteractionVisibility.PrivateTruth)
        {
            throw new ArgumentException(
                "A public-event observation cannot use private-truth visibility.",
                nameof(visibility));
        }

        var ancestry = (sourceAncestryIds ?? [])
            .Append(normalizedSource)
            .Select(item => InteractionNormalization.RequireIdentifier(item, nameof(sourceAncestryIds)))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(item => item, StringComparer.Ordinal)
            .ToArray();
        var provenance = InteractionNormalization.Provenance(
            new[]
            {
                new InteractionProvenance(
                    InteractionProvenanceKind.EvidenceRecord,
                    normalizedSource)
            }.Concat(additionalProvenance ?? []));
        var observationId = InteractionStableIdentifier.Create(
            "peo",
            normalizedScenario,
            InteractionStableIdentifier.Seed(seed),
            normalizedSource,
            normalizedEvent,
            normalizedTarget,
            occurrenceTime.StableIdentity(),
            InteractionStableIdentifier.Tick(availableAtTick),
            evidenceBoundaryState.ToString(),
            visibility.ToString(),
            string.Join("|", ancestry),
            string.Join("|", provenance.Select(ProvenanceIdentity)));

        return new PublicEventObservation(
            observationId,
            normalizedScenario,
            seed,
            normalizedSource,
            normalizedEvent,
            normalizedTarget,
            occurrenceTime,
            availableAtTick,
            evidenceBoundaryState,
            visibility,
            ancestry,
            provenance);
    }

    private static string ProvenanceIdentity(InteractionProvenance provenance) =>
        $"{provenance.Kind}:{provenance.SourceIdentifier}";
}

public sealed record PublicDurationBound
{
    public PublicDurationBound(long minimumTicks, long maximumTicks)
    {
        if (minimumTicks < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(minimumTicks));
        }

        if (maximumTicks < minimumTicks)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumTicks));
        }

        MinimumTicks = minimumTicks;
        MaximumTicks = maximumTicks;
    }

    public long MinimumTicks { get; }

    public long MaximumTicks { get; }
}

public sealed record PublicBoundedDurationDependency
{
    private PublicBoundedDurationDependency(
        string publicInputId,
        string scenarioId,
        string dependencyId,
        string predecessorStateKind,
        string laterEventKind,
        string targetEntityId,
        PublicDurationBound duration,
        string durationSourceId,
        IReadOnlyList<string> allowedPredecessorRegionIds,
        IReadOnlyList<InteractionProvenance> provenance)
    {
        PublicInputId = publicInputId;
        ScenarioId = scenarioId;
        DependencyId = dependencyId;
        PredecessorStateKind = predecessorStateKind;
        LaterEventKind = laterEventKind;
        TargetEntityId = targetEntityId;
        Duration = duration;
        DurationSourceId = durationSourceId;
        AllowedPredecessorRegionIds = allowedPredecessorRegionIds;
        Provenance = provenance;
    }

    public string PublicInputId { get; }

    public string ScenarioId { get; }

    public string DependencyId { get; }

    public string PredecessorStateKind { get; }

    public string LaterEventKind { get; }

    public string TargetEntityId { get; }

    public PublicDurationBound Duration { get; }

    public string DurationSourceId { get; }

    public IReadOnlyList<string> AllowedPredecessorRegionIds { get; }

    public IReadOnlyList<InteractionProvenance> Provenance { get; }

    public static PublicBoundedDurationDependency Create(
        string scenarioId,
        string dependencyId,
        string predecessorStateKind,
        string laterEventKind,
        string targetEntityId,
        PublicDurationBound duration,
        string durationSourceId,
        IEnumerable<string> allowedPredecessorRegionIds)
    {
        var normalizedScenario = InteractionNormalization.RequireIdentifier(
            scenarioId,
            nameof(scenarioId));
        var normalizedDependency = InteractionNormalization.RequireIdentifier(
            dependencyId,
            nameof(dependencyId));
        var normalizedPredecessor = InteractionNormalization.RequireIdentifier(
            predecessorStateKind,
            nameof(predecessorStateKind));
        var normalizedLater = InteractionNormalization.RequireIdentifier(
            laterEventKind,
            nameof(laterEventKind));
        var normalizedTarget = InteractionNormalization.RequireIdentifier(
            targetEntityId,
            nameof(targetEntityId));
        ArgumentNullException.ThrowIfNull(duration);
        var normalizedDurationSource = InteractionNormalization.RequireIdentifier(
            durationSourceId,
            nameof(durationSourceId));
        var regions = InteractionNormalization.RegionIds(
            allowedPredecessorRegionIds,
            requireAtLeastOne: true);
        var publicInputId = InteractionStableIdentifier.Create(
            "pbd",
            normalizedScenario,
            normalizedDependency,
            normalizedPredecessor,
            normalizedLater,
            normalizedTarget,
            InteractionStableIdentifier.Tick(duration.MinimumTicks),
            InteractionStableIdentifier.Tick(duration.MaximumTicks),
            normalizedDurationSource,
            string.Join("|", regions));
        var provenance = InteractionNormalization.Provenance(
        [
            new InteractionProvenance(
                InteractionProvenanceKind.ExternalSource,
                normalizedDependency),
            new InteractionProvenance(
                InteractionProvenanceKind.ExternalSource,
                normalizedDurationSource)
        ]);

        return new PublicBoundedDurationDependency(
            publicInputId,
            normalizedScenario,
            normalizedDependency,
            normalizedPredecessor,
            normalizedLater,
            normalizedTarget,
            duration,
            normalizedDurationSource,
            regions,
            provenance);
    }
}

public sealed record ObservationDerivedConstraint
{
    internal ObservationDerivedConstraint(
        string constraintId,
        string scenarioId,
        int seed,
        string sourceObservationId,
        string sourceEvidenceIdentifier,
        string sourceGroupId,
        IReadOnlyList<string> sourceAncestryIds,
        string targetEntityId,
        InteractionTimeWindow sourceObservedOccurrenceWindow,
        InteractionTimeWindow constrainedTimeWindow,
        IReadOnlyList<string> allowedRegionIds,
        long availableAtTick,
        EvidenceState evidenceBoundaryState,
        InteractionVisibility visibility,
        string derivationRuleId,
        IReadOnlyList<string> publicInputIds,
        PastStateEvidenceUsageRole usageRole,
        IReadOnlyList<InteractionProvenance> provenance)
    {
        ConstraintId = constraintId;
        ScenarioId = scenarioId;
        Seed = seed;
        SourceObservationId = sourceObservationId;
        SourceEvidenceIdentifier = sourceEvidenceIdentifier;
        SourceGroupId = sourceGroupId;
        SourceAncestryIds = sourceAncestryIds;
        TargetEntityId = targetEntityId;
        SourceObservedOccurrenceWindow = sourceObservedOccurrenceWindow;
        ConstrainedTimeWindow = constrainedTimeWindow;
        AllowedRegionIds = allowedRegionIds;
        AvailableAtTick = availableAtTick;
        EvidenceBoundaryState = evidenceBoundaryState;
        Visibility = visibility;
        DerivationRuleId = derivationRuleId;
        PublicInputIds = publicInputIds;
        UsageRole = usageRole;
        Provenance = provenance;
    }

    public string ConstraintId { get; }

    public string ScenarioId { get; }

    public int Seed { get; }

    public string SourceObservationId { get; }

    public string SourceEvidenceIdentifier { get; }

    public string SourceGroupId { get; }

    public IReadOnlyList<string> SourceAncestryIds { get; }

    public string TargetEntityId { get; }

    public InteractionTimeWindow SourceObservedOccurrenceWindow { get; }

    public InteractionTimeWindow ConstrainedTimeWindow { get; }

    public IReadOnlyList<string> AllowedRegionIds { get; }

    public long AvailableAtTick { get; }

    public EvidenceState EvidenceBoundaryState { get; }

    public InteractionVisibility Visibility { get; }

    public string DerivationRuleId { get; }

    public IReadOnlyList<string> PublicInputIds { get; }

    public PastStateEvidenceUsageRole UsageRole { get; }

    public IReadOnlyList<InteractionProvenance> Provenance { get; }
}

public sealed record ObservationConstraintDerivationAudit
{
    public required string RuleId { get; init; }

    public IReadOnlyList<ObservationConstraintInputKind> RequiredInputInformation { get; init; } = [];

    public IReadOnlyList<DerivationApplicabilityCondition> ApplicabilityConditions { get; init; } = [];

    public IReadOnlyList<string> PublicInputIdsUsed { get; init; } = [];

    public required string SourceObservationId { get; init; }

    public required string SourceEvidenceIdentifier { get; init; }

    public required string PublicDependencyId { get; init; }

    public required string PublicDependencyInputId { get; init; }

    public required string DurationSourceId { get; init; }

    public required PublicDurationBound DurationBounds { get; init; }

    public required OccurrenceTimeKnowledge OccurrenceTimeKnowledge { get; init; }

    public ObservationTimeWindowKind? TimeWindowKind { get; init; }

    public InteractionTimeWindow? InputTimeWindow { get; init; }

    public InteractionTimeWindow? OutputTimeWindow { get; init; }

    public required IntervalUncertaintyPropagation UncertaintyPropagation { get; init; }

    public required PastStateEvidenceUsageRole UsageRole { get; init; }

    public required string SourceGroupId { get; init; }

    public IReadOnlyList<string> SourceAncestryIds { get; init; } = [];

    public required bool Success { get; init; }

    public required ObservationConstraintDerivationFailureReason FailureReason { get; init; }

    public string? GeneratedConstraintId { get; init; }
}

public sealed record ObservationConstraintDerivationResult
{
    public ObservationDerivedConstraint? Constraint { get; init; }

    public required ObservationConstraintDerivationAudit Audit { get; init; }
}
