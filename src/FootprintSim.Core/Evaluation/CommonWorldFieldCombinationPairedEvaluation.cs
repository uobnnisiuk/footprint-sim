using FootprintSim.Evidence;
using FootprintSim.Inference.Derivation;
using FootprintSim.Inference.PastStates;
using FootprintSim.Interactions;
using FootprintSim.Interactions.CandidateWarps;

namespace FootprintSim.Evaluation;

public sealed class CommonWorldFieldCombinationWorldEvaluator
{
    public CommonWorldFieldCombinationWorldRun Evaluate(
        CommonWorldFieldCombinationExperimentDefinition experiment,
        GeneratedCommonFieldCombinationWorld world,
        string sourceCommitSha,
        bool dirtyWorkingTree)
    {
        ArgumentNullException.ThrowIfNull(experiment);
        ArgumentNullException.ThrowIfNull(world);
        ValidateWorld(experiment, world);
        var fixture = ToFixture(experiment, world);
        var evaluated = new CommonWorldObservationFixtureEvaluationRunner().Run(
            new CommonWorldObservationFixtureEvaluationRequest
            {
                Fixture = fixture,
                FixtureContentSha256 = world.PublicWorldId,
                SourceCommitSha = sourceCommitSha,
                DirtyWorkingTree = dirtyWorkingTree
            });
        if (evaluated.ExistingS5Changed ||
            evaluated.Manifest.LocationRuleId != ObservedRegionLocationConstraintDeriver.RuleId ||
            evaluated.Manifest.TimeRuleId != BoundedDurationObservationConstraintDeriver.RuleId ||
            evaluated.Manifest.ParticipantRuleId != EncounterParticipantLocationConstraintDeriver.RuleId)
        {
            throw new InvalidOperationException(
                $"Seed {world.Seed} changed a fixed F15 derivation boundary.");
        }

        var setInclusions = experiment.PrimarySetInclusions.Select(definition =>
        {
            var subset = Profile(evaluated.Runs, definition.SubsetProfileId);
            var superset = Profile(evaluated.Runs, definition.SupersetProfileId);
            return new CommonWorldSetInclusionAudit
            {
                Seed = world.Seed,
                SubsetProfileId = subset.ProfileId,
                SupersetProfileId = superset.ProfileId,
                SubsetCandidateIds = subset.CandidateIds,
                SupersetCandidateIds = superset.CandidateIds,
                CandidateSetIsSubset = IsSubset(subset.CandidateIds, superset.CandidateIds)
            };
        }).ToArray();
        var conditional = experiment.ConditionalComparisons.Select(definition =>
            Conditional(experiment, world, evaluated, definition)).ToArray();
        var equivalences = AllProfilePairs(evaluated.Runs).Select(pair =>
            new CommonWorldProfileEquivalenceAudit
            {
                Seed = world.Seed,
                LeftProfileId = pair.Left.ProfileId,
                RightProfileId = pair.Right.ProfileId,
                CandidateSetsEqual = SameCandidates(pair.Left, pair.Right)
            }).ToArray();
        var minimum = MinimumAudit(world.Seed, evaluated.Runs);
        var singles = new[]
        {
            Profile(evaluated.Runs, "location"),
            Profile(evaluated.Runs, "time"),
            Profile(evaluated.Runs, "participant")
        };
        var firstSix = conditional.Take(6).ToArray();
        var lastThree = conditional.Skip(6).ToArray();
        var flags = new CommonWorldOutcomeFlags
        {
            AllThreeSingleAxisCandidateSetsDiffer =
                !SameCandidates(singles[0], singles[1]) &&
                !SameCandidates(singles[0], singles[2]) &&
                !SameCandidates(singles[1], singles[2]),
            AFieldSingleAxisSetIsSubsetOfBothOthers = singles.Any(candidate =>
                singles.Where(other => !ReferenceEquals(candidate, other)).All(other =>
                    IsSubset(candidate.CandidateIds, other.CandidateIds)) &&
                singles.Any(other => !ReferenceEquals(candidate, other) &&
                    !SameCandidates(candidate, other))),
            AnyTwoAxisPlateau = firstSix.Any(item => item.CandidateSetsEqual),
            AnyThirdAxisAdditionalReduction = lastThree.Any(item =>
                item.AdditionalCandidateReduction > 0),
            FullProfileSaturatedAtOneCandidate =
                Profile(evaluated.Runs, "location-time-participant").CandidateCount == 1
        };

        var run = new CommonWorldFieldCombinationWorldRun
        {
            Seed = world.Seed,
            World = world,
            CandidateUniverseId = evaluated.CandidateUniverseId,
            TruthCandidateId = evaluated.TruthCandidateId,
            SourceGraph = evaluated.SourceGraph,
            DerivationAudit = evaluated.DerivationAudit,
            DerivedLocationAllowedRegionIds = evaluated.LocationConstraint.AllowedRegionIds,
            DerivedHistoricalTimeWindow = evaluated.TimeConstraint.ConstrainedTimeWindow,
            DerivedParticipantAllowedRegionIds = evaluated.ParticipantConstraint.AllowedRegionIds,
            Profiles = evaluated.Runs,
            SetInclusionAudits = setInclusions,
            ConditionalReductions = conditional,
            ProfileEquivalences = equivalences,
            MinimumAudit = minimum,
            OutcomeFlags = flags,
            AxisOrthogonalityPreserved =
                evaluated.FunctionalIndependence.LocationReadsOnlyLocationInputs &&
                evaluated.FunctionalIndependence.TimeReadsOnlyTimeInputs &&
                evaluated.FunctionalIndependence.ParticipantReadsOnlyParticipantInputs &&
                evaluated.DerivationAudit.AllObservationsExistInEveryProfile,
            CandidateUniverseInvariant = evaluated.Manifest.CandidateUniverseInvariant,
            RelationInvariant = evaluated.Manifest.RelationInvariant,
            WorldValid = true
        };
        if (run.SetInclusionAudits.Any(item => !item.CandidateSetIsSubset) ||
            run.Profiles.Any(item => item.FalseExclusion) ||
            !run.AxisOrthogonalityPreserved ||
            !run.CandidateUniverseInvariant ||
            !run.RelationInvariant)
        {
            throw new InvalidOperationException(
                $"Seed {world.Seed} violated a preregistered composition, safety, or pairing invariant.");
        }

        return run;
    }

