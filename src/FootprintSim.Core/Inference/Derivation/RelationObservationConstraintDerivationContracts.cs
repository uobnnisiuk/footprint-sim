using FootprintSim.Evidence;
using FootprintSim.Inference.PastStates;
using FootprintSim.Interactions;

namespace FootprintSim.Inference.Derivation;

public enum RelationKindKnowledge
{
    Exact,
    UnknownKind
}

public enum RelationConstraintInputKind
{
    RelationObservation,
    RelationInformation,
    TargetEntity,
    OtherParticipant,
    OccurrenceTimeWindow,
    ParticipantLocationEvidence,
    EvidenceAvailability,
    EvidenceVisibility,
    EvidenceState,
    SourceAncestry,
    UsageRole
}

public enum RelationConstraintApplicabilityCondition
{
    RelationObservationPresent,
    ExactPersonPersonEncounter,
    BinaryRelation,
    PersonTarget,
    PersonOtherParticipant,
    KnownOtherParticipant,
    PublicEvidence,
    AvailableByCutoff,
    ConfirmedEvidenceBoundary,
    SameTimeParticipantLocationEvidence,
    CandidateGenerationOnly
}

public enum RelationConstraintDerivationFailureReason
{
    None,
    ObservationNotPublic,
    ObservationUnavailableAtCutoff,
    UnsupportedObservationState,
    UnknownRelationKind,
    UnsupportedRelationKind,
    RelationArityMismatch,
    EntityKindMismatch,
    MissingParticipantEvidence,
    WrongUsageRole,
    UnderlyingDerivationFailed
}

public sealed record ObservedRelationInformation
{
    private ObservedRelationInformation(
        RelationKindKnowledge knowledge,
        InteractionRelationKind? exactRelationKind)
    {
        Knowledge = knowledge;
        ExactRelationKind = exactRelationKind;
    }

    public RelationKindKnowledge Knowledge { get; }

    public InteractionRelationKind? ExactRelationKind { get; }

    public static ObservedRelationInformation Exact(InteractionRelationKind relationKind) =>
        new(RelationKindKnowledge.Exact, relationKind);

    public static ObservedRelationInformation ExistsButKindUnknown() =>
        new(RelationKindKnowledge.UnknownKind, exactRelationKind: null);

    internal string StableIdentity() => Knowledge == RelationKindKnowledge.UnknownKind
        ? "relation-exists-kind-unknown"
        : $"exact:{ExactRelationKind}";
}

