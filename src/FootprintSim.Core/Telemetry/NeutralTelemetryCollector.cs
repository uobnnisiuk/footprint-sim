using System.Text.Json;
using FootprintSim.Evaluation;
using FootprintSim.Events;
using FootprintSim.Interactions;
using FootprintSim.Interactions.CandidateWarps;
using FootprintSim.Interactions.Tracing;
using FootprintSim.Interactions.Truth;
using FootprintSim.Scenarios;
using FootprintSim.Simulation;

namespace FootprintSim.Telemetry;

internal sealed class NeutralTelemetryCollector
{
    private readonly List<WorldEventTelemetryRecord> _worldEvents = [];
    private readonly List<TruthInteractionTelemetryRecord> _truthInteractions = [];
    private readonly List<ObservationTelemetryRecord> _observations = [];
    private readonly List<InteractionObservationTelemetryRecord> _interactionObservations = [];
    private readonly List<ConstraintTelemetryRecord> _constraints = [];
    private readonly List<ConstraintApplicationTelemetryRecord> _applications = [];
    private readonly List<CandidateTransitionTelemetryRecord> _candidateTransitions = [];
    private readonly List<TracingConstraintTelemetryRecord> _tracingConstraints = [];
    private readonly List<CausalTracingStepTelemetryRecord> _tracingSteps = [];

    public void AddSimulation(ScenarioDefinition scenario, SimulationRun run)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        ArgumentNullException.ThrowIfNull(run);
        var entityKinds = EntityKinds(scenario);

