using FootprintSim.Evidence;
using FootprintSim.Hazards;
using FootprintSim.Interactions.CandidateWarps;

namespace FootprintSim.Interactions.Tracing;

/// <summary>
/// Traverses already-public causal structure. It does not reconstruct hidden truth.
/// </summary>
public sealed class OperationalCausalTracer
{
    public IReadOnlyList<TracingSourceGroup> GroupSources(
        IEnumerable<CausalTracingConstraint> constraints,
        CandidateWarpAccessContext accessContext)
    {
        ArgumentNullException.ThrowIfNull(constraints);
        ArgumentNullException.ThrowIfNull(accessContext);
        var available = constraints
            .Where(item => string.Equals(item.ScenarioId, accessContext.ScenarioId, StringComparison.Ordinal))
            .Where(item => item.Seed == accessContext.Seed)
            .Where(item => string.Equals(item.TargetEntityId, accessContext.TargetEntityId, StringComparison.Ordinal))
            .Where(item => item.Visibility == InteractionVisibility.PublicEvidence)
            .Where(item => item.AvailableAtTick <= accessContext.CurrentTick)
            .OrderBy(item => item.ConstrainedTimeWindow.StartTick)
            .ThenBy(item => item.ConstrainedTimeWindow.EndTick)
            .ThenBy(item => item.SourceObservationId, StringComparer.Ordinal)
            .ThenBy(item => item.ConstraintId, StringComparer.Ordinal)
            .ToArray();
        return BuildSourceGroups(available, accessContext);
    }

    public CausalTracingResult Trace(
        CandidateWarpSet candidateWarps,
        IEnumerable<CausalTracingConstraint> constraints,
        CandidateWarpAccessContext accessContext,
        PublicHazardEnvelope? publicHazardEnvelope)
    {
        ArgumentNullException.ThrowIfNull(candidateWarps);
        ArgumentNullException.ThrowIfNull(constraints);
        ArgumentNullException.ThrowIfNull(accessContext);
        Validate(candidateWarps, accessContext);

        var normalizedConstraints = constraints
            .Where(item => string.Equals(item.ScenarioId, accessContext.ScenarioId, StringComparison.Ordinal))
            .Where(item => item.Seed == accessContext.Seed)
            .Where(item => string.Equals(item.TargetEntityId, accessContext.TargetEntityId, StringComparison.Ordinal))
            .Where(item => item.Visibility == InteractionVisibility.PublicEvidence)
            .Where(item => item.AvailableAtTick <= accessContext.CurrentTick)
            .OrderBy(item => item.ConstrainedTimeWindow.StartTick)
            .ThenBy(item => item.ConstrainedTimeWindow.EndTick)
            .ThenBy(item => item.SourceObservationId, StringComparer.Ordinal)
            .ThenBy(item => item.ConstraintId, StringComparer.Ordinal)
            .ToArray();
        var canonical = normalizedConstraints
            .GroupBy(item => item.SemanticIdentity(), StringComparer.Ordinal)
            .Select(group => group.First())
            .ToArray();
        var sourceGroups = BuildSourceGroups(canonical, accessContext);
        var groupByConstraint = sourceGroups
            .SelectMany(group => group.ConstraintIds.Select(id => (id, group)))
            .ToDictionary(item => item.id, item => item.group, StringComparer.Ordinal);
        var sessionId = InteractionStableIdentifier.Create(
            "cts",
            accessContext.ScenarioId,
            InteractionStableIdentifier.Seed(accessContext.Seed),
            accessContext.TargetEntityId,
            InteractionStableIdentifier.Tick(accessContext.CurrentTick),
            accessContext.QueryContextId,
            canonical.Length == 0
                ? "no-constraints"
                : string.Join("|", canonical.Select(item => item.ConstraintId)));

        var sourceWarp = candidateWarps.Warps
            .OrderBy(warp => warp.Standing)
            .ThenBy(warp => warp.WarpId, StringComparer.Ordinal)
            .First();
        var states = sourceWarp.States
            .Select(state => new WorkingState(state))
            .OrderBy(state => state.Source.TimeWindow.StartTick)
            .ThenBy(state => state.Source.TimeWindow.EndTick)
            .ThenBy(state => state.Source.StateId, StringComparer.Ordinal)
            .ToArray();
        var steps = new List<CausalTracingStepAudit>();
        foreach (var state in states)
        {
            ApplyAtState(
                state,
                canonical,
                groupByConstraint,
                sessionId,
                accessContext,
                steps);
        }

        foreach (var constraint in canonical.Where(constraint => !states.Any(state =>
            Overlaps(state.Source.TimeWindow, constraint.ConstrainedTimeWindow))))
        {
            steps.Add(CreateConstraintStep(
                constraint,
                groupByConstraint[constraint.ConstraintId],
                sessionId,
                accessContext,
                states[0],
                TracingStepDisposition.NoOp,
                TracingStepReason.NoTemporalOverlap,
                states[0].Preferred,
                alternativesRetained: true,
                conflictDetected: false));
        }

        ApplyPublicConsistency(
            states,
            publicHazardEnvelope,
            sessionId,
            accessContext,
            steps);
        // State-level tracing audit is intentionally separate from verified
        // per-region positive-path support; outer includes unknown alternatives.
        PopulateRegionLineage(states, canonical, groupByConstraint,
            publicHazardEnvelope, accessContext.CurrentTick);

        var output = BuildCandidateWarps(
            candidateWarps,
            sourceWarp,
            states,
            canonical,
            accessContext);
        var lineage = BuildLineage(states, canonical, groupByConstraint);
        var orderedSteps = steps
            .OrderBy(item => item.StepNumber)
            .ThenBy(item => item.Direction)
            .ThenBy(item => item.TimeWindow.StartTick)
            .ThenBy(item => item.TimeWindow.EndTick)
            .ThenBy(item => item.ConstraintId, StringComparer.Ordinal)
            .ThenBy(item => item.StepId, StringComparer.Ordinal)
            .Select((item, index) => item with { StepNumber = index + 1 })
            .ToArray();
        var appliedRoles = orderedSteps
            .Where(item => item.Direction == CausalTracingDirection.CrossThread)
            .Where(item => item.Disposition is TracingStepDisposition.Applied or TracingStepDisposition.Branch)
            .Select(item => item.Role)
            .ToHashSet();

        return new CausalTracingResult
        {
            SessionId = sessionId,
            QueryContextId = accessContext.QueryContextId,
            CandidateWarps = output,
            SourceGroups = sourceGroups,
            SupportLineage = lineage,
            Steps = orderedSteps,
            CausalSandwichApplied =
                appliedRoles.Contains(TracingConstraintRole.Upstream) &&
                appliedRoles.Contains(TracingConstraintRole.Local) &&
                appliedRoles.Contains(TracingConstraintRole.Downstream) &&
                orderedSteps.Any(item => item.Direction == CausalTracingDirection.Forward) &&
                orderedSteps.Any(item => item.Direction == CausalTracingDirection.Backward),
            ConstraintOrderInvariant = true
        };
    }

