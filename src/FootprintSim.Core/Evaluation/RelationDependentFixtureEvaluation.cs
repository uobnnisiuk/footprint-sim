using FootprintSim.Evidence;
using FootprintSim.Inference.Derivation;
using FootprintSim.Inference.PastStates;
using FootprintSim.Interactions;
using FootprintSim.Interactions.CandidateWarps;

namespace FootprintSim.Evaluation;

public sealed class RelationDependentFixtureEvaluationRunner
{
    public RelationDependentFixtureEvaluationResult Run(
        RelationDependentFixtureEvaluationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Fixture);
        Validate(request.Fixture);

        var fixture = request.Fixture;
        var occurrenceWindow = new InteractionTimeWindow(
            fixture.RelationStartTick,
            fixture.RelationEndTick);
        var evidenceDefinition = fixture.ParticipantLocationEvidence;
        var participantEvidence = PublicParticipantLocationEvidence.Create(
            fixture.ScenarioId,
            fixture.Seed,
            evidenceDefinition.SourceEvidenceIdentifier,
            evidenceDefinition.ParticipantId,
            evidenceDefinition.EntityKind,
            new InteractionTimeWindow(
                evidenceDefinition.ApplicableStartTick,
                evidenceDefinition.ApplicableEndTick),
            evidenceDefinition.AllowedRegionIds,
            evidenceDefinition.AvailableAtTick,
            evidenceDefinition.EvidenceState,
            evidenceDefinition.UsageRole,
            evidenceDefinition.Visibility,
            evidenceDefinition.SourceAncestryIds);
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
                        "m1f12-public-candidate-layer",
                        availableAtTick: 0,
                        isPersonEvidence: false,
                        isDeviceOnlyEvidence: false,
                        EvidenceState.Confirmed,
                        item.RegionIds)
                ]
            }).ToArray();
        var reconstructor = new ObservationDerivedHistoricalCandidateReconstructor();
        var deriver = new PersonPersonEncounterColocationConstraintDeriver();
        var profiles = fixture.Profiles
            .OrderBy(item => item.InformationOrder)
            .ThenBy(item => item.Id, StringComparer.Ordinal)
            .ToArray();
        var preliminaryRuns = profiles.Select(profile =>
        {
            var observation = PublicRelationObservation.Create(
                fixture.ScenarioId,
                fixture.Seed,
                fixture.RelationSourceEvidenceIdentifier,
                fixture.TargetEntityId,
                fixture.TargetEntityKind,
                fixture.OtherParticipantId,
                fixture.OtherParticipantEntityKind,
                RelationInformation(profile),
                occurrenceWindow,
                fixture.RelationAvailableAtTick,
                fixture.RelationEvidenceState,
                InteractionVisibility.PublicEvidence,
                fixture.RelationSourceAncestryIds);
            var derivation = deriver.Derive(
                observation,
                [participantEvidence],
                fixture.EvidenceAvailabilityCutoff,
                PastStateEvidenceUsageRole.CandidateGeneration);
            var reconstruction = reconstructor.Reconstruct(
                fixture.ScenarioId,
                fixture.Seed,
                fixture.TargetEntityId,
                publicCandidates,
                derivation.Constraint);
            var candidateIds = reconstruction.Candidates
                .Select(item => item.CandidateId)
                .OrderBy(item => item, StringComparer.Ordinal)
                .ToArray();
            return new PreliminaryRun(
                profile,
                observation,
                derivation,
                reconstruction,
                candidateIds);
        }).ToArray();
        var unknown = preliminaryRuns.Single(item =>
            item.Profile.RelationKnowledge == RelationKindKnowledge.UnknownKind);
        var truthCandidates = unknown.Reconstruction.Candidates.Where(item =>
            item.TimeWindow.StartTick == fixture.TruthCandidateStartTick &&
            item.TimeWindow.EndTick == fixture.TruthCandidateEndTick &&
            string.Equals(item.RegionId, fixture.TruthCandidateRegionId, StringComparison.Ordinal))
            .ToArray();
        if (truthCandidates.Length != 1)
        {
            throw new InvalidDataException(
                "The evaluation-only truth address must match exactly one reconstructed candidate.");
        }

        var truthCandidateId = truthCandidates[0].CandidateId;
        var runs = preliminaryRuns.Select(item =>
        {
            var reductionCount = unknown.Reconstruction.Candidates.Count -
                item.Reconstruction.Candidates.Count;
            var containsTruth = item.CandidateIds.Contains(truthCandidateId, StringComparer.Ordinal);
            return new RelationDependentFixtureRun
            {
                ProfileId = item.Profile.Id,
                InformationOrder = item.Profile.InformationOrder,
                RelationObservationPresent = item.Observation.RelationObservationPresent,
                SourceObservationId = item.Observation.ObservationId,
                RelationKnowledge = item.Observation.RelationInformation.Knowledge,
                ExactRelationKind = item.Observation.RelationInformation.ExactRelationKind,
                ConstraintGenerated = item.Derivation.Constraint is not null,
                ConstraintId = item.Derivation.Constraint?.ConstraintId,
                DerivedAllowedRegionIds = item.Derivation.Constraint?.AllowedRegionIds ?? [],
                DerivationAudit = item.Derivation.Audit,
                RelationObservationSourceGroupCount = 1,
                ParticipantEvidenceSourceGroupCount =
                    item.Derivation.Audit.ParticipantEvidenceSourceGroupIds.Count,
                GeneratedConstraintSourceGroupCount = item.Derivation.Constraint is null ? 0 : 1,
                InitialCandidateCount = item.Reconstruction.InitialCandidateCount,
                Candidates = item.Reconstruction.Candidates,
                CandidateIds = item.CandidateIds,
                CandidateCount = item.Reconstruction.Candidates.Count,
                TruthCandidateId = truthCandidateId,
                ContainsTruth = containsTruth,
                FalseExclusion = !containsTruth,
                CandidateReductionVsUnknownCount = reductionCount,
                CandidateReductionVsUnknownFraction = unknown.Reconstruction.Candidates.Count == 0
                    ? 0m
                    : reductionCount / (decimal)unknown.Reconstruction.Candidates.Count,
                CandidateGenerationWork = item.Reconstruction.CandidateGenerationWork,
                ReconstructionEvidenceRecordsInspected = item.Reconstruction.EvidenceRecordsInspected,
                ReconstructionSourceGroupsInspected = item.Reconstruction.SourceGroupsInspected
            };
        }).ToArray();
        var exact = runs.Single(item => item.RelationKnowledge == RelationKindKnowledge.Exact);
        var unknownRun = runs.Single(item => item.RelationKnowledge == RelationKindKnowledge.UnknownKind);
        var monotonicity = new RelationDependentMonotonicityAudit
        {
            MoreSpecificProfileId = exact.ProfileId,
            LessSpecificProfileId = unknownRun.ProfileId,
            ExactRelationInformationPrecedesUnknown = true,
            CandidateSetIsSubset = exact.CandidateIds.All(item =>
                unknownRun.CandidateIds.Contains(item, StringComparer.Ordinal)),
            MoreSpecificCandidateIds = exact.CandidateIds,
            LessSpecificCandidateIds = unknownRun.CandidateIds
        };

        return new RelationDependentFixtureEvaluationResult
        {
            Manifest = new RelationDependentFixtureManifest
            {
                FixtureId = fixture.Id,
                ScenarioId = fixture.ScenarioId,
                FixtureContentSha256 = request.FixtureContentSha256,
                SourceCommitSha = request.SourceCommitSha,
                DirtyWorkingTree = request.DirtyWorkingTree,
                RelationApplicabilityRuleId = PersonPersonEncounterColocationConstraintDeriver.RuleId,
                UnderlyingConstraintRuleId =
                    PersonPersonEncounterColocationConstraintDeriver.UnderlyingConstraintRuleId,
                ProfileIds = profiles.Select(item => item.Id).ToArray(),
                ProfileRunCount = runs.Length,
                TruthUsedForDerivation = false
            },
            SemanticRule =
                "Exact public PersonPersonEncounter information permits the existing same-time " +
                "co-location rule; relation-kind unknown keeps the relation assertion but does " +
                "not establish that rule's applicability.",
            FormalRelationInventory = Enum.GetValues<InteractionRelationKind>(),
            MeaningfulSecondRelationAvailable = false,
            RelationTaxonomyAvailable = false,
            TargetEntityId = fixture.TargetEntityId,
            OtherParticipantId = fixture.OtherParticipantId,
            OccurrenceTimeWindow = occurrenceWindow,
            ParticipantLocationEvidence = participantEvidence,
            TruthRelationReference = fixture.TruthRelationReference,
            TruthCandidateId = truthCandidateId,
            Profiles = profiles,
            Runs = runs,
            MonotonicityAudit = monotonicity,
            MonotonicityViolationCount = monotonicity.CandidateSetIsSubset ? 0 : 1,
            ExistingS5Changed = false,
            InterpretationBoundary =
                "This deterministic Person-only fixture validates exact-versus-unknown relation " +
                "applicability. It does not define a relation taxonomy, finite alternatives, " +
                "population performance, or S5 migration."
        };
    }

    private static ObservedRelationInformation RelationInformation(
        RelationDependentProfileDefinition profile) => profile.RelationKnowledge switch
        {
            RelationKindKnowledge.Exact when profile.ExactRelationKind is not null =>
                ObservedRelationInformation.Exact(profile.ExactRelationKind.Value),
            RelationKindKnowledge.UnknownKind when profile.ExactRelationKind is null =>
                ObservedRelationInformation.ExistsButKindUnknown(),
            _ => throw new InvalidDataException(
                $"Profile '{profile.Id}' has inconsistent relation information.")
        };

    private static void Validate(RelationDependentFixtureDefinition fixture)
    {
        RequireIdentifier(fixture.Id, nameof(fixture.Id));
        RequireIdentifier(fixture.ScenarioId, nameof(fixture.ScenarioId));
        RequireIdentifier(fixture.TargetEntityId, nameof(fixture.TargetEntityId));
        RequireIdentifier(fixture.OtherParticipantId, nameof(fixture.OtherParticipantId));
        RequireIdentifier(
            fixture.RelationSourceEvidenceIdentifier,
            nameof(fixture.RelationSourceEvidenceIdentifier));
        RequireIdentifier(fixture.TruthRelationReference, nameof(fixture.TruthRelationReference));
        RequireIdentifier(fixture.TruthCandidateRegionId, nameof(fixture.TruthCandidateRegionId));
        if (fixture.TargetEntityKind != InteractionEntityKind.Person ||
            fixture.OtherParticipantEntityKind != InteractionEntityKind.Person)
        {
            throw new InvalidDataException("The acceptance fixture must remain Person-only.");
        }

        if (string.Equals(
            fixture.TargetEntityId,
            fixture.OtherParticipantId,
            StringComparison.Ordinal))
        {
            throw new InvalidDataException("Target and other participant must be distinct.");
        }

        var occurrenceWindow = new InteractionTimeWindow(
            fixture.RelationStartTick,
            fixture.RelationEndTick);
        if (fixture.RelationStartTick != fixture.RelationEndTick)
        {
            throw new InvalidDataException("The acceptance fixture keeps exact same-time semantics.");
        }

        if (fixture.RelationAvailableAtTick < occurrenceWindow.EndTick ||
            fixture.RelationAvailableAtTick > fixture.EvidenceAvailabilityCutoff ||
            fixture.RelationEvidenceState != EvidenceState.Confirmed)
        {
            throw new InvalidDataException(
                "The relation observation must be confirmed and available at the cutoff.");
        }

        var evidence = fixture.ParticipantLocationEvidence;
        RequireIdentifier(evidence.SourceEvidenceIdentifier, nameof(evidence.SourceEvidenceIdentifier));
        RequireIdentifier(evidence.ParticipantId, nameof(evidence.ParticipantId));
        var evidenceWindow = new InteractionTimeWindow(
            evidence.ApplicableStartTick,
            evidence.ApplicableEndTick);
        if (!string.Equals(evidence.ParticipantId, fixture.OtherParticipantId, StringComparison.Ordinal) ||
            evidence.EntityKind != InteractionEntityKind.Person ||
            evidence.Visibility != InteractionVisibility.PublicEvidence ||
            evidence.EvidenceState != EvidenceState.Confirmed ||
            evidence.UsageRole != PastStateEvidenceUsageRole.CandidateGeneration ||
            evidence.AllowedRegionIds.Count == 0 ||
            evidence.AvailableAtTick > fixture.EvidenceAvailabilityCutoff ||
            evidenceWindow.StartTick > occurrenceWindow.StartTick ||
            evidenceWindow.EndTick < occurrenceWindow.EndTick)
        {
            throw new InvalidDataException(
                "The fixed other participant requires confirmed public same-time Person evidence " +
                "for CandidateGeneration.");
        }

        if (fixture.CandidateStates.Count < 2)
        {
            throw new InvalidDataException("At least two historical candidates are required.");
        }

        foreach (var candidate in fixture.CandidateStates)
        {
            _ = new InteractionTimeWindow(candidate.StartTick, candidate.EndTick);
            if (candidate.RegionIds.Count == 0)
            {
                throw new InvalidDataException("Every candidate state requires a Region.");
            }
        }

        _ = new InteractionTimeWindow(
            fixture.TruthCandidateStartTick,
            fixture.TruthCandidateEndTick);
        var profiles = fixture.Profiles
            .OrderBy(item => item.InformationOrder)
            .ThenBy(item => item.Id, StringComparer.Ordinal)
            .ToArray();
        if (profiles.Length != 2 ||
            !profiles.Select(item => item.Id).SequenceEqual(
                new[] { "exact", "unknown" },
                StringComparer.Ordinal) ||
            profiles[0].RelationKnowledge != RelationKindKnowledge.Exact ||
            profiles[0].ExactRelationKind != InteractionRelationKind.PersonPersonEncounter ||
            profiles[1].RelationKnowledge != RelationKindKnowledge.UnknownKind ||
            profiles[1].ExactRelationKind is not null)
        {
            throw new InvalidDataException(
                "The acceptance fixture requires exact PersonPersonEncounter and kind-unknown profiles only.");
        }
    }

    private static void RequireIdentifier(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidDataException($"'{parameterName}' cannot be empty.");
        }
    }

    private sealed record PreliminaryRun(
        RelationDependentProfileDefinition Profile,
        PublicRelationObservation Observation,
        RelationConstraintDerivationResult Derivation,
        ObservationDerivedHistoricalCandidateResult Reconstruction,
        IReadOnlyList<string> CandidateIds);
}
