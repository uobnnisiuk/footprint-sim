using FootprintSim.Scenarios;

namespace FootprintSim.Evaluation;

public sealed record CausalTracingRunEvaluation
{
    public int SchemaVersion { get; init; } = 1;

    public required string ScenarioId { get; init; }

    public required int Seed { get; init; }

    public required string StrategyId { get; init; }

    public required CausalTracingCondition Condition { get; init; }

    public required int PreferredCandidateCount { get; init; }

    public required int OuterCandidateCount { get; init; }

    public bool? PreferredContainment { get; init; }

    public bool? OuterContainment { get; init; }

    public decimal? PreferredReduction { get; init; }

    public decimal? OuterReduction { get; init; }

    public decimal? PreferredPersistence { get; init; }

    public required int AppliedConstraintCount { get; init; }

    public required int AppliedUniqueSourceGroupCount { get; init; }

    public required int SupportingSourceGroupCount { get; init; }

    public required int ConflictingSourceGroupCount { get; init; }

    public required int TracingStepCount { get; init; }

    public required int ForwardTracingStepCount { get; init; }

    public required int BackwardTracingStepCount { get; init; }

    public required int RetainedAlternativeCount { get; init; }

    public required bool CausalSandwichApplied { get; init; }

    public required bool ConstraintOrderInvariant { get; init; }
}

public sealed record CausalTracingPairedDelta
{
    public required string ScenarioId { get; init; }

    public CausalTracingCondition? Condition { get; init; }

    public required int PairCount { get; init; }

    public decimal? PreferredCandidateCountDeltaMean { get; init; }

    public decimal? OuterCandidateCountDeltaMean { get; init; }

    public decimal? PreferredContainmentDeltaMean { get; init; }

    public decimal? OuterContainmentDeltaMean { get; init; }

    public decimal? PreferredPersistenceDeltaMean { get; init; }

    public decimal? ConflictSourceGroupDeltaMean { get; init; }
}

public sealed record CausalTracingEvaluationReport
{
    public int SchemaVersion { get; init; } = 1;

    public IReadOnlyList<CausalTracingRunEvaluation> Runs { get; init; } = [];

    public IReadOnlyList<CausalTracingPairedDelta> MultiWeftMinusSingleWeft { get; init; } = [];
}

internal static class CausalTracingEvaluationBuilder
{
    public static CausalTracingEvaluationReport? Build(
        IEnumerable<(PerRunEvaluation Evaluation, SearchStrategyResult Result, CausalTracingCondition? Condition)> inputs)
    {
        var runs = inputs
            .Where(item => item.Condition is not null && item.Result.CausalTracingResult is not null)
            .Select(CreateRun)
            .OrderBy(item => item.ScenarioId, StringComparer.Ordinal)
            .ThenBy(item => item.Seed)
            .ThenBy(item => item.StrategyId, StringComparer.Ordinal)
            .ToArray();
        if (runs.Length == 0)
        {
            return null;
        }

        var pairs = new List<CausalTracingPairedDelta>();
        foreach (var scenario in runs.Select(item => item.ScenarioId).Distinct(StringComparer.Ordinal))
        {
            var scenarioRuns = runs.Where(item => string.Equals(
                item.ScenarioId,
                scenario,
                StringComparison.Ordinal)).ToArray();
            pairs.Add(CreatePair(scenario, condition: null, scenarioRuns));
            foreach (var condition in Enum.GetValues<CausalTracingCondition>())
            {
                pairs.Add(CreatePair(
                    scenario,
                    condition,
                    scenarioRuns.Where(item => item.Condition == condition).ToArray()));
            }
        }

        return new CausalTracingEvaluationReport
        {
            Runs = runs,
            MultiWeftMinusSingleWeft = pairs
                .OrderBy(item => item.ScenarioId, StringComparer.Ordinal)
                .ThenBy(item => item.Condition)
                .ToArray()
        };
    }

