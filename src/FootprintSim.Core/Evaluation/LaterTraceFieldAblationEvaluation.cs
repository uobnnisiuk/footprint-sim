using FootprintSim.Evidence;
using FootprintSim.Inference.PastStates;
using FootprintSim.Interactions;
using FootprintSim.Interactions.Tracing;
using FootprintSim.Scenarios;
using FootprintSim.Simulation;

namespace FootprintSim.Evaluation;

public sealed class LaterTraceFieldAblationEvaluationRunner
{
    public const string BaselineEquivalenceCriterion =
        "For every paired seed: every method has the same candidate-region set, containment, false-exclusion value, and work as Full; reconstruction candidate dispositions and candidate-verification dispositions also match Full.";

    public const string InterpretationBoundary =
        "The current S5 transformation requires subject, time, location, and relation together. An incomplete profile is evaluated by withholding the source-derived constraints, not by inventing a partial constraint. Equivalent incomplete-profile results therefore show representation-limited non-identifiability, not equal real-world field value.";

    public LaterTraceFieldAblationEvaluationResult Run(LaterTraceFieldAblationEvaluationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var seeds = request.Seeds.Distinct().OrderBy(item => item).ToArray();
        Validate(request, seeds);
        var profiles = request.Experiment.Profiles
            .OrderBy(item => item.Id, StringComparer.Ordinal)
            .ToArray();
        var requiredFields = request.Experiment.RequiredFieldIds.ToHashSet(StringComparer.Ordinal);
        var sourceConstraints = SourceConstraints(request.Experiment, request.Scenario);
        var inventory = BuildInventory(request.Experiment, request.Scenario, sourceConstraints);
        var baseResult = new ObservationAblationEvaluationRunner().Run(
            new ObservationAblationEvaluationRequest
            {
                Experiment = request.BaseExperiment,
                Scenario = request.Scenario,
                Seeds = seeds,
                ExperimentContentSha256 = request.BaseExperimentContentSha256,
                ScenarioContentSha256 = request.ScenarioContentSha256,
                SourceCommitSha = request.SourceCommitSha,
                DirtyWorkingTree = request.DirtyWorkingTree
            });
        var baseRuns = baseResult.Runs.ToLookup(item => item.ProfileId, StringComparer.Ordinal);
        var fullRuns = baseRuns["full"].ToDictionary(item => item.Seed);
        var withoutProfileId = $"without-{request.Experiment.SourceObservationKey}";
        var withoutRuns = baseRuns[withoutProfileId].ToDictionary(item => item.Seed);
        var sourceRuntimeBySeed = BuildRuntimeSourceAudit(
            request.Experiment,
            request.Scenario,
            sourceConstraints,
            seeds);
        var candidateRegionsBefore = TargetCandidateRegions(request.Scenario);
        var runs = new List<LaterTraceFieldAblationRun>();

        foreach (var profile in profiles)
        {
            var available = profile.AvailableFieldIds.ToHashSet(StringComparer.Ordinal);
            var missing = request.Experiment.Fields
                .Select(item => item.Id)
                .Where(item => !available.Contains(item))
                .OrderBy(item => item, StringComparer.Ordinal)
                .ToArray();
            var derivable = requiredFields.All(available.Contains);
            foreach (var seed in seeds)
            {
                var full = fullRuns[seed];
                var source = derivable ? full : withoutRuns[seed];
                var activeDefinitionIds = derivable
                    ? sourceConstraints
                        .Where(item => item.AppliesToPastStateConditions.Contains(full.EvidenceCondition))
                        .Select(item => item.Id)
                        .OrderBy(item => item, StringComparer.Ordinal)
                        .ToArray()
                    : [];
                var runtime = sourceRuntimeBySeed[seed];
                var evidenceGenerated = derivable && runtime.SourceObservationResolved &&
                    full.QueryUsableFamilyIds.Contains(
                    request.Experiment.SourceObservationKey,
                    StringComparer.Ordinal);
                var generatedConstraintIds = evidenceGenerated
                    ? runtime.ConstraintIds
                    : [];
                runs.Add(new LaterTraceFieldAblationRun
                {
                    ScenarioId = source.ScenarioId,
                    Seed = seed,
                    EvidenceCondition = source.EvidenceCondition,
                    ProfileId = profile.Id,
                    ProfileKind = profile.Kind,
                    IsCore = profile.IsCore,
                    AvailableFieldIds = profile.AvailableFieldIds
                        .Distinct(StringComparer.Ordinal)
                        .OrderBy(item => item, StringComparer.Ordinal)
                        .ToArray(),
                    MissingFieldIds = missing,
                    UnderlyingSourceObservationExists = runtime.SourceObservationId is not null &&
                        full.PresentFamilyIds.Contains(
                        request.Experiment.SourceObservationKey,
                        StringComparer.Ordinal),
                    SourceObservationId = runtime.SourceObservationId,
                    SourceObservationResolved = runtime.SourceObservationResolved,
                    SourceRecordEligibleForDerivation = derivable && runtime.SourceObservationResolved,
                    EvidenceGenerated = evidenceGenerated,
                    ConstraintDerivationSucceeded = evidenceGenerated && generatedConstraintIds.Count > 0,
                    DerivationFailureReason = derivable
                        ? null
                        : $"Required information unavailable: {string.Join(", ", request.Experiment.RequiredFieldIds.Where(item => !available.Contains(item)).OrderBy(item => item, StringComparer.Ordinal))}.",
                    GeneratedConstraintIds = generatedConstraintIds,
                    GeneratedConstraintDefinitionIds = activeDefinitionIds,
                    DeclaredUsageRoles = inventory.DeclaredUsageRoles,
                    AppliedUsageRoles = evidenceGenerated ? inventory.DeclaredUsageRoles : [],
                    TraversalInputGenerated = evidenceGenerated && inventory.DeclaredUsageRoles.Contains(
                        PastStateEvidenceUsageRole.Traversal),
                    SourceBehaviorReferenceProfileId = derivable ? "full" : withoutProfileId,
                    TruthWorldIdentifier = source.TruthWorldIdentifier,
                    PublicTopologyIdentifier = source.PublicTopologyIdentifier,
                    TruthPersonRegionId = source.TruthPersonRegionId,
                    PresentEvidenceSourceGroupCount = source.PresentEvidenceSourceGroupCount,
                    IndependentSourceGroupCount = source.IndependentSourceGroupCount,
                    SameAncestryDerivedRecordCount = source.SameAncestryDerivedRecordCount,
                    ConflictingEvidenceCount = source.ConflictingEvidenceCount,
                    UncertainEvidenceCount = source.UncertainEvidenceCount,
                    UnavailableEvidenceCount = source.UnavailableEvidenceCount,
                    UnresolvedCandidateCount = source.UnresolvedCandidateCount,
                    ConflictedCandidateCount = source.ConflictedCandidateCount,
                    IndependentlySupportedCandidateCount = source.IndependentlySupportedCandidateCount,
                    IndependentlyRefutedCandidateCount = source.IndependentlyRefutedCandidateCount,
                    ConflictingVerificationCandidateCount = source.ConflictingVerificationCandidateCount,
                    UnresolvedVerificationCandidateCount = source.UnresolvedVerificationCandidateCount,
                    SharedAncestryOnlyCandidateCount = source.SharedAncestryOnlyCandidateCount,
                    UnavailableVerificationCandidateCount = source.UnavailableVerificationCandidateCount,
                    CandidateEvaluations = source.CandidateEvaluations,
                    Methods = source.Methods
                        .OrderBy(item => item.Method)
                        .Select(item => new LaterTraceFieldMethodRun
                        {
                            Method = item.Method,
                            CandidateRegionIdsBefore = candidateRegionsBefore,
                            CandidateRegionIdsAfter = item.CandidateRegionIds,
                            ContainsTruth = item.ContainsTruth,
                            FalseExclusion = item.FalseExclusion,
                            CandidateReduction = item.CandidateReduction,
                            CandidateGenerationWork = item.CandidateGenerationWork,
                            CandidateVerificationWork = item.CandidateVerificationWork,
                            ForwardTraversalWork = item.ForwardTraversalWork,
                            BackwardTraversalWork = item.BackwardTraversalWork,
                            PublicRelationsInspected = item.PublicRelationsInspected,
                            EvidenceRecordsInspected = item.EvidenceRecordsInspected,
                            SourceGroupsInspected = item.SourceGroupsInspected,
                            TransitionIds = item.TransitionIds
                        }).ToArray()
                });
            }
        }

        var orderedRuns = runs
            .OrderBy(item => item.ProfileId, StringComparer.Ordinal)
            .ThenBy(item => item.Seed)
            .ToArray();
        var aggregates = AggregateProfiles(profiles, orderedRuns);
        var fullAggregate = aggregates.Single(item => item.ProfileKind == LaterTraceFieldProfileKind.Full);
        var deltas = aggregates
            .SelectMany(item => Deltas(item, fullAggregate))
            .OrderBy(item => item.ProfileId, StringComparer.Ordinal)
            .ThenBy(item => item.Method)
            .ToArray();
        var contributions = request.Experiment.Fields
            .OrderBy(item => item.Id, StringComparer.Ordinal)
            .Select(item =>
            {
                var removalProfile = $"minus-{item.Id}";
                var presenceProfile = $"{item.Id}-only";
                return new LaterTraceFieldContribution
                {
                    FieldId = item.Id,
                    RemovalProfileId = removalProfile,
                    PresenceProfileId = presenceProfile,
                    RemovalPreventsDerivation = aggregates.Single(profile =>
                        profile.ProfileId == removalProfile).DerivationSuccessCount == 0,
                    PresenceEnablesDerivation = aggregates.Single(profile =>
                        profile.ProfileId == presenceProfile).DerivationSuccessCount > 0,
                    RemovalDeltas = deltas.Where(delta => delta.ProfileId == removalProfile).ToArray()
                };
            }).ToArray();
        var equivalent = aggregates.Where(item => item.BaselineEquivalent).ToArray();
        var minimumFieldCount = equivalent.Min(item => item.AvailableFieldIds.Count);
        var minimal = equivalent
            .Where(item => item.AvailableFieldIds.Count == minimumFieldCount)
            .Select(item => item.ProfileId)
            .OrderBy(item => item, StringComparer.Ordinal)
            .ToArray();

        return new LaterTraceFieldAblationEvaluationResult
        {
            Manifest = new LaterTraceFieldAblationManifest
            {
                ExperimentId = request.Experiment.Id,
                ScenarioId = request.Scenario.Id,
                ExperimentContentSha256 = request.ExperimentContentSha256,
                BaseExperimentContentSha256 = request.BaseExperimentContentSha256,
                ScenarioContentSha256 = request.ScenarioContentSha256,
                Seeds = seeds,
                ProfileIds = profiles.Select(item => item.Id).ToArray(),
                ProfileRunCount = orderedRuns.Length,
                MethodRunCount = orderedRuns.Sum(item => item.Methods.Count),
                SourceCommitSha = request.SourceCommitSha,
                DirtyWorkingTree = request.DirtyWorkingTree
            },
            SourceInventory = inventory,
            Fields = request.Experiment.Fields.OrderBy(item => item.Id, StringComparer.Ordinal).ToArray(),
            RequiredFieldIds = request.Experiment.RequiredFieldIds
                .OrderBy(item => item, StringComparer.Ordinal).ToArray(),
            Profiles = profiles,
            Runs = orderedRuns,
            ProfileAggregates = aggregates,
            DeltasFromFull = deltas,
            FieldContributions = contributions,
            CandidateMinimalProfileIds = minimal,
            CandidateMinimalProfileIsUnique = minimal.Length == 1,
            BaselineEquivalenceCriterion = BaselineEquivalenceCriterion,
            InterpretationBoundary = InterpretationBoundary
        };
    }

