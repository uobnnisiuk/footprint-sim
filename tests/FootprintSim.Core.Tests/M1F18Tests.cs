using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
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

public sealed class M1F18Tests
{
    private static readonly int[] Seeds = Enumerable.Range(0, 200).ToArray();
    private const string ScenarioId = "m1f18-analytic-public-domain";
    private static readonly Lazy<CandidateIntervalWidthResult> Result = new(() =>
        new CandidateIntervalWidthRunner().Run(Request()));

    [Fact]
    public void PointificationPreservesPublicProvenanceAndMultiplicityAndIsIdempotent()
    {
        var original = AnalyticCandidates();
        var snapshot = JsonDefaults.Serialize(original);
        var transform = new CandidateIntervalWidthTransform();
        var points = transform.Apply(original, "point-at-start");

        Assert.Equal(original.Count, points.Count);
        Assert.Equal(snapshot, JsonDefaults.Serialize(original));
        Assert.Equal(JsonDefaults.Serialize(points),
            JsonDefaults.Serialize(transform.Apply(points, "point-at-start")));
        Assert.Equal(JsonDefaults.Serialize(points),
            JsonDefaults.Serialize(transform.Apply(original.Reverse().ToArray(), "point-at-start")));
        Assert.Equal(JsonDefaults.Serialize(transform.Apply(original, "original")),
            JsonDefaults.Serialize(transform.Apply(original.Reverse().ToArray(), "original")));
        Assert.All(points, point =>
        {
            var source = original.Single(candidate => candidate.CandidateKey == point.CandidateKey);
            Assert.Equal(source.PublicSourceIdentifier, point.PublicSourceIdentifier);
            Assert.Equal(source.State.TimeWindow.StartTick, point.State.TimeWindow.StartTick);
            Assert.Equal(point.State.TimeWindow.StartTick, point.State.TimeWindow.EndTick);
            Assert.Equal(CandidateWarpTemporalKind.EventPoint, point.State.TemporalKind);
            var metadataRestored = point.State with
            {
                TimeWindow = source.State.TimeWindow,
                TemporalKind = source.State.TemporalKind
            };
            Assert.Equal(JsonDefaults.Serialize(source.State), JsonDefaults.Serialize(metadataRestored));
        });
    }

    [Fact]
    public void ClosedLowerBoundaryCanAdmitAnIntervalWhoseStartIsExcluded()
    {
        var originals = AnalyticCandidates();
        var points = new CandidateIntervalWidthTransform().Apply(originals, "point-at-start");
        var originalResult = ComposeTime(originals.Select(candidate => candidate.State).ToArray());
        var pointResult = ComposeTime(points.Select(candidate => candidate.State).ToArray());

        // T=[10,11]: [8,10] touches the closed lower boundary; [9,12] crosses it.
        Assert.Equal([8L, 9L, 10L, 11L], originalResult.Candidates
            .Select(candidate => candidate.TimeWindow.StartTick).Order());
        Assert.Equal([10L, 11L], pointResult.Candidates
            .Select(candidate => candidate.TimeWindow.StartTick).Order());
        Assert.Equal(originalResult.CompositionId, pointResult.CompositionId);
        Assert.Equal(originalResult.CandidateGenerationWork, pointResult.CandidateGenerationWork);
        Assert.Equal(originalResult.Audit.CandidatePredicateChecks, pointResult.Audit.CandidatePredicateChecks);
    }

    [Fact]
    public void NativeIdentityChangesOnlyForCandidatesWhoseRepresentationChanges()
    {
        var originals = AnalyticCandidates();
        var points = new CandidateIntervalWidthTransform().Apply(originals, "point-at-start");
        var reconstructor = new ObservationDerivedHistoricalCandidateReconstructor();
        var first = reconstructor.Reconstruct(ScenarioId, 0, "person-a",
            originals.Select(candidate => candidate.State), constraint: null);
        var second = reconstructor.Reconstruct(ScenarioId, 0, "person-a",
            points.Select(candidate => candidate.State), constraint: null);

        Assert.Equal(originals.Count, first.Candidates.Count);
        Assert.Equal(originals.Count, second.Candidates.Count);
        foreach (var source in originals)
        {
            var before = first.Candidates.Single(candidate =>
                candidate.TimeWindow.StartTick == source.State.TimeWindow.StartTick);
            var after = second.Candidates.Single(candidate =>
                candidate.TimeWindow.StartTick == source.State.TimeWindow.StartTick);
            if (source.State.TimeWindow.StartTick == source.State.TimeWindow.EndTick)
            {
                Assert.Equal(before.CandidateId, after.CandidateId);
                Assert.Equal(before.StateId, after.StateId);
            }
            else
            {
                Assert.NotEqual(before.CandidateId, after.CandidateId);
                Assert.NotEqual(before.StateId, after.StateId);
            }
        }
    }

