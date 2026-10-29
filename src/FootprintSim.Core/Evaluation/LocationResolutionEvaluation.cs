using System.Security.Cryptography;
using System.Text;
using FootprintSim.Evidence;
using FootprintSim.Events;
using FootprintSim.Evaluation.Truth;
using FootprintSim.Hazards;
using FootprintSim.Inference.PastStates;
using FootprintSim.Interactions;
using FootprintSim.Interactions.CandidateWarps;
using FootprintSim.Interactions.Tracing;
using FootprintSim.Scenarios;
using FootprintSim.Serialization;
using FootprintSim.Simulation;

namespace FootprintSim.Evaluation;

public sealed class LocationResolutionEvaluationRunner
{
    public const string InterpretationBoundary =
        "Results apply only to current S5, its Region/tick representation, later-trace-a, and current inference rules. Equal profile results may be representation-limited and do not establish a real-world distance requirement.";

    public LocationResolutionEvaluationResult Run(LocationResolutionEvaluationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var seeds = request.Seeds.Distinct().OrderBy(item => item).ToArray();
        Validate(request, seeds);
        var levels = request.Experiment.Levels.OrderBy(item => item.InformationOrder).ToArray();
        var definition = request.Scenario.PastStateReconstruction!;
        var sourceDefinitions = definition.Constraints
            .Where(item => string.Equals(
                item.SourceObservationKey,
                request.Experiment.SourceObservationKey,
                StringComparison.Ordinal))
            .OrderBy(item => item.Id, StringComparer.Ordinal)
            .ToArray();
        var publicTopology = PublicHazardEnvelopeBuilder.Create(request.Scenario.StochasticHazard!);
        var topologyIdentifier = Hash(JsonDefaults.Serialize(publicTopology));
        var engine = new SimulationEngine();
        var runs = new List<LocationResolutionRun>();

        foreach (var seed in seeds)
        {
            var simulation = engine.Run(request.Scenario, seed);
            var truthIdentifier = TruthWorldIdentifier(simulation);
            foreach (var level in levels)
            {
                runs.Add(EvaluateLevel(
                    request.Scenario,
                    request.Experiment,
                    level,
                    sourceDefinitions,
                    simulation,
                    publicTopology,
                    topologyIdentifier,
                    truthIdentifier));
            }
        }

        var orderedRuns = runs
            .OrderBy(item => item.InformationOrder)
            .ThenBy(item => item.Seed)
            .ToArray();
        var aggregates = Aggregate(levels, orderedRuns);
        var current = aggregates.Single(item => item.InformationOrder == 0);
        var deltas = aggregates.SelectMany(item => Deltas(item, current))
            .OrderBy(item => levels.Single(level => level.Id == item.LevelId).InformationOrder)
            .ThenBy(item => item.Method)
            .ToArray();
        var coarsestCandidateOrder = aggregates
            .Where(item => item.CandidateOutputEquivalentToCurrent)
            .Max(item => item.InformationOrder);
        var coarsestBaselineOrder = aggregates
            .Where(item => item.BaselineEquivalentToCurrent)
            .Max(item => item.InformationOrder);

        return new LocationResolutionEvaluationResult
        {
            Manifest = new LocationResolutionManifest
            {
                ExperimentId = request.Experiment.Id,
                ScenarioId = request.Scenario.Id,
                ExperimentContentSha256 = request.ExperimentContentSha256,
                ScenarioContentSha256 = request.ScenarioContentSha256,
                Seeds = seeds,
                LevelIds = levels.Select(item => item.Id).ToArray(),
                ProfileRunCount = orderedRuns.Length,
                MethodRunCount = orderedRuns.Sum(item => item.Methods.Count),
                SourceCommitSha = request.SourceCommitSha,
                DirtyWorkingTree = request.DirtyWorkingTree
            },
            Levels = levels,
            Runs = orderedRuns,
            Aggregates = aggregates,
            DeltasFromCurrent = deltas,
            CoarsestCandidateOutputEquivalentLevelIds = aggregates
                .Where(item => item.CandidateOutputEquivalentToCurrent &&
                    item.InformationOrder == coarsestCandidateOrder)
                .Select(item => item.LevelId).OrderBy(item => item, StringComparer.Ordinal).ToArray(),
            CoarsestBaselineEquivalentLevelIds = aggregates
                .Where(item => item.BaselineEquivalentToCurrent &&
                    item.InformationOrder == coarsestBaselineOrder)
                .Select(item => item.LevelId).OrderBy(item => item, StringComparer.Ordinal).ToArray(),
            InterpretationBoundary = InterpretationBoundary
        };
    }

