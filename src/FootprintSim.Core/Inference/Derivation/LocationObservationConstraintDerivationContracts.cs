using FootprintSim.Evidence;
using FootprintSim.Inference.PastStates;
using FootprintSim.Interactions;

namespace FootprintSim.Inference.Derivation;

public enum LocationConstraintInputKind
{
    SourceObservation,
    TargetEntity,
    ObservedRegionSet,
    EvidenceAvailability,
    EvidenceVisibility,
    EvidenceState,
    SourceAncestry,
    UsageRole
}

public enum LocationConstraintApplicabilityCondition
{
    NonEmptyObservedRegionSet,
    PublicEvidence,
    AvailableByCutoff,
    ConfirmedEvidenceBoundary,
    CandidateGenerationOnly
}

public enum LocationConstraintDerivationFailureReason
{
    None,
    ObservationNotPublic,
    ObservationUnavailableAtCutoff,
    UnsupportedEvidenceState,
    UnsupportedUsageRole
}

public enum LocationRegionPropagation
{
    DirectObservedRegionProjection
}

/// <summary>
/// A public observation whose location content is a finite set of possible Regions. The set is
/// epistemic content: it does not assert one hidden exact Region and accepts no truth-side input.
/// </summary>
public sealed record PublicObservedRegionObservation
{
    private PublicObservedRegionObservation(
        string observationId,
        string scenarioId,
        int seed,
        string sourceEvidenceIdentifier,
        string targetEntityId,
        IReadOnlyList<string> observedRegionIds,
        long availableAtTick,
        EvidenceState evidenceBoundaryState,
        InteractionVisibility visibility,
        IReadOnlyList<string> sourceAncestryIds,
        IReadOnlyList<InteractionProvenance> provenance)
    {
        ObservationId = observationId;
        ScenarioId = scenarioId;
        Seed = seed;
        SourceEvidenceIdentifier = sourceEvidenceIdentifier;
        TargetEntityId = targetEntityId;
        ObservedRegionIds = observedRegionIds;
        AvailableAtTick = availableAtTick;
        EvidenceBoundaryState = evidenceBoundaryState;
        Visibility = visibility;
        SourceAncestryIds = sourceAncestryIds;
        Provenance = provenance;
    }

    public string ObservationId { get; }

    public string ScenarioId { get; }

    public int Seed { get; }

    public string SourceEvidenceIdentifier { get; }

    public string TargetEntityId { get; }

    public IReadOnlyList<string> ObservedRegionIds { get; }

    public long AvailableAtTick { get; }

    public EvidenceState EvidenceBoundaryState { get; }

    public InteractionVisibility Visibility { get; }

    public IReadOnlyList<string> SourceAncestryIds { get; }

    public IReadOnlyList<InteractionProvenance> Provenance { get; }

    public static PublicObservedRegionObservation Create(
        string scenarioId,
        int seed,
        string sourceEvidenceIdentifier,
        string targetEntityId,
        IEnumerable<string> observedRegionIds,
        long availableAtTick,
        EvidenceState evidenceBoundaryState,
        InteractionVisibility visibility = InteractionVisibility.PublicEvidence,
        IEnumerable<string>? sourceAncestryIds = null,
        IEnumerable<InteractionProvenance>? additionalProvenance = null)
    {
        var normalizedScenario = InteractionNormalization.RequireIdentifier(
            scenarioId,
            nameof(scenarioId));
        var normalizedSource = InteractionNormalization.RequireIdentifier(
            sourceEvidenceIdentifier,
            nameof(sourceEvidenceIdentifier));
        var normalizedTarget = InteractionNormalization.RequireIdentifier(
            targetEntityId,
            nameof(targetEntityId));
        var regions = InteractionNormalization.RegionIds(observedRegionIds, requireAtLeastOne: true);
        if (availableAtTick < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(availableAtTick));
        }

        if (visibility == InteractionVisibility.PrivateTruth)
        {
            throw new ArgumentException(
                "A public location observation cannot use private-truth visibility.",
                nameof(visibility));
        }