    private static LaterTraceSourceInventory BuildInventory(
        LaterTraceFieldAblationExperimentDefinition experiment,
        ScenarioDefinition scenario,
        IReadOnlyList<CausalTracingConstraintDefinition> constraints)
    {
        var definition = scenario.PastStateReconstruction!;
        var generation = definition.GenerationConstraintDefinitionIds.ToHashSet(StringComparer.Ordinal);
        var verification = definition.VerificationConstraintDefinitionIds.ToHashSet(StringComparer.Ordinal);
        var traversal = definition.TraversalConstraintDefinitionIds.ToHashSet(StringComparer.Ordinal);
        return new LaterTraceSourceInventory
        {
            SourceObservationKey = experiment.SourceObservationKey,
            ConstraintDefinitionIds = constraints.Select(item => item.Id).ToArray(),
            DirectSourceIds = constraints.Select(item => item.DirectSourceId)
                .Distinct(StringComparer.Ordinal).OrderBy(item => item, StringComparer.Ordinal).ToArray(),
            SourceAncestryIds = constraints.SelectMany(item => item.SourceAncestryIds)
                .Distinct(StringComparer.Ordinal).OrderBy(item => item, StringComparer.Ordinal).ToArray(),
            ObservedSubjectIds = [scenario.TargetPersonId],
            ObserverOrParticipantIds = constraints.Select(item => item.OtherEntityId ?? scenario.TargetPersonId)
                .Distinct(StringComparer.Ordinal).OrderBy(item => item, StringComparer.Ordinal).ToArray(),
            ObservedTimeWindows = constraints.Select(item => new InteractionTimeWindow(
                    item.ObservedStartTick,
                    item.ObservedEndTick))
                .Distinct().OrderBy(item => item.StartTick).ThenBy(item => item.EndTick).ToArray(),
            AvailableAtTicks = constraints.Select(item => item.AvailableAtTick)
                .Distinct().OrderBy(item => item).ToArray(),
            RecordedRegionIds = constraints.SelectMany(item => item.AllowedRegionIds)
                .Distinct(StringComparer.Ordinal).OrderBy(item => item, StringComparer.Ordinal).ToArray(),
            ObservedRelationKinds = [InteractionRelationKind.PersonPersonEncounter.ToString()],
            DerivedRelationKinds = constraints.Select(item => item.RelationKind)
                .Distinct(StringComparer.Ordinal).OrderBy(item => item, StringComparer.Ordinal).ToArray(),
            TransformationKinds = constraints.Select(item => item.DerivationKind)
                .Distinct(StringComparer.Ordinal).OrderBy(item => item, StringComparer.Ordinal).ToArray(),
            EvidenceStates = constraints.Select(item => item.EvidenceState)
                .Distinct().OrderBy(item => item).ToArray(),
            DeclaredUsageRoles = constraints.Select(item => generation.Contains(item.Id)
                    ? PastStateEvidenceUsageRole.CandidateGeneration
                    : verification.Contains(item.Id)
                        ? PastStateEvidenceUsageRole.CandidateVerification
                        : traversal.Contains(item.Id)
                            ? PastStateEvidenceUsageRole.Traversal
                            : PastStateEvidenceUsageRole.EvaluationOnly)
                .Distinct().OrderBy(item => item).ToArray()
        };
    }

