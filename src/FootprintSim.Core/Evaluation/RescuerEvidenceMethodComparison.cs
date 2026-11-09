using FootprintSim.Evidence;
using FootprintSim.Hazards;
using FootprintSim.Inference;
using FootprintSim.Inference.PastStates;
using FootprintSim.Inference.SearchProjection;
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

// #92: Sequential revisions of one published historical question.
// Null standings mean not exposed by a method. Group IDs are unavailable.
public sealed record RescuerEvidenceDecisionRevisionTick
{
    public required long BeforeQueryTick { get; init; }
    public required long AfterQueryTick { get; init; }
    public required string Status { get; init; }
    public string? UnavailableReason { get; init; }
    public IReadOnlyList<string> BeforeReceivedOriginalIds { get; init; } = [];
    public IReadOnlyList<string> AfterReceivedOriginalIds { get; init; } = [];
    public IReadOnlyList<string> BeforeReceivedSourceObservationIds { get; init; } = [];
    public IReadOnlyList<string> AfterReceivedSourceObservationIds { get; init; } = [];
    public required int BeforeAvailableSourceGroupCount { get; init; }
    public required int AfterAvailableSourceGroupCount { get; init; }
    public required int BeforeAvailableConstraintCount { get; init; }
    public required int AfterAvailableConstraintCount { get; init; }
    public RescuerEvidenceMethodRow? BeforeMethod { get; init; }
    public RescuerEvidenceMethodRow? AfterMethod { get; init; }
    public IReadOnlyList<string>? ExcludedCandidateRegionIds { get; init; }
    public IReadOnlyList<string>? ReappearedCandidateRegionIds { get; init; }
    public int? AfterMinusBeforeCandidateCount { get; init; }
    public bool? BeforeCandidatesSubsetOfAfter { get; init; }
    public bool? AfterCandidatesSubsetOfBefore { get; init; }
    public bool? SameOuterCandidates { get; init; }
    public bool? SamePreferredCandidates { get; init; }
    public bool? SameUncertainCandidates { get; init; }
    public bool? SameConflictedCandidates { get; init; }
    public bool? EvidenceStatusChanged { get; init; }
}

public sealed record RescuerEvidenceDecisionRevisionMethod
{
    public required string Method { get; init; }
    public required string Status { get; init; }
    public long? FirstReappearedTick { get; init; }
    public long? FirstExcludedTick { get; init; }
    public long? FirstOuterChangedTick { get; init; }
    public IReadOnlyList<RescuerEvidenceDecisionRevisionTick> Timeline { get; init; } = [];
}

public sealed record RescuerEvidenceDecisionRevisionReport
{
    public required string Status { get; init; }
    public string? UnavailableReason { get; init; }
    public required InteractionTimeWindow TargetWindow { get; init; }
    public IReadOnlyList<long> QueryTicks { get; init; } = [];
    public IReadOnlyList<RescuerEvidenceDecisionRevisionMethod> Methods { get; init; } = [];
}


/* #94: Compare public evidence reports with declared transition knowledge vs
 * the same evidence without provided transitions. Null is unknown, not a
 * claim that travel is physically impossible. Neither report carries truth. */
public sealed record RescuerEvidenceRelationAvailabilityTick
{
    public required long QueryTick { get; init; }
    public required string Status { get; init; }
    public string? UnavailableReason { get; init; }
    public IReadOnlyList<string> ReceivedOriginalIds { get; init; } = [];
    public IReadOnlyList<string> ReceivedSourceObservationIds { get; init; } = [];
    public required int ReceivedOriginalCount { get; init; }
    public required int AvailableSourceGroupCount { get; init; }
    public required int AvailableConstraintCount { get; init; }
    public RescuerEvidenceMethodRow? KnownRelationMethod { get; init; }
    public RescuerEvidenceMethodRow? UnprovidedRelationMethod { get; init; }
    public int? KnownMinusUnprovidedCandidateCount { get; init; }
    public IReadOnlyList<string>? KnownOnlyCandidateRegionIds { get; init; }
    public IReadOnlyList<string>? UnprovidedOnlyCandidateRegionIds { get; init; }
    public bool? KnownCandidatesSubsetOfUnprovided { get; init; }
    public bool? UnprovidedCandidatesSubsetOfKnown { get; init; }
    public bool? SameOuterCandidates { get; init; }
    public bool? SamePreferredCandidates { get; init; }
    public bool? SameUncertainCandidates { get; init; }
    public bool? SameConflictedCandidates { get; init; }
    public bool? SameEvidenceStatus { get; init; }
    public bool? SameSourceUsage { get; init; }
    public bool? SameWorkCounters { get; init; }
}

public sealed record RescuerEvidenceRelationAvailabilityMethod
{
    public required string Method { get; init; }
    public required string Status { get; init; }
    public IReadOnlyList<RescuerEvidenceRelationAvailabilityTick> Timeline { get; init; } = [];
}

public sealed record RescuerEvidenceRelationAvailabilityReport
{
    public required string Status { get; init; }
    public string? UnavailableReason { get; init; }
    public required InteractionTimeWindow TargetWindow { get; init; }
    public IReadOnlyList<long> QueryTicks { get; init; } = [];
    public IReadOnlyList<RescuerEvidenceRelationAvailabilityMethod> Methods { get; init; } = [];
}


/* #100: an additional public historical-tick classification of actual outer
 * regions, not a present-position or action-footprint projection. */
public sealed record RescuerEvidenceHistoricalUnitMethod
{
    public required string Method { get; init; }
    public required string Status { get; init; }
    public string? UnavailableReason { get; init; }
    public int? CandidateRegionCount { get; init; }
    public IReadOnlyList<string>? CandidateRegionIds { get; init; }
    public IReadOnlyList<string> KnownUnitIds { get; init; } = [];
    public int? KnownUnitCount { get; init; }
    public IReadOnlyList<string> UnresolvedRegionIds { get; init; } = [];
    public IReadOnlyList<string> MappingSourceIds { get; init; } = [];
    public required bool ComparisonEligible { get; init; }
}

public sealed record RescuerEvidenceHistoricalUnitTick
{
    public required long QueryTick { get; init; }
    public required int ReceivedOriginalCount { get; init; }
    public required int AvailableSourceGroupCount { get; init; }
    public IReadOnlyList<RescuerEvidenceHistoricalUnitMethod> Methods { get; init; } = [];
}

public sealed record RescuerEvidenceHistoricalUnitReport
{
    public required string Status { get; init; }
    public required long HistoricalTick { get; init; }
    public IReadOnlyList<RescuerEvidenceHistoricalUnitTick> ActualTimeline { get; init; } = [];
}

// #102: an evaluation-only join of the same-cutoff public-source ablations
// and the historical public-unit catalog. No new inference or truth input.
public sealed record RescuerEvidencePairedHistoricalUnitCondition
{
    public required string Label { get; init; }
    public IReadOnlyList<string> ReceivedOriginalIds { get; init; } = [];
    public required string Status { get; init; }
    public RescuerEvidenceMethodRow? PublicMethod { get; init; }
    public required RescuerEvidenceHistoricalUnitMethod UnitProjection { get; init; }
}

public sealed record RescuerEvidencePairedHistoricalUnitMarginal
{
    public required string FromCondition { get; init; }
    public required string ToCondition { get; init; }
    public required string AddedOriginalId { get; init; }
    public required string Status { get; init; }
    public required string UnitComparisonStatus { get; init; }
    public IReadOnlyList<string>? ExcludedOuterRegionIds { get; init; }
    public IReadOnlyList<string>? ReappearedOuterRegionIds { get; init; }
    // Null means that incomplete, unavailable or genuinely empty projection
    // cannot establish a safe known-unit difference.
    public IReadOnlyList<string>? RemovedKnownUnitIds { get; init; }
    public IReadOnlyList<string>? AddedKnownUnitIds { get; init; }
    public bool? SamePreferredCandidates { get; init; }
    public bool? SameUncertainCandidates { get; init; }
    public bool? SameConflictedCandidates { get; init; }
    public int? UsedSourceGroupDelta { get; init; }
    public int? GenerationStepsDelta { get; init; }
    public int? VerificationStepsDelta { get; init; }
    public int? ForwardTransitionStepsDelta { get; init; }
    public int? BackwardTransitionStepsDelta { get; init; }
    public int? EvidenceRecordsInspectedDelta { get; init; }
    public int? PublicRelationsInspectedDelta { get; init; }
}