    private static void ApplyAtState(
        WorkingState state,
        IReadOnlyList<CausalTracingConstraint> constraints,
        IReadOnlyDictionary<string, TracingSourceGroup> groupByConstraint,
        string sessionId,
        CandidateWarpAccessContext accessContext,
        ICollection<CausalTracingStepAudit> steps)
    {
        var overlapping = constraints
            .Where(item => Overlaps(state.Source.TimeWindow, item.ConstrainedTimeWindow))
            .ToArray();
        var unique = overlapping;
        foreach (var item in unique.Where(IsNoOp))
        {
            var reason = !item.IsPersonEvidence || item.IsDeviceOnlyEvidence
                ? TracingStepReason.DeviceOnlyEvidenceCannotConstrainPerson
                : TracingStepReason.EvidenceStateDoesNotConstrain;
            steps.Add(CreateConstraintStep(
                item,
                groupByConstraint[item.ConstraintId],
                sessionId,
                accessContext,
                state,
                TracingStepDisposition.NoOp,
                reason,
                state.Preferred,
                alternativesRetained: true,
                conflictDetected: false));
        }

        var conflicts = unique
            .Where(item => item.IsPersonEvidence && !item.IsDeviceOnlyEvidence)
            .Where(item => item.EvidenceBoundaryState == EvidenceState.Conflicting)
            .ToArray();
        foreach (var item in conflicts)
        {
            var sourceGroup = groupByConstraint[item.ConstraintId];
            state.ConflictingSourceGroups.Add(sourceGroup.SourceGroupId);
            state.Conflicted.UnionWith(state.Outer);
            steps.Add(CreateConstraintStep(
                item,
                sourceGroup,
                sessionId,
                accessContext,
                state,
                TracingStepDisposition.MarkConflict,
                TracingStepReason.EvidenceStateDoesNotConstrain,
                state.Preferred,
                alternativesRetained: true,
                conflictDetected: true));
        }

        var confirmed = unique
            .Where(item => item.IsPersonEvidence && !item.IsDeviceOnlyEvidence)
            .Where(item => item.EvidenceBoundaryState == EvidenceState.Confirmed)
            .ToArray();
        if (confirmed.Length > 0)
        {
            var intersection = new SortedSet<string>(state.Outer, StringComparer.Ordinal);
            foreach (var item in confirmed)
            {
                intersection.IntersectWith(item.AllowedRegionIds);
            }

            if (intersection.Count == 0)
            {
                foreach (var item in confirmed)
                {
                    var sourceGroup = groupByConstraint[item.ConstraintId];
                    state.ConflictingSourceGroups.Add(sourceGroup.SourceGroupId);
                    state.Conflicted.UnionWith(state.Outer);
                    steps.Add(CreateConstraintStep(
                        item,
                        sourceGroup,
                        sessionId,
                        accessContext,
                        state,
                        TracingStepDisposition.MarkConflict,
                        TracingStepReason.NoRegionOverlap,
                        state.Preferred,
                        alternativesRetained: true,
                        conflictDetected: true));
                }
            }
            else
            {
                var preserveCompetingAlternatives = conflicts.Length > 0;
                if (!preserveCompetingAlternatives)
                {
                    state.Outer.IntersectWith(intersection);
                }

                state.Preferred.IntersectWith(intersection);
                if (state.Preferred.Count == 0)
                {
                    state.Preferred.UnionWith(intersection);
                }

                foreach (var item in confirmed)
                {
                    var sourceGroup = groupByConstraint[item.ConstraintId];
                    state.SupportingSourceGroups.Add(sourceGroup.SourceGroupId);
                    state.AppliedConstraints.Add(item.ConstraintId);
                    steps.Add(CreateConstraintStep(
                        item,
                        sourceGroup,
                        sessionId,
                        accessContext,
                        state,
                        TracingStepDisposition.Applied,
                        TracingStepReason.Applied,
                        state.Preferred,
                        alternativesRetained: preserveCompetingAlternatives,
                        conflictDetected: false));
                }
            }
        }

        var uncertain = unique
            .Where(item => item.IsPersonEvidence && !item.IsDeviceOnlyEvidence)
            .Where(item => item.EvidenceBoundaryState == EvidenceState.SupportedButUncertain)
            .ToArray();
        if (uncertain.Length > 0)
        {
            var branches = uncertain
                .Select(item => state.Outer.Intersect(item.AllowedRegionIds, StringComparer.Ordinal))
                .Where(regions => regions.Any())
                .SelectMany(regions => regions)
                .ToHashSet(StringComparer.Ordinal);
            if (branches.Count > 0)
            {
                state.Preferred.IntersectWith(branches);
                if (state.Preferred.Count == 0)
                {
                    state.Preferred.UnionWith(branches);
                }
            }

            foreach (var item in uncertain)
            {
                var sourceGroup = groupByConstraint[item.ConstraintId];
                var hasOverlap = state.Outer.Overlaps(item.AllowedRegionIds);
                if (hasOverlap)
                {
                    state.SupportingSourceGroups.Add(sourceGroup.SourceGroupId);
                    state.AppliedConstraints.Add(item.ConstraintId);
                }

                steps.Add(CreateConstraintStep(
                    item,
                    sourceGroup,
                    sessionId,
                    accessContext,
                    state,
                    hasOverlap ? TracingStepDisposition.Branch : TracingStepDisposition.NoOp,
                    hasOverlap ? TracingStepReason.Applied : TracingStepReason.NoRegionOverlap,
                    state.Preferred,
                    alternativesRetained: true,
                    conflictDetected: false));
            }
        }
    }

    private static bool IsNoOp(CausalTracingConstraint item) =>
        !item.IsPersonEvidence ||
        item.IsDeviceOnlyEvidence ||
        item.EvidenceBoundaryState is EvidenceState.Indeterminate or EvidenceState.UnsupportedForQuery;

