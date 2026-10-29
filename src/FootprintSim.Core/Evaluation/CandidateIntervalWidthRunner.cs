using System.Text.Json;
using System.Text.Json.Serialization;
using FootprintSim.Evidence;
using FootprintSim.Inference.Derivation;
using FootprintSim.Interactions;
using FootprintSim.Interactions.CandidateWarps;
using FootprintSim.Serialization;
using static FootprintSim.Evaluation.CandidateIntervalWidthPublicEvaluator;

namespace FootprintSim.Evaluation;

public static class CandidateIntervalWidthProtocolLoader
{
    public static CandidateIntervalWidthProtocol Load(string path) => Parse(File.ReadAllText(path));
    public static CandidateIntervalWidthProtocol Parse(string json)
    {
        var protocol = JsonSerializer.Deserialize<CandidateIntervalWidthProtocol>(json,
            new JsonSerializerOptions(JsonDefaults.Options) { UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow })
            ?? throw new InvalidDataException("Empty F18 protocol.");
        CandidateIntervalWidthValidator.Validate(protocol, protocol.BaselineExperimentSha256, Enumerable.Range(0, 200).ToArray());
        return protocol;
    }
}

public static class CandidateIntervalWidthValidator
{
    public static void Validate(CandidateIntervalWidthProtocol protocol, string baselineSha256, IReadOnlyList<int> seeds)
    {
        ArgumentNullException.ThrowIfNull(protocol);
        ArgumentNullException.ThrowIfNull(seeds);
        if (protocol.SchemaVersion != 1 || protocol.Id != "m1f18-candidate-interval-width-paired" ||
            protocol.BaselineFinalCommit != "58bb181593c11fd8afff5d03b057b01b5de3ab3b" ||
            protocol.BaselineExperimentSha256 != CommonWorldPlateauDomainAuditValidator.BaselineSha256 ||
            baselineSha256 != CommonWorldPlateauDomainAuditValidator.BaselineSha256 ||
            protocol.RequiredSeedStart != 0 || protocol.RequiredSeedEnd != 199 || protocol.Arms is null ||
            !protocol.Arms.SequenceEqual(["original", "point-at-start"]) ||
            protocol.TransformRuleId != CandidateIntervalWidthTransform.RuleId ||
            protocol.StableKeyRule != "seed-and-original-candidate-key-bijection" ||
            protocol.TruthRule != "unchanged-original-truth-key-resolved-after-public-inference" ||
            protocol.PlateauRuleId != CommonWorldPlateauDomainClassifier.RuleId ||
            protocol.FixedInference != "original-public-derivations-and-m1f14-composer" ||
            protocol.ProspectiveOutcomeInspected || protocol.FailurePolicy != "fail-no-skip-no-regenerate-no-tuning" ||
            seeds.Count != 200 || !seeds.OrderBy(seed => seed).SequenceEqual(Enumerable.Range(0, 200)))
        {
            throw new InvalidDataException("Protocol, baseline hash, or exact F18 seed set differs from registration.");
        }
    }
}

