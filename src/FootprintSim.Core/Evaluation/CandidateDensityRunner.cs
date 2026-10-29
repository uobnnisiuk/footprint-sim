using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using FootprintSim.Inference.Derivation;
using FootprintSim.Inference.PastStates;
using FootprintSim.Interactions.CandidateWarps;
using FootprintSim.Serialization;
using static FootprintSim.Evaluation.CandidateIntervalWidthPublicEvaluator;

namespace FootprintSim.Evaluation;

public static class CandidateDensityProtocolLoader
{
    public static CandidateDensityProtocol Load(string path) => Parse(File.ReadAllText(path));
    public static CandidateDensityProtocol Parse(string json)
    {
        var protocol = JsonSerializer.Deserialize<CandidateDensityProtocol>(json,
            new JsonSerializerOptions(JsonDefaults.Options) { UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow })
            ?? throw new InvalidDataException("Empty F19 protocol.");
        CandidateDensityValidator.Validate(protocol, protocol.BaselineExperimentSha256, Enumerable.Range(0, 200).ToArray());
        return protocol;
    }
}

public static class CandidateDensityValidator
{
    public const string ProtocolSha256 = "f0669251ab8b747fbd4b7517e169d70723f052ae0465d33cfac6d5fe355c6bbd";

    public static void Validate(CandidateDensityProtocol protocol, string baselineSha256, IReadOnlyList<int> seeds)
    {
        ArgumentNullException.ThrowIfNull(protocol);
        ArgumentNullException.ThrowIfNull(seeds);
        if (protocol.SchemaVersion != 1 || protocol.Id != "m1f19-nested-point-support-density" ||
            protocol.BaselineFinalCommit != "77611bc52976178857ac442be6e07a9caa1a2a9d" ||
            protocol.BaselineExperimentSha256 != CommonWorldPlateauDomainAuditValidator.BaselineSha256 ||
            baselineSha256 != CommonWorldPlateauDomainAuditValidator.BaselineSha256 ||
            protocol.RequiredSeedStart != 0 || protocol.RequiredSeedEnd != 199 ||
            protocol.BaselineRepresentation != "m1f18-point-at-existing-start" || protocol.Arms is null ||
            !protocol.Arms.SequenceEqual(CandidateDensityTransform.ArmIds) ||
            protocol.SupportRuleId != CandidateDensityTransform.SupportRuleId ||
            protocol.OrderingRuleId != CandidateDensityTransform.OrderingRuleId ||
            protocol.AdditionNumerators is null || !protocol.AdditionNumerators.SequenceEqual([0, 1, 2]) ||
            protocol.AdditionDenominator != 2 || protocol.Rounding != "floor" || protocol.AddedWidthTicks != 0 ||
            protocol.TruthRule != "unchanged-original-world-truth-resolved-after-all-public-density-arms" ||
            protocol.PlateauRuleId != CommonWorldPlateauDomainClassifier.RuleId ||
            protocol.FixedInference != "original-public-derivations-and-m1f14-composer" ||
            protocol.ProspectiveOutcomeInspected || protocol.FailurePolicy != "fail-no-skip-no-regenerate-no-tuning" ||
            seeds.Count != 200 || !seeds.OrderBy(seed => seed).SequenceEqual(Enumerable.Range(0, 200)))
        {
            throw new InvalidDataException("Protocol, baseline hash, or exact F19 seed set differs from registration.");
        }
    }
}

