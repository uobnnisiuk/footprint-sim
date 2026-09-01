using FootprintSim.Evidence;
using FootprintSim.Inference.PastStates;
using FootprintSim.Interactions;

namespace FootprintSim.Inference.Derivation;

/// <summary>
/// Computes a conservative predecessor window from a public later-event observation and a
/// public bounded-duration dependency. It accepts no truth-side input.
/// </summary>
public sealed class BoundedDurationObservationConstraintDeriver
{
    public const string RuleId = "bounded-duration-predecessor-window-v1";

    private static IReadOnlyList<ObservationConstraintInputKind> RequiredInputs { get; } =
    [
        ObservationConstraintInputKind.SourceObservation,
        ObservationConstraintInputKind.OccurrenceTimeWindow,
        ObservationConstraintInputKind.PublicDependency,
        ObservationConstraintInputKind.DurationBounds,
        ObservationConstraintInputKind.TargetEntity,
        ObservationConstraintInputKind.EvidenceAvailability,
        ObservationConstraintInputKind.EvidenceVisibility,
        ObservationConstraintInputKind.EvidenceState,
        ObservationConstraintInputKind.SourceAncestry,
        ObservationConstraintInputKind.UsageRole
    ];

    private static IReadOnlyList<DerivationApplicabilityCondition> ApplicabilityConditions { get; } =
    [
        DerivationApplicabilityCondition.PublicEvidence,
        DerivationApplicabilityCondition.AvailableByCutoff,
        DerivationApplicabilityCondition.ConfirmedEvidenceBoundary,
        DerivationApplicabilityCondition.MatchingScenario,
        DerivationApplicabilityCondition.MatchingTarget,
        DerivationApplicabilityCondition.MatchingLaterEvent,
        DerivationApplicabilityCondition.KnownOccurrenceTime,
        DerivationApplicabilityCondition.CandidateGenerationOnly,
        DerivationApplicabilityCondition.NonNegativeDerivedWindow
    ];

    public ObservationConstraintDerivationResult Derive(
        PublicEventObservation observation,
        PublicBoundedDurationDependency dependency,
        long evidenceAvailabilityCutoff,
        PastStateEvidenceUsageRole usageRole)
    {
        ArgumentNullException.ThrowIfNull(observation);
        ArgumentNullException.ThrowIfNull(dependency);
        if (evidenceAvailabilityCutoff < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(evidenceAvailabilityCutoff));
        }

        var sourceGroupId = InteractionStableIdentifier.Create(
            "ods",
            string.Join("|", observation.SourceAncestryIds));
        var publicInputs = new[]
        {
            observation.ObservationId,
            dependency.PublicInputId,
            dependency.DurationSourceId
        }.Distinct(StringComparer.Ordinal).OrderBy(item => item, StringComparer.Ordinal).ToArray();

        if (observation.Visibility != InteractionVisibility.PublicEvidence)
        {
            return Failure(ObservationConstraintDerivationFailureReason.ObservationNotPublic);
        }

        if (observation.AvailableAtTick > evidenceAvailabilityCutoff)
        {
            return Failure(ObservationConstraintDerivationFailureReason.ObservationUnavailableAtCutoff);
        }

        if (observation.EvidenceBoundaryState != EvidenceState.Confirmed)
        {
            return Failure(ObservationConstraintDerivationFailureReason.UnsupportedEvidenceState);
        }

        if (!string.Equals(observation.ScenarioId, dependency.ScenarioId, StringComparison.Ordinal))
        {
            return Failure(ObservationConstraintDerivationFailureReason.ScenarioMismatch);
        }

        if (!string.Equals(observation.TargetEntityId, dependency.TargetEntityId, StringComparison.Ordinal))
        {
            return Failure(ObservationConstraintDerivationFailureReason.TargetMismatch);
        }

        if (!string.Equals(observation.EventKind, dependency.LaterEventKind, StringComparison.Ordinal))
        {
            return Failure(ObservationConstraintDerivationFailureReason.LaterEventMismatch);
        }

        if (usageRole != PastStateEvidenceUsageRole.CandidateGeneration)
        {
            return Failure(ObservationConstraintDerivationFailureReason.UnsupportedUsageRole);
        }

        if (observation.OccurrenceTime.Knowledge == OccurrenceTimeKnowledge.Unknown)
        {
            return Failure(ObservationConstraintDerivationFailureReason.UnknownOccurrenceTime);
        }

