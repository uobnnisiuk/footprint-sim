using System.Globalization;
using System.Text;
using System.Text.Json;
using FootprintSim.Serialization;

namespace FootprintSim.Evaluation;

public static class CandidateIntervalWidthOutputNames
{
    public const string ReportJson = "candidate-interval-width.json";
    public const string RunsJsonLines = "candidate-interval-width-runs.jsonl";
    public const string ProfilesCsv = "candidate-interval-width-profiles.csv";
    public const string ConditionalsCsv = "candidate-interval-width-conditionals.csv";
    public const string CrosswalkCsv = "candidate-interval-width-crosswalk.csv";
    public const string ManifestJson = "candidate-interval-width-manifest.json";
    public static IReadOnlyList<string> All { get; } =
        [ReportJson, RunsJsonLines, ProfilesCsv, ConditionalsCsv, CrosswalkCsv, ManifestJson];
}

public sealed class CandidateIntervalWidthOutputWriter
{
    public async Task WriteAsync(string outputDirectory, CandidateIntervalWidthResult result,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputDirectory);
        ArgumentNullException.ThrowIfNull(result);
        Directory.CreateDirectory(outputDirectory);
        var compact = new JsonSerializerOptions(JsonDefaults.Options) { WriteIndented = false };
        await Write(CandidateIntervalWidthOutputNames.ReportJson,
            JsonDefaults.Serialize(new
            {
                result.Manifest,
                result.Summary,
                result.ProfileAggregates,
                result.ConditionalAggregates
            }) + "\n");
        await Write(CandidateIntervalWidthOutputNames.RunsJsonLines,
            string.Join("\n", result.Runs.Select(run => JsonSerializer.Serialize(run, compact))) + "\n");
        await Write(CandidateIntervalWidthOutputNames.ProfilesCsv, Profiles(result));
        await Write(CandidateIntervalWidthOutputNames.ConditionalsCsv, Conditionals(result));
        await Write(CandidateIntervalWidthOutputNames.CrosswalkCsv, Crosswalk(result));
        await Write(CandidateIntervalWidthOutputNames.ManifestJson, JsonDefaults.Serialize(result.Manifest) + "\n");

        async Task Write(string name, string content) => await File.WriteAllTextAsync(
            Path.Combine(outputDirectory, name), content, new UTF8Encoding(false), cancellationToken);
    }

    private static string Profiles(CandidateIntervalWidthResult result)
    {
        var csv = new StringBuilder("seed,profile,original_count,point_count,point_minus_original," +
            "lost_fraction,original_keys,point_keys,lost_keys,new_keys,original_ids,point_ids," +
            "expected_equality,keys_equal,point_subset,boundary_rule,work_composition_unchanged," +
            "original_contains_truth,point_contains_truth\n");
        foreach (var run in result.Runs)
        {
            foreach (var row in run.ProfileChanges)
            {
                Append(csv, [Text(run.Seed), row.ProfileId, Text(row.OriginalCandidateCount),
                    Text(row.PointCandidateCount), Text(row.PointMinusOriginalCount),
                    Text((decimal)row.LostKeys.Count / row.OriginalCandidateCount),
                    Join(row.OriginalCandidateKeys), Join(row.PointCandidateKeys), Join(row.LostKeys),
                    Join(row.NewKeys), Join(row.OriginalCandidateIds), Join(row.PointCandidateIds),
                    Bool(row.ExpectedEquality), Bool(row.KeySetsEqual), Bool(row.PointKeysAreSubset),
                    Bool(row.LostKeysMatchBoundaryRule), Bool(row.WorkAndCompositionUnchanged),
                    Bool(row.OriginalContainsTruth), Bool(row.PointContainsTruth)]);
            }
        }

        return csv.ToString();
    }

    private static string Conditionals(CandidateIntervalWidthResult result)
    {
        var csv = new StringBuilder("seed,comparison,added_axis,expected_delta_sign,original_reduction," +
            "point_reduction,point_minus_original,original_classification,point_classification," +
            "original_top,point_top,original_implied,point_implied,signed_delta_invariant\n");
        foreach (var run in result.Runs)
        {
            foreach (var row in run.ConditionalChanges)
            {
                Append(csv, [Text(run.Seed), row.ComparisonId, EnumText(row.AddedAxis), row.ExpectedDeltaSign,
                    Text(row.OriginalAdditionalReduction), Text(row.PointAdditionalReduction),
                    Text(row.PointMinusOriginalReduction), EnumText(row.OriginalDiagnostic.Classification),
                    EnumText(row.PointDiagnostic.Classification),
                    Bool(row.OriginalDiagnostic.AddedPredicateIsTopForCandidateDomain),
                    Bool(row.PointDiagnostic.AddedPredicateIsTopForCandidateDomain),
                    Bool(row.OriginalDiagnostic.AddedPredicateImpliedByBaselinePredicates),
                    Bool(row.PointDiagnostic.AddedPredicateImpliedByBaselinePredicates), Bool(row.SignedDeltaInvariant)]);
            }
        }

        return csv.ToString();
    }

    private static string Crosswalk(CandidateIntervalWidthResult result)
    {
        var csv = new StringBuilder("seed,key,source,region,original_start,original_end,point_start,point_end," +
            "original_kind,point_kind,original_state_id,point_state_id,original_candidate_id,point_candidate_id," +
            "original_provenance_signature,point_provenance_signature\n");
        foreach (var row in result.Runs.SelectMany(run => run.Crosswalk))
        {
            Append(csv, [Text(row.Seed), row.CandidateKey, row.Original.PublicSourceIdentifier,
                row.Original.RegionId, Text(row.Original.TimeWindow.StartTick), Text(row.Original.TimeWindow.EndTick),
                Text(row.Point.TimeWindow.StartTick), Text(row.Point.TimeWindow.EndTick),
                EnumText(row.Original.TemporalKind), EnumText(row.Point.TemporalKind),
                row.Original.StateId, row.Point.StateId, row.Original.CandidateId, row.Point.CandidateId,
                row.Original.ProvenanceSignature, row.Point.ProvenanceSignature]);
        }

        return csv.ToString();
    }

    private static string Text<T>(T value) where T : IFormattable => value.ToString(null, CultureInfo.InvariantCulture);
    private static string Bool(bool value) => value ? "true" : "false";
    private static string Join(IEnumerable<string> values) => string.Join('|', values);
    private static string EnumText<T>(T value) where T : struct, Enum =>
        JsonNamingPolicy.CamelCase.ConvertName(value.ToString());
    private static void Append(StringBuilder builder, IEnumerable<string> values) =>
        builder.AppendJoin(',', values.Select(Escape)).Append('\n');
    private static string Escape(string value) => value.IndexOfAny([',', '"', '\r', '\n']) >= 0
        ? string.Concat('"', value.Replace("\"", "\"\"", StringComparison.Ordinal), '"') : value;
}
