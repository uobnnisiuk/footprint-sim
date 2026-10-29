using FootprintSim.Inference.Derivation;

namespace FootprintSim.Evaluation;

public sealed record CandidateDensityProtocol
{
    public required int SchemaVersion { get; init; }
    public required string Id { get; init; }
    public required string BaselineFinalCommit { get; init; }
    public required string BaselineExperimentSha256 { get; init; }
    public required int RequiredSeedStart { get; init; }
    public required int RequiredSeedEnd { get; init; }
    public required string BaselineRepresentation { get; init; }
    public required IReadOnlyList<string> Arms { get; init; }
    public required string SupportRuleId { get; init; }
    public required string OrderingRuleId { get; init; }
    public required IReadOnlyList<int> AdditionNumerators { get; init; }
    public required int AdditionDenominator { get; init; }
    public required string Rounding { get; init; }
    public required int AddedWidthTicks { get; init; }
    public required string TruthRule { get; init; }
    public required string PlateauRuleId { get; init; }
    public required string FixedInference { get; init; }
    public required bool ProspectiveOutcomeInspected { get; init; }
    public required string FailurePolicy { get; init; }
}

public sealed record CandidateDensityRequest
{
    public required CandidateDensityProtocol Protocol { get; init; }
    public required string ProtocolContentSha256 { get; init; }
    public required CommonWorldFieldCombinationExperimentDefinition BaselineExperiment { get; init; }
    public required string BaselineExperimentContentSha256 { get; init; }
    public IReadOnlyList<int> Seeds { get; init; } = [];
    public required string SourceCommitSha { get; init; }
    public required bool DirtyWorkingTree { get; init; }
}

/// <summary>Public model support only; no observations, constraints, profiles, or truth.</summary>
public sealed record CandidateDensityPublicSupportRequest
{
    public required int Seed { get; init; }
    public required int BaseTick { get; init; }
    public IReadOnlyList<string> RegisteredRegionIds { get; init; } = [];
    public IReadOnlyList<CandidateIntervalWidthPublicCandidate> BaselinePointCandidates { get; init; } = [];
}

public sealed record CandidateDensityCell(long StartTick, int Offset, string RegionId, int RegionIndex,
    string OrderHash, string CandidateKey, string PublicSourceIdentifier);
public sealed record CandidateDensityTransformArm(string ArmId, int AddedCandidateCount,
    IReadOnlyList<CandidateIntervalWidthPublicCandidate> Candidates);
public sealed record CandidateDensitySupport(int Seed, int BaseTick, IReadOnlyList<string> RegisteredRegionIds,
    int SupportCellCount, string SupportContentSignature, IReadOnlyList<CandidateDensityCell> RankedMissingCells,
    IReadOnlyList<CandidateDensityTransformArm> Arms);
public sealed record CandidateDensityHistogramEntry(string Value, int Count);
public sealed record CandidateDensityOccupancy(string ArmId, int CandidateCount, int AddedCandidateCount,
    int SupportCellCount, decimal Density, int OccupiedRegionCount, int OccupiedTickCount,
    int UnusedRegionCount, decimal UnusedRegionFraction,
    IReadOnlyList<CandidateDensityHistogramEntry> RegionHistogram,
    IReadOnlyList<CandidateDensityHistogramEntry> StartTickHistogram,
    IReadOnlyList<CandidateDensityHistogramEntry> WidthHistogram);
public sealed record CandidateDensityCrosswalk(string CandidateKey, string IntroductionArmId,
    CandidateIntervalWidthNativeCandidate Candidate, IReadOnlyList<string> PresentArmIds,
    string? OriginalIntervalCandidateId);
public sealed record CandidateDensityTruthAddress(string ArmId, string CandidateId);
public sealed record CandidateDensityFixedTruth(string OriginalTruthWorldId, string OriginalTruthCandidateKey,
    long OriginalTruthStartTick, long OriginalTruthEndTick, string OriginalTruthRegionId,
    string OriginalTruthParticipantId, long OriginalEncounterTick, long OriginalRealizedDurationTicks,
    string OriginalIntervalCandidateId, IReadOnlyList<CandidateDensityTruthAddress> ArmAddresses,
    bool ResolvedAfterAllPublicArms);

public sealed record CandidateDensityProfileMetric(string ArmId, string ProfileId, int CandidateCount,
    int OriginalMemberCount, int IntroducedMemberCount, IReadOnlyList<string> ExcludedKeys,
    int ReductionVsNone, decimal ReductionFraction, decimal RetainedFraction, bool ContainsTruth,
    int FalseExclusionCount, int CandidateGenerationWork, bool WorkRuleSatisfied);
