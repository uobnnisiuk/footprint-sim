using System.Text.Json;
using FootprintSim.Evidence;
using FootprintSim.Interactions;
using FootprintSim.Interactions.CandidateWarps;
using FootprintSim.Interactions.Tracing;
using FootprintSim.Inference.PastStates;

namespace FootprintSim.Telemetry;

public static class TelemetrySchema
{
    public const int Version = 3;
}

public enum TelemetryStreamKind
{
    TruthWorld,
    Evidence,
    Evaluation
}

public enum TelemetryTemporalKind
{
    ExactEventPoint,
    InclusiveStateInterval
}

public enum TelemetryVisibility
{
    PrivateTruth,
    PublicEvidence,
    PrivateEvidence,
    EvaluationContext
}

public sealed record TelemetryTimeWindow
{
    public required long StartTick { get; init; }

    public required long EndTick { get; init; }

    public required TelemetryTemporalKind TemporalKind { get; init; }
}

public sealed record TelemetryEntityReference
{
    public required string EntityId { get; init; }

    public required string EntityKind { get; init; }

    public required string Role { get; init; }
}

public sealed record TelemetryProvenanceReference
{
    public required string Kind { get; init; }

    public required string SourceIdentifier { get; init; }
}

public sealed record WorldEventTelemetryRecord
{
    public int TelemetrySchemaVersion { get; init; } = TelemetrySchema.Version;

    public string RecordType { get; init; } = "worldEvent";

    public required string ScenarioId { get; init; }

    public required int Seed { get; init; }

    public required string EventId { get; init; }

    public required int Sequence { get; init; }

    public required string EventKind { get; init; }

    public string? MovementKind { get; init; }

    public required TelemetryTimeWindow OccurrenceTimeWindow { get; init; }

    public IReadOnlyList<TelemetryEntityReference> Entities { get; init; } = [];

    public IReadOnlyList<string> RegionIds { get; init; } = [];

    public IReadOnlyList<string> CausalReferenceIds { get; init; } = [];

    public TelemetryVisibility Visibility { get; init; } = TelemetryVisibility.PrivateTruth;

    public IReadOnlyList<TelemetryProvenanceReference> Provenance { get; init; } = [];

    public IReadOnlyDictionary<string, JsonElement> Payload { get; init; } =
        new SortedDictionary<string, JsonElement>(StringComparer.Ordinal);
}

public sealed record ObservationTelemetryRecord
{
    public int TelemetrySchemaVersion { get; init; } = TelemetrySchema.Version;

    public string RecordType { get; init; } = "observation";

    public required string ScenarioId { get; init; }

    public required int Seed { get; init; }

    public required string ObservationId { get; init; }

    public required string ObservationKind { get; init; }

    public required TelemetryTimeWindow ObservedTimeWindow { get; init; }

    public required long AvailableAtTick { get; init; }

    public IReadOnlyList<TelemetryEntityReference> ObservedEntities { get; init; } = [];

    public IReadOnlyList<string> ObservedRegionIds { get; init; } = [];

    public required EvidenceState EvidenceState { get; init; }

    public required bool IsPersonEvidence { get; init; }

    public required bool IsDeviceOnlyEvidence { get; init; }

    public TelemetryVisibility Visibility { get; init; } = TelemetryVisibility.PublicEvidence;

    public IReadOnlyList<string> SourceAncestryIds { get; init; } = [];

    public IReadOnlyList<TelemetryProvenanceReference> Provenance { get; init; } = [];

    public IReadOnlyDictionary<string, JsonElement> Payload { get; init; } =
        new SortedDictionary<string, JsonElement>(StringComparer.Ordinal);
}

public sealed record TruthInteractionTelemetryRecord
{
    public int TelemetrySchemaVersion { get; init; } = TelemetrySchema.Version;

    public string RecordType { get; init; } = "truthInteraction";

    public required string ScenarioId { get; init; }

    public required int Seed { get; init; }

    public required string InteractionId { get; init; }

    public required string RelationKind { get; init; }

    public IReadOnlyList<TelemetryEntityReference> Participants { get; init; } = [];

    public required TelemetryTimeWindow OccurrenceTimeWindow { get; init; }

    public IReadOnlyList<string> RegionIds { get; init; } = [];

    public required string SourceEventId { get; init; }

    public IReadOnlyList<string> CausalReferenceIds { get; init; } = [];

    public TelemetryVisibility Visibility { get; init; } = TelemetryVisibility.PrivateTruth;

