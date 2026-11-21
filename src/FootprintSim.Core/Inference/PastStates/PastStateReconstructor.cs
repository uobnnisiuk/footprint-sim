using FootprintSim.Evidence;
using FootprintSim.Interactions;
using FootprintSim.Interactions.CandidateWarps;
using FootprintSim.Interactions.Tracing;

namespace FootprintSim.Inference.PastStates;

/// <summary>
/// Constructs candidate historical states from public evidence. It has no truth-side input.
/// </summary>
public sealed class PastStateReconstructor
{
    public PastStateComparisonResult Compare(
        PastStatePublicContext context,
        ObservationSet completePublicObservations,
        CausalTracingConstraintSet completePublicConstraints)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(completePublicObservations);
        ArgumentNullException.ThrowIfNull(completePublicConstraints);
        Validate(context, completePublicObservations, completePublicConstraints);

        var evidenceSet = BuildEvidenceSet(
            context.Query,
            completePublicObservations,
            completePublicConstraints);
        var reconstruction = Reconstruct(context, evidenceSet);
        var backward = RunTraversal(
            PastStateMethod.BackwardTraversal,
            context,
            evidenceSet,
            context.PublicCandidateStates,
            reconstruction.Candidates);
        var localStates = BuildLocalStates(context, reconstruction.Candidates);
        var localTraversal = RunTraversal(
            PastStateMethod.ReconstructionWithLocalTraversal,
            context,
            evidenceSet,
            localStates,
            reconstruction.Candidates);
        var local = localTraversal with
        {
            CandidateGenerationSteps = reconstruction.CandidateGenerationSteps
        };
        var reconstructionMethodId = MethodId(context.Query, PastStateMethod.ConstraintBasedReconstruction);
        var generationSources = context.GenerationDirectSourceIds.ToHashSet(StringComparer.Ordinal);
        var verificationSources = context.VerificationDirectSourceIds.ToHashSet(StringComparer.Ordinal);
        var reconstructionUsages = evidenceSet.Constraints
            .Where(item => Overlaps(item.ConstrainedTimeWindow, context.Query.TargetTimeWindow))
            .Where(item => generationSources.Contains(item.DirectSourceId) ||
                verificationSources.Contains(item.DirectSourceId))
            .Select(item => CreateUsage(
                context,
                evidenceSet,
                item,
                generationSources.Contains(item.DirectSourceId)
                    ? PastStateEvidenceUsageRole.CandidateGeneration
                    : PastStateEvidenceUsageRole.CandidateVerification,
                reconstructionMethodId))
            .OrderBy(item => item.ConstraintId, StringComparer.Ordinal)
            .ToArray();
        var baseCandidateRegions = context.PublicCandidateStates
            .Where(item => Overlaps(item.TimeWindow, context.Query.TargetTimeWindow))
            .SelectMany(item => item.RegionIds)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(item => item, StringComparer.Ordinal)
            .ToArray();
        var reconstructionMethod = new PastStateMethodResult
        {
            MethodId = reconstructionMethodId,
            Method = PastStateMethod.ConstraintBasedReconstruction,
            CandidateRegionIdsBefore = baseCandidateRegions,
            CandidateRegionIds = reconstruction.Candidates
                .Select(item => item.RegionId)
                .OrderBy(item => item, StringComparer.Ordinal)
                .ToArray(),
            Steps = reconstruction.ReconstructionSteps,
            CandidateGenerationSteps = reconstruction.CandidateGenerationSteps,
            CandidateVerificationSteps = reconstruction.CandidateVerificationSteps,
            ForwardTransitionSteps = 0,
            BackwardTransitionSteps = 0,
            PublicRelationsInspected = reconstruction.PublicRelationsInspected,
            EvidenceRecordsInspected = reconstruction.EvidenceRecordsInspected,
            SourceGroupsInspected = reconstruction.SourceGroupsInspected,
            Diagnostics = new PastStateMethodDiagnostics
            {
                MethodId = reconstructionMethodId,
                ConstraintUsages = reconstructionUsages
            }
        };

        var methods = new[] { backward, reconstructionMethod, local };

