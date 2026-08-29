using FootprintSim.Evidence;
using FootprintSim.Hazards;
using FootprintSim.Model;

namespace FootprintSim.Inference;

public sealed record RescueContext
{
    public required string ScenarioId { get; init; }

    public required string TargetPersonId { get; init; }

    public required double NormalWalkingRadius { get; init; }

    public IReadOnlyList<RegionDefinition> PublicRegions { get; init; } = [];

    public IReadOnlyList<string> DeclaredHazardEnvelopeRegionIds { get; init; } = [];

    public PublicHazardEnvelope? PublicHazardEnvelope { get; init; }

    public IReadOnlyList<string> PublicDeviceIds { get; init; } = [];
}

public sealed record SearchZoneSummary
{
    public required string ScenarioId { get; init; }

    public required string TargetPersonId { get; init; }

    public required EvidenceState EvidenceState { get; init; }

    public Position? LastConfirmedPosition { get; init; }

    public Position? LastReportedPosition { get; init; }

    public EvidenceState? LastReportedPositionState { get; init; }

    public required double NormalWalkingRadius { get; init; }

    public IReadOnlyList<string> NormalMovementRegionIds { get; init; } = [];

    public IReadOnlyList<string> HazardTransportEnvelopeRegionIds { get; init; } = [];

    public bool HazardTransportExpandsSearchZone { get; init; }

    public IReadOnlyList<SearchPriorityZone> SearchPriorityZones { get; init; } = [];

    public IReadOnlyList<string> Notes { get; init; } = [];
}

public sealed record SearchPriorityZone
{
    public required int Priority { get; init; }

    public required string Basis { get; init; }

    public required EvidenceState EvidenceState { get; init; }

    public IReadOnlyList<string> RegionIds { get; init; } = [];
}

public interface IRescueInference
{
    SearchZoneSummary Infer(RescueContext context, ObservationSet evidence);
}

// Deliberately consumes only public map/context plus ObservationSet; no ground-truth type is accepted.
public sealed class BasicRescueInference : IRescueInference
{
    public SearchZoneSummary Infer(RescueContext context, ObservationSet evidence)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(evidence);

        if (!string.Equals(context.ScenarioId, evidence.ScenarioId, StringComparison.Ordinal))
        {
            throw new ArgumentException("Rescue context and observation set belong to different scenarios.");
        }

        var relevant = evidence.Observations
            .Where(observation => observation.SubjectIds.Contains(
                context.TargetPersonId,
                StringComparer.Ordinal))
            .ToArray();
        var located = relevant
            .Where(observation => observation.ReportedPosition is not null)
            .OrderBy(observation => observation.ObservedTick)
            .ThenBy(observation => observation.AvailableTick)
            .ThenBy(observation => observation.ObservationId, StringComparer.Ordinal)
            .ToArray();
        var confirmed = located
            .Where(observation => observation.State == EvidenceState.Confirmed)
            .LastOrDefault();
        var lastReported = located.LastOrDefault();

        var state = DetermineState(evidence, relevant);
        var anchor = confirmed?.ReportedPosition ?? lastReported?.ReportedPosition;
        var normalRegions = anchor is null
            ? []
            : context.PublicRegions
                .Where(region => region.Passability > 0 &&
                    region.Center.PlanarDistanceTo(anchor) <= context.NormalWalkingRadius)
                .Select(region => region.Id)
                .OrderBy(id => id, StringComparer.Ordinal)
                .ToArray();
        var hazardRegions = context.DeclaredHazardEnvelopeRegionIds
            .Distinct(StringComparer.Ordinal)
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToArray();
        var expands = hazardRegions.Except(normalRegions, StringComparer.Ordinal).Any();
        var priorityZones = BuildPriorityZones(lastReported, normalRegions, hazardRegions);

        var notes = new List<string>
        {
            "Normal movement is a geometric baseline, not a reachability or survival prediction.",
            "Priority zones are candidates, not point predictions; no region is excluded by this M0 summary."
        };
        if (expands)
        {
            notes.Add("The declared hazard envelope includes regions outside the normal walking baseline.");
        }

        return new SearchZoneSummary
        {
            ScenarioId = context.ScenarioId,
            TargetPersonId = context.TargetPersonId,
            EvidenceState = state,
            LastConfirmedPosition = confirmed?.ReportedPosition,
            LastReportedPosition = lastReported?.ReportedPosition,
            LastReportedPositionState = lastReported?.State,
            NormalWalkingRadius = context.NormalWalkingRadius,
            NormalMovementRegionIds = normalRegions,
            HazardTransportEnvelopeRegionIds = hazardRegions,
            HazardTransportExpandsSearchZone = expands,
            SearchPriorityZones = priorityZones,
            Notes = notes
        };
    }

    private static IReadOnlyList<SearchPriorityZone> BuildPriorityZones(
        ObservationRecord? lastReported,
        IReadOnlyList<string> normalRegions,
        IReadOnlyList<string> hazardRegions)
    {
        var zones = new List<SearchPriorityZone>();
        if (lastReported?.ReportedRegionId is not null)
        {
            zones.Add(new SearchPriorityZone
            {
                Priority = zones.Count + 1,
                Basis = "last-reported-person-observation",
                EvidenceState = lastReported.State,
                RegionIds = [lastReported.ReportedRegionId]
            });
        }

        if (hazardRegions.Count > 0)
        {
            zones.Add(new SearchPriorityZone
            {
                Priority = zones.Count + 1,
                Basis = "declared-hazard-transport-envelope",
                EvidenceState = EvidenceState.SupportedButUncertain,
                RegionIds = hazardRegions
            });
        }

        if (normalRegions.Count > 0)
        {
            zones.Add(new SearchPriorityZone
            {
                Priority = zones.Count + 1,
                Basis = "normal-movement-baseline-from-last-confirmed-position",
                EvidenceState = EvidenceState.SupportedButUncertain,
                RegionIds = normalRegions
            });
        }

        return zones;
    }

    private static EvidenceState DetermineState(
        ObservationSet evidence,
        IReadOnlyCollection<ObservationRecord> relevant)
    {
        if (relevant.Count == 0)
        {
            return evidence.Observations.Count == 0
                ? EvidenceState.Indeterminate
                : EvidenceState.UnsupportedForQuery;
        }

        var latestObservedTick = relevant.Max(observation => observation.ObservedTick);
        var latest = relevant
            .Where(observation => observation.ObservedTick == latestObservedTick)
            .ToArray();

        if (latest.Any(observation => observation.State == EvidenceState.Conflicting))
        {
            return EvidenceState.Conflicting;
        }

        return latest.Any(observation => observation.State == EvidenceState.Confirmed)
            ? EvidenceState.Confirmed
            : EvidenceState.SupportedButUncertain;
    }
}