public sealed class CandidateIntervalWidthRunner
{
    public CandidateIntervalWidthResult Run(CandidateIntervalWidthRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        CandidateIntervalWidthValidator.Validate(request.Protocol, request.BaselineExperimentContentSha256, request.Seeds);
        CommonWorldPlateauDomainAuditRunner.ValidateBaselineDefinition(request.BaselineExperiment);
        if (request.ProtocolContentSha256.Length != 64 || !request.ProtocolContentSha256.All(Uri.IsHexDigit) ||
            string.IsNullOrWhiteSpace(request.SourceCommitSha))
        {
            throw new InvalidDataException("A protocol hash and source commit are required.");
        }

        var seeds = request.Seeds.OrderBy(seed => seed).ToArray();
        var generator = new CommonWorldFieldCombinationSeededWorldGenerator();
        var runs = seeds.Select(seed => EvaluateWorld(request.BaselineExperiment,
            generator.Generate(request.BaselineExperiment, seed), request.SourceCommitSha, request.DirtyWorkingTree)).ToArray();
        var profileAggregates = request.BaselineExperiment.Profiles.OrderBy(profile => profile.Order).Select(profile =>
        {
            var values = runs.Select(run => run.ProfileChanges.Single(item => item.ProfileId == profile.Id)).ToArray();
            return new CandidateIntervalWidthProfileAggregate(profile.Id, values.Length,
                Statistics(values.Select(item => item.OriginalCandidateCount)), Statistics(values.Select(item => item.PointCandidateCount)),
                Statistics(values.Select(item => item.PointMinusOriginalCount)), values.Count(item => !item.KeySetsEqual),
                values.Count(item => item.KeySetsEqual), values.Count(item => item.OriginalContainsTruth),
                values.Count(item => item.PointContainsTruth), values.Sum(item => (item.OriginalContainsTruth ? 0 : 1) + (item.PointContainsTruth ? 0 : 1)));
        }).ToArray();
        var conditionalAggregates = new[] { "all" }.Concat(request.BaselineExperiment.ConditionalComparisons.Select(item => item.Id))
            .Select(comparisonId =>
            {
                var values = runs.SelectMany(run => run.ConditionalChanges.Where(item => comparisonId == "all" || item.ComparisonId == comparisonId)
                    .Select(item => (run.Seed, Value: item))).ToArray();
                var transitions = Enum.GetValues<CommonWorldConditionalOutcomeClassification>().SelectMany(from =>
                    Enum.GetValues<CommonWorldConditionalOutcomeClassification>().Select(to =>
                    {
                        var matching = values.Where(item => item.Value.OriginalDiagnostic.Classification == from && item.Value.PointDiagnostic.Classification == to).ToArray();
                        return new CommonWorldPlateauClassificationTransition
                        {
                            FromClassification = from,
                            ToClassification = to,
                            ComparisonCount = matching.Length,
                            Seeds = matching.Select(item => item.Seed).Distinct().OrderBy(seed => seed).ToArray()
                        };
                    })).ToArray();
                return new CandidateIntervalWidthConditionalAggregate(comparisonId, values.Length,
                    Statistics(values.Select(item => item.Value.OriginalAdditionalReduction)),
                    Statistics(values.Select(item => item.Value.PointAdditionalReduction)),
                    Statistics(values.Select(item => item.Value.PointMinusOriginalReduction)),
                    values.Where(item => item.Value.PointMinusOriginalReduction != 0).Select(item => item.Seed).Distinct().Count(),
                    values.Count(item => item.Value.OriginalDiagnostic.Classification != item.Value.PointDiagnostic.Classification),
                    values.Count(item => item.Value.OriginalDiagnostic.AddedPredicateIsTopForCandidateDomain != item.Value.PointDiagnostic.AddedPredicateIsTopForCandidateDomain),
                    values.Count(item => item.Value.OriginalDiagnostic.AddedPredicateImpliedByBaselinePredicates != item.Value.PointDiagnostic.AddedPredicateImpliedByBaselinePredicates),
                    values.Where(item => item.Value.OriginalDiagnostic.Classification != item.Value.PointDiagnostic.Classification)
                        .Select(item => item.Seed).Distinct().OrderBy(seed => seed).ToArray(), transitions);
            }).ToArray();
        var primaryProfiles = runs.SelectMany(run => run.ProfileChanges).ToArray();
        var summary = new CandidateIntervalWidthSummary(runs.Length, primaryProfiles.Length * 2, runs.Length * 8,
            primaryProfiles.Count(item => item.ExpectedEquality), primaryProfiles.Count(item => !item.ExpectedEquality),
            runs.Sum(run => run.Original.SetInclusions.Count + run.Point.SetInclusions.Count),
            primaryProfiles.Sum(item => (item.OriginalContainsTruth ? 1 : 0) + (item.PointContainsTruth ? 1 : 0)),
            primaryProfiles.Sum(item => (item.OriginalContainsTruth ? 0 : 1) + (item.PointContainsTruth ? 0 : 1)),
            runs.Count(run => run.MinimumChange.MinimumCountChanged), runs.Count(run => run.MinimumChange.InclusionMinimalFamilyChanged),
            runs.Sum(run => run.Crosswalk.Count), runs.Sum(run => run.Crosswalk.Count(item => item.Original.TimeWindow.StartTick != item.Original.TimeWindow.EndTick)),
            0, "This is paired candidate-support sensitivity in the fixed purified F16 family. Start-anchored truth structurally protects containment. " +
                "Removed interval support is not calibrated accuracy, observation precision, dwell time, or rescue work. No general field ranking or minimum observation set follows.");
        return new CandidateIntervalWidthResult(new CandidateIntervalWidthManifest(request.Protocol.Id,
            request.SourceCommitSha, request.DirtyWorkingTree, request.ProtocolContentSha256, request.BaselineExperimentContentSha256,
            request.Protocol.BaselineFinalCommit, request.Protocol.TransformRuleId, request.Protocol.PlateauRuleId, seeds,
            Signature(runs.Select(run => new
            {
                run.Seed,
                run.OriginalWorldBeforeSignature,
                run.RawEvidenceSignature,
                run.SourceGraphSignature,
                run.Original.FixedConstraintSignature
            }).ToArray()),
            Signature(runs.Select(run => new { run.Seed, run.Original, run.Point }).ToArray()), request.Protocol.TruthRule,
            ["candidate-interval-width.json", "candidate-interval-width-runs.jsonl", "candidate-interval-width-profiles.csv",
                "candidate-interval-width-conditionals.csv", "candidate-interval-width-crosswalk.csv", "candidate-interval-width-manifest.json"]),
            summary, runs, profileAggregates, conditionalAggregates);
    }

