using FootprintSim.Evidence;
using FootprintSim.Hazards;

namespace FootprintSim.Interactions.CandidateWarps;

public sealed record ConstraintApplicationAudit
{
    internal ConstraintApplicationAudit(
        string auditId,
        string candidateWarpId,
        string constraintId,
        string sourceObservationId,
        InteractionRelationKind relationKind,
        string targetEntityId,
        long applicationTick,
        string queryContextId,
        InteractionTimeWindow constrainedTimeWindow,
        InteractionTimeWindow sourceObservedTimeWindow,
        ConstraintTemporalKind constraintTemporalKind,
        IReadOnlyList<InteractionParticipant> sourceParticipants,
        EvidenceState evidenceBoundaryState,
        ConstraintApplicationDisposition disposition,
        ConstraintApplicationReason reason,
        int candidateCountBefore,
        int candidateCountAfter,
        bool alternativesRetained,
        bool conflictDetected,
        IReadOnlyList<string> sourceAncestryIds,
        IReadOnlyList<InteractionProvenance> provenance)
    {
        AuditId = auditId;
        CandidateWarpId = candidateWarpId;
        ConstraintId = constraintId;
        SourceObservationId = sourceObservationId;
        RelationKind = relationKind;
        TargetEntityId = targetEntityId;
        ApplicationTick = applicationTick;
        QueryContextId = queryContextId;
        ConstrainedTimeWindow = constrainedTimeWindow;
        SourceObservedTimeWindow = sourceObservedTimeWindow;
        ConstraintTemporalKind = constraintTemporalKind;
        SourceParticipants = sourceParticipants;
        EvidenceBoundaryState = evidenceBoundaryState;
        Disposition = disposition;
        Reason = reason;
        CandidateCountBefore = candidateCountBefore;
        CandidateCountAfter = candidateCountAfter;
        AlternativesRetained = alternativesRetained;
        ConflictDetected = conflictDetected;
        SourceAncestryIds = sourceAncestryIds;
        Provenance = provenance;
    }

    public string AuditId { get; }

    public string CandidateWarpId { get; }

    public string ConstraintId { get; }

    public string SourceObservationId { get; }

    public InteractionRelationKind RelationKind { get; }

    public string TargetEntityId { get; }

    public long ApplicationTick { get; }

    public string QueryContextId { get; }

    public InteractionTimeWindow ConstrainedTimeWindow { get; }

    public InteractionTimeWindow SourceObservedTimeWindow { get; }

    public ConstraintTemporalKind ConstraintTemporalKind { get; }

    public IReadOnlyList<InteractionParticipant> SourceParticipants { get; }

    public EvidenceState EvidenceBoundaryState { get; }

    public ConstraintApplicationDisposition Disposition { get; }

    public ConstraintApplicationReason Reason { get; }

    public int CandidateCountBefore { get; }

    public int CandidateCountAfter { get; }

    public bool AlternativesRetained { get; }

    public bool ConflictDetected { get; }

    public IReadOnlyList<string> SourceAncestryIds { get; }

    public IReadOnlyList<InteractionProvenance> Provenance { get; }
}

public sealed record CandidateWarpApplicationResult
{
    public required CandidateWarpSet CandidateWarps { get; init; }

    public IReadOnlyList<ConstraintApplicationAudit> Audits { get; init; } = [];
}

