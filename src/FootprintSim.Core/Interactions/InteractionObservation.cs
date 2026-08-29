using FootprintSim.Evidence;

namespace FootprintSim.Interactions;

public sealed record InteractionObservation
{
    private InteractionObservation(
        string observationId,
        string scenarioId,
        int seed,
        string sourceEvidenceIdentifier,
        string? referencedInteractionId,
        InteractionRelationKind observedRelationKind,
        IReadOnlyList<InteractionParticipant> observedParticipants,
        InteractionTimeWindow observedTimeWindow,
        IReadOnlyList<string> observedRegionIds,
        long availableAtTick,
        EvidenceState evidenceBoundaryState,
        bool isPersonEvidence,
        bool isDeviceOnlyEvidence,
        InteractionVisibility visibility,
        IReadOnlyList<InteractionProvenance> provenance)
    {
        ObservationId = observationId;
        ScenarioId = scenarioId;
        Seed = seed;
        SourceEvidenceIdentifier = sourceEvidenceIdentifier;
        ReferencedInteractionId = referencedInteractionId;
        ObservedRelationKind = observedRelationKind;
        ObservedParticipants = observedParticipants;
        ObservedTimeWindow = observedTimeWindow;
        ObservedRegionIds = observedRegionIds;
        AvailableAtTick = availableAtTick;
        EvidenceBoundaryState = evidenceBoundaryState;
        IsPersonEvidence = isPersonEvidence;
        IsDeviceOnlyEvidence = isDeviceOnlyEvidence;
        Visibility = visibility;
        Provenance = provenance;
    }

    public string ObservationId { get; }

    public string ScenarioId { get; }

    public int Seed { get; }

    public string SourceEvidenceIdentifier { get; }

    // This is an evidence-side opaque reference, never an InteractionOccurrence object or lookup.
    public string? ReferencedInteractionId { get; }

    public InteractionRelationKind ObservedRelationKind { get; }

    public IReadOnlyList<InteractionParticipant> ObservedParticipants { get; }

    public InteractionTimeWindow ObservedTimeWindow { get; }

    public IReadOnlyList<string> ObservedRegionIds { get; }

    public long AvailableAtTick { get; }

    public EvidenceState EvidenceBoundaryState { get; }

    public bool IsPersonEvidence { get; }

    public bool IsDeviceOnlyEvidence { get; }

    public InteractionVisibility Visibility { get; }

    public IReadOnlyList<InteractionProvenance> Provenance { get; }

