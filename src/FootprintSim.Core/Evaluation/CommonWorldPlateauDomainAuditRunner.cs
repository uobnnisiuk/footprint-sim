using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using FootprintSim.Serialization;

namespace FootprintSim.Evaluation;

public sealed record CommonWorldPlateauDomainAuditRequest
{
    public required CommonWorldPlateauDomainAuditProtocol Protocol { get; init; }
    public required string ProtocolContentSha256 { get; init; }
    public required CommonWorldFieldCombinationExperimentDefinition BaselineExperiment { get; init; }
    public required string BaselineExperimentContentSha256 { get; init; }
    public IReadOnlyList<int> Seeds { get; init; } = [];
    public required string SourceCommitSha { get; init; }
    public required bool DirtyWorkingTree { get; init; }
}

public sealed record CommonWorldPlateauDomainDefinition
{
    public required string DomainId { get; init; }
    public required string Definition { get; init; }
}

public sealed record CommonWorldPlateauDomainAuditManifest
{
    public int SchemaVersion { get; init; } = 1;
    public required string ExperimentId { get; init; }
    public required string SourceCommitSha { get; init; }
    public required bool DirtyWorkingTree { get; init; }
    public required string ProtocolContentSha256 { get; init; }
    public required string BaselineExperimentContentSha256 { get; init; }
    public required string BaselineFinalCommit { get; init; }
    public required string ClassifierRuleId { get; init; }
    public required string TimeTopRule { get; init; }
    public required bool LegacyCorrectionOutcomeAlreadyInspected { get; init; }
    public required string PublicInferenceInputSignature { get; init; }
    public required string PublicInferenceOutputSignature { get; init; }
    public required string DiagnosticInputSignature { get; init; }
    public required string DiagnosticOutputSignature { get; init; }
    public required bool LegacyEvaluationUnchanged { get; init; }
    public required bool ClassifierReceivesOnlyPublicDiagnosticInput { get; init; }
    public IReadOnlyList<int> Seeds { get; init; } = [];
    public IReadOnlyList<CommonWorldPlateauDomainDefinition> DomainDefinitions { get; init; } = [];
    public IReadOnlyList<string> OutputFiles { get; init; } = [];
}

public sealed record CommonWorldPlateauClassificationTransition
{
    public required CommonWorldConditionalOutcomeClassification FromClassification { get; init; }
    public required CommonWorldConditionalOutcomeClassification ToClassification { get; init; }
    public required int ComparisonCount { get; init; }
    public IReadOnlyList<int> Seeds { get; init; } = [];
}

public sealed record CommonWorldPlateauMigrationAggregate
{
    public required string FromDomainId { get; init; }
    public required string ToDomainId { get; init; }
    public required string ComparisonId { get; init; }
    public required int ComparisonCount { get; init; }
    public required int ClassificationChangedCount { get; init; }
    public required int TopFlagChangedCount { get; init; }
    public required int ImplicationFlagChangedCount { get; init; }
    public IReadOnlyList<int> ClassificationAffectedSeeds { get; init; } = [];
    public IReadOnlyList<int> AnyFlagAffectedSeeds { get; init; } = [];
    public IReadOnlyList<CommonWorldPlateauClassificationTransition> ClassificationTransitions { get; init; } = [];
}

public sealed record CommonWorldPlateauDomainClassificationAggregate
{
    public required string DomainId { get; init; }
    public required string ComparisonId { get; init; }
    public IReadOnlyList<CommonWorldPlateauClassificationFrequency> ClassificationFrequencies { get; init; } = [];
}

public sealed record CommonWorldPlateauClassificationFrequency
{
    public required CommonWorldConditionalOutcomeClassification Classification { get; init; }
    public required int ComparisonCount { get; init; }
}