public sealed class CandidateDensityRunner
{
    public CandidateDensityResult Run(CandidateDensityRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        CandidateDensityValidator.Validate(request.Protocol, request.BaselineExperimentContentSha256, request.Seeds);
        CommonWorldPlateauDomainAuditRunner.ValidateBaselineDefinition(request.BaselineExperiment);
        if (request.ProtocolContentSha256 != CandidateDensityValidator.ProtocolSha256 || string.IsNullOrWhiteSpace(request.SourceCommitSha))
        {
            throw new InvalidDataException("The frozen F19 protocol content hash and source commit are required.");
        }

        var seeds = request.Seeds.OrderBy(seed => seed).ToArray();
        var generator = new CommonWorldFieldCombinationSeededWorldGenerator();
        var runs = seeds.Select(seed => EvaluateWorld(request.BaselineExperiment,
            generator.Generate(request.BaselineExperiment, seed), request.SourceCommitSha, request.DirtyWorkingTree)).ToArray();
        var summary = new CandidateDensitySummary(runs.Length, runs.Sum(run => run.ProfileMetrics.Count), runs.Length * 24,
            runs.Sum(run => run.PairChanges.Count), runs.Sum(run => run.PairChanges.Sum(pair => pair.ProfileChanges.Count)),
            runs.Sum(run => run.Arms.Sum(arm => arm.SetInclusions.Count)),
            runs.Sum(run => run.ProfileMetrics.Count(profile => profile.ContainsTruth)),
            runs.Sum(run => run.ProfileMetrics.Sum(profile => profile.FalseExclusionCount)),
            runs.Sum(run => run.Arms[0].Candidates.Count), runs.Sum(run => run.Arms[2].Candidates.Count), 0,
            "This is deterministic missing-cell sensitivity on a fixed public point lattice. Retaining the truth-containing baseline structurally protects safety. " +
            "Empirical Region/time occupancy changes are coupled effects. Added model states are not evidence, people, physical sites, rescue time or work; " +
            "full-support minima are lattice-dependent and imply no general field requirement.");
        var manifest = new CandidateDensityManifest(request.Protocol.Id, request.SourceCommitSha, request.DirtyWorkingTree,
            request.ProtocolContentSha256, request.BaselineExperimentContentSha256, request.Protocol.BaselineFinalCommit,
            request.Protocol.SupportRuleId, request.Protocol.OrderingRuleId, request.Protocol.PlateauRuleId, seeds,
            Signature(runs.Select(run => new
            {
                run.Seed,
                run.OriginalWorldBeforeSignature,
                run.RawEvidenceSignature,
                run.SourceGraphSignature,
                run.Support.SupportContentSignature,
                run.Arms[0].FixedConstraintSignature
            }).ToArray()),
            Signature(runs.Select(run => new { run.Seed, run.Support, run.Arms }).ToArray()), request.Protocol.TruthRule,
            ["candidate-density.json", "candidate-density-runs.jsonl", "candidate-density-profiles.csv",
                "candidate-density-conditionals.csv", "candidate-density-crosswalk.csv", "candidate-density-manifest.json"]);
        return new CandidateDensityResult(manifest, summary, runs, ProfileAggregates(runs), ConditionalAggregates(runs),
            PairAggregates(runs), ArmAggregates(runs));
    }

