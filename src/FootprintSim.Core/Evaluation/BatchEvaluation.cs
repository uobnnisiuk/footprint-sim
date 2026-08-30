using FootprintSim.Evidence;
using FootprintSim.Evaluation.Truth;
using FootprintSim.Events;
using FootprintSim.Hazards;
using FootprintSim.Inference;
using FootprintSim.Interactions;
using FootprintSim.Scenarios;
using FootprintSim.Simulation;
using FootprintSim.Telemetry;

namespace FootprintSim.Evaluation;

public sealed record EvaluationScenarioInput
{
    public required ScenarioDefinition Scenario { get; init; }

    public required string ContentSha256 { get; init; }
}

public sealed record BatchEvaluationRequest
{
    public IReadOnlyList<EvaluationScenarioInput> Scenarios { get; init; } = [];

    public IReadOnlyList<int> Seeds { get; init; } = [];

    public IReadOnlyList<ISearchStrategy> Strategies { get; init; } = [];

    public required string SourceCommitSha { get; init; }

    public required bool DirtyWorkingTree { get; init; }

    public bool CollectTelemetry { get; init; }
}

public sealed record ScenarioManifestEntry
{
    public required string ScenarioId { get; init; }

    public required string ContentSha256 { get; init; }
}

public sealed record EvaluationManifest
{
    public int OutputSchemaVersion { get; init; } = 3;

    public IReadOnlyList<string> ScenarioIdentifiers { get; init; } = [];

    public IReadOnlyList<int> Seeds { get; init; } = [];

    public IReadOnlyList<string> StrategyIdentifiers { get; init; } = [];

    public required int RunCount { get; init; }

    public required string SourceCommitSha { get; init; }

    public required bool DirtyWorkingTree { get; init; }

    public IReadOnlyList<ScenarioManifestEntry> ScenarioInputs { get; init; } = [];
}

public sealed record BatchEvaluationResult
{
    public IReadOnlyList<PerRunEvaluation> Runs { get; init; } = [];

    public required AggregateReport Aggregate { get; init; }

    public required EvaluationManifest Manifest { get; init; }

    public NeutralTelemetryBundle? Telemetry { get; init; }
}