public sealed record CommonWorldPlateauDomainAuditSummary
{
    public required int WorldCount { get; init; }
    public required int ComparisonCount { get; init; }
    public required int ZeroAdditionalEffectCount { get; init; }
    public required int PositiveAdditionalEffectCount { get; init; }
    public required int MigrationViolationCount { get; init; }
    public required CommonWorldFieldCombinationSummary F16Safety { get; init; }
    public IReadOnlyList<CommonWorldPlateauMigrationAggregate> Migrations { get; init; } = [];
    public IReadOnlyList<CommonWorldPlateauDomainClassificationAggregate> DomainClassifications { get; init; } = [];
    public string InterpretationBoundary { get; init; } =
        "Evaluation-only domain diagnostics on fixed F16 inference. Time top uses represented intervals; " +
        "Region top uses the declared diagnostic domain. Domain controls do not change physical " +
        "representation or evidence precision. Source groups are provenance counts, not proof of " +
        "statistical independence. No general field ranking, minimum field set, or rescue performance follows.";
}

public sealed record CommonWorldPlateauDomainAuditResult
{
    public required CommonWorldPlateauDomainAuditManifest Manifest { get; init; }
    public required CommonWorldPlateauDomainAuditSummary Summary { get; init; }
    public IReadOnlyList<CommonWorldPlateauDomainAuditRow> Rows { get; init; } = [];
    public IReadOnlyList<CommonWorldFieldCombinationProfileAggregate> ProfileAggregates { get; init; } = [];
    [JsonIgnore]
    public CommonWorldFieldCombinationEvaluationResult? BaselineResult { get; init; }
}

