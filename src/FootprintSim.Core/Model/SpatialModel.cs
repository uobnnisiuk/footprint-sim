namespace FootprintSim.Model;

public enum RegionType
{
    Outdoor,
    Building,
    Road,
    Slope,
    River,
    Underground,
    Other
}

public sealed record Position(double X, double Y, double Elevation, string RegionId)
{
    public double PlanarDistanceTo(Position other)
    {
        var deltaX = X - other.X;
        var deltaY = Y - other.Y;
        return Math.Sqrt((deltaX * deltaX) + (deltaY * deltaY));
    }
}

public sealed class RegionConnection
{
    public required string ToRegionId { get; init; }

    public double TravelCost { get; init; } = 1.0;

    public bool Passable { get; init; } = true;
}

public sealed class RegionDefinition
{
    public required string Id { get; init; }

    public required RegionType Type { get; init; }

    public required Position Center { get; init; }

    public double Passability { get; init; } = 1.0;

    public IReadOnlyList<RegionConnection> Connections { get; init; } = [];
}