    private static LocationResolutionRun EvaluateLevel(
        ScenarioDefinition scenario,
        LocationResolutionExperimentDefinition experiment,
        LocationResolutionLevelDefinition level,
        IReadOnlyList<CausalTracingConstraintDefinition> sourceDefinitions,
        SimulationRun simulation,
        PublicHazardEnvelope publicTopology,
        string topologyIdentifier,
        string truthIdentifier)
    {
        var definition = scenario.PastStateReconstruction!;
        var condition = simulation.PastStateEvidenceCondition ??
            throw new InvalidOperationException("S5 simulation did not select an evidence condition.");
        var allConstraints = simulation.CausalTracingConstraints ??
            throw new InvalidOperationException("S5 simulation produced no public constraint set.");
        var directSourceIds = sourceDefinitions.Select(item => item.DirectSourceId)
            .ToHashSet(StringComparer.Ordinal);
        var originalConstraints = allConstraints.Constraints
            .Where(item => directSourceIds.Contains(item.DirectSourceId))
            .OrderBy(item => item.ConstraintId, StringComparer.Ordinal)
            .ToArray();
        var originalObservationIds = originalConstraints.Select(item => item.SourceObservationId)
            .Distinct(StringComparer.Ordinal).ToArray();
        if (originalObservationIds.Length != 1)
        {
            throw new InvalidOperationException("later-trace-a must resolve to one source observation per seed.");
        }

        var originalObservation = (simulation.InteractionObservations?.Observations ?? []).Single(item =>
            string.Equals(item.ObservationId, originalObservationIds[0], StringComparison.Ordinal));
        var projection = ProjectSource(
            scenario,
            level,
            sourceDefinitions,
            originalObservation,
            originalConstraints);
        var constraints = allConstraints.Constraints
            .Where(item => !directSourceIds.Contains(item.DirectSourceId))
            .Concat(projection.Constraints)
            .OrderBy(item => item.ConstraintId, StringComparer.Ordinal)
            .ToArray();
        var constraintSet = new CausalTracingConstraintSet(scenario.Id, simulation.Seed, constraints);
        var interactionObservations = (simulation.InteractionObservations?.Observations ?? [])
            .Where(item => !string.Equals(item.ObservationId, originalObservation.ObservationId, StringComparison.Ordinal))
            .Append(projection.Observation)
            .OrderBy(item => item.AvailableAtTick)
            .ThenBy(item => item.ObservedTimeWindow.StartTick)
            .ThenBy(item => item.ObservationId, StringComparer.Ordinal)
            .ToArray();
        var availableObservationIds = simulation.ObservationSet.Observations
            .Where(item => item.AvailableTick <= definition.EvidenceAvailabilityCutoff)
            .Select(item => item.ObservationId)
            .Concat(interactionObservations
                .Where(item => item.Visibility == InteractionVisibility.PublicEvidence)
                .Where(item => item.AvailableAtTick <= definition.EvidenceAvailabilityCutoff)
                .Select(item => item.ObservationId))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(item => item, StringComparer.Ordinal)
            .ToArray();
        var query = PastStateQuery.Create(
            scenario.Id,
            simulation.Seed,
            scenario.TargetPersonId,
            new InteractionTimeWindow(definition.TargetStartTick, definition.TargetEndTick),
            definition.QueryContextId,
            definition.EvidenceAvailabilityCutoff);
        var generationIds = definition.GenerationConstraintDefinitionIds.ToHashSet(StringComparer.Ordinal);
        var verificationIds = definition.VerificationConstraintDefinitionIds.ToHashSet(StringComparer.Ordinal);
        var traversalIds = definition.TraversalConstraintDefinitionIds.ToHashSet(StringComparer.Ordinal);
        var comparison = new PastStateReconstructor().Compare(
            new PastStatePublicContext
            {
                Query = query,
                PublicCandidateStates = PublicCandidateStates(definition),
                GenerationDirectSourceIds = DirectSources(definition, generationIds),
                VerificationDirectSourceIds = DirectSources(definition, verificationIds),
                TraversalDirectSourceIds = DirectSources(definition, traversalIds),
                AvailableObservationIds = availableObservationIds,
                LocalTraversalWindowPaddingTicks = definition.LocalTraversalWindowPaddingTicks,
                PublicHazardEnvelope = publicTopology
            },
            simulation.ObservationSet,
            constraintSet);
        var truthPersonRegion = HistoricalRegion(
            scenario,
            simulation.GroundTruthEvents,
            scenario.TargetPersonId,
            definition.TargetEndTick);
        var deviceId = scenario.Entities.Devices.Select(item => item.Id)
            .OrderBy(item => item, StringComparer.Ordinal).FirstOrDefault();
        var truthDeviceRegion = deviceId is null
            ? null
            : HistoricalRegion(scenario, simulation.GroundTruthEvents, deviceId, definition.TargetEndTick);
        var activeDefinitions = definition.Constraints
            .Where(item => item.AppliesToPastStateConditions.Contains(condition))
            .Where(item => generationIds.Contains(item.Id) ||
                verificationIds.Contains(item.Id) || traversalIds.Contains(item.Id))
            .ToArray();
        var availableIds = availableObservationIds.ToHashSet(StringComparer.Ordinal);
        var runtimeSourceByDirectSource = constraints
            .GroupBy(item => item.DirectSourceId, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.Select(item => item.SourceObservationId).First(),
                StringComparer.Ordinal);
        var unavailableEvidenceCount = activeDefinitions.Count(item =>
            item.Visibility != InteractionVisibility.PublicEvidence ||
            item.AvailableAtTick > definition.EvidenceAvailabilityCutoff ||
            !runtimeSourceByDirectSource.TryGetValue(item.DirectSourceId, out var sourceObservationId) ||
            !availableIds.Contains(sourceObservationId));
        var evaluated = PastStateReconstructionEvaluationBuilder.Evaluate(
            comparison,
            new PastStateTruthEvaluation
            {
                PersonRegionId = truthPersonRegion,
                DeviceRegionId = truthDeviceRegion,
                OutcomeIdentifier = $"person={truthPersonRegion ?? "unavailable"};device={truthDeviceRegion ?? "unavailable"}"
            },
            condition,
            unavailableEvidenceCount);
        var verificationByCandidate = comparison.Reconstruction.Verification
            .ToDictionary(item => item.CandidateId, StringComparer.Ordinal);
        var candidateRegionsBefore = definition.PublicCandidateStates
            .Where(item => item.StartTick <= definition.TargetEndTick &&
                item.EndTick >= definition.TargetStartTick)
            .SelectMany(item => item.RegionIds)
            .Distinct(StringComparer.Ordinal).OrderBy(item => item, StringComparer.Ordinal).ToArray();

        return new LocationResolutionRun
        {
            ScenarioId = scenario.Id,
            Seed = simulation.Seed,
            EvidenceCondition = condition,
            LevelId = level.Id,
            InformationOrder = level.InformationOrder,
            ObservedRegionIds = projection.Observation.ObservedRegionIds,
            ObservedRegionCount = projection.Observation.ObservedRegionIds.Count,
            DerivedAllowedRegionIds = projection.Constraints.SelectMany(item => item.AllowedRegionIds)
                .Distinct(StringComparer.Ordinal).OrderBy(item => item, StringComparer.Ordinal).ToArray(),
            DerivedAllowedRegionCount = projection.Constraints.SelectMany(item => item.AllowedRegionIds)
                .Distinct(StringComparer.Ordinal).Count(),
            ConstraintGenerated = projection.Constraints.Count > 0,
            ConstraintDefinitionIds = level.GenerateConstraint
                ? sourceDefinitions.Select(item => item.Id).ToArray()
                : [],
            ConstraintIds = projection.Constraints.Select(item => item.ConstraintId)
                .OrderBy(item => item, StringComparer.Ordinal).ToArray(),
            SourceObservationId = projection.Observation.ObservationId,
            SourceObservationResolved = interactionObservations.Any(item => string.Equals(
                item.ObservationId,
                projection.Observation.ObservationId,
                StringComparison.Ordinal)),
            ObservedSubjectIds = projection.Observation.ObservedParticipants
                .Select(item => item.EntityId).OrderBy(item => item, StringComparer.Ordinal).ToArray(),
            ObservedTimeWindow = projection.Observation.ObservedTimeWindow,
            ObservedRelationKind = projection.Observation.ObservedRelationKind,
            AvailableAtTick = projection.Observation.AvailableAtTick,
            EvidenceState = projection.Observation.EvidenceBoundaryState,
            SourceAncestryIds = sourceDefinitions.SelectMany(item => item.SourceAncestryIds)
                .Distinct(StringComparer.Ordinal).OrderBy(item => item, StringComparer.Ordinal).ToArray(),
            TruthWorldIdentifier = truthIdentifier,
            PublicTopologyIdentifier = topologyIdentifier,
            TruthPersonRegionId = truthPersonRegion,
            PresentEvidenceSourceGroupCount = evaluated.PresentEvidenceSourceGroupCount,
            SameAncestryDerivedRecordCount = evaluated.RedundantSameSourceRecordCount,
            ConflictingEvidenceCount = evaluated.ConflictingEvidenceCount,
            UncertainEvidenceCount = evaluated.UncertainEvidenceCount,
            UnavailableEvidenceCount = unavailableEvidenceCount,
            UnresolvedCandidateCount = evaluated.UnresolvedCandidateCount,
            ConflictedCandidateCount = evaluated.ConflictedCandidateCount,
            IndependentlySupportedCandidateCount = evaluated.IndependentlySupportedCandidateCount,
            IndependentlyRefutedCandidateCount = evaluated.IndependentlyRefutedCandidateCount,
            ConflictingVerificationCandidateCount = evaluated.ConflictingVerificationCandidateCount,
            UnresolvedVerificationCandidateCount = evaluated.UnresolvedVerificationCandidateCount,
            SharedAncestryOnlyCandidateCount = evaluated.SharedAncestryOnlyCandidateCount,
            UnavailableVerificationCandidateCount = evaluated.UnavailableVerificationCandidateCount,
            CandidateEvaluations = comparison.Reconstruction.Candidates
                .OrderBy(item => item.RegionId, StringComparer.Ordinal)
                .Select(item => new ObservationAblationCandidateEvaluation
                {
                    CandidateId = item.CandidateId,
                    RegionId = item.RegionId,
                    CandidateDisposition = item.Disposition,
                    VerificationDisposition = verificationByCandidate[item.CandidateId].Disposition
                }).ToArray(),
            Methods = comparison.Methods.OrderBy(item => item.Method).Select(item =>
            {
                var evaluation = evaluated.Methods.Single(value => value.Method == item.Method);
                return new LaterTraceFieldMethodRun
                {
                    Method = item.Method,
                    CandidateRegionIdsBefore = candidateRegionsBefore,
                    CandidateRegionIdsAfter = item.CandidateRegionIds,
                    ContainsTruth = evaluation.ContainsTruth,
                    FalseExclusion = evaluation.FalseExclusion,
                    CandidateReduction = evaluation.CandidateReduction,
                    CandidateGenerationWork = item.CandidateGenerationSteps,
                    CandidateVerificationWork = item.CandidateVerificationSteps,
                    ForwardTraversalWork = item.ForwardTransitionSteps,
                    BackwardTraversalWork = item.BackwardTransitionSteps,
                    PublicRelationsInspected = item.PublicRelationsInspected,
                    EvidenceRecordsInspected = item.EvidenceRecordsInspected,
                    SourceGroupsInspected = item.SourceGroupsInspected,
                    TransitionIds = item.TransitionIds
                };
            }).ToArray()
        };
    }