        var ancestry = (sourceAncestryIds ?? [])
            .Append(normalizedSource)
            .Select(item => InteractionNormalization.RequireIdentifier(item, nameof(sourceAncestryIds)))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(item => item, StringComparer.Ordinal)
            .ToArray();
        var provenance = InteractionNormalization.Provenance(
            new[]
            {
                new InteractionProvenance(
                    InteractionProvenanceKind.EvidenceRecord,
                    normalizedSource)
            }.Concat(additionalProvenance ?? []));
        var observationId = InteractionStableIdentifier.Create(
            "locobs",
            normalizedScenario,
            InteractionStableIdentifier.Seed(seed),
            normalizedSource,
            normalizedTarget,
            string.Join("|", regions),
            InteractionStableIdentifier.Tick(availableAtTick),
            evidenceBoundaryState.ToString(),
            visibility.ToString(),
            string.Join("|", ancestry),
            string.Join("|", provenance.Select(ProvenanceIdentity)));

        return new PublicObservedRegionObservation(
            observationId,
            normalizedScenario,
            seed,
            normalizedSource,
            normalizedTarget,
            regions,
            availableAtTick,
            evidenceBoundaryState,
            visibility,
            ancestry,
            provenance);
    }

    private static string ProvenanceIdentity(InteractionProvenance provenance) =>
        $"{provenance.Kind}:{provenance.SourceIdentifier}";
}

public sealed record LocationObservationDerivedConstraint
{
    internal LocationObservationDerivedConstraint(
        string constraintId,
        string scenarioId,
        int seed,
        string sourceObservationId,
        string sourceEvidenceIdentifier,
        string sourceGroupId,
        IReadOnlyList<string> sourceAncestryIds,
        string targetEntityId,
        IReadOnlyList<string> allowedRegionIds,
        long availableAtTick,
        EvidenceState evidenceBoundaryState,
        InteractionVisibility visibility,
        string derivationRuleId,
        IReadOnlyList<string> publicInputIds,
        PastStateEvidenceUsageRole usageRole,
        IReadOnlyList<InteractionProvenance> provenance)
    {
        ConstraintId = constraintId;
        ScenarioId = scenarioId;
        Seed = seed;
        SourceObservationId = sourceObservationId;
        SourceEvidenceIdentifier = sourceEvidenceIdentifier;
        SourceGroupId = sourceGroupId;
        SourceAncestryIds = sourceAncestryIds;
        TargetEntityId = targetEntityId;
        AllowedRegionIds = allowedRegionIds;
        AvailableAtTick = availableAtTick;
        EvidenceBoundaryState = evidenceBoundaryState;
        Visibility = visibility;
        DerivationRuleId = derivationRuleId;
        PublicInputIds = publicInputIds;
        UsageRole = usageRole;
        Provenance = provenance;
    }

    public string ConstraintId { get; }

    public string ScenarioId { get; }

    public int Seed { get; }

    public string SourceObservationId { get; }

    public string SourceEvidenceIdentifier { get; }

    public string SourceGroupId { get; }

    public IReadOnlyList<string> SourceAncestryIds { get; }

    public string TargetEntityId { get; }

    public IReadOnlyList<string> AllowedRegionIds { get; }

    public long AvailableAtTick { get; }

    public EvidenceState EvidenceBoundaryState { get; }

    public InteractionVisibility Visibility { get; }

    public string DerivationRuleId { get; }

    public IReadOnlyList<string> PublicInputIds { get; }

    public PastStateEvidenceUsageRole UsageRole { get; }

    public IReadOnlyList<InteractionProvenance> Provenance { get; }
}

public sealed record LocationConstraintDerivationAudit
{
    public required string RuleId { get; init; }

    public IReadOnlyList<LocationConstraintInputKind> RequiredInputInformation { get; init; } = [];

    public IReadOnlyList<LocationConstraintApplicabilityCondition> ApplicabilityConditions { get; init; } = [];

    public IReadOnlyList<string> PublicInputIdsUsed { get; init; } = [];

    public required string SourceObservationId { get; init; }

    public required string SourceEvidenceIdentifier { get; init; }

    public required string TargetEntityId { get; init; }

    public IReadOnlyList<string> InputObservedRegionIds { get; init; } = [];

    public IReadOnlyList<string> OutputAllowedRegionIds { get; init; } = [];

    public required LocationRegionPropagation UncertaintyPropagation { get; init; }

    public required PastStateEvidenceUsageRole UsageRole { get; init; }

    public required string SourceGroupId { get; init; }

    public IReadOnlyList<string> SourceAncestryIds { get; init; } = [];

    public required bool Success { get; init; }

    public required LocationConstraintDerivationFailureReason FailureReason { get; init; }

    public string? GeneratedConstraintId { get; init; }
}

public sealed record LocationConstraintDerivationResult
{
    public LocationObservationDerivedConstraint? Constraint { get; init; }

    public required LocationConstraintDerivationAudit Audit { get; init; }
}