public sealed record RescuerEvidencePairedHistoricalUnitMethod
{
    public required string Method { get; init; }
    public required string Status { get; init; }
    public IReadOnlyList<RescuerEvidencePairedHistoricalUnitCondition> Conditions { get; init; } = [];
    public IReadOnlyList<RescuerEvidencePairedHistoricalUnitMarginal> Marginals { get; init; } = [];
}

public sealed record RescuerEvidencePairedHistoricalUnitReport
{
    public required string Status { get; init; }
    public string? UnavailableReason { get; init; }
    public required long HistoricalTick { get; init; }
    public long? QueryTick { get; init; }
    public IReadOnlyList<string> OriginalIds { get; init; } = [];
    public IReadOnlyList<RescuerEvidencePairedHistoricalUnitMethod> Methods { get; init; } = [];
}


// #104: A positive match between *received public Person constraints* and a
// declared public branch. Neither a Person-traversable route nor a ranking.
public sealed record RescuerDeclaredPathWitness(
    string CandidateRegionId,
    string EarlyConstraintId,
    string EarlyDirectSourceId,
    string EarlySourceObservationId,
    IReadOnlyList<string> EarlySourceAncestryIds,
    string LaterConstraintId,
    string LaterDirectSourceId,
    string LaterSourceObservationId,
    IReadOnlyList<string> LaterSourceAncestryIds,
    string DestinationRegionId,
    string BranchProvenanceId,
    long BranchPublishedAtTick,
    int MinimumTravelTicks,
    int MaximumTravelTicks,
    long CompatibleEarlyTick,
    long CompatibleLaterTick,
    bool IndependentSourceAncestry,
    string PersonTraversability);

public sealed record RescuerDeclaredPathCandidate(
    string RegionId,
    string Status,
    IReadOnlyList<RescuerDeclaredPathWitness> Witnesses);

public sealed record RescuerDeclaredPathCompatibilityReport(
    long QueryTick,
    InteractionTimeWindow TargetWindow,
    IReadOnlyList<string> ReceivedOriginalIds,
    string Status,
    IReadOnlyList<RescuerDeclaredPathCandidate> Candidates,
    int PublicConstraintsInspected,
    int PublicBranchesInspected,
    int ConstraintBranchPairsInspected);

// #106: Questions about missing *public declarations*, not discovered
// physical relations or observations. A time difference is only the formal
// interval that could be compatible if the relation were later declared.
public sealed record RescuerMissingDeclaredPathQuestion(
    string QuestionId,
    string SourceRegionId,
    string DestinationRegionId,
    long MinimumCompatibleDifferenceTicks,
    long MaximumCompatibleDifferenceTicks,
    string EarlyConstraintId,
    string EarlyDirectSourceId,
    string EarlySourceObservationId,
    IReadOnlyList<string> EarlySourceAncestryIds,
    string LaterConstraintId,
    string LaterDirectSourceId,
    string LaterSourceObservationId,
    IReadOnlyList<string> LaterSourceAncestryIds,
    bool IndependentSourceAncestry,
    string PersonTraversability);

public sealed record RescuerMissingDeclaredPathCandidate(
    string RegionId,
    string DeclaredPathStatus,
    IReadOnlyList<RescuerMissingDeclaredPathQuestion> Questions,
    // Formal public explanations that make a missing direct declaration
    // non-essential. These are not verified Person movements.
    IReadOnlyList<string> PublicAlternativeExplanations);

public sealed record RescuerMissingDeclaredPathReport(
    long QueryTick,
    InteractionTimeWindow TargetWindow,
    IReadOnlyList<string> ReceivedOriginalIds,
    string Status,
    IReadOnlyList<RescuerMissingDeclaredPathCandidate> Candidates,
    int PublicConstraintsInspected,
    int PublicBranchesInspected,
    int ConstraintPairsInspected,
    int PublicTraversalBranchesInspected);

public static class RescuerEvidenceMethodComparison
{
    /// <summary>
    /// Evaluation-only declared-path compatibility, not proof of actual Person
    /// travel. Uses only the receipt-projected public input; never reads
    /// originals, truth, private observations or hidden branch Activation.
    /// Missing public branches mean UNKNOWN, never impossible/hard exclusion.
    /// </summary>
    public static RescuerDeclaredPathCompatibilityReport AnalyzeDeclaredPathCompatibility(
        RescuerEvidenceInput received,
        InteractionTimeWindow targetWindow)
    {
        ArgumentNullException.ThrowIfNull(received);
        ArgumentNullException.ThrowIfNull(targetWindow);
        var input = received.Input;
        var cutoff = input.CurrentTick;
        if (cutoff < targetWindow.EndTick)
        {
            throw new ArgumentOutOfRangeException(nameof(targetWindow));
        }
        var states = input.PublicContext.PublicCausalTracingModel?.CandidateStates
            ?? throw new ArgumentException("A public candidate-state model is required.");
        if (states.SelectMany(state => state.Provenance)
            .Any(source => source.AvailableAtTick > cutoff))
        {
            throw new ArgumentException("A public candidate state is not yet available.");
        }
        var regions = states
            .Where(state => state.TimeWindow.StartTick <= targetWindow.EndTick &&
                targetWindow.StartTick <= state.TimeWindow.EndTick)
            .SelectMany(state => state.RegionIds)
            .Distinct(StringComparer.Ordinal).OrderBy(id => id, StringComparer.Ordinal).ToArray();
        if (regions.Length == 0)
        {
            throw new ArgumentException("There is no public candidate state at the target window.");
        }

        var receivedIds = received.ReceivedRecords
            .Select(item => item.OriginalRecordId)
            .Distinct(StringComparer.Ordinal).OrderBy(id => id, StringComparer.Ordinal).ToArray();
        var availableOriginalIds = receivedIds.ToHashSet(StringComparer.Ordinal);
        var constraints = (input.AvailableCausalTracingConstraints?
            .PubliclyAvailableAt(cutoff).Constraints ?? [])
            .Where(c => c.ScenarioId == input.ScenarioId && c.Seed == input.Seed &&
                c.TargetEntityId == input.PublicContext.TargetPersonId &&
                c.Visibility == InteractionVisibility.PublicEvidence &&
                c.AvailableAtTick <= cutoff &&
                availableOriginalIds.Contains(c.DirectSourceId) &&
                c.IsPersonEvidence && !c.IsDeviceOnlyEvidence)
            .OrderBy(c => c.ConstraintId, StringComparer.Ordinal).ToArray();

        var envelope = input.PublicContext.PublicHazardEnvelope;
        var envelopeAvailable = envelope is not null && envelope.PublishedAtTick <= cutoff;
        // A not-yet-published branch is invisible to this diagnostic; callers
        // must still supply a cutoff-filtered projection to the existing Compare.
        var branches = envelopeAvailable
            ? envelope!.Branches.Where(b => b.PublishedAtTick <= cutoff &&
                    b.MinimumTravelTicks >= 0 &&
                    b.MaximumTravelTicks >= b.MinimumTravelTicks)
                .OrderBy(b => b.SourceRegionId, StringComparer.Ordinal)
                .ThenBy(b => b.DestinationRegionId, StringComparer.Ordinal)
                .ThenBy(b => b.ProvenanceId, StringComparer.Ordinal).ToArray()
            : [];

        static bool Overlaps(InteractionTimeWindow a, InteractionTimeWindow b) =>
            a.StartTick <= b.EndTick && b.StartTick <= a.EndTick;

        // Integer-interval feasibility: do not silently turn an observed window
        // into a point. Select the earliest admissible pair deterministically.
        static bool CompatibleTimes(
            InteractionTimeWindow early, InteractionTimeWindow target,
            InteractionTimeWindow candidateState, InteractionTimeWindow later,
            int minimum, int maximum, out long earlyTick, out long laterTick)
        {
            var lo = Math.Max(Math.Max(early.StartTick, target.StartTick),
                candidateState.StartTick);
            var hi = Math.Min(Math.Min(early.EndTick, target.EndTick),
                candidateState.EndTick);
            var travelMin = Math.Max(1, minimum);
            earlyTick = Math.Max(lo, later.StartTick - maximum);
            laterTick = Math.Max(later.StartTick, earlyTick + travelMin);
            return earlyTick <= hi && laterTick <= later.EndTick &&
                laterTick - earlyTick <= maximum;
        }

        var pairInspections = 0;
        var usablePersonSourceCount = constraints.Select(c => c.DirectSourceId)
            .Distinct(StringComparer.Ordinal).Count();
        var candidates = new List<RescuerDeclaredPathCandidate>();
        foreach (var region in regions)
        {
            // A region may occur only at some ticks in the target window.
            // Keep its public candidate-state windows when proving a witness.
            var candidateWindows = states.Where(state =>
                    Overlaps(state.TimeWindow, targetWindow) &&
                    state.RegionIds.Contains(region, StringComparer.Ordinal))
                .Select(state => state.TimeWindow)
                .OrderBy(window => window.StartTick)
                .ThenBy(window => window.EndTick).ToArray();
            var witnesses = new List<RescuerDeclaredPathWitness>();
            var earlyCandidates = constraints.Where(c =>
                c.AllowedRegionIds.Contains(region, StringComparer.Ordinal) &&
                Overlaps(c.ConstrainedTimeWindow, targetWindow)).ToArray();
            foreach (var early in earlyCandidates.Where(c =>
                c.EvidenceBoundaryState == EvidenceState.Confirmed))
                foreach (var later in constraints.Where(c =>
                    c.EvidenceBoundaryState == EvidenceState.Confirmed &&
                    c.DirectSourceId != early.DirectSourceId &&
                    c.ConstrainedTimeWindow.EndTick > targetWindow.EndTick))
                    foreach (var branch in branches.Where(b =>
                        b.SourceRegionId == region &&
                        later.AllowedRegionIds.Contains(b.DestinationRegionId, StringComparer.Ordinal)))
                    {
                        pairInspections++;
                        long t0 = 0, t1 = 0;
                        var compatible = false;
                        foreach (var candidateWindow in candidateWindows)
                        {
                            if (!CompatibleTimes(early.ConstrainedTimeWindow, targetWindow,
                                candidateWindow, later.ConstrainedTimeWindow,
                                branch.MinimumTravelTicks, branch.MaximumTravelTicks,
                                out t0, out t1))
                            {
                                continue;
                            }
                            compatible = true;
                            break;
                        }
                        if (!compatible)
                        {
                            continue;
                        }
                        var ancestryIndependent = early.SourceAncestryIds.Count > 0 &&
                            later.SourceAncestryIds.Count > 0 &&
                            !early.SourceAncestryIds.Intersect(
                                later.SourceAncestryIds, StringComparer.Ordinal).Any();
                        witnesses.Add(new RescuerDeclaredPathWitness(
                            region,
                            early.ConstraintId, early.DirectSourceId, early.SourceObservationId,
                            early.SourceAncestryIds.OrderBy(id => id, StringComparer.Ordinal).ToArray(),
                            later.ConstraintId, later.DirectSourceId, later.SourceObservationId,
                            later.SourceAncestryIds.OrderBy(id => id, StringComparer.Ordinal).ToArray(),
                            branch.DestinationRegionId, branch.ProvenanceId,
                            branch.PublishedAtTick, branch.MinimumTravelTicks,
                            branch.MaximumTravelTicks, t0, t1,
                            ancestryIndependent, "unknown"));
                    }

            // A conflict in a relevant publicly received Person observation is
            // preserved, but never promoted to a confirmed positive witness.
            var conflicting = constraints.Any(c =>
                c.EvidenceBoundaryState == EvidenceState.Conflicting &&
                (earlyCandidates.Contains(c) ||
                    (c.ConstrainedTimeWindow.EndTick > targetWindow.EndTick &&
                     branches.Any(b => b.SourceRegionId == region &&
                         c.AllowedRegionIds.Contains(b.DestinationRegionId,
                             StringComparer.Ordinal)))));
            var ordered = witnesses
                .OrderBy(w => w.EarlyConstraintId, StringComparer.Ordinal)
                .ThenBy(w => w.LaterConstraintId, StringComparer.Ordinal)
                .ThenBy(w => w.BranchProvenanceId, StringComparer.Ordinal)
                .ToArray();
            var status = ordered.Length > 0 ? "witnessed"
                : conflicting ? "conflict"
                : !envelopeAvailable || usablePersonSourceCount < 2
                    ? "unavailable" : "unknown";
            candidates.Add(new RescuerDeclaredPathCandidate(region, status, ordered));
        }
        return new RescuerDeclaredPathCompatibilityReport(
            cutoff, targetWindow, receivedIds,
            envelopeAvailable ? "available" : "unavailable",
            candidates, constraints.Length, branches.Length, pairInspections);
    }

