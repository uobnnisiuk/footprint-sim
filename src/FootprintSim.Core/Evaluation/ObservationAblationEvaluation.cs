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

public sealed class ObservationAblationEvaluationRunner
{
    public const string BaselineEquivalenceCriterion =
        "For every paired seed: every method has the same candidate-region set, containment, and false-exclusion value as Full; reconstruction candidate dispositions, unresolved/conflicted candidate counts, and candidate-verification dispositions also match Full. Work is reported separately and is not an equivalence requirement.";

    public ObservationAblationEvaluationResult Run(ObservationAblationEvaluationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var seeds = request.Seeds.Distinct().OrderBy(item => item).ToArray();
        Validate(request, seeds);
        var profiles = BuildProfiles(request.Experiment);
        var inventory = BuildInventory(request.Experiment, request.Scenario);
        var runs = new List<ObservationAblationRun>();
        var engine = new SimulationEngine();
        var publicTopology = PublicHazardEnvelopeBuilder.Create(request.Scenario.StochasticHazard!);
        var topologyIdentifier = Hash(JsonDefaults.Serialize(publicTopology));

        foreach (var seed in seeds)
        {
            // The synthetic world is generated exactly once. Profiles only filter public records
            // and their derived constraints after this point.
            var simulation = engine.Run(request.Scenario, seed);
            var truthIdentifier = TruthWorldIdentifier(simulation);
            foreach (var profile in profiles)
            {
                runs.Add(EvaluateProfile(
                    request.Scenario,
                    request.Experiment,
                    profile,
                    simulation,
                    publicTopology,
                    topologyIdentifier,
                    truthIdentifier));
            }
        }

        var orderedRuns = runs
            .OrderBy(item => item.ProfileId, StringComparer.Ordinal)
            .ThenBy(item => item.Seed)
            .ToArray();
        var profileAggregates = BuildProfileAggregates(profiles, orderedRuns);
        var fullAggregate = profileAggregates.Single(item => item.ProfileKind == ObservationProfileKind.Full);
        var deltas = profileAggregates
            .SelectMany(item => CreateDeltas(item, fullAggregate))
            .OrderBy(item => item.ProfileId, StringComparer.Ordinal)
            .ThenBy(item => item.Method)
            .ToArray();
        var contributions = BuildContributions(
            request.Experiment,
            inventory,
            orderedRuns,
            profileAggregates,
            deltas);
        var equivalent = profileAggregates.Where(item => item.BaselineEquivalent).ToArray();
        var minimumFamilyCount = equivalent.Min(item => item.EnabledFamilyIds.Count);
        var candidateMinimal = equivalent
            .Where(item => item.EnabledFamilyIds.Count == minimumFamilyCount)
            .Select(item => item.ProfileId)
            .OrderBy(item => item, StringComparer.Ordinal)
            .ToArray();

        return new ObservationAblationEvaluationResult
        {
            Manifest = new ObservationAblationManifest
            {
                ExperimentId = request.Experiment.Id,
                ScenarioId = request.Scenario.Id,
                ExperimentContentSha256 = request.ExperimentContentSha256,
                ScenarioContentSha256 = request.ScenarioContentSha256,
                Seeds = seeds,
                ProfileIds = profiles.Select(item => item.Id).ToArray(),
                ProfileRunCount = orderedRuns.Length,
                MethodRunCount = orderedRuns.Sum(item => item.Methods.Count),
                SourceCommitSha = request.SourceCommitSha,
                DirtyWorkingTree = request.DirtyWorkingTree
            },
            Families = inventory,
            Profiles = profiles,
            Runs = orderedRuns,
            ProfileAggregates = profileAggregates,
            DeltasFromFull = deltas,
            FamilyContributions = contributions,
            CandidateMinimalProfileIds = candidateMinimal,
            CandidateMinimalProfileIsUnique = candidateMinimal.Length == 1,
            BaselineEquivalenceCriterion = BaselineEquivalenceCriterion
        };
    }

    public static IReadOnlyList<ObservationProfileDefinition> BuildProfiles(
        ObservationAblationExperimentDefinition experiment)
    {
        ArgumentNullException.ThrowIfNull(experiment);
        var familyIds = experiment.Families
            .Select(item => item.Id)
            .OrderBy(item => item, StringComparer.Ordinal)
            .ToArray();
        var profiles = new List<ObservationProfileDefinition>
        {
            new()
            {
                Id = "full",
                Kind = ObservationProfileKind.Full,
                EnabledFamilyIds = familyIds,
                SelectionRationale = "All public, cutoff-available experiment families are enabled."
            },
            new()
            {
                Id = "none",
                Kind = ObservationProfileKind.None,
                EnabledFamilyIds = [],
                SelectionRationale = "No experiment family is enabled; fixed public base records remain."
            }
        };
        profiles.AddRange(familyIds.Select(familyId => new ObservationProfileDefinition
        {
            Id = $"without-{familyId}",
            Kind = ObservationProfileKind.SingleFamilyRemoval,
            EnabledFamilyIds = familyIds.Where(item => !string.Equals(item, familyId, StringComparison.Ordinal)).ToArray(),
            SelectionRationale = $"Single-family removal of {familyId} from Full."
        }));
        profiles.AddRange(familyIds.Select(familyId => new ObservationProfileDefinition
        {
            Id = $"only-{familyId}",
            Kind = ObservationProfileKind.SingleFamilyPresence,
            EnabledFamilyIds = [familyId],
            SelectionRationale = $"Single-family presence of {familyId} over the fixed public base."
        }));
        profiles.AddRange(experiment.SelectedCombinations
            .OrderBy(item => item.Id, StringComparer.Ordinal)
            .Select(item => new ObservationProfileDefinition
            {
                Id = item.Id,
                Kind = ObservationProfileKind.SelectedCombination,
                EnabledFamilyIds = item.FamilyIds
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(familyId => familyId, StringComparer.Ordinal)
                    .ToArray(),
                SelectionRationale = item.SelectionRationale
            }));
        return profiles.ToArray();
    }

