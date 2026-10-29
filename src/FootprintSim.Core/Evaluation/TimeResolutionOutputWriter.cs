using System.Globalization;
using System.Text;
using System.Text.Json;
using FootprintSim.Interactions;
using FootprintSim.Serialization;

namespace FootprintSim.Evaluation;

public static class TimeResolutionOutputNames
{
    public const string ReportJson = "time-resolution.json";
    public const string RunsJsonLines = "time-resolution-runs.jsonl";
    public const string LevelsCsv = "time-resolution.csv";
    public const string DeltasCsv = "time-resolution-deltas.csv";
    public const string LineageCsv = "time-resolution-lineage.csv";
    public const string MonotonicityCsv = "time-resolution-monotonicity.csv";
    public const string ManifestJson = "time-resolution-manifest.json";

    public static IReadOnlyList<string> All { get; } =
    [
        ReportJson,
        RunsJsonLines,
        LevelsCsv,
        DeltasCsv,
        LineageCsv,
        MonotonicityCsv,
        ManifestJson
    ];
}

public sealed class TimeResolutionOutputWriter
{
    private static JsonSerializerOptions CompactJsonOptions { get; } =
        new(JsonDefaults.Options) { WriteIndented = false };

    public async Task WriteAsync(
        string outputDirectory,
        TimeResolutionEvaluationResult result,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputDirectory);
        ArgumentNullException.ThrowIfNull(result);
        Directory.CreateDirectory(outputDirectory);

        await Write(TimeResolutionOutputNames.ReportJson, JsonDefaults.Serialize(result) + "\n");
        await Write(
            TimeResolutionOutputNames.RunsJsonLines,
            string.Join("\n", result.Runs.Select(item => JsonSerializer.Serialize(item, CompactJsonOptions))) + "\n");
        await Write(TimeResolutionOutputNames.LevelsCsv, LevelsCsv(result));
        await Write(TimeResolutionOutputNames.DeltasCsv, DeltasCsv(result));
        await Write(TimeResolutionOutputNames.LineageCsv, LineageCsv(result));
        await Write(TimeResolutionOutputNames.MonotonicityCsv, MonotonicityCsv(result));
        await Write(TimeResolutionOutputNames.ManifestJson, JsonDefaults.Serialize(result.Manifest) + "\n");

        async Task Write(string fileName, string content) => await File.WriteAllTextAsync(
            Path.Combine(outputDirectory, fileName),
            content,
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            cancellationToken);
    }

    private static string LevelsCsv(TimeResolutionEvaluationResult result)
    {
        var builder = new StringBuilder();
        builder.Append(
            "time_condition,information_order,observed_start,observed_end,window_span_ticks," +
            "possible_tick_count,derived_windows,derived_tick_count,affected_state_intervals," +
            "constraint_runs,seeds,method,mean_candidates,containment,false_exclusion,reduction," +
            "generation_work,verification_work,forward,backward,evidence_records,public_relations," +
            "source_groups,candidate_output_equivalent,baseline_equivalent")
            .Append('\n');
        foreach (var profile in result.Aggregates)
        {
            foreach (var method in profile.Methods)
            {
                Append(builder,
                [
                    profile.LevelId,
                    Integer(profile.InformationOrder),
                    NullableLong(profile.ObservedStartTick),
                    NullableLong(profile.ObservedEndTick),
                    Integer(profile.ObservedWindowSpanTicks),
                    Integer(profile.PossibleObservationTickCount),
                    Windows(profile.DerivedHistoricalWindows),
                    Integer(profile.DerivedHistoricalPossibleTickCount),
                    Integer(profile.AffectedCandidateStateIntervalCount),
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
                    Boolean(profile.CandidateOutputEquivalentToCurrent),
                    Boolean(profile.BaselineEquivalentToCurrent)
                ]);
            }
        }

        return builder.ToString();
    }

    private static string DeltasCsv(TimeResolutionEvaluationResult result)
    {
        var builder = new StringBuilder();
        builder.Append(
            "time_condition,method,delta_candidates,delta_containment,delta_false_exclusion," +
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

    private static string LineageCsv(TimeResolutionEvaluationResult result)
    {
        var builder = new StringBuilder();
        builder.Append(
            "seed,time_condition,time_meaning,observed_time_available,observed_start,observed_end," +
            "possible_ticks,underlying_source_id,underlying_source_resolved,projected_source_id," +
            "projected_source_emitted,projected_source_resolved,locations,derived_windows," +
            "derived_regions,constraint_generated,constraint_definition_ids,constraint_ids,constraint_source_windows," +
            "subjects,relation,available_tick,evidence_state,ancestry")
            .Append('\n');
        foreach (var item in result.Runs)
        {
            Append(builder,
            [
                Integer(item.Seed),
                item.LevelId,
                EnumText(item.TimeWindowMeaning),
                Boolean(item.ObservedTimeAvailable),
                NullableLong(item.ObservedStartTick),
                NullableLong(item.ObservedEndTick),
                Join(item.PossibleObservationTicks.Select(Long)),
                item.UnderlyingSourceObservationId,
                Boolean(item.UnderlyingSourceObservationResolved),
                item.ProjectedSourceObservationId,
                Boolean(item.ProjectedSourceObservationEmitted),
                Boolean(item.ProjectedSourceObservationResolved),
                Join(item.ObservedRegionIds),
                Windows(item.DerivedHistoricalWindows),
                Join(item.DerivedAllowedRegionIds),
                Boolean(item.ConstraintGenerated),
                Join(item.ConstraintDefinitionIds),
                Join(item.ConstraintIds),
                Windows(item.ConstraintSourceObservedWindows),
                Join(item.ObservedSubjectIds),
                EnumText(item.ObservedRelationKind),
                Long(item.AvailableAtTick),
                EnumText(item.EvidenceState),
                Join(item.SourceAncestryIds)
            ]);
        }

        return builder.ToString();
    }

    private static string MonotonicityCsv(TimeResolutionEvaluationResult result)
    {
        var builder = new StringBuilder();
        builder.Append(
            "seed,more_precise,coarser,more_precise_candidates,coarser_candidates,is_subset")
            .Append('\n');
        foreach (var item in result.MonotonicityAudits)
        {
            Append(builder,
            [
                Integer(item.Seed),
                item.MorePreciseLevelId,
                item.CoarserLevelId,
                Join(item.MorePreciseCandidateRegionIds),
                Join(item.CoarserCandidateRegionIds),
                Boolean(item.IsSubset)
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

    private static string Windows(IEnumerable<InteractionTimeWindow> values) => string.Join(
        "|",
        values.Distinct().OrderBy(item => item.StartTick).ThenBy(item => item.EndTick)
            .Select(item => $"[{item.StartTick},{item.EndTick}]"));

    private static string EnumText<T>(T value) where T : struct, Enum =>
        JsonNamingPolicy.CamelCase.ConvertName(value.ToString());

    private static string Integer(int value) => value.ToString(CultureInfo.InvariantCulture);

    private static string Long(long value) => value.ToString(CultureInfo.InvariantCulture);

    private static string NullableLong(long? value) => value?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;

    private static string Decimal(decimal value) => value.ToString(
        "0.############################",
        CultureInfo.InvariantCulture);

    private static string Boolean(bool value) => value ? "true" : "false";
}
