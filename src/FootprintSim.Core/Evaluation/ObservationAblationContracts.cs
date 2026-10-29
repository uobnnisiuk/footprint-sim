using System.Text.Json;
using FootprintSim.Evidence;
using FootprintSim.Inference.PastStates;
using FootprintSim.Interactions;
using FootprintSim.Interactions.Tracing;
using FootprintSim.Scenarios;
using FootprintSim.Serialization;

namespace FootprintSim.Evaluation;

public sealed record ObservationAblationExperimentDefinition
{
    public int SchemaVersion { get; init; } = 1;

    public required string Id { get; init; }

    public required string ScenarioPath { get; init; }

    public required string ScenarioId { get; init; }

    public IReadOnlyList<ObservationFamilyDefinition> Families { get; init; } = [];

    public IReadOnlyList<ObservationFamilyCombinationDefinition> SelectedCombinations { get; init; } = [];
}

public sealed record ObservationFamilyDefinition
{
    public required string Id { get; init; }

    public required string Description { get; init; }

    public IReadOnlyList<string> SourceObservationKeys { get; init; } = [];
}

public sealed record ObservationFamilyCombinationDefinition
{
    public required string Id { get; init; }

    public IReadOnlyList<string> FamilyIds { get; init; } = [];

    public required string SelectionRationale { get; init; }
}

public enum ObservationProfileKind
{
    Full,
    None,
    SingleFamilyRemoval,
    SingleFamilyPresence,
    SelectedCombination
}

public sealed record ObservationProfileDefinition
{
    public required string Id { get; init; }

    public required ObservationProfileKind Kind { get; init; }

    public IReadOnlyList<string> EnabledFamilyIds { get; init; } = [];

    public required string SelectionRationale { get; init; }
}

public sealed record ObservationFamilyInventoryEntry
{
    public required string FamilyId { get; init; }

    public required string Description { get; init; }

    public IReadOnlyList<string> SourceObservationKeys { get; init; } = [];

    public IReadOnlyList<string> ConstraintDefinitionIds { get; init; } = [];

    public IReadOnlyList<string> DirectSourceIds { get; init; } = [];

    public IReadOnlyList<TracingConstraintSourceKind> SourceKinds { get; init; } = [];

    public IReadOnlyList<PastStateEvidenceUsageRole> UsageRoles { get; init; } = [];

    public IReadOnlyList<string> ObservedSubjectIds { get; init; } = [];

    public IReadOnlyList<string> ObserverOrSourceIds { get; init; } = [];

    public IReadOnlyList<InteractionTimeWindow> ObservedTimeWindows { get; init; } = [];

    public IReadOnlyList<long> AvailableAtTicks { get; init; } = [];

    public IReadOnlyList<string> RecordedRegionIds { get; init; } = [];

    public IReadOnlyList<string> AllowedConstraintRegionIds { get; init; } = [];

    public IReadOnlyList<InteractionTimeWindow> ConstrainedTimeWindows { get; init; } = [];

    public IReadOnlyList<string> RelationKinds { get; init; } = [];

    public IReadOnlyList<string> TransformationKinds { get; init; } = [];

    public IReadOnlyList<EvidenceState> EvidenceStates { get; init; } = [];

    public IReadOnlyList<InteractionVisibility> Visibilities { get; init; } = [];

    public IReadOnlyList<string> SourceAncestryIds { get; init; } = [];

    public IReadOnlyList<string> InformationDimensions { get; init; } = [];
}

public sealed record ObservationAblationEvaluationRequest
{
    public required ObservationAblationExperimentDefinition Experiment { get; init; }

    public required ScenarioDefinition Scenario { get; init; }

    public IReadOnlyList<int> Seeds { get; init; } = [];

    public required string ExperimentContentSha256 { get; init; }

    public required string ScenarioContentSha256 { get; init; }

    public required string SourceCommitSha { get; init; }

    public required bool DirtyWorkingTree { get; init; }
}

public sealed record ObservationAblationCandidateEvaluation
{
    public required string CandidateId { get; init; }

    public required string RegionId { get; init; }

    public required PastStateCandidateDisposition CandidateDisposition { get; init; }

    public required CandidateVerificationDisposition VerificationDisposition { get; init; }
}

public sealed record ObservationAblationMethodRun
{
    public required PastStateMethod Method { get; init; }

    public IReadOnlyList<string> CandidateRegionIds { get; init; } = [];

    public required int HistoricalCandidateStateCount { get; init; }

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

public sealed record ObservationAblationRun
{
    public required string ScenarioId { get; init; }

    public required int Seed { get; init; }

    public required PastStateEvidenceCondition EvidenceCondition { get; init; }

    public required string ProfileId { get; init; }

    public required ObservationProfileKind ProfileKind { get; init; }

    public IReadOnlyList<string> EnabledFamilyIds { get; init; } = [];

    public IReadOnlyList<string> PresentFamilyIds { get; init; } = [];

    public IReadOnlyList<string> QueryUsableFamilyIds { get; init; } = [];

    public IReadOnlyList<string> RecordedObservationIds { get; init; } = [];

    public IReadOnlyList<string> PublicObservationIds { get; init; } = [];

    public IReadOnlyList<string> AvailableByCutoffObservationIds { get; init; } = [];

