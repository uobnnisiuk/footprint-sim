using FootprintSim.Evidence;
using FootprintSim.Events;
using FootprintSim.Model;
using FootprintSim.Interactions;
using FootprintSim.Interactions.Truth;
using FootprintSim.Scenarios;

namespace FootprintSim.Simulation;

public sealed record SimulationRun
{
    public required string ScenarioId { get; init; }

    public required int Seed { get; init; }

    public IReadOnlyList<SimulationEvent> GroundTruthEvents { get; init; } = [];

    public required ObservationSet ObservationSet { get; init; }

    public IReadOnlyDictionary<string, EntityState> GroundTruthFinalEntityStates { get; init; } =
        new SortedDictionary<string, EntityState>(StringComparer.Ordinal);

    public IReadOnlyList<InteractionOccurrence> GroundTruthInteractionOccurrences { get; init; } = [];

    public InteractionObservationSet? InteractionObservations { get; init; }

    public CrossThreadConstraintSet? CrossThreadConstraints { get; init; }

    public WeftEvidenceCondition? WeftEvidenceCondition { get; init; }
}