public sealed record CandidateDensityConditionalMetric(string ArmId, string ComparisonId,
    IReadOnlyList<string> ExcludedKeys, int AdditionalCandidateReduction, decimal ReductionFraction);
public sealed record CandidateDensityProfileChange(string ProfileId, int FromCount, int ToCount, int CountDelta,
    IReadOnlyList<string> AddedSurvivorKeys, IReadOnlyList<string> ExpectedAddedSurvivorKeys,
    IReadOnlyList<string> NewExcludedKeys, int ReductionDelta, decimal ReductionFractionDelta,
    decimal RetainedFractionDelta, bool KeysNested, bool NativeIdsNested, bool RestrictionRecoversOriginal,
    bool NewSurvivorsMatchPredicates, bool ExcludedKeysNested);
public sealed record CandidateDensityConditionalChange(string ComparisonId, CommonWorldObservationAxis AddedAxis,
    int FromReduction, int ToReduction, int ReductionDelta, decimal ReductionFractionDelta,
    IReadOnlyList<string> NewExcludedKeys, bool ExcludedKeysNested, bool NewlyDetectableEffect,
    bool LostEffect, CommonWorldPlateauDomainDiagnostic FromDiagnostic,
    CommonWorldPlateauDomainDiagnostic ToDiagnostic, bool LogicalFlagsValid);
public sealed record CandidateDensityEquivalenceChange(string LeftProfileId, string RightProfileId,
    bool FromEquivalent, bool ToEquivalent, bool BecameDistinguishable, bool BecameIndistinguishable);
public sealed record CandidateDensityMinimumChange(int FromMinimumCount, int ToMinimumCount,
    int FromMinimumFieldCount, int ToMinimumFieldCount, bool CountChanged, bool FamilyChanged,
    IReadOnlyList<string> LostAtMinimumProfiles, IReadOnlyList<string> GainedAtMinimumProfiles,
    IReadOnlyList<string> LostInclusionMinimalProfiles, IReadOnlyList<string> GainedInclusionMinimalProfiles,
    bool AtMinimumProfilesNested, bool EveryNewMinimalContainsOldMinimal, bool FullNecessityPreserved);
public sealed record CandidateDensityPairChange(string FromArmId, string ToArmId,
    IReadOnlyList<string> AddedUniverseKeys, bool UniverseKeysNested, bool UniverseNativeIdsNested,
    bool OldCandidateDescriptorsUnchanged, IReadOnlyList<CandidateDensityProfileChange> ProfileChanges,
    IReadOnlyList<CandidateDensityConditionalChange> ConditionalChanges,
    IReadOnlyList<CandidateDensityEquivalenceChange> EquivalenceChanges,
    CandidateDensityMinimumChange MinimumChange, bool AllInvariantsSatisfied);

public sealed record CandidateDensityWorldRun(int Seed, GeneratedCommonFieldCombinationWorld OriginalWorld,
    CandidateDensityFixedTruth FixedTruth, IReadOnlyList<CandidateIntervalWidthCrosswalk> IntervalToPointCrosswalk,
    CandidateDensitySupport Support, IReadOnlyList<CandidateIntervalWidthArm> Arms,
    IReadOnlyList<CandidateDensityCrosswalk> CandidateCrosswalk,
    IReadOnlyList<CandidateDensityProfileMetric> ProfileMetrics,
    IReadOnlyList<CandidateDensityConditionalMetric> ConditionalMetrics,
    IReadOnlyList<CandidateDensityPairChange> PairChanges, IReadOnlyList<CandidateDensityOccupancy> Occupancy,
    string OriginalWorldBeforeSignature, string OriginalWorldAfterSignature, string PublicRequestBeforeSignature,
    string PublicRequestAfterSignature, string RawEvidenceSignature, string SourceGraphSignature,
    bool F18PointReferenceMatches, bool FullSupportAnalyticCountsMatch, bool AllInvariantsSatisfied);

public sealed record CandidateDensityDecimalDistributionEntry(decimal Value, int Count);
public sealed record CandidateDensityDecimalStatistics(decimal Mean, decimal Median, decimal Minimum,
    decimal Maximum, IReadOnlyList<CandidateDensityDecimalDistributionEntry> Distribution);
public sealed record CandidateDensityProfileAggregate(string ArmId, string ProfileId, int WorldCount,
    CandidateIntervalWidthStatistics CandidateCount, CandidateIntervalWidthStatistics ReductionVsNone,
    CandidateDensityDecimalStatistics ReductionFraction, CandidateDensityDecimalStatistics RetainedFraction,
    CandidateIntervalWidthStatistics CandidateGenerationWork, int ZeroReductionWorldCount,
    int ContainsTruthCount, int FalseExclusionCount);
