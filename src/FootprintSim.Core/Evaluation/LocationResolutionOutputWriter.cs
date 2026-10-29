using System.Globalization;
using System.Text;
using System.Text.Json;
using FootprintSim.Serialization;

namespace FootprintSim.Evaluation;

public static class LocationResolutionOutputNames
{
    public const string ReportJson = "location-resolution.json";
    public const string RunsJsonLines = "location-resolution-runs.jsonl";
    public const string LevelsCsv = "location-resolution.csv";
    public const string DeltasCsv = "location-resolution-deltas.csv";
    public const string LineageCsv = "location-resolution-lineage.csv";
    public const string ManifestJson = "location-resolution-manifest.json";

    public static IReadOnlyList<string> All { get; } =
    [
        ReportJson,
        RunsJsonLines,
        LevelsCsv,
        DeltasCsv,
        LineageCsv,
        ManifestJson
    ];
}

public sealed class LocationResolutionOutputWriter
{
    private static JsonSerializerOptions CompactJsonOptions { get; } =
        new(JsonDefaults.Options) { WriteIndented = false };

    public async Task WriteAsync(
        string outputDirectory,
        LocationResolutionEvaluationResult result,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputDirectory);
        ArgumentNullException.ThrowIfNull(result);
        Directory.CreateDirectory(outputDirectory);

        await Write(LocationResolutionOutputNames.ReportJson, JsonDefaults.Serialize(result) + "\n");
        await Write(
            LocationResolutionOutputNames.RunsJsonLines,
            string.Join("\n", result.Runs.Select(item => JsonSerializer.Serialize(item, CompactJsonOptions))) + "\n");
        await Write(LocationResolutionOutputNames.LevelsCsv, LevelsCsv(result));
        await Write(LocationResolutionOutputNames.DeltasCsv, DeltasCsv(result));
        await Write(LocationResolutionOutputNames.LineageCsv, LineageCsv(result));
        await Write(LocationResolutionOutputNames.ManifestJson, JsonDefaults.Serialize(result.Manifest) + "\n");

        async Task Write(string fileName, string content) => await File.WriteAllTextAsync(
            Path.Combine(outputDirectory, fileName),
            content,
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            cancellationToken);
    }

    private static string LevelsCsv(LocationResolutionEvaluationResult result)
    {
        var builder = new StringBuilder();
        builder.Append(
            "location_condition,information_order,regions,region_count,derived_regions," +
            "derived_region_count,constraint_runs,seeds,method,mean_candidates,containment," +
            "false_exclusion,reduction,generation_work,verification_work,forward,backward," +
            "evidence_records,public_relations,source_groups,candidate_improvement_from_unknown," +
            "candidate_output_equivalent,baseline_equivalent")
            .Append('\n');
        foreach (var profile in result.Aggregates)
        {
            foreach (var method in profile.Methods)
            {
                Append(builder,
                [
                    profile.LevelId,
                    Integer(profile.InformationOrder),
                    Join(profile.ObservedRegionIds),
                    Integer(profile.ObservedRegionCount),
                    Join(profile.DerivedAllowedRegionIds),
                    Integer(profile.DerivedAllowedRegionCount),
                    Integer(profile.ConstraintGeneratedCount),
                    Integer(profile.SeedCount),
                    EnumText(method.Method),
                    Decimal(method.MeanCandidateCount),
                    Decimal(method.ContainmentRate),
                    Integer(method.FalseExclusionCount),
                    Decimal(method.MeanReduction),
                    Decimal(method.MeanCandidateGenerationWork),
                    Decimal(method.MeanCandidateVerificationWork),
                    Decimal(method.MeanForwardTraversalWork),
                    Decimal(method.MeanBackwardTraversalWork),
                    Decimal(method.MeanEvidenceRecordsInspected),
                    Decimal(method.MeanPublicRelationsInspected),
                    Decimal(method.MeanSourceGroupsInspected),
                    Decimal(method.MeanCandidateImprovementFromUnknown),
                    Boolean(profile.CandidateOutputEquivalentToCurrent),
                    Boolean(profile.BaselineEquivalentToCurrent)
                ]);
            }
        }

        return builder.ToString();
    }

    private static string DeltasCsv(LocationResolutionEvaluationResult result)
    {
        var builder = new StringBuilder();
        builder.Append(
            "location_condition,method,delta_candidates,delta_containment,delta_false_exclusion," +
            "delta_reduction,delta_generation_work,delta_verification_work,delta_forward," +
            "delta_backward,delta_evidence_records,delta_public_relations")
            .Append('\n');
        foreach (var item in result.DeltasFromCurrent)
        {
            Append(builder,
            [
                item.LevelId,
                EnumText(item.Method),
                Decimal(item.MeanCandidateCountDelta),
                Decimal(item.ContainmentRateDelta),
                Integer(item.FalseExclusionCountDelta),
                Decimal(item.MeanReductionDelta),
                Decimal(item.CandidateGenerationWorkDelta),
                Decimal(item.CandidateVerificationWorkDelta),
                Decimal(item.ForwardTraversalWorkDelta),
                Decimal(item.BackwardTraversalWorkDelta),
                Decimal(item.EvidenceRecordsInspectedDelta),
                Decimal(item.PublicRelationsInspectedDelta)
            ]);
        }

        return builder.ToString();
    }

    private static string LineageCsv(LocationResolutionEvaluationResult result)
    {
        var builder = new StringBuilder();
        builder.Append(
            "seed,location_condition,source_observation_id,source_resolved,observed_regions," +
            "derived_regions,constraint_generated,constraint_definition_ids,constraint_ids," +
            "subjects,observed_start,observed_end,relation,available_tick,evidence_state,ancestry")
            .Append('\n');
        foreach (var item in result.Runs)
        {
            Append(builder,
            [
                Integer(item.Seed),
                item.LevelId,
                item.SourceObservationId,
                Boolean(item.SourceObservationResolved),
                Join(item.ObservedRegionIds),
                Join(item.DerivedAllowedRegionIds),
                Boolean(item.ConstraintGenerated),
                Join(item.ConstraintDefinitionIds),
                Join(item.ConstraintIds),
                Join(item.ObservedSubjectIds),
                Long(item.ObservedTimeWindow.StartTick),
                Long(item.ObservedTimeWindow.EndTick),
                EnumText(item.ObservedRelationKind),
                Long(item.AvailableAtTick),
                EnumText(item.EvidenceState),
                Join(item.SourceAncestryIds)
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

    private static string EnumText<T>(T value) where T : struct, Enum =>
        JsonNamingPolicy.CamelCase.ConvertName(value.ToString());

    private static string Integer(int value) => value.ToString(CultureInfo.InvariantCulture);

    private static string Long(long value) => value.ToString(CultureInfo.InvariantCulture);

    private static string Decimal(decimal value) => value.ToString(
        "0.############################",
        CultureInfo.InvariantCulture);

    private static string Boolean(bool value) => value ? "true" : "false";
}
