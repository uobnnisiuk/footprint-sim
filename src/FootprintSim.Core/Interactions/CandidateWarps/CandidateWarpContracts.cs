using FootprintSim.Evidence;
using FootprintSim.Hazards;

namespace FootprintSim.Interactions.CandidateWarps;

public enum CandidateWarpTemporalKind
{
    EventPoint,
    StateInterval
}

public enum CandidateWarpStanding
{
    Preferred,
    OuterPlausible,
    Conflicted
}

public enum CandidateWarpSourceKind
{
    LastConfirmed,
    VoluntaryReachability,
    PublicHazardEnvelope,
    PersonObservation,
    DeviceOnlyObservation,
    CrossThreadObservation,
    ConflictingObservation,
    Unsupported
}

public enum ConstraintTemporalKind
{
    ExactTick,
    InclusiveInterval
}

public enum ConstraintApplicationDisposition
{
    Narrow,
    RetainAlternative,
    Branch,
    MarkConflict,
    NoOp
}

public enum ConstraintApplicationReason
{
    Applied,
    EvidenceStateDoesNotConstrain,
    NoTemporalOverlap,
    NoRegionOverlap,
    DeviceOnlyEvidenceCannotConstrainPerson,
    DuplicateSourceObservation
}

public sealed record CandidateWarpProvenance
{
    private CandidateWarpProvenance(
        CandidateWarpSourceKind sourceKind,
        string sourceIdentifier,
        long availableAtTick,
        bool isPersonEvidence,
        bool isDeviceOnlyEvidence,
        EvidenceState evidenceBoundaryState,
        InteractionVisibility visibility,
        IReadOnlyList<string> regionIds,
        InteractionRelationKind? relationKind,
        string? sourceObservationId,
        InteractionTimeWindow? sourceTimeWindow,
        IReadOnlyList<InteractionParticipant> participants,
        IReadOnlyList<InteractionProvenance> interactionProvenance)
    {
        SourceKind = sourceKind;
        SourceIdentifier = sourceIdentifier;
        AvailableAtTick = availableAtTick;
        IsPersonEvidence = isPersonEvidence;
        IsDeviceOnlyEvidence = isDeviceOnlyEvidence;
        EvidenceBoundaryState = evidenceBoundaryState;
        Visibility = visibility;
        RegionIds = regionIds;
        RelationKind = relationKind;
        SourceObservationId = sourceObservationId;
        SourceTimeWindow = sourceTimeWindow;
        Participants = participants;
        InteractionProvenance = interactionProvenance;
    }

    public CandidateWarpSourceKind SourceKind { get; }

    public string SourceIdentifier { get; }

    public long AvailableAtTick { get; }

    public bool IsPersonEvidence { get; }

    public bool IsDeviceOnlyEvidence { get; }

    public EvidenceState EvidenceBoundaryState { get; }

    public InteractionVisibility Visibility { get; }

    public IReadOnlyList<string> RegionIds { get; }

    public InteractionRelationKind? RelationKind { get; }

    public string? SourceObservationId { get; }

    public InteractionTimeWindow? SourceTimeWindow { get; }

    public IReadOnlyList<InteractionParticipant> Participants { get; }

    public IReadOnlyList<InteractionProvenance> InteractionProvenance { get; }

    public static CandidateWarpProvenance Create(
        CandidateWarpSourceKind sourceKind,
        string sourceIdentifier,
        long availableAtTick,
        bool isPersonEvidence,
        bool isDeviceOnlyEvidence,
        EvidenceState evidenceBoundaryState,
        IEnumerable<string> regionIds,
        InteractionRelationKind? relationKind = null,
        string? sourceObservationId = null,
        InteractionTimeWindow? sourceTimeWindow = null,
        IEnumerable<InteractionParticipant>? participants = null,
        IEnumerable<InteractionProvenance>? interactionProvenance = null,
        InteractionVisibility visibility = InteractionVisibility.PublicEvidence)
    {
        if (availableAtTick < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(availableAtTick));
        }