    private static SourceProjection ProjectSource(
        ScenarioDefinition scenario,
        LocationResolutionLevelDefinition level,
        IReadOnlyList<CausalTracingConstraintDefinition> definitions,
        InteractionObservation originalObservation,
        IReadOnlyList<CausalTracingConstraint> originalConstraints)
    {
        var normalizedRegions = level.ObservedRegionIds.Distinct(StringComparer.Ordinal)
            .OrderBy(item => item, StringComparer.Ordinal).ToArray();
        var originalRegions = originalObservation.ObservedRegionIds
            .OrderBy(item => item, StringComparer.Ordinal).ToArray();
        if (level.GenerateConstraint && normalizedRegions.SequenceEqual(originalRegions, StringComparer.Ordinal))
        {
            return new SourceProjection(originalObservation, originalConstraints);
        }

        var observation = InteractionObservation.Create(
            scenario.Id,
            originalObservation.Seed,
            $"{originalObservation.SourceEvidenceIdentifier}:location-resolution:{level.Id}",
            originalObservation.ReferencedInteractionId,
            originalObservation.ObservedRelationKind,
            originalObservation.ObservedParticipants,
            originalObservation.ObservedTimeWindow,
            normalizedRegions,
            originalObservation.AvailableAtTick,
            originalObservation.EvidenceBoundaryState,
            originalObservation.IsPersonEvidence,
            originalObservation.IsDeviceOnlyEvidence,
            originalObservation.Visibility,
            originalObservation.Provenance.Where(item =>
                item.Kind != InteractionProvenanceKind.EvidenceRecord));
        if (!level.GenerateConstraint)
        {
            return new SourceProjection(observation, []);
        }

        var constraints = definitions.Select(item => CausalTracingConstraint.Create(
            scenario.Id,
            originalObservation.Seed,
            observation.ObservationId,
            item.DirectSourceId,
            item.SourceAncestryIds,
            scenario.TargetPersonId,
            item.SourceKind,
            item.RelationKind,
            originalConstraints[0].Participants,
            item.Role,
            observation.ObservedTimeWindow,
            new InteractionTimeWindow(item.ConstrainedStartTick, item.ConstrainedEndTick),
            observation.ObservedRegionIds,
            observation.AvailableAtTick,
            item.EvidenceState,
            item.IsPersonEvidence,
            item.IsDeviceOnlyEvidence,
            item.Visibility,
            item.DerivationKind,
            [
                new InteractionProvenance(InteractionProvenanceKind.DerivationRule, item.DerivationKind),
                new InteractionProvenance(InteractionProvenanceKind.ExternalSource, item.DirectSourceId)
            ])).OrderBy(item => item.ConstraintId, StringComparer.Ordinal).ToArray();
        return new SourceProjection(observation, constraints);
    }

