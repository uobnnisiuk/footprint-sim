using System.Text.Json.Nodes;
using FootprintSim.Evidence;
using FootprintSim.Evaluation;
using FootprintSim.Hazards;
using FootprintSim.Inference;
using FootprintSim.Interactions;
using FootprintSim.Scenarios;
using FootprintSim.Serialization;
using FootprintSim.Simulation;
using Xunit;

namespace FootprintSim.Core.Tests;

// AR-101 (adversarial review of PR #186): the own-evidence ablation must not
// consume other-person encounter constraints through the hazard-aware
// fallback either. In the S3 variant whose own coarse signal never arrives
// (step target-to-encounter observation dropProbability = 1.0), the weft
// fallback is the only path own-evidence can take, so any cross-thread
// consumption there is directly observable.
public sealed class Ar101Tests
{
    [Fact]
    public void OwnEvidenceNeverAnchorsOnEncounterConstraintsWhenOwnObservationIsDropped()
    {
        var scenario = LoadS3WithoutOwnObservation();
        var result = RunBatch(scenario, Enumerable.Range(0, 20).ToArray());

        var own = result.Runs
            .Where(run => run.StrategyId == SearchStrategyIds.OwnEvidenceConstrained)
            .ToArray();
        var cross = result.Runs
            .Where(run => run.StrategyId == SearchStrategyIds.CrossThreadConstrained)
            .ToArray();
        var hazard = result.Runs
            .Where(run => run.StrategyId == SearchStrategyIds.HazardAware)
            .ToArray();

        Assert.Equal(20, own.Length);
        Assert.Equal(20, cross.Length);
        Assert.Equal(20, hazard.Length);

        // The ablation never fires the relational anchor: no own-evidence
        // run may carry the anchor rationale code.
        Assert.All(own, run => Assert.DoesNotContain(
            "encounter-interval-anchor",
            run.RationaleCodes));

        // With no own observation and no cross-thread input, own-evidence
        // falsely excludes in every seed, exactly as the pre-adoption
        // build (7ba906f) did under this variant.
        Assert.Equal(20, own.Count(run =>
            run.FalseExclusion.Available && run.FalseExclusion.Value == true));

        // The anchor still fires for the two strategies allowed to consume
        // it: 14 of 20 seeds carry a publicly received encounter constraint
        // (confirmed / supportedButUncertain / delayed / conflicting), and
        // false exclusions remain only in the 6 unavailable / deviceOnly
        // seeds.
        Assert.Equal(14, cross.Count(run =>
            run.RationaleCodes.Contains("encounter-interval-anchor")));
        Assert.Equal(14, hazard.Count(run =>
            run.RationaleCodes.Contains("encounter-interval-anchor")));
        Assert.Equal(6, cross.Count(run =>
            run.FalseExclusion.Available && run.FalseExclusion.Value == true));
        Assert.Equal(6, hazard.Count(run =>
            run.FalseExclusion.Available && run.FalseExclusion.Value == true));
    }

    [Fact]
    public void OwnEvidenceOutputIsIdenticalWithAndWithoutCrossThreadConstraints()
    {
        var scenario = LoadS3WithoutOwnObservation();
        var strategy = new OwnEvidenceConstrainedStrategy();

        // Seeds 0, 1, 2, 3 and 6 span the conditions whose encounter
        // constraint is publicly received, so the input actually carries a
        // constraint set the strategy could wrongly consume.
        foreach (var seed in new[] { 0, 1, 2, 3, 6 })
        {
            var run = new SimulationEngine().Run(scenario, seed);
            var input = Input(scenario, run, 30);
            Assert.NotNull(input.AvailableCrossThreadConstraints);
            Assert.NotEmpty(input.AvailableCrossThreadConstraints!.Constraints);

            var withConstraints = strategy.Evaluate(input);
            var withoutConstraints = strategy.Evaluate(
                input with { AvailableCrossThreadConstraints = null });

            Assert.Equal(
                JsonDefaults.Serialize(withConstraints),
                JsonDefaults.Serialize(withoutConstraints));
        }
    }

    private static ScenarioDefinition LoadS3WithoutOwnObservation()
    {
        var source = Path.Combine(
            RepositoryRoot, "scenarios", "s3_cross_thread_encounter", "scenario.json");
        var definition = JsonNode.Parse(File.ReadAllText(source))!;
        var step = Assert.Single(
            definition["steps"]!.AsArray(),
            node => node?["id"]?.GetValue<string>() == "target-to-encounter")!.AsObject();
        step["observation"]!["dropProbability"] = 1.0;
        var directory = TemporaryDirectory();
        var path = Path.Combine(directory, "scenario.json");
        File.WriteAllText(path, definition.ToJsonString());
        return ScenarioLoader.Load(path);
    }

    private static SearchStrategyInput Input(
        ScenarioDefinition scenario,
        SimulationRun run,
        long tick) => new()
        {
            ScenarioId = scenario.Id,
            Seed = run.Seed,
            CurrentTick = tick,
            PublicContext = new RescueContext
            {
                ScenarioId = scenario.Id,
                TargetPersonId = scenario.TargetPersonId,
                NormalWalkingRadius = scenario.NormalWalkingRadius,
                PublicRegions = scenario.Regions,
                DeclaredHazardEnvelopeRegionIds = scenario.HazardTransportEnvelopeRegionIds,
                PublicHazardEnvelope = PublicHazardEnvelopeBuilder.Create(scenario.StochasticHazard!),
                PublicDeviceIds = scenario.Entities.Devices.Select(device => device.Id).ToArray(),
                PublicCandidateWarp = new PublicCandidateWarpDefinition
                {
                    HistoricalTimeWindow = new InteractionTimeWindow(
                    scenario.WeftEvaluation!.HistoricalStartTick,
                    scenario.WeftEvaluation.HistoricalEndTick),
                    AvailableAtTick = scenario.WeftEvaluation.NormalAvailableAtTick,
                    HistoricalCandidateRegionIds = scenario.WeftEvaluation.HistoricalCandidateRegionIds
                }
            },
            AvailableEvidence = new ObservationSet
            {
                ScenarioId = scenario.Id,
                Seed = run.Seed,
                Observations = run.ObservationSet.Observations
                .Where(observation => observation.AvailableTick <= tick)
                .ToArray()
            },
            AvailableCrossThreadConstraints = run.CrossThreadConstraints!.PubliclyAvailableAt(tick)
        };

    private static BatchEvaluationResult RunBatch(
        ScenarioDefinition scenario,
        IReadOnlyList<int> seeds) =>
        new BatchEvaluationRunner().Run(new BatchEvaluationRequest
        {
            Scenarios = [new EvaluationScenarioInput
            {
                Scenario = scenario,
                ContentSha256 = "s3-drop-own-observation-fixture"
            }],
            Seeds = seeds,
            Strategies = SearchStrategyRegistry.All,
            SourceCommitSha = "fixture-commit",
            DirtyWorkingTree = true
        });

    private static string TemporaryDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), $"footprint-ar101-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }

    private static string RepositoryRoot
    {
        get
        {
            var current = new DirectoryInfo(AppContext.BaseDirectory);
            while (current is not null && !File.Exists(Path.Combine(current.FullName, "FootprintSim.sln")))
            {
                current = current.Parent;
            }

            Assert.NotNull(current);
            return current.FullName;
        }
    }
}
