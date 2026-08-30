using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FootprintSim.Cli;
using FootprintSim.Evidence;
using FootprintSim.Evaluation;
using FootprintSim.Evaluation.Truth;
using FootprintSim.Inference;
using FootprintSim.Interactions;
using FootprintSim.Scenarios;
using FootprintSim.Serialization;
using FootprintSim.Simulation;
using Xunit;

namespace FootprintSim.Core.Tests;

public sealed class EvaluationHarnessTests
{
    [Fact]
    public void StrategyContractExposesOnlyPublicContextEvidenceClockAndRunIdentity()
    {
        var properties = typeof(SearchStrategyInput).GetProperties()
            .ToDictionary(property => property.Name, property => property.PropertyType);

        Assert.Equal(6, properties.Count);
        Assert.Equal(typeof(string), properties[nameof(SearchStrategyInput.ScenarioId)]);
        Assert.Equal(typeof(int), properties[nameof(SearchStrategyInput.Seed)]);
        Assert.Equal(typeof(long), properties[nameof(SearchStrategyInput.CurrentTick)]);
        Assert.Equal(typeof(RescueContext), properties[nameof(SearchStrategyInput.PublicContext)]);
        Assert.Equal(typeof(ObservationSet), properties[nameof(SearchStrategyInput.AvailableEvidence)]);
        Assert.Equal(
            typeof(CrossThreadConstraintSet),
            properties[nameof(SearchStrategyInput.AvailableCrossThreadConstraints)]);

        var evaluate = typeof(ISearchStrategy).GetMethod(nameof(ISearchStrategy.Evaluate));
        Assert.NotNull(evaluate);
        Assert.Equal(
            [typeof(SearchStrategyInput)],
            evaluate.GetParameters().Select(parameter => parameter.ParameterType));
        Assert.Equal(typeof(SearchStrategyResult), evaluate.ReturnType);

        Assert.DoesNotContain(properties.Values, type =>
            type == typeof(ScenarioDefinition) ||
            type == typeof(SimulationRun) ||
            type.Namespace == "FootprintSim.Events" ||
            type.Namespace == "FootprintSim.Evaluation.Truth");
    }

    [Fact]
    public void AllSixStrategiesRunThroughOneContract()
    {
        var scenario = LoadScenario("s1_debris_transport");
        var simulation = new SimulationEngine().Run(scenario, 42);
        var input = CreateFinalInput(scenario, simulation, 42);
        var publicRegionIds = scenario.Regions.Select(region => region.Id).ToHashSet();

        Assert.Equal(6, SearchStrategyRegistry.All.Count);
        foreach (var strategy in SearchStrategyRegistry.All)
        {
            var result = strategy.Evaluate(input);

            Assert.Equal(strategy.Id, result.StrategyId);
            Assert.Equal(scenario.Id, result.ScenarioId);
            Assert.Equal(42, result.Seed);
            Assert.Equal(input.CurrentTick, result.ProducedAtTick);
            Assert.NotEmpty(result.RationaleCodes);
            Assert.All(result.CandidateRegionIds, candidate => Assert.Contains(candidate, publicRegionIds));
        }
    }

    [Fact]
    public void S1VoluntaryMissesDepositionWhileHazardAwareContainsIt()
    {
        var scenario = LoadScenario("s1_debris_transport");
        var simulation = new SimulationEngine().Run(scenario, 42);
        var input = CreateFinalInput(scenario, simulation, 42);

        var voluntary = new VoluntaryReachabilityStrategy().Evaluate(input);
        var hazard = new HazardAwareStrategy().Evaluate(input);

        Assert.DoesNotContain("deposition-zone", voluntary.CandidateRegionIds);
        Assert.Contains("deposition-zone", hazard.CandidateRegionIds);
    }

    [Fact]
    public void S1DeviceOnlyRegionIsNeverPromotedToConfirmedPersonLocation()
    {
        var result = RunBatch(["s1_debris_transport"], [42]);

        Assert.All(result.Runs, run =>
        {
            Assert.DoesNotContain("downstream-bank", run.ConfirmedPersonRegionIds);
            Assert.True(run.DeviceOnlyPromotionViolation.Available);
            Assert.Equal(0, run.DeviceOnlyPromotionViolation.Value);
        });
    }

