using System.Text.Json;
using System.Text.Json.Serialization;
using FootprintSim.Evidence;
using FootprintSim.Inference.Derivation;
using FootprintSim.Inference.PastStates;
using FootprintSim.Interactions;
using FootprintSim.Interactions.CandidateWarps;
using FootprintSim.Serialization;

namespace FootprintSim.Evaluation;

public sealed record ConstraintCompositionFixtureDefinition
{
    public int SchemaVersion { get; init; } = 1;

    public required string Id { get; init; }

    public required string ScenarioId { get; init; }

    public required int Seed { get; init; }

    public required string TargetEntityId { get; init; }

    public required InteractionEntityKind TargetEntityKind { get; init; }

    public required string TargetStateContextId { get; init; }

    public required string CandidateDomainId { get; init; }

    public required long EvidenceAvailabilityCutoff { get; init; }

    public required InteractionRelationKind FixedRelationKind { get; init; }

    public required ConstraintCompositionLocationInputDefinition LocationInput { get; init; }

    public required ConstraintCompositionTimeInputDefinition TimeInput { get; init; }

    public required ConstraintCompositionParticipantInputDefinition ParticipantInput { get; init; }

    public IReadOnlyList<ConstraintCompositionCandidateStateDefinition> CandidateStates { get; init; } = [];

    public required long TruthCandidateStartTick { get; init; }

    public required long TruthCandidateEndTick { get; init; }

    public required string TruthCandidateRegionId { get; init; }

    public IReadOnlyList<ConstraintCompositionProfileDefinition> Profiles { get; init; } = [];
}

public sealed record ConstraintCompositionLocationInputDefinition
{
    public required string SourceEvidenceIdentifier { get; init; }

    public required long AvailableAtTick { get; init; }

    public required EvidenceState EvidenceState { get; init; }

    public required InteractionVisibility Visibility { get; init; }

    public IReadOnlyList<string> SourceAncestryIds { get; init; } = [];

    public IReadOnlyList<string> AllowedRegionIds { get; init; } = [];
}

public sealed record ConstraintCompositionTimeInputDefinition
{
    public required string SourceEvidenceIdentifier { get; init; }

    public required string LaterEventKind { get; init; }

    public required long ObservedStartTick { get; init; }

    public required long ObservedEndTick { get; init; }

    public required long AvailableAtTick { get; init; }

    public required EvidenceState EvidenceState { get; init; }

    public IReadOnlyList<string> SourceAncestryIds { get; init; } = [];

    public required BoundedDurationDependencyDefinition PublicDependency { get; init; }
}

public sealed record ConstraintCompositionParticipantInputDefinition
{
    public required string EncounterSourceEvidenceIdentifier { get; init; }

    public required long EncounterStartTick { get; init; }

    public required long EncounterEndTick { get; init; }

    public required long AvailableAtTick { get; init; }

    public required EvidenceState EvidenceState { get; init; }

    public IReadOnlyList<string> SourceAncestryIds { get; init; } = [];

    public IReadOnlyList<string> PossibleParticipantIds { get; init; } = [];

    public IReadOnlyList<ParticipantLocationEvidenceDefinition> ParticipantLocationEvidence { get; init; } = [];
}

public sealed record ConstraintCompositionCandidateStateDefinition
{
    public required long StartTick { get; init; }

    public required long EndTick { get; init; }

    public required CandidateWarpTemporalKind TemporalKind { get; init; }

    public IReadOnlyList<string> RegionIds { get; init; } = [];
}

public sealed record ConstraintCompositionProfileDefinition
{
    public required string Id { get; init; }

    public required int Order { get; init; }

    public required bool IncludeLocation { get; init; }

    public required bool IncludeTime { get; init; }

    public required bool IncludeParticipant { get; init; }
}

public sealed record ConstraintCompositionFixtureEvaluationRequest
{
    public required ConstraintCompositionFixtureDefinition Fixture { get; init; }

    public required string FixtureContentSha256 { get; init; }

    public required string SourceCommitSha { get; init; }

    public required bool DirtyWorkingTree { get; init; }
}

public sealed record ConstraintCompositionFixtureRun
{
    public required string ProfileId { get; init; }

    public required int Order { get; init; }

    public required bool IncludesLocation { get; init; }

    public required bool IncludesTime { get; init; }

    public required bool IncludesParticipant { get; init; }

    public required string CompositionId { get; init; }

    public required CandidateConstraintCompositionStatus Status { get; init; }

    public required int InitialCandidateCount { get; init; }

    public IReadOnlyList<ObservationDerivedHistoricalCandidate> Candidates { get; init; } = [];

    public IReadOnlyList<string> CandidateIds { get; init; } = [];

    public required int CandidateCount { get; init; }

    public required string TruthCandidateId { get; init; }

    public required bool ContainsTruth { get; init; }

    public required bool FalseExclusion { get; init; }

    public required int ReductionVsNoneCount { get; init; }

    public required decimal ReductionVsNoneFraction { get; init; }

    public required int CandidateGenerationWork { get; init; }

    public required CandidateGenerationConstraintCompositionAudit CompositionAudit { get; init; }
}

public sealed record ConstraintCompositionIntersectionAudit
{
    public required string ProfileId { get; init; }

    public IReadOnlyList<string> ComponentProfileIds { get; init; } = [];

    public IReadOnlyList<string> ExpectedCandidateIds { get; init; } = [];

    public IReadOnlyList<string> ActualCandidateIds { get; init; } = [];

    public required bool MatchesIntersection { get; init; }
}

public sealed record ConstraintCompositionFixtureManifest
{
    public int OutputSchemaVersion { get; init; } = 1;

    public required string FixtureId { get; init; }

    public required string ScenarioId { get; init; }

    public required string FixtureContentSha256 { get; init; }

    public required string SourceCommitSha { get; init; }

    public required bool DirtyWorkingTree { get; init; }

    public required string TimeRuleId { get; init; }

    public required string ParticipantRuleId { get; init; }

    public required string LocationInputSemanticsId { get; init; }

    public required InteractionRelationKind FixedRelationKind { get; init; }

    public IReadOnlyList<string> ProfileIds { get; init; } = [];

    public required int ProfileRunCount { get; init; }

    public required bool TruthUsedForComposition { get; init; }

    public required bool InputOrderIndependent { get; init; }
}

public sealed record ConstraintCompositionFixtureEvaluationResult
{
    public required ConstraintCompositionFixtureManifest Manifest { get; init; }

    public required string CompositionSemantics { get; init; }

    public required CandidateGenerationConstraintComponent LocationComponent { get; init; }

    public required CandidateGenerationConstraintComponent TimeComponent { get; init; }

    public required CandidateGenerationConstraintComponent ParticipantComponent { get; init; }

    public IReadOnlyList<ObservationDerivedHistoricalCandidate> CandidateUniverse { get; init; } = [];

    public IReadOnlyList<ConstraintCompositionFixtureRun> Runs { get; init; } = [];

    public IReadOnlyList<ConstraintCompositionIntersectionAudit> IntersectionAudits { get; init; } = [];

    public required int IntersectionViolationCount { get; init; }

    public required int ContainmentCount { get; init; }

    public required int FalseExclusionCount { get; init; }

    public required bool ExistingS5Changed { get; init; }

    public required string InterpretationBoundary { get; init; }
}

public static class ConstraintCompositionFixtureLoader
{
    public static ConstraintCompositionFixtureDefinition Load(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var options = new JsonSerializerOptions(JsonDefaults.Options)
        {
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
        };
        var definition = JsonSerializer.Deserialize<ConstraintCompositionFixtureDefinition>(
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
