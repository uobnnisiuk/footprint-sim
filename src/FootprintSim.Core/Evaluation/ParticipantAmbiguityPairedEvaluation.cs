using FootprintSim.Evidence;
using FootprintSim.Inference.Derivation;
using FootprintSim.Inference.PastStates;
using FootprintSim.Interactions;
using FootprintSim.Interactions.CandidateWarps;

namespace FootprintSim.Evaluation;

public sealed class ParticipantAmbiguityPairedWorldEvaluator
{
    public ParticipantAmbiguityPairedWorldRun Evaluate(
        ParticipantAmbiguityPairedExperimentDefinition experiment,
        GeneratedParticipantAmbiguityWorld world)
    {
        ArgumentNullException.ThrowIfNull(experiment);
        ArgumentNullException.ThrowIfNull(world);
        ValidatePublicWorld(experiment, world);

        var encounterWindow = InteractionTimeWindow.At(world.EncounterTick);
        var participantEvidence = world.Participants.Select(item =>
            PublicParticipantLocationEvidence.Create(
                experiment.ScenarioId,
                world.Seed,
                item.SourceEvidenceIdentifier,
                item.ParticipantId,
                InteractionEntityKind.Person,
                encounterWindow,
                item.AllowedRegionIds,
                world.AvailableAtTick,
                EvidenceState.Confirmed,
                PastStateEvidenceUsageRole.CandidateGeneration,
                InteractionVisibility.PublicEvidence,
                item.SourceAncestryIds))
            .OrderBy(item => item.ParticipantId, StringComparer.Ordinal)
            .ToArray();
        var publicCandidates = world.CandidateStates.Select(item =>
            new PublicCandidateWarpStateDefinition
            {
                TimeWindow = encounterWindow,
                TemporalKind = CandidateWarpTemporalKind.EventPoint,
                RegionIds = [item.RegionId],
                EvidenceBoundaryState = EvidenceState.Confirmed,
                Provenance =
                [
                    CandidateWarpProvenance.Create(
                        CandidateWarpSourceKind.PublicCausalModel,
                        item.PublicSourceIdentifier,
                        availableAtTick: 0,
                        isPersonEvidence: false,
                        isDeviceOnlyEvidence: false,
                        EvidenceState.Confirmed,
                        [item.RegionId])
                ]
            }).ToArray();
        var candidateWarps = PublicCandidateWarpFactory.Create(
            experiment.ScenarioId,
            world.Seed,
            experiment.TargetEntityId,
            publicCandidates);
        var generatedStateIds = world.CandidateStates.ToDictionary(
            item => item.CandidateKey,
            item => candidateWarps.Warps.SelectMany(warp => warp.States).Single(state =>
                state.Provenance.Any(provenance => string.Equals(
                    provenance.SourceIdentifier,
                    item.PublicSourceIdentifier,
                    StringComparison.Ordinal))).StateId,
            StringComparer.Ordinal);
        var deriver = new EncounterParticipantLocationConstraintDeriver();
        var reconstructor = new ObservationDerivedHistoricalCandidateReconstructor();
        var profiles = experiment.Profiles
            .OrderBy(item => item.InformationOrder)
            .ToArray();
        var preliminary = profiles.Select(profile =>
        {
            var participantIds = ParticipantIds(world, profile.Id);
            var information = profile.IdentityUnknown
                ? OtherParticipantInformation.UnknownIdentity(InteractionEntityKind.Person)
                : participantIds.Count == 1
                    ? OtherParticipantInformation.Exact(
                        participantIds.Single(),
                        InteractionEntityKind.Person)
                    : OtherParticipantInformation.FiniteAlternatives(
                        participantIds,
                        InteractionEntityKind.Person);
            var observation = PublicEncounterParticipantObservation.Create(
                experiment.ScenarioId,
                world.Seed,
                experiment.EncounterSourceEvidenceIdentifier,
                experiment.TargetEntityId,
                experiment.TargetEntityKind,
                information,
                experiment.RelationKind,
                encounterWindow,
                world.AvailableAtTick,
                experiment.EncounterEvidenceState,
                InteractionVisibility.PublicEvidence,
                experiment.EncounterSourceAncestryIds);
            var derivation = deriver.Derive(
                observation,
                participantEvidence,
                world.EvidenceAvailabilityCutoff,
                PastStateEvidenceUsageRole.CandidateGeneration);
            var reconstruction = reconstructor.Reconstruct(
                experiment.ScenarioId,
                world.Seed,
                experiment.TargetEntityId,
                publicCandidates,
                derivation.Constraint);
            return new PreliminaryProfileRun(
                profile,
                observation,
                derivation,
                reconstruction,
                reconstruction.Candidates.Select(item => item.CandidateId)
                    .OrderBy(item => item, StringComparer.Ordinal)
                    .ToArray());
        }).ToArray();

        ValidateTruthWorld(world);
        var unknown = preliminary.Single(item => item.Profile.Id == "unknown");
        var truthStateId = generatedStateIds[world.TruthCandidateKey];
        var truthCandidates = unknown.Reconstruction.Candidates.Where(item =>
            string.Equals(item.StateId, truthStateId, StringComparison.Ordinal) &&
            string.Equals(item.RegionId, world.TruthRegionId, StringComparison.Ordinal)).ToArray();
        if (truthCandidates.Length != 1)
        {
            throw new InvalidDataException(
                $"Truth candidate for seed {world.Seed} must resolve after public reconstruction.");
        }

        var truthCandidateId = truthCandidates[0].CandidateId;
        var runs = preliminary.Select(item =>
        {
            var containsTruth = item.CandidateIds.Contains(truthCandidateId, StringComparer.Ordinal);
            var reduction = unknown.Reconstruction.Candidates.Count - item.Reconstruction.Candidates.Count;
            return new ParticipantAmbiguityPairedProfileRun
            {
                ProfileId = item.Profile.Id,
                InformationOrder = item.Profile.InformationOrder,
                EncounterObservationPresent = item.Observation.EncounterObservationPresent,
                SourceObservationId = item.Observation.ObservationId,
                EncounterSourceGroupId = item.Derivation.Audit.EncounterSourceGroupId,
                IdentityKnowledge = item.Observation.OtherParticipant.IdentityKnowledge,
                PossibleParticipantIds = item.Observation.OtherParticipant.PossibleParticipantIds,
                ParticipantAlternativeCount =
                    item.Observation.OtherParticipant.PossibleParticipantIds.Count,
                DerivationSuccess = item.Derivation.Audit.Success,
                FailureReason = item.Derivation.Audit.FailureReason,
                RuleId = item.Derivation.Audit.RuleId,
                UsageRole = item.Derivation.Audit.UsageRole,
                ConstraintGenerated = item.Derivation.Constraint is not null,
                ConstraintId = item.Derivation.Constraint?.ConstraintId,
                DerivedAllowedRegionIds = item.Derivation.Constraint?.AllowedRegionIds ?? [],
                DerivedRegionCount = item.Derivation.Constraint?.AllowedRegionIds.Count ?? 0,
                PublicInputIdsUsed = item.Derivation.Audit.PublicInputIdsUsed,
                DerivedConstraintAncestryIds = item.Derivation.Constraint?.SourceAncestryIds ?? [],
                ParticipantEvidenceSourceGroupIds =
                    item.Derivation.Audit.ParticipantEvidenceSourceGroupIds,
                EncounterSourceGroupCount = 1,
                ParticipantEvidenceSourceGroupCount =
                    item.Derivation.Audit.ParticipantEvidenceSourceGroupIds.Count,
                GeneratedConstraintSourceGroupCount = item.Derivation.Constraint is null ? 0 : 1,
                ParticipantAlternativesEvaluated =
                    item.Derivation.Audit.ParticipantAlternativesEvaluated,
                ParticipantLocationEvidenceRecordsInspected =
                    item.Derivation.Audit.ParticipantLocationEvidenceRecordsInspected,
                CandidateIds = item.CandidateIds,
                CandidateRegionIds = item.Reconstruction.Candidates
                    .Select(candidate => candidate.RegionId)
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(region => region, StringComparer.Ordinal)
                    .ToArray(),
                CandidateCount = item.Reconstruction.Candidates.Count,
                ContainsTruth = containsTruth,
                FalseExclusion = !containsTruth,
                CandidateReductionCountVsUnknown = reduction,
                CandidateReductionFractionVsUnknown = unknown.Reconstruction.Candidates.Count == 0
                    ? 0m
                    : reduction / (decimal)unknown.Reconstruction.Candidates.Count,
                CandidateGenerationWork = item.Reconstruction.CandidateGenerationWork,
                ReconstructionEvidenceRecordsInspected =
                    item.Reconstruction.EvidenceRecordsInspected,
                ReconstructionSourceGroupsInspected = item.Reconstruction.SourceGroupsInspected
            };
        }).ToArray();
        var monotonicity = runs.Zip(runs.Skip(1), (precise, coarse) => AuditTransition(
            world.Seed,
            precise,
            coarse)).ToArray();
        var candidateRuns = world.CandidateStates.Select(item =>
        {
            var stateId = generatedStateIds[item.CandidateKey];
            var candidateId = unknown.Reconstruction.Candidates.Single(candidate =>
                string.Equals(candidate.StateId, stateId, StringComparison.Ordinal) &&
                string.Equals(candidate.RegionId, item.RegionId, StringComparison.Ordinal)).CandidateId;
            return new ParticipantAmbiguityPairedCandidateRun
            {
                CandidateKey = item.CandidateKey,
                CandidateId = candidateId,
                StateId = stateId,
                Tick = item.Tick,
                RegionId = item.RegionId,
                PublicSourceIdentifier = item.PublicSourceIdentifier,
                IsTruthCandidate = string.Equals(
                    item.CandidateKey,
                    world.TruthCandidateKey,
                    StringComparison.Ordinal)
            };
        }).OrderBy(item => item.CandidateKey, StringComparer.Ordinal).ToArray();
        var evidenceRuns = world.Participants.Select(generated =>
        {
            var evidence = participantEvidence.Single(item => string.Equals(
                item.ParticipantId,
                generated.ParticipantId,
                StringComparison.Ordinal));
            return new ParticipantAmbiguityPairedEvidenceRun
            {
                ParticipantId = generated.ParticipantId,
                PublicInputId = evidence.PublicInputId,
                SourceEvidenceIdentifier = evidence.SourceEvidenceIdentifier,
                SourceGroupId = evidence.SourceGroupId,
                SourceAncestryIds = evidence.SourceAncestryIds,
                AllowedRegionIds = evidence.AllowedRegionIds,
                IsTruthParticipant = string.Equals(
                    generated.ParticipantId,
                    world.TruthParticipantId,
                    StringComparison.Ordinal)
            };
        }).OrderBy(item => item.ParticipantId, StringComparer.Ordinal).ToArray();

        return new ParticipantAmbiguityPairedWorldRun
        {
            Seed = world.Seed,
            GeneratorId = world.GeneratorId,
            PublicWorldId = world.PublicWorldId,
            TruthWorldId = world.TruthWorldId,
            CandidateCount = world.CandidateCount,
            RegionUniverseSize = world.RegionUniverseSize,
            RegionUniverse = world.RegionUniverse,
            EncounterTick = world.EncounterTick,
            AvailableAtTick = world.AvailableAtTick,
            ParticipantPoolSize = world.ParticipantPoolSize,
            EvidenceOverlapKind = world.EvidenceOverlapKind,
            TruthParticipantId = world.TruthParticipantId,
            TruthCandidateKey = world.TruthCandidateKey,
            TruthCandidateId = truthCandidateId,
            TruthRegionId = world.TruthRegionId,
            CandidateStates = candidateRuns,
            ParticipantEvidence = evidenceRuns,
            Profiles = runs,
            MonotonicityAudits = monotonicity,
            TruthEvaluatedAfterReconstruction = true,
            WorldValid = true
        };
    }

