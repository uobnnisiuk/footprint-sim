namespace FootprintSim.Evaluation;

/// <summary>
/// Restrict reported search results to a public scope without changing evidence,
/// public topology, historical candidate states, or causal tracing internals.
/// Omitted regions are outside this query, not logically excluded by evidence.
/// </summary>
public static class PublicSearchDomainProjection
{
    public const string ScopeRationale = "restricted-to-predeclared-public-search-domain";

    public static SearchStrategyResult Apply(SearchStrategyInput input, SearchStrategyResult result)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(result);
        if (input.PublicSearchDomainRegionIds is not { } requested)
        {
            return result;
        }

        var map = input.PublicContext.PublicRegions.Select(region => region.Id).ToHashSet(StringComparer.Ordinal);
        var domain = requested.ToHashSet(StringComparer.Ordinal);
        if (requested.Any(string.IsNullOrWhiteSpace) || domain.Count != requested.Count ||
            domain.Any(region => !map.Contains(region)))
        {
            throw new ArgumentException("Public search domain must contain unique, nonblank public map region identifiers.", nameof(input));
        }

        var reportedRegions = result.CandidateRegionIds
            .Concat(result.PreferredCandidateRegionIds)
            .Concat(result.OuterPlausibleCandidateRegionIds)
            .Concat(result.ConflictedCandidateRegionIds)
            .Concat(result.ConfirmedPersonRegionIds)
            .Concat(result.ExplicitlyExcludedRegionIds)
            .Concat(result.CandidateRegionProvenance.Select(item => item.RegionId))
            .ToArray();
        if (reportedRegions.Any(region => !map.Contains(region)))
        {
            throw new InvalidOperationException("Strategy result contains a region outside the full public map.");
        }

        var changed = reportedRegions.Any(region => !domain.Contains(region));
        if (!changed)
        {
            return result;
        }

        return result with
        {
            CandidateRegionIds = Within(result.CandidateRegionIds),
            PreferredCandidateRegionIds = Within(result.PreferredCandidateRegionIds),
            OuterPlausibleCandidateRegionIds = Within(result.OuterPlausibleCandidateRegionIds),
            ConflictedCandidateRegionIds = Within(result.ConflictedCandidateRegionIds),
            ConfirmedPersonRegionIds = Within(result.ConfirmedPersonRegionIds),
            ExplicitlyExcludedRegionIds = Within(result.ExplicitlyExcludedRegionIds),
            CandidateRegionProvenance = result.CandidateRegionProvenance
                .Where(item => domain.Contains(item.RegionId)).ToArray(),
            RationaleCodes = result.RationaleCodes.Append(ScopeRationale).Distinct(StringComparer.Ordinal).ToArray()
        };

        string[] Within(IEnumerable<string> regions) => regions.Where(domain.Contains).ToArray();
    }
}
