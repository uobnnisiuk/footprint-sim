using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using FootprintSim.Serialization;

namespace FootprintSim.Evaluation;

public sealed record CommonWorldPlateauDomainAuditProtocol
{
    public required int SchemaVersion { get; init; }
    public required string Id { get; init; }
    public required string BaselineFinalCommit { get; init; }
    public required string BaselineExperimentSha256 { get; init; }
    public required int RequiredSeedStart { get; init; }
    public required int RequiredSeedEnd { get; init; }
    public required string ClassifierRuleId { get; init; }
    public required IReadOnlyList<string> Domains { get; init; }
    public required string UnusedRegionId { get; init; }
    public required string TimeTopRule { get; init; }
    public required bool LegacyCorrectionOutcomeAlreadyInspected { get; init; }
    public required IReadOnlyList<string> ProspectiveControls { get; init; }
    public required string FixedInference { get; init; }
    public required string FailurePolicy { get; init; }
}

public static class CommonWorldPlateauDomainAuditProtocolLoader
{
    public static CommonWorldPlateauDomainAuditProtocol Load(string path) => Parse(File.ReadAllText(path));

    public static CommonWorldPlateauDomainAuditProtocol Parse(string json)
    {
        var protocol = JsonSerializer.Deserialize<CommonWorldPlateauDomainAuditProtocol>(json,
            new JsonSerializerOptions(JsonDefaults.Options)
            {
                UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
            }) ?? throw new InvalidDataException("The plateau audit protocol is empty.");
        CommonWorldPlateauDomainAuditValidator.Validate(protocol, protocol.BaselineExperimentSha256,
            Enumerable.Range(0, 200).ToArray());
        return protocol;
    }
}

public static class CommonWorldPlateauDomainAuditValidator
{
    public const string BaselineSha256 = "e717669c7178dcf8b278b831b6ee049a2aa8dae5a9c9e6366b8c32712649ca39";

    public static void Validate(CommonWorldPlateauDomainAuditProtocol protocol,
        string baselineSha256, IReadOnlyList<int> seeds)
    {
        ArgumentNullException.ThrowIfNull(protocol);
        ArgumentNullException.ThrowIfNull(seeds);
        if (protocol.SchemaVersion != 1 || protocol.Id != "m1f17-plateau-domain-audit" ||
            protocol.BaselineFinalCommit != "e68e5a88b9feb103f05ec9d768ecbc3b85bd5f11" ||
            protocol.BaselineExperimentSha256 != BaselineSha256 || baselineSha256 != BaselineSha256 ||
            protocol.RequiredSeedStart != 0 || protocol.RequiredSeedEnd != 199 ||
            protocol.ClassifierRuleId != CommonWorldPlateauDomainClassifier.RuleId ||
            protocol.Domains is null || !protocol.Domains.SequenceEqual(["registered", "occupied", "extended"]) ||
            protocol.UnusedRegionId != "m1f17-unused-region" ||
            protocol.TimeTopRule != "all-original-candidate-intervals-overlap-derived-time" ||
            !protocol.LegacyCorrectionOutcomeAlreadyInspected || protocol.ProspectiveControls is null ||
            !protocol.ProspectiveControls.SequenceEqual(["occupied", "extended"]) ||
            protocol.FixedInference != "unchanged-m1f16" ||
            protocol.FailurePolicy != "fail-no-skip-no-regenerate-no-tuning")
        {
            throw new InvalidDataException("Protocol or baseline hash differs from the F17 registration.");
        }

        if (seeds.Count != 200 || !seeds.OrderBy(seed => seed).SequenceEqual(Enumerable.Range(0, 200)))
        {
            throw new InvalidDataException("F17 requires each seed 0..199 exactly once, with no duplicates.");
        }
    }
}

/// <summary>Public diagnostic data only: no world, truth, evidence, or inference request.</summary>
public sealed record CommonWorldPlateauDiagnosticInput
{
    public required int Seed { get; init; }
    public required string ComparisonId { get; init; }
    public required string BaselineProfileId { get; init; }
    public required string CombinedProfileId { get; init; }
    public required CommonWorldObservationAxis AddedAxis { get; init; }
    public IReadOnlyList<string> RegisteredRegionIds { get; init; } = [];
    public IReadOnlyList<string> OccupiedRegionIds { get; init; } = [];
    public IReadOnlyList<string> LocationAllowedRegionIds { get; init; } = [];
    public IReadOnlyList<string> ParticipantAllowedRegionIds { get; init; } = [];
    public IReadOnlyList<string> BaselineCandidateIds { get; init; } = [];
    public IReadOnlyList<string> CombinedCandidateIds { get; init; } = [];
    public IReadOnlyList<CommonWorldObservationAxis> BaselineAxes { get; init; } = [];
    public IReadOnlyList<CommonWorldObservationAxis> CombinedAxes { get; init; } = [];
    public required bool TimePredicateIsTop { get; init; }
    public required CommonWorldConditionalReductionAudit LegacyDiagnostic { get; init; }
}

