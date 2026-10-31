using System.Text.Json;
using System.Text.Json.Nodes;
using FootprintSim.Hazards;
using FootprintSim.Inference.PastStates;
using FootprintSim.Inference.SearchProjection;
using FootprintSim.Interactions;
using FootprintSim.Scenarios;
using FootprintSim.Serialization;
using Xunit;

namespace FootprintSim.Core.Tests;

public sealed class FinitePublicTransitionTests
{
    private static readonly string[] Nodes = ["a", "b", "c"];
    private static readonly string[] Transit = ["ab", "ac", "bc"];
    private static readonly (string From, string To, string Transit)[] Links =
        [("a", "b", "ab"), ("a", "c", "ac"), ("b", "c", "bc")];

    [Fact]
    public void All1944RegisteredQueriesMatchIndependentPathsAndF20UnitSupport()
    {
        var queries = 0;
        for (var graph = 0; graph < 27; graph++)
        {
            var model = Model(graph);
            foreach (var node in Nodes)
            {
                for (var start = 0; start <= 2; start++)
                {
                    for (var end = start; end <= 2; end++)
                    {
                        for (var target = 3; target <= 6; target++)
                        {
                            var row = Produce(model, node, start, end, target);
                            var expected = Oracle(model.Edges, node, start, end, target);
                            Assert.True(row.IsComplete);
                            Assert.Equal(expected, row.CurrentRegionIds);
                            Assert.NotEmpty(expected);
                            var input = Input(row);
                            var result = PublicSearchProjector.Project(input);
                            var expectedUnits = expected.SelectMany(Units).OrderBy(id => id, StringComparer.Ordinal).ToArray();
                            Assert.Equal(expectedUnits, result.KnownUnitIds);
                            Assert.Empty(result.UnresolvedCandidateIds);
                            Assert.Equal("candidate", Assert.Single(result.Candidates).SourceCandidate.CandidateId);
                            var all = Assert.Single(result.Actions, item => item.ActionId == "all");
                            Assert.Equal(new[] { "candidate" }, all.WholeSupportContainedCandidateIds);
                            var partial = Assert.Single(result.Actions, item => item.ActionId == "partial");
                            Assert.Equal(expectedUnits.Contains("u-a") ? new[] { "candidate" } : [],
                                partial.KnownIntersectingCandidateIds);
                            Assert.Equal(expectedUnits.All(id => id == "u-a") ? new[] { "candidate" } : [],
                                partial.WholeSupportContainedCandidateIds);
                            queries++;
                        }
                    }
                }
            }
        }

        Assert.Equal(1944, queries);
    }

    [Fact]
    public void WindowAndTransitMutantsAreRefutedAndTerminalControlIsUseful()
    {
        var model = Model(2); // a->b duration two; no other edges.
        var full = Produce(model, "a", 0, 1, 2);
        var endpointOnly = Produce(model, "a", 1, 1, 2);
        Assert.Equal(new[] { "a", "ab", "b" }, full.CurrentRegionIds);
        Assert.Equal(new[] { "a", "ab" }, endpointOnly.CurrentRegionIds);
        Assert.Contains("b", full.CurrentRegionIds.Except(endpointOnly.CurrentRegionIds));
        var traveling = Produce(model, "a", 0, 0, 1);
        Assert.Equal(new[] { "a", "ab" }, traveling.CurrentRegionIds);
        Assert.Equal(new[] { "a", "b" }, Produce(Model(1), "a", 0, 0, 1).CurrentRegionIds);
        Assert.Equal(new[] { "c" }, Produce(model, "c", 0, 2, 6).CurrentRegionIds);
    }

    [Theory]
    [InlineData("closure")]
    [InlineData("waiting")]
    [InlineData("window")]
    [InlineData("missing-map")]
    [InlineData("partial-map")]
    public void MissingAssurancesOrMapsCannotCertifyWholeSupport(string mode)
    {
        var model = Model(2) with
        {
            IsClosed = mode != "closure",
            WaitingAtEveryNodeDeclared = mode != "waiting"
        };
        var row = FinitePublicTransitionProducer.Create(
            model, "person", "a", 0, 1, 2, 1, mode != "window");
        var input = Input(row);
        if (mode == "missing-map")
        {
            input = input with { UnitMappings = input.UnitMappings.Where(map => map.RegionId != "ab").ToArray() };
        }
        else if (mode == "partial-map")
        {
            input = input with
            {
                UnitMappings = input.UnitMappings.Select(map => map.RegionId == "ab"
                    ? map with { IsComplete = false } : map).ToArray()
            };
        }
        else
        {
            Assert.False(row.IsComplete);
            Assert.Empty(row.CurrentRegionIds);
        }

        var result = PublicSearchProjector.Project(input);
        Assert.Equal(new[] { "candidate" }, result.UnresolvedCandidateIds);
        Assert.All(result.Actions, action => Assert.Empty(action.WholeSupportContainedCandidateIds));
    }