        return new PastStateComparisonResult
        {
            Query = context.Query,
            Reconstruction = reconstruction,
            Methods = methods,
            EvidenceUsage = BuildEvidenceUsage(context, evidenceSet, methods)
        };
    }

    public PublicEvidenceSet BuildEvidenceSet(
        PastStateQuery query,
        ObservationSet completePublicObservations,
        CausalTracingConstraintSet completePublicConstraints)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(completePublicObservations);
        ArgumentNullException.ThrowIfNull(completePublicConstraints);
        var observations = completePublicObservations.Observations
            .Where(item => item.AvailableTick <= query.EvidenceAvailabilityCutoff)
            .OrderBy(item => item.AvailableTick)
            .ThenBy(item => item.ObservedTick)
            .ThenBy(item => item.ObservationId, StringComparer.Ordinal)
            .ToArray();
        var constraints = completePublicConstraints
            .PubliclyAvailableAt(query.EvidenceAvailabilityCutoff)
            .Constraints
            .OrderBy(item => item.AvailableAtTick)
            .ThenBy(item => item.ConstrainedTimeWindow.StartTick)
            .ThenBy(item => item.ConstraintId, StringComparer.Ordinal)
            // The same normalized claim can arrive as separately allocated copies.
            // Identity is the ConstraintId, not its collection object references
            // or the shared source ancestry of other, distinct valid claims.
            .DistinctBy(item => item.ConstraintId, StringComparer.Ordinal)
            .ToArray();
        var access = AccessContext(query);
        var sourceGroups = new OperationalCausalTracer().GroupSources(constraints, access);
        var relationReferences = constraints
            .Select(item => $"{item.ConstraintId}:{item.RelationKind}")
            .Distinct(StringComparer.Ordinal)
            .OrderBy(item => item, StringComparer.Ordinal)
            .ToArray();
        var evidenceSetId = InteractionStableIdentifier.Create(
            "pes",
            query.QueryId,
            IdentifierList(observations.Select(item => item.ObservationId)),
            IdentifierList(constraints.Select(item => item.ConstraintId)),
            IdentifierList(sourceGroups.Select(item => item.SourceGroupId)));
        var excludedFuture = completePublicObservations.Observations.Count(item =>
                item.AvailableTick > query.EvidenceAvailabilityCutoff) +
            completePublicConstraints.Constraints.Count(item =>
                item.Visibility == InteractionVisibility.PublicEvidence &&
                item.AvailableAtTick > query.EvidenceAvailabilityCutoff);

        return new PublicEvidenceSet
        {
            EvidenceSetId = evidenceSetId,
            QueryId = query.QueryId,
            ObservationIds = observations.Select(item => item.ObservationId).ToArray(),
            Constraints = constraints,
            SourceGroups = sourceGroups,
            PublicRelationReferences = relationReferences,
            ExcludedFutureEvidenceCount = excludedFuture
        };
    }

    public IReadOnlyList<CandidateVerification> VerifyCandidates(
        PastStatePublicContext context,
        PublicEvidenceSet evidenceSet,
        IReadOnlyList<PastStateCandidate> candidates)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(evidenceSet);
        ArgumentNullException.ThrowIfNull(candidates);
        if (!string.Equals(evidenceSet.QueryId, context.Query.QueryId, StringComparison.Ordinal) ||
            candidates.Any(item => !string.Equals(
                item.QueryId,
                context.Query.QueryId,
                StringComparison.Ordinal)))
        {
            throw new ArgumentException("Candidate and evidence query identifiers do not match.");
        }

        return Verify(context, evidenceSet, candidates);
    }

    private PastStateReconstructionResult Reconstruct(
        PastStatePublicContext context,
        PublicEvidenceSet evidenceSet)
    {
        var query = context.Query;
        var baseRegions = context.PublicCandidateStates
            .Where(item => Overlaps(item.TimeWindow, query.TargetTimeWindow))
            .SelectMany(item => item.RegionIds)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(item => item, StringComparer.Ordinal)
            .ToArray();
        if (baseRegions.Length == 0)
        {
            throw new InvalidOperationException("The public model has no candidate state at the query window.");
        }

        var generationSources = context.GenerationDirectSourceIds.ToHashSet(StringComparer.Ordinal);
        var generation = evidenceSet.Constraints
            .Where(item => generationSources.Contains(item.DirectSourceId))
            .Where(item => Overlaps(item.ConstrainedTimeWindow, query.TargetTimeWindow))
            .OrderBy(item => item.ConstraintId, StringComparer.Ordinal)
            .ToArray();
        var generationGroups = new OperationalCausalTracer()
            .GroupSources(generation, AccessContext(query));
        var confirmedGroupRegions = generationGroups
            .Select(group => generation
                .Where(item => group.ConstraintIds.Contains(item.ConstraintId, StringComparer.Ordinal))
                .Where(IsConfirmedPersonEvidence)
                .SelectMany(item => item.AllowedRegionIds)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(item => item, StringComparer.Ordinal)
                .ToArray())
            .Where(item => item.Length > 0)
            .ToArray();
        var narrowed = new SortedSet<string>(baseRegions, StringComparer.Ordinal);
        foreach (var regions in confirmedGroupRegions)
        {
            narrowed.IntersectWith(regions);
        }

        var hasConflictingEvidence = generation.Any(item =>
            item.EvidenceBoundaryState == EvidenceState.Conflicting);
        var inconsistentConfirmed = confirmedGroupRegions.Length > 0 && narrowed.Count == 0;
        var candidateRegions = confirmedGroupRegions.Length == 0 ||
            hasConflictingEvidence ||
            inconsistentConfirmed
                ? baseRegions
                : narrowed.ToArray();
        // A window is a union of possible locations, not a single simultaneous
        // location. Keep the legacy common-window layer separate from queries
        // whose evidence has distinct or only partially overlapping times.
        var temporalSlices = RequiresTemporalProjection(context, evidenceSet)
            ? BuildTemporalSlices(context, generation, [])
            : null;
        if (temporalSlices is not null)
        {
            candidateRegions = temporalSlices.SelectMany(item => item.RegionIds)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(item => item, StringComparer.Ordinal)
                .ToArray();
        }
        var generationEvidenceIds = generation
            .Select(item => item.SourceObservationId)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(item => item, StringComparer.Ordinal)
            .ToArray();
        var generationGroupIds = generationGroups
            .Select(item => item.SourceGroupId)
            .OrderBy(item => item, StringComparer.Ordinal)
            .ToArray();
        var generationAncestry = generationGroups
            .SelectMany(item => item.SourceAncestryIds)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(item => item, StringComparer.Ordinal)
            .ToArray();
        var candidates = candidateRegions.Select(region =>
        {
            var applied = generation
                .Where(item => IsSupportingPersonEvidence(item) && item.AllowedRegionIds.Contains(region, StringComparer.Ordinal))
                .Where(item => temporalSlices is null || temporalSlices.Any(slice =>
                    slice.RegionIds.Contains(region, StringComparer.Ordinal) &&
                    slice.Constraints.Any(constraint => constraint.ConstraintId == item.ConstraintId)))
                .Select(item => item.ConstraintId)
                .OrderBy(item => item, StringComparer.Ordinal)
                .ToArray();
            var conflicts = generation
                .Where(item => temporalSlices is null
                    ? item.EvidenceBoundaryState == EvidenceState.Conflicting ||
                        (inconsistentConfirmed && IsConfirmedPersonEvidence(item)) ||
                        (hasConflictingEvidence && IsConfirmedPersonEvidence(item) &&
                            !item.AllowedRegionIds.Contains(region, StringComparer.Ordinal))
                    : temporalSlices.Any(slice => slice.HasConflict &&
                        slice.RegionIds.Contains(region, StringComparer.Ordinal) &&
                        ((slice.Constraints.Any(constraint => constraint.ConstraintId == item.ConstraintId) &&
                            (IsConfirmedPersonEvidence(item) ||
                                item.EvidenceBoundaryState == EvidenceState.Conflicting)) ||
                         slice.OverlappingConflicts.Any(constraint => constraint.ConstraintId == item.ConstraintId))))
                .Select(item => item.ConstraintId)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(item => item, StringComparer.Ordinal)
                .ToArray();
            var unresolved = generation
                .Where(item => !applied.Contains(item.ConstraintId, StringComparer.Ordinal) &&
                    !conflicts.Contains(item.ConstraintId, StringComparer.Ordinal))
                .Select(item => item.ConstraintId)
                .OrderBy(item => item, StringComparer.Ordinal)
                .ToArray();
            var relations = generation
                .Select(item => $"{item.ConstraintId}:{item.RelationKind}")
                .Distinct(StringComparer.Ordinal)
                .OrderBy(item => item, StringComparer.Ordinal)
                .ToArray();
            var disposition = DetermineCandidateDisposition(
                generation,
                applied,
                conflicts,
                region);
            var candidateId = PastStateCandidateIdentity.Create(
                query,
                region,
                generationEvidenceIds,
                generationGroupIds,
                generation.Select(item => item.ConstraintId));
            return new PastStateCandidate
            {
                CandidateId = candidateId,
                QueryId = query.QueryId,
                TargetEntityId = query.TargetEntityId,
                TargetTimeWindow = query.TargetTimeWindow,
                RegionId = region,
                GenerationEvidenceIds = generationEvidenceIds,
                GenerationSourceGroupIds = generationGroupIds,
                SourceAncestryIds = generationAncestry,
                AppliedConstraintIds = applied,
                UnresolvedConstraintIds = unresolved,
                ConflictingConstraintIds = conflicts,
                PublicRelationReferences = relations,
                Disposition = disposition
            };
        }).OrderBy(item => item.CandidateId, StringComparer.Ordinal).ToArray();
        var verification = VerifyCandidates(context, evidenceSet, candidates);
        var verificationSources = context.VerificationDirectSourceIds.ToHashSet(StringComparer.Ordinal);
        var availableVerificationConstraintCount = evidenceSet.Constraints.Count(item =>
            verificationSources.Contains(item.DirectSourceId) &&
            Overlaps(item.ConstrainedTimeWindow, query.TargetTimeWindow));
        var verificationGroupsInspected = verification
            .SelectMany(item => item.VerificationSourceGroupIds.Concat(item.SharedAncestrySourceGroupIds))
            .Distinct(StringComparer.Ordinal)
            .Count();
        var candidateGenerationSteps = 1 + generationGroups.Count + candidates.Length;
        var candidateVerificationSteps = verificationGroupsInspected;
        var reconstructionSteps = candidateGenerationSteps + candidateVerificationSteps;
        var reconstructionId = InteractionStableIdentifier.Create(
            "psr",
            query.QueryId,
            evidenceSet.EvidenceSetId,
            string.Join("|", candidates.Select(item => item.CandidateId)));

        return new PastStateReconstructionResult
        {
            ReconstructionId = reconstructionId,
            Query = query,
            EvidenceSet = evidenceSet,
            InitialCandidateCount = baseRegions.Length,
            Candidates = candidates,
            Verification = verification,
            ReconstructionSteps = reconstructionSteps,
            CandidateGenerationSteps = candidateGenerationSteps,
            CandidateVerificationSteps = candidateVerificationSteps,
            EvidenceRecordsInspected = evidenceSet.ObservationIds.Count + generation.Length +
                availableVerificationConstraintCount,
            SourceGroupsInspected = generationGroups.Count + verificationGroupsInspected,
            PublicRelationsInspected = generation.Length + availableVerificationConstraintCount
        };
    }

    private static IReadOnlyList<CandidateVerification> Verify(
        PastStatePublicContext context,
        PublicEvidenceSet evidenceSet,
        IReadOnlyList<PastStateCandidate> candidates)
    {
        var verificationSources = context.VerificationDirectSourceIds.ToHashSet(StringComparer.Ordinal);
        var constraints = evidenceSet.Constraints
            .Where(item => verificationSources.Contains(item.DirectSourceId))
            .Where(item => Overlaps(item.ConstrainedTimeWindow, context.Query.TargetTimeWindow))
            .OrderBy(item => item.ConstraintId, StringComparer.Ordinal)
            .ToArray();
        var groups = new OperationalCausalTracer()
            .GroupSources(constraints, AccessContext(context.Query));
        var groupByConstraint = groups
            .SelectMany(group => group.ConstraintIds.Select(id => (id, group)))
            .ToDictionary(item => item.id, item => item.group, StringComparer.Ordinal);
        var generationSources = context.GenerationDirectSourceIds.ToHashSet(StringComparer.Ordinal);
        var temporalSlices = RequiresTemporalProjection(context, evidenceSet, includeVerification: true)
            ? BuildTemporalSlices(context, evidenceSet.Constraints
                .Where(item => generationSources.Contains(item.DirectSourceId))
                .Where(item => Overlaps(item.ConstrainedTimeWindow, context.Query.TargetTimeWindow))
                .ToArray(), constraints)
            : null;

        return candidates.Select(candidate =>
        {
            var generationAncestry = candidate.SourceAncestryIds.ToHashSet(StringComparer.Ordinal);
            var sharedGroups = groups
                .Where(group => group.SourceAncestryIds.Any(generationAncestry.Contains))
                .ToArray();
            var independentGroups = groups
                .Where(group => !sharedGroups.Contains(group))
                .ToArray();
            var independentIds = independentGroups.Select(item => item.SourceGroupId).ToHashSet(StringComparer.Ordinal);
            var independent = constraints
                .Where(item => independentIds.Contains(groupByConstraint[item.ConstraintId].SourceGroupId))
                .ToArray();
            var supporting = independent
                .Where(IsConfirmedPersonEvidence)
                .Where(item => item.AllowedRegionIds.Contains(candidate.RegionId, StringComparer.Ordinal))
                .Select(item => item.ConstraintId)
                .OrderBy(item => item, StringComparer.Ordinal)
                .ToArray();
            var refuting = independent
                .Where(IsConfirmedPersonEvidence)
                .Where(item => !item.AllowedRegionIds.Contains(candidate.RegionId, StringComparer.Ordinal))
                .Select(item => item.ConstraintId)
                .OrderBy(item => item, StringComparer.Ordinal)
                .ToArray();
            var conflicting = independent
                .Where(item => item.EvidenceBoundaryState == EvidenceState.Conflicting)
                .Select(item => item.ConstraintId)
                .OrderBy(item => item, StringComparer.Ordinal)
                .ToArray();
            if (temporalSlices is not null)
            {
                (supporting, refuting, conflicting) = VerifyTemporalCandidate(
                    candidate, temporalSlices, independent, independentGroups);
            }
            var overlap = sharedGroups
                .SelectMany(item => item.SourceAncestryIds)
                .Where(generationAncestry.Contains)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(item => item, StringComparer.Ordinal)
                .ToArray();
            var disposition = conflicting.Length > 0 ||
                (temporalSlices is null && supporting.Length > 0 && refuting.Length > 0)
                ? CandidateVerificationDisposition.Conflicting
                : refuting.Length > 0
                    ? CandidateVerificationDisposition.IndependentlyRefuted
                    : supporting.Length > 0
                        ? CandidateVerificationDisposition.IndependentlySupported
                        : sharedGroups.Length > 0 && independentGroups.Length == 0
                            ? CandidateVerificationDisposition.SharedAncestryOnly
                            : constraints.Length == 0
                                ? CandidateVerificationDisposition.Unavailable
                                : CandidateVerificationDisposition.Unresolved;
            return new CandidateVerification
            {
                CandidateId = candidate.CandidateId,
                VerificationEvidenceIds = independent
                    .Select(item => item.SourceObservationId)
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(item => item, StringComparer.Ordinal)
                    .ToArray(),
                VerificationSourceGroupIds = independentGroups
                    .Select(item => item.SourceGroupId)
                    .ToArray(),
                SharedAncestrySourceGroupIds = sharedGroups
                    .Select(item => item.SourceGroupId)
                    .ToArray(),
                SupportingConstraintIds = supporting,
                RefutingConstraintIds = refuting,
                ConflictingConstraintIds = conflicting,
                GenerationVerificationAncestryOverlap = overlap,
                Disposition = disposition
            };
        }).OrderBy(item => item.CandidateId, StringComparer.Ordinal).ToArray();
    }

    private static PastStateMethodResult RunTraversal(
        PastStateMethod method,
        PastStatePublicContext context,
        PublicEvidenceSet evidenceSet,
        IReadOnlyList<PublicCandidateWarpStateDefinition> states,
        IReadOnlyList<PastStateCandidate> candidates)
    {
        var warps = PublicCandidateWarpFactory.Create(
            context.Query.ScenarioId,
            context.Query.Seed,
            context.Query.TargetEntityId,
            states);
        var methodId = MethodId(context.Query, method);
        var traversalSources = context.TraversalDirectSourceIds.ToHashSet(StringComparer.Ordinal);
        var selectedConstraints = evidenceSet.Constraints
            .Where(item => traversalSources.Contains(item.DirectSourceId))
            .Where(item => states.Any(state => Overlaps(item.ConstrainedTimeWindow, state.TimeWindow)))
            .Where(item => method != PastStateMethod.BackwardTraversal ||
                !Overlaps(item.ConstrainedTimeWindow, context.Query.TargetTimeWindow))
            // A re-created copy has different collection references but the
            // same canonical claim identity; distinct claims remain visible.
            .DistinctBy(item => item.ConstraintId, StringComparer.Ordinal)
            .OrderBy(item => item.ConstraintId, StringComparer.Ordinal)
            .ToArray();
        var sourceWarp = warps.Warps
            .OrderBy(item => item.Standing)
            .ThenBy(item => item.WarpId, StringComparer.Ordinal)
            .First();
        var candidateIdsByRegion = candidates.ToDictionary(
            item => item.RegionId,
            item => item.CandidateId,
            StringComparer.Ordinal);
        var tracing = new OperationalCausalTracer().Trace(
            warps,
            selectedConstraints,
            AccessContext(context.Query),
            context.PublicHazardEnvelope);
        // Tracer step IDs refer to partitioned source states. Reuse the
        // exact source-by-source partition instead of applying all output
        // windows to every overlapping original state: that would invent
        // state IDs absent from the actual trace and misattribute candidates.
        var acceptedConstraintIds = tracing.SourceGroups
            .SelectMany(group => group.ConstraintIds)
            .ToHashSet(StringComparer.Ordinal);
        var tracingConstraints = selectedConstraints
            .Where(item => acceptedConstraintIds.Contains(item.ConstraintId))
            .ToArray();
        var localStateMembership = OperationalCausalTracer
            .PartitionSourceStates(sourceWarp, tracingConstraints, warps)
            .Select(item => item.State)
            .DistinctBy(item => item.StateId, StringComparer.Ordinal)
            .Select(slice => new PastStateLocalStateMembership
            {
                StateId = slice.StateId,
                TimeWindow = slice.TimeWindow,
                IsTargetWindowState = Overlaps(
                    slice.TimeWindow, context.Query.TargetTimeWindow),
                RegionIds = slice.RegionIds,
                CandidateIds = Overlaps(slice.TimeWindow, context.Query.TargetTimeWindow)
                    ? slice.RegionIds
                        .Where(candidateIdsByRegion.ContainsKey)
                        .Select(region => candidateIdsByRegion[region])
                        .Distinct(StringComparer.Ordinal)
                        .OrderBy(id => id, StringComparer.Ordinal)
                        .ToArray()
                    : []
            })
            .ToArray();
        var forwardTransitionSteps = tracing.Steps.Count(item =>
            item.Direction == CausalTracingDirection.Forward &&
            item.RealTransitionTraversalOccurred);
        var backwardTransitionSteps = tracing.Steps.Count(item =>
            item.Direction == CausalTracingDirection.Backward &&
            item.RealTransitionTraversalOccurred);
        var constraintUsages = selectedConstraints
            .Select(item => CreateUsage(
                context,
                evidenceSet,
                item,
                PastStateEvidenceUsageRole.Traversal,
                methodId,
                tracing.SourceGroups))
            .OrderBy(item => item.ConstraintId, StringComparer.Ordinal)
            .ToArray();
        return new PastStateMethodResult
        {
            MethodId = methodId,
            Method = method,
            CandidateRegionIdsBefore = sourceWarp.States
                .Where(item => Overlaps(item.TimeWindow, context.Query.TargetTimeWindow))
                .SelectMany(item => item.RegionIds)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(item => item, StringComparer.Ordinal)
                .ToArray(),
            // This window asks where the person may have been at ANY overlapping
            // state time; it is not a prediction of the end tick. Do not
            // collapse differently timed outer sets onto the first tick.
            CandidateRegionIds = tracing.CandidateWarps.Warps
                .Where(warp => warp.Standing == CandidateWarpStanding.OuterPlausible)
                .SelectMany(warp => warp.States)
                .Where(state => Overlaps(state.TimeWindow, context.Query.TargetTimeWindow))
                .SelectMany(state => state.RegionIds)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(region => region, StringComparer.Ordinal)
                .ToArray(),
            Steps = method == PastStateMethod.BackwardTraversal
                ? tracing.Steps.Count(item => item.Direction == CausalTracingDirection.Backward)
                : tracing.Steps.Count,
            CandidateGenerationSteps = 0,
            CandidateVerificationSteps = 0,
            ForwardTransitionSteps = forwardTransitionSteps,
            BackwardTransitionSteps = backwardTransitionSteps,
            TransitionIds = tracing.Steps
                .SelectMany(item => item.TransitionIds)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(item => item, StringComparer.Ordinal)
                .ToArray(),
            PublicRelationsInspected = tracing.Steps.Count,
            EvidenceRecordsInspected = tracing.Steps
                .Select(item => item.SourceObservationId)
                .Where(item => item is not null)
                .Distinct(StringComparer.Ordinal)
                .Count(),
            SourceGroupsInspected = tracing.SourceGroups.Count,
            TracingSessionId = tracing.SessionId,
            Diagnostics = new PastStateMethodDiagnostics
            {
                MethodId = methodId,
                ConstraintUsages = constraintUsages,
                LocalStates = localStateMembership,
                TracingSteps = tracing.Steps
            }
        };
    }

    private static IReadOnlyList<PublicCandidateWarpStateDefinition> BuildLocalStates(
        PastStatePublicContext context,
        IReadOnlyList<PastStateCandidate> candidates)
    {
        var minimum = Math.Max(
            0,
            context.Query.TargetTimeWindow.StartTick - context.LocalTraversalWindowPaddingTicks);
        var maximum = checked(
            context.Query.TargetTimeWindow.EndTick + context.LocalTraversalWindowPaddingTicks);
        var ordered = context.PublicCandidateStates
            .OrderBy(item => item.TimeWindow.StartTick)
            .ThenBy(item => item.TimeWindow.EndTick)
            .ToArray();
        var targetIndices = ordered
            .Select((item, index) => (item, index))
            .Where(pair => Overlaps(pair.item.TimeWindow, context.Query.TargetTimeWindow))
            .Select(pair => pair.index)
            .ToArray();
        if (targetIndices.Length == 0)
        {
            throw new InvalidOperationException("No public candidate states cover the target window.");
        }

        var selectedIndices = ordered
            .Select((item, index) => (item, index))
            .Where(pair => pair.item.TimeWindow.StartTick <= maximum &&
                minimum <= pair.item.TimeWindow.EndTick)
            .Select(pair => pair.index)
            .ToHashSet();
        var firstTarget = targetIndices.Min();
        var lastTarget = targetIndices.Max();
        if (firstTarget > 0)
        {
            selectedIndices.Add(firstTarget - 1);
        }

        if (lastTarget < ordered.Length - 1)
        {
            selectedIndices.Add(lastTarget + 1);
        }

        var local = ordered
            .Select((item, index) => (item, index))
            .Where(pair => selectedIndices.Contains(pair.index))
            .Select(pair => Overlaps(pair.item.TimeWindow, context.Query.TargetTimeWindow)
                ? pair.item with
                {
                    RegionIds = candidates
                        .Select(candidate => candidate.RegionId)
                        .Distinct(StringComparer.Ordinal)
                        .OrderBy(region => region, StringComparer.Ordinal)
                        .ToArray()
                }
                : pair.item)
            .ToArray();
        return local.Length == 0
            ? throw new InvalidOperationException("No public candidate states are available for local traversal.")
            : local;
    }

    private static IReadOnlyList<PastStateConstraintUsage> BuildEvidenceUsage(
        PastStatePublicContext context,
        PublicEvidenceSet evidenceSet,
        IReadOnlyList<PastStateMethodResult> methods)
    {
        var generationSources = context.GenerationDirectSourceIds.ToHashSet(StringComparer.Ordinal);
        var verificationSources = context.VerificationDirectSourceIds.ToHashSet(StringComparer.Ordinal);
        var traversalSources = context.TraversalDirectSourceIds.ToHashSet(StringComparer.Ordinal);
        var usedByConstraint = methods
            .SelectMany(item => item.Diagnostics?.ConstraintUsages ?? [])
            .GroupBy(item => item.ConstraintId, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.SelectMany(item => item.MethodIds)
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(item => item, StringComparer.Ordinal)
                    .ToArray(),
                StringComparer.Ordinal);

        return evidenceSet.Constraints
            .Select(constraint =>
            {
                var roles = new List<PastStateEvidenceUsageRole>();
                if (generationSources.Contains(constraint.DirectSourceId))
                {
                    roles.Add(PastStateEvidenceUsageRole.CandidateGeneration);
                }

                if (verificationSources.Contains(constraint.DirectSourceId))
                {
                    roles.Add(PastStateEvidenceUsageRole.CandidateVerification);
                }

                if (traversalSources.Contains(constraint.DirectSourceId))
                {
                    roles.Add(PastStateEvidenceUsageRole.Traversal);
                }

                if (roles.Count == 0)
                {
                    roles.Add(PastStateEvidenceUsageRole.EvaluationOnly);
                }

                var methodIds = usedByConstraint.GetValueOrDefault(constraint.ConstraintId) ?? [];
                var usage = CreateUsage(
                    context,
                    evidenceSet,
                    constraint,
                    roles[0],
                    methodIds.FirstOrDefault());
                return usage with
                {
                    UsageRoles = roles.Distinct().OrderBy(item => item).ToArray(),
                    MethodIds = methodIds,
                    Disposition = methodIds.Length > 0
                        ? PastStateEvidenceUsageDisposition.Used
                        : PastStateEvidenceUsageDisposition.NotUsedForMethod
                };
            })
            .OrderBy(item => item.ConstraintId, StringComparer.Ordinal)
            .ToArray();
    }

    private static PastStateConstraintUsage CreateUsage(
        PastStatePublicContext context,
        PublicEvidenceSet evidenceSet,
        CausalTracingConstraint constraint,
        PastStateEvidenceUsageRole role,
        string? methodId,
        IReadOnlyList<TracingSourceGroup>? sourceGroups = null)
    {
        var group = (sourceGroups ?? evidenceSet.SourceGroups).FirstOrDefault(item =>
            item.ConstraintIds.Contains(constraint.ConstraintId, StringComparer.Ordinal));
        return new PastStateConstraintUsage
        {
            ConstraintId = constraint.ConstraintId,
            SourceObservationId = constraint.SourceObservationId,
            SourceObservationResolved = context.AvailableObservationIds.Contains(
                constraint.SourceObservationId,
                StringComparer.Ordinal),
            DirectSourceId = constraint.DirectSourceId,
            SourceGroupId = group?.SourceGroupId,
            SourceAncestryIds = group?.SourceAncestryIds ?? constraint.SourceAncestryIds,
            RelationKind = constraint.RelationKind,
            EvidenceState = constraint.EvidenceBoundaryState,
            UsageRoles = [role],
            MethodIds = string.IsNullOrWhiteSpace(methodId) ? [] : [methodId],
            Disposition = string.IsNullOrWhiteSpace(methodId)
                ? PastStateEvidenceUsageDisposition.NotUsedForMethod
                : PastStateEvidenceUsageDisposition.Used
        };
    }

    private static string MethodId(PastStateQuery query, PastStateMethod method) =>
        InteractionStableIdentifier.Create(
            "psm",
            query.QueryId,
            method.ToString());

    private sealed record TemporalSlice(
        InteractionTimeWindow TimeWindow,
        IReadOnlyList<string> RegionIds,
        IReadOnlyList<CausalTracingConstraint> Constraints,
        bool HasConflict,
        IReadOnlyList<CausalTracingConstraint> OverlappingConflicts);

    private static bool RequiresTemporalProjection(
        PastStatePublicContext context,
        PublicEvidenceSet evidenceSet,
        bool includeVerification = false)
    {
        var sources = context.GenerationDirectSourceIds
            .Concat(includeVerification ? context.VerificationDirectSourceIds : [])
            .ToHashSet(StringComparer.Ordinal);
        var target = context.Query.TargetTimeWindow;
        return evidenceSet.Constraints.Any(item => sources.Contains(item.DirectSourceId) &&
            item.IsPersonEvidence && !item.IsDeviceOnlyEvidence &&
            item.TargetEntityId == context.Query.TargetEntityId &&
            (item.EvidenceBoundaryState is EvidenceState.Confirmed or EvidenceState.Conflicting) &&
            Overlaps(item.ConstrainedTimeWindow, target) &&
            (item.ConstrainedTimeWindow.StartTick != target.StartTick ||
                item.ConstrainedTimeWindow.EndTick != target.EndTick));
    }

    private static IReadOnlyList<TemporalSlice> BuildTemporalSlices(
        PastStatePublicContext context,
        IReadOnlyList<CausalTracingConstraint> generation,
        IReadOnlyList<CausalTracingConstraint> verification)
    {
        var target = context.Query.TargetTimeWindow;
        var starts = new SortedSet<long> { target.StartTick };
        void SplitAt(InteractionTimeWindow window)
        {
            if (!Overlaps(window, target)) return;
            if (window.StartTick > target.StartTick) starts.Add(window.StartTick);
            // The strict comparison also makes +1 safe at long.MaxValue.
            if (window.EndTick < target.EndTick) starts.Add(window.EndTick + 1);
        }

        foreach (var state in context.PublicCandidateStates) SplitAt(state.TimeWindow);
        foreach (var item in generation.Concat(verification).Where(item =>
            item.ConstrainedTimeWindow.StartTick == item.ConstrainedTimeWindow.EndTick))
        {
            SplitAt(item.ConstrainedTimeWindow);
        }

        var allRegions = context.PublicCandidateStates
            .Where(item => Overlaps(item.TimeWindow, target))
            .SelectMany(item => item.RegionIds)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(item => item, StringComparer.Ordinal)
            .ToArray();
        var boundaries = starts.ToArray();
        return boundaries.Select((start, index) =>
        {
            var window = new InteractionTimeWindow(start,
                index + 1 < boundaries.Length ? boundaries[index + 1] - 1 : target.EndTick);
            var baseRegions = context.PublicCandidateStates
                .Where(item => Overlaps(item.TimeWindow, window))
                .SelectMany(item => item.RegionIds)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(item => item, StringComparer.Ordinal)
                .ToArray();
            if (baseRegions.Length == 0) baseRegions = allRegions;
            // An inclusive witness is an event somewhere in its interval;
            // broadcasting it to every slice would invent continuous residence.
            var exact = generation.Where(item => item.IsPersonEvidence && !item.IsDeviceOnlyEvidence &&
                    item.TargetEntityId == context.Query.TargetEntityId &&
                    item.ConstrainedTimeWindow.StartTick == window.StartTick &&
                    item.ConstrainedTimeWindow.EndTick == window.EndTick &&
                    window.StartTick == window.EndTick)
                .OrderBy(item => item.ConstraintId, StringComparer.Ordinal)
                .ToArray();
            var groups = new OperationalCausalTracer().GroupSources(exact, AccessContext(context.Query));
            var narrowed = new SortedSet<string>(baseRegions, StringComparer.Ordinal);
            foreach (var group in groups)
            {
                var regions = exact.Where(IsConfirmedPersonEvidence)
                    .Where(item => group.ConstraintIds.Contains(item.ConstraintId, StringComparer.Ordinal))
                    .SelectMany(item => item.AllowedRegionIds).Distinct(StringComparer.Ordinal).ToArray();
                if (regions.Length > 0) narrowed.IntersectWith(regions);
            }

            // A Conflicting interval witnesses competing possibilities somewhere
            // within its window; it cannot be broadcast as a confirmed location.
            // Still, ignoring it during exact-tick projection could falsely remove
            // outer regions. Preserve the overlapping interval as conflict audit
            // and suspend narrowing only in slices it actually overlaps.
            var overlappingConflicts = generation.Where(item =>
                    item.IsPersonEvidence && !item.IsDeviceOnlyEvidence &&
                    item.TargetEntityId == context.Query.TargetEntityId &&
                    item.EvidenceBoundaryState == EvidenceState.Conflicting &&
                    Overlaps(item.ConstrainedTimeWindow, window))
                .OrderBy(item => item.ConstraintId, StringComparer.Ordinal)
                .ToArray();
            var conflict = overlappingConflicts.Length > 0 || narrowed.Count == 0;
            return new TemporalSlice(window, conflict ? baseRegions : narrowed.ToArray(),
                exact, conflict, overlappingConflicts);
        }).ToArray();
    }

    private static (string[] Supporting, string[] Refuting, string[] Conflicting) VerifyTemporalCandidate(
        PastStateCandidate candidate,
        IReadOnlyList<TemporalSlice> slices,
        IReadOnlyList<CausalTracingConstraint> independent,
        IReadOnlyList<TracingSourceGroup> independentGroups)
    {
        var supporting = new SortedSet<string>(StringComparer.Ordinal);
        var refuting = new SortedSet<string>(StringComparer.Ordinal);
        var conflicting = new SortedSet<string>(StringComparer.Ordinal);
        var possible = slices.Where(slice =>
            slice.RegionIds.Contains(candidate.RegionId, StringComparer.Ordinal)).ToArray();
        var refutedEveryPossibleTime = possible.Length > 0;
        foreach (var slice in possible)
        {
            var exact = independent.Where(item => item.IsPersonEvidence && !item.IsDeviceOnlyEvidence &&
                    item.TargetEntityId == candidate.TargetEntityId &&
                    item.ConstrainedTimeWindow.StartTick == slice.TimeWindow.StartTick &&
                    item.ConstrainedTimeWindow.EndTick == slice.TimeWindow.EndTick &&
                    slice.TimeWindow.StartTick == slice.TimeWindow.EndTick)
                .ToArray();
            var sliceSupporting = exact.Where(IsConfirmedPersonEvidence)
                .Where(item => item.AllowedRegionIds.Contains(candidate.RegionId, StringComparer.Ordinal))
                .Select(item => item.ConstraintId).ToArray();
            var sliceRefuting = independentGroups.Select(group => exact.Where(IsConfirmedPersonEvidence)
                    .Where(item => group.ConstraintIds.Contains(item.ConstraintId, StringComparer.Ordinal)).ToArray())
                .Where(group => group.Length > 0 && group.All(item =>
                    !item.AllowedRegionIds.Contains(candidate.RegionId, StringComparer.Ordinal)))
                .SelectMany(group => group).Select(item => item.ConstraintId).ToArray();
            supporting.UnionWith(sliceSupporting);
            refuting.UnionWith(sliceRefuting);
            // Independent interval conflicts remain visible at possible times,
            // without inventing an exact-tick refutation or continuous residence.
            conflicting.UnionWith(independent.Where(item =>
                    item.IsPersonEvidence && !item.IsDeviceOnlyEvidence &&
                    item.TargetEntityId == candidate.TargetEntityId &&
                    item.EvidenceBoundaryState == EvidenceState.Conflicting &&
                    Overlaps(item.ConstrainedTimeWindow, slice.TimeWindow))
                .Select(item => item.ConstraintId));
            if (sliceSupporting.Length > 0 && sliceRefuting.Length > 0)
            {
                conflicting.UnionWith(sliceSupporting);
                conflicting.UnionWith(sliceRefuting);
            }

            refutedEveryPossibleTime &= sliceRefuting.Length > 0;
        }

        // Rejecting one tick does not refute a candidate that could occur at
        // another tick, including any unobserved part of the query window.
        return (supporting.ToArray(), refutedEveryPossibleTime ? refuting.ToArray() : [],
            conflicting.ToArray());
    }

    private static PastStateCandidateDisposition DetermineCandidateDisposition(
        IReadOnlyList<CausalTracingConstraint> generation,
        IReadOnlyCollection<string> applied,
        IReadOnlyCollection<string> conflicts,
        string region)
    {
        if (generation.Count == 0)
        {
            return PastStateCandidateDisposition.Unavailable;
        }

        if (conflicts.Count > 0)
        {
            return PastStateCandidateDisposition.Conflicting;
        }

        if (generation.Any(item =>
            item.EvidenceBoundaryState == EvidenceState.Confirmed &&
            applied.Contains(item.ConstraintId)))
        {
            return PastStateCandidateDisposition.Supported;
        }

        if (generation.Any(item =>
            item.EvidenceBoundaryState == EvidenceState.SupportedButUncertain &&
            applied.Contains(item.ConstraintId)))
        {
            return PastStateCandidateDisposition.Uncertain;
        }

        return PastStateCandidateDisposition.Unsupported;
    }

    private static bool IsConfirmedPersonEvidence(CausalTracingConstraint item) =>
        item.IsPersonEvidence &&
        !item.IsDeviceOnlyEvidence &&
        item.EvidenceBoundaryState == EvidenceState.Confirmed;

    private static bool IsSupportingPersonEvidence(CausalTracingConstraint item) =>
        item.IsPersonEvidence &&
        !item.IsDeviceOnlyEvidence &&
        item.EvidenceBoundaryState is EvidenceState.Confirmed or EvidenceState.SupportedButUncertain;

    private static bool Overlaps(InteractionTimeWindow left, InteractionTimeWindow right) =>
        left.StartTick <= right.EndTick && right.StartTick <= left.EndTick;

    private static string IdentifierList(IEnumerable<string> values)
    {
        var joined = string.Join(
            "|",
            values
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(item => item, StringComparer.Ordinal));
        return joined.Length == 0 ? "none" : joined;
    }

    private static CandidateWarpAccessContext AccessContext(PastStateQuery query) => new()
    {
        ScenarioId = query.ScenarioId,
        Seed = query.Seed,
        TargetEntityId = query.TargetEntityId,
        CurrentTick = query.EvidenceAvailabilityCutoff,
        QueryContextId = query.QueryContextId
    };

    private static void Validate(
        PastStatePublicContext context,
        ObservationSet observations,
        CausalTracingConstraintSet constraints)
    {
        if (!string.Equals(context.Query.ScenarioId, observations.ScenarioId, StringComparison.Ordinal) ||
            context.Query.Seed != observations.Seed ||
            !string.Equals(context.Query.ScenarioId, constraints.ScenarioId, StringComparison.Ordinal) ||
            context.Query.Seed != constraints.Seed)
        {
            throw new ArgumentException("Query and public evidence identifiers do not match.");
        }

        if (context.PublicCandidateStates.Count == 0)
        {
            throw new ArgumentException("At least one public candidate-state definition is required.");
        }

        if (context.LocalTraversalWindowPaddingTicks < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(context));
        }
    }
}
