using FootprintSim.Evidence;
using FootprintSim.Inference.PastStates;
using FootprintSim.Interactions;

namespace FootprintSim.Inference.Derivation;

/// <summary>
/// Uses public same-time location evidence for every possible other person in a reported
/// person-person encounter. Alternative participants contribute a conservative Region union.
/// The method accepts no truth-side participant, occurrence, or trajectory input.
/// </summary>
public sealed class EncounterParticipantLocationConstraintDeriver
{
    public const string RuleId = "encounter-participant-location-union-v1";

    private static IReadOnlyList<ParticipantConstraintInputKind> RequiredInputs { get; } =
    [
        ParticipantConstraintInputKind.EncounterObservation,
        ParticipantConstraintInputKind.TargetEntity,
        ParticipantConstraintInputKind.OtherParticipantInformation,
        ParticipantConstraintInputKind.ExactEncounterRelation,
        ParticipantConstraintInputKind.EncounterTimeWindow,
        ParticipantConstraintInputKind.ParticipantLocationEvidence,
        ParticipantConstraintInputKind.EvidenceAvailability,
        ParticipantConstraintInputKind.EvidenceVisibility,
        ParticipantConstraintInputKind.EvidenceState,
        ParticipantConstraintInputKind.SourceAncestry,
        ParticipantConstraintInputKind.UsageRole
    ];

    private static IReadOnlyList<ParticipantConstraintApplicabilityCondition> ApplicabilityConditions { get; } =
    [
        ParticipantConstraintApplicabilityCondition.EncounterObservationPresent,
        ParticipantConstraintApplicabilityCondition.PersonPersonEncounter,
        ParticipantConstraintApplicabilityCondition.PersonTarget,
        ParticipantConstraintApplicabilityCondition.PersonOtherParticipants,
        ParticipantConstraintApplicabilityCondition.KnownFiniteParticipantIdentities,
        ParticipantConstraintApplicabilityCondition.PublicEvidence,
        ParticipantConstraintApplicabilityCondition.AvailableByCutoff,
        ParticipantConstraintApplicabilityCondition.ConfirmedEvidenceBoundary,
        ParticipantConstraintApplicabilityCondition.MatchingScenarioAndSeed,
        ParticipantConstraintApplicabilityCondition.EvidenceCoversEncounterWindow,
        ParticipantConstraintApplicabilityCondition.CompleteParticipantEvidenceCoverage,
        ParticipantConstraintApplicabilityCondition.CandidateGenerationOnly
    ];

    public ParticipantConstraintDerivationResult Derive(
        PublicEncounterParticipantObservation observation,
        IEnumerable<PublicParticipantLocationEvidence> participantLocationEvidence,
        long evidenceAvailabilityCutoff,
        PastStateEvidenceUsageRole usageRole)
    {
        ArgumentNullException.ThrowIfNull(observation);
        ArgumentNullException.ThrowIfNull(participantLocationEvidence);
        if (evidenceAvailabilityCutoff < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(evidenceAvailabilityCutoff));
        }

        var evidence = participantLocationEvidence
            .OrderBy(item => item.ParticipantId, StringComparer.Ordinal)
            .ThenBy(item => item.PublicInputId, StringComparer.Ordinal)
            .ToArray();
        var sourceGroupId = InteractionStableIdentifier.Create(
            "ods",
            string.Join("|", observation.SourceAncestryIds));
        var branches = Array.Empty<ParticipantLocationEvidenceBranchAudit>();
        var publicInputsInspected = new[] { observation.ObservationId };
        var publicInputsUsed = Array.Empty<string>();
        var outputRegions = Array.Empty<string>();
        var selectedEvidence = Array.Empty<PublicParticipantLocationEvidence>();
        var evidenceRecordsInspected = 0;
        var alternativesEvaluated = 0;

        if (observation.Visibility != InteractionVisibility.PublicEvidence)
        {
            return Failure(ParticipantConstraintDerivationFailureReason.ObservationNotPublic);
        }

