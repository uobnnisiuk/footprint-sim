using System.Globalization;
using FootprintSim.Evidence;
using FootprintSim.Scenarios;

namespace FootprintSim.Evaluation;

public sealed record RateAggregate
{
    public required int AvailableCount { get; init; }

    public required int Numerator { get; init; }

    public required int Denominator { get; init; }

    public decimal? Rate { get; init; }
}

public sealed record DistributionAggregate
{
    public required int AvailableCount { get; init; }

    public decimal? Mean { get; init; }

    public decimal? Median { get; init; }

    public decimal? Minimum { get; init; }

    public decimal? Maximum { get; init; }
}

public sealed record EvidenceStateCounts
{
    public required int Confirmed { get; init; }

    public required int SupportedButUncertain { get; init; }

    public required int Conflicting { get; init; }

    public required int Indeterminate { get; init; }

    public required int UnsupportedForQuery { get; init; }
}

public sealed record StrategyScenarioAggregate
{
    public required string ScenarioId { get; init; }

    public required string StrategyId { get; init; }

    public required int RunCount { get; init; }

    public required int MetricCompleteRunCount { get; init; }

    public required RateAggregate GroundTruthContainment { get; init; }

    public required RateAggregate FalseExclusion { get; init; }

    public required RateAggregate ExplicitFalseExclusion { get; init; }

    public required int DeviceOnlyPromotionViolationCount { get; init; }

    public required DistributionAggregate CandidateRegionCount { get; init; }

    public required DistributionAggregate SearchSpaceFraction { get; init; }

    public required DistributionAggregate SearchSpaceReduction { get; init; }

    public required RateAggregate PreferredContainment { get; init; }

    public required RateAggregate OuterPlausibleContainment { get; init; }

    public required DistributionAggregate PreferredCandidateRegionCount { get; init; }

    public required DistributionAggregate OuterPlausibleCandidateRegionCount { get; init; }

    public required DistributionAggregate PreferredReduction { get; init; }

    public required DistributionAggregate OuterPlausibleReduction { get; init; }

    public required DistributionAggregate PreferredPersistenceFraction { get; init; }

    public required DistributionAggregate OuterSafePersistenceFraction { get; init; }

    public required DistributionAggregate PreferredTimeToUsefulGuidance { get; init; }

    public required RateAggregate PreferredFinalGuidanceUseful { get; init; }

    public required RateAggregate PreferredGuidanceRegressedAfterUseful { get; init; }

    public required DistributionAggregate OuterSafeTimeToUsefulGuidance { get; init; }

    public required RateAggregate OuterSafeFinalGuidanceUseful { get; init; }

    public required RateAggregate OuterSafeGuidanceRegressedAfterUseful { get; init; }

    public required int CrossThreadConstraintAppliedCount { get; init; }

    public required int CrossThreadConstraintConflictCount { get; init; }

    public required int CrossThreadConstraintBranchCount { get; init; }

    public required int RetainedAlternativeCount { get; init; }

    public required DistributionAggregate TimeToUsefulGuidance { get; init; }

    public required RateAggregate FinalGuidanceUseful { get; init; }

    public required DistributionAggregate UsefulGuidanceTickCount { get; init; }

    public required DistributionAggregate EvaluatedTickCount { get; init; }

    public required DistributionAggregate UsefulGuidancePersistenceFraction { get; init; }

    public required DistributionAggregate LongestUsefulGuidanceStreak { get; init; }

    public required RateAggregate GuidanceRegressedAfterUseful { get; init; }

    public required EvidenceStateCounts EvidenceBoundaryStateCounts { get; init; }

    public IReadOnlyList<ProvenanceSourceKindCount> ProvenanceSourceKindCounts { get; init; } = [];

    public required int UniqueOutcomeCount { get; init; }

    public required string SeedSensitivityNote { get; init; }
}

public sealed record ProvenanceSourceKindCount
{
    public required CandidateSourceKind SourceKind { get; init; }

    public required int Count { get; init; }
}

public sealed record RegionOutcomeCount
{
    public required string RegionId { get; init; }

    public required int Count { get; init; }
}