        if (isPersonEvidence && isDeviceOnlyEvidence)
        {
            throw new ArgumentException(
                "Candidate provenance cannot be both person and device-only evidence.");
        }

        if (visibility == InteractionVisibility.PrivateTruth)
        {
            throw new ArgumentException(
                "Candidate provenance cannot use private-truth visibility.",
                nameof(visibility));
        }

        var normalizedSource = InteractionNormalization.RequireIdentifier(
            sourceIdentifier,
            nameof(sourceIdentifier));
        var normalizedRegions = InteractionNormalization.RegionIds(
            regionIds,
            requireAtLeastOne: true);
        var normalizedObservation = string.IsNullOrWhiteSpace(sourceObservationId)
            ? null
            : sourceObservationId;
        var normalizedParticipants = participants is null
            ? []
            : InteractionNormalization.Participants(participants);
        var normalizedInteractionProvenance = interactionProvenance is null
            ? []
            : interactionProvenance
                .Distinct()
                .OrderBy(item => item.Kind)
                .ThenBy(item => item.SourceIdentifier, StringComparer.Ordinal)
                .ToArray();

        return new CandidateWarpProvenance(
            sourceKind,
            normalizedSource,
            availableAtTick,
            isPersonEvidence,
            isDeviceOnlyEvidence,
            evidenceBoundaryState,
            visibility,
            normalizedRegions,
            relationKind,
            normalizedObservation,
            sourceTimeWindow,
            normalizedParticipants,
            normalizedInteractionProvenance);
    }

    internal string StableIdentity() => string.Join(
        ":",
        SourceKind,
        SourceIdentifier,
        InteractionStableIdentifier.Tick(AvailableAtTick),
        IsPersonEvidence,
        IsDeviceOnlyEvidence,
        EvidenceBoundaryState,
        Visibility,
        string.Join("|", RegionIds),
        RelationKind?.ToString() ?? "no-relation",
        SourceObservationId ?? "no-observation",
        SourceTimeWindow is null
            ? "no-source-window"
            : $"{SourceTimeWindow.StartTick}-{SourceTimeWindow.EndTick}",
        string.Join("|", Participants.Select(ParticipantIdentity)),
        string.Join("|", InteractionProvenance.Select(ProvenanceIdentity)));

    private static string ParticipantIdentity(InteractionParticipant participant) =>
        $"{participant.Role}:{participant.EntityKind}:{participant.EntityId}";

    private static string ProvenanceIdentity(InteractionProvenance provenance) =>
        $"{provenance.Kind}:{provenance.SourceIdentifier}";
}

public sealed record PublicCandidateWarpStateDefinition
{
    public required InteractionTimeWindow TimeWindow { get; init; }

    public required CandidateWarpTemporalKind TemporalKind { get; init; }

    public IReadOnlyList<string> RegionIds { get; init; } = [];

    public required EvidenceState EvidenceBoundaryState { get; init; }

    public IReadOnlyList<CandidateWarpProvenance> Provenance { get; init; } = [];
}

public sealed record CandidateWarpState
{
    internal CandidateWarpState(
        string stateId,
        InteractionTimeWindow timeWindow,
        CandidateWarpTemporalKind temporalKind,
        IReadOnlyList<string> regionIds,
        EvidenceState evidenceBoundaryState,
        IReadOnlyList<CandidateWarpProvenance> provenance)
    {
        StateId = stateId;
        TimeWindow = timeWindow;
        TemporalKind = temporalKind;
        RegionIds = regionIds;
        EvidenceBoundaryState = evidenceBoundaryState;
        Provenance = provenance;
    }

    public string StateId { get; }

    public InteractionTimeWindow TimeWindow { get; }

    public CandidateWarpTemporalKind TemporalKind { get; }

    public IReadOnlyList<string> RegionIds { get; }

    public EvidenceState EvidenceBoundaryState { get; }

    public IReadOnlyList<CandidateWarpProvenance> Provenance { get; }
}

