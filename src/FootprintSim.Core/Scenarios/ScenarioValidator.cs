using FootprintSim.Events;
using FootprintSim.Hazards;
using FootprintSim.Model;

namespace FootprintSim.Scenarios;

public static class ScenarioValidator
{
    public static void Validate(ScenarioDefinition scenario)
    {
        ArgumentNullException.ThrowIfNull(scenario);

        if (scenario.SchemaVersion is not (1 or 2 or 3 or 4 or 5))
        {
            throw new InvalidDataException($"Unsupported scenario schema version {scenario.SchemaVersion}.");
        }

        RequireText(scenario.Id, "Scenario id");
        RequireText(scenario.TargetPersonId, "Target person id");
        if (scenario.NormalWalkingRadius < 0)
        {
            throw new InvalidDataException("Normal walking radius cannot be negative.");
        }

        var regionIds = UniqueIds(scenario.Regions.Select(region => region.Id), "region");
        foreach (var region in scenario.Regions)
        {
            if (region.Passability is < 0 or > 1)
            {
                throw new InvalidDataException($"Region '{region.Id}' passability must be between 0 and 1.");
            }

            RequireRegion(region.Center, regionIds, $"region '{region.Id}' center");
            if (!string.Equals(region.Center.RegionId, region.Id, StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    $"Region '{region.Id}' center must identify its owning region.");
            }

            foreach (var connection in region.Connections)
            {
                if (!regionIds.Contains(connection.ToRegionId))
                {
                    throw new InvalidDataException(
                        $"Region '{region.Id}' connects to unknown region '{connection.ToRegionId}'.");
                }
            }
        }

        var entities = EnumerateLocatedEntities(scenario.Entities).ToArray();
        var entityIds = UniqueIds(
            entities.Select(entity => entity.Id)
                .Concat(scenario.Entities.Hazards.Select(hazard => hazard.Id)),
            "entity");
        foreach (var entity in entities)
        {
            RequireRegion(entity.Position, regionIds, $"entity '{entity.Id}'");
        }

        foreach (var hazard in scenario.Entities.Hazards)
        {
            if (hazard.MinimumSpeedPerTick < 0 || hazard.MaximumSpeedPerTick < hazard.MinimumSpeedPerTick)
            {
                throw new InvalidDataException($"Hazard '{hazard.Id}' has an invalid speed range.");
            }

            if (hazard.StopProbability is < 0 or > 1)
            {
                throw new InvalidDataException($"Hazard '{hazard.Id}' stop probability must be between 0 and 1.");
            }

            foreach (var routeRegionId in hazard.RouteRegionIds)
            {
                if (!regionIds.Contains(routeRegionId))
                {
                    throw new InvalidDataException(
                        $"Hazard '{hazard.Id}' route contains unknown region '{routeRegionId}'.");
                }
            }
        }

        if (!scenario.Entities.Persons.Any(person => person.Id == scenario.TargetPersonId))
        {
            throw new InvalidDataException($"Target person '{scenario.TargetPersonId}' is not defined.");
        }

        foreach (var envelopeRegionId in scenario.HazardTransportEnvelopeRegionIds)
        {
            if (!regionIds.Contains(envelopeRegionId))
            {
                throw new InvalidDataException($"Unknown hazard envelope region '{envelopeRegionId}'.");
            }
        }


        ValidateWeftEvaluation(scenario, regionIds);

        ValidateCausalTracingEvaluation(scenario, regionIds, entityIds);

        ValidatePastStateReconstruction(scenario, regionIds, entityIds);

        ValidateStochasticHazard(scenario, regionIds);

        var stepIds = UniqueIds(scenario.Steps.Select(step => step.Id), "step");
        var stepsById = scenario.Steps.ToDictionary(step => step.Id, StringComparer.Ordinal);
        foreach (var step in scenario.Steps)
        {
            if (step.Tick < 0)
            {
                throw new InvalidDataException($"Step '{step.Id}' has a negative tick.");
            }

            foreach (var entityId in step.SubjectIds.Concat(step.RelatedEntityIds))
            {
                if (!entityIds.Contains(entityId))
                {
                    throw new InvalidDataException($"Step '{step.Id}' refers to unknown entity '{entityId}'.");
                }
            }

            if (step.SubjectIds.Count == 0)
            {
                throw new InvalidDataException($"Step '{step.Id}' must identify at least one subject.");
            }

            if (step.CauseStepId is not null && !stepIds.Contains(step.CauseStepId))
            {
                throw new InvalidDataException($"Step '{step.Id}' has unknown cause '{step.CauseStepId}'.");
            }

            if (step.CauseStepId is not null && stepsById[step.CauseStepId].Tick > step.Tick)
            {
                throw new InvalidDataException(
                    $"Step '{step.Id}' cannot be caused by a future step '{step.CauseStepId}'.");
            }

            if (step.Position is not null)
            {
                RequireRegion(step.Position, regionIds, $"step '{step.Id}' position");
            }

            if (step.RelatedPosition is not null)
            {
                RequireRegion(step.RelatedPosition, regionIds, $"step '{step.Id}' related position");
            }

            var requiresPosition = step.Kind is EventKind.RegionEntered
                or EventKind.RegionExited
                or EventKind.VoluntaryMovement
                or EventKind.CarrierMovement
                or EventKind.HazardTransportStarted
                or EventKind.HazardTransported
                or EventKind.PersonDeviceSeparated
                or EventKind.Deposited
                or EventKind.Trapped
                or EventKind.SecondaryTransported;
            if (requiresPosition && step.Position is null)
            {
                throw new InvalidDataException($"Movement step '{step.Id}' requires a position.");
            }

            if (step.Kind == EventKind.PersonDeviceSeparated &&
                step.RelatedEntityIds.Count > 0 &&
                step.RelatedPosition is null)
            {
                throw new InvalidDataException(
                    $"Separation step '{step.Id}' requires a related position for separated entities.");
            }

            ValidateObservation(step, entityIds, regionIds);
        }

        ValidateStochasticSubjectTimeline(scenario);
    }

