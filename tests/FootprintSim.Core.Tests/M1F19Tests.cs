using System.Security.Cryptography;
using FootprintSim.Cli;
using FootprintSim.Evidence;
using FootprintSim.Evaluation;
using FootprintSim.Inference.Derivation;
using FootprintSim.Inference.PastStates;
using FootprintSim.Interactions;
using FootprintSim.Interactions.CandidateWarps;
using FootprintSim.Serialization;
using Xunit;

namespace FootprintSim.Core.Tests;

public sealed class M1F19Tests
{
    private const string ScenarioId = "m1f19-analytic-public-support";
    private static readonly int[] Seeds = Enumerable.Range(0, 200).ToArray();
    private static readonly Lazy<CandidateDensityResult> Result = new(() => new CandidateDensityRunner().Run(Request()));

    [Fact]
    public void PublicSupportBoundaryCannotReceiveTruthEvidenceOrProfileResults()
    {
        var method = typeof(CandidateDensityTransform).GetMethod("Apply")!;
        Assert.Equal([typeof(CandidateDensityPublicSupportRequest)],
            method.GetParameters().Select(parameter => parameter.ParameterType));
        Assert.Equal(new[] { "BaseTick", "BaselinePointCandidates", "RegisteredRegionIds", "Seed" },
            typeof(CandidateDensityPublicSupportRequest).GetProperties().Select(property => property.Name)
                .Order(StringComparer.Ordinal));
        Assert.DoesNotContain(typeof(CandidateIntervalWidthPublicCandidate).GetProperties(), property =>
            property.Name.Contains("Truth", StringComparison.OrdinalIgnoreCase) ||
            property.PropertyType == typeof(GeneratedCommonFieldCombinationWorld));
    }

    [Fact]
    public void RegisteredHashPrefixFillsOnlyMissingCellsAndPreservesOldObjects()
    {
        var request = SupportRequest(["a", "b"]);
        var before = JsonDefaults.Serialize(request);
        var support = new CandidateDensityTransform().Apply(request);

        Assert.Equal(14, support.SupportCellCount);
        Assert.Equal(new[] { "point-baseline", "half-missing", "full-support" }, support.Arms.Select(arm => arm.ArmId));
        Assert.Equal(new[] { 0, 6, 12 }, support.Arms.Select(arm => arm.AddedCandidateCount));
        Assert.Equal(new[] { 2, 8, 14 }, support.Arms.Select(arm => arm.Candidates.Count));
        Assert.Equal(new[] { (8L, "a"), (12L, "a"), (8L, "b"), (9L, "b"), (11L, "a"), (11L, "b") },
            support.RankedMissingCells.Take(6).Select(cell => (cell.StartTick, cell.RegionId)));
        Assert.Equal("080ddf88ec4865af981b5b1143a46cf5c8c07ee7f55939ef6175960345b57cce",
            support.RankedMissingCells[0].OrderHash);
        Assert.Equal("density-t8-r00", support.RankedMissingCells[0].CandidateKey);
        Assert.Equal("seed-007-density-t8-r00-public-model", support.RankedMissingCells[0].PublicSourceIdentifier);
        Assert.Equal(before, JsonDefaults.Serialize(request));
        foreach (var arm in support.Arms)
        {
            foreach (var candidate in request.BaselinePointCandidates)
            {
                Assert.Equal(JsonDefaults.Serialize(candidate),
                    JsonDefaults.Serialize(arm.Candidates.Single(item => item.CandidateKey == candidate.CandidateKey)));
            }

            Assert.Equal(arm.Candidates.Count, arm.Candidates.Select(candidate =>
                (candidate.State.TimeWindow.StartTick, candidate.State.RegionIds.Single())).Distinct().Count());
        }

        Assert.Equal(support.RankedMissingCells.Take(6).Select(cell => cell.CandidateKey).Order(StringComparer.Ordinal),
            support.Arms[1].Candidates.Where(candidate => !request.BaselinePointCandidates.Any(old =>
                old.CandidateKey == candidate.CandidateKey)).Select(candidate => candidate.CandidateKey).Order(StringComparer.Ordinal));
        Assert.Equal(JsonDefaults.Serialize(support), JsonDefaults.Serialize(new CandidateDensityTransform().Apply(request with
        {
            RegisteredRegionIds = request.RegisteredRegionIds.Reverse().ToArray(),
            BaselinePointCandidates = request.BaselinePointCandidates.Reverse().ToArray()
        })));
    }