public sealed record DeviceRegionOutcomeCount
{
    public required string DeviceId { get; init; }

    public required string RegionId { get; init; }

    public required int Count { get; init; }
}

public sealed record ScenarioTruthAggregate
{
    public required string ScenarioId { get; init; }

    public required int SeedCount { get; init; }

    public IReadOnlyList<RegionOutcomeCount> PersonFinalRegionCounts { get; init; } = [];

    public IReadOnlyList<DeviceRegionOutcomeCount> DeviceFinalRegionCounts { get; init; } = [];

    public required int UniqueTruthOutcomeCount { get; init; }

    public required int SecondaryTransportRunCount { get; init; }

    public required int SecondaryTransportEventCount { get; init; }

    public required int PersonDeviceDifferentTerminalCount { get; init; }

    public IReadOnlyList<WeftEvidenceConditionCount> WeftEvidenceConditionCounts { get; init; } = [];
}

public sealed record WeftEvidenceConditionCount
{
    public required WeftEvidenceCondition Condition { get; init; }

    public required int Count { get; init; }
}

public sealed record ConditionStrategyAggregate
{
    public required WeftEvidenceCondition Condition { get; init; }

    public required StrategyScenarioAggregate Metrics { get; init; }
}

public sealed record PairedWeftAggregate
{
    public required string ScenarioId { get; init; }

    public WeftEvidenceCondition? Condition { get; init; }

    public required int PairCount { get; init; }

    public required DistributionAggregate PreferredCandidateCountDelta { get; init; }

    public required DistributionAggregate OuterPlausibleCandidateCountDelta { get; init; }

    public required DistributionAggregate PreferredReductionDelta { get; init; }

    public required DistributionAggregate OuterPlausibleReductionDelta { get; init; }

    public required DistributionAggregate PreferredContainmentDelta { get; init; }

    public required DistributionAggregate OuterPlausibleContainmentDelta { get; init; }

    public required DistributionAggregate TimeToGuidanceDelta { get; init; }

    public required DistributionAggregate PersistenceDelta { get; init; }
}

public sealed record AggregateReport
{
    public int SchemaVersion { get; init; } = 3;

    public IReadOnlyList<StrategyScenarioAggregate> Groups { get; init; } = [];

    public IReadOnlyList<ScenarioTruthAggregate> TruthGroups { get; init; } = [];

    public IReadOnlyList<ConditionStrategyAggregate> ConditionGroups { get; init; } = [];

    public IReadOnlyList<PairedWeftAggregate> PairedWeftGroups { get; init; } = [];
}

public sealed class AggregateEvaluator
{
    public AggregateReport Aggregate(IEnumerable<PerRunEvaluation> evaluations)
    {
        ArgumentNullException.ThrowIfNull(evaluations);
        var groups = evaluations
            .GroupBy(
                evaluation => (evaluation.ScenarioId, evaluation.StrategyId),
                EvaluationGroupComparer.Instance)
            .OrderBy(group => group.Key.ScenarioId, StringComparer.Ordinal)
            .ThenBy(group => group.Key.StrategyId, StringComparer.Ordinal)
            .Select(CreateGroup)
            .ToArray();

        var truthGroups = evaluations
            .GroupBy(run => (run.ScenarioId, run.Seed))
            .Select(group => group.OrderBy(run => run.StrategyId, StringComparer.Ordinal).First())
            .GroupBy(run => run.ScenarioId, StringComparer.Ordinal)
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .Select(CreateTruthGroup)
            .ToArray();

        var conditionGroups = evaluations
            .Where(run => run.WeftEvidenceCondition is not null)
            .GroupBy(run => (run.ScenarioId, run.StrategyId, run.WeftEvidenceCondition!.Value))
            .OrderBy(group => group.Key.ScenarioId, StringComparer.Ordinal)
            .ThenBy(group => group.Key.StrategyId, StringComparer.Ordinal)
            .ThenBy(group => group.Key.Value)
            .Select(group => new ConditionStrategyAggregate
            {
                Condition = group.Key.Value,
                Metrics = CreateGroupFromRuns(
                    group.Key.ScenarioId,
                    group.Key.StrategyId,
                    group.OrderBy(run => run.Seed).ToArray())
            })
            .ToArray();
        var paired = CreatePairedGroups(evaluations);

        return new AggregateReport
        {
            Groups = groups,
            TruthGroups = truthGroups,
            ConditionGroups = conditionGroups,
            PairedWeftGroups = paired
        };
    }

