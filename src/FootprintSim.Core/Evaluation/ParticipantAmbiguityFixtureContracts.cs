using System.Text.Json;
using System.Text.Json.Serialization;
using FootprintSim.Evidence;
using FootprintSim.Inference.Derivation;
using FootprintSim.Inference.PastStates;
using FootprintSim.Interactions;
using FootprintSim.Interactions.CandidateWarps;
using FootprintSim.Serialization;

namespace FootprintSim.Evaluation;

public sealed record ParticipantAmbiguityFixtureDefinition
{
    public int SchemaVersion { get; init; } = 1;

    public required string Id { get; init; }

    public required string ScenarioId { get; init; }

    public required int Seed { get; init; }

    public required string TargetEntityId { get; init; }

    public required InteractionEntityKind TargetEntityKind { get; init; }

    public required string EncounterSourceEvidenceIdentifier { get; init; }

    public required InteractionRelationKind RelationKind { get; init; }

    public required long EncounterStartTick { get; init; }

    public required long EncounterEndTick { get; init; }

    public required long EncounterAvailableAtTick { get; init; }

    public required long EvidenceAvailabilityCutoff { get; init; }

    public required EvidenceState EncounterEvidenceState { get; init; }

    public IReadOnlyList<string> EncounterSourceAncestryIds { get; init; } = [];

    public IReadOnlyList<ParticipantLocationEvidenceDefinition> ParticipantLocationEvidence { get; init; } = [];

    public IReadOnlyList<ParticipantAmbiguityCandidateStateDefinition> CandidateStates { get; init; } = [];

    public required string TruthOtherParticipantId { get; init; }

    public required long TruthCandidateStartTick { get; init; }

    public required long TruthCandidateEndTick { get; init; }

    public required string TruthCandidateRegionId { get; init; }

    public IReadOnlyList<ParticipantAmbiguityProfileDefinition> Profiles { get; init; } = [];
}

public sealed record ParticipantLocationEvidenceDefinition
{
    public required string SourceEvidenceIdentifier { get; init; }

    public required string ParticipantId { get; init; }

    public required InteractionEntityKind EntityKind { get; init; }

    public required long ApplicableStartTick { get; init; }

    public required long ApplicableEndTick { get; init; }

    public IReadOnlyList<string> AllowedRegionIds { get; init; } = [];

    public required long AvailableAtTick { get; init; }

    public required EvidenceState EvidenceState { get; init; }

    public required InteractionVisibility Visibility { get; init; }

    public required PastStateEvidenceUsageRole UsageRole { get; init; }

    public IReadOnlyList<string> SourceAncestryIds { get; init; } = [];
}

public sealed record ParticipantAmbiguityCandidateStateDefinition
{
    public required long StartTick { get; init; }

    public required long EndTick { get; init; }

    public required CandidateWarpTemporalKind TemporalKind { get; init; }

    public IReadOnlyList<string> RegionIds { get; init; } = [];
}

public sealed record ParticipantAmbiguityProfileDefinition
{
    public required string Id { get; init; }

    public required int InformationOrder { get; init; }

    public required string Description { get; init; }

    public required ParticipantIdentityKnowledge IdentityKnowledge { get; init; }

    public required InteractionEntityKind EntityKind { get; init; }

    public IReadOnlyList<string> PossibleParticipantIds { get; init; } = [];
}

public sealed record ParticipantAmbiguityFixtureEvaluationRequest
{
    public required ParticipantAmbiguityFixtureDefinition Fixture { get; init; }

    public required string FixtureContentSha256 { get; init; }

    public required string SourceCommitSha { get; init; }

    public required bool DirtyWorkingTree { get; init; }
}

public sealed record ParticipantAmbiguityFixtureRun
{
    public required string ProfileId { get; init; }

    public required int InformationOrder { get; init; }

    public required bool EncounterObservationPresent { get; init; }

    public required string SourceObservationId { get; init; }

    public required string EncounterSourceGroupId { get; init; }

    public required ParticipantIdentityKnowledge IdentityKnowledge { get; init; }

    public required InteractionEntityKind ParticipantEntityKind { get; init; }

    public IReadOnlyList<string> PossibleParticipantIds { get; init; } = [];

    public required bool ConstraintGenerated { get; init; }

    public string? ConstraintId { get; init; }

    public IReadOnlyList<string> DerivedAllowedRegionIds { get; init; } = [];

    public required ParticipantConstraintDerivationAudit DerivationAudit { get; init; }

    public required int EncounterSourceGroupCount { get; init; }

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

    public required int ParticipantAlternativesEvaluated { get; init; }

    public required int ParticipantLocationEvidenceRecordsInspected { get; init; }

    public required int CandidateGenerationWork { get; init; }

    public required int ReconstructionEvidenceRecordsInspected { get; init; }

    public required int ReconstructionSourceGroupsInspected { get; init; }
}

public sealed record ParticipantAmbiguityMonotonicityAudit
{
    public required string MorePreciseProfileId { get; init; }

    public required string CoarserProfileId { get; init; }

    public required bool ParticipantInformationIsSubset { get; init; }

    public required bool DerivedRegionSetIsSubset { get; init; }

    public required bool CandidateSetIsSubset { get; init; }

    public IReadOnlyList<string> MorePreciseCandidateIds { get; init; } = [];

    public IReadOnlyList<string> CoarserCandidateIds { get; init; } = [];
}

public sealed record ParticipantAmbiguityFixtureManifest
{
    public int OutputSchemaVersion { get; init; } = 1;

    public required string FixtureId { get; init; }

    public required string ScenarioId { get; init; }

    public required string FixtureContentSha256 { get; init; }

    public required string SourceCommitSha { get; init; }

    public required bool DirtyWorkingTree { get; init; }

    public required string RuleId { get; init; }

    public IReadOnlyList<string> ProfileIds { get; init; } = [];

    public required int ProfileRunCount { get; init; }

    public required bool TruthUsedForDerivation { get; init; }
}

public sealed record ParticipantAmbiguityFixtureEvaluationResult
{
    public required ParticipantAmbiguityFixtureManifest Manifest { get; init; }

    public required string SemanticRule { get; init; }

    public required string ConservativeUnionFormula { get; init; }

    public required string TargetEntityId { get; init; }

    public required InteractionRelationKind RelationKind { get; init; }

    public required InteractionTimeWindow EncounterTimeWindow { get; init; }

    public IReadOnlyList<PublicParticipantLocationEvidence> PublicParticipantLocationEvidence { get; init; } = [];

    public required string TruthOtherParticipantId { get; init; }

    public required string TruthCandidateId { get; init; }

    public IReadOnlyList<ParticipantAmbiguityProfileDefinition> Profiles { get; init; } = [];

    public IReadOnlyList<ParticipantAmbiguityFixtureRun> Runs { get; init; } = [];

    public IReadOnlyList<ParticipantAmbiguityMonotonicityAudit> MonotonicityAudits { get; init; } = [];

    public required int MonotonicityViolationCount { get; init; }

    public required bool ExistingS5Changed { get; init; }

    public required string InterpretationBoundary { get; init; }
}

public static class ParticipantAmbiguityFixtureLoader
{
    public static ParticipantAmbiguityFixtureDefinition Load(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var options = new JsonSerializerOptions(JsonDefaults.Options)
        {
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
        };
        var definition = JsonSerializer.Deserialize<ParticipantAmbiguityFixtureDefinition>(
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