    private static ParticipantAmbiguityPairedMonotonicityAudit AuditTransition(
        int seed,
        ParticipantAmbiguityPairedProfileRun precise,
        ParticipantAmbiguityPairedProfileRun coarse)
    {
        var coarseUnknown = coarse.IdentityKnowledge == ParticipantIdentityKnowledge.UnknownIdentity;
        var participantSubset = coarseUnknown || precise.PossibleParticipantIds.All(item =>
            coarse.PossibleParticipantIds.Contains(item, StringComparer.Ordinal));
        var participantStrict = coarseUnknown ||
            coarse.PossibleParticipantIds.Count > precise.PossibleParticipantIds.Count;
        var regionSubset = coarseUnknown || precise.DerivedAllowedRegionIds.All(item =>
            coarse.DerivedAllowedRegionIds.Contains(item, StringComparer.Ordinal));
        var regionsEqual = !coarseUnknown &&
            precise.DerivedAllowedRegionIds.SequenceEqual(coarse.DerivedAllowedRegionIds);
        var regionStrict = !coarseUnknown && regionSubset && !regionsEqual;
        var candidateSubset = precise.CandidateIds.All(item =>
            coarse.CandidateIds.Contains(item, StringComparer.Ordinal));
        var candidatesEqual = precise.CandidateIds.SequenceEqual(coarse.CandidateIds);
        var classification = coarseUnknown
            ? ParticipantPropagationClassification.UnknownBaselineTransition
            : regionsEqual
                ? ParticipantPropagationClassification.ParticipantToRegionPlateau
                : candidatesEqual
                    ? ParticipantPropagationClassification.RegionToCandidatePlateau
                    : ParticipantPropagationClassification.CandidateExpansion;

        return new ParticipantAmbiguityPairedMonotonicityAudit
        {
            Seed = seed,
            MorePreciseProfileId = precise.ProfileId,
            CoarserProfileId = coarse.ProfileId,
            MorePreciseParticipantIds = precise.PossibleParticipantIds,
            CoarserParticipantIds = coarse.PossibleParticipantIds,
            ParticipantSetIsSubset = participantSubset,
            ParticipantSetStrictlyWidened = participantStrict,
            MorePreciseRegionIds = precise.DerivedAllowedRegionIds,
            CoarserRegionIds = coarse.DerivedAllowedRegionIds,
            DerivedRegionSetIsSubset = regionSubset,
            DerivedRegionSetsEqual = regionsEqual,
            DerivedRegionSetStrictlyWidened = regionStrict,
            MorePreciseCandidateIds = precise.CandidateIds,
            CoarserCandidateIds = coarse.CandidateIds,
            CandidateSetIsSubset = candidateSubset,
            CandidateSetsEqual = candidatesEqual,
            Classification = classification
        };
    }

