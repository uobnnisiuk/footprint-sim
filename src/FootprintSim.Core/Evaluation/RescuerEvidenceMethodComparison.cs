using FootprintSim.Evidence;
using FootprintSim.Inference;
using FootprintSim.Inference.PastStates;
using FootprintSim.Interactions;
using FootprintSim.Interactions.Tracing;

namespace FootprintSim.Evaluation;

/// <summary>
/// A bounded, receipt-gated, truth-free comparison. Every method consumes the
/// same public input; role selection is an explicit method difference.
/// </summary>
public sealed record RescuerEvidenceMethodRow
{
    public required string Method { get; init; }
    public required string EvidenceStatus { get; init; }
    public IReadOnlyList<string> OuterCandidateRegionIds { get; init; } = [];
    public required int CandidateCount { get; init; }
    public required int BaselineCandidateCount { get; init; }
    public required int CandidateReduction { get; init; }
    // Null means this particular existing method does not expose the standing,
    // not an empty preferred/conflict/unknown set.
    public IReadOnlyList<string>? PreferredCandidateRegionIds { get; init; }
    public IReadOnlyList<string>? ConflictedCandidateRegionIds { get; init; }
    public IReadOnlyList<string>? UncertainCandidateRegionIds { get; init; }
    public IReadOnlyList<string> GenerationDirectSourceIds { get; init; } = [];
    public IReadOnlyList<string> TraversalDirectSourceIds { get; init; } = [];
    public IReadOnlyList<string> UnusedDirectSourceIds { get; init; } = [];
    public required int SourceGroupCount { get; init; }
    public required int Steps { get; init; }
    public required int GenerationSteps { get; init; }
    public required int VerificationSteps { get; init; }
    public required int ForwardTransitionSteps { get; init; }
    public required int BackwardTransitionSteps { get; init; }
    public required int EvidenceRecordsInspected { get; init; }
    public required int PublicRelationsInspected { get; init; }
    public IReadOnlyList<string> PublicTransitionIds { get; init; } = [];
}

public sealed record RescuerEvidenceMethodQueryRow
{
    public required string Condition { get; init; } // actual or same-cutoff-ablation
    public required long QueryTick { get; init; }
    public required int ReceivedOriginalCount { get; init; }
    public required int SourceGroupCount { get; init; }
    public IReadOnlyList<string> ReceivedOriginalIds { get; init; } = [];
    public IReadOnlyList<string> ReceivedSourceObservationIds { get; init; } = [];
    public required int AvailableConstraintCount { get; init; }
    public IReadOnlyList<RescuerEvidenceMethodRow> Methods { get; init; } = [];
}

public sealed record RescuerEvidenceMethodFirstDifference(
    string Method,
    long? FirstDifferentTick);

public sealed record RescuerEvidenceMethodReport
{
    public int SchemaVersion { get; init; } = 1;
    public required string ScenarioId { get; init; }
    public required int Seed { get; init; }
    public required string RescuerId { get; init; }
    public required InteractionTimeWindow TargetWindow { get; init; }
    public IReadOnlyList<RescuerEvidenceMethodQueryRow> ActualTimeline { get; init; } = [];
    public IReadOnlyList<RescuerEvidenceMethodQueryRow> SameCutoffAblations { get; init; } = [];
    public IReadOnlyList<RescuerEvidenceMethodFirstDifference> FirstDifferences { get; init; } = [];
}

// Only the separate evaluation call receives a synthetic truth region.
public sealed record RescuerEvidenceMethodTruthRow(
    string Condition,
    long QueryTick,
    int ReceivedOriginalCount,
    string Method,
    bool ContainsSyntheticTruth,
    bool FalseExclusion);

public sealed record RescuerEvidenceMethodTruthReport(
    string SyntheticTruthRegionId,
    IReadOnlyList<RescuerEvidenceMethodTruthRow> Evaluations);

