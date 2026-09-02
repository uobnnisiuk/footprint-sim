using FootprintSim.Evidence;
using FootprintSim.Inference.PastStates;
using FootprintSim.Interactions;

namespace FootprintSim.Inference.Derivation;

/// <summary>
/// Dispatches the existing same-time co-location derivation only when public relation information
/// exactly identifies PersonPersonEncounter. It accepts no truth-side relation or occurrence input.
/// </summary>
public sealed class PersonPersonEncounterColocationConstraintDeriver
{
    public const string RuleId = CrossThreadConstraintDeriver.PersonEncounterCoLocationRule;

    public const string UnderlyingConstraintRuleId = EncounterParticipantLocationConstraintDeriver.RuleId;

    private static IReadOnlyList<RelationConstraintInputKind> RequiredInputs { get; } =
    [
        RelationConstraintInputKind.RelationObservation,
        RelationConstraintInputKind.RelationInformation,
        RelationConstraintInputKind.TargetEntity,
        RelationConstraintInputKind.OtherParticipant,
        RelationConstraintInputKind.OccurrenceTimeWindow,
        RelationConstraintInputKind.ParticipantLocationEvidence,
        RelationConstraintInputKind.EvidenceAvailability,
        RelationConstraintInputKind.EvidenceVisibility,
        RelationConstraintInputKind.EvidenceState,
        RelationConstraintInputKind.SourceAncestry,
        RelationConstraintInputKind.UsageRole
    ];

    private static IReadOnlyList<RelationConstraintApplicabilityCondition> ApplicabilityConditions { get; } =
    [
        RelationConstraintApplicabilityCondition.RelationObservationPresent,
        RelationConstraintApplicabilityCondition.ExactPersonPersonEncounter,
        RelationConstraintApplicabilityCondition.BinaryRelation,
        RelationConstraintApplicabilityCondition.PersonTarget,
        RelationConstraintApplicabilityCondition.PersonOtherParticipant,
        RelationConstraintApplicabilityCondition.KnownOtherParticipant,
        RelationConstraintApplicabilityCondition.PublicEvidence,
        RelationConstraintApplicabilityCondition.AvailableByCutoff,
        RelationConstraintApplicabilityCondition.ConfirmedEvidenceBoundary,
        RelationConstraintApplicabilityCondition.SameTimeParticipantLocationEvidence,
        RelationConstraintApplicabilityCondition.CandidateGenerationOnly
    ];