    private static IReadOnlyList<string> ParticipantIds(
        GeneratedParticipantAmbiguityWorld world,
        string profileId) => profileId switch
        {
            "exact" => world.ExactParticipantIds,
            "narrow" => world.NarrowParticipantIds,
            "broad" => world.BroadParticipantIds,
            "unknown" => [],
            _ => throw new InvalidDataException($"Unknown participant profile '{profileId}'.")
        };

    private static void ValidatePublicWorld(
        ParticipantAmbiguityPairedExperimentDefinition experiment,
        GeneratedParticipantAmbiguityWorld world)
    {
        var generator = experiment.Generator;
        if (!string.Equals(world.GeneratorId, generator.Id, StringComparison.Ordinal) ||
            world.Seed < experiment.RequiredSeedStart || world.Seed > experiment.RequiredSeedEnd ||
            world.CandidateCount < generator.CandidateCountMinimum ||
            world.CandidateCount > generator.CandidateCountMaximum ||
            world.CandidateStates.Count != world.CandidateCount ||
            world.RegionUniverseSize < generator.RegionUniverseSizeMinimum ||
            world.RegionUniverseSize > generator.RegionUniverseSizeMaximum ||
            world.RegionUniverse.Count != world.RegionUniverseSize ||
            world.ParticipantPoolSize < generator.ParticipantPoolSizeMinimum ||
            world.ParticipantPoolSize > generator.ParticipantPoolSizeMaximum ||
            world.Participants.Count != world.ParticipantPoolSize ||
            world.EncounterTick < generator.EncounterTickMinimum ||
            world.EncounterTick > generator.EncounterTickMaximum ||
            world.AvailableAtTick != world.EncounterTick + generator.AvailabilityLagTicks ||
            world.EvidenceAvailabilityCutoff != world.AvailableAtTick)
        {
            throw new InvalidDataException(
                $"Generated public participant world for seed {world.Seed} is outside registered ranges.");
        }

        if (world.RegionUniverse.Distinct(StringComparer.Ordinal).Count() != world.RegionUniverseSize ||
            world.CandidateStates.Select(item => item.CandidateKey)
                .Distinct(StringComparer.Ordinal).Count() != world.CandidateCount ||
            world.CandidateStates.Select(item => item.PublicSourceIdentifier)
                .Distinct(StringComparer.Ordinal).Count() != world.CandidateCount ||
            world.CandidateStates.Any(item =>
                item.Tick != world.EncounterTick ||
                !world.RegionUniverse.Contains(item.RegionId, StringComparer.Ordinal)))
        {
            throw new InvalidDataException(
                $"Generated candidate representation for seed {world.Seed} is invalid.");
        }

        if (world.Participants.Select(item => item.ParticipantId)
                .Distinct(StringComparer.Ordinal).Count() != world.ParticipantPoolSize ||
            world.Participants.Select(item => item.SourceEvidenceIdentifier)
                .Distinct(StringComparer.Ordinal).Count() != world.ParticipantPoolSize ||
            world.Participants.Any(item =>
                item.AllowedRegionIds.Count < generator.ParticipantRegionSetSizeMinimum ||
                item.AllowedRegionIds.Count > generator.ParticipantRegionSetSizeMaximum ||
                item.AllowedRegionIds.Distinct(StringComparer.Ordinal).Count() !=
                    item.AllowedRegionIds.Count ||
                item.AllowedRegionIds.Any(region =>
                    !world.RegionUniverse.Contains(region, StringComparer.Ordinal)) ||
                item.SourceAncestryIds.Count == 0))
        {
            throw new InvalidDataException(
                $"Generated participant evidence for seed {world.Seed} is invalid.");
        }

        if (world.ExactParticipantIds.Count != 1 ||
            world.NarrowParticipantIds.Count != 2 ||
            world.BroadParticipantIds.Count != world.ParticipantPoolSize ||
            !world.ExactParticipantIds.All(item =>
                world.NarrowParticipantIds.Contains(item, StringComparer.Ordinal)) ||
            !world.NarrowParticipantIds.All(item =>
                world.BroadParticipantIds.Contains(item, StringComparer.Ordinal)) ||
            !world.BroadParticipantIds.OrderBy(item => item, StringComparer.Ordinal).SequenceEqual(
                world.Participants.Select(item => item.ParticipantId)
                    .OrderBy(item => item, StringComparer.Ordinal)))
        {
            throw new InvalidDataException(
                $"Generated participant profiles for seed {world.Seed} are not strictly nested.");
        }
    }