public sealed record CandidateDensityClassificationCount(CommonWorldConditionalOutcomeClassification Classification,
    int Count, IReadOnlyList<int> Seeds);
public sealed record CandidateDensityConditionalAggregate(string ArmId, string ComparisonId, int ComparisonCount,
    CandidateIntervalWidthStatistics AdditionalReduction, CandidateDensityDecimalStatistics ReductionFraction,
    int ZeroEffectCount, IReadOnlyList<CandidateDensityClassificationCount> ClassificationCounts);
public sealed record CandidateDensityFamilyFrequency(string Family, int WorldCount, IReadOnlyList<int> Seeds);
public sealed record CandidateDensityProfileFrequency(string ProfileId, int AtMinimumWorldCount,
    int InclusionMinimalWorldCount);
public sealed record CandidateDensityArmAggregate(string ArmId, int WorldCount,
    CandidateIntervalWidthStatistics UniverseCount, CandidateDensityDecimalStatistics Density,
    CandidateIntervalWidthStatistics OccupiedRegionCount, CandidateIntervalWidthStatistics OccupiedTickCount,
    CandidateDensityDecimalStatistics UnusedRegionFraction, CandidateIntervalWidthStatistics MinimumCount,
    CandidateIntervalWidthStatistics MinimumFieldCount, int FullNecessaryWorldCount,
    IReadOnlyList<CandidateDensityProfileFrequency> MinimumProfileFrequencies,
    IReadOnlyList<CandidateDensityFamilyFrequency> MinimumFamilyFrequencies);
public sealed record CandidateDensityPairProfileAggregate(string ProfileId,
    CandidateIntervalWidthStatistics CountDelta, CandidateIntervalWidthStatistics ReductionDelta,
    CandidateDensityDecimalStatistics ReductionFractionDelta, int ChangedWorldCount, int ZeroEffectWorldCount);
public sealed record CandidateDensityPairConditionalAggregate(string ComparisonId, int ComparisonCount,
    CandidateIntervalWidthStatistics ReductionDelta, CandidateDensityDecimalStatistics ReductionFractionDelta,
    int ChangedWorldCount, int ClassificationChangedCount, IReadOnlyList<int> ClassificationAffectedSeeds,
    int NewlyDetectableEffectCount, int LostEffectCount, int TopFlagChangedCount, int ImplicationFlagChangedCount,
    IReadOnlyList<CommonWorldPlateauClassificationTransition> ClassificationTransitions);
public sealed record CandidateDensityPairAggregate(string FromArmId, string ToArmId, int WorldCount,
    int MinimumCountChangedWorldCount, int MinimumFieldCountChangedWorldCount, int MinimumFamilyChangedWorldCount,
    IReadOnlyList<int> MinimumFamilyAffectedSeeds, int AtMinimumProfilesChangedWorldCount,
    int EquivalenceChangedCount, int EquivalenceChangedWorldCount, int BecameIndistinguishableCount,
    IReadOnlyList<CandidateDensityPairProfileAggregate> Profiles,
    IReadOnlyList<CandidateDensityPairConditionalAggregate> Conditionals);
public sealed record CandidateDensitySummary(int WorldCount, int PrimaryProfileEvaluationCount,
    int ReferenceProfileEvaluationCount, int ArmPairCount, int ProfileNestingCheckCount,
    int JointSetInclusionCheckCount, int TruthContainmentCount, int FalseExclusionCount,
    int BaselineCandidateCount, int FullSupportCandidateCount, int InvariantViolationCount,
    string InterpretationBoundary);
public sealed record CandidateDensityManifest(string ExperimentId, string SourceCommitSha, bool DirtyWorkingTree,
    string ProtocolContentSha256, string BaselineExperimentContentSha256, string BaselineFinalCommit,
    string SupportRuleId, string OrderingRuleId, string PlateauRuleId, IReadOnlyList<int> Seeds,
    string FixedInputSignature, string PublicOutputSignature, string TruthMappingRule,
    IReadOnlyList<string> OutputFiles);
public sealed record CandidateDensityResult(CandidateDensityManifest Manifest, CandidateDensitySummary Summary,
    IReadOnlyList<CandidateDensityWorldRun> Runs, IReadOnlyList<CandidateDensityProfileAggregate> ProfileAggregates,
    IReadOnlyList<CandidateDensityConditionalAggregate> ConditionalAggregates,
    IReadOnlyList<CandidateDensityPairAggregate> PairAggregates,
    IReadOnlyList<CandidateDensityArmAggregate> ArmAggregates);