public sealed record CommonWorldPlateauDomainDiagnostic
{
    public required string DomainId { get; init; }
    public IReadOnlyList<string> DomainRegionIds { get; init; } = [];
    public IReadOnlyList<string> ImplicationDomainRegionIds { get; init; } = [];
    public required bool AddedPredicateIsTopForCandidateDomain { get; init; }
    public required bool AddedPredicateImpliedByBaselinePredicates { get; init; }
    public required bool RegionImplicationApplicable { get; init; }
    public IReadOnlyList<string> ImplicationCounterexampleRegionIds { get; init; } = [];
    public required CommonWorldConditionalOutcomeClassification Classification { get; init; }
}

public sealed record CommonWorldPlateauDomainMigration
{
    public required string FromDomainId { get; init; }
    public required string ToDomainId { get; init; }
    public required CommonWorldConditionalOutcomeClassification FromClassification { get; init; }
    public required CommonWorldConditionalOutcomeClassification ToClassification { get; init; }
    public required bool ClassificationChanged { get; init; }
    public required bool TopFlagChanged { get; init; }
    public required bool ImplicationFlagChanged { get; init; }
}

public sealed record CommonWorldPlateauDomainAuditRow
{
    public required int Seed { get; init; }
    public required string ComparisonId { get; init; }
    public required string BaselineProfileId { get; init; }
    public required string CombinedProfileId { get; init; }
    public required CommonWorldObservationAxis AddedAxis { get; init; }
    public IReadOnlyList<string> BaselineCandidateIds { get; init; } = [];
    public IReadOnlyList<string> CombinedCandidateIds { get; init; } = [];
    public required int AdditionalCandidateReduction { get; init; }
    public required bool CandidateSetsEqual { get; init; }
    public required CommonWorldPlateauDiagnosticInput Input { get; init; }
    public required CommonWorldPlateauDomainDiagnostic Legacy { get; init; }
    public required CommonWorldPlateauDomainDiagnostic Registered { get; init; }
    public required CommonWorldPlateauDomainDiagnostic Occupied { get; init; }
    public required CommonWorldPlateauDomainDiagnostic Extended { get; init; }
    public IReadOnlyList<CommonWorldPlateauDomainMigration> Migrations { get; init; } = [];
    public required string InputSignature { get; init; }
    public required string OutputSignature { get; init; }
}

public sealed class CommonWorldPlateauDomainClassifier
{
    public const string RuleId = "m1f17-explicit-region-domain-plateau-v1";

