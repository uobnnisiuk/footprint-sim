using FootprintSim.Events;

namespace FootprintSim.Interactions.Truth;

public sealed record InteractionOccurrence
{
    internal InteractionOccurrence(
        string interactionId,
        string scenarioId,
        int seed,
        InteractionRelationKind relationKind,
        IReadOnlyList<InteractionParticipant> participants,
        InteractionTimeWindow occurrenceTimeWindow,
        IReadOnlyList<string> regionIds,
        string sourceEventId,
        string? causalParentEventId,
        IReadOnlyList<InteractionProvenance> provenance)
    {
        InteractionId = interactionId;
        ScenarioId = scenarioId;
        Seed = seed;
        RelationKind = relationKind;
        Participants = participants;
        OccurrenceTimeWindow = occurrenceTimeWindow;
        RegionIds = regionIds;
        SourceEventId = sourceEventId;
        CausalParentEventId = causalParentEventId;
        Provenance = provenance;
    }

    public string InteractionId { get; }

    public string ScenarioId { get; }

    public int Seed { get; }

    public InteractionRelationKind RelationKind { get; }

    public IReadOnlyList<InteractionParticipant> Participants { get; }

    public InteractionTimeWindow OccurrenceTimeWindow { get; }

    public IReadOnlyList<string> RegionIds { get; }

    public string SourceEventId { get; }

    public string? CausalParentEventId { get; }

    public IReadOnlyList<InteractionProvenance> Provenance { get; }

    public InteractionVisibility Visibility => InteractionVisibility.PrivateTruth;
}

public static class InteractionOccurrenceProjector
{
    public static InteractionOccurrence FromPersonPersonEncounter(
        string scenarioId,
        int seed,
        SimulationEvent sourceEvent,
        IEnumerable<string> personEntityIds,
        IEnumerable<InteractionProvenance>? additionalProvenance = null)
    {
        InteractionNormalization.RequireIdentifier(scenarioId, nameof(scenarioId));
        ArgumentNullException.ThrowIfNull(sourceEvent);
        ArgumentNullException.ThrowIfNull(personEntityIds);
        if (sourceEvent.Kind != EventKind.Encounter)
        {
            throw new ArgumentException(
                "A person-person interaction occurrence must be projected from an Encounter event.",
                nameof(sourceEvent));
        }

        var knownPersons = personEntityIds.ToHashSet(StringComparer.Ordinal);
        var participants = InteractionNormalization.Participants(sourceEvent.SubjectIds.Select(
            entityId => new InteractionParticipant(
                entityId,
                InteractionEntityKind.Person,
                InteractionParticipantRole.EncounterParticipant)));
        if (participants.Count != 2 || participants.Any(item => !knownPersons.Contains(item.EntityId)))
        {
            throw new ArgumentException(
                "A person-person encounter must identify exactly two known persons.",
                nameof(sourceEvent));
        }

        var regionId = sourceEvent.RegionId ?? sourceEvent.Position?.RegionId;
        var regions = InteractionNormalization.RegionIds(
            regionId is null ? [] : [regionId],
            requireAtLeastOne: true);
        var sourceEventId = InteractionNormalization.RequireIdentifier(
            sourceEvent.Id,
            nameof(sourceEvent));
        var provenance = InteractionNormalization.Provenance(
            new[]
            {
                new InteractionProvenance(
                    InteractionProvenanceKind.WorldEvent,
                    sourceEventId)
            }.Concat(additionalProvenance ?? []));
        var relationKind = InteractionRelationKind.PersonPersonEncounter;
        var interactionId = InteractionStableIdentifier.Create(
            "int",
            scenarioId,
            InteractionStableIdentifier.Seed(seed),
            relationKind.ToString(),
            sourceEventId,
            InteractionStableIdentifier.Tick(sourceEvent.Tick),
            string.Join("|", participants.Select(ParticipantIdentity)),
            string.Join("|", regions));

        return new InteractionOccurrence(
            interactionId,
            scenarioId,
            seed,
            relationKind,
            participants,
            InteractionTimeWindow.At(sourceEvent.Tick),
            regions,
            sourceEventId,
            sourceEvent.CauseEventId,
            provenance);
    }

    private static string ParticipantIdentity(InteractionParticipant participant) =>
        $"{participant.Role}:{participant.EntityKind}:{participant.EntityId}";
}