    private static void ValidateStochasticSubjectTimeline(ScenarioDefinition scenario)
    {
        var hazard = scenario.StochasticHazard;
        if (hazard is null)
        {
            return;
        }

        var transportedIds = new HashSet<string>(StringComparer.Ordinal)
        {
            scenario.TargetPersonId
        };
        if (hazard.DeviceId is not null)
        {
            transportedIds.Add(hazard.DeviceId);
        }

        var positions = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [scenario.TargetPersonId] = scenario.Entities.Persons.Single(person =>
                person.Id == scenario.TargetPersonId).InitialPosition.RegionId
        };
        if (hazard.DeviceId is not null)
        {
            positions[hazard.DeviceId] = scenario.Entities.Devices.Single(device =>
                device.Id == hazard.DeviceId).InitialPosition.RegionId;
        }

        foreach (var step in scenario.Steps
            .Select((value, index) => (value, index))
            .OrderBy(item => item.value.Tick)
            .ThenBy(item => item.index))
        {
            var movesSubjects = step.value.Kind is EventKind.RegionEntered
                or EventKind.RegionExited
                or EventKind.VoluntaryMovement
                or EventKind.CarrierMovement
                or EventKind.HazardTransportStarted
                or EventKind.HazardTransported
                or EventKind.PersonDeviceSeparated
                or EventKind.Deposited
                or EventKind.Trapped
                or EventKind.SecondaryTransported;
            var affectsTransportedEntity = step.value.SubjectIds
                .Concat(step.value.RelatedEntityIds)
                .Any(transportedIds.Contains);
            if (movesSubjects && affectsTransportedEntity && step.value.Tick >= hazard.StartTick)
            {
                throw new InvalidDataException(
                    $"Step '{step.value.Id}' moves a stochastic-hazard subject at or after hazard start.");
            }

            if (!movesSubjects || step.value.Position is null)
            {
                continue;
            }

            foreach (var subjectId in step.value.SubjectIds.Where(transportedIds.Contains))
            {
                positions[subjectId] = step.value.Position.RegionId;
            }

            if (step.value.Kind == EventKind.PersonDeviceSeparated &&
                step.value.RelatedPosition is not null)
            {
                foreach (var relatedId in step.value.RelatedEntityIds.Where(transportedIds.Contains))
                {
                    positions[relatedId] = step.value.RelatedPosition.RegionId;
                }
            }
        }