    [Fact]
    public void S1EvidenceAwareSeparatesFirstUsefulGuidanceFromFinalUsefulness()
    {
        var result = RunBatch(["s1_debris_transport"], [42]);
        var evidenceAware = Assert.Single(result.Runs, run =>
            run.StrategyId == SearchStrategyIds.EvidenceAwareCurrent);

        Assert.True(evidenceAware.TimeToUsefulGuidance.Available);
        Assert.Equal(0, evidenceAware.TimeToUsefulGuidance.Value);
        Assert.Equal(6, evidenceAware.CandidateRegionCount.Value);
        Assert.Equal(0m, evidenceAware.SearchSpaceReduction.Value);
        Assert.Equal(false, evidenceAware.FinalGuidanceUseful.Value);
        Assert.Equal(true, evidenceAware.GuidanceRegressedAfterUseful.Value);
    }

    [Fact]
    public void HandCalculatedPerRunMetricsAndTimeToGuidanceMatch()
    {
        var truth = Truth("region-b", ["region-a", "region-b", "region-c", "region-d"]);
        var allRegions = Result(0, ["region-a", "region-b", "region-c", "region-d"]);
        var useful = Result(5, ["region-a", "region-b"]);
        var final = Result(10, ["region-a", "region-b"]);

        var evaluation = new PerRunEvaluator().Evaluate(
            truth,
            final,
            [allRegions, useful, final],
            EmptyEvidence());

        Assert.Equal(new BooleanMetric
        {
            Available = true,
            Value = true,
            Numerator = 1,
            Denominator = 1
        }, evaluation.GroundTruthContainment);
        Assert.Equal(2, evaluation.CandidateRegionCount.Value);
        Assert.Equal(0.5m, evaluation.SearchSpaceFraction.Value);
        Assert.Equal(2, evaluation.SearchSpaceFraction.Numerator);
        Assert.Equal(4, evaluation.SearchSpaceFraction.Denominator);
        Assert.Equal(0.5m, evaluation.SearchSpaceReduction.Value);
        Assert.Equal(false, evaluation.FalseExclusion.Value);
        Assert.Equal(false, evaluation.ExplicitFalseExclusion.Value);
        Assert.True(evaluation.TimeToUsefulGuidance.Available);
        Assert.Equal(5, evaluation.TimeToUsefulGuidance.Value);
    }

    [Fact]
    public void UnavailableMetricsRemainNullRatherThanBecomingZero()
    {
        var truth = Truth(null, ["region-a"]);
        var final = Result(4, []);

        var evaluation = new PerRunEvaluator().Evaluate(
            truth,
            final,
            [final],
            EmptyEvidence());

        Assert.False(evaluation.GroundTruthContainment.Available);
        Assert.Null(evaluation.GroundTruthContainment.Value);
        Assert.Null(evaluation.GroundTruthContainment.Numerator);
        Assert.Null(evaluation.GroundTruthContainment.Denominator);
        Assert.False(evaluation.TimeToUsefulGuidance.Available);
        Assert.Null(evaluation.TimeToUsefulGuidance.Value);
        Assert.Equal("truth-region-unavailable-or-not-searchable", evaluation.TimeToUsefulGuidance.UnavailableReason);

        using var json = JsonDocument.Parse(JsonDefaults.Serialize(evaluation));
        Assert.Equal(
            JsonValueKind.Null,
            json.RootElement.GetProperty("timeToUsefulGuidance").GetProperty("value").ValueKind);
    }

    [Fact]
    public void SearchableRegionDenominatorIncludesEveryDeclaredScenarioRegion()
    {
        var result = RunBatch(["s1_debris_transport"], [0]);

        Assert.All(result.Runs, run => Assert.Equal(6, run.SearchableRegionCount));
        var hazard = Assert.Single(result.Runs, run => run.StrategyId == SearchStrategyIds.HazardAware);
        Assert.Equal(4, hazard.SearchSpaceFraction.Numerator);
        Assert.Equal(6, hazard.SearchSpaceFraction.Denominator);
        Assert.Equal(4m / 6m, hazard.SearchSpaceFraction.Value);
        Assert.All(result.Aggregate.Groups, group => Assert.Equal(
            "seed sensitivity unavailable: fewer than two distinct seeds",
            group.SeedSensitivityNote));
    }