public sealed class BatchEvaluationRunner
{
    public BatchEvaluationResult Run(BatchEvaluationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var scenarios = NormalizeScenarios(request.Scenarios);
        var seeds = request.Seeds.Distinct().OrderBy(seed => seed).ToArray();
        var strategies = request.Strategies
            .GroupBy(strategy => strategy.Id, StringComparer.Ordinal)
            .Select(group => group.First())
            .OrderBy(strategy => strategy.Id, StringComparer.Ordinal)
            .ToArray();
        ValidateRequest(request, scenarios, seeds, strategies);

        var evaluations = new List<PerRunEvaluation>();
        var simulationEngine = new SimulationEngine();
        var perRunEvaluator = new PerRunEvaluator();
        var telemetry = request.CollectTelemetry ? new NeutralTelemetryCollector() : null;

        foreach (var scenarioInput in scenarios)
        {
            var scenario = scenarioInput.Scenario;
            var searchableRegionIds = (scenario.EvaluationSearchableRegionIds.Count > 0
                    ? scenario.EvaluationSearchableRegionIds
                    : scenario.Regions.Select(region => region.Id))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(regionId => regionId, StringComparer.Ordinal)
                .ToArray();
            var publicHazardEnvelope = scenario.StochasticHazard is null
                ? null
                : PublicHazardEnvelopeBuilder.Create(scenario.StochasticHazard);
            var publicContext = new RescueContext
            {
                ScenarioId = scenario.Id,
                TargetPersonId = scenario.TargetPersonId,
                NormalWalkingRadius = scenario.NormalWalkingRadius,
                PublicRegions = scenario.Regions,
                DeclaredHazardEnvelopeRegionIds = scenario.HazardTransportEnvelopeRegionIds,
                PublicHazardEnvelope = publicHazardEnvelope,
                PublicDeviceIds = scenario.Entities.Devices
                    .Select(device => device.Id)
                    .OrderBy(deviceId => deviceId, StringComparer.Ordinal)
                    .ToArray(),
                PublicCandidateWarp = scenario.WeftEvaluation is null
                    ? null
                    : new PublicCandidateWarpDefinition
                    {
                        HistoricalTimeWindow = new InteractionTimeWindow(
                            scenario.WeftEvaluation.HistoricalStartTick,
                            scenario.WeftEvaluation.HistoricalEndTick),
                        AvailableAtTick = scenario.WeftEvaluation.NormalAvailableAtTick,
                        HistoricalCandidateRegionIds = scenario.WeftEvaluation
                            .HistoricalCandidateRegionIds
                    }
            };

            foreach (var seed in seeds)
            {
                var simulation = simulationEngine.Run(scenario, seed);
                telemetry?.AddSimulation(scenario, simulation);
                var deviceFinalRegions = new SortedDictionary<string, string>(StringComparer.Ordinal);
                foreach (var deviceId in scenario.Entities.Devices
                    .Select(device => device.Id)
                    .OrderBy(deviceId => deviceId, StringComparer.Ordinal))
                {
                    var deviceRegion = simulation.GroundTruthFinalEntityStates
                        .GetValueOrDefault(deviceId)?.Position.RegionId;
                    if (deviceRegion is not null)
                    {
                        deviceFinalRegions.Add(deviceId, deviceRegion);
                    }
                }

                var truePersonRegion = simulation.GroundTruthFinalEntityStates
                    .GetValueOrDefault(scenario.TargetPersonId)?.Position.RegionId;
                var secondaryTransportCount = simulation.GroundTruthEvents.Count(item =>
                    item.Kind == EventKind.SecondaryTransported);
                var inferredFinalTick = Math.Max(
                    simulation.GroundTruthEvents
                        .Select(simulationEvent => simulationEvent.Tick)
                        .DefaultIfEmpty(0)
                        .Max(),
                    simulation.ObservationSet.Observations
                        .Select(observation => observation.AvailableTick)
                        .DefaultIfEmpty(0)
                        .Max());
                var finalTick = scenario.WeftEvaluation?.EvaluationEndTick ?? inferredFinalTick;
                var truth = new GroundTruthEvaluationContext
                {
                    ScenarioId = scenario.Id,
                    Seed = seed,
                    TargetPersonId = scenario.TargetPersonId,
                    TruePersonRegionId = truePersonRegion,
                    SearchableRegionIds = searchableRegionIds,
                    DeviceIds = scenario.Entities.Devices
                        .Select(device => device.Id)
                        .OrderBy(deviceId => deviceId, StringComparer.Ordinal)
                        .ToArray(),
                    TrueDeviceRegionIds = deviceFinalRegions,
                    SecondaryTransportEventCount = secondaryTransportCount,
                    WeftEvidenceCondition = simulation.WeftEvidenceCondition,
                    TruthOutcomeIdentifier = CreateTruthOutcomeIdentifier(
                        truePersonRegion,
                        deviceFinalRegions,
                        secondaryTransportCount)
                };

                foreach (var strategy in strategies)
                {
                    var timeline = EvaluateTimeline(
                        strategy,
                        scenario.Id,
                        seed,
                        finalTick,
                        publicContext,
                        simulation.ObservationSet,
                        simulation.CrossThreadConstraints);
                    telemetry?.AddEvaluation(scenario.TargetPersonId, timeline);
                    evaluations.Add(perRunEvaluator.Evaluate(
                        truth,
                        timeline[^1],
                        timeline,
                        simulation.ObservationSet));
                }
            }
        }

        var orderedEvaluations = evaluations
            .OrderBy(evaluation => evaluation.ScenarioId, StringComparer.Ordinal)
            .ThenBy(evaluation => evaluation.Seed)
            .ThenBy(evaluation => evaluation.StrategyId, StringComparer.Ordinal)
            .ToArray();
        var aggregate = new AggregateEvaluator().Aggregate(orderedEvaluations);
        var manifest = new EvaluationManifest
        {
            ScenarioIdentifiers = scenarios
                .Select(input => input.Scenario.Id)
                .ToArray(),
            Seeds = seeds,
            StrategyIdentifiers = strategies.Select(strategy => strategy.Id).ToArray(),
            RunCount = orderedEvaluations.Length,
            SourceCommitSha = request.SourceCommitSha,
            DirtyWorkingTree = request.DirtyWorkingTree,
            ScenarioInputs = scenarios.Select(input => new ScenarioManifestEntry
            {
                ScenarioId = input.Scenario.Id,
                ContentSha256 = input.ContentSha256
            }).ToArray()
        };

        return new BatchEvaluationResult
        {
            Runs = orderedEvaluations,
            Aggregate = aggregate,
            Manifest = manifest,
            Telemetry = telemetry?.Build(
                scenarios,
                seeds,
                strategies,
                orderedEvaluations.Length,
                request.SourceCommitSha,
                request.DirtyWorkingTree)
        };
    }

