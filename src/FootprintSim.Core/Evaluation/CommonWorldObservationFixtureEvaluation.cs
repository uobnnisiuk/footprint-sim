using FootprintSim.Evidence;
using FootprintSim.Inference.Derivation;
using FootprintSim.Inference.PastStates;
using FootprintSim.Interactions;
using FootprintSim.Interactions.CandidateWarps;

namespace FootprintSim.Evaluation;

public sealed class CommonWorldObservationFixtureEvaluationRunner
{
    private static readonly string[] ExpectedProfiles =
    [
        "none",
        "location",
        "time",
        "participant",
        "location-time",
        "location-participant",
        "time-participant",
        "location-time-participant"
    ];

    public CommonWorldObservationFixtureEvaluationResult Run(
        CommonWorldObservationFixtureEvaluationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Fixture);
        Validate(request.Fixture);

        var fixture = request.Fixture;
        var publicCandidates = fixture.CandidateStates.Select(item =>
            new PublicCandidateWarpStateDefinition
            {
                TimeWindow = new InteractionTimeWindow(item.StartTick, item.EndTick),
                TemporalKind = item.TemporalKind,
                RegionIds = item.RegionIds,
                EvidenceBoundaryState = EvidenceState.Confirmed,
                Provenance =
                [
                    CandidateWarpProvenance.Create(
                        CandidateWarpSourceKind.PublicCausalModel,
                        "m1f15-public-candidate-domain",
                        availableAtTick: 0,
                        isPersonEvidence: false,
                        isDeviceOnlyEvidence: false,
                        EvidenceState.Confirmed,
                        item.RegionIds)
                ]
            }).ToArray();
        var universe = new ObservationDerivedHistoricalCandidateReconstructor().Reconstruct(
            fixture.ScenarioId,
            fixture.Seed,
            fixture.TargetEntityId,
            publicCandidates,
            constraint: null).Candidates;
        var universeIds = universe.Select(item => item.CandidateId)
            .OrderBy(item => item, StringComparer.Ordinal)
            .ToArray();
        var universeId = InteractionStableIdentifier.Create(
            "cdu",
            fixture.ScenarioId,
            InteractionStableIdentifier.Seed(fixture.Seed),
            fixture.TargetEntityId,
            fixture.TargetStateContextId,
            fixture.CandidateDomainId,
            string.Join("|", universeIds));

        var locationObservation = CreateLocationObservation(fixture);
        var locationDerivation = new ObservedRegionLocationConstraintDeriver().Derive(
            locationObservation,
            fixture.EvidenceAvailabilityCutoff,
            PastStateEvidenceUsageRole.CandidateGeneration);
        var locationConstraint = locationDerivation.Constraint ?? throw new InvalidDataException(
            $"Location derivation failed: {locationDerivation.Audit.FailureReason}.");

        var (timeObservation, dependency) = CreateTimeInputs(fixture);
        var timeDerivation = new BoundedDurationObservationConstraintDeriver().Derive(
            timeObservation,
            dependency,
            fixture.EvidenceAvailabilityCutoff,
            PastStateEvidenceUsageRole.CandidateGeneration);
        var timeConstraint = timeDerivation.Constraint ?? throw new InvalidDataException(
            $"Time derivation failed: {timeDerivation.Audit.FailureReason}.");

        var (participantObservation, participantEvidence) = CreateParticipantInputs(fixture);
        var participantDerivation = new EncounterParticipantLocationConstraintDeriver().Derive(
            participantObservation,
            participantEvidence,
            fixture.EvidenceAvailabilityCutoff,
            PastStateEvidenceUsageRole.CandidateGeneration);
        var participantConstraint = participantDerivation.Constraint ?? throw new InvalidDataException(
            $"Participant derivation failed: {participantDerivation.Audit.FailureReason}.");

