using FootprintSim.Evidence;
using FootprintSim.Inference.Derivation;
using FootprintSim.Interactions;
using FootprintSim.Interactions.CandidateWarps;

namespace FootprintSim.Evaluation;

public sealed class TimeResolutionPairedWorldEvaluator
{
    public TimeResolutionPairedWorldRun Evaluate(
        TimeResolutionPairedExperimentDefinition experiment,
        GeneratedTimeResolutionWorld world,
        string experimentContentSha256,
        string sourceCommitSha,
        bool dirtyWorkingTree)
    {
        ArgumentNullException.ThrowIfNull(experiment);
        ArgumentNullException.ThrowIfNull(world);
        ValidateWorld(experiment, world);

        var fixture = new BoundedDurationTimeFixtureDefinition
        {
            Id = $"{experiment.Id}-seed-{world.Seed:D3}",
            ScenarioId = experiment.ScenarioId,
            Seed = world.Seed,
            TargetEntityId = experiment.TargetEntityId,
            SourceEvidenceIdentifier = experiment.SourceEvidenceIdentifier,
            LaterEventKind = experiment.LaterEventKind,
            AvailableAtTick = world.AvailableAtTick,
            EvidenceAvailabilityCutoff = world.EvidenceAvailabilityCutoff,
            EvidenceState = EvidenceState.Confirmed,
            SourceAncestryIds = experiment.SourceAncestryIds,
            PublicDependency = new BoundedDurationDependencyDefinition
            {
                Id = experiment.DependencyId,
                PredecessorStateKind = experiment.PredecessorStateKind,
                LaterEventKind = experiment.LaterEventKind,
                TargetEntityId = experiment.TargetEntityId,
                MinimumDurationTicks = world.DurationBounds.MinimumTicks,
                MaximumDurationTicks = world.DurationBounds.MaximumTicks,
                DurationSourceId = experiment.DurationSourceId,
                AllowedPredecessorRegionIds = world.CandidateStates
                    .Select(item => item.RegionId)
                    .OrderBy(item => item, StringComparer.Ordinal)
                    .ToArray()
            },
            CandidateStates = world.CandidateStates.Select(item =>
                new BoundedDurationCandidateStateDefinition
                {
                    StartTick = item.Tick,
                    EndTick = item.Tick,
                    TemporalKind = CandidateWarpTemporalKind.EventPoint,
                    RegionIds = [item.RegionId]
                }).ToArray(),
            TruthRegionId = world.TruthRegionId,
            Profiles = experiment.Profiles.OrderBy(item => item.InformationOrder).Select(item =>
                new BoundedDurationTimeProfileDefinition
                {
                    Id = item.Id,
                    InformationOrder = item.InformationOrder,
                    Description = item.Rationale,
                    ObservedStartTick = item.OccurrenceTimeUnknown
                        ? null
                        : checked(world.LaterEventTick + item.ObservedStartOffsetFromLaterTick!.Value),
                    ObservedEndTick = item.OccurrenceTimeUnknown
                        ? null
                        : checked(world.LaterEventTick + item.ObservedEndOffsetFromLaterTick!.Value),
                    BoundaryRationale = item.Rationale
                }).ToArray()
        };
        var evaluated = new BoundedDurationTimeFixtureEvaluationRunner().Run(
            new BoundedDurationTimeFixtureEvaluationRequest
            {
                Fixture = fixture,
                FixtureContentSha256 = experimentContentSha256,
                SourceCommitSha = sourceCommitSha,
                DirtyWorkingTree = dirtyWorkingTree
            });
        if (!string.Equals(
                evaluated.IntervalFormula,
                "[o_min - d_max, o_max - d_min]",
                StringComparison.Ordinal) ||
            evaluated.ExistingS5Changed)
        {
            throw new InvalidOperationException("The fixed M1-F8 rule boundary changed.");
        }

        var unknown = evaluated.Runs.Single(item => item.ProfileId == "unknown");
        var unknownCount = unknown.CandidateCount;
        var profiles = evaluated.Runs.OrderBy(item => item.InformationOrder).Select(item =>
        {
            var reductionCount = unknownCount - item.CandidateCount;
            return new TimeResolutionPairedProfileRun
            {
                ProfileId = item.ProfileId,
                InformationOrder = item.InformationOrder,
                SourceObservationPresent = item.SourceObservationPresent,
                SourceObservationId = item.SourceObservationId,
                OccurrenceTimeKnowledge = item.OccurrenceTimeKnowledge,
                TimeWindowKind = item.TimeWindowKind,
                ObservedOccurrenceWindow = item.ObservedOccurrenceWindow,
                AvailableAtTick = item.AvailableAtTick,
                ConstraintGenerated = item.ConstraintGenerated,
                ConstraintId = item.ConstraintId,
                DerivedPastWindow = item.DerivedPastWindow,
                FailureReason = item.DerivationAudit.FailureReason,
                RuleId = item.DerivationAudit.RuleId,
                PublicInputIdsUsed = item.DerivationAudit.PublicInputIdsUsed,
                UsageRole = item.DerivationAudit.UsageRole,
                SourceGroupId = item.DerivationAudit.SourceGroupId,
                SourceAncestryIds = item.DerivationAudit.SourceAncestryIds,
                CandidateIds = item.Candidates.Select(candidate => candidate.CandidateId)
                    .OrderBy(candidateId => candidateId, StringComparer.Ordinal).ToArray(),
                CandidateRegionIds = item.Candidates.Select(candidate => candidate.RegionId)
                    .OrderBy(regionId => regionId, StringComparer.Ordinal).ToArray(),
                CandidateCount = item.CandidateCount,
                ContainsTruth = item.ContainsTruth,
                FalseExclusion = item.FalseExclusion,
                CandidateReductionCountVsUnknown = reductionCount,
                CandidateReductionFractionVsUnknown = reductionCount / (decimal)unknownCount,
                CandidateGenerationWork = item.CandidateGenerationWork,
                EvidenceRecordsInspected = item.EvidenceRecordsInspected,
                SourceGroupsInspected = item.SourceGroupsInspected,
                GeneratedConstraintSourceGroupCount = item.GeneratedConstraintSourceGroupCount
            };
        }).ToArray();
        var monotonicity = profiles.Zip(profiles.Skip(1), (morePrecise, coarser) =>
        {
            var windowSubset = IsWindowSubset(
                morePrecise.DerivedPastWindow,
                coarser.DerivedPastWindow);
            var windowStrict = IsStrictWindowExpansion(
                morePrecise.DerivedPastWindow,
                coarser.DerivedPastWindow);
            var candidateSubset = morePrecise.CandidateIds.All(item =>
                coarser.CandidateIds.Contains(item, StringComparer.Ordinal));
            var candidateEqual = morePrecise.CandidateIds.SequenceEqual(coarser.CandidateIds);
            return new TimeResolutionPairedMonotonicityAudit
            {
                Seed = world.Seed,
                MorePreciseProfileId = morePrecise.ProfileId,
                CoarserProfileId = coarser.ProfileId,
                MorePreciseDerivedWindow = morePrecise.DerivedPastWindow,
                CoarserDerivedWindow = coarser.DerivedPastWindow,
                DerivedWindowIsSubset = windowSubset,
                DerivedWindowStrictlyExpanded = windowStrict,
                MorePreciseCandidateIds = morePrecise.CandidateIds,
                CoarserCandidateIds = coarser.CandidateIds,
                CandidateSetIsSubset = candidateSubset,
                CandidateSetsEqual = candidateEqual,
                ConstraintWidenedButCandidateUnchanged = windowStrict && candidateEqual
            };
        }).ToArray();
        var unknownCandidates = evaluated.Runs.Single(item => item.ProfileId == "unknown").Candidates;
        var candidateStates = world.CandidateStates.Select(item =>
            new TimeResolutionPairedCandidateStateRun
            {
                CandidateKey = item.CandidateKey,
                CandidateId = unknownCandidates.Single(candidate =>
                    string.Equals(candidate.RegionId, item.RegionId, StringComparison.Ordinal)).CandidateId,
                Tick = item.Tick,
                RegionId = item.RegionId,
                IsTruthCandidate = item.IsTruthCandidate
            }).ToArray();
        var truthCandidateId = candidateStates.Single(item => item.IsTruthCandidate).CandidateId;
        var sourceGroupIds = profiles.Select(item => item.SourceGroupId)
            .Distinct(StringComparer.Ordinal).ToArray();
        if (sourceGroupIds.Length != 1 ||
            profiles[..3].Any(item =>
                !item.ConstraintGenerated ||
                item.FailureReason != ObservationConstraintDerivationFailureReason.None) ||
            profiles[3].ConstraintGenerated ||
            profiles[3].FailureReason !=
                ObservationConstraintDerivationFailureReason.UnknownOccurrenceTime ||
            profiles.Any(item => !item.SourceObservationPresent))
        {
            throw new InvalidOperationException(
                $"Seed {world.Seed} violated the fixed derivation or source-independence contract.");
        }

        return new TimeResolutionPairedWorldRun
        {
            Seed = world.Seed,
            GeneratorId = world.GeneratorId,
            PublicWorldId = world.PublicWorldId,
            TruthWorldId = world.TruthWorldId,
            CandidateCount = world.CandidateCount,
            LaterEventTick = world.LaterEventTick,
            DurationBounds = world.DurationBounds,
            DurationWidthTicks = world.DurationWidthTicks,
            ActualTruthDurationTicks = world.ActualTruthDurationTicks,
            CandidateTickMinimum = world.CandidateTickMinimum,
            CandidateTickMaximum = world.CandidateTickMaximum,
            AvailableAtTick = world.AvailableAtTick,
            EvidenceAvailabilityCutoff = world.EvidenceAvailabilityCutoff,
            PublicDependencyId = evaluated.PublicDependency.DependencyId,
            PublicDependencyInputId = evaluated.PublicDependency.PublicInputId,
            DurationSourceId = evaluated.PublicDependency.DurationSourceId,
            TruthCandidateKey = world.TruthCandidateKey,
            TruthCandidateId = truthCandidateId,
            TruthRegionId = world.TruthRegionId,
            TruthTick = world.TruthTick,
            CandidateStates = candidateStates,
            Profiles = profiles,
            MonotonicityAudits = monotonicity,
            WorldValid = true
        };
    }