    /// <summary>
    /// Evaluation-only, receipt-gated questions for an *unconfirmed* public
    /// relation. This does not assert that a relation exists or feed it into
    /// inference. The #104 diagnostic supplies candidate eligibility/status;
    /// all source joins and candidate-time intersections use its public rules.
    /// </summary>
    public static RescuerMissingDeclaredPathReport AnalyzeMissingDeclaredPathQuestions(
        RescuerEvidenceInput received,
        InteractionTimeWindow targetWindow)
    {
        // Keep #104's unavailable/conflict/witness distinction and validations
        // unchanged. In particular, lack of a published branch is not absence.
        var declared = AnalyzeDeclaredPathCompatibility(received, targetWindow);
        var input = received.Input;
        var cutoff = input.CurrentTick;
        var originals = received.ReceivedRecords
            .Select(record => record.OriginalRecordId)
            .ToHashSet(StringComparer.Ordinal);
        var constraints = (input.AvailableCausalTracingConstraints?
            .PubliclyAvailableAt(cutoff).Constraints ?? [])
            .Where(c => c.ScenarioId == input.ScenarioId && c.Seed == input.Seed &&
                c.TargetEntityId == input.PublicContext.TargetPersonId &&
                c.Visibility == InteractionVisibility.PublicEvidence &&
                c.AvailableAtTick <= cutoff && originals.Contains(c.DirectSourceId) &&
                c.IsPersonEvidence && !c.IsDeviceOnlyEvidence)
            .OrderBy(c => c.ConstraintId, StringComparer.Ordinal).ToArray();
        var states = input.PublicContext.PublicCausalTracingModel!.CandidateStates;
        var envelope = input.PublicContext.PublicHazardEnvelope;
        var publishedBranches = declared.Status == "available"
            ? envelope!.Branches.Where(b => b.PublishedAtTick <= cutoff &&
                b.MinimumTravelTicks >= 0 &&
                b.MaximumTravelTicks >= b.MinimumTravelTicks).ToArray()
            : [];
        // A declared endpoint pair cannot become an "unknown relation" merely
        // because its published travel bound has no formal witness.
        var publishedPairs = publishedBranches
            .Select(b => (b.SourceRegionId, b.DestinationRegionId)).ToHashSet();

        var inspected = 0;
        var traversedPublicBranches = 0;
        var result = new List<RescuerMissingDeclaredPathCandidate>();
        foreach (var candidate in declared.Candidates)
        {
            var questions = new List<RescuerMissingDeclaredPathQuestion>();
            var explanations = new SortedSet<string>(StringComparer.Ordinal);
            if (candidate.Status == "unknown" && declared.Status == "available")
            {
                var region = candidate.RegionId;
                var windows = states.Where(s =>
                        s.RegionIds.Contains(region, StringComparer.Ordinal) &&
                        s.TimeWindow.StartTick <= targetWindow.EndTick &&
                        targetWindow.StartTick <= s.TimeWindow.EndTick)
                    .Select(s => s.TimeWindow)
                    .OrderBy(w => w.StartTick).ThenBy(w => w.EndTick).ToArray();
                var earlyConstraints = constraints.Where(c =>
                    c.EvidenceBoundaryState == EvidenceState.Confirmed &&
                    c.AllowedRegionIds.Contains(region, StringComparer.Ordinal) &&
                    c.ConstrainedTimeWindow.StartTick <= targetWindow.EndTick &&
                    targetWindow.StartTick <= c.ConstrainedTimeWindow.EndTick);
                foreach (var early in earlyConstraints)
                    foreach (var later in constraints.Where(c =>
                        c.EvidenceBoundaryState == EvidenceState.Confirmed &&
                        c.DirectSourceId != early.DirectSourceId &&
                        c.ConstrainedTimeWindow.EndTick > targetWindow.EndTick))
                    {
                        inspected++;
                        var independent = early.SourceAncestryIds.Count > 0 &&
                            later.SourceAncestryIds.Count > 0 &&
                            !early.SourceAncestryIds.Intersect(
                                later.SourceAncestryIds, StringComparer.Ordinal).Any();
                        foreach (var destination in later.AllowedRegionIds
                            .Distinct(StringComparer.Ordinal)
                            .OrderBy(id => id, StringComparer.Ordinal))
                        {
                            // A published direct pair can have incompatible
                            // timing. Preserve that fact rather than pretending
                            // that no published declaration exists (AR-12).
                            var directPublished = publishedPairs.Contains((region, destination));
                            // Build questions per endpoint pair. One compatible
                            // public alternative makes a new direct edge *unneeded*
                            // for a formal explanation, not impossible or false.
                            var pending = new List<RescuerMissingDeclaredPathQuestion>();
                            var pairExplained = false;
                            // Preserve disjoint candidate-state windows: never
                            // bridge a gap into a fabricated continuous interval.
                            var seenRanges = new HashSet<(long Minimum, long Maximum)>();
                            foreach (var window in windows)
                            {
                                var earliest = Math.Max(
                                    Math.Max(early.ConstrainedTimeWindow.StartTick,
                                        targetWindow.StartTick), window.StartTick);
                                var latest = Math.Min(
                                    Math.Min(early.ConstrainedTimeWindow.EndTick,
                                        targetWindow.EndTick), window.EndTick);
                                if (earliest > latest ||
                                    later.ConstrainedTimeWindow.EndTick <= earliest)
                                {
                                    continue;
                                }
                                // This is the integer difference between two
                                // possible observation ticks, NOT a claimed
                                // Person travel time or a new physical bound.
                                var minimum = Math.Max(1L,
                                    later.ConstrainedTimeWindow.StartTick - latest);
                                var maximum = later.ConstrainedTimeWindow.EndTick - earliest;
                                if (minimum > maximum ||
                                    !seenRanges.Add((minimum, maximum)))
                                {
                                    continue;
                                }
                                if (directPublished)
                                {
                                    // A published pair remains known even when
                                    // #104 has no direct witness. Distinguish
                                    // early arrival (dwell untested) from a
                                    // route that cannot arrive by the later tick.
                                    explanations.Add("direct-pair-published-timing-unverified:" +
                                        region + ">" + destination);
                                    foreach (var edge in publishedBranches.Where(b =>
                                        b.SourceRegionId == region &&
                                        b.DestinationRegionId == destination)
                                        .OrderBy(b => b.ProvenanceId, StringComparer.Ordinal))
                                    {
                                        traversedPublicBranches++;
                                        var tag = ":source=" + edge.ProvenanceId +
                                            ":travel=" + edge.MinimumTravelTicks + "-" +
                                            edge.MaximumTravelTicks + ":gap=" +
                                            minimum + "-" + maximum;
                                        if (edge.MaximumTravelTicks < minimum)
                                            explanations.Add(
                                                "direct-pair-published-shorter-dwell-unverified:" +
                                                region + ">" + destination + tag);
                                        else if (edge.MinimumTravelTicks > maximum)
                                            explanations.Add(
                                                "direct-pair-published-too-long:" +
                                                region + ">" + destination + tag);
                                    }
                                    pairExplained = true;
                                    continue;
                                }
                                // This is a formal same-region explanation,
                                // never evidence of a realized Person stay.
                                if (region == destination)
                                {
                                    explanations.Add("formal-stay:" + region);
                                    pairExplained = true;
                                    continue;
                                }
                                // Search only finite simple routes of already-
                                // published branches. A positive interval overlap
                                // needs no missing direct declaration; too-short
                                // paths still need an untested dwell assumption.
                                var paths = FindPublishedRoutes(region, destination,
                                    minimum, maximum, publishedBranches);
                                traversedPublicBranches += paths.BranchesInspected;
                                if (paths.Positive is not null)
                                {
                                    explanations.Add(paths.Positive);
                                    pairExplained = true;
                                }
                                if (paths.Shorter is not null)
                                    explanations.Add(paths.Shorter);
                                if (paths.TooLong is not null)
                                    explanations.Add(paths.TooLong);
                                if (paths.Incomplete)
                                    explanations.Add("public-path-search-incomplete:" +
                                        region + ">" + destination +
                                        ":depth=8:edge-budget=4096");
                                var questionId = "unconfirmed:" + region + ":" +
                                    destination + ":" + early.ConstraintId + ":" +
                                    later.ConstraintId + ":" + minimum + "-" + maximum;
                                pending.Add(new RescuerMissingDeclaredPathQuestion(
                                    questionId, region, destination, minimum, maximum,
                                    early.ConstraintId, early.DirectSourceId,
                                    early.SourceObservationId,
                                    early.SourceAncestryIds
                                        .OrderBy(id => id, StringComparer.Ordinal).ToArray(),
                                    later.ConstraintId, later.DirectSourceId,
                                    later.SourceObservationId,
                                    later.SourceAncestryIds
                                        .OrderBy(id => id, StringComparer.Ordinal).ToArray(),
                                    independent, "unknown"));
                            }
                            // If some compatible time window already has a public
                            // explanation, do not call the direct pair necessary.
                            // Keep the #104 direct-only status unchanged.
                            if (!pairExplained)
                            {
                                questions.AddRange(pending);
                            }
                        }
                    }
            }
            result.Add(new RescuerMissingDeclaredPathCandidate(
                candidate.RegionId, candidate.Status,
                questions.OrderBy(q => q.DestinationRegionId, StringComparer.Ordinal)
                    .ThenBy(q => q.EarlyConstraintId, StringComparer.Ordinal)
                    .ThenBy(q => q.LaterConstraintId, StringComparer.Ordinal)
                    .ThenBy(q => q.MinimumCompatibleDifferenceTicks)
                    .ThenBy(q => q.MaximumCompatibleDifferenceTicks).ToArray(),
                explanations.ToArray()));
        }
        return new RescuerMissingDeclaredPathReport(
            cutoff, targetWindow, declared.ReceivedOriginalIds, declared.Status,
            result, constraints.Length, publishedBranches.Length, inspected,
            traversedPublicBranches);
    }

