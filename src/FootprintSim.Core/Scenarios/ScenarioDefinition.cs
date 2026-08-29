using System.Text.Json;
using FootprintSim.Evidence;
using FootprintSim.Events;
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

    public IReadOnlyList<RegionDefinition> Regions { get; init; } = [];

    public ScenarioEntities Entities { get; init; } = new();

    public IReadOnlyList<ScenarioStep> Steps { get; init; } = [];
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
