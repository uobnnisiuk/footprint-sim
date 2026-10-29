using System.Text.Json;
using FootprintSim.Evidence;
using FootprintSim.Inference.PastStates;
using FootprintSim.Interactions;
using FootprintSim.Scenarios;
using FootprintSim.Serialization;

namespace FootprintSim.Evaluation;

public enum TimeObservationWindowMeaning
{
    OccursAtOnePossibleTick
}

public sealed record TimeResolutionExperimentDefinition
{
    public int SchemaVersion { get; init; } = 1;

    public required string Id { get; init; }

    public required string ScenarioPath { get; init; }

    public required string ScenarioId { get; init; }

    public required string SourceObservationKey { get; init; }

    public required TimeObservationWindowMeaning TimeWindowMeaning { get; init; }

    public IReadOnlyList<TimeResolutionLevelDefinition> Levels { get; init; } = [];
}

public sealed record TimeResolutionLevelDefinition
{
    public required string Id { get; init; }

    public required int InformationOrder { get; init; }

    public required string Description { get; init; }

    public long? ObservedStartTick { get; init; }

    public long? ObservedEndTick { get; init; }

    public required bool GenerateConstraint { get; init; }

    public required string BoundaryRationale { get; init; }
}

public sealed record TimeResolutionEvaluationRequest
{
    public required TimeResolutionExperimentDefinition Experiment { get; init; }

    public required ScenarioDefinition Scenario { get; init; }

    public IReadOnlyList<int> Seeds { get; init; } = [];

    public required string ExperimentContentSha256 { get; init; }

    public required string ScenarioContentSha256 { get; init; }

    public required string SourceCommitSha { get; init; }

    public required bool DirtyWorkingTree { get; init; }
}

public sealed record PossibleExactTimeDerivation
{
    public required long PossibleObservationTick { get; init; }

    public required string ConstraintDefinitionId { get; init; }

    public required InteractionTimeWindow DerivedHistoricalWindow { get; init; }

    public IReadOnlyList<string> DerivedAllowedRegionIds { get; init; } = [];
}

public sealed record TimeResolutionRun
{
    public required string ScenarioId { get; init; }

    public required int Seed { get; init; }

    public required PastStateEvidenceCondition EvidenceCondition { get; init; }

    public required string LevelId { get; init; }

    public required int InformationOrder { get; init; }

    public required TimeObservationWindowMeaning TimeWindowMeaning { get; init; }

    public required bool ObservedTimeAvailable { get; init; }

    public long? ObservedStartTick { get; init; }

    public long? ObservedEndTick { get; init; }

    public required int ObservedWindowSpanTicks { get; init; }

    public IReadOnlyList<long> PossibleObservationTicks { get; init; } = [];

    public required int PossibleObservationTickCount { get; init; }

    public IReadOnlyList<PossibleExactTimeDerivation> PossibleExactTimeDerivations { get; init; } = [];

    public IReadOnlyList<InteractionTimeWindow> DerivedHistoricalWindows { get; init; } = [];

    public required int DerivedHistoricalPossibleTickCount { get; init; }

    public IReadOnlyList<string> DerivedAllowedRegionIds { get; init; } = [];

    public IReadOnlyList<InteractionTimeWindow> AffectedCandidateStateWindows { get; init; } = [];

    public required int AffectedCandidateStateIntervalCount { get; init; }

    public required bool ConstraintGenerated { get; init; }

    public IReadOnlyList<string> ConstraintDefinitionIds { get; init; } = [];

    public IReadOnlyList<string> ConstraintIds { get; init; } = [];

    public IReadOnlyList<InteractionTimeWindow> ConstraintSourceObservedWindows { get; init; } = [];

    public required string UnderlyingSourceObservationId { get; init; }

    public required bool UnderlyingSourceObservationResolved { get; init; }

    public string? ProjectedSourceObservationId { get; init; }

    public required bool ProjectedSourceObservationEmitted { get; init; }

    public required bool ProjectedSourceObservationResolved { get; init; }

    public IReadOnlyList<string> ObservedSubjectIds { get; init; } = [];

    public IReadOnlyList<string> ObservedRegionIds { get; init; } = [];

    public required InteractionRelationKind ObservedRelationKind { get; init; }

    public required long AvailableAtTick { get; init; }

    public required EvidenceState EvidenceState { get; init; }

    public IReadOnlyList<string> SourceAncestryIds { get; init; } = [];

    public required string TruthWorldIdentifier { get; init; }

    public required string PublicTopologyIdentifier { get; init; }

    public string? TruthPersonRegionId { get; init; }

    public required int PresentEvidenceSourceGroupCount { get; init; }

    public required int SameAncestryDerivedRecordCount { get; init; }

    public required int ConflictingEvidenceCount { get; init; }

    public required int UncertainEvidenceCount { get; init; }

    public required int UnavailableEvidenceCount { get; init; }

    public required int UnresolvedCandidateCount { get; init; }

    public required int ConflictedCandidateCount { get; init; }

    public required int IndependentlySupportedCandidateCount { get; init; }

    public required int IndependentlyRefutedCandidateCount { get; init; }

    public required int ConflictingVerificationCandidateCount { get; init; }

    public required int UnresolvedVerificationCandidateCount { get; init; }

    public required int SharedAncestryOnlyCandidateCount { get; init; }

    public required int UnavailableVerificationCandidateCount { get; init; }

    public IReadOnlyList<ObservationAblationCandidateEvaluation> CandidateEvaluations { get; init; } = [];

    public IReadOnlyList<LaterTraceFieldMethodRun> Methods { get; init; } = [];
}

public sealed record TimeResolutionMethodAggregate
{
    public required PastStateMethod Method { get; init; }

    public required decimal MeanCandidateCount { get; init; }

