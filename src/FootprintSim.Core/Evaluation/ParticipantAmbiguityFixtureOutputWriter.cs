using System.Globalization;
using System.Text;
using FootprintSim.Serialization;

namespace FootprintSim.Evaluation;

public static class ParticipantAmbiguityFixtureOutputNames
{
    public const string ReportJson = "participant-ambiguity-derivation.json";
    public const string ProfilesCsv = "participant-ambiguity-derivation.csv";
    public const string ManifestJson = "participant-ambiguity-derivation-manifest.json";

    public static IReadOnlyList<string> All { get; } =
    [
        ReportJson,
        ProfilesCsv,
        ManifestJson
    ];
}

public sealed class ParticipantAmbiguityFixtureOutputWriter
{
    public async Task WriteAsync(
        string outputDirectory,
        ParticipantAmbiguityFixtureEvaluationResult result,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputDirectory);
        ArgumentNullException.ThrowIfNull(result);
        Directory.CreateDirectory(outputDirectory);

        await Write(ParticipantAmbiguityFixtureOutputNames.ReportJson, JsonDefaults.Serialize(result) + "\n");
        await Write(ParticipantAmbiguityFixtureOutputNames.ProfilesCsv, ProfilesCsv(result));
        await Write(
            ParticipantAmbiguityFixtureOutputNames.ManifestJson,
            JsonDefaults.Serialize(result.Manifest) + "\n");

        async Task Write(string fileName, string content) => await File.WriteAllTextAsync(
            Path.Combine(outputDirectory, fileName),
            content,
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            cancellationToken);
    }

    private static string ProfilesCsv(ParticipantAmbiguityFixtureEvaluationResult result)
    {
        var builder = new StringBuilder();
        builder.Append(
            "profile,information_order,identity_knowledge,entity_kind,possible_participants," +
            "derivation_success,failure_reason,allowed_regions,constraint_id,candidate_count," +
            "candidate_ids,containment,false_exclusion,reduction_vs_unknown_count," +
            "reduction_vs_unknown_fraction,participant_alternatives_evaluated," +
            "participant_location_evidence_inspected,generation_work,encounter_source_groups," +
            "participant_evidence_source_groups,generated_constraint_source_groups")
            .Append('\n');
        foreach (var run in result.Runs)
        {
            Append(builder,
            [
                run.ProfileId,
                Integer(run.InformationOrder),
                EnumText(run.IdentityKnowledge),
                EnumText(run.ParticipantEntityKind),
                string.Join("|", run.PossibleParticipantIds),
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
                Integer(run.ParticipantAlternativesEvaluated),
                Integer(run.ParticipantLocationEvidenceRecordsInspected),
                Integer(run.CandidateGenerationWork),
                Integer(run.EncounterSourceGroupCount),
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
