using FootprintSim.Inference.PastStates;

namespace FootprintSim.Inference.SearchProjection;

/// <summary>A declared public relation for the entire historical window, not a realized path.</summary>
public sealed record PublicRegionProjection(
    string HistoricalRegionId,
    long StartTick,
    long EndTick,
    long TargetTick,
    IReadOnlyList<string> CurrentRegionIds,
    bool IsComplete,
    string SourceId,
    long AvailableTick);

public sealed record PublicSearchUnitMapping(
    string RegionId,
    long TargetTick,
    IReadOnlyList<string> UnitIds,
    bool IsComplete,
    string SourceId,
    long AvailableTick);

/// <summary>A declared footprint only; it neither executes a search nor guarantees detection.</summary>
public sealed record PublicActionFootprint(
    string ActionId,
    long TargetTick,
    IReadOnlyList<string> UnitIds,
    string SourceId,
    long AvailableTick);

public sealed record PublicSearchProjectionInput(
    string QueryId,
    string TargetEntityId,
    long TargetTick,
    long EvidenceCutoff,
    IReadOnlyList<PastStateCandidate> Candidates,
    IReadOnlyList<string> RegionIds,
    IReadOnlyList<string> SearchUnitIds,
    IReadOnlyList<PublicRegionProjection> RegionProjections,
    IReadOnlyList<PublicSearchUnitMapping> UnitMappings,
    IReadOnlyList<PublicActionFootprint> Actions);

public sealed record CandidateLocationProjection(
    PastStateCandidate SourceCandidate,
    long TargetTick,
    IReadOnlyList<string> KnownCurrentRegionIds,
    IReadOnlyList<string> KnownUnitIds,
    IReadOnlyList<string> UnresolvedReasons,
    IReadOnlyList<string> TransitionSourceIds,
    IReadOnlyList<string> UnitMappingSourceIds)
{
    public bool HasUnresolvedPossibilities => UnresolvedReasons.Count != 0;
}

public sealed record ActionSupportProjection(
    string ActionId,
    long TargetTick,
    string SourceId,
    IReadOnlyList<string> DeclaredUnitIds,
    IReadOnlyList<string> KnownIntersectingUnitIds,
    IReadOnlyList<string> KnownIntersectingCandidateIds,
    IReadOnlyList<string> WholeSupportContainedCandidateIds);

public sealed record PublicSearchProjectionResult(
    string ContractVersion,
    string QueryId,
    string TargetEntityId,
    long TargetTick,
    long EvidenceCutoff,
    IReadOnlyList<CandidateLocationProjection> Candidates,
    IReadOnlyList<string> KnownCurrentRegionIds,
    IReadOnlyList<string> KnownUnitIds,
    IReadOnlyList<string> UnresolvedCandidateIds,
    IReadOnlyList<ActionSupportProjection> Actions);