    public IReadOnlyList<TelemetryProvenanceReference> Provenance { get; init; } = [];
}

public sealed record InteractionObservationTelemetryRecord
{
    public int TelemetrySchemaVersion { get; init; } = TelemetrySchema.Version;

    public string RecordType { get; init; } = "interactionObservation";

    public required string ScenarioId { get; init; }

    public required int Seed { get; init; }

    public required string ObservationId { get; init; }

    public required string SourceEvidenceIdentifier { get; init; }

    public string? ReferencedInteractionId { get; init; }

    public required string ObservedRelationKind { get; init; }

    public IReadOnlyList<TelemetryEntityReference> ObservedParticipants { get; init; } = [];

    public required TelemetryTimeWindow ObservedTimeWindow { get; init; }

    public IReadOnlyList<string> ObservedRegionIds { get; init; } = [];

    public required long AvailableAtTick { get; init; }

    public required EvidenceState EvidenceState { get; init; }

    public required bool IsPersonEvidence { get; init; }

    public required bool IsDeviceOnlyEvidence { get; init; }

    public required TelemetryVisibility Visibility { get; init; }

    public IReadOnlyList<string> SourceAncestryIds { get; init; } = [];

    public IReadOnlyList<TelemetryProvenanceReference> Provenance { get; init; } = [];
}

public sealed record ConstraintTelemetryRecord
{
    public int TelemetrySchemaVersion { get; init; } = TelemetrySchema.Version;

    public string RecordType { get; init; } = "crossThreadConstraint";

    public required string ScenarioId { get; init; }

    public required int Seed { get; init; }

    public required string ConstraintId { get; init; }

    public required string SourceObservationId { get; init; }

    public required string SourceEvidenceIdentifier { get; init; }

    public required string TargetEntityId { get; init; }

    public required string SourceRelationKind { get; init; }

    public IReadOnlyList<TelemetryEntityReference> SourceParticipants { get; init; } = [];

    public required TelemetryTimeWindow SourceObservedTimeWindow { get; init; }

    public required TelemetryTimeWindow ConstrainedTimeWindow { get; init; }

    public IReadOnlyList<string> AllowedRegionIds { get; init; } = [];

    public required long AvailableAtTick { get; init; }

    public required EvidenceState EvidenceState { get; init; }

    public required bool IsPersonEvidence { get; init; }

    public required bool IsDeviceOnlyEvidence { get; init; }

    public required string DerivationKind { get; init; }

    public required TelemetryVisibility Visibility { get; init; }

    public IReadOnlyList<string> SourceAncestryIds { get; init; } = [];

    public IReadOnlyList<TelemetryProvenanceReference> Provenance { get; init; } = [];
}

public sealed record ConstraintApplicationTelemetryRecord
{
    public int TelemetrySchemaVersion { get; init; } = TelemetrySchema.Version;

    public string RecordType { get; init; } = "constraintApplication";

    public required string ScenarioId { get; init; }

    public required int Seed { get; init; }

    public required string StrategyId { get; init; }

    public required string ApplicationId { get; init; }

    public required string CandidateWarpId { get; init; }

    public required string ConstraintId { get; init; }

    public required string SourceObservationId { get; init; }

    public required string RelationKind { get; init; }

    public required string TargetEntityId { get; init; }

    public required long ApplicationTick { get; init; }

    public required string QueryAccessContextId { get; init; }

    public required TelemetryTimeWindow ConstrainedTimeWindow { get; init; }

    public required TelemetryTimeWindow SourceObservedTimeWindow { get; init; }

    public IReadOnlyList<TelemetryEntityReference> SourceParticipants { get; init; } = [];

    public required EvidenceState EvidenceState { get; init; }

    public required ConstraintApplicationDisposition Disposition { get; init; }

    public required ConstraintApplicationReason Reason { get; init; }

    public required int CandidateCountBefore { get; init; }

    public required int CandidateCountAfter { get; init; }

    public required bool AlternativesRetained { get; init; }

    public required bool ConflictDetected { get; init; }

    public IReadOnlyList<string> SourceAncestryIds { get; init; } = [];

    public IReadOnlyList<TelemetryProvenanceReference> Provenance { get; init; } = [];

    public TelemetryVisibility Visibility { get; init; } = TelemetryVisibility.EvaluationContext;
}

public sealed record TracingConstraintTelemetryRecord
{
    public int TelemetrySchemaVersion { get; init; } = TelemetrySchema.Version;