    private static void ValidateTruthWorld(GeneratedParticipantAmbiguityWorld world)
    {
        if (world.CandidateStates.Count(item => item.IsTruthCandidate) != 1 ||
            world.Participants.Count(item => item.IsTruthParticipant) != 1)
        {
            throw new InvalidDataException($"Generated truth cardinality for seed {world.Seed} is invalid.");
        }

        var truthCandidate = world.CandidateStates.Single(item => item.IsTruthCandidate);
        var truthParticipant = world.Participants.Single(item => item.IsTruthParticipant);
        if (!string.Equals(
                truthCandidate.CandidateKey,
                world.TruthCandidateKey,
                StringComparison.Ordinal) ||
            !string.Equals(truthCandidate.RegionId, world.TruthRegionId, StringComparison.Ordinal) ||
            !string.Equals(
                truthParticipant.ParticipantId,
                world.TruthParticipantId,
                StringComparison.Ordinal) ||
            !world.ExactParticipantIds.SequenceEqual([world.TruthParticipantId]) ||
            !truthParticipant.AllowedRegionIds.Contains(world.TruthRegionId, StringComparer.Ordinal))
        {
            throw new InvalidDataException(
                $"Generated truth for seed {world.Seed} is not compatible with public evidence.");
        }
    }

    private sealed record PreliminaryProfileRun(
        ParticipantAmbiguityPairedProfileDefinition Profile,
        PublicEncounterParticipantObservation Observation,
        ParticipantConstraintDerivationResult Derivation,
        ObservationDerivedHistoricalCandidateResult Reconstruction,
        IReadOnlyList<string> CandidateIds);
}