    private static IReadOnlyList<LocationResolutionAggregate> Aggregate(
        IReadOnlyList<LocationResolutionLevelDefinition> levels,
        IReadOnlyList<LocationResolutionRun> runs)
    {
        var currentRuns = runs.Where(item => item.InformationOrder == 0).ToDictionary(item => item.Seed);
        var initial = levels.Select(level =>
        {
            var profileRuns = runs.Where(item => item.LevelId == level.Id).ToArray();
            return new LocationResolutionAggregate
            {
                LevelId = level.Id,
                InformationOrder = level.InformationOrder,
                ObservedRegionIds = level.ObservedRegionIds,
                ObservedRegionCount = level.ObservedRegionIds.Count,
                DerivedAllowedRegionIds = profileRuns.SelectMany(item => item.DerivedAllowedRegionIds)
                    .Distinct(StringComparer.Ordinal).OrderBy(item => item, StringComparer.Ordinal).ToArray(),
                DerivedAllowedRegionCount = profileRuns.SelectMany(item => item.DerivedAllowedRegionIds)
                    .Distinct(StringComparer.Ordinal).Count(),
                SeedCount = profileRuns.Length,
                ConstraintGeneratedCount = profileRuns.Count(item => item.ConstraintGenerated),
                CandidateOutputEquivalentToCurrent = profileRuns.All(item =>
                    CandidateOutputEquivalent(item, currentRuns[item.Seed])),
                BaselineEquivalentToCurrent = profileRuns.All(item =>
                    BaselineEquivalent(item, currentRuns[item.Seed])),
                MeanPresentEvidenceSourceGroupCount = Mean(profileRuns.Select(
                    item => item.PresentEvidenceSourceGroupCount)),
                MeanSameAncestryDerivedRecordCount = Mean(profileRuns.Select(
                    item => item.SameAncestryDerivedRecordCount)),
                MeanConflictingEvidenceCount = Mean(profileRuns.Select(item => item.ConflictingEvidenceCount)),
                MeanUncertainEvidenceCount = Mean(profileRuns.Select(item => item.UncertainEvidenceCount)),
                MeanUnavailableEvidenceCount = Mean(profileRuns.Select(item => item.UnavailableEvidenceCount)),
                MeanUnresolvedCandidateCount = Mean(profileRuns.Select(item => item.UnresolvedCandidateCount)),
                MeanConflictedCandidateCount = Mean(profileRuns.Select(item => item.ConflictedCandidateCount)),
                IndependentlySupportedCandidateCount = profileRuns.Sum(item =>
                    item.IndependentlySupportedCandidateCount),
                IndependentlyRefutedCandidateCount = profileRuns.Sum(item =>
                    item.IndependentlyRefutedCandidateCount),
                ConflictingVerificationCandidateCount = profileRuns.Sum(item =>
                    item.ConflictingVerificationCandidateCount),
                UnresolvedVerificationCandidateCount = profileRuns.Sum(item =>
                    item.UnresolvedVerificationCandidateCount),
                SharedAncestryOnlyCandidateCount = profileRuns.Sum(item =>
                    item.SharedAncestryOnlyCandidateCount),
                UnavailableVerificationCandidateCount = profileRuns.Sum(item =>
                    item.UnavailableVerificationCandidateCount),
                Methods = Enum.GetValues<PastStateMethod>()
                    .Select(method => AggregateMethod(profileRuns, method, 0m)).ToArray()
            };
        }).OrderBy(item => item.InformationOrder).ToArray();
        var unknown = initial.Single(item => item.LevelId == "unknown");
        return initial.Select(item => item with
        {
            Methods = item.Methods.Select(method => method with
            {
                MeanCandidateImprovementFromUnknown = unknown.Methods.Single(value =>
                    value.Method == method.Method).MeanCandidateCount - method.MeanCandidateCount
            }).ToArray()
        }).ToArray();
    }