    // The positive route is existential and uses only public declarations.
    // Search absence is never proof of physical impossibility or of no relation.
    // The cap is per candidate/source-pair/window and prevents unbounded paths.
    private sealed record PublishedRouteSearch(
        string? Positive, string? Shorter, string? TooLong,
        bool Incomplete, int BranchesInspected);

    private static PublishedRouteSearch FindPublishedRoutes(
        string source, string destination, long gapMin, long gapMax,
        IReadOnlyList<PublicHazardBranch> branches)
    {
        const int maxDepth = 8;
        const int edgeBudget = 4096;
        var outgoing = branches
            .GroupBy(edge => edge.SourceRegionId, StringComparer.Ordinal)
            .ToDictionary(group => group.Key,
                group => group.OrderBy(edge => edge.DestinationRegionId,
                        StringComparer.Ordinal)
                    .ThenBy(edge => edge.ProvenanceId, StringComparer.Ordinal)
                    .ThenBy(edge => edge.MinimumTravelTicks)
                    .ThenBy(edge => edge.MaximumTravelTicks).ToArray(),
                StringComparer.Ordinal);
        var visited = new HashSet<string>(StringComparer.Ordinal) { source };
        var route = new List<PublicHazardBranch>();
        string? positive = null, shorter = null, tooLong = null;
        var incomplete = false;
        var inspections = 0;

        void Visit(string at, long totalMin, long totalMax)
        {
            if (!outgoing.TryGetValue(at, out var edges))
                return;
            if (route.Count >= maxDepth)
            {
                if (edges.Any(edge => !visited.Contains(edge.DestinationRegionId)))
                    incomplete = true;
                return;
            }
            foreach (var edge in edges)
            {
                if (inspections >= edgeBudget)
                {
                    incomplete = true;
                    return;
                }
                inspections++;
                if (visited.Contains(edge.DestinationRegionId))
                    continue;
                // Every edge bound is nonnegative; checked arithmetic ensures
                // overflow cannot accidentally turn a long path into a witness.
                if (totalMax > long.MaxValue - edge.MaximumTravelTicks ||
                    totalMin > long.MaxValue - edge.MinimumTravelTicks)
                {
                    incomplete = true;
                    continue;
                }
                var nextMin = totalMin + edge.MinimumTravelTicks;
                var nextMax = totalMax + edge.MaximumTravelTicks;
                route.Add(edge);
                if (edge.DestinationRegionId == destination)
                {
                    if (route.Count >= 2)
                    {
                        var labels = source + ">" + string.Join(">",
                            route.Select(part => part.DestinationRegionId));
                        var details = ":sources=" + string.Join(",",
                            route.Select(part => part.ProvenanceId)) +
                            ":travel=" + nextMin + "-" + nextMax +
                            ":gap=" + gapMin + "-" + gapMax;
                        if (nextMin <= gapMax && nextMax >= gapMin)
                            positive ??= (route.Count == 2
                                ? "formal-two-hop:" + labels
                                : "formal-multihop:" + labels + details);
                        else if (nextMax < gapMin)
                            shorter ??= "published-path-shorter-dwell-unverified:" +
                                labels + details;
                        else if (nextMin > gapMax)
                            tooLong ??= "published-path-too-long:" + labels + details;
                    }
                }
                else
                {
                    visited.Add(edge.DestinationRegionId);
                    Visit(edge.DestinationRegionId, nextMin, nextMax);
                    visited.Remove(edge.DestinationRegionId);
                }
                route.RemoveAt(route.Count - 1);
                if (incomplete && inspections >= edgeBudget)
                    return;
            }
        }

        Visit(source, 0, 0);
        return new PublishedRouteSearch(positive, shorter, tooLong,
            incomplete, inspections);
    }

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
    /// Compare adjacent actual published queries of one fixed past target.
    /// Do not read truth, private originals or hypothetical ablations.
    /// First changes are among the sampled ticks, not continuous time.
    /// Metadata is not proof of the declared world's physical accuracy.
    /// </summary>
    public static RescuerEvidenceDecisionRevisionReport AnalyzeDecisionRevision(
        RescuerEvidenceMethodReport publicReport)
    {
        ArgumentNullException.ThrowIfNull(publicReport);
        static string[] Sort(IEnumerable<string> ids) =>
            ids.Distinct(StringComparer.Ordinal).OrderBy(id => id, StringComparer.Ordinal).ToArray();
        static bool? SameStanding(IReadOnlyList<string>? a, IReadOnlyList<string>? b) =>
            a is null || b is null ? null :
                Sort(a).SequenceEqual(Sort(b), StringComparer.Ordinal);
        RescuerEvidenceDecisionRevisionReport Unavailable(string reason) => new()
        {
            Status = "unavailable",
            UnavailableReason = reason,
            TargetWindow = publicReport.TargetWindow
        };
        var actual = publicReport.ActualTimeline.OrderBy(row => row.QueryTick).ToArray();
        if (actual.Length < 2 ||
            actual.Select(row => row.QueryTick).Distinct().Count() != actual.Length ||
            actual.Any(row => row.Condition != "actual" ||
                row.QueryTick < publicReport.TargetWindow.EndTick ||
                row.ReceivedOriginalCount != row.ReceivedOriginalIds.Count ||
                row.ReceivedOriginalIds.Distinct(StringComparer.Ordinal).Count() != row.ReceivedOriginalIds.Count ||
                row.ReceivedSourceObservationIds.Distinct(StringComparer.Ordinal).Count() !=
                    row.ReceivedSourceObservationIds.Count ||
                row.ReceivedSourceObservationIds.Count != row.ReceivedOriginalIds.Count ||
                row.AvailableSourceGroupCount < 0 ||
                row.AvailableSourceGroupCount > row.ReceivedOriginalCount ||
                row.AvailableConstraintCount < 0))
            return Unavailable("At least two consistent, distinct actual queries are required.");

        var names = new[] { "public-prior", "known-relation-backward",
            "distributed-reconstruction", "distributed-reconstruction-local" }
            .Concat(actual.SelectMany(row => row.Methods.Select(method => method.Method)))
            .Distinct(StringComparer.Ordinal).OrderBy(name => name, StringComparer.Ordinal).ToArray();
        var methods = names.Select(name =>
        {
            var steps = actual.Zip(actual.Skip(1), (prev, next) =>
            {
                var beforeRows = prev.Methods.Where(method => method.Method == name).ToArray();
                var afterRows = next.Methods.Where(method => method.Method == name).ToArray();
                static bool IsValid(RescuerEvidenceMethodRow row) =>
                    row.OuterCandidateRegionIds is not null &&
                    row.CandidateCount == row.OuterCandidateRegionIds.Count &&
                    row.OuterCandidateRegionIds.Distinct(StringComparer.Ordinal).Count() ==
                        row.OuterCandidateRegionIds.Count &&
                    row.AvailableSourceGroupCount >= 0 &&
                    row.UsedSourceGroupCount >= 0 &&
                    row.UsedSourceGroupCount <= row.AvailableSourceGroupCount;
                var valid = beforeRows.Length == 1 && afterRows.Length == 1 &&
                    IsValid(beforeRows[0]) && IsValid(afterRows[0]) &&
                    beforeRows[0].AvailableSourceGroupCount == prev.AvailableSourceGroupCount &&
                    afterRows[0].AvailableSourceGroupCount == next.AvailableSourceGroupCount;
                var before = valid ? beforeRows[0] : null;
                var after = valid ? afterRows[0] : null;
                var excluded = valid ? Sort(before!.OuterCandidateRegionIds.Except(
                    after!.OuterCandidateRegionIds, StringComparer.Ordinal)) : null;
                var reappeared = valid ? Sort(after!.OuterCandidateRegionIds.Except(
                    before!.OuterCandidateRegionIds, StringComparer.Ordinal)) : null;
                return new RescuerEvidenceDecisionRevisionTick
                {
                    BeforeQueryTick = prev.QueryTick,
                    AfterQueryTick = next.QueryTick,
                    Status = valid ? "available" : "unavailable",
                    UnavailableReason = valid ? null : "Missing, duplicated or inconsistent method result.",
                    BeforeReceivedOriginalIds = Sort(prev.ReceivedOriginalIds),
                    AfterReceivedOriginalIds = Sort(next.ReceivedOriginalIds),
                    BeforeReceivedSourceObservationIds = Sort(prev.ReceivedSourceObservationIds),
                    AfterReceivedSourceObservationIds = Sort(next.ReceivedSourceObservationIds),
                    BeforeAvailableSourceGroupCount = prev.AvailableSourceGroupCount,
                    AfterAvailableSourceGroupCount = next.AvailableSourceGroupCount,
                    BeforeAvailableConstraintCount = prev.AvailableConstraintCount,
                    AfterAvailableConstraintCount = next.AvailableConstraintCount,
                    BeforeMethod = before,
                    AfterMethod = after,
                    ExcludedCandidateRegionIds = excluded,
                    ReappearedCandidateRegionIds = reappeared,
                    AfterMinusBeforeCandidateCount = valid ? after!.CandidateCount - before!.CandidateCount : null,
                    BeforeCandidatesSubsetOfAfter = valid ? excluded!.Length == 0 : null,
                    AfterCandidatesSubsetOfBefore = valid ? reappeared!.Length == 0 : null,
                    SameOuterCandidates = valid ? excluded!.Length == 0 && reappeared!.Length == 0 : null,
                    SamePreferredCandidates = valid ? SameStanding(
                        before!.PreferredCandidateRegionIds, after!.PreferredCandidateRegionIds) : null,
                    SameUncertainCandidates = valid ? SameStanding(
                        before!.UncertainCandidateRegionIds, after!.UncertainCandidateRegionIds) : null,
                    SameConflictedCandidates = valid ? SameStanding(
                        before!.ConflictedCandidateRegionIds, after!.ConflictedCandidateRegionIds) : null,
                    EvidenceStatusChanged = valid ? before!.EvidenceStatus != after!.EvidenceStatus : null
                };
            }).ToArray();
            return new RescuerEvidenceDecisionRevisionMethod
            {
                Method = name,
                Status = steps.All(x => x.Status == "available") ? "available" :
                    steps.All(x => x.Status == "unavailable") ? "unavailable" : "partial",
                Timeline = steps,
                FirstReappearedTick = steps.Where(x => x.ReappearedCandidateRegionIds?.Count > 0)
                    .Select(x => (long?)x.AfterQueryTick).FirstOrDefault(),
                FirstExcludedTick = steps.Where(x => x.ExcludedCandidateRegionIds?.Count > 0)
                    .Select(x => (long?)x.AfterQueryTick).FirstOrDefault(),
                FirstOuterChangedTick = steps.Where(x => x.SameOuterCandidates == false)
                    .Select(x => (long?)x.AfterQueryTick).FirstOrDefault()
            };
        }).ToArray();
        return new RescuerEvidenceDecisionRevisionReport
        {
            Status = methods.All(x => x.Status == "available") ? "available" :
                methods.All(x => x.Status == "unavailable") ? "unavailable" : "partial",
            TargetWindow = publicReport.TargetWindow,
            QueryTicks = actual.Select(x => x.QueryTick).ToArray(),
            Methods = methods
        };
    }