public sealed class ParticipantAmbiguityPairedEvaluationRunner
{
    public ParticipantAmbiguityPairedEvaluationResult Run(
        ParticipantAmbiguityPairedEvaluationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ParticipantAmbiguityPairedExperimentValidator.Validate(request.Experiment, request.Seeds);
        var experiment = request.Experiment;
        var seeds = request.Seeds.Distinct().OrderBy(item => item).ToArray();
        var generator = new ParticipantAmbiguitySeededWorldGenerator();
        var evaluator = new ParticipantAmbiguityPairedWorldEvaluator();
        var runs = seeds.Select(seed => evaluator.Evaluate(
                experiment,
                generator.Generate(experiment, seed)))
            .OrderBy(item => item.Seed)
            .ToArray();
        var aggregates = experiment.Profiles.OrderBy(item => item.InformationOrder).Select(profile =>
        {
            var values = runs.Select(item => Profile(item, profile.Id)).ToArray();
            var candidateCounts = values.Select(item => item.CandidateCount).OrderBy(item => item).ToArray();
            var regionCounts = values.Select(item => item.DerivedRegionCount).OrderBy(item => item).ToArray();
            return new ParticipantAmbiguityPairedProfileAggregate
            {
                ProfileId = profile.Id,
                InformationOrder = profile.InformationOrder,
                SeedCount = values.Length,
                MeanCandidateCount = Mean(candidateCounts),
                MedianCandidateCount = Median(candidateCounts),
                MinimumCandidateCount = candidateCounts.Min(),
                MaximumCandidateCount = candidateCounts.Max(),
                MeanDerivedRegionCount = Mean(regionCounts),
                MedianDerivedRegionCount = Median(regionCounts),
                ContainmentRate = values.Count(item => item.ContainsTruth) / (decimal)values.Length,
                FalseExclusionCount = values.Count(item => item.FalseExclusion),
                MeanCandidateReductionCountVsUnknown = Mean(
                    values.Select(item => item.CandidateReductionCountVsUnknown)),
                MeanCandidateReductionFractionVsUnknown = Mean(
                    values.Select(item => item.CandidateReductionFractionVsUnknown)),
                MeanParticipantAlternativeCount = Mean(
                    values.Select(item => item.ParticipantAlternativeCount)),
                MeanParticipantAlternativesEvaluated = Mean(
                    values.Select(item => item.ParticipantAlternativesEvaluated)),
                DerivationSuccessCount = values.Count(item => item.DerivationSuccess),
                DerivationFailureCount = values.Count(item => !item.DerivationSuccess),
                MeanCandidateGenerationWork = Mean(
                    values.Select(item => item.CandidateGenerationWork)),
                MeanParticipantLocationEvidenceRecordsInspected = Mean(
                    values.Select(item => item.ParticipantLocationEvidenceRecordsInspected)),
                MeanEncounterSourceGroupCount = Mean(
                    values.Select(item => item.EncounterSourceGroupCount)),
                MeanParticipantEvidenceSourceGroupCount = Mean(
                    values.Select(item => item.ParticipantEvidenceSourceGroupCount)),
                MeanGeneratedConstraintSourceGroupCount = Mean(
                    values.Select(item => item.GeneratedConstraintSourceGroupCount))
            };
        }).ToArray();
        var allTransitions = runs.SelectMany(item => item.MonotonicityAudits).ToArray();
        var finiteTransitions = allTransitions.Where(item =>
            item.Classification != ParticipantPropagationClassification.UnknownBaselineTransition).ToArray();
        var participantRegionPlateauSeeds = finiteTransitions.Where(item =>
                item.Classification == ParticipantPropagationClassification.ParticipantToRegionPlateau)
            .Select(item => item.Seed).Distinct().OrderBy(item => item).ToArray();
        var regionCandidatePlateauSeeds = finiteTransitions.Where(item =>
                item.Classification == ParticipantPropagationClassification.RegionToCandidatePlateau)
            .Select(item => item.Seed).Distinct().OrderBy(item => item).ToArray();
        var finiteEqualSeeds = runs.Where(item =>
                SameCandidates(Profile(item, "exact"), Profile(item, "narrow")) &&
                SameCandidates(Profile(item, "narrow"), Profile(item, "broad")))
            .Select(item => item.Seed).ToArray();
        var broadUnknownSeeds = runs.Where(item =>
                SameCandidates(Profile(item, "broad"), Profile(item, "unknown")))
            .Select(item => item.Seed).ToArray();
        var sourceViolations = runs.Count(SourceIndependenceViolation);
        var unknownAbsenceViolations = runs.Count(item =>
        {
            var unknown = Profile(item, "unknown");
            return !unknown.EncounterObservationPresent ||
                unknown.ConstraintGenerated ||
                unknown.FailureReason !=
                    ParticipantConstraintDerivationFailureReason.UnknownParticipantIdentity;
        });
        var summary = new ParticipantAmbiguityPairedSummary
        {
            WorldCount = runs.Length,
            PairedProfileRunCount = runs.Sum(item => item.Profiles.Count),
            FiniteTransitionCount = finiteTransitions.Length,
            DerivedRegionMonotonicityViolationCount = allTransitions.Count(item =>
                !item.DerivedRegionSetIsSubset),
            CandidateSetMonotonicityViolationCount = allTransitions.Count(item =>
                !item.CandidateSetIsSubset),
            FiniteDerivationFailureCount = runs.SelectMany(item => item.Profiles)
                .Count(item => item.IdentityKnowledge != ParticipantIdentityKnowledge.UnknownIdentity &&
                    !item.DerivationSuccess),
            FalseExclusionCount = runs.SelectMany(item => item.Profiles)
                .Count(item => item.FalseExclusion),
            SourceIndependenceViolationCount = sourceViolations,
            UnknownEncounterAbsenceViolationCount = unknownAbsenceViolations,
            ExactLessThanNarrowSeedCount = runs.Count(item =>
                Profile(item, "exact").CandidateCount < Profile(item, "narrow").CandidateCount),
            NarrowLessThanBroadSeedCount = runs.Count(item =>
                Profile(item, "narrow").CandidateCount < Profile(item, "broad").CandidateCount),
            BroadLessThanUnknownSeedCount = runs.Count(item =>
                Profile(item, "broad").CandidateCount < Profile(item, "unknown").CandidateCount),
            FiniteCandidateSetsAllEqualSeedCount = finiteEqualSeeds.Length,
            ParticipantWidenedRegionUnchangedSeedCount = participantRegionPlateauSeeds.Length,
            ParticipantWidenedRegionUnchangedTransitionCount = finiteTransitions.Count(item =>
                item.Classification ==
                    ParticipantPropagationClassification.ParticipantToRegionPlateau),
            RegionWidenedCandidateUnchangedSeedCount = regionCandidatePlateauSeeds.Length,
            RegionWidenedCandidateUnchangedTransitionCount = finiteTransitions.Count(item =>
                item.Classification ==
                    ParticipantPropagationClassification.RegionToCandidatePlateau),
            CandidateExpansionTransitionCount = finiteTransitions.Count(item =>
                item.Classification == ParticipantPropagationClassification.CandidateExpansion),
            BroadUnknownSaturationSeedCount = broadUnknownSeeds.Length,
            SkippedSeedCount = 0,
            RegeneratedSeedCount = 0,
            FiniteCandidateSetsAllEqualSeeds = finiteEqualSeeds,
            ParticipantWidenedRegionUnchangedSeeds = participantRegionPlateauSeeds,
            RegionWidenedCandidateUnchangedSeeds = regionCandidatePlateauSeeds,
            BroadUnknownSaturationSeeds = broadUnknownSeeds,
            OverlapFrequencies = Enum.GetValues<ParticipantEvidenceOverlapKind>()
                .Select(kind => new ParticipantOverlapFrequency
                {
                    Kind = kind,
                    WorldCount = runs.Count(item => item.EvidenceOverlapKind == kind)
                }).ToArray(),
            PropagationFrequencies = Enum.GetValues<ParticipantPropagationClassification>()
                .Where(item => item !=
                    ParticipantPropagationClassification.UnknownBaselineTransition)
                .Select(classification => new ParticipantPropagationFrequency
                {
                    Classification = classification,
                    TransitionCount = finiteTransitions.Count(item =>
                        item.Classification == classification)
                }).ToArray()
        };
        if (summary.DerivedRegionMonotonicityViolationCount > 0 ||
            summary.CandidateSetMonotonicityViolationCount > 0 ||
            summary.FiniteDerivationFailureCount > 0 ||
            summary.FalseExclusionCount > 0 ||
            summary.SourceIndependenceViolationCount > 0 ||
            summary.UnknownEncounterAbsenceViolationCount > 0 ||
            summary.FiniteTransitionCount != runs.Length * 2 ||
            summary.PropagationFrequencies.Sum(item => item.TransitionCount) !=
                summary.FiniteTransitionCount)
        {
            throw new InvalidOperationException(
                "A preregistered participant monotonicity, safety, or audit failure occurred; " +
                "aggregate interpretation is blocked.");
        }

        return new ParticipantAmbiguityPairedEvaluationResult
        {
            Manifest = new ParticipantAmbiguityPairedManifest
            {
                ExperimentId = experiment.Id,
                ScenarioId = experiment.ScenarioId,
                GeneratorId = experiment.Generator.Id,
                RuleId = EncounterParticipantLocationConstraintDeriver.RuleId,
                ExperimentContentSha256 = request.ExperimentContentSha256,
                SourceCommitSha = request.SourceCommitSha,
                DirtyWorkingTree = request.DirtyWorkingTree,
                Seeds = seeds,
                ProfileIds = experiment.Profiles.OrderBy(item => item.InformationOrder)
                    .Select(item => item.Id).ToArray(),
                WorldCount = runs.Length,
                PairedProfileRunCount = summary.PairedProfileRunCount,
                TruthUsedForDerivation = false,
                OutputFiles = ParticipantAmbiguityPairedOutputNames.All
            },
            ConservativeUnionFormula =
                "AllowedRegions(target) = union(AllowedRegions(p) for p in PossibleParticipants)",
            Generator = experiment.Generator,
            Profiles = experiment.Profiles.OrderBy(item => item.InformationOrder).ToArray(),
            Runs = runs,
            Aggregates = aggregates,
            Summary = summary,
            OriginalF10FixtureRetained = true,
            F8F9RegressionsRetained = true,
            ExistingS5Changed = false,
            InterpretationBoundary =
                "Results apply only to the preregistered synthetic participant-world family under " +
                "the unchanged encounter-participant-location-union-v1 public same-time rule."
        };
    }

