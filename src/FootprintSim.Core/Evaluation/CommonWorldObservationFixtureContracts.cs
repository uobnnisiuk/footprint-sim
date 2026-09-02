using System.Text.Json;
using System.Text.Json.Serialization;
using FootprintSim.Evidence;
using FootprintSim.Inference.Derivation;
using FootprintSim.Inference.PastStates;
using FootprintSim.Interactions;
using FootprintSim.Interactions.CandidateWarps;
using FootprintSim.Serialization;

namespace FootprintSim.Evaluation;

public enum CommonWorldObservationAxis
{
    Location,
    Time,
    Participant
}

public sealed record CommonWorldObservationFixtureDefinition
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

    public required CommonWorldLocationObservationDefinition LocationObservation { get; init; }

    public required CommonWorldTimeObservationDefinition TimeObservation { get; init; }

    public required CommonWorldParticipantObservationDefinition ParticipantObservation { get; init; }

    public IReadOnlyList<CommonWorldCandidateStateDefinition> CandidateStates { get; init; } = [];

    public required string TruthOtherParticipantId { get; init; }

    public required long TruthCandidateStartTick { get; init; }

    public required long TruthCandidateEndTick { get; init; }

    public required string TruthCandidateRegionId { get; init; }

    public IReadOnlyList<CommonWorldProfileDefinition> Profiles { get; init; } = [];
}

public sealed record CommonWorldLocationObservationDefinition
{
    public required string SourceEvidenceIdentifier { get; init; }

    public IReadOnlyList<string> ObservedRegionIds { get; init; } = [];

    public required long AvailableAtTick { get; init; }

    public required EvidenceState EvidenceState { get; init; }

    public required InteractionVisibility Visibility { get; init; }

    public IReadOnlyList<string> SourceAncestryIds { get; init; } = [];
}

public sealed record CommonWorldTimeObservationDefinition
{
    public required string SourceEvidenceIdentifier { get; init; }

    public required string LaterEventKind { get; init; }

    public required long ObservedStartTick { get; init; }

    public required long ObservedEndTick { get; init; }

    public required long AvailableAtTick { get; init; }

    public required EvidenceState EvidenceState { get; init; }

    public required InteractionVisibility Visibility { get; init; }

    public IReadOnlyList<string> SourceAncestryIds { get; init; } = [];

    public required BoundedDurationDependencyDefinition PublicDependency { get; init; }
}

public sealed record CommonWorldParticipantObservationDefinition
{
    public required string EncounterSourceEvidenceIdentifier { get; init; }

    public required long EncounterStartTick { get; init; }

    public required long EncounterEndTick { get; init; }

    public required long AvailableAtTick { get; init; }

    public required EvidenceState EvidenceState { get; init; }

    public required InteractionVisibility Visibility { get; init; }

    public IReadOnlyList<string> SourceAncestryIds { get; init; } = [];

    public IReadOnlyList<string> PossibleParticipantIds { get; init; } = [];

    public IReadOnlyList<ParticipantLocationEvidenceDefinition> ParticipantLocationEvidence { get; init; } = [];
}

public sealed record CommonWorldCandidateStateDefinition
{
    public required long StartTick { get; init; }

    public required long EndTick { get; init; }

    public required CandidateWarpTemporalKind TemporalKind { get; init; }

    public IReadOnlyList<string> RegionIds { get; init; } = [];
}

public sealed record CommonWorldProfileDefinition
{
    public required string Id { get; init; }

    public required int Order { get; init; }

    public required bool SelectLocation { get; init; }

    public required bool SelectTime { get; init; }

    public required bool SelectParticipant { get; init; }
}

public sealed record CommonWorldObservationFixtureEvaluationRequest
{
    public required CommonWorldObservationFixtureDefinition Fixture { get; init; }

    public required string FixtureContentSha256 { get; init; }

    public required string SourceCommitSha { get; init; }

    public required bool DirtyWorkingTree { get; init; }
}

public sealed record CommonWorldSourceGraphNode
{
    public required string SourceGroupId { get; init; }

    public required string SourceKind { get; init; }

    public IReadOnlyList<string> FieldIds { get; init; } = [];

    public IReadOnlyList<string> PublicInputIds { get; init; } = [];
}

public sealed record CommonWorldDerivationAudit
{
    public required LocationConstraintDerivationAudit Location { get; init; }

    public required ObservationConstraintDerivationAudit Time { get; init; }

    public required ParticipantConstraintDerivationAudit Participant { get; init; }

    public required RelationKindKnowledge RelationKnowledge { get; init; }

    public required InteractionRelationKind ExactRelationKind { get; init; }

    public required bool AllObservationsExistInEveryProfile { get; init; }

    public required bool TruthUsedForDerivation { get; init; }
}

public sealed record CommonWorldProfileRun
{
    public required string ProfileId { get; init; }

    public required int Order { get; init; }

    public IReadOnlyList<CommonWorldObservationAxis> ActiveAxes { get; init; } = [];

    public IReadOnlyList<CommonWorldObservationAxis> OmittedAxes { get; init; } = [];

