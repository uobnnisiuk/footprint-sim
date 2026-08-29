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

    public IReadOnlyDictionary<string, string> TrueDeviceRegionIds { get; init; } =
        new SortedDictionary<string, string>(StringComparer.Ordinal);

    public int SecondaryTransportEventCount { get; init; }

    public string TruthOutcomeIdentifier { get; init; } = "unavailable";
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
        var guidance = EvaluateGuidance(
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
            CandidateRegionProvenance = finalResult.CandidateRegionProvenance,
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
            TimeToUsefulGuidance = guidance.TimeToUsefulGuidance,
            FinalGuidanceUseful = guidance.FinalGuidanceUseful,
            UsefulGuidanceTickCount = guidance.UsefulGuidanceTickCount,
            EvaluatedTickCount = guidance.EvaluatedTickCount,
            UsefulGuidancePersistenceFraction = guidance.PersistenceFraction,
            LongestUsefulGuidanceStreak = guidance.LongestStreak,
            GuidanceRegressedAfterUseful = guidance.RegressedAfterUseful,
            RealizedPersonFinalRegionId = truth.TruePersonRegionId,
            RealizedDeviceFinalRegionIds = truth.TrueDeviceRegionIds,
            SecondaryTransportEventCount = truth.SecondaryTransportEventCount,
            TruthOutcomeIdentifier = truth.TruthOutcomeIdentifier
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

    private static GuidanceMetrics EvaluateGuidance(
        GroundTruthEvaluationContext truth,
        IReadOnlySet<string> searchable,
        IEnumerable<SearchStrategyResult> timeline)
    {
        var ordered = timeline.OrderBy(result => result.ProducedAtTick).ToArray();
        var evaluatedTickCount = new IntegerMetric
        {
            Available = true,
            Value = ordered.Length
        };
        if (truth.TruePersonRegionId is null || !searchable.Contains(truth.TruePersonRegionId))
        {
            const string reason = "truth-region-unavailable-or-not-searchable";
            return new GuidanceMetrics(
                new TickMetric
                {
                    Available = false,
                    UnavailableReason = reason
                },
                UnavailableBoolean(reason),
                UnavailableInteger(reason),
                evaluatedTickCount,
                UnavailableDecimal(reason),
                UnavailableInteger(reason),
                UnavailableBoolean(reason));
        }

        if (ordered.Length == 0)
        {
            const string reason = "no-evaluated-ticks";
            return new GuidanceMetrics(
                new TickMetric
                {
                    Available = false,
                    UnavailableReason = reason
                },
                UnavailableBoolean(reason),
                UnavailableInteger(reason),
                evaluatedTickCount,
                UnavailableDecimal(reason),
                UnavailableInteger(reason),
                UnavailableBoolean(reason));
        }

        var usefulByTick = ordered.Select(result => IsUseful(
            result,
            truth.TruePersonRegionId,
            searchable.Count)).ToArray();
        var firstUsefulIndex = Array.FindIndex(usefulByTick, value => value);
        var usefulCount = usefulByTick.Count(value => value);
        var longestStreak = 0;
        var currentStreak = 0;
        foreach (var useful in usefulByTick)
        {
            currentStreak = useful ? currentStreak + 1 : 0;
            longestStreak = Math.Max(longestStreak, currentStreak);
        }

        var regressed = firstUsefulIndex >= 0 && usefulByTick
            .Skip(firstUsefulIndex + 1)
            .Any(value => !value);
        var timeToGuidance = firstUsefulIndex < 0
            ? new TickMetric
            {
                Available = false,
                UnavailableReason = "condition-never-met"
            }
            : new TickMetric
            {
                Available = true,
                Value = ordered[firstUsefulIndex].ProducedAtTick
            };

        return new GuidanceMetrics(
            timeToGuidance,
            AvailableBoolean(usefulByTick[^1]),
            AvailableInteger(usefulCount),
            evaluatedTickCount,
            AvailableDecimal(usefulCount, ordered.Length),
            AvailableInteger(longestStreak),
            AvailableBoolean(regressed));
    }

    private static bool IsUseful(
        SearchStrategyResult result,
        string truthRegionId,
        int searchableCount)
    {
        return result.CandidateRegionIds.Contains(truthRegionId, StringComparer.Ordinal) &&
            result.CandidateRegionIds.Distinct(StringComparer.Ordinal).Count() < searchableCount;
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

    private static IntegerMetric AvailableInteger(int value) => new()
    {
        Available = true,
        Value = value
    };

    private static IntegerMetric UnavailableInteger(string reason) => new()
    {
        Available = false,
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

        var candidateSet = result.CandidateRegionIds.ToHashSet(StringComparer.Ordinal);
        var invalidProvenance = result.CandidateRegionProvenance.FirstOrDefault(item =>
            !candidateSet.Contains(item.RegionId) ||
            item.AvailableAtTick > result.ProducedAtTick ||
            item.AvailableAtTick < 0 ||
            item.IsPersonEvidence && item.IsDeviceOnlyEvidence);
        if (invalidProvenance is not null)
        {
            throw new InvalidOperationException(
                $"Strategy '{result.StrategyId}' returned invalid candidate provenance.");
        }

        var missingProvenance = candidateSet.FirstOrDefault(regionId =>
            !result.CandidateRegionProvenance.Any(item => string.Equals(
                item.RegionId,
                regionId,
                StringComparison.Ordinal)));
        if (missingProvenance is not null)
        {
            throw new InvalidOperationException(
                $"Strategy '{result.StrategyId}' omitted provenance for '{missingProvenance}'.");
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

            ValidateResultRegions(result, searchable);
        }
    }

    private sealed record GuidanceMetrics(
        TickMetric TimeToUsefulGuidance,
        BooleanMetric FinalGuidanceUseful,
        IntegerMetric UsefulGuidanceTickCount,
        IntegerMetric EvaluatedTickCount,
        DecimalMetric PersistenceFraction,
        IntegerMetric LongestStreak,
        BooleanMetric RegressedAfterUseful);
}