    [Fact]
    public void AddedSourcesAreHypothesisAddressesWithOneSharedModelAncestry()
    {
        var request = SupportRequest(["a", "b", "c", "d", "e"]);
        var support = new CandidateDensityTransform().Apply(request);
        var added = support.Arms[2].Candidates.Where(candidate => !request.BaselinePointCandidates.Any(old =>
            old.CandidateKey == candidate.CandidateKey)).ToArray();
        Assert.Equal(16, support.Arms[1].AddedCandidateCount);
        Assert.Equal(18, support.Arms[1].Candidates.Count);
        Assert.Equal(33, added.Length);
        Assert.Equal(33, added.Select(candidate => candidate.PublicSourceIdentifier).Distinct().Count());
        Assert.All(added, candidate =>
        {
            var provenance = Assert.Single(candidate.State.Provenance);
            Assert.Equal(CandidateWarpSourceKind.PublicCausalModel, provenance.SourceKind);
            Assert.Equal(InteractionVisibility.PublicEvidence, provenance.Visibility);
            Assert.Equal(EvidenceState.Confirmed, provenance.EvidenceBoundaryState);
            Assert.Equal(candidate.PublicSourceIdentifier, provenance.SourceIdentifier);
            Assert.Equal(0, provenance.AvailableAtTick);
            Assert.False(provenance.IsPersonEvidence);
            Assert.False(provenance.IsDeviceOnlyEvidence);
            Assert.Null(provenance.SourceObservationId);
            Assert.Null(provenance.RelationKind);
            Assert.Empty(provenance.Participants);
            var ancestry = Assert.Single(provenance.InteractionProvenance);
            Assert.Equal(InteractionProvenanceKind.ExternalSource, ancestry.Kind);
            Assert.Equal("m1f19-seed-007-point-support-model", ancestry.SourceIdentifier);
            Assert.Equal(CandidateWarpTemporalKind.EventPoint, candidate.State.TemporalKind);
            Assert.Equal(candidate.State.TimeWindow.StartTick, candidate.State.TimeWindow.EndTick);
        });
    }

    [Theory]
    [InlineData("negative-seed")]
    [InlineData("negative-support-tick")]
    [InlineData("empty-regions")]
    [InlineData("duplicate-region")]
    [InlineData("empty-baseline")]
    [InlineData("interval")]
    [InlineData("wrong-kind")]
    [InlineData("outside-ticks")]
    [InlineData("outside-regions")]
    [InlineData("duplicate-key")]
    [InlineData("duplicate-source")]
    [InlineData("duplicate-cell")]
    [InlineData("future-key-collision")]
    [InlineData("future-source-collision")]
    public void TransformRejectsMalformedSupportBeforeConstructingAnyArm(string defect)
    {
        var request = SupportRequest(["a", "b"]);
        var candidates = request.BaselinePointCandidates.ToArray();
        var first = candidates[0];
        request = defect switch
        {
            "negative-seed" => request with { Seed = -1 },
            "negative-support-tick" => request with { BaseTick = 2 },
            "empty-regions" => request with { RegisteredRegionIds = [] },
            "duplicate-region" => request with { RegisteredRegionIds = ["a", "a", "b"] },
            "empty-baseline" => request with { BaselinePointCandidates = [] },
            _ => request
        };
        candidates[0] = defect switch
        {
            "interval" => first with { State = first.State with { TimeWindow = new InteractionTimeWindow(10, 11), TemporalKind = CandidateWarpTemporalKind.StateInterval } },
            "wrong-kind" => first with { State = first.State with { TemporalKind = CandidateWarpTemporalKind.StateInterval } },
            "outside-ticks" => Point(first.CandidateKey, 6, "a"),
            "outside-regions" => Point(first.CandidateKey, 10, "unknown"),
            "duplicate-key" => first with { CandidateKey = candidates[1].CandidateKey },
            "duplicate-source" => WithSource(first, candidates[1].PublicSourceIdentifier),
            "duplicate-cell" => Point(first.CandidateKey, 10, "b"),
            "future-key-collision" => first with { CandidateKey = "density-t7-r00" },
            "future-source-collision" => WithSource(first, "seed-007-density-t7-r00-public-model"),
            _ => first
        };
        if (defect is not ("negative-seed" or "negative-support-tick" or "empty-regions" or "duplicate-region" or "empty-baseline"))
        {
            request = request with { BaselinePointCandidates = candidates };
        }

        Assert.Throws<InvalidDataException>(() => new CandidateDensityTransform().Apply(request));
    }

    [Theory]
    [InlineData("person")]
    [InlineData("device")]
    [InlineData("observation")]
    [InlineData("unconfirmed")]
    [InlineData("available-later")]
    [InlineData("nonmodel")]
    public void TransformRejectsEvidenceMasqueradingAsPublicModelSupport(string defect)
    {
        var request = SupportRequest(["a", "b"]);
        var candidate = request.BaselinePointCandidates[0];
        var provenance = CandidateWarpProvenance.Create(
            defect == "nonmodel" ? CandidateWarpSourceKind.PersonObservation : CandidateWarpSourceKind.PublicCausalModel,
            candidate.PublicSourceIdentifier, defect == "available-later" ? 1 : 0,
            isPersonEvidence: defect == "person", isDeviceOnlyEvidence: defect == "device",
            defect == "unconfirmed" ? EvidenceState.Indeterminate : EvidenceState.Confirmed, ["a"],
            sourceObservationId: defect == "observation" ? "new-observation" : null);
        var changed = request with
        {
            BaselinePointCandidates = [candidate with { State = candidate.State with { Provenance = [provenance] } }, request.BaselinePointCandidates[1]]
        };
        Assert.Throws<InvalidDataException>(() => new CandidateDensityTransform().Apply(changed));
    }

