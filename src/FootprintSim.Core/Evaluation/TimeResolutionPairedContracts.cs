using System.Text.Json;
using System.Text.Json.Serialization;
using FootprintSim.Inference.Derivation;
using FootprintSim.Inference.PastStates;
using FootprintSim.Interactions;
using FootprintSim.Serialization;

namespace FootprintSim.Evaluation;

public sealed record TimeResolutionPairedExperimentDefinition
{
    public int SchemaVersion { get; init; } = 1;

    public required string Id { get; init; }

    public required string ScenarioId { get; init; }

    public required int RequiredSeedStart { get; init; }

    public required int RequiredSeedEnd { get; init; }

    public required string TargetEntityId { get; init; }

    public required string SourceEvidenceIdentifier { get; init; }

    public required string LaterEventKind { get; init; }

    public required string PredecessorStateKind { get; init; }

    public required string DependencyId { get; init; }

    public required string DurationSourceId { get; init; }

    public IReadOnlyList<string> SourceAncestryIds { get; init; } = [];

    public required TimeResolutionWorldGeneratorDefinition Generator { get; init; }

    public IReadOnlyList<TimeResolutionPairedProfileDefinition> Profiles { get; init; } = [];
}

public sealed record TimeResolutionWorldGeneratorDefinition
{
    public required string Id { get; init; }

    public required int CandidateCountMinimum { get; init; }

    public required int CandidateCountMaximum { get; init; }

    public required int LaterEventTickMinimum { get; init; }

    public required int LaterEventTickMaximum { get; init; }

    public required int DurationMinimumTickMinimum { get; init; }

    public required int DurationMinimumTickMaximum { get; init; }

    public required int DurationWidthMinimum { get; init; }

    public required int DurationWidthMaximum { get; init; }

    public required int CandidatePaddingOutsideBroadTicks { get; init; }

    public required int AvailabilityLagAfterBroadEndTicks { get; init; }

    public required bool CandidateTickCollisionsAllowed { get; init; }
}

public sealed record TimeResolutionPairedProfileDefinition
{
    public required string Id { get; init; }

    public required int InformationOrder { get; init; }

    public int? ObservedStartOffsetFromLaterTick { get; init; }

    public int? ObservedEndOffsetFromLaterTick { get; init; }

    public required bool OccurrenceTimeUnknown { get; init; }

    public required string Rationale { get; init; }
}

public sealed record TimeResolutionPairedEvaluationRequest
{
    public required TimeResolutionPairedExperimentDefinition Experiment { get; init; }

    public IReadOnlyList<int> Seeds { get; init; } = [];

    public required string ExperimentContentSha256 { get; init; }

    public required string SourceCommitSha { get; init; }

    public required bool DirtyWorkingTree { get; init; }
}

public sealed record GeneratedTimeResolutionCandidateState
{
    public required string CandidateKey { get; init; }

    public required long Tick { get; init; }

    public required string RegionId { get; init; }

    public required bool IsTruthCandidate { get; init; }
}

public sealed record GeneratedTimeResolutionWorld
{
    public required string GeneratorId { get; init; }

    public required string PublicWorldId { get; init; }

    public required string TruthWorldId { get; init; }

    public required int Seed { get; init; }

    public required int CandidateCount { get; init; }

    public required long LaterEventTick { get; init; }

    public required PublicDurationBound DurationBounds { get; init; }

    public required int DurationWidthTicks { get; init; }

    public required long ActualTruthDurationTicks { get; init; }

    public required long CandidateTickMinimum { get; init; }

    public required long CandidateTickMaximum { get; init; }

    public required long AvailableAtTick { get; init; }

    public required long EvidenceAvailabilityCutoff { get; init; }

    public required string TruthCandidateKey { get; init; }

    public required string TruthRegionId { get; init; }

    public required long TruthTick { get; init; }

    public IReadOnlyList<GeneratedTimeResolutionCandidateState> CandidateStates { get; init; } = [];
}

public sealed record TimeResolutionPairedCandidateStateRun
{
    public required string CandidateKey { get; init; }

    public required string CandidateId { get; init; }

    public required long Tick { get; init; }

    public required string RegionId { get; init; }

    public required bool IsTruthCandidate { get; init; }
}

public sealed record TimeResolutionPairedProfileRun
{
    public required string ProfileId { get; init; }

    public required int InformationOrder { get; init; }

    public required bool SourceObservationPresent { get; init; }