    private static CommonWorldConditionalReductionAudit Conditional(
        CommonWorldFieldCombinationExperimentDefinition experiment,
        GeneratedCommonFieldCombinationWorld world,
        CommonWorldObservationFixtureEvaluationResult evaluated,
        CommonWorldConditionalComparisonDefinition definition)
    {
        var baseline = Profile(evaluated.Runs, definition.BaselineProfileId);
        var combined = Profile(evaluated.Runs, definition.CombinedProfileId);
        var equal = SameCandidates(baseline, combined);
        var isTop = AddedPredicateIsTop(world, evaluated, definition.AddedAxis);
        var implied = AddedPredicateImpliedByBaseline(
            experiment,
            evaluated,
            definition.BaselineProfileId,
            definition.AddedAxis);
        var classification = !equal
            ? CommonWorldConditionalOutcomeClassification.AdditionalCandidateReduction
            : baseline.CandidateCount == 1
                ? CommonWorldConditionalOutcomeClassification.Saturation
                : isTop
                    ? CommonWorldConditionalOutcomeClassification.ObservationToConstraintPlateau
                    : implied
                        ? CommonWorldConditionalOutcomeClassification.ConstraintOverlapPlateau
                        : CommonWorldConditionalOutcomeClassification.ConstraintToCandidatePlateau;
        return new CommonWorldConditionalReductionAudit
        {
            Seed = world.Seed,
            ComparisonId = definition.Id,
            BaselineProfileId = baseline.ProfileId,
            CombinedProfileId = combined.ProfileId,
            AddedAxis = definition.AddedAxis,
            BaselineCandidateCount = baseline.CandidateCount,
            CombinedCandidateCount = combined.CandidateCount,
            AdditionalCandidateReduction = baseline.CandidateCount - combined.CandidateCount,
            CandidateSetsEqual = equal,
            AddedPredicateIsTopForCandidateDomain = isTop,
            AddedPredicateImpliedByBaselinePredicates = implied,
            Classification = classification
        };
    }

    private static bool AddedPredicateIsTop(
        GeneratedCommonFieldCombinationWorld world,
        CommonWorldObservationFixtureEvaluationResult evaluated,
        CommonWorldObservationAxis axis) => axis switch
        {
            CommonWorldObservationAxis.Location => world.RegionUniverse.All(region =>
                evaluated.LocationConstraint.AllowedRegionIds.Contains(region, StringComparer.Ordinal)),
            CommonWorldObservationAxis.Participant => world.RegionUniverse.All(region =>
                evaluated.ParticipantConstraint.AllowedRegionIds.Contains(region, StringComparer.Ordinal)),
            CommonWorldObservationAxis.Time => evaluated.CandidateUniverse.All(candidate =>
                Overlaps(candidate.TimeWindow, evaluated.TimeConstraint.ConstrainedTimeWindow)),
            _ => throw new ArgumentOutOfRangeException(nameof(axis))
        };