        if (observation.AvailableAtTick > evidenceAvailabilityCutoff)
        {
            return Failure(ParticipantConstraintDerivationFailureReason.ObservationUnavailableAtCutoff);
        }

        if (observation.EvidenceBoundaryState != EvidenceState.Confirmed)
        {
            return Failure(ParticipantConstraintDerivationFailureReason.UnsupportedObservationState);
        }

        if (observation.RelationKind != InteractionRelationKind.PersonPersonEncounter)
        {
            return Failure(ParticipantConstraintDerivationFailureReason.UnsupportedRelation);
        }

        if (observation.TargetEntityKind != InteractionEntityKind.Person)
        {
            return Failure(ParticipantConstraintDerivationFailureReason.TargetEntityKindMismatch);
        }

        if (observation.OtherParticipant.EntityKind != InteractionEntityKind.Person)
        {
            return Failure(ParticipantConstraintDerivationFailureReason.OtherParticipantEntityKindMismatch);
        }

        if (usageRole != PastStateEvidenceUsageRole.CandidateGeneration)
        {
            return Failure(ParticipantConstraintDerivationFailureReason.UnsupportedUsageRole);
        }

        if (observation.OtherParticipant.IdentityKnowledge == ParticipantIdentityKnowledge.UnknownIdentity)
        {
            return Failure(ParticipantConstraintDerivationFailureReason.UnknownParticipantIdentity);
        }

        evidenceRecordsInspected = evidence.Length;
        publicInputsInspected = new[] { observation.ObservationId }
            .Concat(evidence.Select(item => item.PublicInputId))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(item => item, StringComparer.Ordinal)
            .ToArray();
        branches = observation.OtherParticipant.PossibleParticipantIds
            .Select(CreateBranch)
            .ToArray();
        alternativesEvaluated = branches.Length;
        var failedBranch = branches.FirstOrDefault(item => !item.Derivable);
        if (failedBranch is not null)
        {
            return Failure(failedBranch.FailureReason);
        }

        var possibleIds = observation.OtherParticipant.PossibleParticipantIds;
        selectedEvidence = evidence
            .Where(item => possibleIds.Contains(item.ParticipantId, StringComparer.Ordinal))
            .OrderBy(item => item.ParticipantId, StringComparer.Ordinal)
            .ThenBy(item => item.PublicInputId, StringComparer.Ordinal)
            .ToArray();
        outputRegions = branches
            .SelectMany(item => item.AllowedRegionIds)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(item => item, StringComparer.Ordinal)
            .ToArray();
        publicInputsUsed = new[] { observation.ObservationId }
            .Concat(selectedEvidence.Select(item => item.PublicInputId))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(item => item, StringComparer.Ordinal)
            .ToArray();
        var sourceAncestry = observation.SourceAncestryIds
            .Concat(selectedEvidence.SelectMany(item => item.SourceAncestryIds))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(item => item, StringComparer.Ordinal)
            .ToArray();
        var provenance = InteractionNormalization.Provenance(
            observation.Provenance
                .Concat(selectedEvidence.SelectMany(item => item.Provenance))
                .Append(new InteractionProvenance(
                    InteractionProvenanceKind.InteractionObservation,
                    observation.ObservationId))
                .Append(new InteractionProvenance(
                    InteractionProvenanceKind.DerivationRule,
                    RuleId))
                .Concat(selectedEvidence.Select(item => new InteractionProvenance(
                    InteractionProvenanceKind.ExternalSource,
                    item.PublicInputId))));
        var availableAtTick = Math.Max(
            observation.AvailableAtTick,
            selectedEvidence.Max(item => item.AvailableAtTick));
        var constraintId = InteractionStableIdentifier.Create(
            "odc",
            observation.ScenarioId,
            InteractionStableIdentifier.Seed(observation.Seed),
            observation.ObservationId,
            observation.SourceEvidenceIdentifier,
            sourceGroupId,
            observation.TargetEntityId,
            observation.RelationKind.ToString(),
            observation.OtherParticipant.StableIdentity(),
            InteractionStableIdentifier.Tick(observation.EncounterTimeWindow.StartTick),
            InteractionStableIdentifier.Tick(observation.EncounterTimeWindow.EndTick),
            InteractionStableIdentifier.Tick(availableAtTick),
            observation.EvidenceBoundaryState.ToString(),
            observation.Visibility.ToString(),
            RuleId,
            usageRole.ToString(),
            string.Join("|", sourceAncestry),
            string.Join("|", outputRegions),
            string.Join("|", publicInputsUsed),
            string.Join("|", provenance.Select(ProvenanceIdentity)));
        var constraint = new ObservationDerivedConstraint(
            constraintId,
            observation.ScenarioId,
            observation.Seed,
            observation.ObservationId,
            observation.SourceEvidenceIdentifier,
            sourceGroupId,
            sourceAncestry,
            observation.TargetEntityId,
            observation.EncounterTimeWindow,
            observation.EncounterTimeWindow,
            outputRegions,
            availableAtTick,
            observation.EvidenceBoundaryState,
            observation.Visibility,
            RuleId,
            publicInputsUsed,
            usageRole,
            provenance);

