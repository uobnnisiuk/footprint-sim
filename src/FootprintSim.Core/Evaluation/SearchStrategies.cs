using FootprintSim.Evidence;
using FootprintSim.Inference;

namespace FootprintSim.Evaluation;

public static class SearchStrategyIds
{
    public const string LastConfirmedOnly = "last-confirmed-only";
    public const string VoluntaryReachability = "voluntary-reachability";
    public const string HazardAware = "hazard-aware";
    public const string EvidenceAwareCurrent = "evidence-aware-current";
}

public sealed class LastConfirmedOnlyStrategy : ISearchStrategy
{
    public string Id => SearchStrategyIds.LastConfirmedOnly;

    public SearchStrategyResult Evaluate(SearchStrategyInput input)
    {
        var summary = StrategySupport.Infer(input);
        var confirmed = summary.LastConfirmedPosition?.RegionId;

        return StrategySupport.Result(
            this,
            input,
            confirmed is null ? [] : [confirmed],
            summary.EvidenceState,
            confirmed is null
                ? ["no-confirmed-person-region"]
                : ["last-confirmed-person-region"],
            confirmed is null ? [] : [confirmed]);
    }
}

public sealed class VoluntaryReachabilityStrategy : ISearchStrategy
{
    public string Id => SearchStrategyIds.VoluntaryReachability;

    public SearchStrategyResult Evaluate(SearchStrategyInput input)
    {
        var summary = StrategySupport.Infer(input);
        var confirmed = summary.LastConfirmedPosition?.RegionId;
        IReadOnlyList<string> candidates = confirmed is null ? [] : summary.NormalMovementRegionIds;
        IReadOnlyList<string> rationale = confirmed is null
            ? ["no-confirmed-person-region", "normal-movement-unavailable"]
            : ["normal-movement-baseline", "anchored-at-last-confirmed-person-observation"];

        return StrategySupport.Result(
            this,
            input,
            candidates,
            summary.EvidenceState,
            rationale,
            confirmed is null ? [] : [confirmed]);
    }
}

public sealed class HazardAwareStrategy : ISearchStrategy
{
    public string Id => SearchStrategyIds.HazardAware;

    public SearchStrategyResult Evaluate(SearchStrategyInput input)
    {
        var summary = StrategySupport.Infer(input);
        var confirmed = summary.LastConfirmedPosition?.RegionId;
        var hazardRegions = summary.HazardTransportEnvelopeRegionIds;
        var hazardEvidence = input.AvailableEvidence.Observations
            .Where(observation => observation.SubjectIds.Contains(
                input.PublicContext.TargetPersonId,
                StringComparer.Ordinal))
            .Where(observation => observation.ReportedRegionId is not null)
            .Any(observation => hazardRegions.Contains(
                observation.ReportedRegionId!,
                StringComparer.Ordinal));

        if (!hazardEvidence || hazardRegions.Count == 0)
        {
            return StrategySupport.Result(
                this,
                input,
                summary.NormalMovementRegionIds,
                summary.EvidenceState,
                [
                    "no-person-observation-in-declared-hazard-envelope",
                    "normal-movement-fallback"
                ],
                confirmed is null ? [] : [confirmed]);
        }

        var candidates = hazardRegions
            .Concat(summary.LastReportedPosition?.RegionId is null
                ? []
                : [summary.LastReportedPosition.RegionId])
            .Distinct(StringComparer.Ordinal)
            .OrderBy(regionId => regionId, StringComparer.Ordinal)
            .ToArray();

        return StrategySupport.Result(
            this,
            input,
            candidates,
            summary.EvidenceState,
            [
                "person-observation-in-declared-hazard-envelope",
                "declared-hazard-transport-envelope"
            ],
            confirmed is null ? [] : [confirmed]);
    }
}

public sealed class EvidenceAwareCurrentStrategy : ISearchStrategy
{
    public string Id => SearchStrategyIds.EvidenceAwareCurrent;

