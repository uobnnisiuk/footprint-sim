using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FootprintSim.Events;
using FootprintSim.Model;
using FootprintSim.Scenarios;
using FootprintSim.Simulation;

namespace FootprintSim.Hazards;

public sealed record HazardPropagationResult
{
    public IReadOnlyList<SimulationEvent> Events { get; init; } = [];

    public IReadOnlyDictionary<string, Position> FinalPositions { get; init; } =
        new SortedDictionary<string, Position>(StringComparer.Ordinal);
}

public sealed class StochasticHazardPropagator
{
    public HazardPropagationResult Propagate(ScenarioDefinition scenario, int seed)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        ScenarioValidator.Validate(scenario);
        var definition = scenario.StochasticHazard ?? throw new ArgumentException(
            "Scenario does not define stochastic hazard propagation.",
            nameof(scenario));
        var nodes = definition.Nodes.ToDictionary(node => node.RegionId, StringComparer.Ordinal);
        var events = new List<SimulationEvent>();
        var finalPositions = new SortedDictionary<string, Position>(StringComparer.Ordinal);
        var personId = scenario.TargetPersonId;
        var entityIds = definition.DeviceId is null
            ? new[] { personId }
            : new[] { personId, definition.DeviceId };
        var rootSeed = unchecked((ulong)(long)seed);
        var sharedRandom = new StableRandom(StableSeed.Derive(
            rootSeed,
            "hazard",
            definition.HazardId,
            "shared"));
        var sourcePosition = RegionPosition(scenario, definition.SourceRegionId);
        var startEvent = CreateEvent(
            scenario,
            seed,
            "shared",
            events.Count,
            definition.StartTick,
            EventKind.HazardTransportStarted,
            MovementKind.HazardDriven,
            entityIds,
            [],
            null,
            sourcePosition,
            new SortedDictionary<string, JsonElement>(StringComparer.Ordinal)
            {
                ["hazardId"] = JsonSerializer.SerializeToElement(definition.HazardId),
                ["model"] = JsonSerializer.SerializeToElement("synthetic-stochastic-hazard")
            });
        events.Add(startEvent);

        var sharedState = new PropagationState(
            definition.SourceRegionId,
            definition.StartTick,
            startEvent.Id,
            0);
        var firstTransition = ApplicableTransitions(
            definition,
            sharedState.RegionId,
            EntityRole.Group,
            separated: false,
            HazardTransportType.HazardDriven);
        if (firstTransition.Count > 0)
        {
            sharedState = Move(
                scenario,
                seed,
                definition,
                events,
                entityIds,
                "shared",
                sharedState,
                SelectTransition(firstTransition, sharedRandom),
                sharedRandom);
        }

        var separated = definition.DeviceId is not null &&
            sharedRandom.NextDouble() < definition.SeparationProbability;
        if (separated)
        {
            var separationPosition = RegionPosition(scenario, sharedState.RegionId);
            var separation = CreateEvent(
                scenario,
                seed,
                "separation",
                events.Count,
                sharedState.Tick,
                EventKind.PersonDeviceSeparated,
                MovementKind.HazardDriven,
                [personId],
                [definition.DeviceId!],
                sharedState.CauseEventId,
                separationPosition,
                new SortedDictionary<string, JsonElement>(StringComparer.Ordinal)
                {
                    ["hazardId"] = JsonSerializer.SerializeToElement(definition.HazardId),
                    ["mechanism"] = JsonSerializer.SerializeToElement("synthetic-branch-separation")
                },
                new SortedDictionary<string, Position>(StringComparer.Ordinal)
                {
                    [definition.DeviceId!] = separationPosition
                });
            events.Add(separation);

            var separatedState = sharedState with { CauseEventId = separation.Id };
            var personRandom = new StableRandom(StableSeed.Derive(
                rootSeed,
                "hazard",
                definition.HazardId,
                "entity",
                personId));
            var personFinal = PropagateEntity(
                scenario,
                seed,
                definition,
                nodes,
                events,
                [personId],
                personId,
                EntityRole.Person,
                separatedState,
                personRandom,
                separated: true);
            finalPositions[personId] = RegionPosition(scenario, personFinal.RegionId);

            var deviceRandom = new StableRandom(StableSeed.Derive(
                rootSeed,
                "hazard",
                definition.HazardId,
                "entity",
                definition.DeviceId!));
            var deviceFinal = PropagateEntity(
                scenario,
                seed,
                definition,
                nodes,
                events,
                [definition.DeviceId!],
                definition.DeviceId!,
                EntityRole.Device,
                separatedState,
                deviceRandom,
                separated: true);
            finalPositions[definition.DeviceId!] = RegionPosition(scenario, deviceFinal.RegionId);
        }
        else
        {
            var sharedFinal = PropagateEntity(
                scenario,
                seed,
                definition,
                nodes,
                events,
                entityIds,
                "shared",
                EntityRole.Group,
                sharedState,
                sharedRandom,
                separated: false);
            foreach (var entityId in entityIds)
            {
                finalPositions[entityId] = RegionPosition(scenario, sharedFinal.RegionId);
            }
        }