    public string RecordType { get; init; } = "causalTracingConstraint";

    public required string ScenarioId { get; init; }

    public required int Seed { get; init; }

    public required string ConstraintId { get; init; }

    public required string SourceObservationId { get; init; }

    public required string DirectSourceId { get; init; }

    public IReadOnlyList<string> SourceAncestryIds { get; init; } = [];

    public required string TargetEntityId { get; init; }

    public required string SourceKind { get; init; }

    public required string RelationKind { get; init; }

    public IReadOnlyList<TelemetryEntityReference> Participants { get; init; } = [];

    public required string TracingRole { get; init; }

    public required TelemetryTimeWindow ObservedTimeWindow { get; init; }

    public required TelemetryTimeWindow ConstrainedTimeWindow { get; init; }

    public IReadOnlyList<string> AllowedRegionIds { get; init; } = [];

    public required long AvailableAtTick { get; init; }

    public required EvidenceState EvidenceState { get; init; }

    public required bool IsPersonEvidence { get; init; }

    public required bool IsDeviceOnlyEvidence { get; init; }

    public required string DerivationKind { get; init; }

    public required TelemetryVisibility Visibility { get; init; }

    public IReadOnlyList<TelemetryProvenanceReference> Provenance { get; init; } = [];
}

public sealed record CausalTracingStepTelemetryRecord
{
    public int TelemetrySchemaVersion { get; init; } = TelemetrySchema.Version;

    public string RecordType { get; init; } = "causalTracingStep";

    public required string ScenarioId { get; init; }

    public required int Seed { get; init; }

    public required string StrategyId { get; init; }

    public required string SessionId { get; init; }

    public required string QueryContextId { get; init; }

    public required string StepId { get; init; }

    public required int StepNumber { get; init; }

    public required CausalTracingDirection Direction { get; init; }

    public required TracingConstraintRole Role { get; init; }

    public required TelemetryTimeWindow TimeWindow { get; init; }

    public required string TargetEntityId { get; init; }

    public string? ConstraintId { get; init; }

    public string? SourceObservationId { get; init; }

    public string? SourceGroupId { get; init; }

    public required string RelationKind { get; init; }

    public required EvidenceState EvidenceState { get; init; }

    public required TracingStepDisposition Disposition { get; init; }

    public required TracingStepReason Reason { get; init; }

    public IReadOnlyList<string> CandidateInputRegionIds { get; init; } = [];

    public IReadOnlyList<string> CandidateOutputRegionIds { get; init; } = [];

    public IReadOnlyList<string> AppliedConstraintIds { get; init; } = [];

    public IReadOnlyList<string> SkippedConstraintIds { get; init; } = [];

    public IReadOnlyList<string> SupportingSourceGroupIds { get; init; } = [];

    public IReadOnlyList<string> ConflictingSourceGroupIds { get; init; } = [];

    public required bool AlternativesRetained { get; init; }

    public required bool ConflictDetected { get; init; }

    public IReadOnlyList<string> SourceAncestryIds { get; init; } = [];

    public IReadOnlyList<TelemetryProvenanceReference> Provenance { get; init; } = [];

    public TelemetryVisibility Visibility { get; init; } = TelemetryVisibility.EvaluationContext;
}

public sealed record CandidateProvenanceTelemetryRecord
{
    public required string RegionId { get; init; }

    public required string SourceKind { get; init; }

    public required string SourceIdentifier { get; init; }

    public required long AvailableAtTick { get; init; }

    public required bool IsPersonEvidence { get; init; }

    public required bool IsDeviceOnlyEvidence { get; init; }

    public required EvidenceState EvidenceState { get; init; }
}

public sealed record CandidateTransitionTelemetryRecord
{
    public int TelemetrySchemaVersion { get; init; } = TelemetrySchema.Version;

    public string RecordType { get; init; } = "candidateStateTransition";

    public required string ScenarioId { get; init; }

    public required int Seed { get; init; }

    public required string StrategyId { get; init; }

    public required string TransitionId { get; init; }

    public string? CandidateSetIdBefore { get; init; }

    public required string CandidateSetIdAfter { get; init; }

    public required string TargetEntityId { get; init; }

    public required TelemetryTimeWindow TransitionTimeWindow { get; init; }

    public IReadOnlyList<string> CandidateRegionsBefore { get; init; } = [];

    public IReadOnlyList<string> CandidateRegionsAfter { get; init; } = [];