public sealed class CommonWorldPlateauDomainAuditRunner
{
    public CommonWorldPlateauDomainAuditResult Run(CommonWorldPlateauDomainAuditRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        CommonWorldPlateauDomainAuditValidator.Validate(request.Protocol,
            request.BaselineExperimentContentSha256, request.Seeds);
        CommonWorldFieldCombinationExperimentValidator.Validate(request.BaselineExperiment, request.Seeds);
        ValidateBaselineDefinition(request.BaselineExperiment);
        if (request.ProtocolContentSha256.Length != 64 || !request.ProtocolContentSha256.All(Uri.IsHexDigit) ||
            string.IsNullOrWhiteSpace(request.SourceCommitSha))
        {
            throw new InvalidDataException("Protocol hash and source commit are required.");
        }

        var seeds = request.Seeds.OrderBy(seed => seed).ToArray();
        var baseline = new CommonWorldFieldCombinationEvaluationRunner().Run(
            new CommonWorldFieldCombinationEvaluationRequest
            {
                Experiment = request.BaselineExperiment,
                ExperimentContentSha256 = request.BaselineExperimentContentSha256,
                Seeds = seeds,
                SourceCommitSha = request.SourceCommitSha,
                DirtyWorkingTree = request.DirtyWorkingTree
            });
        var originalSignature = CommonWorldPlateauDomainClassifier.Signature(baseline);
        var publicInputSignature = PublicInferenceSignature(baseline);
        var classifier = new CommonWorldPlateauDomainClassifier();
        var rows = baseline.Runs.OrderBy(run => run.Seed).SelectMany(run =>
            run.ConditionalReductions.Select(comparison => classifier.Audit(
                CommonWorldPlateauDiagnosticProjection.FromLegacy(run, comparison),
                request.Protocol.UnusedRegionId))).ToArray();
        var publicOutputSignature = PublicInferenceSignature(baseline);
        if (rows.Length != 1800 || publicInputSignature != publicOutputSignature ||
            originalSignature != CommonWorldPlateauDomainClassifier.Signature(baseline))
        {
            throw new InvalidDataException("F17 changed the original F16 evaluation or comparison count.");
        }

        var comparisonIds = new[] { "all" }.Concat(request.BaselineExperiment.ConditionalComparisons.Select(item => item.Id));
        var migrations = new List<CommonWorldPlateauMigrationAggregate>();
        var classifications = new List<CommonWorldPlateauDomainClassificationAggregate>();
        foreach (var comparisonId in comparisonIds)
        {
            var selected = rows.Where(row => comparisonId == "all" || row.ComparisonId == comparisonId).ToArray();
            foreach (var pair in new[] { ("legacy", "registered"), ("registered", "occupied"), ("registered", "extended") })
            {
                var values = selected.Select(row => (row.Seed, Migration: row.Migrations.Single(item =>
                    item.FromDomainId == pair.Item1 && item.ToDomainId == pair.Item2))).ToArray();
                var transitions = Enum.GetValues<CommonWorldConditionalOutcomeClassification>().SelectMany(from =>
                    Enum.GetValues<CommonWorldConditionalOutcomeClassification>().Select(to =>
                    {
                        var matches = values.Where(item => item.Migration.FromClassification == from &&
                            item.Migration.ToClassification == to).ToArray();
                        return new CommonWorldPlateauClassificationTransition
                        {
                            FromClassification = from,
                            ToClassification = to,
                            ComparisonCount = matches.Length,
                            Seeds = matches.Select(item => item.Seed).Distinct().OrderBy(seed => seed).ToArray()
                        };
                    })).ToArray();
                migrations.Add(new CommonWorldPlateauMigrationAggregate
                {
                    FromDomainId = pair.Item1,
                    ToDomainId = pair.Item2,
                    ComparisonId = comparisonId,
                    ComparisonCount = values.Length,
                    ClassificationChangedCount = values.Count(item => item.Migration.ClassificationChanged),
                    TopFlagChangedCount = values.Count(item => item.Migration.TopFlagChanged),
                    ImplicationFlagChangedCount = values.Count(item => item.Migration.ImplicationFlagChanged),
                    ClassificationAffectedSeeds = values.Where(item => item.Migration.ClassificationChanged)
                        .Select(item => item.Seed).Distinct().OrderBy(seed => seed).ToArray(),
                    AnyFlagAffectedSeeds = values.Where(item => item.Migration.TopFlagChanged || item.Migration.ImplicationFlagChanged)
                        .Select(item => item.Seed).Distinct().OrderBy(seed => seed).ToArray(),
                    ClassificationTransitions = transitions
                });
            }

            foreach (var domain in new[] { "legacy", "registered", "occupied", "extended" })
            {
                classifications.Add(new CommonWorldPlateauDomainClassificationAggregate
                {
                    DomainId = domain,
                    ComparisonId = comparisonId,
                    ClassificationFrequencies = Enum.GetValues<CommonWorldConditionalOutcomeClassification>()
                        .Select(classification => new CommonWorldPlateauClassificationFrequency
                        {
                            Classification = classification,
                            ComparisonCount = selected.Count(row => Domain(row, domain).Classification == classification)
                        }).ToArray()
                });
            }
        }

        return new CommonWorldPlateauDomainAuditResult
        {
            Manifest = new CommonWorldPlateauDomainAuditManifest
            {
                ExperimentId = request.Protocol.Id,
                SourceCommitSha = request.SourceCommitSha,
                DirtyWorkingTree = request.DirtyWorkingTree,
                ProtocolContentSha256 = request.ProtocolContentSha256,
                BaselineExperimentContentSha256 = request.BaselineExperimentContentSha256,
                BaselineFinalCommit = request.Protocol.BaselineFinalCommit,
                ClassifierRuleId = request.Protocol.ClassifierRuleId,
                TimeTopRule = request.Protocol.TimeTopRule,
                LegacyCorrectionOutcomeAlreadyInspected = request.Protocol.LegacyCorrectionOutcomeAlreadyInspected,
                PublicInferenceInputSignature = publicInputSignature,
                PublicInferenceOutputSignature = publicOutputSignature,
                DiagnosticInputSignature = CommonWorldPlateauDomainClassifier.Signature(rows.Select(row => row.InputSignature).ToArray()),
                DiagnosticOutputSignature = CommonWorldPlateauDomainClassifier.Signature(rows.Select(row => row.OutputSignature).ToArray()),
                LegacyEvaluationUnchanged = true,
                ClassifierReceivesOnlyPublicDiagnosticInput = true,
                Seeds = seeds,
                DomainDefinitions =
                [
                    new() { DomainId = "registered", Definition = "Original declared RegionUniverse, sorted and unique." },
                    new() { DomainId = "occupied", Definition = "Distinct original candidate Regions; diagnostic scope only." },
                    new() { DomainId = "extended", Definition = "Original declared Regions plus m1f17-unused-region; no inference change." }
                ],
                OutputFiles = CommonWorldPlateauDomainAuditOutputNames.All
            },
            Summary = new CommonWorldPlateauDomainAuditSummary
            {
                WorldCount = baseline.Runs.Count,
                ComparisonCount = rows.Length,
                ZeroAdditionalEffectCount = rows.Count(row => row.CandidateSetsEqual),
                PositiveAdditionalEffectCount = rows.Count(row => row.AdditionalCandidateReduction > 0),
                MigrationViolationCount = 0,
                F16Safety = baseline.Summary,
                Migrations = migrations,
                DomainClassifications = classifications
            },
            Rows = rows,
            ProfileAggregates = baseline.ProfileAggregates,
            BaselineResult = baseline
        };
    }

