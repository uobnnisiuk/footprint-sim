using System.Globalization;
using System.Text;
using FootprintSim.Serialization;

namespace FootprintSim.Evaluation;

public static class CommonWorldObservationFixtureOutputNames
{
    public const string SummaryJson = "common-world-observation-summary.json";
    public const string ProfilesCsv = "common-world-observation-profiles.csv";
    public const string DerivationAuditJson = "common-world-derivation-audit.json";
    public const string CompositionAuditJsonl = "common-world-composition-audit.jsonl";
    public const string ManifestJson = "common-world-observation-manifest.json";

    public static IReadOnlyList<string> All { get; } =
    [
        SummaryJson,
        ProfilesCsv,
        DerivationAuditJson,
        CompositionAuditJsonl,
        ManifestJson
    ];
}

public sealed class CommonWorldObservationFixtureOutputWriter
{
    public async Task WriteAsync(
        string outputDirectory,
        CommonWorldObservationFixtureEvaluationResult result,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputDirectory);
        ArgumentNullException.ThrowIfNull(result);
        Directory.CreateDirectory(outputDirectory);

        await Write(
            CommonWorldObservationFixtureOutputNames.SummaryJson,
            JsonDefaults.Serialize(result) + "\n");
        await Write(
            CommonWorldObservationFixtureOutputNames.ProfilesCsv,
            Profiles(result));
        await Write(
            CommonWorldObservationFixtureOutputNames.DerivationAuditJson,
            JsonDefaults.Serialize(result.DerivationAudit) + "\n");
        await Write(
            CommonWorldObservationFixtureOutputNames.CompositionAuditJsonl,
            string.Concat(result.Runs.Select(run =>
                JsonDefaults.Serialize(run.CompositionAudit) + "\n")));
        await Write(
            CommonWorldObservationFixtureOutputNames.ManifestJson,
            JsonDefaults.Serialize(result.Manifest) + "\n");

        async Task Write(string fileName, string content) => await File.WriteAllTextAsync(
            Path.Combine(outputDirectory, fileName),
            content,
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            cancellationToken);
    }

    private static string Profiles(CommonWorldObservationFixtureEvaluationResult result)
    {
        var builder = new StringBuilder();
        builder.Append(
            "profile,active_axes,omitted_axes,active_constraint_ids,status,composition_id," +
            "candidate_universe_id,candidate_count,candidate_ids,containment,false_exclusion," +
            "reduction_vs_none_count,reduction_vs_none_fraction,active_constraints," +
            "supporting_source_groups,selected_fields,components_received,predicates_applied," +
            "candidate_predicate_checks,generation_work")
            .Append('\n');
        foreach (var run in result.Runs)
        {
            Append(builder,
            [
                run.ProfileId,
                string.Join("|", run.ActiveAxes.Select(EnumText)),
                string.Join("|", run.OmittedAxes.Select(EnumText)),
                string.Join("|", run.ActiveConstraintIds),
                EnumText(run.Status),
                run.CompositionId,
                run.CandidateUniverseId,
                Integer(run.CandidateCount),
                string.Join("|", run.CandidateIds),
                Boolean(run.ContainsTruth),
                Boolean(run.FalseExclusion),
                Integer(run.ReductionVsNoneCount),
                Decimal(run.ReductionVsNoneFraction),
                Integer(run.ActiveConstraintCount),
                Integer(run.SelectedSupportingSourceGroupCount),
                Integer(run.SelectedFieldCount),
                Integer(run.CompositionAudit.InputComponentsReceived),
                Integer(run.CompositionAudit.PredicatesApplied),
                Integer(run.CompositionAudit.CandidatePredicateChecks),
                Integer(run.CandidateGenerationWork)
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

    private static string Decimal(decimal value) => value.ToString(
        "0.############################",
        CultureInfo.InvariantCulture);

    private static string Boolean(bool value) => value ? "true" : "false";
}