public static class RescuerEvidenceMethodComparison
{
    /// <summary>
    /// Builds both the actual receipt timeline and a same-cutoff 0/1/2-original
    /// ablation. With one selected original, both possible sources are tested.
    /// Withholding an already-received original is a controlled ablation, not
    /// a statement about what the rescuer actually knew.
    /// </summary>
    public static RescuerEvidenceMethodReport Run(
        RescueContext publicContext,
        int seed,
        string rescuerId,
        IEnumerable<DeviceHeldInteractionRecord> heldOriginals,
        IEnumerable<InteractionRecordReceipt> receipts,
        InteractionTimeWindow targetWindow,
        IEnumerable<long> actualQueryTicks,
        long ablationCutoff)
    {
        ArgumentNullException.ThrowIfNull(publicContext);
        ArgumentNullException.ThrowIfNull(heldOriginals);
        ArgumentNullException.ThrowIfNull(receipts);
        ArgumentNullException.ThrowIfNull(targetWindow);
        ArgumentNullException.ThrowIfNull(actualQueryTicks);
        var held = heldOriginals.ToArray();
        var ledger = receipts.ToArray();
        var ticks = actualQueryTicks.OrderBy(tick => tick).ToArray();
        if (held.Length != 2 || ticks.Length == 0 || ticks.Distinct().Count() != ticks.Length ||
            ticks.Any(tick => tick < targetWindow.EndTick) || ablationCutoff < targetWindow.EndTick)
        {
            throw new ArgumentException("Two originals, valid distinct query ticks and a legal cutoff are required.");
        }

        var builder = new RescuerEvidenceInputBuilder();
        RescuerEvidenceMethodQueryRow At(long tick, string condition,
            IReadOnlySet<string>? includeOriginalIds = null)
        {
            var selectedReceipts = includeOriginalIds is null
                ? ledger
                : ledger.Where(receipt => includeOriginalIds.Contains(receipt.OriginalRecordId)).ToArray();
            var snapshot = builder.BuildDistinctOriginals(
                publicContext, seed, tick, rescuerId, held, selectedReceipts);
            return Compare(snapshot, targetWindow, condition);
        }

        var actual = ticks.Select(tick => At(tick, "actual")).ToArray();
        // First validate the complete ledger and use only genuine receipts by
        // this cutoff. No identity from a future receipt enters an actual row.
        var atCutoff = builder.BuildDistinctOriginals(
            publicContext, seed, ablationCutoff, rescuerId, held, ledger);
        if (atCutoff.ReceivedRecords.Count != 2)
        {
            throw new ArgumentException("Both originals must be received by the ablation cutoff.");
        }

        var ids = atCutoff.ReceivedRecords
            .OrderBy(item => item.FirstReceivedAtTick)
            .ThenBy(item => item.OriginalRecordId, StringComparer.Ordinal)
            .Select(item => item.OriginalRecordId)
            .ToArray();
        var ablations = new[]
        {
            Array.Empty<string>(),
            new[] { ids[0] },
            new[] { ids[1] },
            ids
        }.Select(set => At(
            ablationCutoff,
            "same-cutoff-ablation",
            new HashSet<string>(set, StringComparer.Ordinal))).ToArray();

        var first = actual[0].Methods
            .Where(method => method.Method != "public-prior")
            .Select(method => new RescuerEvidenceMethodFirstDifference(
                method.Method,
                actual.Where(row =>
                {
                    var baseline = row.Methods.Single(item => item.Method == "public-prior");
                    var compared = row.Methods.Single(item => item.Method == method.Method);
                    return !compared.OuterCandidateRegionIds.SequenceEqual(
                        baseline.OuterCandidateRegionIds, StringComparer.Ordinal);
                }).Select(row => (long?)row.QueryTick).FirstOrDefault()))
            .ToArray();
        return new RescuerEvidenceMethodReport
        {
            ScenarioId = publicContext.ScenarioId,
            Seed = seed,
            RescuerId = rescuerId,
            TargetWindow = targetWindow,
            ActualTimeline = actual,
            SameCutoffAblations = ablations,
            FirstDifferences = first
        };
    }

