using FootprintSim.Evidence;
using FootprintSim.Interactions;
using FootprintSim.Interactions.CandidateWarps;

namespace FootprintSim.Evaluation;

public sealed class OwnEvidenceConstrainedStrategy : ISearchStrategy
{
    public string Id => SearchStrategyIds.OwnEvidenceConstrained;

    public SearchStrategyResult Evaluate(SearchStrategyInput input) =>
        WeftStrategySupport.Evaluate(this, input, applyCrossThread: false);
}

public sealed class CrossThreadConstrainedStrategy : ISearchStrategy
{
    public string Id => SearchStrategyIds.CrossThreadConstrained;

    public SearchStrategyResult Evaluate(SearchStrategyInput input) =>
        WeftStrategySupport.Evaluate(this, input, applyCrossThread: true);
}

internal static class WeftStrategySupport
{
    public static SearchStrategyResult Evaluate(
        ISearchStrategy strategy,
        SearchStrategyInput input,
        bool applyCrossThread)
    {
        var definition = input.PublicContext.PublicCandidateWarp;
        var ownObservation = definition is null
            ? null
            : input.AvailableEvidence.Observations
                .Where(observation => observation.AvailableTick >= definition.AvailableAtTick)
                .Where(observation => observation.SubjectIds.Contains(
                    input.PublicContext.TargetPersonId,
                    StringComparer.Ordinal))
                .Where(observation => observation.ReportedRegionId is null ||
                    definition.HistoricalCandidateRegionIds.Contains(
                        observation.ReportedRegionId,
                        StringComparer.Ordinal))
                .OrderBy(observation => observation.AvailableTick)
                .ThenBy(observation => observation.ObservationId, StringComparer.Ordinal)
                .FirstOrDefault();
        if (definition is null || ownObservation is null)
        {
            var fallback = new HazardAwareStrategy().Evaluate(input);
            return fallback with
            {
                StrategyId = strategy.Id,
                RationaleCodes = fallback.RationaleCodes
                    .Append("weft-evaluation-unavailable")
                    .Distinct(StringComparer.Ordinal)
                    .ToArray()
            };
        }

        var temporalKind = definition.HistoricalTimeWindow.StartTick ==
            definition.HistoricalTimeWindow.EndTick
            ? CandidateWarpTemporalKind.EventPoint
            : CandidateWarpTemporalKind.StateInterval;
        var provenance = CandidateWarpProvenance.Create(
            CandidateWarpSourceKind.PersonObservation,
            ownObservation.ObservationId,
            ownObservation.AvailableTick,
            isPersonEvidence: true,
            isDeviceOnlyEvidence: false,
            ownObservation.State,
            definition.HistoricalCandidateRegionIds);
        var warps = PublicCandidateWarpFactory.Create(
            input.ScenarioId,
            input.Seed,
            input.PublicContext.TargetPersonId,
            [new PublicCandidateWarpStateDefinition
            {
                TimeWindow = definition.HistoricalTimeWindow,
                TemporalKind = temporalKind,
                RegionIds = definition.HistoricalCandidateRegionIds,
                EvidenceBoundaryState = EvidenceState.SupportedButUncertain,
                Provenance = [provenance]
            }],
            CandidateWarpStanding.Preferred);

        IReadOnlyList<ConstraintApplicationAudit> audits = [];
        if (applyCrossThread && input.AvailableCrossThreadConstraints is { } constraints)
        {
            var application = new CrossThreadConstraintApplicator().Apply(
                warps,
                constraints.Constraints,
                new CandidateWarpAccessContext
                {
                    ScenarioId = input.ScenarioId,
                    Seed = input.Seed,
                    TargetEntityId = input.PublicContext.TargetPersonId,
                    CurrentTick = input.CurrentTick,
                    QueryContextId = "s3-public-search"
                },
                input.PublicContext.PublicHazardEnvelope);
            warps = application.CandidateWarps;
            audits = application.Audits;
        }

        warps = PublicCandidateWarpProjection.ForwardTo(
            warps,
            input.CurrentTick,
            input.PublicContext.PublicHazardEnvelope);
        var outer = warps.CandidateRegionIdsAt(input.CurrentTick);
        var preferred = warps.PreferredRegionIdsAt(input.CurrentTick);
        var conflicted = warps.Warps
            .Where(warp => warp.Standing == CandidateWarpStanding.Conflicted)
            .SelectMany(warp => warp.CandidateRegionIdsAt(input.CurrentTick))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(regionId => regionId, StringComparer.Ordinal)
            .ToArray();
        var state = audits.Any(audit => audit.ConflictDetected)
            ? EvidenceState.Conflicting
            : audits.Any(audit => audit.EvidenceBoundaryState == EvidenceState.Confirmed &&
                audit.Disposition == ConstraintApplicationDisposition.Narrow)
                ? EvidenceState.Confirmed
                : EvidenceState.SupportedButUncertain;
        var result = StrategySupport.Result(
            strategy,
            input,
            outer,
            state,
            applyCrossThread
                ? ["own-evidence-candidate-warp", "available-cross-thread-constraint"]
                : ["own-evidence-candidate-warp", "cross-thread-ablation"],
            [],
            CandidateProvenance(warps, input.CurrentTick));
        return result with
        {
            PreferredCandidateRegionIds = preferred,
            OuterPlausibleCandidateRegionIds = outer,
            ConflictedCandidateRegionIds = conflicted,
            CrossThreadConstraintAudits = audits
        };
    }

