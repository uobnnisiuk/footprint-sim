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
            context.PublicCandidateStates);
        var localStates = BuildLocalStates(context, reconstruction.Candidates);
        var local = RunTraversal(
            PastStateMethod.ReconstructionWithLocalTraversal,
            context,
            evidenceSet,
            localStates);
        var reconstructionMethod = new PastStateMethodResult
        {
            Method = PastStateMethod.ConstraintBasedReconstruction,
            CandidateRegionIds = reconstruction.Candidates
                .Select(item => item.RegionId)
                .OrderBy(item => item, StringComparer.Ordinal)
                .ToArray(),
            Steps = reconstruction.ReconstructionSteps,
            PublicRelationsInspected = reconstruction.PublicRelationsInspected,
            EvidenceRecordsInspected = reconstruction.EvidenceRecordsInspected,
            SourceGroupsInspected = reconstruction.SourceGroupsInspected
        };

        return new PastStateComparisonResult
        {
            Query = context.Query,
            Reconstruction = reconstruction,
            Methods = [backward, reconstructionMethod, local]
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

        var groupByConstraint = evidenceSet.SourceGroups
            .SelectMany(group => group.ConstraintIds.Select(id => (id, group)))
            .ToDictionary(item => item.id, item => item.group, StringComparer.Ordinal);
        return Verify(context, evidenceSet, candidates, groupByConstraint);
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
        var groupByConstraint = evidenceSet.SourceGroups
            .SelectMany(group => group.ConstraintIds.Select(id => (id, group)))
            .ToDictionary(item => item.id, item => item.group, StringComparer.Ordinal);
        var generationGroups = generation
            .Select(item => groupByConstraint[item.ConstraintId])
            .DistinctBy(item => item.SourceGroupId, StringComparer.Ordinal)
            .OrderBy(item => item.SourceGroupId, StringComparer.Ordinal)
            .ToArray();
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
                .Select(item => item.ConstraintId)
                .OrderBy(item => item, StringComparer.Ordinal)
                .ToArray();
            var conflicts = generation
                .Where(item => item.EvidenceBoundaryState == EvidenceState.Conflicting ||
                    (inconsistentConfirmed && IsConfirmedPersonEvidence(item)) ||
                    (hasConflictingEvidence && IsConfirmedPersonEvidence(item) &&
                        !item.AllowedRegionIds.Contains(region, StringComparer.Ordinal)))
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
        var reconstructionSteps = 1 + generationGroups.Length + candidates.Length + verificationGroupsInspected;
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
            EvidenceRecordsInspected = evidenceSet.ObservationIds.Count + generation.Length +
                availableVerificationConstraintCount,
            SourceGroupsInspected = generationGroups.Length + verificationGroupsInspected,
            PublicRelationsInspected = generation.Length + availableVerificationConstraintCount
        };
    }

    private static IReadOnlyList<CandidateVerification> Verify(
        PastStatePublicContext context,
        PublicEvidenceSet evidenceSet,
        IReadOnlyList<PastStateCandidate> candidates,
        IReadOnlyDictionary<string, TracingSourceGroup> groupByConstraint)
    {
        var verificationSources = context.VerificationDirectSourceIds.ToHashSet(StringComparer.Ordinal);
        var constraints = evidenceSet.Constraints
            .Where(item => verificationSources.Contains(item.DirectSourceId))
            .Where(item => Overlaps(item.ConstrainedTimeWindow, context.Query.TargetTimeWindow))
            .OrderBy(item => item.ConstraintId, StringComparer.Ordinal)
            .ToArray();
        var groups = constraints
            .Select(item => groupByConstraint[item.ConstraintId])
            .DistinctBy(item => item.SourceGroupId, StringComparer.Ordinal)
            .OrderBy(item => item.SourceGroupId, StringComparer.Ordinal)
            .ToArray();

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
            var overlap = sharedGroups
                .SelectMany(item => item.SourceAncestryIds)
                .Where(generationAncestry.Contains)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(item => item, StringComparer.Ordinal)
                .ToArray();
            var disposition = conflicting.Length > 0 || (supporting.Length > 0 && refuting.Length > 0)
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
        IReadOnlyList<PublicCandidateWarpStateDefinition> states)
    {
        var warps = PublicCandidateWarpFactory.Create(
            context.Query.ScenarioId,
            context.Query.Seed,
            context.Query.TargetEntityId,
            states);
        var selectedConstraints = method == PastStateMethod.BackwardTraversal
            ? evidenceSet.Constraints.Where(item =>
                !Overlaps(item.ConstrainedTimeWindow, context.Query.TargetTimeWindow)).ToArray()
            : evidenceSet.Constraints.Where(item => states.Any(state =>
                Overlaps(item.ConstrainedTimeWindow, state.TimeWindow))).ToArray();
        var tracing = new OperationalCausalTracer().Trace(
            warps,
            selectedConstraints,
            AccessContext(context.Query),
            context.PublicHazardEnvelope);
        return new PastStateMethodResult
        {
            Method = method,
            CandidateRegionIds = tracing.CandidateWarps
                .OuterPlausibleRegionIdsAt(context.Query.TargetTimeWindow.StartTick),
            Steps = method == PastStateMethod.BackwardTraversal
                ? tracing.Steps.Count(item => item.Direction == CausalTracingDirection.Backward)
                : tracing.Steps.Count,
            PublicRelationsInspected = tracing.Steps.Count,
            EvidenceRecordsInspected = tracing.Steps
                .Select(item => item.SourceObservationId)
                .Where(item => item is not null)
                .Distinct(StringComparer.Ordinal)
                .Count(),
            SourceGroupsInspected = tracing.SourceGroups.Count,
            TracingSessionId = tracing.SessionId
        };
    }

    private static IReadOnlyList<PublicCandidateWarpStateDefinition> BuildLocalStates(
        PastStatePublicContext context,
        IReadOnlyList<PastStateCandidate> candidates)
    {
        var minimum = Math.Max(0, context.Query.TargetTimeWindow.StartTick - context.LocalTraversalWindowPaddingTicks);
        var maximum = checked(context.Query.TargetTimeWindow.EndTick + context.LocalTraversalWindowPaddingTicks);
        var local = context.PublicCandidateStates
            .Where(item => item.TimeWindow.StartTick <= maximum && minimum <= item.TimeWindow.EndTick)
            .OrderBy(item => item.TimeWindow.StartTick)
            .ThenBy(item => item.TimeWindow.EndTick)
            .Select(item => Overlaps(item.TimeWindow, context.Query.TargetTimeWindow)
                ? item with
                {
                    RegionIds = candidates
                        .Select(candidate => candidate.RegionId)
                        .Distinct(StringComparer.Ordinal)
                        .OrderBy(region => region, StringComparer.Ordinal)
                        .ToArray()
                }
                : item)
            .ToArray();
        return local.Length == 0
            ? throw new InvalidOperationException("No public candidate states are available for local traversal.")
            : local;
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
            item.AllowedRegionIds.Contains(region, StringComparer.Ordinal)))
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