    public SearchStrategyResult Evaluate(SearchStrategyInput input)
    {
        var summary = StrategySupport.Infer(input);
        var candidates = summary.SearchPriorityZones
            .OrderBy(zone => zone.Priority)
            .SelectMany(zone => zone.RegionIds)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var rationale = new[] { "m0-current-inference-adapter" }
            .Concat(summary.SearchPriorityZones
                .OrderBy(zone => zone.Priority)
                .Select(zone => $"priority-basis:{zone.Basis}"))
            .ToArray();
        var confirmed = summary.LastConfirmedPosition?.RegionId;

        return StrategySupport.Result(
            this,
            input,
            candidates,
            summary.EvidenceState,
            rationale,
            confirmed is null ? [] : [confirmed]);
    }
}

public static class SearchStrategyRegistry
{
    public static IReadOnlyList<ISearchStrategy> All { get; } =
    [
        new EvidenceAwareCurrentStrategy(),
        new HazardAwareStrategy(),
        new LastConfirmedOnlyStrategy(),
        new VoluntaryReachabilityStrategy()
    ];

    public static IReadOnlyList<ISearchStrategy> Resolve(IEnumerable<string> identifiers)
    {
        ArgumentNullException.ThrowIfNull(identifiers);
        var requested = identifiers
            .Where(identifier => !string.IsNullOrWhiteSpace(identifier))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (requested.Length == 1 &&
            string.Equals(requested[0], "all", StringComparison.OrdinalIgnoreCase))
        {
            return All;
        }

        if (requested.Any(identifier =>
            string.Equals(identifier, "all", StringComparison.OrdinalIgnoreCase)))
        {
            throw new ArgumentException("Strategy 'all' cannot be combined with explicit identifiers.");
        }

        var byId = All.ToDictionary(strategy => strategy.Id, StringComparer.OrdinalIgnoreCase);
        var result = new List<ISearchStrategy>();
        foreach (var identifier in requested)
        {
            if (!byId.TryGetValue(identifier, out var strategy))
            {
                throw new ArgumentException($"Unknown strategy '{identifier}'.");
            }

            result.Add(strategy);
        }

        if (result.Count == 0)
        {
            throw new ArgumentException("At least one strategy is required.");
        }

        return result.OrderBy(strategy => strategy.Id, StringComparer.Ordinal).ToArray();
    }
}

internal static class StrategySupport
{
    public static SearchZoneSummary Infer(SearchStrategyInput input)
    {
        Validate(input);
        return new BasicRescueInference().Infer(input.PublicContext, input.AvailableEvidence);
    }

    public static SearchStrategyResult Result(
        ISearchStrategy strategy,
        SearchStrategyInput input,
        IEnumerable<string> candidates,
        EvidenceState state,
        IEnumerable<string> rationaleCodes,
        IEnumerable<string> confirmedPersonRegions)
    {
        var publicRegionIds = input.PublicContext.PublicRegions
            .Select(region => region.Id)
            .ToHashSet(StringComparer.Ordinal);
        var orderedCandidates = candidates
            .Distinct(StringComparer.Ordinal)
            .Where(publicRegionIds.Contains)
            .ToArray();

        return new SearchStrategyResult
        {
            StrategyId = strategy.Id,
            ScenarioId = input.ScenarioId,
            Seed = input.Seed,
            ProducedAtTick = input.CurrentTick,
            CandidateRegionIds = orderedCandidates,
            EvidenceBoundaryState = state,
            RationaleCodes = rationaleCodes.Distinct(StringComparer.Ordinal).ToArray(),
            ConfirmedPersonRegionIds = confirmedPersonRegions
                .Distinct(StringComparer.Ordinal)
                .Where(publicRegionIds.Contains)
                .OrderBy(regionId => regionId, StringComparer.Ordinal)
                .ToArray()
        };
    }

    private static void Validate(SearchStrategyInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (input.CurrentTick < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(input), "Current tick cannot be negative.");
        }

        if (!string.Equals(input.ScenarioId, input.PublicContext.ScenarioId, StringComparison.Ordinal) ||
            !string.Equals(input.ScenarioId, input.AvailableEvidence.ScenarioId, StringComparison.Ordinal) ||
            input.Seed != input.AvailableEvidence.Seed)
        {
            throw new ArgumentException("Strategy input identifiers do not match.");
        }

        if (input.AvailableEvidence.Observations.Any(observation =>
            observation.AvailableTick > input.CurrentTick))
        {
            throw new ArgumentException("Strategy input contains evidence unavailable at the current tick.");
        }
    }
}
