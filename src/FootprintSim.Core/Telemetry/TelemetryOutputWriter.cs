using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using FootprintSim.Serialization;

namespace FootprintSim.Telemetry;

public static class TelemetryOutputNames
{
    public const string RootDirectory = "telemetry";
    public const string WorldEvents = "truth/world-events.jsonl";
    public const string TruthInteractions = "truth/interactions.jsonl";
    public const string Observations = "evidence/observations.jsonl";
    public const string InteractionObservations = "evidence/interaction-observations.jsonl";
    public const string Constraints = "evidence/constraints.jsonl";
    public const string ConstraintApplications = "evaluation/constraint-applications.jsonl";
    public const string CandidateTransitions = "evaluation/candidate-transitions.jsonl";
    public const string Manifest = "telemetry-manifest.json";

    public static IReadOnlyList<string> JsonLines { get; } =
    [
        WorldEvents,
        TruthInteractions,
        Observations,
        InteractionObservations,
        Constraints,
        ConstraintApplications,
        CandidateTransitions
    ];

    public static IReadOnlyList<string> All { get; } = [.. JsonLines, Manifest];
}

public sealed class TelemetryOutputWriter
{
    private static readonly JsonSerializerOptions CompactJsonOptions = CreateCompactJsonOptions();

    public async Task WriteAsync(
        string telemetryDirectory,
        NeutralTelemetryBundle telemetry,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(telemetryDirectory);
        ArgumentNullException.ThrowIfNull(telemetry);

        await WriteJsonLinesAsync(
            TelemetryOutputNames.WorldEvents,
            telemetry.Truth.WorldEvents,
            cancellationToken);
        await WriteJsonLinesAsync(
            TelemetryOutputNames.TruthInteractions,
            telemetry.Truth.Interactions,
            cancellationToken);
        await WriteJsonLinesAsync(
            TelemetryOutputNames.Observations,
            telemetry.Evidence.Observations,
            cancellationToken);
        await WriteJsonLinesAsync(
            TelemetryOutputNames.InteractionObservations,
            telemetry.Evidence.InteractionObservations,
            cancellationToken);
        await WriteJsonLinesAsync(
            TelemetryOutputNames.Constraints,
            telemetry.Evidence.Constraints,
            cancellationToken);
        await WriteJsonLinesAsync(
            TelemetryOutputNames.ConstraintApplications,
            telemetry.Evaluation.ConstraintApplications,
            cancellationToken);
        await WriteJsonLinesAsync(
            TelemetryOutputNames.CandidateTransitions,
            telemetry.Evaluation.CandidateTransitions,
            cancellationToken);
        await WriteTextAsync(
            TelemetryOutputNames.Manifest,
            JsonDefaults.Serialize(telemetry.Manifest) + "\n",
            cancellationToken);

        async Task WriteJsonLinesAsync<T>(
            string relativePath,
            IReadOnlyList<T> records,
            CancellationToken token)
        {
            var content = records.Count == 0
                ? string.Empty
                : string.Join(
                    "\n",
                    records.Select(record => JsonSerializer.Serialize(
                        record,
                        CompactJsonOptions))) + "\n";
            await WriteTextAsync(relativePath, content, token);
        }

        async Task WriteTextAsync(string relativePath, string content, CancellationToken token)
        {
            var path = Path.Combine(
                telemetryDirectory,
                relativePath.Replace('/', Path.DirectorySeparatorChar));
            var directory = Path.GetDirectoryName(path);
            if (directory is not null)
            {
                Directory.CreateDirectory(directory);
            }

            await File.WriteAllTextAsync(
                path,
                content,
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
                token);
        }
    }

    private static JsonSerializerOptions CreateCompactJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonDefaults.Options)
        {
            WriteIndented = false
        };
        options.Converters.Clear();
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
        return options;
    }
}