    public RelationConstraintDerivationResult Derive(
        PublicRelationObservation observation,
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

        var relationSourceGroupId = InteractionStableIdentifier.Create(
            "ods",
            string.Join("|", observation.SourceAncestryIds));
        var dispatchAttempted = false;
        ParticipantConstraintDerivationResult? underlying = null;
        PublicEncounterParticipantObservation? encounterObservation = null;

        if (observation.Visibility != InteractionVisibility.PublicEvidence)
        {
            return Failure(RelationConstraintDerivationFailureReason.ObservationNotPublic);
        }

        if (observation.AvailableAtTick > evidenceAvailabilityCutoff)
        {
            return Failure(RelationConstraintDerivationFailureReason.ObservationUnavailableAtCutoff);
        }

        if (observation.EvidenceBoundaryState != EvidenceState.Confirmed)
        {
            return Failure(RelationConstraintDerivationFailureReason.UnsupportedObservationState);
        }

        if (observation.RelationInformation.Knowledge == RelationKindKnowledge.UnknownKind)
        {
            return Failure(RelationConstraintDerivationFailureReason.UnknownRelationKind);
        }

        dispatchAttempted = true;
        if (observation.RelationInformation.ExactRelationKind !=
            InteractionRelationKind.PersonPersonEncounter)
        {
            return Failure(RelationConstraintDerivationFailureReason.UnsupportedRelationKind);
        }

        if (observation.Arity != 2)
        {
            return Failure(RelationConstraintDerivationFailureReason.RelationArityMismatch);
        }

        if (observation.TargetEntityKind != InteractionEntityKind.Person ||
            observation.OtherParticipantEntityKind != InteractionEntityKind.Person)
        {
            return Failure(RelationConstraintDerivationFailureReason.EntityKindMismatch);
        }

        if (usageRole != PastStateEvidenceUsageRole.CandidateGeneration)
        {
            return Failure(RelationConstraintDerivationFailureReason.WrongUsageRole);
        }

        encounterObservation = PublicEncounterParticipantObservation.Create(
            observation.ScenarioId,
            observation.Seed,
            observation.SourceEvidenceIdentifier,
            observation.TargetEntityId,
            observation.TargetEntityKind,
            OtherParticipantInformation.Exact(
                observation.OtherParticipantId,
                observation.OtherParticipantEntityKind),
            InteractionRelationKind.PersonPersonEncounter,
            observation.OccurrenceTimeWindow,
            observation.AvailableAtTick,
            observation.EvidenceBoundaryState,
            observation.Visibility,
            observation.SourceAncestryIds,
            observation.Provenance.Concat(
            [
                new InteractionProvenance(
                    InteractionProvenanceKind.InteractionObservation,
                    observation.ObservationId),
                new InteractionProvenance(
                    InteractionProvenanceKind.DerivationRule,
                    RuleId)
            ]));
        underlying = new EncounterParticipantLocationConstraintDeriver().Derive(
            encounterObservation,
            participantLocationEvidence,
            evidenceAvailabilityCutoff,
            usageRole);
        if (underlying.Constraint is null)
        {
            return Failure(MapFailure(underlying.Audit.FailureReason));
        }

        return new RelationConstraintDerivationResult
        {
            Constraint = underlying.Constraint,
            Audit = Audit(
                success: true,
                RelationConstraintDerivationFailureReason.None,
                underlying.Constraint.ConstraintId)
        };

        RelationConstraintDerivationResult Failure(
            RelationConstraintDerivationFailureReason reason) => new()
            {
                Constraint = null,
                Audit = Audit(success: false, reason, constraintId: null)
            };

        RelationConstraintDerivationAudit Audit(
            bool success,
            RelationConstraintDerivationFailureReason reason,
            string? constraintId)
        {
            var inspected = new[] { observation.ObservationId }
                .Concat(underlying?.Audit.PublicInputIdsInspected ?? [])
                .Distinct(StringComparer.Ordinal)
                .OrderBy(item => item, StringComparer.Ordinal)
                .ToArray();
            var used = success
                ? new[] { observation.ObservationId }
                    .Concat(underlying?.Audit.PublicInputIdsUsed ?? [])
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(item => item, StringComparer.Ordinal)
                    .ToArray()
                : Array.Empty<string>();

            return new RelationConstraintDerivationAudit
            {
                RuleId = RuleId,
                UnderlyingConstraintRuleId = UnderlyingConstraintRuleId,
                RequiredInputInformation = RequiredInputs,
                ApplicabilityConditions = ApplicabilityConditions,
                RelationObservationPresent = observation.RelationObservationPresent,
                SourceObservationId = observation.ObservationId,
                SourceEvidenceIdentifier = observation.SourceEvidenceIdentifier,
                RelationKnowledge = observation.RelationInformation.Knowledge,
                ExactRelationKind = observation.RelationInformation.ExactRelationKind,
                RelationArity = observation.Arity,
                TargetEntityId = observation.TargetEntityId,
                TargetEntityKind = observation.TargetEntityKind,
                OtherParticipantId = observation.OtherParticipantId,
                OtherParticipantEntityKind = observation.OtherParticipantEntityKind,
                OccurrenceTimeWindow = observation.OccurrenceTimeWindow,
                RelationDispatchAttempted = dispatchAttempted,
                EncounterRuleApplied = success,
                PublicInputIdsInspected = inspected,
                PublicInputIdsUsed = used,
                OutputAllowedRegionIds = underlying?.Constraint?.AllowedRegionIds ?? [],
                UsageRole = usageRole,
                RelationObservationSourceGroupId = relationSourceGroupId,
                ParticipantEvidenceSourceGroupIds = underlying?.Audit.ParticipantEvidenceSourceGroupIds ?? [],
                SourceAncestryIds = underlying?.Audit.SourceAncestryIds ?? observation.SourceAncestryIds,
                Success = success,
                FailureReason = reason,
                UnderlyingFailureReason = underlying?.Audit.FailureReason,
                UnderlyingObservationId = encounterObservation?.ObservationId,
                GeneratedConstraintId = constraintId
            };
        }
    }

    private static RelationConstraintDerivationFailureReason MapFailure(
        ParticipantConstraintDerivationFailureReason reason) => reason switch
        {
            ParticipantConstraintDerivationFailureReason.MissingParticipantLocationEvidence =>
                RelationConstraintDerivationFailureReason.MissingParticipantEvidence,
            ParticipantConstraintDerivationFailureReason.TargetEntityKindMismatch or
            ParticipantConstraintDerivationFailureReason.OtherParticipantEntityKindMismatch or
            ParticipantConstraintDerivationFailureReason.ParticipantEvidenceEntityKindMismatch =>
                RelationConstraintDerivationFailureReason.EntityKindMismatch,
            ParticipantConstraintDerivationFailureReason.UnsupportedUsageRole or
            ParticipantConstraintDerivationFailureReason.ParticipantEvidenceUsageMismatch =>
                RelationConstraintDerivationFailureReason.WrongUsageRole,
            _ => RelationConstraintDerivationFailureReason.UnderlyingDerivationFailed
        };
}