    public required string SourceObservationId { get; init; }

    public required OccurrenceTimeKnowledge OccurrenceTimeKnowledge { get; init; }

    public ObservationTimeWindowKind? TimeWindowKind { get; init; }

    public InteractionTimeWindow? ObservedOccurrenceWindow { get; init; }

    public required long AvailableAtTick { get; init; }

    public required bool ConstraintGenerated { get; init; }

    public string? ConstraintId { get; init; }

    public InteractionTimeWindow? DerivedPastWindow { get; init; }

    public required ObservationConstraintDerivationFailureReason FailureReason { get; init; }

    public required string RuleId { get; init; }

    public IReadOnlyList<string> PublicInputIdsUsed { get; init; } = [];

    public required PastStateEvidenceUsageRole UsageRole { get; init; }

    public required string SourceGroupId { get; init; }

    public IReadOnlyList<string> SourceAncestryIds { get; init; } = [];

    public IReadOnlyList<string> CandidateIds { get; init; } = [];

    public IReadOnlyList<string> CandidateRegionIds { get; init; } = [];

    public required int CandidateCount { get; init; }

    public required bool ContainsTruth { get; init; }

    public required bool FalseExclusion { get; init; }

    public required int CandidateReductionCountVsUnknown { get; init; }

    public required decimal CandidateReductionFractionVsUnknown { get; init; }

    public required int CandidateGenerationWork { get; init; }

    public required int EvidenceRecordsInspected { get; init; }

    public required int SourceGroupsInspected { get; init; }

    public required int GeneratedConstraintSourceGroupCount { get; init; }
}

public sealed record TimeResolutionPairedMonotonicityAudit
{
    public required int Seed { get; init; }

    public required string MorePreciseProfileId { get; init; }

    public required string CoarserProfileId { get; init; }

    public InteractionTimeWindow? MorePreciseDerivedWindow { get; init; }

    public InteractionTimeWindow? CoarserDerivedWindow { get; init; }

    public required bool DerivedWindowIsSubset { get; init; }

    public required bool DerivedWindowStrictlyExpanded { get; init; }

    public IReadOnlyList<string> MorePreciseCandidateIds { get; init; } = [];

    public IReadOnlyList<string> CoarserCandidateIds { get; init; } = [];

    public required bool CandidateSetIsSubset { get; init; }

    public required bool CandidateSetsEqual { get; init; }

    public required bool ConstraintWidenedButCandidateUnchanged { get; init; }
}

public sealed record TimeResolutionPairedWorldRun
{
    public required int Seed { get; init; }

    public required string GeneratorId { get; init; }

    public required string PublicWorldId { get; init; }

    public required string TruthWorldId { get; init; }

    public required int CandidateCount { get; init; }

    public required long LaterEventTick { get; init; }

    public required PublicDurationBound DurationBounds { get; init; }

    public required int DurationWidthTicks { get; init; }

    public required long ActualTruthDurationTicks { get; init; }

    public required long CandidateTickMinimum { get; init; }

    public required long CandidateTickMaximum { get; init; }

    public required long AvailableAtTick { get; init; }

    public required long EvidenceAvailabilityCutoff { get; init; }

    public required string PublicDependencyId { get; init; }

    public required string PublicDependencyInputId { get; init; }

    public required string DurationSourceId { get; init; }

    public required string TruthCandidateKey { get; init; }

    public required string TruthCandidateId { get; init; }

    public required string TruthRegionId { get; init; }

    public required long TruthTick { get; init; }

    public IReadOnlyList<TimeResolutionPairedCandidateStateRun> CandidateStates { get; init; } = [];

    public IReadOnlyList<TimeResolutionPairedProfileRun> Profiles { get; init; } = [];

    public IReadOnlyList<TimeResolutionPairedMonotonicityAudit> MonotonicityAudits { get; init; } = [];

    public required bool WorldValid { get; init; }
}

public sealed record TimeResolutionPairedProfileAggregate
{
    public required string ProfileId { get; init; }

    public required int InformationOrder { get; init; }

    public required int SeedCount { get; init; }

    public required decimal MeanCandidateCount { get; init; }

    public required decimal MedianCandidateCount { get; init; }

    public required int MinimumCandidateCount { get; init; }

    public required int MaximumCandidateCount { get; init; }

    public required decimal ContainmentRate { get; init; }

    public required int FalseExclusionCount { get; init; }

