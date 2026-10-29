using FootprintSim.Evidence;
using FootprintSim.Inference;

namespace FootprintSim.Evaluation;

public static class SearchStrategyIds
{
    public const string LastConfirmedOnly = "last-confirmed-only";
    public const string VoluntaryReachability = "voluntary-reachability";
    public const string HazardAware = "hazard-aware";
    public const string EvidenceAwareCurrent = "evidence-aware-current";
    public const string OwnEvidenceConstrained = "own-evidence-constrained";
    public const string CrossThreadConstrained = "cross-thread-constrained";
    public const string SingleWeft = "single-weft";
    public const string MultiWeftTracing = "multi-weft-tracing";
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
            confirmed is null ? [] : [confirmed],
            confirmed is null
                ? []
                : StrategySupport.LastConfirmedProvenance(input, confirmed));
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
            confirmed is null ? [] : [confirmed],
            StrategySupport.VoluntaryProvenance(input, candidates));
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
                confirmed is null ? [] : [confirmed],
                StrategySupport.VoluntaryProvenance(input, summary.NormalMovementRegionIds));
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
            confirmed is null ? [] : [confirmed],
            StrategySupport.HazardAndObservationProvenance(input, candidates));
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
            confirmed is null ? [] : [confirmed],
            StrategySupport.EvidenceAwareProvenance(
                input,
                candidates,
                summary.NormalMovementRegionIds));
    }
}

public static class SearchStrategyRegistry
{
    public static IReadOnlyList<ISearchStrategy> All { get; } =
    [
        new EvidenceAwareCurrentStrategy(),
        new HazardAwareStrategy(),
        new LastConfirmedOnlyStrategy(),
        new MultiWeftTracingStrategy(),
        new OwnEvidenceConstrainedStrategy(),
        new CrossThreadConstrainedStrategy(),
        new SingleWeftStrategy(),
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
        var publicContext = input.PublicContext.PublicHazardEnvelope is { } envelope
            ? input.PublicContext with
            {
                DeclaredHazardEnvelopeRegionIds = envelope.Regions
                    .Where(region => region.PublishedAtTick <= input.CurrentTick)
                    .Select(region => region.RegionId)
                    .OrderBy(regionId => regionId, StringComparer.Ordinal)
                    .ToArray()
            }
            : input.PublicContext;
        return new BasicRescueInference().Infer(publicContext, input.AvailableEvidence);
    }