    [Fact]
    public void CompletingCartesianSupportExposesHiddenEffectsAndReplacesMinimumFamily()
    {
        var request = AnalyticPublicRequest();
        var support = new CandidateDensityTransform().Apply(SupportRequest(request.RegisteredRegionIds));
        var arms = CandidateDensityRunner.EvaluatePublicArms(request, support);
        var baseline = arms[0];
        var complete = arms[2];

        Assert.Equal(new[] { "participant" }, baseline.Minimum.InclusionMinimalProfileIds);
        Assert.Equal(new[] { "location-time-participant" }, complete.Minimum.InclusionMinimalProfileIds);
        Assert.Equal(1, baseline.Minimum.MinimumCandidateCount);
        Assert.Equal(2, complete.Minimum.MinimumCandidateCount);
        Assert.Equal(new[] { 35, 21, 10, 14, 6, 7, 4, 2 }, complete.Profiles.Select(profile => profile.CandidateCount));
        Assert.Equal(CommonWorldConditionalOutcomeClassification.ConstraintToCandidatePlateau,
            baseline.Conditionals.Single(item => item.ComparisonId == "location-added-to-time").Diagnostic.Classification);
        Assert.Equal(CommonWorldConditionalOutcomeClassification.AdditionalCandidateReduction,
            complete.Conditionals.Single(item => item.ComparisonId == "location-added-to-time").Diagnostic.Classification);
        Assert.Equal(CommonWorldConditionalOutcomeClassification.ObservationToConstraintPlateau,
            baseline.Conditionals.Single(item => item.ComparisonId == "time-added-to-location").Diagnostic.Classification);
        Assert.Equal(CommonWorldConditionalOutcomeClassification.AdditionalCandidateReduction,
            complete.Conditionals.Single(item => item.ComparisonId == "time-added-to-location").Diagnostic.Classification);
        Assert.DoesNotContain(complete.Conditionals, item =>
            item.Diagnostic.Classification == CommonWorldConditionalOutcomeClassification.ConstraintToCandidatePlateau);
        Assert.Equal(baseline.FixedConstraintSignature, complete.FixedConstraintSignature);
        Assert.All(baseline.Candidates, candidate => Assert.Equal(JsonDefaults.Serialize(candidate),
            JsonDefaults.Serialize(complete.Candidates.Single(item => item.CandidateKey == candidate.CandidateKey))));

        // A previously sufficient profile can be replaced by a strict field superset; the family itself is not nested.
        Assert.DoesNotContain("participant", complete.Minimum.InclusionMinimalProfileIds);
        Assert.Contains("participant", baseline.Minimum.ProfilesAtMinimumCount);
        Assert.DoesNotContain("participant", complete.Minimum.ProfilesAtMinimumCount);
    }

    [Fact]
    public void PublicPermutationLeavesTransformAndInferenceByteIdentical()
    {
        var request = AnalyticPublicRequest();
        var supportRequest = SupportRequest(request.RegisteredRegionIds);
        var support = new CandidateDensityTransform().Apply(supportRequest);
        var reorderedSupport = new CandidateDensityTransform().Apply(supportRequest with
        {
            RegisteredRegionIds = supportRequest.RegisteredRegionIds.Reverse().ToArray(),
            BaselinePointCandidates = supportRequest.BaselinePointCandidates.Reverse().ToArray()
        });
        var reordered = request with
        {
            Candidates = request.Candidates.Reverse().ToArray(),
            RegisteredRegionIds = request.RegisteredRegionIds.Reverse().ToArray(),
            AxisComponents = request.AxisComponents.Reverse().ToArray(),
            Profiles = request.Profiles.Reverse().ToArray()
        };
        Assert.Equal(JsonDefaults.Serialize(CandidateDensityRunner.EvaluatePublicArms(request, support)),
            JsonDefaults.Serialize(CandidateDensityRunner.EvaluatePublicArms(reordered, reorderedSupport)));
    }

    [Theory]
    [InlineData("seed")]
    [InlineData("region-domain")]
    [InlineData("baseline-source")]
    [InlineData("ranked-prefix")]
    public void PublicEvaluatorRejectsSupportFromAnotherInputOrAChangedOrdering(string defect)
    {
        var request = AnalyticPublicRequest();
        var support = new CandidateDensityTransform().Apply(SupportRequest(request.RegisteredRegionIds));
        request = defect switch
        {
            "seed" => request with { Seed = 8 },
            "region-domain" => request with { RegisteredRegionIds = request.RegisteredRegionIds.Append("f").ToArray() },
            "baseline-source" => request with
            {
                Candidates = [WithSource(request.Candidates[0], "another-public-model-source"), request.Candidates[1]]
            },
            _ => request
        };
        if (defect == "ranked-prefix") support = support with { RankedMissingCells = support.RankedMissingCells.Reverse().ToArray() };
        Assert.Throws<InvalidDataException>(() => CandidateDensityRunner.EvaluatePublicArms(request, support));
    }