/// <summary>
/// Projects public candidates using explicit, time-indexed public relations.
/// Completeness is a caller's model declaration, not independently verified real-world safety.
/// Unknown alternatives survive projection; no evidence or search priority is manufactured.
/// </summary>
public static class PublicSearchProjector
{
    public static PublicSearchProjectionResult Project(PublicSearchProjectionInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        RequireId(input.QueryId);
        RequireId(input.TargetEntityId);
        if (input.EvidenceCutoff < 0 || input.TargetTick < input.EvidenceCutoff)
        {
            throw new InvalidDataException("Projection cutoff must be nonnegative and no later than target tick.");
        }

        var regions = UniqueIds(input.RegionIds);
        var units = UniqueIds(input.SearchUnitIds);
        ArgumentNullException.ThrowIfNull(input.Candidates);
        ArgumentNullException.ThrowIfNull(input.RegionProjections);
        ArgumentNullException.ThrowIfNull(input.UnitMappings);
        ArgumentNullException.ThrowIfNull(input.Actions);

        var transitions = new Dictionary<(string Region, long Start, long End, long Target), PublicRegionProjection>();
        foreach (var row in input.RegionProjections)
        {
            ArgumentNullException.ThrowIfNull(row);
            RequireMember(row.HistoricalRegionId, regions);
            RequireAvailable(row.SourceId, row.AvailableTick, input.EvidenceCutoff);
            if (row.StartTick < 0 || row.EndTick < row.StartTick || row.TargetTick < row.EndTick)
            {
                throw new InvalidDataException("Invalid public transition time window.");
            }

            var targets = UniqueIds(row.CurrentRegionIds);
            RequireMembers(targets, regions);
            RequireNonemptyComplete(row.IsComplete, targets.Count);
            if (!transitions.TryAdd(
                (row.HistoricalRegionId, row.StartTick, row.EndTick, row.TargetTick),
                row with { CurrentRegionIds = Ordered(targets) }))
            {
                throw new InvalidDataException("Duplicate public transition key.");
            }
        }

        var mappings = new Dictionary<(string Region, long Target), PublicSearchUnitMapping>();
        foreach (var row in input.UnitMappings)
        {
            ArgumentNullException.ThrowIfNull(row);
            RequireMember(row.RegionId, regions);
            RequireAvailable(row.SourceId, row.AvailableTick, input.EvidenceCutoff);
            if (row.TargetTick < 0)
            {
                throw new InvalidDataException("Unit mapping target tick cannot be negative.");
            }

            var targets = UniqueIds(row.UnitIds);
            RequireMembers(targets, units);
            RequireNonemptyComplete(row.IsComplete, targets.Count);
            if (!mappings.TryAdd((row.RegionId, row.TargetTick), row with { UnitIds = Ordered(targets) }))
            {
                throw new InvalidDataException("Duplicate public unit-mapping key.");
            }
        }

        var actions = new List<PublicActionFootprint>();
        var actionIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var action in input.Actions)
        {
            ArgumentNullException.ThrowIfNull(action);
            RequireId(action.ActionId);
            RequireAvailable(action.SourceId, action.AvailableTick, input.EvidenceCutoff);
            if (action.TargetTick != input.TargetTick || !actionIds.Add(action.ActionId))
            {
                throw new InvalidDataException("Action identity or target tick is invalid.");
            }

            var footprint = UniqueIds(action.UnitIds);
            RequireMembers(footprint, units);
            actions.Add(action with { UnitIds = Ordered(footprint) });
        }

        var candidateIds = new HashSet<string>(StringComparer.Ordinal);
        var projected = new List<CandidateLocationProjection>();
        foreach (var candidate in input.Candidates)
        {
            ArgumentNullException.ThrowIfNull(candidate);
            RequireId(candidate.CandidateId);
            ArgumentNullException.ThrowIfNull(candidate.TargetTimeWindow);
            if (!candidateIds.Add(candidate.CandidateId)
                || !string.Equals(candidate.QueryId, input.QueryId, StringComparison.Ordinal)
                || !string.Equals(candidate.TargetEntityId, input.TargetEntityId, StringComparison.Ordinal)
                || candidate.TargetTimeWindow.EndTick > input.TargetTick
                || !Enum.IsDefined(candidate.Disposition))
            {
                throw new InvalidDataException("Candidate identity, query, entity, disposition or time is invalid.");
            }

            RequireMember(candidate.RegionId, regions);
            var source = CanonicalCandidate(candidate);
            var knownRegions = new HashSet<string>(StringComparer.Ordinal);
            var knownUnits = new HashSet<string>(StringComparer.Ordinal);
            var reasons = new HashSet<string>(StringComparer.Ordinal);
            var transitionSources = new HashSet<string>(StringComparer.Ordinal);
            var mappingSources = new HashSet<string>(StringComparer.Ordinal);
            var key = (candidate.RegionId, candidate.TargetTimeWindow.StartTick,
                candidate.TargetTimeWindow.EndTick, input.TargetTick);
            if (!transitions.TryGetValue(key, out var transition))
            {
                reasons.Add("transition-unmapped");
            }
            else
            {
                knownRegions.UnionWith(transition.CurrentRegionIds);
                transitionSources.Add(transition.SourceId);
                if (!transition.IsComplete)
                {
                    reasons.Add("transition-incomplete");
                }
            }

            foreach (var region in knownRegions)
            {
                if (!mappings.TryGetValue((region, input.TargetTick), out var mapping))
                {
                    reasons.Add("unit-mapping-unmapped");
                    continue;
                }

                knownUnits.UnionWith(mapping.UnitIds);
                mappingSources.Add(mapping.SourceId);
                if (!mapping.IsComplete)
                {
                    reasons.Add("unit-mapping-incomplete");
                }
            }

            projected.Add(new CandidateLocationProjection(
                source, input.TargetTick, Ordered(knownRegions), Ordered(knownUnits), Ordered(reasons),
                Ordered(transitionSources), Ordered(mappingSources)));
        }

