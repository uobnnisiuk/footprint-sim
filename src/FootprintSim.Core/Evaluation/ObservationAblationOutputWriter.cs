using System.Globalization;
using System.Text;
using System.Text.Json;
using FootprintSim.Serialization;

namespace FootprintSim.Evaluation;

public static class ObservationAblationOutputNames
{
    public const string ReportJson = "observation-ablation.json";
    public const string RunsJsonLines = "observation-ablation-runs.jsonl";
    public const string ConditionsCsv = "observation-ablation.csv";
    public const string DeltasCsv = "observation-ablation-deltas.csv";
    public const string ContributionsCsv = "observation-family-contributions.csv";
    public const string ManifestJson = "observation-ablation-manifest.json";

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

public sealed class ObservationAblationOutputWriter
{
    private static JsonSerializerOptions CompactJsonOptions { get; } =
        new(JsonDefaults.Options) { WriteIndented = false };

    public async Task WriteAsync(
        string outputDirectory,
        ObservationAblationEvaluationResult result,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputDirectory);
        ArgumentNullException.ThrowIfNull(result);
        Directory.CreateDirectory(outputDirectory);

        await Write(ObservationAblationOutputNames.ReportJson, JsonDefaults.Serialize(result) + "\n");
        await Write(
            ObservationAblationOutputNames.RunsJsonLines,
            string.Join("\n", result.Runs.Select(item => JsonSerializer.Serialize(item, CompactJsonOptions))) + "\n");
        await Write(ObservationAblationOutputNames.ConditionsCsv, ConditionsCsv(result));
        await Write(ObservationAblationOutputNames.DeltasCsv, DeltasCsv(result));
        await Write(ObservationAblationOutputNames.ContributionsCsv, ContributionsCsv(result));
        await Write(ObservationAblationOutputNames.ManifestJson, JsonDefaults.Serialize(result.Manifest) + "\n");

        async Task Write(string fileName, string content) => await File.WriteAllTextAsync(
            Path.Combine(outputDirectory, fileName),
            content,
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            cancellationToken);
    }

    private static string ConditionsCsv(ObservationAblationEvaluationResult result)
    {
        var builder = new StringBuilder();
        builder.Append(
            "observation_condition,seeds,method,enabled_families,mean_candidates,containment," +
            "false_exclusion,reduction,generation_work,verification_work,forward,backward," +
            "public_relations,evidence_records,source_groups,safety_equivalent,baseline_equivalent")
            .Append('\n');
        foreach (var profile in result.ProfileAggregates)
        {
            foreach (var method in profile.Methods)
            {
                Append(builder,
                [
                    profile.ProfileId,
                    Integer(profile.SeedCount),
                    EnumText(method.Method),
                    Join(profile.EnabledFamilyIds),
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

    private static string DeltasCsv(ObservationAblationEvaluationResult result)
    {
        var builder = new StringBuilder();
        builder.Append(
            "observation_condition,method,removed_from_full,delta_candidates,delta_containment," +
            "delta_false_exclusion,delta_reduction,delta_generation_work,delta_verification_work," +
            "delta_forward,delta_backward,delta_public_relations,delta_evidence_records," +
            "delta_unresolved_candidates,delta_conflicted_candidates")
            .Append('\n');
        foreach (var item in result.DeltasFromFull)
        {
            Append(builder,
            [
                item.ProfileId,
                EnumText(item.Method),
                Join(item.RemovedFromFullFamilyIds),
                Decimal(item.MeanCandidateCountDelta),
                Decimal(item.ContainmentRateDelta),
                Integer(item.FalseExclusionCountDelta),
                Decimal(item.MeanReductionDelta),
                Decimal(item.CandidateGenerationWorkDelta),
                Decimal(item.CandidateVerificationWorkDelta),
                Decimal(item.ForwardTraversalWorkDelta),
                Decimal(item.BackwardTraversalWorkDelta),
                Decimal(item.PublicRelationsInspectedDelta),
                Decimal(item.EvidenceRecordsInspectedDelta),
                Decimal(item.UnresolvedCandidateCountDelta),
                Decimal(item.ConflictedCandidateCountDelta)
            ]);
        }

        return builder.ToString();
    }

    private static string ContributionsCsv(ObservationAblationEvaluationResult result)
    {
        var builder = new StringBuilder();
        builder.Append(
            "family_id,source_keys,source_types,usage,source_ancestry,raw_observation_present_seeds," +
            "active_constraint_seeds,active_constraint_uses,without_profile,only_profile," +
            "independent_verification_disposition_delta,unresolved_verification_disposition_delta," +
            "removal_method_deltas")
            .Append('\n');
        foreach (var item in result.FamilyContributions)
        {
            var inventory = result.Families.Single(value => value.FamilyId == item.FamilyId);
            Append(builder,
            [
                item.FamilyId,
                Join(inventory.SourceObservationKeys),
                Join(inventory.SourceKinds.Select(EnumText)),
                Join(inventory.UsageRoles.Select(EnumText)),
                Join(inventory.SourceAncestryIds),
                Integer(item.RawObservationPresentSeedCount),
                Integer(item.ActiveConstraintSeedCount),
                Integer(item.ActiveConstraintUseCount),
                item.WithoutProfileId,
                item.OnlyProfileId,
                Integer(item.IndependentVerificationDispositionDelta),
                Integer(item.UnresolvedVerificationDispositionDelta),
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

    private static string Decimal(decimal value) => value.ToString("0.############################", CultureInfo.InvariantCulture);

    private static string Boolean(bool value) => value ? "true" : "false";
}
