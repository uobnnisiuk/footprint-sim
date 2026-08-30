using System.Text.Json;
using FootprintSim.Evidence;
using FootprintSim.Events;
using FootprintSim.Hazards;
using FootprintSim.Interactions;
using FootprintSim.Interactions.CandidateWarps;
using FootprintSim.Interactions.Tracing;
using FootprintSim.Model;

namespace FootprintSim.Scenarios;

public sealed class ScenarioDefinition
{
    public int SchemaVersion { get; init; } = 1;

    public required string Id { get; init; }

    public required string Title { get; init; }

    public required string Description { get; init; }

    public required string TargetPersonId { get; init; }

    public required double NormalWalkingRadius { get; init; }

    public IReadOnlyList<string> HazardTransportEnvelopeRegionIds { get; init; } = [];

    public StochasticHazardDefinition? StochasticHazard { get; init; }

    public WeftEvaluationDefinition? WeftEvaluation { get; init; }

    public CausalTracingEvaluationDefinition? CausalTracingEvaluation { get; init; }

    public IReadOnlyList<string> EvaluationSearchableRegionIds { get; init; } = [];

    public IReadOnlyList<RegionDefinition> Regions { get; init; } = [];

    public ScenarioEntities Entities { get; init; } = new();

    public IReadOnlyList<ScenarioStep> Steps { get; init; } = [];
}

public enum CausalTracingCondition
{
    TwoIndependentConsistentWefts,
    TwoSameSourceDerivedConstraints,
    ConfirmedAndUncertain,
    IndependentConflictingSources,
    UpstreamAndLocal,
    LocalAndDownstream,
    CausalSandwich,
    DelayedConstraint,
    UnavailableConstraint,
    DeviceOnlyConstraint
}

public sealed class CausalTracingEvaluationDefinition
{
    public required long EvaluationEndTick { get; init; }

    public IReadOnlyList<CausalTracingCondition> ConditionCycle { get; init; } = [];

    public IReadOnlyList<CausalTracingCandidateStateDefinition> BaseCandidateStates { get; init; } = [];

    public IReadOnlyList<CausalTracingConstraintDefinition> Constraints { get; init; } = [];
}

public sealed class CausalTracingCandidateStateDefinition
{
    public required long StartTick { get; init; }

    public required long EndTick { get; init; }

    public required CandidateWarpTemporalKind TemporalKind { get; init; }

    public IReadOnlyList<string> RegionIds { get; init; } = [];
}

public sealed class CausalTracingConstraintDefinition
{
    public required string Id { get; init; }

    public required string SourceObservationKey { get; init; }

    public required string DirectSourceId { get; init; }

    public IReadOnlyList<string> SourceAncestryIds { get; init; } = [];

    public required TracingConstraintSourceKind SourceKind { get; init; }

    public required string RelationKind { get; init; }

    public string? OtherEntityId { get; init; }

    public required TracingConstraintRole Role { get; init; }

    public required long ObservedStartTick { get; init; }

    public required long ObservedEndTick { get; init; }

    public required long ConstrainedStartTick { get; init; }

    public required long ConstrainedEndTick { get; init; }

    public IReadOnlyList<string> AllowedRegionIds { get; init; } = [];

    public required long AvailableAtTick { get; init; }

    public required EvidenceState EvidenceState { get; init; }

    public required bool IsPersonEvidence { get; init; }

    public required bool IsDeviceOnlyEvidence { get; init; }

    public InteractionVisibility Visibility { get; init; } = InteractionVisibility.PublicEvidence;

    public required string DerivationKind { get; init; }

    public string? OccurrenceStepId { get; init; }

    public IReadOnlyList<CausalTracingCondition> AppliesToConditions { get; init; } = [];
}

public enum WeftEvidenceCondition
{
    Confirmed,
    SupportedButUncertain,
    Delayed,
    Conflicting,
    Unavailable,
    DeviceOnly
}

public sealed class WeftEvaluationDefinition
{
    public required string EncounterStepId { get; init; }

    public required string OtherPersonId { get; init; }

    public required string TargetDeviceId { get; init; }

    public required string EncounterRegionId { get; init; }

    public required string ConflictingRegionId { get; init; }

    public required long HistoricalStartTick { get; init; }

    public required long HistoricalEndTick { get; init; }

    public required long NormalAvailableAtTick { get; init; }

    public required long DelayedAvailableAtTick { get; init; }

    public required long EvaluationEndTick { get; init; }

    public IReadOnlyList<string> HistoricalCandidateRegionIds { get; init; } = [];

    public IReadOnlyList<WeftEvidenceCondition> ConditionCycle { get; init; } = [];
}

public sealed class ScenarioStep
{
    public required string Id { get; init; }

    public required long Tick { get; init; }

    public required EventKind Kind { get; init; }

    public MovementKind? MovementKind { get; init; }

    public IReadOnlyList<string> SubjectIds { get; init; } = [];

    public IReadOnlyList<string> RelatedEntityIds { get; init; } = [];

    public string? CauseStepId { get; init; }

    public Position? Position { get; init; }

    public Position? RelatedPosition { get; init; }

    public string? RegionId { get; init; }

    public IReadOnlyDictionary<string, JsonElement> Payload { get; init; } =
        new SortedDictionary<string, JsonElement>(StringComparer.Ordinal);

    public ObservationSpecification? Observation { get; init; }
}

public sealed class ObservationSpecification
{
    public required string EvidenceType { get; init; }

    public IReadOnlyList<string> ReportedSubjectIds { get; init; } = [];

    public Position? ReportedPosition { get; init; }

    public string? ReportedRegionId { get; init; }

    public double EmissionProbability { get; init; } = 1.0;

    public double DropProbability { get; init; }

    public int MinimumDelayTicks { get; init; }

    public int MaximumDelayTicks { get; init; }

    public EvidenceState State { get; init; } = EvidenceState.SupportedButUncertain;

    public double ConflictProbability { get; init; }

    public double ConflictOffsetX { get; init; }

    public double ConflictOffsetY { get; init; }

    public string? ConflictRegionId { get; init; }

    public IReadOnlyDictionary<string, JsonElement> PublicPayload { get; init; } =
        new SortedDictionary<string, JsonElement>(StringComparer.Ordinal);
}