    [Theory]
    [InlineData("missing-seed")]
    [InlineData("extra-seed")]
    [InlineData("duplicate-seed")]
    [InlineData("baseline-hash")]
    [InlineData("baseline-generator")]
    [InlineData("support-rule")]
    [InlineData("ordering-rule")]
    [InlineData("arm-order")]
    [InlineData("density-level")]
    [InlineData("width")]
    [InlineData("truth-rule")]
    public void RunnerRejectsUnregisteredProtocolAndSeedChanges(string defect)
    {
        var request = Request();
        request = defect switch
        {
            "missing-seed" => request with { Seeds = Seeds.Skip(1).ToArray() },
            "extra-seed" => request with { Seeds = Seeds.Append(200).ToArray() },
            "duplicate-seed" => request with { Seeds = Seeds.SkipLast(1).Append(0).ToArray() },
            "baseline-hash" => request with { BaselineExperimentContentSha256 = new string('0', 64) },
            "baseline-generator" => request with { BaselineExperiment = request.BaselineExperiment with { Generator = request.BaselineExperiment.Generator with { CandidateCountMaximum = 17 } } },
            "support-rule" => request with { Protocol = request.Protocol with { SupportRuleId = "occupied-regions-only" } },
            "ordering-rule" => request with { Protocol = request.Protocol with { OrderingRuleId = "truth-first" } },
            "arm-order" => request with { Protocol = request.Protocol with { Arms = ["point-baseline", "full-support", "half-missing"] } },
            "density-level" => request with { Protocol = request.Protocol with { AdditionNumerators = [0, 0, 2] } },
            "width" => request with { Protocol = request.Protocol with { AddedWidthTicks = 1 } },
            "truth-rule" => request with { Protocol = request.Protocol with { TruthRule = "reselect-truth" } },
            _ => throw new ArgumentOutOfRangeException(nameof(defect))
        };
        Assert.Throws<InvalidDataException>(() => new CandidateDensityRunner().Run(request));
    }

