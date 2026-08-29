using System.Globalization;
using System.Text;
using System.Text.Json;
using FootprintSim.Serialization;

namespace FootprintSim.Evaluation;

public static class EvaluationOutputNames
{
    public const string RunsJsonLines = "runs.jsonl";
    public const string RunsCsv = "runs.csv";
    public const string AggregateJson = "aggregate.json";
    public const string AggregateCsv = "aggregate.csv";
    public const string ManifestJson = "manifest.json";

    public static IReadOnlyList<string> All { get; } =
    [
        RunsJsonLines,
        RunsCsv,
        AggregateJson,
        AggregateCsv,
        ManifestJson
    ];
}

public sealed class EvaluationOutputWriter
{
    public const string RunsCsvHeader =
        "schema_version,scenario_id,seed,strategy_id,produced_at_tick,evidence_boundary_state," +
        "candidate_regions,confirmed_person_regions,explicitly_excluded_regions,rationale_codes," +
        "candidate_region_provenance," +
        "searchable_region_count,ground_truth_containment_available,ground_truth_containment_value," +
        "ground_truth_containment_numerator,ground_truth_containment_denominator," +
        "ground_truth_containment_unavailable_reason,candidate_region_count_available," +
        "candidate_region_count_value,candidate_region_count_unavailable_reason," +
        "search_space_fraction_available,search_space_fraction_value,search_space_fraction_numerator," +
        "search_space_fraction_denominator,search_space_fraction_unavailable_reason," +
        "search_space_reduction_available,search_space_reduction_value,search_space_reduction_numerator," +
        "search_space_reduction_denominator,search_space_reduction_unavailable_reason," +
        "false_exclusion_available,false_exclusion_value,false_exclusion_numerator," +
        "false_exclusion_denominator,false_exclusion_unavailable_reason," +
        "explicit_false_exclusion_available,explicit_false_exclusion_value," +
        "explicit_false_exclusion_numerator,explicit_false_exclusion_denominator," +
        "explicit_false_exclusion_unavailable_reason,device_only_promotion_violation_available," +
        "device_only_promotion_violation_value,device_only_promotion_violation_unavailable_reason," +
        "time_to_useful_guidance_available,time_to_useful_guidance_value," +
        "time_to_useful_guidance_unavailable_reason,final_guidance_useful_available," +
        "final_guidance_useful_value,final_guidance_useful_numerator," +
        "final_guidance_useful_denominator,final_guidance_useful_unavailable_reason," +
        "useful_guidance_tick_count_available,useful_guidance_tick_count_value," +
        "useful_guidance_tick_count_unavailable_reason,evaluated_tick_count_available," +
        "evaluated_tick_count_value,evaluated_tick_count_unavailable_reason," +
        "useful_guidance_persistence_fraction_available," +
        "useful_guidance_persistence_fraction_value," +
        "useful_guidance_persistence_fraction_numerator," +
        "useful_guidance_persistence_fraction_denominator," +
        "useful_guidance_persistence_fraction_unavailable_reason," +
        "longest_useful_guidance_streak_available,longest_useful_guidance_streak_value," +
        "longest_useful_guidance_streak_unavailable_reason," +
        "guidance_regressed_after_useful_available,guidance_regressed_after_useful_value," +
        "guidance_regressed_after_useful_numerator,guidance_regressed_after_useful_denominator," +
        "guidance_regressed_after_useful_unavailable_reason,realized_person_final_region," +
        "realized_device_final_regions,secondary_transport_event_count,truth_outcome_identifier";