    private static bool AddedPredicateImpliedByBaseline(
        CommonWorldFieldCombinationExperimentDefinition experiment,
        CommonWorldObservationFixtureEvaluationResult evaluated,
        string baselineProfileId,
        CommonWorldObservationAxis addedAxis)
    {
        var baseline = experiment.Profiles.Single(item => item.Id == baselineProfileId);
        if (addedAxis == CommonWorldObservationAxis.Time ||
            (!baseline.SelectLocation && !baseline.SelectParticipant))
        {
            return false;
        }

        var baseRegions = new HashSet<string>(evaluated.CandidateUniverse.Select(item => item.RegionId));
        if (baseline.SelectLocation)
        {
            baseRegions.IntersectWith(evaluated.LocationConstraint.AllowedRegionIds);
        }

        if (baseline.SelectParticipant)
        {
            baseRegions.IntersectWith(evaluated.ParticipantConstraint.AllowedRegionIds);
        }

        var addedRegions = addedAxis switch
        {
            CommonWorldObservationAxis.Location => evaluated.LocationConstraint.AllowedRegionIds,
            CommonWorldObservationAxis.Participant => evaluated.ParticipantConstraint.AllowedRegionIds,
            _ => throw new ArgumentOutOfRangeException(nameof(addedAxis))
        };
        return baseRegions.All(region => addedRegions.Contains(region, StringComparer.Ordinal));
    }

    private static CommonWorldPerWorldMinimumAudit MinimumAudit(
        int seed,
        IReadOnlyList<CommonWorldProfileRun> runs)
    {
        var minimum = runs.Min(item => item.CandidateCount);
        var atMinimum = runs.Where(item => item.CandidateCount == minimum)
            .OrderBy(item => item.Order).ToArray();
        var inclusionMinimal = atMinimum.Where(candidate => !atMinimum.Any(other =>
                other.ProfileId != candidate.ProfileId &&
                IsProperAxisSubset(other.ActiveAxes, candidate.ActiveAxes)))
            .Select(item => item.ProfileId).ToArray();
        var groups = atMinimum.GroupBy(
                item => string.Join("|", item.CandidateIds),
                StringComparer.Ordinal)
            .Select(group => new CommonWorldMinimumCandidateSetGroup
            {
                CandidateSetId = InteractionStableIdentifier.Create(
                    "mcs",
                    seed.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    group.Key),
                CandidateIds = group.First().CandidateIds,
                ProfileIds = group.Select(item => item.ProfileId)
                    .OrderBy(item => item, StringComparer.Ordinal).ToArray()
            })
            .OrderBy(item => item.CandidateSetId, StringComparer.Ordinal)
            .ToArray();
        return new CommonWorldPerWorldMinimumAudit
        {
            Seed = seed,
            MinimumCandidateCount = minimum,
            ProfilesAtMinimumCount = atMinimum.Select(item => item.ProfileId).ToArray(),
            InclusionMinimalProfileIds = inclusionMinimal,
            CandidateSetGroups = groups,
            AllMinimumProfilesReturnSameCandidateSet = groups.Length == 1
        };
    }

    private static bool IsProperAxisSubset(
        IReadOnlyList<CommonWorldObservationAxis> left,
        IReadOnlyList<CommonWorldObservationAxis> right) =>
        left.Count < right.Count && left.All(right.Contains);

    private static IEnumerable<(CommonWorldProfileRun Left, CommonWorldProfileRun Right)>
        AllProfilePairs(IReadOnlyList<CommonWorldProfileRun> runs)
    {
        for (var left = 0; left < runs.Count; left++)
        {
            for (var right = left + 1; right < runs.Count; right++)
            {
                yield return (runs[left], runs[right]);
            }
        }
    }