        var locationComponent = CandidateGenerationConstraintComponent.FromLocationDerivedConstraint(
            locationConstraint,
            fixture.TargetStateContextId,
            fixture.CandidateDomainId);
        var timeComponent = CandidateGenerationConstraintComponent.FromDerivedConstraint(
            timeConstraint,
            fixture.TargetStateContextId,
            fixture.CandidateDomainId,
            CandidateConstraintDimensionSelection.TimeOnly);
        var participantComponent = CandidateGenerationConstraintComponent.FromDerivedConstraint(
            participantConstraint,
            fixture.TargetStateContextId,
            fixture.CandidateDomainId,
            CandidateConstraintDimensionSelection.RegionOnly);
        var groupsByAxis = new Dictionary<CommonWorldObservationAxis, CandidateGenerationConstraintGroup>
        {
            [CommonWorldObservationAxis.Location] = Joint(locationComponent),
            [CommonWorldObservationAxis.Time] = Joint(timeComponent),
            [CommonWorldObservationAxis.Participant] = Joint(participantComponent)
        };
        var componentByAxis = new Dictionary<CommonWorldObservationAxis, CandidateGenerationConstraintComponent>
        {
            [CommonWorldObservationAxis.Location] = locationComponent,
            [CommonWorldObservationAxis.Time] = timeComponent,
            [CommonWorldObservationAxis.Participant] = participantComponent
        };
        var dependencySourceGroupId = InteractionStableIdentifier.Create(
            "srcg",
            dependency.DurationSourceId);
        var sourceGraph = SourceGraph(
            locationDerivation,
            timeDerivation,
            participantDerivation,
            dependency,
            participantEvidence);
        var sourceGroupsByAxis = new Dictionary<CommonWorldObservationAxis, IReadOnlyList<string>>
        {
            [CommonWorldObservationAxis.Location] = [locationDerivation.Audit.SourceGroupId],
            [CommonWorldObservationAxis.Time] =
            [
                timeDerivation.Audit.SourceGroupId,
                dependencySourceGroupId
            ],
            [CommonWorldObservationAxis.Participant] = new[]
            {
                participantDerivation.Audit.EncounterSourceGroupId
            }.Concat(participantDerivation.Audit.ParticipantEvidenceSourceGroupIds).ToArray()
        };
        var observationIds = new[]
        {
            locationObservation.ObservationId,
            timeObservation.ObservationId,
            participantObservation.ObservationId
        }.OrderBy(item => item, StringComparer.Ordinal).ToArray();
        var profiles = fixture.Profiles.OrderBy(item => item.Order).ToArray();
        var composer = new CandidateGenerationConstraintComposer();
        var preliminary = profiles.Select(profile =>
        {
            var activeAxes = ActiveAxes(profile);
            var groups = activeAxes.Select(axis => groupsByAxis[axis]).ToArray();
            var forward = composer.Compose(CompositionRequest(fixture, publicCandidates, groups));
            var reverse = composer.Compose(CompositionRequest(
                fixture,
                publicCandidates,
                groups.Reverse().ToArray()));
            if (!Equivalent(forward, reverse))
            {
                throw new InvalidDataException(
                    $"Profile '{profile.Id}' changed when constraint order was reversed.");
            }

            return new PreliminaryRun(profile, activeAxes, forward);
        }).ToArray();
        var none = preliminary.Single(item => item.Profile.Id == "none");
        if (!none.Result.Candidates.Select(item => item.CandidateId)
            .OrderBy(item => item, StringComparer.Ordinal)
            .SequenceEqual(universeIds, StringComparer.Ordinal))
        {
            throw new InvalidDataException("The None profile changed the common candidate universe.");
        }

        var truthCandidates = universe.Where(item =>
            item.TimeWindow.StartTick == fixture.TruthCandidateStartTick &&
            item.TimeWindow.EndTick == fixture.TruthCandidateEndTick &&
            string.Equals(item.RegionId, fixture.TruthCandidateRegionId, StringComparison.Ordinal))
            .ToArray();
        if (truthCandidates.Length != 1)
        {
            throw new InvalidDataException(
                "The evaluation-only truth address must match exactly one common-world candidate.");
        }