    public CandidateIntervalWidthWorldRun EvaluateWorld(CommonWorldFieldCombinationExperimentDefinition experiment,
        GeneratedCommonFieldCombinationWorld world, string sourceCommitSha = "test", bool dirtyWorkingTree = false)
    {
        CommonWorldPlateauDomainAuditRunner.ValidateBaselineDefinition(experiment);
        var before = Signature(world);
        var fixture = CommonWorldFieldCombinationWorldEvaluator.ToFixture(experiment, world);
        var reference = CreateReference(experiment, world, sourceCommitSha, dirtyWorkingTree);
        var publicRequest = ProjectPublicRequest(experiment, world, reference);
        var publicBefore = Signature(publicRequest);
        var evaluator = new CandidateIntervalWidthPublicEvaluator();
        var original = evaluator.Evaluate(publicRequest, "original");
        var point = evaluator.Evaluate(publicRequest, "point-at-start");
        var referenceMatches = original.CandidateUniverseId == reference.CandidateUniverseId && original.Profiles.All(profile =>
        {
            var expected = reference.Runs.Single(item => item.ProfileId == profile.ProfileId);
            return profile.CandidateIds.SequenceEqual(expected.CandidateIds) && profile.CandidateCount == expected.CandidateCount &&
                profile.CandidateGenerationWork == expected.CandidateGenerationWork && profile.CompositionId == expected.CompositionId &&
                Signature(profile.CompositionAudit) == Signature(expected.CompositionAudit);
        });
        var crosswalk = original.Candidates.Select(item => new CandidateIntervalWidthCrosswalk(world.Seed, item.CandidateKey,
            item, point.Candidates.Single(other => other.CandidateKey == item.CandidateKey))).ToArray();
        if (!referenceMatches || crosswalk.Length != world.CandidateStates.Count || point.Candidates.Count != crosswalk.Length ||
            original.FixedConstraintSignature != point.FixedConstraintSignature || publicBefore != Signature(publicRequest) ||
            crosswalk.Any(item => item.Original.PublicSourceIdentifier != item.Point.PublicSourceIdentifier ||
                item.Original.RegionId != item.Point.RegionId || item.Original.ProvenanceSignature != item.Point.ProvenanceSignature ||
                item.Original.TimeWindow.StartTick != item.Point.TimeWindow.StartTick || item.Point.TimeWindow.EndTick != item.Point.TimeWindow.StartTick ||
                item.Point.TemporalKind != CandidateWarpTemporalKind.EventPoint ||
                (item.Original.TimeWindow.StartTick == item.Original.TimeWindow.EndTick) != (item.Original.CandidateId == item.Point.CandidateId)))
        {
            throw new InvalidDataException("Original-reference, crosswalk, fixed-input, or multiplicity invariant failed.");
        }

        // Primary-arm truth addresses are mapped after both public results and their crosswalk exist.
        // The separate legacy reference retains its existing evaluation-only truth-address check.
        var truth = crosswalk.Single(item => item.CandidateKey == world.TruthCandidateKey);
        var fixedTruth = new CandidateIntervalWidthFixedTruth(world.TruthWorldId, world.TruthCandidateKey,
            world.TruthCandidateStartTick, world.TruthCandidateEndTick, world.TruthRegionId, world.TruthParticipantId,
            world.EncounterTick, world.ActualTruthDurationTicks, truth.Original.CandidateId, truth.Point.CandidateId, true);
        if (truth.Original.TimeWindow.StartTick != fixedTruth.OriginalTruthStartTick ||
            truth.Original.TimeWindow.EndTick != fixedTruth.OriginalTruthEndTick || truth.Original.RegionId != fixedTruth.OriginalTruthRegionId ||
            truth.Original.CandidateId != reference.TruthCandidateId)
        {
            throw new InvalidDataException("Fixed original truth does not match its public crosswalk address.");
        }

        var lower = reference.TimeConstraint.ConstrainedTimeWindow.StartTick;
        var changes = original.Profiles.Select(profile =>
        {
            var other = point.Profiles.Single(item => item.ProfileId == profile.ProfileId);
            var noTime = !profile.ActiveAxes.Contains(CommonWorldObservationAxis.Time);
            var lost = Set(profile.CandidateKeys.Except(other.CandidateKeys, StringComparer.Ordinal));
            var added = Set(other.CandidateKeys.Except(profile.CandidateKeys, StringComparer.Ordinal));
            var expectedLost = noTime ? [] : Set(profile.CandidateKeys.Where(key => crosswalk.Single(item => item.CandidateKey == key).Original.TimeWindow.StartTick < lower));
            var witnesses = lost.Select(key =>
            {
                var candidate = crosswalk.Single(item => item.CandidateKey == key).Original;
                return new CandidateIntervalWidthLostWitness(key, candidate.TimeWindow.StartTick, candidate.TimeWindow.EndTick,
                    lower, candidate.TimeWindow.EndTick == lower);
            }).ToArray();
            var workSame = profile.CandidateGenerationWork == other.CandidateGenerationWork && profile.CompositionId == other.CompositionId &&
                Signature(profile.CompositionAudit) == Signature(other.CompositionAudit);
            var result = new CandidateIntervalWidthProfileChange(profile.ProfileId, profile.CandidateCount, other.CandidateCount,
                other.CandidateCount - profile.CandidateCount, profile.CandidateIds, other.CandidateIds,
                profile.CandidateKeys, other.CandidateKeys, lost, added, witnesses, noTime,
                profile.CandidateKeys.SequenceEqual(other.CandidateKeys), Subset(other.CandidateKeys, profile.CandidateKeys),
                lost.SequenceEqual(expectedLost) && witnesses.All(item => item.OriginalStartTick < lower && item.OriginalEndTick >= lower),
                workSame, profile.CandidateIds.Contains(truth.Original.CandidateId), other.CandidateIds.Contains(truth.Point.CandidateId));
            if (!result.PointKeysAreSubset || noTime && !result.KeySetsEqual || !result.LostKeysMatchBoundaryRule ||
                !workSame || !result.OriginalContainsTruth || !result.PointContainsTruth)
            {
                throw new InvalidDataException("Paired key, boundary, work, or truth-containment invariant failed.");
            }

            return result;
        }).ToArray();
        var conditionalChanges = original.Conditionals.Select(item =>
        {
            var other = point.Conditionals.Single(value => value.ComparisonId == item.ComparisonId);
            var baselineHasTime = original.Profiles.Single(profile => profile.ProfileId == item.BaselineProfileId).ActiveAxes.Contains(CommonWorldObservationAxis.Time);
            var delta = other.AdditionalCandidateReduction - item.AdditionalCandidateReduction;
            var sign = item.AddedAxis == CommonWorldObservationAxis.Time ? "nonnegative" : baselineHasTime ? "nonpositive" : "zero";
            var valid = sign == "nonnegative" ? delta >= 0 : sign == "nonpositive" ? delta <= 0 : delta == 0;
            var flagsValid = item.Diagnostic.AddedPredicateImpliedByBaselinePredicates == other.Diagnostic.AddedPredicateImpliedByBaselinePredicates &&
                (item.AddedAxis == CommonWorldObservationAxis.Time
                    ? item.Diagnostic.AddedPredicateIsTopForCandidateDomain || !other.Diagnostic.AddedPredicateIsTopForCandidateDomain
                    : item.Diagnostic.AddedPredicateIsTopForCandidateDomain == other.Diagnostic.AddedPredicateIsTopForCandidateDomain);
            if (!valid || !flagsValid)
            {
                throw new InvalidDataException("Conditional signed-delta or fixed-domain logical-flag invariant failed.");
            }

            return new CandidateIntervalWidthConditionalChange(item.ComparisonId, item.AddedAxis, sign,
                item.AdditionalCandidateReduction, other.AdditionalCandidateReduction, delta, item.Diagnostic, other.Diagnostic, valid);
        }).ToArray();
        var minimumChange = new CandidateIntervalWidthMinimumChange(original.Minimum.MinimumCandidateCount,
            point.Minimum.MinimumCandidateCount, original.Minimum.MinimumCandidateCount != point.Minimum.MinimumCandidateCount,
            !Set(original.Minimum.InclusionMinimalProfileIds).SequenceEqual(Set(point.Minimum.InclusionMinimalProfileIds)),
            Difference(point.Minimum.ProfilesAtMinimumCount, original.Minimum.ProfilesAtMinimumCount),
            Difference(original.Minimum.ProfilesAtMinimumCount, point.Minimum.ProfilesAtMinimumCount),
            Difference(point.Minimum.InclusionMinimalProfileIds, original.Minimum.InclusionMinimalProfileIds),
            Difference(original.Minimum.InclusionMinimalProfileIds, point.Minimum.InclusionMinimalProfileIds));
        var after = Signature(world);
        if (before != after || !original.Minimum.AllMinimumProfilesReturnSameKeySet || !point.Minimum.AllMinimumProfilesReturnSameKeySet)
        {
            throw new InvalidDataException("Original world changed or count-minimum key sets disagree.");
        }

        return new CandidateIntervalWidthWorldRun(world.Seed, world, fixedTruth, crosswalk, original, point,
            changes, conditionalChanges, minimumChange, before, after,
            Signature(new
            {
                fixture.LocationObservation,
                fixture.TimeObservation,
                fixture.ParticipantObservation,
                fixture.FixedRelationKind,
                fixture.EvidenceAvailabilityCutoff
            }), Signature(reference.SourceGraph), referenceMatches, true);
    }

