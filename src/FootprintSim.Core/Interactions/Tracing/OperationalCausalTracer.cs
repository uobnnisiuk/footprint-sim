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
        var conflict = allowedOuter.Count == 0;
        var changed = false;
        if (!conflict)
        {
            changed = !destination.Outer.SetEquals(allowedOuter);
            destination.Outer.IntersectWith(allowedOuter);
            if (allowedPreferred.Count > 0)
            {
                changed |= !destination.Preferred.SetEquals(allowedPreferred);
                destination.Preferred.IntersectWith(allowedPreferred);
            }

            destination.SupportingSourceGroups.UnionWith(source.SupportingSourceGroups);
            destination.ConflictingSourceGroups.UnionWith(source.ConflictingSourceGroups);
            destination.AppliedConstraints.UnionWith(source.AppliedConstraints);
        }
        else
        {
            destination.Conflicted.UnionWith(destination.Outer);
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
            Disposition = conflict
                    ? TracingStepDisposition.MarkConflict
                    : TracingStepDisposition.Applied,
            Reason = conflict
                    ? TracingStepReason.NoRegionOverlap
                    : TracingStepReason.PublicTransitionConsistency,
            CandidateInputRegionIds = before,
            CandidateOutputRegionIds = destination.Outer.ToArray(),
            TransitionIds = transitionIds.ToArray(),
            RealTransitionTraversalOccurred = transitionIds.Count > 0,
            AlternativesRetained = conflict,
            ConflictDetected = conflict,
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
        out IReadOnlyList<string> transitionIds)
    {
        if (string.Equals(sourceRegion, destinationRegion, StringComparison.Ordinal))
        {
            transitionIds = [];
            return sourceWindow.StartTick <= destinationWindow.EndTick;
        }

        var queue = new Queue<(string Region, long MinimumTick, long MaximumTick, int Depth, IReadOnlyList<string> Path)>();
        queue.Enqueue((sourceRegion, sourceWindow.StartTick, sourceWindow.EndTick, 0, []));
        var visited = new HashSet<string>(StringComparer.Ordinal);
        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (!visited.Add($"{current.Region}:{current.MinimumTick}:{current.MaximumTick}"))
            {
                continue;
            }

            foreach (var branch in envelope.Branches
                .Where(item => item.PublishedAtTick <= currentTick)
                .Where(item => string.Equals(item.SourceRegionId, current.Region, StringComparison.Ordinal))
                .OrderBy(item => item.DestinationRegionId, StringComparer.Ordinal)
                .ThenBy(item => item.ProvenanceId, StringComparer.Ordinal))
            {
                var minimum = checked(current.MinimumTick + branch.MinimumTravelTicks);
                var maximum = checked(current.MaximumTick + branch.MaximumTravelTicks);
                var transitionId = InteractionStableIdentifier.Create(
                    "ptr",
                    branch.SourceRegionId,
                    branch.DestinationRegionId,
                    InteractionStableIdentifier.Tick(branch.MinimumTravelTicks),
                    InteractionStableIdentifier.Tick(branch.MaximumTravelTicks),
                    branch.TransportType.ToString(),
                    branch.ProvenanceId,
                    InteractionStableIdentifier.Tick(branch.PublishedAtTick));
                var path = current.Path.Append(transitionId).ToArray();
                if (string.Equals(branch.DestinationRegionId, destinationRegion, StringComparison.Ordinal) &&
                    minimum <= destinationWindow.EndTick && destinationWindow.StartTick <= maximum)
                {
                    transitionIds = path;
                    return true;
                }

                if (current.Depth < envelope.Branches.Count && minimum <= destinationWindow.EndTick)
                {
                    queue.Enqueue((branch.DestinationRegionId, minimum, maximum, current.Depth + 1, path));
                }
            }
        }

        transitionIds = [];
        return false;
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
            .Where(item => state.AppliedConstraints.Contains(item.ConstraintId))
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
        .SelectMany(state => state.Outer.Select(region => new CandidateSupportLineage
        {
            CandidateStateId = state.Source.StateId,
            RegionId = region,
            SupportingSourceGroupIds = state.SupportingSourceGroups.OrderBy(item => item, StringComparer.Ordinal).ToArray(),
            CompetingSourceGroupIds = state.ConflictingSourceGroups.OrderBy(item => item, StringComparer.Ordinal).ToArray(),
            ConstraintIds = state.AppliedConstraints.OrderBy(item => item, StringComparer.Ordinal).ToArray(),
            RelationKinds = constraints.Where(item => state.AppliedConstraints.Contains(item.ConstraintId))
                .Select(item => item.RelationKind)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(item => item, StringComparer.Ordinal)
                .ToArray(),
            ConstrainedTimeWindows = constraints.Where(item => state.AppliedConstraints.Contains(item.ConstraintId))
                .Select(item => item.ConstrainedTimeWindow)
                .Distinct()
                .OrderBy(item => item.StartTick)
                .ThenBy(item => item.EndTick)
                .ToArray(),
            EvidenceStates = constraints.Where(item => state.AppliedConstraints.Contains(item.ConstraintId))
                .Select(item => item.EvidenceBoundaryState)
                .Distinct()
                .OrderBy(item => item)
                .ToArray()
        }))
        .OrderBy(item => item.CandidateStateId, StringComparer.Ordinal)
        .ThenBy(item => item.RegionId, StringComparer.Ordinal)
        .ToArray();

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
        }

        public CandidateWarpState Source { get; }

        public IReadOnlyList<string> OriginalRegions { get; }

        public SortedSet<string> Preferred { get; }

        public SortedSet<string> Outer { get; }

        public SortedSet<string> Conflicted { get; }

        public HashSet<string> AppliedConstraints { get; } = new(StringComparer.Ordinal);

        public HashSet<string> SupportingSourceGroups { get; } = new(StringComparer.Ordinal);

        public HashSet<string> ConflictingSourceGroups { get; } = new(StringComparer.Ordinal);
    }
}