public sealed class CrossThreadConstraintApplicator
{
    public CandidateWarpApplicationResult Apply(
        CandidateWarpSet candidateWarps,
        IEnumerable<CrossThreadConstraint> constraints,
        CandidateWarpAccessContext accessContext,
        PublicHazardEnvelope? publicHazardEnvelope)
    {
        ArgumentNullException.ThrowIfNull(candidateWarps);
        ArgumentNullException.ThrowIfNull(constraints);
        ArgumentNullException.ThrowIfNull(accessContext);
        Validate(candidateWarps, accessContext);

        var eligible = constraints
            .Where(constraint =>
                string.Equals(
                    constraint.ScenarioId,
                    accessContext.ScenarioId,
                    StringComparison.Ordinal) &&
                constraint.Seed == accessContext.Seed &&
                string.Equals(
                    constraint.TargetEntityId,
                    accessContext.TargetEntityId,
                    StringComparison.Ordinal) &&
                constraint.Visibility == InteractionVisibility.PublicEvidence &&
                constraint.AvailableAtTick <= accessContext.CurrentTick)
            // At a fixed receipt cutoff, observation chronology rather than
            // delivery order determines how historical constraints compose.
            .OrderBy(constraint => constraint.ConstrainedTimeWindow.StartTick)
            .ThenBy(constraint => constraint.ConstrainedTimeWindow.EndTick)
            .ThenBy(constraint => constraint.AvailableAtTick)
            .ThenBy(constraint => constraint.SourceObservationId, StringComparer.Ordinal)
            .ThenBy(constraint => constraint.ConstraintId, StringComparer.Ordinal)
            .ToArray();

        var currentSet = candidateWarps;
        var audits = new List<ConstraintApplicationAudit>();
        foreach (var sourceGroup in eligible
            .GroupBy(constraint => constraint.SourceObservationId, StringComparer.Ordinal)
            .OrderBy(group => group.Min(item => item.ConstrainedTimeWindow.StartTick))
            .ThenBy(group => group.Min(item => item.ConstrainedTimeWindow.EndTick))
            .ThenBy(group => group.Key, StringComparer.Ordinal))
        {
            var orderedSourceConstraints = sourceGroup
                .OrderBy(constraint => constraint.ConstraintId, StringComparer.Ordinal)
                .ToArray();
            var primary = orderedSourceConstraints[0];
            var applied = ApplySingle(
                currentSet,
                primary,
                accessContext,
                publicHazardEnvelope);
            currentSet = applied.CandidateWarps;
            audits.AddRange(applied.Audits);

            if (orderedSourceConstraints.Length > 1)
            {
                foreach (var warp in currentSet.Warps)
                {
                    var count = warp.CandidateRegionIdsAt(accessContext.CurrentTick).Count;
                    audits.Add(CreateAudit(
                        warp.WarpId,
                        orderedSourceConstraints[1],
                        accessContext,
                        ConstraintApplicationDisposition.NoOp,
                        ConstraintApplicationReason.DuplicateSourceObservation,
                        count,
                        count,
                        alternativesRetained: true,
                        conflictDetected: false));
                }
            }
        }

        return new CandidateWarpApplicationResult
        {
            CandidateWarps = currentSet,
            Audits = audits
                .DistinctBy(audit => audit.AuditId, StringComparer.Ordinal)
                .OrderBy(audit => audit.ApplicationTick)
                .ThenBy(audit => audit.ConstrainedTimeWindow.StartTick)
                .ThenBy(audit => audit.ConstrainedTimeWindow.EndTick)
                .ThenBy(audit => audit.SourceObservationId, StringComparer.Ordinal)
                .ThenBy(audit => audit.ConstraintId, StringComparer.Ordinal)
                .ThenBy(audit => audit.CandidateWarpId, StringComparer.Ordinal)
                .ThenBy(audit => audit.Disposition)
                .ToArray()
        };
    }

    private static CandidateWarpApplicationResult ApplySingle(
        CandidateWarpSet candidateWarps,
        CrossThreadConstraint constraint,
        CandidateWarpAccessContext accessContext,
        PublicHazardEnvelope? publicHazardEnvelope)
    {
        var output = new List<CandidateWarp>();
        var audits = new List<ConstraintApplicationAudit>();
        foreach (var warp in candidateWarps.Warps)
        {
            var result = ApplyToWarp(
                warp,
                constraint,
                accessContext,
                publicHazardEnvelope);
            output.AddRange(result.Warps);
            audits.Add(result.Audit);
        }

        return new CandidateWarpApplicationResult
        {
            CandidateWarps = CandidateWarpNormalization.CreateSet(
                candidateWarps.ScenarioId,
                candidateWarps.Seed,
                candidateWarps.TargetEntityId,
                CollapseOuterAlternatives(output)),
            Audits = audits
        };
    }

    // A separate warp per hypothetical unobserved stop makes a series of
    // received encounters exponential. Outer alternatives may be unioned
    // conservatively: their state provenance is preserved, but their
    // incompatible histories are NEVER promoted to a preferred path.
    private static IReadOnlyList<CandidateWarp> CollapseOuterAlternatives(
        IReadOnlyList<CandidateWarp> warps)
    {
        var outer = warps.Where(warp =>
            warp.Standing == CandidateWarpStanding.OuterPlausible)
            .OrderBy(warp => warp.WarpId, StringComparer.Ordinal)
            .ToArray();
        if (outer.Length < 2)
        {
            return warps;
        }

        var history = outer.SelectMany(warp => warp.States)
            .GroupBy(state => state.StateId, StringComparer.Ordinal)
            .Select(group => group.First())
            .ToArray();
        var union = CandidateWarpNormalization.CreateWarp(
            outer[0].ScenarioId,
            outer[0].Seed,
            outer[0].TargetEntityId,
            CandidateWarpStanding.OuterPlausible,
            parentWarpId: null,
            history,
            "outer-conservative-union");
        return warps.Where(warp =>
            warp.Standing != CandidateWarpStanding.OuterPlausible)
            .Append(union)
            .ToArray();
    }