    private static LocationResolutionMethodAggregate AggregateMethod(
        IReadOnlyList<LocationResolutionRun> runs,
        PastStateMethod method,
        decimal improvementFromUnknown)
    {
        var values = runs.Select(item => item.Methods.Single(value => value.Method == method)).ToArray();
        var containment = values.Where(item => item.ContainsTruth.HasValue).ToArray();
        return new LocationResolutionMethodAggregate
        {
            Method = method,
            MeanCandidateCount = Mean(values.Select(item => item.CandidateRegionIdsAfter.Count)),
            ContainmentRate = containment.Length == 0
                ? 0m
                : (decimal)containment.Count(item => item.ContainsTruth == true) / containment.Length,
            FalseExclusionCount = values.Count(item => item.FalseExclusion == true),
            MeanReduction = Mean(values.Select(item => item.CandidateReduction)),
            MeanCandidateGenerationWork = Mean(values.Select(item => item.CandidateGenerationWork)),
            MeanCandidateVerificationWork = Mean(values.Select(item => item.CandidateVerificationWork)),
            MeanForwardTraversalWork = Mean(values.Select(item => item.ForwardTraversalWork)),
            MeanBackwardTraversalWork = Mean(values.Select(item => item.BackwardTraversalWork)),
            MeanEvidenceRecordsInspected = Mean(values.Select(item => item.EvidenceRecordsInspected)),
            MeanPublicRelationsInspected = Mean(values.Select(item => item.PublicRelationsInspected)),
            MeanSourceGroupsInspected = Mean(values.Select(item => item.SourceGroupsInspected)),
            MeanCandidateImprovementFromUnknown = improvementFromUnknown
        };
    }