    public static SearchStrategyResult Result(
        ISearchStrategy strategy,
        SearchStrategyInput input,
        IEnumerable<string> candidates,
        EvidenceState state,
        IEnumerable<string> rationaleCodes,
        IEnumerable<string> confirmedPersonRegions,
        IEnumerable<CandidateRegionProvenance>? provenance = null)
    {
        var publicRegionIds = input.PublicContext.PublicRegions
            .Select(region => region.Id)
            .ToHashSet(StringComparer.Ordinal);
        var orderedCandidates = candidates
            .Distinct(StringComparer.Ordinal)
            .Where(publicRegionIds.Contains)
            .ToArray();
        var candidateIndex = orderedCandidates
            .Select((regionId, index) => (regionId, index))
            .ToDictionary(item => item.regionId, item => item.index, StringComparer.Ordinal);
        var orderedProvenance = (provenance ?? [])
            .Where(item => candidateIndex.ContainsKey(item.RegionId))
            .Distinct()
            .OrderBy(item => candidateIndex[item.RegionId])
            .ThenBy(item => item.SourceKind)
            .ThenBy(item => item.SourceIdentifier, StringComparer.Ordinal)
            .ToList();
        foreach (var candidate in orderedCandidates.Where(candidate =>
            !orderedProvenance.Any(item => string.Equals(
                item.RegionId,
                candidate,
                StringComparison.Ordinal))))
        {
            orderedProvenance.Add(new CandidateRegionProvenance
            {
                RegionId = candidate,
                SourceKind = CandidateSourceKind.Unsupported,
                SourceIdentifier = $"strategy:{strategy.Id}",
                AvailableAtTick = input.CurrentTick,
                IsPersonEvidence = false,
                IsDeviceOnlyEvidence = false,
                EvidenceBoundaryState = EvidenceState.UnsupportedForQuery
            });
        }

        orderedProvenance = orderedProvenance
            .OrderBy(item => candidateIndex[item.RegionId])
            .ThenBy(item => item.SourceKind)
            .ThenBy(item => item.SourceIdentifier, StringComparer.Ordinal)
            .ToList();

        return PublicSearchDomainProjection.Apply(input, new SearchStrategyResult
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
                .ToArray(),
            CandidateRegionProvenance = orderedProvenance,
            PreferredCandidateRegionIds = orderedCandidates,
            OuterPlausibleCandidateRegionIds = orderedCandidates,
            HasCandidateStandingSets = true
        });
    }

    public static IReadOnlyList<CandidateRegionProvenance> LastConfirmedProvenance(
        SearchStrategyInput input,
        string regionId)
    {
        var observation = LocatedPersonObservations(input)
            .Where(item => item.State == EvidenceState.Confirmed)
            .LastOrDefault();
        return observation is null
            ? []
            : [FromObservation(
                regionId,
                observation,
                CandidateSourceKind.LastConfirmed,
                isPersonEvidence: true,
                isDeviceOnlyEvidence: false)];
    }

    public static IReadOnlyList<CandidateRegionProvenance> VoluntaryProvenance(
        SearchStrategyInput input,
        IEnumerable<string> regions)
    {
        var anchor = LocatedPersonObservations(input)
            .Where(item => item.State == EvidenceState.Confirmed)
            .LastOrDefault() ?? LocatedPersonObservations(input).LastOrDefault();
        return regions.Select(regionId => new CandidateRegionProvenance
        {
            RegionId = regionId,
            SourceKind = CandidateSourceKind.VoluntaryReachability,
            SourceIdentifier = anchor?.ObservationId ?? "no-person-anchor",
            AvailableAtTick = anchor?.AvailableTick ?? input.CurrentTick,
            IsPersonEvidence = anchor is not null,
            IsDeviceOnlyEvidence = false,
            EvidenceBoundaryState = anchor?.State ?? EvidenceState.Indeterminate
        }).ToArray();
    }

    public static IReadOnlyList<CandidateRegionProvenance> HazardAndObservationProvenance(
        SearchStrategyInput input,
        IEnumerable<string> regions)
    {
        var result = HazardProvenance(input, regions).ToList();
        var lastReported = LocatedPersonObservations(input).LastOrDefault();
        if (lastReported?.ReportedRegionId is { } regionId && regions.Contains(
            regionId,
            StringComparer.Ordinal))
        {
            result.Add(FromObservation(
                regionId,
                lastReported,
                lastReported.State == EvidenceState.Conflicting
                    ? CandidateSourceKind.ConflictingObservation
                    : CandidateSourceKind.PersonObservation,
                isPersonEvidence: true,
                isDeviceOnlyEvidence: false));
        }

        return result;
    }

    public static IReadOnlyList<CandidateRegionProvenance> EvidenceAwareProvenance(
        SearchStrategyInput input,
        IEnumerable<string> regions,
        IEnumerable<string> voluntaryRegions)
    {
        var candidateSet = regions.ToHashSet(StringComparer.Ordinal);
        var result = HazardProvenance(input, candidateSet)
            .Concat(VoluntaryProvenance(
                input,
                voluntaryRegions.Where(candidateSet.Contains)))
            .ToList();
        foreach (var observation in input.AvailableEvidence.Observations
            .Where(item => item.ReportedRegionId is not null)
            .OrderBy(item => item.AvailableTick)
            .ThenBy(item => item.ObservationId, StringComparer.Ordinal))
        {
            var regionId = observation.ReportedRegionId!;
            if (!candidateSet.Contains(regionId))
            {
                continue;
            }

            var isPerson = observation.SubjectIds.Contains(
                input.PublicContext.TargetPersonId,
                StringComparer.Ordinal);
            var isDeviceOnly = !isPerson && observation.SubjectIds.Any(subjectId =>
                input.PublicContext.PublicDeviceIds.Contains(subjectId, StringComparer.Ordinal));
            result.Add(FromObservation(
                regionId,
                observation,
                observation.State == EvidenceState.Conflicting
                    ? CandidateSourceKind.ConflictingObservation
                    : isPerson
                        ? CandidateSourceKind.PersonObservation
                        : isDeviceOnly
                            ? CandidateSourceKind.DeviceOnlyObservation
                            : CandidateSourceKind.Unsupported,
                isPerson,
                isDeviceOnly));
        }

        return result;
    }

    private static IEnumerable<CandidateRegionProvenance> HazardProvenance(
        SearchStrategyInput input,
        IEnumerable<string> regions)
    {
        var candidateSet = regions.ToHashSet(StringComparer.Ordinal);
        var envelope = input.PublicContext.PublicHazardEnvelope;
        if (envelope is null)
        {
            return input.PublicContext.DeclaredHazardEnvelopeRegionIds
                .Where(candidateSet.Contains)
                .Select(regionId => new CandidateRegionProvenance
                {
                    RegionId = regionId,
                    SourceKind = CandidateSourceKind.PublicHazardEnvelope,
                    SourceIdentifier = "scenario-declared-hazard-envelope",
                    AvailableAtTick = 0,
                    IsPersonEvidence = false,
                    IsDeviceOnlyEvidence = false,
                    EvidenceBoundaryState = EvidenceState.SupportedButUncertain
                });
        }

        if (envelope.PublishedAtTick > input.CurrentTick)
        {
            return [];
        }

        return envelope.Regions
            .Where(region => region.PublishedAtTick <= input.CurrentTick)
            .Where(region => candidateSet.Contains(region.RegionId))
            .SelectMany(region => region.ProvenanceIds.Select(sourceId =>
                new CandidateRegionProvenance
                {
                    RegionId = region.RegionId,
                    SourceKind = CandidateSourceKind.PublicHazardEnvelope,
                    SourceIdentifier = sourceId,
                    AvailableAtTick = region.PublishedAtTick,
                    IsPersonEvidence = false,
                    IsDeviceOnlyEvidence = false,
                    EvidenceBoundaryState = EvidenceState.SupportedButUncertain
                }));
    }

    private static ObservationRecord[] LocatedPersonObservations(SearchStrategyInput input)
    {
        return input.AvailableEvidence.Observations
            .Where(item => item.ReportedRegionId is not null)
            .Where(item => item.SubjectIds.Contains(
                input.PublicContext.TargetPersonId,
                StringComparer.Ordinal))
            .OrderBy(item => item.ObservedTick)
            .ThenBy(item => item.AvailableTick)
            .ThenBy(item => item.ObservationId, StringComparer.Ordinal)
            .ToArray();
    }

    private static CandidateRegionProvenance FromObservation(
        string regionId,
        ObservationRecord observation,
        CandidateSourceKind sourceKind,
        bool isPersonEvidence,
        bool isDeviceOnlyEvidence)
    {
        return new CandidateRegionProvenance
        {
            RegionId = regionId,
            SourceKind = sourceKind,
            SourceIdentifier = observation.ObservationId,
            AvailableAtTick = observation.AvailableTick,
            IsPersonEvidence = isPersonEvidence,
            IsDeviceOnlyEvidence = isDeviceOnlyEvidence,
            EvidenceBoundaryState = observation.State
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
