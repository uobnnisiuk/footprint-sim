using FootprintSim.Inference.Derivation;
using FootprintSim.Interactions;
using FootprintSim.Interactions.CandidateWarps;

namespace FootprintSim.Evaluation;

public sealed record CandidateIntervalWidthProtocol
{
    public required int SchemaVersion { get; init; }
    public required string Id { get; init; }
    public required string BaselineFinalCommit { get; init; }
    public required string BaselineExperimentSha256 { get; init; }
    public required int RequiredSeedStart { get; init; }
    public required int RequiredSeedEnd { get; init; }
    public required IReadOnlyList<string> Arms { get; init; }
    public required string TransformRuleId { get; init; }
    public required string StableKeyRule { get; init; }
    public required string TruthRule { get; init; }
    public required string PlateauRuleId { get; init; }
    public required string FixedInference { get; init; }
    public required bool ProspectiveOutcomeInspected { get; init; }
    public required string FailurePolicy { get; init; }
}

public sealed record CandidateIntervalWidthRequest
{
    public required CandidateIntervalWidthProtocol Protocol { get; init; }
    public required string ProtocolContentSha256 { get; init; }
    public required CommonWorldFieldCombinationExperimentDefinition BaselineExperiment { get; init; }
    public required string BaselineExperimentContentSha256 { get; init; }
    public IReadOnlyList<int> Seeds { get; init; } = [];
    public required string SourceCommitSha { get; init; }
    public required bool DirtyWorkingTree { get; init; }
}

public sealed record CandidateIntervalWidthPublicCandidate
{
    public required string CandidateKey { get; init; }
    public required string PublicSourceIdentifier { get; init; }
    public required PublicCandidateWarpStateDefinition State { get; init; }
}

public sealed record CandidateIntervalWidthAxisComponent
{
    public required CommonWorldObservationAxis Axis { get; init; }
    public required CandidateGenerationConstraintComponent Component { get; init; }
}

/// <summary>Both primary inference arms consume only public structure and constraints.</summary>
public sealed record CandidateIntervalWidthPublicRequest
{
    public required string ScenarioId { get; init; }
    public required int Seed { get; init; }
    public required string TargetEntityId { get; init; }
    public required string TargetStateContextId { get; init; }
    public required string CandidateDomainId { get; init; }
    public required long EvidenceAvailabilityCutoff { get; init; }
    public IReadOnlyList<string> RegisteredRegionIds { get; init; } = [];
    public IReadOnlyList<CandidateIntervalWidthPublicCandidate> Candidates { get; init; } = [];
    public IReadOnlyList<CandidateIntervalWidthAxisComponent> AxisComponents { get; init; } = [];
    public IReadOnlyList<CommonWorldProfileDefinition> Profiles { get; init; } = [];
    public IReadOnlyList<CommonWorldConditionalComparisonDefinition> ConditionalComparisons { get; init; } = [];
    public IReadOnlyList<CommonWorldSetInclusionDefinition> PrimarySetInclusions { get; init; } = [];
}

public sealed record CandidateIntervalWidthNativeCandidate(
    string CandidateKey, string PublicSourceIdentifier, string RegionId,
    InteractionTimeWindow TimeWindow, CandidateWarpTemporalKind TemporalKind,
    string StateId, string CandidateId, string ProvenanceSignature);

public sealed record CandidateIntervalWidthProfile(
    string ProfileId, int Order, IReadOnlyList<CommonWorldObservationAxis> ActiveAxes,
    IReadOnlyList<string> CandidateIds, IReadOnlyList<string> CandidateKeys,
    int CandidateCount, int CandidateGenerationWork, string CompositionId,
    CandidateGenerationConstraintCompositionAudit CompositionAudit);

public sealed record CandidateIntervalWidthConditional(
    string ComparisonId, string BaselineProfileId, string CombinedProfileId,
    CommonWorldObservationAxis AddedAxis, int BaselineCandidateCount, int CombinedCandidateCount,
    int AdditionalCandidateReduction, CommonWorldPlateauDomainDiagnostic Diagnostic);

public sealed record CandidateIntervalWidthSetInclusion(string SubsetProfileId,
    string SupersetProfileId, bool CandidateKeysAreSubset);

public sealed record CandidateIntervalWidthEquivalence(string LeftProfileId,
    string RightProfileId, bool CandidateKeySetsEqual);

public sealed record CandidateIntervalWidthMinimum(int MinimumCandidateCount,
    IReadOnlyList<string> ProfilesAtMinimumCount, IReadOnlyList<string> InclusionMinimalProfileIds,
    bool AllMinimumProfilesReturnSameKeySet);

public sealed record CandidateIntervalWidthArm(
    string ArmId, string RepresentationId, string CandidateUniverseId,
    string FixedConstraintSignature, string CandidateInputSignature,
    IReadOnlyList<CandidateIntervalWidthNativeCandidate> Candidates,
    IReadOnlyList<CandidateIntervalWidthProfile> Profiles,
    IReadOnlyList<CandidateIntervalWidthConditional> Conditionals,
    IReadOnlyList<CandidateIntervalWidthSetInclusion> SetInclusions,
    IReadOnlyList<CandidateIntervalWidthEquivalence> ProfileEquivalences,
    CandidateIntervalWidthMinimum Minimum);

public sealed record CandidateIntervalWidthCrosswalk(int Seed, string CandidateKey,
    CandidateIntervalWidthNativeCandidate Original, CandidateIntervalWidthNativeCandidate Point);

public sealed record CandidateIntervalWidthLostWitness(string CandidateKey,
    long OriginalStartTick, long OriginalEndTick, long DerivedTimeLowerBound,
    bool EndTouchesLowerBound);

