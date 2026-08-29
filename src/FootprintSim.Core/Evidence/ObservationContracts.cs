using System.Text.Json;
using FootprintSim.Model;

namespace FootprintSim.Evidence;

public enum EvidenceState
{
    Confirmed,
    SupportedButUncertain,
    Conflicting,
    Indeterminate,
    UnsupportedForQuery
}
public sealed record ObservationRecord
{
    public required string ObservationId { get; init; }

    public required long ObservedTick { get; init; }

    public required long AvailableTick { get; init; }

    public required string EvidenceType { get; init; }

    public IReadOnlyList<string> SubjectIds { get; init; } = [];

    public Position? ReportedPosition { get; init; }

    public string? ReportedRegionId { get; init; }

    public required EvidenceState State { get; init; }

    public IReadOnlyDictionary<string, JsonElement> PublicPayload { get; init; } =
        new SortedDictionary<string, JsonElement>(StringComparer.Ordinal);
}

public sealed record ObservationSet
{
    public required string ScenarioId { get; init; }

    public required int Seed { get; init; }

    public IReadOnlyList<ObservationRecord> Observations { get; init; } = [];
}