public sealed record CandidateWarp
{
    internal CandidateWarp(
        string warpId,
        string scenarioId,
        int seed,
        string targetEntityId,
        CandidateWarpStanding standing,
        string? parentWarpId,
        IReadOnlyList<CandidateWarpState> states)
    {
        WarpId = warpId;
        ScenarioId = scenarioId;
        Seed = seed;
        TargetEntityId = targetEntityId;
        Standing = standing;
        ParentWarpId = parentWarpId;
        States = states;
    }

    public string WarpId { get; }

    public string ScenarioId { get; }

    public int Seed { get; }

    public string TargetEntityId { get; }

    public CandidateWarpStanding Standing { get; }

    public string? ParentWarpId { get; }

    public IReadOnlyList<CandidateWarpState> States { get; }

    public IReadOnlyList<string> CandidateRegionIdsAt(long tick)
    {
        if (tick < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(tick));
        }

        var statesAtTick = States
            .Where(state => Contains(state.TimeWindow, tick))
            .ToArray();
        if (statesAtTick.Length == 0)
        {
            var latestEndTick = States
                .Where(state => state.TimeWindow.EndTick <= tick)
                .Select(state => (long?)state.TimeWindow.EndTick)
                .Max();
            statesAtTick = latestEndTick is null
                ? []
                : States.Where(state => state.TimeWindow.EndTick == latestEndTick).ToArray();
        }

        return statesAtTick
            .SelectMany(state => state.RegionIds)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(regionId => regionId, StringComparer.Ordinal)
            .ToArray();
    }

    private static bool Contains(InteractionTimeWindow window, long tick) =>
        window.StartTick <= tick && tick <= window.EndTick;
}

public sealed record CandidateWarpSet
{
    internal CandidateWarpSet(
        string scenarioId,
        int seed,
        string targetEntityId,
        IReadOnlyList<CandidateWarp> warps)
    {
        ScenarioId = scenarioId;
        Seed = seed;
        TargetEntityId = targetEntityId;
        Warps = warps;
    }

    public string ScenarioId { get; }

    public int Seed { get; }

    public string TargetEntityId { get; }

    public IReadOnlyList<CandidateWarp> Warps { get; }

    public IReadOnlyList<string> CandidateRegionIdsAt(long tick) => Warps
        .SelectMany(warp => warp.CandidateRegionIdsAt(tick))
        .Distinct(StringComparer.Ordinal)
        .OrderBy(regionId => regionId, StringComparer.Ordinal)
        .ToArray();

    public IReadOnlyList<string> PreferredRegionIdsAt(long tick) => Warps
        .Where(warp => warp.Standing == CandidateWarpStanding.Preferred)
        .SelectMany(warp => warp.CandidateRegionIdsAt(tick))
        .Distinct(StringComparer.Ordinal)
        .OrderBy(regionId => regionId, StringComparer.Ordinal)
        .ToArray();

    public IReadOnlyList<string> OuterPlausibleRegionIdsAt(long tick) => Warps
        .Where(warp => warp.Standing == CandidateWarpStanding.OuterPlausible)
        .SelectMany(warp => warp.CandidateRegionIdsAt(tick))
        .Distinct(StringComparer.Ordinal)
        .OrderBy(regionId => regionId, StringComparer.Ordinal)
        .ToArray();
}

public sealed record CandidateWarpAccessContext
{
    public required string ScenarioId { get; init; }

    public required int Seed { get; init; }

    public required string TargetEntityId { get; init; }

    public required long CurrentTick { get; init; }

    public string QueryContextId { get; init; } = "public-search";
}