    public CandidateDensityWorldRun EvaluateWorld(CommonWorldFieldCombinationExperimentDefinition experiment,
        GeneratedCommonFieldCombinationWorld world, string sourceCommitSha = "test", bool dirtyWorkingTree = false)
    {
        CommonWorldPlateauDomainAuditRunner.ValidateBaselineDefinition(experiment);
        var before = Signature(world);
        // This legacy reference performs its own existing evaluation-only truth check.
        // It derives the public constraints once; all primary arms reuse those components.
        var reference = CandidateIntervalWidthRunner.CreateReference(experiment, world, sourceCommitSha, dirtyWorkingTree);
        var originalPublicRequest = CandidateIntervalWidthRunner.ProjectPublicRequest(experiment, world, reference);
        var originalRequestBefore = Signature(originalPublicRequest);
        var evaluator = new CandidateIntervalWidthPublicEvaluator();
        var originalReference = evaluator.Evaluate(originalPublicRequest, "original");
        var pointReference = evaluator.Evaluate(originalPublicRequest, "point-at-start");
        var pointCandidates = new CandidateIntervalWidthTransform().Apply(originalPublicRequest.Candidates, "point-at-start");
        var publicRequest = originalPublicRequest with { Candidates = pointCandidates };
        var publicBefore = Signature(publicRequest);
        var support = new CandidateDensityTransform().Apply(new CandidateDensityPublicSupportRequest
        {
            Seed = world.Seed,
            BaseTick = world.CandidateBaseTick,
            RegisteredRegionIds = world.RegionUniverse,
            BaselinePointCandidates = pointCandidates
        });
        var arms = EvaluatePublicArms(publicRequest, support);
        var referenceMatches = originalReference.CandidateUniverseId == reference.CandidateUniverseId &&
            originalReference.Profiles.All(profile =>
            {
                var expected = reference.Runs.Single(item => item.ProfileId == profile.ProfileId);
                return profile.CandidateIds.SequenceEqual(expected.CandidateIds) && profile.CandidateCount == expected.CandidateCount &&
                    profile.CandidateGenerationWork == expected.CandidateGenerationWork && profile.CompositionId == expected.CompositionId &&
                    Signature(profile.CompositionAudit) == Signature(expected.CompositionAudit);
            }) && arms[0].CandidateUniverseId == pointReference.CandidateUniverseId &&
            Signature(arms[0].Candidates) == Signature(pointReference.Candidates) &&
            Signature(arms[0].Profiles) == Signature(pointReference.Profiles) &&
            Signature(arms[0].Conditionals) == Signature(pointReference.Conditionals) &&
            Signature(arms[0].Minimum) == Signature(pointReference.Minimum);
        Require(referenceMatches, "F15 original or F18 point reference changed.");
        var bridge = originalReference.Candidates.Select(candidate => new CandidateIntervalWidthCrosswalk(world.Seed,
            candidate.CandidateKey, candidate, arms[0].Candidates.Single(point => point.CandidateKey == candidate.CandidateKey))).ToArray();
        Require(bridge.Length == world.CandidateStates.Count && bridge.All(item =>
            item.Original.PublicSourceIdentifier == item.Point.PublicSourceIdentifier && item.Original.RegionId == item.Point.RegionId &&
            item.Original.ProvenanceSignature == item.Point.ProvenanceSignature && item.Original.TimeWindow.StartTick == item.Point.TimeWindow.StartTick &&
            item.Point.TimeWindow.StartTick == item.Point.TimeWindow.EndTick && item.Point.TemporalKind == CandidateWarpTemporalKind.EventPoint &&
            (item.Original.TimeWindow.StartTick == item.Original.TimeWindow.EndTick) == (item.Original.CandidateId == item.Point.CandidateId)),
            "F16 interval to F18 point bridge failed.");
        var pairChanges = new[] { CompareArms(publicRequest, arms[0], arms[1]), CompareArms(publicRequest, arms[1], arms[2]),
            CompareArms(publicRequest, arms[0], arms[2]) };
        var fullAnalytic = ValidateFullSupport(publicRequest, support, arms[2]);

        // Resolve primary truth addresses only after all three public arms and their checks returned.
        var truthBridge = bridge.Single(item => item.CandidateKey == world.TruthCandidateKey);
        Require(truthBridge.Original.CandidateId == reference.TruthCandidateId &&
            truthBridge.Original.TimeWindow.StartTick == world.TruthCandidateStartTick &&
            truthBridge.Original.TimeWindow.EndTick == world.TruthCandidateEndTick &&
            truthBridge.Original.RegionId == world.TruthRegionId && world.EncounterTick == world.TruthCandidateStartTick,
            "Unchanged truth does not resolve to the original public candidate address.");
        var truth = new CandidateDensityFixedTruth(world.TruthWorldId, world.TruthCandidateKey, world.TruthCandidateStartTick,
            world.TruthCandidateEndTick, world.TruthRegionId, world.TruthParticipantId, world.EncounterTick,
            world.ActualTruthDurationTicks, truthBridge.Original.CandidateId,
            arms.Select(arm => new CandidateDensityTruthAddress(arm.ArmId,
                arm.Candidates.Single(candidate => candidate.CandidateKey == world.TruthCandidateKey).CandidateId)).ToArray(), true);
        var baselineKeys = Set(arms[0].Candidates.Select(candidate => candidate.CandidateKey));
        var metrics = arms.SelectMany(arm => arm.Profiles.Select(profile =>
        {
            var excluded = Difference(arm.Candidates.Select(candidate => candidate.CandidateKey), profile.CandidateKeys);
            var contains = profile.CandidateIds.Contains(truth.ArmAddresses.Single(address => address.ArmId == arm.ArmId).CandidateId);
            var workValid = WorkRule(arm, profile);
            Require(contains && workValid, "Primary truth containment or fixed composer work rule failed.");
            return new CandidateDensityProfileMetric(arm.ArmId, profile.ProfileId, profile.CandidateCount,
                profile.CandidateKeys.Count(key => baselineKeys.Contains(key, StringComparer.Ordinal)),
                profile.CandidateKeys.Count(key => !baselineKeys.Contains(key, StringComparer.Ordinal)), excluded,
                excluded.Length, Fraction(excluded.Length, arm.Candidates.Count), Fraction(profile.CandidateCount, arm.Candidates.Count),
                contains, contains ? 0 : 1, profile.CandidateGenerationWork, workValid);
        })).ToArray();
        var conditionals = arms.SelectMany(arm => arm.Conditionals.Select(conditional =>
            new CandidateDensityConditionalMetric(arm.ArmId, conditional.ComparisonId,
                Excluded(arm, conditional), conditional.AdditionalCandidateReduction,
                Fraction(conditional.AdditionalCandidateReduction, conditional.BaselineCandidateCount)))).ToArray();
        var crosswalk = arms[2].Candidates.Select(candidate => new CandidateDensityCrosswalk(candidate.CandidateKey,
            arms.First(arm => arm.Candidates.Any(item => item.CandidateKey == candidate.CandidateKey)).ArmId, candidate,
            arms.Where(arm => arm.Candidates.Any(item => item.CandidateKey == candidate.CandidateKey)).Select(arm => arm.ArmId).ToArray(),
            bridge.SingleOrDefault(item => item.CandidateKey == candidate.CandidateKey)?.Original.CandidateId)).ToArray();
        var occupancy = arms.Select(arm => Occupancy(arm, support)).ToArray();
        var after = Signature(world);
        var publicAfter = Signature(publicRequest);
        Require(before == after && publicBefore == publicAfter && originalRequestBefore == Signature(originalPublicRequest),
            "World or public evidence inputs changed during density evaluation.");
        var fixture = CommonWorldFieldCombinationWorldEvaluator.ToFixture(experiment, world);
        return new CandidateDensityWorldRun(world.Seed, world, truth, bridge, support, arms, crosswalk, metrics, conditionals,
            pairChanges, occupancy, before, after, publicBefore, publicAfter,
            Signature(new
            {
                fixture.LocationObservation,
                fixture.TimeObservation,
                fixture.ParticipantObservation,
                fixture.FixedRelationKind,
                fixture.EvidenceAvailabilityCutoff
            }), Signature(reference.SourceGraph), referenceMatches, fullAnalytic, true);
    }