        return new ParticipantConstraintDerivationResult
        {
            Constraint = constraint,
            Audit = Audit(
                success: true,
                ParticipantConstraintDerivationFailureReason.None,
                constraintId,
                sourceAncestry)
        };

        ParticipantLocationEvidenceBranchAudit CreateBranch(string participantId)
        {
            var matches = evidence
                .Where(item => string.Equals(
                    item.ParticipantId,
                    participantId,
                    StringComparison.Ordinal))
                .ToArray();
            if (matches.Length == 0)
            {
                return BranchFailure(
                    participantId,
                    ParticipantConstraintDerivationFailureReason.MissingParticipantLocationEvidence,
                    matches);
            }

            if (matches.Length > 1)
            {
                return BranchFailure(
                    participantId,
                    ParticipantConstraintDerivationFailureReason.MultipleParticipantLocationEvidenceRecords,
                    matches);
            }

            var item = matches[0];
            if (!string.Equals(item.ScenarioId, observation.ScenarioId, StringComparison.Ordinal))
            {
                return BranchFailure(
                    participantId,
                    ParticipantConstraintDerivationFailureReason.ParticipantEvidenceScenarioMismatch,
                    matches);
            }

            if (item.Seed != observation.Seed)
            {
                return BranchFailure(
                    participantId,
                    ParticipantConstraintDerivationFailureReason.ParticipantEvidenceSeedMismatch,
                    matches);
            }

            if (item.ParticipantEntityKind != InteractionEntityKind.Person)
            {
                return BranchFailure(
                    participantId,
                    ParticipantConstraintDerivationFailureReason.ParticipantEvidenceEntityKindMismatch,
                    matches);
            }

            if (item.Visibility != InteractionVisibility.PublicEvidence)
            {
                return BranchFailure(
                    participantId,
                    ParticipantConstraintDerivationFailureReason.ParticipantEvidenceNotPublic,
                    matches);
            }

            if (item.AvailableAtTick > evidenceAvailabilityCutoff)
            {
                return BranchFailure(
                    participantId,
                    ParticipantConstraintDerivationFailureReason.ParticipantEvidenceUnavailableAtCutoff,
                    matches);
            }

            if (item.EvidenceBoundaryState != EvidenceState.Confirmed)
            {
                return BranchFailure(
                    participantId,
                    ParticipantConstraintDerivationFailureReason.UnsupportedParticipantEvidenceState,
                    matches);
            }

            if (item.UsageRole != PastStateEvidenceUsageRole.CandidateGeneration)
            {
                return BranchFailure(
                    participantId,
                    ParticipantConstraintDerivationFailureReason.ParticipantEvidenceUsageMismatch,
                    matches);
            }

            if (!Covers(item.ApplicableTimeWindow, observation.EncounterTimeWindow))
            {
                return BranchFailure(
                    participantId,
                    ParticipantConstraintDerivationFailureReason.ParticipantEvidenceTimeMismatch,
                    matches);
            }

            return new ParticipantLocationEvidenceBranchAudit
            {
                ParticipantId = participantId,
                EntityKind = item.ParticipantEntityKind,
                MatchingPublicInputIds = [item.PublicInputId],
                AllowedRegionIds = item.AllowedRegionIds,
                SourceGroupIds = [item.SourceGroupId],
                Derivable = true,
                FailureReason = ParticipantConstraintDerivationFailureReason.None
            };
        }

