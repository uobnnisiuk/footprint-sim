namespace FootprintSim.Hazards;

public enum HazardTransportType
{
    HazardDriven,
    SecondaryTransport
}

public enum HazardTransitionActivation
{
    AnyEntity,
    PersonOnly,
    DeviceOnly,
    AfterSeparation
}

public sealed class StochasticHazardDefinition
{
    public required string HazardId { get; init; }

    public required string SourceRegionId { get; init; }

    public required long StartTick { get; init; }

    public string? DeviceId { get; init; }

    public double SeparationProbability { get; init; }

    public int MaximumTransitionsPerEntity { get; init; } = 32;

    public IReadOnlyList<HazardNodeDefinition> Nodes { get; init; } = [];

    public IReadOnlyList<HazardTransitionDefinition> Transitions { get; init; } = [];
}

public sealed class HazardNodeDefinition
{
    public required string RegionId { get; init; }

    public required long MinimumArrivalTick { get; init; }

    public required long MaximumArrivalTick { get; init; }

    public double DepositionProbability { get; init; }

    public double TrappingProbability { get; init; }

    public double ReleaseProbability { get; init; }

    public int MinimumReleaseDelayTicks { get; init; }

    public int MaximumReleaseDelayTicks { get; init; }

    public bool IsTerminal { get; init; }

    public bool IsRequiredTerminal { get; init; }

    public bool PubliclyVisible { get; init; } = true;

    public long PublishedAtTick { get; init; }

    public string? PublicProvenanceId { get; init; }
}

public sealed class HazardTransitionDefinition
{
    public required string Id { get; init; }

    public required string SourceRegionId { get; init; }

    public required string DestinationRegionId { get; init; }

    public required double Weight { get; init; }

    public required int MinimumTravelTicks { get; init; }

    public required int MaximumTravelTicks { get; init; }

    public required HazardTransportType TransportType { get; init; }

    public HazardTransitionActivation Activation { get; init; } =
        HazardTransitionActivation.AnyEntity;

    public bool PubliclyVisible { get; init; } = true;

    public required string ProvenanceId { get; init; }
}

public sealed record PublicHazardEnvelope
{
    public required string HazardId { get; init; }

    public required string SourceRegionId { get; init; }

    public required long PublishedAtTick { get; init; }

    public IReadOnlyList<PublicHazardRegion> Regions { get; init; } = [];

    public IReadOnlyList<PublicHazardBranch> Branches { get; init; } = [];
}

public sealed record PublicHazardRegion
{
    public required string RegionId { get; init; }

    public required long PublishedAtTick { get; init; }

    public required long MinimumArrivalTick { get; init; }

    public required long MaximumArrivalTick { get; init; }

    public IReadOnlyList<HazardTransportType> TransportTypes { get; init; } = [];

    public IReadOnlyList<string> ProvenanceIds { get; init; } = [];
}

public sealed record PublicHazardBranch
{
    public required string SourceRegionId { get; init; }

    public required string DestinationRegionId { get; init; }

    public required int MinimumTravelTicks { get; init; }

    public required int MaximumTravelTicks { get; init; }

    public required HazardTransportType TransportType { get; init; }

    public required string ProvenanceId { get; init; }

    public required long PublishedAtTick { get; init; }
}

public static class PublicHazardEnvelopeBuilder
{
    public static PublicHazardEnvelope Create(StochasticHazardDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        var publicNodes = definition.Nodes
            .Where(node => node.PubliclyVisible)
            .OrderBy(node => node.RegionId, StringComparer.Ordinal)
            .ToArray();
        var publicTransitions = definition.Transitions
            .Where(transition => transition.PubliclyVisible)
            .OrderBy(transition => transition.SourceRegionId, StringComparer.Ordinal)
            .ThenBy(transition => transition.DestinationRegionId, StringComparer.Ordinal)
            .ThenBy(transition => transition.Id, StringComparer.Ordinal)
            .ToArray();
        var publicNodesByRegion = publicNodes.ToDictionary(
            node => node.RegionId,
            StringComparer.Ordinal);

        return new PublicHazardEnvelope
        {
            HazardId = definition.HazardId,
            SourceRegionId = definition.SourceRegionId,
            PublishedAtTick = publicNodes
                .Select(node => node.PublishedAtTick)
                .DefaultIfEmpty(definition.StartTick)
                .Min(),
            Regions = publicNodes.Select(node => new PublicHazardRegion
            {
                RegionId = node.RegionId,
                PublishedAtTick = node.PublishedAtTick,
                MinimumArrivalTick = node.MinimumArrivalTick,
                MaximumArrivalTick = node.MaximumArrivalTick,
                TransportTypes = publicTransitions
                    .Where(transition =>
                        string.Equals(
                            transition.DestinationRegionId,
                            node.RegionId,
                            StringComparison.Ordinal))
                    .Select(transition => transition.TransportType)
                    .Distinct()
                    .OrderBy(transportType => transportType)
                    .ToArray(),
                ProvenanceIds = publicTransitions
                    .Where(transition =>
                        string.Equals(
                            transition.DestinationRegionId,
                            node.RegionId,
                            StringComparison.Ordinal))
                    .Select(transition => transition.ProvenanceId)
                    .Append(node.PublicProvenanceId!)
                    .Where(provenanceId => !string.IsNullOrWhiteSpace(provenanceId))
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(provenanceId => provenanceId, StringComparer.Ordinal)
                    .ToArray()
            }).ToArray(),
            Branches = publicTransitions.Select(transition => new PublicHazardBranch
            {
                SourceRegionId = transition.SourceRegionId,
                DestinationRegionId = transition.DestinationRegionId,
                MinimumTravelTicks = transition.MinimumTravelTicks,
                MaximumTravelTicks = transition.MaximumTravelTicks,
                TransportType = transition.TransportType,
                ProvenanceId = transition.ProvenanceId,
                PublishedAtTick = Math.Max(
                    publicNodesByRegion[transition.SourceRegionId].PublishedAtTick,
                    publicNodesByRegion[transition.DestinationRegionId].PublishedAtTick)
            }).ToArray()
        };
    }
}