    public required decimal ContainmentRate { get; init; }

    public required int FalseExclusionCount { get; init; }

    public required decimal MeanReduction { get; init; }

    public required decimal MeanCandidateGenerationWork { get; init; }

    public required decimal MeanCandidateVerificationWork { get; init; }

    public required decimal MeanForwardTraversalWork { get; init; }

    public required decimal MeanBackwardTraversalWork { get; init; }

    public required decimal MeanEvidenceRecordsInspected { get; init; }

    public required decimal MeanPublicRelationsInspected { get; init; }

    public required decimal MeanSourceGroupsInspected { get; init; }
}

public sealed record TimeResolutionAggregate
{
    public required string LevelId { get; init; }

    public required int InformationOrder { get; init; }

    public required bool ObservedTimeAvailable { get; init; }

    public long? ObservedStartTick { get; init; }

    public long? ObservedEndTick { get; init; }

    public required int ObservedWindowSpanTicks { get; init; }

    public required int PossibleObservationTickCount { get; init; }

    public IReadOnlyList<InteractionTimeWindow> DerivedHistoricalWindows { get; init; } = [];

    public required int DerivedHistoricalPossibleTickCount { get; init; }

    public required int AffectedCandidateStateIntervalCount { get; init; }

    public required int SeedCount { get; init; }

    public required int ConstraintGeneratedCount { get; init; }

    public required bool CandidateOutputEquivalentToCurrent { get; init; }

    public required bool BaselineEquivalentToCurrent { get; init; }

    public required decimal MeanPresentEvidenceSourceGroupCount { get; init; }

    public required decimal MeanSameAncestryDerivedRecordCount { get; init; }

    public required decimal MeanUnavailableEvidenceCount { get; init; }

    public required decimal MeanUnresolvedCandidateCount { get; init; }

    public required decimal MeanConflictedCandidateCount { get; init; }

    public required int IndependentlySupportedCandidateCount { get; init; }

    public required int IndependentlyRefutedCandidateCount { get; init; }

    public required int ConflictingVerificationCandidateCount { get; init; }

    public required int UnresolvedVerificationCandidateCount { get; init; }

    public required int SharedAncestryOnlyCandidateCount { get; init; }

    public required int UnavailableVerificationCandidateCount { get; init; }

    public IReadOnlyList<TimeResolutionMethodAggregate> Methods { get; init; } = [];
}

public sealed record TimeResolutionMethodDelta
{
    public required string LevelId { get; init; }

    public required PastStateMethod Method { get; init; }

    public required decimal MeanCandidateCountDelta { get; init; }

    public required decimal ContainmentRateDelta { get; init; }

    public required int FalseExclusionCountDelta { get; init; }

    public required decimal MeanReductionDelta { get; init; }

    public required decimal CandidateGenerationWorkDelta { get; init; }

    public required decimal CandidateVerificationWorkDelta { get; init; }

    public required decimal ForwardTraversalWorkDelta { get; init; }

    public required decimal BackwardTraversalWorkDelta { get; init; }

    public required decimal EvidenceRecordsInspectedDelta { get; init; }

    public required decimal PublicRelationsInspectedDelta { get; init; }
}

public sealed record TimeResolutionMonotonicityAudit
{
    public required int Seed { get; init; }

    public required string MorePreciseLevelId { get; init; }

    public required string CoarserLevelId { get; init; }

    public IReadOnlyList<string> MorePreciseCandidateRegionIds { get; init; } = [];

    public IReadOnlyList<string> CoarserCandidateRegionIds { get; init; } = [];

    public required bool IsSubset { get; init; }
}

public sealed record TimeResolutionManifest
{
    public int OutputSchemaVersion { get; init; } = 1;

    public required string ExperimentId { get; init; }

    public required string ScenarioId { get; init; }

    public required string ExperimentContentSha256 { get; init; }

    public required string ScenarioContentSha256 { get; init; }

    public IReadOnlyList<int> Seeds { get; init; } = [];

    public IReadOnlyList<string> LevelIds { get; init; } = [];

    public required int ProfileRunCount { get; init; }

    public required int MethodRunCount { get; init; }

    public required string SourceCommitSha { get; init; }

    public required bool DirtyWorkingTree { get; init; }
}

public sealed record TimeResolutionEvaluationResult
{
    public required TimeResolutionManifest Manifest { get; init; }

    public IReadOnlyList<TimeResolutionLevelDefinition> Levels { get; init; } = [];

    public IReadOnlyList<TimeResolutionRun> Runs { get; init; } = [];

    public IReadOnlyList<TimeResolutionAggregate> Aggregates { get; init; } = [];

    public IReadOnlyList<TimeResolutionMethodDelta> DeltasFromCurrent { get; init; } = [];

    public IReadOnlyList<TimeResolutionMonotonicityAudit> MonotonicityAudits { get; init; } = [];

    public required int MonotonicityViolationCount { get; init; }

    public required bool CurrentTransformationUsesObservedTick { get; init; }

    public IReadOnlyList<string> CoarsestCandidateOutputEquivalentLevelIds { get; init; } = [];

    public IReadOnlyList<string> CoarsestBaselineEquivalentLevelIds { get; init; } = [];

    public required string InterpretationBoundary { get; init; }
}

public static class TimeResolutionExperimentLoader
{
    public static TimeResolutionExperimentDefinition Load(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var definition = JsonSerializer.Deserialize<TimeResolutionExperimentDefinition>(
            File.ReadAllText(path),
            JsonDefaults.Options) ?? throw new InvalidDataException($"Experiment '{path}' was empty.");
        if (definition.SchemaVersion != 1)
        {
            throw new InvalidDataException(
                $"Experiment '{definition.Id}' has unsupported schema version {definition.SchemaVersion}.");
        }

        return definition;
    }
}