    private static IReadOnlyList<CausalTracingConstraintDefinition> SourceConstraints(
        LaterTraceFieldAblationExperimentDefinition experiment,
        ScenarioDefinition scenario) => scenario.PastStateReconstruction!.Constraints
        .Where(item => string.Equals(
            item.SourceObservationKey,
            experiment.SourceObservationKey,
            StringComparison.Ordinal))
        .OrderBy(item => item.Id, StringComparer.Ordinal)
        .ToArray();

    private static IReadOnlyList<string> TargetCandidateRegions(ScenarioDefinition scenario)
    {
        var definition = scenario.PastStateReconstruction!;
        return definition.PublicCandidateStates
            .Where(item => item.StartTick <= definition.TargetEndTick &&
                item.EndTick >= definition.TargetStartTick)
            .SelectMany(item => item.RegionIds)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(item => item, StringComparer.Ordinal)
            .ToArray();
    }

    private static IReadOnlyDictionary<int, RuntimeSourceAudit> BuildRuntimeSourceAudit(
        LaterTraceFieldAblationExperimentDefinition experiment,
        ScenarioDefinition scenario,
        IReadOnlyList<CausalTracingConstraintDefinition> sourceConstraints,
        IReadOnlyList<int> seeds)
    {
        var directSourceIds = sourceConstraints.Select(item => item.DirectSourceId)
            .ToHashSet(StringComparer.Ordinal);
        var engine = new SimulationEngine();
        return seeds.ToDictionary(seed => seed, seed =>
        {
            var simulation = engine.Run(scenario, seed);
            var runtimeConstraints = (simulation.CausalTracingConstraints?.Constraints ?? [])
                .Where(item => directSourceIds.Contains(item.DirectSourceId))
                .OrderBy(item => item.ConstraintId, StringComparer.Ordinal)
                .ToArray();
            var sourceObservationIds = runtimeConstraints.Select(item => item.SourceObservationId)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(item => item, StringComparer.Ordinal)
                .ToArray();
            var observationId = sourceObservationIds.Length == 1 ? sourceObservationIds[0] : null;
            var resolved = observationId is not null &&
                (simulation.InteractionObservations?.Observations ?? []).Any(item =>
                    string.Equals(item.ObservationId, observationId, StringComparison.Ordinal));
            return new RuntimeSourceAudit(
                observationId,
                resolved,
                runtimeConstraints.Select(item => item.ConstraintId).ToArray());
        });
    }