        return new HazardPropagationResult
        {
            Events = events,
            FinalPositions = finalPositions
        };
    }

    private static PropagationState PropagateEntity(
        ScenarioDefinition scenario,
        int seed,
        StochasticHazardDefinition definition,
        IReadOnlyDictionary<string, HazardNodeDefinition> nodes,
        ICollection<SimulationEvent> events,
        IReadOnlyList<string> subjectIds,
        string streamId,
        EntityRole role,
        PropagationState state,
        StableRandom random,
        bool separated)
    {
        while (state.TransitionCount < definition.MaximumTransitionsPerEntity)
        {
            var node = nodes[state.RegionId];
            var shouldStop = node.IsTerminal || random.NextDouble() < node.DepositionProbability;
            if (shouldStop)
            {
                var trapped = random.NextDouble() < node.TrappingProbability;
                var stopEvent = CreateEvent(
                    scenario,
                    seed,
                    streamId,
                    events.Count,
                    state.Tick,
                    trapped ? EventKind.Trapped : EventKind.Deposited,
                    MovementKind.DepositionOrTrapping,
                    subjectIds,
                    [],
                    state.CauseEventId,
                    RegionPosition(scenario, state.RegionId),
                    new SortedDictionary<string, JsonElement>(StringComparer.Ordinal)
                    {
                        ["hazardId"] = JsonSerializer.SerializeToElement(definition.HazardId),
                        ["outcome"] = JsonSerializer.SerializeToElement(
                            trapped ? "synthetic-trapping" : "synthetic-deposition")
                    });
                events.Add(stopEvent);
                state = state with { CauseEventId = stopEvent.Id };

                var secondary = ApplicableTransitions(
                    definition,
                    state.RegionId,
                    role,
                    separated,
                    HazardTransportType.SecondaryTransport);
                if (secondary.Count == 0 || random.NextDouble() >= node.ReleaseProbability)
                {
                    return state;
                }

                var releaseDelay = random.NextInclusive(
                    node.MinimumReleaseDelayTicks,
                    node.MaximumReleaseDelayTicks);
                state = state with { Tick = state.Tick + releaseDelay };
                state = Move(
                    scenario,
                    seed,
                    definition,
                    events,
                    subjectIds,
                    streamId,
                    state,
                    SelectTransition(secondary, random),
                    random);
                continue;
            }

            var primary = ApplicableTransitions(
                definition,
                state.RegionId,
                role,
                separated,
                HazardTransportType.HazardDriven);
            if (primary.Count == 0)
            {
                throw new InvalidOperationException(
                    $"No applicable transition from '{state.RegionId}' for {role}.");
            }

            state = Move(
                scenario,
                seed,
                definition,
                events,
                subjectIds,
                streamId,
                state,
                SelectTransition(primary, random),
                random);
        }

        throw new InvalidOperationException(
            $"Hazard propagation exceeded {definition.MaximumTransitionsPerEntity} transitions.");
    }

    private static PropagationState Move(
        ScenarioDefinition scenario,
        int seed,
        StochasticHazardDefinition definition,
        ICollection<SimulationEvent> events,
        IReadOnlyList<string> subjectIds,
        string streamId,
        PropagationState state,
        HazardTransitionDefinition transition,
        StableRandom random)
    {
        var travelTicks = random.NextInclusive(
            transition.MinimumTravelTicks,
            transition.MaximumTravelTicks);
        var tick = state.Tick + travelTicks;
        var movementKind = transition.TransportType == HazardTransportType.SecondaryTransport
            ? MovementKind.SecondaryTransport
            : MovementKind.HazardDriven;
        var kind = transition.TransportType == HazardTransportType.SecondaryTransport
            ? EventKind.SecondaryTransported
            : EventKind.HazardTransported;
        var movement = CreateEvent(
            scenario,
            seed,
            streamId,
            events.Count,
            tick,
            kind,
            movementKind,
            subjectIds,
            [definition.HazardId],
            state.CauseEventId,
            RegionPosition(scenario, transition.DestinationRegionId),
            new SortedDictionary<string, JsonElement>(StringComparer.Ordinal)
            {
                ["hazardId"] = JsonSerializer.SerializeToElement(definition.HazardId),
                ["transitionId"] = JsonSerializer.SerializeToElement(transition.Id),
                ["transitionProvenanceId"] = JsonSerializer.SerializeToElement(
                    transition.ProvenanceId),
                ["transportType"] = JsonSerializer.SerializeToElement(transition.TransportType),
                ["travelTicks"] = JsonSerializer.SerializeToElement(travelTicks)
            });
        events.Add(movement);
        return new PropagationState(
            transition.DestinationRegionId,
            tick,
            movement.Id,
            state.TransitionCount + 1);
    }

    private static IReadOnlyList<HazardTransitionDefinition> ApplicableTransitions(
        StochasticHazardDefinition definition,
        string sourceRegionId,
        EntityRole role,
        bool separated,
        HazardTransportType transportType)
    {
        return definition.Transitions
            .Where(transition => transition.TransportType == transportType)
            .Where(transition => string.Equals(
                transition.SourceRegionId,
                sourceRegionId,
                StringComparison.Ordinal))
            .Where(transition => transition.Activation switch
            {
                HazardTransitionActivation.AnyEntity => true,
                HazardTransitionActivation.PersonOnly => role == EntityRole.Person,
                HazardTransitionActivation.DeviceOnly => role == EntityRole.Device,
                HazardTransitionActivation.AfterSeparation => separated,
                _ => false
            })
            .OrderBy(transition => transition.Id, StringComparer.Ordinal)
            .ToArray();
    }

    private static HazardTransitionDefinition SelectTransition(
        IReadOnlyList<HazardTransitionDefinition> transitions,
        StableRandom random)
    {
        var weightSum = transitions.Sum(transition => transition.Weight);
        var selected = random.NextDouble() * weightSum;
        var cumulative = 0d;
        foreach (var transition in transitions)
        {
            cumulative += transition.Weight;
            if (selected < cumulative)
            {
                return transition;
            }
        }

        return transitions[^1];
    }

    private static Position RegionPosition(ScenarioDefinition scenario, string regionId) =>
        scenario.Regions.Single(region => string.Equals(
            region.Id,
            regionId,
            StringComparison.Ordinal)).Center;

    private static SimulationEvent CreateEvent(
        ScenarioDefinition scenario,
        int seed,
        string streamId,
        int eventOrdinal,
        long tick,
        EventKind kind,
        MovementKind movementKind,
        IReadOnlyList<string> subjectIds,
        IReadOnlyList<string> relatedEntityIds,
        string? causeEventId,
        Position position,
        IReadOnlyDictionary<string, JsonElement> payload,
        IReadOnlyDictionary<string, Position>? relatedPositions = null)
    {
        var identity = string.Join(
            "\n",
            scenario.Id,
            seed.ToString(System.Globalization.CultureInfo.InvariantCulture),
            streamId,
            eventOrdinal.ToString(System.Globalization.CultureInfo.InvariantCulture),
            kind.ToString(),
            position.RegionId);
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(identity));
        return new SimulationEvent
        {
            Id = $"evt-{Convert.ToHexString(hash.AsSpan(0, 8)).ToLowerInvariant()}",
            Tick = tick,
            Sequence = 0,
            Kind = kind,
            MovementKind = movementKind,
            SubjectIds = subjectIds,
            RelatedEntityIds = relatedEntityIds,
            CauseEventId = causeEventId,
            Position = position,
            RelatedPositions = relatedPositions ??
                new SortedDictionary<string, Position>(StringComparer.Ordinal),
            RegionId = position.RegionId,
            Payload = payload
        };
    }

    private enum EntityRole
    {
        Group,
        Person,
        Device
    }

    private sealed record PropagationState(
        string RegionId,
        long Tick,
        string CauseEventId,
        int TransitionCount);
}
