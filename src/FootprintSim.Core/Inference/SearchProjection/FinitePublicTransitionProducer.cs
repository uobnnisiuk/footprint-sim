namespace FootprintSim.Inference.SearchProjection;

public sealed record FinitePublicEdge(string From, string To, int Duration, string TransitRegionId);

/// <summary>
/// An opt-in finite synthetic model. Closure and waiting are author assurances,
/// not certification of the physical world. No hazard truth is accepted here.
/// </summary>
public sealed record FinitePublicTransitionModel(
    string SourceId,
    string EntityId,
    long AvailableTick,
    long ValidFromTick,
    long ValidThroughTick,
    IReadOnlyList<string> NodeIds,
    IReadOnlyList<string> TransitRegionIds,
    IReadOnlyList<FinitePublicEdge> Edges,
    bool IsClosed,
    bool WaitingAtEveryNodeDeclared);

public static class FinitePublicTransitionProducer
{
    /// <summary>
    /// The interval is an unknown single presence instant, not continuous residence.
    /// Without that explicit interpretation or closed dynamics, returns unknown.
    /// </summary>
    public static PublicRegionProjection Create(
        FinitePublicTransitionModel model,
        string entityId,
        string historicalRegionId,
        long startTick,
        long endTick,
        long targetTick,
        long evidenceCutoff,
        bool unknownInstantWindowCertified)
    {
        Validate(model);
        if (entityId != model.EntityId || !model.NodeIds.Contains(historicalRegionId, StringComparer.Ordinal)
            || startTick < model.ValidFromTick || endTick < startTick || targetTick < endTick
            || targetTick > model.ValidThroughTick || evidenceCutoff < endTick
            || evidenceCutoff > targetTick || model.AvailableTick > evidenceCutoff)
        {
            throw new InvalidDataException("Query is outside the available finite model's entity, region or time contract.");
        }

        var complete = model.IsClosed && model.WaitingAtEveryNodeDeclared && unknownInstantWindowCertified;
        var locations = new HashSet<string>(StringComparer.Ordinal);
        if (complete)
        {
            for (var originTick = startTick; originTick <= endTick; originTick++)
            {
                var states = new HashSet<State> { new(historicalRegionId, null) };
                for (var tick = originTick; tick < targetTick; tick++)
                {
                    var next = new HashSet<State>();
                    foreach (var state in states)
                    {
                        if (state.Destination is not null)
                        {
                            next.Add(new State(state.Destination, null));
                            continue;
                        }

                        next.Add(state); // Explicitly declared one-tick waiting at a node.
                        foreach (var edge in model.Edges.Where(edge => edge.From == state.Location))
                        {
                            next.Add(edge.Duration == 1
                                ? new State(edge.To, null)
                                : new State(edge.TransitRegionId, edge.To));
                        }
                    }

                    states = next;
                }

                locations.UnionWith(states.Select(state => state.Location));
            }
        }

        return new PublicRegionProjection(
            historicalRegionId, startTick, endTick, targetTick,
            locations.OrderBy(id => id, StringComparer.Ordinal).ToArray(),
            complete, model.SourceId, model.AvailableTick);
    }

    private static void Validate(FinitePublicTransitionModel model)
    {
        ArgumentNullException.ThrowIfNull(model);
        RequireId(model.SourceId);
        RequireId(model.EntityId);
        if (model.AvailableTick < 0 || model.ValidFromTick < 0
            || model.ValidThroughTick < model.ValidFromTick || model.ValidThroughTick > 6)
        {
            throw new InvalidDataException("This study supports only a finite validity interval within ticks 0..6.");
        }

        var nodes = ValidateIds(model.NodeIds);
        var transit = ValidateIds(model.TransitRegionIds);
        if (nodes.Count is < 1 or > 5 || nodes.Overlaps(transit))
        {
            throw new InvalidDataException("Finite nodes and transit regions must be distinct; require 1..5 nodes.");
        }

        ArgumentNullException.ThrowIfNull(model.Edges);
        var pairs = new HashSet<(string, string)>();
        var usedTransit = new HashSet<string>(StringComparer.Ordinal);
        foreach (var edge in model.Edges)
        {
            ArgumentNullException.ThrowIfNull(edge);
            if (!nodes.Contains(edge.From) || !nodes.Contains(edge.To) || edge.From == edge.To
                || edge.Duration is < 1 or > 2 || !transit.Contains(edge.TransitRegionId)
                || !pairs.Add((edge.From, edge.To)) || !usedTransit.Add(edge.TransitRegionId))
            {
                throw new InvalidDataException("Invalid, duplicate or unrepresented finite edge.");
            }
        }
    }

    private static HashSet<string> ValidateIds(IReadOnlyList<string> ids)
    {
        ArgumentNullException.ThrowIfNull(ids);
        var set = new HashSet<string>(StringComparer.Ordinal);
        foreach (var id in ids)
        {
            RequireId(id);
            if (!set.Add(id))
            {
                throw new InvalidDataException("Duplicate finite model identifier.");
            }
        }

        return set;
    }

    private static void RequireId(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 512
            || value != value.Trim() || value.Any(char.IsControl))
        {
            throw new InvalidDataException("Invalid finite model identifier.");
        }
    }

    private sealed record State(string Location, string? Destination);
}