    private static WarpApplication ApplyToWarp(
        CandidateWarp warp,
        CrossThreadConstraint constraint,
        CandidateWarpAccessContext accessContext,
        PublicHazardEnvelope? publicHazardEnvelope)
    {
        var overlappingStates = warp.States
            .Where(state => Overlaps(state.TimeWindow, constraint.ConstrainedTimeWindow))
            .ToArray();
        var beforeCount = overlappingStates
            .SelectMany(state => state.RegionIds)
            .Distinct(StringComparer.Ordinal)
            .Count();
        if (overlappingStates.Length == 0)
        {
            return NoOp(
                warp,
                constraint,
                accessContext,
                ConstraintApplicationReason.NoTemporalOverlap,
                beforeCount);
        }

        if (!constraint.IsPersonEvidence || constraint.IsDeviceOnlyEvidence)
        {
            return NoOp(
                warp,
                constraint,
                accessContext,
                ConstraintApplicationReason.DeviceOnlyEvidenceCannotConstrainPerson,
                beforeCount);
        }

        if (constraint.EvidenceBoundaryState is EvidenceState.Indeterminate or
            EvidenceState.UnsupportedForQuery)
        {
            return NoOp(
                warp,
                constraint,
                accessContext,
                ConstraintApplicationReason.EvidenceStateDoesNotConstrain,
                beforeCount);
        }

        if (constraint.EvidenceBoundaryState == EvidenceState.Conflicting)
        {
            var conflicted = CandidateWarpNormalization.CreateWarp(
                warp.ScenarioId,
                warp.Seed,
                warp.TargetEntityId,
                CandidateWarpStanding.Conflicted,
                warp.WarpId,
                warp.States,
                $"conflict:{constraint.ConstraintId}");
            return new WarpApplication(
                [conflicted],
                CreateAudit(
                    warp.WarpId,
                    constraint,
                    accessContext,
                    ConstraintApplicationDisposition.MarkConflict,
                    ConstraintApplicationReason.EvidenceStateDoesNotConstrain,
                    beforeCount,
                    beforeCount,
                    alternativesRetained: true,
                    conflictDetected: true));
        }

        var allowed = constraint.AllowedRegionIds.ToHashSet(StringComparer.Ordinal);
        var afterCount = overlappingStates
            .SelectMany(state => state.RegionIds)
            .Where(allowed.Contains)
            .Distinct(StringComparer.Ordinal)
            .Count();
        if (afterCount == 0)
        {
            if (constraint.EvidenceBoundaryState == EvidenceState.Confirmed)
            {
                var conflicted = CandidateWarpNormalization.CreateWarp(
                    warp.ScenarioId,
                    warp.Seed,
                    warp.TargetEntityId,
                    CandidateWarpStanding.Conflicted,
                    warp.WarpId,
                    warp.States,
                    $"empty-intersection:{constraint.ConstraintId}");
                return new WarpApplication(
                    [conflicted],
                    CreateAudit(
                        warp.WarpId,
                        constraint,
                        accessContext,
                        ConstraintApplicationDisposition.MarkConflict,
                        ConstraintApplicationReason.NoRegionOverlap,
                        beforeCount,
                        beforeCount,
                        alternativesRetained: true,
                        conflictDetected: true));
            }

            return new WarpApplication(
                [warp],
                CreateAudit(
                    warp.WarpId,
                    constraint,
                    accessContext,
                    ConstraintApplicationDisposition.RetainAlternative,
                    ConstraintApplicationReason.NoRegionOverlap,
                    beforeCount,
                    beforeCount,
                    alternativesRetained: true,
                    conflictDetected: false));
        }

        // The preceding encounter can have several time/location states.
        // Choosing its latest state would mistake one possible encounter
        // alternative for a mandatory waypoint. Group its states by original
        // observation identity, not by the last state alone.
        var precedingObservations = warp.States
            .Where(state => state.TimeWindow.EndTick <
                constraint.ConstrainedTimeWindow.StartTick)
            .SelectMany(state => state.Provenance
                .Where(source =>
                    source.SourceKind == CandidateWarpSourceKind.CrossThreadObservation &&
                    source.EvidenceBoundaryState is
                        EvidenceState.Confirmed or EvidenceState.SupportedButUncertain)
                .Select(source => new
                {
                    State = state,
                    SourceId = source.SourceObservationId ?? source.SourceIdentifier,
                    SourceEndTick = source.SourceTimeWindow?.EndTick ??
                        state.TimeWindow.EndTick
                }))
            .ToArray();
        var latestEncounter = precedingObservations
            .OrderByDescending(item => item.SourceEndTick)
            .ThenByDescending(item => item.State.TimeWindow.EndTick)
            .ThenBy(item => item.SourceId, StringComparer.Ordinal)
            .FirstOrDefault();
        var previousStates = latestEncounter is null
            ? Array.Empty<CandidateWarpState>()
            : precedingObservations
                .Where(item => string.Equals(
                    item.SourceId, latestEncounter.SourceId, StringComparison.Ordinal))
                .Select(item => item.State)
                .DistinctBy(state => state.StateId, StringComparer.Ordinal)
                .OrderBy(state => state.TimeWindow.StartTick)
                .ThenBy(state => state.StateId, StringComparer.Ordinal)
                .ToArray();

        // A directly received, confirmed exact observation at the query
        // tick stands on its own. It need not prove an earlier public path.
        var directCurrentConfirmation =
            constraint.EvidenceBoundaryState == EvidenceState.Confirmed &&
            constraint.ConstrainedTimeWindow.StartTick == accessContext.CurrentTick &&
            constraint.ConstrainedTimeWindow.EndTick == accessContext.CurrentTick;
        var verifiedRegionIds = overlappingStates
            .SelectMany(state => state.RegionIds)
            .Where(allowed.Contains)
            .ToHashSet(StringComparer.Ordinal);
        var unverifiedRegionIds = new HashSet<string>(StringComparer.Ordinal);
        var verifiedStartTicks = new Dictionary<string, long>(StringComparer.Ordinal);
        if (constraint.EvidenceBoundaryState is
            EvidenceState.Confirmed or EvidenceState.SupportedButUncertain &&
            previousStates.Length > 0 && !directCurrentConfirmation)
        {
            // A verified region also needs a verified encounter TIME.
            // Reaching a region at the END of an inclusive observation
            // window does not authorize forward projection from its start.
            unverifiedRegionIds.UnionWith(verifiedRegionIds);
            if (publicHazardEnvelope is null)
            {
                // A hypothetical stay at the previous region is allowed
                // as a preferred *model assumption*, not a fact established
                // by an ambiguous encounter. Other pedestrian moves without
                // published edges remain outer-only, never hard-excluded.
                verifiedRegionIds.IntersectWith(previousStates.SelectMany(state => state.RegionIds));
            }
            else
            {
                var lastEncounterTick = Math.Min(
                    constraint.ConstrainedTimeWindow.EndTick,
                    accessContext.CurrentTick);
                foreach (var regionId in verifiedRegionIds.ToArray())
                {
                    var reachedAtEnd = PublicCandidateWarpPropagator.Propagate(
                        warp.ScenarioId, warp.Seed, warp.TargetEntityId,
                        previousStates, lastEncounterTick,
                        publicHazardEnvelope, retainUndeclaredStay: true);
                    if (!reachedAtEnd.RegionIds.Contains(regionId, StringComparer.Ordinal))
                    {
                        verifiedRegionIds.Remove(regionId);
                        continue;
                    }

                    // Public reachability with undeclared stay is monotone
                    // in the time bound. Binary search avoids enumerating
                    // every tick of a potentially long interval.
                    var first = constraint.ConstrainedTimeWindow.StartTick;
                    var last = lastEncounterTick;
                    while (first < last)
                    {
                        var mid = first + (last - first) / 2;
                        var reached = PublicCandidateWarpPropagator.Propagate(
                            warp.ScenarioId, warp.Seed, warp.TargetEntityId,
                            previousStates, mid,
                            publicHazardEnvelope, retainUndeclaredStay: true);
                        if (reached.RegionIds.Contains(regionId, StringComparer.Ordinal))
                        {
                            last = mid;
                        }
                        else
                        {
                            first = mid + 1;
                        }
                    }

                    verifiedStartTicks[regionId] = first;
                }
            }

            // A region and a public arrival time must coincide in the SAME
            // historical state within the inclusive receipt window. A b@1
            // state does not certify b@[1,2] when the public route first
            // reaches b at tick 2. Keep such alternatives outer-only.
            foreach (var (regionId, firstTick) in verifiedStartTicks.ToArray())
            {
                var hasTimeConsistentState = overlappingStates.Any(state =>
                {
                    if (!state.RegionIds.Contains(regionId, StringComparer.Ordinal))
                    {
                        return false;
                    }

                    var firstPossibleTick = Math.Max(
                        Math.Max(state.TimeWindow.StartTick,
                            constraint.ConstrainedTimeWindow.StartTick),
                        firstTick);
                    var lastPossibleTick = Math.Min(
                        Math.Min(state.TimeWindow.EndTick,
                            constraint.ConstrainedTimeWindow.EndTick),
                        accessContext.CurrentTick);
                    return firstPossibleTick <= lastPossibleTick;
                });
                if (!hasTimeConsistentState)
                {
                    verifiedRegionIds.Remove(regionId);
                    verifiedStartTicks.Remove(regionId);
                }
            }

            unverifiedRegionIds.ExceptWith(verifiedRegionIds);
        }

        if (verifiedRegionIds.Count == 0)
        {
            // An unpublished link is unknown rather than impossible.
            var unknown = BuildAppliedWarp(
                warp, constraint, accessContext, publicHazardEnvelope,
                narrowRegions: true,
                CandidateWarpStanding.OuterPlausible,
                "unverified-public-link", retainUndeclaredStay: true,
                constrainedRegions: unverifiedRegionIds);
            return new WarpApplication(
                [unknown],
                CreateAudit(
                    warp.WarpId, constraint, accessContext,
                    ConstraintApplicationDisposition.RetainAlternative,
                    ConstraintApplicationReason.Applied,
                    beforeCount, afterCount,
                    alternativesRetained: true, conflictDetected: false));
        }

        // A directly confirmed current location can be preferred WITHOUT
        // promoting an unverified outer-only historical route. Warp standing
        // applies to every tick, so the new preferred anchor has no past.
        if (directCurrentConfirmation &&
            warp.Standing == CandidateWarpStanding.OuterPlausible &&
            previousStates.Length > 0)
        {
            var currentState = CandidateWarpNormalization.CreateState(
                warp.ScenarioId, warp.Seed, warp.TargetEntityId,
                InteractionTimeWindow.At(accessContext.CurrentTick),
                CandidateWarpTemporalKind.EventPoint,
                verifiedRegionIds, EvidenceState.Confirmed,
                [CreateCrossThreadProvenance(constraint)]);
            var currentAnchor = CandidateWarpNormalization.CreateWarp(
                warp.ScenarioId, warp.Seed, warp.TargetEntityId,
                CandidateWarpStanding.Preferred, parentWarpId: null,
                [currentState], $"direct-current:{constraint.ConstraintId}");
            // Current confirmation cannot erase an earlier received fact.
            // Keep its past trajectory outer-only even with just one earlier
            // confirmed original; the preferred current anchor remains past-free.
            var distinctPastReceipts = warp.States
                .Where(state => state.TimeWindow.EndTick < accessContext.CurrentTick)
                .SelectMany(state => state.Provenance)
                .Where(source =>
                    source.SourceKind == CandidateWarpSourceKind.CrossThreadObservation &&
                    source.EvidenceBoundaryState == EvidenceState.Confirmed)
                .Select(source => source.SourceObservationId ?? source.SourceIdentifier)
                .Distinct(StringComparer.Ordinal)
                .Take(2)
                .Count();
            var retainPastOuter = distinctPastReceipts >= 1;
            var anchoredWarps = new List<CandidateWarp> { currentAnchor };
            if (retainPastOuter)
            {
                anchoredWarps.Add(BuildAppliedWarp(
                    warp, constraint, accessContext, publicHazardEnvelope,
                    narrowRegions: true, CandidateWarpStanding.OuterPlausible,
                    "direct-current-unverified-history",
                    retainUndeclaredStay: true,
                    constrainedRegions: verifiedRegionIds));
            }

            return new WarpApplication(
                anchoredWarps,
                CreateAudit(
                    warp.WarpId, constraint, accessContext,
                    ConstraintApplicationDisposition.Narrow,
                    ConstraintApplicationReason.Applied,
                    beforeCount, afterCount,
                    alternativesRetained: retainPastOuter,
                    conflictDetected: false));
        }

        // A previous outer-only or conflicted alternative must not become
        // preferred merely because another observation arrived.
        var standing = previousStates.Length > 0 &&
            warp.Standing != CandidateWarpStanding.Preferred &&
            !directCurrentConfirmation
            ? warp.Standing
            : CandidateWarpStanding.Preferred;
        var constrainedWarp = BuildAppliedWarp(
            warp,
            constraint,
            accessContext,
            publicHazardEnvelope,
            narrowRegions: true,
            standing,
            "constraint-applied",
            constrainedRegions: verifiedRegionIds,
            verifiedStartTicks: verifiedStartTicks);
        if (constraint.EvidenceBoundaryState == EvidenceState.Confirmed)
        {
            // A historical confirmation can narrow the past, but a published
            // outgoing branch cannot prove an actual departure. Preserve the
            // undeclared stopping possibility as outer-only, not preferred.
            var withUnobservedStay = BuildAppliedWarp(
                warp,
                constraint,
                accessContext,
                publicHazardEnvelope,
                narrowRegions: true,
                CandidateWarpStanding.OuterPlausible,
                "unobserved-stay",
                retainUndeclaredStay: true,
                constrainedRegions: verifiedRegionIds);
            var hasExtraOuterRegion = withUnobservedStay
                .CandidateRegionIdsAt(accessContext.CurrentTick)
                .Except(constrainedWarp.CandidateRegionIdsAt(accessContext.CurrentTick),
                    StringComparer.Ordinal)
                .Any();
            var resultWarps = new List<CandidateWarp> { constrainedWarp };
            // The unverified EARLIER part of a received interval remains
            // possible even if the same regions are reachable at its end.
            var hasUnverifiedEncounterTime = verifiedStartTicks.Any(item =>
                item.Value > constraint.ConstrainedTimeWindow.StartTick);
            if (hasExtraOuterRegion || hasUnverifiedEncounterTime)
            {
                resultWarps.Add(withUnobservedStay);
            }

            if (unverifiedRegionIds.Count > 0)
            {
                // A path to b must never promote unverified d and its
                // descendants merely because both were in one observation.
                resultWarps.Add(BuildAppliedWarp(
                    warp, constraint, accessContext, publicHazardEnvelope,
                    narrowRegions: true,
                    CandidateWarpStanding.OuterPlausible,
                    "unverified-public-link", retainUndeclaredStay: true,
                    constrainedRegions: unverifiedRegionIds));
            }

            return new WarpApplication(
                resultWarps,
                CreateAudit(
                    warp.WarpId,
                    constraint,
                    accessContext,
                    ConstraintApplicationDisposition.Narrow,
                    ConstraintApplicationReason.Applied,
                    beforeCount,
                    afterCount,
                    alternativesRetained: resultWarps.Count > 1,
                    conflictDetected: false));
        }

        var outerWarp = BuildAppliedWarp(
            warp,
            constraint,
            accessContext,
            publicHazardEnvelope,
            narrowRegions: false,
            CandidateWarpStanding.OuterPlausible,
            "outer-alternative",
            retainUndeclaredStay: true);
        return new WarpApplication(
            [constrainedWarp, outerWarp],
            CreateAudit(
                warp.WarpId,
                constraint,
                accessContext,
                ConstraintApplicationDisposition.Branch,
                ConstraintApplicationReason.Applied,
                beforeCount,
                afterCount,
                alternativesRetained: true,
                conflictDetected: false));
    }