    [Fact]
    public void RegisteredWorldsKeepEvidenceAndTruthFixedAndMatchRawPublicCartesianPredicates()
    {
        var experiment = Experiment();
        var generator = new CommonWorldFieldCombinationSeededWorldGenerator();
        Assert.Equal(Seeds, Result.Value.Runs.Select(run => run.Seed));
        foreach (var run in Result.Value.Runs)
        {
            var world = run.OriginalWorld;
            Assert.Equal(JsonDefaults.Serialize(generator.Generate(experiment, run.Seed)), JsonDefaults.Serialize(world));
            Assert.Equal(run.OriginalWorldBeforeSignature, run.OriginalWorldAfterSignature);
            Assert.Equal(run.PublicRequestBeforeSignature, run.PublicRequestAfterSignature);
            Assert.True(run.F18PointReferenceMatches);
            Assert.True(run.FixedTruth.ResolvedAfterAllPublicArms);
            Assert.Equal(world.TruthCandidateKey, run.FixedTruth.OriginalTruthCandidateKey);
            Assert.Equal(world.TruthWorldId, run.FixedTruth.OriginalTruthWorldId);
            Assert.Equal(world.TruthCandidateEndTick, run.FixedTruth.OriginalTruthEndTick);
            Assert.Equal(world.TruthParticipantId, run.FixedTruth.OriginalTruthParticipantId);
            Assert.Equal(world.EncounterTick, run.FixedTruth.OriginalEncounterTick);
            Assert.Equal(7 * world.RegionUniverse.Count, run.Support.SupportCellCount);
            Assert.Equal(world.CandidateBaseTick, run.Support.BaseTick);
            Assert.Equal(world.CandidateCount, run.IntervalToPointCrosswalk.Count);
            Assert.Single(run.Arms.Select(arm => arm.FixedConstraintSignature).Distinct());
            var location = world.LocationObservedRegionIds.ToHashSet(StringComparer.Ordinal);
            var participant = world.Participants.Where(person => world.ObservedPossibleParticipantIds.Contains(person.ParticipantId))
                .SelectMany(person => person.AllowedRegionIds).ToHashSet(StringComparer.Ordinal);
            var lower = world.LaterEventTick - world.DurationMaximumTicks;
            var upper = world.LaterEventTick - world.DurationMinimumTicks;
            var timeCellCount = Enumerable.Range(world.CandidateBaseTick - 3, 7).Count(tick => lower <= tick && tick <= upper);

            foreach (var arm in run.Arms)
            {
                Assert.Equal(arm.Candidates.Count, arm.Candidates.Select(candidate => candidate.CandidateId).Distinct().Count());
                foreach (var profile in arm.Profiles)
                {
                    var expected = arm.Candidates.Where(candidate =>
                        (!profile.ActiveAxes.Contains(CommonWorldObservationAxis.Location) || location.Contains(candidate.RegionId)) &&
                        (!profile.ActiveAxes.Contains(CommonWorldObservationAxis.Participant) || participant.Contains(candidate.RegionId)) &&
                        (!profile.ActiveAxes.Contains(CommonWorldObservationAxis.Time) ||
                            lower <= candidate.TimeWindow.StartTick && candidate.TimeWindow.StartTick <= upper)).ToArray();
                    Assert.Equal(expected.Select(candidate => candidate.CandidateKey).Order(StringComparer.Ordinal), profile.CandidateKeys);
                    Assert.Equal(expected.Select(candidate => candidate.CandidateId).Order(StringComparer.Ordinal), profile.CandidateIds);
                    Assert.Contains(world.TruthCandidateKey, profile.CandidateKeys);
                    var metric = run.ProfileMetrics.Single(item => item.ArmId == arm.ArmId && item.ProfileId == profile.ProfileId);
                    Assert.Equal(arm.Candidates.Count - expected.Length, metric.ReductionVsNone);
                    Assert.Equal((decimal)(arm.Candidates.Count - expected.Length) / arm.Candidates.Count, metric.ReductionFraction);
                    Assert.Equal((decimal)expected.Length / arm.Candidates.Count, metric.RetainedFraction);
                    Assert.True(metric.ContainsTruth);
                    Assert.Equal(0, metric.FalseExclusionCount);
                    Assert.True(metric.WorkRuleSatisfied);
                    if (arm.ArmId == "full-support")
                    {
                        var regionCount = world.RegionUniverse.Count(region =>
                            (!profile.ActiveAxes.Contains(CommonWorldObservationAxis.Location) || location.Contains(region)) &&
                            (!profile.ActiveAxes.Contains(CommonWorldObservationAxis.Participant) || participant.Contains(region)));
                        Assert.Equal(regionCount * (profile.ActiveAxes.Contains(CommonWorldObservationAxis.Time) ? timeCellCount : 7),
                            profile.CandidateCount);
                    }
                }

                var address = run.FixedTruth.ArmAddresses.Single(item => item.ArmId == arm.ArmId);
                Assert.All(arm.Profiles, profile => Assert.Contains(address.CandidateId, profile.CandidateIds));
                var occupancy = run.Occupancy.Single(item => item.ArmId == arm.ArmId);
                Assert.Equal(arm.Candidates.Count, occupancy.RegionHistogram.Sum(entry => entry.Count));
                Assert.Equal(arm.Candidates.Count, occupancy.StartTickHistogram.Sum(entry => entry.Count));
                var width = Assert.Single(occupancy.WidthHistogram);
                Assert.Equal("0", width.Value);
                Assert.Equal(arm.Candidates.Count, width.Count);
                Assert.Equal((decimal)arm.Candidates.Count / run.Support.SupportCellCount, occupancy.Density);
            }

            var full = run.Arms.Single(arm => arm.ArmId == "full-support");
            Assert.Equal(run.Support.SupportCellCount, full.Candidates.Count);
            var completeOccupancy = run.Occupancy.Single(item => item.ArmId == "full-support");
            Assert.Equal(0, completeOccupancy.UnusedRegionCount);
            Assert.Equal(0m, completeOccupancy.UnusedRegionFraction);
            Assert.Equal(7, completeOccupancy.OccupiedTickCount);
            Assert.Equal(world.RegionUniverse.Count, completeOccupancy.OccupiedRegionCount);
            Assert.All(completeOccupancy.RegionHistogram, entry => Assert.Equal(7, entry.Count));
            Assert.All(completeOccupancy.StartTickHistogram, entry => Assert.Equal(world.RegionUniverse.Count, entry.Count));
            Assert.DoesNotContain(full.Conditionals, item => item.Diagnostic.Classification ==
                CommonWorldConditionalOutcomeClassification.ConstraintToCandidatePlateau);
            Assert.All(full.Conditionals.Where(item => item.AddedAxis == CommonWorldObservationAxis.Time),
                item => Assert.True(item.AdditionalCandidateReduction > 0));
            Assert.True(run.FullSupportAnalyticCountsMatch);
        }

        Assert.Equal(4800, Result.Value.Summary.PrimaryProfileEvaluationCount);
        Assert.Equal(4800, Result.Value.Summary.ProfileNestingCheckCount);
        Assert.Equal(7200, Result.Value.Summary.JointSetInclusionCheckCount);
        Assert.Equal(4800, Result.Value.Summary.TruthContainmentCount);
        Assert.Equal(0, Result.Value.Summary.FalseExclusionCount);
        Assert.Equal(0, Result.Value.Summary.InvariantViolationCount);
    }

