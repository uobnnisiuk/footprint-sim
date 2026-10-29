using FootprintSim.Evidence;
using FootprintSim.Inference.PastStates;
using FootprintSim.Interactions;

namespace FootprintSim.Inference.Derivation;

public enum ParticipantIdentityKnowledge
{
    Exact,
    FiniteAlternatives,
    UnknownIdentity
}

public enum ParticipantConstraintInputKind
{
    EncounterObservation,
    TargetEntity,
    OtherParticipantInformation,
    ExactEncounterRelation,
    EncounterTimeWindow,
    ParticipantLocationEvidence,
    EvidenceAvailability,
    EvidenceVisibility,
    EvidenceState,
    SourceAncestry,
    UsageRole
}

public enum ParticipantConstraintApplicabilityCondition
{
    EncounterObservationPresent,
    PersonPersonEncounter,
    PersonTarget,
    PersonOtherParticipants,
    KnownFiniteParticipantIdentities,
    PublicEvidence,
    AvailableByCutoff,
    ConfirmedEvidenceBoundary,
    MatchingScenarioAndSeed,
    EvidenceCoversEncounterWindow,
    CompleteParticipantEvidenceCoverage,
    CandidateGenerationOnly
}

public enum ParticipantConstraintDerivationFailureReason
{
    None,
    ObservationNotPublic,
    ObservationUnavailableAtCutoff,
    UnsupportedObservationState,
    UnsupportedRelation,
    TargetEntityKindMismatch,
    OtherParticipantEntityKindMismatch,
    UnknownParticipantIdentity,
    MissingParticipantLocationEvidence,
    MultipleParticipantLocationEvidenceRecords,
    ParticipantEvidenceScenarioMismatch,
    ParticipantEvidenceSeedMismatch,
    ParticipantEvidenceEntityKindMismatch,
    ParticipantEvidenceNotPublic,
    ParticipantEvidenceUnavailableAtCutoff,
    UnsupportedParticipantEvidenceState,
    ParticipantEvidenceTimeMismatch,
    ParticipantEvidenceUsageMismatch,
    UnsupportedUsageRole
}

public enum ParticipantRegionUncertaintyPropagation
{
    ConservativeUnionAcrossPossibleParticipants
}

public sealed record OtherParticipantInformation
{
    private OtherParticipantInformation(
        ParticipantIdentityKnowledge identityKnowledge,
        InteractionEntityKind entityKind,
        IReadOnlyList<string> possibleParticipantIds)
    {
        IdentityKnowledge = identityKnowledge;
        EntityKind = entityKind;
        PossibleParticipantIds = possibleParticipantIds;
    }

    public ParticipantIdentityKnowledge IdentityKnowledge { get; }

    public InteractionEntityKind EntityKind { get; }

    public IReadOnlyList<string> PossibleParticipantIds { get; }

    public static OtherParticipantInformation Exact(
        string participantId,
        InteractionEntityKind entityKind = InteractionEntityKind.Person) => new(
            ParticipantIdentityKnowledge.Exact,
            entityKind,
            [InteractionNormalization.RequireIdentifier(participantId, nameof(participantId))]);

    public static OtherParticipantInformation FiniteAlternatives(
        IEnumerable<string> participantIds,
        InteractionEntityKind entityKind = InteractionEntityKind.Person)
    {
        var ids = NormalizeParticipantIds(participantIds);
        if (ids.Length < 2)
        {
            throw new ArgumentException(
                "Finite participant alternatives require at least two identities.",
                nameof(participantIds));
        }

        return new OtherParticipantInformation(
            ParticipantIdentityKnowledge.FiniteAlternatives,
            entityKind,
            ids);
    }

    public static OtherParticipantInformation UnknownIdentity(
        InteractionEntityKind entityKind = InteractionEntityKind.Person) => new(
            ParticipantIdentityKnowledge.UnknownIdentity,
            entityKind,
            []);

