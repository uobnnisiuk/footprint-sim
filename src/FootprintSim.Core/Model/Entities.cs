namespace FootprintSim.Model;

public sealed class PersonDefinition
{
    public required string Id { get; init; }

    public required Position InitialPosition { get; init; }
}

public sealed class DeviceDefinition
{
    public required string Id { get; init; }

    public required Position InitialPosition { get; init; }
}

public sealed class CarrierDefinition
{
    public required string Id { get; init; }

    public required Position InitialPosition { get; init; }
}

public sealed class RescuerDefinition
{
    public required string Id { get; init; }

    public required Position InitialPosition { get; init; }
}

public sealed class HazardDefinition
{
    public required string Id { get; init; }

    public required string Kind { get; init; }

    public IReadOnlyList<string> RouteRegionIds { get; init; } = [];

    public double MinimumSpeedPerTick { get; init; }

    public double MaximumSpeedPerTick { get; init; }

    public double StopProbability { get; init; }
}

public sealed class ScenarioEntities
{
    public IReadOnlyList<PersonDefinition> Persons { get; init; } = [];

    public IReadOnlyList<DeviceDefinition> Devices { get; init; } = [];

    public IReadOnlyList<CarrierDefinition> Carriers { get; init; } = [];

    public IReadOnlyList<RescuerDefinition> Rescuers { get; init; } = [];

    public IReadOnlyList<HazardDefinition> Hazards { get; init; } = [];
}

public sealed record EntityState(string EntityId, string EntityType, Position Position);