    private static void ApplyPublicConsistency(
        IReadOnlyList<WorkingState> states,
        PublicHazardEnvelope? envelope,
        string sessionId,
        CandidateWarpAccessContext context,
        ICollection<CausalTracingStepAudit> steps)
    {
        if (envelope is null || states.Count < 2)
        {
            return;
        }

        var stepNumber = 10_000;
        for (var iteration = 0; iteration < states.Count; iteration++)
        {
            var changed = false;
            for (var index = 0; index < states.Count - 1; index++)
            {
                changed |= ApplyDirection(
                    states[index],
                    states[index + 1],
                    envelope,
                    CausalTracingDirection.Forward,
                    sessionId,
                    context,
                    steps,
                    stepNumber++);
            }

            for (var index = states.Count - 1; index > 0; index--)
            {
                changed |= ApplyDirection(
                    states[index],
                    states[index - 1],
                    envelope,
                    CausalTracingDirection.Backward,
                    sessionId,
                    context,
                    steps,
                    stepNumber++);
            }

            if (!changed)
            {
                break;
            }
        }
    }

    private static bool ApplyDirection(
        WorkingState source,
        WorkingState destination,
        PublicHazardEnvelope envelope,
        CausalTracingDirection direction,
        string sessionId,
        CandidateWarpAccessContext context,
        ICollection<CausalTracingStepAudit> steps,
        int stepNumber)
    {
        var publicOrigins = direction == CausalTracingDirection.Forward
            ? source.Outer
            : destination.Outer;
        if (!envelope.Branches.Any(branch =>
            branch.PublishedAtTick <= context.CurrentTick &&
            publicOrigins.Contains(branch.SourceRegionId)))
        {
            // A missing public transition is not evidence that movement was impossible.
            return false;
        }

        var before = destination.Outer.ToArray();
        var transitionIds = new SortedSet<string>(StringComparer.Ordinal);
        var allowedOuter = ReachableDestinationRegions(
            source.Outer,
            source.Source.TimeWindow,
            destination.Outer,
            destination.Source.TimeWindow,
            envelope,
            context.CurrentTick,
            direction,
            transitionIds);
        var allowedPreferred = ReachableDestinationRegions(
            source.Preferred,
            source.Source.TimeWindow,
            destination.Preferred,
            destination.Source.TimeWindow,
            envelope,
            context.CurrentTick,
            direction,
            transitionIds);
        // No published branch list is guaranteed exhaustive. A positive path
        // can support preferred candidates; a missing path is not proof that
        // an outer candidate is impossible. Only actual received Confirmed
        // constraints may independently hard-narrow the outer.
        var hasPublicSupport = allowedOuter.Count > 0;
        var changed = false;
        if (allowedPreferred.Count > 0)
        {
            changed = !destination.Preferred.SetEquals(allowedPreferred);
            destination.Preferred.IntersectWith(allowedPreferred);
        }

        if (hasPublicSupport)
        {
            destination.SupportingSourceGroups.UnionWith(source.SupportingSourceGroups);
            // Conflicting source ancestry must survive positive public traversal.
            // It is not a reason to hard-exclude any outer candidate.
            destination.ConflictingSourceGroups.UnionWith(source.ConflictingSourceGroups);
            destination.AppliedConstraints.UnionWith(source.AppliedConstraints);
        }

        var relation = "public-causal-transition";
        var stepId = InteractionStableIdentifier.Create(
                "tst",
                sessionId,
                direction.ToString(),
                source.Source.StateId,
                destination.Source.StateId,
                stepNumber.ToString(System.Globalization.CultureInfo.InvariantCulture),
                string.Join("|", before),
                string.Join("|", destination.Outer));
        steps.Add(new CausalTracingStepAudit
        {
            StepId = stepId,
            SessionId = sessionId,
            QueryContextId = context.QueryContextId,
            StepNumber = stepNumber,
            Direction = direction,
            Role = direction == CausalTracingDirection.Forward
                    ? TracingConstraintRole.Upstream
                    : TracingConstraintRole.Downstream,
            TimeWindow = destination.Source.TimeWindow,
            TargetEntityId = context.TargetEntityId,
            SourceStateId = source.Source.StateId,
            DestinationStateId = destination.Source.StateId,
            RelationKind = relation,
            EvidenceState = EvidenceState.SupportedButUncertain,
            Disposition = hasPublicSupport
                    ? TracingStepDisposition.Applied
                    : TracingStepDisposition.NoOp,
            Reason = hasPublicSupport
                    ? TracingStepReason.PublicTransitionConsistency
                    : TracingStepReason.NoRegionOverlap,
            CandidateInputRegionIds = before,
            CandidateOutputRegionIds = destination.Outer.ToArray(),
            TransitionIds = transitionIds.ToArray(),
            RealTransitionTraversalOccurred = transitionIds.Count > 0,
            AlternativesRetained = true,
            ConflictDetected = false,
            Provenance = [new InteractionProvenance(
                    InteractionProvenanceKind.ExternalSource,
                    envelope.HazardId)]
        });

        return changed;
    }

    private static HashSet<string> ReachableDestinationRegions(
        IEnumerable<string> sourceRegions,
        InteractionTimeWindow sourceWindow,
        IEnumerable<string> destinationRegions,
        InteractionTimeWindow destinationWindow,
        PublicHazardEnvelope envelope,
        long currentTick,
        CausalTracingDirection direction,
        ISet<string> transitionIds)
    {
        var allowed = new HashSet<string>(StringComparer.Ordinal);
        foreach (var destinationRegion in destinationRegions)
        {
            foreach (var sourceRegion in sourceRegions)
            {
                var reachable = direction == CausalTracingDirection.Forward
                    ? TryFindPath(
                        sourceRegion,
                        sourceWindow,
                        destinationRegion,
                        destinationWindow,
                        envelope,
                        currentTick,
                        out var path)
                    : TryFindPath(
                        destinationRegion,
                        destinationWindow,
                        sourceRegion,
                        sourceWindow,
                        envelope,
                        currentTick,
                        out path);
                if (!reachable)
                {
                    continue;
                }

                allowed.Add(destinationRegion);
                transitionIds.UnionWith(path);
                break;
            }
        }

        return allowed;
    }