    [Theory]
    [InlineData("future")]
    [InlineData("entity")]
    [InlineData("region")]
    [InlineData("window")]
    [InlineData("validity")]
    [InlineData("cutoff")]
    [InlineData("duration")]
    [InlineData("duplicate")]
    [InlineData("unknown-transit")]
    [InlineData("source")]
    public void UnsupportedOrUnavailableContractsAreRejected(string mode)
    {
        var model = Model(2);
        model = mode switch
        {
            "future" => model with { AvailableTick = 2 },
            "validity" => model with { ValidThroughTick = 2 },
            "duration" => model with { Edges = [model.Edges[0] with { Duration = 0 }] },
            "duplicate" => model with { Edges = [model.Edges[0], model.Edges[0]] },
            "unknown-transit" => model with { TransitRegionIds = [] },
            "source" => model with { SourceId = " " },
            _ => model
        };
        Assert.Throws<InvalidDataException>(() => FinitePublicTransitionProducer.Create(
            model, mode == "entity" ? "device" : "person", mode == "region" ? "missing" : "a",
            0, mode == "window" ? -1 : 1, 3, mode == "cutoff" ? 4 : 1, true));
    }

    [Fact]
    public async Task GeneratedDeclarationFlowsThroughExistingCliDeterministically()
    {
        var model = Model(26);
        var before = JsonDefaults.Serialize(model);
        var row = Produce(model, "a", 0, 1, 3);
        var expectedRegions = Oracle(model.Edges, "a", 0, 1, 3);
        var expectedUnits = expectedRegions.SelectMany(Units).OrderBy(id => id, StringComparer.Ordinal).ToArray();
        var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            byte[]? first = null;
            for (var iteration = 0; iteration < 3; iteration++)
            {
                var supplied = iteration == 2 ? model with
                {
                    NodeIds = model.NodeIds.Reverse().ToArray(),
                    TransitRegionIds = model.TransitRegionIds.Reverse().ToArray(),
                    Edges = model.Edges.Reverse().ToArray()
                } : model;
                var input = Input(Produce(supplied, "a", 0, 1, 3));
                var inputPath = Path.Combine(directory, "input.json");
                var outputPath = Path.Combine(directory, "report.json");
                await File.WriteAllTextAsync(inputPath, JsonDefaults.Serialize(input));
                Assert.Equal(0, await FootprintSim.Cli.CliApplication.RunAsync(
                    ["evaluate-public-search-projection", "--input", inputPath, "--output", outputPath]));
                var bytes = await File.ReadAllBytesAsync(outputPath);
                var report = JsonSerializer.Deserialize<PublicSearchProjectionResult>(bytes, JsonDefaults.Options);
                Assert.NotNull(report);
                Assert.Equal(expectedRegions, report.KnownCurrentRegionIds);
                Assert.Equal(expectedUnits, report.KnownUnitIds);
                Assert.Empty(report.UnresolvedCandidateIds);
                if (first is not null)
                {
                    Assert.Equal(first, bytes);
                }

                first = bytes;
            }

            Assert.Equal(before, JsonDefaults.Serialize(model));
            var mutableEdges = model.Edges.ToList();
            var snapshot = Produce(model with { Edges = mutableEdges }, "a", 0, 1, 3);
            mutableEdges.Clear();
            Assert.Equal(row.CurrentRegionIds, snapshot.CurrentRegionIds);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void ValidHazardPairHasIdenticalPublicEnvelopeButDifferentPersonPossibilities()
    {
        // Evaluator-only counterexample: these private definitions never enter the producer.
        var personScenario = HazardScenario("personOnly");
        var deviceScenario = HazardScenario("deviceOnly");
        ScenarioValidator.Validate(personScenario);
        ScenarioValidator.Validate(deviceScenario);
        var personModel = personScenario.StochasticHazard!;
        var deviceModel = deviceScenario.StochasticHazard!;
        Assert.Equal(JsonDefaults.Serialize(PublicHazardEnvelopeBuilder.Create(personModel)),
            JsonDefaults.Serialize(PublicHazardEnvelopeBuilder.Create(deviceModel)));
        Assert.Equal(1.0, personModel.SeparationProbability);
        Assert.Equal(1.0, deviceModel.SeparationProbability);
        Assert.True(PersonCanReachNorth(personModel));
        Assert.False(PersonCanReachNorth(deviceModel));
        var north = personModel.Nodes.Single(node => node.RegionId == "deposition-north");
        Assert.True(north.IsTerminal);
        Assert.Equal(0.0, north.ReleaseProbability);
        Assert.All(personModel.Transitions, edge => Assert.True(edge.Weight > 0));
    }

    private static ScenarioDefinition HazardScenario(string activation)
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "FootprintSim.sln")))
        {
            current = current.Parent;
        }

        Assert.NotNull(current);
        var root = JsonNode.Parse(File.ReadAllText(Path.Combine(
            current.FullName, "scenarios", "s2_branching_debris_transport", "scenario.json")))!;
        var hazard = root["stochasticHazard"]!;
        hazard["separationProbability"] = 1.0;
        var branch = hazard["transitions"]!.AsArray().Single(
            item => item!["id"]!.GetValue<string>() == "fork-to-river")!;
        branch["activation"] = activation;
        return JsonSerializer.Deserialize<ScenarioDefinition>(root.ToJsonString(), JsonDefaults.Options)!;
    }

    private static bool PersonCanReachNorth(StochasticHazardDefinition definition)
    {
        // After the mandatory shared trunk, separation is certain in both worlds.
        // Positive primary weights and zero deposition at fork/river make this
        // graph path possible. North is a permanent terminal, so its reachability
        // distinguishes possible final locations, not merely a sampled outcome.
        var known = new HashSet<string>(StringComparer.Ordinal) { "debris-fork" };
        var frontier = new Queue<string>(known);
        while (frontier.TryDequeue(out var node))
        {
            foreach (var edge in definition.Transitions.Where(edge => edge.SourceRegionId == node
                && edge.TransportType == HazardTransportType.HazardDriven
                && edge.Activation is HazardTransitionActivation.AnyEntity or HazardTransitionActivation.PersonOnly))
            {
                if (known.Add(edge.DestinationRegionId))
                {
                    frontier.Enqueue(edge.DestinationRegionId);
                }
            }
        }

        Assert.All(definition.Nodes.Where(node => node.RegionId is "debris-fork" or "river-channel"),
            node => Assert.Equal(0.0, node.DepositionProbability));
        return known.Contains("deposition-north");
    }

    private static FinitePublicTransitionModel Model(int code)
    {
        var remaining = code;
        var edges = new List<FinitePublicEdge>();
        foreach (var (from, to, transit) in Links)
        {
            var duration = remaining % 3;
            remaining /= 3;
            if (duration != 0)
            {
                edges.Add(new FinitePublicEdge(from, to, duration, transit));
            }
        }

        return new FinitePublicTransitionModel("f21-model-" + code, "person", 0, 0, 6,
            Nodes, Transit, edges, true, true);
    }

    private static PublicRegionProjection Produce(
        FinitePublicTransitionModel model, string node, int start, int end, int target) =>
        FinitePublicTransitionProducer.Create(model, "person", node, start, end, target, end, true);

    private static string[] Oracle(
        IReadOnlyList<FinitePublicEdge> edges, string node, int start, int end, int target)
    {
        var output = new HashSet<string>(StringComparer.Ordinal);
        // Enumerate paths by complete moves, unlike the producer's one-tick state sets.
        void Visit(string location, int tick)
        {
            if (tick == target)
            {
                output.Add(location);
                return;
            }

            Visit(location, tick + 1);
            foreach (var edge in edges.Where(edge => edge.From == location))
            {
                if (tick + edge.Duration > target)
                {
                    output.Add(edge.TransitRegionId);
                }
                else
                {
                    Visit(edge.To, tick + edge.Duration);
                }
            }
        }

        for (var tick = start; tick <= end; tick++)
        {
            Visit(node, tick);
        }

        return output.OrderBy(id => id, StringComparer.Ordinal).ToArray();
    }

    private static string[] Units(string region) => region.Length == 1
        ? ["u-" + region] : ["u-" + region + "-0", "u-" + region + "-1"];

    private static PublicSearchProjectionInput Input(PublicRegionProjection row)
    {
        var regions = Nodes.Concat(Transit).ToArray();
        var units = regions.SelectMany(Units).ToArray();
        return new PublicSearchProjectionInput("query", "person", row.TargetTick, row.EndTick,
            [new PastStateCandidate
            {
                CandidateId = "candidate",
                QueryId = "query",
                TargetEntityId = "person",
                RegionId = row.HistoricalRegionId,
                TargetTimeWindow = new InteractionTimeWindow(row.StartTick, row.EndTick),
                Disposition = PastStateCandidateDisposition.Uncertain,
                SourceAncestryIds = ["f21-public-model"]
            }],
            regions, units, [row],
            regions.Select(region => new PublicSearchUnitMapping(
                region, row.TargetTick, Units(region), true, "f21-unit-map", 0)).ToArray(),
            [new PublicActionFootprint("all", row.TargetTick, units, "f21-action", 0),
                new PublicActionFootprint("partial", row.TargetTick, ["u-a"], "f21-action", 0)]);
    }
}