    private static CausalTracingRunEvaluation CreateRun(
        (PerRunEvaluation Evaluation, SearchStrategyResult Result, CausalTracingCondition? Condition) input)
    {
        var tracing = input.Result.CausalTracingResult!;
        var applied = tracing.Steps
            .Where(step => step.Disposition is
                Interactions.Tracing.TracingStepDisposition.Applied or
                Interactions.Tracing.TracingStepDisposition.Branch)
            .ToArray();
        return new CausalTracingRunEvaluation
        {
            ScenarioId = input.Evaluation.ScenarioId,
            Seed = input.Evaluation.Seed,
            StrategyId = input.Evaluation.StrategyId,
            Condition = input.Condition!.Value,
            PreferredCandidateCount = input.Evaluation.PreferredCandidateRegionCount.Value ??
                throw new InvalidOperationException("Tracing preferred candidate count is unavailable."),
            OuterCandidateCount = input.Evaluation.OuterPlausibleCandidateRegionCount.Value ??
                throw new InvalidOperationException("Tracing outer candidate count is unavailable."),
            PreferredContainment = input.Evaluation.PreferredContainment.Value,
            OuterContainment = input.Evaluation.OuterPlausibleContainment.Value,
            PreferredReduction = input.Evaluation.PreferredReduction.Value,
            OuterReduction = input.Evaluation.OuterPlausibleReduction.Value,
            PreferredPersistence = input.Evaluation.PreferredGuidance.PersistenceFraction.Value,
            AppliedConstraintCount = applied
                .Select(step => step.ConstraintId)
                .Where(id => id is not null)
                .Distinct(StringComparer.Ordinal)
                .Count(),
            AppliedUniqueSourceGroupCount = applied
                .Select(step => step.SourceGroupId)
                .Where(id => id is not null)
                .Distinct(StringComparer.Ordinal)
                .Count(),
            SupportingSourceGroupCount = tracing.SupportLineage
                .SelectMany(item => item.SupportingSourceGroupIds)
                .Distinct(StringComparer.Ordinal)
                .Count(),
            ConflictingSourceGroupCount = tracing.SupportLineage
                .SelectMany(item => item.CompetingSourceGroupIds)
                .Concat(tracing.Steps.SelectMany(item => item.ConflictingSourceGroupIds))
                .Distinct(StringComparer.Ordinal)
                .Count(),
            TracingStepCount = tracing.Steps.Count,
            ForwardTracingStepCount = tracing.Steps.Count(step =>
                step.Direction == Interactions.Tracing.CausalTracingDirection.Forward),
            BackwardTracingStepCount = tracing.Steps.Count(step =>
                step.Direction == Interactions.Tracing.CausalTracingDirection.Backward),
            RetainedAlternativeCount = tracing.Steps.Count(step => step.AlternativesRetained),
            CausalSandwichApplied = tracing.CausalSandwichApplied,
            ConstraintOrderInvariant = tracing.ConstraintOrderInvariant
        };
    }

    private static CausalTracingPairedDelta CreatePair(
        string scenario,
        CausalTracingCondition? condition,
        IReadOnlyList<CausalTracingRunEvaluation> runs)
    {
        var pairs = runs
            .GroupBy(item => item.Seed)
            .Select(group => new
            {
                Single = group.SingleOrDefault(item => item.StrategyId == SearchStrategyIds.SingleWeft),
                Multi = group.SingleOrDefault(item => item.StrategyId == SearchStrategyIds.MultiWeftTracing)
            })
            .Where(item => item.Single is not null && item.Multi is not null)
            .ToArray();
        return new CausalTracingPairedDelta
        {
            ScenarioId = scenario,
            Condition = condition,
            PairCount = pairs.Length,
            PreferredCandidateCountDeltaMean = Mean(pairs.Select(item =>
                (decimal?)(item.Multi!.PreferredCandidateCount - item.Single!.PreferredCandidateCount))),
            OuterCandidateCountDeltaMean = Mean(pairs.Select(item =>
                (decimal?)(item.Multi!.OuterCandidateCount - item.Single!.OuterCandidateCount))),
            PreferredContainmentDeltaMean = Mean(pairs.Select(item => Difference(
                item.Multi!.PreferredContainment,
                item.Single!.PreferredContainment))),
            OuterContainmentDeltaMean = Mean(pairs.Select(item => Difference(
                item.Multi!.OuterContainment,
                item.Single!.OuterContainment))),
            PreferredPersistenceDeltaMean = Mean(pairs.Select(item => Difference(
                item.Multi!.PreferredPersistence,
                item.Single!.PreferredPersistence))),
            ConflictSourceGroupDeltaMean = Mean(pairs.Select(item =>
                (decimal?)(item.Multi!.ConflictingSourceGroupCount - item.Single!.ConflictingSourceGroupCount)))
        };
    }

    private static decimal? Difference(bool? left, bool? right) =>
        left is null || right is null ? null : (left.Value ? 1m : 0m) - (right.Value ? 1m : 0m);

    private static decimal? Difference(decimal? left, decimal? right) =>
        left is null || right is null ? null : left.Value - right.Value;

    private static decimal? Mean(IEnumerable<decimal?> values)
    {
        var available = values.Where(item => item is not null).Select(item => item!.Value).ToArray();
        return available.Length == 0 ? null : available.Average();
    }
}