    private static IReadOnlyList<LocationResolutionMethodDelta> Deltas(
        LocationResolutionAggregate profile,
        LocationResolutionAggregate current) => profile.Methods.Select(item =>
    {
        var baseline = current.Methods.Single(value => value.Method == item.Method);
        return new LocationResolutionMethodDelta
        {
            LevelId = profile.LevelId,
            Method = item.Method,
            MeanCandidateCountDelta = item.MeanCandidateCount - baseline.MeanCandidateCount,
            ContainmentRateDelta = item.ContainmentRate - baseline.ContainmentRate,
            FalseExclusionCountDelta = item.FalseExclusionCount - baseline.FalseExclusionCount,
            MeanReductionDelta = item.MeanReduction - baseline.MeanReduction,
            CandidateGenerationWorkDelta = item.MeanCandidateGenerationWork - baseline.MeanCandidateGenerationWork,
            CandidateVerificationWorkDelta = item.MeanCandidateVerificationWork - baseline.MeanCandidateVerificationWork,
            ForwardTraversalWorkDelta = item.MeanForwardTraversalWork - baseline.MeanForwardTraversalWork,
            BackwardTraversalWorkDelta = item.MeanBackwardTraversalWork - baseline.MeanBackwardTraversalWork,
            EvidenceRecordsInspectedDelta = item.MeanEvidenceRecordsInspected - baseline.MeanEvidenceRecordsInspected,
            PublicRelationsInspectedDelta = item.MeanPublicRelationsInspected - baseline.MeanPublicRelationsInspected
        };
    }).ToArray();