    private static bool TryFindPath(
        string sourceRegion,
        InteractionTimeWindow sourceWindow,
        string destinationRegion,
        InteractionTimeWindow destinationWindow,
        PublicHazardEnvelope envelope,
        long currentTick,
        out IReadOnlyList<string> transitionIds) =>
        TryFindPath(sourceRegion, sourceWindow, destinationRegion, destinationWindow,
            envelope, currentTick, out transitionIds, out _);

    // Private diagnostic overload: each removed search state and every examined
    // published branch count as work, including precomputation. The count is
    // deliberately not a public rescue/performance metric or an AR-32 total.
    private static bool TryFindPath(
        string sourceRegion,
        InteractionTimeWindow sourceWindow,
        string destinationRegion,
        InteractionTimeWindow destinationWindow,
        PublicHazardEnvelope envelope,
        long currentTick,
        out IReadOnlyList<string> transitionIds,
        out long workCount)
    {
        transitionIds = [];
        long work = 0;
        if (string.Equals(sourceRegion, destinationRegion, StringComparison.Ordinal))
        {
            workCount = work;
            return sourceWindow.StartTick <= destinationWindow.EndTick;
        }

        // Read the received public catalog only. Order independent of its input
        // enumeration; a new disconnected edge cannot change chosen witnesses.
        var visible = new List<PublicHazardBranch>();
        foreach (var branch in envelope.Branches)
        {
            work++;
            if (branch.PublishedAtTick <= currentTick)
                visible.Add(branch);
        }

        work += 2L * visible.Count; // outgoing and incoming index construction
        var outgoing = visible
            .GroupBy(branch => branch.SourceRegionId, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group
                .OrderBy(branch => branch.DestinationRegionId, StringComparer.Ordinal)
                .ThenBy(branch => branch.ProvenanceId, StringComparer.Ordinal)
                .ThenBy(branch => branch.MinimumTravelTicks)
                .ThenBy(branch => branch.MaximumTravelTicks)
                .ThenBy(branch => branch.TransportType)
                .ToArray(), StringComparer.Ordinal);
        var incoming = visible
            .GroupBy(branch => branch.DestinationRegionId, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.ToArray(),
                StringComparer.Ordinal);

        var canReachDestination = new HashSet<string>(StringComparer.Ordinal)
        {
            destinationRegion
        };
        var reverseQueue = new Queue<string>();
        reverseQueue.Enqueue(destinationRegion);
        while (reverseQueue.Count > 0)
        {
            work++;
            var region = reverseQueue.Dequeue();
            if (!incoming.TryGetValue(region, out var predecessors))
                continue;
            foreach (var branch in predecessors)
            {
                work++;
                if (canReachDestination.Add(branch.SourceRegionId))
                    reverseQueue.Enqueue(branch.SourceRegionId);
            }
        }
        if (!canReachDestination.Contains(sourceRegion))
        {
            workCount = work;
            return false;
        }

        // Drop vertices not on any source -> destination walk. A disconnected
        // cycle must not disable exact DAG state coalescing.
        var relevant = new HashSet<string>(StringComparer.Ordinal) { sourceRegion };
        var forwardQueue = new Queue<string>();
        forwardQueue.Enqueue(sourceRegion);
        while (forwardQueue.Count > 0)
        {
            work++;
            var region = forwardQueue.Dequeue();
            if (!outgoing.TryGetValue(region, out var branches))
                continue;
            foreach (var branch in branches)
            {
                work++;
                if (canReachDestination.Contains(branch.DestinationRegionId) &&
                    relevant.Add(branch.DestinationRegionId))
                    forwardQueue.Enqueue(branch.DestinationRegionId);
            }
        }

        var inDegree = relevant.ToDictionary(region => region, _ => 0,
            StringComparer.Ordinal);
        foreach (var region in relevant)
        {
            if (!outgoing.TryGetValue(region, out var branches))
                continue;
            foreach (var branch in branches)
            {
                work++;
                if (relevant.Contains(branch.DestinationRegionId))
                    inDegree[branch.DestinationRegionId]++;
            }
        }
        var ready = new SortedSet<string>(
            inDegree.Where(pair => pair.Value == 0).Select(pair => pair.Key),
            StringComparer.Ordinal);
        var topoOrder = new List<string>();
        while (ready.Count > 0)
        {
            work++;
            var region = ready.Min!;
            ready.Remove(region);
            topoOrder.Add(region);
            if (!outgoing.TryGetValue(region, out var branches))
                continue;
            foreach (var branch in branches)
            {
                work++;
                if (!relevant.Contains(branch.DestinationRegionId))
                    continue;
                if (--inDegree[branch.DestinationRegionId] == 0)
                    ready.Add(branch.DestinationRegionId);
            }
        }

        static string TransitionId(PublicHazardBranch branch) =>
            InteractionStableIdentifier.Create(
                "ptr",
                branch.SourceRegionId,
                branch.DestinationRegionId,
                InteractionStableIdentifier.Tick(branch.MinimumTravelTicks),
                InteractionStableIdentifier.Tick(branch.MaximumTravelTicks),
                branch.TransportType.ToString(),
                branch.ProvenanceId,
                InteractionStableIdentifier.Tick(branch.PublishedAtTick));

        if (topoOrder.Count == relevant.Count)
        {
            // DAG: a path cannot revisit a region. Only identical exact arrival
            // intervals may share a state; never fill holes via a convex hull.
            // Retain the first real witness in deterministic traversal order.
            var arrivals = new Dictionary<string,
                List<(long Minimum, long Maximum, IReadOnlyList<string> Path)>>(
                    StringComparer.Ordinal);
            var exactIntervals = relevant.ToDictionary(region => region,
                _ => new HashSet<(long Minimum, long Maximum)>(),
                StringComparer.Ordinal);
            arrivals[sourceRegion] =
            [
                (sourceWindow.StartTick, sourceWindow.EndTick, Array.Empty<string>())
            ];
            exactIntervals[sourceRegion].Add(
                (sourceWindow.StartTick, sourceWindow.EndTick));
            foreach (var region in topoOrder)
            {
                if (!arrivals.TryGetValue(region, out var states) ||
                    !outgoing.TryGetValue(region, out var branches))
                    continue;
                foreach (var state in states)
                {
                    work++;
                    foreach (var branch in branches)
                    {
                        work++;
                        if (!relevant.Contains(branch.DestinationRegionId))
                            continue;
                        var minimum = checked(state.Minimum + branch.MinimumTravelTicks);
                        var maximum = checked(state.Maximum + branch.MaximumTravelTicks);
                        if (minimum > destinationWindow.EndTick)
                            continue;
                        if (branch.DestinationRegionId == destinationRegion)
                        {
                            if (maximum >= destinationWindow.StartTick)
                            {
                                transitionIds = state.Path.Append(TransitionId(branch)).ToArray();
                                workCount = work;
                                return true;
                            }
                            continue;
                        }
                        if (!exactIntervals[branch.DestinationRegionId]
                            .Add((minimum, maximum)))
                            continue;
                        if (!arrivals.TryGetValue(branch.DestinationRegionId,
                            out var nextStates))
                        {
                            nextStates = [];
                            arrivals.Add(branch.DestinationRegionId, nextStates);
                        }
                        nextStates.Add((minimum, maximum,
                            state.Path.Append(TransitionId(branch)).ToArray()));
                    }
                }
            }
            workCount = work;
            return false;
        }

        // A public direct edge is a complete simple-path witness even when
        // a larger cyclic catalog would otherwise enumerate failed paths first.
        if (outgoing.TryGetValue(sourceRegion, out var directBranches))
        {
            foreach (var branch in directBranches)
            {
                work++;
                if (branch.DestinationRegionId != destinationRegion ||
                    (decimal)sourceWindow.StartTick + branch.MinimumTravelTicks >
                    destinationWindow.EndTick ||
                    (decimal)sourceWindow.EndTick + branch.MaximumTravelTicks <
                    destinationWindow.StartTick)
                    continue;
                transitionIds = [TransitionId(branch)];
                workCount = work;
                return true;
            }
        }

        // A simple path uses at most |V|-1 published edges. This upper
        // arrival bound is safe only for nonnegative travel durations.
        // Count every inspected relevant branch as preprocessing work.
        var nonnegativeRelevantDurations = true;
        long maximumRelevantDuration = 0;
        foreach (var region in relevant)
        {
            if (!outgoing.TryGetValue(region, out var branches))
                continue;
            foreach (var branch in branches)
            {
                work++;
                if (!relevant.Contains(branch.DestinationRegionId))
                    continue;
                if (branch.MinimumTravelTicks < 0 ||
                    branch.MaximumTravelTicks < 0)
                    nonnegativeRelevantDurations = false;
                maximumRelevantDuration = Math.Max(maximumRelevantDuration,
                    branch.MaximumTravelTicks);
            }
        }
        if (nonnegativeRelevantDurations &&
            (decimal)sourceWindow.EndTick +
            (decimal)(relevant.Count - 1) * maximumRelevantDuration <
            destinationWindow.StartTick)
        {
            workCount = work;
            return false;
        }

        // In cyclic structure, two equal time/region states can have different
        // legal continuations. Keep visited regions path-local, as in #141.
        // Conservative reverse shortest-time bounds only remove paths which
        // could not possibly arrive before the destination window closes.
        var minimumRemaining = new Dictionary<string, long>(StringComparer.Ordinal);
        if (visible.All(branch => branch.MinimumTravelTicks >= 0))
        {
            work += visible.Count;
            var pending = new PriorityQueue<string, long>();
            pending.Enqueue(destinationRegion, 0);
            minimumRemaining[destinationRegion] = 0;
            while (pending.TryDequeue(out var region, out var distance))
            {
                work++;
                if (minimumRemaining[region] != distance ||
                    !incoming.TryGetValue(region, out var predecessors))
                    continue;
                foreach (var branch in predecessors)
                {
                    work++;
                    if (!relevant.Contains(branch.SourceRegionId) ||
                        distance > long.MaxValue - branch.MinimumTravelTicks)
                        continue;
                    var next = distance + branch.MinimumTravelTicks;
                    if (!minimumRemaining.TryGetValue(branch.SourceRegionId, out var known) ||
                        next < known)
                    {
                        minimumRemaining[branch.SourceRegionId] = next;
                        pending.Enqueue(branch.SourceRegionId, next);
                    }
                }
            }
        }

        // For a small discrete horizon, time-expanded *walk* reachability is
        // a necessary condition for a simple path, never positive evidence.
        // This safely rejects parity and time-gap negatives without inferring
        // that a repeated-region walk is a valid witnessed simple path.
        // Larger horizons skip this optimization and use the exact fallback;
        // the local bound is never a search timeout or a proof of absence.
        const int maximumWalkHorizon = 256;
        var walkHorizon = (decimal)destinationWindow.EndTick -
            sourceWindow.StartTick;
        if (nonnegativeRelevantDurations && walkHorizon >= 0 &&
            walkHorizon <= maximumWalkHorizon)
        {
            var seenWalkStates = new HashSet<(string Region, long Tick)>();
            var pendingWalkStates = new Queue<(string Region, long Tick)>();
            var lastSourceTick = Math.Min(sourceWindow.EndTick,
                destinationWindow.EndTick);
            var sourceSpan = (long)((decimal)lastSourceTick -
                sourceWindow.StartTick);
            for (long delta = 0; delta <= sourceSpan; delta++)
            {
                var state = (sourceRegion, sourceWindow.StartTick + delta);
                if (seenWalkStates.Add(state))
                    pendingWalkStates.Enqueue(state);
            }

            var compatibleWalkExists = false;
            while (pendingWalkStates.Count > 0)
            {
                var (region, tick) = pendingWalkStates.Dequeue();
                work++;
                if (region == destinationRegion &&
                    tick >= destinationWindow.StartTick)
                {
                    compatibleWalkExists = true;
                    break;
                }
                if (!outgoing.TryGetValue(region, out var branches))
                    continue;
                foreach (var branch in branches)
                {
                    work++;
                    if (!relevant.Contains(branch.DestinationRegionId))
                        continue;
                    var lastDuration = (long)Math.Min(
                        (decimal)branch.MaximumTravelTicks,
                        (decimal)destinationWindow.EndTick - tick);
                    for (long duration = branch.MinimumTravelTicks;
                         duration <= lastDuration; duration++)
                    {
                        // Each examined discrete successor is counted as work,
                        // in addition to dequeues and public edge inspections.
                        work++;
                        var next = (branch.DestinationRegionId, tick + duration);
                        if (seenWalkStates.Add(next))
                            pendingWalkStates.Enqueue(next);
                    }
                }
            }
            if (!compatibleWalkExists)
            {
                workCount = work;
                return false;
            }
        }

        var pathOnStack = new List<string>();
        var visitedOnStack = new HashSet<string>(StringComparer.Ordinal)
        {
            sourceRegion
        };
        bool FindSimplePath(string region, long minimumTick, long maximumTick)
        {
            work++;
            if (!outgoing.TryGetValue(region, out var branches))
                return false;
            foreach (var branch in branches)
            {
                work++;
                var nextRegion = branch.DestinationRegionId;
                if (!relevant.Contains(nextRegion) || visitedOnStack.Contains(nextRegion))
                    continue;
                var minimum = checked(minimumTick + branch.MinimumTravelTicks);
                var maximum = checked(maximumTick + branch.MaximumTravelTicks);
                if (minimum > destinationWindow.EndTick)
                    continue;
                if (minimumRemaining.TryGetValue(nextRegion, out var remaining) &&
                    (decimal)minimum + remaining > destinationWindow.EndTick)
                    continue;
                if (nextRegion == destinationRegion &&
                    maximum < destinationWindow.StartTick)
                    continue;

                pathOnStack.Add(TransitionId(branch));
                if (nextRegion == destinationRegion)
                    return true;
                visitedOnStack.Add(nextRegion);
                if (FindSimplePath(nextRegion, minimum, maximum))
                    return true;
                visitedOnStack.Remove(nextRegion);
                pathOnStack.RemoveAt(pathOnStack.Count - 1);
            }
            return false;
        }

        var found = FindSimplePath(sourceRegion, sourceWindow.StartTick,
            sourceWindow.EndTick);
        if (found)
            transitionIds = pathOnStack.ToArray();
        workCount = work;
        return found;
    }