        var misplaced = positions.FirstOrDefault(item => !string.Equals(
            item.Value,
            hazard.SourceRegionId,
            StringComparison.Ordinal));
        if (!misplaced.Equals(default(KeyValuePair<string, string>)))
        {
            throw new InvalidDataException(
                $"Stochastic-hazard subject '{misplaced.Key}' must be at the source before start.");
        }
    }

    private static IEnumerable<(string Id, Position Position)> EnumerateLocatedEntities(ScenarioEntities entities)
    {
        foreach (var person in entities.Persons)
        {
            yield return (person.Id, person.InitialPosition);
        }

        foreach (var device in entities.Devices)
        {
            yield return (device.Id, device.InitialPosition);
        }

        foreach (var carrier in entities.Carriers)
        {
            yield return (carrier.Id, carrier.InitialPosition);
        }

        foreach (var rescuer in entities.Rescuers)
        {
            yield return (rescuer.Id, rescuer.InitialPosition);
        }
    }

    private static void ValidateObservation(
        ScenarioStep step,
        ISet<string> entityIds,
        ISet<string> regionIds)
    {
        var observation = step.Observation;
        if (observation is null)
        {
            return;
        }

        RequireText(observation.EvidenceType, $"Step '{step.Id}' observation evidence type");
        foreach (var subjectId in observation.ReportedSubjectIds)
        {
            if (!entityIds.Contains(subjectId))
            {
                throw new InvalidDataException(
                    $"Step '{step.Id}' observation reports unknown entity '{subjectId}'.");
            }
        }

        if (observation.ReportedPosition is not null)
        {
            RequireRegion(
                observation.ReportedPosition,
                regionIds,
                $"step '{step.Id}' observation position");
        }

        if (observation.ReportedRegionId is not null &&
            !regionIds.Contains(observation.ReportedRegionId))
        {
            throw new InvalidDataException(
                $"Step '{step.Id}' observation reports unknown region '{observation.ReportedRegionId}'.");
        }

        if (observation.ReportedPosition is not null &&
            observation.ReportedRegionId is not null &&
            !string.Equals(
                observation.ReportedPosition.RegionId,
                observation.ReportedRegionId,
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                $"Step '{step.Id}' observation position and region disagree.");
        }

        if (observation.EmissionProbability is < 0 or > 1 ||
            observation.DropProbability is < 0 or > 1 ||
            observation.ConflictProbability is < 0 or > 1)
        {
            throw new InvalidDataException($"Step '{step.Id}' contains a probability outside [0, 1].");
        }

        if (observation.MinimumDelayTicks < 0 ||
            observation.MaximumDelayTicks < observation.MinimumDelayTicks)
        {
            throw new InvalidDataException($"Step '{step.Id}' contains an invalid observation delay range.");
        }
    }

    private static HashSet<string> UniqueIds(IEnumerable<string> ids, string kind)
    {
        var result = new HashSet<string>(StringComparer.Ordinal);
        foreach (var id in ids)
        {
            RequireText(id, $"{kind} id");
            if (!result.Add(id))
            {
                throw new InvalidDataException($"Duplicate {kind} id '{id}'.");
            }
        }

        return result;
    }

    private static void ValidateStochasticHazard(
        ScenarioDefinition scenario,
        ISet<string> regionIds)
    {
        var hazard = scenario.StochasticHazard;
        if (hazard is null)
        {
            if (scenario.SchemaVersion is 2 or 3 or 4 or 5)
            {
                throw new InvalidDataException(
                    "Scenario schema version 2 requires a stochastic hazard definition.");
            }

            return;
        }

        if (scenario.SchemaVersion is not (2 or 3 or 4 or 5))
        {
            throw new InvalidDataException(
                "Stochastic hazard definitions require scenario schema version 2.");
        }

        RequireText(hazard.HazardId, "Stochastic hazard id");
        RequireText(hazard.SourceRegionId, "Stochastic hazard source region");
        if (!scenario.Entities.Hazards.Any(entity =>
            string.Equals(entity.Id, hazard.HazardId, StringComparison.Ordinal)))
        {
            throw new InvalidDataException(
                $"Stochastic hazard '{hazard.HazardId}' is not a declared hazard entity.");
        }

        if (!regionIds.Contains(hazard.SourceRegionId))
        {
            throw new InvalidDataException(
                $"Stochastic hazard source '{hazard.SourceRegionId}' is not a declared region.");
        }

        if (hazard.StartTick < 0)
        {
            throw new InvalidDataException("Stochastic hazard start tick cannot be negative.");
        }

        RequireProbability(hazard.SeparationProbability, "Separation probability");
        if (hazard.MaximumTransitionsPerEntity <= 0)
        {
            throw new InvalidDataException("Maximum transitions per entity must be positive.");
        }

        if (hazard.DeviceId is not null && !scenario.Entities.Devices.Any(device =>
            string.Equals(device.Id, hazard.DeviceId, StringComparison.Ordinal)))
        {
            throw new InvalidDataException(
                $"Stochastic hazard device '{hazard.DeviceId}' is not a declared device.");
        }

        var nodesByRegion = new Dictionary<string, HazardNodeDefinition>(StringComparer.Ordinal);
        foreach (var node in hazard.Nodes)
        {
            RequireText(node.RegionId, "Hazard node region");
            if (!regionIds.Contains(node.RegionId))
            {
                throw new InvalidDataException(
                    $"Hazard node refers to unknown region '{node.RegionId}'.");
            }

            if (!nodesByRegion.TryAdd(node.RegionId, node))
            {
                throw new InvalidDataException($"Duplicate hazard node '{node.RegionId}'.");
            }

            if (node.MinimumArrivalTick < 0 ||
                node.MaximumArrivalTick < node.MinimumArrivalTick)
            {
                throw new InvalidDataException(
                    $"Hazard node '{node.RegionId}' has an invalid arrival window.");
            }

            RequireProbability(
                node.DepositionProbability,
                $"Hazard node '{node.RegionId}' deposition probability");
            RequireProbability(
                node.TrappingProbability,
                $"Hazard node '{node.RegionId}' trapping probability");
            RequireProbability(
                node.ReleaseProbability,
                $"Hazard node '{node.RegionId}' release probability");
            if (node.MinimumReleaseDelayTicks < 0 ||
                node.MaximumReleaseDelayTicks < node.MinimumReleaseDelayTicks)
            {
                throw new InvalidDataException(
                    $"Hazard node '{node.RegionId}' has an invalid release delay range.");
            }

            if (node.IsRequiredTerminal && !node.IsTerminal)
            {
                throw new InvalidDataException(
                    $"Required terminal '{node.RegionId}' must be marked terminal.");
            }

            if (node.PubliclyVisible)
            {
                RequireText(
                    node.PublicProvenanceId,
                    $"Public hazard node '{node.RegionId}' provenance");
                if (node.PublishedAtTick < 0 || node.PublishedAtTick > node.MinimumArrivalTick)
                {
                    throw new InvalidDataException(
                        $"Public hazard node '{node.RegionId}' has a contradictory publication tick.");
                }
            }
        }

        if (!nodesByRegion.TryGetValue(hazard.SourceRegionId, out var sourceNode))
        {
            throw new InvalidDataException("Stochastic hazard source must also be a hazard node.");
        }

        if (!sourceNode.PubliclyVisible)
        {
            throw new InvalidDataException("The public hazard source cannot be private.");
        }

        if (hazard.StartTick < sourceNode.MinimumArrivalTick ||
            hazard.StartTick > sourceNode.MaximumArrivalTick)
        {
            throw new InvalidDataException("Hazard source arrival window must contain the start tick.");
        }

        if (sourceNode.IsTerminal ||
            sourceNode.DepositionProbability != 0 ||
            sourceNode.ReleaseProbability != 0)
        {
            throw new InvalidDataException(
                "The shared hazard source must transport entities before deposition or release.");
        }

        var transitionIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var transition in hazard.Transitions)
        {
            RequireText(transition.Id, "Hazard transition id");
            RequireText(transition.SourceRegionId, $"Hazard transition '{transition.Id}' source");
            RequireText(
                transition.DestinationRegionId,
                $"Hazard transition '{transition.Id}' destination");
            RequireText(transition.ProvenanceId, $"Hazard transition '{transition.Id}' provenance");
            if (!transitionIds.Add(transition.Id))
            {
                throw new InvalidDataException($"Duplicate hazard transition '{transition.Id}'.");
            }

            if (!nodesByRegion.ContainsKey(transition.SourceRegionId) ||
                !nodesByRegion.ContainsKey(transition.DestinationRegionId))
            {
                throw new InvalidDataException(
                    $"Hazard transition '{transition.Id}' refers to an unknown node.");
            }

            if (!double.IsFinite(transition.Weight) || transition.Weight <= 0)
            {
                throw new InvalidDataException(
                    $"Hazard transition '{transition.Id}' must have a finite positive weight.");
            }

            if (transition.MinimumTravelTicks <= 0 ||
                transition.MaximumTravelTicks < transition.MinimumTravelTicks)
            {
                throw new InvalidDataException(
                    $"Hazard transition '{transition.Id}' has an invalid travel tick range.");
            }

            if (!Enum.IsDefined(transition.TransportType) ||
                !Enum.IsDefined(transition.Activation))
            {
                throw new InvalidDataException(
                    $"Hazard transition '{transition.Id}' has an unsupported enum value.");
            }

            if (transition.PubliclyVisible &&
                (!nodesByRegion[transition.SourceRegionId].PubliclyVisible ||
                 !nodesByRegion[transition.DestinationRegionId].PubliclyVisible))
            {
                throw new InvalidDataException(
                    $"Public transition '{transition.Id}' cannot expose a private node.");
            }

            var source = nodesByRegion[transition.SourceRegionId];
            var destination = nodesByRegion[transition.DestinationRegionId];
            var minimumReleaseDelay = transition.TransportType == HazardTransportType.SecondaryTransport
                ? source.MinimumReleaseDelayTicks
                : 0;
            var maximumReleaseDelay = transition.TransportType == HazardTransportType.SecondaryTransport
                ? source.MaximumReleaseDelayTicks
                : 0;
            long earliestArrival;
            long latestArrival;
            try
            {
                earliestArrival = checked(source.MinimumArrivalTick + minimumReleaseDelay +
                    transition.MinimumTravelTicks);
                latestArrival = checked(source.MaximumArrivalTick + maximumReleaseDelay +
                    transition.MaximumTravelTicks);
            }
            catch (OverflowException exception)
            {
                throw new InvalidDataException(
                    $"Hazard transition '{transition.Id}' arrival ticks overflow.",
                    exception);
            }
            if (destination.MinimumArrivalTick > earliestArrival ||
                destination.MaximumArrivalTick < latestArrival)
            {
                throw new InvalidDataException(
                    $"Hazard transition '{transition.Id}' can arrive outside the destination window.");
            }
        }

        foreach (var node in hazard.Nodes)
        {
            var outgoing = hazard.Transitions
                .Where(transition => string.Equals(
                    transition.SourceRegionId,
                    node.RegionId,
                    StringComparison.Ordinal))
                .ToArray();
            var primary = outgoing
                .Where(transition => transition.TransportType == HazardTransportType.HazardDriven)
                .ToArray();
            var secondary = outgoing
                .Where(transition => transition.TransportType == HazardTransportType.SecondaryTransport)
                .ToArray();

            if (!node.IsTerminal && primary.Length == 0 && node.DepositionProbability < 1)
            {
                throw new InvalidDataException(
                    $"Non-terminal hazard node '{node.RegionId}' has no primary destination.");
            }

            if (node.ReleaseProbability > 0 && secondary.Length == 0)
            {
                throw new InvalidDataException(
                    $"Hazard node '{node.RegionId}' can release but has no secondary destination.");
            }

            if (node.ReleaseProbability == 0 && secondary.Length > 0)
            {
                throw new InvalidDataException(
                    $"Hazard node '{node.RegionId}' has an inactive secondary transition.");
            }

            foreach (var group in outgoing.GroupBy(transition => transition.TransportType))
            {
                var weightSum = group.Sum(transition => transition.Weight);
                if (!double.IsFinite(weightSum) || weightSum <= 0)
                {
                    throw new InvalidDataException(
                        $"Hazard node '{node.RegionId}' has non-normalizable transition weights.");
                }
            }
        }


        if (!hazard.Transitions.Any(transition =>
            transition.TransportType == HazardTransportType.HazardDriven &&
            transition.Activation == HazardTransitionActivation.AnyEntity &&
            string.Equals(
                transition.SourceRegionId,
                hazard.SourceRegionId,
                StringComparison.Ordinal)))
        {
            throw new InvalidDataException(
                "The hazard source requires a primary transition available to the shared entity group.");
        }

        RejectCycles(hazard, nodesByRegion.Keys);
        var reachable = ReachableRegions(hazard);
        foreach (var requiredTerminal in hazard.Nodes.Where(node => node.IsRequiredTerminal))
        {
            if (!reachable.Contains(requiredTerminal.RegionId))
            {
                throw new InvalidDataException(
                    $"Required terminal '{requiredTerminal.RegionId}' is unreachable.");
            }
        }

        var publicRegionIds = hazard.Nodes
            .Where(node => node.PubliclyVisible)
            .Select(node => node.RegionId)
            .ToHashSet(StringComparer.Ordinal);
        var publiclyReachable = ReachableRegions(hazard, publicOnly: true);
        if (!publicRegionIds.IsSubsetOf(publiclyReachable))
        {
            throw new InvalidDataException(
                "Every public hazard node must be reachable through public transitions.");
        }

        if (!publicRegionIds.SetEquals(scenario.HazardTransportEnvelopeRegionIds))
        {
            throw new InvalidDataException(
                "Declared hazard envelope regions must exactly match the public stochastic nodes.");
        }
    }


    private static void ValidateWeftEvaluation(
        ScenarioDefinition scenario,
        ISet<string> regionIds)
    {
        var definition = scenario.WeftEvaluation;
        if (definition is null)
        {
            if (scenario.SchemaVersion == 3)
            {
                throw new InvalidDataException(
                    "Scenario schema version 3 requires a weft evaluation definition.");
            }

            if (scenario.EvaluationSearchableRegionIds.Count > 0 &&
                scenario.SchemaVersion is not (4 or 5))
            {
                throw new InvalidDataException(
                    "A target-specific searchable region set requires scenario schema version 3.");
            }

            return;
        }

        if (scenario.SchemaVersion != 3)
        {
            throw new InvalidDataException(
                "Weft evaluation definitions require scenario schema version 3.");
        }

        RequireText(definition.EncounterStepId, "Weft encounter step id");
        RequireText(definition.OtherPersonId, "Weft other person id");
        RequireText(definition.TargetDeviceId, "Weft target device id");
        if (!scenario.Steps.Any(step => string.Equals(
            step.Id,
            definition.EncounterStepId,
            StringComparison.Ordinal)))
        {
            throw new InvalidDataException(
                $"Unknown weft encounter step '{definition.EncounterStepId}'.");
        }

        var encounterStep = scenario.Steps.Single(step => string.Equals(
            step.Id,
            definition.EncounterStepId,
            StringComparison.Ordinal));
        if (encounterStep.Kind != EventKind.Encounter ||
            encounterStep.Tick != definition.HistoricalEndTick ||
            !string.Equals(
                encounterStep.RegionId ?? encounterStep.Position?.RegionId,
                definition.EncounterRegionId,
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "The M1-C3 encounter step must match the configured encounter region and window end.");
        }

        if (!scenario.Entities.Persons.Any(person => string.Equals(
            person.Id,
            definition.OtherPersonId,
            StringComparison.Ordinal)))
        {
            throw new InvalidDataException(
                $"Unknown weft participant '{definition.OtherPersonId}'.");
        }

        if (!scenario.Entities.Devices.Any(device => string.Equals(
            device.Id,
            definition.TargetDeviceId,
            StringComparison.Ordinal)))
        {
            throw new InvalidDataException(
                $"Unknown weft target device '{definition.TargetDeviceId}'.");
        }


        if (!string.Equals(
            scenario.StochasticHazard?.DeviceId,
            definition.TargetDeviceId,
            StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "The M1-C3 target device must match the stochastic-hazard device.");
        }

        foreach (var regionId in definition.HistoricalCandidateRegionIds
            .Append(definition.EncounterRegionId)
            .Append(definition.ConflictingRegionId)
            .Concat(scenario.EvaluationSearchableRegionIds))
        {
            if (!regionIds.Contains(regionId))
            {
                throw new InvalidDataException($"Unknown M1-C3 region '{regionId}'.");
            }
        }

        if (definition.HistoricalCandidateRegionIds.Count == 0 ||
            scenario.EvaluationSearchableRegionIds.Count == 0)
        {
            throw new InvalidDataException(
                "M1-C3 historical candidates and searchable regions cannot be empty.");
        }


        if (!definition.HistoricalCandidateRegionIds.Contains(
                definition.EncounterRegionId,
                StringComparer.Ordinal) ||
            !definition.HistoricalCandidateRegionIds.Contains(
                definition.ConflictingRegionId,
                StringComparer.Ordinal))
        {
            throw new InvalidDataException(
                "M1-C3 encounter and conflicting regions must be public historical candidates.");
        }

        if (definition.HistoricalStartTick < 0 ||
            definition.HistoricalEndTick < definition.HistoricalStartTick ||
            definition.NormalAvailableAtTick < definition.HistoricalEndTick ||
            definition.DelayedAvailableAtTick <= definition.NormalAvailableAtTick ||
            definition.EvaluationEndTick < definition.DelayedAvailableAtTick)
        {
            throw new InvalidDataException("M1-C3 tick boundaries are inconsistent.");
        }


        var maximumHazardTick = scenario.StochasticHazard?.Nodes
            .Select(node => node.MaximumArrivalTick)
            .DefaultIfEmpty(0)
            .Max() ?? 0;
        if (definition.EvaluationEndTick < maximumHazardTick)
        {
            throw new InvalidDataException(
                "M1-C3 evaluation end must cover the declared hazard arrival windows.");
        }

        var searchable = scenario.EvaluationSearchableRegionIds.ToHashSet(StringComparer.Ordinal);
        var missingTerminal = scenario.StochasticHazard?.Nodes
            .Where(node => node.IsRequiredTerminal)
            .Select(node => node.RegionId)
            .FirstOrDefault(regionId => !searchable.Contains(regionId));
        if (missingTerminal is not null)
        {
            throw new InvalidDataException(
                $"M1-C3 searchable regions omit required terminal '{missingTerminal}'.");
        }

        if (definition.ConditionCycle.Count == 0 ||
            definition.ConditionCycle.Distinct().Count() !=
            Enum.GetValues<WeftEvidenceCondition>().Length)
        {
            throw new InvalidDataException(
                "M1-C3 condition cycle must contain every evidence condition at least once.");
        }
    }

    private static void ValidateCausalTracingEvaluation(
        ScenarioDefinition scenario,
        ISet<string> regionIds,
        ISet<string> entityIds)
    {
        var definition = scenario.CausalTracingEvaluation;
        if (definition is null)
        {
            if (scenario.SchemaVersion == 4)
            {
                throw new InvalidDataException(
                    "Scenario schema version 4 requires a causal tracing evaluation definition.");
            }

            return;
        }

        if (scenario.SchemaVersion != 4 || scenario.WeftEvaluation is not null)
        {
            throw new InvalidDataException(
                "Causal tracing evaluation requires schema version 4 and cannot replace M1-C3 semantics.");
        }

        if (definition.EvaluationEndTick < 0 || definition.BaseCandidateStates.Count == 0 ||
            scenario.EvaluationSearchableRegionIds.Count == 0)
        {
            throw new InvalidDataException(
                "M1-D requires an evaluation end tick, public candidate states, and target-searchable regions.");
        }

        if (definition.ConditionCycle.Count == 0 ||
            definition.ConditionCycle.Distinct().Count() != Enum.GetValues<CausalTracingCondition>().Length)
        {
            throw new InvalidDataException(
                "M1-D condition cycle must contain every tracing condition at least once.");
        }

        foreach (var state in definition.BaseCandidateStates)
        {
            if (state.StartTick < 0 || state.EndTick < state.StartTick || state.RegionIds.Count == 0 ||
                state.RegionIds.Any(regionId => !regionIds.Contains(regionId)))
            {
                throw new InvalidDataException("M1-D contains an invalid public candidate state.");
            }

            if (state.TemporalKind == Interactions.CandidateWarps.CandidateWarpTemporalKind.EventPoint &&
                state.StartTick != state.EndTick)
            {
                throw new InvalidDataException("M1-D event-point candidate states require an exact tick.");
            }
        }

        var definitionIds = UniqueIds(definition.Constraints.Select(item => item.Id), "tracing constraint");
        _ = definitionIds;
        foreach (var constraint in definition.Constraints)
        {
            RequireText(constraint.SourceObservationKey, "Tracing source observation key");
            RequireText(constraint.DirectSourceId, "Tracing direct source id");
            RequireText(constraint.RelationKind, "Tracing relation kind");
            RequireText(constraint.DerivationKind, "Tracing derivation kind");
            if (constraint.OtherEntityId is not null && !entityIds.Contains(constraint.OtherEntityId))
            {
                throw new InvalidDataException(
                    $"Tracing constraint '{constraint.Id}' refers to unknown entity '{constraint.OtherEntityId}'.");
            }

            if (constraint.ObservedStartTick < 0 ||
                constraint.ObservedEndTick < constraint.ObservedStartTick ||
                constraint.ConstrainedStartTick < 0 ||
                constraint.ConstrainedEndTick < constraint.ConstrainedStartTick ||
                constraint.AvailableAtTick < constraint.ObservedEndTick ||
                constraint.AllowedRegionIds.Count == 0 ||
                constraint.AllowedRegionIds.Any(regionId => !regionIds.Contains(regionId)))
            {
                throw new InvalidDataException($"Tracing constraint '{constraint.Id}' is inconsistent.");
            }

            if (constraint.IsPersonEvidence && constraint.IsDeviceOnlyEvidence)
            {
                throw new InvalidDataException(
                    $"Tracing constraint '{constraint.Id}' cannot cross the person/device boundary.");
            }

            if (constraint.OccurrenceStepId is not null && !scenario.Steps.Any(step =>
                string.Equals(step.Id, constraint.OccurrenceStepId, StringComparison.Ordinal)))
            {
                throw new InvalidDataException(
                    $"Tracing constraint '{constraint.Id}' refers to unknown occurrence step.");
            }

            if (constraint.AppliesToConditions.Count == 0)
            {
                throw new InvalidDataException(
                    $"Tracing constraint '{constraint.Id}' must identify at least one synthetic condition.");
            }
        }

        foreach (var sourceGroup in definition.Constraints
            .Where(item => item.SourceKind == Interactions.Tracing.TracingConstraintSourceKind.CrossThreadObservation)
            .GroupBy(item => item.SourceObservationKey, StringComparer.Ordinal))
        {
            var sourceDescriptions = sourceGroup.Select(item => (
                    item.DirectSourceId,
                    item.OtherEntityId,
                    item.ObservedStartTick,
                    item.ObservedEndTick,
                    item.EvidenceState,
                    item.IsPersonEvidence,
                    item.IsDeviceOnlyEvidence,
                    item.Visibility))
                .Distinct()
                .Count();
            if (sourceDescriptions != 1)
            {
                throw new InvalidDataException(
                    $"Tracing source observation '{sourceGroup.Key}' has inconsistent source semantics.");
            }
        }

        var searchable = scenario.EvaluationSearchableRegionIds.ToHashSet(StringComparer.Ordinal);
        if (scenario.StochasticHazard?.Nodes
            .Where(node => node.IsRequiredTerminal)
            .Any(node => !searchable.Contains(node.RegionId)) == true)
        {
            throw new InvalidDataException("M1-D searchable regions omit a required terminal.");
        }
    }

    private static void ValidatePastStateReconstruction(
        ScenarioDefinition scenario,
        ISet<string> regionIds,
        ISet<string> entityIds)
    {
        var definition = scenario.PastStateReconstruction;
        if (definition is null)
        {
            if (scenario.SchemaVersion == 5)
            {
                throw new InvalidDataException(
                    "Scenario schema version 5 requires a past-state reconstruction definition.");
            }

            return;
        }

        if (scenario.SchemaVersion != 5 ||
            scenario.WeftEvaluation is not null ||
            scenario.CausalTracingEvaluation is not null)
        {
            throw new InvalidDataException(
                "Past-state reconstruction requires schema version 5 and a separate scenario definition.");
        }

        RequireText(definition.QueryContextId, "Past-state query context id");
        if (definition.TargetStartTick < 0 ||
            definition.TargetEndTick < definition.TargetStartTick ||
            definition.EvidenceAvailabilityCutoff < definition.TargetEndTick ||
            definition.EvaluationEndTick < definition.EvidenceAvailabilityCutoff ||
            definition.LocalTraversalWindowPaddingTicks < 0 ||
            definition.PublicCandidateStates.Count == 0 ||
            scenario.EvaluationSearchableRegionIds.Count == 0)
        {
            throw new InvalidDataException("M1-E query, cutoff, or public candidate-state boundaries are invalid.");
        }

        if (definition.ConditionCycle.Count == 0 ||
            definition.ConditionCycle.Distinct().Count() != Enum.GetValues<PastStateEvidenceCondition>().Length)
        {
            throw new InvalidDataException(
                "M1-E condition cycle must contain every past-state evidence condition at least once.");
        }

        foreach (var state in definition.PublicCandidateStates)
        {
            if (state.StartTick < 0 ||
                state.EndTick < state.StartTick ||
                state.RegionIds.Count == 0 ||
                state.RegionIds.Any(regionId => !regionIds.Contains(regionId)))
            {
                throw new InvalidDataException("M1-E contains an invalid public candidate state.");
            }

            if (state.TemporalKind == Interactions.CandidateWarps.CandidateWarpTemporalKind.EventPoint &&
                state.StartTick != state.EndTick)
            {
                throw new InvalidDataException("M1-E event-point candidate states require an exact tick.");
            }
        }

        if (!definition.PublicCandidateStates.Any(state =>
            state.StartTick <= definition.TargetEndTick &&
            definition.TargetStartTick <= state.EndTick))
        {
            throw new InvalidDataException("M1-E public candidate states do not cover the target window.");
        }

        var definitionIds = UniqueIds(
            definition.Constraints.Select(item => item.Id),
            "past-state constraint");
        if (definition.GenerationConstraintDefinitionIds.Count == 0 ||
            definition.GenerationConstraintDefinitionIds.Any(item => !definitionIds.Contains(item)) ||
            definition.VerificationConstraintDefinitionIds.Count == 0 ||
            definition.VerificationConstraintDefinitionIds.Any(item => !definitionIds.Contains(item)) ||
            definition.TraversalConstraintDefinitionIds.Count == 0 ||
            definition.TraversalConstraintDefinitionIds.Any(item => !definitionIds.Contains(item)))
        {
            throw new InvalidDataException(
                "M1-E generation, verification, and traversal constraint identifiers must be declared explicitly.");
        }

        if (definition.GenerationConstraintDefinitionIds.Intersect(
                definition.VerificationConstraintDefinitionIds,
                StringComparer.Ordinal).Any() ||
            definition.GenerationConstraintDefinitionIds.Intersect(
                definition.TraversalConstraintDefinitionIds,
                StringComparer.Ordinal).Any() ||
            definition.VerificationConstraintDefinitionIds.Intersect(
                definition.TraversalConstraintDefinitionIds,
                StringComparer.Ordinal).Any())
        {
            throw new InvalidDataException(
                "M1-E generation, verification, and traversal definition identifiers cannot overlap.");
        }

        var generationSources = definition.Constraints
            .Where(item => definition.GenerationConstraintDefinitionIds.Contains(item.Id, StringComparer.Ordinal))
            .Select(item => item.DirectSourceId)
            .ToHashSet(StringComparer.Ordinal);
        var verificationSources = definition.Constraints
            .Where(item => definition.VerificationConstraintDefinitionIds.Contains(item.Id, StringComparer.Ordinal))
            .Select(item => item.DirectSourceId)
            .ToHashSet(StringComparer.Ordinal);
        var traversalSources = definition.Constraints
            .Where(item => definition.TraversalConstraintDefinitionIds.Contains(item.Id, StringComparer.Ordinal))
            .Select(item => item.DirectSourceId)
            .ToHashSet(StringComparer.Ordinal);
        if (generationSources.Overlaps(verificationSources) ||
            generationSources.Overlaps(traversalSources) ||
            verificationSources.Overlaps(traversalSources))
        {
            throw new InvalidDataException(
                "M1-E direct sources cannot cross usage roles without an explicit per-constraint usage contract.");
        }

        foreach (var constraint in definition.Constraints)
        {
            RequireText(constraint.SourceObservationKey, "Past-state source observation key");
            RequireText(constraint.DirectSourceId, "Past-state direct source id");
            RequireText(constraint.RelationKind, "Past-state relation kind");
            RequireText(constraint.DerivationKind, "Past-state derivation kind");
            if (constraint.OtherEntityId is not null && !entityIds.Contains(constraint.OtherEntityId))
            {
                throw new InvalidDataException(
                    $"Past-state constraint '{constraint.Id}' refers to an unknown entity.");
            }

            if (constraint.ObservedStartTick < 0 ||
                constraint.ObservedEndTick < constraint.ObservedStartTick ||
                constraint.ConstrainedStartTick < 0 ||
                constraint.ConstrainedEndTick < constraint.ConstrainedStartTick ||
                constraint.AvailableAtTick < constraint.ObservedEndTick ||
                constraint.AllowedRegionIds.Count == 0 ||
                constraint.AllowedRegionIds.Any(regionId => !regionIds.Contains(regionId)) ||
                constraint.AppliesToPastStateConditions.Count == 0)
            {
                throw new InvalidDataException($"Past-state constraint '{constraint.Id}' is inconsistent.");
            }

            if (constraint.IsPersonEvidence && constraint.IsDeviceOnlyEvidence)
            {
                throw new InvalidDataException(
                    $"Past-state constraint '{constraint.Id}' cannot cross the person/device boundary.");
            }
        }

        foreach (var sourceGroup in definition.Constraints
            .Where(item => item.SourceKind == Interactions.Tracing.TracingConstraintSourceKind.CrossThreadObservation)
            .GroupBy(item => item.SourceObservationKey, StringComparer.Ordinal))
        {
            var sourceDescriptions = sourceGroup.Select(item => (
                    item.DirectSourceId,
                    item.OtherEntityId,
                    item.ObservedStartTick,
                    item.ObservedEndTick,
                    item.EvidenceState,
                    item.IsPersonEvidence,
                    item.IsDeviceOnlyEvidence,
                    item.Visibility))
                .Distinct()
                .Count();
            if (sourceDescriptions != 1)
            {
                throw new InvalidDataException(
                    $"Past-state source observation '{sourceGroup.Key}' has inconsistent semantics.");
            }
        }

        var searchable = scenario.EvaluationSearchableRegionIds.ToHashSet(StringComparer.Ordinal);
        if (definition.PublicCandidateStates
            .SelectMany(item => item.RegionIds)
            .Any(item => !searchable.Contains(item)))
        {
            throw new InvalidDataException("M1-E searchable regions omit a public candidate region.");
        }
    }

    private static void RejectCycles(
        StochasticHazardDefinition hazard,
        IEnumerable<string> regionIds)
    {
        var visiting = new HashSet<string>(StringComparer.Ordinal);
        var visited = new HashSet<string>(StringComparer.Ordinal);
        foreach (var regionId in regionIds.OrderBy(value => value, StringComparer.Ordinal))
        {
            Visit(regionId);
        }

        void Visit(string regionId)
        {
            if (visited.Contains(regionId))
            {
                return;
            }

            if (!visiting.Add(regionId))
            {
                throw new InvalidDataException("Stochastic hazard transitions cannot contain cycles.");
            }

            foreach (var destination in hazard.Transitions
                .Where(transition => string.Equals(
                    transition.SourceRegionId,
                    regionId,
                    StringComparison.Ordinal))
                .Select(transition => transition.DestinationRegionId)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal))
            {
                Visit(destination);
            }

            visiting.Remove(regionId);
            visited.Add(regionId);
        }
    }

    private static HashSet<string> ReachableRegions(
        StochasticHazardDefinition hazard,
        bool publicOnly = false)
    {
        var reachable = new HashSet<string>(StringComparer.Ordinal) { hazard.SourceRegionId };
        var pending = new Queue<string>();
        pending.Enqueue(hazard.SourceRegionId);
        while (pending.TryDequeue(out var source))
        {
            foreach (var destination in hazard.Transitions
                .Where(transition => !publicOnly || transition.PubliclyVisible)
                .Where(transition => string.Equals(
                    transition.SourceRegionId,
                    source,
                    StringComparison.Ordinal))
                .Select(transition => transition.DestinationRegionId))
            {
                if (reachable.Add(destination))
                {
                    pending.Enqueue(destination);
                }
            }
        }

        return reachable;
    }

    private static void RequireProbability(double value, string field)
    {
        if (!double.IsFinite(value) || value is < 0 or > 1)
        {
            throw new InvalidDataException($"{field} must be finite and between 0 and 1.");
        }
    }

    private static void RequireRegion(Position position, ISet<string> regionIds, string owner)
    {
        if (!regionIds.Contains(position.RegionId))
        {
            throw new InvalidDataException($"{owner} refers to unknown region '{position.RegionId}'.");
        }
    }

    private static void RequireText(string? value, string field)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidDataException($"{field} is required.");
        }
    }
}
