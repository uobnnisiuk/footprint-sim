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
        // A preferred trajectory may omit raw historical states that remain
        // on another outer branch. Never invent a preferred encounter in that
        // gap if some alternative already represents the historical window.
        // Such evidence requires an actual region/time intersection there.
        var hasHistoricalCoverage = candidateWarps.Warps.Any(candidate =>
            candidate.States.Any(state =>
                state.TimeWindow.StartTick < accessContext.CurrentTick &&
                Overlaps(state.TimeWindow, constraint.ConstrainedTimeWindow)));
        foreach (var warp in candidateWarps.Warps)
        {
            var result = ApplyToWarp(
                warp,
                constraint,
                accessContext,
                publicHazardEnvelope,
                hasHistoricalCoverage);
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

    // Keep outer history growth bounded when alternatives can be combined
    // without losing any of their per-warp historical time fallbacks.
    // A state-list union is NOT generally a union of trajectories: if one
    // branch has a@0,a@4 and another has a@0,b@2,b@4, the combined list
    // returns only b at tick 3 rather than the safe outer union {a,b}.
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

        // Combine only compatible branches. Distinct incompatible histories
        // must remain distinct, even though their standing is both outer.
        // Stable WarpId ordering keeps the grouping deterministic.
        var groups = new List<List<CandidateWarp>>();
        foreach (var warp in outer)
        {
            var combined = false;
            foreach (var group in groups)
            {
                var proposed = group.Append(warp).ToArray();
                var merged = UnionOuterStates(proposed);
                if (!PreservesOuterFallback(proposed, merged))
                {
                    continue;
                }

                group.Add(warp);
                combined = true;
                break;
            }

            if (!combined)
            {
                groups.Add([warp]);
            }
        }

        return warps.Where(warp =>
            warp.Standing != CandidateWarpStanding.OuterPlausible)
            .Concat(groups.Select(group => group.Count == 1
                ? group[0]
                : UnionOuterStates(group)))
            .ToArray();
    }

    private static CandidateWarp UnionOuterStates(
        IReadOnlyList<CandidateWarp> alternatives)
    {
        var first = alternatives[0];
        var history = alternatives.SelectMany(warp => warp.States)
            .GroupBy(state => state.StateId, StringComparer.Ordinal)
            .Select(group => group.First())
            .ToArray();
        return CandidateWarpNormalization.CreateWarp(
            first.ScenarioId, first.Seed, first.TargetEntityId,
            CandidateWarpStanding.OuterPlausible,
            parentWarpId: null, history, "outer-conservative-union");
    }

    private static bool PreservesOuterFallback(
        IReadOnlyList<CandidateWarp> alternatives, CandidateWarp merged)
    {
        // CandidateRegionIdsAt changes only at starts and immediately after
        // ends of state windows; between those boundaries, either the same
        // windows contain the tick or the same latest preceding states win.
        // Inspect boundaries without expanding an arbitrarily long window.
        var boundaries = alternatives.SelectMany(warp => warp.States)
            .SelectMany(state => state.TimeWindow.EndTick == long.MaxValue
                ? new[] { state.TimeWindow.StartTick, state.TimeWindow.EndTick }
                : new[] { state.TimeWindow.StartTick, state.TimeWindow.EndTick,
                    state.TimeWindow.EndTick + 1 })
            .Distinct()
            .OrderBy(tick => tick);
        foreach (var tick in boundaries)
        {
            var mergedRegions = merged.CandidateRegionIdsAt(tick)
                .ToHashSet(StringComparer.Ordinal);
            if (alternatives.Any(warp =>
                warp.CandidateRegionIdsAt(tick).Any(region =>
                    !mergedRegions.Contains(region))))
            {
                return false;
            }
        }

        return true;
    }

    private static WarpApplication ApplyToWarp(
        CandidateWarp warp,
        CrossThreadConstraint constraint,
        CandidateWarpAccessContext accessContext,
        PublicHazardEnvelope? publicHazardEnvelope,
        bool hasHistoricalCoverage)
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
            // Verifying a later raw EventPoint/interval may leave a sparse
            // preferred history (earlier receipt + query projection). A
            // subsequently received encounter inside that gap is not a
            // no-op proving the preferred path; keep it as unknown/outer.
            var crossesUnverifiedGap =
                warp.Standing == CandidateWarpStanding.Preferred &&
                constraint.IsPersonEvidence && !constraint.IsDeviceOnlyEvidence &&
                constraint.EvidenceBoundaryState is
                    EvidenceState.Confirmed or EvidenceState.SupportedButUncertain &&
                warp.States.Any(state =>
                    state.TimeWindow.EndTick < constraint.ConstrainedTimeWindow.StartTick &&
                    state.Provenance.Any(source =>
                        source.SourceKind == CandidateWarpSourceKind.CrossThreadObservation &&
                        source.EvidenceBoundaryState == EvidenceState.Confirmed)) &&
                warp.States.Any(state =>
                    state.TimeWindow.StartTick > constraint.ConstrainedTimeWindow.EndTick);
            if (crossesUnverifiedGap)
            {
                if (hasHistoricalCoverage)
                {
                    // Other outer alternatives hold original states in this
                    // window. A preferred warp's missing state is not a
                    // license to synthesize preferred evidence over them.
                    var retained = CandidateWarpNormalization.CreateWarp(
                        warp.ScenarioId, warp.Seed, warp.TargetEntityId,
                        CandidateWarpStanding.OuterPlausible, warp.WarpId,
                        warp.States, $"unverified-historical-gap:{constraint.ConstraintId}");
                    return new WarpApplication(
                        [retained],
                        CreateAudit(
                            warp.WarpId, constraint, accessContext,
                            ConstraintApplicationDisposition.RetainAlternative,
                            ConstraintApplicationReason.NoTemporalOverlap,
                            beforeCount, beforeCount,
                            alternativesRetained: true, conflictDetected: false));
                }

                // The lack of an original state at this tick is not evidence
                // that a later received encounter did not occur. Introduce
                // one provisional observation-time possibility, then use the
                // SAME public region/time checks as an overlapping encounter.
                // It is not a newly observed source or a declared path.
                var receiptWindow = new InteractionTimeWindow(
                    constraint.ConstrainedTimeWindow.StartTick,
                    Math.Min(constraint.ConstrainedTimeWindow.EndTick,
                        accessContext.CurrentTick));
                var provisionalState = CandidateWarpNormalization.CreateState(
                    warp.ScenarioId, warp.Seed, warp.TargetEntityId,
                    receiptWindow,
                    receiptWindow.StartTick == receiptWindow.EndTick
                        ? CandidateWarpTemporalKind.EventPoint
                        : CandidateWarpTemporalKind.StateInterval,
                    constraint.AllowedRegionIds,
                    constraint.EvidenceBoundaryState,
                    [CreateCrossThreadProvenance(constraint)]);
                var provisionalWarp = CandidateWarpNormalization.CreateWarp(
                    warp.ScenarioId, warp.Seed, warp.TargetEntityId,
                    warp.Standing, warp.WarpId,
                    warp.States.Append(provisionalState),
                    $"received-gap:{constraint.ConstraintId}");
                var checkedGap = ApplyToWarp(
                    provisionalWarp, constraint, accessContext,
                    publicHazardEnvelope, hasHistoricalCoverage: true);
                var gapWarps = checkedGap.Warps.ToList();
                if (constraint.EvidenceBoundaryState ==
                    EvidenceState.SupportedButUncertain)
                {
                    // An uncertain encounter can be wrong. Preserve the
                    // original path as a genuine unconstrained outer option.
                    gapWarps.Add(CandidateWarpNormalization.CreateWarp(
                        warp.ScenarioId, warp.Seed, warp.TargetEntityId,
                        CandidateWarpStanding.OuterPlausible, warp.WarpId,
                        warp.States, $"weak-gap-unknown:{constraint.ConstraintId}"));
                }

                return new WarpApplication(
                    gapWarps,
                    CreateAudit(
                        warp.WarpId, constraint, accessContext,
                        checkedGap.Audit.Disposition, checkedGap.Audit.Reason,
                        beforeCount, checkedGap.Audit.CandidateCountAfter,
                        alternativesRetained: gapWarps.Count > 1 ||
                            checkedGap.Audit.AlternativesRetained,
                        conflictDetected: checkedGap.Audit.ConflictDetected));
            }

            // A weak historical sighting can be false. When a later
            // independently confirmed receipt falls into the remaining
            // outer-only history gap, retain that new location as an outer
            // possibility rather than silently discarding the receipt.
            var earlierWeakReceipt = warp.States.Any(state =>
                state.Provenance.Any(source =>
                    source.SourceKind == CandidateWarpSourceKind.CrossThreadObservation &&
                    source.EvidenceBoundaryState == EvidenceState.SupportedButUncertain &&
                    source.SourceTimeWindow is { } weakWindow &&
                    weakWindow.StartTick < constraint.ConstrainedTimeWindow.StartTick));
            if (warp.Standing == CandidateWarpStanding.OuterPlausible &&
                !hasHistoricalCoverage && constraint.IsPersonEvidence &&
                !constraint.IsDeviceOnlyEvidence &&
                constraint.EvidenceBoundaryState == EvidenceState.Confirmed &&
                earlierWeakReceipt)
            {
                return RetainConfirmedAfterWeakGap(
                    warp, constraint, accessContext, publicHazardEnvelope, beforeCount);
            }

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

        // A later agreeing receipt cannot erase an earlier logical conflict.
        // Retain both conflicting locations independently of source-ID order.
        if (warp.Standing == CandidateWarpStanding.Conflicted &&
            constraint.EvidenceBoundaryState is
                EvidenceState.Confirmed or EvidenceState.SupportedButUncertain)
        {
            return RetainConflictedReceipt(
                warp, constraint, accessContext, publicHazardEnvelope,
                beforeCount, newConflict: false);
        }

        var allowed = constraint.AllowedRegionIds.ToHashSet(StringComparer.Ordinal);
        // Compare ORIGINAL receipt regions, not a projected/publicly
        // reachable region that happens to overlap the later interval.
        // Two different inclusive region sets may have different witness
        // ticks even when they share a region: {a,b} and {b,c} need not both
        // be witnessed at b. Preserve their alternatives outer-only until
        // joint timing and movement have actually been verified.
        var otherInclusiveReceipt =
            constraint.ConstrainedTimeWindow.StartTick <
                constraint.ConstrainedTimeWindow.EndTick &&
            constraint.ConstrainedTimeWindow.StartTick < accessContext.CurrentTick &&
            constraint.EvidenceBoundaryState is
                EvidenceState.Confirmed or EvidenceState.SupportedButUncertain &&
            overlappingStates.Any(state =>
                state.Provenance.Any(source =>
                    source.SourceKind ==
                        CandidateWarpSourceKind.CrossThreadObservation &&
                    source.EvidenceBoundaryState is
                        EvidenceState.Confirmed or EvidenceState.SupportedButUncertain &&
                    source.SourceTimeWindow is { } earlierWindow &&
                    earlierWindow.StartTick < earlierWindow.EndTick &&
                    Overlaps(state.TimeWindow, earlierWindow) &&
                    !source.RegionIds.ToHashSet(StringComparer.Ordinal)
                        .SetEquals(allowed) &&
                    !string.Equals(source.SourceObservationId,
                        constraint.SourceObservationId, StringComparison.Ordinal)));
        if (otherInclusiveReceipt)
        {
            return RetainOverlappingInclusiveWitnesses(
                warp, constraint, accessContext, publicHazardEnvelope,
                overlappingStates.SelectMany(state => state.RegionIds)
                    .Distinct(StringComparer.Ordinal).Count());
        }

        // An exact receipt need not be witnessed at the same tick as an
        // earlier inclusive encounter, even if their regions partially
        // overlap. Retain both witness alternatives rather than intersecting
        // their regions as though the interval were continuous residence.
        var exactWithinDifferentInclusive =
            constraint.ConstrainedTimeWindow.StartTick ==
                constraint.ConstrainedTimeWindow.EndTick &&
            constraint.EvidenceBoundaryState is
                EvidenceState.Confirmed or EvidenceState.SupportedButUncertain &&
            overlappingStates.Any(state => state.Provenance.Any(source =>
                source.SourceKind == CandidateWarpSourceKind.CrossThreadObservation &&
                source.EvidenceBoundaryState is
                    EvidenceState.Confirmed or EvidenceState.SupportedButUncertain &&
                source.SourceTimeWindow is { } window &&
                window.StartTick < window.EndTick &&
                window.StartTick <= constraint.ConstrainedTimeWindow.StartTick &&
                constraint.ConstrainedTimeWindow.EndTick <= window.EndTick &&
                !source.RegionIds.ToHashSet(StringComparer.Ordinal).SetEquals(allowed) &&
                // This new guard is only for *partially shared* sets. Fully
                // disjoint sets already use the existing exact/inclusive
                // witness handling, including publicly verified routes.
                source.RegionIds.Intersect(allowed, StringComparer.Ordinal).Any() &&
                !string.Equals(source.SourceObservationId,
                    constraint.SourceObservationId, StringComparison.Ordinal) &&
                (state.TimeWindow.StartTick <
                    constraint.ConstrainedTimeWindow.StartTick ||
                 state.TimeWindow.EndTick >
                    constraint.ConstrainedTimeWindow.EndTick))) &&
            // Two disjoint *exact* Confirmed receipts at this tick still
            // constitute a real conflict, regardless of an older interval.
            !overlappingStates.Any(state => state.Provenance.Any(source =>
                source.SourceKind == CandidateWarpSourceKind.CrossThreadObservation &&
                source.EvidenceBoundaryState == EvidenceState.Confirmed &&
                source.SourceTimeWindow is { } window &&
                window.StartTick == constraint.ConstrainedTimeWindow.StartTick &&
                window.EndTick == constraint.ConstrainedTimeWindow.EndTick &&
                !string.Equals(source.SourceObservationId,
                    constraint.SourceObservationId, StringComparison.Ordinal) &&
                !source.RegionIds.Intersect(allowed, StringComparer.Ordinal).Any()));
        if (exactWithinDifferentInclusive)
        {
            return RetainExactWithinEarlierInterval(
                warp, constraint, accessContext, publicHazardEnvelope, beforeCount);
        }

        // The converse ordering is also existential: an exact observation at
        // the inclusive start does not force its later witness to the overlap.
        var differentInclusiveAfterExact =
            constraint.ConstrainedTimeWindow.StartTick <
                constraint.ConstrainedTimeWindow.EndTick &&
            constraint.ConstrainedTimeWindow.StartTick < accessContext.CurrentTick &&
            constraint.EvidenceBoundaryState is
                EvidenceState.Confirmed or EvidenceState.SupportedButUncertain &&
            overlappingStates.Any(state => state.Provenance.Any(source =>
                source.SourceKind == CandidateWarpSourceKind.CrossThreadObservation &&
                source.EvidenceBoundaryState == EvidenceState.Confirmed &&
                source.SourceTimeWindow is { } window &&
                window.StartTick == window.EndTick &&
                window.StartTick == constraint.ConstrainedTimeWindow.StartTick &&
                !source.RegionIds.ToHashSet(StringComparer.Ordinal).SetEquals(allowed) &&
                // This new guard is only for *partially shared* sets. Fully
                // disjoint sets already use the existing exact/inclusive
                // witness handling, including publicly verified routes.
                source.RegionIds.Intersect(allowed, StringComparer.Ordinal).Any() &&
                !string.Equals(source.SourceObservationId,
                    constraint.SourceObservationId, StringComparison.Ordinal)));
        if (differentInclusiveAfterExact)
        {
            return RetainLaterInclusiveAfterExact(
                warp, constraint, accessContext, publicHazardEnvelope, beforeCount);
        }

        var afterCount = overlappingStates
            .SelectMany(state => state.RegionIds)
            .Where(allowed.Contains)
            .Distinct(StringComparer.Ordinal)
            .Count();
        if (afterCount == 0)
        {
            var exactReceipt = constraint.ConstrainedTimeWindow.StartTick ==
                constraint.ConstrainedTimeWindow.EndTick;
            var opposingExactConfirmed = exactReceipt &&
                overlappingStates.Any(state => state.Provenance.Any(source =>
                    source.SourceKind == CandidateWarpSourceKind.CrossThreadObservation &&
                    source.EvidenceBoundaryState == EvidenceState.Confirmed &&
                    source.SourceTimeWindow is { } window &&
                    window.StartTick == constraint.ConstrainedTimeWindow.StartTick &&
                    window.EndTick == constraint.ConstrainedTimeWindow.EndTick &&
                    !string.Equals(source.SourceObservationId,
                        constraint.SourceObservationId, StringComparison.Ordinal)));
            if (constraint.EvidenceBoundaryState == EvidenceState.Confirmed &&
                opposingExactConfirmed)
            {
                return RetainConflictedReceipt(
                    warp, constraint, accessContext, publicHazardEnvelope,
                    beforeCount, newConflict: true);
            }

            // An inclusive encounter is existential: a confirmed y@[2,4]
            // and a different confirmed d@3 can both be true (y at tick 2).
            // The overlap cannot establish continuous residence at y.
            var inclusiveAlternative = exactReceipt && !opposingExactConfirmed &&
                overlappingStates.Any(state => state.Provenance.Any(source =>
                    source.SourceKind == CandidateWarpSourceKind.CrossThreadObservation &&
                    source.EvidenceBoundaryState is
                        EvidenceState.Confirmed or EvidenceState.SupportedButUncertain &&
                    source.SourceTimeWindow is { } window &&
                    window.StartTick < window.EndTick &&
                    window.StartTick <= constraint.ConstrainedTimeWindow.StartTick &&
                    window.EndTick >= constraint.ConstrainedTimeWindow.EndTick &&
                    !string.Equals(source.SourceObservationId,
                        constraint.SourceObservationId, StringComparison.Ordinal) &&
                    (state.TimeWindow.StartTick < constraint.ConstrainedTimeWindow.StartTick ||
                     state.TimeWindow.EndTick > constraint.ConstrainedTimeWindow.EndTick)));
            if (constraint.EvidenceBoundaryState == EvidenceState.Confirmed &&
                inclusiveAlternative)
            {
                return RetainExactWithinEarlierInterval(
                    warp, constraint, accessContext, publicHazardEnvelope,
                    beforeCount);
            }

            // An exact confirmation at the START of a subsequent inclusive
            // encounter does not require residence there throughout its window.
            // The person may visit the different region at a later tick;
            // absent public movement edges make that link unknown, not false.
            var laterInclusiveAfterExact =
                constraint.ConstrainedTimeWindow.StartTick <
                    constraint.ConstrainedTimeWindow.EndTick &&
                constraint.ConstrainedTimeWindow.StartTick < accessContext.CurrentTick &&
                constraint.EvidenceBoundaryState is
                    EvidenceState.Confirmed or EvidenceState.SupportedButUncertain &&
                overlappingStates.Any(state =>
                    state.Provenance.Any(source =>
                        source.SourceKind ==
                            CandidateWarpSourceKind.CrossThreadObservation &&
                        source.EvidenceBoundaryState == EvidenceState.Confirmed &&
                        source.SourceTimeWindow is { } window &&
                        window.StartTick == window.EndTick &&
                        window.StartTick ==
                            constraint.ConstrainedTimeWindow.StartTick &&
                        !string.Equals(source.SourceObservationId,
                            constraint.SourceObservationId, StringComparison.Ordinal)));
            if (laterInclusiveAfterExact)
            {
                return RetainLaterInclusiveAfterExact(
                    warp, constraint, accessContext, publicHazardEnvelope,
                    beforeCount);
            }

            if (constraint.EvidenceBoundaryState == EvidenceState.Confirmed)
            {
                // The union of outer alternatives can contain only a
                // potentially false weak encounter at this tick. It is not
                // sound to turn that possible weak location into a mandatory
                // contradiction of a later independent Confirmed receipt.
                // Do not apply this exception to two disjoint Confirmed facts.
                var weakOnlyOverlap = warp.Standing ==
                    CandidateWarpStanding.OuterPlausible &&
                    overlappingStates.All(state =>
                        state.Provenance.Any(source =>
                            source.SourceKind ==
                                CandidateWarpSourceKind.CrossThreadObservation &&
                            source.EvidenceBoundaryState ==
                                EvidenceState.SupportedButUncertain &&
                            source.SourceTimeWindow is { } weakWindow &&
                            Overlaps(weakWindow, constraint.ConstrainedTimeWindow)) &&
                        !state.Provenance.Any(source =>
                            source.SourceKind ==
                                CandidateWarpSourceKind.CrossThreadObservation &&
                            source.EvidenceBoundaryState == EvidenceState.Confirmed &&
                            source.SourceTimeWindow is { } strongWindow &&
                            Overlaps(strongWindow, constraint.ConstrainedTimeWindow)));
                if (weakOnlyOverlap)
                {
                    return RetainConfirmedAfterWeakGap(
                        warp, constraint, accessContext, publicHazardEnvelope,
                        beforeCount);
                }

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

            // An inconsistent weak encounter is not proof of a public path,
            // but silently ignoring it can leave a prior preferred route
            // falsely confirmed. Demote that route to an outer alternative,
            // without excluding any regions or treating the weak event as true.
            var hadPriorConfirmedReceipt = warp.States.Any(state =>
                state.Provenance.Any(source =>
                    source.SourceKind == CandidateWarpSourceKind.CrossThreadObservation &&
                    source.EvidenceBoundaryState == EvidenceState.Confirmed &&
                    source.SourceTimeWindow?.EndTick <
                        constraint.ConstrainedTimeWindow.StartTick));
            var retained = warp.Standing == CandidateWarpStanding.Preferred &&
                hadPriorConfirmedReceipt
                ? CandidateWarpNormalization.CreateWarp(
                    warp.ScenarioId, warp.Seed, warp.TargetEntityId,
                    CandidateWarpStanding.OuterPlausible, warp.WarpId,
                    warp.States, $"weak-unknown:{constraint.ConstraintId}")
                : warp;
            return new WarpApplication(
                [retained],
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
        // An earlier receipt may overlap this inclusive encounter. It can
        // precede a later-time alternative even if its end equals the new
        // window's start. Read the ORIGINAL observed window; a projected
        // state carrying its provenance is not a second past encounter.
        var precedingObservations = warp.States
            .SelectMany(state => state.Provenance
                .Where(source =>
                    source.SourceKind == CandidateWarpSourceKind.CrossThreadObservation &&
                    source.EvidenceBoundaryState is
                        EvidenceState.Confirmed or EvidenceState.SupportedButUncertain &&
                    // A provisional received-gap state has this receipt's
                    // own source ID. It must never verify itself as an earlier
                    // published movement, for either exact or interval input.
                    !string.Equals(
                        source.SourceObservationId ?? source.SourceIdentifier,
                        constraint.SourceObservationId, StringComparison.Ordinal) &&
                    source.SourceTimeWindow is { } sourceWindow &&
                    sourceWindow.StartTick < constraint.ConstrainedTimeWindow.EndTick &&
                    Overlaps(state.TimeWindow, sourceWindow))
                .Select(source => new
                {
                    State = state,
                    SourceId = source.SourceObservationId ?? source.SourceIdentifier,
                    SourceWindow = source.SourceTimeWindow!
                }))
            .Where(item => item.State.TimeWindow.StartTick <
                constraint.ConstrainedTimeWindow.EndTick)
            .Select(item =>
            {
                var startTick = Math.Max(item.State.TimeWindow.StartTick,
                    item.SourceWindow.StartTick);
                var endTick = Math.Min(
                    Math.Min(item.State.TimeWindow.EndTick, item.SourceWindow.EndTick),
                    constraint.ConstrainedTimeWindow.EndTick - 1);
                return new
                {
                    State = CandidateWarpNormalization.CreateState(
                        warp.ScenarioId, warp.Seed, warp.TargetEntityId,
                        new InteractionTimeWindow(startTick, endTick),
                        item.State.TemporalKind, item.State.RegionIds,
                        item.State.EvidenceBoundaryState, item.State.Provenance),
                    item.SourceId,
                    SourceEndTick = item.SourceWindow.EndTick
                };
            })
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
                        publicHazardEnvelope, retainUndeclaredStay: true,
                        publicationCutoffTick: accessContext.CurrentTick);
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
                            publicHazardEnvelope, retainUndeclaredStay: true,
                        publicationCutoffTick: accessContext.CurrentTick);
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
            // An unpublished link is unknown rather than impossible, but
            // unknown reachability cannot resolve an earlier hard conflict.
            // Preserve the standing on BOTH weak/unknown alternatives.
            var retainedStanding = warp.Standing == CandidateWarpStanding.Conflicted
                ? CandidateWarpStanding.Conflicted
                : CandidateWarpStanding.OuterPlausible;
            var unknown = BuildAppliedWarp(
                warp, constraint, accessContext, publicHazardEnvelope,
                narrowRegions: true,
                retainedStanding,
                "unverified-public-link", retainUndeclaredStay: true,
                constrainedRegions: unverifiedRegionIds);
            var branches = new List<CandidateWarp> { unknown };
            // A weak encounter may be wrong. Its unverified public link
            // must NOT turn a hypothetical location into a hard exclusion:
            // retain the original, unconstrained outer path as a real branch.
            var retainWeakAlternative =
                constraint.EvidenceBoundaryState == EvidenceState.SupportedButUncertain;
            if (retainWeakAlternative)
            {
                branches.Add(BuildAppliedWarp(
                    warp, constraint, accessContext, publicHazardEnvelope,
                    narrowRegions: false,
                    retainedStanding,
                    "outer-alternative", retainUndeclaredStay: true));
            }

            return new WarpApplication(
                branches,
                CreateAudit(
                    warp.WarpId, constraint, accessContext,
                    retainWeakAlternative
                        ? ConstraintApplicationDisposition.Branch
                        : ConstraintApplicationDisposition.RetainAlternative,
                    ConstraintApplicationReason.Applied,
                    beforeCount, afterCount,
                    alternativesRetained: true,
                    conflictDetected: warp.Standing == CandidateWarpStanding.Conflicted));
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
            // One earlier confirmed receipt must survive as outer history
            // when the route to this direct current anchor is unverified.
            // A fully published link without an intervening weak encounter
            // does not require a duplicate outer path at the current tick.
            var hasUnverifiedIntermediate = warp.States
                .SelectMany(state => state.Provenance)
                .Any(source =>
                    source.SourceKind == CandidateWarpSourceKind.CrossThreadObservation &&
                    source.EvidenceBoundaryState == EvidenceState.SupportedButUncertain &&
                    // An inclusive encounter [t, current] also has a PAST
                    // portion. Ignoring it would erase earlier Confirmed history
                    // when creating a direct-current preferred anchor.
                    source.SourceTimeWindow?.StartTick < accessContext.CurrentTick);
            var hasPublicLinkToCurrent = publicHazardEnvelope is not null &&
                PublicCandidateWarpPropagator.Propagate(
                    warp.ScenarioId, warp.Seed, warp.TargetEntityId,
                    previousStates, accessContext.CurrentTick, publicHazardEnvelope,
                    retainUndeclaredStay: true,
                    publicationCutoffTick: accessContext.CurrentTick).RegionIds
                .Intersect(verifiedRegionIds, StringComparer.Ordinal)
                .Any();
            var retainPastOuter = distinctPastReceipts >= 2 ||
                (distinctPastReceipts >= 1 &&
                    (hasUnverifiedIntermediate || !hasPublicLinkToCurrent));
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
            // Raw later historical states are required for future receipts, but
            // cannot be presented as preferred history. Retain them on the
            // outer-only branch even if query-time region counts are equal.
            var hasRawHistoricalTail = warp.States.Any(state =>
                state.TimeWindow.EndTick > constraint.ConstrainedTimeWindow.EndTick &&
                Math.Max(state.TimeWindow.StartTick,
                    constraint.ConstrainedTimeWindow.EndTick + 1) <
                accessContext.CurrentTick);
            // The unverified EARLIER part of a received interval remains
            // possible even if the same regions are reachable at its end.
            var hasUnverifiedEncounterTime = verifiedStartTicks.Any(item =>
                item.Value > constraint.ConstrainedTimeWindow.StartTick);
            if (hasExtraOuterRegion || hasUnverifiedEncounterTime ||
                hasRawHistoricalTail)
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

    // Keep both exact locations and their ancestry; adding further
    // agreements is not a majority vote resolving a logical conflict.
    private static WarpApplication RetainConflictedReceipt(
        CandidateWarp warp, CrossThreadConstraint constraint,
        CandidateWarpAccessContext accessContext,
        PublicHazardEnvelope? publicHazardEnvelope,
        int beforeCount, bool newConflict)
    {
        var window = new InteractionTimeWindow(
            constraint.ConstrainedTimeWindow.StartTick,
            Math.Min(constraint.ConstrainedTimeWindow.EndTick,
                accessContext.CurrentTick));
        var observed = CandidateWarpNormalization.CreateState(
            warp.ScenarioId, warp.Seed, warp.TargetEntityId, window,
            window.StartTick == window.EndTick
                ? CandidateWarpTemporalKind.EventPoint
                : CandidateWarpTemporalKind.StateInterval,
            constraint.AllowedRegionIds, constraint.EvidenceBoundaryState,
            [CreateCrossThreadProvenance(constraint)]);
        var states = warp.States.Append(observed).ToList();
        if (window.EndTick < accessContext.CurrentTick)
        {
            states.Add(PublicCandidateWarpPropagator.Propagate(
                warp.ScenarioId, warp.Seed, warp.TargetEntityId,
                [observed], accessContext.CurrentTick, publicHazardEnvelope,
                retainUndeclaredStay: true,
                publicationCutoffTick: accessContext.CurrentTick));
        }

        var result = CandidateWarpNormalization.CreateWarp(
            warp.ScenarioId, warp.Seed, warp.TargetEntityId,
            CandidateWarpStanding.Conflicted, warp.WarpId, states,
            $"retained-conflict:{constraint.ConstraintId}");
        return new WarpApplication([result], CreateAudit(
            warp.WarpId, constraint, accessContext,
            newConflict ? ConstraintApplicationDisposition.MarkConflict
                : ConstraintApplicationDisposition.RetainAlternative,
            newConflict ? ConstraintApplicationReason.NoRegionOverlap
                : ConstraintApplicationReason.Applied,
            beforeCount, beforeCount, alternativesRetained: true,
            conflictDetected: true));
    }

    // Keep both inclusive witness windows and their ancestry in a
    // conservative OUTER union. This does not prove either actual route
    // or universal consistency of the two constraints.
    private static WarpApplication RetainOverlappingInclusiveWitnesses(
        CandidateWarp warp, CrossThreadConstraint constraint,
        CandidateWarpAccessContext accessContext,
        PublicHazardEnvelope? publicHazardEnvelope, int beforeCount)
    {
        var window = new InteractionTimeWindow(
            constraint.ConstrainedTimeWindow.StartTick,
            Math.Min(constraint.ConstrainedTimeWindow.EndTick,
                accessContext.CurrentTick));
        var witness = CandidateWarpNormalization.CreateState(
            warp.ScenarioId, warp.Seed, warp.TargetEntityId, window,
            CandidateWarpTemporalKind.StateInterval,
            constraint.AllowedRegionIds, constraint.EvidenceBoundaryState,
            [CreateCrossThreadProvenance(constraint)]);
        var history = warp.States.Append(witness).ToList();
        if (window.EndTick < accessContext.CurrentTick)
        {
            history.Add(PublicCandidateWarpPropagator.Propagate(
                warp.ScenarioId, warp.Seed, warp.TargetEntityId,
                [witness], accessContext.CurrentTick, publicHazardEnvelope,
                retainUndeclaredStay: true,
                publicationCutoffTick: accessContext.CurrentTick));
        }

        var retained = CandidateWarpNormalization.CreateWarp(
            warp.ScenarioId, warp.Seed, warp.TargetEntityId,
            CandidateWarpStanding.OuterPlausible, warp.WarpId, history,
            $"overlapping-inclusive-existential:{constraint.ConstraintId}");
        return new WarpApplication([retained], CreateAudit(
            warp.WarpId, constraint, accessContext,
            ConstraintApplicationDisposition.RetainAlternative,
            ConstraintApplicationReason.Applied, beforeCount,
            constraint.AllowedRegionIds.Count, alternativesRetained: true,
            conflictDetected: false));
    }

    // Two distinct confirmed facts can coexist at different ticks when one
    // has an inclusive witness window. Keep the earlier exact and the later
    // possible witness as an OUTER union, not a proven public movement or a
    // preferred trajectory. The original source window stays in provenance.
    private static WarpApplication RetainLaterInclusiveAfterExact(
        CandidateWarp warp, CrossThreadConstraint constraint,
        CandidateWarpAccessContext accessContext,
        PublicHazardEnvelope? publicHazardEnvelope, int beforeCount)
    {
        var possibleStart = constraint.ConstrainedTimeWindow.StartTick + 1;
        var possibleEnd = Math.Min(
            constraint.ConstrainedTimeWindow.EndTick, accessContext.CurrentTick);
        var possibleWindow = new InteractionTimeWindow(possibleStart, possibleEnd);
        var laterWitness = CandidateWarpNormalization.CreateState(
            warp.ScenarioId, warp.Seed, warp.TargetEntityId, possibleWindow,
            CandidateWarpTemporalKind.StateInterval,
            constraint.AllowedRegionIds, constraint.EvidenceBoundaryState,
            [CreateCrossThreadProvenance(constraint)]);
        var history = warp.States.Append(laterWitness).ToList();
        if (possibleEnd < accessContext.CurrentTick)
        {
            history.Add(PublicCandidateWarpPropagator.Propagate(
                warp.ScenarioId, warp.Seed, warp.TargetEntityId,
                [laterWitness], accessContext.CurrentTick, publicHazardEnvelope,
                retainUndeclaredStay: true,
                publicationCutoffTick: accessContext.CurrentTick));
        }

        var retained = CandidateWarpNormalization.CreateWarp(
            warp.ScenarioId, warp.Seed, warp.TargetEntityId,
            CandidateWarpStanding.OuterPlausible, warp.WarpId, history,
            $"later-inclusive-existential:{constraint.ConstraintId}");
        return new WarpApplication([retained], CreateAudit(
            warp.WarpId, constraint, accessContext,
            ConstraintApplicationDisposition.RetainAlternative,
            ConstraintApplicationReason.Applied, beforeCount,
            constraint.AllowedRegionIds.Count, alternativesRetained: true,
            conflictDetected: false));
    }

    // Preserve the different possible witness times of an interval in an
    // outer over-approximation, never in a preferred mandatory-stay history.
    private static WarpApplication RetainExactWithinEarlierInterval(
        CandidateWarp warp, CrossThreadConstraint constraint,
        CandidateWarpAccessContext accessContext,
        PublicHazardEnvelope? publicHazardEnvelope, int beforeCount)
    {
        var tick = constraint.ConstrainedTimeWindow.StartTick;
        var history = new List<CandidateWarpState>();
        foreach (var state in warp.States)
        {
            var intervalWitness = state.TimeWindow.StartTick <= tick &&
                tick <= state.TimeWindow.EndTick &&
                state.Provenance.Any(source =>
                    source.SourceKind == CandidateWarpSourceKind.CrossThreadObservation &&
                    source.EvidenceBoundaryState is
                        EvidenceState.Confirmed or EvidenceState.SupportedButUncertain &&
                    source.SourceTimeWindow is { } window &&
                    window.StartTick < window.EndTick &&
                    window.StartTick <= tick && tick <= window.EndTick &&
                    !string.Equals(source.SourceObservationId,
                        constraint.SourceObservationId, StringComparison.Ordinal));
            if (!intervalWitness)
            {
                history.Add(state);
                continue;
            }

            // Before/after are alternatives, not simultaneous locations.
            // The conservative union is outer-only and retains provenance.
            if (state.TimeWindow.StartTick < tick)
            {
                history.Add(CandidateWarpNormalization.CreateState(
                    warp.ScenarioId, warp.Seed, warp.TargetEntityId,
                    new InteractionTimeWindow(state.TimeWindow.StartTick, tick - 1),
                    state.TemporalKind, state.RegionIds,
                    state.EvidenceBoundaryState, state.Provenance));
            }

            if (tick < state.TimeWindow.EndTick)
            {
                history.Add(CandidateWarpNormalization.CreateState(
                    warp.ScenarioId, warp.Seed, warp.TargetEntityId,
                    new InteractionTimeWindow(tick + 1, state.TimeWindow.EndTick),
                    state.TemporalKind, state.RegionIds,
                    state.EvidenceBoundaryState, state.Provenance));
            }
        }

        var exact = CandidateWarpNormalization.CreateState(
            warp.ScenarioId, warp.Seed, warp.TargetEntityId,
            InteractionTimeWindow.At(tick), CandidateWarpTemporalKind.EventPoint,
            constraint.AllowedRegionIds, constraint.EvidenceBoundaryState,
            [CreateCrossThreadProvenance(constraint)]);
        history.Add(exact);
        if (tick < accessContext.CurrentTick)
        {
            history.Add(PublicCandidateWarpPropagator.Propagate(
                warp.ScenarioId, warp.Seed, warp.TargetEntityId,
                [exact], accessContext.CurrentTick, publicHazardEnvelope,
                retainUndeclaredStay: true,
                publicationCutoffTick: accessContext.CurrentTick));
        }

        var result = CandidateWarpNormalization.CreateWarp(
            warp.ScenarioId, warp.Seed, warp.TargetEntityId,
            CandidateWarpStanding.OuterPlausible, warp.WarpId, history,
            $"interval-existential:{constraint.ConstraintId}");
        // An uncertain exact report may be false. Preserve the original
        // candidate history alongside the reported witness.
        var alternatives = new List<CandidateWarp> { result };
        if (constraint.EvidenceBoundaryState ==
            EvidenceState.SupportedButUncertain)
        {
            alternatives.Add(CandidateWarpNormalization.CreateWarp(
                warp.ScenarioId, warp.Seed, warp.TargetEntityId,
                CandidateWarpStanding.OuterPlausible, warp.WarpId,
                warp.States, $"weak-exact-unknown:{constraint.ConstraintId}"));
        }

        return new WarpApplication(alternatives, CreateAudit(
            warp.WarpId, constraint, accessContext,
            ConstraintApplicationDisposition.RetainAlternative,
            ConstraintApplicationReason.Applied, beforeCount,
            constraint.AllowedRegionIds.Count, alternativesRetained: true,
            conflictDetected: false));
    }

    // An outer branch which has only a weak or absent history at this tick
    // must not reject a later independently received Confirmed observation.
    // Keep the new observation and its public/unknown forward possibilities
    // outer-only; this does not certify any link from the weak alternative.
    private static WarpApplication RetainConfirmedAfterWeakGap(
        CandidateWarp warp,
        CrossThreadConstraint constraint,
        CandidateWarpAccessContext accessContext,
        PublicHazardEnvelope? publicHazardEnvelope,
        int beforeCount)
    {
        var observedWindow = new InteractionTimeWindow(
            constraint.ConstrainedTimeWindow.StartTick,
            Math.Min(constraint.ConstrainedTimeWindow.EndTick,
                accessContext.CurrentTick));
        var confirmedState = CandidateWarpNormalization.CreateState(
            warp.ScenarioId, warp.Seed, warp.TargetEntityId, observedWindow,
            observedWindow.StartTick == observedWindow.EndTick
                ? CandidateWarpTemporalKind.EventPoint
                : CandidateWarpTemporalKind.StateInterval,
            constraint.AllowedRegionIds, EvidenceState.Confirmed,
            [CreateCrossThreadProvenance(constraint)]);
        var history = warp.States.Append(confirmedState).ToList();
        if (observedWindow.EndTick < accessContext.CurrentTick)
        {
            history.Add(PublicCandidateWarpPropagator.Propagate(
                warp.ScenarioId, warp.Seed, warp.TargetEntityId,
                [confirmedState], accessContext.CurrentTick,
                publicHazardEnvelope, retainUndeclaredStay: true,
                publicationCutoffTick: accessContext.CurrentTick));
        }

        var retained = CandidateWarpNormalization.CreateWarp(
            warp.ScenarioId, warp.Seed, warp.TargetEntityId,
            CandidateWarpStanding.OuterPlausible, warp.WarpId, history,
            $"confirmed-after-weak-unknown:{constraint.ConstraintId}");
        return new WarpApplication(
            [retained],
            CreateAudit(
                warp.WarpId, constraint, accessContext,
                ConstraintApplicationDisposition.RetainAlternative,
                ConstraintApplicationReason.Applied, beforeCount,
                constraint.AllowedRegionIds.Count,
                alternativesRetained: true, conflictDetected: false));
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
        // Raw later historical candidates (StateInterval AND EventPoint) are
        // outer possibilities until public reachability verifies their region
        // and arrival tick. Do not promote an entire raw state.
        var unverifiedTails = new List<CandidateWarpState>();
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
                    // Preserve a later historical state for future receipts,
                    // but never leave its query-tick tail beside a new projection.
                    var lastHistoricalTick = Math.Min(
                        state.TimeWindow.EndTick, accessContext.CurrentTick - 1);
                    if (state.TimeWindow.StartTick <= lastHistoricalTick)
                    {
                        var tail = state.TimeWindow.EndTick <= lastHistoricalTick
                            ? state
                            : CandidateWarpNormalization.CreateState(
                                warp.ScenarioId, warp.Seed, warp.TargetEntityId,
                                new InteractionTimeWindow(
                                    state.TimeWindow.StartTick, lastHistoricalTick),
                                state.TemporalKind, state.RegionIds,
                                state.EvidenceBoundaryState, state.Provenance);
                        if (standing == CandidateWarpStanding.Preferred && narrowRegions)
                        {
                            unverifiedTails.Add(tail);
                        }
                        else
                        {
                            history.Add(tail);
                        }
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
                // A later-time option inside an overlapping encounter must
                // not erase a distinct earlier Confirmed receipt. The same
                // tick cannot satisfy two disjoint exact confirmations;
                // those are conflicts, handled by the overlap check above.
                if (narrowRegions)
                {
                    foreach (var source in state.Provenance.Where(item =>
                        item.SourceKind ==
                            CandidateWarpSourceKind.CrossThreadObservation &&
                        item.EvidenceBoundaryState == EvidenceState.Confirmed &&
                        !string.Equals(item.SourceObservationId,
                            constraint.SourceObservationId, StringComparison.Ordinal) &&
                        item.SourceTimeWindow is { } priorWindow &&
                        priorWindow.StartTick < constraint.ConstrainedTimeWindow.EndTick))
                    {
                        if (source.SourceTimeWindow is not { } observed ||
                            !TryIntersect(state.TimeWindow, observed, out var pastWindow))
                        {
                            continue;
                        }

                        var lastPastTick = Math.Min(pastWindow.EndTick,
                            constraint.ConstrainedTimeWindow.EndTick - 1);
                        if (pastWindow.StartTick <= lastPastTick)
                        {
                            history.Add(CandidateWarpNormalization.CreateState(
                                warp.ScenarioId, warp.Seed, warp.TargetEntityId,
                                new InteractionTimeWindow(
                                    pastWindow.StartTick, lastPastTick),
                                state.TemporalKind, state.RegionIds,
                                state.EvidenceBoundaryState, state.Provenance));
                        }
                    }
                }

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
                    var tail = CandidateWarpNormalization.CreateState(
                        warp.ScenarioId, warp.Seed, warp.TargetEntityId,
                        new InteractionTimeWindow(intersection.EndTick + 1, tailEnd),
                        state.TemporalKind, state.RegionIds,
                        state.EvidenceBoundaryState, state.Provenance);
                    if (standing == CandidateWarpStanding.Preferred && narrowRegions)
                    {
                        unverifiedTails.Add(tail);
                    }
                    else
                    {
                        history.Add(tail);
                    }
                }
            }
        }

        // Prefer only the region/tick portion of raw later states reachable
        // from this received constraint using the *public* model. The unfiltered
        // tail is retained on a separate outer warp by ApplyToWarp. Do not
        // treat the same raw StateInterval as a confirmed stay at every tick.
        if (unverifiedTails.Count > 0 && propagationSources.Count > 0)
        {
            var reachableByTick = new Dictionary<long, HashSet<string>>();
            var earliestSourceTick = propagationSources.Min(state => state.TimeWindow.StartTick);

            HashSet<string> ReachableAt(long tick)
            {
                if (!reachableByTick.TryGetValue(tick, out var regions))
                {
                    regions = PublicCandidateWarpPropagator.Propagate(
                        warp.ScenarioId, warp.Seed, warp.TargetEntityId,
                        propagationSources, tick, publicHazardEnvelope,
                        retainUndeclaredStay: true,
                    publicationCutoffTick: accessContext.CurrentTick).RegionIds
                        .ToHashSet(StringComparer.Ordinal);
                    reachableByTick.Add(tick, regions);
                }

                return regions;
            }

            foreach (var tail in unverifiedTails)
            {
                if (tail.TimeWindow.EndTick < earliestSourceTick)
                {
                    continue;
                }

                var tailStart = Math.Max(tail.TimeWindow.StartTick, earliestSourceTick);
                foreach (var regionId in tail.RegionIds)
                {
                    if (!ReachableAt(tail.TimeWindow.EndTick).Contains(regionId))
                    {
                        continue;
                    }

                    // Retaining undeclared stay makes public reachability
                    // monotone for this bounded query window.
                    var first = tailStart;
                    var last = tail.TimeWindow.EndTick;
                    while (first < last)
                    {
                        var middle = first + (last - first) / 2;
                        if (ReachableAt(middle).Contains(regionId))
                        {
                            last = middle;
                        }
                        else
                        {
                            first = middle + 1;
                        }
                    }

                    history.Add(CandidateWarpNormalization.CreateState(
                        warp.ScenarioId, warp.Seed, warp.TargetEntityId,
                        new InteractionTimeWindow(first, tail.TimeWindow.EndTick),
                        tail.TemporalKind, [regionId], tail.EvidenceBoundaryState,
                        tail.Provenance));
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
                retainUndeclaredStay,
                publicationCutoffTick: accessContext.CurrentTick);
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
        bool retainUndeclaredStay = false,
        long? publicationCutoffTick = null)
    {
        // Historical propagation time and information availability are
        // different axes: a later-published path can support an earlier
        // encounter once the rescuer has received it by the query cutoff.
        var availableAtTick = publicationCutoffTick ?? currentTick;
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
                    retainUndeclaredStay: retainUndeclaredStay,
                    publicationCutoffTick: availableAtTick);
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
        bool retainUndeclaredStay,
        long publicationCutoffTick)
    {
        if (envelope is null)
        {
            return [new ReachedRegion(regionId, [])];
        }

        var publicRegion = envelope.Regions.SingleOrDefault(region => string.Equals(
            region.RegionId,
            regionId,
            StringComparison.Ordinal));
        // A destination's published earliest hazard arrival is a physical
        // lower bound on a branch-supported arrival, not just a publication
        // cutoff. It must hold at the HISTORICAL tick being reconstructed.
        // Do not reject a direct observation of an origin before the hazard.
        if (depth > 0 && publicRegion is not null &&
            currentTick < publicRegion.MinimumArrivalTick)
        {
            return [];
        }

        if (depth > envelope.Branches.Count)
        {
            return [new ReachedRegion(regionId, [])];
        }

        if (publicRegion is not null)
        {
            earliestTick = Math.Max(earliestTick, publicRegion.MinimumArrivalTick);
            latestTick = Math.Max(latestTick, publicRegion.MinimumArrivalTick);
        }

        var branches = envelope.Branches
            .Where(branch => branch.PublishedAtTick <= publicationCutoffTick)
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
                retainUndeclaredStay,
                publicationCutoffTick).Select(item => item with
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