    public CommonWorldPlateauDomainAuditRow Audit(CommonWorldPlateauDiagnosticInput input,
        string unusedRegionId = "m1f17-unused-region")
    {
        ArgumentNullException.ThrowIfNull(input);
        var canonical = input with
        {
            RegisteredRegionIds = Set(input.RegisteredRegionIds),
            OccupiedRegionIds = Set(input.OccupiedRegionIds),
            LocationAllowedRegionIds = Set(input.LocationAllowedRegionIds),
            ParticipantAllowedRegionIds = Set(input.ParticipantAllowedRegionIds),
            BaselineCandidateIds = Set(input.BaselineCandidateIds),
            CombinedCandidateIds = Set(input.CombinedCandidateIds),
            BaselineAxes = input.BaselineAxes.Distinct().OrderBy(axis => axis).ToArray(),
            CombinedAxes = input.CombinedAxes.Distinct().OrderBy(axis => axis).ToArray()
        };
        ValidateInput(canonical, unusedRegionId);
        var legacy = Diagnose(canonical, "legacy", canonical.RegisteredRegionIds, canonical.OccupiedRegionIds);
        var registered = Diagnose(canonical, "registered", canonical.RegisteredRegionIds, canonical.RegisteredRegionIds);
        var occupied = Diagnose(canonical, "occupied", canonical.OccupiedRegionIds, canonical.OccupiedRegionIds);
        var extendedRegions = Set(canonical.RegisteredRegionIds.Append(unusedRegionId));
        var extended = Diagnose(canonical, "extended", extendedRegions, extendedRegions);
        var original = canonical.LegacyDiagnostic;
        if (original.AddedPredicateIsTopForCandidateDomain != legacy.AddedPredicateIsTopForCandidateDomain ||
            original.AddedPredicateImpliedByBaselinePredicates != legacy.AddedPredicateImpliedByBaselinePredicates ||
            original.Classification != legacy.Classification)
        {
            throw new InvalidDataException("The supplied legacy row differs from the unchanged F16 diagnostic rule.");
        }

        var migrations = new[] { Migration(legacy, registered), Migration(registered, occupied), Migration(registered, extended) };
        ValidateMigrations(canonical, legacy, registered, occupied, extended);
        return new CommonWorldPlateauDomainAuditRow
        {
            Seed = canonical.Seed,
            ComparisonId = canonical.ComparisonId,
            BaselineProfileId = canonical.BaselineProfileId,
            CombinedProfileId = canonical.CombinedProfileId,
            AddedAxis = canonical.AddedAxis,
            BaselineCandidateIds = canonical.BaselineCandidateIds,
            CombinedCandidateIds = canonical.CombinedCandidateIds,
            AdditionalCandidateReduction = canonical.BaselineCandidateIds.Count - canonical.CombinedCandidateIds.Count,
            CandidateSetsEqual = canonical.BaselineCandidateIds.SequenceEqual(canonical.CombinedCandidateIds),
            Input = canonical,
            Legacy = legacy,
            Registered = registered,
            Occupied = occupied,
            Extended = extended,
            Migrations = migrations,
            InputSignature = Signature(canonical),
            OutputSignature = Signature(new
            {
                legacy,
                registered,
                occupied,
                extended,
                migrations,
                canonical.BaselineCandidateIds,
                canonical.CombinedCandidateIds
            })
        };
    }

    private static CommonWorldPlateauDomainDiagnostic Diagnose(CommonWorldPlateauDiagnosticInput input,
        string domainId, IReadOnlyList<string> topDomain, IReadOnlyList<string> implicationDomain) =>
        Diagnose(input.AddedAxis, input.LocationAllowedRegionIds, input.ParticipantAllowedRegionIds,
            input.TimePredicateIsTop, input.BaselineAxes, input.BaselineCandidateIds, input.CombinedCandidateIds,
            domainId, topDomain, implicationDomain);

    internal static CommonWorldPlateauDomainDiagnostic Diagnose(CommonWorldObservationAxis addedAxis,
        IReadOnlyList<string> locationAllowedRegionIds, IReadOnlyList<string> participantAllowedRegionIds,
        bool timePredicateIsTop, IReadOnlyList<CommonWorldObservationAxis> baselineAxes,
        IReadOnlyList<string> baselineCandidateIds, IReadOnlyList<string> combinedCandidateIds,
        string domainId, IReadOnlyList<string> topDomain, IReadOnlyList<string> implicationDomain)
    {
        var regionAxis = addedAxis != CommonWorldObservationAxis.Time;
        var addedRegions = addedAxis == CommonWorldObservationAxis.Location
            ? locationAllowedRegionIds : participantAllowedRegionIds;
        var top = regionAxis ? topDomain.All(addedRegions.Contains) : timePredicateIsTop;
        var implicationApplicable = regionAxis && baselineAxes.Any(axis =>
            axis is CommonWorldObservationAxis.Location or CommonWorldObservationAxis.Participant);
        var baseRegions = implicationDomain.AsEnumerable();
        if (baselineAxes.Contains(CommonWorldObservationAxis.Location))
        {
            baseRegions = baseRegions.Intersect(locationAllowedRegionIds, StringComparer.Ordinal);
        }

        if (baselineAxes.Contains(CommonWorldObservationAxis.Participant))
        {
            baseRegions = baseRegions.Intersect(participantAllowedRegionIds, StringComparer.Ordinal);
        }

        var witnesses = implicationApplicable
            ? Set(baseRegions.Except(addedRegions, StringComparer.Ordinal)) : [];
        var implied = implicationApplicable && witnesses.Length == 0;
        var equal = baselineCandidateIds.SequenceEqual(combinedCandidateIds);
        return new CommonWorldPlateauDomainDiagnostic
        {
            DomainId = domainId,
            DomainRegionIds = topDomain,
            ImplicationDomainRegionIds = implicationDomain,
            AddedPredicateIsTopForCandidateDomain = top,
            AddedPredicateImpliedByBaselinePredicates = implied,
            RegionImplicationApplicable = implicationApplicable,
            ImplicationCounterexampleRegionIds = witnesses,
            Classification = !equal ? CommonWorldConditionalOutcomeClassification.AdditionalCandidateReduction
                : baselineCandidateIds.Count == 1 ? CommonWorldConditionalOutcomeClassification.Saturation
                : top ? CommonWorldConditionalOutcomeClassification.ObservationToConstraintPlateau
                : implied ? CommonWorldConditionalOutcomeClassification.ConstraintOverlapPlateau
                : CommonWorldConditionalOutcomeClassification.ConstraintToCandidatePlateau
        };
    }