    private static StrategyScenarioAggregate CreateGroup(
        IGrouping<(string ScenarioId, string StrategyId), PerRunEvaluation> group)
    {
        var runs = group.OrderBy(run => run.Seed).ToArray();
        return CreateGroupFromRuns(group.Key.ScenarioId, group.Key.StrategyId, runs);
    }

    private static StrategyScenarioAggregate CreateGroupFromRuns(
        string scenarioId,
        string strategyId,
        IReadOnlyList<PerRunEvaluation> runs)
    {
        var uniqueOutcomes = runs
            .Select(OutcomeFingerprint)
            .Distinct(StringComparer.Ordinal)
            .Count();
        var distinctSeedCount = runs.Select(run => run.Seed).Distinct().Count();

        return new StrategyScenarioAggregate
        {
            ScenarioId = scenarioId,
            StrategyId = strategyId,
            RunCount = runs.Count,
            MetricCompleteRunCount = runs.Count(HasCompleteCoreMetrics),
            GroundTruthContainment = Rate(runs.Select(run => run.GroundTruthContainment)),
            FalseExclusion = Rate(runs.Select(run => run.FalseExclusion)),
            ExplicitFalseExclusion = Rate(runs.Select(run => run.ExplicitFalseExclusion)),
            DeviceOnlyPromotionViolationCount = runs
                .Where(run => run.DeviceOnlyPromotionViolation.Available)
                .Sum(run => run.DeviceOnlyPromotionViolation.Value ?? 0),
            CandidateRegionCount = Distribution(runs
                .Where(run => run.CandidateRegionCount.Available)
                .Select(run => (decimal)run.CandidateRegionCount.Value!.Value)),
            SearchSpaceFraction = Distribution(runs
                .Where(run => run.SearchSpaceFraction.Available)
                .Select(run => run.SearchSpaceFraction.Value!.Value)),
            SearchSpaceReduction = Distribution(runs
                .Where(run => run.SearchSpaceReduction.Available)
                .Select(run => run.SearchSpaceReduction.Value!.Value)),
            PreferredContainment = Rate(runs.Select(run => run.PreferredContainment)),
            OuterPlausibleContainment = Rate(runs.Select(run => run.OuterPlausibleContainment)),
            PreferredCandidateRegionCount = Distribution(runs
                .Where(run => run.PreferredCandidateRegionCount.Available)
                .Select(run => (decimal)run.PreferredCandidateRegionCount.Value!.Value)),
            OuterPlausibleCandidateRegionCount = Distribution(runs
                .Where(run => run.OuterPlausibleCandidateRegionCount.Available)
                .Select(run => (decimal)run.OuterPlausibleCandidateRegionCount.Value!.Value)),
            PreferredReduction = Distribution(runs
                .Where(run => run.PreferredReduction.Available)
                .Select(run => run.PreferredReduction.Value!.Value)),
            OuterPlausibleReduction = Distribution(runs
                .Where(run => run.OuterPlausibleReduction.Available)
                .Select(run => run.OuterPlausibleReduction.Value!.Value)),
            PreferredPersistenceFraction = Distribution(runs
                .Where(run => run.PreferredGuidance.PersistenceFraction.Available)
                .Select(run => run.PreferredGuidance.PersistenceFraction.Value!.Value)),
            OuterSafePersistenceFraction = Distribution(runs
                .Where(run => run.OuterSafeGuidance.PersistenceFraction.Available)
                .Select(run => run.OuterSafeGuidance.PersistenceFraction.Value!.Value)),
            PreferredTimeToUsefulGuidance = Distribution(runs
                .Where(run => run.PreferredGuidance.TimeToUsefulGuidance.Available)
                .Select(run => (decimal)run.PreferredGuidance.TimeToUsefulGuidance.Value!.Value)),
            PreferredFinalGuidanceUseful = Rate(runs.Select(run =>
                run.PreferredGuidance.FinalGuidanceUseful)),
            PreferredGuidanceRegressedAfterUseful = Rate(runs.Select(run =>
                run.PreferredGuidance.RegressedAfterUseful)),
            OuterSafeTimeToUsefulGuidance = Distribution(runs
                .Where(run => run.OuterSafeGuidance.TimeToUsefulGuidance.Available)
                .Select(run => (decimal)run.OuterSafeGuidance.TimeToUsefulGuidance.Value!.Value)),
            OuterSafeFinalGuidanceUseful = Rate(runs.Select(run =>
                run.OuterSafeGuidance.FinalGuidanceUseful)),
            OuterSafeGuidanceRegressedAfterUseful = Rate(runs.Select(run =>
                run.OuterSafeGuidance.RegressedAfterUseful)),
            CrossThreadConstraintAppliedCount = runs.Sum(run =>
                run.CrossThreadConstraintAppliedCount.Value ?? 0),
            CrossThreadConstraintConflictCount = runs.Sum(run =>
                run.CrossThreadConstraintConflictCount.Value ?? 0),
            CrossThreadConstraintBranchCount = runs.Sum(run =>
                run.CrossThreadConstraintBranchCount.Value ?? 0),
            RetainedAlternativeCount = runs.Sum(run => run.RetainedAlternativeCount.Value ?? 0),
            TimeToUsefulGuidance = Distribution(runs
                .Where(run => run.TimeToUsefulGuidance.Available)
                .Select(run => (decimal)run.TimeToUsefulGuidance.Value!.Value)),
            FinalGuidanceUseful = Rate(runs.Select(run => run.FinalGuidanceUseful)),
            UsefulGuidanceTickCount = Distribution(runs
                .Where(run => run.UsefulGuidanceTickCount.Available)
                .Select(run => (decimal)run.UsefulGuidanceTickCount.Value!.Value)),
            EvaluatedTickCount = Distribution(runs
                .Where(run => run.EvaluatedTickCount.Available)
                .Select(run => (decimal)run.EvaluatedTickCount.Value!.Value)),
            UsefulGuidancePersistenceFraction = Distribution(runs
                .Where(run => run.UsefulGuidancePersistenceFraction.Available)
                .Select(run => run.UsefulGuidancePersistenceFraction.Value!.Value)),
            LongestUsefulGuidanceStreak = Distribution(runs
                .Where(run => run.LongestUsefulGuidanceStreak.Available)
                .Select(run => (decimal)run.LongestUsefulGuidanceStreak.Value!.Value)),
            GuidanceRegressedAfterUseful = Rate(runs.Select(run =>
                run.GuidanceRegressedAfterUseful)),
            EvidenceBoundaryStateCounts = new EvidenceStateCounts
            {
                Confirmed = runs.Count(run => run.EvidenceBoundaryState == EvidenceState.Confirmed),
                SupportedButUncertain = runs.Count(run =>
                    run.EvidenceBoundaryState == EvidenceState.SupportedButUncertain),
                Conflicting = runs.Count(run => run.EvidenceBoundaryState == EvidenceState.Conflicting),
                Indeterminate = runs.Count(run => run.EvidenceBoundaryState == EvidenceState.Indeterminate),
                UnsupportedForQuery = runs.Count(run =>
                    run.EvidenceBoundaryState == EvidenceState.UnsupportedForQuery)
            },
            ProvenanceSourceKindCounts = runs
                .SelectMany(run => run.CandidateRegionProvenance)
                .GroupBy(item => item.SourceKind)
                .OrderBy(grouping => grouping.Key)
                .Select(grouping => new ProvenanceSourceKindCount
                {
                    SourceKind = grouping.Key,
                    Count = grouping.Count()
                })
                .ToArray(),
            UniqueOutcomeCount = uniqueOutcomes,
            SeedSensitivityNote = distinctSeedCount < 2
                ? "seed sensitivity unavailable: fewer than two distinct seeds"
                : uniqueOutcomes == 1
                    ? scenarioId.StartsWith("s2-", StringComparison.Ordinal)
                        ? "current synthetic S2 strategy result is seed-insensitive"
                        : scenarioId.StartsWith("s3-", StringComparison.Ordinal)
                            ? "current synthetic S3 strategy result is seed-insensitive"
                            : "current M0 scenario is seed-insensitive for this strategy result"
                    : scenarioId.StartsWith("s2-", StringComparison.Ordinal) ||
                        scenarioId.StartsWith("s3-", StringComparison.Ordinal)
                        ? "outcomes vary across seeds in the synthetic stochastic scenario"
                        : "outcomes vary across seeds under current M0 observation timing"
        };
    }