    public static void ValidateBaselineDefinition(CommonWorldFieldCombinationExperimentDefinition experiment)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            WriteCanonical(JsonSerializer.SerializeToElement(experiment, JsonDefaults.Options), writer);
        }

        var hash = Convert.ToHexString(SHA256.HashData(stream.ToArray())).ToLowerInvariant();
        if (hash != "725375ec76d3834ccf3a7bbd73f1ecf8f27ea3ce04da995d1cbeff11d1f2b923")
        {
            throw new InvalidDataException("The F16 definition was changed despite its supplied content hash.");
        }
    }

    private static void WriteCanonical(JsonElement value, Utf8JsonWriter writer)
    {
        if (value.ValueKind == JsonValueKind.Object)
        {
            writer.WriteStartObject();
            foreach (var property in value.EnumerateObject().OrderBy(item => item.Name, StringComparer.Ordinal))
            {
                writer.WritePropertyName(property.Name);
                WriteCanonical(property.Value, writer);
            }

            writer.WriteEndObject();
        }
        else if (value.ValueKind == JsonValueKind.Array)
        {
            writer.WriteStartArray();
            foreach (var item in value.EnumerateArray())
            {
                WriteCanonical(item, writer);
            }

            writer.WriteEndArray();
        }
        else
        {
            value.WriteTo(writer);
        }
    }

    private static string PublicInferenceSignature(CommonWorldFieldCombinationEvaluationResult result) =>
        CommonWorldPlateauDomainClassifier.Signature(result.Runs.Select(run => new
        {
            run.Seed,
            run.World.PublicWorldId,
            run.CandidateUniverseId,
            run.SourceGraph,
            run.DerivationAudit.Location,
            run.DerivationAudit.Time,
            run.DerivationAudit.Participant,
            run.DerivationAudit.ExactRelationKind,
            run.DerivedLocationAllowedRegionIds,
            run.DerivedHistoricalTimeWindow,
            run.DerivedParticipantAllowedRegionIds,
            Profiles = run.Profiles.Select(profile => new
            {
                profile.ProfileId,
                profile.Order,
                profile.ActiveAxes,
                profile.OmittedAxes,
                profile.OmittedAxesMeanNotSelected,
                profile.ActiveConstraintIds,
                profile.ObservationIdsPresent,
                profile.CandidateUniverseId,
                profile.CandidateUniverseIds,
                profile.CompositionId,
                profile.Status,
                profile.Candidates,
                profile.CandidateIds,
                profile.CandidateCount,
                profile.ReductionVsNoneCount,
                profile.ReductionVsNoneFraction,
                profile.SelectedSupportingSourceGroupIds,
                profile.SelectedSupportingSourceGroupCount,
                profile.SelectedFieldCount,
                profile.ActiveConstraintCount,
                profile.CandidateGenerationWork,
                profile.CompositionAudit
            }).ToArray()
        }).ToArray());

    internal static CommonWorldPlateauDomainDiagnostic Domain(CommonWorldPlateauDomainAuditRow row, string domain) =>
        domain switch
        {
            "legacy" => row.Legacy,
            "registered" => row.Registered,
            "occupied" => row.Occupied,
            "extended" => row.Extended,
            _ => throw new ArgumentOutOfRangeException(nameof(domain))
        };
}