    internal string StableIdentity() => string.Join(
        ':',
        IdentityKnowledge,
        EntityKind,
        PossibleParticipantIds.Count == 0
            ? "no-known-identity"
            : string.Join("|", PossibleParticipantIds));

    private static string[] NormalizeParticipantIds(IEnumerable<string> participantIds)
    {
        ArgumentNullException.ThrowIfNull(participantIds);
        return participantIds
            .Select(item => InteractionNormalization.RequireIdentifier(item, nameof(participantIds)))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(item => item, StringComparer.Ordinal)
            .ToArray();
    }
}

public sealed record PublicEncounterParticipantObservation
{
    private PublicEncounterParticipantObservation(
        string observationId,
        string scenarioId,
        int seed,
        string sourceEvidenceIdentifier,
        string targetEntityId,
        InteractionEntityKind targetEntityKind,
        OtherParticipantInformation otherParticipant,
        InteractionRelationKind relationKind,
        InteractionTimeWindow encounterTimeWindow,
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
        OtherParticipant = otherParticipant;
        RelationKind = relationKind;
        EncounterTimeWindow = encounterTimeWindow;
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

    public OtherParticipantInformation OtherParticipant { get; }

    public InteractionRelationKind RelationKind { get; }

    public InteractionTimeWindow EncounterTimeWindow { get; }

    public long AvailableAtTick { get; }

    public EvidenceState EvidenceBoundaryState { get; }

    public InteractionVisibility Visibility { get; }

    public IReadOnlyList<string> SourceAncestryIds { get; }

    public IReadOnlyList<InteractionProvenance> Provenance { get; }

    public bool EncounterObservationPresent => true;

    public static PublicEncounterParticipantObservation Create(
        string scenarioId,
        int seed,
        string sourceEvidenceIdentifier,
        string targetEntityId,
        InteractionEntityKind targetEntityKind,
        OtherParticipantInformation otherParticipant,
        InteractionRelationKind relationKind,
        InteractionTimeWindow encounterTimeWindow,
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
        ArgumentNullException.ThrowIfNull(otherParticipant);
        ArgumentNullException.ThrowIfNull(encounterTimeWindow);
        if (availableAtTick < encounterTimeWindow.EndTick)
        {
            throw new ArgumentOutOfRangeException(
                nameof(availableAtTick),
                "Evidence cannot become available before the encounter window ends.");
        }

        if (visibility == InteractionVisibility.PrivateTruth)
        {
            throw new ArgumentException(
                "An encounter-participant observation cannot use private-truth visibility.",
                nameof(visibility));
        }

        if (otherParticipant.PossibleParticipantIds.Contains(normalizedTarget, StringComparer.Ordinal))
        {
            throw new ArgumentException(
                "The target cannot also be an other-participant alternative.",
                nameof(otherParticipant));
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
            "epobs",
            normalizedScenario,
            InteractionStableIdentifier.Seed(seed),
            normalizedSource,
            normalizedTarget,
            targetEntityKind.ToString(),
            otherParticipant.StableIdentity(),
            relationKind.ToString(),
            InteractionStableIdentifier.Tick(encounterTimeWindow.StartTick),
            InteractionStableIdentifier.Tick(encounterTimeWindow.EndTick),
            InteractionStableIdentifier.Tick(availableAtTick),
            evidenceBoundaryState.ToString(),
            visibility.ToString(),
            string.Join("|", ancestry),
            string.Join("|", provenance.Select(ProvenanceIdentity)));

        return new PublicEncounterParticipantObservation(
            observationId,
            normalizedScenario,
            seed,
            normalizedSource,
            normalizedTarget,
            targetEntityKind,
            otherParticipant,
            relationKind,
            encounterTimeWindow,
            availableAtTick,
            evidenceBoundaryState,
            visibility,
            ancestry,
            provenance);
    }

    private static string ProvenanceIdentity(InteractionProvenance provenance) =>
        $"{provenance.Kind}:{provenance.SourceIdentifier}";
}

public sealed record PublicParticipantLocationEvidence
{
    private PublicParticipantLocationEvidence(
        string publicInputId,
        string scenarioId,
        int seed,
        string sourceEvidenceIdentifier,
        string sourceGroupId,
        string participantId,
        InteractionEntityKind participantEntityKind,
        InteractionTimeWindow applicableTimeWindow,
        IReadOnlyList<string> allowedRegionIds,
        long availableAtTick,
        EvidenceState evidenceBoundaryState,
        InteractionVisibility visibility,
        PastStateEvidenceUsageRole usageRole,
        IReadOnlyList<string> sourceAncestryIds,
        IReadOnlyList<InteractionProvenance> provenance)
    {
        PublicInputId = publicInputId;
        ScenarioId = scenarioId;
        Seed = seed;
        SourceEvidenceIdentifier = sourceEvidenceIdentifier;
        SourceGroupId = sourceGroupId;
        ParticipantId = participantId;
        ParticipantEntityKind = participantEntityKind;
        ApplicableTimeWindow = applicableTimeWindow;
        AllowedRegionIds = allowedRegionIds;
        AvailableAtTick = availableAtTick;
        EvidenceBoundaryState = evidenceBoundaryState;
        Visibility = visibility;
        UsageRole = usageRole;
        SourceAncestryIds = sourceAncestryIds;
        Provenance = provenance;
    }