    // A constraint supports an individual region only when that region is
    // allowed at the matching historical state, or a published positive path
    // connects a previously supported region. Unknown paths never confer support.
    private static void PopulateRegionLineage(
        IReadOnlyList<WorkingState> states,
        IReadOnlyList<CausalTracingConstraint> constraints,
        IReadOnlyDictionary<string, TracingSourceGroup> groupByConstraint,
        PublicHazardEnvelope? envelope,
        long currentTick)
    {
        foreach (var state in states)
        {
            foreach (var constraint in constraints.Where(item =>
                Overlaps(state.Source.TimeWindow, item.ConstrainedTimeWindow) &&
                state.AppliedConstraints.Contains(item.ConstraintId)))
            {
                var sourceGroupId = groupByConstraint[constraint.ConstraintId].SourceGroupId;
                foreach (var region in state.Outer.Where(region =>
                    constraint.AllowedRegionIds.Contains(region, StringComparer.Ordinal)))
                {
                    state.RegionSupportingSourceGroups[region].Add(sourceGroupId);
                    state.RegionAppliedConstraints[region].Add(constraint.ConstraintId);
                }
            }
        }

        if (envelope is null || states.Count < 2)
        {
            return;
        }

        // Fixed point over known positive paths only. A constraint may move
        // forward from its evidence window to descendants, or backward to
        // ancestors, but never reverse direction after leaving that window.
        // Otherwise two branches converging on a state would spread A-only
        // evidence back into an unrelated B ancestor.
        var constraintById = constraints.ToDictionary(item => item.ConstraintId, StringComparer.Ordinal);
        for (var iteration = 0; iteration < states.Count; iteration++)
        {
            var changed = false;
            for (var index = 0; index < states.Count - 1; index++)
            {
                changed |= PropagateRegionLineage(
                    states[index], states[index + 1], envelope,
                    constraintById, groupByConstraint,
                    currentTick, CausalTracingDirection.Forward);
            }

            for (var index = states.Count - 1; index > 0; index--)
            {
                changed |= PropagateRegionLineage(
                    states[index], states[index - 1], envelope,
                    constraintById, groupByConstraint,
                    currentTick, CausalTracingDirection.Backward);
            }

            if (!changed)
            {
                break;
            }
        }
    }