    public IReadOnlyList<string> PreferredRegionsBefore { get; init; } = [];

    public IReadOnlyList<string> PreferredRegionsAfter { get; init; } = [];

    public IReadOnlyList<string> OuterPlausibleRegionsBefore { get; init; } = [];

    public IReadOnlyList<string> OuterPlausibleRegionsAfter { get; init; } = [];

    public IReadOnlyList<string> ConflictedRegionsBefore { get; init; } = [];

    public IReadOnlyList<string> ConflictedRegionsAfter { get; init; } = [];

    public EvidenceState? EvidenceStateBefore { get; init; }

    public required EvidenceState EvidenceStateAfter { get; init; }

    public IReadOnlyList<string> TriggerReferenceIds { get; init; } = [];

    public IReadOnlyList<string> QueryAccessContextIds { get; init; } = [];

    public IReadOnlyList<string> SourceAncestryIds { get; init; } = [];

    public IReadOnlyList<CandidateProvenanceTelemetryRecord> Provenance { get; init; } = [];

    public TelemetryVisibility Visibility { get; init; } = TelemetryVisibility.EvaluationContext;
}

public sealed record PastStateQueryTelemetryRecord
{
    public int TelemetrySchemaVersion { get; init; } = TelemetrySchema.Version;

    public string RecordType { get; init; } = "pastStateQuery";

    public required string ScenarioId { get; init; }

    public required int Seed { get; init; }

    public required string QueryId { get; init; }

    public required string QueryContextId { get; init; }

    public required string TargetEntityId { get; init; }

    public required TelemetryTimeWindow TargetTimeWindow { get; init; }

    public required long EvidenceAvailabilityCutoff { get; init; }

    public required string EvidenceSetId { get; init; }

    public IReadOnlyList<string> ObservationIds { get; init; } = [];

    public IReadOnlyList<string> ConstraintIds { get; init; } = [];

    public IReadOnlyList<string> SourceGroupIds { get; init; } = [];

    public IReadOnlyList<string> PublicRelationReferences { get; init; } = [];

    public required int ExcludedFutureEvidenceCount { get; init; }

    public TelemetryVisibility Visibility { get; init; } = TelemetryVisibility.EvaluationContext;
}

public sealed record PastStateCandidateTelemetryRecord
{
    public int TelemetrySchemaVersion { get; init; } = TelemetrySchema.Version;

    public string RecordType { get; init; } = "pastStateCandidate";

    public required string ScenarioId { get; init; }

    public required int Seed { get; init; }

    public required string QueryId { get; init; }

    public required string ReconstructionId { get; init; }

    public required string CandidateId { get; init; }

    public required string TargetEntityId { get; init; }

    public required TelemetryTimeWindow TargetTimeWindow { get; init; }

    public required string RegionId { get; init; }

    public IReadOnlyList<string> GenerationEvidenceIds { get; init; } = [];

    public IReadOnlyList<string> GenerationSourceGroupIds { get; init; } = [];

    public IReadOnlyList<string> SourceAncestryIds { get; init; } = [];

    public IReadOnlyList<string> AppliedConstraintIds { get; init; } = [];

    public IReadOnlyList<string> UnresolvedConstraintIds { get; init; } = [];

    public IReadOnlyList<string> ConflictingConstraintIds { get; init; } = [];

    public IReadOnlyList<string> PublicRelationReferences { get; init; } = [];

    public required PastStateCandidateDisposition Disposition { get; init; }

    public TelemetryVisibility Visibility { get; init; } = TelemetryVisibility.EvaluationContext;
}

public sealed record CandidateVerificationTelemetryRecord
{
    public int TelemetrySchemaVersion { get; init; } = TelemetrySchema.Version;

    public string RecordType { get; init; } = "candidateVerification";

    public required string ScenarioId { get; init; }

    public required int Seed { get; init; }

    public required string QueryId { get; init; }

    public required string CandidateId { get; init; }

    public IReadOnlyList<string> VerificationEvidenceIds { get; init; } = [];

    public IReadOnlyList<string> VerificationSourceGroupIds { get; init; } = [];

    public IReadOnlyList<string> SharedAncestrySourceGroupIds { get; init; } = [];

    public IReadOnlyList<string> SupportingConstraintIds { get; init; } = [];

    public IReadOnlyList<string> RefutingConstraintIds { get; init; } = [];

    public IReadOnlyList<string> ConflictingConstraintIds { get; init; } = [];