    private static void ValidateInput(CommonWorldPlateauDiagnosticInput input, string sentinel)
    {
        var legacy = input.LegacyDiagnostic;
        if (string.IsNullOrWhiteSpace(sentinel) || input.Seed < 0 ||
            string.IsNullOrWhiteSpace(input.ComparisonId) || string.IsNullOrWhiteSpace(input.BaselineProfileId) ||
            string.IsNullOrWhiteSpace(input.CombinedProfileId) || !Enum.IsDefined(input.AddedAxis) ||
            input.BaselineAxes.Any(axis => !Enum.IsDefined(axis)) || input.CombinedAxes.Any(axis => !Enum.IsDefined(axis)) ||
            input.RegisteredRegionIds.Count == 0 || input.OccupiedRegionIds.Count == 0 ||
            input.LocationAllowedRegionIds.Count == 0 || input.ParticipantAllowedRegionIds.Count == 0 ||
            input.BaselineCandidateIds.Count == 0 || input.CombinedCandidateIds.Count == 0 ||
            input.OccupiedRegionIds.Concat(input.LocationAllowedRegionIds).Concat(input.ParticipantAllowedRegionIds)
                .Any(region => !input.RegisteredRegionIds.Contains(region, StringComparer.Ordinal)) ||
            input.RegisteredRegionIds.Contains(sentinel, StringComparer.Ordinal) ||
            input.CombinedCandidateIds.Any(id => !input.BaselineCandidateIds.Contains(id, StringComparer.Ordinal)) ||
            input.BaselineAxes.Contains(input.AddedAxis) ||
            !input.CombinedAxes.SequenceEqual(input.BaselineAxes.Append(input.AddedAxis).OrderBy(axis => axis)) ||
            legacy is null || legacy.Seed != input.Seed || legacy.ComparisonId != input.ComparisonId ||
            legacy.BaselineProfileId != input.BaselineProfileId || legacy.CombinedProfileId != input.CombinedProfileId ||
            legacy.AddedAxis != input.AddedAxis || legacy.BaselineCandidateCount != input.BaselineCandidateIds.Count ||
            legacy.CombinedCandidateCount != input.CombinedCandidateIds.Count ||
            legacy.AdditionalCandidateReduction != input.BaselineCandidateIds.Count - input.CombinedCandidateIds.Count ||
            legacy.CandidateSetsEqual != input.BaselineCandidateIds.SequenceEqual(input.CombinedCandidateIds))
        {
            throw new InvalidDataException("Invalid public plateau input, domain, candidate nesting, or legacy row.");
        }
    }