    [Fact]
    public void EveryRegisteredPairPreservesOldDecisionsAndCanOnlyExposeAdditionalConstraintWitnesses()
    {
        foreach (var run in Result.Value.Runs)
        {
            foreach (var pair in run.PairChanges)
            {
                var from = run.Arms.Single(arm => arm.ArmId == pair.FromArmId);
                var to = run.Arms.Single(arm => arm.ArmId == pair.ToArmId);
                var oldKeys = from.Candidates.Select(candidate => candidate.CandidateKey).ToHashSet(StringComparer.Ordinal);
                Assert.Empty(oldKeys.Except(to.Candidates.Select(candidate => candidate.CandidateKey)));
                foreach (var candidate in from.Candidates)
                {
                    Assert.Equal(JsonDefaults.Serialize(candidate),
                        JsonDefaults.Serialize(to.Candidates.Single(item => item.CandidateKey == candidate.CandidateKey)));
                }

                foreach (var profile in from.Profiles)
                {
                    var denser = to.Profiles.Single(item => item.ProfileId == profile.ProfileId);
                    Assert.Equal(profile.CandidateKeys, denser.CandidateKeys.Where(oldKeys.Contains));
                    Assert.Empty(profile.CandidateIds.Except(denser.CandidateIds));
                    var oldExcluded = oldKeys.Except(profile.CandidateKeys).ToArray();
                    var newExcluded = to.Candidates.Select(item => item.CandidateKey).Except(denser.CandidateKeys).ToArray();
                    Assert.Empty(oldExcluded.Except(newExcluded));
                }

                foreach (var conditional in from.Conditionals)
                {
                    var denser = to.Conditionals.Single(item => item.ComparisonId == conditional.ComparisonId);
                    var fromExcluded = from.Profiles.Single(item => item.ProfileId == conditional.BaselineProfileId).CandidateKeys
                        .Except(from.Profiles.Single(item => item.ProfileId == conditional.CombinedProfileId).CandidateKeys).ToArray();
                    var toExcluded = to.Profiles.Single(item => item.ProfileId == conditional.BaselineProfileId).CandidateKeys
                        .Except(to.Profiles.Single(item => item.ProfileId == conditional.CombinedProfileId).CandidateKeys).ToArray();
                    Assert.Empty(fromExcluded.Except(toExcluded));
                    Assert.Equal(fromExcluded.Length, conditional.AdditionalCandidateReduction);
                    Assert.Equal(toExcluded.Length, denser.AdditionalCandidateReduction);
                    Assert.True(denser.AdditionalCandidateReduction >= conditional.AdditionalCandidateReduction);
                    if (conditional.AddedAxis != CommonWorldObservationAxis.Time)
                    {
                        Assert.Equal(conditional.Diagnostic.AddedPredicateIsTopForCandidateDomain, denser.Diagnostic.AddedPredicateIsTopForCandidateDomain);
                        Assert.Equal(conditional.Diagnostic.AddedPredicateImpliedByBaselinePredicates,
                            denser.Diagnostic.AddedPredicateImpliedByBaselinePredicates);
                    }
                    else Assert.False(!conditional.Diagnostic.AddedPredicateIsTopForCandidateDomain && denser.Diagnostic.AddedPredicateIsTopForCandidateDomain);
                    if (conditional.Diagnostic.Classification != CommonWorldConditionalOutcomeClassification.Saturation)
                        Assert.NotEqual(CommonWorldConditionalOutcomeClassification.Saturation, denser.Diagnostic.Classification);
                }

                Assert.Equal(28, from.ProfileEquivalences.Count);
                Assert.Equal(28, to.ProfileEquivalences.Count);
                foreach (var equivalent in to.ProfileEquivalences.Where(item => item.CandidateKeySetsEqual))
                {
                    Assert.True(from.ProfileEquivalences.Single(item => item.LeftProfileId == equivalent.LeftProfileId &&
                        item.RightProfileId == equivalent.RightProfileId).CandidateKeySetsEqual);
                }

                Assert.True(from.Minimum.MinimumCandidateCount <= to.Minimum.MinimumCandidateCount);
                Assert.Empty(to.Minimum.ProfilesAtMinimumCount.Except(from.Minimum.ProfilesAtMinimumCount));
                foreach (var minimal in to.Minimum.InclusionMinimalProfileIds)
                {
                    var axes = to.Profiles.Single(item => item.ProfileId == minimal).ActiveAxes;
                    Assert.Contains(from.Minimum.InclusionMinimalProfileIds, old =>
                        from.Profiles.Single(item => item.ProfileId == old).ActiveAxes.All(axes.Contains));
                }

                Assert.True(pair.MinimumChange.FromMinimumFieldCount <= pair.MinimumChange.ToMinimumFieldCount);
                Assert.True(pair.MinimumChange.FullNecessityPreserved);
                Assert.True(pair.AllInvariantsSatisfied);
            }
        }

        Assert.All(Result.Value.PairAggregates, pair =>
        {
            Assert.Equal(0, pair.BecameIndistinguishableCount);
            Assert.All(pair.Conditionals, conditional =>
            {
                Assert.Equal(0, conditional.LostEffectCount);
                Assert.Equal(25, conditional.ClassificationTransitions.Count);
                Assert.Equal(conditional.ComparisonCount, conditional.ClassificationTransitions.Sum(item => item.ComparisonCount));
            });
        });
    }