    private static bool PropagateRegionLineage(
        WorkingState source,
        WorkingState destination,
        PublicHazardEnvelope envelope,
        IReadOnlyDictionary<string, CausalTracingConstraint> constraintById,
        IReadOnlyDictionary<string, TracingSourceGroup> groupByConstraint,
        long currentTick,
        CausalTracingDirection direction)
    {
        var changed = false;
        foreach (var destinationRegion in destination.Outer)
        {
            foreach (var sourceRegion in source.Outer)
            {
                var hasPositivePath = direction == CausalTracingDirection.Forward
                    ? TryFindPath(sourceRegion, source.Source.TimeWindow,
                        destinationRegion, destination.Source.TimeWindow,
                        envelope, currentTick, out _)
                    : TryFindPath(destinationRegion, destination.Source.TimeWindow,
                        sourceRegion, source.Source.TimeWindow,
                        envelope, currentTick, out _);
                if (!hasPositivePath)
                {
                    continue;
                }

                // Carry individual constraints rather than a merged group label:
                // source ancestry alone cannot tell whether this destination
                // is permitted by the original observation.
                foreach (var constraintId in source.RegionAppliedConstraints[sourceRegion])
                {
                    var constraint = constraintById[constraintId];
                    // Prevent a lineage that has left the evidence time in one
                    // direction from returning through a converging public path.
                    if (direction == CausalTracingDirection.Forward
                        ? constraint.ConstrainedTimeWindow.StartTick > source.Source.TimeWindow.EndTick
                        : constraint.ConstrainedTimeWindow.EndTick < source.Source.TimeWindow.StartTick)
                    {
                        continue;
                    }

                    // At any state overlapping the original evidence window,
                    // its allowed regions remain binding even for positive paths.
                    if (Overlaps(destination.Source.TimeWindow, constraint.ConstrainedTimeWindow) &&
                        !constraint.AllowedRegionIds.Contains(destinationRegion, StringComparer.Ordinal))
                    {
                        continue;
                    }

                    changed |= destination.RegionAppliedConstraints[destinationRegion].Add(constraintId);
                    changed |= destination.RegionSupportingSourceGroups[destinationRegion].Add(
                        groupByConstraint[constraintId].SourceGroupId);
                }
            }
        }

        return changed;
    }