    /// <summary>
    /// Pure comparison of two already-published actual timelines. The caller
    /// must fix identical synthetic world, original evidence and candidate
    /// states independently; matching report metadata cannot prove this.
    /// The "unprovided" side means unknown public transitions, never
    /// an assertion that transitions do not physically exist.
    /// </summary>
    public static RescuerEvidenceRelationAvailabilityReport AnalyzePublicRelationAvailability(
        RescuerEvidenceMethodReport withKnownRelations,
        RescuerEvidenceMethodReport withUnprovidedRelations)
    {
        ArgumentNullException.ThrowIfNull(withKnownRelations);
        ArgumentNullException.ThrowIfNull(withUnprovidedRelations);
        RescuerEvidenceRelationAvailabilityReport Unavailable(string reason) => new()
        {
            Status = "unavailable",
            UnavailableReason = reason,
            TargetWindow = withKnownRelations.TargetWindow
        };
        if (withKnownRelations.SchemaVersion != 1 ||
            withKnownRelations.SchemaVersion != withUnprovidedRelations.SchemaVersion ||
            withKnownRelations.ScenarioId != withUnprovidedRelations.ScenarioId ||
            withKnownRelations.Seed != withUnprovidedRelations.Seed ||
            withKnownRelations.RescuerId != withUnprovidedRelations.RescuerId ||
            withKnownRelations.TargetWindow.StartTick != withUnprovidedRelations.TargetWindow.StartTick ||
            withKnownRelations.TargetWindow.EndTick != withUnprovidedRelations.TargetWindow.EndTick ||
            withKnownRelations.ActualTimeline.Count == 0 ||
            withKnownRelations.ActualTimeline.Count != withUnprovidedRelations.ActualTimeline.Count)
        {
            return Unavailable("Incompatible public report metadata or actual timeline lengths.");
        }

        var known = withKnownRelations.ActualTimeline.OrderBy(row => row.QueryTick).ToArray();
        var unknown = withUnprovidedRelations.ActualTimeline.OrderBy(row => row.QueryTick).ToArray();
        static bool SameIds(IReadOnlyList<string> left, IReadOnlyList<string> right) =>
            left.OrderBy(id => id, StringComparer.Ordinal)
                .SequenceEqual(right.OrderBy(id => id, StringComparer.Ordinal), StringComparer.Ordinal);
        static string[] Sorted(IEnumerable<string> ids) =>
            ids.Distinct(StringComparer.Ordinal).OrderBy(id => id, StringComparer.Ordinal).ToArray();
        static bool? SameStanding(IReadOnlyList<string>? left, IReadOnlyList<string>? right) =>
            left is null || right is null ? null : SameIds(left, right);
        static bool ValidQuery(RescuerEvidenceMethodQueryRow row, long targetTick) =>
            row.Condition == "actual" && row.QueryTick >= targetTick &&
            row.ReceivedOriginalCount >= 0 &&
            row.ReceivedOriginalCount == row.ReceivedOriginalIds.Count &&
            row.ReceivedSourceObservationIds.Count == row.ReceivedOriginalCount &&
            row.ReceivedOriginalIds.Distinct(StringComparer.Ordinal).Count() ==
                row.ReceivedOriginalCount &&
            row.ReceivedSourceObservationIds.Distinct(StringComparer.Ordinal).Count() ==
                row.ReceivedOriginalCount &&
            row.AvailableSourceGroupCount >= 0 &&
            row.AvailableSourceGroupCount <= row.ReceivedOriginalCount &&
            row.AvailableConstraintCount >= 0;
        if (known.Select(row => row.QueryTick).Distinct().Count() != known.Length ||
            !known.Select(row => row.QueryTick).SequenceEqual(
                unknown.Select(row => row.QueryTick)) ||
            known.Any(row => !ValidQuery(row, withKnownRelations.TargetWindow.EndTick)) ||
            unknown.Any(row => !ValidQuery(row, withKnownRelations.TargetWindow.EndTick)) ||
            known.Zip(unknown, (a, b) => !SameIds(a.ReceivedOriginalIds, b.ReceivedOriginalIds) ||
                !SameIds(a.ReceivedSourceObservationIds, b.ReceivedSourceObservationIds) ||
                a.AvailableSourceGroupCount != b.AvailableSourceGroupCount ||
                a.AvailableConstraintCount != b.AvailableConstraintCount).Any(different => different))
        {
            return Unavailable("Actual cutoffs, received evidence or ancestry differ between conditions.");
        }

        static bool ValidMethod(RescuerEvidenceMethodRow row, int availableGroups) =>
            row.OuterCandidateRegionIds is not null &&
            row.CandidateCount >= 0 &&
            row.CandidateCount == row.OuterCandidateRegionIds.Count &&
            row.OuterCandidateRegionIds.Distinct(StringComparer.Ordinal).Count() ==
                row.CandidateCount &&
            row.BaselineCandidateCount >= 0 &&
            row.CandidateReduction == row.BaselineCandidateCount - row.CandidateCount &&
            row.AvailableSourceGroupCount == availableGroups &&
            row.UsedSourceGroupCount >= 0 &&
            row.UsedSourceGroupCount <= availableGroups &&
            row.Steps >= 0 && row.GenerationSteps >= 0 &&
            row.VerificationSteps >= 0 && row.ForwardTransitionSteps >= 0 &&
            row.BackwardTransitionSteps >= 0 && row.EvidenceRecordsInspected >= 0 &&
            row.PublicRelationsInspected >= 0;
        static bool SameWork(RescuerEvidenceMethodRow left, RescuerEvidenceMethodRow right) =>
            left.Steps == right.Steps &&
            left.GenerationSteps == right.GenerationSteps &&
            left.VerificationSteps == right.VerificationSteps &&
            left.ForwardTransitionSteps == right.ForwardTransitionSteps &&
            left.BackwardTransitionSteps == right.BackwardTransitionSteps &&
            left.EvidenceRecordsInspected == right.EvidenceRecordsInspected &&
            left.PublicRelationsInspected == right.PublicRelationsInspected;
        static bool SameSources(RescuerEvidenceMethodRow left, RescuerEvidenceMethodRow right) =>
            SameIds(left.GenerationDirectSourceIds, right.GenerationDirectSourceIds) &&
            SameIds(left.TraversalDirectSourceIds, right.TraversalDirectSourceIds) &&
            SameIds(left.UnusedDirectSourceIds, right.UnusedDirectSourceIds) &&
            left.UsedSourceGroupCount == right.UsedSourceGroupCount &&
            SameIds(left.PublicTransitionIds, right.PublicTransitionIds);

        var names = new[]
        {
            "public-prior", "known-relation-backward",
            "distributed-reconstruction", "distributed-reconstruction-local"
        }.Concat(known.Concat(unknown).SelectMany(row => row.Methods.Select(m => m.Method)))
            .Distinct(StringComparer.Ordinal).OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();
        var methods = names.Select(name =>
        {
            var timeline = known.Zip(unknown, (kRow, uRow) =>
            {
                var kMatches = kRow.Methods.Where(m => m.Method == name).ToArray();
                var uMatches = uRow.Methods.Where(m => m.Method == name).ToArray();
                var valid = kMatches.Length == 1 && uMatches.Length == 1 &&
                    ValidMethod(kMatches[0], kRow.AvailableSourceGroupCount) &&
                    ValidMethod(uMatches[0], uRow.AvailableSourceGroupCount);
                var k = valid ? kMatches[0] : null;
                var u = valid ? uMatches[0] : null;
                var knownOnly = valid ? Sorted(k!.OuterCandidateRegionIds.Except(
                    u!.OuterCandidateRegionIds, StringComparer.Ordinal)) : null;
                var unprovidedOnly = valid ? Sorted(u!.OuterCandidateRegionIds.Except(
                    k!.OuterCandidateRegionIds, StringComparer.Ordinal)) : null;
                return new RescuerEvidenceRelationAvailabilityTick
                {
                    QueryTick = kRow.QueryTick,
                    Status = valid ? "available" : "unavailable",
                    UnavailableReason = valid ? null :
                        "Missing, duplicated or inconsistent method result.",
                    ReceivedOriginalIds = Sorted(kRow.ReceivedOriginalIds),
                    ReceivedSourceObservationIds = Sorted(kRow.ReceivedSourceObservationIds),
                    ReceivedOriginalCount = kRow.ReceivedOriginalCount,
                    AvailableSourceGroupCount = kRow.AvailableSourceGroupCount,
                    AvailableConstraintCount = kRow.AvailableConstraintCount,
                    KnownRelationMethod = k,
                    UnprovidedRelationMethod = u,
                    KnownMinusUnprovidedCandidateCount = valid ? k!.CandidateCount - u!.CandidateCount : null,
                    KnownOnlyCandidateRegionIds = knownOnly,
                    UnprovidedOnlyCandidateRegionIds = unprovidedOnly,
                    KnownCandidatesSubsetOfUnprovided = valid ? knownOnly!.Length == 0 : null,
                    UnprovidedCandidatesSubsetOfKnown = valid ? unprovidedOnly!.Length == 0 : null,
                    SameOuterCandidates = valid ? knownOnly!.Length == 0 &&
                        unprovidedOnly!.Length == 0 : null,
                    SamePreferredCandidates = valid ? SameStanding(
                        k!.PreferredCandidateRegionIds, u!.PreferredCandidateRegionIds) : null,
                    SameUncertainCandidates = valid ? SameStanding(
                        k!.UncertainCandidateRegionIds, u!.UncertainCandidateRegionIds) : null,
                    SameConflictedCandidates = valid ? SameStanding(
                        k!.ConflictedCandidateRegionIds, u!.ConflictedCandidateRegionIds) : null,
                    SameEvidenceStatus = valid ? k!.EvidenceStatus == u!.EvidenceStatus : null,
                    SameSourceUsage = valid ? SameSources(k!, u!) : null,
                    SameWorkCounters = valid ? SameWork(k!, u!) : null
                };
            }).ToArray();
            return new RescuerEvidenceRelationAvailabilityMethod
            {
                Method = name,
                Status = timeline.All(t => t.Status == "available") ? "available" :
                    timeline.All(t => t.Status == "unavailable") ? "unavailable" : "partial",
                Timeline = timeline
            };
        }).ToArray();
        return new RescuerEvidenceRelationAvailabilityReport
        {
            Status = methods.All(m => m.Status == "available") ? "available" :
                methods.All(m => m.Status == "unavailable") ? "unavailable" : "partial",
            TargetWindow = withKnownRelations.TargetWindow,
            QueryTicks = known.Select(row => row.QueryTick).ToArray(),
            Methods = methods
        };
    }


