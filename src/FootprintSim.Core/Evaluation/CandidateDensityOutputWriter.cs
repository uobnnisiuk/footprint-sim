using System.Globalization;
using System.Text;
using System.Text.Json;
using FootprintSim.Serialization;

namespace FootprintSim.Evaluation;

public static class CandidateDensityOutputNames
{
    public const string ReportJson = "candidate-density.json";
    public const string RunsJsonLines = "candidate-density-runs.jsonl";
    public const string ProfilesCsv = "candidate-density-profiles.csv";
    public const string ConditionalsCsv = "candidate-density-conditionals.csv";
    public const string CrosswalkCsv = "candidate-density-crosswalk.csv";
    public const string ManifestJson = "candidate-density-manifest.json";
    public static IReadOnlyList<string> All { get; } =
        [ReportJson, RunsJsonLines, ProfilesCsv, ConditionalsCsv, CrosswalkCsv, ManifestJson];
}

public sealed class CandidateDensityOutputWriter
{
    public async Task WriteAsync(string outputDirectory, CandidateDensityResult result,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputDirectory);
        ArgumentNullException.ThrowIfNull(result);
        Directory.CreateDirectory(outputDirectory);
        var compact = new JsonSerializerOptions(JsonDefaults.Options) { WriteIndented = false };
        await Write(CandidateDensityOutputNames.ReportJson, JsonDefaults.Serialize(new
        {
            result.Manifest,
            result.Summary,
            result.ArmAggregates,
            result.ProfileAggregates,
            result.ConditionalAggregates,
            result.PairAggregates
        }) + "\n");
        await Write(CandidateDensityOutputNames.RunsJsonLines,
            string.Join("\n", result.Runs.Select(run => JsonSerializer.Serialize(run, compact))) + "\n");
        await Write(CandidateDensityOutputNames.ProfilesCsv, Profiles(result));
        await Write(CandidateDensityOutputNames.ConditionalsCsv, Conditionals(result));
        await Write(CandidateDensityOutputNames.CrosswalkCsv, Crosswalk(result));
        await Write(CandidateDensityOutputNames.ManifestJson, JsonDefaults.Serialize(result.Manifest) + "\n");

        async Task Write(string name, string content) => await File.WriteAllTextAsync(
            Path.Combine(outputDirectory, name), content, new UTF8Encoding(false), cancellationToken);
    }

    private static string Profiles(CandidateDensityResult result)
    {
        var csv = new StringBuilder("seed,arm,profile,universe_count,density,candidate_count," +
            "original_member_count,introduced_member_count,reduction_vs_none,reduction_fraction," +
            "retained_fraction,candidate_keys,candidate_ids,excluded_keys,contains_truth," +
            "false_exclusion_count,candidate_generation_work,work_rule_satisfied,universe_id,composition_id\n");
        foreach (var run in result.Runs)
        {
            foreach (var metric in run.ProfileMetrics)
            {
                var arm = run.Arms.Single(item => item.ArmId == metric.ArmId);
                var profile = arm.Profiles.Single(item => item.ProfileId == metric.ProfileId);
                var occupancy = run.Occupancy.Single(item => item.ArmId == metric.ArmId);
                Append(csv, [Text(run.Seed), metric.ArmId, metric.ProfileId, Text(occupancy.CandidateCount),
                    Text(occupancy.Density), Text(metric.CandidateCount), Text(metric.OriginalMemberCount),
                    Text(metric.IntroducedMemberCount), Text(metric.ReductionVsNone), Text(metric.ReductionFraction),
                    Text(metric.RetainedFraction), Join(profile.CandidateKeys), Join(profile.CandidateIds),
                    Join(metric.ExcludedKeys), Bool(metric.ContainsTruth), Text(metric.FalseExclusionCount),
                    Text(metric.CandidateGenerationWork), Bool(metric.WorkRuleSatisfied), arm.CandidateUniverseId,
                    profile.CompositionId]);
            }
        }

        return csv.ToString();
    }

    private static string Conditionals(CandidateDensityResult result)
    {
        var csv = new StringBuilder("seed,arm,comparison,added_axis,baseline_profile,combined_profile," +
            "baseline_count,combined_count,additional_reduction,reduction_fraction,excluded_keys," +
            "classification,top,implied,implication_applicable,implication_counterexample_regions\n");
        foreach (var run in result.Runs)
        {
            foreach (var metric in run.ConditionalMetrics)
            {
                var conditional = run.Arms.Single(item => item.ArmId == metric.ArmId).Conditionals
                    .Single(item => item.ComparisonId == metric.ComparisonId);
                var diagnostic = conditional.Diagnostic;
                Append(csv, [Text(run.Seed), metric.ArmId, metric.ComparisonId, EnumText(conditional.AddedAxis),
                    conditional.BaselineProfileId, conditional.CombinedProfileId, Text(conditional.BaselineCandidateCount),
                    Text(conditional.CombinedCandidateCount), Text(metric.AdditionalCandidateReduction),
                    Text(metric.ReductionFraction), Join(metric.ExcludedKeys), EnumText(diagnostic.Classification),
                    Bool(diagnostic.AddedPredicateIsTopForCandidateDomain),
                    Bool(diagnostic.AddedPredicateImpliedByBaselinePredicates),
                    Bool(diagnostic.RegionImplicationApplicable), Join(diagnostic.ImplicationCounterexampleRegionIds)]);
            }
        }

        return csv.ToString();
    }

    private static string Crosswalk(CandidateDensityResult result)
    {
        var csv = new StringBuilder("seed,key,introduction_arm,present_arms,source,region,start,end,kind," +
            "state_id,candidate_id,provenance_signature,model_ancestry,original_interval_candidate_id," +
            "original_interval_state_id,original_interval_start,original_interval_end,original_interval_kind\n");
        foreach (var run in result.Runs)
        {
            foreach (var row in run.CandidateCrosswalk)
            {
                var candidate = row.Candidate;
                var source = run.Support.Arms.Last().Candidates.Single(item => item.CandidateKey == row.CandidateKey);
                var bridge = run.IntervalToPointCrosswalk.SingleOrDefault(item => item.CandidateKey == row.CandidateKey);
                var ancestry = source.State.Provenance.SelectMany(item => item.InteractionProvenance)
                    .Select(item => item.SourceIdentifier).Distinct(StringComparer.Ordinal).OrderBy(item => item, StringComparer.Ordinal);
                Append(csv, [Text(run.Seed), row.CandidateKey, row.IntroductionArmId, Join(row.PresentArmIds),
                    candidate.PublicSourceIdentifier, candidate.RegionId, Text(candidate.TimeWindow.StartTick),
                    Text(candidate.TimeWindow.EndTick), EnumText(candidate.TemporalKind), candidate.StateId,
                    candidate.CandidateId, candidate.ProvenanceSignature, Join(ancestry), row.OriginalIntervalCandidateId ?? "",
                    bridge?.Original.StateId ?? "", bridge is null ? "" : Text(bridge.Original.TimeWindow.StartTick),
                    bridge is null ? "" : Text(bridge.Original.TimeWindow.EndTick),
                    bridge is null ? "" : EnumText(bridge.Original.TemporalKind)]);
            }
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