        var orderedCandidates = projected.OrderBy(item => item.SourceCandidate.CandidateId, StringComparer.Ordinal).ToArray();
        var actionResults = actions.OrderBy(item => item.ActionId, StringComparer.Ordinal).Select(action =>
        {
            var footprint = action.UnitIds.ToHashSet(StringComparer.Ordinal);
            var overlaps = orderedCandidates.Where(item => item.KnownUnitIds.Any(footprint.Contains)).ToArray();
            // Nonempty support prevents a vacuous containment claim for an unmapped candidate.
            var contained = orderedCandidates.Where(item => !item.HasUnresolvedPossibilities
                && item.KnownUnitIds.Count > 0 && item.KnownUnitIds.All(footprint.Contains));
            return new ActionSupportProjection(
                action.ActionId, input.TargetTick, action.SourceId, action.UnitIds,
                Ordered(overlaps.SelectMany(item => item.KnownUnitIds).Where(footprint.Contains)),
                Ordered(overlaps.Select(item => item.SourceCandidate.CandidateId)),
                Ordered(contained.Select(item => item.SourceCandidate.CandidateId)));
        }).ToArray();

        return new PublicSearchProjectionResult(
            "public-search-projection-v1", input.QueryId, input.TargetEntityId, input.TargetTick, input.EvidenceCutoff,
            orderedCandidates,
            Ordered(orderedCandidates.SelectMany(item => item.KnownCurrentRegionIds)),
            Ordered(orderedCandidates.SelectMany(item => item.KnownUnitIds)),
            Ordered(orderedCandidates.Where(item => item.HasUnresolvedPossibilities)
                .Select(item => item.SourceCandidate.CandidateId)),
            actionResults);
    }

    private static PastStateCandidate CanonicalCandidate(PastStateCandidate candidate) => candidate with
    {
        GenerationEvidenceIds = CanonicalMetadata(candidate.GenerationEvidenceIds),
        GenerationSourceGroupIds = CanonicalMetadata(candidate.GenerationSourceGroupIds),
        SourceAncestryIds = CanonicalMetadata(candidate.SourceAncestryIds),
        AppliedConstraintIds = CanonicalMetadata(candidate.AppliedConstraintIds),
        UnresolvedConstraintIds = CanonicalMetadata(candidate.UnresolvedConstraintIds),
        ConflictingConstraintIds = CanonicalMetadata(candidate.ConflictingConstraintIds),
        PublicRelationReferences = CanonicalMetadata(candidate.PublicRelationReferences)
    };

    private static string[] CanonicalMetadata(IReadOnlyList<string> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        foreach (var value in values)
        {
            RequireId(value);
        }

        return Ordered(values);
    }

    private static string[] Ordered(IEnumerable<string> values) =>
        values.Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray();

    private static HashSet<string> UniqueIds(IReadOnlyList<string> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        var result = new HashSet<string>(StringComparer.Ordinal);
        foreach (var value in values)
        {
            RequireId(value);
            if (!result.Add(value))
            {
                throw new InvalidDataException("Duplicate identifier in a public projection set.");
            }
        }

        return result;
    }

    private static void RequireId(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 512
            || !string.Equals(value, value.Trim(), StringComparison.Ordinal) || value.Any(char.IsControl))
        {
            throw new InvalidDataException("Invalid public projection identifier.");
        }
    }

    private static void RequireMember(string value, HashSet<string> catalogue)
    {
        RequireId(value);
        if (!catalogue.Contains(value))
        {
            throw new InvalidDataException("Public projection references an undeclared identifier.");
        }
    }

    private static void RequireMembers(IEnumerable<string> values, HashSet<string> catalogue)
    {
        foreach (var value in values)
        {
            RequireMember(value, catalogue);
        }
    }

    private static void RequireAvailable(string sourceId, long availableTick, long cutoff)
    {
        RequireId(sourceId);
        if (availableTick < 0 || availableTick > cutoff)
        {
            throw new InvalidDataException("Public projection source is unavailable at the cutoff.");
        }
    }

    private static void RequireNonemptyComplete(bool complete, int count)
    {
        if (complete && count == 0)
        {
            throw new InvalidDataException("A complete public projection cannot have empty support.");
        }
    }
}