    private static void ValidateMigrations(CommonWorldPlateauDiagnosticInput input,
        CommonWorldPlateauDomainDiagnostic legacy, CommonWorldPlateauDomainDiagnostic registered,
        CommonWorldPlateauDomainDiagnostic occupied, CommonWorldPlateauDomainDiagnostic extended)
    {
        var correctionAllowed = legacy.Classification == registered.Classification ||
            legacy.Classification == CommonWorldConditionalOutcomeClassification.ConstraintOverlapPlateau &&
            registered.Classification == CommonWorldConditionalOutcomeClassification.ConstraintToCandidatePlateau;
        var extensionAllowed = registered.Classification == extended.Classification ||
            registered.Classification == CommonWorldConditionalOutcomeClassification.ObservationToConstraintPlateau &&
            extended.Classification is CommonWorldConditionalOutcomeClassification.ConstraintOverlapPlateau or
                CommonWorldConditionalOutcomeClassification.ConstraintToCandidatePlateau;
        if (!correctionAllowed || !extensionAllowed ||
            legacy.AddedPredicateIsTopForCandidateDomain != registered.AddedPredicateIsTopForCandidateDomain ||
            !legacy.AddedPredicateImpliedByBaselinePredicates && registered.AddedPredicateImpliedByBaselinePredicates ||
            registered.AddedPredicateImpliedByBaselinePredicates != extended.AddedPredicateImpliedByBaselinePredicates ||
            !registered.AddedPredicateIsTopForCandidateDomain && extended.AddedPredicateIsTopForCandidateDomain ||
            input.AddedAxis == CommonWorldObservationAxis.Time && new[] { occupied, extended }.Any(item =>
                item.Classification != registered.Classification ||
                item.AddedPredicateIsTopForCandidateDomain != registered.AddedPredicateIsTopForCandidateDomain ||
                item.AddedPredicateImpliedByBaselinePredicates != registered.AddedPredicateImpliedByBaselinePredicates) ||
            registered.Classification is CommonWorldConditionalOutcomeClassification.AdditionalCandidateReduction or
                CommonWorldConditionalOutcomeClassification.Saturation &&
            new[] { occupied, extended }.Any(item => item.Classification != registered.Classification))
        {
            throw new InvalidDataException("A preregistered plateau migration invariant failed.");
        }
    }

    private static CommonWorldPlateauDomainMigration Migration(CommonWorldPlateauDomainDiagnostic from,
        CommonWorldPlateauDomainDiagnostic to) => new()
        {
            FromDomainId = from.DomainId,
            ToDomainId = to.DomainId,
            FromClassification = from.Classification,
            ToClassification = to.Classification,
            ClassificationChanged = from.Classification != to.Classification,
            TopFlagChanged = from.AddedPredicateIsTopForCandidateDomain != to.AddedPredicateIsTopForCandidateDomain,
            ImplicationFlagChanged = from.AddedPredicateImpliedByBaselinePredicates != to.AddedPredicateImpliedByBaselinePredicates
        };

    internal static string[] Set(IEnumerable<string> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        var result = values.Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray();
        if (result.Any(string.IsNullOrWhiteSpace))
        {
            throw new InvalidDataException("Diagnostic sets cannot contain empty identifiers.");
        }

        return result;
    }

    internal static string Signature<T>(T value) => Convert.ToHexString(SHA256.HashData(
        Encoding.UTF8.GetBytes(JsonDefaults.Serialize(value)))).ToLowerInvariant();
}

/// <summary>The adapter alone reads legacy evaluation records and drops their truth fields.</summary>
public static class CommonWorldPlateauDiagnosticProjection
{
    public static CommonWorldPlateauDiagnosticInput FromLegacy(CommonWorldFieldCombinationWorldRun run,
        CommonWorldConditionalReductionAudit comparison)
    {
        var baseline = run.Profiles.Single(item => item.ProfileId == comparison.BaselineProfileId);
        var combined = run.Profiles.Single(item => item.ProfileId == comparison.CombinedProfileId);
        return new CommonWorldPlateauDiagnosticInput
        {
            Seed = run.Seed,
            ComparisonId = comparison.ComparisonId,
            BaselineProfileId = comparison.BaselineProfileId,
            CombinedProfileId = comparison.CombinedProfileId,
            AddedAxis = comparison.AddedAxis,
            RegisteredRegionIds = run.World.RegionUniverse,
            OccupiedRegionIds = run.World.CandidateStates.Select(item => item.RegionId).ToArray(),
            LocationAllowedRegionIds = run.DerivedLocationAllowedRegionIds,
            ParticipantAllowedRegionIds = run.DerivedParticipantAllowedRegionIds,
            BaselineCandidateIds = baseline.CandidateIds,
            CombinedCandidateIds = combined.CandidateIds,
            BaselineAxes = baseline.ActiveAxes,
            CombinedAxes = combined.ActiveAxes,
            TimePredicateIsTop = run.World.CandidateStates.All(candidate =>
                candidate.StartTick <= run.DerivedHistoricalTimeWindow.EndTick &&
                run.DerivedHistoricalTimeWindow.StartTick <= candidate.EndTick),
            LegacyDiagnostic = comparison
        };
    }
}
