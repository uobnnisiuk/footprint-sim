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
    // Eligible ancestry groups and actual method-use groups are separate.
    // One group can cover multiple originals with shared ancestry.
    public required int AvailableSourceGroupCount { get; init; }
    public required int UsedSourceGroupCount { get; init; }
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
    public required int AvailableSourceGroupCount { get; init; }
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


public sealed record RescuerEvidencePairedCondition
{
    public required string Label { get; init; }
    public required long QueryTick { get; init; }
    public IReadOnlyList<string> ReceivedOriginalIds { get; init; } = [];
    public required int ReceivedOriginalCount { get; init; }
    public required int AvailableSourceGroupCount { get; init; }
    public required string EvidenceStatus { get; init; }
    public IReadOnlyList<string> OuterCandidateRegionIds { get; init; } = [];
    public required int CandidateCount { get; init; }
    public IReadOnlyList<string>? PreferredCandidateRegionIds { get; init; }
    public IReadOnlyList<string>? UncertainCandidateRegionIds { get; init; }
    public IReadOnlyList<string>? ConflictedCandidateRegionIds { get; init; }
    public IReadOnlyList<string> GenerationDirectSourceIds { get; init; } = [];
    public IReadOnlyList<string> TraversalDirectSourceIds { get; init; } = [];
    public required int UsedSourceGroupCount { get; init; }
    public required int GenerationSteps { get; init; }
    public required int VerificationSteps { get; init; }
    public required int ForwardTransitionSteps { get; init; }
    public required int BackwardTransitionSteps { get; init; }
    public required int EvidenceRecordsInspected { get; init; }
    public required int PublicRelationsInspected { get; init; }
    public IReadOnlyList<string> PublicTransitionIds { get; init; } = [];
}

public sealed record RescuerEvidencePairedMethodContribution
{
    public required string Method { get; init; }
    public required string Status { get; init; }
    public string? UnavailableReason { get; init; }
    public IReadOnlyList<RescuerEvidencePairedCondition> Conditions { get; init; } = [];
    public bool? BothStrictlyNarrowerThanEachSingle { get; init; }
    // Negative values mean that adding a trace increased candidates.
    public int? CandidateReductionFromFirst { get; init; }
    public int? CandidateReductionFromSecond { get; init; }
    public IReadOnlyList<string>? ExcludedWhenAddingSecond { get; init; }
    public IReadOnlyList<string>? ExcludedWhenAddingFirst { get; init; }
    public IReadOnlyList<string>? AdditionalExclusionsBeyondSingles { get; init; }
    public IReadOnlyList<string>? ReappearedComparedWithEitherSingle { get; init; }
}

public sealed record RescuerEvidencePairedContributionReport
{
    public required string Status { get; init; }
    public string? UnavailableReason { get; init; }
    public long? QueryTick { get; init; }
    public IReadOnlyList<string> OriginalIds { get; init; } = [];
    public IReadOnlyList<RescuerEvidencePairedMethodContribution> Methods { get; init; } = [];
}


//// #90: Published timeline comparison. Source-group IDs are not exposed by
//// Run; counts are not a claim of independently verified real-world sources.
public sealed record RescuerEvidenceReceiptTimingTick
{
    public required long QueryTick { get; init; }
    public required string Status { get; init; }
    public string? UnavailableReason { get; init; }
    public IReadOnlyList<string> EarlyReceivedOriginalIds { get; init; } = [];
    public IReadOnlyList<string> LateReceivedOriginalIds { get; init; } = [];
    public IReadOnlyList<string> EarlyReceivedSourceObservationIds { get; init; } = [];
    public IReadOnlyList<string> LateReceivedSourceObservationIds { get; init; } = [];
    public required int EarlyAvailableSourceGroupCount { get; init; }
    public required int LateAvailableSourceGroupCount { get; init; }
    public required int EarlyAvailableConstraintCount { get; init; }
    public required int LateAvailableConstraintCount { get; init; }
    public RescuerEvidenceMethodRow? EarlyMethod { get; init; }
    public RescuerEvidenceMethodRow? LateMethod { get; init; }
    // Signed: negative means fewer candidates in the early-arrival schedule.
    public int? EarlyMinusLateCandidateCount { get; init; }
    public IReadOnlyList<string>? EarlyOnlyCandidateRegionIds { get; init; }
    public IReadOnlyList<string>? LateOnlyCandidateRegionIds { get; init; }
    public bool? EarlyCandidatesSubsetOfLate { get; init; }
    public bool? LateCandidatesSubsetOfEarly { get; init; }
    public bool? SameOuterCandidates { get; init; }
    public bool? SameMethodResult { get; init; }
}