    private static IReadOnlyList<CandidateRegionProvenance> CandidateProvenance(
        CandidateWarpSet warps,
        long tick)
    {
        var result = new List<CandidateRegionProvenance>();
        foreach (var warp in warps.Warps)
        {
            var regions = warp.CandidateRegionIdsAt(tick);
            var states = warp.States.Where(state => state.TimeWindow.StartTick <= tick)
                .OrderByDescending(state => state.TimeWindow.EndTick)
                .ThenBy(state => state.StateId, StringComparer.Ordinal)
                .ToArray();
            var selected = states.Where(state => state.TimeWindow.StartTick <= tick &&
                tick <= state.TimeWindow.EndTick).ToArray();
            if (selected.Length == 0 && states.Length > 0)
            {
                var latest = states.Max(state => state.TimeWindow.EndTick);
                selected = states.Where(state => state.TimeWindow.EndTick == latest).ToArray();
            }

            foreach (var region in regions)
            {
                foreach (var item in selected.SelectMany(state => state.Provenance)
                    .Where(item => item.RegionIds.Contains(region, StringComparer.Ordinal) ||
                        item.SourceKind is CandidateWarpSourceKind.CrossThreadObservation or
                            CandidateWarpSourceKind.ConflictingObservation))
                {
                    result.Add(new CandidateRegionProvenance
                    {
                        RegionId = region,
                        SourceKind = item.SourceKind switch
                        {
                            CandidateWarpSourceKind.LastConfirmed => CandidateSourceKind.LastConfirmed,
                            CandidateWarpSourceKind.VoluntaryReachability => CandidateSourceKind.VoluntaryReachability,
                            CandidateWarpSourceKind.PublicHazardEnvelope => CandidateSourceKind.PublicHazardEnvelope,
                            CandidateWarpSourceKind.PersonObservation => CandidateSourceKind.PersonObservation,
                            CandidateWarpSourceKind.DeviceOnlyObservation => CandidateSourceKind.DeviceOnlyObservation,
                            CandidateWarpSourceKind.CrossThreadObservation => CandidateSourceKind.CrossThreadObservation,
                            CandidateWarpSourceKind.ConflictingObservation => CandidateSourceKind.ConflictingObservation,
                            _ => CandidateSourceKind.Unsupported
                        },
                        SourceIdentifier = item.SourceIdentifier,
                        AvailableAtTick = item.AvailableAtTick,
                        IsPersonEvidence = item.IsPersonEvidence,
                        IsDeviceOnlyEvidence = item.IsDeviceOnlyEvidence,
                        EvidenceBoundaryState = item.EvidenceBoundaryState
                    });
                }
            }
        }

        return result.Distinct().OrderBy(item => item.RegionId, StringComparer.Ordinal)
            .ThenBy(item => item.SourceKind)
            .ThenBy(item => item.SourceIdentifier, StringComparer.Ordinal)
            .ToArray();
    }
}
