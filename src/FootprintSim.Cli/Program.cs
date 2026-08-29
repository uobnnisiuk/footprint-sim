using System.Security.Cryptography;
using System.Text.Json;
using FootprintSim.Inference;
using FootprintSim.Scenarios;
using FootprintSim.Serialization;
using FootprintSim.Simulation;

return await CliApplication.RunAsync(args);

internal static class CliApplication
{
    public static async Task<int> RunAsync(string[] args)
    {
        try
        {
            if (args.Length == 0 || args.Contains("--help", StringComparer.Ordinal))
            {
                PrintUsage();
                return args.Length == 0 ? 2 : 0;
            }

            var options = ParseOptions(args);
            var scenarioPath = Require(options, "--scenario");
            var outputPath = Require(options, "--output");
            var seedText = Require(options, "--seed");
            if (!int.TryParse(seedText, out var seed))
            {
                throw new ArgumentException("--seed must be a 32-bit integer.");
            }

            scenarioPath = Path.GetFullPath(scenarioPath);
            outputPath = Path.GetFullPath(outputPath);
            var scenario = ScenarioLoader.Load(scenarioPath);
            var run = new SimulationEngine().Run(scenario, seed);

            // Inference receives only the observation contract and declared public context.
            var summary = new BasicRescueInference().Infer(
                new RescueContext
                {
                    ScenarioId = scenario.Id,
                    TargetPersonId = scenario.TargetPersonId,
                    NormalWalkingRadius = scenario.NormalWalkingRadius,
                    PublicRegions = scenario.Regions,
                    DeclaredHazardEnvelopeRegionIds = scenario.HazardTransportEnvelopeRegionIds
                },
                run.ObservationSet);

            Directory.CreateDirectory(outputPath);
            var metadata = new RunMetadata
            {
                SchemaVersion = 1,
                EngineVersion = "m0",
                ScenarioId = scenario.Id,
                Seed = seed,
                ScenarioSha256 = Convert.ToHexString(SHA256.HashData(
                    await File.ReadAllBytesAsync(scenarioPath))).ToLowerInvariant(),
                GroundTruthEventCount = run.GroundTruthEvents.Count,
                ObservationCount = run.ObservationSet.Observations.Count,
                Outputs =
                [
                    "ground-truth.events.json",
                    "observations.json",
                    "search-zone.summary.json",
                    "run.metadata.json"
                ]
            };

            await WriteJsonAsync(
                Path.Combine(outputPath, "ground-truth.events.json"),
                new GroundTruthEventLog
                {
                    ScenarioId = scenario.Id,
                    Seed = seed,
                    Events = run.GroundTruthEvents
                });
            await WriteJsonAsync(Path.Combine(outputPath, "observations.json"), run.ObservationSet);
            await WriteJsonAsync(Path.Combine(outputPath, "search-zone.summary.json"), summary);
            await WriteJsonAsync(Path.Combine(outputPath, "run.metadata.json"), metadata);

            Console.WriteLine(
                $"Scenario '{scenario.Id}' completed with seed {seed}: " +
                $"{run.GroundTruthEvents.Count} truth events, " +
                $"{run.ObservationSet.Observations.Count} observations.");
            Console.WriteLine($"Results: {outputPath}");
            return 0;
        }
        catch (Exception exception) when (exception is
            ArgumentException or IOException or JsonException or UnauthorizedAccessException)
        {
            Console.Error.WriteLine($"error: {exception.Message}");
            return 2;
        }
    }

    private static Dictionary<string, string> ParseOptions(IReadOnlyList<string> args)
    {
        var options = new Dictionary<string, string>(StringComparer.Ordinal);
        for (var index = 0; index < args.Count; index += 2)
        {
            var name = args[index];
            if (name is not ("--scenario" or "--seed" or "--output"))
            {
                throw new ArgumentException($"Unknown option '{name}'.");
            }

            if (index + 1 >= args.Count)
            {
                throw new ArgumentException($"Option '{name}' requires a value.");
            }

            if (!options.TryAdd(name, args[index + 1]))
            {
                throw new ArgumentException($"Option '{name}' was specified more than once.");
            }
        }

        return options;
    }

    private static string Require(IReadOnlyDictionary<string, string> options, string name)
    {
        return options.TryGetValue(name, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value
            : throw new ArgumentException($"Required option '{name}' is missing.");
    }

    private static async Task WriteJsonAsync<T>(string path, T value)
    {
        await File.WriteAllTextAsync(path, JsonDefaults.Serialize(value) + "\n");
    }

    private static void PrintUsage()
    {
        Console.WriteLine(
            "Usage: dotnet run --project src/FootprintSim.Cli -- " +
            "--scenario <scenario.json> --seed <integer> --output <directory>");
    }
}

internal sealed record GroundTruthEventLog
{
    public required string ScenarioId { get; init; }

    public required int Seed { get; init; }

    public required IReadOnlyList<FootprintSim.Events.SimulationEvent> Events { get; init; }
}

internal sealed record RunMetadata
{
    public required int SchemaVersion { get; init; }

    public required string EngineVersion { get; init; }

    public required string ScenarioId { get; init; }

    public required int Seed { get; init; }

    public required string ScenarioSha256 { get; init; }

    public required int GroundTruthEventCount { get; init; }

    public required int ObservationCount { get; init; }

    public required IReadOnlyList<string> Outputs { get; init; }
}