    public static CommonWorldObservationFixtureEvaluationResult CreateReference(CommonWorldFieldCombinationExperimentDefinition experiment,
        GeneratedCommonFieldCombinationWorld world, string sourceCommitSha = "test", bool dirtyWorkingTree = false) =>
        new CommonWorldObservationFixtureEvaluationRunner().Run(new CommonWorldObservationFixtureEvaluationRequest
        {
            Fixture = CommonWorldFieldCombinationWorldEvaluator.ToFixture(experiment, world),
            FixtureContentSha256 = world.PublicWorldId,
            SourceCommitSha = sourceCommitSha,
            DirtyWorkingTree = dirtyWorkingTree
        });

    public static CandidateIntervalWidthPublicRequest ProjectPublicRequest(CommonWorldFieldCombinationExperimentDefinition experiment,
        GeneratedCommonFieldCombinationWorld world, CommonWorldObservationFixtureEvaluationResult reference) => new()
        {
            ScenarioId = experiment.ScenarioId,
            Seed = world.Seed,
            TargetEntityId = experiment.TargetEntityId,
            TargetStateContextId = experiment.TargetStateContextId,
            CandidateDomainId = experiment.CandidateDomainId,
            EvidenceAvailabilityCutoff = world.EvidenceAvailabilityCutoff,
            RegisteredRegionIds = world.RegionUniverse,
            Candidates = world.CandidateStates.Select(item => new CandidateIntervalWidthPublicCandidate
            {
                CandidateKey = item.CandidateKey,
                PublicSourceIdentifier = item.PublicSourceIdentifier,
                State = new PublicCandidateWarpStateDefinition
                {
                    TimeWindow = new InteractionTimeWindow(item.StartTick, item.EndTick),
                    TemporalKind = item.StartTick == item.EndTick ? CandidateWarpTemporalKind.EventPoint : CandidateWarpTemporalKind.StateInterval,
                    RegionIds = [item.RegionId],
                    EvidenceBoundaryState = EvidenceState.Confirmed,
                    Provenance = [CandidateWarpProvenance.Create(CandidateWarpSourceKind.PublicCausalModel,
                        item.PublicSourceIdentifier, 0, false, false, EvidenceState.Confirmed, [item.RegionId])]
                }
            }).ToArray(),
            AxisComponents =
            [
                new() { Axis = CommonWorldObservationAxis.Location,
                    Component = CandidateGenerationConstraintComponent.FromLocationDerivedConstraint(reference.LocationConstraint,
                        experiment.TargetStateContextId, experiment.CandidateDomainId) },
                new() { Axis = CommonWorldObservationAxis.Time,
                    Component = CandidateGenerationConstraintComponent.FromDerivedConstraint(reference.TimeConstraint,
                        experiment.TargetStateContextId, experiment.CandidateDomainId, CandidateConstraintDimensionSelection.TimeOnly) },
                new() { Axis = CommonWorldObservationAxis.Participant,
                    Component = CandidateGenerationConstraintComponent.FromDerivedConstraint(reference.ParticipantConstraint,
                        experiment.TargetStateContextId, experiment.CandidateDomainId, CandidateConstraintDimensionSelection.RegionOnly) }
            ],
            Profiles = experiment.Profiles,
            PrimarySetInclusions = experiment.PrimarySetInclusions,
            ConditionalComparisons = experiment.ConditionalComparisons
        };

    private static string[] Difference(IEnumerable<string> left, IEnumerable<string> right) => Set(left.Except(right, StringComparer.Ordinal));
    private static CandidateIntervalWidthStatistics Statistics(IEnumerable<int> source)
    {
        var values = source.OrderBy(value => value).ToArray();
        var median = values.Length % 2 == 0 ? (values[values.Length / 2 - 1] + (decimal)values[values.Length / 2]) / 2 : values[values.Length / 2];
        return new CandidateIntervalWidthStatistics(values.Average(value => (decimal)value), median, values[0], values[^1],
            values.GroupBy(value => value).Select(group => new CandidateIntervalWidthDistributionEntry(group.Key, group.Count())).ToArray());
    }
}
