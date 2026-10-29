using System.Globalization;
using System.Text;
using FootprintSim.Serialization;

namespace FootprintSim.Evaluation;

public static class RelationDependentFixtureOutputNames
{
    public const string ReportJson = "relation-dependent-derivation.json";
    public const string ProfilesCsv = "relation-dependent-derivation.csv";
    public const string ManifestJson = "relation-dependent-derivation-manifest.json";

    public static IReadOnlyList<string> All { get; } =
    [
        ReportJson,
        ProfilesCsv,
        ManifestJson
    ];
}

public sealed class RelationDependentFixtureOutputWriter
{
    public async Task WriteAsync(
        string outputDirectory,
        RelationDependentFixtureEvaluationResult result,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputDirectory);
        ArgumentNullException.ThrowIfNull(result);
        Directory.CreateDirectory(outputDirectory);

        await Write(RelationDependentFixtureOutputNames.ReportJson, JsonDefaults.Serialize(result) + "\n");
        await Write(RelationDependentFixtureOutputNames.ProfilesCsv, ProfilesCsv(result));
        await Write(
            RelationDependentFixtureOutputNames.ManifestJson,
            JsonDefaults.Serialize(result.Manifest) + "\n");

        async Task Write(string fileName, string content) => await File.WriteAllTextAsync(
            Path.Combine(outputDirectory, fileName),
            content,
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            cancellationToken);
    }

    private static string ProfilesCsv(RelationDependentFixtureEvaluationResult result)
    {
        var builder = new StringBuilder();
        builder.Append(
            "profile,information_order,relation_observation_present,relation_knowledge," +
            "exact_relation_kind,dispatch_attempted,encounter_rule_applied,derivation_success," +
            "failure_reason,allowed_regions,constraint_id,candidate_count,candidate_ids," +
            "containment,false_exclusion,reduction_vs_unknown_count," +
            "reduction_vs_unknown_fraction,generation_work,relation_observation_source_groups," +
            "participant_evidence_source_groups,generated_constraint_source_groups")
            .Append('\n');
        foreach (var run in result.Runs)
        {
            Append(builder,
            [
                run.ProfileId,
                Integer(run.InformationOrder),
                Boolean(run.RelationObservationPresent),
                EnumText(run.RelationKnowledge),
                run.ExactRelationKind is null ? null : EnumText(run.ExactRelationKind.Value),
                Boolean(run.DerivationAudit.RelationDispatchAttempted),
                Boolean(run.DerivationAudit.EncounterRuleApplied),
                Boolean(run.DerivationAudit.Success),
                EnumText(run.DerivationAudit.FailureReason),
                string.Join("|", run.DerivedAllowedRegionIds),
                run.ConstraintId,
                Integer(run.CandidateCount),
                string.Join("|", run.CandidateIds),
                Boolean(run.ContainsTruth),
                Boolean(run.FalseExclusion),
                Integer(run.CandidateReductionVsUnknownCount),
                Decimal(run.CandidateReductionVsUnknownFraction),
                Integer(run.CandidateGenerationWork),
                Integer(run.RelationObservationSourceGroupCount),
                Integer(run.ParticipantEvidenceSourceGroupCount),
                Integer(run.GeneratedConstraintSourceGroupCount)
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

    private static string EnumText<T>(T value) where T : struct, Enum =>
        System.Text.Json.JsonNamingPolicy.CamelCase.ConvertName(value.ToString());

    private static string Integer(int value) => value.ToString(CultureInfo.InvariantCulture);

    private static string Decimal(decimal value) => value.ToString(
        "0.############################",
        CultureInfo.InvariantCulture);

    private static string Boolean(bool value) => value ? "true" : "false";
}
