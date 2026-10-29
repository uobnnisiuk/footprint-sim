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
            .OrderBy(constraint => constraint.AvailableAtTick)
            .ThenBy(constraint => constraint.ConstrainedTimeWindow.StartTick)
            .ThenBy(constraint => constraint.ConstrainedTimeWindow.EndTick)
            .ThenBy(constraint => constraint.SourceObservationId, StringComparer.Ordinal)
            .ThenBy(constraint => constraint.ConstraintId, StringComparer.Ordinal)
            .ToArray();

        var currentSet = candidateWarps;
        var audits = new List<ConstraintApplicationAudit>();
        foreach (var sourceGroup in eligible
            .GroupBy(constraint => constraint.SourceObservationId, StringComparer.Ordinal)
            .OrderBy(group => group.Min(item => item.AvailableAtTick))
            .ThenBy(group => group.Min(item => item.ConstrainedTimeWindow.StartTick))
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
                output),
            Audits = audits
        };
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

        var constrainedWarp = BuildAppliedWarp(
            warp,
            constraint,
            accessContext,
            publicHazardEnvelope,
            narrowRegions: true,
            CandidateWarpStanding.Preferred,
            "constraint-applied");
        if (constraint.EvidenceBoundaryState == EvidenceState.Confirmed)
        {
            return new WarpApplication(
                [constrainedWarp],
                CreateAudit(
                    warp.WarpId,
                    constraint,
                    accessContext,
                    ConstraintApplicationDisposition.Narrow,
                    ConstraintApplicationReason.Applied,
                    beforeCount,
                    afterCount,
                    alternativesRetained: false,
                    conflictDetected: false));
        }

        var outerWarp = BuildAppliedWarp(
            warp,
            constraint,
            accessContext,
            publicHazardEnvelope,
            narrowRegions: false,
            CandidateWarpStanding.OuterPlausible,
            "outer-alternative");
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
        string identityPrefix)
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
                    .Intersect(constraint.AllowedRegionIds, StringComparer.Ordinal)
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
            var constrainedState = CandidateWarpNormalization.CreateState(
                warp.ScenarioId,
                warp.Seed,
                warp.TargetEntityId,
                intersection,
                state.TemporalKind,
                regions,
                narrowRegions
                    ? constraint.EvidenceBoundaryState
                    : state.EvidenceBoundaryState,
                provenance);
            history.Add(constrainedState);
            propagationSources.Add(constrainedState);
        }

        if (accessContext.CurrentTick > propagationSources.Max(state => state.TimeWindow.EndTick))
        {
            var propagated = PublicCandidateWarpPropagator.Propagate(
                warp.ScenarioId,
                warp.Seed,
                warp.TargetEntityId,
                propagationSources,
                accessContext.CurrentTick,
                publicHazardEnvelope);
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
        PublicHazardEnvelope? envelope)
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
                    depth: 0);
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
        int depth)
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
        if (branches.Any(branch => branch.TransportType == HazardTransportType.SecondaryTransport))
        {
            // A public secondary route is possible, not proof that release/re-transport occurred.
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
                depth + 1).Select(item => item with
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