    /// <summary>
    /// Compare actual, previously receipt-projected evidence only. Never pass
    /// private InteractionObservation or synthetic truth to the reconstructor.
    /// </summary>
    public static RescuerEvidenceMethodQueryRow Compare(
        RescuerEvidenceInput received,
        InteractionTimeWindow targetWindow,
        string condition = "actual")
    {
        ArgumentNullException.ThrowIfNull(received);
        ArgumentNullException.ThrowIfNull(targetWindow);
        var input = received.Input;
        if (input.CurrentTick < targetWindow.EndTick)
        {
            throw new ArgumentOutOfRangeException(nameof(targetWindow));
        }

        var states = input.PublicContext.PublicCausalTracingModel?.CandidateStates
            ?? throw new ArgumentException("A public candidate-state model is required.");
        var envelope = input.PublicContext.PublicHazardEnvelope;
        if (envelope is not null &&
            (envelope.PublishedAtTick > input.CurrentTick ||
                envelope.Branches.Any(branch => branch.PublishedAtTick > input.CurrentTick)))
        {
            throw new ArgumentException("A public transition is not yet published at the query cutoff.");
        }
        if (states.SelectMany(state => state.Provenance)
            .Any(source => source.AvailableAtTick > input.CurrentTick))
        {
            throw new ArgumentException("A public candidate state is not yet available.");
        }

        var baseRegions = states
            .Where(state => state.TimeWindow.StartTick <= targetWindow.EndTick &&
                targetWindow.StartTick <= state.TimeWindow.EndTick)
            .SelectMany(state => state.RegionIds)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToArray();
        if (baseRegions.Length == 0)
        {
            throw new ArgumentException("There is no public candidate state at the target window.");
        }

        var constraints = input.AvailableCausalTracingConstraints?
            .PubliclyAvailableAt(input.CurrentTick).Constraints
            ?? [];
        var allSources = constraints.Select(item => item.DirectSourceId)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToArray();
        var targetSources = constraints
            .Where(item => item.ConstrainedTimeWindow.StartTick <= targetWindow.EndTick &&
                targetWindow.StartTick <= item.ConstrainedTimeWindow.EndTick)
            .Select(item => item.DirectSourceId)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToArray();
        var laterSources = constraints
            .Where(item => item.ConstrainedTimeWindow.StartTick > targetWindow.EndTick)
            .Select(item => item.DirectSourceId)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToArray();
        var query = PastStateQuery.Create(
            input.ScenarioId, input.Seed, input.PublicContext.TargetPersonId,
            targetWindow, "receipt-gated-method-comparison", input.CurrentTick);
        PastStatePublicContext Context(
            IReadOnlyList<string> generation,
            IReadOnlyList<string> traversal) => new()
            {
                Query = query,
                PublicCandidateStates = states,
                GenerationDirectSourceIds = generation,
                VerificationDirectSourceIds = [],
                TraversalDirectSourceIds = traversal,
                AvailableObservationIds = input.AvailableEvidence.Observations
                .Select(item => item.ObservationId).ToArray(),
                LocalTraversalWindowPaddingTicks = 0,
                PublicHazardEnvelope = envelope
            };
        var reconstructor = new PastStateReconstructor();
        var available = input.AvailableCausalTracingConstraints
            ?? new CausalTracingConstraintSet(input.ScenarioId, input.Seed, []);
        // Both calls receive the identical eligible set. Only the role lists
        // differ. We select B's backward result and C's two distinct outputs;
        // other Compare results are not counted as independent methods.
        var backwardComparison = reconstructor.Compare(
            Context([], laterSources), input.AvailableEvidence, available);
        var weftComparison = reconstructor.Compare(
            Context(targetSources, laterSources), input.AvailableEvidence, available);
        var backward = backwardComparison.Methods.Single(
            item => item.Method == PastStateMethod.BackwardTraversal);
        var reconstruction = weftComparison.Methods.Single(
            item => item.Method == PastStateMethod.ConstraintBasedReconstruction);
        var local = weftComparison.Methods.Single(
            item => item.Method == PastStateMethod.ReconstructionWithLocalTraversal);
        var groups = weftComparison.Reconstruction.EvidenceSet.SourceGroups.Count;
        var confirmed = new LastConfirmedOnlyStrategy().Evaluate(input);
        var hasLastConfirmed = confirmed.ConfirmedPersonRegionIds.Count > 0;

        RescuerEvidenceMethodRow Make(
            string methodName,
            PastStateMethodResult method,
            IReadOnlyList<string> generation,
            IReadOnlyList<string> traversal,
            bool exposeReconstructionStanding)
        {
            var regions = method.CandidateRegionIds
                .Distinct(StringComparer.Ordinal)
                .OrderBy(id => id, StringComparer.Ordinal)
                .ToArray();
            var standing = weftComparison.Reconstruction.Candidates
                .Where(candidate => regions.Contains(candidate.RegionId, StringComparer.Ordinal))
                .ToArray();
            return new RescuerEvidenceMethodRow
            {
                Method = methodName,
                EvidenceStatus = generation.Count == 0 && traversal.Count == 0
                    ? "public-model-only" : "method-executed",
                OuterCandidateRegionIds = regions,
                CandidateCount = regions.Length,
                BaselineCandidateCount = baseRegions.Length,
                CandidateReduction = baseRegions.Length - regions.Length,
                PreferredCandidateRegionIds = null,
                ConflictedCandidateRegionIds = exposeReconstructionStanding
                    ? standing.Where(item => item.Disposition == PastStateCandidateDisposition.Conflicting)
                        .Select(item => item.RegionId).OrderBy(id => id, StringComparer.Ordinal).ToArray()
                    : null,
                UncertainCandidateRegionIds = exposeReconstructionStanding
                    ? standing.Where(item => item.Disposition is
                            PastStateCandidateDisposition.Uncertain or
                            PastStateCandidateDisposition.Unavailable or
                            PastStateCandidateDisposition.Unsupported)
                        .Select(item => item.RegionId).OrderBy(id => id, StringComparer.Ordinal).ToArray()
                    : null,
                GenerationDirectSourceIds = generation,
                TraversalDirectSourceIds = traversal,
                UnusedDirectSourceIds = allSources
                    .Except(generation.Concat(traversal), StringComparer.Ordinal)
                    .OrderBy(id => id, StringComparer.Ordinal).ToArray(),
                SourceGroupCount = groups,
                Steps = method.Steps,
                GenerationSteps = method.CandidateGenerationSteps,
                VerificationSteps = method.CandidateVerificationSteps,
                ForwardTransitionSteps = method.ForwardTransitionSteps,
                BackwardTransitionSteps = method.BackwardTransitionSteps,
                EvidenceRecordsInspected = method.EvidenceRecordsInspected,
                PublicRelationsInspected = method.PublicRelationsInspected,
                PublicTransitionIds = method.TransitionIds
            };
        }

        var baseline = new RescuerEvidenceMethodRow
        {
            Method = "public-prior",
            EvidenceStatus = hasLastConfirmed
                ? "last-confirmed-available-but-not-used-for-historical-prior"
                : "last-confirmed-unavailable-public-prior",
            OuterCandidateRegionIds = baseRegions,
            CandidateCount = baseRegions.Length,
            BaselineCandidateCount = baseRegions.Length,
            CandidateReduction = 0,
            UnusedDirectSourceIds = allSources,
            SourceGroupCount = groups,
            Steps = 0,
            GenerationSteps = 0,
            VerificationSteps = 0,
            ForwardTransitionSteps = 0,
            BackwardTransitionSteps = 0,
            EvidenceRecordsInspected = 0,
            PublicRelationsInspected = 0
        };
        return new RescuerEvidenceMethodQueryRow
        {
            Condition = condition,
            QueryTick = input.CurrentTick,
            ReceivedOriginalCount = received.ReceivedRecords.Count,
            SourceGroupCount = groups,
            ReceivedOriginalIds = received.ReceivedRecords
                .Select(item => item.OriginalRecordId)
                .OrderBy(id => id, StringComparer.Ordinal).ToArray(),
            ReceivedSourceObservationIds = received.ReceivedRecords
                .Select(item => item.OriginalObservationId)
                .OrderBy(id => id, StringComparer.Ordinal).ToArray(),
            AvailableConstraintCount = constraints.Count,
            Methods =
            [
                baseline,
                Make("known-relation-backward", backward, [], laterSources, false),
                Make("distributed-reconstruction", reconstruction, targetSources, [], true),
                Make("distributed-reconstruction-local", local, targetSources, laterSources, true)
            ]
        };
    }

    /// <summary>
    /// Synthetic truth scoring is deliberately unavailable to Run and Compare.
    /// Call this only after the entire public report has been constructed.
    /// </summary>
    public static RescuerEvidenceMethodTruthReport EvaluateSyntheticTruth(
        RescuerEvidenceMethodReport completedPublicReport,
        string syntheticTruthRegionId)
    {
        ArgumentNullException.ThrowIfNull(completedPublicReport);
        if (string.IsNullOrWhiteSpace(syntheticTruthRegionId))
        {
            throw new ArgumentException("A truth region is required.", nameof(syntheticTruthRegionId));
        }

        return new RescuerEvidenceMethodTruthReport(
            syntheticTruthRegionId,
            completedPublicReport.ActualTimeline.Concat(completedPublicReport.SameCutoffAblations)
                .SelectMany(row => row.Methods.Select(method =>
                {
                    var contains = method.OuterCandidateRegionIds.Contains(
                        syntheticTruthRegionId, StringComparer.Ordinal);
                    return new RescuerEvidenceMethodTruthRow(
                        row.Condition, row.QueryTick, row.ReceivedOriginalCount,
                        method.Method, contains, !contains);
                })).ToArray());
    }
}