    private static ScenarioTruthAggregate CreateTruthGroup(
        IGrouping<string, PerRunEvaluation> group)
    {
        var runs = group.OrderBy(run => run.Seed).ToArray();
        return new ScenarioTruthAggregate
        {
            ScenarioId = group.Key,
            SeedCount = runs.Length,
            PersonFinalRegionCounts = runs
                .Where(run => run.RealizedPersonFinalRegionId is not null)
                .GroupBy(run => run.RealizedPersonFinalRegionId!, StringComparer.Ordinal)
                .OrderBy(item => item.Key, StringComparer.Ordinal)
                .Select(item => new RegionOutcomeCount
                {
                    RegionId = item.Key,
                    Count = item.Count()
                })
                .ToArray(),
            DeviceFinalRegionCounts = runs
                .SelectMany(run => run.RealizedDeviceFinalRegionIds)
                .GroupBy(item => (item.Key, item.Value))
                .OrderBy(item => item.Key.Key, StringComparer.Ordinal)
                .ThenBy(item => item.Key.Value, StringComparer.Ordinal)
                .Select(item => new DeviceRegionOutcomeCount
                {
                    DeviceId = item.Key.Key,
                    RegionId = item.Key.Value,
                    Count = item.Count()
                })
                .ToArray(),
            UniqueTruthOutcomeCount = runs
                .Select(run => run.TruthOutcomeIdentifier)
                .Distinct(StringComparer.Ordinal)
                .Count(),
            SecondaryTransportRunCount = runs.Count(run =>
                run.SecondaryTransportEventCount > 0),
            SecondaryTransportEventCount = runs.Sum(run =>
                run.SecondaryTransportEventCount),
            PersonDeviceDifferentTerminalCount = runs.Count(run =>
                run.RealizedPersonFinalRegionId is not null &&
                run.RealizedDeviceFinalRegionIds.Values.Any(regionId => !string.Equals(
                    regionId,
                    run.RealizedPersonFinalRegionId,
                    StringComparison.Ordinal))),
            WeftEvidenceConditionCounts = runs
                .Where(run => run.WeftEvidenceCondition is not null)
                .GroupBy(run => run.WeftEvidenceCondition!.Value)
                .OrderBy(grouping => grouping.Key)
                .Select(grouping => new WeftEvidenceConditionCount
                {
                    Condition = grouping.Key,
                    Count = grouping.Count()
                })
                .ToArray()
        };
    }

