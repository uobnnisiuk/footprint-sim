using System.Globalization;
using FootprintSim.Evidence;

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
}

public sealed record AggregateReport
{
    public int SchemaVersion { get; init; } = 2;

    public IReadOnlyList<StrategyScenarioAggregate> Groups { get; init; } = [];

    public IReadOnlyList<ScenarioTruthAggregate> TruthGroups { get; init; } = [];
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

        return new AggregateReport { Groups = groups, TruthGroups = truthGroups };
    }

    private static StrategyScenarioAggregate CreateGroup(
        IGrouping<(string ScenarioId, string StrategyId), PerRunEvaluation> group)
    {
        var runs = group.OrderBy(run => run.Seed).ToArray();
        var uniqueOutcomes = runs
            .Select(OutcomeFingerprint)
            .Distinct(StringComparer.Ordinal)
            .Count();
        var distinctSeedCount = runs.Select(run => run.Seed).Distinct().Count();

        return new StrategyScenarioAggregate
        {
            ScenarioId = group.Key.ScenarioId,
            StrategyId = group.Key.StrategyId,
            RunCount = runs.Length,
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
                    ? group.Key.ScenarioId.StartsWith("s2-", StringComparison.Ordinal)
                        ? "current synthetic S2 strategy result is seed-insensitive"
                        : "current M0 scenario is seed-insensitive for this strategy result"
                    : group.Key.ScenarioId.StartsWith("s2-", StringComparison.Ordinal)
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
                run.SecondaryTransportEventCount)
        };
    }

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
            string.Join("|", run.ConfirmedPersonRegionIds),
            string.Join("|", run.ExplicitlyExcludedRegionIds),
            run.EvidenceBoundaryState.ToString(),
            MetricText(run.GroundTruthContainment.Available, run.GroundTruthContainment.Value),
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
                run.GuidanceRegressedAfterUseful.Value));
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