    [Fact]
    public void CompatibleTruthSubstitutionLeavesSupportAllPublicArmsAndRawInputsUnchanged()
    {
        var original = Result.Value.Runs.First(run => run.OriginalWorld.CandidateStates.Any(candidate =>
            !candidate.IsTruthCandidate && candidate.StartTick == run.OriginalWorld.EncounterTick &&
            run.OriginalWorld.LocationObservedRegionIds.Contains(candidate.RegionId, StringComparer.Ordinal) &&
            run.OriginalWorld.Participants.Single(person => person.IsTruthParticipant).AllowedRegionIds.Contains(candidate.RegionId, StringComparer.Ordinal)));
        var world = original.OriginalWorld;
        var alternative = world.CandidateStates.First(candidate => !candidate.IsTruthCandidate &&
            candidate.StartTick == world.EncounterTick && world.LocationObservedRegionIds.Contains(candidate.RegionId, StringComparer.Ordinal) &&
            world.Participants.Single(person => person.IsTruthParticipant).AllowedRegionIds.Contains(candidate.RegionId, StringComparer.Ordinal));
        var changedWorld = world with
        {
            TruthWorldId = world.TruthWorldId + "-alternative-candidate",
            TruthCandidateKey = alternative.CandidateKey,
            TruthCandidateStartTick = alternative.StartTick,
            TruthCandidateEndTick = alternative.EndTick,
            TruthRegionId = alternative.RegionId,
            CandidateStates = world.CandidateStates.Select(candidate => candidate with { IsTruthCandidate = candidate.CandidateKey == alternative.CandidateKey }).ToArray()
        };
        var changed = new CandidateDensityRunner().EvaluateWorld(Experiment(), changedWorld);
        Assert.NotEqual(original.FixedTruth.OriginalTruthCandidateKey, changed.FixedTruth.OriginalTruthCandidateKey);
        AssertPublicEqual(original, changed);

        var participantOriginal = Result.Value.Runs.First(run => run.OriginalWorld.Participants.Any(person =>
            !person.IsTruthParticipant && person.AllowedRegionIds.Contains(run.OriginalWorld.TruthRegionId, StringComparer.Ordinal)));
        var participantWorld = participantOriginal.OriginalWorld;
        var alternativePerson = participantWorld.Participants.First(person => !person.IsTruthParticipant &&
            person.AllowedRegionIds.Contains(participantWorld.TruthRegionId, StringComparer.Ordinal));
        var participantChangedWorld = participantWorld with
        {
            TruthWorldId = participantWorld.TruthWorldId + "-alternative-participant",
            TruthParticipantId = alternativePerson.ParticipantId,
            Participants = participantWorld.Participants.Select(person => person with { IsTruthParticipant = person.ParticipantId == alternativePerson.ParticipantId }).ToArray()
        };
        var participantChanged = new CandidateDensityRunner().EvaluateWorld(Experiment(), participantChangedWorld);
        Assert.NotEqual(participantOriginal.FixedTruth.OriginalTruthParticipantId, participantChanged.FixedTruth.OriginalTruthParticipantId);
        AssertPublicEqual(participantOriginal, participantChanged);
        Assert.Equal(JsonDefaults.Serialize(changed),
            JsonDefaults.Serialize(new CandidateDensityRunner().EvaluateWorld(Experiment(), changedWorld)));
    }

    private static void AssertPublicEqual(CandidateDensityWorldRun expected, CandidateDensityWorldRun actual)
    {
        Assert.Equal(JsonDefaults.Serialize(expected.Support), JsonDefaults.Serialize(actual.Support));
        Assert.Equal(JsonDefaults.Serialize(expected.Arms), JsonDefaults.Serialize(actual.Arms));
        Assert.Equal(JsonDefaults.Serialize(expected.CandidateCrosswalk), JsonDefaults.Serialize(actual.CandidateCrosswalk));
        Assert.Equal(expected.RawEvidenceSignature, actual.RawEvidenceSignature);
        Assert.Equal(expected.SourceGraphSignature, actual.SourceGraphSignature);
        Assert.Equal(expected.PublicRequestBeforeSignature, actual.PublicRequestBeforeSignature);
    }

    [Theory]
    [InlineData("0..198")]
    [InlineData("0..200")]
    [InlineData("0..199,0")]
    public async Task CliRejectsNonregisteredSeedListsBeforeCreatingOutput(string seeds)
    {
        var output = Path.Combine(Path.GetTempPath(), $"footprint-m1f19-invalid-cli-{Guid.NewGuid():N}");
        var code = await CliApplication.RunAsync([
            "evaluate-candidate-density", "--experiment", ProtocolPath, "--baseline-experiment", ExperimentPath,
            "--seeds", seeds, "--output", output]);
        Assert.NotEqual(0, code);
        Assert.False(Directory.Exists(output));
    }