/// <summary>
/// Public binary relation assertion with exact endpoints and independently represented relation
/// information. RelationObservationPresent remains true when the kind is unknown.
/// </summary>
public sealed record PublicRelationObservation
{
    private PublicRelationObservation(
        string observationId,
        string scenarioId,
        int seed,
        string sourceEvidenceIdentifier,
        string targetEntityId,
        InteractionEntityKind targetEntityKind,
        string otherParticipantId,
        InteractionEntityKind otherParticipantEntityKind,
        ObservedRelationInformation relationInformation,
        InteractionTimeWindow occurrenceTimeWindow,
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
        TargetEntityId = targetEntityId;
        TargetEntityKind = targetEntityKind;
        OtherParticipantId = otherParticipantId;
        OtherParticipantEntityKind = otherParticipantEntityKind;
        RelationInformation = relationInformation;
        OccurrenceTimeWindow = occurrenceTimeWindow;
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

    public string TargetEntityId { get; }

    public InteractionEntityKind TargetEntityKind { get; }

    public string OtherParticipantId { get; }

    public InteractionEntityKind OtherParticipantEntityKind { get; }

    public ObservedRelationInformation RelationInformation { get; }

    public InteractionTimeWindow OccurrenceTimeWindow { get; }

    public long AvailableAtTick { get; }

    public EvidenceState EvidenceBoundaryState { get; }

    public InteractionVisibility Visibility { get; }

    public IReadOnlyList<string> SourceAncestryIds { get; }

    public IReadOnlyList<InteractionProvenance> Provenance { get; }

    public bool RelationObservationPresent => true;

    public int Arity => 2;

    public static PublicRelationObservation Create(
        string scenarioId,
        int seed,
        string sourceEvidenceIdentifier,
        string targetEntityId,
        InteractionEntityKind targetEntityKind,
        string otherParticipantId,
        InteractionEntityKind otherParticipantEntityKind,
        ObservedRelationInformation relationInformation,
        InteractionTimeWindow occurrenceTimeWindow,
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
        var normalizedTarget = InteractionNormalization.RequireIdentifier(
            targetEntityId,
            nameof(targetEntityId));
        var normalizedOther = InteractionNormalization.RequireIdentifier(
            otherParticipantId,
            nameof(otherParticipantId));
        ArgumentNullException.ThrowIfNull(relationInformation);
        ArgumentNullException.ThrowIfNull(occurrenceTimeWindow);
        if (string.Equals(normalizedTarget, normalizedOther, StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "A binary relation observation requires distinct target and other endpoints.",
                nameof(otherParticipantId));
        }

        if (availableAtTick < occurrenceTimeWindow.EndTick)
        {
            throw new ArgumentOutOfRangeException(
                nameof(availableAtTick),
                "Evidence cannot become available before the relation occurrence window ends.");
        }

        if (visibility == InteractionVisibility.PrivateTruth)
        {
            throw new ArgumentException(
                "A public relation observation cannot use private-truth visibility.",
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
            "relobs",
            normalizedScenario,
            InteractionStableIdentifier.Seed(seed),
            normalizedSource,
            normalizedTarget,
            targetEntityKind.ToString(),
            normalizedOther,
            otherParticipantEntityKind.ToString(),
            relationInformation.StableIdentity(),
            InteractionStableIdentifier.Tick(occurrenceTimeWindow.StartTick),
            InteractionStableIdentifier.Tick(occurrenceTimeWindow.EndTick),
            InteractionStableIdentifier.Tick(availableAtTick),
            evidenceBoundaryState.ToString(),
            visibility.ToString(),
            string.Join("|", ancestry),
            string.Join("|", provenance.Select(ProvenanceIdentity)));

        return new PublicRelationObservation(
            observationId,
            normalizedScenario,
            seed,
            normalizedSource,
            normalizedTarget,
            targetEntityKind,
            normalizedOther,
            otherParticipantEntityKind,
            relationInformation,
            occurrenceTimeWindow,
            availableAtTick,
            evidenceBoundaryState,
            visibility,
            ancestry,
            provenance);
    }

    private static string ProvenanceIdentity(InteractionProvenance provenance) =>
        $"{provenance.Kind}:{provenance.SourceIdentifier}";
}

public sealed record RelationConstraintDerivationAudit
{
    public required string RuleId { get; init; }

    public required string UnderlyingConstraintRuleId { get; init; }

    public IReadOnlyList<RelationConstraintInputKind> RequiredInputInformation { get; init; } = [];

    public IReadOnlyList<RelationConstraintApplicabilityCondition> ApplicabilityConditions { get; init; } = [];

    public required bool RelationObservationPresent { get; init; }

    public required string SourceObservationId { get; init; }

    public required string SourceEvidenceIdentifier { get; init; }

    public required RelationKindKnowledge RelationKnowledge { get; init; }

    public InteractionRelationKind? ExactRelationKind { get; init; }

    public required int RelationArity { get; init; }

    public required string TargetEntityId { get; init; }

    public required InteractionEntityKind TargetEntityKind { get; init; }

    public required string OtherParticipantId { get; init; }

    public required InteractionEntityKind OtherParticipantEntityKind { get; init; }

    public required InteractionTimeWindow OccurrenceTimeWindow { get; init; }

    public required bool RelationDispatchAttempted { get; init; }

    public required bool EncounterRuleApplied { get; init; }

    public IReadOnlyList<string> PublicInputIdsInspected { get; init; } = [];

    public IReadOnlyList<string> PublicInputIdsUsed { get; init; } = [];

    public IReadOnlyList<string> OutputAllowedRegionIds { get; init; } = [];

    public required PastStateEvidenceUsageRole UsageRole { get; init; }

    public required string RelationObservationSourceGroupId { get; init; }

    public IReadOnlyList<string> ParticipantEvidenceSourceGroupIds { get; init; } = [];

    public IReadOnlyList<string> SourceAncestryIds { get; init; } = [];

    public required bool Success { get; init; }

    public required RelationConstraintDerivationFailureReason FailureReason { get; init; }

    public ParticipantConstraintDerivationFailureReason? UnderlyingFailureReason { get; init; }

    public string? UnderlyingObservationId { get; init; }

    public string? GeneratedConstraintId { get; init; }
}

public sealed record RelationConstraintDerivationResult
{
    public ObservationDerivedConstraint? Constraint { get; init; }

    public required RelationConstraintDerivationAudit Audit { get; init; }
}