    private static IReadOnlyList<PairedWeftAggregate> CreatePairedGroups(
        IEnumerable<PerRunEvaluation> evaluations)
    {
        var pairs = evaluations
            .Where(run => run.StrategyId is SearchStrategyIds.OwnEvidenceConstrained or
                SearchStrategyIds.CrossThreadConstrained)
            .GroupBy(run => (run.ScenarioId, run.Seed))
            .Select(group => new
            {
                group.Key.ScenarioId,
                group.Key.Seed,
                Own = group.SingleOrDefault(run =>
                    run.StrategyId == SearchStrategyIds.OwnEvidenceConstrained),
                Cross = group.SingleOrDefault(run =>
                    run.StrategyId == SearchStrategyIds.CrossThreadConstrained)
            })
            .Where(item => item.Own is not null && item.Cross is not null)
            .Select(item => new Pair(item.ScenarioId, item.Seed, item.Own!, item.Cross!))
            .OrderBy(item => item.ScenarioId, StringComparer.Ordinal)
            .ThenBy(item => item.Seed)
            .ToArray();

        var result = new List<PairedWeftAggregate>();
        foreach (var scenarioGroup in pairs.GroupBy(pair => pair.ScenarioId, StringComparer.Ordinal)
            .OrderBy(group => group.Key, StringComparer.Ordinal))
        {
            result.Add(CreatePairedGroup(scenarioGroup.Key, null, scenarioGroup.ToArray()));
            result.AddRange(scenarioGroup
                .Where(pair => pair.Cross.WeftEvidenceCondition is not null)
                .GroupBy(pair => pair.Cross.WeftEvidenceCondition!.Value)
                .OrderBy(group => group.Key)
                .Select(group => CreatePairedGroup(
                    scenarioGroup.Key,
                    group.Key,
                    group.ToArray())));
        }

        return result;
    }