public static class PublicCandidateWarpFactory
{
    public static CandidateWarpSet Create(
        string scenarioId,
        int seed,
        string targetEntityId,
        IEnumerable<PublicCandidateWarpStateDefinition> states,
        CandidateWarpStanding standing = CandidateWarpStanding.OuterPlausible)
    {
        var normalizedScenario = InteractionNormalization.RequireIdentifier(
            scenarioId,
            nameof(scenarioId));
        var normalizedTarget = InteractionNormalization.RequireIdentifier(
            targetEntityId,
            nameof(targetEntityId));
        ArgumentNullException.ThrowIfNull(states);
        var normalizedStates = states
            .Select(state => CandidateWarpNormalization.CreateState(
                normalizedScenario,
                seed,
                normalizedTarget,
                state))
            .GroupBy(state => state.StateId, StringComparer.Ordinal)
            .Select(group => group.First())
            .OrderBy(state => state.TimeWindow.StartTick)
            .ThenBy(state => state.TimeWindow.EndTick)
            .ThenBy(state => state.TemporalKind)
            .ThenBy(state => state.StateId, StringComparer.Ordinal)
            .ToArray();
        if (normalizedStates.Length == 0)
        {
            throw new ArgumentException("At least one public candidate state is required.", nameof(states));
        }

        var warp = CandidateWarpNormalization.CreateWarp(
            normalizedScenario,
            seed,
            normalizedTarget,
            standing,
            parentWarpId: null,
            normalizedStates,
            identitySuffix: "public-origin");
        return CandidateWarpNormalization.CreateSet(
            normalizedScenario,
            seed,
            normalizedTarget,
            [warp]);
    }
}

public static class PublicCandidateWarpProjection
{
    public static CandidateWarpSet ForwardTo(
        CandidateWarpSet candidateWarps,
        long currentTick,
        PublicHazardEnvelope? publicHazardEnvelope)
    {
        ArgumentNullException.ThrowIfNull(candidateWarps);
        if (currentTick < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(currentTick));
        }

        var projected = candidateWarps.Warps.Select(warp =>
        {
            if (warp.States.Any(state =>
                state.TimeWindow.StartTick <= currentTick &&
                currentTick <= state.TimeWindow.EndTick))
            {
                return warp;
            }

            var sources = warp.States
                .Where(state => state.TimeWindow.EndTick <= currentTick)
                .OrderByDescending(state => state.TimeWindow.EndTick)
                .ThenBy(state => state.StateId, StringComparer.Ordinal)
                .ToArray();
            if (sources.Length == 0)
            {
                return warp;
            }

            var latestTick = sources[0].TimeWindow.EndTick;
            var latest = sources.Where(state => state.TimeWindow.EndTick == latestTick).ToArray();
            var future = PublicCandidateWarpPropagator.Propagate(
                warp.ScenarioId,
                warp.Seed,
                warp.TargetEntityId,
                latest,
                currentTick,
                publicHazardEnvelope);
            return CandidateWarpNormalization.CreateWarp(
                warp.ScenarioId,
                warp.Seed,
                warp.TargetEntityId,
                warp.Standing,
                warp.WarpId,
                warp.States.Append(future),
                $"public-forward:{currentTick}");
        });

        return CandidateWarpNormalization.CreateSet(
            candidateWarps.ScenarioId,
            candidateWarps.Seed,
            candidateWarps.TargetEntityId,
            projected);
    }
}

internal static class CandidateWarpNormalization
{
    public static CandidateWarpState CreateState(
        string scenarioId,
        int seed,
        string targetEntityId,
        PublicCandidateWarpStateDefinition definition) => CreateState(
            scenarioId,
            seed,
            targetEntityId,
            definition.TimeWindow,
            definition.TemporalKind,
            definition.RegionIds,
            definition.EvidenceBoundaryState,
            definition.Provenance);

