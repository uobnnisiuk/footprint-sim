using System.Text.Json;
using FootprintSim.Evidence;
using FootprintSim.Inference.PastStates;
using FootprintSim.Interactions;
using FootprintSim.Scenarios;
using FootprintSim.Serialization;

namespace FootprintSim.Evaluation;

public sealed record LaterTraceFieldAblationExperimentDefinition
{
    public int SchemaVersion { get; init; } = 1;

    public required string Id { get; init; }

    public required string ScenarioPath { get; init; }

    public required string ScenarioId { get; init; }

    public required string BaseObservationAblationExperimentPath { get; init; }

    public required string SourceObservationKey { get; init; }

    public IReadOnlyList<LaterTraceInformationFieldDefinition> Fields { get; init; } = [];

    public IReadOnlyList<string> RequiredFieldIds { get; init; } = [];

    public IReadOnlyList<LaterTraceFieldProfileDefinition> Profiles { get; init; } = [];
}

public sealed record LaterTraceInformationFieldDefinition
{
    public required string Id { get; init; }

    public required string Description { get; init; }
}

public enum LaterTraceFieldProfileKind
{
    Full,
    SingleFieldRemoval,
    SingleFieldPresence,
    None,
    SelectedCombination
}

public sealed record LaterTraceFieldProfileDefinition
{
    public required string Id { get; init; }

    public required LaterTraceFieldProfileKind Kind { get; init; }

    public IReadOnlyList<string> AvailableFieldIds { get; init; } = [];

    public required bool IsCore { get; init; }

    public required string SelectionRationale { get; init; }
}

public sealed record LaterTraceSourceInventory
{
    public required string SourceObservationKey { get; init; }

    public IReadOnlyList<string> ConstraintDefinitionIds { get; init; } = [];

    public IReadOnlyList<string> DirectSourceIds { get; init; } = [];

    public IReadOnlyList<string> SourceAncestryIds { get; init; } = [];

    public IReadOnlyList<string> ObservedSubjectIds { get; init; } = [];

    public IReadOnlyList<string> ObserverOrParticipantIds { get; init; } = [];

    public IReadOnlyList<InteractionTimeWindow> ObservedTimeWindows { get; init; } = [];

    public IReadOnlyList<long> AvailableAtTicks { get; init; } = [];

    public IReadOnlyList<string> RecordedRegionIds { get; init; } = [];

    public IReadOnlyList<string> ObservedRelationKinds { get; init; } = [];

    public IReadOnlyList<string> DerivedRelationKinds { get; init; } = [];

    public IReadOnlyList<string> TransformationKinds { get; init; } = [];

    public IReadOnlyList<EvidenceState> EvidenceStates { get; init; } = [];

    public IReadOnlyList<PastStateEvidenceUsageRole> DeclaredUsageRoles { get; init; } = [];
}

public sealed record LaterTraceFieldAblationEvaluationRequest
{
    public required LaterTraceFieldAblationExperimentDefinition Experiment { get; init; }

    public required ObservationAblationExperimentDefinition BaseExperiment { get; init; }

    public required ScenarioDefinition Scenario { get; init; }

    public IReadOnlyList<int> Seeds { get; init; } = [];

    public required string ExperimentContentSha256 { get; init; }

    public required string BaseExperimentContentSha256 { get; init; }

    public required string ScenarioContentSha256 { get; init; }

    public required string SourceCommitSha { get; init; }

    public required bool DirtyWorkingTree { get; init; }
}

public sealed record LaterTraceFieldMethodRun
{
    public required PastStateMethod Method { get; init; }

    public IReadOnlyList<string> CandidateRegionIdsBefore { get; init; } = [];

    public IReadOnlyList<string> CandidateRegionIdsAfter { get; init; } = [];

    public required bool? ContainsTruth { get; init; }

    public required bool? FalseExclusion { get; init; }

    public required decimal CandidateReduction { get; init; }

    public required int CandidateGenerationWork { get; init; }

    public required int CandidateVerificationWork { get; init; }

    public required int ForwardTraversalWork { get; init; }

    public required int BackwardTraversalWork { get; init; }

    public required int PublicRelationsInspected { get; init; }

    public required int EvidenceRecordsInspected { get; init; }

    public required int SourceGroupsInspected { get; init; }

    public IReadOnlyList<string> TransitionIds { get; init; } = [];
}

public sealed record LaterTraceFieldAblationRun
{
    public required string ScenarioId { get; init; }

    public required int Seed { get; init; }

    public required PastStateEvidenceCondition EvidenceCondition { get; init; }

    public required string ProfileId { get; init; }

    public required LaterTraceFieldProfileKind ProfileKind { get; init; }

    public required bool IsCore { get; init; }

    public IReadOnlyList<string> AvailableFieldIds { get; init; } = [];

    public IReadOnlyList<string> MissingFieldIds { get; init; } = [];

    public required bool UnderlyingSourceObservationExists { get; init; }

    public string? SourceObservationId { get; init; }

    public required bool SourceObservationResolved { get; init; }

    public required bool SourceRecordEligibleForDerivation { get; init; }

    public required bool EvidenceGenerated { get; init; }

    public required bool ConstraintDerivationSucceeded { get; init; }

    public string? DerivationFailureReason { get; init; }

    public IReadOnlyList<string> GeneratedConstraintIds { get; init; } = [];

    public IReadOnlyList<string> GeneratedConstraintDefinitionIds { get; init; } = [];

    public IReadOnlyList<PastStateEvidenceUsageRole> DeclaredUsageRoles { get; init; } = [];

    public IReadOnlyList<PastStateEvidenceUsageRole> AppliedUsageRoles { get; init; } = [];

