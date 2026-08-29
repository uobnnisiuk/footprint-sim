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

    public IReadOnlyList<CandidateRegionProvenance> CandidateRegionProvenance { get; init; } = [];
}

public enum CandidateSourceKind
{
    LastConfirmed,
    VoluntaryReachability,
    PublicHazardEnvelope,
    PersonObservation,
    DeviceOnlyObservation,
    ConflictingObservation,
    Unsupported
}

public sealed record CandidateRegionProvenance
{
    public required string RegionId { get; init; }

    public required CandidateSourceKind SourceKind { get; init; }

    public required string SourceIdentifier { get; init; }

    public required long AvailableAtTick { get; init; }

    public required bool IsPersonEvidence { get; init; }

    public required bool IsDeviceOnlyEvidence { get; init; }

    public required EvidenceState EvidenceBoundaryState { get; init; }
}

public interface ISearchStrategy
{
    string Id { get; }

    SearchStrategyResult Evaluate(SearchStrategyInput input);
}
