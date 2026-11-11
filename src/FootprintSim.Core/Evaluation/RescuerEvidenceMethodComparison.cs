using FootprintSim.Interactions.CandidateWarps;
using FootprintSim.Evidence;
using FootprintSim.Hazards;
using FootprintSim.Inference;
using FootprintSim.Inference.PastStates;
using FootprintSim.Inference.SearchProjection;
using FootprintSim.Interactions;
using FootprintSim.Interactions.Tracing;
using FootprintSim.Serialization;

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


// #116: Evaluation-only bridge from the receipt-gated M1-E result to the
// existing F20 public-declaration projector; no inferred real position.
public sealed record RescuerReceivedPastProjectionReport
{
    public required long QueryTick { get; init; }
    public required long TargetTick { get; init; }
    public required InteractionTimeWindow HistoricalWindow { get; init; }
    public IReadOnlyList<string> ReceivedOriginalIds { get; init; } = [];
    public IReadOnlyList<string> ReceivedSourceObservationIds { get; init; } = [];
    public required int AvailableSourceGroupCount { get; init; }
    public required int ReconstructionSteps { get; init; }
    public required RescuerEvidenceMethodRow DistributedReconstructionMethod { get; init; }
    public required PublicSearchProjectionResult Projection { get; init; }
}

// #124: the same genuinely received original evidence, four actual M1-E
// method rows, and a single public query-time unit catalog. A declaration
// projection is NOT a synthetic PastStateCandidate or an F20 candidate claim.
public sealed record RescuerCurrentUnitHistoricalRegion(
    string HistoricalRegionId,
    IReadOnlyList<string> KnownCurrentRegionIds,
    IReadOnlyList<string> KnownUnitIds,
    IReadOnlyList<string> UnresolvedReasons,
    IReadOnlyList<string> TransitionSourceIds,
    IReadOnlyList<string> UnitMappingSourceIds);

// #128: One received, confirmed Person observation at the target tick can
// contradict an otherwise complete *published* current-region declaration.
// This is a diagnostic, never an additional hard candidate exclusion.
public sealed record RescuerCurrentUnitTargetObservationConflict(
    string ReasonCode,
    string ConstraintId,
    string DirectSourceId,
    string SourceObservationId,
    IReadOnlyList<string> SourceAncestryIds,
    InteractionTimeWindow ObservationWindow,
    IReadOnlyList<string> AllowedRegionIds);

// #130: A conditional search-unit suggestion only; never a replacement
// for the published current outer, target conflict, or genuine F20 output.
public sealed record RescuerConditionalCurrentUnitProposal(
    string Status,
    string ReasonCode,
    IReadOnlyList<string>? RegionIds,
    IReadOnlyList<string>? UnitIds,
    int? UnitCountReduction,
    IReadOnlyList<string> ObservationConstraintIds,
    IReadOnlyList<string> ObservationDirectSourceIds,
    IReadOnlyList<string> ObservationSourceAncestryIds,
    IReadOnlyList<string> PublicTransitionSourceIds,
    IReadOnlyList<string> PublicUnitMappingSourceIds,
    int InspectedTargetObservationCount,
    int InspectedAncestryGroupCount);

// #132: evaluation-only latest received Person sighting, independent of all
// historical method results and the conditional historical gate.
public sealed record RescuerDirectTargetUnitBaseline(
    string Status,
    string ReasonCode,
    IReadOnlyList<string>? RegionIds,
    IReadOnlyList<string>? UnitIds,
    IReadOnlyList<string> ObservationConstraintIds,
    IReadOnlyList<string> ObservationDirectSourceIds,
    IReadOnlyList<string> ObservationSourceObservationIds,
    IReadOnlyList<string> ObservationSourceAncestryIds,
    IReadOnlyList<string> PublicUnitMappingSourceIds,
    int InspectedTargetObservationCount,
    int InspectedAncestryGroupCount);

// Neither a hard exclusion nor a working fifth inference method: contrasts
// directly observed units, the existing conditional gate, and historical
// published outer. A hypothetical intersection is explicitly NOT adopted.
public sealed record RescuerDirectWeftUnitAudit(
    string Status,
    string ReasonCode,
    int? ConditionalUnitDeltaFromDirect,
    IReadOnlyList<string>? HypotheticalIntersectionRegionIds,
    IReadOnlyList<string>? HypotheticalIntersectionUnitIds,
    int? HypotheticalUnitReductionFromDirect);

// #134: evaluation-only eligibility for a provisional overlap. Unlike the
// #132 hypothetical audit, this explicitly checks source ancestry across
// the historical generator and the latest received Person observation.
public sealed record RescuerProvisionalOverlapAssessment
{
    public required string Status { get; init; }
    public required string ReasonCode { get; init; }
    public IReadOnlyList<string>? OverlapRegionIds { get; init; }
    public IReadOnlyList<string>? OverlapUnitIds { get; init; }
    public int? DirectRegionCount { get; init; }
    public int? OverlapRegionCount { get; init; }
    public int? RegionReductionFromDirect { get; init; }
    public int? DirectUnitCount { get; init; }
    public int? OverlapUnitCount { get; init; }
    public int? UnitReductionFromDirect { get; init; }
    public IReadOnlyList<string> HistoricalGenerationDirectSourceIds { get; init; } = [];
    public IReadOnlyList<string> ObservationDirectSourceIds { get; init; } = [];
    public IReadOnlyList<string> SharedSourceAncestryIds { get; init; } = [];
    public IReadOnlyList<string> PublicTransitionSourceIds { get; init; } = [];
    public IReadOnlyList<string> PublicUnitMappingSourceIds { get; init; } = [];
    public required int InspectedHistoricalSourceCount { get; init; }
    public required int InspectedTargetObservationCount { get; init; }
}

public sealed record RescuerCurrentUnitMethod(
    RescuerEvidenceMethodRow HistoricalMethod,
    string ProjectionStatus,
    IReadOnlyList<RescuerCurrentUnitHistoricalRegion> HistoricalRegions,
    IReadOnlyList<string>? KnownCurrentRegionIds,
    IReadOnlyList<string>? KnownUnitIds,
    IReadOnlyList<string>? UnresolvedHistoricalRegionIds)
{
    // Empty means no established contradiction, not proof of agreement.
    public IReadOnlyList<RescuerCurrentUnitTargetObservationConflict> TargetTickConflicts { get; init; } = [];
    public RescuerConditionalCurrentUnitProposal? ConditionalTargetUnitProposal { get; init; }
    public RescuerDirectWeftUnitAudit? DirectVsWeftAudit { get; init; }
    public RescuerProvisionalOverlapAssessment? ProvisionalOverlapAssessment { get; init; }
}

public sealed record RescuerCurrentUnitReceivedOriginal(
    string OriginalRecordId,
    string OriginalObservationId,
    long FirstReceivedAtTick);