    public IReadOnlyList<string> QueryUsableSourceObservationIds { get; init; } = [];

    public IReadOnlyList<string> ConstraintIds { get; init; } = [];

    public required string TruthWorldIdentifier { get; init; }

    public required string PublicTopologyIdentifier { get; init; }

    public string? TruthPersonRegionId { get; init; }

    public string? TruthDeviceRegionId { get; init; }

    public required int SearchableRegionCount { get; init; }

    public required int RawObservationCount { get; init; }

    public required int PublicObservationCount { get; init; }

    public required int AvailableByCutoffObservationCount { get; init; }

    public required int QueryUsableSourceObservationCount { get; init; }

    public required int FamilyRawObservationCount { get; init; }

    public required int GenerationConstraintCount { get; init; }

    public required int VerificationConstraintCount { get; init; }

    public required int TraversalConstraintCount { get; init; }

    public required int ResolvedConstraintSourceCount { get; init; }

    public required int UnresolvedConstraintSourceCount { get; init; }

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

    public IReadOnlyList<ObservationAblationMethodRun> Methods { get; init; } = [];
}

public sealed record ObservationAblationMethodAggregate
{
    public required PastStateMethod Method { get; init; }

    public required int SeedCount { get; init; }

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

public sealed record ObservationProfileAggregate
{
    public required string ProfileId { get; init; }

    public required ObservationProfileKind ProfileKind { get; init; }

    public IReadOnlyList<string> EnabledFamilyIds { get; init; } = [];

    public required int SeedCount { get; init; }

    public required bool SafetyEquivalentToFull { get; init; }

    public required bool BaselineEquivalent { get; init; }

    public required decimal MeanRawObservationCount { get; init; }

    public required decimal MeanPublicObservationCount { get; init; }

    public required decimal MeanAvailableByCutoffObservationCount { get; init; }

    public required decimal MeanQueryUsableSourceObservationCount { get; init; }

    public required decimal MeanFamilyRawObservationCount { get; init; }

    public required decimal MeanGenerationConstraintCount { get; init; }

    public required decimal MeanVerificationConstraintCount { get; init; }

    public required decimal MeanTraversalConstraintCount { get; init; }

    public required decimal MeanPresentEvidenceSourceGroupCount { get; init; }

    public required decimal MeanIndependentSourceGroupCount { get; init; }

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

    public IReadOnlyList<ObservationAblationMethodAggregate> Methods { get; init; } = [];
}

public sealed record ObservationProfileMethodDelta
{
    public required string ProfileId { get; init; }

    public required PastStateMethod Method { get; init; }

    public IReadOnlyList<string> RemovedFromFullFamilyIds { get; init; } = [];

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

    public required decimal UnresolvedCandidateCountDelta { get; init; }

    public required decimal ConflictedCandidateCountDelta { get; init; }
}

public sealed record ObservationFamilyContribution
{
    public required string FamilyId { get; init; }

    public required string WithoutProfileId { get; init; }

    public required string OnlyProfileId { get; init; }

    public required int RawObservationPresentSeedCount { get; init; }

    public required int ActiveConstraintSeedCount { get; init; }

    public required int ActiveConstraintUseCount { get; init; }

    public IReadOnlyList<ObservationProfileMethodDelta> RemovalDeltas { get; init; } = [];

    public required int IndependentVerificationDispositionDelta { get; init; }

    public required int UnresolvedVerificationDispositionDelta { get; init; }
}

public sealed record ObservationAblationManifest
{
    public int OutputSchemaVersion { get; init; } = 1;

    public required string ExperimentId { get; init; }

    public required string ScenarioId { get; init; }

    public required string ExperimentContentSha256 { get; init; }

    public required string ScenarioContentSha256 { get; init; }

    public IReadOnlyList<int> Seeds { get; init; } = [];

    public IReadOnlyList<string> ProfileIds { get; init; } = [];

    public required int ProfileRunCount { get; init; }

    public required int MethodRunCount { get; init; }

    public required string SourceCommitSha { get; init; }

    public required bool DirtyWorkingTree { get; init; }
}

public sealed record ObservationAblationEvaluationResult
{
    public required ObservationAblationManifest Manifest { get; init; }

    public IReadOnlyList<ObservationFamilyInventoryEntry> Families { get; init; } = [];

    public IReadOnlyList<ObservationProfileDefinition> Profiles { get; init; } = [];

    public IReadOnlyList<ObservationAblationRun> Runs { get; init; } = [];

    public IReadOnlyList<ObservationProfileAggregate> ProfileAggregates { get; init; } = [];

    public IReadOnlyList<ObservationProfileMethodDelta> DeltasFromFull { get; init; } = [];

    public IReadOnlyList<ObservationFamilyContribution> FamilyContributions { get; init; } = [];

    public IReadOnlyList<string> CandidateMinimalProfileIds { get; init; } = [];

    public required bool CandidateMinimalProfileIsUnique { get; init; }

    public required string BaselineEquivalenceCriterion { get; init; }
}

public static class ObservationAblationExperimentLoader
{
    public static ObservationAblationExperimentDefinition Load(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var definition = JsonSerializer.Deserialize<ObservationAblationExperimentDefinition>(
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