    public static CandidateWarpState CreateState(
        string scenarioId,
        int seed,
        string targetEntityId,
        InteractionTimeWindow timeWindow,
        CandidateWarpTemporalKind temporalKind,
        IEnumerable<string> regionIds,
        EvidenceState evidenceBoundaryState,
        IEnumerable<CandidateWarpProvenance> provenance)
    {
        ArgumentNullException.ThrowIfNull(timeWindow);
        if (temporalKind == CandidateWarpTemporalKind.EventPoint &&
            timeWindow.StartTick != timeWindow.EndTick)
        {
            throw new ArgumentException(
                "An event-point candidate state must use an exact-tick time window.",
                nameof(timeWindow));
        }

        var regions = InteractionNormalization.RegionIds(regionIds, requireAtLeastOne: true);
        ArgumentNullException.ThrowIfNull(provenance);
        var orderedProvenance = provenance
            .Distinct()
            .OrderBy(item => item.SourceKind)
            .ThenBy(item => item.SourceIdentifier, StringComparer.Ordinal)
            .ThenBy(item => item.SourceObservationId, StringComparer.Ordinal)
            .ThenBy(item => item.StableIdentity(), StringComparer.Ordinal)
            .ToArray();
        if (orderedProvenance.Length == 0)
        {
            throw new ArgumentException("Candidate state provenance is required.", nameof(provenance));
        }


        if (orderedProvenance.Any(item =>
            item.Visibility != InteractionVisibility.PublicEvidence))
        {
            throw new ArgumentException(
                "Public candidate states require public evidence/model provenance.",
                nameof(provenance));
        }

        var stateId = InteractionStableIdentifier.Create(
            "cws",
            scenarioId,
            InteractionStableIdentifier.Seed(seed),
            targetEntityId,
            temporalKind.ToString(),
            InteractionStableIdentifier.Tick(timeWindow.StartTick),
            InteractionStableIdentifier.Tick(timeWindow.EndTick),
            evidenceBoundaryState.ToString(),
            string.Join("|", regions),
            string.Join("|", orderedProvenance.Select(item => item.StableIdentity())));
        return new CandidateWarpState(
            stateId,
            timeWindow,
            temporalKind,
            regions,
            evidenceBoundaryState,
            orderedProvenance);
    }

    public static CandidateWarp CreateWarp(
        string scenarioId,
        int seed,
        string targetEntityId,
        CandidateWarpStanding standing,
        string? parentWarpId,
        IEnumerable<CandidateWarpState> states,
        string identitySuffix)
    {
        var orderedStates = states
            .Distinct()
            .OrderBy(state => state.TimeWindow.StartTick)
            .ThenBy(state => state.TimeWindow.EndTick)
            .ThenBy(state => state.TemporalKind)
            .ThenBy(state => state.StateId, StringComparer.Ordinal)
            .ToArray();
        if (orderedStates.Length == 0)
        {
            throw new ArgumentException("A candidate warp must contain at least one state.", nameof(states));
        }

        var warpId = InteractionStableIdentifier.Create(
            "cwp",
            scenarioId,
            InteractionStableIdentifier.Seed(seed),
            targetEntityId,
            standing.ToString(),
            parentWarpId ?? "no-parent",
            identitySuffix,
            string.Join("|", orderedStates.Select(state => state.StateId)));
        return new CandidateWarp(
            warpId,
            scenarioId,
            seed,
            targetEntityId,
            standing,
            parentWarpId,
            orderedStates);
    }

    public static CandidateWarpSet CreateSet(
        string scenarioId,
        int seed,
        string targetEntityId,
        IEnumerable<CandidateWarp> warps)
    {
        var ordered = warps
            .GroupBy(warp => warp.WarpId, StringComparer.Ordinal)
            .Select(group => group.First())
            .OrderBy(warp => warp.Standing)
            .ThenBy(warp => warp.WarpId, StringComparer.Ordinal)
            .ToArray();
        if (ordered.Length == 0)
        {
            throw new ArgumentException("At least one candidate warp is required.", nameof(warps));
        }

        if (ordered.Any(warp =>
            !string.Equals(warp.ScenarioId, scenarioId, StringComparison.Ordinal) ||
            warp.Seed != seed ||
            !string.Equals(warp.TargetEntityId, targetEntityId, StringComparison.Ordinal)))
        {
            throw new ArgumentException("Candidate warp identifiers do not match their set.", nameof(warps));
        }

        return new CandidateWarpSet(scenarioId, seed, targetEntityId, ordered);
    }
}
