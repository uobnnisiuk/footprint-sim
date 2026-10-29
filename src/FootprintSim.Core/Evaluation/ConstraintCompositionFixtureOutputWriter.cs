using System.Globalization;
using System.Text;
using FootprintSim.Serialization;

namespace FootprintSim.Evaluation;

public static class ConstraintCompositionFixtureOutputNames
{
    public const string SummaryJson = "constraint-composition-summary.json";
    public const string ProfilesCsv = "constraint-composition-profiles.csv";
    public const string AuditJsonl = "constraint-composition-audit.jsonl";
    public const string ManifestJson = "constraint-composition-manifest.json";

    public static IReadOnlyList<string> All { get; } =
    [
        SummaryJson,
        ProfilesCsv,
        AuditJsonl,
        ManifestJson
    ];
}

public sealed class ConstraintCompositionFixtureOutputWriter
{
    public async Task WriteAsync(
        string outputDirectory,
        ConstraintCompositionFixtureEvaluationResult result,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputDirectory);
        ArgumentNullException.ThrowIfNull(result);
        Directory.CreateDirectory(outputDirectory);

        await Write(
            ConstraintCompositionFixtureOutputNames.SummaryJson,
            JsonDefaults.Serialize(result) + "\n");
        await Write(
            ConstraintCompositionFixtureOutputNames.ProfilesCsv,
            Profiles(result));
        await Write(
            ConstraintCompositionFixtureOutputNames.AuditJsonl,
            string.Concat(result.Runs.Select(item =>
                JsonDefaults.Serialize(item.CompositionAudit) + "\n")));
        await Write(
            ConstraintCompositionFixtureOutputNames.ManifestJson,
            JsonDefaults.Serialize(result.Manifest) + "\n");

        async Task Write(string fileName, string content) => await File.WriteAllTextAsync(
            Path.Combine(outputDirectory, fileName),
            content,
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            cancellationToken);
    }

    private static string Profiles(ConstraintCompositionFixtureEvaluationResult result)
    {
        var builder = new StringBuilder();
        builder.Append(
            "profile,location,time,participant,status,composition_id,candidate_count," +
            "candidate_ids,containment,false_exclusion,reduction_vs_none_count," +
            "reduction_vs_none_fraction,constraints_received,predicates_applied," +
            "candidate_predicate_checks,duplicates_suppressed,alternative_branches," +
            "source_group_count,generation_work")
            .Append('\n');
        foreach (var run in result.Runs)
        {
            Append(builder,
            [
                run.ProfileId,
                Boolean(run.IncludesLocation),
                Boolean(run.IncludesTime),
                Boolean(run.IncludesParticipant),
                EnumText(run.Status),
                run.CompositionId,
                Integer(run.CandidateCount),
                string.Join("|", run.CandidateIds),
                Boolean(run.ContainsTruth),
                Boolean(run.FalseExclusion),
                Integer(run.ReductionVsNoneCount),
                Decimal(run.ReductionVsNoneFraction),
                Integer(run.CompositionAudit.ConstraintsReceived),
                Integer(run.CompositionAudit.PredicatesApplied),
                Integer(run.CompositionAudit.CandidatePredicateChecks),
                Integer(run.CompositionAudit.DuplicateConstraintsSuppressed),
                Integer(run.CompositionAudit.AlternativeBranchesEvaluated),
                Integer(run.CompositionAudit.DistinctSourceGroupCount),
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
