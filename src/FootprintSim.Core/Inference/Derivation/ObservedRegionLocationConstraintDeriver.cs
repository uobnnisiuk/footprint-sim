using FootprintSim.Evidence;
using FootprintSim.Inference.PastStates;
using FootprintSim.Interactions;

namespace FootprintSim.Inference.Derivation;

/// <summary>
/// Reusable implementation of the M1-F2 direct projection: the public finite Region set in a
/// location observation becomes the same finite AllowedRegion set. It accepts no truth input.
/// </summary>
public sealed class ObservedRegionLocationConstraintDeriver
{
    public const string RuleId = "m1f2-observed-regions-direct-projection";

    private static IReadOnlyList<LocationConstraintInputKind> RequiredInputs { get; } =
    [
        LocationConstraintInputKind.SourceObservation,
        LocationConstraintInputKind.TargetEntity,
        LocationConstraintInputKind.ObservedRegionSet,
        LocationConstraintInputKind.EvidenceAvailability,
        LocationConstraintInputKind.EvidenceVisibility,
        LocationConstraintInputKind.EvidenceState,
        LocationConstraintInputKind.SourceAncestry,
        LocationConstraintInputKind.UsageRole
    ];

    private static IReadOnlyList<LocationConstraintApplicabilityCondition> ApplicabilityConditions { get; } =
    [
        LocationConstraintApplicabilityCondition.NonEmptyObservedRegionSet,
        LocationConstraintApplicabilityCondition.PublicEvidence,
        LocationConstraintApplicabilityCondition.AvailableByCutoff,
        LocationConstraintApplicabilityCondition.ConfirmedEvidenceBoundary,
        LocationConstraintApplicabilityCondition.CandidateGenerationOnly
    ];

    public LocationConstraintDerivationResult Derive(
        PublicObservedRegionObservation observation,
        long evidenceAvailabilityCutoff,
        PastStateEvidenceUsageRole usageRole)
    {
        ArgumentNullException.ThrowIfNull(observation);
        if (evidenceAvailabilityCutoff < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(evidenceAvailabilityCutoff));
        }

        var sourceGroupId = InteractionStableIdentifier.Create(
            "ods",
            string.Join("|", observation.SourceAncestryIds));
        var publicInputs = new[] { observation.ObservationId };
        if (observation.Visibility != InteractionVisibility.PublicEvidence)
        {
            return Failure(LocationConstraintDerivationFailureReason.ObservationNotPublic);
        }

        if (observation.AvailableAtTick > evidenceAvailabilityCutoff)
        {
            return Failure(LocationConstraintDerivationFailureReason.ObservationUnavailableAtCutoff);
        }

        if (observation.EvidenceBoundaryState != EvidenceState.Confirmed)
        {
            return Failure(LocationConstraintDerivationFailureReason.UnsupportedEvidenceState);
        }

        if (usageRole != PastStateEvidenceUsageRole.CandidateGeneration)
        {
            return Failure(LocationConstraintDerivationFailureReason.UnsupportedUsageRole);
        }

        var provenance = InteractionNormalization.Provenance(
            observation.Provenance
                .Append(new InteractionProvenance(
                    InteractionProvenanceKind.InteractionObservation,
                    observation.ObservationId))
                .Append(new InteractionProvenance(
                    InteractionProvenanceKind.DerivationRule,
                    RuleId)));
        var constraintId = InteractionStableIdentifier.Create(
            "lodc",
            observation.ScenarioId,
            InteractionStableIdentifier.Seed(observation.Seed),
            observation.ObservationId,
            observation.SourceEvidenceIdentifier,
            sourceGroupId,
            observation.TargetEntityId,
            string.Join("|", observation.ObservedRegionIds),
            InteractionStableIdentifier.Tick(observation.AvailableAtTick),
            observation.EvidenceBoundaryState.ToString(),
            observation.Visibility.ToString(),
            RuleId,
            usageRole.ToString(),
            string.Join("|", observation.SourceAncestryIds),
            string.Join("|", publicInputs),
            string.Join("|", provenance.Select(ProvenanceIdentity)));
        var constraint = new LocationObservationDerivedConstraint(
            constraintId,
            observation.ScenarioId,
            observation.Seed,
            observation.ObservationId,
            observation.SourceEvidenceIdentifier,
            sourceGroupId,
            observation.SourceAncestryIds,
            observation.TargetEntityId,
            observation.ObservedRegionIds,
            observation.AvailableAtTick,
            observation.EvidenceBoundaryState,
            observation.Visibility,
            RuleId,
            publicInputs,
            usageRole,
            provenance);

        return new LocationConstraintDerivationResult
        {
            Constraint = constraint,
            Audit = Audit(
                success: true,
                LocationConstraintDerivationFailureReason.None,
                constraintId)
        };

        LocationConstraintDerivationResult Failure(
            LocationConstraintDerivationFailureReason reason) => new()
            {
                Constraint = null,
                Audit = Audit(success: false, reason, constraintId: null)
            };

        LocationConstraintDerivationAudit Audit(
            bool success,
            LocationConstraintDerivationFailureReason reason,
            string? constraintId) => new()
            {
                RuleId = RuleId,
                RequiredInputInformation = RequiredInputs,
                ApplicabilityConditions = ApplicabilityConditions,
                PublicInputIdsUsed = publicInputs,
                SourceObservationId = observation.ObservationId,
                SourceEvidenceIdentifier = observation.SourceEvidenceIdentifier,
                TargetEntityId = observation.TargetEntityId,
                InputObservedRegionIds = observation.ObservedRegionIds,
                OutputAllowedRegionIds = success ? observation.ObservedRegionIds : [],
                UncertaintyPropagation = LocationRegionPropagation.DirectObservedRegionProjection,
                UsageRole = usageRole,
                SourceGroupId = sourceGroupId,
                SourceAncestryIds = observation.SourceAncestryIds,
                Success = success,
                FailureReason = reason,
                GeneratedConstraintId = constraintId
            };
    }

    private static string ProvenanceIdentity(InteractionProvenance provenance) =>
        $"{provenance.Kind}:{provenance.SourceIdentifier}";
}