    private static CandidateWarp BuildAppliedWarp(
        CandidateWarp warp,
        CrossThreadConstraint constraint,
        CandidateWarpAccessContext accessContext,
        PublicHazardEnvelope? publicHazardEnvelope,
        bool narrowRegions,
        CandidateWarpStanding standing,
        string identityPrefix,
        bool retainUndeclaredStay = false,
        IReadOnlyCollection<string>? constrainedRegions = null,
        IReadOnlyDictionary<string, long>? verifiedStartTicks = null)
    {
        var history = new List<CandidateWarpState>();
        var propagationSources = new List<CandidateWarpState>();
        var crossThreadProvenance = CreateCrossThreadProvenance(constraint);
        foreach (var state in warp.States)
        {
            if (!TryIntersect(
                state.TimeWindow,
                constraint.ConstrainedTimeWindow,
                out var intersection))
            {
                if (state.TimeWindow.EndTick < constraint.ConstrainedTimeWindow.StartTick)
                {
                    history.Add(state);
                }
                else if (state.TimeWindow.StartTick >
                    constraint.ConstrainedTimeWindow.EndTick &&
                    state.TimeWindow.StartTick < accessContext.CurrentTick)
                {
                    // Preserve a later historical interval for future receipts,
                    // but never leave its query-tick tail beside a new projection.
                    var lastHistoricalTick = Math.Min(
                        state.TimeWindow.EndTick, accessContext.CurrentTick - 1);
                    if (state.TimeWindow.EndTick <= lastHistoricalTick)
                    {
                        history.Add(state);
                    }
                    else if (state.TimeWindow.StartTick <= lastHistoricalTick)
                    {
                        history.Add(CandidateWarpNormalization.CreateState(
                            warp.ScenarioId, warp.Seed, warp.TargetEntityId,
                            new InteractionTimeWindow(
                                state.TimeWindow.StartTick, lastHistoricalTick),
                            state.TemporalKind, state.RegionIds,
                            state.EvidenceBoundaryState, state.Provenance));
                    }
                }

                continue;
            }

            if (state.TimeWindow.StartTick < intersection.StartTick)
            {
                history.Add(CandidateWarpNormalization.CreateState(
                    warp.ScenarioId,
                    warp.Seed,
                    warp.TargetEntityId,
                    new InteractionTimeWindow(
                        state.TimeWindow.StartTick,
                        intersection.StartTick - 1),
                    state.TemporalKind,
                    state.RegionIds,
                    state.EvidenceBoundaryState,
                    state.Provenance));
            }

            var regions = narrowRegions
                ? state.RegionIds
                    .Intersect(constrainedRegions ?? constraint.AllowedRegionIds, StringComparer.Ordinal)
                    .OrderBy(regionId => regionId, StringComparer.Ordinal)
                    .ToArray()
                : state.RegionIds;
            if (regions.Count == 0)
            {
                continue;
            }

            var provenance = narrowRegions
                ? state.Provenance.Append(crossThreadProvenance)
                : state.Provenance;
            // Regions reached at different times must NOT share the full
            // encounter window as a propagation source. Group by the first
            // PUBLICLY verified tick and keep each time-bound separately.
            foreach (var group in regions.GroupBy(regionId =>
                verifiedStartTicks is not null &&
                verifiedStartTicks.TryGetValue(regionId, out var firstTick)
                    ? Math.Max(intersection.StartTick, firstTick)
                    : intersection.StartTick).OrderBy(group => group.Key))
            {
                if (group.Key > intersection.EndTick)
                {
                    continue;
                }

                var boundedWindow = new InteractionTimeWindow(
                    group.Key, intersection.EndTick);
                var constrainedState = CandidateWarpNormalization.CreateState(
                    warp.ScenarioId, warp.Seed, warp.TargetEntityId,
                    boundedWindow, state.TemporalKind,
                    group.ToArray(),
                    narrowRegions
                        ? constraint.EvidenceBoundaryState
                        : state.EvidenceBoundaryState,
                    provenance);
                history.Add(constrainedState);
                propagationSources.Add(constrainedState);
            }

            // A StateInterval is a set of possible regions at each tick,
            // not a declaration that a person remained in one region.
            // Retain the unconstrained *later* portion for the next receipt;
            // never carry a stale current-tick projection forward.
            if (intersection.EndTick < state.TimeWindow.EndTick &&
                intersection.EndTick < accessContext.CurrentTick)
            {
                var tailEnd = Math.Min(state.TimeWindow.EndTick,
                    accessContext.CurrentTick - 1);
                // The query tick is represented by the recomputed projection,
                // not a historical tail with start > end.
                if (intersection.EndTick + 1 <= tailEnd)
                {
                    history.Add(CandidateWarpNormalization.CreateState(
                        warp.ScenarioId, warp.Seed, warp.TargetEntityId,
                        new InteractionTimeWindow(intersection.EndTick + 1, tailEnd),
                        state.TemporalKind, state.RegionIds,
                        state.EvidenceBoundaryState, state.Provenance));
                }
            }
        }

        if (accessContext.CurrentTick > propagationSources.Max(state => state.TimeWindow.EndTick))
        {
            var propagated = PublicCandidateWarpPropagator.Propagate(
                warp.ScenarioId,
                warp.Seed,
                warp.TargetEntityId,
                propagationSources,
                accessContext.CurrentTick,
                publicHazardEnvelope,
                retainUndeclaredStay);
            history.Add(propagated);
        }

        return CandidateWarpNormalization.CreateWarp(
            warp.ScenarioId,
            warp.Seed,
            warp.TargetEntityId,
            standing,
            warp.WarpId,
            history,
            $"{identityPrefix}:{constraint.ConstraintId}");
    }