    public IReadOnlyList<string> GenerationVerificationAncestryOverlap { get; init; } = [];

    public required CandidateVerificationDisposition Disposition { get; init; }

    public TelemetryVisibility Visibility { get; init; } = TelemetryVisibility.EvaluationContext;
}

public sealed record PastStateMethodTelemetryRecord
{
    public int TelemetrySchemaVersion { get; init; } = TelemetrySchema.Version;

    public string RecordType { get; init; } = "pastStateMethod";

    public required string ScenarioId { get; init; }

    public required int Seed { get; init; }

    public required string QueryId { get; init; }

    public required PastStateMethod Method { get; init; }

    public IReadOnlyList<string> CandidateRegionIds { get; init; } = [];

    public required int Steps { get; init; }

    public required int PublicRelationsInspected { get; init; }

    public required int EvidenceRecordsInspected { get; init; }

    public required int SourceGroupsInspected { get; init; }

    public string? TracingSessionId { get; init; }

    public TelemetryVisibility Visibility { get; init; } = TelemetryVisibility.EvaluationContext;
}

public sealed record TelemetryRecordCategoryManifest
{
    public required string Category { get; init; }

    public required TelemetryStreamKind Stream { get; init; }

    public required string RelativePath { get; init; }

    public required int RecordCount { get; init; }
}

public sealed record TelemetryScenarioInput
{
    public required string ScenarioId { get; init; }

    public required string ContentSha256 { get; init; }
}

public sealed record TelemetryManifest
{
    public int TelemetrySchemaVersion { get; init; } = TelemetrySchema.Version;

    public required string SourceCommitSha { get; init; }

    public required bool DirtyWorkingTree { get; init; }

    public IReadOnlyList<TelemetryScenarioInput> ScenarioInputs { get; init; } = [];

    public IReadOnlyList<int> Seeds { get; init; } = [];

    public IReadOnlyList<string> StrategyIdentifiers { get; init; } = [];

    public required int EvaluationRunCount { get; init; }

    public IReadOnlyList<TelemetryRecordCategoryManifest> RecordCategories { get; init; } = [];

    public required int RawConstraintRecordCount { get; init; }

    public required int UniqueConstraintSourceObservationCount { get; init; }

    public required int RawTracingConstraintRecordCount { get; init; }

    public required int UniqueTracingSourceObservationCount { get; init; }

    public IReadOnlyList<string> DeterministicOrderContract { get; init; } = [];

    public string NewlinePolicy { get; init; } = "UTF-8 without BOM; LF; one JSON object per line";
}

public sealed record TruthTelemetryStream
{
    public IReadOnlyList<WorldEventTelemetryRecord> WorldEvents { get; init; } = [];

    public IReadOnlyList<TruthInteractionTelemetryRecord> Interactions { get; init; } = [];
}

public sealed record EvidenceTelemetryStream
{
    public IReadOnlyList<ObservationTelemetryRecord> Observations { get; init; } = [];

    public IReadOnlyList<InteractionObservationTelemetryRecord> InteractionObservations { get; init; } = [];

    public IReadOnlyList<ConstraintTelemetryRecord> Constraints { get; init; } = [];

    public IReadOnlyList<TracingConstraintTelemetryRecord> TracingConstraints { get; init; } = [];

}

public sealed record EvaluationTelemetryStream
{
    public IReadOnlyList<PastStateQueryTelemetryRecord> PastStateQueries { get; init; } = [];

    public IReadOnlyList<ConstraintApplicationTelemetryRecord> ConstraintApplications { get; init; } = [];

    public IReadOnlyList<CandidateTransitionTelemetryRecord> CandidateTransitions { get; init; } = [];

    public IReadOnlyList<CausalTracingStepTelemetryRecord> CausalTracingSteps { get; init; } = [];

    public IReadOnlyList<PastStateCandidateTelemetryRecord> PastStateCandidates { get; init; } = [];

    public IReadOnlyList<CandidateVerificationTelemetryRecord> CandidateVerifications { get; init; } = [];

    public IReadOnlyList<PastStateMethodTelemetryRecord> PastStateMethods { get; init; } = [];
}

public sealed record NeutralTelemetryBundle
{
    public required TruthTelemetryStream Truth { get; init; }

    public required EvidenceTelemetryStream Evidence { get; init; }

    public required EvaluationTelemetryStream Evaluation { get; init; }

    public required TelemetryManifest Manifest { get; init; }
}