        var truthCandidateId = truthCandidates[0].CandidateId;
        var runs = preliminary.Select(item =>
        {
            var ids = item.Result.Candidates.Select(candidate => candidate.CandidateId)
                .OrderBy(candidateId => candidateId, StringComparer.Ordinal)
                .ToArray();
            var reduction = universe.Count - ids.Length;
            var containsTruth = ids.Contains(truthCandidateId, StringComparer.Ordinal);
            var omitted = Enum.GetValues<CommonWorldObservationAxis>()
                .Except(item.ActiveAxes)
                .OrderBy(axis => axis)
                .ToArray();
            var selectedSourceGroupIds = item.ActiveAxes
                .SelectMany(axis => sourceGroupsByAxis[axis])
                .Distinct(StringComparer.Ordinal)
                .OrderBy(id => id, StringComparer.Ordinal)
                .ToArray();
            var selectedFieldCount = sourceGraph
                .Where(node => selectedSourceGroupIds.Contains(
                    node.SourceGroupId,
                    StringComparer.Ordinal))
                .Sum(node => node.FieldIds.Count);
            return new CommonWorldProfileRun
            {
                ProfileId = item.Profile.Id,
                Order = item.Profile.Order,
                ActiveAxes = item.ActiveAxes,
                OmittedAxes = omitted,
                OmittedAxesMeanNotSelected = true,
                ActiveConstraintIds = item.ActiveAxes
                    .Select(axis => componentByAxis[axis].ConstraintId!)
                    .OrderBy(id => id, StringComparer.Ordinal)
                    .ToArray(),
                ObservationIdsPresent = observationIds,
                CandidateUniverseId = universeId,
                CandidateUniverseIds = universeIds,
                CompositionId = item.Result.CompositionId,
                Status = item.Result.Status,
                Candidates = item.Result.Candidates,
                CandidateIds = ids,
                CandidateCount = ids.Length,
                ReductionVsNoneCount = reduction,
                ReductionVsNoneFraction = universe.Count == 0
                    ? 0m
                    : reduction / (decimal)universe.Count,
                TruthCandidateId = truthCandidateId,
                ContainsTruth = containsTruth,
                FalseExclusion = !containsTruth,
                SelectedSupportingSourceGroupIds = selectedSourceGroupIds,
                SelectedSupportingSourceGroupCount = selectedSourceGroupIds.Length,
                SelectedFieldCount = selectedFieldCount,
                ActiveConstraintCount = item.ActiveAxes.Count,
                CandidateGenerationWork = item.Result.CandidateGenerationWork,
                CompositionAudit = item.Result.Audit
            };
        }).ToArray();
        var intersectionAudits = new[]
        {
            Intersection(runs, "location-time", "location", "time"),
            Intersection(runs, "location-participant", "location", "participant"),
            Intersection(runs, "time-participant", "time", "participant"),
            Intersection(
                runs,
                "location-time-participant",
                "location",
                "time",
                "participant")
        };
        if (runs.Any(item => !item.ContainsTruth))
        {
            throw new InvalidDataException(
                "The purified common world must contain truth under every selected-axis profile.");
        }

        var locationExpectedInputs = new[] { locationObservation.ObservationId };
        var timeExpectedInputs = new[]
        {
            timeObservation.ObservationId,
            dependency.PublicInputId,
            dependency.DurationSourceId
        }.OrderBy(item => item, StringComparer.Ordinal).ToArray();
        var participantExpectedInputs = new[] { participantObservation.ObservationId }
            .Concat(participantEvidence.Select(item => item.PublicInputId))
            .OrderBy(item => item, StringComparer.Ordinal)
            .ToArray();
        var independence = new CommonWorldFunctionalIndependenceAudit
        {
            LocationPublicInputIds = locationDerivation.Audit.PublicInputIdsUsed,
            TimePublicInputIds = timeDerivation.Audit.PublicInputIdsUsed,
            ParticipantPublicInputIds = participantDerivation.Audit.PublicInputIdsUsed,
            LocationReadsOnlyLocationInputs = locationDerivation.Audit.PublicInputIdsUsed
                .SequenceEqual(locationExpectedInputs, StringComparer.Ordinal),
            TimeReadsOnlyTimeInputs = timeDerivation.Audit.PublicInputIdsUsed
                .SequenceEqual(timeExpectedInputs, StringComparer.Ordinal),
            ParticipantReadsOnlyParticipantInputs = participantDerivation.Audit.PublicInputIdsUsed
                .SequenceEqual(participantExpectedInputs, StringComparer.Ordinal)
        };
        if (!independence.LocationReadsOnlyLocationInputs ||
            !independence.TimeReadsOnlyTimeInputs ||
            !independence.ParticipantReadsOnlyParticipantInputs)
        {
            throw new InvalidDataException("An axis derivation read a public input owned by another axis.");
        }