    private static CandidateWarpProvenance CreateCrossThreadProvenance(
        CrossThreadConstraint constraint) => CandidateWarpProvenance.Create(
            constraint.EvidenceBoundaryState == EvidenceState.Conflicting
                ? CandidateWarpSourceKind.ConflictingObservation
                : CandidateWarpSourceKind.CrossThreadObservation,
            constraint.ConstraintId,
            constraint.AvailableAtTick,
            constraint.IsPersonEvidence,
            constraint.IsDeviceOnlyEvidence,
            constraint.EvidenceBoundaryState,
            constraint.AllowedRegionIds,
            constraint.SourceRelationKind,
            constraint.SourceObservationId,
            constraint.SourceObservedTimeWindow,
            constraint.SourceParticipants,
            constraint.Provenance);

    private static WarpApplication NoOp(
        CandidateWarp warp,
        CrossThreadConstraint constraint,
        CandidateWarpAccessContext accessContext,
        ConstraintApplicationReason reason,
        int candidateCount)
    {
        return new WarpApplication(
            [warp],
            CreateAudit(
                warp.WarpId,
                constraint,
                accessContext,
                ConstraintApplicationDisposition.NoOp,
                reason,
                candidateCount,
                candidateCount,
                alternativesRetained: true,
                conflictDetected: false));
    }

