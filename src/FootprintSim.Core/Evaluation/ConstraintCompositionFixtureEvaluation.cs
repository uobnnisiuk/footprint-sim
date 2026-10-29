using FootprintSim.Evidence;
using FootprintSim.Inference.Derivation;
using FootprintSim.Inference.PastStates;
using FootprintSim.Interactions;
using FootprintSim.Interactions.CandidateWarps;

namespace FootprintSim.Evaluation;

public sealed class ConstraintCompositionFixtureEvaluationRunner
{
    public const string LocationInputSemanticsId = ObservedRegionLocationConstraintDeriver.RuleId;

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

    public ConstraintCompositionFixtureEvaluationResult Run(
        ConstraintCompositionFixtureEvaluationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Fixture);
        Validate(request.Fixture);

        var fixture = request.Fixture;
        var publicCandidateStates = fixture.CandidateStates.Select(item =>
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
                        "m1f14-public-candidate-domain",
                        availableAtTick: 0,
                        isPersonEvidence: false,
                        isDeviceOnlyEvidence: false,
                        EvidenceState.Confirmed,
                        item.RegionIds)
                ]
            }).ToArray();
        var location = CreateLocationComponent(fixture);
        var time = CreateTimeComponent(fixture);
        var participant = CreateParticipantComponent(fixture);
        var componentGroups = new Dictionary<string, CandidateGenerationConstraintGroup>(
            StringComparer.Ordinal)
        {
            ["location"] = Joint(location),
            ["time"] = Joint(time),
            ["participant"] = Joint(participant)
        };
        var profiles = fixture.Profiles
            .OrderBy(item => item.Order)
            .ThenBy(item => item.Id, StringComparer.Ordinal)
            .ToArray();
        var composer = new CandidateGenerationConstraintComposer();
        var preliminary = profiles.Select(profile =>
        {
            var groups = Groups(profile, componentGroups);
            var forward = composer.Compose(Request(fixture, publicCandidateStates, groups));
            var reverse = composer.Compose(Request(
                fixture,
                publicCandidateStates,
                groups.Reverse().ToArray()));
            if (!Equivalent(forward, reverse))
            {
                throw new InvalidDataException(
                    $"Profile '{profile.Id}' changed when constraint order was reversed.");
            }

            return new PreliminaryRun(profile, forward);
        }).ToArray();
        var none = preliminary.Single(item => item.Profile.Id == "none");
        var truthCandidates = none.Result.Candidates.Where(item =>
            item.TimeWindow.StartTick == fixture.TruthCandidateStartTick &&
            item.TimeWindow.EndTick == fixture.TruthCandidateEndTick &&
            string.Equals(item.RegionId, fixture.TruthCandidateRegionId, StringComparison.Ordinal))
            .ToArray();
        if (truthCandidates.Length != 1)
        {
            throw new InvalidDataException(
                "The evaluation-only truth address must match exactly one candidate.");
        }

        var truthCandidateId = truthCandidates[0].CandidateId;
        var runs = preliminary.Select(item =>
        {
            var candidateIds = item.Result.Candidates
                .Select(candidate => candidate.CandidateId)
                .OrderBy(candidateId => candidateId, StringComparer.Ordinal)
                .ToArray();
            var reduction = none.Result.Candidates.Count - item.Result.Candidates.Count;
            var containsTruth = candidateIds.Contains(truthCandidateId, StringComparer.Ordinal);
            return new ConstraintCompositionFixtureRun
            {
                ProfileId = item.Profile.Id,
                Order = item.Profile.Order,
                IncludesLocation = item.Profile.IncludeLocation,
                IncludesTime = item.Profile.IncludeTime,
                IncludesParticipant = item.Profile.IncludeParticipant,
                CompositionId = item.Result.CompositionId,
                Status = item.Result.Status,
                InitialCandidateCount = item.Result.InitialCandidateCount,
                Candidates = item.Result.Candidates,
                CandidateIds = candidateIds,
                CandidateCount = candidateIds.Length,
                TruthCandidateId = truthCandidateId,
                ContainsTruth = containsTruth,
                FalseExclusion = !containsTruth,
                ReductionVsNoneCount = reduction,
                ReductionVsNoneFraction = none.Result.Candidates.Count == 0
                    ? 0m
                    : reduction / (decimal)none.Result.Candidates.Count,
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
        var noneRun = runs.Single(item => item.ProfileId == "none");

        return new ConstraintCompositionFixtureEvaluationResult
        {
            Manifest = new ConstraintCompositionFixtureManifest
            {
                FixtureId = fixture.Id,
                ScenarioId = fixture.ScenarioId,
                FixtureContentSha256 = request.FixtureContentSha256,
                SourceCommitSha = request.SourceCommitSha,
                DirtyWorkingTree = request.DirtyWorkingTree,
                TimeRuleId = BoundedDurationObservationConstraintDeriver.RuleId,
                ParticipantRuleId = EncounterParticipantLocationConstraintDeriver.RuleId,
                LocationInputSemanticsId = LocationInputSemanticsId,
                FixedRelationKind = fixture.FixedRelationKind,
                ProfileIds = profiles.Select(item => item.Id).ToArray(),
                ProfileRunCount = runs.Length,
                TruthUsedForComposition = false,
                InputOrderIndependent = true
            },
            CompositionSemantics =
                "Top-level groups are jointly required. JointRequirement evaluates all member " +
                "predicates with AND; Alternative evaluates member predicates with OR; " +
                "EquivalentSupport applies one normalized predicate; NonRestricting applies none. " +
                "Source groups are audited and never choose the logical operator.",
            LocationComponent = location,
            TimeComponent = time,
            ParticipantComponent = participant,
            CandidateUniverse = noneRun.Candidates,
            Runs = runs,
            IntersectionAudits = intersectionAudits,
            IntersectionViolationCount = intersectionAudits.Count(item => !item.MatchesIntersection),
            ContainmentCount = runs.Count(item => item.ContainsTruth),
            FalseExclusionCount = runs.Count(item => item.FalseExclusion),
            ExistingS5Changed = false,
            InterpretationBoundary =
                "This one purified deterministic fixture validates explicit logical composition. " +
                "Candidate counts are functional checks, not estimates of axis value, minimum " +
                "observations, source sufficiency, or rescue performance."
        };
    }

    private static CandidateGenerationConstraintComponent CreateLocationComponent(
        ConstraintCompositionFixtureDefinition fixture)
    {
        var input = fixture.LocationInput;
        var ancestry = input.SourceAncestryIds
            .Append(input.SourceEvidenceIdentifier)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(item => item, StringComparer.Ordinal)
            .ToArray();
        var observationId = InteractionStableIdentifier.Create(
            "locobs",
            fixture.ScenarioId,
            InteractionStableIdentifier.Seed(fixture.Seed),
            fixture.TargetEntityId,
            input.SourceEvidenceIdentifier,
            string.Join("|", input.AllowedRegionIds.OrderBy(item => item, StringComparer.Ordinal)));
        var sourceGroupId = InteractionStableIdentifier.Create(
            "ods",
            string.Join("|", ancestry));
        return CandidateGenerationConstraintComponent.PublicPredicate(
            fixture.ScenarioId,
            fixture.Seed,
            fixture.TargetEntityId,
            fixture.TargetStateContextId,
            fixture.CandidateDomainId,
            CandidateConstraintPredicate.Region(input.AllowedRegionIds),
            input.SourceEvidenceIdentifier,
            observationId,
            sourceGroupId,
            ancestry,
            LocationInputSemanticsId,
            [observationId],
            input.AvailableAtTick,
            input.EvidenceState,
            input.Visibility,
            PastStateEvidenceUsageRole.CandidateGeneration,
            [
                new InteractionProvenance(
                    InteractionProvenanceKind.EvidenceRecord,
                    input.SourceEvidenceIdentifier),
                new InteractionProvenance(
                    InteractionProvenanceKind.DerivationRule,
                    LocationInputSemanticsId)
            ]);
    }

    private static CandidateGenerationConstraintComponent CreateTimeComponent(
        ConstraintCompositionFixtureDefinition fixture)
    {
        var input = fixture.TimeInput;
        var definition = input.PublicDependency;
        var observation = PublicEventObservation.Create(
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
            InteractionVisibility.PublicEvidence,
            input.SourceAncestryIds);
        var dependency = PublicBoundedDurationDependency.Create(
            fixture.ScenarioId,
            definition.Id,
            definition.PredecessorStateKind,
            definition.LaterEventKind,
            definition.TargetEntityId,
            new PublicDurationBound(
                definition.MinimumDurationTicks,
                definition.MaximumDurationTicks),
            definition.DurationSourceId,
            definition.AllowedPredecessorRegionIds);
        var derivation = new BoundedDurationObservationConstraintDeriver().Derive(
            observation,
            dependency,
            fixture.EvidenceAvailabilityCutoff,
            PastStateEvidenceUsageRole.CandidateGeneration);
        var constraint = derivation.Constraint ?? throw new InvalidDataException(
            $"Time input derivation failed: {derivation.Audit.FailureReason}.");
        return CandidateGenerationConstraintComponent.FromDerivedConstraint(
            constraint,
            fixture.TargetStateContextId,
            fixture.CandidateDomainId,
            CandidateConstraintDimensionSelection.TimeOnly);
    }

    private static CandidateGenerationConstraintComponent CreateParticipantComponent(
        ConstraintCompositionFixtureDefinition fixture)
    {
        var input = fixture.ParticipantInput;
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
            InteractionVisibility.PublicEvidence,
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
        var derivation = new EncounterParticipantLocationConstraintDeriver().Derive(
            observation,
            evidence,
            fixture.EvidenceAvailabilityCutoff,
            PastStateEvidenceUsageRole.CandidateGeneration);
        var constraint = derivation.Constraint ?? throw new InvalidDataException(
            $"Participant input derivation failed: {derivation.Audit.FailureReason}.");
        return CandidateGenerationConstraintComponent.FromDerivedConstraint(
            constraint,
            fixture.TargetStateContextId,
            fixture.CandidateDomainId,
            CandidateConstraintDimensionSelection.RegionOnly);
    }

    private static CandidateGenerationConstraintGroup Joint(
        CandidateGenerationConstraintComponent component) =>
        CandidateGenerationConstraintGroup.Create(
            CandidateConstraintLogicalRole.JointRequirement,
            [component]);

    private static CandidateGenerationConstraintGroup[] Groups(
        ConstraintCompositionProfileDefinition profile,
        IReadOnlyDictionary<string, CandidateGenerationConstraintGroup> groups) =>
        new[]
        {
            (profile.IncludeLocation, groups["location"]),
            (profile.IncludeTime, groups["time"]),
            (profile.IncludeParticipant, groups["participant"])
        }
            .Where(item => item.Item1)
            .Select(item => item.Item2)
            .ToArray();

    private static CandidateGenerationConstraintCompositionRequest Request(
        ConstraintCompositionFixtureDefinition fixture,
        IReadOnlyList<PublicCandidateWarpStateDefinition> publicCandidateStates,
        IReadOnlyList<CandidateGenerationConstraintGroup> groups) => new()
        {
            ScenarioId = fixture.ScenarioId,
            Seed = fixture.Seed,
            TargetEntityId = fixture.TargetEntityId,
            TargetStateContextId = fixture.TargetStateContextId,
            CandidateDomainId = fixture.CandidateDomainId,
            EvidenceAvailabilityCutoff = fixture.EvidenceAvailabilityCutoff,
            UsageRole = PastStateEvidenceUsageRole.CandidateGeneration,
            PublicCandidateStates = publicCandidateStates,
            ConstraintGroups = groups
        };

    private static bool Equivalent(
        CandidateGenerationConstraintCompositionResult left,
        CandidateGenerationConstraintCompositionResult right) =>
        left.CompositionId == right.CompositionId &&
        left.Status == right.Status &&
        left.FailureReason == right.FailureReason &&
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
        IReadOnlyList<ConstraintCompositionFixtureRun> runs,
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

    private static void Validate(ConstraintCompositionFixtureDefinition fixture)
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
                "The acceptance fixture requires one Person target and exact PersonPersonEncounter.");
        }

        if (fixture.EvidenceAvailabilityCutoff < 0 || fixture.CandidateStates.Count is < 4 or > 6)
        {
            throw new InvalidDataException(
                "The acceptance fixture requires a non-negative cutoff and four to six candidates.");
        }

        if (fixture.LocationInput.EvidenceState != EvidenceState.Confirmed ||
            fixture.LocationInput.Visibility != InteractionVisibility.PublicEvidence ||
            fixture.LocationInput.AvailableAtTick > fixture.EvidenceAvailabilityCutoff)
        {
            throw new InvalidDataException(
                "Location input must be confirmed, public, and available at the cutoff.");
        }

        var orderedProfiles = fixture.Profiles
            .OrderBy(item => item.Order)
            .Select(item => item.Id)
            .ToArray();
        if (!orderedProfiles.SequenceEqual(ExpectedProfiles, StringComparer.Ordinal) ||
            fixture.Profiles.Select(item => item.Id).Distinct(StringComparer.Ordinal).Count() != 8)
        {
            throw new InvalidDataException(
                "The fixture must contain the preregistered eight profiles in semantic order.");
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
            var flags = expectedFlags[profile.Id];
            if (profile.IncludeLocation != flags.L ||
                profile.IncludeTime != flags.T ||
                profile.IncludeParticipant != flags.P)
            {
                throw new InvalidDataException($"Profile '{profile.Id}' changed its fixed axes.");
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
        ConstraintCompositionProfileDefinition Profile,
        CandidateGenerationConstraintCompositionResult Result);
}