    private static void ValidateWorld(
        TimeResolutionPairedExperimentDefinition experiment,
        GeneratedTimeResolutionWorld world)
    {
        var generator = experiment.Generator;
        if (!string.Equals(world.GeneratorId, generator.Id, StringComparison.Ordinal) ||
            world.Seed < experiment.RequiredSeedStart || world.Seed > experiment.RequiredSeedEnd ||
            world.CandidateCount < generator.CandidateCountMinimum ||
            world.CandidateCount > generator.CandidateCountMaximum ||
            world.CandidateStates.Count != world.CandidateCount ||
            world.LaterEventTick < generator.LaterEventTickMinimum ||
            world.LaterEventTick > generator.LaterEventTickMaximum ||
            world.DurationBounds.MinimumTicks < generator.DurationMinimumTickMinimum ||
            world.DurationBounds.MinimumTicks > generator.DurationMinimumTickMaximum ||
            world.DurationWidthTicks < generator.DurationWidthMinimum ||
            world.DurationWidthTicks > generator.DurationWidthMaximum ||
            world.DurationBounds.MaximumTicks - world.DurationBounds.MinimumTicks !=
                world.DurationWidthTicks ||
            world.ActualTruthDurationTicks < world.DurationBounds.MinimumTicks ||
            world.ActualTruthDurationTicks > world.DurationBounds.MaximumTicks)
        {
            throw new InvalidDataException($"Generated world for seed {world.Seed} is outside registered ranges.");
        }

        if (world.CandidateStates.Select(item => item.CandidateKey)
                .Distinct(StringComparer.Ordinal).Count() != world.CandidateCount ||
            world.CandidateStates.Select(item => item.RegionId)
                .Distinct(StringComparer.Ordinal).Count() != world.CandidateCount ||
            world.CandidateStates.Any(item =>
                item.Tick < world.CandidateTickMinimum || item.Tick > world.CandidateTickMaximum) ||
            world.CandidateStates.Count(item => item.IsTruthCandidate) != 1)
        {
            throw new InvalidDataException($"Generated candidate layer for seed {world.Seed} is invalid.");
        }

        var truth = world.CandidateStates.Single(item => item.IsTruthCandidate);
        var exactWindow = new InteractionTimeWindow(
            world.LaterEventTick - world.DurationBounds.MaximumTicks,
            world.LaterEventTick - world.DurationBounds.MinimumTicks);
        if (!string.Equals(truth.CandidateKey, world.TruthCandidateKey, StringComparison.Ordinal) ||
            !string.Equals(truth.RegionId, world.TruthRegionId, StringComparison.Ordinal) ||
            truth.Tick != world.TruthTick ||
            world.TruthTick != world.LaterEventTick - world.ActualTruthDurationTicks ||
            !Contains(exactWindow, world.TruthTick) ||
            world.CandidateTickMinimum < 0)
        {
            throw new InvalidDataException($"Generated truth for seed {world.Seed} is incompatible.");
        }
    }