    private static bool CandidateOutputEquivalent(LocationResolutionRun candidate, LocationResolutionRun current) =>
        candidate.Methods.OrderBy(item => item.Method).Select(item =>
            (item.Method, Regions: string.Join("|", item.CandidateRegionIdsAfter), item.ContainsTruth, item.FalseExclusion))
            .SequenceEqual(current.Methods.OrderBy(item => item.Method).Select(item =>
                (item.Method, Regions: string.Join("|", item.CandidateRegionIdsAfter), item.ContainsTruth, item.FalseExclusion))) &&
        candidate.CandidateEvaluations.Select(item =>
                (item.RegionId, item.CandidateDisposition, item.VerificationDisposition))
            .SequenceEqual(current.CandidateEvaluations.Select(item =>
                (item.RegionId, item.CandidateDisposition, item.VerificationDisposition)));

    private static bool BaselineEquivalent(LocationResolutionRun candidate, LocationResolutionRun current) =>
        CandidateOutputEquivalent(candidate, current) &&
        candidate.Methods.OrderBy(item => item.Method).Select(item => new
        {
            item.Method,
            item.CandidateGenerationWork,
            item.CandidateVerificationWork,
            item.ForwardTraversalWork,
            item.BackwardTraversalWork,
            item.EvidenceRecordsInspected,
            item.PublicRelationsInspected,
            item.SourceGroupsInspected
        }).SequenceEqual(current.Methods.OrderBy(item => item.Method).Select(item => new
        {
            item.Method,
            item.CandidateGenerationWork,
            item.CandidateVerificationWork,
            item.ForwardTraversalWork,
            item.BackwardTraversalWork,
            item.EvidenceRecordsInspected,
            item.PublicRelationsInspected,
            item.SourceGroupsInspected
        }));

    private static IReadOnlyList<PublicCandidateWarpStateDefinition> PublicCandidateStates(
        PastStateReconstructionDefinition definition) => definition.PublicCandidateStates
        .OrderBy(item => item.StartTick).ThenBy(item => item.EndTick)
        .Select(item => new PublicCandidateWarpStateDefinition
        {
            TimeWindow = new InteractionTimeWindow(item.StartTick, item.EndTick),
            TemporalKind = item.TemporalKind,
            RegionIds = item.RegionIds,
            EvidenceBoundaryState = EvidenceState.SupportedButUncertain,
            Provenance = [CandidateWarpProvenance.Create(
                CandidateWarpSourceKind.PublicCausalModel,
                $"s5-public-candidate-state:{item.StartTick}:{item.EndTick}",
                0,
                true,
                false,
                EvidenceState.SupportedButUncertain,
                item.RegionIds)]
        }).ToArray();

    private static IReadOnlyList<string> DirectSources(
        PastStateReconstructionDefinition definition,
        IReadOnlySet<string> ids) => definition.Constraints
        .Where(item => ids.Contains(item.Id)).Select(item => item.DirectSourceId)
        .Distinct(StringComparer.Ordinal).OrderBy(item => item, StringComparer.Ordinal).ToArray();

    private static string? HistoricalRegion(
        ScenarioDefinition scenario,
        IReadOnlyList<SimulationEvent> events,
        string entityId,
        long targetEndTick) => events
        .Where(item => item.Tick <= targetEndTick)
        .Where(item => item.SubjectIds.Contains(entityId, StringComparer.Ordinal))
        .Where(item => item.Position is not null)
        .OrderBy(item => item.Tick).ThenBy(item => item.Sequence).ThenBy(item => item.Id, StringComparer.Ordinal)
        .LastOrDefault()?.Position?.RegionId ?? scenario.Entities.Persons
            .Where(item => item.Id == entityId).Select(item => item.InitialPosition.RegionId)
            .Concat(scenario.Entities.Devices.Where(item => item.Id == entityId)
                .Select(item => item.InitialPosition.RegionId)).FirstOrDefault();

    private static string TruthWorldIdentifier(SimulationRun simulation) => Hash(JsonDefaults.Serialize(new
    {
        simulation.GroundTruthEvents,
        FinalStates = simulation.GroundTruthFinalEntityStates.OrderBy(item => item.Key, StringComparer.Ordinal)
            .Select(item => new { EntityId = item.Key, item.Value.EntityType, item.Value.Position }).ToArray(),
        simulation.GroundTruthInteractionOccurrences
    }));

