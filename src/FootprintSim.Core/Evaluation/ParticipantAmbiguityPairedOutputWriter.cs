using System.Globalization;
using System.Text;
using System.Text.Json;
using FootprintSim.Serialization;

namespace FootprintSim.Evaluation;

public static class ParticipantAmbiguityPairedOutputNames
{
    public const string ReportJson = "participant-ambiguity-paired.json";
    public const string RunsJsonLines = "participant-ambiguity-paired-runs.jsonl";
    public const string ProfilesCsv = "participant-ambiguity-paired.csv";
    public const string MonotonicityCsv = "participant-ambiguity-paired-monotonicity.csv";
    public const string ManifestJson = "participant-ambiguity-paired-manifest.json";

    public static IReadOnlyList<string> All { get; } =
    [
        ReportJson,
        RunsJsonLines,
        ProfilesCsv,
        MonotonicityCsv,
        ManifestJson
    ];
}

public sealed class ParticipantAmbiguityPairedOutputWriter
{
    private static JsonSerializerOptions CompactJsonOptions { get; } =
        new(JsonDefaults.Options) { WriteIndented = false };

    public async Task WriteAsync(
        string outputDirectory,
        ParticipantAmbiguityPairedEvaluationResult result,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputDirectory);
        ArgumentNullException.ThrowIfNull(result);
        Directory.CreateDirectory(outputDirectory);

        await Write(ParticipantAmbiguityPairedOutputNames.ReportJson, JsonDefaults.Serialize(result) + "\n");
        await Write(
            ParticipantAmbiguityPairedOutputNames.RunsJsonLines,
            string.Join("\n", result.Runs.Select(item =>
                JsonSerializer.Serialize(item, CompactJsonOptions))) + "\n");
        await Write(ParticipantAmbiguityPairedOutputNames.ProfilesCsv, ProfilesCsv(result));
        await Write(
            ParticipantAmbiguityPairedOutputNames.MonotonicityCsv,
            MonotonicityCsv(result));
        await Write(
            ParticipantAmbiguityPairedOutputNames.ManifestJson,
            JsonDefaults.Serialize(result.Manifest) + "\n");

        async Task Write(string fileName, string content) => await File.WriteAllTextAsync(
            Path.Combine(outputDirectory, fileName),
            content,
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            cancellationToken);
    }

    private static string ProfilesCsv(ParticipantAmbiguityPairedEvaluationResult result)
    {
        var builder = new StringBuilder();
        builder.Append(
            "profile,information_order,seeds,mean_candidates,median_candidates,min_candidates," +
            "max_candidates,mean_derived_regions,median_derived_regions,containment," +
            "false_exclusion,mean_reduction_count_vs_unknown," +
            "mean_reduction_fraction_vs_unknown,mean_participant_alternatives," +
            "mean_derivation_alternatives_evaluated,derivation_successes,derivation_failures," +
            "generation_work,participant_location_evidence_inspected," +
            "encounter_source_groups,participant_evidence_source_groups," +
            "generated_constraint_source_groups")
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
                Decimal(item.MeanDerivedRegionCount),
                Decimal(item.MedianDerivedRegionCount),
                Decimal(item.ContainmentRate),
                Integer(item.FalseExclusionCount),
                Decimal(item.MeanCandidateReductionCountVsUnknown),
                Decimal(item.MeanCandidateReductionFractionVsUnknown),
                Decimal(item.MeanParticipantAlternativeCount),
                Decimal(item.MeanParticipantAlternativesEvaluated),
                Integer(item.DerivationSuccessCount),
                Integer(item.DerivationFailureCount),
                Decimal(item.MeanCandidateGenerationWork),
                Decimal(item.MeanParticipantLocationEvidenceRecordsInspected),
                Decimal(item.MeanEncounterSourceGroupCount),
                Decimal(item.MeanParticipantEvidenceSourceGroupCount),
                Decimal(item.MeanGeneratedConstraintSourceGroupCount)
            ]);
        }

        return builder.ToString();
    }

    private static string MonotonicityCsv(ParticipantAmbiguityPairedEvaluationResult result)
    {
        var builder = new StringBuilder();
        builder.Append(
            "seed,more_precise,coarser,more_precise_participants,coarser_participants," +
            "participant_subset,participant_strictly_widened,more_precise_regions," +
            "coarser_regions,region_subset,regions_equal,region_strictly_widened," +
            "more_precise_candidates,coarser_candidates,candidate_subset,candidates_equal," +
            "classification")
            .Append('\n');
        foreach (var item in result.Runs.SelectMany(run => run.MonotonicityAudits))
        {
            Append(builder,
            [
                Integer(item.Seed),
                item.MorePreciseProfileId,
                item.CoarserProfileId,
                Join(item.MorePreciseParticipantIds),
                Join(item.CoarserParticipantIds),
                Boolean(item.ParticipantSetIsSubset),
                Boolean(item.ParticipantSetStrictlyWidened),
                Join(item.MorePreciseRegionIds),
                Join(item.CoarserRegionIds),
                Boolean(item.DerivedRegionSetIsSubset),
                Boolean(item.DerivedRegionSetsEqual),
                Boolean(item.DerivedRegionSetStrictlyWidened),
                Join(item.MorePreciseCandidateIds),
                Join(item.CoarserCandidateIds),
                Boolean(item.CandidateSetIsSubset),
                Boolean(item.CandidateSetsEqual),
                EnumText(item.Classification)
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
