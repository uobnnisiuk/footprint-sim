using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FootprintSim.Evidence;
using FootprintSim.Events;
using FootprintSim.Hazards;
using FootprintSim.Interactions;
using FootprintSim.Interactions.Truth;
using FootprintSim.Model;
using FootprintSim.Scenarios;

namespace FootprintSim.Simulation;

public sealed class SimulationEngine
{
    public SimulationRun Run(ScenarioDefinition scenario, int seed)
    {
        ScenarioValidator.Validate(scenario);

        var random = new StableRandom(seed);
        var eventCandidates = new List<EventCandidate>();
        var observations = new List<ObservationRecord>();
        var entityStates = CreateInitialEntityStates(scenario.Entities);

        var indexedSteps = scenario.Steps
            .Select((step, index) => new IndexedStep(step, index))
            .OrderBy(item => item.Step.Tick)
            .ThenBy(item => item.Index)
            .ToArray();

        var eventIdsByStep = scenario.Steps
            .Select((step, index) => new
            {
                step.Id,
                EventId = StableId("evt", scenario.Id, seed, index, step.Id, "source")
            })
            .ToDictionary(item => item.Id, item => item.EventId, StringComparer.Ordinal);

        foreach (var indexedStep in indexedSteps)
        {
            var step = indexedStep.Step;
            var stableOrder = ((long)indexedStep.Index) * 100;
            var sourceEvent = new SimulationEvent
            {
                Id = eventIdsByStep[step.Id],
                Tick = step.Tick,
                Sequence = 0,
                Kind = step.Kind,
                MovementKind = step.MovementKind,
                SubjectIds = step.SubjectIds,
                RelatedEntityIds = step.RelatedEntityIds,
                CauseEventId = step.CauseStepId is null ? null : eventIdsByStep[step.CauseStepId],
                Position = step.Position,
                RelatedPositions = CreateRelatedPositions(step),
                RegionId = step.RegionId ?? step.Position?.RegionId,
                Payload = SortedPayload(step.Payload)
            };

            eventCandidates.Add(new EventCandidate(sourceEvent, stableOrder));
            ApplyStep(step, entityStates);

            if (step.Observation is not null)
            {
                GenerateObservation(
                    scenario.Id,
                    seed,
                    indexedStep.Index,
                    stableOrder,
                    sourceEvent,
                    step.Observation,
                    random,
                    observations,
                    eventCandidates);
            }
        }

        if (scenario.StochasticHazard is not null)
        {
            var propagation = new StochasticHazardPropagator().Propagate(scenario, seed);
            var stableOrderBase = ((long)scenario.Steps.Count) * 100;
            foreach (var item in propagation.Events.Select((simulationEvent, index) =>
                (simulationEvent, index)))
            {
                eventCandidates.Add(new EventCandidate(
                    item.simulationEvent,
                    stableOrderBase + item.index));
            }

            foreach (var finalPosition in propagation.FinalPositions)
            {
                UpdatePosition(entityStates, finalPosition.Key, finalPosition.Value);
            }
        }

        var orderedEvents = eventCandidates
            .OrderBy(candidate => candidate.Event.Tick)
            .ThenBy(candidate => candidate.StableOrder)
            .ThenBy(candidate => candidate.Event.Id, StringComparer.Ordinal)
            .Select((candidate, sequence) => candidate.Event with { Sequence = sequence })
            .ToArray();

        var orderedObservations = observations
            .OrderBy(observation => observation.AvailableTick)
            .ThenBy(observation => observation.ObservedTick)
            .ThenBy(observation => observation.ObservationId, StringComparer.Ordinal)
            .ToArray();

        var interactionProjection = CreateInteractionProjection(
            scenario,
            seed,
            orderedEvents,
            eventIdsByStep);

        return new SimulationRun
        {
            ScenarioId = scenario.Id,
            Seed = seed,
            GroundTruthEvents = orderedEvents,
            ObservationSet = new ObservationSet
            {
                ScenarioId = scenario.Id,
                Seed = seed,
                Observations = orderedObservations
            },
            GroundTruthFinalEntityStates = new SortedDictionary<string, EntityState>(
                entityStates,
                StringComparer.Ordinal),
            GroundTruthInteractionOccurrences = interactionProjection.Occurrences,
            InteractionObservations = interactionProjection.Observations,
            CrossThreadConstraints = interactionProjection.Constraints,
            WeftEvidenceCondition = interactionProjection.Condition
        };
    }