    public required decimal MeanCandidateReductionCountVsUnknown { get; init; }

    public required decimal MeanCandidateReductionFractionVsUnknown { get; init; }

    public required decimal MeanCandidateGenerationWork { get; init; }
}

public sealed record DurationWidthFrequency
{
    public required int WidthTicks { get; init; }

    public required int WorldCount { get; init; }
}

public sealed record TimeResolutionDurationSummary
{
    public required long MinimumDurationMinimumTicks { get; init; }

    public required long MaximumDurationMinimumTicks { get; init; }

    public required long MinimumDurationMaximumTicks { get; init; }

    public required long MaximumDurationMaximumTicks { get; init; }

    public required int MinimumWidthTicks { get; init; }

    public required int MaximumWidthTicks { get; init; }

    public required decimal MeanWidthTicks { get; init; }

    public IReadOnlyList<DurationWidthFrequency> WidthFrequencies { get; init; } = [];
}

public sealed record TimeResolutionPairedSummary
{
    public required int WorldCount { get; init; }

    public required int PairedProfileRunCount { get; init; }

    public required int DerivedWindowMonotonicityViolationCount { get; init; }

    public required int CandidateSetMonotonicityViolationCount { get; init; }

    public required int FalseExclusionCount { get; init; }

    public required int ExactLessThanMediumSeedCount { get; init; }

    public required int MediumLessThanBroadSeedCount { get; init; }

    public required int BroadLessThanUnknownSeedCount { get; init; }

    public required int FiniteProfilesAllEqualSeedCount { get; init; }

    public required int FiniteDerivedWindowEqualitySeedCount { get; init; }

    public required int ConstraintWidenedButCandidateUnchangedSeedCount { get; init; }

    public required int BroadUnknownSaturationSeedCount { get; init; }

    public IReadOnlyList<int> FiniteProfilesAllEqualSeeds { get; init; } = [];

    public IReadOnlyList<int> ConstraintWidenedButCandidateUnchangedSeeds { get; init; } = [];

    public IReadOnlyList<int> BroadUnknownSaturationSeeds { get; init; } = [];
}

public sealed record TimeResolutionPairedManifest
{
    public int OutputSchemaVersion { get; init; } = 1;

    public required string ExperimentId { get; init; }

    public required string ScenarioId { get; init; }

    public required string GeneratorId { get; init; }

    public required string RuleId { get; init; }

    public required string ExperimentContentSha256 { get; init; }

    public required string SourceCommitSha { get; init; }

    public required bool DirtyWorkingTree { get; init; }

    public IReadOnlyList<int> Seeds { get; init; } = [];

    public IReadOnlyList<string> ProfileIds { get; init; } = [];

    public required int WorldCount { get; init; }

    public required int PairedProfileRunCount { get; init; }

    public IReadOnlyList<string> OutputFiles { get; init; } = [];
}

public sealed record TimeResolutionPairedEvaluationResult
{
    public required TimeResolutionPairedManifest Manifest { get; init; }

    public required string IntervalFormula { get; init; }

    public required TimeResolutionWorldGeneratorDefinition Generator { get; init; }

    public IReadOnlyList<TimeResolutionPairedProfileDefinition> Profiles { get; init; } = [];

    public IReadOnlyList<TimeResolutionPairedWorldRun> Runs { get; init; } = [];

    public IReadOnlyList<TimeResolutionPairedProfileAggregate> Aggregates { get; init; } = [];

    public required TimeResolutionDurationSummary DurationSummary { get; init; }

    public required TimeResolutionPairedSummary Summary { get; init; }

    public required bool OriginalF8FixtureRetained { get; init; }

    public required bool ExistingS5Changed { get; init; }

    public required string InterpretationBoundary { get; init; }
}

public static class TimeResolutionPairedExperimentLoader
{
    public static TimeResolutionPairedExperimentDefinition Load(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var options = new JsonSerializerOptions(JsonDefaults.Options)
        {
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
        };
        var definition = JsonSerializer.Deserialize<TimeResolutionPairedExperimentDefinition>(
            File.ReadAllText(path),
            options) ?? throw new InvalidDataException($"Experiment '{path}' was empty.");
        if (definition.SchemaVersion != 1)
        {
            throw new InvalidDataException(
                $"Experiment '{definition.Id}' has unsupported schema version {definition.SchemaVersion}.");
        }

        return definition;
    }
}
