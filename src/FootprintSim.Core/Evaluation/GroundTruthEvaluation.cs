using FootprintSim.Evidence;

namespace FootprintSim.Evaluation.Truth;

public sealed record GroundTruthEvaluationContext
{
    public required string ScenarioId { get; init; }

    public required int Seed { get; init; }

    public required string TargetPersonId { get; init; }

    public string? TruePersonRegionId { get; init; }

    public IReadOnlyList<string> SearchableRegionIds { get; init; } = [];

    public IReadOnlyList<string> DeviceIds { get; init; } = [];
}

public sealed class PerRunEvaluator
{
    public PerRunEvaluation Evaluate(
        GroundTruthEvaluationContext truth,
        SearchStrategyResult finalResult,
        IReadOnlyList<SearchStrategyResult> timeline,
        ObservationSet completeEvidence)
    {
        ArgumentNullException.ThrowIfNull(truth);
        ArgumentNullException.ThrowIfNull(finalResult);
        ArgumentNullException.ThrowIfNull(timeline);
        ArgumentNullException.ThrowIfNull(completeEvidence);
        ValidateIdentifiers(truth, finalResult, completeEvidence);

        var searchable = truth.SearchableRegionIds
            .Distinct(StringComparer.Ordinal)
            .OrderBy(regionId => regionId, StringComparer.Ordinal)
            .ToArray();
        var searchableSet = searchable.ToHashSet(StringComparer.Ordinal);
        var candidates = finalResult.CandidateRegionIds
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var unknownCandidate = candidates.FirstOrDefault(candidate => !searchableSet.Contains(candidate));
        if (unknownCandidate is not null)
        {
            throw new InvalidOperationException(
                $"Strategy '{finalResult.StrategyId}' returned unknown region '{unknownCandidate}'.");
        }

        ValidateResultRegions(finalResult, searchableSet);
        ValidateTimeline(truth, finalResult, timeline, searchableSet);

        var candidateCount = candidates.Length;
        var truthAvailable = truth.TruePersonRegionId is not null &&
            searchableSet.Contains(truth.TruePersonRegionId);
        var containment = truthAvailable
            ? AvailableBoolean(candidates.Contains(truth.TruePersonRegionId!, StringComparer.Ordinal))
            : UnavailableBoolean("truth-region-unavailable-or-not-searchable");
        var falseExclusion = containment.Available
            ? AvailableBoolean(!containment.Value!.Value)
            : UnavailableBoolean(containment.UnavailableReason!);
        var explicitFalseExclusion = truthAvailable
            ? AvailableBoolean(finalResult.ExplicitlyExcludedRegionIds.Contains(
                truth.TruePersonRegionId!,
                StringComparer.Ordinal))
            : UnavailableBoolean("truth-region-unavailable-or-not-searchable");

        var fraction = searchable.Length == 0
            ? UnavailableDecimal("no-searchable-regions")
            : AvailableDecimal(candidateCount, searchable.Length);
        var reduction = searchable.Length == 0
            ? UnavailableDecimal("no-searchable-regions")
            : AvailableDecimal(searchable.Length - candidateCount, searchable.Length);
        var violations = CountDeviceOnlyPromotionViolations(
            truth,
            finalResult,
            completeEvidence);
        var timeToGuidance = EvaluateTimeToUsefulGuidance(
            truth,
            searchableSet,
            timeline);

        return new PerRunEvaluation
        {
            ScenarioId = finalResult.ScenarioId,
            Seed = finalResult.Seed,
            StrategyId = finalResult.StrategyId,
            ProducedAtTick = finalResult.ProducedAtTick,
            CandidateRegionIds = candidates,
            ConfirmedPersonRegionIds = finalResult.ConfirmedPersonRegionIds,
            ExplicitlyExcludedRegionIds = finalResult.ExplicitlyExcludedRegionIds,
            RationaleCodes = finalResult.RationaleCodes,
            EvidenceBoundaryState = finalResult.EvidenceBoundaryState,
            SearchableRegionCount = searchable.Length,
            GroundTruthContainment = containment,
            CandidateRegionCount = new IntegerMetric { Available = true, Value = candidateCount },
            SearchSpaceFraction = fraction,
            SearchSpaceReduction = reduction,
            FalseExclusion = falseExclusion,
            ExplicitFalseExclusion = explicitFalseExclusion,
            DeviceOnlyPromotionViolation = new IntegerMetric
            {
                Available = true,
                Value = violations
            },
            TimeToUsefulGuidance = timeToGuidance
        };
    }