    public required bool TraversalInputGenerated { get; init; }

    public required string SourceBehaviorReferenceProfileId { get; init; }

    public required string TruthWorldIdentifier { get; init; }

    public required string PublicTopologyIdentifier { get; init; }

    public string? TruthPersonRegionId { get; init; }

    public required int PresentEvidenceSourceGroupCount { get; init; }

    public required int IndependentSourceGroupCount { get; init; }

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

public sealed record LaterTraceFieldMethodAggregate
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

    public required decimal MeanPublicRelationsInspected { get; init; }

    public required decimal MeanEvidenceRecordsInspected { get; init; }

    public required decimal MeanSourceGroupsInspected { get; init; }
}

public sealed record LaterTraceFieldProfileAggregate
{
    public required string ProfileId { get; init; }

    public required LaterTraceFieldProfileKind ProfileKind { get; init; }

    public required bool IsCore { get; init; }

    public IReadOnlyList<string> AvailableFieldIds { get; init; } = [];

    public IReadOnlyList<string> MissingFieldIds { get; init; } = [];

    public required int SeedCount { get; init; }

    public required int DerivationSuccessCount { get; init; }

    public required decimal MeanGeneratedConstraintCount { get; init; }

    public required bool SafetyEquivalentToFull { get; init; }

    public required bool BaselineEquivalent { get; init; }

    public required decimal MeanPresentEvidenceSourceGroupCount { get; init; }

    public required decimal MeanSameAncestryDerivedRecordCount { get; init; }

    public required decimal MeanConflictingEvidenceCount { get; init; }

    public required decimal MeanUncertainEvidenceCount { get; init; }

    public required decimal MeanUnavailableEvidenceCount { get; init; }

    public required decimal MeanUnresolvedCandidateCount { get; init; }

    public required decimal MeanConflictedCandidateCount { get; init; }

    public required int IndependentlySupportedCandidateCount { get; init; }

    public required int IndependentlyRefutedCandidateCount { get; init; }

    public required int ConflictingVerificationCandidateCount { get; init; }

    public required int UnresolvedVerificationCandidateCount { get; init; }

    public required int SharedAncestryOnlyCandidateCount { get; init; }

    public required int UnavailableVerificationCandidateCount { get; init; }

    public IReadOnlyList<LaterTraceFieldMethodAggregate> Methods { get; init; } = [];
}

public sealed record LaterTraceFieldMethodDelta
{
    public required string ProfileId { get; init; }

    public required PastStateMethod Method { get; init; }

    public required decimal MeanCandidateCountDelta { get; init; }

    public required decimal ContainmentRateDelta { get; init; }

    public required int FalseExclusionCountDelta { get; init; }

    public required decimal MeanReductionDelta { get; init; }

    public required decimal CandidateGenerationWorkDelta { get; init; }

    public required decimal CandidateVerificationWorkDelta { get; init; }

    public required decimal ForwardTraversalWorkDelta { get; init; }

    public required decimal BackwardTraversalWorkDelta { get; init; }

    public required decimal PublicRelationsInspectedDelta { get; init; }

    public required decimal EvidenceRecordsInspectedDelta { get; init; }
}

public sealed record LaterTraceFieldContribution
{
    public required string FieldId { get; init; }

    public required string RemovalProfileId { get; init; }

    public required string PresenceProfileId { get; init; }

    public required bool RemovalPreventsDerivation { get; init; }

    public required bool PresenceEnablesDerivation { get; init; }

    public IReadOnlyList<LaterTraceFieldMethodDelta> RemovalDeltas { get; init; } = [];
}

public sealed record LaterTraceFieldAblationManifest
{
    public int OutputSchemaVersion { get; init; } = 1;

    public required string ExperimentId { get; init; }

    public required string ScenarioId { get; init; }

    public required string ExperimentContentSha256 { get; init; }

    public required string BaseExperimentContentSha256 { get; init; }

    public required string ScenarioContentSha256 { get; init; }

    public IReadOnlyList<int> Seeds { get; init; } = [];

    public IReadOnlyList<string> ProfileIds { get; init; } = [];

    public required int ProfileRunCount { get; init; }

    public required int MethodRunCount { get; init; }

    public required string SourceCommitSha { get; init; }

    public required bool DirtyWorkingTree { get; init; }
}

public sealed record LaterTraceFieldAblationEvaluationResult
{
    public required LaterTraceFieldAblationManifest Manifest { get; init; }

    public required LaterTraceSourceInventory SourceInventory { get; init; }

    public IReadOnlyList<LaterTraceInformationFieldDefinition> Fields { get; init; } = [];

    public IReadOnlyList<string> RequiredFieldIds { get; init; } = [];

    public IReadOnlyList<LaterTraceFieldProfileDefinition> Profiles { get; init; } = [];

    public IReadOnlyList<LaterTraceFieldAblationRun> Runs { get; init; } = [];

    public IReadOnlyList<LaterTraceFieldProfileAggregate> ProfileAggregates { get; init; } = [];

    public IReadOnlyList<LaterTraceFieldMethodDelta> DeltasFromFull { get; init; } = [];

    public IReadOnlyList<LaterTraceFieldContribution> FieldContributions { get; init; } = [];

    public IReadOnlyList<string> CandidateMinimalProfileIds { get; init; } = [];

    public required bool CandidateMinimalProfileIsUnique { get; init; }

    public required string BaselineEquivalenceCriterion { get; init; }

    public required string InterpretationBoundary { get; init; }
}

public static class LaterTraceFieldAblationExperimentLoader
{
    public static LaterTraceFieldAblationExperimentDefinition Load(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var definition = JsonSerializer.Deserialize<LaterTraceFieldAblationExperimentDefinition>(
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