    private static IReadOnlyList<LaterTraceFieldProfileAggregate> AggregateProfiles(
        IReadOnlyList<LaterTraceFieldProfileDefinition> profiles,
        IReadOnlyList<LaterTraceFieldAblationRun> runs)
    {
        var fullRuns = runs.Where(item => item.ProfileKind == LaterTraceFieldProfileKind.Full)
            .ToDictionary(item => item.Seed);
        return profiles.OrderBy(item => item.Id, StringComparer.Ordinal).Select(profile =>
        {
            var profileRuns = runs.Where(item => item.ProfileId == profile.Id).ToArray();
            return new LaterTraceFieldProfileAggregate
            {
                ProfileId = profile.Id,
                ProfileKind = profile.Kind,
                IsCore = profile.IsCore,
                AvailableFieldIds = profile.AvailableFieldIds,
                MissingFieldIds = profileRuns[0].MissingFieldIds,
                SeedCount = profileRuns.Length,
                DerivationSuccessCount = profileRuns.Count(item => item.ConstraintDerivationSucceeded),
                MeanGeneratedConstraintCount = Mean(profileRuns.Select(item => item.GeneratedConstraintIds.Count)),
                SafetyEquivalentToFull = profileRuns.All(item => SafetyEquivalent(item, fullRuns[item.Seed])),
                BaselineEquivalent = profileRuns.All(item => BaselineEquivalent(item, fullRuns[item.Seed])),
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
                    .Select(method => AggregateMethod(profileRuns, method))
                    .ToArray()
            };
        }).ToArray();
    }