        _worldEvents.AddRange(run.GroundTruthEvents.Select(worldEvent =>
            WorldEvent(run, worldEvent, entityKinds)));
        _truthInteractions.AddRange(run.GroundTruthInteractionOccurrences.Select(
            TruthInteraction));
        _observations.AddRange(run.ObservationSet.Observations.Select(observation =>
            Observation(run, observation, entityKinds)));
        _interactionObservations.AddRange(
            (run.InteractionObservations?.Observations ?? []).Select(InteractionObservation));
        _constraints.AddRange(
            (run.CrossThreadConstraints?.Constraints ?? []).Select(Constraint));
        _tracingConstraints.AddRange(
            (run.CausalTracingConstraints?.Constraints ?? []).Select(TracingConstraint));
    }

    public void AddEvaluation(
        string targetEntityId,
        IReadOnlyList<SearchStrategyResult> timeline)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(targetEntityId);
        ArgumentNullException.ThrowIfNull(timeline);
        var ordered = timeline.OrderBy(result => result.ProducedAtTick).ToArray();
        SearchStrategyResult? previous = null;
        foreach (var result in ordered)
        {
            _applications.AddRange(result.CrossThreadConstraintAudits.Select(audit =>
                ConstraintApplication(result, audit)));
            _candidateTransitions.Add(CandidateTransition(targetEntityId, previous, result));
            if (result.CausalTracingResult is { } tracing)
            {
                _tracingSteps.AddRange(tracing.Steps.Select(step =>
                    CausalTracingStep(result, step)));
            }
            previous = result;
        }
    }

    public NeutralTelemetryBundle Build(
        IReadOnlyList<EvaluationScenarioInput> scenarios,
        IReadOnlyList<int> seeds,
        IReadOnlyList<ISearchStrategy> strategies,
        int evaluationRunCount,
        string sourceCommitSha,
        bool dirtyWorkingTree)
    {
        var truthEvents = _worldEvents
            .DistinctBy(record => (record.ScenarioId, record.Seed, record.EventId))
            .OrderBy(record => record.ScenarioId, StringComparer.Ordinal)
            .ThenBy(record => record.Seed)
            .ThenBy(record => record.OccurrenceTimeWindow.StartTick)
            .ThenBy(record => record.Sequence)
            .ThenBy(record => record.EventId, StringComparer.Ordinal)
            .ToArray();
        var truthInteractions = _truthInteractions
            .DistinctBy(record => (record.ScenarioId, record.Seed, record.InteractionId))
            .OrderBy(record => record.ScenarioId, StringComparer.Ordinal)
            .ThenBy(record => record.Seed)
            .ThenBy(record => record.OccurrenceTimeWindow.StartTick)
            .ThenBy(record => record.OccurrenceTimeWindow.EndTick)
            .ThenBy(record => record.InteractionId, StringComparer.Ordinal)
            .ToArray();
        var observations = _observations
            .DistinctBy(record => (record.ScenarioId, record.Seed, record.ObservationId))
            .OrderBy(record => record.ScenarioId, StringComparer.Ordinal)
            .ThenBy(record => record.Seed)
            .ThenBy(record => record.AvailableAtTick)
            .ThenBy(record => record.ObservedTimeWindow.StartTick)
            .ThenBy(record => record.ObservationId, StringComparer.Ordinal)
            .ToArray();
        var interactionObservations = _interactionObservations
            .DistinctBy(record => (record.ScenarioId, record.Seed, record.ObservationId))
            .OrderBy(record => record.ScenarioId, StringComparer.Ordinal)
            .ThenBy(record => record.Seed)
            .ThenBy(record => record.AvailableAtTick)
            .ThenBy(record => record.ObservedTimeWindow.StartTick)
            .ThenBy(record => record.ObservedTimeWindow.EndTick)
            .ThenBy(record => record.ObservationId, StringComparer.Ordinal)
            .ToArray();
        var constraints = _constraints
            .DistinctBy(record => (record.ScenarioId, record.Seed, record.ConstraintId))
            .OrderBy(record => record.ScenarioId, StringComparer.Ordinal)
            .ThenBy(record => record.Seed)
            .ThenBy(record => record.AvailableAtTick)
            .ThenBy(record => record.ConstrainedTimeWindow.StartTick)
            .ThenBy(record => record.ConstrainedTimeWindow.EndTick)
            .ThenBy(record => record.SourceObservationId, StringComparer.Ordinal)
            .ThenBy(record => record.ConstraintId, StringComparer.Ordinal)
            .ToArray();
        var applications = _applications
            .DistinctBy(record =>
                (record.ScenarioId, record.Seed, record.StrategyId, record.ApplicationId))
            .OrderBy(record => record.ScenarioId, StringComparer.Ordinal)
            .ThenBy(record => record.Seed)
            .ThenBy(record => record.StrategyId, StringComparer.Ordinal)
            .ThenBy(record => record.ApplicationTick)
            .ThenBy(record => record.SourceObservationId, StringComparer.Ordinal)
            .ThenBy(record => record.ConstraintId, StringComparer.Ordinal)
            .ThenBy(record => record.CandidateWarpId, StringComparer.Ordinal)
            .ThenBy(record => record.ApplicationId, StringComparer.Ordinal)
            .ToArray();
        var tracingConstraints = _tracingConstraints
            .DistinctBy(record => (record.ScenarioId, record.Seed, record.ConstraintId))
            .OrderBy(record => record.ScenarioId, StringComparer.Ordinal)
            .ThenBy(record => record.Seed)
            .ThenBy(record => record.AvailableAtTick)
            .ThenBy(record => record.ConstrainedTimeWindow.StartTick)
            .ThenBy(record => record.SourceObservationId, StringComparer.Ordinal)
            .ThenBy(record => record.ConstraintId, StringComparer.Ordinal)
            .ToArray();
        var tracingSteps = _tracingSteps
            .DistinctBy(record =>
                (record.ScenarioId, record.Seed, record.StrategyId, record.SessionId, record.StepId))
            .OrderBy(record => record.ScenarioId, StringComparer.Ordinal)
            .ThenBy(record => record.Seed)
            .ThenBy(record => record.StrategyId, StringComparer.Ordinal)
            .ThenBy(record => record.SessionId, StringComparer.Ordinal)
            .ThenBy(record => record.StepNumber)
            .ThenBy(record => record.StepId, StringComparer.Ordinal)
            .ToArray();
        var transitions = _candidateTransitions
            .DistinctBy(record =>
                (record.ScenarioId, record.Seed, record.StrategyId, record.TransitionId))
            .OrderBy(record => record.ScenarioId, StringComparer.Ordinal)
            .ThenBy(record => record.Seed)
            .ThenBy(record => record.StrategyId, StringComparer.Ordinal)
            .ThenBy(record => record.TransitionTimeWindow.StartTick)
            .ThenBy(record => record.TransitionId, StringComparer.Ordinal)
            .ToArray();
        var categories = new[]
        {
            Category("worldEvents", TelemetryStreamKind.TruthWorld, TelemetryOutputNames.WorldEvents, truthEvents.Length),
            Category("truthInteractions", TelemetryStreamKind.TruthWorld, TelemetryOutputNames.TruthInteractions, truthInteractions.Length),
            Category("observations", TelemetryStreamKind.Evidence, TelemetryOutputNames.Observations, observations.Length),
            Category("interactionObservations", TelemetryStreamKind.Evidence, TelemetryOutputNames.InteractionObservations, interactionObservations.Length),
            Category("constraints", TelemetryStreamKind.Evidence, TelemetryOutputNames.Constraints, constraints.Length),
            Category("tracingConstraints", TelemetryStreamKind.Evidence, TelemetryOutputNames.TracingConstraints, tracingConstraints.Length),
            Category("constraintApplications", TelemetryStreamKind.Evaluation, TelemetryOutputNames.ConstraintApplications, applications.Length),
            Category("candidateTransitions", TelemetryStreamKind.Evaluation, TelemetryOutputNames.CandidateTransitions, transitions.Length),
            Category("causalTracingSteps", TelemetryStreamKind.Evaluation, TelemetryOutputNames.CausalTracingSteps, tracingSteps.Length)
        };

        return new NeutralTelemetryBundle
        {
            Truth = new TruthTelemetryStream
            {
                WorldEvents = truthEvents,
                Interactions = truthInteractions
            },
            Evidence = new EvidenceTelemetryStream
            {
                Observations = observations,
                InteractionObservations = interactionObservations,
                Constraints = constraints,
                TracingConstraints = tracingConstraints
            },
            Evaluation = new EvaluationTelemetryStream
            {
                ConstraintApplications = applications,
                CandidateTransitions = transitions,
                CausalTracingSteps = tracingSteps
            },
            Manifest = new TelemetryManifest
            {
                SourceCommitSha = sourceCommitSha,
                DirtyWorkingTree = dirtyWorkingTree,
                ScenarioInputs = scenarios
                    .OrderBy(item => item.Scenario.Id, StringComparer.Ordinal)
                    .Select(item => new TelemetryScenarioInput
                    {
                        ScenarioId = item.Scenario.Id,
                        ContentSha256 = item.ContentSha256
                    })
                    .ToArray(),
                Seeds = seeds.Distinct().OrderBy(seed => seed).ToArray(),
                StrategyIdentifiers = strategies.Select(strategy => strategy.Id)
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(strategyId => strategyId, StringComparer.Ordinal)
                    .ToArray(),
                EvaluationRunCount = evaluationRunCount,
                RecordCategories = categories,
                RawConstraintRecordCount = constraints.Length,
                UniqueConstraintSourceObservationCount = constraints
                    .Select(record => (record.ScenarioId, record.Seed, record.SourceObservationId))
                    .Distinct()
                    .Count(),
                RawTracingConstraintRecordCount = tracingConstraints.Length,
                UniqueTracingSourceObservationCount = tracingConstraints
                    .Select(record => (record.ScenarioId, record.Seed, record.SourceObservationId))
                    .Distinct()
                    .Count(),
                DeterministicOrderContract =
                [
                    "scenarioId ordinal ascending",
                    "seed numeric ascending",
                    "strategyId ordinal ascending where applicable",
                    "time window start/end numeric ascending",
                    "stable source and record identifiers ordinal ascending",
                    "nested entity, region, ancestry, provenance, and candidate collections normalized"
                ]
            }
        };
    }

    private static TelemetryRecordCategoryManifest Category(
        string category,
        TelemetryStreamKind stream,
        string relativePath,
        int count) => new()
        {
            Category = category,
            Stream = stream,
            RelativePath = relativePath,
            RecordCount = count
        };

    private static WorldEventTelemetryRecord WorldEvent(
        SimulationRun run,
        SimulationEvent worldEvent,
        IReadOnlyDictionary<string, string> entityKinds)
    {
        var entities = worldEvent.SubjectIds
            .Select(entityId => Entity(entityId, Kind(entityKinds, entityId), "subject"))
            .Concat(worldEvent.RelatedEntityIds.Select(entityId =>
                Entity(entityId, Kind(entityKinds, entityId), "related")))
            .Concat(worldEvent.RelatedPositions.Keys.Select(entityId =>
                Entity(entityId, Kind(entityKinds, entityId), "relatedPosition")))
            .Distinct()
            .OrderBy(entity => entity.Role, StringComparer.Ordinal)
            .ThenBy(entity => entity.EntityKind, StringComparer.Ordinal)
            .ThenBy(entity => entity.EntityId, StringComparer.Ordinal)
            .ToArray();
        var regions = new[] { worldEvent.RegionId, worldEvent.Position?.RegionId }
            .Concat(worldEvent.RelatedPositions.Values.Select(position => position.RegionId))
            .Where(regionId => !string.IsNullOrWhiteSpace(regionId))
            .Select(regionId => regionId!)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(regionId => regionId, StringComparer.Ordinal)
            .ToArray();

        return new WorldEventTelemetryRecord
        {
            ScenarioId = run.ScenarioId,
            Seed = run.Seed,
            EventId = worldEvent.Id,
            Sequence = worldEvent.Sequence,
            EventKind = worldEvent.Kind.ToString(),
            MovementKind = worldEvent.MovementKind?.ToString(),
            OccurrenceTimeWindow = At(worldEvent.Tick),
            Entities = entities,
            RegionIds = regions,
            CausalReferenceIds = worldEvent.CauseEventId is null
                ? []
                : [worldEvent.CauseEventId],
            Provenance =
            [
                new TelemetryProvenanceReference
                {
                    Kind = "worldEvent",
                    SourceIdentifier = worldEvent.Id
                }
            ],
            Payload = SortedPayload(worldEvent.Payload)
        };
    }

    private static TruthInteractionTelemetryRecord TruthInteraction(
        InteractionOccurrence occurrence) => new()
        {
            ScenarioId = occurrence.ScenarioId,
            Seed = occurrence.Seed,
            InteractionId = occurrence.InteractionId,
            RelationKind = occurrence.RelationKind.ToString(),
            Participants = Participants(occurrence.Participants),
            OccurrenceTimeWindow = Window(occurrence.OccurrenceTimeWindow),
            RegionIds = Ordered(occurrence.RegionIds),
            SourceEventId = occurrence.SourceEventId,
            CausalReferenceIds = occurrence.CausalParentEventId is null
                ? []
                : [occurrence.CausalParentEventId],
            Provenance = Provenance(occurrence.Provenance)
        };

    private static ObservationTelemetryRecord Observation(
        SimulationRun run,
        Evidence.ObservationRecord observation,
        IReadOnlyDictionary<string, string> entityKinds)
    {
        var hasPerson = observation.SubjectIds.Any(entityId =>
            string.Equals(Kind(entityKinds, entityId), "person", StringComparison.Ordinal));
        var hasDevice = observation.SubjectIds.Any(entityId =>
            string.Equals(Kind(entityKinds, entityId), "device", StringComparison.Ordinal));
        return new ObservationTelemetryRecord
        {
            ScenarioId = run.ScenarioId,
            Seed = run.Seed,
            ObservationId = observation.ObservationId,
            ObservationKind = observation.EvidenceType,
            ObservedTimeWindow = At(observation.ObservedTick),
            AvailableAtTick = observation.AvailableTick,
            ObservedEntities = observation.SubjectIds
                .Select(entityId => Entity(
                    entityId,
                    Kind(entityKinds, entityId),
                    "observedSubject"))
                .OrderBy(entity => entity.EntityKind, StringComparer.Ordinal)
                .ThenBy(entity => entity.EntityId, StringComparer.Ordinal)
                .ToArray(),
            ObservedRegionIds = Ordered(new[]
            {
                observation.ReportedRegionId,
                observation.ReportedPosition?.RegionId
            }.Where(regionId => !string.IsNullOrWhiteSpace(regionId)).Select(regionId => regionId!)),
            EvidenceState = observation.State,
            IsPersonEvidence = hasPerson,
            IsDeviceOnlyEvidence = hasDevice && !hasPerson,
            SourceAncestryIds = [],
            Provenance =
            [
                new TelemetryProvenanceReference
                {
                    Kind = "evidenceRecord",
                    SourceIdentifier = observation.ObservationId
                }
            ],
            Payload = SortedPayload(observation.PublicPayload)
        };
    }

    private static InteractionObservationTelemetryRecord InteractionObservation(
        Interactions.InteractionObservation observation) => new()
        {
            ScenarioId = observation.ScenarioId,
            Seed = observation.Seed,
            ObservationId = observation.ObservationId,
            SourceEvidenceIdentifier = observation.SourceEvidenceIdentifier,
            ReferencedInteractionId = observation.ReferencedInteractionId,
            ObservedRelationKind = observation.ObservedRelationKind.ToString(),
            ObservedParticipants = Participants(observation.ObservedParticipants),
            ObservedTimeWindow = Window(observation.ObservedTimeWindow),
            ObservedRegionIds = Ordered(observation.ObservedRegionIds),
            AvailableAtTick = observation.AvailableAtTick,
            EvidenceState = observation.EvidenceBoundaryState,
            IsPersonEvidence = observation.IsPersonEvidence,
            IsDeviceOnlyEvidence = observation.IsDeviceOnlyEvidence,
            Visibility = Visibility(observation.Visibility),
            SourceAncestryIds = observation.Provenance
                .Select(item => item.SourceIdentifier)
                .Append(observation.SourceEvidenceIdentifier)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(item => item, StringComparer.Ordinal)
                .ToArray(),
            Provenance = Provenance(observation.Provenance)
        };

    private static ConstraintTelemetryRecord Constraint(CrossThreadConstraint constraint) => new()
    {
        ScenarioId = constraint.ScenarioId,
        Seed = constraint.Seed,
        ConstraintId = constraint.ConstraintId,
        SourceObservationId = constraint.SourceObservationId,
        SourceEvidenceIdentifier = constraint.SourceEvidenceIdentifier,
        TargetEntityId = constraint.TargetEntityId,
        SourceRelationKind = constraint.SourceRelationKind.ToString(),
        SourceParticipants = Participants(constraint.SourceParticipants),
        SourceObservedTimeWindow = Window(constraint.SourceObservedTimeWindow),
        ConstrainedTimeWindow = Window(constraint.ConstrainedTimeWindow),
        AllowedRegionIds = Ordered(constraint.AllowedRegionIds),
        AvailableAtTick = constraint.AvailableAtTick,
        EvidenceState = constraint.EvidenceBoundaryState,
        IsPersonEvidence = constraint.IsPersonEvidence,
        IsDeviceOnlyEvidence = constraint.IsDeviceOnlyEvidence,
        DerivationKind = constraint.DerivationKind.ToString(),
        Visibility = Visibility(constraint.Visibility),
        SourceAncestryIds = constraint.Provenance
            .Select(item => item.SourceIdentifier)
            .Append(constraint.SourceObservationId)
            .Append(constraint.SourceEvidenceIdentifier)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(item => item, StringComparer.Ordinal)
            .ToArray(),
        Provenance = Provenance(constraint.Provenance)
    };

    private static TracingConstraintTelemetryRecord TracingConstraint(
        CausalTracingConstraint constraint) => new()
        {
            ScenarioId = constraint.ScenarioId,
            Seed = constraint.Seed,
            ConstraintId = constraint.ConstraintId,
            SourceObservationId = constraint.SourceObservationId,
            DirectSourceId = constraint.DirectSourceId,
            SourceAncestryIds = Ordered(constraint.SourceAncestryIds),
            TargetEntityId = constraint.TargetEntityId,
            SourceKind = constraint.SourceKind.ToString(),
            RelationKind = constraint.RelationKind,
            Participants = Participants(constraint.Participants),
            TracingRole = constraint.Role.ToString(),
            ObservedTimeWindow = Window(constraint.SourceObservedTimeWindow),
            ConstrainedTimeWindow = Window(constraint.ConstrainedTimeWindow),
            AllowedRegionIds = Ordered(constraint.AllowedRegionIds),
            AvailableAtTick = constraint.AvailableAtTick,
            EvidenceState = constraint.EvidenceBoundaryState,
            IsPersonEvidence = constraint.IsPersonEvidence,
            IsDeviceOnlyEvidence = constraint.IsDeviceOnlyEvidence,
            DerivationKind = constraint.DerivationKind,
            Visibility = Visibility(constraint.Visibility),
            Provenance = Provenance(constraint.Provenance)
        };

    private static ConstraintApplicationTelemetryRecord ConstraintApplication(
        SearchStrategyResult result,
        ConstraintApplicationAudit audit) => new()
        {
            ScenarioId = result.ScenarioId,
            Seed = result.Seed,
            StrategyId = result.StrategyId,
            ApplicationId = audit.AuditId,
            CandidateWarpId = audit.CandidateWarpId,
            ConstraintId = audit.ConstraintId,
            SourceObservationId = audit.SourceObservationId,
            RelationKind = audit.RelationKind.ToString(),
            TargetEntityId = audit.TargetEntityId,
            ApplicationTick = audit.ApplicationTick,
            QueryAccessContextId = audit.QueryContextId,
            ConstrainedTimeWindow = Window(audit.ConstrainedTimeWindow),
            SourceObservedTimeWindow = Window(audit.SourceObservedTimeWindow),
            SourceParticipants = Participants(audit.SourceParticipants),
            EvidenceState = audit.EvidenceBoundaryState,
            Disposition = audit.Disposition,
            Reason = audit.Reason,
            CandidateCountBefore = audit.CandidateCountBefore,
            CandidateCountAfter = audit.CandidateCountAfter,
            AlternativesRetained = audit.AlternativesRetained,
            ConflictDetected = audit.ConflictDetected,
            SourceAncestryIds = Ordered(audit.SourceAncestryIds),
            Provenance = Provenance(audit.Provenance)
        };

    private static CausalTracingStepTelemetryRecord CausalTracingStep(
        SearchStrategyResult result,
        CausalTracingStepAudit step) => new()
        {
            ScenarioId = result.ScenarioId,
            Seed = result.Seed,
            StrategyId = result.StrategyId,
            SessionId = step.SessionId,
            QueryContextId = step.QueryContextId,
            StepId = step.StepId,
            StepNumber = step.StepNumber,
            Direction = step.Direction,
            Role = step.Role,
            TimeWindow = Window(step.TimeWindow),
            TargetEntityId = step.TargetEntityId,
            ConstraintId = step.ConstraintId,
            SourceObservationId = step.SourceObservationId,
            SourceGroupId = step.SourceGroupId,
            RelationKind = step.RelationKind,
            EvidenceState = step.EvidenceState,
            Disposition = step.Disposition,
            Reason = step.Reason,
            CandidateInputRegionIds = Ordered(step.CandidateInputRegionIds),
            CandidateOutputRegionIds = Ordered(step.CandidateOutputRegionIds),
            AppliedConstraintIds = Ordered(step.AppliedConstraintIds),
            SkippedConstraintIds = Ordered(step.SkippedConstraintIds),
            SupportingSourceGroupIds = Ordered(step.SupportingSourceGroupIds),
            ConflictingSourceGroupIds = Ordered(step.ConflictingSourceGroupIds),
            AlternativesRetained = step.AlternativesRetained,
            ConflictDetected = step.ConflictDetected,
            SourceAncestryIds = Ordered(step.SourceAncestryIds),
            Provenance = Provenance(step.Provenance)
        };

    private static CandidateTransitionTelemetryRecord CandidateTransition(
        string targetEntityId,
        SearchStrategyResult? before,
        SearchStrategyResult after)
    {
        var beforeId = before is null ? null : CandidateSetId(before);
        var afterId = CandidateSetId(after);
        var audits = after.CrossThreadConstraintAudits;
        var provenance = after.CandidateRegionProvenance
            .Distinct()
            .OrderBy(item => item.RegionId, StringComparer.Ordinal)
            .ThenBy(item => item.SourceKind)
            .ThenBy(item => item.SourceIdentifier, StringComparer.Ordinal)
            .Select(item => new CandidateProvenanceTelemetryRecord
            {
                RegionId = item.RegionId,
                SourceKind = item.SourceKind.ToString(),
                SourceIdentifier = item.SourceIdentifier,
                AvailableAtTick = item.AvailableAtTick,
                IsPersonEvidence = item.IsPersonEvidence,
                IsDeviceOnlyEvidence = item.IsDeviceOnlyEvidence,
                EvidenceState = item.EvidenceBoundaryState
            })
            .ToArray();
        var triggerReferences = audits.Select(audit => audit.AuditId)
            .Concat(audits.Select(audit => audit.ConstraintId))
            .Concat(provenance.Select(item => item.SourceIdentifier))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(item => item, StringComparer.Ordinal)
            .ToArray();
        var transitionId = InteractionStableIdentifier.Create(
            "cst",
            after.ScenarioId,
            InteractionStableIdentifier.Seed(after.Seed),
            after.StrategyId,
            InteractionStableIdentifier.Tick(after.ProducedAtTick),
            beforeId ?? "no-before-candidate-set",
            afterId);

        return new CandidateTransitionTelemetryRecord
        {
            ScenarioId = after.ScenarioId,
            Seed = after.Seed,
            StrategyId = after.StrategyId,
            TransitionId = transitionId,
            CandidateSetIdBefore = beforeId,
            CandidateSetIdAfter = afterId,
            TargetEntityId = targetEntityId,
            TransitionTimeWindow = At(after.ProducedAtTick),
            CandidateRegionsBefore = before is null ? [] : Ordered(before.CandidateRegionIds),
            CandidateRegionsAfter = Ordered(after.CandidateRegionIds),
            PreferredRegionsBefore = before is null
                ? []
                : Ordered(before.PreferredCandidateRegionIds),
            PreferredRegionsAfter = Ordered(after.PreferredCandidateRegionIds),
            OuterPlausibleRegionsBefore = before is null
                ? []
                : Ordered(before.OuterPlausibleCandidateRegionIds),
            OuterPlausibleRegionsAfter = Ordered(after.OuterPlausibleCandidateRegionIds),
            ConflictedRegionsBefore = before is null
                ? []
                : Ordered(before.ConflictedCandidateRegionIds),
            ConflictedRegionsAfter = Ordered(after.ConflictedCandidateRegionIds),
            EvidenceStateBefore = before?.EvidenceBoundaryState,
            EvidenceStateAfter = after.EvidenceBoundaryState,
            TriggerReferenceIds = triggerReferences,
            QueryAccessContextIds = audits.Select(audit => audit.QueryContextId)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(item => item, StringComparer.Ordinal)
                .ToArray(),
            SourceAncestryIds = audits.SelectMany(audit => audit.SourceAncestryIds)
                .Concat(provenance.Select(item => item.SourceIdentifier))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(item => item, StringComparer.Ordinal)
                .ToArray(),
            Provenance = provenance
        };
    }

    private static string CandidateSetId(SearchStrategyResult result) =>
        InteractionStableIdentifier.Create(
            "cset",
            result.ScenarioId,
            InteractionStableIdentifier.Seed(result.Seed),
            result.StrategyId,
            InteractionStableIdentifier.Tick(result.ProducedAtTick),
            result.EvidenceBoundaryState.ToString(),
            Joined(result.CandidateRegionIds),
            Joined(result.PreferredCandidateRegionIds),
            Joined(result.OuterPlausibleCandidateRegionIds),
            Joined(result.ConflictedCandidateRegionIds));

    private static string Joined(IEnumerable<string> values)
    {
        var ordered = Ordered(values);
        return ordered.Count == 0 ? "none" : string.Join("|", ordered);
    }

    private static TelemetryTimeWindow At(long tick) => new()
    {
        StartTick = tick,
        EndTick = tick,
        TemporalKind = TelemetryTemporalKind.ExactEventPoint
    };

    private static TelemetryTimeWindow Window(InteractionTimeWindow window) => new()
    {
        StartTick = window.StartTick,
        EndTick = window.EndTick,
        TemporalKind = window.StartTick == window.EndTick
            ? TelemetryTemporalKind.ExactEventPoint
            : TelemetryTemporalKind.InclusiveStateInterval
    };

    private static IReadOnlyList<TelemetryEntityReference> Participants(
        IEnumerable<InteractionParticipant> participants) => participants
        .Select(participant => Entity(
            participant.EntityId,
            participant.EntityKind.ToString(),
            participant.Role.ToString()))
        .Distinct()
        .OrderBy(participant => participant.Role, StringComparer.Ordinal)
        .ThenBy(participant => participant.EntityKind, StringComparer.Ordinal)
        .ThenBy(participant => participant.EntityId, StringComparer.Ordinal)
        .ToArray();

    private static IReadOnlyList<TelemetryProvenanceReference> Provenance(
        IEnumerable<InteractionProvenance> provenance) => provenance
        .Select(item => new TelemetryProvenanceReference
        {
            Kind = item.Kind.ToString(),
            SourceIdentifier = item.SourceIdentifier
        })
        .Distinct()
        .OrderBy(item => item.Kind, StringComparer.Ordinal)
        .ThenBy(item => item.SourceIdentifier, StringComparer.Ordinal)
        .ToArray();

    private static TelemetryVisibility Visibility(InteractionVisibility visibility) =>
        visibility switch
        {
            InteractionVisibility.PrivateTruth => TelemetryVisibility.PrivateTruth,
            InteractionVisibility.PublicEvidence => TelemetryVisibility.PublicEvidence,
            InteractionVisibility.PrivateEvidence => TelemetryVisibility.PrivateEvidence,
            _ => throw new ArgumentOutOfRangeException(nameof(visibility))
        };

    private static TelemetryEntityReference Entity(string id, string kind, string role) => new()
    {
        EntityId = id,
        EntityKind = kind,
        Role = role
    };

    private static IReadOnlyDictionary<string, string> EntityKinds(ScenarioDefinition scenario)
    {
        var kinds = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (var entity in scenario.Entities.Persons)
        {
            kinds.Add(entity.Id, "person");
        }

        foreach (var entity in scenario.Entities.Devices)
        {
            kinds.Add(entity.Id, "device");
        }

        foreach (var entity in scenario.Entities.Carriers)
        {
            kinds.Add(entity.Id, "carrier");
        }

        foreach (var entity in scenario.Entities.Rescuers)
        {
            kinds.Add(entity.Id, "rescuer");
        }

        foreach (var entity in scenario.Entities.Hazards)
        {
            kinds.Add(entity.Id, "hazard");
        }

        return kinds;
    }

    private static string Kind(IReadOnlyDictionary<string, string> kinds, string entityId) =>
        kinds.GetValueOrDefault(entityId) ?? "unknown";

    private static IReadOnlyList<string> Ordered(IEnumerable<string> values) => values
        .Where(value => !string.IsNullOrWhiteSpace(value))
        .Distinct(StringComparer.Ordinal)
        .OrderBy(value => value, StringComparer.Ordinal)
        .ToArray();

    private static IReadOnlyDictionary<string, JsonElement> SortedPayload(
        IReadOnlyDictionary<string, JsonElement> payload)
    {
        var sorted = new SortedDictionary<string, JsonElement>(StringComparer.Ordinal);
        foreach (var item in payload.OrderBy(item => item.Key, StringComparer.Ordinal))
        {
            sorted.Add(item.Key, item.Value);
        }

        return sorted;
    }
}