    /// <summary>
    /// Pure same-historical-tick public unit count of actual receipt-gated outer
    /// regions. Original report/method/ancestry remains unchanged. Future mapping,
    /// partial mapping and missing region mapping are unresolved, never zero sites.
    /// This does not call F20's future public projection or invent candidates.
    /// </summary>
    public static RescuerEvidenceHistoricalUnitReport AnalyzeHistoricalSearchUnits(
        RescuerEvidenceMethodReport report,
        IReadOnlyList<string> regionIds,
        IReadOnlyList<string> unitIds,
        IReadOnlyList<PublicSearchUnitMapping> unitMappings)
    {
        ArgumentNullException.ThrowIfNull(report);
        ArgumentNullException.ThrowIfNull(report.TargetWindow);
        ArgumentNullException.ThrowIfNull(report.ActualTimeline);
        ArgumentNullException.ThrowIfNull(unitMappings);
        if (report.SchemaVersion != 1 || report.ActualTimeline.Count == 0 ||
            report.TargetWindow.StartTick != report.TargetWindow.EndTick)
        {
            throw new InvalidDataException("An actual exact-tick schema-v1 report is required.");
        }

        static void RequireId(string id)
        {
            if (string.IsNullOrWhiteSpace(id) || id.Length > 512 ||
                id != id.Trim() || id.Any(char.IsControl))
            {
                throw new InvalidDataException("Invalid public projection identifier.");
            }
        }
        static HashSet<string> Catalog(IReadOnlyList<string> entries)
        {
            ArgumentNullException.ThrowIfNull(entries);
            var result = new HashSet<string>(StringComparer.Ordinal);
            foreach (var id in entries)
            {
                RequireId(id);
                if (!result.Add(id))
                {
                    throw new InvalidDataException("Duplicate public catalog identifier.");
                }
            }
            return result;
        }
        static string[] Ordered(IEnumerable<string> ids) =>
            ids.Distinct(StringComparer.Ordinal).OrderBy(id => id, StringComparer.Ordinal).ToArray();

        var regions = Catalog(regionIds);
        var units = Catalog(unitIds);
        var mappings = new Dictionary<string, PublicSearchUnitMapping>(StringComparer.Ordinal);
        foreach (var mapping in unitMappings)
        {
            ArgumentNullException.ThrowIfNull(mapping);
            RequireId(mapping.RegionId);
            RequireId(mapping.SourceId);
            if (!regions.Contains(mapping.RegionId) ||
                mapping.TargetTick != report.TargetWindow.EndTick || mapping.AvailableTick < 0)
            {
                throw new InvalidDataException("Invalid historical mapping region or time.");
            }
            var targets = Catalog(mapping.UnitIds);
            if (targets.Any(id => !units.Contains(id)) ||
                (mapping.IsComplete && targets.Count == 0) ||
                !mappings.TryAdd(mapping.RegionId, mapping with { UnitIds = Ordered(targets) }))
            {
                throw new InvalidDataException("Invalid or duplicate public unit mapping.");
            }
        }

        var names = new[]
        {
            "public-prior", "known-relation-backward",
            "distributed-reconstruction", "distributed-reconstruction-local"
        };
        var ticks = report.ActualTimeline.OrderBy(row => row.QueryTick).ToArray();
        if (ticks.Any(row => row is null || row.Condition != "actual" ||
            row.QueryTick < report.TargetWindow.EndTick || row.Methods is null) ||
            ticks.Select(row => row.QueryTick).Distinct().Count() != ticks.Length)
        {
            throw new InvalidDataException("Invalid or repeated actual query tick.");
        }
        var timeline = ticks.Select(row => new RescuerEvidenceHistoricalUnitTick
        {
            QueryTick = row.QueryTick,
            ReceivedOriginalCount = row.ReceivedOriginalCount,
            AvailableSourceGroupCount = row.AvailableSourceGroupCount,
            Methods = names.Select(name =>
            {
                var matches = row.Methods.Where(method =>
                    method is not null && method.Method == name).ToArray();
                var valid = matches.Length == 1 &&
                    matches[0].OuterCandidateRegionIds is not null &&
                    matches[0].CandidateCount >= 0 && matches[0].BaselineCandidateCount >= 0 &&
                    matches[0].CandidateCount == matches[0].OuterCandidateRegionIds.Count &&
                    matches[0].CandidateReduction ==
                        matches[0].BaselineCandidateCount - matches[0].CandidateCount &&
                    matches[0].AvailableSourceGroupCount == row.AvailableSourceGroupCount &&
                    matches[0].OuterCandidateRegionIds.All(id =>
                        !string.IsNullOrWhiteSpace(id) && id.Length <= 512 &&
                        id == id.Trim() && !id.Any(char.IsControl)) &&
                    matches[0].OuterCandidateRegionIds.Distinct(StringComparer.Ordinal).Count() ==
                        matches[0].CandidateCount;
                if (!valid)
                {
                    return new RescuerEvidenceHistoricalUnitMethod
                    {
                        Method = name,
                        Status = "unavailable",
                        UnavailableReason = "Missing, duplicated or inconsistent public method row.",
                        ComparisonEligible = false
                    };
                }
                var candidates = Ordered(matches[0].OuterCandidateRegionIds);
                var known = new HashSet<string>(StringComparer.Ordinal);
                var unresolved = new List<string>();
                var sources = new HashSet<string>(StringComparer.Ordinal);
                foreach (var region in candidates)
                {
                    if (!regions.Contains(region) ||
                        !mappings.TryGetValue(region, out var mapping) ||
                        mapping.AvailableTick > row.QueryTick)
                    {
                        unresolved.Add(region);
                        continue;
                    }
                    known.UnionWith(mapping.UnitIds);
                    sources.Add(mapping.SourceId);
                    if (!mapping.IsComplete)
                    {
                        unresolved.Add(region);
                    }
                }
                var empty = candidates.Length == 0;
                return new RescuerEvidenceHistoricalUnitMethod
                {
                    Method = name,
                    Status = empty ? "empty-candidates" :
                        unresolved.Count == 0 ? "available" : "partial",
                    CandidateRegionCount = candidates.Length,
                    CandidateRegionIds = candidates,
                    KnownUnitIds = Ordered(known),
                    KnownUnitCount = known.Count,
                    UnresolvedRegionIds = Ordered(unresolved),
                    MappingSourceIds = Ordered(sources),
                    ComparisonEligible = !empty && unresolved.Count == 0
                };
            }).ToArray()
        }).ToArray();
        var methods = timeline.SelectMany(row => row.Methods).ToArray();
        return new RescuerEvidenceHistoricalUnitReport
        {
            Status = methods.All(m => m.ComparisonEligible) ? "available" :
                methods.All(m => !m.ComparisonEligible) ? "unavailable" : "partial",
            HistoricalTick = report.TargetWindow.EndTick,
            ActualTimeline = timeline
        };
    }