    /// <summary>All three primary inference arms run before any evaluation-only truth address is supplied.</summary>
    public static IReadOnlyList<CandidateIntervalWidthArm> EvaluatePublicArms(CandidateIntervalWidthPublicRequest request,
        CandidateDensitySupport support)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(support);
        var canonical = new CandidateDensityTransform().Apply(new CandidateDensityPublicSupportRequest
        {
            Seed = support.Seed,
            BaseTick = support.BaseTick,
            RegisteredRegionIds = support.RegisteredRegionIds,
            BaselinePointCandidates = request.Candidates
        });
        Require(request.Seed == support.Seed && Set(request.RegisteredRegionIds).SequenceEqual(support.RegisteredRegionIds) &&
            Signature(canonical) == Signature(support), "Public request and frozen density support disagree.");
        var evaluator = new CandidateIntervalWidthPublicEvaluator();
        var before = Signature(request);
        var arms = support.Arms.Select(transformArm =>
        {
            var publicArm = evaluator.Evaluate(request with { Candidates = transformArm.Candidates }, "original");
            return publicArm with
            {
                ArmId = transformArm.ArmId,
                RepresentationId = Signature(new { support.SupportContentSignature, transformArm.ArmId, publicArm.CandidateInputSignature })
            };
        }).ToArray();
        Require(before == Signature(request) && arms.All(arm => arm.FixedConstraintSignature == arms[0].FixedConstraintSignature &&
            arm.Profiles.All(profile => WorkRule(arm, profile))), "Public inputs or work rule changed between density arms.");
        return arms;
    }

    public static CandidateDensityPairChange CompareArms(CandidateIntervalWidthPublicRequest request,
        CandidateIntervalWidthArm from, CandidateIntervalWidthArm to)
    {
        var fromKeys = Set(from.Candidates.Select(candidate => candidate.CandidateKey));
        var toKeys = Set(to.Candidates.Select(candidate => candidate.CandidateKey));
        var added = Difference(toKeys, fromKeys);
        var nested = Subset(fromKeys, toKeys);
        var nativeNested = Subset(from.Candidates.Select(candidate => candidate.CandidateId), to.Candidates.Select(candidate => candidate.CandidateId));
        var descriptors = from.Candidates.All(candidate => to.Candidates.Any(other => Signature(candidate) == Signature(other)));
        var changes = from.Profiles.Select(profile =>
        {
            var other = to.Profiles.Single(item => item.ProfileId == profile.ProfileId);
            var newSurvivors = Difference(other.CandidateKeys, profile.CandidateKeys);
            var expected = Set(to.Candidates.Where(candidate => added.Contains(candidate.CandidateKey, StringComparer.Ordinal) &&
                Matches(candidate, profile.ActiveAxes, request.AxisComponents)).Select(candidate => candidate.CandidateKey));
            var oldExcluded = Difference(fromKeys, profile.CandidateKeys);
            var newExcluded = Difference(toKeys, other.CandidateKeys);
            var result = new CandidateDensityProfileChange(profile.ProfileId, profile.CandidateCount, other.CandidateCount,
                other.CandidateCount - profile.CandidateCount, newSurvivors, expected, Difference(newExcluded, oldExcluded),
                newExcluded.Length - oldExcluded.Length,
                Fraction(newExcluded.Length, toKeys.Length) - Fraction(oldExcluded.Length, fromKeys.Length),
                Fraction(other.CandidateCount, toKeys.Length) - Fraction(profile.CandidateCount, fromKeys.Length),
                Subset(profile.CandidateKeys, other.CandidateKeys), Subset(profile.CandidateIds, other.CandidateIds),
                Set(other.CandidateKeys.Intersect(fromKeys, StringComparer.Ordinal)).SequenceEqual(profile.CandidateKeys),
                newSurvivors.SequenceEqual(expected), Subset(oldExcluded, newExcluded));
            Require(result.KeysNested && result.NativeIdsNested && result.RestrictionRecoversOriginal && result.NewSurvivorsMatchPredicates &&
                result.ExcludedKeysNested && result.ReductionDelta >= 0 && WorkRule(from, profile) && WorkRule(to, other) &&
                profile.CompositionId == other.CompositionId &&
                Signature(profile.CompositionAudit with { CandidatePredicateChecks = 0 }) ==
                Signature(other.CompositionAudit with { CandidatePredicateChecks = 0 }), "Profile membership, excluded witnesses, composition or work invariant failed.");
            return result;
        }).ToArray();
        var conditionals = from.Conditionals.Select(conditional =>
        {
            var other = to.Conditionals.Single(item => item.ComparisonId == conditional.ComparisonId);
            var flags = conditional.Diagnostic.AddedPredicateImpliedByBaselinePredicates == other.Diagnostic.AddedPredicateImpliedByBaselinePredicates &&
                conditional.Diagnostic.RegionImplicationApplicable == other.Diagnostic.RegionImplicationApplicable &&
                conditional.Diagnostic.ImplicationCounterexampleRegionIds.SequenceEqual(other.Diagnostic.ImplicationCounterexampleRegionIds) &&
                (conditional.AddedAxis == CommonWorldObservationAxis.Time
                    ? conditional.Diagnostic.AddedPredicateIsTopForCandidateDomain || !other.Diagnostic.AddedPredicateIsTopForCandidateDomain
                    : conditional.Diagnostic.AddedPredicateIsTopForCandidateDomain == other.Diagnostic.AddedPredicateIsTopForCandidateDomain);
            var result = new CandidateDensityConditionalChange(conditional.ComparisonId, conditional.AddedAxis,
                conditional.AdditionalCandidateReduction, other.AdditionalCandidateReduction,
                other.AdditionalCandidateReduction - conditional.AdditionalCandidateReduction,
                Fraction(other.AdditionalCandidateReduction, other.BaselineCandidateCount) -
                Fraction(conditional.AdditionalCandidateReduction, conditional.BaselineCandidateCount),
                Difference(Excluded(to, other), Excluded(from, conditional)), Subset(Excluded(from, conditional), Excluded(to, other)),
                conditional.AdditionalCandidateReduction == 0 && other.AdditionalCandidateReduction > 0,
                conditional.AdditionalCandidateReduction > 0 && other.AdditionalCandidateReduction == 0,
                conditional.Diagnostic, other.Diagnostic, flags);
            Require(result.ExcludedKeysNested && result.ReductionDelta >= 0 && !result.LostEffect && flags &&
                (conditional.Diagnostic.Classification == CommonWorldConditionalOutcomeClassification.Saturation ||
                    other.Diagnostic.Classification != CommonWorldConditionalOutcomeClassification.Saturation),
                "Conditional exclusion, logical flag or saturation invariant failed.");
            return result;
        }).ToArray();
        var equivalences = from.ProfileEquivalences.Select(equivalence =>
        {
            var other = to.ProfileEquivalences.Single(item => item.LeftProfileId == equivalence.LeftProfileId && item.RightProfileId == equivalence.RightProfileId);
            var result = new CandidateDensityEquivalenceChange(equivalence.LeftProfileId, equivalence.RightProfileId,
                equivalence.CandidateKeySetsEqual, other.CandidateKeySetsEqual,
                equivalence.CandidateKeySetsEqual && !other.CandidateKeySetsEqual,
                !equivalence.CandidateKeySetsEqual && other.CandidateKeySetsEqual);
            Require(!result.BecameIndistinguishable, "Previously distinct profiles became equivalent under nested support.");
            return result;
        }).ToArray();
        var oldMinimal = from.Profiles.Where(profile => from.Minimum.InclusionMinimalProfileIds.Contains(profile.ProfileId)).ToArray();
        var newMinimal = to.Profiles.Where(profile => to.Minimum.InclusionMinimalProfileIds.Contains(profile.ProfileId)).ToArray();
        var minimum = new CandidateDensityMinimumChange(from.Minimum.MinimumCandidateCount, to.Minimum.MinimumCandidateCount,
            MinimumFieldCount(from), MinimumFieldCount(to), from.Minimum.MinimumCandidateCount != to.Minimum.MinimumCandidateCount,
            !Set(from.Minimum.InclusionMinimalProfileIds).SequenceEqual(Set(to.Minimum.InclusionMinimalProfileIds)),
            Difference(from.Minimum.ProfilesAtMinimumCount, to.Minimum.ProfilesAtMinimumCount),
            Difference(to.Minimum.ProfilesAtMinimumCount, from.Minimum.ProfilesAtMinimumCount),
            Difference(from.Minimum.InclusionMinimalProfileIds, to.Minimum.InclusionMinimalProfileIds),
            Difference(to.Minimum.InclusionMinimalProfileIds, from.Minimum.InclusionMinimalProfileIds),
            Subset(to.Minimum.ProfilesAtMinimumCount, from.Minimum.ProfilesAtMinimumCount),
            newMinimal.All(profile => oldMinimal.Any(old => old.ActiveAxes.All(profile.ActiveAxes.Contains))),
            !FullNecessary(from) || FullNecessary(to));
        Require(nested && nativeNested && descriptors && minimum.AtMinimumProfilesNested && minimum.EveryNewMinimalContainsOldMinimal &&
            minimum.FullNecessityPreserved && minimum.ToMinimumCount >= minimum.FromMinimumCount &&
            minimum.ToMinimumFieldCount >= minimum.FromMinimumFieldCount && from.Minimum.AllMinimumProfilesReturnSameKeySet &&
            to.Minimum.AllMinimumProfilesReturnSameKeySet && from.FixedConstraintSignature == to.FixedConstraintSignature,
            "Universe identity, fixed constraints or minimum-family invariant failed.");
        return new CandidateDensityPairChange(from.ArmId, to.ArmId, added, nested, nativeNested, descriptors,
            changes, conditionals, equivalences, minimum, true);
    }

    private static bool ValidateFullSupport(CandidateIntervalWidthPublicRequest request, CandidateDensitySupport support,
        CandidateIntervalWidthArm full)
    {
        var time = request.AxisComponents.Single(component => component.Axis == CommonWorldObservationAxis.Time).Component.Predicate.ConstrainedTimeWindow!;
        Require(time.EndTick - time.StartTick <= 3 && full.Candidates.Count == support.SupportCellCount &&
            full.Candidates.Select(candidate => (candidate.TimeWindow.StartTick, candidate.RegionId)).Distinct().Count() == support.SupportCellCount,
            "Full lattice or registered time width failed.");
        foreach (var profile in full.Profiles)
        {
            var regionCount = support.RegisteredRegionIds.Count(region => profile.ActiveAxes.Where(axis => axis != CommonWorldObservationAxis.Time)
                .All(axis => request.AxisComponents.Single(component => component.Axis == axis).Component.Predicate.AllowedRegionIds.Contains(region)));
            var tickCount = profile.ActiveAxes.Contains(CommonWorldObservationAxis.Time)
                ? Enumerable.Range(-3, 7).Count(offset => time.StartTick <= (long)support.BaseTick + offset && (long)support.BaseTick + offset <= time.EndTick) : 7;
            Require(profile.CandidateCount == regionCount * tickCount, "Full-support Cartesian analytic count failed.");
        }

        Require(full.Conditionals.All(conditional => conditional.Diagnostic.Classification != CommonWorldConditionalOutcomeClassification.ConstraintToCandidatePlateau &&
            (conditional.AddedAxis != CommonWorldObservationAxis.Time || conditional.AdditionalCandidateReduction > 0)),
            "Complete support retained a candidate plateau or failed strict T reduction.");
        return true;
    }

    private static bool Matches(CandidateIntervalWidthNativeCandidate candidate, IReadOnlyList<CommonWorldObservationAxis> axes,
        IReadOnlyList<CandidateIntervalWidthAxisComponent> components) => axes.All(axis =>
        {
            var predicate = components.Single(component => component.Axis == axis).Component.Predicate;
            return (predicate.RegionRestriction == CandidateConstraintDimensionRestriction.Unrestricted || predicate.AllowedRegionIds.Contains(candidate.RegionId)) &&
                (predicate.TimeRestriction == CandidateConstraintDimensionRestriction.Unrestricted || Overlaps(candidate.TimeWindow, predicate.ConstrainedTimeWindow!));
        });

    private static bool WorkRule(CandidateIntervalWidthArm arm, CandidateIntervalWidthProfile profile) =>
        profile.CandidateGenerationWork == arm.Candidates.Count * (1 + profile.ActiveAxes.Count) &&
        profile.CompositionAudit.CandidatePredicateChecks == arm.Candidates.Count * profile.ActiveAxes.Count &&
        profile.CompositionAudit.PredicatesApplied == profile.ActiveAxes.Count &&
        profile.CompositionAudit.UsageRole == PastStateEvidenceUsageRole.CandidateGeneration;

    private static CandidateDensityOccupancy Occupancy(CandidateIntervalWidthArm arm, CandidateDensitySupport support)
    {
        var regions = support.RegisteredRegionIds.Select(region => new CandidateDensityHistogramEntry(region,
            arm.Candidates.Count(candidate => candidate.RegionId == region))).ToArray();
        var ticks = Enumerable.Range(-3, 7).Select(offset => new CandidateDensityHistogramEntry(
            ((long)support.BaseTick + offset).ToString(CultureInfo.InvariantCulture),
            arm.Candidates.Count(candidate => candidate.TimeWindow.StartTick == (long)support.BaseTick + offset))).ToArray();
        var unused = regions.Count(region => region.Count == 0);
        return new CandidateDensityOccupancy(arm.ArmId, arm.Candidates.Count,
            support.Arms.Single(item => item.ArmId == arm.ArmId).AddedCandidateCount, support.SupportCellCount,
            Fraction(arm.Candidates.Count, support.SupportCellCount), regions.Length - unused, ticks.Count(tick => tick.Count > 0),
            unused, Fraction(unused, regions.Length), regions, ticks, [new CandidateDensityHistogramEntry("0", arm.Candidates.Count)]);
    }

    private static string[] Excluded(CandidateIntervalWidthArm arm, CandidateIntervalWidthConditional conditional) => Difference(
        arm.Profiles.Single(profile => profile.ProfileId == conditional.BaselineProfileId).CandidateKeys,
        arm.Profiles.Single(profile => profile.ProfileId == conditional.CombinedProfileId).CandidateKeys);
    private static int MinimumFieldCount(CandidateIntervalWidthArm arm) => arm.Profiles
        .Where(profile => arm.Minimum.ProfilesAtMinimumCount.Contains(profile.ProfileId)).Min(profile => profile.ActiveAxes.Count);
    private static bool FullNecessary(CandidateIntervalWidthArm arm) =>
        arm.Minimum.InclusionMinimalProfileIds.SequenceEqual(["location-time-participant"]);
    private static string[] Difference(IEnumerable<string> left, IEnumerable<string> right) => Set(left.Except(right, StringComparer.Ordinal));
    private static decimal Fraction(int numerator, int denominator) => (decimal)numerator / denominator;
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidDataException(message);
    }

    private static CandidateIntervalWidthStatistics Statistics(IEnumerable<int> source)
    {
        var values = source.OrderBy(value => value).ToArray();
        var median = values.Length % 2 == 0 ? (values[values.Length / 2 - 1] + (decimal)values[values.Length / 2]) / 2 : values[values.Length / 2];
        return new CandidateIntervalWidthStatistics(values.Average(value => (decimal)value), median, values[0], values[^1],
            values.GroupBy(value => value).Select(group => new CandidateIntervalWidthDistributionEntry(group.Key, group.Count())).ToArray());
    }

    private static CandidateDensityDecimalStatistics DecimalStatistics(IEnumerable<decimal> source)
    {
        var values = source.OrderBy(value => value).ToArray();
        var median = values.Length % 2 == 0 ? (values[values.Length / 2 - 1] + values[values.Length / 2]) / 2 : values[values.Length / 2];
        return new CandidateDensityDecimalStatistics(values.Average(), median, values[0], values[^1],
            values.GroupBy(value => value).Select(group => new CandidateDensityDecimalDistributionEntry(group.Key, group.Count())).ToArray());
    }

    private static IReadOnlyList<CandidateDensityProfileAggregate> ProfileAggregates(IReadOnlyList<CandidateDensityWorldRun> runs) =>
        CandidateDensityTransform.ArmIds.SelectMany(armId => runs[0].Arms[0].Profiles.Select(profile =>
        {
            var values = runs.Select(run => run.ProfileMetrics.Single(metric => metric.ArmId == armId && metric.ProfileId == profile.ProfileId)).ToArray();
            return new CandidateDensityProfileAggregate(armId, profile.ProfileId, values.Length, Statistics(values.Select(value => value.CandidateCount)),
                Statistics(values.Select(value => value.ReductionVsNone)), DecimalStatistics(values.Select(value => value.ReductionFraction)),
                DecimalStatistics(values.Select(value => value.RetainedFraction)), Statistics(values.Select(value => value.CandidateGenerationWork)),
                values.Count(value => value.ReductionVsNone == 0), values.Count(value => value.ContainsTruth), values.Sum(value => value.FalseExclusionCount));
        })).ToArray();

    private static IReadOnlyList<CandidateDensityConditionalAggregate> ConditionalAggregates(IReadOnlyList<CandidateDensityWorldRun> runs) =>
        CandidateDensityTransform.ArmIds.SelectMany(armId => new[] { "all" }.Concat(runs[0].Arms[0].Conditionals.Select(item => item.ComparisonId))
            .Select(comparisonId =>
            {
                var values = runs.SelectMany(run => run.Arms.Single(arm => arm.ArmId == armId).Conditionals
                    .Where(conditional => comparisonId == "all" || conditional.ComparisonId == comparisonId)
                    .Select(conditional => (run.Seed, Conditional: conditional))).ToArray();
                var classifications = Enum.GetValues<CommonWorldConditionalOutcomeClassification>().Select(classification =>
                {
                    var matching = values.Where(value => value.Conditional.Diagnostic.Classification == classification).ToArray();
                    return new CandidateDensityClassificationCount(classification, matching.Length, matching.Select(value => value.Seed).Distinct().OrderBy(seed => seed).ToArray());
                }).ToArray();
                return new CandidateDensityConditionalAggregate(armId, comparisonId, values.Length,
                    Statistics(values.Select(value => value.Conditional.AdditionalCandidateReduction)),
                    DecimalStatistics(values.Select(value => Fraction(value.Conditional.AdditionalCandidateReduction, value.Conditional.BaselineCandidateCount))),
                    values.Count(value => value.Conditional.AdditionalCandidateReduction == 0), classifications);
            })).ToArray();

    private static IReadOnlyList<CandidateDensityArmAggregate> ArmAggregates(IReadOnlyList<CandidateDensityWorldRun> runs) =>
        CandidateDensityTransform.ArmIds.Select(armId =>
        {
            var values = runs.Select(run => (run.Seed, Arm: run.Arms.Single(arm => arm.ArmId == armId),
                Occupancy: run.Occupancy.Single(occupancy => occupancy.ArmId == armId))).ToArray();
            var families = values.GroupBy(value => string.Join("|", Set(value.Arm.Minimum.InclusionMinimalProfileIds)))
                .OrderBy(group => group.Key, StringComparer.Ordinal).Select(group => new CandidateDensityFamilyFrequency(group.Key, group.Count(),
                    group.Select(value => value.Seed).OrderBy(seed => seed).ToArray())).ToArray();
            var profiles = values[0].Arm.Profiles.Select(profile => new CandidateDensityProfileFrequency(profile.ProfileId,
                values.Count(value => value.Arm.Minimum.ProfilesAtMinimumCount.Contains(profile.ProfileId)),
                values.Count(value => value.Arm.Minimum.InclusionMinimalProfileIds.Contains(profile.ProfileId)))).ToArray();
            return new CandidateDensityArmAggregate(armId, values.Length, Statistics(values.Select(value => value.Occupancy.CandidateCount)),
                DecimalStatistics(values.Select(value => value.Occupancy.Density)), Statistics(values.Select(value => value.Occupancy.OccupiedRegionCount)),
                Statistics(values.Select(value => value.Occupancy.OccupiedTickCount)), DecimalStatistics(values.Select(value => value.Occupancy.UnusedRegionFraction)),
                Statistics(values.Select(value => value.Arm.Minimum.MinimumCandidateCount)), Statistics(values.Select(value => MinimumFieldCount(value.Arm))),
                values.Count(value => FullNecessary(value.Arm)), profiles, families);
        }).ToArray();

    private static IReadOnlyList<CandidateDensityPairAggregate> PairAggregates(IReadOnlyList<CandidateDensityWorldRun> runs) =>
        runs[0].PairChanges.Select(pairDefinition =>
        {
            var values = runs.Select(run => (run.Seed, Pair: run.PairChanges.Single(pair => pair.FromArmId == pairDefinition.FromArmId &&
                pair.ToArmId == pairDefinition.ToArmId))).ToArray();
            var profiles = pairDefinition.ProfileChanges.Select(profile =>
            {
                var matching = values.Select(value => value.Pair.ProfileChanges.Single(change => change.ProfileId == profile.ProfileId)).ToArray();
                return new CandidateDensityPairProfileAggregate(profile.ProfileId, Statistics(matching.Select(change => change.CountDelta)),
                    Statistics(matching.Select(change => change.ReductionDelta)), DecimalStatistics(matching.Select(change => change.ReductionFractionDelta)),
                    matching.Count(change => change.CountDelta != 0), matching.Count(change => change.CountDelta == 0));
            }).ToArray();
            var conditionals = new[] { "all" }.Concat(pairDefinition.ConditionalChanges.Select(item => item.ComparisonId)).Select(comparisonId =>
            {
                var matching = values.SelectMany(value => value.Pair.ConditionalChanges
                    .Where(change => comparisonId == "all" || change.ComparisonId == comparisonId).Select(change => (value.Seed, Change: change))).ToArray();
                var changed = matching.Where(value => value.Change.FromDiagnostic.Classification != value.Change.ToDiagnostic.Classification).ToArray();
                var transitions = Enum.GetValues<CommonWorldConditionalOutcomeClassification>().SelectMany(from =>
                    Enum.GetValues<CommonWorldConditionalOutcomeClassification>().Select(to =>
                    {
                        var transition = matching.Where(value => value.Change.FromDiagnostic.Classification == from && value.Change.ToDiagnostic.Classification == to).ToArray();
                        return new CommonWorldPlateauClassificationTransition
                        {
                            FromClassification = from,
                            ToClassification = to,
                            ComparisonCount = transition.Length,
                            Seeds = transition.Select(value => value.Seed).Distinct().OrderBy(seed => seed).ToArray()
                        };
                    })).ToArray();
                return new CandidateDensityPairConditionalAggregate(comparisonId, matching.Length,
                    Statistics(matching.Select(value => value.Change.ReductionDelta)), DecimalStatistics(matching.Select(value => value.Change.ReductionFractionDelta)),
                    matching.Where(value => value.Change.ReductionDelta != 0).Select(value => value.Seed).Distinct().Count(), changed.Length,
                    changed.Select(value => value.Seed).Distinct().OrderBy(seed => seed).ToArray(), matching.Count(value => value.Change.NewlyDetectableEffect),
                    matching.Count(value => value.Change.LostEffect),
                    matching.Count(value => value.Change.FromDiagnostic.AddedPredicateIsTopForCandidateDomain != value.Change.ToDiagnostic.AddedPredicateIsTopForCandidateDomain),
                    matching.Count(value => value.Change.FromDiagnostic.AddedPredicateImpliedByBaselinePredicates != value.Change.ToDiagnostic.AddedPredicateImpliedByBaselinePredicates), transitions);
            }).ToArray();
            return new CandidateDensityPairAggregate(pairDefinition.FromArmId, pairDefinition.ToArmId, values.Length,
                values.Count(value => value.Pair.MinimumChange.CountChanged),
                values.Count(value => value.Pair.MinimumChange.FromMinimumFieldCount != value.Pair.MinimumChange.ToMinimumFieldCount),
                values.Count(value => value.Pair.MinimumChange.FamilyChanged),
                values.Where(value => value.Pair.MinimumChange.FamilyChanged).Select(value => value.Seed).OrderBy(seed => seed).ToArray(),
                values.Count(value => value.Pair.MinimumChange.LostAtMinimumProfiles.Count != 0),
                values.Sum(value => value.Pair.EquivalenceChanges.Count(change => change.BecameDistinguishable)),
                values.Count(value => value.Pair.EquivalenceChanges.Any(change => change.BecameDistinguishable)),
                values.Sum(value => value.Pair.EquivalenceChanges.Count(change => change.BecameIndistinguishable)), profiles, conditionals);
        }).ToArray();
}
