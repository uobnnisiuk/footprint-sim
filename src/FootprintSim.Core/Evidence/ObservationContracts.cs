using System.Text.Json;
using FootprintSim.Model;

namespace FootprintSim.Evidence;

public enum EvidenceState
{
    /// <summary>
    /// The evidence record is asserted without an uncertainty qualifier at the evidence boundary.
    /// This is not a claim that the record matches simulation truth or has calibrated accuracy.
    /// </summary>
    Confirmed,

    /// <summary>
    /// The evidence record carries an explicit uncertainty qualifier.
    /// </summary>
    SupportedButUncertain,

    /// <summary>
    /// The evidence record explicitly conflicts with another claim or the query basis.
    /// </summary>
    Conflicting,

    /// <summary>
    /// The evidence record does not determine the queried proposition.
    /// </summary>
    Indeterminate,

    /// <summary>
    /// The evidence record exists but does not support this query.
    /// </summary>
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
