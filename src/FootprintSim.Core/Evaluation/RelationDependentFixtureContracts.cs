using System.Text.Json;
using System.Text.Json.Serialization;
using FootprintSim.Evidence;
using FootprintSim.Inference.Derivation;
using FootprintSim.Inference.PastStates;
using FootprintSim.Interactions;
using FootprintSim.Serialization;

namespace FootprintSim.Evaluation;

public sealed record RelationDependentFixtureDefinition
{
    public int SchemaVersion { get; init; } = 1;

    public required string Id { get; init; }

    public required string ScenarioId { get; init; }

    public required int Seed { get; init; }

    public required string TargetEntityId { get; init; }

    public required InteractionEntityKind TargetEntityKind { get; init; }

    public required string OtherParticipantId { get; init; }

    public required InteractionEntityKind OtherParticipantEntityKind { get; init; }

    public required string RelationSourceEvidenceIdentifier { get; init; }

    public required long RelationStartTick { get; init; }

    public required long RelationEndTick { get; init; }

    public required long RelationAvailableAtTick { get; init; }

    public required long EvidenceAvailabilityCutoff { get; init; }

    public required EvidenceState RelationEvidenceState { get; init; }

    public IReadOnlyList<string> RelationSourceAncestryIds { get; init; } = [];

    public required ParticipantLocationEvidenceDefinition ParticipantLocationEvidence { get; init; }

    public IReadOnlyList<ParticipantAmbiguityCandidateStateDefinition> CandidateStates { get; init; } = [];

    public required string TruthRelationReference { get; init; }

    public required long TruthCandidateStartTick { get; init; }

    public required long TruthCandidateEndTick { get; init; }

    public required string TruthCandidateRegionId { get; init; }

    public IReadOnlyList<RelationDependentProfileDefinition> Profiles { get; init; } = [];
}

public sealed record RelationDependentProfileDefinition
{
    public required string Id { get; init; }

    public required int InformationOrder { get; init; }

    public required string Description { get; init; }

    public required RelationKindKnowledge RelationKnowledge { get; init; }

    public InteractionRelationKind? ExactRelationKind { get; init; }
}

public sealed record RelationDependentFixtureEvaluationRequest
{
    public required RelationDependentFixtureDefinition Fixture { get; init; }

    public required string FixtureContentSha256 { get; init; }

    public required string SourceCommitSha { get; init; }

    public required bool DirtyWorkingTree { get; init; }
}

public sealed record RelationDependentFixtureRun
{
    public required string ProfileId { get; init; }

    public required int InformationOrder { get; init; }

    public required bool RelationObservationPresent { get; init; }

    public required string SourceObservationId { get; init; }

    public required RelationKindKnowledge RelationKnowledge { get; init; }

    public InteractionRelationKind? ExactRelationKind { get; init; }

    public required bool ConstraintGenerated { get; init; }

    public string? ConstraintId { get; init; }

    public IReadOnlyList<string> DerivedAllowedRegionIds { get; init; } = [];

    public required RelationConstraintDerivationAudit DerivationAudit { get; init; }

    public required int RelationObservationSourceGroupCount { get; init; }

    public required int ParticipantEvidenceSourceGroupCount { get; init; }

    public required int GeneratedConstraintSourceGroupCount { get; init; }

    public required int InitialCandidateCount { get; init; }

    public IReadOnlyList<ObservationDerivedHistoricalCandidate> Candidates { get; init; } = [];

    public IReadOnlyList<string> CandidateIds { get; init; } = [];

    public required int CandidateCount { get; init; }

    public required string TruthCandidateId { get; init; }

    public required bool ContainsTruth { get; init; }

    public required bool FalseExclusion { get; init; }

    public required int CandidateReductionVsUnknownCount { get; init; }

    public required decimal CandidateReductionVsUnknownFraction { get; init; }

    public required int CandidateGenerationWork { get; init; }

    public required int ReconstructionEvidenceRecordsInspected { get; init; }

    public required int ReconstructionSourceGroupsInspected { get; init; }
}

public sealed record RelationDependentMonotonicityAudit
{
    public required string MoreSpecificProfileId { get; init; }

    public required string LessSpecificProfileId { get; init; }

    public required bool ExactRelationInformationPrecedesUnknown { get; init; }

    public required bool CandidateSetIsSubset { get; init; }

    public IReadOnlyList<string> MoreSpecificCandidateIds { get; init; } = [];

    public IReadOnlyList<string> LessSpecificCandidateIds { get; init; } = [];
}

public sealed record RelationDependentFixtureManifest
{
    public int OutputSchemaVersion { get; init; } = 1;

    public required string FixtureId { get; init; }

    public required string ScenarioId { get; init; }

    public required string FixtureContentSha256 { get; init; }

    public required string SourceCommitSha { get; init; }

    public required bool DirtyWorkingTree { get; init; }

    public required string RelationApplicabilityRuleId { get; init; }

    public required string UnderlyingConstraintRuleId { get; init; }

    public IReadOnlyList<string> ProfileIds { get; init; } = [];

    public required int ProfileRunCount { get; init; }

    public required bool TruthUsedForDerivation { get; init; }
}

public sealed record RelationDependentFixtureEvaluationResult
{
    public required RelationDependentFixtureManifest Manifest { get; init; }

    public required string SemanticRule { get; init; }

    public IReadOnlyList<InteractionRelationKind> FormalRelationInventory { get; init; } = [];

    public required bool MeaningfulSecondRelationAvailable { get; init; }

    public required bool RelationTaxonomyAvailable { get; init; }

    public required string TargetEntityId { get; init; }

    public required string OtherParticipantId { get; init; }

    public required InteractionTimeWindow OccurrenceTimeWindow { get; init; }

    public required PublicParticipantLocationEvidence ParticipantLocationEvidence { get; init; }

    public required string TruthRelationReference { get; init; }

    public required string TruthCandidateId { get; init; }

    public IReadOnlyList<RelationDependentProfileDefinition> Profiles { get; init; } = [];

    public IReadOnlyList<RelationDependentFixtureRun> Runs { get; init; } = [];

    public required RelationDependentMonotonicityAudit MonotonicityAudit { get; init; }

    public required int MonotonicityViolationCount { get; init; }

    public required bool ExistingS5Changed { get; init; }

    public required string InterpretationBoundary { get; init; }
}

public static class RelationDependentFixtureLoader
{
    public static RelationDependentFixtureDefinition Load(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var options = new JsonSerializerOptions(JsonDefaults.Options)
        {
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
        };
        var definition = JsonSerializer.Deserialize<RelationDependentFixtureDefinition>(
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