    private static string CreateTruthOutcomeIdentifier(
        string? personRegion,
        IReadOnlyDictionary<string, string> deviceRegions,
        int secondaryTransportCount)
    {
        return string.Join(
            ";",
            $"person={personRegion ?? "unavailable"}",
            $"devices={string.Join('|', deviceRegions.Select(item => $"{item.Key}={item.Value}"))}",
            $"secondary={secondaryTransportCount}");
    }

    private static IReadOnlyList<SearchStrategyResult> EvaluateTimeline(
        ISearchStrategy strategy,
        string scenarioId,
        int seed,
        long finalTick,
        RescueContext publicContext,
        ObservationSet completeEvidence,
        CrossThreadConstraintSet? completeCrossThreadConstraints)
    {
        var results = new List<SearchStrategyResult>();
        for (long tick = 0; tick <= finalTick; tick++)
        {
            var availableEvidence = new ObservationSet
            {
                ScenarioId = scenarioId,
                Seed = seed,
                Observations = completeEvidence.Observations
                    .Where(observation => observation.AvailableTick <= tick)
                    .ToArray()
            };
            results.Add(strategy.Evaluate(new SearchStrategyInput
            {
                ScenarioId = scenarioId,
                Seed = seed,
                CurrentTick = tick,
                PublicContext = publicContext,
                AvailableEvidence = availableEvidence,
                AvailableCrossThreadConstraints = completeCrossThreadConstraints?
                    .PubliclyAvailableAt(tick)
            }));
        }

        return results;
    }

    private static EvaluationScenarioInput[] NormalizeScenarios(
        IEnumerable<EvaluationScenarioInput> scenarios)
    {
        var ordered = scenarios
            .OrderBy(input => input.Scenario.Id, StringComparer.Ordinal)
            .ToArray();
        var duplicate = ordered
            .GroupBy(input => input.Scenario.Id, StringComparer.Ordinal)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicate is not null)
        {
            throw new ArgumentException($"Duplicate scenario identifier '{duplicate.Key}'.");
        }

        return ordered;
    }

    private static void ValidateRequest(
        BatchEvaluationRequest request,
        IReadOnlyCollection<EvaluationScenarioInput> scenarios,
        IReadOnlyCollection<int> seeds,
        IReadOnlyCollection<ISearchStrategy> strategies)
    {
        if (scenarios.Count == 0)
        {
            throw new ArgumentException("At least one scenario is required.");
        }

        if (seeds.Count == 0)
        {
            throw new ArgumentException("At least one seed is required.");
        }

        if (strategies.Count == 0)
        {
            throw new ArgumentException("At least one strategy is required.");
        }

        if (string.IsNullOrWhiteSpace(request.SourceCommitSha))
        {
            throw new ArgumentException("Source commit SHA is required.");
        }

        foreach (var scenario in scenarios)
        {
            ScenarioValidator.Validate(scenario.Scenario);
            if (string.IsNullOrWhiteSpace(scenario.ContentSha256))
            {
                throw new ArgumentException(
                    $"Scenario '{scenario.Scenario.Id}' content hash is required.");
            }
        }
    }
}