    private static CausalTracingStepAudit CreateConstraintStep(
        CausalTracingConstraint constraint,
        TracingSourceGroup sourceGroup,
        string sessionId,
        CandidateWarpAccessContext context,
        WorkingState state,
        TracingStepDisposition disposition,
        TracingStepReason reason,
        IEnumerable<string> output,
        bool alternativesRetained,
        bool conflictDetected)
    {
        var input = state.OriginalRegions;
        var outputRegions = output
            .Distinct(StringComparer.Ordinal)
            .OrderBy(item => item, StringComparer.Ordinal)
            .ToArray();
        var stepId = InteractionStableIdentifier.Create(
            "tst",
            sessionId,
            constraint.ConstraintId,
            state.Source.StateId,
            disposition.ToString(),
            reason.ToString(),
            string.Join("|", outputRegions));
        return new CausalTracingStepAudit
        {
            StepId = stepId,
            SessionId = sessionId,
            QueryContextId = context.QueryContextId,
            StepNumber = (int)Math.Min(int.MaxValue, constraint.ConstrainedTimeWindow.StartTick),
            Direction = CausalTracingDirection.CrossThread,
            Role = constraint.Role,
            TimeWindow = constraint.ConstrainedTimeWindow,
            TargetEntityId = context.TargetEntityId,
            SourceStateId = state.Source.StateId,
            DestinationStateId = state.Source.StateId,
            ConstraintId = constraint.ConstraintId,
            SourceObservationId = constraint.SourceObservationId,
            SourceGroupId = sourceGroup.SourceGroupId,
            RelationKind = constraint.RelationKind,
            EvidenceState = constraint.EvidenceBoundaryState,
            Disposition = disposition,
            Reason = reason,
            CandidateInputRegionIds = input,
            CandidateOutputRegionIds = outputRegions,
            RealTransitionTraversalOccurred = false,
            AppliedConstraintIds = (disposition is TracingStepDisposition.Applied or TracingStepDisposition.Branch)
                ? [constraint.ConstraintId]
                : [],
            SkippedConstraintIds = disposition == TracingStepDisposition.NoOp
                ? [constraint.ConstraintId]
                : [],
            SupportingSourceGroupIds = (disposition is TracingStepDisposition.Applied or TracingStepDisposition.Branch)
                ? [sourceGroup.SourceGroupId]
                : [],
            ConflictingSourceGroupIds = conflictDetected ? [sourceGroup.SourceGroupId] : [],
            AlternativesRetained = alternativesRetained,
            ConflictDetected = conflictDetected,
            SourceAncestryIds = sourceGroup.SourceAncestryIds,
            Provenance = constraint.Provenance
        };
    }

    private static CandidateWarpSet BuildCandidateWarps(
        CandidateWarpSet candidateWarps,
        CandidateWarp sourceWarp,
        IReadOnlyList<WorkingState> states,
        IReadOnlyList<CausalTracingConstraint> constraints,
        CandidateWarpAccessContext context)
    {
        var preferredStates = states.Select(state => CreateOutputState(
            candidateWarps,
            state,
            state.Preferred,
            constraints,
            context,
            CandidateWarpStanding.Preferred)).ToArray();
        var outerStates = states.Select(state => CreateOutputState(
            candidateWarps,
            state,
            state.Outer,
            constraints,
            context,
            CandidateWarpStanding.OuterPlausible)).ToArray();
        var warps = new List<CandidateWarp>
        {
            CandidateWarpNormalization.CreateWarp(
                candidateWarps.ScenarioId,
                candidateWarps.Seed,
                candidateWarps.TargetEntityId,
                CandidateWarpStanding.Preferred,
                sourceWarp.WarpId,
                preferredStates,
                "causal-tracing-preferred"),
            CandidateWarpNormalization.CreateWarp(
                candidateWarps.ScenarioId,
                candidateWarps.Seed,
                candidateWarps.TargetEntityId,
                CandidateWarpStanding.OuterPlausible,
                sourceWarp.WarpId,
                outerStates,
                "causal-tracing-outer")
        };
        if (states.Any(state => state.Conflicted.Count > 0))
        {
            warps.Add(CandidateWarpNormalization.CreateWarp(
                candidateWarps.ScenarioId,
                candidateWarps.Seed,
                candidateWarps.TargetEntityId,
                CandidateWarpStanding.Conflicted,
                sourceWarp.WarpId,
                states.Select(state => CreateOutputState(
                    candidateWarps,
                    state,
                    state.Conflicted.Count == 0 ? state.Outer : state.Conflicted,
                    constraints,
                    context,
                    CandidateWarpStanding.Conflicted)),
                "causal-tracing-conflicted"));
        }

        return CandidateWarpNormalization.CreateSet(
            candidateWarps.ScenarioId,
            candidateWarps.Seed,
            candidateWarps.TargetEntityId,
            warps);
    }

    private static CandidateWarpState CreateOutputState(
        CandidateWarpSet candidateWarps,
        WorkingState state,
        IEnumerable<string> regions,
        IReadOnlyList<CausalTracingConstraint> constraints,
        CandidateWarpAccessContext context,
        CandidateWarpStanding standing)
    {
        var normalized = regions
            .Distinct(StringComparer.Ordinal)
            .OrderBy(item => item, StringComparer.Ordinal)
            .ToArray();
        if (normalized.Length == 0)
        {
            normalized = state.OriginalRegions.ToArray();
        }

        var provenance = new List<CandidateWarpProvenance>(state.Source.Provenance);
        foreach (var constraint in constraints
            .Where(item => normalized.All(region =>
                state.RegionAppliedConstraints[region].Contains(item.ConstraintId)))
            .OrderBy(item => item.ConstraintId, StringComparer.Ordinal))
        {
            provenance.Add(CandidateWarpProvenance.Create(
                constraint.EvidenceBoundaryState == EvidenceState.Conflicting
                    ? CandidateWarpSourceKind.ConflictingObservation
                    : constraint.SourceKind == TracingConstraintSourceKind.DeviceOnlyObservation
                        ? CandidateWarpSourceKind.DeviceOnlyObservation
                        : constraint.SourceKind == TracingConstraintSourceKind.PublicEnvironmentalObservation
                            ? CandidateWarpSourceKind.PublicEnvironmentalObservation
                            : CandidateWarpSourceKind.CrossThreadObservation,
                constraint.ConstraintId,
                constraint.AvailableAtTick,
                constraint.IsPersonEvidence,
                constraint.IsDeviceOnlyEvidence,
                constraint.EvidenceBoundaryState,
                constraint.AllowedRegionIds,
                sourceObservationId: constraint.SourceObservationId,
                sourceTimeWindow: constraint.SourceObservedTimeWindow,
                participants: constraint.Participants,
                interactionProvenance: constraint.Provenance));
        }

        return CandidateWarpNormalization.CreateState(
            candidateWarps.ScenarioId,
            candidateWarps.Seed,
            candidateWarps.TargetEntityId,
            state.Source.TimeWindow,
            state.Source.TemporalKind,
            normalized,
            standing == CandidateWarpStanding.Conflicted
                ? EvidenceState.Conflicting
                : state.Source.EvidenceBoundaryState,
            provenance);
    }

