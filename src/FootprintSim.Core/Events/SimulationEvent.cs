using System.Text.Json;
using FootprintSim.Model;

namespace FootprintSim.Events;

public enum EventKind
{
    RegionEntered,
    RegionExited,
    PersonDeviceAssociated,
    Encounter,
    EvidenceExchanged,
    CarrierBoarded,
    CarrierExited,
    VoluntaryMovement,
    CarrierMovement,
    HazardTransportStarted,
    HazardTransported,
    PersonDeviceSeparated,
    Deposited,
    Trapped,
    SecondaryTransported,
    CommunicationLost,
    ObservationGenerated,
    ObservationDelayed,
    ObservationDropped,
    ObservationConflict
}

public enum MovementKind
{
    Voluntary,
    Carrier,
    HazardDriven,
    DepositionOrTrapping,
    SecondaryTransport
}

public sealed record SimulationEvent
{
    public required string Id { get; init; }

    public required long Tick { get; init; }

    public required int Sequence { get; init; }

    public required EventKind Kind { get; init; }

    public MovementKind? MovementKind { get; init; }

    public IReadOnlyList<string> SubjectIds { get; init; } = [];

    public IReadOnlyList<string> RelatedEntityIds { get; init; } = [];

    public string? CauseEventId { get; init; }

    public Position? Position { get; init; }

    public IReadOnlyDictionary<string, Position> RelatedPositions { get; init; } =
        new SortedDictionary<string, Position>(StringComparer.Ordinal);

    public string? RegionId { get; init; }

    public IReadOnlyDictionary<string, JsonElement> Payload { get; init; } =
        new SortedDictionary<string, JsonElement>(StringComparer.Ordinal);
}
