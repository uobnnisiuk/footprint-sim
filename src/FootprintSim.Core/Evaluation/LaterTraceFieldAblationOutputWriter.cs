using System.Globalization;
using System.Text;
using System.Text.Json;
using FootprintSim.Serialization;

namespace FootprintSim.Evaluation;

public static class LaterTraceFieldAblationOutputNames
{
    public const string ReportJson = "later-trace-field-ablation.json";
    public const string RunsJsonLines = "later-trace-field-ablation-runs.jsonl";
    public const string ConditionsCsv = "later-trace-field-ablation.csv";
    public const string DeltasCsv = "later-trace-field-ablation-deltas.csv";
    public const string ContributionsCsv = "later-trace-field-contributions.csv";
    public const string ManifestJson = "later-trace-field-ablation-manifest.json";

    public static IReadOnlyList<string> All { get; } =
    [
        ReportJson,
        RunsJsonLines,
        ConditionsCsv,
        DeltasCsv,
        ContributionsCsv,
        ManifestJson
    ];
}

public sealed class LaterTraceFieldAblationOutputWriter
{
    private static JsonSerializerOptions CompactJsonOptions { get; } =
        new(JsonDefaults.Options) { WriteIndented = false };

    public async Task WriteAsync(
        string outputDirectory,
        LaterTraceFieldAblationEvaluationResult result,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputDirectory);
        ArgumentNullException.ThrowIfNull(result);
        Directory.CreateDirectory(outputDirectory);

        await Write(LaterTraceFieldAblationOutputNames.ReportJson, JsonDefaults.Serialize(result) + "\n");
        await Write(
            LaterTraceFieldAblationOutputNames.RunsJsonLines,
            string.Join("\n", result.Runs.Select(item => JsonSerializer.Serialize(item, CompactJsonOptions))) + "\n");
        await Write(LaterTraceFieldAblationOutputNames.ConditionsCsv, ConditionsCsv(result));
        await Write(LaterTraceFieldAblationOutputNames.DeltasCsv, DeltasCsv(result));
        await Write(LaterTraceFieldAblationOutputNames.ContributionsCsv, ContributionsCsv(result));
        await Write(
            LaterTraceFieldAblationOutputNames.ManifestJson,
            JsonDefaults.Serialize(result.Manifest) + "\n");

        async Task Write(string fileName, string content) => await File.WriteAllTextAsync(
            Path.Combine(outputDirectory, fileName),
            content,
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            cancellationToken);
    }

    private static string ConditionsCsv(LaterTraceFieldAblationEvaluationResult result)
    {
        var builder = new StringBuilder();
        builder.Append(
            "condition,core,fields,missing_fields,derivation_successes,generated_constraints,method," +
            "seeds,mean_candidates,containment,false_exclusion,reduction,generation_work," +
            "verification_work,forward,backward,public_relations,evidence_records,source_groups," +
            "safety_equivalent,baseline_equivalent")
            .Append('\n');
        foreach (var profile in result.ProfileAggregates)
        {
            foreach (var method in profile.Methods)
            {
                Append(builder,
                [
                    profile.ProfileId,
                    Boolean(profile.IsCore),
                    Join(profile.AvailableFieldIds),
                    Join(profile.MissingFieldIds),
                    Integer(profile.DerivationSuccessCount),
                    Decimal(profile.MeanGeneratedConstraintCount),
                    EnumText(method.Method),
                    Integer(profile.SeedCount),
                    Decimal(method.MeanCandidateCount),
                    Decimal(method.ContainmentRate),
                    Integer(method.FalseExclusionCount),
                    Decimal(method.MeanReduction),
                    Decimal(method.MeanCandidateGenerationWork),
                    Decimal(method.MeanCandidateVerificationWork),
                    Decimal(method.MeanForwardTraversalWork),
                    Decimal(method.MeanBackwardTraversalWork),
                    Decimal(method.MeanPublicRelationsInspected),
                    Decimal(method.MeanEvidenceRecordsInspected),
                    Decimal(method.MeanSourceGroupsInspected),
                    Boolean(profile.SafetyEquivalentToFull),
                    Boolean(profile.BaselineEquivalent)
                ]);
            }
        }

        return builder.ToString();
    }

    private static string DeltasCsv(LaterTraceFieldAblationEvaluationResult result)
    {
        var builder = new StringBuilder();
        builder.Append(
            "condition,method,delta_candidates,delta_containment,delta_false_exclusion," +
            "delta_reduction,delta_generation_work,delta_verification_work,delta_forward," +
            "delta_backward,delta_public_relations,delta_evidence_records")
            .Append('\n');
        foreach (var item in result.DeltasFromFull)
        {
            Append(builder,
            [
                item.ProfileId,
                EnumText(item.Method),
                Decimal(item.MeanCandidateCountDelta),
                Decimal(item.ContainmentRateDelta),
                Integer(item.FalseExclusionCountDelta),
                Decimal(item.MeanReductionDelta),
                Decimal(item.CandidateGenerationWorkDelta),
                Decimal(item.CandidateVerificationWorkDelta),
                Decimal(item.ForwardTraversalWorkDelta),
                Decimal(item.BackwardTraversalWorkDelta),
                Decimal(item.PublicRelationsInspectedDelta),
                Decimal(item.EvidenceRecordsInspectedDelta)
            ]);
        }

        return builder.ToString();
    }

    private static string ContributionsCsv(LaterTraceFieldAblationEvaluationResult result)
    {
        var builder = new StringBuilder();
        builder.Append(
            "field_id,removal_profile,presence_profile,removal_prevents_derivation," +
            "presence_enables_derivation,removal_method_deltas")
            .Append('\n');
        foreach (var item in result.FieldContributions)
        {
            Append(builder,
            [
                item.FieldId,
                item.RemovalProfileId,
                item.PresenceProfileId,
                Boolean(item.RemovalPreventsDerivation),
                Boolean(item.PresenceEnablesDerivation),
                JsonSerializer.Serialize(item.RemovalDeltas, CompactJsonOptions)
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

    private static string Decimal(decimal value) => value.ToString(
        "0.############################",
        CultureInfo.InvariantCulture);

    private static string Boolean(bool value) => value ? "true" : "false";
}