public sealed record CandidateIntervalWidthProfileChange(string ProfileId,
    int OriginalCandidateCount, int PointCandidateCount, int PointMinusOriginalCount,
    IReadOnlyList<string> OriginalCandidateIds, IReadOnlyList<string> PointCandidateIds,
    IReadOnlyList<string> OriginalCandidateKeys, IReadOnlyList<string> PointCandidateKeys,
    IReadOnlyList<string> LostKeys, IReadOnlyList<string> NewKeys,
    IReadOnlyList<CandidateIntervalWidthLostWitness> LostWitnesses,
    bool ExpectedEquality, bool KeySetsEqual, bool PointKeysAreSubset,
    bool LostKeysMatchBoundaryRule, bool WorkAndCompositionUnchanged,
    bool OriginalContainsTruth, bool PointContainsTruth);

public sealed record CandidateIntervalWidthConditionalChange(string ComparisonId,
    CommonWorldObservationAxis AddedAxis, string ExpectedDeltaSign,
    int OriginalAdditionalReduction, int PointAdditionalReduction, int PointMinusOriginalReduction,
    CommonWorldPlateauDomainDiagnostic OriginalDiagnostic,
    CommonWorldPlateauDomainDiagnostic PointDiagnostic, bool SignedDeltaInvariant);

public sealed record CandidateIntervalWidthMinimumChange(int OriginalMinimumCount,
    int PointMinimumCount, bool MinimumCountChanged, bool InclusionMinimalFamilyChanged,
    IReadOnlyList<string> GainedMinimumProfiles, IReadOnlyList<string> LostMinimumProfiles,
    IReadOnlyList<string> GainedInclusionMinimalProfiles, IReadOnlyList<string> LostInclusionMinimalProfiles);

public sealed record CandidateIntervalWidthFixedTruth(string OriginalTruthWorldId,
    string OriginalTruthCandidateKey, long OriginalTruthStartTick, long OriginalTruthEndTick,
    string OriginalTruthRegionId, string OriginalTruthParticipantId, long OriginalEncounterTick,
    long OriginalRealizedDurationTicks, string OriginalCandidateId, string PointCandidateId,
    bool ResolvedAfterBothPublicArms);

public sealed record CandidateIntervalWidthWorldRun(int Seed,
    GeneratedCommonFieldCombinationWorld OriginalWorld, CandidateIntervalWidthFixedTruth FixedTruth,
    IReadOnlyList<CandidateIntervalWidthCrosswalk> Crosswalk, CandidateIntervalWidthArm Original,
    CandidateIntervalWidthArm Point, IReadOnlyList<CandidateIntervalWidthProfileChange> ProfileChanges,
    IReadOnlyList<CandidateIntervalWidthConditionalChange> ConditionalChanges,
    CandidateIntervalWidthMinimumChange MinimumChange, string OriginalWorldBeforeSignature,
    string OriginalWorldAfterSignature, string RawEvidenceSignature, string SourceGraphSignature,
    bool ReferenceMatches, bool AllInvariantsSatisfied);

public sealed record CandidateIntervalWidthDistributionEntry(int Value, int Count);
public sealed record CandidateIntervalWidthStatistics(decimal Mean, decimal Median, int Minimum, int Maximum,
    IReadOnlyList<CandidateIntervalWidthDistributionEntry> Distribution);
public sealed record CandidateIntervalWidthProfileAggregate(string ProfileId, int WorldCount,
    CandidateIntervalWidthStatistics Original, CandidateIntervalWidthStatistics Point,
    CandidateIntervalWidthStatistics PointMinusOriginal, int ChangedWorldCount, int ZeroEffectWorldCount,
    int OriginalContainmentCount, int PointContainmentCount, int FalseExclusionCount);
public sealed record CandidateIntervalWidthConditionalAggregate(string ComparisonId, int ComparisonCount,
    CandidateIntervalWidthStatistics OriginalReduction, CandidateIntervalWidthStatistics PointReduction,
    CandidateIntervalWidthStatistics PointMinusOriginal, int ChangedWorldCount,
    int ClassificationChangedCount, int TopFlagChangedCount, int ImplicationFlagChangedCount,
    IReadOnlyList<int> ClassificationAffectedSeeds,
    IReadOnlyList<CommonWorldPlateauClassificationTransition> ClassificationTransitions);
public sealed record CandidateIntervalWidthSummary(int WorldCount, int PrimaryProfileEvaluationCount,
    int ReferenceProfileEvaluationCount, int NonTimeEqualityCheckCount, int TimeSubsetCheckCount,
    int JointSetInclusionCheckCount, int TruthContainmentCount, int FalseExclusionCount,
    int MinimumCountChangedWorldCount, int MinimumFamilyChangedWorldCount,
    int OriginalCandidateCount, int PositiveWidthCandidateCount, int InvariantViolationCount,
    string InterpretationBoundary);
public sealed record CandidateIntervalWidthManifest(string ExperimentId, string SourceCommitSha,
    bool DirtyWorkingTree, string ProtocolContentSha256, string BaselineExperimentContentSha256,
    string BaselineFinalCommit, string TransformRuleId, string PlateauRuleId,
    IReadOnlyList<int> Seeds, string FixedInputSignature, string PublicOutputSignature,
    string TruthMappingRule, IReadOnlyList<string> OutputFiles);
public sealed record CandidateIntervalWidthResult(CandidateIntervalWidthManifest Manifest,
    CandidateIntervalWidthSummary Summary, IReadOnlyList<CandidateIntervalWidthWorldRun> Runs,
    IReadOnlyList<CandidateIntervalWidthProfileAggregate> ProfileAggregates,
    IReadOnlyList<CandidateIntervalWidthConditionalAggregate> ConditionalAggregates);