    /// <summary>
    /// Project each of four published same-cutoff ablations onto one historical
    /// public unit catalog, then compare addition of either original to the
    /// other singleton. This only reads public method rows and reuses the
    /// existing historical projection's catalog and cutoff validation.
    /// An ablation is a counterfactual withheld source, not an actual receipt.
    /// </summary>
    public static RescuerEvidencePairedHistoricalUnitReport AnalyzePairedHistoricalSearchUnits(
        RescuerEvidenceMethodReport report,
        IReadOnlyList<string> regionIds,
        IReadOnlyList<string> unitIds,
        IReadOnlyList<PublicSearchUnitMapping> unitMappings)
    {
        ArgumentNullException.ThrowIfNull(report);
        ArgumentNullException.ThrowIfNull(report.TargetWindow);
        ArgumentNullException.ThrowIfNull(report.SameCutoffAblations);
        ArgumentNullException.ThrowIfNull(unitMappings);
        RescuerEvidencePairedHistoricalUnitReport Unavailable(string reason) => new()
        {
            Status = "unavailable",
            UnavailableReason = reason,
            HistoricalTick = report.TargetWindow.EndTick
        };
        // The existing paired analysis validates exact source identities,
        // singleton/both membership, one common cutoff and duplicate rows.
        var paired = AnalyzePairedContributions(report);
        if (paired.OriginalIds.Count != 2 || paired.QueryTick is null)
        {
            return Unavailable(paired.UnavailableReason ?? "Invalid same-cutoff ablations.");
        }

        var ablations = report.SameCutoffAblations;
        var firstId = paired.OriginalIds[0];
        var secondId = paired.OriginalIds[1];
        var rows = new[]
        {
            ablations.Single(row => row.ReceivedOriginalCount == 0),
            ablations.Single(row => row.ReceivedOriginalCount == 1 &&
                row.ReceivedOriginalIds[0] == firstId),
            ablations.Single(row => row.ReceivedOriginalCount == 1 &&
                row.ReceivedOriginalIds[0] == secondId),
            ablations.Single(row => row.ReceivedOriginalCount == 2)
        };
        var labels = new[] { "neither", "first-only", "second-only", "both" };
        // The historical API intentionally accepts only actual rows. Convert
        // the *published* ablation row's label on a temporary report, without
        // changing candidates, cutoff, receipts or the original schema-v1.
        var projections = rows.Select(row => AnalyzeHistoricalSearchUnits(
            report with { ActualTimeline = [row with { Condition = "actual" }] },
            regionIds, unitIds, unitMappings).ActualTimeline.Single()).ToArray();
        static string[] Sorted(IEnumerable<string> ids) =>
            ids.Distinct(StringComparer.Ordinal).OrderBy(id => id, StringComparer.Ordinal).ToArray();
        static bool? SameStanding(IReadOnlyList<string>? a, IReadOnlyList<string>? b) =>
            a is null || b is null ? null :
                Sorted(a).SequenceEqual(Sorted(b), StringComparer.Ordinal);
        var names = new[]
        {
            "public-prior", "known-relation-backward",
            "distributed-reconstruction", "distributed-reconstruction-local"
        };
        var methods = names.Select(name =>
        {
            var conditions = rows.Select((row, i) =>
            {
                var unit = projections[i].Methods.Single(method => method.Method == name);
                var matches = row.Methods.Where(method =>
                    method is not null && method.Method == name).ToArray();
                return new RescuerEvidencePairedHistoricalUnitCondition
                {
                    Label = labels[i],
                    ReceivedOriginalIds = Sorted(row.ReceivedOriginalIds),
                    Status = unit.Status,
                    PublicMethod = unit.Status != "unavailable" && matches.Length == 1
                        ? matches[0] : null,
                    UnitProjection = unit
                };
            }).ToArray();

            RescuerEvidencePairedHistoricalUnitMarginal CompareAddition(
                int beforeIndex, string addedOriginalId)
            {
                var before = conditions[beforeIndex];
                var after = conditions[3];
                var b = before.PublicMethod;
                var a = after.PublicMethod;
                var eligible = before.UnitProjection.ComparisonEligible &&
                    after.UnitProjection.ComparisonEligible;
                var unitsStatus = b is null || a is null ? "unavailable" :
                    eligible ? "available" :
                    before.Status == "empty-candidates" || after.Status == "empty-candidates"
                        ? "empty-candidates" : "partial";
                return new RescuerEvidencePairedHistoricalUnitMarginal
                {
                    FromCondition = before.Label,
                    ToCondition = after.Label,
                    AddedOriginalId = addedOriginalId,
                    Status = b is null || a is null ? "unavailable" :
                        eligible ? "available" : "partial",
                    UnitComparisonStatus = unitsStatus,
                    ExcludedOuterRegionIds = b is null || a is null ? null :
                        Sorted(b.OuterCandidateRegionIds.Except(
                            a.OuterCandidateRegionIds, StringComparer.Ordinal)),
                    ReappearedOuterRegionIds = b is null || a is null ? null :
                        Sorted(a.OuterCandidateRegionIds.Except(
                            b.OuterCandidateRegionIds, StringComparer.Ordinal)),
                    RemovedKnownUnitIds = !eligible ? null :
                        Sorted(before.UnitProjection.KnownUnitIds.Except(
                            after.UnitProjection.KnownUnitIds, StringComparer.Ordinal)),
                    AddedKnownUnitIds = !eligible ? null :
                        Sorted(after.UnitProjection.KnownUnitIds.Except(
                            before.UnitProjection.KnownUnitIds, StringComparer.Ordinal)),
                    SamePreferredCandidates = b is null || a is null ? null :
                        SameStanding(b.PreferredCandidateRegionIds, a.PreferredCandidateRegionIds),
                    SameUncertainCandidates = b is null || a is null ? null :
                        SameStanding(b.UncertainCandidateRegionIds, a.UncertainCandidateRegionIds),
                    SameConflictedCandidates = b is null || a is null ? null :
                        SameStanding(b.ConflictedCandidateRegionIds, a.ConflictedCandidateRegionIds),
                    UsedSourceGroupDelta = b is null || a is null ? null :
                        a.UsedSourceGroupCount - b.UsedSourceGroupCount,
                    GenerationStepsDelta = b is null || a is null ? null :
                        a.GenerationSteps - b.GenerationSteps,
                    VerificationStepsDelta = b is null || a is null ? null :
                        a.VerificationSteps - b.VerificationSteps,
                    ForwardTransitionStepsDelta = b is null || a is null ? null :
                        a.ForwardTransitionSteps - b.ForwardTransitionSteps,
                    BackwardTransitionStepsDelta = b is null || a is null ? null :
                        a.BackwardTransitionSteps - b.BackwardTransitionSteps,
                    EvidenceRecordsInspectedDelta = b is null || a is null ? null :
                        a.EvidenceRecordsInspected - b.EvidenceRecordsInspected,
                    PublicRelationsInspectedDelta = b is null || a is null ? null :
                        a.PublicRelationsInspected - b.PublicRelationsInspected
                };
            }

            var marginals = new[]
            {
                CompareAddition(1, secondId),
                CompareAddition(2, firstId)
            };
            return new RescuerEvidencePairedHistoricalUnitMethod
            {
                Method = name,
                Status = conditions.All(item => item.Status == "available") ? "available" :
                    conditions.All(item => item.Status == "unavailable") ? "unavailable" : "partial",
                Conditions = conditions,
                Marginals = marginals
            };
        }).ToArray();
        return new RescuerEvidencePairedHistoricalUnitReport
        {
            Status = methods.All(method => method.Status == "available") ? "available" :
                methods.All(method => method.Status == "unavailable") ? "unavailable" : "partial",
            HistoricalTick = report.TargetWindow.EndTick,
            QueryTick = paired.QueryTick,
            OriginalIds = paired.OriginalIds,
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