        return new CommonWorldObservationFixtureEvaluationResult
        {
            Manifest = new CommonWorldObservationFixtureManifest
            {
                FixtureId = fixture.Id,
                ScenarioId = fixture.ScenarioId,
                FixtureContentSha256 = request.FixtureContentSha256,
                SourceCommitSha = request.SourceCommitSha,
                DirtyWorkingTree = request.DirtyWorkingTree,
                LocationRuleId = ObservedRegionLocationConstraintDeriver.RuleId,
                TimeRuleId = BoundedDurationObservationConstraintDeriver.RuleId,
                ParticipantRuleId = EncounterParticipantLocationConstraintDeriver.RuleId,
                FixedRelationKind = fixture.FixedRelationKind,
                ProfileIds = profiles.Select(item => item.Id).ToArray(),
                ProfileRunCount = runs.Length,
                TruthUsedForDerivationOrComposition = false,
                InputOrderIndependent = true,
                CandidateUniverseInvariant = runs.All(item =>
                    item.CandidateUniverseId == universeId &&
                    item.CandidateUniverseIds.SequenceEqual(universeIds, StringComparer.Ordinal)),
                RelationInvariant = runs.All(_ =>
                    fixture.FixedRelationKind == InteractionRelationKind.PersonPersonEncounter)
            },
            TargetEntityId = fixture.TargetEntityId,
            TruthOtherParticipantId = fixture.TruthOtherParticipantId,
            TruthCandidateId = truthCandidateId,
            CandidateUniverseId = universeId,
            CandidateUniverse = universe,
            LocationObservation = locationObservation,
            TimeObservation = timeObservation,
            PublicDependency = dependency,
            ParticipantObservation = participantObservation,
            ParticipantLocationEvidence = participantEvidence,
            LocationConstraint = locationConstraint,
            TimeConstraint = timeConstraint,
            ParticipantConstraint = participantConstraint,
            DerivationAudit = new CommonWorldDerivationAudit
            {
                Location = locationDerivation.Audit,
                Time = timeDerivation.Audit,
                Participant = participantDerivation.Audit,
                RelationKnowledge = RelationKindKnowledge.Exact,
                ExactRelationKind = fixture.FixedRelationKind,
                AllObservationsExistInEveryProfile = runs.All(item =>
                    item.ObservationIdsPresent.SequenceEqual(observationIds, StringComparer.Ordinal)),
                TruthUsedForDerivation = false
            },
            SourceGraph = sourceGraph,
            WorldFieldCount = sourceGraph.Sum(item => item.FieldIds.Count),
            WorldIndependentSourceGroupCount = sourceGraph.Length,
            FunctionalIndependence = independence,
            Runs = runs,
            IntersectionAudits = intersectionAudits,
            IntersectionViolationCount = intersectionAudits.Count(item => !item.MatchesIntersection),
            ContainmentCount = runs.Count(item => item.ContainsTruth),
            FalseExclusionCount = runs.Count(item => item.FalseExclusion),
            ExistingS5Changed = false,
            InterpretationBoundary =
                "This one purified deterministic common world is a field-combination functional " +
                "baseline. It does not estimate general axis importance, a minimum observation " +
                "or source set, population interactions, sensor requirements, or rescue performance."
        };
    }

    private static PublicObservedRegionObservation CreateLocationObservation(
        CommonWorldObservationFixtureDefinition fixture)
    {
        var input = fixture.LocationObservation;
        return PublicObservedRegionObservation.Create(
            fixture.ScenarioId,
            fixture.Seed,
            input.SourceEvidenceIdentifier,
            fixture.TargetEntityId,
            input.ObservedRegionIds,
            input.AvailableAtTick,
            input.EvidenceState,
            input.Visibility,
            input.SourceAncestryIds);
    }

    private static (PublicEventObservation Observation, PublicBoundedDurationDependency Dependency)
        CreateTimeInputs(CommonWorldObservationFixtureDefinition fixture)
    {
        var input = fixture.TimeObservation;
        var definition = input.PublicDependency;
        return (
            PublicEventObservation.Create(
                fixture.ScenarioId,
                fixture.Seed,
                input.SourceEvidenceIdentifier,
                input.LaterEventKind,
                fixture.TargetEntityId,
                ObservedOccurrenceTime.Known(new InteractionTimeWindow(
                    input.ObservedStartTick,
                    input.ObservedEndTick)),
                input.AvailableAtTick,
                input.EvidenceState,
                input.Visibility,
                input.SourceAncestryIds),
            PublicBoundedDurationDependency.Create(
                fixture.ScenarioId,
                definition.Id,
                definition.PredecessorStateKind,
                definition.LaterEventKind,
                definition.TargetEntityId,
                new PublicDurationBound(
                    definition.MinimumDurationTicks,
                    definition.MaximumDurationTicks),
                definition.DurationSourceId,
                definition.AllowedPredecessorRegionIds));
    }

    private static (
        PublicEncounterParticipantObservation Observation,
        PublicParticipantLocationEvidence[] Evidence) CreateParticipantInputs(
        CommonWorldObservationFixtureDefinition fixture)
    {
        var input = fixture.ParticipantObservation;
        var observation = PublicEncounterParticipantObservation.Create(
            fixture.ScenarioId,
            fixture.Seed,
            input.EncounterSourceEvidenceIdentifier,
            fixture.TargetEntityId,
            fixture.TargetEntityKind,
            OtherParticipantInformation.FiniteAlternatives(input.PossibleParticipantIds),
            fixture.FixedRelationKind,
            new InteractionTimeWindow(input.EncounterStartTick, input.EncounterEndTick),
            input.AvailableAtTick,
            input.EvidenceState,
            input.Visibility,
            input.SourceAncestryIds);
        var evidence = input.ParticipantLocationEvidence.Select(definition =>
            PublicParticipantLocationEvidence.Create(
                fixture.ScenarioId,
                fixture.Seed,
                definition.SourceEvidenceIdentifier,
                definition.ParticipantId,
                definition.EntityKind,
                new InteractionTimeWindow(
                    definition.ApplicableStartTick,
                    definition.ApplicableEndTick),
                definition.AllowedRegionIds,
                definition.AvailableAtTick,
                definition.EvidenceState,
                definition.UsageRole,
                definition.Visibility,
                definition.SourceAncestryIds)).ToArray();
        return (observation, evidence);
    }

    private static CommonWorldSourceGraphNode[] SourceGraph(
        LocationConstraintDerivationResult location,
        ObservationConstraintDerivationResult time,
        ParticipantConstraintDerivationResult participant,
        PublicBoundedDurationDependency dependency,
        IReadOnlyList<PublicParticipantLocationEvidence> participantEvidence)
    {
        var dependencyGroup = InteractionStableIdentifier.Create("srcg", dependency.DurationSourceId);
        return new[]
        {
            Node(
                location.Audit.SourceGroupId,
                "location-observation",
                ["location.regions"],
                location.Audit.PublicInputIdsUsed),
            Node(
                time.Audit.SourceGroupId,
                "later-event-observation",
                ["time.occurrence-window"],
                [time.Audit.SourceObservationId]),
            Node(
                dependencyGroup,
                "duration-dependency-source",
                ["time.dependency", "time.duration-bounds"],
                [dependency.PublicInputId, dependency.DurationSourceId]),
            Node(
                participant.Audit.EncounterSourceGroupId,
                "encounter-observation",
                ["encounter.time", "participant.identities", "relation.kind"],
                [participant.Audit.SourceObservationId])
        }.Concat(participantEvidence.Select(item => Node(
            item.SourceGroupId,
            "participant-location-evidence",
            [$"participant.location:{item.ParticipantId}"],
            [item.PublicInputId])))
            .OrderBy(item => item.SourceGroupId, StringComparer.Ordinal)
            .ToArray();
    }

    private static CommonWorldSourceGraphNode Node(
        string sourceGroupId,
        string sourceKind,
        IEnumerable<string> fields,
        IEnumerable<string> publicInputs) => new()
        {
            SourceGroupId = sourceGroupId,
            SourceKind = sourceKind,
            FieldIds = fields.OrderBy(item => item, StringComparer.Ordinal).ToArray(),
            PublicInputIds = publicInputs.Distinct(StringComparer.Ordinal)
                .OrderBy(item => item, StringComparer.Ordinal)
                .ToArray()
        };

    private static CandidateGenerationConstraintGroup Joint(
        CandidateGenerationConstraintComponent component) =>
        CandidateGenerationConstraintGroup.Create(
            CandidateConstraintLogicalRole.JointRequirement,
            [component]);

    private static CommonWorldObservationAxis[] ActiveAxes(CommonWorldProfileDefinition profile) =>
        new[]
        {
            (profile.SelectLocation, CommonWorldObservationAxis.Location),
            (profile.SelectTime, CommonWorldObservationAxis.Time),
            (profile.SelectParticipant, CommonWorldObservationAxis.Participant)
        }
            .Where(item => item.Item1)
            .Select(item => item.Item2)
            .ToArray();

    private static CandidateGenerationConstraintCompositionRequest CompositionRequest(
        CommonWorldObservationFixtureDefinition fixture,
        IReadOnlyList<PublicCandidateWarpStateDefinition> candidates,
        IReadOnlyList<CandidateGenerationConstraintGroup> groups) => new()
        {
            ScenarioId = fixture.ScenarioId,
            Seed = fixture.Seed,
            TargetEntityId = fixture.TargetEntityId,
            TargetStateContextId = fixture.TargetStateContextId,
            CandidateDomainId = fixture.CandidateDomainId,
            EvidenceAvailabilityCutoff = fixture.EvidenceAvailabilityCutoff,
            UsageRole = PastStateEvidenceUsageRole.CandidateGeneration,
            PublicCandidateStates = candidates,
            ConstraintGroups = groups
        };

    private static bool Equivalent(
        CandidateGenerationConstraintCompositionResult left,
        CandidateGenerationConstraintCompositionResult right) =>
        left.CompositionId == right.CompositionId &&
        left.Status == right.Status &&
        left.FailureReason == right.FailureReason &&
        left.CandidateGenerationWork == right.CandidateGenerationWork &&
        left.Candidates.Select(item => item.CandidateId).SequenceEqual(
            right.Candidates.Select(item => item.CandidateId),
            StringComparer.Ordinal) &&
        left.Audit.SourceObservationIds.SequenceEqual(
            right.Audit.SourceObservationIds,
            StringComparer.Ordinal) &&
        left.Audit.SourceAncestryIds.SequenceEqual(
            right.Audit.SourceAncestryIds,
            StringComparer.Ordinal);

    private static ConstraintCompositionIntersectionAudit Intersection(
        IReadOnlyList<CommonWorldProfileRun> runs,
        string profileId,
        params string[] componentProfileIds)
    {
        var actual = runs.Single(item => item.ProfileId == profileId).CandidateIds;
        var expected = componentProfileIds
            .Select(id => runs.Single(item => item.ProfileId == id).CandidateIds.AsEnumerable())
            .Aggregate((left, right) => left.Intersect(right, StringComparer.Ordinal))
            .OrderBy(item => item, StringComparer.Ordinal)
            .ToArray();
        return new ConstraintCompositionIntersectionAudit
        {
            ProfileId = profileId,
            ComponentProfileIds = componentProfileIds,
            ExpectedCandidateIds = expected,
            ActualCandidateIds = actual,
            MatchesIntersection = expected.SequenceEqual(actual, StringComparer.Ordinal)
        };
    }

    private static void Validate(CommonWorldObservationFixtureDefinition fixture)
    {
        RequireIdentifier(fixture.Id, nameof(fixture.Id));
        RequireIdentifier(fixture.ScenarioId, nameof(fixture.ScenarioId));
        RequireIdentifier(fixture.TargetEntityId, nameof(fixture.TargetEntityId));
        RequireIdentifier(fixture.TargetStateContextId, nameof(fixture.TargetStateContextId));
        RequireIdentifier(fixture.CandidateDomainId, nameof(fixture.CandidateDomainId));
        if (fixture.TargetEntityKind != InteractionEntityKind.Person ||
            fixture.FixedRelationKind != InteractionRelationKind.PersonPersonEncounter)
        {
            throw new InvalidDataException(
                "The common world requires a Person target and exact PersonPersonEncounter.");
        }

        if (fixture.EvidenceAvailabilityCutoff < 0 || fixture.CandidateStates.Count is < 4 or > 10)
        {
            throw new InvalidDataException(
                "The common world requires a non-negative cutoff and four to ten candidates.");
        }

        if (fixture.CandidateStates.Any(item => item.RegionIds.Count != 1))
        {
            throw new InvalidDataException(
                "Each hand-checkable common-world candidate must have exactly one Region.");
        }

        if (fixture.LocationObservation.EvidenceState != EvidenceState.Confirmed ||
            fixture.LocationObservation.Visibility != InteractionVisibility.PublicEvidence ||
            fixture.LocationObservation.AvailableAtTick > fixture.EvidenceAvailabilityCutoff ||
            fixture.LocationObservation.ObservedRegionIds.Count == 0)
        {
            throw new InvalidDataException(
                "Location observation must be non-empty, confirmed, public, and available.");
        }

        if (fixture.TimeObservation.EvidenceState != EvidenceState.Confirmed ||
            fixture.TimeObservation.Visibility != InteractionVisibility.PublicEvidence ||
            fixture.TimeObservation.AvailableAtTick > fixture.EvidenceAvailabilityCutoff ||
            fixture.TimeObservation.LaterEventKind != fixture.TimeObservation.PublicDependency.LaterEventKind ||
            fixture.TargetEntityId != fixture.TimeObservation.PublicDependency.TargetEntityId)
        {
            throw new InvalidDataException(
                "Time observation and public dependency must be public, available, and matching.");
        }

        var participant = fixture.ParticipantObservation;
        if (participant.EvidenceState != EvidenceState.Confirmed ||
            participant.Visibility != InteractionVisibility.PublicEvidence ||
            participant.AvailableAtTick > fixture.EvidenceAvailabilityCutoff ||
            participant.PossibleParticipantIds.Count < 2 ||
            participant.ParticipantLocationEvidence.Any(item =>
                item.EntityKind != InteractionEntityKind.Person ||
                item.EvidenceState != EvidenceState.Confirmed ||
                item.Visibility != InteractionVisibility.PublicEvidence ||
                item.UsageRole != PastStateEvidenceUsageRole.CandidateGeneration ||
                item.AvailableAtTick > fixture.EvidenceAvailabilityCutoff))
        {
            throw new InvalidDataException(
                "Participant inputs must be complete Person-only public CandidateGeneration evidence.");
        }

        var possible = participant.PossibleParticipantIds.ToHashSet(StringComparer.Ordinal);
        var evidenceIds = participant.ParticipantLocationEvidence
            .Select(item => item.ParticipantId)
            .ToArray();
        if (!possible.SetEquals(evidenceIds) || evidenceIds.Distinct(StringComparer.Ordinal).Count() != evidenceIds.Length)
        {
            throw new InvalidDataException(
                "Every possible participant must have exactly one location-evidence definition.");
        }

        if (!possible.Contains(fixture.TruthOtherParticipantId))
        {
            throw new InvalidDataException(
                "Evaluation truth participant must be compatible with the public alternatives.");
        }

        var orderedProfiles = fixture.Profiles.OrderBy(item => item.Order)
            .Select(item => item.Id)
            .ToArray();
        if (!orderedProfiles.SequenceEqual(ExpectedProfiles, StringComparer.Ordinal) ||
            fixture.Profiles.Select(item => item.Id).Distinct(StringComparer.Ordinal).Count() != 8)
        {
            throw new InvalidDataException(
                "The fixture must contain the fixed eight selection profiles in semantic order.");
        }

        var expectedFlags = new Dictionary<string, (bool L, bool T, bool P)>(StringComparer.Ordinal)
        {
            ["none"] = (false, false, false),
            ["location"] = (true, false, false),
            ["time"] = (false, true, false),
            ["participant"] = (false, false, true),
            ["location-time"] = (true, true, false),
            ["location-participant"] = (true, false, true),
            ["time-participant"] = (false, true, true),
            ["location-time-participant"] = (true, true, true)
        };
        foreach (var profile in fixture.Profiles)
        {
            var expected = expectedFlags[profile.Id];
            if (profile.SelectLocation != expected.L ||
                profile.SelectTime != expected.T ||
                profile.SelectParticipant != expected.P)
            {
                throw new InvalidDataException($"Profile '{profile.Id}' changed its fixed selection.");
            }
        }
    }

    private static string RequireIdentifier(string value, string name)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidDataException($"{name} must be non-empty.");
        }

        return value;
    }

    private sealed record PreliminaryRun(
        CommonWorldProfileDefinition Profile,
        IReadOnlyList<CommonWorldObservationAxis> ActiveAxes,
        CandidateGenerationConstraintCompositionResult Result);
}
