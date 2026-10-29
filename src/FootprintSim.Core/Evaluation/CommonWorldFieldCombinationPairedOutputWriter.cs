using System.Globalization;
using System.Text;
using System.Text.Json;
using FootprintSim.Serialization;

namespace FootprintSim.Evaluation;

public static class CommonWorldFieldCombinationOutputNames
{
    public const string ReportJson = "common-world-field-combination.json";
    public const string RunsJsonLines = "common-world-field-combination-runs.jsonl";
    public const string ProfilesCsv = "common-world-field-combination-profiles.csv";
    public const string ConditionalReductionsCsv =
        "common-world-field-combination-conditional-reductions.csv";
    public const string PlateauAuditCsv = "common-world-field-combination-plateau-audit.csv";
    public const string SetInclusionCsv = "common-world-field-combination-set-inclusion.csv";
    public const string ProfileEquivalenceCsv =
        "common-world-field-combination-profile-equivalence.csv";
    public const string ManifestJson = "common-world-field-combination-manifest.json";

    public static IReadOnlyList<string> All { get; } =
    [
        ReportJson,
        RunsJsonLines,
        ProfilesCsv,
        ConditionalReductionsCsv,
        PlateauAuditCsv,
        SetInclusionCsv,
        ProfileEquivalenceCsv,
        ManifestJson
    ];
}

public sealed class CommonWorldFieldCombinationPairedOutputWriter
{
    private static JsonSerializerOptions CompactJsonOptions { get; } =
        new(JsonDefaults.Options) { WriteIndented = false };

    public async Task WriteAsync(
        string outputDirectory,
        CommonWorldFieldCombinationEvaluationResult result,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputDirectory);
        ArgumentNullException.ThrowIfNull(result);
        Directory.CreateDirectory(outputDirectory);

        await Write(CommonWorldFieldCombinationOutputNames.ReportJson,
            JsonDefaults.Serialize(result) + "\n");
        await Write(CommonWorldFieldCombinationOutputNames.RunsJsonLines,
            string.Join("\n", result.Runs.Select(item =>
                JsonSerializer.Serialize(item, CompactJsonOptions))) + "\n");
        await Write(CommonWorldFieldCombinationOutputNames.ProfilesCsv, ProfilesCsv(result));
        await Write(CommonWorldFieldCombinationOutputNames.ConditionalReductionsCsv,
            ConditionalReductionsCsv(result));
        await Write(CommonWorldFieldCombinationOutputNames.PlateauAuditCsv,
            PlateauAuditCsv(result));
        await Write(CommonWorldFieldCombinationOutputNames.SetInclusionCsv,
            SetInclusionCsv(result));
        await Write(CommonWorldFieldCombinationOutputNames.ProfileEquivalenceCsv,
            ProfileEquivalenceCsv(result));
        await Write(CommonWorldFieldCombinationOutputNames.ManifestJson,
            JsonDefaults.Serialize(result.Manifest) + "\n");