    [Fact]
    public void EvaluatorDetectsDeviceOnlyPromotionWhenAResultActuallyClaimsIt()
    {
        var evidence = new ObservationSet
        {
            ScenarioId = "fixture",
            Seed = 7,
            Observations =
            [
                new ObservationRecord
                {
                    ObservationId = "device-fix",
                    ObservedTick = 1,
                    AvailableTick = 1,
                    EvidenceType = "device-fix",
                    SubjectIds = ["device-1"],
                    ReportedRegionId = "region-c",
                    State = EvidenceState.SupportedButUncertain
                }
            ]
        };
        var final = Result(2, ["region-c"]) with
        {
            ConfirmedPersonRegionIds = ["region-c"]
        };

        var evaluation = new PerRunEvaluator().Evaluate(
            Truth("region-a", ["region-a", "region-c"]),
            final,
            [final],
            evidence);

        Assert.Equal(1, evaluation.DeviceOnlyPromotionViolation.Value);
    }

    [Fact]
    public void AggregatePreservesNumeratorsDenominatorsAndRates()
    {
        var result = RunBatch(["s1_debris_transport"], [2, 0, 1]);
        var voluntary = Assert.Single(result.Aggregate.Groups, group =>
            group.StrategyId == SearchStrategyIds.VoluntaryReachability);
        var hazard = Assert.Single(result.Aggregate.Groups, group =>
            group.StrategyId == SearchStrategyIds.HazardAware);

        Assert.Equal(3, voluntary.GroundTruthContainment.AvailableCount);
        Assert.Equal(0, voluntary.GroundTruthContainment.Numerator);
        Assert.Equal(3, voluntary.GroundTruthContainment.Denominator);
        Assert.Equal(0m, voluntary.GroundTruthContainment.Rate);
        Assert.Equal(3, voluntary.FalseExclusion.Numerator);
        Assert.Equal(1m, voluntary.FalseExclusion.Rate);

        Assert.Equal(3, hazard.GroundTruthContainment.Numerator);
        Assert.Equal(3, hazard.GroundTruthContainment.Denominator);
        Assert.Equal(1m, hazard.GroundTruthContainment.Rate);
        Assert.Equal(0, hazard.DeviceOnlyPromotionViolationCount);
    }

    [Fact]
    public void AggregateDistributionsMatchHandCalculatedValuesAndIgnoreUnavailableTime()
    {
        var searchable = new[] { "region-a", "region-b", "region-c", "region-d" };
        var evaluations = new[]
        {
            EvaluateFixtureRun(1, 1, ["region-a"], searchable),
            EvaluateFixtureRun(2, 2, ["region-a", "region-b"], searchable),
            EvaluateFixtureRun(3, 3, searchable, searchable)
        };

        var group = Assert.Single(new AggregateEvaluator().Aggregate(evaluations).Groups);

        Assert.Equal(3, group.CandidateRegionCount.AvailableCount);
        Assert.Equal(7m / 3m, group.CandidateRegionCount.Mean);
        Assert.Equal(2m, group.CandidateRegionCount.Median);
        Assert.Equal(1m, group.CandidateRegionCount.Minimum);
        Assert.Equal(4m, group.CandidateRegionCount.Maximum);

        Assert.Equal(7m / 12m, group.SearchSpaceFraction.Mean);
        Assert.Equal(0.5m, group.SearchSpaceFraction.Median);
        Assert.Equal(0.25m, group.SearchSpaceFraction.Minimum);
        Assert.Equal(1m, group.SearchSpaceFraction.Maximum);

        Assert.Equal(5m / 12m, group.SearchSpaceReduction.Mean);
        Assert.Equal(0.5m, group.SearchSpaceReduction.Median);
        Assert.Equal(0m, group.SearchSpaceReduction.Minimum);
        Assert.Equal(0.75m, group.SearchSpaceReduction.Maximum);

        Assert.Equal(2, group.TimeToUsefulGuidance.AvailableCount);
        Assert.Equal(1.5m, group.TimeToUsefulGuidance.Mean);
        Assert.Equal(1.5m, group.TimeToUsefulGuidance.Median);
        Assert.Equal(1m, group.TimeToUsefulGuidance.Minimum);
        Assert.Equal(2m, group.TimeToUsefulGuidance.Maximum);
    }