    private static PairedWeftAggregate CreatePairedGroup(
        string scenarioId,
        WeftEvidenceCondition? condition,
        IReadOnlyList<Pair> pairs) => new()
        {
            ScenarioId = scenarioId,
            Condition = condition,
            PairCount = pairs.Count,
            PreferredCandidateCountDelta = Distribution(pairs.Select(pair =>
                (decimal)(pair.Cross.PreferredCandidateRegionCount.Value!.Value -
                    pair.Own.PreferredCandidateRegionCount.Value!.Value))),
            OuterPlausibleCandidateCountDelta = Distribution(pairs.Select(pair =>
                (decimal)(pair.Cross.OuterPlausibleCandidateRegionCount.Value!.Value -
                    pair.Own.OuterPlausibleCandidateRegionCount.Value!.Value))),
            PreferredReductionDelta = Distribution(pairs.Select(pair =>
                pair.Cross.PreferredReduction.Value!.Value - pair.Own.PreferredReduction.Value!.Value)),
            OuterPlausibleReductionDelta = Distribution(pairs.Select(pair =>
                pair.Cross.OuterPlausibleReduction.Value!.Value -
                    pair.Own.OuterPlausibleReduction.Value!.Value)),
            PreferredContainmentDelta = Distribution(pairs
            .Where(pair => pair.Cross.PreferredContainment.Available &&
                pair.Own.PreferredContainment.Available)
            .Select(pair => BoolDecimal(pair.Cross.PreferredContainment.Value) -
                BoolDecimal(pair.Own.PreferredContainment.Value))),
            OuterPlausibleContainmentDelta = Distribution(pairs
            .Where(pair => pair.Cross.OuterPlausibleContainment.Available &&
                pair.Own.OuterPlausibleContainment.Available)
            .Select(pair => BoolDecimal(pair.Cross.OuterPlausibleContainment.Value) -
                BoolDecimal(pair.Own.OuterPlausibleContainment.Value))),
            TimeToGuidanceDelta = Distribution(pairs
            .Where(pair => pair.Cross.PreferredGuidance.TimeToUsefulGuidance.Available &&
                pair.Own.PreferredGuidance.TimeToUsefulGuidance.Available)
            .Select(pair => (decimal)(
                pair.Cross.PreferredGuidance.TimeToUsefulGuidance.Value!.Value -
                pair.Own.PreferredGuidance.TimeToUsefulGuidance.Value!.Value))),
            PersistenceDelta = Distribution(pairs
            .Where(pair => pair.Cross.PreferredGuidance.PersistenceFraction.Available &&
                pair.Own.PreferredGuidance.PersistenceFraction.Available)
            .Select(pair => pair.Cross.PreferredGuidance.PersistenceFraction.Value!.Value -
                pair.Own.PreferredGuidance.PersistenceFraction.Value!.Value))
        };

    private static decimal BoolDecimal(bool? value) => value == true ? 1m : 0m;

    private sealed record Pair(
        string ScenarioId,
        int Seed,
        PerRunEvaluation Own,
        PerRunEvaluation Cross);

    private static RateAggregate Rate(IEnumerable<BooleanMetric> metrics)
    {
        var available = metrics.Where(metric => metric.Available).ToArray();
        var numerator = available.Sum(metric => metric.Numerator ?? 0);
        var denominator = available.Sum(metric => metric.Denominator ?? 0);
        return new RateAggregate
        {
            AvailableCount = available.Length,
            Numerator = numerator,
            Denominator = denominator,
            Rate = denominator == 0 ? null : (decimal)numerator / denominator
        };
    }

