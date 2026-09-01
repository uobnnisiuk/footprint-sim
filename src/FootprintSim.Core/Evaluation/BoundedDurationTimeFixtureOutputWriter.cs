using System.Globalization;
using System.Text;
using FootprintSim.Serialization;

namespace FootprintSim.Evaluation;

public static class BoundedDurationTimeFixtureOutputNames
{
    public const string ReportJson = "observation-constraint-derivation.json";
    public const string ProfilesCsv = "observation-constraint-derivation.csv";
    public const string ManifestJson = "observation-constraint-derivation-manifest.json";

    public static IReadOnlyList<string> All { get; } =
    [
        ReportJson,
        ProfilesCsv,
        ManifestJson
    ];
}

public sealed class BoundedDurationTimeFixtureOutputWriter
{
    public async Task WriteAsync(
        string outputDirectory,
        BoundedDurationTimeFixtureEvaluationResult result,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputDirectory);
        ArgumentNullException.ThrowIfNull(result);
        Directory.CreateDirectory(outputDirectory);

        await Write(BoundedDurationTimeFixtureOutputNames.ReportJson, JsonDefaults.Serialize(result) + "\n");
        await Write(BoundedDurationTimeFixtureOutputNames.ProfilesCsv, ProfilesCsv(result));
        await Write(
            BoundedDurationTimeFixtureOutputNames.ManifestJson,
            JsonDefaults.Serialize(result.Manifest) + "\n");

        async Task Write(string fileName, string content) => await File.WriteAllTextAsync(
            Path.Combine(outputDirectory, fileName),
            content,
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            cancellationToken);
    }

    private static string ProfilesCsv(BoundedDurationTimeFixtureEvaluationResult result)
    {
        var builder = new StringBuilder();
        builder.Append(
            "profile,information_order,occurrence_time_knowledge,observed_start,observed_end," +
            "derivation_success,failure_reason,derived_start,derived_end,constraint_id," +
            "candidate_count,containment,false_exclusion,reduction,generation_work," +
            "evidence_inspected,source_groups_inspected")
            .Append('\n');
        foreach (var run in result.Runs)
        {
            Append(builder,
            [
                run.ProfileId,
                Integer(run.InformationOrder),
                EnumText(run.OccurrenceTimeKnowledge),
                NullableLong(run.ObservedOccurrenceWindow?.StartTick),
                NullableLong(run.ObservedOccurrenceWindow?.EndTick),
                Boolean(run.DerivationAudit.Success),
                EnumText(run.DerivationAudit.FailureReason),
                NullableLong(run.DerivedPastWindow?.StartTick),
                NullableLong(run.DerivedPastWindow?.EndTick),
                run.ConstraintId,
                Integer(run.CandidateCount),
                Boolean(run.ContainsTruth),
                Boolean(run.FalseExclusion),
                Decimal(run.Reduction),
                Integer(run.CandidateGenerationWork),
                Integer(run.EvidenceRecordsInspected),
                Integer(run.SourceGroupsInspected)
            ]);
        }

        return builder.ToString();
    }

    private static void Append(StringBuilder builder, IEnumerable<string?> values) => builder
        .AppendJoin(',', values.Select(Csv))
        .Append('\n');

    private static string Csv(string? value)
    {
        value ??= string.Empty;
        return value.IndexOfAny([',', '"', '\r', '\n']) < 0
            ? value
            : $"\"{value.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
    }

    private static string EnumText<T>(T value) where T : struct, Enum =>
        System.Text.Json.JsonNamingPolicy.CamelCase.ConvertName(value.ToString());

    private static string Integer(int value) => value.ToString(CultureInfo.InvariantCulture);

    private static string NullableLong(long? value) =>
        value?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;

    private static string Decimal(decimal value) => value.ToString(
        "0.############################",
        CultureInfo.InvariantCulture);

    private static string Boolean(bool value) => value ? "true" : "false";
}