    [Fact]
    public async Task SameBatchProducesByteIdenticalStableJsonAndCsvOutputs()
    {
        var firstDirectory = CreateTemporaryDirectory();
        var secondDirectory = CreateTemporaryDirectory();
        try
        {
            var first = RunBatch(
                ["s1_debris_transport", "s0_walking_baseline"],
                [2, 0, 1],
                SearchStrategyRegistry.All.Reverse().ToArray());
            var second = RunBatch(
                ["s0_walking_baseline", "s1_debris_transport"],
                [1, 2, 0],
                SearchStrategyRegistry.All);
            var writer = new EvaluationOutputWriter();
            await writer.WriteAsync(firstDirectory, first);
            await writer.WriteAsync(secondDirectory, second);

            foreach (var name in EvaluationOutputNames.All)
            {
                Assert.Equal(
                    File.ReadAllBytes(Path.Combine(firstDirectory, name)),
                    File.ReadAllBytes(Path.Combine(secondDirectory, name)));
            }

            var expectedOrder = first.Runs
                .OrderBy(run => run.ScenarioId, StringComparer.Ordinal)
                .ThenBy(run => run.Seed)
                .ThenBy(run => run.StrategyId, StringComparer.Ordinal)
                .Select(run => $"{run.ScenarioId}|{run.Seed}|{run.StrategyId}");
            Assert.Equal(
                expectedOrder,
                first.Runs.Select(run => $"{run.ScenarioId}|{run.Seed}|{run.StrategyId}"));

            var jsonLines = File.ReadAllLines(Path.Combine(firstDirectory, EvaluationOutputNames.RunsJsonLines));
            Assert.Equal(first.Manifest.RunCount, jsonLines.Length);
            Assert.All(jsonLines, line =>
            {
                using var document = JsonDocument.Parse(line);
                Assert.Equal(JsonValueKind.Object, document.RootElement.ValueKind);
            });

            AssertCsvShape(
                Path.Combine(firstDirectory, EvaluationOutputNames.RunsCsv),
                EvaluationOutputWriter.RunsCsvHeader,
                first.Manifest.RunCount);
            AssertCsvShape(
                Path.Combine(firstDirectory, EvaluationOutputNames.AggregateCsv),
                EvaluationOutputWriter.AggregateCsvHeader,
                first.Aggregate.Groups.Count);
            Assert.Contains("0.6666666666666666666666666667", File.ReadAllText(
                Path.Combine(firstDirectory, EvaluationOutputNames.AggregateCsv)));
        }
        finally
        {
            Directory.Delete(firstDirectory, recursive: true);
            Directory.Delete(secondDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task InvalidCliInputsReturnNonZeroExitCodes()
    {
        var scenarios = Path.Combine(RepositoryRoot, "scenarios");
        var output = Path.Combine(Path.GetTempPath(), $"footprint-cli-test-{Guid.NewGuid():N}");

        var invalidRange = await CliApplication.RunAsync(
            ["evaluate", "--scenarios", scenarios, "--seeds", "3..1", "--strategies", "all", "--output", output]);
        var unknownStrategy = await CliApplication.RunAsync(
            ["evaluate", "--scenarios", scenarios, "--seeds", "0", "--strategies", "unknown", "--output", output]);
        var unknownScenario = await CliApplication.RunAsync(
            ["evaluate", "--scenarios", output, "--seeds", "0", "--strategies", "all", "--output", output]);

        Assert.NotEqual(0, invalidRange);
        Assert.NotEqual(0, unknownStrategy);
        Assert.NotEqual(0, unknownScenario);
        Assert.False(Directory.Exists(output));
    }

    [Fact]
    public void SeedParserAcceptsSinglesRangesAndListsInStableOrder()
    {
        Assert.Equal([-2, -1, 0, 2, 4, 5], SeedExpressionParser.Parse("4..5,-2..0,2,4"));
    }

    private static BatchEvaluationResult RunBatch(
        IReadOnlyList<string> scenarioDirectories,
        IReadOnlyList<int> seeds,
        IReadOnlyList<ISearchStrategy>? strategies = null)
    {
        var scenarios = scenarioDirectories.Select(directory =>
        {
            var path = ScenarioPath(directory);
            return new EvaluationScenarioInput
            {
                Scenario = ScenarioLoader.Load(path),
                ContentSha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)))
                    .ToLowerInvariant()
            };
        }).ToArray();

        return new BatchEvaluationRunner().Run(new BatchEvaluationRequest
        {
            Scenarios = scenarios,
            Seeds = seeds,
            Strategies = strategies ?? SearchStrategyRegistry.All,
            SourceCommitSha = "0123456789abcdef",
            DirtyWorkingTree = true
        });
    }