    private static int CountDeviceOnlyPromotionViolations(
        GroundTruthEvaluationContext truth,
        SearchStrategyResult result,
        ObservationSet evidence)
    {
        var deviceIds = truth.DeviceIds.ToHashSet(StringComparer.Ordinal);
        var confirmedPersonRegions = result.ConfirmedPersonRegionIds.ToHashSet(StringComparer.Ordinal);
        return evidence.Observations.Count(observation =>
            observation.AvailableTick <= result.ProducedAtTick &&
            observation.ReportedRegionId is not null &&
            observation.SubjectIds.Any(deviceIds.Contains) &&
            !observation.SubjectIds.Contains(truth.TargetPersonId, StringComparer.Ordinal) &&
            confirmedPersonRegions.Contains(observation.ReportedRegionId));
    }

    private static TickMetric EvaluateTimeToUsefulGuidance(
        GroundTruthEvaluationContext truth,
        IReadOnlySet<string> searchable,
        IEnumerable<SearchStrategyResult> timeline)
    {
        if (truth.TruePersonRegionId is null || !searchable.Contains(truth.TruePersonRegionId))
        {
            return new TickMetric
            {
                Available = false,
                UnavailableReason = "truth-region-unavailable-or-not-searchable"
            };
        }

        var useful = timeline
            .OrderBy(result => result.ProducedAtTick)
            .FirstOrDefault(result =>
                result.CandidateRegionIds.Contains(truth.TruePersonRegionId, StringComparer.Ordinal) &&
                result.CandidateRegionIds.Distinct(StringComparer.Ordinal).Count() < searchable.Count);

        return useful is null
            ? new TickMetric
            {
                Available = false,
                Value = null,
                UnavailableReason = "condition-never-met"
            }
            : new TickMetric
            {
                Available = true,
                Value = useful.ProducedAtTick
            };
    }

    private static BooleanMetric AvailableBoolean(bool value) => new()
    {
        Available = true,
        Value = value,
        Numerator = value ? 1 : 0,
        Denominator = 1
    };

    private static BooleanMetric UnavailableBoolean(string reason) => new()
    {
        Available = false,
        Value = null,
        Numerator = null,
        Denominator = null,
        UnavailableReason = reason
    };

    private static DecimalMetric AvailableDecimal(int numerator, int denominator) => new()
    {
        Available = true,
        Value = (decimal)numerator / denominator,
        Numerator = numerator,
        Denominator = denominator
    };

    private static DecimalMetric UnavailableDecimal(string reason) => new()
    {
        Available = false,
        Value = null,
        Numerator = null,
        Denominator = null,
        UnavailableReason = reason
    };

    private static void ValidateIdentifiers(
        GroundTruthEvaluationContext truth,
        SearchStrategyResult result,
        ObservationSet evidence)
    {
        if (!string.Equals(truth.ScenarioId, result.ScenarioId, StringComparison.Ordinal) ||
            !string.Equals(truth.ScenarioId, evidence.ScenarioId, StringComparison.Ordinal) ||
            truth.Seed != result.Seed ||
            truth.Seed != evidence.Seed)
        {
            throw new ArgumentException("Evaluation identifiers do not match.");
        }
    }

    private static void ValidateResultRegions(
        SearchStrategyResult result,
        IReadOnlySet<string> searchable)
    {
        var unknownRegion = result.ConfirmedPersonRegionIds
            .Concat(result.ExplicitlyExcludedRegionIds)
            .FirstOrDefault(regionId => !searchable.Contains(regionId));
        if (unknownRegion is not null)
        {
            throw new InvalidOperationException(
                $"Strategy '{result.StrategyId}' classified unknown region '{unknownRegion}'.");
        }
    }

    private static void ValidateTimeline(
        GroundTruthEvaluationContext truth,
        SearchStrategyResult finalResult,
        IEnumerable<SearchStrategyResult> timeline,
        IReadOnlySet<string> searchable)
    {
        foreach (var result in timeline)
        {
            if (!string.Equals(truth.ScenarioId, result.ScenarioId, StringComparison.Ordinal) ||
                truth.Seed != result.Seed ||
                !string.Equals(finalResult.StrategyId, result.StrategyId, StringComparison.Ordinal))
            {
                throw new ArgumentException("Timeline evaluation identifiers do not match.");
            }

            if (result.ProducedAtTick < 0 || result.ProducedAtTick > finalResult.ProducedAtTick)
            {
                throw new ArgumentException("Timeline contains a result outside the evaluated tick range.");
            }

            var unknownRegion = result.CandidateRegionIds.FirstOrDefault(regionId =>
                !searchable.Contains(regionId));
            if (unknownRegion is not null)
            {
                throw new InvalidOperationException(
                    $"Strategy '{result.StrategyId}' returned unknown timeline region '{unknownRegion}'.");
            }
        }
    }
}