    public const string AggregateCsvHeader =
        "schema_version,scenario_id,strategy_id,run_count,metric_complete_run_count," +
        "containment_available_count,containment_numerator,containment_denominator,containment_rate," +
        "false_exclusion_available_count,false_exclusion_numerator,false_exclusion_denominator," +
        "false_exclusion_rate,explicit_false_exclusion_available_count," +
        "explicit_false_exclusion_numerator,explicit_false_exclusion_denominator," +
        "explicit_false_exclusion_rate,device_only_promotion_violation_count," +
        "candidate_count_available_count,candidate_count_mean,candidate_count_median," +
        "candidate_count_min,candidate_count_max,search_fraction_available_count," +
        "search_fraction_mean,search_fraction_median,search_fraction_min,search_fraction_max," +
        "search_reduction_available_count,search_reduction_mean,search_reduction_median," +
        "search_reduction_min,search_reduction_max,time_guidance_available_count," +
        "time_guidance_mean,time_guidance_median,time_guidance_min,time_guidance_max," +
        "final_guidance_available_count,final_guidance_numerator,final_guidance_denominator," +
        "final_guidance_rate,useful_tick_count_available_count,useful_tick_count_mean," +
        "useful_tick_count_median,useful_tick_count_min,useful_tick_count_max," +
        "evaluated_tick_count_available_count,evaluated_tick_count_mean," +
        "evaluated_tick_count_median,evaluated_tick_count_min,evaluated_tick_count_max," +
        "persistence_fraction_available_count,persistence_fraction_mean," +
        "persistence_fraction_median,persistence_fraction_min,persistence_fraction_max," +
        "longest_streak_available_count,longest_streak_mean,longest_streak_median," +
        "longest_streak_min,longest_streak_max,regressed_available_count," +
        "regressed_numerator,regressed_denominator,regressed_rate," +
        "evidence_confirmed_count,evidence_supported_but_uncertain_count,evidence_conflicting_count," +
        "evidence_indeterminate_count,evidence_unsupported_for_query_count,unique_outcome_count," +
        "seed_sensitivity_note,provenance_source_kind_counts,truth_seed_count," +
        "person_final_region_counts,device_final_region_counts,unique_truth_outcome_count," +
        "secondary_transport_run_count,secondary_transport_event_count";

    private static JsonSerializerOptions CompactJsonOptions { get; } =
        new(JsonDefaults.Options) { WriteIndented = false };

    public async Task WriteAsync(
        string outputDirectory,
        BatchEvaluationResult result,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputDirectory);
        ArgumentNullException.ThrowIfNull(result);
        Directory.CreateDirectory(outputDirectory);

        var jsonLines = string.Join(
            "\n",
            result.Runs.Select(run => JsonSerializer.Serialize(run, CompactJsonOptions))) + "\n";
        await WriteAsync(EvaluationOutputNames.RunsJsonLines, jsonLines);
        await WriteAsync(EvaluationOutputNames.RunsCsv, CreateRunsCsv(result.Runs));
        await WriteAsync(
            EvaluationOutputNames.AggregateJson,
            JsonDefaults.Serialize(result.Aggregate) + "\n");
        await WriteAsync(EvaluationOutputNames.AggregateCsv, CreateAggregateCsv(result.Aggregate));
        await WriteAsync(
            EvaluationOutputNames.ManifestJson,
            JsonDefaults.Serialize(result.Manifest) + "\n");