    private static SearchStrategyInput CreateFinalInput(
        ScenarioDefinition scenario,
        SimulationRun simulation,
        int seed)
    {
        var tick = simulation.GroundTruthEvents.Max(item => item.Tick);
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
                DeclaredHazardEnvelopeRegionIds = scenario.HazardTransportEnvelopeRegionIds
            },
            AvailableEvidence = simulation.ObservationSet with
            {
                Observations = simulation.ObservationSet.Observations
                    .Where(observation => observation.AvailableTick <= tick)
                    .ToArray()
            }
        };
    }

    private static PerRunEvaluation EvaluateFixtureRun(
        int seed,
        long tick,
        IReadOnlyList<string> candidates,
        IReadOnlyList<string> searchable)
    {
        var truth = Truth("region-a", searchable) with { Seed = seed };
        var result = Result(tick, candidates) with { Seed = seed };
        var evidence = EmptyEvidence() with { Seed = seed };
        return new PerRunEvaluator().Evaluate(truth, result, [result], evidence);
    }

    private static GroundTruthEvaluationContext Truth(
        string? trueRegion,
        IReadOnlyList<string> searchableRegions)
    {
        return new GroundTruthEvaluationContext
        {
            ScenarioId = "fixture",
            Seed = 7,
            TargetPersonId = "person-1",
            TruePersonRegionId = trueRegion,
            SearchableRegionIds = searchableRegions,
            DeviceIds = ["device-1"]
        };
    }

    private static SearchStrategyResult Result(long tick, IReadOnlyList<string> candidates)
    {
        return new SearchStrategyResult
        {
            StrategyId = "fixture-strategy",
            ScenarioId = "fixture",
            Seed = 7,
            ProducedAtTick = tick,
            CandidateRegionIds = candidates,
            EvidenceBoundaryState = EvidenceState.SupportedButUncertain,
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
                    EvidenceBoundaryState = EvidenceState.UnsupportedForQuery
                }).ToArray()
        };
    }

    private static ObservationSet EmptyEvidence()
    {
        return new ObservationSet
        {
            ScenarioId = "fixture",
            Seed = 7
        };
    }

    private static void AssertCsvShape(string path, string expectedHeader, int expectedDataRows)
    {
        var lines = File.ReadAllLines(path);
        Assert.Equal(expectedDataRows + 1, lines.Length);
        Assert.Equal(expectedHeader, lines[0]);
        var columnCount = ParseCsvLine(lines[0]).Count;
        Assert.All(lines.Skip(1), line => Assert.Equal(columnCount, ParseCsvLine(line).Count));
    }

    private static IReadOnlyList<string> ParseCsvLine(string line)
    {
        var values = new List<string>();
        var value = new StringBuilder();
        var quoted = false;
        for (var index = 0; index < line.Length; index++)
        {
            var current = line[index];
            if (current == '"')
            {
                if (quoted && index + 1 < line.Length && line[index + 1] == '"')
                {
                    value.Append('"');
                    index++;
                }
                else
                {
                    quoted = !quoted;
                }
            }
            else if (current == ',' && !quoted)
            {
                values.Add(value.ToString());
                value.Clear();
            }
            else
            {
                value.Append(current);
            }
        }

        Assert.False(quoted);
        values.Add(value.ToString());
        return values;
    }

    private static string CreateTemporaryDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), $"footprint-evaluation-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }

    private static ScenarioDefinition LoadScenario(string directory) =>
        ScenarioLoader.Load(ScenarioPath(directory));

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