    private static IReadOnlyList<TracingSourceGroup> BuildSourceGroups(
        IReadOnlyList<CausalTracingConstraint> constraints,
        CandidateWarpAccessContext context)
    {
        var groups = new List<HashSet<string>>();
        foreach (var constraint in constraints)
        {
            var tokens = constraint.SourceAncestryIds
                .Append(constraint.SourceObservationId)
                .Append(constraint.DirectSourceId)
                .ToHashSet(StringComparer.Ordinal);
            var overlapping = groups.Where(group => group.Overlaps(tokens)).ToArray();
            if (overlapping.Length == 0)
            {
                groups.Add(tokens);
                continue;
            }

            var merged = new HashSet<string>(tokens, StringComparer.Ordinal);
            foreach (var group in overlapping)
            {
                merged.UnionWith(group);
                groups.Remove(group);
            }

            groups.Add(merged);
        }

        return groups
            .Select(tokens =>
            {
                var members = constraints.Where(constraint => constraint.SourceAncestryIds
                    .Append(constraint.SourceObservationId)
                    .Append(constraint.DirectSourceId)
                    .Any(tokens.Contains)).ToArray();
                var orderedTokens = tokens.OrderBy(item => item, StringComparer.Ordinal).ToArray();
                return new TracingSourceGroup
                {
                    SourceGroupId = InteractionStableIdentifier.Create(
                        "tsg",
                        context.ScenarioId,
                        InteractionStableIdentifier.Seed(context.Seed),
                        string.Join("|", orderedTokens)),
                    SourceObservationIds = members.Select(item => item.SourceObservationId)
                        .Distinct(StringComparer.Ordinal)
                        .OrderBy(item => item, StringComparer.Ordinal)
                        .ToArray(),
                    SourceAncestryIds = orderedTokens,
                    ConstraintIds = members.Select(item => item.ConstraintId)
                        .Distinct(StringComparer.Ordinal)
                        .OrderBy(item => item, StringComparer.Ordinal)
                        .ToArray()
                };
            })
            .OrderBy(item => item.SourceGroupId, StringComparer.Ordinal)
            .ToArray();
    }

    private static IReadOnlyList<CandidateSupportLineage> BuildLineage(
        IReadOnlyList<WorkingState> states,
        IReadOnlyList<CausalTracingConstraint> constraints,
        IReadOnlyDictionary<string, TracingSourceGroup> groupByConstraint) => states
        .SelectMany(state => state.Outer.Select(region =>
        {
            var applied = state.RegionAppliedConstraints[region];
            return new CandidateSupportLineage
            {
                CandidateStateId = state.Source.StateId,
                RegionId = region,
                SupportingSourceGroupIds = state.RegionSupportingSourceGroups[region]
                    .OrderBy(item => item, StringComparer.Ordinal).ToArray(),
                CompetingSourceGroupIds = state.ConflictingSourceGroups
                    .OrderBy(item => item, StringComparer.Ordinal).ToArray(),
                ConstraintIds = applied.OrderBy(item => item, StringComparer.Ordinal).ToArray(),
                RelationKinds = constraints.Where(item => applied.Contains(item.ConstraintId))
                    .Select(item => item.RelationKind)
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(item => item, StringComparer.Ordinal).ToArray(),
                ConstrainedTimeWindows = constraints.Where(item => applied.Contains(item.ConstraintId))
                    .Select(item => item.ConstrainedTimeWindow)
                    .Distinct()
                    .OrderBy(item => item.StartTick)
                    .ThenBy(item => item.EndTick).ToArray(),
                EvidenceStates = constraints.Where(item => applied.Contains(item.ConstraintId))
                    .Select(item => item.EvidenceBoundaryState)
                    .Distinct()
                    .OrderBy(item => item).ToArray()
            };
        }))
        .OrderBy(item => item.CandidateStateId, StringComparer.Ordinal)
        .ThenBy(item => item.RegionId, StringComparer.Ordinal).ToArray();

    private static bool Overlaps(InteractionTimeWindow left, InteractionTimeWindow right) =>
        left.StartTick <= right.EndTick && right.StartTick <= left.EndTick;

    private static void Validate(CandidateWarpSet warps, CandidateWarpAccessContext context)
    {
        if (!string.Equals(warps.ScenarioId, context.ScenarioId, StringComparison.Ordinal) ||
            warps.Seed != context.Seed ||
            !string.Equals(warps.TargetEntityId, context.TargetEntityId, StringComparison.Ordinal))
        {
            throw new ArgumentException("Candidate warp and tracing context identifiers do not match.");
        }

        if (context.CurrentTick < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(context));
        }
    }

    private sealed class WorkingState
    {
        public WorkingState(CandidateWarpState source)
        {
            Source = source;
            OriginalRegions = source.RegionIds.ToArray();
            Preferred = new SortedSet<string>(source.RegionIds, StringComparer.Ordinal);
            Outer = new SortedSet<string>(source.RegionIds, StringComparer.Ordinal);
            Conflicted = new SortedSet<string>(StringComparer.Ordinal);
            foreach (var region in source.RegionIds)
            {
                RegionSupportingSourceGroups[region] = new HashSet<string>(StringComparer.Ordinal);
                RegionAppliedConstraints[region] = new HashSet<string>(StringComparer.Ordinal);
            }
        }

        public CandidateWarpState Source { get; }

        public IReadOnlyList<string> OriginalRegions { get; }

        public SortedSet<string> Preferred { get; }

        public SortedSet<string> Outer { get; }

        public SortedSet<string> Conflicted { get; }

        public HashSet<string> AppliedConstraints { get; } = new(StringComparer.Ordinal);

        public HashSet<string> SupportingSourceGroups { get; } = new(StringComparer.Ordinal);

        public HashSet<string> ConflictingSourceGroups { get; } = new(StringComparer.Ordinal);

        public Dictionary<string, HashSet<string>> RegionSupportingSourceGroups { get; } =
            new(StringComparer.Ordinal);

        public Dictionary<string, HashSet<string>> RegionAppliedConstraints { get; } =
            new(StringComparer.Ordinal);
    }
}