    private static ConstraintApplicationAudit CreateAudit(
        string candidateWarpId,
        CrossThreadConstraint constraint,
        CandidateWarpAccessContext accessContext,
        ConstraintApplicationDisposition disposition,
        ConstraintApplicationReason reason,
        int candidateCountBefore,
        int candidateCountAfter,
        bool alternativesRetained,
        bool conflictDetected)
    {
        var provenance = constraint.Provenance
            .Distinct()
            .OrderBy(item => item.Kind)
            .ThenBy(item => item.SourceIdentifier, StringComparer.Ordinal)
            .ToArray();
        var ancestry = provenance
            .Select(item => item.SourceIdentifier)
            .Append(constraint.SourceObservationId)
            .Append(constraint.SourceEvidenceIdentifier)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(item => item, StringComparer.Ordinal)
            .ToArray();
        var temporalKind = constraint.ConstrainedTimeWindow.StartTick ==
            constraint.ConstrainedTimeWindow.EndTick
            ? ConstraintTemporalKind.ExactTick
            : ConstraintTemporalKind.InclusiveInterval;
        var auditId = InteractionStableIdentifier.Create(
            "cta",
            candidateWarpId,
            constraint.ConstraintId,
            InteractionStableIdentifier.Tick(accessContext.CurrentTick),
            accessContext.QueryContextId,
            disposition.ToString(),
            reason.ToString(),
            candidateCountBefore.ToString(System.Globalization.CultureInfo.InvariantCulture),
            candidateCountAfter.ToString(System.Globalization.CultureInfo.InvariantCulture),
            alternativesRetained.ToString(),
            conflictDetected.ToString());
        return new ConstraintApplicationAudit(
            auditId,
            candidateWarpId,
            constraint.ConstraintId,
            constraint.SourceObservationId,
            constraint.SourceRelationKind,
            constraint.TargetEntityId,
            accessContext.CurrentTick,
            accessContext.QueryContextId,
            constraint.ConstrainedTimeWindow,
            constraint.SourceObservedTimeWindow,
            temporalKind,
            constraint.SourceParticipants,
            constraint.EvidenceBoundaryState,
            disposition,
            reason,
            candidateCountBefore,
            candidateCountAfter,
            alternativesRetained,
            conflictDetected,
            ancestry,
            provenance);
    }