    private static LaterTraceFieldMethodAggregate AggregateMethod(
        IReadOnlyList<LaterTraceFieldAblationRun> runs,
        PastStateMethod method)
    {
        var values = runs.Select(item => item.Methods.Single(value => value.Method == method)).ToArray();
        var containment = values.Where(item => item.ContainsTruth.HasValue).ToArray();
        return new LaterTraceFieldMethodAggregate
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
            MeanPublicRelationsInspected = Mean(values.Select(item => item.PublicRelationsInspected)),
            MeanEvidenceRecordsInspected = Mean(values.Select(item => item.EvidenceRecordsInspected)),
            MeanSourceGroupsInspected = Mean(values.Select(item => item.SourceGroupsInspected))
        };
    }

    private static IReadOnlyList<LaterTraceFieldMethodDelta> Deltas(
        LaterTraceFieldProfileAggregate profile,
        LaterTraceFieldProfileAggregate full) => profile.Methods.Select(item =>
    {
        var baseline = full.Methods.Single(value => value.Method == item.Method);
        return new LaterTraceFieldMethodDelta
        {
            ProfileId = profile.ProfileId,
            Method = item.Method,
            MeanCandidateCountDelta = item.MeanCandidateCount - baseline.MeanCandidateCount,
            ContainmentRateDelta = item.ContainmentRate - baseline.ContainmentRate,
            FalseExclusionCountDelta = item.FalseExclusionCount - baseline.FalseExclusionCount,
            MeanReductionDelta = item.MeanReduction - baseline.MeanReduction,
            CandidateGenerationWorkDelta = item.MeanCandidateGenerationWork - baseline.MeanCandidateGenerationWork,
            CandidateVerificationWorkDelta = item.MeanCandidateVerificationWork - baseline.MeanCandidateVerificationWork,
            ForwardTraversalWorkDelta = item.MeanForwardTraversalWork - baseline.MeanForwardTraversalWork,
            BackwardTraversalWorkDelta = item.MeanBackwardTraversalWork - baseline.MeanBackwardTraversalWork,
            PublicRelationsInspectedDelta = item.MeanPublicRelationsInspected - baseline.MeanPublicRelationsInspected,
            EvidenceRecordsInspectedDelta = item.MeanEvidenceRecordsInspected - baseline.MeanEvidenceRecordsInspected
        };
    }).ToArray();

    private static bool SafetyEquivalent(
        LaterTraceFieldAblationRun candidate,
        LaterTraceFieldAblationRun full) => candidate.Methods
        .OrderBy(item => item.Method)
        .Select(item => (item.Method, item.ContainsTruth, item.FalseExclusion))
        .SequenceEqual(full.Methods.OrderBy(item => item.Method).Select(item =>
            (item.Method, item.ContainsTruth, item.FalseExclusion)));

    private static bool BaselineEquivalent(
        LaterTraceFieldAblationRun candidate,
        LaterTraceFieldAblationRun full) => SafetyEquivalent(candidate, full) &&
        candidate.Methods.OrderBy(item => item.Method).Select(item => new
        {
            item.Method,
            Regions = string.Join("|", item.CandidateRegionIdsAfter),
            item.CandidateGenerationWork,
            item.CandidateVerificationWork,
            item.ForwardTraversalWork,
            item.BackwardTraversalWork,
            item.PublicRelationsInspected,
            item.EvidenceRecordsInspected,
            item.SourceGroupsInspected
        }).SequenceEqual(full.Methods.OrderBy(item => item.Method).Select(item => new
        {
            item.Method,
            Regions = string.Join("|", item.CandidateRegionIdsAfter),
            item.CandidateGenerationWork,
            item.CandidateVerificationWork,
            item.ForwardTraversalWork,
            item.BackwardTraversalWork,
            item.PublicRelationsInspected,
            item.EvidenceRecordsInspected,
            item.SourceGroupsInspected
        })) &&
        candidate.CandidateEvaluations.Select(item =>
                (item.RegionId, item.CandidateDisposition, item.VerificationDisposition))
            .SequenceEqual(full.CandidateEvaluations.Select(item =>
                (item.RegionId, item.CandidateDisposition, item.VerificationDisposition)));

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

    private static void Validate(LaterTraceFieldAblationEvaluationRequest request, IReadOnlyList<int> seeds)
    {
        ScenarioValidator.Validate(request.Scenario);
        if (seeds.Count == 0)
        {
            throw new ArgumentException("At least one seed is required.");
        }

        if (!string.Equals(request.Experiment.ScenarioId, request.Scenario.Id, StringComparison.Ordinal) ||
            !string.Equals(request.BaseExperiment.ScenarioId, request.Scenario.Id, StringComparison.Ordinal))
        {
            throw new ArgumentException("Field experiment, base experiment, and scenario identifiers must match.");
        }

        var fieldIds = request.Experiment.Fields.Select(item => item.Id).ToArray();
        if (fieldIds.Length != fieldIds.Distinct(StringComparer.Ordinal).Count() ||
            fieldIds.Any(string.IsNullOrWhiteSpace))
        {
            throw new InvalidDataException("Information fields must be non-empty and uniquely identified.");
        }

        var required = request.Experiment.RequiredFieldIds.ToHashSet(StringComparer.Ordinal);
        if (required.Count == 0 || required.Any(item => !fieldIds.Contains(item, StringComparer.Ordinal)))
        {
            throw new InvalidDataException("Required fields must refer to declared information fields.");
        }

        var profileIds = request.Experiment.Profiles.Select(item => item.Id).ToArray();
        if (profileIds.Length == 0 || profileIds.Length != profileIds.Distinct(StringComparer.Ordinal).Count())
        {
            throw new InvalidDataException("Field profiles must be non-empty and uniquely identified.");
        }

        foreach (var profile in request.Experiment.Profiles)
        {
            if (profile.AvailableFieldIds.Distinct(StringComparer.Ordinal).Count() !=
                profile.AvailableFieldIds.Count ||
                profile.AvailableFieldIds.Any(item => !fieldIds.Contains(item, StringComparer.Ordinal)))
            {
                throw new InvalidDataException($"Field profile '{profile.Id}' is invalid.");
            }
        }

        if (request.Experiment.Profiles.Count(item => item.Kind == LaterTraceFieldProfileKind.Full) != 1 ||
            request.Experiment.Profiles.Count(item => item.Kind == LaterTraceFieldProfileKind.None) != 1)
        {
            throw new InvalidDataException("Exactly one Full and one None field profile are required.");
        }

        var fullProfile = request.Experiment.Profiles.Single(item => item.Kind == LaterTraceFieldProfileKind.Full);
        var noneProfile = request.Experiment.Profiles.Single(item => item.Kind == LaterTraceFieldProfileKind.None);
        if (!fullProfile.AvailableFieldIds.ToHashSet(StringComparer.Ordinal).SetEquals(fieldIds) ||
            noneProfile.AvailableFieldIds.Count != 0)
        {
            throw new InvalidDataException("Full must contain every field and None must contain no fields.");
        }

        foreach (var fieldId in fieldIds)
        {
            if (!profileIds.Contains($"minus-{fieldId}", StringComparer.Ordinal) ||
                !profileIds.Contains($"{fieldId}-only", StringComparer.Ordinal))
            {
                throw new InvalidDataException($"Removal and presence profiles are required for '{fieldId}'.");
            }


            var removal = request.Experiment.Profiles.Single(item => item.Id == $"minus-{fieldId}");
            var presence = request.Experiment.Profiles.Single(item => item.Id == $"{fieldId}-only");
            if (removal.Kind != LaterTraceFieldProfileKind.SingleFieldRemoval ||
                !removal.AvailableFieldIds.ToHashSet(StringComparer.Ordinal).SetEquals(
                    fieldIds.Where(item => !string.Equals(item, fieldId, StringComparison.Ordinal))) ||
                presence.Kind != LaterTraceFieldProfileKind.SingleFieldPresence ||
                !presence.AvailableFieldIds.SequenceEqual([fieldId]))
            {
                throw new InvalidDataException($"Removal and presence profiles for '{fieldId}' are malformed.");
            }
        }

        var baseFamily = request.BaseExperiment.Families.SingleOrDefault(item => item.SourceObservationKeys.Contains(
            request.Experiment.SourceObservationKey,
            StringComparer.Ordinal));
        if (baseFamily is null || !string.Equals(
            baseFamily.Id,
            request.Experiment.SourceObservationKey,
            StringComparison.Ordinal))
        {
            throw new InvalidDataException("The field source must map to the matching M1-F family.");
        }

        var constraints = SourceConstraints(request.Experiment, request.Scenario);
        if (constraints.Count == 0)
        {
            throw new InvalidDataException("The field source has no scenario-declared constraints.");
        }

        var generation = request.Scenario.PastStateReconstruction!.GenerationConstraintDefinitionIds
            .ToHashSet(StringComparer.Ordinal);
        if (constraints.Any(item => !generation.Contains(item.Id)))
        {
            throw new InvalidDataException("This experiment is limited to a generation-only source family.");
        }

        if (constraints.Any(item => item.SourceKind != TracingConstraintSourceKind.CrossThreadObservation))
        {
            throw new InvalidDataException("This experiment requires a public interaction-observation source.");
        }

        if (string.IsNullOrWhiteSpace(request.SourceCommitSha) ||
            string.IsNullOrWhiteSpace(request.ExperimentContentSha256) ||
            string.IsNullOrWhiteSpace(request.BaseExperimentContentSha256) ||
            string.IsNullOrWhiteSpace(request.ScenarioContentSha256))
        {
            throw new ArgumentException("Source commit and input hashes are required.");
        }
    }

    private sealed record RuntimeSourceAudit(
        string? SourceObservationId,
        bool SourceObservationResolved,
        IReadOnlyList<string> ConstraintIds);
}