    public string PublicInputId { get; }

    public string ScenarioId { get; }

    public int Seed { get; }

    public string SourceEvidenceIdentifier { get; }

    public string SourceGroupId { get; }

    public string ParticipantId { get; }

    public InteractionEntityKind ParticipantEntityKind { get; }

    public InteractionTimeWindow ApplicableTimeWindow { get; }

    public IReadOnlyList<string> AllowedRegionIds { get; }

    public long AvailableAtTick { get; }

    public EvidenceState EvidenceBoundaryState { get; }

    public InteractionVisibility Visibility { get; }

    public PastStateEvidenceUsageRole UsageRole { get; }

    public IReadOnlyList<string> SourceAncestryIds { get; }

    public IReadOnlyList<InteractionProvenance> Provenance { get; }

    public static PublicParticipantLocationEvidence Create(
        string scenarioId,
        int seed,
        string sourceEvidenceIdentifier,
        string participantId,
        InteractionEntityKind participantEntityKind,
        InteractionTimeWindow applicableTimeWindow,
        IEnumerable<string> allowedRegionIds,
        long availableAtTick,
        EvidenceState evidenceBoundaryState,
        PastStateEvidenceUsageRole usageRole,
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
        var normalizedParticipant = InteractionNormalization.RequireIdentifier(
            participantId,
            nameof(participantId));
        ArgumentNullException.ThrowIfNull(applicableTimeWindow);
        if (availableAtTick < applicableTimeWindow.EndTick)
        {
            throw new ArgumentOutOfRangeException(
                nameof(availableAtTick),
                "Evidence cannot become available before its applicable time window ends.");
        }

        if (visibility == InteractionVisibility.PrivateTruth)
        {
            throw new ArgumentException(
                "Participant location evidence cannot use private-truth visibility.",
                nameof(visibility));
        }

        var regions = InteractionNormalization.RegionIds(allowedRegionIds, requireAtLeastOne: true);
        var ancestry = (sourceAncestryIds ?? [])
            .Append(normalizedSource)
            .Select(item => InteractionNormalization.RequireIdentifier(item, nameof(sourceAncestryIds)))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(item => item, StringComparer.Ordinal)
            .ToArray();
        var sourceGroupId = InteractionStableIdentifier.Create(
            "ples",
            string.Join("|", ancestry));
        var provenance = InteractionNormalization.Provenance(
            new[]
            {
                new InteractionProvenance(
                    InteractionProvenanceKind.EvidenceRecord,
                    normalizedSource)
            }.Concat(additionalProvenance ?? []));
        var publicInputId = InteractionStableIdentifier.Create(
            "ple",
            normalizedScenario,
            InteractionStableIdentifier.Seed(seed),
            normalizedSource,
            normalizedParticipant,
            participantEntityKind.ToString(),
            InteractionStableIdentifier.Tick(applicableTimeWindow.StartTick),
            InteractionStableIdentifier.Tick(applicableTimeWindow.EndTick),
            string.Join("|", regions),
            InteractionStableIdentifier.Tick(availableAtTick),
            evidenceBoundaryState.ToString(),
            usageRole.ToString(),
            visibility.ToString(),
            string.Join("|", ancestry),
            string.Join("|", provenance.Select(ProvenanceIdentity)));

        return new PublicParticipantLocationEvidence(
            publicInputId,
            normalizedScenario,
            seed,
            normalizedSource,
            sourceGroupId,
            normalizedParticipant,
            participantEntityKind,
            applicableTimeWindow,
            regions,
            availableAtTick,
            evidenceBoundaryState,
            visibility,
            usageRole,
            ancestry,
            provenance);
    }