    private static ParticipantAmbiguityPairedProfileRun Profile(
        ParticipantAmbiguityPairedWorldRun run,
        string profileId) => run.Profiles.Single(item => item.ProfileId == profileId);

    private static bool SameCandidates(
        ParticipantAmbiguityPairedProfileRun left,
        ParticipantAmbiguityPairedProfileRun right) =>
        left.CandidateIds.SequenceEqual(right.CandidateIds);

    private static bool SourceIndependenceViolation(ParticipantAmbiguityPairedWorldRun run)
    {
        var encounterGroups = run.Profiles.Select(item => item.EncounterSourceGroupId)
            .Distinct(StringComparer.Ordinal).Count();
        var finite = run.Profiles.Where(item =>
            item.IdentityKnowledge != ParticipantIdentityKnowledge.UnknownIdentity).ToArray();
        var unknown = run.Profiles.Single(item =>
            item.IdentityKnowledge == ParticipantIdentityKnowledge.UnknownIdentity);
        return encounterGroups != 1 ||
            finite.Any(item =>
                item.EncounterSourceGroupCount != 1 ||
                item.GeneratedConstraintSourceGroupCount != 1 ||
                item.ParticipantEvidenceSourceGroupCount != item.ParticipantAlternativeCount) ||
            unknown.EncounterSourceGroupCount != 1 ||
            unknown.GeneratedConstraintSourceGroupCount != 0 ||
            unknown.ParticipantEvidenceSourceGroupCount != 0;
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