// Only M1-E has actual candidate IDs, dispositions and ancestry for F20.
public sealed record RescuerCurrentUnitMethodComparison(
    long QueryTick,
    long TargetTick,
    InteractionTimeWindow HistoricalWindow,
    IReadOnlyList<RescuerCurrentUnitReceivedOriginal> ReceivedOriginals,
    IReadOnlyList<RescuerCurrentUnitMethod> Methods,
    PublicSearchProjectionResult ActualReconstructionF20Projection)
{
    public RescuerDirectTargetUnitBaseline? DirectTargetUnitBaseline { get; init; }
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

// Formal public explanations make a missing direct declaration non-essential;
// they are not verified Person movements.
public sealed record RescuerMissingDeclaredPathCandidate(
    string RegionId,
    string DeclaredPathStatus,
    IReadOnlyList<RescuerMissingDeclaredPathQuestion> Questions,
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


// #110: This report is strictly separate from the existing question-suppression
// explanations. A public conditional candidate never verifies a Person stay.
public sealed record RescuerConditionalPublishedDwellReport(
    RescuerMissingDeclaredPathReport Baseline,
    string Status,
    string? HoldReason,
    IReadOnlyList<string> ConditionalPublicCandidates,
    int BaselineQuestionCount,
    int RemainingQuestionCount,
    int UnconditionallyResolvedQuestionCount,
    int PreflightRegionsInspected,
    int PreflightBranchesInspected,
    bool? PublicGraphIsDag,
    bool? LongArithmeticBoundVerified,
    int PropagateCalls,
    int SelectedPathEdgesInspected);


/* #112: one fixed, evaluation-only information contrast. Neither a new
 * inference method nor evidence that a declared public edge was traversed. */
public sealed record RescuerRelationQuestionUtilityMethod(
    string Method,
    string UnitDifferenceStatus,
    RescuerEvidenceMethodRow WithoutDeclaration,
    RescuerEvidenceMethodRow WithDeclaration,
    RescuerEvidenceHistoricalUnitMethod UnitsWithoutDeclaration,
    RescuerEvidenceHistoricalUnitMethod UnitsWithDeclaration,
    IReadOnlyList<string> ExcludedOuterRegionIds,
    IReadOnlyList<string> ReappearedOuterRegionIds,
    IReadOnlyList<string>? RemovedKnownUnitIds,
    IReadOnlyList<string>? AddedKnownUnitIds);

public sealed record RescuerRelationQuestionUtilityReport
{
    public required string Status { get; init; }
    public string? HoldReason { get; init; }
    public required long QueryTick { get; init; }
    public required long HistoricalTick { get; init; }
    public IReadOnlyList<string> ReceivedOriginalIds { get; init; } = [];
    public IReadOnlyList<RescuerMissingDeclaredPathQuestion> QuestionsWithoutDeclaration { get; init; } = [];
    public IReadOnlyList<RescuerMissingDeclaredPathQuestion> QuestionsWithDeclaration { get; init; } = [];
    public IReadOnlyList<string> ResolvedQuestionIds { get; init; } = [];
    public IReadOnlyList<string> NewlyUnconfirmedQuestionIds { get; init; } = [];
    public IReadOnlyList<RescuerRelationQuestionUtilityMethod> Methods { get; init; } = [];
}



/* #114: public unit-level grouping of the #104 declared-path diagnostics.
 * A witness is a formal, sourced compatibility explanation, never proof of
 * Person travel, safety, or a warrant to remove outer candidates/units. */
// #118: unwitnessed diagnostic labels, not negative evidence.
public sealed record RescuerDeclaredWitnessUnitRegion(
    string RegionId,
    string CompatibilityStatus,
    IReadOnlyList<RescuerDeclaredPathWitness> DirectWitnesses,
    IReadOnlyList<string> UnwitnessedReasonCodes);

public sealed record RescuerDeclaredWitnessUnit(
    string UnitId,
    IReadOnlyList<RescuerDeclaredWitnessUnitRegion> Regions,
    IReadOnlyList<string> WitnessedRegionIds,
    IReadOnlyList<string> UnknownRegionIds,
    IReadOnlyList<string> ConflictedRegionIds,
    IReadOnlyList<string> UnavailableRegionIds);

public sealed record RescuerDeclaredWitnessUnresolvedRegion(
    string RegionId,
    string Reason);

public sealed record RescuerDeclaredWitnessUnitCondition(
    string Label,
    string Status,
    string? HoldReason,
    RescuerEvidenceMethodRow PublicMethod,
    RescuerEvidenceHistoricalUnitMethod UnitProjection,
    IReadOnlyList<RescuerDeclaredWitnessUnit> Units,
    IReadOnlyList<RescuerDeclaredWitnessUnresolvedRegion> UnresolvedRegions,
    int PublicConstraintsInspected,
    int PublicBranchesInspected,
    int ConstraintBranchPairsInspected);

public sealed record RescuerDeclaredWitnessUnitMethod(
    string Method,
    RescuerDeclaredWitnessUnitCondition WithoutDeclaration,
    RescuerDeclaredWitnessUnitCondition WithDeclaration);

public sealed record RescuerDeclaredWitnessUnitReport(
    string Status,
    string? HoldReason,
    long QueryTick,
    long HistoricalTick,
    IReadOnlyList<string> ReceivedOriginalIds,
    IReadOnlyList<RescuerDeclaredWitnessUnitMethod> Methods);

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
                                    // This label describes the direct edge only:
                                    // a separately published multi-hop alternative
                                    // may still fit the same observation window.
                                }
                                // This is a formal same-region explanation,
                                // never evidence of a realized Person stay. Keep
                                // the existing direct-pair semantics for self-pairs.
                                if (region == destination)
                                {
                                    if (!directPublished)
                                        explanations.Add("formal-stay:" + region);
                                    pairExplained = true;
                                    continue;
                                }
                                // Search only finite simple routes of already-
                                // published branches. Exclude the direct pair
                                // already diagnosed above: only paths with two
                                // or more published edges can be alternatives.
                                // A positive interval overlap needs no missing
                                // direct declaration; too-short paths still
                                // need an untested dwell assumption.
                                var routeBranches = directPublished
                                    ? publishedBranches.Where(b =>
                                        b.SourceRegionId != region ||
                                        b.DestinationRegionId != destination).ToArray()
                                    : publishedBranches;
                                var paths = FindPublishedRoutes(region, destination,
                                    minimum, maximum, routeBranches);
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
                                // A known direct pair never generates an
                                // unconfirmed-direct-edge question, even when
                                // only alternative route timing is uncertain.
                                if (directPublished)
                                    continue;
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


    /// <summary>
    /// #110 / AR-15. A deliberately fixed, synthetic B@2 -> D@6 calibration.
    /// The #108 question report is never changed. The only new result is an
    /// explicitly hypothetical public path conditional on NO unpublished forced
    /// departure from D. This does not decide an actual Person location.
    /// </summary>
    public static RescuerConditionalPublishedDwellReport AnalyzeConditionalPublishedDwell(
        RescuerEvidenceInput received,
        InteractionTimeWindow targetWindow,
        long historicalTargetTick)
    {
        var baseline = AnalyzeMissingDeclaredPathQuestions(received, targetWindow);
        var questionCount = baseline.Candidates.Sum(row => row.Questions.Count);
        var inspectedRegions = 0;
        var inspectedBranches = 0;
        int propagateCalls = 0, pathInspections = 0;
        bool? dagVerified = null, boundVerified = null;
        var conditional = new List<string>();
        string? withheld = null;
        var input = received.Input;
        var envelope = input.PublicContext.PublicHazardEnvelope;

        // The two independently received originals and historical Person ticks
        // are a testable fixed slice, not a hidden rule for arbitrary histories.
        var question = baseline.Candidates
            .Where(row => row.RegionId == "B")
            .SelectMany(row => row.Questions)
            .FirstOrDefault(q => q.SourceRegionId == "B" &&
                q.DestinationRegionId == "D" &&
                q.MinimumCompatibleDifferenceTicks == 4 &&
                q.MaximumCompatibleDifferenceTicks == 4 &&
                q.IndependentSourceAncestry);
        if (input.CurrentTick != 8 || historicalTargetTick != 6 ||
            targetWindow.StartTick != 2 || targetWindow.EndTick != 2 ||
            received.ReceivedRecords.Count != 2 || question is null ||
            !received.ReceivedRecords.Any(r => r.OriginalRecordId ==
                question.EarlyDirectSourceId && r.FirstReceivedAtTick == 7) ||
            !received.ReceivedRecords.Any(r => r.OriginalRecordId ==
                question.LaterDirectSourceId && r.FirstReceivedAtTick == 8))
        {
            withheld = received.ReceivedRecords.Count < 2
                ? "fixed-two-independent-originals-not-yet-eligible"
                : "outside-fixed-calibration-slice";
        }
        else if (envelope is null)
        {
            withheld = "public-envelope-unavailable";
        }
        else
        {
            var candidateStates = input.PublicContext.PublicCausalTracingModel!.CandidateStates
                .Where(s => s.TimeWindow.StartTick == 2 &&
                    s.TimeWindow.EndTick == 2 &&
                    s.RegionIds.Contains("B", StringComparer.Ordinal)).ToArray();
            var constraints = (input.AvailableCausalTracingConstraints?
                .PubliclyAvailableAt(input.CurrentTick).Constraints ?? []).ToArray();
            var early = constraints.SingleOrDefault(c =>
                c.ConstraintId == question.EarlyConstraintId);
            var later = constraints.SingleOrDefault(c =>
                c.ConstraintId == question.LaterConstraintId);
            if (candidateStates.Length != 1 || early is null || later is null ||
                early.ConstrainedTimeWindow.StartTick != 2 ||
                early.ConstrainedTimeWindow.EndTick != 2 ||
                later.ConstrainedTimeWindow.StartTick != 6 ||
                later.ConstrainedTimeWindow.EndTick != 6 ||
                !early.AllowedRegionIds.Contains("B", StringComparer.Ordinal) ||
                !later.AllowedRegionIds.Contains("D", StringComparer.Ordinal))
            {
                withheld = "person-window-not-exact";
            }
            else if (!ConditionalEnvelopePreflight(envelope, 2, historicalTargetTick,
                out inspectedRegions, out inspectedBranches,
                out dagVerified, out boundVerified, out withheld))
            {
                // On a failed preflight, do not call Propagate or infer a
                // negative stay. The baseline's unknown questions survive.
            }
            else if (envelope.Branches.Any(b => b.SourceRegionId == "D"))
            {
                withheld = "published-departure-from-D";
            }
            else
            {
                // A reached-D aggregate can originate at a shifted time. Verify
                // the advertised B@2 source against the public region before
                // interpreting any B@2>E@3>F@4>D@5 path as time compatible.
                var origin = envelope.Regions.SingleOrDefault(r => r.RegionId == "B");
                if (origin is null || origin.MinimumArrivalTick > 2 ||
                    origin.MaximumArrivalTick < 2)
                    withheld = "selected-route-origin-window-incompatible";

                // Check this very path, not simply an aggregated reached-D set.
                // Each probe inspects all public branches, in canonical order.
                var selected = new List<PublicHazardBranch>();
                var hops = new[] { ("B", "E"), ("E", "F"), ("F", "D") };
                for (var i = 0; withheld is null && i < hops.Length; i++)
                {
                    var (from, to) = hops[i];
                    PublicHazardBranch? match = null;
                    foreach (var branch in envelope.Branches.OrderBy(b =>
                        b.SourceRegionId, StringComparer.Ordinal).ThenBy(b =>
                        b.DestinationRegionId, StringComparer.Ordinal).ThenBy(b =>
                        b.ProvenanceId, StringComparer.Ordinal))
                    {
                        pathInspections++;
                        if (branch.SourceRegionId == from &&
                            branch.DestinationRegionId == to &&
                            branch.MinimumTravelTicks == 1 &&
                            branch.MaximumTravelTicks == 1 && match is null)
                            match = branch;
                    }
                    if (match is null)
                    {
                        withheld = "selected-route-not-fixed-or-unpublished";
                        break;
                    }
                    var arrivalTick = 3L + i;
                    var node = envelope.Regions.SingleOrDefault(r =>
                        r.RegionId == to);
                    if (node is null || node.MinimumArrivalTick > arrivalTick ||
                        node.MaximumArrivalTick < arrivalTick)
                    {
                        withheld = "selected-route-arrival-window-incompatible";
                        break;
                    }
                    selected.Add(match);
                }
                if (withheld is null)
                {
                    var source = CandidateWarpNormalization.CreateState(
                        input.ScenarioId, input.Seed,
                        input.PublicContext.TargetPersonId,
                        InteractionTimeWindow.At(2),
                        CandidateWarpTemporalKind.EventPoint, ["B"],
                        EvidenceState.SupportedButUncertain, candidateStates[0].Provenance);
                    propagateCalls++;
                    var reached = PublicCandidateWarpPropagator.Propagate(
                        input.ScenarioId, input.Seed,
                        input.PublicContext.TargetPersonId,
                        [source], historicalTargetTick, envelope,
                        retainUndeclaredStay: false);
                    if (reached.RegionIds.Contains("D", StringComparer.Ordinal))
                    {
                        conditional.Add(
                            "public-conditional-early-arrival-with-stay-assumption:" +
                            "B@2>E@3>F@4>D@5~D@6:" +
                            "sources=" + string.Join(",", selected.Select(b => b.ProvenanceId)) +
                            ":assumption=no-unpublished-forced-departure-from-D+" +
                            "public-edges-person-applicable");
                    }
                    else
                    {
                        withheld = "public-propagation-does-not-retain-D";
                    }
                }
            }
        }
        // No branch mutates the baseline, even when a conditional candidate
        // exists. No conditional label enters PublicAlternativeExplanations.
        return new RescuerConditionalPublishedDwellReport(
            baseline, conditional.Count == 0 ? "withheld" : "conditional-only",
            withheld, conditional,
            questionCount, questionCount, 0, inspectedRegions, inspectedBranches,
            dagVerified, boundVerified, propagateCalls, pathInspections);
    }

    // Sufficient guard for the limited synthetic model, not a general
    // performance contract. ALL published entries are checked before recursion.
    private static bool ConditionalEnvelopePreflight(
        PublicHazardEnvelope envelope, long sourceTick, long targetTick,
        out int regionsInspected, out int branchesInspected,
        out bool? dagVerified, out bool? boundVerified, out string? reason)
    {
        // Counts reflect per-entry validation work, not envelope sizes.
        // Size/publication exits have inspected zero entries.
        regionsInspected = 0;
        branchesInspected = 0;
        dagVerified = null;
        boundVerified = null;
        reason = null;
        if (sourceTick < 0 || sourceTick > targetTick ||
            envelope.Regions.Count > 16 || envelope.Branches.Count > 8 ||
            envelope.PublishedAtTick > targetTick)
        {
            reason = "preflight-size-or-publication";
            return false;
        }
        var nodes = new HashSet<string>(StringComparer.Ordinal);
        foreach (var node in envelope.Regions)
        {
            regionsInspected++;
            if (string.IsNullOrWhiteSpace(node.RegionId) ||
                !nodes.Add(node.RegionId) ||
                node.PublishedAtTick > targetTick ||
                node.MinimumArrivalTick < 0 ||
                node.MaximumArrivalTick < node.MinimumArrivalTick)
            {
                reason = "preflight-invalid-region";
                return false;
            }
        }
        nodes.Add("B");
        var provenanceIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var edge in envelope.Branches)
        {
            branchesInspected++;
            if (!nodes.Contains(edge.SourceRegionId) ||
                !nodes.Contains(edge.DestinationRegionId) ||
                string.IsNullOrWhiteSpace(edge.ProvenanceId) ||
                !provenanceIds.Add(edge.ProvenanceId) ||
                edge.PublishedAtTick > targetTick ||
                edge.MinimumTravelTicks < 0 ||
                edge.MaximumTravelTicks < edge.MinimumTravelTicks)
            {
                reason = "preflight-invalid-or-late-branch";
                return false;
            }
        }
        // Kahn's algorithm tests *all* edges including irrelevant zero-tick
        // and hidden-from-this-query PUBLIC cycles; no path is pruned.
        var degrees = nodes.ToDictionary(id => id, _ => 0, StringComparer.Ordinal);
        foreach (var edge in envelope.Branches)
            degrees[edge.DestinationRegionId]++;
        var ready = new SortedSet<string>(
            degrees.Where(kv => kv.Value == 0).Select(kv => kv.Key),
            StringComparer.Ordinal);
        var processed = 0;
        while (ready.Count > 0)
        {
            var id = ready.Min!;
            ready.Remove(id);
            processed++;
            foreach (var edge in envelope.Branches.Where(b => b.SourceRegionId == id))
            {
                degrees[edge.DestinationRegionId]--;
                if (degrees[edge.DestinationRegionId] == 0)
                    ready.Add(edge.DestinationRegionId);
            }
        }
        dagVerified = processed == nodes.Count;
        if (!dagVerified.Value)
        {
            reason = "preflight-public-cycle";
            return false;
        }
        var maxArrival = Math.Max(sourceTick,
            envelope.Regions.Select(r => r.MinimumArrivalTick)
                .DefaultIfEmpty(sourceTick).Max());
        var maxTravel = envelope.Branches.Select(b => (long)b.MaximumTravelTicks)
            .DefaultIfEmpty(0L).Max();
        // Division first prevents overflow even when a node is near long.Max.
        boundVerified = maxTravel == 0 ||
            envelope.Branches.Count <= (long.MaxValue - maxArrival) / maxTravel;
        if (!boundVerified.Value)
        {
            reason = "preflight-long-overflow-bound";
            return false;
        }
        return true;
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
        // Breadth-first traversal cannot exhaust its budget in a dense deep
        // subtree before considering a shallow, already published route.
        var pending = new Queue<(string At, long Min, long Max,
            PublicHazardBranch[] Path, string[] Seen)>();
        pending.Enqueue((source, 0L, 0L, [], [source]));
        // Keep feasible shallow explanations ahead of already overlong
        // prefixes. Follow overlong prefixes later only to report complete
        // published routes that cannot reach the later observation in time.
        var overlongPending = new Queue<(string At, long Min, long Max,
            PublicHazardBranch[] Path, string[] Seen)>();
        string? positive = null, shorter = null, tooLong = null;
        var incomplete = false;
        var inspections = 0;

        while (pending.Count > 0 || overlongPending.Count > 0)
        {
            var current = pending.Count > 0
                ? pending.Dequeue() : overlongPending.Dequeue();
            if (!outgoing.TryGetValue(current.At, out var edges))
                continue;
            if (current.Path.Length >= maxDepth)
            {
                if (edges.Any(edge => !current.Seen.Contains(
                    edge.DestinationRegionId, StringComparer.Ordinal)))
                    incomplete = true;
                continue;
            }
            foreach (var edge in edges)
            {
                if (inspections >= edgeBudget)
                {
                    incomplete = true;
                    break;
                }
                inspections++;
                if (current.Seen.Contains(edge.DestinationRegionId,
                    StringComparer.Ordinal))
                    continue;
                // Only nonnegative public travel is accepted by the caller.
                if (current.Max > long.MaxValue - edge.MaximumTravelTicks ||
                    current.Min > long.MaxValue - edge.MinimumTravelTicks)
                {
                    incomplete = true;
                    continue;
                }
                var nextMin = current.Min + edge.MinimumTravelTicks;
                var nextMax = current.Max + edge.MaximumTravelTicks;
                var nextPath = current.Path.Append(edge).ToArray();
                if (edge.DestinationRegionId == destination)
                {
                    if (nextPath.Length >= 2)
                    {
                        var labels = source + ">" + string.Join(">",
                            nextPath.Select(part => part.DestinationRegionId));
                        var details = ":sources=" + string.Join(",",
                            nextPath.Select(part => part.ProvenanceId)) +
                            ":travel=" + nextMin + "-" + nextMax +
                            ":gap=" + gapMin + "-" + gapMax;
                        if (nextMin <= gapMax && nextMax >= gapMin)
                            positive ??= (nextPath.Length == 2
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
                    var next = (At: edge.DestinationRegionId, Min: nextMin,
                        Max: nextMax, Path: nextPath,
                        Seen: current.Seen.Append(edge.DestinationRegionId).ToArray());
                    if (nextMin <= gapMax)
                        pending.Enqueue(next);
                    else
                        overlongPending.Enqueue(next);
                }
            }
            if (inspections >= edgeBudget)
            {
                if (pending.Count > 0 || overlongPending.Count > 0)
                    incomplete = true;
                break;
            }
        }
        // A positive witness is enough to avoid the direct-edge question
        // even when unrelated, deeper alternatives remain unexplored.
        return new PublishedRouteSearch(positive, shorter, tooLong,
            positive is null && incomplete, inspections);
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
        string condition = "actual") =>
        CompareWithReconstruction(received, targetWindow, condition).Row;

    // Both the published row and the original candidate objects come from
    // one identical receipt-filtered M1-E execution, never a second inference.
    private static (RescuerEvidenceMethodQueryRow Row,
        PastStateReconstructionResult Reconstruction) CompareWithReconstruction(
        RescuerEvidenceInput received,
        InteractionTimeWindow targetWindow,
        string condition)
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
        return (new RescuerEvidenceMethodQueryRow
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
        }, weftComparison.Reconstruction);
    }


    /// <summary>
    /// Project actual receipt-gated M1-E past candidates with explicit F20
    /// published maps. No physical journey, completeness, or site reduction
    /// is inferred from a successful projection.
    /// </summary>
    public static RescuerReceivedPastProjectionReport ProjectReceivedPastCandidates(
        RescuerEvidenceInput received,
        InteractionTimeWindow historicalWindow,
        long targetTick,
        IReadOnlyList<string> regionIds,
        IReadOnlyList<string> searchUnitIds,
        IReadOnlyList<PublicRegionProjection> regionProjections,
        IReadOnlyList<PublicSearchUnitMapping> unitMappings,
        IReadOnlyList<PublicActionFootprint> actions)
    {
        var (comparison, reconstruction) =
            CompareWithReconstruction(received, historicalWindow, "actual");
        var method = comparison.Methods.Single(row =>
            row.Method == "distributed-reconstruction");
        var actualRegions = reconstruction.Candidates
            .Select(candidate => candidate.RegionId)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(id => id, StringComparer.Ordinal).ToArray();
        if (!actualRegions.SequenceEqual(method.OuterCandidateRegionIds,
            StringComparer.Ordinal))
        {
            throw new InvalidDataException(
                "Receipt-gated reconstruction candidates disagree with the method outer.");
        }

        var projectionInput = PublicPastStateProjectionAdapter.CreateInput(
            reconstruction, targetTick, regionIds, searchUnitIds,
            regionProjections, unitMappings, actions);
        var projected = PublicSearchProjector.Project(projectionInput);
        return new RescuerReceivedPastProjectionReport
        {
            QueryTick = comparison.QueryTick,
            TargetTick = targetTick,
            HistoricalWindow = historicalWindow,
            ReceivedOriginalIds = comparison.ReceivedOriginalIds,
            ReceivedSourceObservationIds = comparison.ReceivedSourceObservationIds,
            AvailableSourceGroupCount = comparison.AvailableSourceGroupCount,
            ReconstructionSteps = reconstruction.ReconstructionSteps,
            DistributedReconstructionMethod = method,
            Projection = projected
        };
    }

    // Shared-ancestry membership is transitive, including when the middle
    // observation joins two previously separate groups. Empty ancestry stays
    // ungrouped with other sources and cannot establish a hard conflict.
    private static int[][] GroupTransitiveSourceAncestry(
        IReadOnlyList<IReadOnlyList<string>> ancestries)
    {
        var groups = new List<List<int>>();
        for (var index = 0; index < ancestries.Count; index++)
        {
            var merged = new List<int> { index };
            var overlapping = groups.Where(group =>
                group.Any(member => ancestries[member]
                    .Intersect(ancestries[index], StringComparer.Ordinal).Any()))
                .ToArray();
            foreach (var group in overlapping)
            {
                merged.AddRange(group);
                groups.Remove(group);
            }
            groups.Add(merged.OrderBy(i => i).ToList());
        }
        return groups.Select(group => group.ToArray()).ToArray();
    }

    /// <summary>
    /// Compare four actual receipt-gated historical methods at the same
    /// declared current tick. For methods other than M1-E, these are public
    /// reachability outer sets, NOT F20 candidate projections or actual travel.
    /// Published declarations are validated by F20 with an EMPTY candidate
    /// list; no fake candidate identity, disposition or ancestry is created.
    /// </summary>
    public static RescuerCurrentUnitMethodComparison CompareReceivedCurrentUnits(
        RescuerEvidenceInput received,
        InteractionTimeWindow historicalWindow,
        long targetTick,
        IReadOnlyList<string> regionIds,
        IReadOnlyList<string> searchUnitIds,
        IReadOnlyList<PublicRegionProjection> regionProjections,
        IReadOnlyList<PublicSearchUnitMapping> unitMappings,
        IReadOnlyList<PublicActionFootprint> actions)
    {
        var (comparison, reconstruction) =
            CompareWithReconstruction(received, historicalWindow, "actual");
        var actual = comparison.Methods.Single(row =>
            row.Method == "distributed-reconstruction");
        var realRegions = reconstruction.Candidates.Select(candidate => candidate.RegionId)
            .Distinct(StringComparer.Ordinal).OrderBy(id => id, StringComparer.Ordinal).ToArray();
        if (!realRegions.SequenceEqual(actual.OuterCandidateRegionIds, StringComparer.Ordinal))
        {
            throw new InvalidDataException(
                "Actual M1-E reconstruction differs from its published historical outer.");
        }

        // F20 checks every declaration row (including unused ones) and the
        // authentic M1-E candidates. Never construct a fake PastStateCandidate.
        var input = PublicPastStateProjectionAdapter.CreateInput(
            reconstruction, targetTick, regionIds, searchUnitIds,
            regionProjections, unitMappings, actions);
        var realF20 = PublicSearchProjector.Project(input);
        PublicSearchProjector.Project(input with { Candidates = [] });

        static string[] OrderedIds(IEnumerable<string> ids) =>
            ids.Distinct(StringComparer.Ordinal).OrderBy(id => id, StringComparer.Ordinal).ToArray();

        // #128: Only constraints actually available to this rescuer at the
        // comparison cutoff may diagnose an observed target-tick mismatch.
        // No later/hidden Person history, Device trace, or synthetic truth is
        // allowed into the declaration-only projection.
        var receivedIds = received.ReceivedRecords
            .Where(record => record.FirstReceivedAtTick <= comparison.QueryTick)
            .Select(record => record.OriginalRecordId)
            .ToHashSet(StringComparer.Ordinal);
        var targetObservations = (received.Input.AvailableCausalTracingConstraints?
            .PubliclyAvailableAt(comparison.QueryTick).Constraints ?? [])
            .Where(c => c.ScenarioId == received.Input.ScenarioId &&
                c.Seed == received.Input.Seed &&
                c.TargetEntityId == received.Input.PublicContext.TargetPersonId &&
                c.Visibility == InteractionVisibility.PublicEvidence &&
                c.AvailableAtTick <= comparison.QueryTick &&
                receivedIds.Contains(c.DirectSourceId) &&
                c.IsPersonEvidence && !c.IsDeviceOnlyEvidence &&
                targetTick <= comparison.QueryTick &&
                // Interval occurrence time is not proof of location at targetTick.
                // Only an exact-tick Person observation can establish a hard mismatch.
                c.ConstrainedTimeWindow.StartTick == targetTick &&
                c.ConstrainedTimeWindow.EndTick == targetTick &&
                c.AllowedRegionIds.Count > 0)
            .OrderBy(c => c.ConstraintId, StringComparer.Ordinal)
            .ToArray();

        var observationSourceGroups = GroupTransitiveSourceAncestry(
            targetObservations.Select(c => c.SourceAncestryIds).ToArray());

        // #132: One shared eligibility/ancestry evaluation, before any
        // historical-method result exists. Neither historical outer nor the
        // conditional proposal can influence this received-observation set.
        (string Status, string Reason, string[]? Regions) TargetEvidenceRegions()
        {
            if (observationSourceGroups.Length == 0)
                return ("unavailable", "no-received-exact-target-person-observation", null);
            if (observationSourceGroups.Any(group => group.Any(index =>
            {
                var c = targetObservations[index];
                return c.EvidenceBoundaryState != EvidenceState.Confirmed ||
                    c.SourceAncestryIds.Count == 0 ||
                    string.IsNullOrWhiteSpace(c.DirectSourceId) ||
                    string.IsNullOrWhiteSpace(c.SourceObservationId);
            })))
                return ("unavailable", "target-observation-not-fully-confirmed-or-sourced", null);

            // A shared source group is a union of alternatives, NOT votes.
            // Independent incompatible groups withhold an empty recommendation.
            var alternatives = observationSourceGroups.Select(group =>
                OrderedIds(group.SelectMany(index =>
                    targetObservations[index].AllowedRegionIds))).ToArray();
            var selected = alternatives[0].ToHashSet(StringComparer.Ordinal);
            foreach (var group in alternatives.Skip(1))
                selected.IntersectWith(group);
            return selected.Count == 0
                ? ("conflict", "independent-target-observations-disagree", null)
                : ("available", "received-exact-person-observation", OrderedIds(selected));
        }

        var targetEvidence = TargetEvidenceRegions();
        RescuerDirectTargetUnitBaseline DirectResult(
            string status, string reason, string[]? regions = null,
            string[]? units = null, string[]? mappingSources = null) => new(
                status, reason, regions, units,
                OrderedIds(targetObservations.Select(c => c.ConstraintId)),
                OrderedIds(targetObservations.Select(c => c.DirectSourceId)),
                OrderedIds(targetObservations.Select(c => c.SourceObservationId)),
                OrderedIds(targetObservations.SelectMany(c => c.SourceAncestryIds)),
                mappingSources ?? [],
                targetObservations.Length, observationSourceGroups.Length);

        RescuerDirectTargetUnitBaseline BuildDirectBaseline()
        {
            if (targetEvidence.Status != "available")
                return DirectResult(targetEvidence.Status, targetEvidence.Reason);
            var regions = targetEvidence.Regions!;
            // input.UnitMappings has already been validated by the genuine
            // F20 catalog validation, independently of historical candidates.
            var mappings = regions.Select(region =>
                input.UnitMappings.SingleOrDefault(mapping =>
                    mapping.RegionId == region && mapping.TargetTick == targetTick)).ToArray();
            if (mappings.Any(mapping => mapping is null || !mapping.IsComplete))
                return DirectResult("partial", "direct-unit-mapping-not-complete");
            var units = OrderedIds(mappings.SelectMany(mapping => mapping!.UnitIds));
            if (units.Length == 0)
                return DirectResult("unavailable", "direct-unit-mapping-has-no-units");
            return DirectResult("available", "direct-received-person-and-published-unit",
                regions, units, OrderedIds(mappings.Select(mapping => mapping!.SourceId)));
        }

        var directBaseline = BuildDirectBaseline();

        RescuerDirectWeftUnitAudit CompareDirectWithWeft(
            string historicalStatus, IReadOnlyList<string>? historicalRegions,
            RescuerConditionalCurrentUnitProposal proposal)
        {
            if (directBaseline.Status == "available" && proposal.Status == "available" &&
                !directBaseline.UnitIds!.SequenceEqual(proposal.UnitIds!, StringComparer.Ordinal))
                throw new InvalidDataException(
                    "Existing conditional gate unexpectedly changed direct observed units.");

            // An exact sighting can exist while the direct baseline is
            // uncertain, partial or conflicting. Never label those cases as
            // having no received exact observation (AR-27).
            var noExactSighting = directBaseline.ReasonCode ==
                "no-received-exact-target-person-observation";
            var unresolvedDirectWithHistoricalOuter =
                directBaseline.Status != "available" &&
                historicalStatus == "available" && !noExactSighting;
            string status = directBaseline.Status == "available" && proposal.Status == "available"
                ? "both-available-identical-by-design"
                : directBaseline.Status != "available" && historicalStatus == "available"
                    ? noExactSighting
                        ? "historical-outer-only"
                        : $"direct-{directBaseline.Status}-historical-outer-available"
                    : directBaseline.Status == "available"
                        ? "direct-available-conditional-withheld"
                        : "no-comparable-complete-proposal";
            var reason = status == "both-available-identical-by-design"
                ? "conditional-only-checks-membership-in-historical-outer"
                : status == "historical-outer-only"
                    ? "no-direct-exact-observation-but-published-historical-outer-remains"
                    : unresolvedDirectWithHistoricalOuter
                        ? directBaseline.ReasonCode
                        : proposal.ReasonCode;
            string[]? intersectionRegions = null;
            string[]? intersectionUnits = null;
            int? hypotheticalReduction = null;
            if (directBaseline.Status == "available" && historicalStatus == "available" &&
                historicalRegions is not null)
            {
                var overlap = OrderedIds(directBaseline.RegionIds!
                    .Intersect(historicalRegions, StringComparer.Ordinal));
                if (overlap.Length > 0 && overlap.Length < directBaseline.RegionIds!.Count)
                {
                    // Explanatory counterfactual ONLY; do not pass these values
                    // to published candidate sets, F20, or method proposals.
                    var mappings = overlap.Select(region => input.UnitMappings
                        .Single(mapping => mapping.RegionId == region &&
                            mapping.TargetTick == targetTick)).ToArray();
                    if (mappings.All(mapping => mapping.IsComplete))
                    {
                        intersectionRegions = overlap;
                        intersectionUnits = OrderedIds(mappings.SelectMany(mapping => mapping.UnitIds));
                        hypotheticalReduction =
                            directBaseline.UnitIds!.Count - intersectionUnits.Length;
                    }
                }
            }
            return new RescuerDirectWeftUnitAudit(
                status, reason,
                status == "both-available-identical-by-design" ? 0 : null,
                intersectionRegions, intersectionUnits, hypotheticalReduction);
        }

        // #134 / AR-28: inspect only receipt-gated public ancestry already
        // consumed by the historical method and exact Person observations.
        // This is a comparison label, NEVER a fifth method or a hard filter.
        // The earlier #132 hypothetical audit stays unchanged for continuity;
        // only this independently sourced assessment can be counted as a
        // provisional added contribution.
        RescuerProvisionalOverlapAssessment AssessProvisionalOverlap(
            RescuerEvidenceMethodRow historicalMethod,
            string projectionStatus,
            IReadOnlyList<RescuerCurrentUnitHistoricalRegion> historicalRegions,
            IReadOnlyList<string>? knownRegions)
        {
            var generationIds = OrderedIds(historicalMethod.GenerationDirectSourceIds);
            RescuerProvisionalOverlapAssessment Result(
                string status, string reason, string[]? regions = null,
                string[]? units = null, int? regionReduction = null,
                int? unitReduction = null, string[]? sharedAncestry = null) => new()
            {
                Status = status,
                ReasonCode = reason,
                OverlapRegionIds = regions,
                OverlapUnitIds = units,
                DirectRegionCount = directBaseline.RegionIds?.Count,
                OverlapRegionCount = regions?.Length,
                RegionReductionFromDirect = regionReduction,
                DirectUnitCount = directBaseline.UnitIds?.Count,
                OverlapUnitCount = units?.Length,
                UnitReductionFromDirect = unitReduction,
                HistoricalGenerationDirectSourceIds = generationIds,
                ObservationDirectSourceIds = OrderedIds(
                    targetObservations.Select(c => c.DirectSourceId)),
                SharedSourceAncestryIds = sharedAncestry ?? [],
                PublicTransitionSourceIds = OrderedIds(
                    historicalRegions.SelectMany(row => row.TransitionSourceIds)),
                PublicUnitMappingSourceIds = OrderedIds(
                    historicalRegions.SelectMany(row => row.UnitMappingSourceIds)),
                InspectedHistoricalSourceCount = generationIds.Length,
                InspectedTargetObservationCount = targetObservations.Length
            };

            if (directBaseline.Status == "conflict" || projectionStatus == "conflict")
                return Result("conflict", directBaseline.Status == "conflict"
                    ? directBaseline.ReasonCode
                    : "received-observation-conflicts-with-declared-outer");
            if (directBaseline.Status != "available")
                return Result("withheld", directBaseline.ReasonCode);
            if (projectionStatus != "available" || knownRegions is null)
            {
                var missing = OrderedIds(historicalRegions.SelectMany(row =>
                    row.UnresolvedReasons));
                return Result("withheld", missing.FirstOrDefault()
                    ?? "public-current-outer-not-complete");
            }
            if (generationIds.Length == 0)
                return Result("withheld", "historical-generation-has-no-received-source");

            // The generating method's direct sources must be represented by
            // received, public constraints. Source IDs alone are not evidence
            // of independent witnesses. Missing ancestry cannot certify that
            // the historical and direct observations are independent.
            var publicConstraints = (received.Input.AvailableCausalTracingConstraints?
                .PubliclyAvailableAt(comparison.QueryTick).Constraints ?? [])
                .Where(c => c.ScenarioId == received.Input.ScenarioId &&
                    c.Seed == received.Input.Seed &&
                    c.Visibility == InteractionVisibility.PublicEvidence &&
                    c.AvailableAtTick <= comparison.QueryTick &&
                    receivedIds.Contains(c.DirectSourceId))
                .ToArray();
            if (generationIds.Any(source => !publicConstraints.Any(c =>
                c.DirectSourceId == source && c.SourceAncestryIds.Count > 0)))
                return Result("withheld", "historical-generation-ancestry-unavailable");

            var historicalAncestry = OrderedIds(publicConstraints
                .Where(c => generationIds.Contains(c.DirectSourceId, StringComparer.Ordinal))
                .SelectMany(c => c.SourceAncestryIds));
            var directAncestry = OrderedIds(targetObservations
                .SelectMany(c => c.SourceAncestryIds));
            var shared = OrderedIds(historicalAncestry.Intersect(
                directAncestry, StringComparer.Ordinal));
            if (shared.Length != 0 || generationIds.Intersect(
                targetObservations.Select(c => c.DirectSourceId),
                StringComparer.Ordinal).Any())
                return Result("withheld", "shared-history-target-ancestry",
                    sharedAncestry: shared);

            var overlap = OrderedIds(directBaseline.RegionIds!.Intersect(
                knownRegions, StringComparer.Ordinal));
            if (overlap.Length == 0)
                return Result("conflict", "no-supported-direct-historical-overlap");

            // All directly observed regions have complete published unit
            // mappings; the available historical outer has complete mappings
            // as well. Re-check the specific overlap to avoid turning an
            // unknown declaration into a numerical zero.
            var overlapMappings = overlap.Select(region => input.UnitMappings
                .SingleOrDefault(mapping => mapping.RegionId == region &&
                    mapping.TargetTick == targetTick)).ToArray();
            if (overlapMappings.Any(mapping => mapping is null || !mapping.IsComplete))
                return Result("withheld", "overlap-unit-mapping-not-complete");
            var overlapUnits = OrderedIds(overlapMappings
                .SelectMany(mapping => mapping!.UnitIds));
            if (overlapUnits.Length == 0)
                return Result("withheld", "overlap-has-no-declared-unit");
            var regionDelta = directBaseline.RegionIds!.Count - overlap.Length;
            var unitDelta = directBaseline.UnitIds!.Count - overlapUnits.Length;
            if (unitDelta < 0)
                return Result("withheld", "overlap-unit-count-inconsistent");
            return Result(unitDelta > 0 ? "eligible-unit-reduction" :
                    regionDelta > 0 ? "region-only-change" : "no-region-change",
                unitDelta > 0 ? "independent-sources-and-complete-declarations" :
                    regionDelta > 0 ? "published-regions-share-current-unit" :
                    "historical-outer-adds-no-region-constraint",
                overlap, overlapUnits, regionDelta, unitDelta);
        }

        // #130: Keep the conditional proposal strictly outside all method
        // candidate and F20 calculations. The #128 received/exact-tick filter
        // and transitive ancestry groups are reused, not independently rebuilt.
        RescuerConditionalCurrentUnitProposal ConditionalProposal(
            string projectionStatus,
            IReadOnlyList<RescuerCurrentUnitHistoricalRegion> historicalRegions,
            IReadOnlyList<string>? knownRegions,
            IReadOnlyList<string>? knownUnits)
        {
            RescuerConditionalCurrentUnitProposal Result(
                string status, string reason, string[]? regions = null,
                string[]? units = null, int? reduction = null,
                string[]? mappingSources = null) => new(
                    status, reason, regions, units, reduction,
                    OrderedIds(targetObservations.Select(c => c.ConstraintId)),
                    OrderedIds(targetObservations.Select(c => c.DirectSourceId)),
                    OrderedIds(targetObservations.SelectMany(c => c.SourceAncestryIds)),
                    OrderedIds(historicalRegions.SelectMany(h => h.TransitionSourceIds)),
                    mappingSources ?? [],
                    targetObservations.Length, observationSourceGroups.Length);

            // Even a perfectly confirmed observation cannot close an
            // incomplete published outer. A previously detected conflict is
            // retained, not reinterpreted as an empty successful reduction.
            if (projectionStatus != "available" || knownRegions is null ||
                knownUnits is null)
            {
                return Result(projectionStatus,
                    projectionStatus == "conflict"
                        ? "received-observation-conflicts-with-declared-outer"
                        : "public-current-outer-not-complete");
            }
            if (targetEvidence.Status != "available")
                return Result(targetEvidence.Status, targetEvidence.Reason);

            // Identical received evidence/ancestry eligibility to direct A;
            // historical knowledge is used only as the original B gate.
            var conditionalSet = targetEvidence.Regions!.ToHashSet(StringComparer.Ordinal);

            var publishedSet = knownRegions.ToHashSet(StringComparer.Ordinal);
            if (conditionalSet.Any(id => !publishedSet.Contains(id)))
            {
                return Result("partial",
                    "observation-has-alternatives-outside-declared-outer");
            }
            var selectedRegions = OrderedIds(conditionalSet);
            var selectedMappings = selectedRegions.Select(region =>
                input.UnitMappings.SingleOrDefault(mapping =>
                    mapping.RegionId == region && mapping.TargetTick == targetTick)).ToArray();
            if (selectedMappings.Any(mapping =>
                mapping is null || !mapping.IsComplete))
            {
                return Result("partial", "selected-region-unit-mapping-not-complete");
            }
            var selectedUnits = OrderedIds(selectedMappings.SelectMany(mapping =>
                mapping!.UnitIds));
            if (selectedUnits.Length == 0 ||
                selectedUnits.Except(knownUnits, StringComparer.Ordinal).Any())
            {
                return Result("unavailable", "conditional-units-not-supported-by-public-outer");
            }
            return Result("available", "conditional-on-received-person-and-public-model",
                selectedRegions, selectedUnits, knownUnits.Count - selectedUnits.Length,
                OrderedIds(selectedMappings.Select(mapping => mapping!.SourceId)));
        }

        var rows = comparison.Methods.Select(method =>
        {
            var historicalRegions = method.OuterCandidateRegionIds.Select(region =>
            {
                // These keys and reason codes deliberately match F20's
                // published-declaration semantics, without candidate metadata.
                var transition = input.RegionProjections.SingleOrDefault(row =>
                    row.HistoricalRegionId == region &&
                    row.StartTick == historicalWindow.StartTick &&
                    row.EndTick == historicalWindow.EndTick &&
                    row.TargetTick == targetTick);
                string[] current = transition is null
                    ? [] : OrderedIds(transition.CurrentRegionIds);
                var reasons = new List<string>();
                if (transition is null)
                    reasons.Add("transition-unmapped");
                else if (!transition.IsComplete)
                    reasons.Add("transition-incomplete");

                var knownUnits = new List<string>();
                var unitSources = new List<string>();
                foreach (var currentRegion in current)
                {
                    var mapping = input.UnitMappings.SingleOrDefault(row =>
                        row.RegionId == currentRegion && row.TargetTick == targetTick);
                    if (mapping is null)
                    {
                        reasons.Add("unit-mapping-unmapped");
                        continue;
                    }

                    knownUnits.AddRange(mapping.UnitIds);
                    unitSources.Add(mapping.SourceId);
                    if (!mapping.IsComplete)
                        reasons.Add("unit-mapping-incomplete");
                }

                return new RescuerCurrentUnitHistoricalRegion(
                    region, current, OrderedIds(knownUnits), OrderedIds(reasons),
                    transition is null ? [] : [transition.SourceId],
                    OrderedIds(unitSources));
            }).ToArray();

            if (historicalRegions.Length == 0)
                return new RescuerCurrentUnitMethod(method, "unavailable", historicalRegions,
                    null, null, null)
                {
                    ConditionalTargetUnitProposal = ConditionalProposal(
                        "unavailable", historicalRegions, null, null),
                    DirectVsWeftAudit = CompareDirectWithWeft("unavailable", null,
                        ConditionalProposal("unavailable", historicalRegions, null, null)),
                    ProvisionalOverlapAssessment = AssessProvisionalOverlap(
                        method, "unavailable", historicalRegions, null)
                };

            var knownRegions = OrderedIds(historicalRegions.SelectMany(row =>
                row.KnownCurrentRegionIds));
            var units = OrderedIds(historicalRegions.SelectMany(row => row.KnownUnitIds));
            var unresolved = OrderedIds(historicalRegions.Where(row =>
                row.UnresolvedReasons.Count != 0).Select(row => row.HistoricalRegionId));
            var status = units.Length == 0 ? "unavailable"
                : unresolved.Length != 0 ? "partial" : "available";

            // Only a fully declared region outer can contradict an observation.
            // A partial/unmapped outer is unknown outside the published subset,
            // not a closed-world assertion. A multi-region observation conflicts
            // only if NONE of its allowed regions overlaps the complete outer.
            // Do not turn ambiguous or mutually alternative observations
            // of a shared original into a definite, independent contradiction.
            var knownSet = knownRegions.ToHashSet(StringComparer.Ordinal);
            var contradictions = status == "available"
                ? observationSourceGroups
                    .Where(group => group.All(index =>
                    {
                        var c = targetObservations[index];
                        return c.EvidenceBoundaryState == EvidenceState.Confirmed &&
                            c.SourceAncestryIds.Count > 0 &&
                            !string.IsNullOrWhiteSpace(c.DirectSourceId) &&
                            !string.IsNullOrWhiteSpace(c.SourceObservationId) &&
                            c.AllowedRegionIds.All(id => !knownSet.Contains(id));
                    }))
                    .Select(group => targetObservations[group.Min()])
                    .Select(c => new RescuerCurrentUnitTargetObservationConflict(
                        "confirmed-target-observation-outside-complete-declaration",
                        c.ConstraintId, c.DirectSourceId, c.SourceObservationId,
                        OrderedIds(c.SourceAncestryIds),
                        c.ConstrainedTimeWindow, OrderedIds(c.AllowedRegionIds)))
                    .ToArray()
                : [];
            if (contradictions.Length > 0)
                status = "conflict";
            var proposal = ConditionalProposal(
                status, historicalRegions, knownRegions, units);
            return new RescuerCurrentUnitMethod(method, status, historicalRegions,
                status == "unavailable" ? null : knownRegions,
                status == "unavailable" ? null : units, unresolved)
            {
                TargetTickConflicts = contradictions,
                ConditionalTargetUnitProposal = proposal,
                DirectVsWeftAudit = CompareDirectWithWeft(status, knownRegions, proposal),
                ProvisionalOverlapAssessment = AssessProvisionalOverlap(
                    method, status, historicalRegions, knownRegions)
            };
        }).ToArray();

        // A source-free outer projection may coincide with F20's union of
        // declared units, but must not claim F20's candidate-level support.
        // Compare against the genuine F20 output for M1-E, including unknown.
        var m1e = rows.Single(row =>
            row.HistoricalMethod.Method == "distributed-reconstruction");
        if (m1e.HistoricalRegions.Count != realF20.Candidates.Count)
        {
            throw new InvalidDataException("M1-E current-unit comparison is not F20-equivalent.");
        }

        foreach (var actualCandidate in realF20.Candidates)
        {
            var declared = m1e.HistoricalRegions.Single(row =>
                row.HistoricalRegionId == actualCandidate.SourceCandidate.RegionId);
            if (!declared.KnownCurrentRegionIds.SequenceEqual(
                    actualCandidate.KnownCurrentRegionIds, StringComparer.Ordinal) ||
                !declared.KnownUnitIds.SequenceEqual(
                    actualCandidate.KnownUnitIds, StringComparer.Ordinal) ||
                !declared.UnresolvedReasons.SequenceEqual(
                    actualCandidate.UnresolvedReasons, StringComparer.Ordinal) ||
                !declared.TransitionSourceIds.SequenceEqual(
                    actualCandidate.TransitionSourceIds, StringComparer.Ordinal) ||
                !declared.UnitMappingSourceIds.SequenceEqual(
                    actualCandidate.UnitMappingSourceIds, StringComparer.Ordinal))
            {
                throw new InvalidDataException(
                    "Published outer projection disagrees with genuine M1-E F20.");
            }
        }

        var receipts = received.ReceivedRecords
            .OrderBy(record => record.OriginalRecordId, StringComparer.Ordinal)
            .Select(record => new RescuerCurrentUnitReceivedOriginal(
                record.OriginalRecordId, record.OriginalObservationId,
                record.FirstReceivedAtTick)).ToArray();
        return new RescuerCurrentUnitMethodComparison(
            comparison.QueryTick, targetTick, historicalWindow, receipts, rows, realF20)
        {
            DirectTargetUnitBaseline = directBaseline
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
    /// #112: compare the SAME received evidence at tick 7 / historical tick 2,
    /// with exactly one separately published B->D public 3-tick declaration.
    /// The public declaration is an information difference, not a fourth
    /// inference method, a verified Person movement, or a unit saving.
    /// </summary>
    public static RescuerRelationQuestionUtilityReport AnalyzeRelationQuestionUtility(
        RescuerEvidenceInput withoutDeclaration,
        RescuerEvidenceInput withDeclaration,
        InteractionTimeWindow historicalWindow,
        IReadOnlyList<string> regionIds,
        IReadOnlyList<string> unitIds,
        IReadOnlyList<PublicSearchUnitMapping> unitMappings)
    {
        ArgumentNullException.ThrowIfNull(withoutDeclaration);
        ArgumentNullException.ThrowIfNull(withDeclaration);
        ArgumentNullException.ThrowIfNull(historicalWindow);
        ArgumentNullException.ThrowIfNull(regionIds);
        ArgumentNullException.ThrowIfNull(unitIds);
        ArgumentNullException.ThrowIfNull(unitMappings);
        var u = withoutDeclaration.Input;
        var k = withDeclaration.Input;
        var cutoff = u.CurrentTick;
        if (cutoff != 7 || k.CurrentTick != cutoff ||
            historicalWindow.StartTick != 2 || historicalWindow.EndTick != 2)
        {
            throw new InvalidDataException("The fixed #112 contrast requires historical tick 2 and common query tick 7.");
        }

        var uPublic = u.PublicContext;
        var kPublic = k.PublicContext;
        var uEnvelope = uPublic.PublicHazardEnvelope
            ?? throw new InvalidDataException("Both inputs require a declared public envelope.");
        var kEnvelope = kPublic.PublicHazardEnvelope
            ?? throw new InvalidDataException("Both inputs require a declared public envelope.");
        var uEdges = uEnvelope.Branches;
        var kEdges = kEnvelope.Branches;
        if (kEdges.Count != uEdges.Count + 1)
        {
            throw new InvalidDataException("Exactly one additional public declaration is required.");
        }
        var added = kEdges[^1];
        if (added.SourceRegionId != "B" || added.DestinationRegionId != "D" ||
            added.MinimumTravelTicks != 3 || added.MaximumTravelTicks != 3 ||
            added.PublishedAtTick != 7 ||
            uEdges.Any(edge => edge.SourceRegionId == "B" &&
                edge.DestinationRegionId == "D"))
        {
            throw new InvalidDataException("Only the separately published B-to-D 3-tick relation is allowed.");
        }
        // Compare the actual complete PUBLIC contexts, not merely a label,
        // after removing the single appended K branch. This rejects extra
        // state, availability, transition or public-model changes. Both
        // inputs must also carry byte-equivalent receipt-projected evidence.
        var normalizedK = kPublic with
        {
            PublicHazardEnvelope = kEnvelope with
            {
                Branches = kEdges.Take(kEdges.Count - 1).ToArray()
            }
        };
        if (JsonDefaults.Serialize(uPublic) != JsonDefaults.Serialize(normalizedK) ||
            u.ScenarioId != k.ScenarioId || u.Seed != k.Seed ||
            JsonDefaults.Serialize(withoutDeclaration.ReceivedRecords) !=
                JsonDefaults.Serialize(withDeclaration.ReceivedRecords) ||
            JsonDefaults.Serialize(u.AvailableEvidence) !=
                JsonDefaults.Serialize(k.AvailableEvidence) ||
            JsonDefaults.Serialize(u.AvailableCausalTracingConstraints) !=
                JsonDefaults.Serialize(k.AvailableCausalTracingConstraints) ||
            JsonDefaults.Serialize(u.AvailableCrossThreadConstraints) !=
                JsonDefaults.Serialize(k.AvailableCrossThreadConstraints) ||
            JsonDefaults.Serialize(u.PublicSearchDomainRegionIds) !=
                JsonDefaults.Serialize(k.PublicSearchDomainRegionIds) ||
            withoutDeclaration.RescuerId != withDeclaration.RescuerId)
        {
            throw new InvalidDataException("The public context or receipt-projected originals differ beyond B-to-D.");
        }
        var ids = withoutDeclaration.ReceivedRecords
            .Select(item => item.OriginalRecordId)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(id => id, StringComparer.Ordinal).ToArray();
        if (ids.Length != 2 || withoutDeclaration.ReceivedRecords.Count != 2)
        {
            return new RescuerRelationQuestionUtilityReport
            {
                Status = "unavailable",
                HoldReason = "two-distinct-received-originals-not-yet-eligible",
                QueryTick = cutoff,
                HistoricalTick = historicalWindow.EndTick,
                ReceivedOriginalIds = ids
            };
        }
        var questionsU = AnalyzeMissingDeclaredPathQuestions(withoutDeclaration, historicalWindow);
        var questionsK = AnalyzeMissingDeclaredPathQuestions(withDeclaration, historicalWindow);
        if (questionsU.Status != "available" || questionsK.Status != "available")
        {
            return new RescuerRelationQuestionUtilityReport
            {
                Status = "unavailable",
                HoldReason = "public-question-diagnostic-unavailable",
                QueryTick = cutoff,
                HistoricalTick = historicalWindow.EndTick,
                ReceivedOriginalIds = ids
            };
        }
        static string[] Sorted(IEnumerable<string> source) =>
            source.Distinct(StringComparer.Ordinal)
                .OrderBy(id => id, StringComparer.Ordinal).ToArray();
        static RescuerMissingDeclaredPathQuestion[] AllQuestions(
            RescuerMissingDeclaredPathReport report) =>
            report.Candidates.SelectMany(candidate => candidate.Questions)
                .OrderBy(question => question.QuestionId, StringComparer.Ordinal).ToArray();

        var qU = AllQuestions(questionsU);
        var qK = AllQuestions(questionsK);
        var qUIds = Sorted(qU.Select(question => question.QuestionId));
        var qKIds = Sorted(qK.Select(question => question.QuestionId));
        var methodU = Compare(withoutDeclaration, historicalWindow);
        var methodK = Compare(withDeclaration, historicalWindow);
        RescuerEvidenceMethodReport AsHistoricalReport(RescuerEvidenceMethodQueryRow row) =>
            new()
            {
                ScenarioId = u.ScenarioId,
                Seed = u.Seed,
                RescuerId = "fixed-receipt-scoped-comparison",
                TargetWindow = historicalWindow,
                ActualTimeline = [row]
            };
        // The same supplied public unit catalog is applied separately to
        // each condition. Ineligible projections never become zero savings.
        var unitsU = AnalyzeHistoricalSearchUnits(AsHistoricalReport(methodU),
            regionIds, unitIds, unitMappings).ActualTimeline.Single();
        var unitsK = AnalyzeHistoricalSearchUnits(AsHistoricalReport(methodK),
            regionIds, unitIds, unitMappings).ActualTimeline.Single();
        var names = new[]
        {
            "public-prior", "known-relation-backward",
            "distributed-reconstruction", "distributed-reconstruction-local"
        };
        var methods = names.Select(name =>
        {
            var before = methodU.Methods.Single(row => row.Method == name);
            var after = methodK.Methods.Single(row => row.Method == name);
            var beforeUnits = unitsU.Methods.Single(row => row.Method == name);
            var afterUnits = unitsK.Methods.Single(row => row.Method == name);
            var eligible = beforeUnits.ComparisonEligible && afterUnits.ComparisonEligible;
            return new RescuerRelationQuestionUtilityMethod(
                name,
                eligible ? "available" :
                    beforeUnits.Status == "empty-candidates" ||
                        afterUnits.Status == "empty-candidates"
                        ? "empty-candidates" : "partial",
                before,
                after,
                beforeUnits,
                afterUnits,
                Sorted(before.OuterCandidateRegionIds.Except(
                    after.OuterCandidateRegionIds, StringComparer.Ordinal)),
                Sorted(after.OuterCandidateRegionIds.Except(
                    before.OuterCandidateRegionIds, StringComparer.Ordinal)),
                eligible ? Sorted(beforeUnits.KnownUnitIds.Except(
                    afterUnits.KnownUnitIds, StringComparer.Ordinal)) : null,
                eligible ? Sorted(afterUnits.KnownUnitIds.Except(
                    beforeUnits.KnownUnitIds, StringComparer.Ordinal)) : null);
        }).ToArray();
        return new RescuerRelationQuestionUtilityReport
        {
            Status = methods.All(row => row.UnitDifferenceStatus == "available")
                ? "available" : "partial",
            HoldReason = methods.All(row => row.UnitDifferenceStatus == "available")
                ? null : "incomplete-or-empty-public-unit-projection",
            QueryTick = cutoff,
            HistoricalTick = historicalWindow.EndTick,
            ReceivedOriginalIds = ids,
            QuestionsWithoutDeclaration = qU,
            QuestionsWithDeclaration = qK,
            ResolvedQuestionIds = Sorted(qUIds.Except(qKIds, StringComparer.Ordinal)),
            NewlyUnconfirmedQuestionIds = Sorted(qKIds.Except(qUIds, StringComparer.Ordinal)),
            Methods = methods
        };
    }


    /// <summary>
    /// #114: join formal declared-path witnesses to the SAME tick's public
    /// historical search units for each method's actual outer candidates.
    /// This is a receipt-gated, evaluation-only classification. It neither
    /// excludes unknown candidates nor claims any whole unit is witnessed.
    /// The #112 identical-world/receipt gate runs before either diagnostic.
    /// </summary>
    public static RescuerDeclaredWitnessUnitReport AnalyzeDeclaredWitnessUnitCoverage(
        RescuerEvidenceInput withoutDeclaration,
        RescuerEvidenceInput withDeclaration,
        InteractionTimeWindow historicalWindow,
        IReadOnlyList<string> regionIds,
        IReadOnlyList<string> unitIds,
        IReadOnlyList<PublicSearchUnitMapping> unitMappings)
    {
        var comparison = AnalyzeRelationQuestionUtility(
            withoutDeclaration, withDeclaration, historicalWindow,
            regionIds, unitIds, unitMappings);
        if (comparison.Methods.Count == 0)
        {
            return new RescuerDeclaredWitnessUnitReport(
                "unavailable", comparison.HoldReason, comparison.QueryTick,
                comparison.HistoricalTick, comparison.ReceivedOriginalIds, []);
        }

        var diagnosticU = AnalyzeDeclaredPathCompatibility(
            withoutDeclaration, historicalWindow);
        var diagnosticK = AnalyzeDeclaredPathCompatibility(
            withDeclaration, historicalWindow);
        if (diagnosticU.QueryTick != comparison.QueryTick ||
            diagnosticK.QueryTick != comparison.QueryTick ||
            !diagnosticU.ReceivedOriginalIds.SequenceEqual(
                comparison.ReceivedOriginalIds, StringComparer.Ordinal) ||
            !diagnosticK.ReceivedOriginalIds.SequenceEqual(
                comparison.ReceivedOriginalIds, StringComparer.Ordinal))
        {
            throw new InvalidDataException(
                "Declared-path diagnostics do not match the receipt-gated comparison.");
        }

        // #118/#122: Keep #104's witness/status decision intact. For unknown
        // candidates, reconcile the old direct-pair labels with #106's same
        // receipt-gated public alternatives; neither implies Person travel nor
        // changes safe outer candidates or public units.
        static Dictionary<string, string[]> UnwitnessedReasons(
            RescuerEvidenceInput received,
            RescuerDeclaredPathCompatibilityReport diagnostic)
        {
            var input = received.Input;
            var cutoff = diagnostic.QueryTick;
            // #106 already has the bounded public route/stay/timing analysis;
            // never implement a second path search inside the unit diagnostic.
            var missing = AnalyzeMissingDeclaredPathQuestions(
                received, diagnostic.TargetWindow);
            if (missing.QueryTick != cutoff ||
                missing.TargetWindow != diagnostic.TargetWindow ||
                missing.Status != diagnostic.Status ||
                !missing.ReceivedOriginalIds.SequenceEqual(
                    diagnostic.ReceivedOriginalIds, StringComparer.Ordinal) ||
                missing.Candidates.Count != diagnostic.Candidates.Count ||
                missing.Candidates.Where((c, i) =>
                    c.RegionId != diagnostic.Candidates[i].RegionId ||
                    c.DeclaredPathStatus != diagnostic.Candidates[i].Status).Any())
            {
                throw new InvalidDataException(
                    "Public alternatives do not match the same receipt/cutoff and declared-path candidates.");
            }
            var alternatives = missing.Candidates.ToDictionary(
                candidate => candidate.RegionId, StringComparer.Ordinal);
            var originals = received.ReceivedRecords.Select(record =>
                record.OriginalRecordId).ToHashSet(StringComparer.Ordinal);
            var constraints = (input.AvailableCausalTracingConstraints?
                .PubliclyAvailableAt(cutoff).Constraints ?? [])
                .Where(c => c.ScenarioId == input.ScenarioId && c.Seed == input.Seed &&
                    c.TargetEntityId == input.PublicContext.TargetPersonId &&
                    c.Visibility == InteractionVisibility.PublicEvidence &&
                    c.AvailableAtTick <= cutoff && originals.Contains(c.DirectSourceId) &&
                    c.IsPersonEvidence && !c.IsDeviceOnlyEvidence)
                .OrderBy(c => c.ConstraintId, StringComparer.Ordinal).ToArray();
            var envelope = input.PublicContext.PublicHazardEnvelope;
            var publishedPairs = envelope is not null && envelope.PublishedAtTick <= cutoff
                ? envelope.Branches.Where(branch => branch.PublishedAtTick <= cutoff &&
                        branch.MinimumTravelTicks >= 0 &&
                        branch.MaximumTravelTicks >= branch.MinimumTravelTicks)
                    .Select(branch => (branch.SourceRegionId, branch.DestinationRegionId))
                    .ToHashSet()
                : new HashSet<(string SourceRegionId, string DestinationRegionId)>();
            var earlyAtWindow = constraints.Where(c =>
                c.ConstrainedTimeWindow.StartTick <= diagnostic.TargetWindow.EndTick &&
                diagnostic.TargetWindow.StartTick <= c.ConstrainedTimeWindow.EndTick)
                .ToArray();
            var confirmedEarly = earlyAtWindow.Where(c =>
                c.EvidenceBoundaryState == EvidenceState.Confirmed).ToArray();
            var reasons = new Dictionary<string, string[]>(StringComparer.Ordinal);
            foreach (var candidate in diagnostic.Candidates)
            {
                // Conflict, unavailable and witnessed already have their own
                // semantics. Never add a negative claim to those states.
                if (candidate.Status != "unknown")
                {
                    reasons.Add(candidate.RegionId, []);
                    continue;
                }
                var flags = new SortedSet<string>(StringComparer.Ordinal);
                var supportingEarly = confirmedEarly.Where(c =>
                    c.AllowedRegionIds.Contains(candidate.RegionId, StringComparer.Ordinal))
                    .ToArray();
                // Only classify non-support if an actual confirmed early Person
                // constraint is available AND no uncertain/conflicting early
                // constraint could support the candidate.
                if (confirmedEarly.Length > 0 && supportingEarly.Length == 0 &&
                    earlyAtWindow.All(c =>
                        c.EvidenceBoundaryState == EvidenceState.Confirmed))
                {
                    flags.Add("early-person-not-supported");
                }
                foreach (var early in supportingEarly)
                {
                    foreach (var later in constraints.Where(c =>
                        c.EvidenceBoundaryState == EvidenceState.Confirmed &&
                        c.DirectSourceId != early.DirectSourceId &&
                        c.ConstrainedTimeWindow.EndTick > diagnostic.TargetWindow.EndTick))
                    {
                        foreach (var destination in later.AllowedRegionIds)
                        {
                            // No new declared edge is required to ask whether
                            // a Person remained in the same region.
                            if (destination == candidate.RegionId)
                                continue;
                            if (publishedPairs.Contains((candidate.RegionId, destination)))
                            {
                                // #104 has already found no compatible witness:
                                // this published endpoint pair has no formal
                                // time-window match, not proven impassability.
                                flags.Add("published-pair-timing-incompatible");
                            }
                            else
                            {
                                flags.Add("endpoint-pair-unpublished");
                            }
                        }
                    }
                }
                // Alternative explanations are formal PUBLIC possibilities,
                // not new received observations, witness status or proof that
                // an unpublished relation is absent. A single candidate can
                // also have a separate, still-unanswered destination (ST2).
                var publicCandidate = alternatives[candidate.RegionId];
                var formalAlternative = false;
                foreach (var explanation in publicCandidate.PublicAlternativeExplanations)
                {
                    if (explanation.StartsWith("formal-stay:", StringComparison.Ordinal))
                    {
                        formalAlternative = true;
                        flags.Add("formal-stay-public-alternative");
                    }
                    else if (explanation.StartsWith("formal-two-hop:", StringComparison.Ordinal))
                    {
                        formalAlternative = true;
                        flags.Add("formal-two-hop-public-alternative");
                    }
                    else if (explanation.StartsWith("formal-multihop:", StringComparison.Ordinal))
                    {
                        formalAlternative = true;
                        flags.Add("formal-multihop-public-alternative");
                    }
                    else if (explanation.StartsWith(
                        "direct-pair-published-shorter-dwell-unverified:",
                        StringComparison.Ordinal))
                        flags.Add("published-pair-shorter-dwell-unverified");
                    else if (explanation.StartsWith(
                        "direct-pair-published-too-long:", StringComparison.Ordinal))
                        flags.Add("published-pair-too-long");
                    else if (explanation.StartsWith(
                        "published-path-shorter-dwell-unverified:",
                        StringComparison.Ordinal))
                        flags.Add("published-path-shorter-dwell-unverified");
                    else if (explanation.StartsWith(
                        "published-path-too-long:", StringComparison.Ordinal))
                        flags.Add("published-path-too-long");
                    else if (explanation.StartsWith(
                        "public-path-search-incomplete:", StringComparison.Ordinal))
                        flags.Add("public-path-search-incomplete");
                }
                // #106 suppressed *all* questions only when a formal public
                // alternative explains each applicable endpoint pair. For ST2
                // its remaining D question preserves endpoint-pair-unpublished.
                if (formalAlternative && publicCandidate.Questions.Count == 0)
                    flags.Remove("endpoint-pair-unpublished");
                if (flags.Count == 0)
                    flags.Add("insufficient-public-evidence");
                reasons.Add(candidate.RegionId, flags.ToArray());
            }
            return reasons;
        }

        var unwitnessedU = UnwitnessedReasons(withoutDeclaration, diagnosticU);
        var unwitnessedK = UnwitnessedReasons(withDeclaration, diagnosticK);

        static string[] Sorted(IEnumerable<string> values) =>
            values.Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal).ToArray();

        // AnalyzeHistoricalSearchUnits has already validated the public
        // catalog, historical tick, target identifiers and duplicate mapping.
        // We reuse its projection for every method instead of inventing a
        // second unit-completeness or empty-candidates rule.
        var publicRegions = regionIds.ToHashSet(StringComparer.Ordinal);
        var mappings = unitMappings.ToDictionary(
            mapping => mapping.RegionId, StringComparer.Ordinal);
        RescuerDeclaredWitnessUnitCondition Group(
            string label,
            RescuerEvidenceMethodRow method,
            RescuerEvidenceHistoricalUnitMethod projection,
            RescuerDeclaredPathCompatibilityReport diagnostic,
            IReadOnlyDictionary<string, string[]> unwitnessed)
        {
            var empty = Array.Empty<RescuerDeclaredWitnessUnit>();
            var noUnresolved = Array.Empty<RescuerDeclaredWitnessUnresolvedRegion>();
            if (projection.Status == "unavailable" ||
                projection.CandidateRegionIds is null ||
                method.OuterCandidateRegionIds is null ||
                diagnostic.Status != "available")
            {
                return new RescuerDeclaredWitnessUnitCondition(
                    label, "unavailable", "public-method-or-diagnostic-unavailable",
                    method, projection, empty, noUnresolved,
                    diagnostic.PublicConstraintsInspected,
                    diagnostic.PublicBranchesInspected,
                    diagnostic.ConstraintBranchPairsInspected);
            }

            var candidates = Sorted(method.OuterCandidateRegionIds);
            if (!candidates.SequenceEqual(
                    Sorted(projection.CandidateRegionIds), StringComparer.Ordinal))
            {
                return new RescuerDeclaredWitnessUnitCondition(
                    label, "unavailable", "method-and-unit-candidates-mismatch",
                    method, projection, empty, noUnresolved,
                    diagnostic.PublicConstraintsInspected,
                    diagnostic.PublicBranchesInspected,
                    diagnostic.ConstraintBranchPairsInspected);
            }
            var diagnosticByRegion = diagnostic.Candidates.ToDictionary(
                candidate => candidate.RegionId, StringComparer.Ordinal);
            if (candidates.Any(id => !diagnosticByRegion.ContainsKey(id)))
            {
                return new RescuerDeclaredWitnessUnitCondition(
                    label, "unavailable", "method-and-declared-path-candidates-mismatch",
                    method, projection, empty, noUnresolved,
                    diagnostic.PublicConstraintsInspected,
                    diagnostic.PublicBranchesInspected,
                    diagnostic.ConstraintBranchPairsInspected);
            }

            var grouped = new SortedDictionary<string,
                List<RescuerDeclaredWitnessUnitRegion>>(StringComparer.Ordinal);
            var unresolved = new List<RescuerDeclaredWitnessUnresolvedRegion>();
            foreach (var region in candidates)
            {
                if (!publicRegions.Contains(region))
                {
                    unresolved.Add(new(region, "outside-public-region-catalog"));
                    continue;
                }
                if (!mappings.TryGetValue(region, out var mapping))
                {
                    unresolved.Add(new(region, "missing-public-mapping"));
                    continue;
                }
                if (mapping.AvailableTick > comparison.QueryTick)
                {
                    unresolved.Add(new(region, "future-public-mapping"));
                    continue;
                }
                if (!mapping.IsComplete)
                {
                    unresolved.Add(new(region, "incomplete-public-mapping"));
                }
                var diagnosticRegion = diagnosticByRegion[region];
                // The positive evidence remains attached to its actual
                // candidate, even for many-to-one and one-to-many mappings.
                var entry = new RescuerDeclaredWitnessUnitRegion(
                    region, diagnosticRegion.Status, diagnosticRegion.Witnesses,
                    unwitnessed[region]);
                foreach (var unit in Sorted(mapping.UnitIds))
                {
                    if (!grouped.TryGetValue(unit, out var members))
                    {
                        members = [];
                        grouped.Add(unit, members);
                    }
                    members.Add(entry);
                }
            }
            var units = grouped.Select(item =>
            {
                var members = item.Value.OrderBy(
                    member => member.RegionId, StringComparer.Ordinal).ToArray();
                string[] InState(string status) => Sorted(
                    members.Where(member => member.CompatibilityStatus == status)
                        .Select(member => member.RegionId));
                return new RescuerDeclaredWitnessUnit(
                    item.Key, members, InState("witnessed"), InState("unknown"),
                    InState("conflict"), InState("unavailable"));
            }).ToArray();
            if (!Sorted(units.Select(unit => unit.UnitId))
                    .SequenceEqual(projection.KnownUnitIds, StringComparer.Ordinal) ||
                !Sorted(unresolved.Select(item => item.RegionId))
                    .SequenceEqual(projection.UnresolvedRegionIds,
                        StringComparer.Ordinal))
            {
                return new RescuerDeclaredWitnessUnitCondition(
                    label, "unavailable", "public-unit-projection-inconsistent",
                    method, projection, empty, noUnresolved,
                    diagnostic.PublicConstraintsInspected,
                    diagnostic.PublicBranchesInspected,
                    diagnostic.ConstraintBranchPairsInspected);
            }
            var status = projection.Status;
            return new RescuerDeclaredWitnessUnitCondition(
                label, status,
                status == "available" ? null :
                    status == "empty-candidates" ? "no-outer-candidates" :
                    "incomplete-or-unavailable-public-mapping",
                method, projection, units,
                unresolved.OrderBy(item => item.RegionId, StringComparer.Ordinal)
                    .ToArray(),
                diagnostic.PublicConstraintsInspected,
                diagnostic.PublicBranchesInspected,
                diagnostic.ConstraintBranchPairsInspected);
        }

        var methods = comparison.Methods.Select(row =>
            new RescuerDeclaredWitnessUnitMethod(
                row.Method,
                Group("U", row.WithoutDeclaration,
                    row.UnitsWithoutDeclaration, diagnosticU, unwitnessedU),
                Group("K", row.WithDeclaration,
                    row.UnitsWithDeclaration, diagnosticK, unwitnessedK))).ToArray();
        var statuses = methods.SelectMany(method => new[]
            { method.WithoutDeclaration.Status, method.WithDeclaration.Status }).ToArray();
        var overallStatus = statuses.All(status => status == "available")
            ? "available" : statuses.All(status => status == "unavailable")
                ? "unavailable" : "partial";
        return new RescuerDeclaredWitnessUnitReport(
            overallStatus,
            overallStatus == "available" ? null :
                "some-unit-projections-or-witness-joins-are-unavailable",
            comparison.QueryTick, comparison.HistoricalTick,
            comparison.ReceivedOriginalIds, methods);
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