    internal static CommonWorldObservationFixtureDefinition ToFixture(
        CommonWorldFieldCombinationExperimentDefinition experiment,
        GeneratedCommonFieldCombinationWorld world) => new()
        {
            Id = $"m1f16-world-{world.Seed:D3}",
            ScenarioId = experiment.ScenarioId,
            Seed = world.Seed,
            TargetEntityId = experiment.TargetEntityId,
            TargetEntityKind = experiment.TargetEntityKind,
            TargetStateContextId = experiment.TargetStateContextId,
            CandidateDomainId = experiment.CandidateDomainId,
            EvidenceAvailabilityCutoff = world.EvidenceAvailabilityCutoff,
            FixedRelationKind = experiment.FixedRelationKind,
            LocationObservation = new CommonWorldLocationObservationDefinition
            {
                SourceEvidenceIdentifier = world.LocationObservationSourceId,
                ObservedRegionIds = world.LocationObservedRegionIds,
                AvailableAtTick = world.EncounterTick + experiment.Generator.AvailabilityLagTicks,
                EvidenceState = EvidenceState.Confirmed,
                Visibility = InteractionVisibility.PublicEvidence,
                SourceAncestryIds = [world.LocationSourceAncestryId]
            },
            TimeObservation = new CommonWorldTimeObservationDefinition
            {
                SourceEvidenceIdentifier = world.LaterEventObservationSourceId,
                LaterEventKind = "later-public-event",
                ObservedStartTick = world.LaterEventTick,
                ObservedEndTick = world.LaterEventTick,
                AvailableAtTick = world.EvidenceAvailabilityCutoff,
                EvidenceState = EvidenceState.Confirmed,
                Visibility = InteractionVisibility.PublicEvidence,
                SourceAncestryIds = [world.LaterEventSourceAncestryId],
                PublicDependency = new BoundedDurationDependencyDefinition
                {
                    Id = world.DependencyId,
                    PredecessorStateKind = "target-predecessor-state",
                    LaterEventKind = "later-public-event",
                    TargetEntityId = experiment.TargetEntityId,
                    MinimumDurationTicks = world.DurationMinimumTicks,
                    MaximumDurationTicks = world.DurationMaximumTicks,
                    DurationSourceId = world.DurationSourceId,
                    AllowedPredecessorRegionIds = world.RegionUniverse
                }
            },
            ParticipantObservation = new CommonWorldParticipantObservationDefinition
            {
                EncounterSourceEvidenceIdentifier = world.EncounterObservationSourceId,
                EncounterStartTick = world.EncounterTick,
                EncounterEndTick = world.EncounterTick,
                AvailableAtTick = world.EncounterTick + experiment.Generator.AvailabilityLagTicks,
                EvidenceState = EvidenceState.Confirmed,
                Visibility = InteractionVisibility.PublicEvidence,
                SourceAncestryIds = [world.EncounterSourceAncestryId],
                PossibleParticipantIds = world.ObservedPossibleParticipantIds,
                ParticipantLocationEvidence = world.Participants.Select(item =>
                    new ParticipantLocationEvidenceDefinition
                    {
                        SourceEvidenceIdentifier = item.SourceEvidenceIdentifier,
                        ParticipantId = item.ParticipantId,
                        EntityKind = InteractionEntityKind.Person,
                        ApplicableStartTick = world.EncounterTick,
                        ApplicableEndTick = world.EncounterTick,
                        AllowedRegionIds = item.AllowedRegionIds,
                        AvailableAtTick = world.EncounterTick +
                            experiment.Generator.AvailabilityLagTicks,
                        EvidenceState = EvidenceState.Confirmed,
                        Visibility = InteractionVisibility.PublicEvidence,
                        UsageRole = PastStateEvidenceUsageRole.CandidateGeneration,
                        SourceAncestryIds = [item.SourceAncestryId]
                    }).ToArray()
            },
            CandidateStates = world.CandidateStates.Select(item =>
                new CommonWorldCandidateStateDefinition
                {
                    StartTick = item.StartTick,
                    EndTick = item.EndTick,
                    TemporalKind = item.StartTick == item.EndTick
                        ? CandidateWarpTemporalKind.EventPoint
                        : CandidateWarpTemporalKind.StateInterval,
                    RegionIds = [item.RegionId],
                    PublicSourceIdentifier = item.PublicSourceIdentifier
                }).ToArray(),
            TruthOtherParticipantId = world.TruthParticipantId,
            TruthCandidateStartTick = world.TruthCandidateStartTick,
            TruthCandidateEndTick = world.TruthCandidateEndTick,
            TruthCandidateRegionId = world.TruthRegionId,
            Profiles = experiment.Profiles
        };