public sealed record RescuerEvidenceReceiptTimingMethod
{
    public required string Method { get; init; }
    public required string Status { get; init; }
    public long? FirstCandidateDifferentTick { get; init; }
    public long? FirstMethodResultDifferentTick { get; init; }
    // First later matching row *among the supplied query ticks*.
    public long? FirstCandidateConvergenceTick { get; init; }
    public long? FirstMethodResultConvergenceTick { get; init; }
    public IReadOnlyList<RescuerEvidenceReceiptTimingTick> Timeline { get; init; } = [];
}

public sealed record RescuerEvidenceReceiptTimingReport
{
    public required string Status { get; init; }
    public string? UnavailableReason { get; init; }
    public IReadOnlyList<long> QueryTicks { get; init; } = [];
    public IReadOnlyList<RescuerEvidenceReceiptTimingMethod> Methods { get; init; } = [];
}

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
        var availableGroups = weftComparison.Reconstruction.EvidenceSet.SourceGroups;
        var confirmed = new LastConfirmedOnlyStrategy().Evaluate(input);
        var hasLastConfirmed = confirmed.ConfirmedPersonRegionIds.Count > 0;

        RescuerEvidenceMethodRow Make(
            string methodName,
            PastStateMethodResult method,
            bool exposeReconstructionStanding)
        {
            // Count only actual usage records emitted by M1-E. Local traversal
            // keeps its own traversal diagnostics; its generation work belongs
            // to the separately selected reconstruction result.
            var methodUsages = method.Diagnostics!.ConstraintUsages;
            var generationUsages = method.Method == PastStateMethod.ReconstructionWithLocalTraversal
                ? reconstruction.Diagnostics!.ConstraintUsages
                : methodUsages;
            var usedGeneration = generationUsages
                .Where(item => item.UsageRoles.Contains(PastStateEvidenceUsageRole.CandidateGeneration))
                .Select(item => item.DirectSourceId)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(id => id, StringComparer.Ordinal).ToArray();
            var usedTraversal = methodUsages
                .Where(item => item.UsageRoles.Contains(PastStateEvidenceUsageRole.Traversal))
                .Select(item => item.DirectSourceId)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(id => id, StringComparer.Ordinal).ToArray();
            var usedConstraintIds = generationUsages
                .Where(item => item.UsageRoles.Contains(PastStateEvidenceUsageRole.CandidateGeneration))
                .Concat(methodUsages.Where(item =>
                    item.UsageRoles.Contains(PastStateEvidenceUsageRole.Traversal)))
                .Select(item => item.ConstraintId)
                .ToHashSet(StringComparer.Ordinal);
            var usedGroupCount = availableGroups.Count(group =>
                group.ConstraintIds.Any(usedConstraintIds.Contains));
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
                EvidenceStatus = usedGeneration.Length == 0 && usedTraversal.Length == 0
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
                GenerationDirectSourceIds = usedGeneration,
                TraversalDirectSourceIds = usedTraversal,
                UnusedDirectSourceIds = allSources
                    .Except(usedGeneration.Concat(usedTraversal), StringComparer.Ordinal)
                    .OrderBy(id => id, StringComparer.Ordinal).ToArray(),
                AvailableSourceGroupCount = availableGroups.Count,
                UsedSourceGroupCount = usedGroupCount,
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
            AvailableSourceGroupCount = availableGroups.Count,
            UsedSourceGroupCount = 0,
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
            AvailableSourceGroupCount = availableGroups.Count,
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
                Make("known-relation-backward", backward, false),
                Make("distributed-reconstruction", reconstruction, true),
                Make("distributed-reconstruction-local", local, true)
            ]
        };
    }


    /// <summary>
    /// Compare four published same-cutoff ablations without reading originals,
    /// private observations, synthetic truth or the inference engine.
    /// Missing rows are unavailable, not zero-sized candidate sets.
    /// </summary>
    public static RescuerEvidencePairedContributionReport AnalyzePairedContributions(
        RescuerEvidenceMethodReport completedPublicReport)
    {
        ArgumentNullException.ThrowIfNull(completedPublicReport);
        var ablations = completedPublicReport.SameCutoffAblations;
        static RescuerEvidencePairedContributionReport Unavailable(string reason) => new()
        {
            Status = "unavailable",
            UnavailableReason = reason
        };
        if (ablations.Count != 4 ||
            ablations.Any(row => row.Condition != "same-cutoff-ablation" ||
                row.QueryTick != ablations[0].QueryTick ||
                row.ReceivedOriginalCount != row.ReceivedOriginalIds.Count ||
                row.ReceivedOriginalIds.Distinct(StringComparer.Ordinal).Count() !=
                    row.ReceivedOriginalIds.Count))
        {
            return Unavailable("Four compatible, distinct-receipt ablations are required.");
        }

        var none = ablations.Where(row => row.ReceivedOriginalCount == 0).ToArray();
        var both = ablations.Where(row => row.ReceivedOriginalCount == 2).ToArray();
        var singles = ablations.Where(row => row.ReceivedOriginalCount == 1).ToArray();
        if (none.Length != 1 || both.Length != 1 || singles.Length != 2)
        {
            return Unavailable("A zero, two singleton and paired ablations are required.");
        }

        var originalIds = both[0].ReceivedOriginalIds
            .OrderBy(id => id, StringComparer.Ordinal).ToArray();
        if (originalIds.Length != 2 ||
            !singles.Select(row => row.ReceivedOriginalIds[0])
                .OrderBy(id => id, StringComparer.Ordinal)
                .SequenceEqual(originalIds, StringComparer.Ordinal))
        {
            return Unavailable("Each singleton must correspond to a paired original.");
        }

        var rows = new[]
        {
            none[0],
            singles.Single(row => row.ReceivedOriginalIds[0] == originalIds[0]),
            singles.Single(row => row.ReceivedOriginalIds[0] == originalIds[1]),
            both[0]
        };
        static string[] Sort(IEnumerable<string> ids) =>
            ids.Distinct(StringComparer.Ordinal).OrderBy(id => id, StringComparer.Ordinal).ToArray();
        // Run/Compare exposes these four named methods. An absent method is
        // not an empty candidate set, even when all four ablation rows exist.
        // Include observed names as well so an unexpected method remains visible.
        var expectedMethods = new[]
        {
            "public-prior",
            "known-relation-backward",
            "distributed-reconstruction",
            "distributed-reconstruction-local"
        };
        var methodNames = expectedMethods
            .Concat(rows.SelectMany(row => row.Methods.Select(item => item.Method)))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(id => id, StringComparer.Ordinal).ToArray();
        var methods = methodNames.Select(name =>
        {
            var matches = rows.Select(row => row.Methods.Where(method => method.Method == name)
                .ToArray()).ToArray();
            if (matches.Any(group => group.Length != 1 ||
                group[0].OuterCandidateRegionIds is null ||
                group[0].CandidateCount != group[0].OuterCandidateRegionIds.Count ||
                group[0].UsedSourceGroupCount > group[0].AvailableSourceGroupCount))
            {
                return new RescuerEvidencePairedMethodContribution
                {
                    Method = name,
                    Status = "unavailable",
                    UnavailableReason = "Missing, ambiguous or inconsistent method result."
                };
            }

            var values = matches.Select(group => group[0]).ToArray();
            var prior = values[0].OuterCandidateRegionIds;
            var first = values[1].OuterCandidateRegionIds;
            var second = values[2].OuterCandidateRegionIds;
            var pair = values[3].OuterCandidateRegionIds;
            var conditionLabels = new[] { "neither", "first-only", "second-only", "both" };
            var conditions = rows.Select((row, index) =>
            {
                var value = values[index];
                return new RescuerEvidencePairedCondition
                {
                    Label = conditionLabels[index],
                    QueryTick = row.QueryTick,
                    ReceivedOriginalIds = Sort(row.ReceivedOriginalIds),
                    ReceivedOriginalCount = row.ReceivedOriginalCount,
                    AvailableSourceGroupCount = row.AvailableSourceGroupCount,
                    EvidenceStatus = value.EvidenceStatus,
                    OuterCandidateRegionIds = Sort(value.OuterCandidateRegionIds),
                    CandidateCount = value.CandidateCount,
                    PreferredCandidateRegionIds = value.PreferredCandidateRegionIds is null
                        ? null : Sort(value.PreferredCandidateRegionIds),
                    UncertainCandidateRegionIds = value.UncertainCandidateRegionIds is null
                        ? null : Sort(value.UncertainCandidateRegionIds),
                    ConflictedCandidateRegionIds = value.ConflictedCandidateRegionIds is null
                        ? null : Sort(value.ConflictedCandidateRegionIds),
                    GenerationDirectSourceIds = Sort(value.GenerationDirectSourceIds),
                    TraversalDirectSourceIds = Sort(value.TraversalDirectSourceIds),
                    UsedSourceGroupCount = value.UsedSourceGroupCount,
                    GenerationSteps = value.GenerationSteps,
                    VerificationSteps = value.VerificationSteps,
                    ForwardTransitionSteps = value.ForwardTransitionSteps,
                    BackwardTransitionSteps = value.BackwardTransitionSteps,
                    EvidenceRecordsInspected = value.EvidenceRecordsInspected,
                    PublicRelationsInspected = value.PublicRelationsInspected,
                    PublicTransitionIds = Sort(value.PublicTransitionIds)
                };
            }).ToArray();
            return new RescuerEvidencePairedMethodContribution
            {
                Method = name,
                Status = "available",
                Conditions = conditions,
                BothStrictlyNarrowerThanEachSingle =
                    !pair.Except(first, StringComparer.Ordinal).Any() &&
                    !pair.Except(second, StringComparer.Ordinal).Any() &&
                    first.Except(pair, StringComparer.Ordinal).Any() &&
                    second.Except(pair, StringComparer.Ordinal).Any(),
                CandidateReductionFromFirst = first.Count - pair.Count,
                CandidateReductionFromSecond = second.Count - pair.Count,
                ExcludedWhenAddingSecond = Sort(first.Except(pair, StringComparer.Ordinal)),
                ExcludedWhenAddingFirst = Sort(second.Except(pair, StringComparer.Ordinal)),
                AdditionalExclusionsBeyondSingles = Sort(prior
                    .Intersect(first, StringComparer.Ordinal)
                    .Intersect(second, StringComparer.Ordinal)
                    .Except(pair, StringComparer.Ordinal)),
                ReappearedComparedWithEitherSingle = Sort(pair.Except(
                    first.Intersect(second, StringComparer.Ordinal), StringComparer.Ordinal))
            };
        }).ToArray();

        return new RescuerEvidencePairedContributionReport
        {
            Status = methods.All(item => item.Status == "available") ? "available" :
                methods.All(item => item.Status == "unavailable") ? "unavailable" : "partial",
            QueryTick = ablations[0].QueryTick,
            OriginalIds = originalIds,
            Methods = methods
        };
    }


    /// <summary>
    /// Compare two already-published actual receipt timelines without reading
    /// originals, truth, private observations or re-running inference. Callers
    /// must establish identical public worlds/originals separately: report
    /// metadata equality alone cannot prove that experimental control.
    /// </summary>
    public static RescuerEvidenceReceiptTimingReport AnalyzeReceiptDecisionTiming(
        RescuerEvidenceMethodReport early,
        RescuerEvidenceMethodReport late)
    {
        ArgumentNullException.ThrowIfNull(early);
        ArgumentNullException.ThrowIfNull(late);
        static RescuerEvidenceReceiptTimingReport Unavailable(string reason) => new()
        {
            Status = "unavailable",
            UnavailableReason = reason
        };
        if (early.SchemaVersion != late.SchemaVersion ||
            early.ScenarioId != late.ScenarioId || early.Seed != late.Seed ||
            early.RescuerId != late.RescuerId ||
            early.TargetWindow.StartTick != late.TargetWindow.StartTick ||
            early.TargetWindow.EndTick != late.TargetWindow.EndTick ||
            early.ActualTimeline.Count == 0 ||
            early.ActualTimeline.Count != late.ActualTimeline.Count)
        {
            return Unavailable("Incompatible public report metadata or timeline lengths.");
        }

        var a = early.ActualTimeline.OrderBy(row => row.QueryTick).ToArray();
        var b = late.ActualTimeline.OrderBy(row => row.QueryTick).ToArray();
        if (a.Select(row => row.QueryTick).Distinct().Count() != a.Length ||
            !a.Select(row => row.QueryTick).SequenceEqual(b.Select(row => row.QueryTick)) ||
            a.Any(row => row.Condition != "actual") ||
            b.Any(row => row.Condition != "actual"))
        {
            return Unavailable("Matching, distinct actual query ticks are required.");
        }

        static string[] Sort(IEnumerable<string> ids) =>
            ids.Distinct(StringComparer.Ordinal).OrderBy(id => id, StringComparer.Ordinal).ToArray();
        static bool Same(IReadOnlyList<string>? x, IReadOnlyList<string>? y) =>
            x is null ? y is null :
                y is not null && Sort(x).SequenceEqual(Sort(y), StringComparer.Ordinal);
        static bool SameMethod(RescuerEvidenceMethodRow x, RescuerEvidenceMethodRow y) =>
            x.Method == y.Method && x.EvidenceStatus == y.EvidenceStatus &&
            Same(x.OuterCandidateRegionIds, y.OuterCandidateRegionIds) &&
            x.CandidateCount == y.CandidateCount &&
            x.BaselineCandidateCount == y.BaselineCandidateCount &&
            x.CandidateReduction == y.CandidateReduction &&
            Same(x.PreferredCandidateRegionIds, y.PreferredCandidateRegionIds) &&
            Same(x.UncertainCandidateRegionIds, y.UncertainCandidateRegionIds) &&
            Same(x.ConflictedCandidateRegionIds, y.ConflictedCandidateRegionIds) &&
            Same(x.GenerationDirectSourceIds, y.GenerationDirectSourceIds) &&
            Same(x.TraversalDirectSourceIds, y.TraversalDirectSourceIds) &&
            Same(x.UnusedDirectSourceIds, y.UnusedDirectSourceIds) &&
            x.AvailableSourceGroupCount == y.AvailableSourceGroupCount &&
            x.UsedSourceGroupCount == y.UsedSourceGroupCount &&
            x.Steps == y.Steps && x.GenerationSteps == y.GenerationSteps &&
            x.VerificationSteps == y.VerificationSteps &&
            x.ForwardTransitionSteps == y.ForwardTransitionSteps &&
            x.BackwardTransitionSteps == y.BackwardTransitionSteps &&
            x.EvidenceRecordsInspected == y.EvidenceRecordsInspected &&
            x.PublicRelationsInspected == y.PublicRelationsInspected &&
            Same(x.PublicTransitionIds, y.PublicTransitionIds);

        // At the final common cutoff both schedules must have the same publicly
        // received original and observation identities. This is necessary but
        // insufficient to prove identical raw observations or world definitions.
        if (!Same(a[^1].ReceivedOriginalIds, b[^1].ReceivedOriginalIds) ||
            !Same(a[^1].ReceivedSourceObservationIds, b[^1].ReceivedSourceObservationIds))
        {
            return Unavailable("Final received public identities differ.");
        }

        var names = new[]
        {
            "public-prior", "known-relation-backward",
            "distributed-reconstruction", "distributed-reconstruction-local"
        }.Concat(a.Concat(b).SelectMany(row => row.Methods.Select(item => item.Method)))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(name => name, StringComparer.Ordinal).ToArray();
        var methods = names.Select(name =>
        {
            var rows = a.Zip(b, (earlyRow, lateRow) =>
            {
                var es = earlyRow.Methods.Where(item => item.Method == name).ToArray();
                var ls = lateRow.Methods.Where(item => item.Method == name).ToArray();
                var valid = es.Length == 1 && ls.Length == 1 &&
                    es[0].OuterCandidateRegionIds is not null &&
                    ls[0].OuterCandidateRegionIds is not null &&
                    es[0].CandidateCount == es[0].OuterCandidateRegionIds.Count &&
                    ls[0].CandidateCount == ls[0].OuterCandidateRegionIds.Count &&
                    es[0].UsedSourceGroupCount <= es[0].AvailableSourceGroupCount &&
                    ls[0].UsedSourceGroupCount <= ls[0].AvailableSourceGroupCount &&
                    es[0].UsedSourceGroupCount >= 0 && ls[0].UsedSourceGroupCount >= 0;
                var e = valid ? es[0] : null;
                var l = valid ? ls[0] : null;
                var eOnly = valid ? Sort(e!.OuterCandidateRegionIds.Except(
                    l!.OuterCandidateRegionIds, StringComparer.Ordinal)) : null;
                var lOnly = valid ? Sort(l!.OuterCandidateRegionIds.Except(
                    e!.OuterCandidateRegionIds, StringComparer.Ordinal)) : null;
                return new RescuerEvidenceReceiptTimingTick
                {
                    QueryTick = earlyRow.QueryTick,
                    Status = valid ? "available" : "unavailable",
                    UnavailableReason = valid ? null :
                        "Missing, duplicated or inconsistent method result.",
                    EarlyReceivedOriginalIds = Sort(earlyRow.ReceivedOriginalIds),
                    LateReceivedOriginalIds = Sort(lateRow.ReceivedOriginalIds),
                    EarlyReceivedSourceObservationIds = Sort(earlyRow.ReceivedSourceObservationIds),
                    LateReceivedSourceObservationIds = Sort(lateRow.ReceivedSourceObservationIds),
                    EarlyAvailableSourceGroupCount = earlyRow.AvailableSourceGroupCount,
                    LateAvailableSourceGroupCount = lateRow.AvailableSourceGroupCount,
                    EarlyAvailableConstraintCount = earlyRow.AvailableConstraintCount,
                    LateAvailableConstraintCount = lateRow.AvailableConstraintCount,
                    EarlyMethod = e,
                    LateMethod = l,
                    EarlyMinusLateCandidateCount = valid ? e!.CandidateCount - l!.CandidateCount : null,
                    EarlyOnlyCandidateRegionIds = eOnly,
                    LateOnlyCandidateRegionIds = lOnly,
                    EarlyCandidatesSubsetOfLate = valid ? eOnly!.Length == 0 : null,
                    LateCandidatesSubsetOfEarly = valid ? lOnly!.Length == 0 : null,
                    SameOuterCandidates = valid ? eOnly!.Length == 0 && lOnly!.Length == 0 : null,
                    SameMethodResult = valid ? SameMethod(e!, l!) : null
                };
            }).ToArray();
            static long? FirstDifference(
                IEnumerable<RescuerEvidenceReceiptTimingTick> timeline,
                Func<RescuerEvidenceReceiptTimingTick, bool?> predicate) =>
                timeline.Where(row => predicate(row) == false)
                    .Select(row => (long?)row.QueryTick).FirstOrDefault();
            static long? Rejoin(
                IEnumerable<RescuerEvidenceReceiptTimingTick> timeline,
                long? after,
                Func<RescuerEvidenceReceiptTimingTick, bool?> predicate) =>
                after is null ? null :
                    timeline.Where(row => row.QueryTick > after && predicate(row) == true)
                        .Select(row => (long?)row.QueryTick).FirstOrDefault();
            var firstCandidates = FirstDifference(rows, row => row.SameOuterCandidates);
            var firstMethod = FirstDifference(rows, row => row.SameMethodResult);
            return new RescuerEvidenceReceiptTimingMethod
            {
                Method = name,
                Status = rows.All(row => row.Status == "available") ? "available" :
                    rows.All(row => row.Status == "unavailable") ? "unavailable" : "partial",
                Timeline = rows,
                FirstCandidateDifferentTick = firstCandidates,
                FirstMethodResultDifferentTick = firstMethod,
                FirstCandidateConvergenceTick = Rejoin(rows, firstCandidates,
                    row => row.SameOuterCandidates),
                FirstMethodResultConvergenceTick = Rejoin(rows, firstMethod,
                    row => row.SameMethodResult)
            };
        }).ToArray();

        return new RescuerEvidenceReceiptTimingReport
        {
            Status = methods.All(item => item.Status == "available") ? "available" :
                methods.All(item => item.Status == "unavailable") ? "unavailable" : "partial",
            QueryTicks = a.Select(row => row.QueryTick).ToArray(),
            Methods = methods
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