    private static void Validate(
        CandidateWarpSet candidateWarps,
        CandidateWarpAccessContext accessContext)
    {
        if (accessContext.CurrentTick < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(accessContext));
        }

        InteractionNormalization.RequireIdentifier(
            accessContext.QueryContextId,
            nameof(accessContext.QueryContextId));
        if (!string.Equals(
                candidateWarps.ScenarioId,
                accessContext.ScenarioId,
                StringComparison.Ordinal) ||
            candidateWarps.Seed != accessContext.Seed ||
            !string.Equals(
                candidateWarps.TargetEntityId,
                accessContext.TargetEntityId,
                StringComparison.Ordinal))
        {
            throw new ArgumentException("Candidate warp and access context identifiers do not match.");
        }
    }

    private static bool Overlaps(
        InteractionTimeWindow first,
        InteractionTimeWindow second) =>
        first.StartTick <= second.EndTick && second.StartTick <= first.EndTick;

    private static bool TryIntersect(
        InteractionTimeWindow first,
        InteractionTimeWindow second,
        out InteractionTimeWindow intersection)
    {
        var start = Math.Max(first.StartTick, second.StartTick);
        var end = Math.Min(first.EndTick, second.EndTick);
        if (end < start)
        {
            intersection = null!;
            return false;
        }

        intersection = new InteractionTimeWindow(start, end);
        return true;
    }

    private sealed record WarpApplication(
        IReadOnlyList<CandidateWarp> Warps,
        ConstraintApplicationAudit Audit);
}

