using FootprintSim.Evidence;
using FootprintSim.Inference;

namespace FootprintSim.Evaluation;

public sealed record SearchStrategyInput
{
    public required string ScenarioId { get; init; }

    public required int Seed { get; init; }

    public required long CurrentTick { get; init; }

    public required RescueContext PublicContext { get; init; }

    public required ObservationSet AvailableEvidence { get; init; }
}

public sealed record SearchStrategyResult
{
    public required string StrategyId { get; init; }

    public required string ScenarioId { get; init; }

    public required int Seed { get; init; }

    public required long ProducedAtTick { get; init; }

    public IReadOnlyList<string> CandidateRegionIds { get; init; } = [];

    public required EvidenceState EvidenceBoundaryState { get; init; }

    public IReadOnlyList<string> RationaleCodes { get; init; } = [];

    public IReadOnlyList<string> ConfirmedPersonRegionIds { get; init; } = [];

    public IReadOnlyList<string> ExplicitlyExcludedRegionIds { get; init; } = [];
}

public interface ISearchStrategy
{
    string Id { get; }

    SearchStrategyResult Evaluate(SearchStrategyInput input);
}
