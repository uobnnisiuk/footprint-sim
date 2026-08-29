using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using FootprintSim.Cli;
using FootprintSim.Evaluation;
using FootprintSim.Evaluation.Truth;
using FootprintSim.Events;
using FootprintSim.Hazards;
using FootprintSim.Inference;
using FootprintSim.Scenarios;
using FootprintSim.Serialization;
using FootprintSim.Simulation;
using Xunit;

namespace FootprintSim.Core.Tests;

public sealed class M1BTests
{
    [Fact]
    public void SplitMix64MatchesPublishedAlgorithmTestVector()
    {
        var random = new StableRandom(0UL);

        Assert.Equal(
            [
                0xe220a8397b1dcdafUL,
                0x6e789e6aa1b965f4UL,
                0x06c45d188009454fUL,
                0xf88bb8a8724c81ecUL,
                0x1b39896a51a8749bUL
            ],
            Enumerable.Range(0, 5).Select(_ => random.NextUInt64()));
    }

    [Fact]
    public void StableRandomIsSeedStableBoundedAndRejectsInvalidRanges()
    {
        var first = new StableRandom(9384UL);
        var second = new StableRandom(9384UL);
        var other = new StableRandom(9385UL);
        var firstValues = Enumerable.Range(0, 100).Select(_ => first.NextUInt64()).ToArray();

        Assert.Equal(firstValues, Enumerable.Range(0, 100).Select(_ => second.NextUInt64()));
        Assert.False(firstValues.SequenceEqual(
            Enumerable.Range(0, 100).Select(_ => other.NextUInt64())));

        var bounded = new StableRandom(12UL);
        Assert.All(
            Enumerable.Range(0, 10_000).Select(_ => bounded.NextInclusive(-7, 13)),
            value => Assert.InRange(value, -7, 13));
        var probabilities = new StableRandom(99UL);
        Assert.All(
            Enumerable.Range(0, 10_000).Select(_ => probabilities.NextDouble()),
            value => Assert.True(value >= 0 && value < 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => bounded.NextInclusive(4, 3));
    }

    [Fact]
    public void S2PropagationIsDeterministicButVariesAcrossSeeds()
    {
        var scenario = LoadS2();
        var propagator = new StochasticHazardPropagator();
        var first = propagator.Propagate(scenario, 42);
        var second = propagator.Propagate(scenario, 42);

        Assert.Equal(JsonDefaults.Serialize(first), JsonDefaults.Serialize(second));
        var outcomes = Enumerable.Range(0, 50)
            .Select(seed => new SimulationEngine().Run(scenario, seed))
            .Select(run => string.Join(
                '|',
                run.GroundTruthFinalEntityStates[scenario.TargetPersonId].Position.RegionId,
                run.GroundTruthFinalEntityStates[scenario.StochasticHazard!.DeviceId!].Position.RegionId))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        Assert.True(outcomes.Length > 1);
    }

    [Fact]
    public void S2TravelTicksCausalityTerminalsSecondaryTransportAndEntitySeparationAreValid()
    {
        var scenario = LoadS2();
        var transitions = scenario.StochasticHazard!.Transitions
            .ToDictionary(item => item.Id, StringComparer.Ordinal);
        var runs = Enumerable.Range(0, 200)
            .Select(seed => new SimulationEngine().Run(scenario, seed))
            .ToArray();

        foreach (var run in runs)
        {
            var eventIds = run.GroundTruthEvents.Select(item => item.Id).ToHashSet(StringComparer.Ordinal);
            foreach (var movement in run.GroundTruthEvents.Where(item =>
                item.Kind is EventKind.HazardTransported or EventKind.SecondaryTransported))
            {
                var transitionId = movement.Payload["transitionId"].GetString()!;
                var travelTicks = movement.Payload["travelTicks"].GetInt32();
                Assert.InRange(
                    travelTicks,
                    transitions[transitionId].MinimumTravelTicks,
                    transitions[transitionId].MaximumTravelTicks);
                Assert.NotNull(movement.CauseEventId);
                Assert.Contains(movement.CauseEventId!, eventIds);
                var arrivalWindow = scenario.StochasticHazard.Nodes.Single(item =>
                    item.RegionId == movement.RegionId);
                Assert.InRange(
                    movement.Tick,
                    arrivalWindow.MinimumArrivalTick,
                    arrivalWindow.MaximumArrivalTick);
            }

            Assert.Contains(run.GroundTruthEvents, item =>
                item.Kind is EventKind.Deposited or EventKind.Trapped &&
                item.Payload.ContainsKey("hazardId"));
            Assert.All(
                run.GroundTruthEvents.Where(item =>
                    item.Payload.ContainsKey("hazardId") &&
                    item.Kind != EventKind.HazardTransportStarted),
                item => Assert.Contains(item.CauseEventId!, eventIds));
        }

        Assert.Contains(runs, run => run.GroundTruthEvents.Any(item =>
            item.Kind == EventKind.SecondaryTransported));
        Assert.Contains(runs, run =>
            run.GroundTruthFinalEntityStates[scenario.TargetPersonId].Position.RegionId !=
            run.GroundTruthFinalEntityStates[scenario.StochasticHazard!.DeviceId!].Position.RegionId);
    }

    [Theory]
    [InlineData("weight", "-1", null, null)]
    [InlineData("destinationRegionId", "\"unknown-region\"", null, null)]
    [InlineData("minimumTravelTicks", "9", "maximumTravelTicks", "2")]
    public void InvalidHazardTransitionDefinitionsAreRejected(
        string property,
        string value,
        string? secondProperty = null,
        string? secondValue = null)
    {
        var root = JsonNode.Parse(File.ReadAllText(S2Path))!.AsObject();
        var transition = root["stochasticHazard"]!["transitions"]![0]!.AsObject();
        transition[property] = JsonNode.Parse(value);
        if (secondProperty is not null)
        {
            transition[secondProperty] = JsonNode.Parse(secondValue!);
        }

        var scenario = JsonSerializer.Deserialize<ScenarioDefinition>(
            root.ToJsonString(),
            JsonDefaults.Options)!;
        Assert.Throws<InvalidDataException>(() => ScenarioValidator.Validate(scenario));
    }

    [Fact]
    public void PublicEnvelopeIsSeedIndependentCompleteAndContainsNoRealizedSelections()
    {
        var scenario = LoadS2();
        var first = PublicHazardEnvelopeBuilder.Create(scenario.StochasticHazard!);
        var second = PublicHazardEnvelopeBuilder.Create(scenario.StochasticHazard!);
        var json = JsonDefaults.Serialize(first);

        Assert.Equal(JsonDefaults.Serialize(first), JsonDefaults.Serialize(second));
        Assert.Equal(
            scenario.StochasticHazard!.Transitions.Count(item => item.PubliclyVisible),
            first.Branches.Count);
        Assert.Equal(
            scenario.HazardTransportEnvelopeRegionIds.OrderBy(value => value, StringComparer.Ordinal),
            first.Regions.Select(item => item.RegionId));
        Assert.DoesNotContain("selectedTransition", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("randomDraw", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("trueDeposition", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void StrategiesReceiveOnlyPublicEnvelopeAndEmitDeterministicCandidateProvenance()
    {
        var scenario = LoadS2();
        var simulation = new SimulationEngine().Run(scenario, 11);
        var input = CreateInput(scenario, simulation, 11);

        foreach (var strategy in SearchStrategyRegistry.All)
        {
            var result = strategy.Evaluate(input);
            Assert.All(result.CandidateRegionIds, regionId => Assert.Contains(
                result.CandidateRegionProvenance,
                item => item.RegionId == regionId));
            Assert.Equal(
                result.CandidateRegionProvenance,
                strategy.Evaluate(input).CandidateRegionProvenance);
        }

        var evidenceAware = new EvidenceAwareCurrentStrategy().Evaluate(input);
        Assert.DoesNotContain("downstream-bank", evidenceAware.ConfirmedPersonRegionIds);
        Assert.Contains(evidenceAware.CandidateRegionProvenance, item =>
            item.SourceKind == CandidateSourceKind.DeviceOnlyObservation &&
            item.IsDeviceOnlyEvidence &&
            !item.IsPersonEvidence);

        var publicContextJson = JsonDefaults.Serialize(input.PublicContext);
        Assert.DoesNotContain("transitionId", publicContextJson, StringComparison.Ordinal);
        Assert.DoesNotContain("travelTicks", publicContextJson, StringComparison.Ordinal);
        Assert.DoesNotContain("random", publicContextJson, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void GuidancePersistenceMetricsMatchHandCalculatedTimeline()
    {
        var truth = new GroundTruthEvaluationContext
        {
            ScenarioId = "timeline",
            Seed = 7,
            TargetPersonId = "person",
            TruePersonRegionId = "b",
            SearchableRegionIds = ["a", "b", "c", "d"],
            TruthOutcomeIdentifier = "fixture"
        };
        var timeline = new[]
        {
            Result(0, ["a", "b", "c", "d"]),
            Result(1, ["a", "b"]),
            Result(2, ["b"]),
            Result(3, ["a"]),
            Result(4, ["b", "c"]),
            Result(5, ["b", "d"])
        };
        var evaluation = new PerRunEvaluator().Evaluate(
            truth,
            timeline[^1],
            timeline,
            new FootprintSim.Evidence.ObservationSet { ScenarioId = "timeline", Seed = 7 });

        Assert.Equal(1, evaluation.TimeToUsefulGuidance.Value);
        Assert.Equal(true, evaluation.FinalGuidanceUseful.Value);
        Assert.Equal(4, evaluation.UsefulGuidanceTickCount.Value);
        Assert.Equal(6, evaluation.EvaluatedTickCount.Value);
        Assert.Equal(4m / 6m, evaluation.UsefulGuidancePersistenceFraction.Value);
        Assert.Equal(2, evaluation.LongestUsefulGuidanceStreak.Value);
        Assert.Equal(true, evaluation.GuidanceRegressedAfterUseful.Value);
    }

    [Fact]
    public void UnavailableGuidancePersistenceDoesNotBecomeMeasuredZero()
    {
        var truth = new GroundTruthEvaluationContext
        {
            ScenarioId = "timeline",
            Seed = 7,
            TargetPersonId = "person",
            SearchableRegionIds = ["a"],
            TruthOutcomeIdentifier = "fixture"
        };
        var result = Result(0, []);
        var evaluation = new PerRunEvaluator().Evaluate(
            truth,
            result,
            [result],
            new FootprintSim.Evidence.ObservationSet { ScenarioId = "timeline", Seed = 7 });

        Assert.False(evaluation.FinalGuidanceUseful.Available);
        Assert.Null(evaluation.FinalGuidanceUseful.Value);
        Assert.False(evaluation.UsefulGuidanceTickCount.Available);
        Assert.Null(evaluation.UsefulGuidanceTickCount.Value);
        Assert.False(evaluation.UsefulGuidancePersistenceFraction.Available);
        Assert.Null(evaluation.UsefulGuidancePersistenceFraction.Value);
        Assert.True(evaluation.EvaluatedTickCount.Available);
        Assert.Equal(1, evaluation.EvaluatedTickCount.Value);
    }

    [Fact]
    public async Task S2SingleRunAndBatchOutputsAreByteIdenticalAtSchemaVersionTwo()
    {
        var scenario = LoadS2();
        var firstRun = new SimulationEngine().Run(scenario, 91);
        var secondRun = new SimulationEngine().Run(scenario, 91);
        Assert.Equal(JsonDefaults.Serialize(firstRun), JsonDefaults.Serialize(secondRun));

        var first = RunBatch(scenario, [2, 0, 1]);
        var second = RunBatch(scenario, [1, 2, 0]);
        var firstDirectory = TemporaryDirectory();
        var secondDirectory = TemporaryDirectory();
        try
        {
            var writer = new EvaluationOutputWriter();
            await writer.WriteAsync(firstDirectory, first);
            await writer.WriteAsync(secondDirectory, second);
            Assert.Equal(2, first.Manifest.OutputSchemaVersion);
            Assert.All(first.Runs, run => Assert.Equal(2, run.SchemaVersion));
            Assert.Equal(2, first.Aggregate.SchemaVersion);
            foreach (var outputName in EvaluationOutputNames.All)
            {
                Assert.Equal(
                    File.ReadAllBytes(Path.Combine(firstDirectory, outputName)),
                    File.ReadAllBytes(Path.Combine(secondDirectory, outputName)));
            }
        }
        finally
        {
            Directory.Delete(firstDirectory, recursive: true);
            Directory.Delete(secondDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task M0SingleRunFilesRetainBaselineBytes()
    {
        var expected = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["s0/ground-truth.events.json"] = "30453d88aaa70854b36d5eeafe2ca8ce469a12fdb8f4be41ee5e5ef11c04ad5e",
            ["s0/observations.json"] = "7bc4efde4ca29bd779ffea15ab1c3217b258d175e7fc0b0dbd27969415b04f72",
            ["s0/run.metadata.json"] = "380d447a67dcb5b90ad2456b86374ac6004a91d8ae94fbdb758a5ae369f84640",
            ["s0/search-zone.summary.json"] = "9da83e04a13ff260290455ef549373dd91788674b9bda9fad167353017b4de6d",
            ["s1/ground-truth.events.json"] = "8524ccb723dfd470d68da0aa31a78143ff14538e7cd13769ef4e409454a72f62",
            ["s1/observations.json"] = "06956fd9dba75ede0901936982de5475d24c81ca0845cec3f222f9edc22afd6a",
            ["s1/run.metadata.json"] = "9533ff477c2b1398f986a2ad0cfd42b85d8603307c976e805fb4501b194b73ee",
            ["s1/search-zone.summary.json"] = "434f5e24220485b792f990f60154dcd817ac10b12168e348c3e7cea64299e476"
        };
        var directory = TemporaryDirectory();
        try
        {
            foreach (var scenario in new[] { "s0", "s1" })
            {
                var scenarioDirectory = scenario == "s0"
                    ? "s0_walking_baseline"
                    : "s1_debris_transport";
                Assert.Equal(0, await CliApplication.RunAsync(
                [
                    "--scenario", ScenarioPath(scenarioDirectory),
                    "--seed", "42",
                    "--output", Path.Combine(directory, scenario)
                ]));
            }

            foreach (var item in expected)
            {
                var actual = Convert.ToHexString(SHA256.HashData(
                    File.ReadAllBytes(Path.Combine(directory, item.Key))))
                    .ToLowerInvariant();
                Assert.Equal(item.Value, actual);
            }
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static SearchStrategyInput CreateInput(
        ScenarioDefinition scenario,
        SimulationRun simulation,
        int seed)
    {
        var tick = Math.Max(
            simulation.GroundTruthEvents.Max(item => item.Tick),
            simulation.ObservationSet.Observations.Max(item => item.AvailableTick));
        return new SearchStrategyInput
        {
            ScenarioId = scenario.Id,
            Seed = seed,
            CurrentTick = tick,
            PublicContext = new RescueContext
            {
                ScenarioId = scenario.Id,
                TargetPersonId = scenario.TargetPersonId,
                NormalWalkingRadius = scenario.NormalWalkingRadius,
                PublicRegions = scenario.Regions,
                DeclaredHazardEnvelopeRegionIds = scenario.HazardTransportEnvelopeRegionIds,
                PublicHazardEnvelope = PublicHazardEnvelopeBuilder.Create(scenario.StochasticHazard!),
                PublicDeviceIds = scenario.Entities.Devices.Select(device => device.Id).ToArray()
            },
            AvailableEvidence = simulation.ObservationSet
        };
    }

    private static SearchStrategyResult Result(long tick, IReadOnlyList<string> candidates) => new()
    {
        StrategyId = "fixture",
        ScenarioId = "timeline",
        Seed = 7,
        ProducedAtTick = tick,
        CandidateRegionIds = candidates,
        EvidenceBoundaryState = FootprintSim.Evidence.EvidenceState.SupportedButUncertain,
        RationaleCodes = ["fixture"],
        CandidateRegionProvenance = candidates.Select(regionId =>
            new CandidateRegionProvenance
            {
                RegionId = regionId,
                SourceKind = CandidateSourceKind.Unsupported,
                SourceIdentifier = "fixture",
                AvailableAtTick = tick,
                IsPersonEvidence = false,
                IsDeviceOnlyEvidence = false,
                EvidenceBoundaryState = FootprintSim.Evidence.EvidenceState.UnsupportedForQuery
            }).ToArray()
    };

    private static BatchEvaluationResult RunBatch(
        ScenarioDefinition scenario,
        IReadOnlyList<int> seeds)
    {
        return new BatchEvaluationRunner().Run(new BatchEvaluationRequest
        {
            Scenarios =
            [
                new EvaluationScenarioInput
                {
                    Scenario = scenario,
                    ContentSha256 = "fixture-hash"
                }
            ],
            Seeds = seeds,
            Strategies = SearchStrategyRegistry.All,
            SourceCommitSha = "fixture-commit",
            DirtyWorkingTree = true
        });
    }

    private static string TemporaryDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), $"footprint-m1b-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }

    private static ScenarioDefinition LoadS2() => ScenarioLoader.Load(S2Path);

    private static string S2Path => ScenarioPath("s2_branching_debris_transport");

    private static string ScenarioPath(string directory) =>
        Path.Combine(RepositoryRoot, "scenarios", directory, "scenario.json");

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