        async Task Write(string fileName, string content) => await File.WriteAllTextAsync(
            Path.Combine(outputDirectory, fileName),
            content,
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            cancellationToken);
    }

    private static string ProfilesCsv(CommonWorldFieldCombinationEvaluationResult result)
    {
        var builder = Header(
            "profile,order,seeds,mean_candidates,median_candidates,min_candidates,max_candidates," +
            "containment,false_exclusion,mean_reduction_count_vs_none," +
            "mean_reduction_fraction_vs_none,mean_generation_work,mean_components_received," +
            "mean_predicates_applied,mean_candidate_predicate_checks," +
            "mean_supporting_source_groups,mean_selected_fields," +
            "mean_participant_alternatives,duplicates_suppressed,alternative_branches");
        foreach (var item in result.ProfileAggregates.OrderBy(item => item.Order))
        {
            Append(builder,
            [
                item.ProfileId,
                Integer(item.Order),
                Integer(item.SeedCount),
                Decimal(item.MeanCandidateCount),
                Decimal(item.MedianCandidateCount),
                Integer(item.MinimumCandidateCount),
                Integer(item.MaximumCandidateCount),
                Decimal(item.ContainmentRate),
                Integer(item.FalseExclusionCount),
                Decimal(item.MeanReductionCountVsNone),
                Decimal(item.MeanReductionFractionVsNone),
                Decimal(item.MeanCandidateGenerationWork),
                Decimal(item.MeanComponentsReceived),
                Decimal(item.MeanPredicatesApplied),
                Decimal(item.MeanCandidatePredicateChecks),
                Decimal(item.MeanSupportingSourceGroupCount),
                Decimal(item.MeanSelectedFieldCount),
                Decimal(item.MeanParticipantAlternativesEvaluatedWhenSelected),
                Integer(item.DuplicateConstraintsSuppressed),
                Integer(item.AlternativeBranchesEvaluated)
            ]);
        }

        return builder.ToString();
    }

    private static string ConditionalReductionsCsv(
        CommonWorldFieldCombinationEvaluationResult result)
    {
        var builder = Header(
            "seed,comparison,baseline,combined,added_axis,baseline_candidates," +
            "combined_candidates,additional_reduction,candidate_sets_equal," +
            "added_predicate_top,added_predicate_implied,classification");
        foreach (var item in result.Runs.SelectMany(run => run.ConditionalReductions))
        {
            Append(builder,
            [
                Integer(item.Seed),
                item.ComparisonId,
                item.BaselineProfileId,
                item.CombinedProfileId,
                EnumText(item.AddedAxis),
                Integer(item.BaselineCandidateCount),
                Integer(item.CombinedCandidateCount),
                Integer(item.AdditionalCandidateReduction),
                Boolean(item.CandidateSetsEqual),
                Boolean(item.AddedPredicateIsTopForCandidateDomain),
                Boolean(item.AddedPredicateImpliedByBaselinePredicates),
                EnumText(item.Classification)
            ]);
        }

        return builder.ToString();
    }

    private static string PlateauAuditCsv(CommonWorldFieldCombinationEvaluationResult result)
    {
        var builder = Header(
            "seed,comparison,added_axis,zero_additional_effect,classification," +
            "added_predicate_top,added_predicate_implied,baseline_candidates,combined_candidates");
        foreach (var item in result.Runs.SelectMany(run => run.ConditionalReductions))
        {
            Append(builder,
            [
                Integer(item.Seed),
                item.ComparisonId,
                EnumText(item.AddedAxis),
                Boolean(item.CandidateSetsEqual),
                EnumText(item.Classification),
                Boolean(item.AddedPredicateIsTopForCandidateDomain),
                Boolean(item.AddedPredicateImpliedByBaselinePredicates),
                Integer(item.BaselineCandidateCount),
                Integer(item.CombinedCandidateCount)
            ]);
        }

        return builder.ToString();
    }

    private static string SetInclusionCsv(CommonWorldFieldCombinationEvaluationResult result)
    {
        var builder = Header(
            "seed,subset_profile,superset_profile,subset_candidate_ids," +
            "superset_candidate_ids,set_inclusion_holds");
        foreach (var item in result.Runs.SelectMany(run => run.SetInclusionAudits))
        {
            Append(builder,
            [
                Integer(item.Seed),
                item.SubsetProfileId,
                item.SupersetProfileId,
                Join(item.SubsetCandidateIds),
                Join(item.SupersetCandidateIds),
                Boolean(item.CandidateSetIsSubset)
            ]);
        }

        return builder.ToString();
    }

    private static string ProfileEquivalenceCsv(
        CommonWorldFieldCombinationEvaluationResult result)
    {
        var builder = Header("seed,left_profile,right_profile,candidate_sets_equal");
        foreach (var item in result.Runs.SelectMany(run => run.ProfileEquivalences))
        {
            Append(builder,
            [
                Integer(item.Seed),
                item.LeftProfileId,
                item.RightProfileId,
                Boolean(item.CandidateSetsEqual)
            ]);
        }

        return builder.ToString();
    }

    private static StringBuilder Header(string value) => new StringBuilder(value).Append('\n');

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