internal static class PublicCandidateWarpPropagator
{
    public static CandidateWarpState Propagate(
        string scenarioId,
        int seed,
        string targetEntityId,
        IEnumerable<CandidateWarpState> sourceStates,
        long currentTick,
        PublicHazardEnvelope? envelope,
        bool retainUndeclaredStay = false)
    {
        var sources = sourceStates
            .OrderBy(state => state.TimeWindow.StartTick)
            .ThenBy(state => state.TimeWindow.EndTick)
            .ThenBy(state => state.StateId, StringComparer.Ordinal)
            .ToArray();
        var regions = new SortedSet<string>(StringComparer.Ordinal);
        var provenance = new List<CandidateWarpProvenance>();
        foreach (var state in sources)
        {
            foreach (var regionId in state.RegionIds)
            {
                var reached = Traverse(
                    regionId,
                    state.TimeWindow.StartTick,
                    state.TimeWindow.EndTick,
                    currentTick,
                    envelope,
                    depth: 0,
                    retainUndeclaredStay: retainUndeclaredStay);
                foreach (var item in reached)
                {
                    regions.Add(item.RegionId);
                    provenance.AddRange(state.Provenance);
                    provenance.AddRange(item.Provenance);
                }
            }
        }

        return CandidateWarpNormalization.CreateState(
            scenarioId,
            seed,
            targetEntityId,
            InteractionTimeWindow.At(currentTick),
            CandidateWarpTemporalKind.EventPoint,
            regions,
            EvidenceState.SupportedButUncertain,
            provenance);
    }

    private static IReadOnlyList<ReachedRegion> Traverse(
        string regionId,
        long earliestTick,
        long latestTick,
        long currentTick,
        PublicHazardEnvelope? envelope,
        int depth,
        bool retainUndeclaredStay)
    {
        if (envelope is null || depth > envelope.Branches.Count)
        {
            return [new ReachedRegion(regionId, [])];
        }

        var publicRegion = envelope.Regions.SingleOrDefault(region => string.Equals(
            region.RegionId,
            regionId,
            StringComparison.Ordinal));
        if (publicRegion is not null)
        {
            earliestTick = Math.Max(earliestTick, publicRegion.MinimumArrivalTick);
            latestTick = Math.Max(latestTick, publicRegion.MinimumArrivalTick);
        }

        var branches = envelope.Branches
            .Where(branch => branch.PublishedAtTick <= currentTick)
            .Where(branch => string.Equals(
                branch.SourceRegionId,
                regionId,
                StringComparison.Ordinal))
            .OrderBy(branch => branch.DestinationRegionId, StringComparer.Ordinal)
            .ThenBy(branch => branch.ProvenanceId, StringComparer.Ordinal)
            .ToArray();
        if (branches.Length == 0)
        {
            return [new ReachedRegion(regionId, [])];
        }

        var reached = new List<ReachedRegion>();
        if (retainUndeclaredStay ||
            branches.Any(branch => branch.TransportType == HazardTransportType.SecondaryTransport))
        {
            // A public departure is a possibility, not proof it occurred.
            // An unobserved stop adds no invented positive branch provenance.
            reached.Add(new ReachedRegion(regionId, []));
        }

        foreach (var branch in branches)
        {
            var arrivalEarliest = checked(earliestTick + branch.MinimumTravelTicks);
            var arrivalLatest = checked(latestTick + branch.MaximumTravelTicks);
            if (currentTick < arrivalEarliest)
            {
                reached.Add(new ReachedRegion(regionId, []));
                continue;
            }

            if (currentTick < arrivalLatest)
            {
                reached.Add(new ReachedRegion(regionId, []));
            }

            var branchProvenance = CandidateWarpProvenance.Create(
                CandidateWarpSourceKind.PublicHazardEnvelope,
                branch.ProvenanceId,
                branch.PublishedAtTick,
                isPersonEvidence: false,
                isDeviceOnlyEvidence: false,
                EvidenceState.SupportedButUncertain,
                [branch.DestinationRegionId]);
            reached.AddRange(Traverse(
                branch.DestinationRegionId,
                arrivalEarliest,
                arrivalLatest,
                currentTick,
                envelope,
                depth + 1,
                retainUndeclaredStay).Select(item => item with
                {
                    Provenance = item.Provenance.Append(branchProvenance).ToArray()
                }));
        }

        return reached
            .GroupBy(item => item.RegionId, StringComparer.Ordinal)
            .Select(group => new ReachedRegion(
                group.Key,
                group.SelectMany(item => item.Provenance)
                    .Distinct()
                    .OrderBy(item => item.SourceKind)
                    .ThenBy(item => item.SourceIdentifier, StringComparer.Ordinal)
                    .ToArray()))
            .OrderBy(item => item.RegionId, StringComparer.Ordinal)
            .ToArray();
    }

    private sealed record ReachedRegion(
        string RegionId,
        IReadOnlyList<CandidateWarpProvenance> Provenance);
}