    private static InteractionProjection CreateInteractionProjection(
        ScenarioDefinition scenario,
        int seed,
        IReadOnlyList<SimulationEvent> events,
        IReadOnlyDictionary<string, string> eventIdsByStep)
    {
        var definition = scenario.WeftEvaluation;
        if (definition is null)
        {
            return new InteractionProjection([], null, null, null);
        }

        var sourceEventId = eventIdsByStep[definition.EncounterStepId];
        var sourceEvent = events.Single(item => string.Equals(
            item.Id,
            sourceEventId,
            StringComparison.Ordinal));
        var occurrence = InteractionOccurrenceProjector.FromPersonPersonEncounter(
            scenario.Id,
            seed,
            sourceEvent,
            scenario.Entities.Persons.Select(person => person.Id));
        var condition = definition.ConditionCycle[(int)((uint)seed % definition.ConditionCycle.Count)];
        InteractionObservation? observation = null;
        if (condition != WeftEvidenceCondition.DeviceOnly)
        {
            var observedWindow = ((uint)seed / (uint)definition.ConditionCycle.Count) % 2 == 0
                ? InteractionTimeWindow.At(definition.HistoricalEndTick)
                : new InteractionTimeWindow(
                    definition.HistoricalStartTick,
                    definition.HistoricalEndTick);
            var state = condition switch
            {
                WeftEvidenceCondition.SupportedButUncertain =>
                    EvidenceState.SupportedButUncertain,
                WeftEvidenceCondition.Conflicting => EvidenceState.Conflicting,
                _ => EvidenceState.Confirmed
            };
            var availableAt = condition == WeftEvidenceCondition.Delayed
                ? definition.DelayedAvailableAtTick
                : definition.NormalAvailableAtTick;
            var visibility = condition == WeftEvidenceCondition.Unavailable
                ? InteractionVisibility.PrivateEvidence
                : InteractionVisibility.PublicEvidence;
            var regionId = condition == WeftEvidenceCondition.Conflicting
                ? definition.ConflictingRegionId
                : definition.EncounterRegionId;
            observation = InteractionObservation.Create(
                scenario.Id,
                seed,
                $"synthetic-encounter-evidence:{scenario.Id}:{seed}",
                referencedInteractionId: null,
                InteractionRelationKind.PersonPersonEncounter,
                [
                    new InteractionParticipant(
                        scenario.TargetPersonId,
                        InteractionEntityKind.Person,
                        InteractionParticipantRole.EncounterParticipant),
                    new InteractionParticipant(
                        definition.OtherPersonId,
                        InteractionEntityKind.Person,
                        InteractionParticipantRole.EncounterParticipant)
                ],
                observedWindow,
                [regionId],
                availableAt,
                state,
                isPersonEvidence: true,
                isDeviceOnlyEvidence: false,
                visibility,
                [new InteractionProvenance(
                    InteractionProvenanceKind.ExternalSource,
                    $"synthetic-s3-condition:{condition}")]);
        }

        var observationSet = new InteractionObservationSet(
            scenario.Id,
            seed,
            observation is null ? [] : [observation]);
        var constraint = observation is null
            ? null
            : CrossThreadConstraintDeriver.DerivePersonEncounterCoLocation(
                observation,
                scenario.TargetPersonId);
        var constraintSet = new CrossThreadConstraintSet(
            scenario.Id,
            seed,
            constraint is null ? [] : [constraint]);
        return new InteractionProjection(
            [occurrence],
            observationSet,
            constraintSet,
            condition);
    }

    private static Dictionary<string, EntityState> CreateInitialEntityStates(ScenarioEntities entities)
    {
        var states = new Dictionary<string, EntityState>(StringComparer.Ordinal);
        foreach (var person in entities.Persons)
        {
            states.Add(person.Id, new EntityState(person.Id, "person", person.InitialPosition));
        }

        foreach (var device in entities.Devices)
        {
            states.Add(device.Id, new EntityState(device.Id, "device", device.InitialPosition));
        }

        foreach (var carrier in entities.Carriers)
        {
            states.Add(carrier.Id, new EntityState(carrier.Id, "carrier", carrier.InitialPosition));
        }

        foreach (var rescuer in entities.Rescuers)
        {
            states.Add(rescuer.Id, new EntityState(rescuer.Id, "rescuer", rescuer.InitialPosition));
        }

        return states;
    }