    private static ObservationAblationRun EvaluateProfile(
        ScenarioDefinition scenario,
        ObservationAblationExperimentDefinition experiment,
        ObservationProfileDefinition profile,
        SimulationRun simulation,
        PublicHazardEnvelope publicTopology,
        string topologyIdentifier,
        string truthIdentifier)
    {
        var definition = scenario.PastStateReconstruction!;
        var condition = simulation.PastStateEvidenceCondition ??
            throw new InvalidOperationException("S5 simulation did not select an evidence condition.");
        var enabledFamilies = profile.EnabledFamilyIds.ToHashSet(StringComparer.Ordinal);
        var familyBySourceKey = experiment.Families
            .SelectMany(family => family.SourceObservationKeys.Select(key => (key, family.Id)))
            .ToDictionary(item => item.key, item => item.Id, StringComparer.Ordinal);
        var familyByDirectSource = definition.Constraints
            .Where(item => familyBySourceKey.ContainsKey(item.SourceObservationKey))
            .GroupBy(item => item.DirectSourceId, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => familyBySourceKey[group.First().SourceObservationKey],
                StringComparer.Ordinal);
        var allConstraints = simulation.CausalTracingConstraints ??
            throw new InvalidOperationException("S5 simulation produced no public constraint set.");
        var retainedConstraints = allConstraints.Constraints
            .Where(item => !familyByDirectSource.TryGetValue(item.DirectSourceId, out var familyId) ||
                enabledFamilies.Contains(familyId))
            .OrderBy(item => item.ConstraintId, StringComparer.Ordinal)
            .ToArray();
        var retainedConstraintSet = new CausalTracingConstraintSet(
            scenario.Id,
            simulation.Seed,
            retainedConstraints);
        var retainedPublicObservations = simulation.ObservationSet.Observations
            .Where(item => !familyBySourceKey.TryGetValue(item.EvidenceType, out var familyId) ||
                enabledFamilies.Contains(familyId))
            .OrderBy(item => item.AvailableTick)
            .ThenBy(item => item.ObservedTick)
            .ThenBy(item => item.ObservationId, StringComparer.Ordinal)
            .ToArray();
        var retainedSourceObservationIds = retainedConstraints
            .Select(item => item.SourceObservationId)
            .ToHashSet(StringComparer.Ordinal);
        var retainedInteractionObservations = (simulation.InteractionObservations?.Observations ?? [])
            .Where(item => retainedSourceObservationIds.Contains(item.ObservationId))
            .OrderBy(item => item.AvailableAtTick)
            .ThenBy(item => item.ObservedTimeWindow.StartTick)
            .ThenBy(item => item.ObservationId, StringComparer.Ordinal)
            .ToArray();
        var observationSet = new ObservationSet
        {
            ScenarioId = scenario.Id,
            Seed = simulation.Seed,
            Observations = retainedPublicObservations
        };
        var recordedObservationIds = retainedPublicObservations
            .Select(item => item.ObservationId)
            .Concat(retainedInteractionObservations.Select(item => item.ObservationId))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(item => item, StringComparer.Ordinal)
            .ToArray();
        var publicObservationIds = retainedPublicObservations
            .Select(item => item.ObservationId)
            .Concat(retainedInteractionObservations
                .Where(item => item.Visibility == InteractionVisibility.PublicEvidence)
                .Select(item => item.ObservationId))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(item => item, StringComparer.Ordinal)
            .ToArray();
        var availableObservationIds = retainedPublicObservations
            .Where(item => item.AvailableTick <= definition.EvidenceAvailabilityCutoff)
            .Select(item => item.ObservationId)
            .Concat(retainedInteractionObservations
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
        var generationDefinitionIds = definition.GenerationConstraintDefinitionIds.ToHashSet(StringComparer.Ordinal);
        var verificationDefinitionIds = definition.VerificationConstraintDefinitionIds.ToHashSet(StringComparer.Ordinal);
        var traversalDefinitionIds = definition.TraversalConstraintDefinitionIds.ToHashSet(StringComparer.Ordinal);
        var comparison = new PastStateReconstructor().Compare(
            new PastStatePublicContext
            {
                Query = query,
                PublicCandidateStates = PublicCandidateStates(definition),
                GenerationDirectSourceIds = DirectSources(definition, generationDefinitionIds),
                VerificationDirectSourceIds = DirectSources(definition, verificationDefinitionIds),
                TraversalDirectSourceIds = DirectSources(definition, traversalDefinitionIds),
                AvailableObservationIds = availableObservationIds,
                LocalTraversalWindowPaddingTicks = definition.LocalTraversalWindowPaddingTicks,
                PublicHazardEnvelope = publicTopology
            },
            observationSet,
            retainedConstraintSet);
        var truthPersonRegion = HistoricalRegion(
            scenario,
            simulation.GroundTruthEvents,
            scenario.TargetPersonId,
            definition.TargetEndTick);
        var deviceId = scenario.Entities.Devices
            .Select(item => item.Id)
            .OrderBy(item => item, StringComparer.Ordinal)
            .FirstOrDefault();
        var truthDeviceRegion = deviceId is null
            ? null
            : HistoricalRegion(scenario, simulation.GroundTruthEvents, deviceId, definition.TargetEndTick);
        var activeDefinitions = definition.Constraints
            .Where(item => item.AppliesToPastStateConditions.Contains(condition))
            .Where(item => generationDefinitionIds.Contains(item.Id) ||
                verificationDefinitionIds.Contains(item.Id) ||
                traversalDefinitionIds.Contains(item.Id))
            .ToArray();
        var availableIds = availableObservationIds.ToHashSet(StringComparer.Ordinal);
        var runtimeSourceIdByDirectSource = allConstraints.Constraints
            .GroupBy(item => item.DirectSourceId, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.Select(item => item.SourceObservationId).First(),
                StringComparer.Ordinal);
        var unavailableEvidenceCount = activeDefinitions.Count(item =>
            item.Visibility != InteractionVisibility.PublicEvidence ||
            item.AvailableAtTick > definition.EvidenceAvailabilityCutoff ||
            (familyBySourceKey.TryGetValue(item.SourceObservationKey, out var familyId) &&
                !enabledFamilies.Contains(familyId)) ||
            !runtimeSourceIdByDirectSource.TryGetValue(item.DirectSourceId, out var sourceObservationId) ||
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
        var familyRawObservationIds = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        foreach (var family in experiment.Families)
        {
            var ids = retainedPublicObservations
                .Where(item => family.SourceObservationKeys.Contains(item.EvidenceType, StringComparer.Ordinal))
                .Select(item => item.ObservationId)
                .ToHashSet(StringComparer.Ordinal);
            var directSources = definition.Constraints
                .Where(item => family.SourceObservationKeys.Contains(item.SourceObservationKey, StringComparer.Ordinal))
                .Select(item => item.DirectSourceId)
                .ToHashSet(StringComparer.Ordinal);
            foreach (var constraint in retainedConstraints.Where(item => directSources.Contains(item.DirectSourceId)))
            {
                if (retainedInteractionObservations.Any(item => string.Equals(
                    item.ObservationId,
                    constraint.SourceObservationId,
                    StringComparison.Ordinal)))
                {
                    ids.Add(constraint.SourceObservationId);
                }
            }

            familyRawObservationIds.Add(family.Id, ids);
        }

        var queryUsableFamilyIds = comparison.Reconstruction.EvidenceSet.Constraints
            .Select(item => familyByDirectSource.GetValueOrDefault(item.DirectSourceId))
            .Where(item => item is not null)
            .Cast<string>()
            .Distinct(StringComparer.Ordinal)
            .OrderBy(item => item, StringComparer.Ordinal)
            .ToArray();
        var usage = comparison.EvidenceUsage;
        var queryUsableSourceObservationIds = usage
            .Where(item => item.Disposition == PastStateEvidenceUsageDisposition.Used)
            .Select(item => item.SourceObservationId)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(item => item, StringComparer.Ordinal)
            .ToArray();
        var methods = comparison.Methods
            .OrderBy(item => item.Method)
            .Select(item =>
            {
                var methodEvaluation = evaluated.Methods.Single(method => method.Method == item.Method);
                return new ObservationAblationMethodRun
                {
                    Method = item.Method,
                    CandidateRegionIds = item.CandidateRegionIds,
                    HistoricalCandidateStateCount = item.CandidateRegionIds.Count,
                    ContainsTruth = methodEvaluation.ContainsTruth,
                    FalseExclusion = methodEvaluation.FalseExclusion,
                    CandidateReduction = methodEvaluation.CandidateReduction,
                    CandidateGenerationWork = item.CandidateGenerationSteps,
                    CandidateVerificationWork = item.CandidateVerificationSteps,
                    ForwardTraversalWork = item.ForwardTransitionSteps,
                    BackwardTraversalWork = item.BackwardTransitionSteps,
                    PublicRelationsInspected = item.PublicRelationsInspected,
                    EvidenceRecordsInspected = item.EvidenceRecordsInspected,
                    SourceGroupsInspected = item.SourceGroupsInspected,
                    TransitionIds = item.TransitionIds
                };
            }).ToArray();

        return new ObservationAblationRun
        {
            ScenarioId = scenario.Id,
            Seed = simulation.Seed,
            EvidenceCondition = condition,
            ProfileId = profile.Id,
            ProfileKind = profile.Kind,
            EnabledFamilyIds = profile.EnabledFamilyIds,
            PresentFamilyIds = familyRawObservationIds
                .Where(item => item.Value.Count > 0)
                .Select(item => item.Key)
                .OrderBy(item => item, StringComparer.Ordinal)
                .ToArray(),
            QueryUsableFamilyIds = queryUsableFamilyIds,
            RecordedObservationIds = recordedObservationIds,
            PublicObservationIds = publicObservationIds,
            AvailableByCutoffObservationIds = availableObservationIds,
            QueryUsableSourceObservationIds = queryUsableSourceObservationIds,
            ConstraintIds = comparison.Reconstruction.EvidenceSet.Constraints
                .Select(item => item.ConstraintId)
                .OrderBy(item => item, StringComparer.Ordinal)
                .ToArray(),
            TruthWorldIdentifier = truthIdentifier,
            PublicTopologyIdentifier = topologyIdentifier,
            TruthPersonRegionId = truthPersonRegion,
            TruthDeviceRegionId = truthDeviceRegion,
            SearchableRegionCount = scenario.EvaluationSearchableRegionIds.Count,
            RawObservationCount = recordedObservationIds.Length,
            PublicObservationCount = publicObservationIds.Length,
            AvailableByCutoffObservationCount = availableObservationIds.Length,
            QueryUsableSourceObservationCount = queryUsableSourceObservationIds.Length,
            FamilyRawObservationCount = familyRawObservationIds.Sum(item => item.Value.Count),
            GenerationConstraintCount = usage.Count(item => item.UsageRoles.Contains(
                PastStateEvidenceUsageRole.CandidateGeneration)),
            VerificationConstraintCount = usage.Count(item => item.UsageRoles.Contains(
                PastStateEvidenceUsageRole.CandidateVerification)),
            TraversalConstraintCount = usage.Count(item => item.UsageRoles.Contains(
                PastStateEvidenceUsageRole.Traversal)),
            ResolvedConstraintSourceCount = usage.Count(item => item.SourceObservationResolved),
            UnresolvedConstraintSourceCount = usage.Count(item => !item.SourceObservationResolved),
            PresentEvidenceSourceGroupCount = evaluated.PresentEvidenceSourceGroupCount,
            IndependentSourceGroupCount = evaluated.PresentEvidenceSourceGroupCount,
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
            Methods = methods
        };
    }

    private static IReadOnlyList<ObservationProfileAggregate> BuildProfileAggregates(
        IReadOnlyList<ObservationProfileDefinition> profiles,
        IReadOnlyList<ObservationAblationRun> runs)
    {
        var fullRuns = runs
            .Where(item => item.ProfileKind == ObservationProfileKind.Full)
            .ToDictionary(item => item.Seed);
        return profiles
            .OrderBy(item => item.Id, StringComparer.Ordinal)
            .Select(profile =>
            {
                var profileRuns = runs.Where(item => item.ProfileId == profile.Id).ToArray();
                var safetyEquivalent = profileRuns.All(item => SafetyEquivalent(item, fullRuns[item.Seed]));
                var baselineEquivalent = profileRuns.All(item => BaselineEquivalent(item, fullRuns[item.Seed]));
                return new ObservationProfileAggregate
                {
                    ProfileId = profile.Id,
                    ProfileKind = profile.Kind,
                    EnabledFamilyIds = profile.EnabledFamilyIds,
                    SeedCount = profileRuns.Length,
                    SafetyEquivalentToFull = safetyEquivalent,
                    BaselineEquivalent = baselineEquivalent,
                    MeanRawObservationCount = Mean(profileRuns.Select(item => item.RawObservationCount)),
                    MeanPublicObservationCount = Mean(profileRuns.Select(item => item.PublicObservationCount)),
                    MeanAvailableByCutoffObservationCount = Mean(profileRuns.Select(
                        item => item.AvailableByCutoffObservationCount)),
                    MeanQueryUsableSourceObservationCount = Mean(profileRuns.Select(
                        item => item.QueryUsableSourceObservationCount)),
                    MeanFamilyRawObservationCount = Mean(profileRuns.Select(item => item.FamilyRawObservationCount)),
                    MeanGenerationConstraintCount = Mean(profileRuns.Select(item => item.GenerationConstraintCount)),
                    MeanVerificationConstraintCount = Mean(profileRuns.Select(item => item.VerificationConstraintCount)),
                    MeanTraversalConstraintCount = Mean(profileRuns.Select(item => item.TraversalConstraintCount)),
                    MeanPresentEvidenceSourceGroupCount = Mean(profileRuns.Select(item => item.PresentEvidenceSourceGroupCount)),
                    MeanIndependentSourceGroupCount = Mean(profileRuns.Select(item => item.IndependentSourceGroupCount)),
                    MeanSameAncestryDerivedRecordCount = Mean(profileRuns.Select(item => item.SameAncestryDerivedRecordCount)),
                    MeanConflictingEvidenceCount = Mean(profileRuns.Select(item => item.ConflictingEvidenceCount)),
                    MeanUncertainEvidenceCount = Mean(profileRuns.Select(item => item.UncertainEvidenceCount)),
                    MeanUnavailableEvidenceCount = Mean(profileRuns.Select(item => item.UnavailableEvidenceCount)),
                    MeanUnresolvedCandidateCount = Mean(profileRuns.Select(item => item.UnresolvedCandidateCount)),
                    MeanConflictedCandidateCount = Mean(profileRuns.Select(item => item.ConflictedCandidateCount)),
                    IndependentlySupportedCandidateCount = profileRuns.Sum(item => item.IndependentlySupportedCandidateCount),
                    IndependentlyRefutedCandidateCount = profileRuns.Sum(item => item.IndependentlyRefutedCandidateCount),
                    ConflictingVerificationCandidateCount = profileRuns.Sum(item => item.ConflictingVerificationCandidateCount),
                    UnresolvedVerificationCandidateCount = profileRuns.Sum(item => item.UnresolvedVerificationCandidateCount),
                    SharedAncestryOnlyCandidateCount = profileRuns.Sum(item => item.SharedAncestryOnlyCandidateCount),
                    UnavailableVerificationCandidateCount = profileRuns.Sum(item => item.UnavailableVerificationCandidateCount),
                    Methods = Enum.GetValues<PastStateMethod>()
                        .Select(method => AggregateMethod(profileRuns, method))
                        .ToArray()
                };
            }).ToArray();
    }

    private static ObservationAblationMethodAggregate AggregateMethod(
        IReadOnlyList<ObservationAblationRun> runs,
        PastStateMethod method)
    {
        var methods = runs.Select(item => item.Methods.Single(value => value.Method == method)).ToArray();
        var containmentAvailable = methods.Where(item => item.ContainsTruth.HasValue).ToArray();
        return new ObservationAblationMethodAggregate
        {
            Method = method,
            SeedCount = methods.Length,
            MeanCandidateCount = Mean(methods.Select(item => item.HistoricalCandidateStateCount)),
            ContainmentRate = containmentAvailable.Length == 0
                ? 0m
                : (decimal)containmentAvailable.Count(item => item.ContainsTruth == true) /
                    containmentAvailable.Length,
            FalseExclusionCount = methods.Count(item => item.FalseExclusion == true),
            MeanReduction = Mean(methods.Select(item => item.CandidateReduction)),
            MeanCandidateGenerationWork = Mean(methods.Select(item => item.CandidateGenerationWork)),
            MeanCandidateVerificationWork = Mean(methods.Select(item => item.CandidateVerificationWork)),
            MeanForwardTraversalWork = Mean(methods.Select(item => item.ForwardTraversalWork)),
            MeanBackwardTraversalWork = Mean(methods.Select(item => item.BackwardTraversalWork)),
            MeanPublicRelationsInspected = Mean(methods.Select(item => item.PublicRelationsInspected)),
            MeanEvidenceRecordsInspected = Mean(methods.Select(item => item.EvidenceRecordsInspected)),
            MeanSourceGroupsInspected = Mean(methods.Select(item => item.SourceGroupsInspected))
        };
    }

    private static IReadOnlyList<ObservationProfileMethodDelta> CreateDeltas(
        ObservationProfileAggregate profile,
        ObservationProfileAggregate full)
    {
        var removed = full.EnabledFamilyIds
            .Except(profile.EnabledFamilyIds, StringComparer.Ordinal)
            .OrderBy(item => item, StringComparer.Ordinal)
            .ToArray();
        return profile.Methods.Select(method =>
        {
            var baseline = full.Methods.Single(item => item.Method == method.Method);
            return new ObservationProfileMethodDelta
            {
                ProfileId = profile.ProfileId,
                Method = method.Method,
                RemovedFromFullFamilyIds = removed,
                MeanCandidateCountDelta = method.MeanCandidateCount - baseline.MeanCandidateCount,
                ContainmentRateDelta = method.ContainmentRate - baseline.ContainmentRate,
                FalseExclusionCountDelta = method.FalseExclusionCount - baseline.FalseExclusionCount,
                MeanReductionDelta = method.MeanReduction - baseline.MeanReduction,
                CandidateGenerationWorkDelta = method.MeanCandidateGenerationWork - baseline.MeanCandidateGenerationWork,
                CandidateVerificationWorkDelta = method.MeanCandidateVerificationWork - baseline.MeanCandidateVerificationWork,
                ForwardTraversalWorkDelta = method.MeanForwardTraversalWork - baseline.MeanForwardTraversalWork,
                BackwardTraversalWorkDelta = method.MeanBackwardTraversalWork - baseline.MeanBackwardTraversalWork,
                PublicRelationsInspectedDelta = method.MeanPublicRelationsInspected - baseline.MeanPublicRelationsInspected,
                EvidenceRecordsInspectedDelta = method.MeanEvidenceRecordsInspected - baseline.MeanEvidenceRecordsInspected,
                UnresolvedCandidateCountDelta = profile.MeanUnresolvedCandidateCount - full.MeanUnresolvedCandidateCount,
                ConflictedCandidateCountDelta = profile.MeanConflictedCandidateCount - full.MeanConflictedCandidateCount
            };
        }).ToArray();
    }

    private static IReadOnlyList<ObservationFamilyContribution> BuildContributions(
        ObservationAblationExperimentDefinition experiment,
        IReadOnlyList<ObservationFamilyInventoryEntry> inventory,
        IReadOnlyList<ObservationAblationRun> runs,
        IReadOnlyList<ObservationProfileAggregate> aggregates,
        IReadOnlyList<ObservationProfileMethodDelta> deltas)
    {
        var full = aggregates.Single(item => item.ProfileKind == ObservationProfileKind.Full);
        var fullRuns = runs.Where(item => item.ProfileKind == ObservationProfileKind.Full).ToArray();
        return experiment.Families.OrderBy(item => item.Id, StringComparer.Ordinal).Select(family =>
        {
            var familyInventory = inventory.Single(item => item.FamilyId == family.Id);
            var withoutId = $"without-{family.Id}";
            var onlyId = $"only-{family.Id}";
            var without = aggregates.Single(item => item.ProfileId == withoutId);
            return new ObservationFamilyContribution
            {
                FamilyId = family.Id,
                WithoutProfileId = withoutId,
                OnlyProfileId = onlyId,
                RawObservationPresentSeedCount = fullRuns.Count(item => item.PresentFamilyIds.Contains(
                    family.Id,
                    StringComparer.Ordinal)),
                ActiveConstraintSeedCount = fullRuns.Count(item => item.QueryUsableFamilyIds.Contains(
                    family.Id,
                    StringComparer.Ordinal)),
                ActiveConstraintUseCount = fullRuns.Count(item => item.QueryUsableFamilyIds.Contains(
                        family.Id,
                        StringComparer.Ordinal)) * familyInventory.ConstraintDefinitionIds.Count,
                RemovalDeltas = deltas.Where(item => item.ProfileId == withoutId).ToArray(),
                IndependentVerificationDispositionDelta =
                    (without.IndependentlySupportedCandidateCount + without.IndependentlyRefutedCandidateCount) -
                    (full.IndependentlySupportedCandidateCount + full.IndependentlyRefutedCandidateCount),
                UnresolvedVerificationDispositionDelta =
                    without.UnresolvedVerificationCandidateCount - full.UnresolvedVerificationCandidateCount
            };
        }).ToArray();
    }

    private static IReadOnlyList<ObservationFamilyInventoryEntry> BuildInventory(
        ObservationAblationExperimentDefinition experiment,
        ScenarioDefinition scenario)
    {
        var definition = scenario.PastStateReconstruction!;
        var generation = definition.GenerationConstraintDefinitionIds.ToHashSet(StringComparer.Ordinal);
        var verification = definition.VerificationConstraintDefinitionIds.ToHashSet(StringComparer.Ordinal);
        var traversal = definition.TraversalConstraintDefinitionIds.ToHashSet(StringComparer.Ordinal);
        return experiment.Families.OrderBy(item => item.Id, StringComparer.Ordinal).Select(family =>
        {
            var constraints = definition.Constraints
                .Where(item => family.SourceObservationKeys.Contains(item.SourceObservationKey, StringComparer.Ordinal))
                .OrderBy(item => item.Id, StringComparer.Ordinal)
                .ToArray();
            var roles = constraints.Select(item => generation.Contains(item.Id)
                    ? PastStateEvidenceUsageRole.CandidateGeneration
                    : verification.Contains(item.Id)
                        ? PastStateEvidenceUsageRole.CandidateVerification
                        : traversal.Contains(item.Id)
                            ? PastStateEvidenceUsageRole.Traversal
                            : PastStateEvidenceUsageRole.EvaluationOnly)
                .Distinct()
                .OrderBy(item => item)
                .ToArray();
            var recordedRegions = scenario.Steps
                .Where(item => item.Observation is not null && family.SourceObservationKeys.Contains(
                    item.Observation.EvidenceType,
                    StringComparer.Ordinal))
                .Select(item => item.Observation!.ReportedRegionId)
                .Where(item => item is not null)
                .Cast<string>()
                .Concat(constraints
                    .Where(item => item.SourceKind == TracingConstraintSourceKind.CrossThreadObservation)
                    .SelectMany(item => item.AllowedRegionIds))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(item => item, StringComparer.Ordinal)
                .ToArray();
            return new ObservationFamilyInventoryEntry
            {
                FamilyId = family.Id,
                Description = family.Description,
                SourceObservationKeys = family.SourceObservationKeys,
                ConstraintDefinitionIds = constraints.Select(item => item.Id).ToArray(),
                DirectSourceIds = constraints.Select(item => item.DirectSourceId)
                    .Distinct(StringComparer.Ordinal).OrderBy(item => item, StringComparer.Ordinal).ToArray(),
                SourceKinds = constraints.Select(item => item.SourceKind).Distinct().OrderBy(item => item).ToArray(),
                UsageRoles = roles,
                ObservedSubjectIds = constraints.Select(item => item.IsDeviceOnlyEvidence
                        ? item.OtherEntityId ?? scenario.TargetPersonId
                        : scenario.TargetPersonId)
                    .Distinct(StringComparer.Ordinal).OrderBy(item => item, StringComparer.Ordinal).ToArray(),
                ObserverOrSourceIds = constraints.Select(item => item.OtherEntityId ?? scenario.TargetPersonId)
                    .Distinct(StringComparer.Ordinal).OrderBy(item => item, StringComparer.Ordinal).ToArray(),
                ObservedTimeWindows = constraints.Select(item => new InteractionTimeWindow(
                        item.ObservedStartTick,
                        item.ObservedEndTick))
                    .Distinct().OrderBy(item => item.StartTick).ThenBy(item => item.EndTick).ToArray(),
                AvailableAtTicks = constraints.Select(item => item.AvailableAtTick).Distinct().OrderBy(item => item).ToArray(),
                RecordedRegionIds = recordedRegions,
                AllowedConstraintRegionIds = constraints.SelectMany(item => item.AllowedRegionIds)
                    .Distinct(StringComparer.Ordinal).OrderBy(item => item, StringComparer.Ordinal).ToArray(),
                ConstrainedTimeWindows = constraints.Select(item => new InteractionTimeWindow(
                        item.ConstrainedStartTick,
                        item.ConstrainedEndTick))
                    .Distinct().OrderBy(item => item.StartTick).ThenBy(item => item.EndTick).ToArray(),
                RelationKinds = constraints.Select(item => item.RelationKind)
                    .Distinct(StringComparer.Ordinal).OrderBy(item => item, StringComparer.Ordinal).ToArray(),
                TransformationKinds = constraints.Select(item => item.DerivationKind)
                    .Distinct(StringComparer.Ordinal).OrderBy(item => item, StringComparer.Ordinal).ToArray(),
                EvidenceStates = constraints.Select(item => item.EvidenceState).Distinct().OrderBy(item => item).ToArray(),
                Visibilities = constraints.Select(item => item.Visibility).Distinct().OrderBy(item => item).ToArray(),
                SourceAncestryIds = constraints.SelectMany(item => item.SourceAncestryIds)
                    .Distinct(StringComparer.Ordinal).OrderBy(item => item, StringComparer.Ordinal).ToArray(),
                InformationDimensions =
                [
                    "subject", "entity-kind", "observed-time", "available-time", "region-or-state",
                    "relation", "evidence-state", "visibility", "usage-role", "source-ancestry"
                ]
            };
        }).ToArray();
    }

    private static bool SafetyEquivalent(ObservationAblationRun candidate, ObservationAblationRun full) =>
        candidate.Methods.OrderBy(item => item.Method).Select(item => (item.Method, item.ContainsTruth, item.FalseExclusion))
            .SequenceEqual(full.Methods.OrderBy(item => item.Method).Select(item =>
                (item.Method, item.ContainsTruth, item.FalseExclusion)));

    private static bool BaselineEquivalent(ObservationAblationRun candidate, ObservationAblationRun full) =>
        SafetyEquivalent(candidate, full) &&
        candidate.UnresolvedCandidateCount == full.UnresolvedCandidateCount &&
        candidate.ConflictedCandidateCount == full.ConflictedCandidateCount &&
        candidate.Methods.OrderBy(item => item.Method).Select(item => new
        {
            item.Method,
            Regions = string.Join("|", item.CandidateRegionIds)
        })
            .SequenceEqual(full.Methods.OrderBy(item => item.Method).Select(item => new
            {
                item.Method,
                Regions = string.Join("|", item.CandidateRegionIds)
            })) &&
        candidate.CandidateEvaluations.Select(item =>
                (item.RegionId, item.CandidateDisposition, item.VerificationDisposition))
            .SequenceEqual(full.CandidateEvaluations.Select(item =>
                (item.RegionId, item.CandidateDisposition, item.VerificationDisposition)));

    private static IReadOnlyList<PublicCandidateWarpStateDefinition> PublicCandidateStates(
        PastStateReconstructionDefinition definition) => definition.PublicCandidateStates
        .OrderBy(item => item.StartTick)
        .ThenBy(item => item.EndTick)
        .Select(item => new PublicCandidateWarpStateDefinition
        {
            TimeWindow = new InteractionTimeWindow(item.StartTick, item.EndTick),
            TemporalKind = item.TemporalKind,
            RegionIds = item.RegionIds,
            EvidenceBoundaryState = EvidenceState.SupportedButUncertain,
            Provenance = [CandidateWarpProvenance.Create(
                CandidateWarpSourceKind.PublicCausalModel,
                $"s5-public-candidate-state:{item.StartTick}:{item.EndTick}",
                availableAtTick: 0,
                isPersonEvidence: true,
                isDeviceOnlyEvidence: false,
                EvidenceState.SupportedButUncertain,
                item.RegionIds)]
        }).ToArray();

    private static IReadOnlyList<string> DirectSources(
        PastStateReconstructionDefinition definition,
        IReadOnlySet<string> definitionIds) => definition.Constraints
        .Where(item => definitionIds.Contains(item.Id))
        .Select(item => item.DirectSourceId)
        .Distinct(StringComparer.Ordinal)
        .OrderBy(item => item, StringComparer.Ordinal)
        .ToArray();

    private static string? HistoricalRegion(
        ScenarioDefinition scenario,
        IReadOnlyList<SimulationEvent> events,
        string entityId,
        long targetEndTick)
    {
        var eventRegion = events
            .Where(item => item.Tick <= targetEndTick)
            .Where(item => item.SubjectIds.Contains(entityId, StringComparer.Ordinal))
            .Where(item => item.Position is not null)
            .OrderBy(item => item.Tick)
            .ThenBy(item => item.Sequence)
            .ThenBy(item => item.Id, StringComparer.Ordinal)
            .LastOrDefault()?.Position?.RegionId;
        return eventRegion ?? scenario.Entities.Persons
            .Where(item => item.Id == entityId)
            .Select(item => item.InitialPosition.RegionId)
            .Concat(scenario.Entities.Devices
                .Where(item => item.Id == entityId)
                .Select(item => item.InitialPosition.RegionId))
            .FirstOrDefault();
    }

    private static string TruthWorldIdentifier(SimulationRun simulation)
    {
        var material = JsonDefaults.Serialize(new
        {
            simulation.GroundTruthEvents,
            FinalStates = simulation.GroundTruthFinalEntityStates
                .OrderBy(item => item.Key, StringComparer.Ordinal)
                .Select(item => new { EntityId = item.Key, item.Value.EntityType, item.Value.Position })
                .ToArray(),
            simulation.GroundTruthInteractionOccurrences
        });
        return Hash(material);
    }

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

    private static void Validate(ObservationAblationEvaluationRequest request, IReadOnlyList<int> seeds)
    {
        ScenarioValidator.Validate(request.Scenario);
        if (request.Scenario.PastStateReconstruction is null || request.Scenario.StochasticHazard is null)
        {
            throw new ArgumentException("Observation ablation requires past-state reconstruction and a public hazard envelope.");
        }

        if (!string.Equals(request.Experiment.ScenarioId, request.Scenario.Id, StringComparison.Ordinal))
        {
            throw new ArgumentException("Experiment and scenario identifiers do not match.");
        }

        if (seeds.Count == 0)
        {
            throw new ArgumentException("At least one seed is required.");
        }

        if (string.IsNullOrWhiteSpace(request.SourceCommitSha) ||
            string.IsNullOrWhiteSpace(request.ExperimentContentSha256) ||
            string.IsNullOrWhiteSpace(request.ScenarioContentSha256))
        {
            throw new ArgumentException("Source commit and input hashes are required.");
        }

        var duplicateFamily = request.Experiment.Families
            .GroupBy(item => item.Id, StringComparer.Ordinal)
            .FirstOrDefault(item => item.Count() > 1);
        if (request.Experiment.Families.Count == 0 || duplicateFamily is not null)
        {
            throw new InvalidDataException("Experiment families must be non-empty and uniquely identified.");
        }

        var duplicateSourceKey = request.Experiment.Families
            .SelectMany(item => item.SourceObservationKeys)
            .GroupBy(item => item, StringComparer.Ordinal)
            .FirstOrDefault(item => item.Count() > 1);
        if (duplicateSourceKey is not null)
        {
            throw new InvalidDataException(
                $"Source observation key '{duplicateSourceKey.Key}' belongs to more than one family.");
        }

        var familyIds = request.Experiment.Families.Select(item => item.Id).ToHashSet(StringComparer.Ordinal);
        foreach (var combination in request.Experiment.SelectedCombinations)
        {
            if (combination.FamilyIds.Count < 2 || combination.FamilyIds.Any(item => !familyIds.Contains(item)))
            {
                throw new InvalidDataException($"Selected combination '{combination.Id}' is invalid.");
            }
        }

        var definition = request.Scenario.PastStateReconstruction;
        var usedDefinitionIds = definition.GenerationConstraintDefinitionIds
            .Concat(definition.VerificationConstraintDefinitionIds)
            .Concat(definition.TraversalConstraintDefinitionIds)
            .ToHashSet(StringComparer.Ordinal);
        var mappedKeys = request.Experiment.Families
            .SelectMany(item => item.SourceObservationKeys)
            .ToHashSet(StringComparer.Ordinal);
        var unmapped = definition.Constraints
            .Where(item => usedDefinitionIds.Contains(item.Id))
            .Where(item => item.Visibility == InteractionVisibility.PublicEvidence)
            .Where(item => item.AvailableAtTick <= definition.EvidenceAvailabilityCutoff)
            .Where(item => !mappedKeys.Contains(item.SourceObservationKey))
            .Select(item => item.Id)
            .OrderBy(item => item, StringComparer.Ordinal)
            .ToArray();
        if (unmapped.Length > 0)
        {
            throw new InvalidDataException(
                $"Every public cutoff-usable method source must map to one family: {string.Join(", ", unmapped)}.");
        }

        var missingDefinitions = request.Experiment.Families
            .Where(family => !definition.Constraints.Any(item =>
                family.SourceObservationKeys.Contains(item.SourceObservationKey, StringComparer.Ordinal)))
            .Select(item => item.Id)
            .ToArray();
        if (missingDefinitions.Length > 0)
        {
            throw new InvalidDataException(
                $"Experiment families have no scenario constraints: {string.Join(", ", missingDefinitions)}.");
        }
    }
}