    private static string ProvenanceIdentity(InteractionProvenance provenance) =>
        $"{provenance.Kind}:{provenance.SourceIdentifier}";
}

public sealed record ParticipantLocationEvidenceBranchAudit
{
    public required string ParticipantId { get; init; }

    public required InteractionEntityKind EntityKind { get; init; }

    public IReadOnlyList<string> MatchingPublicInputIds { get; init; } = [];

    public IReadOnlyList<string> AllowedRegionIds { get; init; } = [];

    public IReadOnlyList<string> SourceGroupIds { get; init; } = [];

    public required bool Derivable { get; init; }

    public required ParticipantConstraintDerivationFailureReason FailureReason { get; init; }
}

public sealed record ParticipantConstraintDerivationAudit
{
    public required string RuleId { get; init; }

    public IReadOnlyList<ParticipantConstraintInputKind> RequiredInputInformation { get; init; } = [];

    public IReadOnlyList<ParticipantConstraintApplicabilityCondition> ApplicabilityConditions { get; init; } = [];

    public required bool EncounterObservationPresent { get; init; }

    public required string SourceObservationId { get; init; }

    public required string SourceEvidenceIdentifier { get; init; }

    public required string TargetEntityId { get; init; }

    public required InteractionRelationKind RelationKind { get; init; }

    public required InteractionTimeWindow EncounterTimeWindow { get; init; }

    public required ParticipantIdentityKnowledge ParticipantIdentityKnowledge { get; init; }

    public required InteractionEntityKind ParticipantEntityKind { get; init; }

    public IReadOnlyList<string> PossibleParticipantIds { get; init; } = [];

    public IReadOnlyList<ParticipantLocationEvidenceBranchAudit> ParticipantBranches { get; init; } = [];

    public IReadOnlyList<string> PublicInputIdsInspected { get; init; } = [];

    public IReadOnlyList<string> PublicInputIdsUsed { get; init; } = [];

    public IReadOnlyList<string> OutputAllowedRegionIds { get; init; } = [];

    public required ParticipantRegionUncertaintyPropagation UncertaintyPropagation { get; init; }

    public required PastStateEvidenceUsageRole UsageRole { get; init; }

    public required string EncounterSourceGroupId { get; init; }

    public IReadOnlyList<string> ParticipantEvidenceSourceGroupIds { get; init; } = [];

    public IReadOnlyList<string> SourceAncestryIds { get; init; } = [];

    public required int ParticipantAlternativesEvaluated { get; init; }

    public required int ParticipantLocationEvidenceRecordsInspected { get; init; }

    public required bool Success { get; init; }

    public required ParticipantConstraintDerivationFailureReason FailureReason { get; init; }

    public string? GeneratedConstraintId { get; init; }
}

public sealed record ParticipantConstraintDerivationResult
{
    public ObservationDerivedConstraint? Constraint { get; init; }

    public required ParticipantConstraintDerivationAudit Audit { get; init; }
}
