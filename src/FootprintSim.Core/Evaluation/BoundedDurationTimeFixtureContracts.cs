using System.Text.Json;
using System.Text.Json.Serialization;
using FootprintSim.Evidence;
using FootprintSim.Inference.Derivation;
using FootprintSim.Interactions;
using FootprintSim.Interactions.CandidateWarps;
using FootprintSim.Serialization;

namespace FootprintSim.Evaluation;

public sealed record BoundedDurationTimeFixtureDefinition
{
    public int SchemaVersion { get; init; } = 1;

    public required string Id { get; init; }

    public required string ScenarioId { get; init; }

    public required int Seed { get; init; }

    public required string TargetEntityId { get; init; }

    public required string SourceEvidenceIdentifier { get; init; }

    public required string LaterEventKind { get; init; }

    public required long AvailableAtTick { get; init; }

    public required long EvidenceAvailabilityCutoff { get; init; }

    public required EvidenceState EvidenceState { get; init; }

    public IReadOnlyList<string> SourceAncestryIds { get; init; } = [];

    public required BoundedDurationDependencyDefinition PublicDependency { get; init; }

    public IReadOnlyList<BoundedDurationCandidateStateDefinition> CandidateStates { get; init; } = [];

    public required string TruthRegionId { get; init; }

    public IReadOnlyList<BoundedDurationTimeProfileDefinition> Profiles { get; init; } = [];
}

public sealed record BoundedDurationDependencyDefinition
{
    public required string Id { get; init; }

    public required string PredecessorStateKind { get; init; }

    public required string LaterEventKind { get; init; }

    public required string TargetEntityId { get; init; }

    public required long MinimumDurationTicks { get; init; }

    public required long MaximumDurationTicks { get; init; }

    public required string DurationSourceId { get; init; }

    public IReadOnlyList<string> AllowedPredecessorRegionIds { get; init; } = [];
}

public sealed record BoundedDurationCandidateStateDefinition
{
    public required long StartTick { get; init; }

    public required long EndTick { get; init; }

    public required CandidateWarpTemporalKind TemporalKind { get; init; }

    public IReadOnlyList<string> RegionIds { get; init; } = [];
}

public sealed record BoundedDurationTimeProfileDefinition
{
    public required string Id { get; init; }

    public required int InformationOrder { get; init; }

    public required string Description { get; init; }

    public long? ObservedStartTick { get; init; }

    public long? ObservedEndTick { get; init; }

    public required string BoundaryRationale { get; init; }
}

public sealed record BoundedDurationTimeFixtureEvaluationRequest
{
    public required BoundedDurationTimeFixtureDefinition Fixture { get; init; }

    public required string FixtureContentSha256 { get; init; }

    public required string SourceCommitSha { get; init; }

    public required bool DirtyWorkingTree { get; init; }
}

public sealed record BoundedDurationTimeFixtureRun
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

    public required ObservationConstraintDerivationAudit DerivationAudit { get; init; }

    public required int ObservationSourceGroupCount { get; init; }

    public required int GeneratedConstraintSourceGroupCount { get; init; }

    public required int InitialCandidateCount { get; init; }

    public IReadOnlyList<ObservationDerivedHistoricalCandidate> Candidates { get; init; } = [];

    public required int CandidateCount { get; init; }

    public required bool ContainsTruth { get; init; }

    public required bool FalseExclusion { get; init; }

    public required decimal Reduction { get; init; }

    public required int CandidateGenerationWork { get; init; }

    public required int EvidenceRecordsInspected { get; init; }

    public required int SourceGroupsInspected { get; init; }
}

public sealed record BoundedDurationTimeMonotonicityAudit
{
    public required string MorePreciseProfileId { get; init; }

    public required string CoarserProfileId { get; init; }

    public IReadOnlyList<string> MorePreciseCandidateIds { get; init; } = [];

    public IReadOnlyList<string> CoarserCandidateIds { get; init; } = [];

    public required bool DerivedWindowIsSubset { get; init; }

    public required bool CandidateSetIsSubset { get; init; }
}

public sealed record BoundedDurationTimeFixtureManifest
{
    public int OutputSchemaVersion { get; init; } = 1;

    public required string FixtureId { get; init; }

    public required string ScenarioId { get; init; }

    public required string FixtureContentSha256 { get; init; }

    public required string SourceCommitSha { get; init; }

    public required bool DirtyWorkingTree { get; init; }

    public IReadOnlyList<string> ProfileIds { get; init; } = [];

    public required int ProfileRunCount { get; init; }
}

public sealed record BoundedDurationTimeFixtureEvaluationResult
{
    public required BoundedDurationTimeFixtureManifest Manifest { get; init; }

    public required string IntervalFormula { get; init; }

    public required PublicBoundedDurationDependency PublicDependency { get; init; }

    public IReadOnlyList<BoundedDurationTimeProfileDefinition> Profiles { get; init; } = [];

    public IReadOnlyList<BoundedDurationTimeFixtureRun> Runs { get; init; } = [];

    public IReadOnlyList<BoundedDurationTimeMonotonicityAudit> MonotonicityAudits { get; init; } = [];

    public required int MonotonicityViolationCount { get; init; }

    public required bool ExistingS5Changed { get; init; }

    public required string InterpretationBoundary { get; init; }
}

public static class BoundedDurationTimeFixtureLoader
{
    public static BoundedDurationTimeFixtureDefinition Load(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var options = new JsonSerializerOptions(JsonDefaults.Options)
        {
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
        };
        var definition = JsonSerializer.Deserialize<BoundedDurationTimeFixtureDefinition>(
            File.ReadAllText(path),
            options) ?? throw new InvalidDataException($"Fixture '{path}' was empty.");
        if (definition.SchemaVersion != 1)
        {
            throw new InvalidDataException(
                $"Fixture '{definition.Id}' has unsupported schema version {definition.SchemaVersion}.");
        }

        return definition;
    }
}