    private static void ApplyStep(ScenarioStep step, IDictionary<string, EntityState> states)
    {
        var movesSubjects = step.Kind is EventKind.RegionEntered
            or EventKind.RegionExited
            or EventKind.VoluntaryMovement
            or EventKind.CarrierMovement
            or EventKind.HazardTransportStarted
            or EventKind.HazardTransported
            or EventKind.PersonDeviceSeparated
            or EventKind.Deposited
            or EventKind.Trapped
            or EventKind.SecondaryTransported;

        if (movesSubjects && step.Position is not null)
        {
            foreach (var subjectId in step.SubjectIds)
            {
                UpdatePosition(states, subjectId, step.Position);
            }
        }

        if (step.Kind == EventKind.PersonDeviceSeparated && step.RelatedPosition is not null)
        {
            foreach (var relatedId in step.RelatedEntityIds)
            {
                UpdatePosition(states, relatedId, step.RelatedPosition);
            }
        }
    }

    private static void UpdatePosition(
        IDictionary<string, EntityState> states,
        string entityId,
        Position position)
    {
        if (states.TryGetValue(entityId, out var state))
        {
            states[entityId] = state with { Position = position };
        }
    }

    private static void GenerateObservation(
        string scenarioId,
        int seed,
        int stepIndex,
        long stableOrder,
        SimulationEvent sourceEvent,
        ObservationSpecification specification,
        StableRandom random,
        ICollection<ObservationRecord> observations,
        ICollection<EventCandidate> eventCandidates)
    {
        var emissionRoll = random.NextDouble();
        var dropRoll = random.NextDouble();
        var emitted = emissionRoll < specification.EmissionProbability;
        var dropped = dropRoll < specification.DropProbability;

        if (!emitted || dropped)
        {
            var reason = !emitted ? "not-emitted" : "dropped";
            eventCandidates.Add(CreateLifecycleEvent(
                scenarioId,
                seed,
                stepIndex,
                sourceEvent,
                EventKind.ObservationDropped,
                sourceEvent.Tick,
                stableOrder + 1,
                new SortedDictionary<string, JsonElement>(StringComparer.Ordinal)
                {
                    ["reason"] = JsonSerializer.SerializeToElement(reason)
                }));
            return;
        }

        var delay = random.NextInclusive(
            specification.MinimumDelayTicks,
            specification.MaximumDelayTicks);
        var availableTick = sourceEvent.Tick + delay;
        var observationId = StableId(
            "obs",
            scenarioId,
            seed,
            stepIndex,
            sourceEvent.Id,
            "primary");

        observations.Add(new ObservationRecord
        {
            ObservationId = observationId,
            ObservedTick = sourceEvent.Tick,
            AvailableTick = availableTick,
            EvidenceType = specification.EvidenceType,
            SubjectIds = specification.ReportedSubjectIds,
            ReportedPosition = specification.ReportedPosition,
            ReportedRegionId = specification.ReportedRegionId ?? specification.ReportedPosition?.RegionId,
            State = specification.State,
            PublicPayload = SortedPayload(specification.PublicPayload)
        });

        eventCandidates.Add(CreateLifecycleEvent(
            scenarioId,
            seed,
            stepIndex,
            sourceEvent,
            EventKind.ObservationGenerated,
            sourceEvent.Tick,
            stableOrder + 1,
            new SortedDictionary<string, JsonElement>(StringComparer.Ordinal)
            {
                ["availableTick"] = JsonSerializer.SerializeToElement(availableTick),
                ["observationId"] = JsonSerializer.SerializeToElement(observationId)
            }));

        if (delay > 0)
        {
            eventCandidates.Add(CreateLifecycleEvent(
                scenarioId,
                seed,
                stepIndex,
                sourceEvent,
                EventKind.ObservationDelayed,
                availableTick,
                stableOrder + 2,
                new SortedDictionary<string, JsonElement>(StringComparer.Ordinal)
                {
                    ["delayTicks"] = JsonSerializer.SerializeToElement(delay),
                    ["observationId"] = JsonSerializer.SerializeToElement(observationId)
                }));
        }

        if (random.NextDouble() >= specification.ConflictProbability)
        {
            return;
        }

        var conflictingId = StableId(
            "obs",
            scenarioId,
            seed,
            stepIndex,
            sourceEvent.Id,
            "conflict");
        var conflictingPosition = specification.ReportedPosition is null
            ? null
            : specification.ReportedPosition with
            {
                X = specification.ReportedPosition.X + specification.ConflictOffsetX,
                Y = specification.ReportedPosition.Y + specification.ConflictOffsetY,
                RegionId = specification.ConflictRegionId ?? specification.ReportedPosition.RegionId
            };

        observations.Add(new ObservationRecord
        {
            ObservationId = conflictingId,
            ObservedTick = sourceEvent.Tick,
            AvailableTick = availableTick,
            EvidenceType = specification.EvidenceType,
            SubjectIds = specification.ReportedSubjectIds,
            ReportedPosition = conflictingPosition,
            ReportedRegionId = specification.ConflictRegionId ??
                specification.ReportedRegionId ??
                specification.ReportedPosition?.RegionId,
            State = EvidenceState.Conflicting,
            PublicPayload = SortedPayload(specification.PublicPayload)
        });

        eventCandidates.Add(CreateLifecycleEvent(
            scenarioId,
            seed,
            stepIndex,
            sourceEvent,
            EventKind.ObservationConflict,
            availableTick,
            stableOrder + 3,
            new SortedDictionary<string, JsonElement>(StringComparer.Ordinal)
            {
                ["observationId"] = JsonSerializer.SerializeToElement(conflictingId)
            }));
    }