        ParticipantLocationEvidenceBranchAudit BranchFailure(
            string participantId,
            ParticipantConstraintDerivationFailureReason reason,
            IReadOnlyList<PublicParticipantLocationEvidence> matches) => new()
            {
                ParticipantId = participantId,
                EntityKind = observation.OtherParticipant.EntityKind,
                MatchingPublicInputIds = matches
                    .Select(item => item.PublicInputId)
                    .OrderBy(item => item, StringComparer.Ordinal)
                    .ToArray(),
                AllowedRegionIds = [],
                SourceGroupIds = matches
                    .Select(item => item.SourceGroupId)
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(item => item, StringComparer.Ordinal)
                    .ToArray(),
                Derivable = false,
                FailureReason = reason
            };

        ParticipantConstraintDerivationResult Failure(
            ParticipantConstraintDerivationFailureReason reason) => new()
            {
                Constraint = null,
                Audit = Audit(success: false, reason, constraintId: null, observation.SourceAncestryIds)
            };

        ParticipantConstraintDerivationAudit Audit(
            bool success,
            ParticipantConstraintDerivationFailureReason reason,
            string? constraintId,
            IReadOnlyList<string> sourceAncestry) => new()
            {
                RuleId = RuleId,
                RequiredInputInformation = RequiredInputs,
                ApplicabilityConditions = ApplicabilityConditions,
                EncounterObservationPresent = observation.EncounterObservationPresent,
                SourceObservationId = observation.ObservationId,
                SourceEvidenceIdentifier = observation.SourceEvidenceIdentifier,
                TargetEntityId = observation.TargetEntityId,
                RelationKind = observation.RelationKind,
                EncounterTimeWindow = observation.EncounterTimeWindow,
                ParticipantIdentityKnowledge = observation.OtherParticipant.IdentityKnowledge,
                ParticipantEntityKind = observation.OtherParticipant.EntityKind,
                PossibleParticipantIds = observation.OtherParticipant.PossibleParticipantIds,
                ParticipantBranches = branches,
                PublicInputIdsInspected = publicInputsInspected,
                PublicInputIdsUsed = publicInputsUsed,
                OutputAllowedRegionIds = outputRegions,
                UncertaintyPropagation =
                    ParticipantRegionUncertaintyPropagation.ConservativeUnionAcrossPossibleParticipants,
                UsageRole = usageRole,
                EncounterSourceGroupId = sourceGroupId,
                ParticipantEvidenceSourceGroupIds = selectedEvidence
                    .Select(item => item.SourceGroupId)
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(item => item, StringComparer.Ordinal)
                    .ToArray(),
                SourceAncestryIds = sourceAncestry,
                ParticipantAlternativesEvaluated = alternativesEvaluated,
                ParticipantLocationEvidenceRecordsInspected = evidenceRecordsInspected,
                Success = success,
                FailureReason = reason,
                GeneratedConstraintId = constraintId
            };
    }

    private static bool Covers(InteractionTimeWindow evidence, InteractionTimeWindow encounter) =>
        evidence.StartTick <= encounter.StartTick && encounter.EndTick <= evidence.EndTick;

    private static string ProvenanceIdentity(InteractionProvenance provenance) =>
        $"{provenance.Kind}:{provenance.SourceIdentifier}";
}