        async Task WriteAsync(string fileName, string content)
        {
            await File.WriteAllTextAsync(
                Path.Combine(outputDirectory, fileName),
                content,
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
                cancellationToken);
        }
    }

    private static string CreateRunsCsv(IEnumerable<PerRunEvaluation> runs)
    {
        var builder = new StringBuilder();
        builder.Append(RunsCsvHeader).Append('\n');
        foreach (var run in runs)
        {
            AppendCsvRow(builder,
            [
                run.SchemaVersion.ToString(CultureInfo.InvariantCulture),
                run.ScenarioId,
                run.Seed.ToString(CultureInfo.InvariantCulture),
                run.StrategyId,
                run.ProducedAtTick.ToString(CultureInfo.InvariantCulture),
                EnumText(run.EvidenceBoundaryState),
                Join(run.CandidateRegionIds),
                Join(run.ConfirmedPersonRegionIds),
                Join(run.ExplicitlyExcludedRegionIds),
                Join(run.RationaleCodes),
                JsonSerializer.Serialize(run.CandidateRegionProvenance, CompactJsonOptions),
                run.SearchableRegionCount.ToString(CultureInfo.InvariantCulture),
                Boolean(run.GroundTruthContainment.Available),
                Boolean(run.GroundTruthContainment.Value),
                Integer(run.GroundTruthContainment.Numerator),
                Integer(run.GroundTruthContainment.Denominator),
                run.GroundTruthContainment.UnavailableReason,
                Boolean(run.CandidateRegionCount.Available),
                Integer(run.CandidateRegionCount.Value),
                run.CandidateRegionCount.UnavailableReason,
                Boolean(run.SearchSpaceFraction.Available),
                Decimal(run.SearchSpaceFraction.Value),
                Integer(run.SearchSpaceFraction.Numerator),
                Integer(run.SearchSpaceFraction.Denominator),
                run.SearchSpaceFraction.UnavailableReason,
                Boolean(run.SearchSpaceReduction.Available),
                Decimal(run.SearchSpaceReduction.Value),
                Integer(run.SearchSpaceReduction.Numerator),
                Integer(run.SearchSpaceReduction.Denominator),
                run.SearchSpaceReduction.UnavailableReason,
                Boolean(run.FalseExclusion.Available),
                Boolean(run.FalseExclusion.Value),
                Integer(run.FalseExclusion.Numerator),
                Integer(run.FalseExclusion.Denominator),
                run.FalseExclusion.UnavailableReason,
                Boolean(run.ExplicitFalseExclusion.Available),
                Boolean(run.ExplicitFalseExclusion.Value),
                Integer(run.ExplicitFalseExclusion.Numerator),
                Integer(run.ExplicitFalseExclusion.Denominator),
                run.ExplicitFalseExclusion.UnavailableReason,
                Boolean(run.DeviceOnlyPromotionViolation.Available),
                Integer(run.DeviceOnlyPromotionViolation.Value),
                run.DeviceOnlyPromotionViolation.UnavailableReason,
                Boolean(run.TimeToUsefulGuidance.Available),
                Long(run.TimeToUsefulGuidance.Value),
                run.TimeToUsefulGuidance.UnavailableReason,
                Boolean(run.FinalGuidanceUseful.Available),
                Boolean(run.FinalGuidanceUseful.Value),
                Integer(run.FinalGuidanceUseful.Numerator),
                Integer(run.FinalGuidanceUseful.Denominator),
                run.FinalGuidanceUseful.UnavailableReason,
                Boolean(run.UsefulGuidanceTickCount.Available),
                Integer(run.UsefulGuidanceTickCount.Value),
                run.UsefulGuidanceTickCount.UnavailableReason,
                Boolean(run.EvaluatedTickCount.Available),
                Integer(run.EvaluatedTickCount.Value),
                run.EvaluatedTickCount.UnavailableReason,
                Boolean(run.UsefulGuidancePersistenceFraction.Available),
                Decimal(run.UsefulGuidancePersistenceFraction.Value),
                Integer(run.UsefulGuidancePersistenceFraction.Numerator),
                Integer(run.UsefulGuidancePersistenceFraction.Denominator),
                run.UsefulGuidancePersistenceFraction.UnavailableReason,
                Boolean(run.LongestUsefulGuidanceStreak.Available),
                Integer(run.LongestUsefulGuidanceStreak.Value),
                run.LongestUsefulGuidanceStreak.UnavailableReason,
                Boolean(run.GuidanceRegressedAfterUseful.Available),
                Boolean(run.GuidanceRegressedAfterUseful.Value),
                Integer(run.GuidanceRegressedAfterUseful.Numerator),
                Integer(run.GuidanceRegressedAfterUseful.Denominator),
                run.GuidanceRegressedAfterUseful.UnavailableReason,
                run.RealizedPersonFinalRegionId,
                Join(run.RealizedDeviceFinalRegionIds.Select(item => $"{item.Key}={item.Value}")),
                run.SecondaryTransportEventCount.ToString(CultureInfo.InvariantCulture),
                run.TruthOutcomeIdentifier
            ]);
        }

        return builder.ToString();
    }

    private static string CreateAggregateCsv(AggregateReport report)
    {
        var builder = new StringBuilder();
        builder.Append(AggregateCsvHeader).Append('\n');
        foreach (var group in report.Groups)
        {
            var truth = report.TruthGroups.Single(item => string.Equals(
                item.ScenarioId,
                group.ScenarioId,
                StringComparison.Ordinal));
            AppendCsvRow(builder,
            [
                report.SchemaVersion.ToString(CultureInfo.InvariantCulture),
                group.ScenarioId,
                group.StrategyId,
                group.RunCount.ToString(CultureInfo.InvariantCulture),
                group.MetricCompleteRunCount.ToString(CultureInfo.InvariantCulture),
                group.GroundTruthContainment.AvailableCount.ToString(CultureInfo.InvariantCulture),
                group.GroundTruthContainment.Numerator.ToString(CultureInfo.InvariantCulture),
                group.GroundTruthContainment.Denominator.ToString(CultureInfo.InvariantCulture),
                Decimal(group.GroundTruthContainment.Rate),
                group.FalseExclusion.AvailableCount.ToString(CultureInfo.InvariantCulture),
                group.FalseExclusion.Numerator.ToString(CultureInfo.InvariantCulture),
                group.FalseExclusion.Denominator.ToString(CultureInfo.InvariantCulture),
                Decimal(group.FalseExclusion.Rate),
                group.ExplicitFalseExclusion.AvailableCount.ToString(CultureInfo.InvariantCulture),
                group.ExplicitFalseExclusion.Numerator.ToString(CultureInfo.InvariantCulture),
                group.ExplicitFalseExclusion.Denominator.ToString(CultureInfo.InvariantCulture),
                Decimal(group.ExplicitFalseExclusion.Rate),
                group.DeviceOnlyPromotionViolationCount.ToString(CultureInfo.InvariantCulture),
                group.CandidateRegionCount.AvailableCount.ToString(CultureInfo.InvariantCulture),
                Decimal(group.CandidateRegionCount.Mean),
                Decimal(group.CandidateRegionCount.Median),
                Decimal(group.CandidateRegionCount.Minimum),
                Decimal(group.CandidateRegionCount.Maximum),
                group.SearchSpaceFraction.AvailableCount.ToString(CultureInfo.InvariantCulture),
                Decimal(group.SearchSpaceFraction.Mean),
                Decimal(group.SearchSpaceFraction.Median),
                Decimal(group.SearchSpaceFraction.Minimum),
                Decimal(group.SearchSpaceFraction.Maximum),
                group.SearchSpaceReduction.AvailableCount.ToString(CultureInfo.InvariantCulture),
                Decimal(group.SearchSpaceReduction.Mean),
                Decimal(group.SearchSpaceReduction.Median),
                Decimal(group.SearchSpaceReduction.Minimum),
                Decimal(group.SearchSpaceReduction.Maximum),
                group.TimeToUsefulGuidance.AvailableCount.ToString(CultureInfo.InvariantCulture),
                Decimal(group.TimeToUsefulGuidance.Mean),
                Decimal(group.TimeToUsefulGuidance.Median),
                Decimal(group.TimeToUsefulGuidance.Minimum),
                Decimal(group.TimeToUsefulGuidance.Maximum),
                group.FinalGuidanceUseful.AvailableCount.ToString(CultureInfo.InvariantCulture),
                group.FinalGuidanceUseful.Numerator.ToString(CultureInfo.InvariantCulture),
                group.FinalGuidanceUseful.Denominator.ToString(CultureInfo.InvariantCulture),
                Decimal(group.FinalGuidanceUseful.Rate),
                group.UsefulGuidanceTickCount.AvailableCount.ToString(CultureInfo.InvariantCulture),
                Decimal(group.UsefulGuidanceTickCount.Mean),
                Decimal(group.UsefulGuidanceTickCount.Median),
                Decimal(group.UsefulGuidanceTickCount.Minimum),
                Decimal(group.UsefulGuidanceTickCount.Maximum),
                group.EvaluatedTickCount.AvailableCount.ToString(CultureInfo.InvariantCulture),
                Decimal(group.EvaluatedTickCount.Mean),
                Decimal(group.EvaluatedTickCount.Median),
                Decimal(group.EvaluatedTickCount.Minimum),
                Decimal(group.EvaluatedTickCount.Maximum),
                group.UsefulGuidancePersistenceFraction.AvailableCount.ToString(
                    CultureInfo.InvariantCulture),
                Decimal(group.UsefulGuidancePersistenceFraction.Mean),
                Decimal(group.UsefulGuidancePersistenceFraction.Median),
                Decimal(group.UsefulGuidancePersistenceFraction.Minimum),
                Decimal(group.UsefulGuidancePersistenceFraction.Maximum),
                group.LongestUsefulGuidanceStreak.AvailableCount.ToString(
                    CultureInfo.InvariantCulture),
                Decimal(group.LongestUsefulGuidanceStreak.Mean),
                Decimal(group.LongestUsefulGuidanceStreak.Median),
                Decimal(group.LongestUsefulGuidanceStreak.Minimum),
                Decimal(group.LongestUsefulGuidanceStreak.Maximum),
                group.GuidanceRegressedAfterUseful.AvailableCount.ToString(
                    CultureInfo.InvariantCulture),
                group.GuidanceRegressedAfterUseful.Numerator.ToString(
                    CultureInfo.InvariantCulture),
                group.GuidanceRegressedAfterUseful.Denominator.ToString(
                    CultureInfo.InvariantCulture),
                Decimal(group.GuidanceRegressedAfterUseful.Rate),
                group.EvidenceBoundaryStateCounts.Confirmed.ToString(CultureInfo.InvariantCulture),
                group.EvidenceBoundaryStateCounts.SupportedButUncertain.ToString(
                    CultureInfo.InvariantCulture),
                group.EvidenceBoundaryStateCounts.Conflicting.ToString(CultureInfo.InvariantCulture),
                group.EvidenceBoundaryStateCounts.Indeterminate.ToString(CultureInfo.InvariantCulture),
                group.EvidenceBoundaryStateCounts.UnsupportedForQuery.ToString(
                    CultureInfo.InvariantCulture),
                group.UniqueOutcomeCount.ToString(CultureInfo.InvariantCulture),
                group.SeedSensitivityNote,
                Join(group.ProvenanceSourceKindCounts.Select(item =>
                    $"{EnumText(item.SourceKind)}={item.Count.ToString(CultureInfo.InvariantCulture)}")),
                truth.SeedCount.ToString(CultureInfo.InvariantCulture),
                Join(truth.PersonFinalRegionCounts.Select(item =>
                    $"{item.RegionId}={item.Count.ToString(CultureInfo.InvariantCulture)}")),
                Join(truth.DeviceFinalRegionCounts.Select(item =>
                    $"{item.DeviceId}:{item.RegionId}={item.Count.ToString(CultureInfo.InvariantCulture)}")),
                truth.UniqueTruthOutcomeCount.ToString(CultureInfo.InvariantCulture),
                truth.SecondaryTransportRunCount.ToString(CultureInfo.InvariantCulture),
                truth.SecondaryTransportEventCount.ToString(CultureInfo.InvariantCulture)
            ]);
        }

        return builder.ToString();
    }

    private static void AppendCsvRow(StringBuilder builder, IEnumerable<string?> values)
    {
        builder.AppendJoin(',', values.Select(EscapeCsv)).Append('\n');
    }

    private static string EscapeCsv(string? value)
    {
        value ??= string.Empty;
        return value.IndexOfAny([',', '"', '\r', '\n']) < 0
            ? value
            : $"\"{value.Replace("\"", "\"\"")}\"";
    }

    private static string Join(IEnumerable<string> values) => string.Join('|', values);

    private static string Boolean(bool value) => value ? "true" : "false";

    private static string Boolean(bool? value) => value.HasValue ? Boolean(value.Value) : string.Empty;

    private static string Integer(int? value) =>
        value?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;

    private static string Long(long? value) =>
        value?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;

    private static string Decimal(decimal? value) =>
        value?.ToString("0.############################", CultureInfo.InvariantCulture) ?? string.Empty;

    private static string EnumText<T>(T value) where T : struct, Enum =>
        JsonNamingPolicy.CamelCase.ConvertName(value.ToString());
}