    public required bool OmittedAxesMeanNotSelected { get; init; }

    public IReadOnlyList<string> ActiveConstraintIds { get; init; } = [];

    public IReadOnlyList<string> ObservationIdsPresent { get; init; } = [];

    public required string CandidateUniverseId { get; init; }

    public IReadOnlyList<string> CandidateUniverseIds { get; init; } = [];

    public required string CompositionId { get; init; }

    public required CandidateConstraintCompositionStatus Status { get; init; }

    public IReadOnlyList<ObservationDerivedHistoricalCandidate> Candidates { get; init; } = [];

    public IReadOnlyList<string> CandidateIds { get; init; } = [];

    public required int CandidateCount { get; init; }

    public required int ReductionVsNoneCount { get; init; }

    public required decimal ReductionVsNoneFraction { get; init; }

    public required string TruthCandidateId { get; init; }

    public required bool ContainsTruth { get; init; }

    public required bool FalseExclusion { get; init; }

    public IReadOnlyList<string> SelectedSupportingSourceGroupIds { get; init; } = [];

    public required int SelectedSupportingSourceGroupCount { get; init; }

    public required int SelectedFieldCount { get; init; }

    public required int ActiveConstraintCount { get; init; }

    public required int CandidateGenerationWork { get; init; }

    public required CandidateGenerationConstraintCompositionAudit CompositionAudit { get; init; }
}

public sealed record CommonWorldFunctionalIndependenceAudit
{
    public IReadOnlyList<string> LocationPublicInputIds { get; init; } = [];

    public IReadOnlyList<string> TimePublicInputIds { get; init; } = [];

    public IReadOnlyList<string> ParticipantPublicInputIds { get; init; } = [];

    public required bool LocationReadsOnlyLocationInputs { get; init; }

    public required bool TimeReadsOnlyTimeInputs { get; init; }

    public required bool ParticipantReadsOnlyParticipantInputs { get; init; }
}

public sealed record CommonWorldObservationFixtureManifest
{
    public int OutputSchemaVersion { get; init; } = 1;

    public required string FixtureId { get; init; }

    public required string ScenarioId { get; init; }

    public required string FixtureContentSha256 { get; init; }

    public required string SourceCommitSha { get; init; }

    public required bool DirtyWorkingTree { get; init; }

    public required string LocationRuleId { get; init; }

    public required string TimeRuleId { get; init; }

    public required string ParticipantRuleId { get; init; }

    public required InteractionRelationKind FixedRelationKind { get; init; }

    public IReadOnlyList<string> ProfileIds { get; init; } = [];

    public required int ProfileRunCount { get; init; }

    public required bool TruthUsedForDerivationOrComposition { get; init; }

    public required bool InputOrderIndependent { get; init; }

    public required bool CandidateUniverseInvariant { get; init; }

    public required bool RelationInvariant { get; init; }
}

public sealed record CommonWorldObservationFixtureEvaluationResult
{
    public required CommonWorldObservationFixtureManifest Manifest { get; init; }

    public required string TargetEntityId { get; init; }

    public required string TruthOtherParticipantId { get; init; }

    public required string TruthCandidateId { get; init; }

    public required string CandidateUniverseId { get; init; }

    public IReadOnlyList<ObservationDerivedHistoricalCandidate> CandidateUniverse { get; init; } = [];

    public required PublicObservedRegionObservation LocationObservation { get; init; }

    public required PublicEventObservation TimeObservation { get; init; }

    public required PublicBoundedDurationDependency PublicDependency { get; init; }

    public required PublicEncounterParticipantObservation ParticipantObservation { get; init; }

    public IReadOnlyList<PublicParticipantLocationEvidence> ParticipantLocationEvidence { get; init; } = [];

    public required LocationObservationDerivedConstraint LocationConstraint { get; init; }

    public required ObservationDerivedConstraint TimeConstraint { get; init; }

    public required ObservationDerivedConstraint ParticipantConstraint { get; init; }

    public required CommonWorldDerivationAudit DerivationAudit { get; init; }

    public IReadOnlyList<CommonWorldSourceGraphNode> SourceGraph { get; init; } = [];

    public required int WorldFieldCount { get; init; }

    public required int WorldIndependentSourceGroupCount { get; init; }

    public required CommonWorldFunctionalIndependenceAudit FunctionalIndependence { get; init; }

    public IReadOnlyList<CommonWorldProfileRun> Runs { get; init; } = [];

    public IReadOnlyList<ConstraintCompositionIntersectionAudit> IntersectionAudits { get; init; } = [];

    public required int IntersectionViolationCount { get; init; }

    public required int ContainmentCount { get; init; }

    public required int FalseExclusionCount { get; init; }

    public required bool ExistingS5Changed { get; init; }

    public required string InterpretationBoundary { get; init; }
}

public static class CommonWorldObservationFixtureLoader
{
    public static CommonWorldObservationFixtureDefinition Load(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var options = new JsonSerializerOptions(JsonDefaults.Options)
        {
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
        };
        var definition = JsonSerializer.Deserialize<CommonWorldObservationFixtureDefinition>(
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