    private static EventCandidate CreateLifecycleEvent(
        string scenarioId,
        int seed,
        int stepIndex,
        SimulationEvent sourceEvent,
        EventKind kind,
        long tick,
        long stableOrder,
        IReadOnlyDictionary<string, JsonElement> payload)
    {
        return new EventCandidate(
            new SimulationEvent
            {
                Id = StableId("evt", scenarioId, seed, stepIndex, sourceEvent.Id, kind.ToString()),
                Tick = tick,
                Sequence = 0,
                Kind = kind,
                SubjectIds = sourceEvent.SubjectIds,
                RelatedEntityIds = sourceEvent.RelatedEntityIds,
                CauseEventId = sourceEvent.Id,
                Position = sourceEvent.Position,
                RegionId = sourceEvent.RegionId,
                Payload = payload
            },
            stableOrder);
    }

    private static SortedDictionary<string, JsonElement> SortedPayload(
        IReadOnlyDictionary<string, JsonElement> payload)
    {
        var sorted = new SortedDictionary<string, JsonElement>(StringComparer.Ordinal);
        foreach (var item in payload)
        {
            sorted.Add(item.Key, item.Value);
        }

        return sorted;
    }

    private static IReadOnlyDictionary<string, Position> CreateRelatedPositions(ScenarioStep step)
    {
        var positions = new SortedDictionary<string, Position>(StringComparer.Ordinal);
        if (step.RelatedPosition is null)
        {
            return positions;
        }

        foreach (var relatedId in step.RelatedEntityIds)
        {
            positions.Add(relatedId, step.RelatedPosition);
        }

        return positions;
    }

    private static string StableId(
        string prefix,
        string scenarioId,
        int seed,
        int stepIndex,
        string identity,
        string suffix)
    {
        var input = $"{scenarioId}\n{seed}\n{stepIndex}\n{identity}\n{suffix}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return $"{prefix}-{Convert.ToHexString(hash.AsSpan(0, 8)).ToLowerInvariant()}";
    }

    private sealed record IndexedStep(ScenarioStep Step, int Index);

    private sealed record EventCandidate(SimulationEvent Event, long StableOrder);

    private sealed record InteractionProjection(
        IReadOnlyList<InteractionOccurrence> Occurrences,
        InteractionObservationSet? Observations,
        CrossThreadConstraintSet? Constraints,
        WeftEvidenceCondition? Condition);
}
