using FootprintSim.Evidence;

namespace FootprintSim.Interactions;

public enum CrossThreadConstraintDerivationKind
{
    PersonEncounterCoLocation
}

public sealed record CrossThreadConstraint
{
    internal CrossThreadConstraint(
        string constraintId,
        string scenarioId,
        int seed,
        string sourceObservationId,
        string sourceEvidenceIdentifier,
        string targetEntityId,
        InteractionRelationKind sourceRelationKind,
        IReadOnlyList<InteractionParticipant> sourceParticipants,
        InteractionTimeWindow sourceObservedTimeWindow,
        InteractionTimeWindow constrainedTimeWindow,
        IReadOnlyList<string> allowedRegionIds,
        long availableAtTick,
        EvidenceState evidenceBoundaryState,
        bool isPersonEvidence,
        bool isDeviceOnlyEvidence,
        CrossThreadConstraintDerivationKind derivationKind,
        InteractionVisibility visibility,
        IReadOnlyList<InteractionProvenance> provenance)
    {
        ConstraintId = constraintId;
        ScenarioId = scenarioId;
        Seed = seed;
        SourceObservationId = sourceObservationId;
        SourceEvidenceIdentifier = sourceEvidenceIdentifier;
        TargetEntityId = targetEntityId;
        SourceRelationKind = sourceRelationKind;
        SourceParticipants = sourceParticipants;
        SourceObservedTimeWindow = sourceObservedTimeWindow;
        ConstrainedTimeWindow = constrainedTimeWindow;
        AllowedRegionIds = allowedRegionIds;
        AvailableAtTick = availableAtTick;
        EvidenceBoundaryState = evidenceBoundaryState;
        IsPersonEvidence = isPersonEvidence;
        IsDeviceOnlyEvidence = isDeviceOnlyEvidence;
        DerivationKind = derivationKind;
        Visibility = visibility;
        Provenance = provenance;
    }

    public string ConstraintId { get; }

    public string ScenarioId { get; }

    public int Seed { get; }

    public string SourceObservationId { get; }

    public string SourceEvidenceIdentifier { get; }

    public string TargetEntityId { get; }

    public InteractionRelationKind SourceRelationKind { get; }

    public IReadOnlyList<InteractionParticipant> SourceParticipants { get; }

    public InteractionTimeWindow SourceObservedTimeWindow { get; }

    public InteractionTimeWindow ConstrainedTimeWindow { get; }

    public IReadOnlyList<string> AllowedRegionIds { get; }

    public long AvailableAtTick { get; }

    public EvidenceState EvidenceBoundaryState { get; }

    public bool IsPersonEvidence { get; }

    public bool IsDeviceOnlyEvidence { get; }

    public CrossThreadConstraintDerivationKind DerivationKind { get; }

    public InteractionVisibility Visibility { get; }

    public IReadOnlyList<InteractionProvenance> Provenance { get; }
}

public static class CrossThreadConstraintDeriver
{
    public const string PersonEncounterCoLocationRule =
        "person-person-encounter-co-location-v1";

    public static CrossThreadConstraint? DerivePersonEncounterCoLocation(
        InteractionObservation observation,
        string targetEntityId)
    {
        ArgumentNullException.ThrowIfNull(observation);
        var normalizedTarget = InteractionNormalization.RequireIdentifier(
            targetEntityId,
            nameof(targetEntityId));
        var targetParticipant = observation.ObservedParticipants.SingleOrDefault(item =>
            string.Equals(item.EntityId, normalizedTarget, StringComparison.Ordinal));
        if (observation.ObservedRelationKind != InteractionRelationKind.PersonPersonEncounter ||
            targetParticipant?.EntityKind != InteractionEntityKind.Person ||
            !observation.IsPersonEvidence ||
            observation.IsDeviceOnlyEvidence ||
            observation.ObservedRegionIds.Count == 0)
        {
            return null;
        }

        var derivationKind = CrossThreadConstraintDerivationKind.PersonEncounterCoLocation;
        var regions = InteractionNormalization.RegionIds(
            observation.ObservedRegionIds,
            requireAtLeastOne: true);
        var provenance = InteractionNormalization.Provenance(
            observation.Provenance.Concat(
            [
                new InteractionProvenance(
                    InteractionProvenanceKind.InteractionObservation,
                    observation.ObservationId),
                new InteractionProvenance(
                    InteractionProvenanceKind.DerivationRule,
                    PersonEncounterCoLocationRule)
            ]));
        var constraintId = InteractionStableIdentifier.Create(
            "ctc",
            observation.ScenarioId,
            InteractionStableIdentifier.Seed(observation.Seed),
            observation.ObservationId,
            normalizedTarget,
            derivationKind.ToString(),
            InteractionStableIdentifier.Tick(observation.ObservedTimeWindow.StartTick),
            InteractionStableIdentifier.Tick(observation.ObservedTimeWindow.EndTick),
            InteractionStableIdentifier.Tick(observation.AvailableAtTick),
            string.Join("|", regions));

        return new CrossThreadConstraint(
            constraintId,
            observation.ScenarioId,
            observation.Seed,
            observation.ObservationId,
            observation.SourceEvidenceIdentifier,
            normalizedTarget,
            observation.ObservedRelationKind,
            observation.ObservedParticipants,
            observation.ObservedTimeWindow,
            observation.ObservedTimeWindow,
            regions,
            observation.AvailableAtTick,
            observation.EvidenceBoundaryState,
            observation.IsPersonEvidence,
            observation.IsDeviceOnlyEvidence,
            derivationKind,
            observation.Visibility,
            provenance);
    }
}

public sealed record CrossThreadConstraintSet
{
    public CrossThreadConstraintSet(
        string scenarioId,
        int seed,
        IEnumerable<CrossThreadConstraint> constraints)
    {
        ScenarioId = InteractionNormalization.RequireIdentifier(scenarioId, nameof(scenarioId));
        Seed = seed;
        ArgumentNullException.ThrowIfNull(constraints);
        var ordered = constraints
            .OrderBy(item => item.AvailableAtTick)
            .ThenBy(item => item.ConstrainedTimeWindow.StartTick)
            .ThenBy(item => item.ConstraintId, StringComparer.Ordinal)
            .ToArray();
        if (ordered.Any(item =>
            !string.Equals(item.ScenarioId, ScenarioId, StringComparison.Ordinal) ||
            item.Seed != Seed))
        {
            throw new ArgumentException("Cross-thread constraint identifiers do not match their set.");
        }

        var duplicate = ordered
            .GroupBy(item => item.ConstraintId, StringComparer.Ordinal)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicate is not null)
        {
            throw new ArgumentException(
                $"Cross-thread constraint identifier '{duplicate.Key}' is duplicated.");
        }

        Constraints = ordered;
    }

    public string ScenarioId { get; }

    public int Seed { get; }

    public IReadOnlyList<CrossThreadConstraint> Constraints { get; }

    public CrossThreadConstraintSet PubliclyAvailableAt(long tick)
    {
        if (tick < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(tick));
        }

        return new CrossThreadConstraintSet(
            ScenarioId,
            Seed,
            Constraints.Where(item =>
                item.Visibility == InteractionVisibility.PublicEvidence &&
                item.AvailableAtTick <= tick));
    }
}
