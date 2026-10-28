using FootprintSim.Events;
using FootprintSim.Model;

namespace FootprintSim.Scenarios;

public static class ScenarioValidator
{
    public static void Validate(ScenarioDefinition scenario)
    {
        ArgumentNullException.ThrowIfNull(scenario);

        if (scenario.SchemaVersion != 1)
        {
            throw new InvalidDataException($"Unsupported scenario schema version {scenario.SchemaVersion}.");
        }

        RequireText(scenario.Id, "Scenario id");
        RequireText(scenario.TargetPersonId, "Target person id");
        if (!double.IsFinite(scenario.NormalWalkingRadius) || scenario.NormalWalkingRadius < 0)
        {
            throw new InvalidDataException("Normal walking radius cannot be negative.");
        }

        var regionIds = UniqueIds(scenario.Regions.Select(region => region.Id), "region");
        foreach (var region in scenario.Regions)
        {
            if (!double.IsFinite(region.Passability) || region.Passability is < 0 or > 1)
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
                if (!double.IsFinite(connection.TravelCost))
                {
                    throw new InvalidDataException(
                        $"Region '{region.Id}' connection to '{connection.ToRegionId}' travel cost must be finite.");
                }

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