    private static CandidateDensityPublicSupportRequest SupportRequest(IReadOnlyList<string> regions) => new()
    {
        Seed = 7,
        BaseTick = 10,
        RegisteredRegionIds = regions,
        BaselinePointCandidates = [Point("old-a", 10, "a"), Point("old-b", 10, "b")]
    };

    private static CandidateIntervalWidthPublicCandidate Point(string key, long tick, string region) => new()
    {
        CandidateKey = key,
        PublicSourceIdentifier = $"public-model-{key}",
        State = new PublicCandidateWarpStateDefinition
        {
            TimeWindow = new InteractionTimeWindow(tick, tick),
            TemporalKind = CandidateWarpTemporalKind.EventPoint,
            RegionIds = [region],
            EvidenceBoundaryState = EvidenceState.Confirmed,
            Provenance = [CandidateWarpProvenance.Create(CandidateWarpSourceKind.PublicCausalModel, $"public-model-{key}",
                0, false, false, EvidenceState.Confirmed, [region])]
        }
    };

    private static CandidateIntervalWidthPublicCandidate WithSource(CandidateIntervalWidthPublicCandidate candidate, string source) =>
        candidate with
        {
            PublicSourceIdentifier = source,
            State = candidate.State with
            {
                Provenance = [CandidateWarpProvenance.Create(CandidateWarpSourceKind.PublicCausalModel, source,
                    0, false, false, EvidenceState.Confirmed, candidate.State.RegionIds)]
            }
        };

    private static CandidateIntervalWidthPublicRequest AnalyticPublicRequest()
    {
        var experiment = Experiment();
        var support = SupportRequest(["a", "b", "c", "d", "e"]);
        return new CandidateIntervalWidthPublicRequest
        {
            ScenarioId = ScenarioId,
            Seed = support.Seed,
            TargetEntityId = "person-a",
            TargetStateContextId = "encounter-state",
            CandidateDomainId = "public-domain",
            EvidenceAvailabilityCutoff = 16,
            RegisteredRegionIds = support.RegisteredRegionIds,
            Candidates = support.BaselinePointCandidates,
            AxisComponents = [
                Axis(CommonWorldObservationAxis.Location, CandidateConstraintPredicate.Region(["a", "b", "e"])),
                Axis(CommonWorldObservationAxis.Time, CandidateConstraintPredicate.Time(new InteractionTimeWindow(10, 11))),
                Axis(CommonWorldObservationAxis.Participant, CandidateConstraintPredicate.Region(["a", "c"]))
            ],
            Profiles = experiment.Profiles,
            ConditionalComparisons = experiment.ConditionalComparisons,
            PrimarySetInclusions = experiment.PrimarySetInclusions
        };
    }

    private static CandidateIntervalWidthAxisComponent Axis(CommonWorldObservationAxis axis,
        CandidateConstraintPredicate predicate) => new()
        {
            Axis = axis,
            Component = CandidateGenerationConstraintComponent.PublicPredicate(
            ScenarioId, 7, "person-a", "encounter-state", "public-domain", predicate,
            $"assertion-{axis}", $"observation-{axis}", $"source-{axis}", [$"source-{axis}"],
            $"rule-{axis}", [$"input-{axis}"], 15, EvidenceState.Confirmed,
            InteractionVisibility.PublicEvidence, PastStateEvidenceUsageRole.CandidateGeneration,
            [new InteractionProvenance(InteractionProvenanceKind.EvidenceRecord, $"input-{axis}")])
        };

    private static CommonWorldFieldCombinationExperimentDefinition Experiment() =>
        CommonWorldFieldCombinationExperimentLoader.Load(ExperimentPath);

    private static CandidateDensityRequest Request() => new()
    {
        Protocol = CandidateDensityProtocolLoader.Load(ProtocolPath),
        ProtocolContentSha256 = HashFile(ProtocolPath),
        BaselineExperiment = Experiment(),
        BaselineExperimentContentSha256 = HashFile(ExperimentPath),
        Seeds = Seeds,
        SourceCommitSha = "m1f19-test",
        DirtyWorkingTree = false
    };

    private static string HashFile(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();
    private static string ExperimentPath => Path.Combine(RepositoryRoot, "experiments", "m1f16_common_world_field_combination", "experiment.json");
    private static string ProtocolPath => Path.Combine(RepositoryRoot, "experiments", "m1f19_nested_point_support_density", "experiment.json");

    private static string RepositoryRoot
    {
        get
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "FootprintSim.sln"))) directory = directory.Parent;
            return directory?.FullName ?? throw new DirectoryNotFoundException("Repository root not found.");
        }
    }
}