    private static void ValidateWorld(
        CommonWorldFieldCombinationExperimentDefinition experiment,
        GeneratedCommonFieldCombinationWorld world)
    {
        var generator = experiment.Generator;
        if (world.GeneratorId != generator.Id ||
            world.Seed < experiment.RequiredSeedStart || world.Seed > experiment.RequiredSeedEnd ||
            world.CandidateCount < generator.CandidateCountMinimum ||
            world.CandidateCount > generator.CandidateCountMaximum ||
            world.CandidateStates.Count != world.CandidateCount ||
            world.RegionUniverse.Count < generator.RegionUniverseSizeMinimum ||
            world.RegionUniverse.Count > generator.RegionUniverseSizeMaximum ||
            world.CandidateBaseTick < generator.CandidateBaseTickMinimum ||
            world.CandidateBaseTick > generator.CandidateBaseTickMaximum)
        {
            throw new InvalidDataException($"Seed {world.Seed} is outside registered world ranges.");
        }

        if (world.RegionUniverse.Distinct(StringComparer.Ordinal).Count() != world.RegionUniverse.Count ||
            world.CandidateStates.Select(item => item.CandidateKey).Distinct(StringComparer.Ordinal).Count() != world.CandidateCount ||
            world.CandidateStates.Select(item => item.PublicSourceIdentifier).Distinct(StringComparer.Ordinal).Count() != world.CandidateCount ||
            world.CandidateStates.Select(item => (item.StartTick, item.RegionId)).Distinct().Count() != world.CandidateCount ||
            world.CandidateStates.Any(item =>
                item.StartTick < 0 ||
                item.EndTick < item.StartTick ||
                item.EndTick - item.StartTick < generator.CandidateIntervalWidthMinimum ||
                item.EndTick - item.StartTick > generator.CandidateIntervalWidthMaximum ||
                !world.RegionUniverse.Contains(item.RegionId, StringComparer.Ordinal)))
        {
            throw new InvalidDataException($"Seed {world.Seed} has an invalid candidate universe.");
        }

        var truthCandidates = world.CandidateStates.Where(item => item.IsTruthCandidate).ToArray();
        if (truthCandidates.Length != 1 ||
            truthCandidates[0].CandidateKey != world.TruthCandidateKey ||
            truthCandidates[0].StartTick != world.TruthCandidateStartTick ||
            truthCandidates[0].EndTick != world.TruthCandidateEndTick ||
            truthCandidates[0].RegionId != world.TruthRegionId ||
            world.EncounterTick != world.TruthCandidateStartTick ||
            !world.LocationObservedRegionIds.Contains(world.TruthRegionId, StringComparer.Ordinal))
        {
            throw new InvalidDataException($"Seed {world.Seed} truth is not compatible with L.");
        }

        if (world.DurationMinimumTicks < generator.DurationMinimumTickMinimum ||
            world.DurationMinimumTicks > generator.DurationMinimumTickMaximum ||
            world.DurationMaximumTicks - world.DurationMinimumTicks < generator.DurationWidthMinimum ||
            world.DurationMaximumTicks - world.DurationMinimumTicks > generator.DurationWidthMaximum ||
            world.ActualTruthDurationTicks < world.DurationMinimumTicks ||
            world.ActualTruthDurationTicks > world.DurationMaximumTicks ||
            world.LaterEventTick != world.EncounterTick + world.ActualTruthDurationTicks)
        {
            throw new InvalidDataException($"Seed {world.Seed} truth is not compatible with T.");
        }

        if (world.LocationObservedRegionIds.Count < generator.LocationObservedRegionCountMinimum ||
            world.LocationObservedRegionIds.Count > generator.LocationObservedRegionCountMaximum ||
            world.LocationObservedRegionIds.Distinct(StringComparer.Ordinal).Count() !=
                world.LocationObservedRegionIds.Count ||
            world.LocationObservedRegionIds.Any(item =>
                !world.RegionUniverse.Contains(item, StringComparer.Ordinal)) ||
            world.UnusedRegionCount != world.RegionUniverse.Count(region =>
                world.CandidateStates.All(candidate => candidate.RegionId != region)))
        {
            throw new InvalidDataException($"Seed {world.Seed} has invalid Region observations.");
        }

        if (world.Participants.Count < generator.ParticipantPoolSizeMinimum ||
            world.Participants.Count > generator.ParticipantPoolSizeMaximum ||
            world.Participants.Select(item => item.ParticipantId).Distinct(StringComparer.Ordinal).Count() != world.Participants.Count ||
            world.Participants.Select(item => item.SourceEvidenceIdentifier).Distinct(StringComparer.Ordinal).Count() != world.Participants.Count ||
            world.Participants.Any(item =>
                item.AllowedRegionIds.Count < generator.ParticipantRegionSetSizeMinimum ||
                item.AllowedRegionIds.Count > generator.ParticipantRegionSetSizeMaximum ||
                item.AllowedRegionIds.Distinct(StringComparer.Ordinal).Count() != item.AllowedRegionIds.Count ||
                item.AllowedRegionIds.Any(region => !world.RegionUniverse.Contains(region, StringComparer.Ordinal))) ||
            world.ObservedPossibleParticipantIds.Count != world.Participants.Count ||
            !world.ObservedPossibleParticipantIds.OrderBy(item => item, StringComparer.Ordinal).SequenceEqual(
                world.Participants.Select(item => item.ParticipantId).OrderBy(item => item, StringComparer.Ordinal)))
        {
            throw new InvalidDataException($"Seed {world.Seed} has invalid participant evidence.");
        }

        var truthParticipants = world.Participants.Where(item => item.IsTruthParticipant).ToArray();
        if (truthParticipants.Length != 1 ||
            truthParticipants[0].ParticipantId != world.TruthParticipantId ||
            !truthParticipants[0].AllowedRegionIds.Contains(world.TruthRegionId, StringComparer.Ordinal) ||
            world.EvidenceAvailabilityCutoff != world.LaterEventTick + generator.AvailabilityLagTicks)
        {
            throw new InvalidDataException($"Seed {world.Seed} truth is not compatible with P.");
        }
    }

