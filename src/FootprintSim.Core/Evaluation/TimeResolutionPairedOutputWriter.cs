using System.Globalization;
using System.Text;
using System.Text.Json;
using FootprintSim.Interactions;
using FootprintSim.Serialization;

namespace FootprintSim.Evaluation;

public static class TimeResolutionPairedOutputNames
{
    public const string ReportJson = "time-resolution-paired.json";
    public const string RunsJsonLines = "time-resolution-paired-runs.jsonl";
    public const string ProfilesCsv = "time-resolution-paired.csv";
    public const string MonotonicityCsv = "time-resolution-paired-monotonicity.csv";
    public const string ManifestJson = "time-resolution-paired-manifest.json";

    public static IReadOnlyList<string> All { get; } =
    [
        ReportJson,
        RunsJsonLines,
        ProfilesCsv,
        MonotonicityCsv,
        ManifestJson
    ];
}

public sealed class TimeResolutionPairedOutputWriter
{
    private static JsonSerializerOptions CompactJsonOptions { get; } =
        new(JsonDefaults.Options) { WriteIndented = false };

    public async Task WriteAsync(
        string outputDirectory,
        TimeResolutionPairedEvaluationResult result,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputDirectory);
        ArgumentNullException.ThrowIfNull(result);
        Directory.CreateDirectory(outputDirectory);

        await Write(TimeResolutionPairedOutputNames.ReportJson, JsonDefaults.Serialize(result) + "\n");
        await Write(
            TimeResolutionPairedOutputNames.RunsJsonLines,
            string.Join("\n", result.Runs.Select(item =>
                JsonSerializer.Serialize(item, CompactJsonOptions))) + "\n");
        await Write(TimeResolutionPairedOutputNames.ProfilesCsv, ProfilesCsv(result));
        await Write(TimeResolutionPairedOutputNames.MonotonicityCsv, MonotonicityCsv(result));
        await Write(
            TimeResolutionPairedOutputNames.ManifestJson,
            JsonDefaults.Serialize(result.Manifest) + "\n");

        async Task Write(string fileName, string content) => await File.WriteAllTextAsync(
            Path.Combine(outputDirectory, fileName),
            content,
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            cancellationToken);
    }

    private static string ProfilesCsv(TimeResolutionPairedEvaluationResult result)
    {
        var builder = new StringBuilder();
        builder.Append(
            "profile,information_order,seeds,mean_candidates,median_candidates,min_candidates," +
            "max_candidates,containment,false_exclusion,mean_reduction_count_vs_unknown," +
            "mean_reduction_fraction_vs_unknown,generation_work")
            .Append('\n');
        foreach (var item in result.Aggregates)
        {
            Append(builder,
            [
                item.ProfileId,
                Integer(item.InformationOrder),
                Integer(item.SeedCount),
                Decimal(item.MeanCandidateCount),
                Decimal(item.MedianCandidateCount),
                Integer(item.MinimumCandidateCount),
                Integer(item.MaximumCandidateCount),
                Decimal(item.ContainmentRate),
                Integer(item.FalseExclusionCount),
                Decimal(item.MeanCandidateReductionCountVsUnknown),
                Decimal(item.MeanCandidateReductionFractionVsUnknown),
                Decimal(item.MeanCandidateGenerationWork)
            ]);
        }

        return builder.ToString();
    }

    private static string MonotonicityCsv(TimeResolutionPairedEvaluationResult result)
    {
        var builder = new StringBuilder();
        builder.Append(
            "seed,more_precise,coarser,more_precise_window,coarser_window,window_subset," +
            "window_strictly_expanded,more_precise_candidates,coarser_candidates," +
            "candidate_subset,candidate_sets_equal,constraint_widened_candidate_unchanged")
            .Append('\n');
        foreach (var item in result.Runs.SelectMany(run => run.MonotonicityAudits))
        {
            Append(builder,
            [
                Integer(item.Seed),
                item.MorePreciseProfileId,
                item.CoarserProfileId,
                Window(item.MorePreciseDerivedWindow),
                Window(item.CoarserDerivedWindow),
                Boolean(item.DerivedWindowIsSubset),
                Boolean(item.DerivedWindowStrictlyExpanded),
                Join(item.MorePreciseCandidateIds),
                Join(item.CoarserCandidateIds),
                Boolean(item.CandidateSetIsSubset),
                Boolean(item.CandidateSetsEqual),
                Boolean(item.ConstraintWidenedButCandidateUnchanged)
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

    private static string Join(IEnumerable<string> values) => string.Join(
        "|",
        values.Distinct(StringComparer.Ordinal).OrderBy(item => item, StringComparer.Ordinal));

    private static string Window(InteractionTimeWindow? value) => value is null
        ? string.Empty
        : $"[{value.StartTick},{value.EndTick}]";

    private static string Integer(int value) => value.ToString(CultureInfo.InvariantCulture);

    private static string Decimal(decimal value) => value.ToString(
        "0.############################",
        CultureInfo.InvariantCulture);

    private static string Boolean(bool value) => value ? "true" : "false";
}
