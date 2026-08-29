using FootprintSim.Evidence;

namespace FootprintSim.Evaluation;

public sealed record BooleanMetric
{
    public required bool Available { get; init; }

    public bool? Value { get; init; }

    public int? Numerator { get; init; }

    public int? Denominator { get; init; }

    public string? UnavailableReason { get; init; }
}

public sealed record IntegerMetric
{
    public required bool Available { get; init; }

    public int? Value { get; init; }

    public string? UnavailableReason { get; init; }
}

public sealed record DecimalMetric
{
    public required bool Available { get; init; }

    public decimal? Value { get; init; }

    public int? Numerator { get; init; }

    public int? Denominator { get; init; }

    public string? UnavailableReason { get; init; }
}

public sealed record TickMetric
{
    public required bool Available { get; init; }

    public long? Value { get; init; }

    public string? UnavailableReason { get; init; }
}

public sealed record PerRunEvaluation
{
    public int SchemaVersion { get; init; } = 1;

    public required string ScenarioId { get; init; }

    public required int Seed { get; init; }

    public required string StrategyId { get; init; }

    public required long ProducedAtTick { get; init; }

    public IReadOnlyList<string> CandidateRegionIds { get; init; } = [];

    public IReadOnlyList<string> ConfirmedPersonRegionIds { get; init; } = [];

    public IReadOnlyList<string> ExplicitlyExcludedRegionIds { get; init; } = [];

    public IReadOnlyList<string> RationaleCodes { get; init; } = [];

    public required EvidenceState EvidenceBoundaryState { get; init; }

    public required int SearchableRegionCount { get; init; }

    public required BooleanMetric GroundTruthContainment { get; init; }

    public required IntegerMetric CandidateRegionCount { get; init; }

    public required DecimalMetric SearchSpaceFraction { get; init; }

    public required DecimalMetric SearchSpaceReduction { get; init; }

    public required BooleanMetric FalseExclusion { get; init; }

    public required BooleanMetric ExplicitFalseExclusion { get; init; }

    public required IntegerMetric DeviceOnlyPromotionViolation { get; init; }

    public required TickMetric TimeToUsefulGuidance { get; init; }
}
