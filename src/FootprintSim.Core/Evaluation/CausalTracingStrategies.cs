using FootprintSim.Evidence;
using FootprintSim.Interactions.CandidateWarps;
using FootprintSim.Interactions.Tracing;

namespace FootprintSim.Evaluation;

public sealed class SingleWeftStrategy : ISearchStrategy
{
    public string Id => SearchStrategyIds.SingleWeft;

    public SearchStrategyResult Evaluate(SearchStrategyInput input) =>
        CausalTracingStrategySupport.Evaluate(this, input, CausalTracingStrategyMode.SingleWeft);
}

public sealed class MultiWeftTracingStrategy : ISearchStrategy
{
    public string Id => SearchStrategyIds.MultiWeftTracing;

    public SearchStrategyResult Evaluate(SearchStrategyInput input) =>
        CausalTracingStrategySupport.Evaluate(this, input, CausalTracingStrategyMode.MultiWeft);
}

internal enum CausalTracingStrategyMode
{
    OwnEvidence,
    SingleWeft,
    MultiWeft
}

internal static class CausalTracingStrategySupport
{
    public static SearchStrategyResult Evaluate(
        ISearchStrategy strategy,
        SearchStrategyInput input,
        CausalTracingStrategyMode mode)
    {
        var model = input.PublicContext.PublicCausalTracingModel;
        if (model is null)
        {
            var fallback = new HazardAwareStrategy().Evaluate(input);
            return fallback with
            {
                StrategyId = strategy.Id,
                RationaleCodes = fallback.RationaleCodes
                    .Append("causal-tracing-model-unavailable")
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(item => item, StringComparer.Ordinal)
                    .ToArray()
            };
        }

        var warps = PublicCandidateWarpFactory.Create(
            input.ScenarioId,
            input.Seed,
            input.PublicContext.TargetPersonId,
            model.CandidateStates,
            CandidateWarpStanding.Preferred);
        var available = input.AvailableCausalTracingConstraints?.Constraints ?? [];
        var selected = mode switch
        {
            CausalTracingStrategyMode.OwnEvidence => [],
            CausalTracingStrategyMode.SingleWeft => SelectSingle(available),
            CausalTracingStrategyMode.MultiWeft => available,
            _ => throw new ArgumentOutOfRangeException(nameof(mode))
        };
        var tracing = new OperationalCausalTracer().Trace(
            warps,
            selected,
            new CandidateWarpAccessContext
            {
                ScenarioId = input.ScenarioId,
                Seed = input.Seed,
                TargetEntityId = input.PublicContext.TargetPersonId,
                CurrentTick = input.CurrentTick,
                QueryContextId = $"s4-public-search:{strategy.Id}"
            },
            input.PublicContext.PublicHazardEnvelope);
        var preferred = tracing.CandidateWarps.PreferredRegionIdsAt(input.CurrentTick);
        var outer = tracing.CandidateWarps.OuterPlausibleRegionIdsAt(input.CurrentTick);
        var conflicted = tracing.CandidateWarps.Warps
            .Where(warp => warp.Standing == CandidateWarpStanding.Conflicted)
            .SelectMany(warp => warp.CandidateRegionIdsAt(input.CurrentTick))
            .Where(outer.Contains)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(regionId => regionId, StringComparer.Ordinal)
            .ToArray();
        var state = tracing.Steps.Any(step => step.ConflictDetected)
            ? EvidenceState.Conflicting
            : tracing.Steps.Any(step => step.EvidenceState == EvidenceState.Confirmed &&
                step.Disposition == TracingStepDisposition.Applied)
                ? EvidenceState.Confirmed
                : EvidenceState.SupportedButUncertain;
        var rationale = mode switch
        {
            CausalTracingStrategyMode.OwnEvidence =>
                new[] { "public-candidate-warp", "multiple-weft-ablation" },
            CausalTracingStrategyMode.SingleWeft =>
                ["public-candidate-warp", "single-cross-thread-constraint"],
            _ => ["public-candidate-warp", "operational-causal-tracing", "multiple-cross-thread-constraints"]
        };
        var result = StrategySupport.Result(
            strategy,
            input,
            outer,
            state,
            rationale,
            [],
            WeftStrategySupport.CandidateProvenance(tracing.CandidateWarps, input.CurrentTick));
        return result with
        {
            PreferredCandidateRegionIds = preferred,
            OuterPlausibleCandidateRegionIds = outer,
            ConflictedCandidateRegionIds = conflicted,
            HasCandidateStandingSets = true,
            CausalTracingResult = tracing
        };
    }

    private static IReadOnlyList<CausalTracingConstraint> SelectSingle(
        IReadOnlyList<CausalTracingConstraint> constraints)
    {
        var selected = constraints
            .Where(item => item.IsPersonEvidence && !item.IsDeviceOnlyEvidence)
            .Where(item => item.EvidenceBoundaryState is
                EvidenceState.Confirmed or EvidenceState.SupportedButUncertain)
            .OrderBy(item => item.Role == TracingConstraintRole.Local ? 0 : 1)
            .ThenBy(item => item.AvailableAtTick)
            .ThenBy(item => item.ConstrainedTimeWindow.StartTick)
            .ThenBy(item => item.SourceObservationId, StringComparer.Ordinal)
            .ThenBy(item => item.ConstraintId, StringComparer.Ordinal)
            .FirstOrDefault();
        return selected is null ? [] : [selected];
    }
}