    public static InteractionObservation Create(
        string scenarioId,
        int seed,
        string sourceEvidenceIdentifier,
        string? referencedInteractionId,
        InteractionRelationKind observedRelationKind,
        IEnumerable<InteractionParticipant> observedParticipants,
        InteractionTimeWindow observedTimeWindow,
        IEnumerable<string> observedRegionIds,
        long availableAtTick,
        EvidenceState evidenceBoundaryState,
        bool isPersonEvidence,
        bool isDeviceOnlyEvidence,
        InteractionVisibility visibility = InteractionVisibility.PublicEvidence,
        IEnumerable<InteractionProvenance>? additionalProvenance = null)
    {
        var normalizedScenarioId = InteractionNormalization.RequireIdentifier(
            scenarioId,
            nameof(scenarioId));
        var normalizedSource = InteractionNormalization.RequireIdentifier(
            sourceEvidenceIdentifier,
            nameof(sourceEvidenceIdentifier));
        ArgumentNullException.ThrowIfNull(observedTimeWindow);
        if (availableAtTick < observedTimeWindow.EndTick)
        {
            throw new ArgumentOutOfRangeException(
                nameof(availableAtTick),
                "Evidence cannot become available before its observed time window ends.");
        }

        if (isPersonEvidence == isDeviceOnlyEvidence)
        {
            throw new ArgumentException(
                "Interaction evidence must be classified as either person evidence or device-only evidence.");
        }

        if (visibility == InteractionVisibility.PrivateTruth)
        {
            throw new ArgumentException(
                "An interaction observation cannot use private-truth visibility.",
                nameof(visibility));
        }

        var participants = InteractionNormalization.Participants(observedParticipants);
        var regions = InteractionNormalization.RegionIds(
            observedRegionIds,
            requireAtLeastOne: false);
        var provenance = InteractionNormalization.Provenance(
            new[]
            {
                new InteractionProvenance(
                    InteractionProvenanceKind.EvidenceRecord,
                    normalizedSource)
            }.Concat(additionalProvenance ?? []));
        var normalizedReference = string.IsNullOrWhiteSpace(referencedInteractionId)
            ? null
            : referencedInteractionId;
        var observationId = InteractionStableIdentifier.Create(
            "iobs",
            normalizedScenarioId,
            InteractionStableIdentifier.Seed(seed),
            normalizedSource,
            normalizedReference ?? "no-interaction-reference",
            observedRelationKind.ToString(),
            InteractionStableIdentifier.Tick(observedTimeWindow.StartTick),
            InteractionStableIdentifier.Tick(observedTimeWindow.EndTick),
            InteractionStableIdentifier.Tick(availableAtTick),
            evidenceBoundaryState.ToString(),
            isPersonEvidence ? "person-evidence" : "device-only-evidence",
            visibility.ToString(),
            string.Join("|", participants.Select(ParticipantIdentity)),
            regions.Count == 0 ? "no-region" : string.Join("|", regions),
            string.Join("|", provenance.Select(ProvenanceIdentity)));

        return new InteractionObservation(
            observationId,
            normalizedScenarioId,
            seed,
            normalizedSource,
            normalizedReference,
            observedRelationKind,
            participants,
            observedTimeWindow,
            regions,
            availableAtTick,
            evidenceBoundaryState,
            isPersonEvidence,
            isDeviceOnlyEvidence,
            visibility,
            provenance);
    }

    private static string ParticipantIdentity(InteractionParticipant participant) =>
        $"{participant.Role}:{participant.EntityKind}:{participant.EntityId}";

    private static string ProvenanceIdentity(InteractionProvenance provenance) =>
        $"{provenance.Kind}:{provenance.SourceIdentifier}";
}

public sealed record InteractionObservationSet
{
    public InteractionObservationSet(
        string scenarioId,
        int seed,
        IEnumerable<InteractionObservation> observations)
    {
        ScenarioId = InteractionNormalization.RequireIdentifier(scenarioId, nameof(scenarioId));
        Seed = seed;
        ArgumentNullException.ThrowIfNull(observations);
        var ordered = observations
            .OrderBy(item => item.AvailableAtTick)
            .ThenBy(item => item.ObservedTimeWindow.StartTick)
            .ThenBy(item => item.ObservationId, StringComparer.Ordinal)
            .ToArray();
        if (ordered.Any(item =>
            !string.Equals(item.ScenarioId, ScenarioId, StringComparison.Ordinal) ||
            item.Seed != Seed))
        {
            throw new ArgumentException("Interaction observation identifiers do not match their set.");
        }

        var duplicate = ordered
            .GroupBy(item => item.ObservationId, StringComparer.Ordinal)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicate is not null)
        {
            throw new ArgumentException(
                $"Interaction observation identifier '{duplicate.Key}' is duplicated.");
        }

        Observations = ordered;
    }

    public string ScenarioId { get; }

    public int Seed { get; }

    public IReadOnlyList<InteractionObservation> Observations { get; }

    public InteractionObservationSet PubliclyAvailableAt(long tick)
    {
        if (tick < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(tick));
        }

        return new InteractionObservationSet(
            ScenarioId,
            Seed,
            Observations.Where(item =>
                item.Visibility == InteractionVisibility.PublicEvidence &&
                item.AvailableAtTick <= tick));
    }
}