    private static bool Contains(InteractionTimeWindow window, long tick) =>
        window.StartTick <= tick && tick <= window.EndTick;

    private static bool IsWindowSubset(
        InteractionTimeWindow? morePrecise,
        InteractionTimeWindow? coarser) =>
        morePrecise is not null &&
        (coarser is null ||
            (coarser.StartTick <= morePrecise.StartTick &&
                morePrecise.EndTick <= coarser.EndTick));

    private static bool IsStrictWindowExpansion(
        InteractionTimeWindow? morePrecise,
        InteractionTimeWindow? coarser) =>
        morePrecise is not null && coarser is not null &&
        coarser.StartTick <= morePrecise.StartTick &&
        morePrecise.EndTick <= coarser.EndTick &&
        (coarser.StartTick < morePrecise.StartTick ||
            morePrecise.EndTick < coarser.EndTick);
}

public sealed class TimeResolutionPairedEvaluationRunner
{
    public TimeResolutionPairedEvaluationResult Run(TimeResolutionPairedEvaluationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        TimeResolutionPairedExperimentValidator.Validate(request.Experiment, request.Seeds);
        var experiment = request.Experiment;
        var seeds = request.Seeds.OrderBy(item => item).ToArray();
        var generator = new TimeResolutionSeededWorldGenerator();
        var evaluator = new TimeResolutionPairedWorldEvaluator();
        var runs = seeds.Select(seed => evaluator.Evaluate(
                experiment,
                generator.Generate(experiment, seed),
                request.ExperimentContentSha256,
                request.SourceCommitSha,
                request.DirtyWorkingTree))
            .OrderBy(item => item.Seed)
            .ToArray();
        var aggregates = experiment.Profiles.OrderBy(item => item.InformationOrder).Select(profile =>
        {
            var values = runs.Select(item => Profile(item, profile.Id)).ToArray();
            var candidateCounts = values.Select(item => item.CandidateCount).OrderBy(item => item).ToArray();
            return new TimeResolutionPairedProfileAggregate
            {
                ProfileId = profile.Id,
                InformationOrder = profile.InformationOrder,
                SeedCount = values.Length,
                MeanCandidateCount = Mean(candidateCounts),
                MedianCandidateCount = Median(candidateCounts),
                MinimumCandidateCount = candidateCounts.Min(),
                MaximumCandidateCount = candidateCounts.Max(),
                ContainmentRate = values.Count(item => item.ContainsTruth) / (decimal)values.Length,
                FalseExclusionCount = values.Count(item => item.FalseExclusion),
                MeanCandidateReductionCountVsUnknown = Mean(
                    values.Select(item => item.CandidateReductionCountVsUnknown)),
                MeanCandidateReductionFractionVsUnknown = Mean(
                    values.Select(item => item.CandidateReductionFractionVsUnknown)),
                MeanCandidateGenerationWork = Mean(
                    values.Select(item => item.CandidateGenerationWork))
            };
        }).ToArray();
        var monotonicity = runs.SelectMany(item => item.MonotonicityAudits).ToArray();
        var finiteEqual = runs.Where(item =>
                SameCandidates(Profile(item, "exact"), Profile(item, "medium")) &&
                SameCandidates(Profile(item, "medium"), Profile(item, "broad")))
            .Select(item => item.Seed).ToArray();
        var finiteWindowEqual = runs.Where(item =>
                SameWindow(Profile(item, "exact").DerivedPastWindow, Profile(item, "medium").DerivedPastWindow) ||
                SameWindow(Profile(item, "medium").DerivedPastWindow, Profile(item, "broad").DerivedPastWindow))
            .Select(item => item.Seed).ToArray();
        var widenedUnchanged = runs.Where(item => item.MonotonicityAudits.Any(audit =>
                audit.ConstraintWidenedButCandidateUnchanged))
            .Select(item => item.Seed).ToArray();
        var broadUnknownSaturation = runs.Where(item =>
                SameCandidates(Profile(item, "broad"), Profile(item, "unknown")))
            .Select(item => item.Seed).ToArray();
        var summary = new TimeResolutionPairedSummary
        {
            WorldCount = runs.Length,
            PairedProfileRunCount = runs.Sum(item => item.Profiles.Count),
            DerivedWindowMonotonicityViolationCount = monotonicity.Count(item =>
                !item.DerivedWindowIsSubset),
            CandidateSetMonotonicityViolationCount = monotonicity.Count(item =>
                !item.CandidateSetIsSubset),
            FalseExclusionCount = runs.SelectMany(item => item.Profiles).Count(item => item.FalseExclusion),
            ExactLessThanMediumSeedCount = runs.Count(item =>
                Profile(item, "exact").CandidateCount < Profile(item, "medium").CandidateCount),
            MediumLessThanBroadSeedCount = runs.Count(item =>
                Profile(item, "medium").CandidateCount < Profile(item, "broad").CandidateCount),
            BroadLessThanUnknownSeedCount = runs.Count(item =>
                Profile(item, "broad").CandidateCount < Profile(item, "unknown").CandidateCount),
            FiniteProfilesAllEqualSeedCount = finiteEqual.Length,
            FiniteDerivedWindowEqualitySeedCount = finiteWindowEqual.Length,
            ConstraintWidenedButCandidateUnchangedSeedCount = widenedUnchanged.Length,
            BroadUnknownSaturationSeedCount = broadUnknownSaturation.Length,
            FiniteProfilesAllEqualSeeds = finiteEqual,
            ConstraintWidenedButCandidateUnchangedSeeds = widenedUnchanged,
            BroadUnknownSaturationSeeds = broadUnknownSaturation
        };
        if (summary.DerivedWindowMonotonicityViolationCount > 0 ||
            summary.CandidateSetMonotonicityViolationCount > 0 ||
            summary.FalseExclusionCount > 0)
        {
            throw new InvalidOperationException(
                "A preregistered monotonicity or safety failure occurred; aggregate interpretation is blocked.");
        }

        var widths = runs.GroupBy(item => item.DurationWidthTicks).OrderBy(item => item.Key).ToArray();
        return new TimeResolutionPairedEvaluationResult
        {
            Manifest = new TimeResolutionPairedManifest
            {
                ExperimentId = experiment.Id,
                ScenarioId = experiment.ScenarioId,
                GeneratorId = experiment.Generator.Id,
                RuleId = BoundedDurationObservationConstraintDeriver.RuleId,
                ExperimentContentSha256 = request.ExperimentContentSha256,
                SourceCommitSha = request.SourceCommitSha,
                DirtyWorkingTree = request.DirtyWorkingTree,
                Seeds = seeds,
                ProfileIds = experiment.Profiles.OrderBy(item => item.InformationOrder)
                    .Select(item => item.Id).ToArray(),
                WorldCount = runs.Length,
                PairedProfileRunCount = summary.PairedProfileRunCount,
                OutputFiles = TimeResolutionPairedOutputNames.All
            },
            IntervalFormula = "[o_min - d_max, o_max - d_min]",
            Generator = experiment.Generator,
            Profiles = experiment.Profiles.OrderBy(item => item.InformationOrder).ToArray(),
            Runs = runs,
            Aggregates = aggregates,
            DurationSummary = new TimeResolutionDurationSummary
            {
                MinimumDurationMinimumTicks = runs.Min(item => item.DurationBounds.MinimumTicks),
                MaximumDurationMinimumTicks = runs.Max(item => item.DurationBounds.MinimumTicks),
                MinimumDurationMaximumTicks = runs.Min(item => item.DurationBounds.MaximumTicks),
                MaximumDurationMaximumTicks = runs.Max(item => item.DurationBounds.MaximumTicks),
                MinimumWidthTicks = runs.Min(item => item.DurationWidthTicks),
                MaximumWidthTicks = runs.Max(item => item.DurationWidthTicks),
                MeanWidthTicks = Mean(runs.Select(item => item.DurationWidthTicks)),
                WidthFrequencies = widths.Select(item => new DurationWidthFrequency
                {
                    WidthTicks = item.Key,
                    WorldCount = item.Count()
                }).ToArray()
            },
            Summary = summary,
            OriginalF8FixtureRetained = true,
            ExistingS5Changed = false,
            InterpretationBoundary =
                "Results apply only to the preregistered synthetic world family under the unchanged " +
                "bounded-duration-predecessor-window-v1 public rule."
        };
    }

    private static TimeResolutionPairedProfileRun Profile(
        TimeResolutionPairedWorldRun run,
        string profileId) => run.Profiles.Single(item => item.ProfileId == profileId);

    private static bool SameCandidates(
        TimeResolutionPairedProfileRun left,
        TimeResolutionPairedProfileRun right) => left.CandidateIds.SequenceEqual(right.CandidateIds);

    private static bool SameWindow(InteractionTimeWindow? left, InteractionTimeWindow? right) =>
        left == right;

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