        var observedWindow = observation.OccurrenceTime.Window ??
            throw new InvalidOperationException("Known occurrence time requires a window.");
        if (observedWindow.StartTick < dependency.Duration.MaximumTicks)
        {
            return Failure(ObservationConstraintDerivationFailureReason.DerivedWindowBeforeZero);
        }

        InteractionTimeWindow outputWindow;
        try
        {
            outputWindow = new InteractionTimeWindow(
                checked(observedWindow.StartTick - dependency.Duration.MaximumTicks),
                checked(observedWindow.EndTick - dependency.Duration.MinimumTicks));
        }
        catch (OverflowException)
        {
            return Failure(ObservationConstraintDerivationFailureReason.ArithmeticOverflow);
        }

        var provenance = InteractionNormalization.Provenance(
            observation.Provenance
                .Concat(dependency.Provenance)
                .Append(new InteractionProvenance(
                    InteractionProvenanceKind.InteractionObservation,
                    observation.ObservationId))
                .Append(new InteractionProvenance(
                    InteractionProvenanceKind.DerivationRule,
                    RuleId))
                .Append(new InteractionProvenance(
                    InteractionProvenanceKind.ExternalSource,
                    dependency.PublicInputId)));
        var constraintId = InteractionStableIdentifier.Create(
            "odc",
            observation.ScenarioId,
            InteractionStableIdentifier.Seed(observation.Seed),
            observation.ObservationId,
            observation.SourceEvidenceIdentifier,
            sourceGroupId,
            observation.TargetEntityId,
            InteractionStableIdentifier.Tick(observedWindow.StartTick),
            InteractionStableIdentifier.Tick(observedWindow.EndTick),
            InteractionStableIdentifier.Tick(outputWindow.StartTick),
            InteractionStableIdentifier.Tick(outputWindow.EndTick),
            InteractionStableIdentifier.Tick(observation.AvailableAtTick),
            observation.EvidenceBoundaryState.ToString(),
            observation.Visibility.ToString(),
            RuleId,
            usageRole.ToString(),
            string.Join("|", observation.SourceAncestryIds),
            string.Join("|", dependency.AllowedPredecessorRegionIds),
            string.Join("|", publicInputs),
            string.Join("|", provenance.Select(ProvenanceIdentity)));
        var constraint = new ObservationDerivedConstraint(
            constraintId,
            observation.ScenarioId,
            observation.Seed,
            observation.ObservationId,
            observation.SourceEvidenceIdentifier,
            sourceGroupId,
            observation.SourceAncestryIds,
            observation.TargetEntityId,
            observedWindow,
            outputWindow,
            dependency.AllowedPredecessorRegionIds,
            observation.AvailableAtTick,
            observation.EvidenceBoundaryState,
            observation.Visibility,
            RuleId,
            publicInputs,
            usageRole,
            provenance);

        return new ObservationConstraintDerivationResult
        {
            Constraint = constraint,
            Audit = Audit(
                success: true,
                ObservationConstraintDerivationFailureReason.None,
                outputWindow,
                constraintId)
        };

        ObservationConstraintDerivationResult Failure(
            ObservationConstraintDerivationFailureReason reason) => new()
            {
                Constraint = null,
                Audit = Audit(success: false, reason, outputWindow: null, constraintId: null)
            };

        ObservationConstraintDerivationAudit Audit(
            bool success,
            ObservationConstraintDerivationFailureReason reason,
            InteractionTimeWindow? outputWindow,
            string? constraintId) => new()
            {
                RuleId = RuleId,
                RequiredInputInformation = RequiredInputs,
                ApplicabilityConditions = ApplicabilityConditions,
                PublicInputIdsUsed = publicInputs,
                SourceObservationId = observation.ObservationId,
                SourceEvidenceIdentifier = observation.SourceEvidenceIdentifier,
                PublicDependencyId = dependency.DependencyId,
                PublicDependencyInputId = dependency.PublicInputId,
                DurationSourceId = dependency.DurationSourceId,
                DurationBounds = dependency.Duration,
                OccurrenceTimeKnowledge = observation.OccurrenceTime.Knowledge,
                TimeWindowKind = observation.OccurrenceTime.WindowKind,
                InputTimeWindow = observation.OccurrenceTime.Window,
                OutputTimeWindow = outputWindow,
                UncertaintyPropagation = IntervalUncertaintyPropagation.ConservativeInverseDurationHull,
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