    private static DistributionAggregate Distribution(IEnumerable<decimal> values)
    {
        var ordered = values.OrderBy(value => value).ToArray();
        if (ordered.Length == 0)
        {
            return new DistributionAggregate { AvailableCount = 0 };
        }

        var middle = ordered.Length / 2;
        var median = ordered.Length % 2 == 1
            ? ordered[middle]
            : (ordered[middle - 1] + ordered[middle]) / 2m;

        return new DistributionAggregate
        {
            AvailableCount = ordered.Length,
            Mean = ordered.Sum() / ordered.Length,
            Median = median,
            Minimum = ordered[0],
            Maximum = ordered[^1]
        };
    }

    private static bool HasCompleteCoreMetrics(PerRunEvaluation run)
    {
        return run.GroundTruthContainment.Available &&
            run.CandidateRegionCount.Available &&
            run.SearchSpaceFraction.Available &&
            run.SearchSpaceReduction.Available &&
            run.FalseExclusion.Available &&
            run.ExplicitFalseExclusion.Available &&
            run.DeviceOnlyPromotionViolation.Available;
    }

    private static string OutcomeFingerprint(PerRunEvaluation run)
    {
        return string.Join(
            "\n",
            string.Join("|", run.CandidateRegionIds),
            string.Join("|", run.PreferredCandidateRegionIds),
            string.Join("|", run.OuterPlausibleCandidateRegionIds),
            string.Join("|", run.ConflictedCandidateRegionIds),
            string.Join("|", run.ConfirmedPersonRegionIds),
            string.Join("|", run.ExplicitlyExcludedRegionIds),
            run.EvidenceBoundaryState.ToString(),
            MetricText(run.GroundTruthContainment.Available, run.GroundTruthContainment.Value),
            MetricText(run.PreferredContainment.Available, run.PreferredContainment.Value),
            MetricText(run.OuterPlausibleContainment.Available, run.OuterPlausibleContainment.Value),
            MetricText(run.SearchSpaceFraction.Available, run.SearchSpaceFraction.Value),
            MetricText(run.SearchSpaceReduction.Available, run.SearchSpaceReduction.Value),
            MetricText(run.DeviceOnlyPromotionViolation.Available, run.DeviceOnlyPromotionViolation.Value),
            MetricText(run.TimeToUsefulGuidance.Available, run.TimeToUsefulGuidance.Value),
            MetricText(run.FinalGuidanceUseful.Available, run.FinalGuidanceUseful.Value),
            MetricText(
                run.UsefulGuidancePersistenceFraction.Available,
                run.UsefulGuidancePersistenceFraction.Value),
            MetricText(
                run.GuidanceRegressedAfterUseful.Available,
                run.GuidanceRegressedAfterUseful.Value),
            run.WeftEvidenceCondition?.ToString() ?? "no-weft-condition",
            string.Join("|", run.CrossThreadConstraintAudits.Select(audit =>
                $"{audit.SourceObservationId}:{audit.Disposition}:{audit.Reason}")));
    }

    private static string MetricText<T>(bool available, T? value)
    {
        if (!available)
        {
            return "unavailable";
        }

        return value switch
        {
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
            _ => value?.ToString() ?? "null"
        };
    }

    private sealed class EvaluationGroupComparer :
        IEqualityComparer<(string ScenarioId, string StrategyId)>
    {
        public static EvaluationGroupComparer Instance { get; } = new();

        public bool Equals(
            (string ScenarioId, string StrategyId) x,
            (string ScenarioId, string StrategyId) y)
        {
            return string.Equals(x.ScenarioId, y.ScenarioId, StringComparison.Ordinal) &&
                string.Equals(x.StrategyId, y.StrategyId, StringComparison.Ordinal);
        }

        public int GetHashCode((string ScenarioId, string StrategyId) obj)
        {
            return HashCode.Combine(
                StringComparer.Ordinal.GetHashCode(obj.ScenarioId),
                StringComparer.Ordinal.GetHashCode(obj.StrategyId));
        }
    }
}