    [Theory]
    [InlineData("duplicate-key")]
    [InlineData("duplicate-source")]
    [InlineData("duplicate-coordinate")]
    [InlineData("multiple-regions")]
    public void TransformRejectsAmbiguousCandidateCorrespondence(string defect)
    {
        var candidates = AnalyticCandidates().ToArray();
        candidates[1] = defect switch
        {
            "duplicate-key" => candidates[1] with { CandidateKey = candidates[0].CandidateKey },
            "duplicate-source" => candidates[1] with { PublicSourceIdentifier = candidates[0].PublicSourceIdentifier },
            "duplicate-coordinate" => candidates[1] with
            {
                State = candidates[1].State with { TimeWindow = candidates[0].State.TimeWindow }
            },
            "multiple-regions" => candidates[1] with
            {
                State = candidates[1].State with { RegionIds = ["region-a", "region-b"] }
            },
            _ => throw new ArgumentOutOfRangeException(nameof(defect))
        };

        Assert.Throws<InvalidDataException>(() =>
            new CandidateIntervalWidthTransform().Apply(candidates, "point-at-start"));
    }

    [Fact]
    public void TransformAndPublicInferenceInputsCannotReceiveEvaluationWorldOrTruth()
    {
        var transformMethod = typeof(CandidateIntervalWidthTransform).GetMethod("Apply")!;
        Assert.Equal([typeof(IReadOnlyList<CandidateIntervalWidthPublicCandidate>), typeof(string)],
            transformMethod.GetParameters().Select(parameter => parameter.ParameterType));
        foreach (var contract in new[]
                 {
                     typeof(CandidateIntervalWidthPublicCandidate), typeof(CandidateIntervalWidthPublicRequest)
                 })
        {
            Assert.DoesNotContain(contract.GetProperties(), property =>
                property.Name.Contains("Truth", StringComparison.OrdinalIgnoreCase) ||
                property.PropertyType == typeof(GeneratedCommonFieldCombinationWorld) ||
                property.PropertyType == typeof(CommonWorldFieldCombinationWorldRun));
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void TransformRejectsTemporalKindInconsistentWithIntervalWidth(bool zeroWidthInterval)
    {
        var candidates = AnalyticCandidates().ToArray();
        var state = candidates[0].State;
        candidates[0] = candidates[0] with
        {
            State = state with
            {
                TimeWindow = zeroWidthInterval
                    ? new InteractionTimeWindow(state.TimeWindow.StartTick, state.TimeWindow.StartTick)
                    : new InteractionTimeWindow(state.TimeWindow.StartTick, state.TimeWindow.StartTick + 1),
                TemporalKind = zeroWidthInterval ? CandidateWarpTemporalKind.StateInterval : CandidateWarpTemporalKind.EventPoint
            }
        };
        Assert.Throws<InvalidDataException>(() => new CandidateIntervalWidthTransform().Apply(candidates, "original"));
        Assert.Throws<InvalidDataException>(() => new CandidateIntervalWidthTransform().Apply(candidates, "point-at-start"));
    }

    [Fact]
    public void AnalyticMinimumFamilyCanChangeWhileMinimumCountAndEvidenceRemainFixed()
    {
        var request = AnalyticPublicRequest();
        var evaluator = new CandidateIntervalWidthPublicEvaluator();
        var original = evaluator.Evaluate(request, "original");
        var point = evaluator.Evaluate(request, "point-at-start");

        Assert.Equal(1, original.Minimum.MinimumCandidateCount);
        Assert.Equal(1, point.Minimum.MinimumCandidateCount);
        Assert.Equal(["location-participant"], original.Minimum.InclusionMinimalProfileIds);
        Assert.Equal(new[] { "location-participant", "location-time" },
            point.Minimum.InclusionMinimalProfileIds.Order(StringComparer.Ordinal));
        Assert.True(original.Minimum.AllMinimumProfilesReturnSameKeySet);
        Assert.True(point.Minimum.AllMinimumProfilesReturnSameKeySet);
        Assert.Equal(original.FixedConstraintSignature, point.FixedConstraintSignature);
        var implication = original.Conditionals.Single(conditional =>
            conditional.ComparisonId == "participant-added-to-location").Diagnostic;
        Assert.Equal("registered", implication.DomainId);
        Assert.Contains("unused-e", implication.ImplicationCounterexampleRegionIds);
        Assert.False(implication.AddedPredicateImpliedByBaselinePredicates);
        Assert.Equal(CommonWorldConditionalOutcomeClassification.AdditionalCandidateReduction,
            original.Conditionals.Single(conditional =>
                conditional.ComparisonId == "participant-added-to-location-time").Diagnostic.Classification);
        Assert.Equal(CommonWorldConditionalOutcomeClassification.Saturation,
            point.Conditionals.Single(conditional =>
                conditional.ComparisonId == "participant-added-to-location-time").Diagnostic.Classification);
    }

    [Fact]
    public void PublicInputPermutationDoesNotChangeEitherArm()
    {
        var request = AnalyticPublicRequest();
        var reordered = request with
        {
            Candidates = request.Candidates.Reverse().ToArray(),
            AxisComponents = request.AxisComponents.Reverse().ToArray(),
            Profiles = request.Profiles.Reverse().ToArray(),
            RegisteredRegionIds = request.RegisteredRegionIds.Reverse().ToArray()
        };
        var evaluator = new CandidateIntervalWidthPublicEvaluator();
        foreach (var arm in new[] { "original", "point-at-start" })
        {
            Assert.Equal(JsonDefaults.Serialize(evaluator.Evaluate(request, arm)),
                JsonDefaults.Serialize(evaluator.Evaluate(reordered, arm)));
        }
    }

    [Fact]
    public void PublicEvaluatorRejectsFutureCandidatesAndVerificationComponents()
    {
        var request = AnalyticPublicRequest();
        var candidates = request.Candidates.ToArray();
        var candidate = candidates[0];
        candidates[0] = candidate with
        {
            State = candidate.State with
            {
                Provenance =
                [
                    CandidateWarpProvenance.Create(CandidateWarpSourceKind.PublicCausalModel,
                        candidate.PublicSourceIdentifier, availableAtTick: 17,
                        isPersonEvidence: false, isDeviceOnlyEvidence: false,
                        EvidenceState.Confirmed, candidate.State.RegionIds)
                ]
            }
        };
        var future = request with { Candidates = candidates };
        var verification = request with
        {
            AxisComponents = request.AxisComponents.Select(component =>
                component.Axis == CommonWorldObservationAxis.Time
                    ? Axis(CommonWorldObservationAxis.Time,
                        CandidateConstraintPredicate.Time(new InteractionTimeWindow(10, 11)),
                        PastStateEvidenceUsageRole.CandidateVerification)
                    : component).ToArray()
        };

        Assert.Throws<InvalidDataException>(() =>
            new CandidateIntervalWidthPublicEvaluator().Evaluate(future, "point-at-start"));
        Assert.Throws<InvalidDataException>(() =>
            new CandidateIntervalWidthPublicEvaluator().Evaluate(verification, "point-at-start"));
    }

    [Theory]
    [InlineData("missing-seed")]
    [InlineData("extra-seed")]
    [InlineData("duplicate-seed")]
    [InlineData("baseline-hash")]
    [InlineData("baseline-object")]
    [InlineData("renamed-generator")]
    [InlineData("changed-arm")]
    [InlineData("changed-truth-rule")]
    public void RunnerRejectsProtocolOrBaselineDriftBeforeProducingResults(string defect)
    {
        var request = Request();
        request = defect switch
        {
            "missing-seed" => request with { Seeds = Seeds.Skip(1).ToArray() },
            "extra-seed" => request with { Seeds = Seeds.Append(200).ToArray() },
            "duplicate-seed" => request with { Seeds = Seeds.SkipLast(1).Append(0).ToArray() },
            "baseline-hash" => request with { BaselineExperimentContentSha256 = new string('0', 64) },
            "baseline-object" => request with
            {
                BaselineExperiment = request.BaselineExperiment with
                {
                    Generator = request.BaselineExperiment.Generator with { CandidateIntervalWidthMaximum = 3 }
                }
            },
            "renamed-generator" => request with
            {
                BaselineExperiment = request.BaselineExperiment with
                {
                    Generator = request.BaselineExperiment.Generator with { Id = "renamed-generator" }
                }
            },
            "changed-arm" => request with { Protocol = request.Protocol with { Arms = ["original", "point-at-end"] } },
            "changed-truth-rule" => request with
            {
                Protocol = request.Protocol with { TruthRule = "truth-after-pointification" }
            },
            _ => throw new ArgumentOutOfRangeException(nameof(defect))
        };

        Assert.Throws<InvalidDataException>(() => new CandidateIntervalWidthRunner().Run(request));
    }

    [Fact]
    public void RegisteredWorldsRetainOriginalTruthAndBijectivePublicCandidateIdentity()
    {
        var generator = new CommonWorldFieldCombinationSeededWorldGenerator();
        var experiment = Experiment();
        Assert.Equal(Seeds, Result.Value.Runs.Select(run => run.Seed));
        Assert.All(Result.Value.Runs, run =>
        {
            Assert.Equal(JsonDefaults.Serialize(generator.Generate(experiment, run.Seed)),
                JsonDefaults.Serialize(run.OriginalWorld));
            Assert.Equal(run.OriginalWorldBeforeSignature, run.OriginalWorldAfterSignature);
            Assert.Equal(run.OriginalWorld.CandidateCount, run.Crosswalk.Count);
            Assert.Equal(run.Crosswalk.Count, run.Crosswalk.Select(row => row.CandidateKey).Distinct().Count());
            Assert.Equal(run.Crosswalk.Count, run.Crosswalk.Select(row => row.Original.CandidateId).Distinct().Count());
            Assert.Equal(run.Crosswalk.Count, run.Crosswalk.Select(row => row.Point.CandidateId).Distinct().Count());
            Assert.Equal(run.OriginalWorld.TruthWorldId, run.FixedTruth.OriginalTruthWorldId);
            Assert.Equal(run.OriginalWorld.TruthCandidateKey, run.FixedTruth.OriginalTruthCandidateKey);
            Assert.Equal(run.OriginalWorld.TruthCandidateEndTick, run.FixedTruth.OriginalTruthEndTick);
            Assert.Equal(run.OriginalWorld.EncounterTick, run.FixedTruth.OriginalEncounterTick);
            Assert.Equal(run.OriginalWorld.TruthParticipantId, run.FixedTruth.OriginalTruthParticipantId);
            Assert.True(run.FixedTruth.ResolvedAfterBothPublicArms);
            Assert.True(run.ReferenceMatches);
            Assert.Equal(run.Original.FixedConstraintSignature, run.Point.FixedConstraintSignature);
            foreach (var row in run.Crosswalk)
            {
                Assert.Equal(row.Original.CandidateKey, row.Point.CandidateKey);
                Assert.Equal(row.Original.PublicSourceIdentifier, row.Point.PublicSourceIdentifier);
                Assert.Equal(row.Original.RegionId, row.Point.RegionId);
                Assert.Equal(row.Original.ProvenanceSignature, row.Point.ProvenanceSignature);
                Assert.Equal(row.Original.TimeWindow.StartTick, row.Point.TimeWindow.StartTick);
                Assert.Equal(row.Point.TimeWindow.StartTick, row.Point.TimeWindow.EndTick);
                Assert.Equal(row.Original.TimeWindow.StartTick == row.Original.TimeWindow.EndTick,
                    row.Original.CandidateId == row.Point.CandidateId);
            }
        });
    }

    [Fact]
    public void PairedKeyLossesMatchTheLowerBoundaryAndLeaveNonTimeProfilesUnchanged()
    {
        Assert.All(Result.Value.Runs, run =>
        {
            var lower = run.OriginalWorld.LaterEventTick - run.OriginalWorld.DurationMaximumTicks;
            var crosswalk = run.Crosswalk.ToDictionary(row => row.CandidateKey, StringComparer.Ordinal);
            foreach (var change in run.ProfileChanges)
            {
                var original = run.Original.Profiles.Single(profile => profile.ProfileId == change.ProfileId);
                var point = run.Point.Profiles.Single(profile => profile.ProfileId == change.ProfileId);
                var usesTime = original.ActiveAxes.Contains(CommonWorldObservationAxis.Time);
                var expectedLost = usesTime
                    ? original.CandidateKeys.Where(key => crosswalk[key].Original.TimeWindow.StartTick < lower)
                        .Order(StringComparer.Ordinal).ToArray()
                    : [];
                Assert.Equal(expectedLost, change.LostKeys);
                Assert.Empty(change.NewKeys);
                Assert.Equal(original.CandidateKeys.Except(point.CandidateKeys).Order(StringComparer.Ordinal),
                    change.LostKeys);
                Assert.All(point.CandidateKeys, key => Assert.Contains(key, original.CandidateKeys));
                if (!usesTime) Assert.Equal(original.CandidateKeys, point.CandidateKeys);
                Assert.Equal(original.CandidateGenerationWork, point.CandidateGenerationWork);
                Assert.Equal(original.CompositionId, point.CompositionId);
                Assert.Contains(run.FixedTruth.OriginalTruthCandidateKey, original.CandidateKeys);
                Assert.Contains(run.FixedTruth.OriginalTruthCandidateKey, point.CandidateKeys);
                Assert.All(change.LostWitnesses, witness =>
                {
                    Assert.True(witness.OriginalStartTick < lower && lower <= witness.OriginalEndTick);
                    Assert.Equal(lower, witness.DerivedTimeLowerBound);
                    Assert.Equal(witness.OriginalEndTick == lower, witness.EndTouchesLowerBound);
                });
                Assert.Equal(change.LostKeys,
                    change.LostWitnesses.Select(witness => witness.CandidateKey).Order(StringComparer.Ordinal));
            }
        });
        Assert.Equal(3200, Result.Value.Summary.PrimaryProfileEvaluationCount);
        Assert.Equal(800, Result.Value.Summary.NonTimeEqualityCheckCount);
        Assert.Equal(800, Result.Value.Summary.TimeSubsetCheckCount);
        Assert.Equal(4800, Result.Value.Summary.JointSetInclusionCheckCount);
        Assert.Equal(3200, Result.Value.Summary.TruthContainmentCount);
        Assert.Equal(0, Result.Value.Summary.FalseExclusionCount);
        Assert.Equal(0, Result.Value.Summary.InvariantViolationCount);
    }

    [Fact]
    public void ConditionalSignsAndRegisteredDomainFlagsFollowTheFrozenSingleFactorDesign()
    {
        var experiment = Experiment();
        Assert.All(Result.Value.Runs, run =>
        {
            foreach (var change in run.ConditionalChanges)
            {
                var definition = experiment.ConditionalComparisons.Single(item => item.Id == change.ComparisonId);
                var baseline = run.Original.Profiles.Single(profile => profile.ProfileId == definition.BaselineProfileId);
                var delta = change.PointAdditionalReduction - change.OriginalAdditionalReduction;
                Assert.Equal(delta, change.PointMinusOriginalReduction);
                if (definition.AddedAxis == CommonWorldObservationAxis.Time)
                {
                    Assert.True(delta >= 0);
                    Assert.False(!change.OriginalDiagnostic.AddedPredicateIsTopForCandidateDomain &&
                        change.PointDiagnostic.AddedPredicateIsTopForCandidateDomain);
                }
                else
                {
                    Assert.Equal(change.OriginalDiagnostic.AddedPredicateIsTopForCandidateDomain,
                        change.PointDiagnostic.AddedPredicateIsTopForCandidateDomain);
                    Assert.Equal(change.OriginalDiagnostic.AddedPredicateImpliedByBaselinePredicates,
                        change.PointDiagnostic.AddedPredicateImpliedByBaselinePredicates);
                    if (baseline.ActiveAxes.Contains(CommonWorldObservationAxis.Time)) Assert.True(delta <= 0);
                    else Assert.Equal(0, delta);
                }

                Assert.Equal("registered", change.OriginalDiagnostic.DomainId);
                Assert.Equal("registered", change.PointDiagnostic.DomainId);
            }
        });
        Assert.All(Result.Value.ConditionalAggregates, aggregate =>
        {
            Assert.Equal(25, aggregate.ClassificationTransitions.Count);
            Assert.Equal(aggregate.ComparisonCount,
                aggregate.ClassificationTransitions.Sum(transition => transition.ComparisonCount));
        });
    }

    [Fact]
    public void CompatibleCandidateAndParticipantTruthSwapsLeaveBothPublicArmsUnchanged()
    {
        var original = Result.Value.Runs.First(run => run.OriginalWorld.CandidateStates.Any(candidate =>
            !candidate.IsTruthCandidate && candidate.StartTick == run.OriginalWorld.EncounterTick &&
            run.OriginalWorld.LocationObservedRegionIds.Contains(candidate.RegionId, StringComparer.Ordinal) &&
            run.OriginalWorld.Participants.Single(person => person.IsTruthParticipant).AllowedRegionIds
                .Contains(candidate.RegionId, StringComparer.Ordinal)));
        var world = original.OriginalWorld;
        var alternative = world.CandidateStates.First(candidate =>
            !candidate.IsTruthCandidate && candidate.StartTick == world.EncounterTick &&
            world.LocationObservedRegionIds.Contains(candidate.RegionId, StringComparer.Ordinal) &&
            world.Participants.Single(person => person.IsTruthParticipant).AllowedRegionIds
                .Contains(candidate.RegionId, StringComparer.Ordinal));
        var changedWorld = world with
        {
            TruthWorldId = world.TruthWorldId + "-alternative-candidate",
            TruthCandidateKey = alternative.CandidateKey,
            TruthCandidateStartTick = alternative.StartTick,
            TruthCandidateEndTick = alternative.EndTick,
            TruthRegionId = alternative.RegionId,
            CandidateStates = world.CandidateStates.Select(candidate => candidate with
            {
                IsTruthCandidate = candidate.CandidateKey == alternative.CandidateKey
            }).ToArray()
        };
        var changed = new CandidateIntervalWidthRunner().EvaluateWorld(Experiment(), changedWorld);
        Assert.NotEqual(original.FixedTruth.OriginalTruthCandidateKey, changed.FixedTruth.OriginalTruthCandidateKey);
        AssertPublicArmsEqual(original, changed);

        var participantOriginal = Result.Value.Runs.First(run => run.OriginalWorld.Participants.Any(person =>
            !person.IsTruthParticipant && person.AllowedRegionIds.Contains(run.OriginalWorld.TruthRegionId,
                StringComparer.Ordinal)));
        var participantWorld = participantOriginal.OriginalWorld;
        var alternativePerson = participantWorld.Participants.First(person =>
            !person.IsTruthParticipant && person.AllowedRegionIds.Contains(participantWorld.TruthRegionId,
                StringComparer.Ordinal));
        var participantChangedWorld = participantWorld with
        {
            TruthWorldId = participantWorld.TruthWorldId + "-alternative-person",
            TruthParticipantId = alternativePerson.ParticipantId,
            Participants = participantWorld.Participants.Select(person => person with
            {
                IsTruthParticipant = person.ParticipantId == alternativePerson.ParticipantId
            }).ToArray()
        };
        var participantChanged = new CandidateIntervalWidthRunner().EvaluateWorld(Experiment(), participantChangedWorld);
        Assert.NotEqual(participantOriginal.FixedTruth.OriginalTruthParticipantId,
            participantChanged.FixedTruth.OriginalTruthParticipantId);
        AssertPublicArmsEqual(participantOriginal, participantChanged);
    }

    [Fact]
    public void RepeatedCompleteEvaluationIsDeterministicWhenSeedOrderChanges()
    {
        var reordered = new CandidateIntervalWidthRunner().Run(Request() with { Seeds = Seeds.Reverse().ToArray() });

        Assert.Equal(JsonDefaults.Serialize(Result.Value), JsonDefaults.Serialize(reordered));
        Assert.All(reordered.ProfileAggregates, aggregate =>
        {
            Assert.Equal(200, aggregate.WorldCount);
            Assert.Equal(200, aggregate.ChangedWorldCount + aggregate.ZeroEffectWorldCount);
            Assert.Equal(200, aggregate.PointMinusOriginal.Distribution.Sum(value => value.Count));
        });
    }

    private static void AssertPublicArmsEqual(CandidateIntervalWidthWorldRun original,
        CandidateIntervalWidthWorldRun changed)
    {
        Assert.Equal(JsonDefaults.Serialize(original.Original), JsonDefaults.Serialize(changed.Original));
        Assert.Equal(JsonDefaults.Serialize(original.Point), JsonDefaults.Serialize(changed.Point));
        Assert.Equal(JsonDefaults.Serialize(original.Crosswalk), JsonDefaults.Serialize(changed.Crosswalk));
        Assert.Equal(original.RawEvidenceSignature, changed.RawEvidenceSignature);
        Assert.Equal(original.SourceGraphSignature, changed.SourceGraphSignature);
    }

    [Theory]
    [InlineData("0..198")]
    [InlineData("0..200")]
    [InlineData("0..199,0")]
    public async Task CliRejectsMissingExtraAndDuplicateSeedsBeforeCreatingOutput(string seeds)
    {
        var output = Path.Combine(Path.GetTempPath(), $"footprint-m1f18-invalid-cli-{Guid.NewGuid():N}");
        var code = await CliApplication.RunAsync(
        [
            "evaluate-candidate-interval-width", "--experiment", ProtocolPath,
            "--baseline-experiment", ExperimentPath, "--seeds", seeds, "--output", output
        ]);

        Assert.NotEqual(0, code);
        Assert.False(Directory.Exists(output));
    }

    private static CandidateGenerationConstraintCompositionResult ComposeTime(
        IReadOnlyList<PublicCandidateWarpStateDefinition> candidates)
    {
        var component = CandidateGenerationConstraintComponent.PublicPredicate(
            ScenarioId, 0, "person-a", "encounter-state", "public-domain",
            CandidateConstraintPredicate.Time(new InteractionTimeWindow(10, 11)),
            "time-assertion", "time-observation", "time-source", ["time-source"],
            "analytic-time-rule", ["time-input"], 15, EvidenceState.Confirmed,
            InteractionVisibility.PublicEvidence, PastStateEvidenceUsageRole.CandidateGeneration,
            [new InteractionProvenance(InteractionProvenanceKind.EvidenceRecord, "time-input")]);
        return new CandidateGenerationConstraintComposer().Compose(
            new CandidateGenerationConstraintCompositionRequest
            {
                ScenarioId = ScenarioId,
                Seed = 0,
                TargetEntityId = "person-a",
                TargetStateContextId = "encounter-state",
                CandidateDomainId = "public-domain",
                EvidenceAvailabilityCutoff = 16,
                UsageRole = PastStateEvidenceUsageRole.CandidateGeneration,
                PublicCandidateStates = candidates,
                ConstraintGroups =
                [
                    CandidateGenerationConstraintGroup.Create(
                        CandidateConstraintLogicalRole.JointRequirement, [component])
                ]
            });
    }

    private static PublicCandidateWarpStateDefinition State(string key, long start, long end,
        string region = "region-a") => new()
        {
            TimeWindow = new InteractionTimeWindow(start, end),
            TemporalKind = start == end ? CandidateWarpTemporalKind.EventPoint : CandidateWarpTemporalKind.StateInterval,
            RegionIds = [region],
            EvidenceBoundaryState = EvidenceState.Confirmed,
            Provenance =
        [
            CandidateWarpProvenance.Create(
                CandidateWarpSourceKind.PublicCausalModel, $"public-candidate-{key}",
                availableAtTick: 0, isPersonEvidence: false, isDeviceOnlyEvidence: false,
                EvidenceState.Confirmed, [region])
        ]
        };

    private static IReadOnlyList<CandidateIntervalWidthPublicCandidate> AnalyticCandidates() =>
        new[]
        {
            (Key: "before", Start: 7L, End: 9L),
            (Key: "endpoint", Start: 8L, End: 10L),
            (Key: "crossing", Start: 9L, End: 12L),
            (Key: "lower", Start: 10L, End: 10L),
            (Key: "upper", Start: 11L, End: 13L),
            (Key: "after", Start: 12L, End: 14L)
        }.Select(candidate => new CandidateIntervalWidthPublicCandidate
        {
            CandidateKey = candidate.Key,
            PublicSourceIdentifier = $"public-candidate-{candidate.Key}",
            State = State(candidate.Key, candidate.Start, candidate.End)
        }).ToArray();

    private static CommonWorldFieldCombinationExperimentDefinition Experiment() =>
        CommonWorldFieldCombinationExperimentLoader.Load(ExperimentPath);

    private static CandidateIntervalWidthRequest Request() => new()
    {
        Protocol = CandidateIntervalWidthProtocolLoader.Load(ProtocolPath),
        ProtocolContentSha256 = HashFile(ProtocolPath),
        BaselineExperiment = Experiment(),
        BaselineExperimentContentSha256 = HashFile(ExperimentPath),
        Seeds = Seeds,
        SourceCommitSha = "m1f18-test",
        DirtyWorkingTree = false
    };

    private static CandidateIntervalWidthPublicRequest AnalyticPublicRequest()
    {
        var experiment = Experiment();
        return new CandidateIntervalWidthPublicRequest
        {
            ScenarioId = ScenarioId,
            Seed = 0,
            TargetEntityId = "person-a",
            TargetStateContextId = "encounter-state",
            CandidateDomainId = "public-domain",
            EvidenceAvailabilityCutoff = 16,
            RegisteredRegionIds = ["a", "b", "c", "d", "unused-e"],
            Candidates = new[]
            {
                (Key: "a", Start: 9L, End: 10L),
                (Key: "b", Start: 10L, End: 10L),
                (Key: "c", Start: 11L, End: 11L),
                (Key: "d", Start: 12L, End: 12L)
            }.Select(candidate => new CandidateIntervalWidthPublicCandidate
            {
                CandidateKey = candidate.Key,
                PublicSourceIdentifier = $"public-candidate-{candidate.Key}",
                State = State(candidate.Key, candidate.Start, candidate.End, candidate.Key)
            }).ToArray(),
            AxisComponents =
            [
                Axis(CommonWorldObservationAxis.Location, CandidateConstraintPredicate.Region(["a", "b", "unused-e"])),
                Axis(CommonWorldObservationAxis.Time, CandidateConstraintPredicate.Time(new InteractionTimeWindow(10, 11))),
                Axis(CommonWorldObservationAxis.Participant, CandidateConstraintPredicate.Region(["b", "c"]))
            ],
            Profiles = experiment.Profiles,
            ConditionalComparisons = experiment.ConditionalComparisons,
            PrimarySetInclusions = experiment.PrimarySetInclusions
        };
    }

    private static CandidateIntervalWidthAxisComponent Axis(CommonWorldObservationAxis axis,
        CandidateConstraintPredicate predicate,
        PastStateEvidenceUsageRole usage = PastStateEvidenceUsageRole.CandidateGeneration) => new()
        {
            Axis = axis,
            Component = CandidateGenerationConstraintComponent.PublicPredicate(
            ScenarioId, 0, "person-a", "encounter-state", "public-domain", predicate,
            $"assertion-{axis}", $"observation-{axis}", $"source-{axis}", [$"source-{axis}"],
            $"rule-{axis}", [$"input-{axis}"], 15, EvidenceState.Confirmed,
            InteractionVisibility.PublicEvidence, usage,
            [new InteractionProvenance(InteractionProvenanceKind.EvidenceRecord, $"input-{axis}")])
        };

    private static string HashFile(string path) =>
        Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();

    private static string ExperimentPath => Path.Combine(RepositoryRoot,
        "experiments", "m1f16_common_world_field_combination", "experiment.json");

    private static string ProtocolPath => Path.Combine(RepositoryRoot,
        "experiments", "m1f18_candidate_interval_width", "experiment.json");

    private static string RepositoryRoot
    {
        get
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "FootprintSim.sln")))
            {
                directory = directory.Parent;
            }

            return directory?.FullName ?? throw new DirectoryNotFoundException("Repository root not found.");
        }
    }
}