    private static string Hash(string value) => Convert.ToHexString(
        SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();

    private static decimal Mean(IEnumerable<int> values)
    {
        var array = values.ToArray();
        return array.Length == 0 ? 0m : (decimal)array.Sum() / array.Length;
    }

    private static decimal Mean(IEnumerable<decimal> values)
    {
        var array = values.ToArray();
        return array.Length == 0 ? 0m : array.Sum() / array.Length;
    }

    private static void Validate(LocationResolutionEvaluationRequest request, IReadOnlyList<int> seeds)
    {
        ScenarioValidator.Validate(request.Scenario);
        if (seeds.Count == 0 || request.Scenario.PastStateReconstruction is null ||
            request.Scenario.StochasticHazard is null)
        {
            throw new ArgumentException("Location-resolution evaluation requires seeds and S5 public contracts.");
        }

        if (!string.Equals(request.Experiment.ScenarioId, request.Scenario.Id, StringComparison.Ordinal))
        {
            throw new ArgumentException("Experiment and scenario identifiers do not match.");
        }

        var levels = request.Experiment.Levels.OrderBy(item => item.InformationOrder).ToArray();
        if (levels.Length < 4 || levels.Select(item => item.Id).Distinct(StringComparer.Ordinal).Count() != levels.Length ||
            !levels.Select(item => item.InformationOrder).SequenceEqual(Enumerable.Range(0, levels.Length)))
        {
            throw new InvalidDataException("Location levels must have unique IDs and contiguous information order.");
        }

        if (!string.Equals(levels[0].Id, "current", StringComparison.Ordinal) ||
            !string.Equals(levels[^1].Id, "unknown", StringComparison.Ordinal) ||
            levels[^1].GenerateConstraint || levels[^1].ObservedRegionIds.Count != 0 ||
            levels[..^1].Any(item => !item.GenerateConstraint || item.ObservedRegionIds.Count == 0))
        {
            throw new InvalidDataException("Current must be finite and Unknown must be location-free.");
        }

        for (var index = 1; index < levels.Length - 1; index++)
        {
            if (!levels[index].ObservedRegionIds.ToHashSet(StringComparer.Ordinal).IsSupersetOf(
                levels[index - 1].ObservedRegionIds))
            {
                throw new InvalidDataException("Finite location levels must be monotonically nested.");
            }
        }

        var scenarioRegions = request.Scenario.EvaluationSearchableRegionIds.ToHashSet(StringComparer.Ordinal);
        if (levels.SelectMany(item => item.ObservedRegionIds).Any(item => !scenarioRegions.Contains(item)))
        {
            throw new InvalidDataException("Location levels may use only fixed S5 evaluation Regions.");
        }

        var definition = request.Scenario.PastStateReconstruction;
        var sourceDefinitions = definition.Constraints.Where(item => string.Equals(
            item.SourceObservationKey,
            request.Experiment.SourceObservationKey,
            StringComparison.Ordinal)).ToArray();
        var generationIds = definition.GenerationConstraintDefinitionIds.ToHashSet(StringComparer.Ordinal);
        if (sourceDefinitions.Length == 0 || sourceDefinitions.Any(item =>
            !generationIds.Contains(item.Id) || item.SourceKind != TracingConstraintSourceKind.CrossThreadObservation))
        {
            throw new InvalidDataException("The location source must be a generation-only interaction observation.");
        }

        var currentRegions = sourceDefinitions.SelectMany(item => item.AllowedRegionIds)
            .Distinct(StringComparer.Ordinal).ToHashSet(StringComparer.Ordinal);
        if (!currentRegions.SetEquals(levels[0].ObservedRegionIds))
        {
            throw new InvalidDataException("Current location level must reproduce the unchanged source payload.");
        }

        if (string.IsNullOrWhiteSpace(request.SourceCommitSha) ||
            string.IsNullOrWhiteSpace(request.ExperimentContentSha256) ||
            string.IsNullOrWhiteSpace(request.ScenarioContentSha256))
        {
            throw new ArgumentException("Source commit and input hashes are required.");
        }
    }

    private sealed record SourceProjection(
        InteractionObservation Observation,
        IReadOnlyList<CausalTracingConstraint> Constraints);
}
