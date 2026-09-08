using System.Globalization;
using System.Text;
using System.Text.Json;
using FootprintSim.Serialization;

namespace FootprintSim.Evaluation;

public static class CommonWorldPlateauDomainAuditOutputNames
{
    public const string ReportJson = "plateau-domain-audit.json";
    public const string RunsJsonLines = "plateau-domain-audit-runs.jsonl";
    public const string AuditCsv = "plateau-domain-audit.csv";
    public const string ManifestJson = "plateau-domain-audit-manifest.json";
    public static IReadOnlyList<string> All { get; } =
        [ReportJson, RunsJsonLines, AuditCsv, ManifestJson];
}

public sealed class CommonWorldPlateauDomainAuditOutputWriter
{
    public async Task WriteAsync(CommonWorldPlateauDomainAuditResult result, string outputDirectory,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputDirectory);
        Directory.CreateDirectory(outputDirectory);
        var compact = new JsonSerializerOptions(JsonDefaults.Options) { WriteIndented = false };
        await Write(CommonWorldPlateauDomainAuditOutputNames.ReportJson,
            JsonDefaults.Serialize(new { result.Manifest, result.Summary, result.ProfileAggregates }) + "\n");
        await Write(CommonWorldPlateauDomainAuditOutputNames.RunsJsonLines,
            string.Join("\n", result.Rows.Select(row => JsonSerializer.Serialize(row, compact))) + "\n");
        await Write(CommonWorldPlateauDomainAuditOutputNames.AuditCsv, Csv(result));
        await Write(CommonWorldPlateauDomainAuditOutputNames.ManifestJson,
            JsonDefaults.Serialize(result.Manifest) + "\n");

        async Task Write(string name, string content) => await File.WriteAllTextAsync(
            Path.Combine(outputDirectory, name), content, new UTF8Encoding(false), cancellationToken);
    }

    private static string Csv(CommonWorldPlateauDomainAuditResult result)
    {
        var builder = new StringBuilder(
            "seed,comparison,domain,added_axis,baseline_profile,combined_profile,baseline_count," +
            "combined_count,additional_reduction,candidate_sets_equal,top,implied," +
            "region_implication_applicable,classification,top_domain_regions,implication_domain_regions," +
            "implication_counterexample_regions,baseline_candidate_ids,combined_candidate_ids\n");
        foreach (var row in result.Rows)
        {
            foreach (var domain in new[] { row.Legacy, row.Registered, row.Occupied, row.Extended })
            {
                string[] fields =
                [
                    Integer(row.Seed), row.ComparisonId, domain.DomainId, EnumText(row.AddedAxis),
                    row.BaselineProfileId, row.CombinedProfileId, Integer(row.BaselineCandidateIds.Count),
                    Integer(row.CombinedCandidateIds.Count), Integer(row.AdditionalCandidateReduction),
                    Boolean(row.CandidateSetsEqual), Boolean(domain.AddedPredicateIsTopForCandidateDomain),
                    Boolean(domain.AddedPredicateImpliedByBaselinePredicates),
                    Boolean(domain.RegionImplicationApplicable), EnumText(domain.Classification),
                    string.Join("|", domain.DomainRegionIds), string.Join("|", domain.ImplicationDomainRegionIds),
                    string.Join("|", domain.ImplicationCounterexampleRegionIds),
                    string.Join("|", row.BaselineCandidateIds), string.Join("|", row.CombinedCandidateIds)
                ];
                builder.AppendJoin(',', fields.Select(Escape)).Append('\n');
            }
        }

        return builder.ToString();
    }

    private static string Integer(int value) => value.ToString(CultureInfo.InvariantCulture);
    private static string Boolean(bool value) => value ? "true" : "false";
    private static string EnumText<T>(T value) where T : struct, Enum =>
        JsonNamingPolicy.CamelCase.ConvertName(value.ToString());
    private static string Escape(string value) => value.IndexOfAny([',', '"', '\n', '\r']) >= 0
        ? string.Concat('"', value.Replace("\"", "\"\"", StringComparison.Ordinal), '"') : value;
}