    private static CommonWorldProfileRun Profile(
        IReadOnlyList<CommonWorldProfileRun> runs,
        string id) => runs.Single(item => item.ProfileId == id);

    private static bool IsSubset(IEnumerable<string> subset, IEnumerable<string> superset)
    {
        var outer = superset.ToHashSet(StringComparer.Ordinal);
        return subset.All(outer.Contains);
    }

    private static bool SameCandidates(CommonWorldProfileRun left, CommonWorldProfileRun right) =>
        left.CandidateIds.SequenceEqual(right.CandidateIds, StringComparer.Ordinal);

    private static bool Overlaps(InteractionTimeWindow left, InteractionTimeWindow right) =>
        left.StartTick <= right.EndTick && right.StartTick <= left.EndTick;
}

public sealed class CommonWorldFieldCombinationEvaluationRunner
{
    public CommonWorldFieldCombinationEvaluationResult Run(
        CommonWorldFieldCombinationEvaluationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        CommonWorldFieldCombinationExperimentValidator.Validate(request.Experiment, request.Seeds);
        var experiment = request.Experiment;
        var seeds = request.Seeds.Distinct().OrderBy(item => item).ToArray();
        var generator = new CommonWorldFieldCombinationSeededWorldGenerator();
        var evaluator = new CommonWorldFieldCombinationWorldEvaluator();
        var runs = seeds.Select(seed => evaluator.Evaluate(
                experiment,
                generator.Generate(experiment, seed),
                request.SourceCommitSha,
                request.DirtyWorkingTree))
            .OrderBy(item => item.Seed)
            .ToArray();
        var profileAggregates = experiment.Profiles.OrderBy(item => item.Order).Select(profile =>
        {
            var values = runs.Select(run => Profile(run, profile.Id)).ToArray();
            var counts = values.Select(item => item.CandidateCount).OrderBy(item => item).ToArray();
            return new CommonWorldFieldCombinationProfileAggregate
            {
                ProfileId = profile.Id,
                Order = profile.Order,
                SeedCount = values.Length,
                MeanCandidateCount = Mean(counts),
                MedianCandidateCount = Median(counts),
                MinimumCandidateCount = counts.Min(),
                MaximumCandidateCount = counts.Max(),
                ContainmentRate = values.Count(item => item.ContainsTruth) / (decimal)values.Length,
                FalseExclusionCount = values.Count(item => item.FalseExclusion),
                MeanReductionCountVsNone = Mean(values.Select(item => item.ReductionVsNoneCount)),
                MeanReductionFractionVsNone = Mean(values.Select(item => item.ReductionVsNoneFraction)),
                MeanCandidateGenerationWork = Mean(values.Select(item => item.CandidateGenerationWork)),
                MeanComponentsReceived = Mean(values.Select(item => item.CompositionAudit.InputComponentsReceived)),
                MeanPredicatesApplied = Mean(values.Select(item => item.CompositionAudit.PredicatesApplied)),
                MeanCandidatePredicateChecks = Mean(values.Select(item => item.CompositionAudit.CandidatePredicateChecks)),
                MeanSupportingSourceGroupCount = Mean(values.Select(item => item.SelectedSupportingSourceGroupCount)),
                MeanSelectedFieldCount = Mean(values.Select(item => item.SelectedFieldCount)),
                MeanParticipantAlternativesEvaluatedWhenSelected = Mean(runs.Select(run =>
                    profile.SelectParticipant
                        ? run.DerivationAudit.Participant.ParticipantAlternativesEvaluated
                        : 0)),
                DuplicateConstraintsSuppressed = values.Sum(item => item.CompositionAudit.DuplicateConstraintsSuppressed),
                AlternativeBranchesEvaluated = values.Sum(item => item.CompositionAudit.AlternativeBranchesEvaluated)
            };
        }).ToArray();
        var conditionalAggregates = experiment.ConditionalComparisons.Select(definition =>
        {
            var values = runs.Select(run => run.ConditionalReductions.Single(item =>
                item.ComparisonId == definition.Id)).ToArray();
            var reductions = values.Select(item => item.AdditionalCandidateReduction)
                .OrderBy(item => item).ToArray();
            return new CommonWorldConditionalReductionAggregate
            {
                ComparisonId = definition.Id,
                BaselineProfileId = definition.BaselineProfileId,
                CombinedProfileId = definition.CombinedProfileId,
                AddedAxis = definition.AddedAxis,
                MeanAdditionalCandidateReduction = Mean(reductions),
                MedianAdditionalCandidateReduction = Median(reductions),
                MinimumAdditionalCandidateReduction = reductions.Min(),
                MaximumAdditionalCandidateReduction = reductions.Max(),
                PositiveAdditionalEffectWorldCount = values.Count(item =>
                    item.AdditionalCandidateReduction > 0),
                ZeroAdditionalEffectWorldCount = values.Count(item => item.CandidateSetsEqual),
                ClassificationFrequencies = Enum.GetValues<CommonWorldConditionalOutcomeClassification>()
                    .Select(classification => new CommonWorldConditionalClassificationFrequency
                    {
                        Classification = classification,
                        WorldCount = values.Count(item => item.Classification == classification)
                    }).ToArray()
            };
        }).ToArray();
        var equivalenceAggregates = AllProfilePairs(experiment.Profiles).Select(pair =>
            new CommonWorldProfileEquivalenceAggregate
            {
                LeftProfileId = pair.Left.Id,
                RightProfileId = pair.Right.Id,
                EqualCandidateSetWorldCount = runs.Count(run => run.ProfileEquivalences.Single(item =>
                    item.LeftProfileId == pair.Left.Id &&
                    item.RightProfileId == pair.Right.Id).CandidateSetsEqual)
            }).ToArray();
        var minimumFrequencies = experiment.Profiles.OrderBy(item => item.Order).Select(profile =>
            new CommonWorldMinimumProfileFrequency
            {
                ProfileId = profile.Id,
                MinimumCountWorldCount = runs.Count(item =>
                    item.MinimumAudit.ProfilesAtMinimumCount.Contains(profile.Id, StringComparer.Ordinal)),
                InclusionMinimalWorldCount = runs.Count(item =>
                    item.MinimumAudit.InclusionMinimalProfileIds.Contains(profile.Id, StringComparer.Ordinal))
            }).ToArray();
        var inclusionAudits = runs.SelectMany(item => item.SetInclusionAudits).ToArray();
        var allProfiles = runs.SelectMany(item => item.Profiles).ToArray();
        var summary = new CommonWorldFieldCombinationSummary
        {
            WorldCount = runs.Length,
            ProfileEvaluationCount = allProfiles.Length,
            SetInclusionAuditCount = inclusionAudits.Length,
            JointSetInclusionViolationCount = inclusionAudits.Count(item => !item.CandidateSetIsSubset),
            FalseExclusionCount = allProfiles.Count(item => item.FalseExclusion),
            AxisOrthogonalityViolationCount = runs.Count(item => !item.AxisOrthogonalityPreserved),
            CandidateUniverseInvarianceViolationCount = runs.Count(item => !item.CandidateUniverseInvariant),
            RelationInvarianceViolationCount = runs.Count(item => !item.RelationInvariant),
            SkippedSeedCount = 0,
            RegeneratedSeedCount = 0,
            AllThreeSinglesDifferWorldCount = runs.Count(item =>
                item.OutcomeFlags.AllThreeSingleAxisCandidateSetsDiffer),
            SingleAxisDominanceWorldCount = runs.Count(item =>
                item.OutcomeFlags.AFieldSingleAxisSetIsSubsetOfBothOthers),
            AnyTwoAxisPlateauWorldCount = runs.Count(item => item.OutcomeFlags.AnyTwoAxisPlateau),
            AnyThirdAxisAdditionalReductionWorldCount = runs.Count(item =>
                item.OutcomeFlags.AnyThirdAxisAdditionalReduction),
            FullSaturationWorldCount = runs.Count(item =>
                item.OutcomeFlags.FullProfileSaturatedAtOneCandidate),
            MeanPerWorldMinimumCandidateCount = Mean(runs.Select(item =>
                item.MinimumAudit.MinimumCandidateCount)),
            AllMinimumProfilesSameCandidateSetWorldCount = runs.Count(item =>
                item.MinimumAudit.AllMinimumProfilesReturnSameCandidateSet)
        };
        if (summary.JointSetInclusionViolationCount > 0 ||
            summary.FalseExclusionCount > 0 ||
            summary.AxisOrthogonalityViolationCount > 0 ||
            summary.CandidateUniverseInvarianceViolationCount > 0 ||
            summary.RelationInvarianceViolationCount > 0 ||
            runs.Any(item => !item.WorldValid) ||
            summary.ProfileEvaluationCount != 1600 ||
            summary.SetInclusionAuditCount != 2400)
        {
            throw new InvalidOperationException(
                "A preregistered common-world pairing, composition, or safety failure occurred; " +
                "aggregate interpretation is blocked.");
        }

        return new CommonWorldFieldCombinationEvaluationResult
        {
            Manifest = new CommonWorldFieldCombinationManifest
            {
                ExperimentId = experiment.Id,
                ScenarioId = experiment.ScenarioId,
                GeneratorId = experiment.Generator.Id,
                ExperimentContentSha256 = request.ExperimentContentSha256,
                SourceCommitSha = request.SourceCommitSha,
                DirtyWorkingTree = request.DirtyWorkingTree,
                Seeds = seeds,
                ProfileIds = experiment.Profiles.OrderBy(item => item.Order)
                    .Select(item => item.Id).ToArray(),
                WorldCount = runs.Length,
                ProfileEvaluationCount = allProfiles.Length,
                LocationRuleId = ObservedRegionLocationConstraintDeriver.RuleId,
                TimeRuleId = BoundedDurationObservationConstraintDeriver.RuleId,
                ParticipantRuleId = EncounterParticipantLocationConstraintDeriver.RuleId,
                FixedRelationKind = experiment.FixedRelationKind,
                TruthUsedForDerivationOrComposition = false,
                OutputFiles = CommonWorldFieldCombinationOutputNames.All
            },
            Generator = experiment.Generator,
            Profiles = experiment.Profiles.OrderBy(item => item.Order).ToArray(),
            PrimarySetInclusions = experiment.PrimarySetInclusions,
            ConditionalComparisons = experiment.ConditionalComparisons,
            FailureConditions = experiment.FailureConditions,
            Runs = runs,
            ProfileAggregates = profileAggregates,
            ConditionalReductionAggregates = conditionalAggregates,
            ProfileEquivalenceAggregates = equivalenceAggregates,
            MinimumProfileFrequencies = minimumFrequencies,
            RepresentationSummary = new CommonWorldFieldCombinationRepresentationSummary
            {
                RealizedCandidateCountMinimum = runs.Min(item => item.World.CandidateCount),
                RealizedCandidateCountMaximum = runs.Max(item => item.World.CandidateCount),
                RealizedRegionUniverseMinimum = runs.Min(item => item.World.RegionUniverse.Count),
                RealizedRegionUniverseMaximum = runs.Max(item => item.World.RegionUniverse.Count),
                WorldsWithUnusedRegions = runs.Count(item => item.World.UnusedRegionCount > 0),
                WorldsWithSameRegionCandidates = runs.Count(item => item.World.CandidateStates
                    .GroupBy(candidate => candidate.RegionId).Any(group => group.Count() > 1)),
                WorldsWithSameStartTickCandidates = runs.Count(item => item.World.CandidateStates
                    .GroupBy(candidate => candidate.StartTick).Any(group => group.Count() > 1)),
                WorldsWithStateIntervals = runs.Count(item => item.World.CandidateStates.Any(candidate =>
                    candidate.StartTick != candidate.EndTick)),
                WorldsWithIdenticalParticipantEvidence = runs.Count(item =>
                    item.World.ParticipantEvidenceOverlapKind == ParticipantEvidenceOverlapKind.Identical),
                WorldsWithPartialParticipantEvidenceOverlap = runs.Count(item =>
                    item.World.ParticipantEvidenceOverlapKind == ParticipantEvidenceOverlapKind.PartiallyOverlapping),
                WorldsWithDisjointParticipantEvidence = runs.Count(item =>
                    item.World.ParticipantEvidenceOverlapKind == ParticipantEvidenceOverlapKind.CompletelyDistinct)
            },
            Summary = summary,
            OriginalF15FixtureRetained = true,
            F8ThroughF14RegressionsRetained = true,
            ExistingS5Changed = false,
            InterpretationBoundary =
                "Results are paired field-combination effects only within this preregistered, " +
                "purified synthetic common-world family. They do not rank fields generally or " +
                "establish real sensors, minimum sources, S5 applicability, or rescue performance."
        };
    }

    private static CommonWorldProfileRun Profile(CommonWorldFieldCombinationWorldRun run, string id) =>
        run.Profiles.Single(item => item.ProfileId == id);

    private static IEnumerable<(CommonWorldProfileDefinition Left, CommonWorldProfileDefinition Right)>
        AllProfilePairs(IReadOnlyList<CommonWorldProfileDefinition> profiles)
    {
        var ordered = profiles.OrderBy(item => item.Order).ToArray();
        for (var left = 0; left < ordered.Length; left++)
        {
            for (var right = left + 1; right < ordered.Length; right++)
            {
                yield return (ordered[left], ordered[right]);
            }
        }
    }

    private static decimal Mean(IEnumerable<int> values)
    {
        var array = values.ToArray();
        return array.Sum(item => (decimal)item) / array.Length;
    }

    private static decimal Mean(IEnumerable<decimal> values)
    {
        var array = values.ToArray();
        return array.Sum() / array.Length;
    }

    private static decimal Median(IReadOnlyList<int> ordered)
    {
        var midpoint = ordered.Count / 2;
        return ordered.Count % 2 == 1
            ? ordered[midpoint]
            : (ordered[midpoint - 1] + ordered[midpoint]) / 2m;
    }
}
