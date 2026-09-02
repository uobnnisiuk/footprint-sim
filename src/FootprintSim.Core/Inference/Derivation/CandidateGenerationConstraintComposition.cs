using FootprintSim.Evidence;
using FootprintSim.Inference.PastStates;
using FootprintSim.Interactions;
using FootprintSim.Interactions.CandidateWarps;

namespace FootprintSim.Inference.Derivation;

public enum CandidateConstraintLogicalRole
{
    JointRequirement,
    Alternative,
    EquivalentSupport,
    NonRestricting
}

public enum CandidateConstraintInputState
{
    Applicable,
    Unknown,
    Unavailable,
    ExplicitNoRestriction
}

public enum CandidateConstraintDimensionSelection
{
    RegionAndTime,
    RegionOnly,
    TimeOnly
}

public enum CandidateConstraintDimensionRestriction
{
    Restricted,
    Unrestricted
}

public enum CandidateConstraintCompositionStatus
{
    Applied,
    NonRestricting,
    Unsatisfied,
    Invalid
}

public enum CandidateConstraintCompositionFailureReason
{
    None,
    UnsupportedUsageRole,
    ScenarioMismatch,
    SeedMismatch,
    TargetMismatch,
    TargetStateContextMismatch,
    CandidateDomainMismatch,
    PrivateInput,
    UnsupportedEvidenceState,
    ApplicableInputUnavailable,
    UnavailableStateMismatch,
    InvalidLogicalGroup,
    EquivalentPredicateMismatch,
    InvalidPredicateState,
    UnsatisfiedJointRequirements
}

/// <summary>
/// A candidate predicate with an explicit top value for each supported dimension.
/// An unrestricted dimension is not an empty set and does not reject a candidate.
/// </summary>
public sealed record CandidateConstraintPredicate
{
    private CandidateConstraintPredicate(
        CandidateConstraintDimensionRestriction regionRestriction,
        IReadOnlyList<string> allowedRegionIds,
        CandidateConstraintDimensionRestriction timeRestriction,
        InteractionTimeWindow? constrainedTimeWindow)
    {
        RegionRestriction = regionRestriction;
        AllowedRegionIds = allowedRegionIds;
        TimeRestriction = timeRestriction;
        ConstrainedTimeWindow = constrainedTimeWindow;
    }

    public CandidateConstraintDimensionRestriction RegionRestriction { get; }

    public IReadOnlyList<string> AllowedRegionIds { get; }

    public CandidateConstraintDimensionRestriction TimeRestriction { get; }

    public InteractionTimeWindow? ConstrainedTimeWindow { get; }

    public bool IsNonRestricting =>
        RegionRestriction == CandidateConstraintDimensionRestriction.Unrestricted &&
        TimeRestriction == CandidateConstraintDimensionRestriction.Unrestricted;

    public static CandidateConstraintPredicate Region(IEnumerable<string> allowedRegionIds) => new(
        CandidateConstraintDimensionRestriction.Restricted,
        InteractionNormalization.RegionIds(allowedRegionIds, requireAtLeastOne: true),
        CandidateConstraintDimensionRestriction.Unrestricted,
        constrainedTimeWindow: null);

    public static CandidateConstraintPredicate Time(InteractionTimeWindow constrainedTimeWindow)
    {
        ArgumentNullException.ThrowIfNull(constrainedTimeWindow);
        return new CandidateConstraintPredicate(
            CandidateConstraintDimensionRestriction.Unrestricted,
            [],
            CandidateConstraintDimensionRestriction.Restricted,
            constrainedTimeWindow);
    }

    public static CandidateConstraintPredicate RegionAndTime(
        IEnumerable<string> allowedRegionIds,
        InteractionTimeWindow constrainedTimeWindow)
    {
        ArgumentNullException.ThrowIfNull(constrainedTimeWindow);
        return new CandidateConstraintPredicate(
            CandidateConstraintDimensionRestriction.Restricted,
            InteractionNormalization.RegionIds(allowedRegionIds, requireAtLeastOne: true),
            CandidateConstraintDimensionRestriction.Restricted,
            constrainedTimeWindow);
    }

    public static CandidateConstraintPredicate Unrestricted() => new(
        CandidateConstraintDimensionRestriction.Unrestricted,
        [],
        CandidateConstraintDimensionRestriction.Unrestricted,
        constrainedTimeWindow: null);

    internal bool Matches(ObservationDerivedHistoricalCandidate candidate)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        var regionMatches = RegionRestriction == CandidateConstraintDimensionRestriction.Unrestricted ||
            AllowedRegionIds.Contains(candidate.RegionId, StringComparer.Ordinal);
        var timeMatches = TimeRestriction == CandidateConstraintDimensionRestriction.Unrestricted ||
            Overlaps(candidate.TimeWindow, ConstrainedTimeWindow!);
        return regionMatches && timeMatches;
    }

    internal string StableIdentity() => string.Join(
        ':',
        RegionRestriction,
        string.Join("|", AllowedRegionIds),
        TimeRestriction,
        ConstrainedTimeWindow is null
            ? "*"
            : $"{InteractionStableIdentifier.Tick(ConstrainedTimeWindow.StartTick)}-" +
              InteractionStableIdentifier.Tick(ConstrainedTimeWindow.EndTick));

    private static bool Overlaps(InteractionTimeWindow left, InteractionTimeWindow right) =>
        left.StartTick <= right.EndTick && right.StartTick <= left.EndTick;
}

/// <summary>
/// One public input to candidate composition. Source metadata is retained independently from the
/// logical group that determines how this input is evaluated.
/// </summary>
public sealed record CandidateGenerationConstraintComponent
{
    private CandidateGenerationConstraintComponent(
        string componentId,
        string scenarioId,
        int seed,
        string targetEntityId,
        string targetStateContextId,
        string candidateDomainId,
        CandidateConstraintInputState inputState,
        CandidateConstraintPredicate predicate,
        string? constraintId,
        string sourceAssertionId,
        string sourceObservationId,
        string sourceGroupId,
        IReadOnlyList<string> sourceAncestryIds,
        string derivationRuleId,
        IReadOnlyList<string> publicInputIds,
        long availableAtTick,
        EvidenceState evidenceBoundaryState,
        InteractionVisibility visibility,
        PastStateEvidenceUsageRole usageRole,
        IReadOnlyList<InteractionProvenance> provenance)
    {
        ComponentId = componentId;
        ScenarioId = scenarioId;
        Seed = seed;
        TargetEntityId = targetEntityId;
        TargetStateContextId = targetStateContextId;
        CandidateDomainId = candidateDomainId;
        InputState = inputState;
        Predicate = predicate;
        ConstraintId = constraintId;
        SourceAssertionId = sourceAssertionId;
        SourceObservationId = sourceObservationId;
        SourceGroupId = sourceGroupId;
        SourceAncestryIds = sourceAncestryIds;
        DerivationRuleId = derivationRuleId;
        PublicInputIds = publicInputIds;
        AvailableAtTick = availableAtTick;
        EvidenceBoundaryState = evidenceBoundaryState;
        Visibility = visibility;
        UsageRole = usageRole;
        Provenance = provenance;
    }

    public string ComponentId { get; }

    public string ScenarioId { get; }

    public int Seed { get; }

    public string TargetEntityId { get; }

    public string TargetStateContextId { get; }

    public string CandidateDomainId { get; }

    public CandidateConstraintInputState InputState { get; }

    public CandidateConstraintPredicate Predicate { get; }

    public string? ConstraintId { get; }

    public string SourceAssertionId { get; }

    public string SourceObservationId { get; }

    public string SourceGroupId { get; }

    public IReadOnlyList<string> SourceAncestryIds { get; }

    public string DerivationRuleId { get; }

    public IReadOnlyList<string> PublicInputIds { get; }

    public long AvailableAtTick { get; }

    public EvidenceState EvidenceBoundaryState { get; }

    public InteractionVisibility Visibility { get; }

    public PastStateEvidenceUsageRole UsageRole { get; }

    public IReadOnlyList<InteractionProvenance> Provenance { get; }

    public static CandidateGenerationConstraintComponent FromDerivedConstraint(
        ObservationDerivedConstraint constraint,
        string targetStateContextId,
        string candidateDomainId,
        CandidateConstraintDimensionSelection dimensionSelection)
    {
        ArgumentNullException.ThrowIfNull(constraint);
        var predicate = dimensionSelection switch
        {
            CandidateConstraintDimensionSelection.RegionAndTime =>
                CandidateConstraintPredicate.RegionAndTime(
                    constraint.AllowedRegionIds,
                    constraint.ConstrainedTimeWindow),
            CandidateConstraintDimensionSelection.RegionOnly =>
                CandidateConstraintPredicate.Region(constraint.AllowedRegionIds),
            CandidateConstraintDimensionSelection.TimeOnly =>
                CandidateConstraintPredicate.Time(constraint.ConstrainedTimeWindow),
            _ => throw new ArgumentOutOfRangeException(nameof(dimensionSelection))
        };

        return Create(
            constraint.ScenarioId,
            constraint.Seed,
            constraint.TargetEntityId,
            targetStateContextId,
            candidateDomainId,
            CandidateConstraintInputState.Applicable,
            predicate,
            constraint.ConstraintId,
            constraint.SourceEvidenceIdentifier,
            constraint.SourceObservationId,
            constraint.SourceGroupId,
            constraint.SourceAncestryIds,
            constraint.DerivationRuleId,
            constraint.PublicInputIds,
            constraint.AvailableAtTick,
            constraint.EvidenceBoundaryState,
            constraint.Visibility,
            constraint.UsageRole,
            constraint.Provenance);
    }

    public static CandidateGenerationConstraintComponent FromLocationDerivedConstraint(
        LocationObservationDerivedConstraint constraint,
        string targetStateContextId,
        string candidateDomainId)
    {
        ArgumentNullException.ThrowIfNull(constraint);
        return Create(
            constraint.ScenarioId,
            constraint.Seed,
            constraint.TargetEntityId,
            targetStateContextId,
            candidateDomainId,
            CandidateConstraintInputState.Applicable,
            CandidateConstraintPredicate.Region(constraint.AllowedRegionIds),
            constraint.ConstraintId,
            constraint.SourceEvidenceIdentifier,
            constraint.SourceObservationId,
            constraint.SourceGroupId,
            constraint.SourceAncestryIds,
            constraint.DerivationRuleId,
            constraint.PublicInputIds,
            constraint.AvailableAtTick,
            constraint.EvidenceBoundaryState,
            constraint.Visibility,
            constraint.UsageRole,
            constraint.Provenance);
    }

    public static CandidateGenerationConstraintComponent PublicPredicate(
        string scenarioId,
        int seed,
        string targetEntityId,
        string targetStateContextId,
        string candidateDomainId,
        CandidateConstraintPredicate predicate,
        string sourceAssertionId,
        string sourceObservationId,
        string sourceGroupId,
        IEnumerable<string> sourceAncestryIds,
        string derivationRuleId,
        IEnumerable<string> publicInputIds,
        long availableAtTick,
        EvidenceState evidenceBoundaryState,
        InteractionVisibility visibility,
        PastStateEvidenceUsageRole usageRole,
        IEnumerable<InteractionProvenance>? provenance = null) => Create(
            scenarioId,
            seed,
            targetEntityId,
            targetStateContextId,
            candidateDomainId,
            CandidateConstraintInputState.Applicable,
            predicate,
            constraintId: null,
            sourceAssertionId,
            sourceObservationId,
            sourceGroupId,
            sourceAncestryIds,
            derivationRuleId,
            publicInputIds,
            availableAtTick,
            evidenceBoundaryState,
            visibility,
            usageRole,
            provenance ?? []);

    public static CandidateGenerationConstraintComponent NonRestricting(
        string scenarioId,
        int seed,
        string targetEntityId,
        string targetStateContextId,
        string candidateDomainId,
        CandidateConstraintInputState inputState,
        string sourceAssertionId,
        string sourceObservationId,
        string sourceGroupId,
        IEnumerable<string> sourceAncestryIds,
        string derivationRuleId,
        IEnumerable<string> publicInputIds,
        long availableAtTick,
        EvidenceState evidenceBoundaryState,
        InteractionVisibility visibility,
        PastStateEvidenceUsageRole usageRole,
        IEnumerable<InteractionProvenance>? provenance = null)
    {
        if (inputState == CandidateConstraintInputState.Applicable)
        {
            throw new ArgumentException(
                "A non-restricting input cannot be marked Applicable.",
                nameof(inputState));
        }

        return Create(
            scenarioId,
            seed,
            targetEntityId,
            targetStateContextId,
            candidateDomainId,
            inputState,
            CandidateConstraintPredicate.Unrestricted(),
            constraintId: null,
            sourceAssertionId,
            sourceObservationId,
            sourceGroupId,
            sourceAncestryIds,
            derivationRuleId,
            publicInputIds,
            availableAtTick,
            evidenceBoundaryState,
            visibility,
            usageRole,
            provenance ?? []);
    }

    private static CandidateGenerationConstraintComponent Create(
        string scenarioId,
        int seed,
        string targetEntityId,
        string targetStateContextId,
        string candidateDomainId,
        CandidateConstraintInputState inputState,
        CandidateConstraintPredicate predicate,
        string? constraintId,
        string sourceAssertionId,
        string sourceObservationId,
        string sourceGroupId,
        IEnumerable<string> sourceAncestryIds,
        string derivationRuleId,
        IEnumerable<string> publicInputIds,
        long availableAtTick,
        EvidenceState evidenceBoundaryState,
        InteractionVisibility visibility,
        PastStateEvidenceUsageRole usageRole,
        IEnumerable<InteractionProvenance> provenance)
    {
        var normalizedScenario = InteractionNormalization.RequireIdentifier(
            scenarioId,
            nameof(scenarioId));
        var normalizedTarget = InteractionNormalization.RequireIdentifier(
            targetEntityId,
            nameof(targetEntityId));
        var normalizedContext = InteractionNormalization.RequireIdentifier(
            targetStateContextId,
            nameof(targetStateContextId));
        var normalizedDomain = InteractionNormalization.RequireIdentifier(
            candidateDomainId,
            nameof(candidateDomainId));
        ArgumentNullException.ThrowIfNull(predicate);
        var normalizedAssertion = InteractionNormalization.RequireIdentifier(
            sourceAssertionId,
            nameof(sourceAssertionId));
        var normalizedObservation = InteractionNormalization.RequireIdentifier(
            sourceObservationId,
            nameof(sourceObservationId));
        var normalizedSourceGroup = InteractionNormalization.RequireIdentifier(
            sourceGroupId,
            nameof(sourceGroupId));
        var normalizedRule = InteractionNormalization.RequireIdentifier(
            derivationRuleId,
            nameof(derivationRuleId));
        if (availableAtTick < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(availableAtTick));
        }

        var ancestry = sourceAncestryIds
            .Select(item => InteractionNormalization.RequireIdentifier(item, nameof(sourceAncestryIds)))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(item => item, StringComparer.Ordinal)
            .ToArray();
        var inputs = publicInputIds
            .Select(item => InteractionNormalization.RequireIdentifier(item, nameof(publicInputIds)))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(item => item, StringComparer.Ordinal)
            .ToArray();
        var suppliedProvenance = provenance.ToArray();
        var normalizedProvenance = InteractionNormalization.Provenance(
            suppliedProvenance.Length == 0
                ? [new InteractionProvenance(
                    InteractionProvenanceKind.EvidenceRecord,
                    normalizedAssertion)]
                : suppliedProvenance);
        var normalizedConstraintId = constraintId is null
            ? InteractionStableIdentifier.Create(
                "pcn",
                normalizedScenario,
                InteractionStableIdentifier.Seed(seed),
                normalizedTarget,
                normalizedContext,
                normalizedDomain,
                normalizedAssertion,
                normalizedObservation,
                normalizedSourceGroup,
                normalizedRule,
                predicate.StableIdentity(),
                string.Join("|", inputs))
            : InteractionNormalization.RequireIdentifier(constraintId, nameof(constraintId));
        var componentId = InteractionStableIdentifier.Create(
            "cgc",
            normalizedScenario,
            InteractionStableIdentifier.Seed(seed),
            normalizedTarget,
            normalizedContext,
            normalizedDomain,
            inputState.ToString(),
            predicate.StableIdentity(),
            normalizedConstraintId,
            normalizedAssertion,
            normalizedObservation,
            normalizedSourceGroup,
            string.Join("|", ancestry),
            normalizedRule,
            string.Join("|", inputs),
            InteractionStableIdentifier.Tick(availableAtTick),
            evidenceBoundaryState.ToString(),
            visibility.ToString(),
            usageRole.ToString(),
            string.Join("|", normalizedProvenance.Select(ProvenanceIdentity)));

        return new CandidateGenerationConstraintComponent(
            componentId,
            normalizedScenario,
            seed,
            normalizedTarget,
            normalizedContext,
            normalizedDomain,
            inputState,
            predicate,
            inputState == CandidateConstraintInputState.Applicable ? normalizedConstraintId : null,
            normalizedAssertion,
            normalizedObservation,
            normalizedSourceGroup,
            ancestry,
            normalizedRule,
            inputs,
            availableAtTick,
            evidenceBoundaryState,
            visibility,
            usageRole,
            normalizedProvenance);
    }

    private static string ProvenanceIdentity(InteractionProvenance provenance) =>
        $"{provenance.Kind}:{provenance.SourceIdentifier}";
}

public sealed record CandidateGenerationConstraintGroup
{
    private CandidateGenerationConstraintGroup(
        string groupId,
        CandidateConstraintLogicalRole logicalRole,
        IReadOnlyList<CandidateGenerationConstraintComponent> components,
        int duplicateReferencesSuppressed)
    {
        GroupId = groupId;
        LogicalRole = logicalRole;
        Components = components;
        DuplicateReferencesSuppressed = duplicateReferencesSuppressed;
    }

    public string GroupId { get; }

    public CandidateConstraintLogicalRole LogicalRole { get; }

    public IReadOnlyList<CandidateGenerationConstraintComponent> Components { get; }

    public int DuplicateReferencesSuppressed { get; }

    public static CandidateGenerationConstraintGroup Create(
        CandidateConstraintLogicalRole logicalRole,
        IEnumerable<CandidateGenerationConstraintComponent> components)
    {
        ArgumentNullException.ThrowIfNull(components);
        var raw = components.ToArray();
        var normalized = raw
            .DistinctBy(item => item.ComponentId, StringComparer.Ordinal)
            .OrderBy(item => item.ComponentId, StringComparer.Ordinal)
            .ToArray();
        var groupId = InteractionStableIdentifier.Create(
            "cgg",
            logicalRole.ToString(),
            string.Join("|", normalized.Select(item => item.ComponentId)));
        return new CandidateGenerationConstraintGroup(
            groupId,
            logicalRole,
            normalized,
            raw.Length - normalized.Length);
    }
}

public sealed record CandidateGenerationConstraintCompositionRequest
{
    public required string ScenarioId { get; init; }

    public required int Seed { get; init; }

    public required string TargetEntityId { get; init; }

    public required string TargetStateContextId { get; init; }

    public required string CandidateDomainId { get; init; }

    public required long EvidenceAvailabilityCutoff { get; init; }

    public required PastStateEvidenceUsageRole UsageRole { get; init; }

    public IReadOnlyList<PublicCandidateWarpStateDefinition> PublicCandidateStates { get; init; } = [];

    public IReadOnlyList<CandidateGenerationConstraintGroup> ConstraintGroups { get; init; } = [];
}

public sealed record CandidateGenerationConstraintComponentAudit
{
    public required string ComponentId { get; init; }

    public string? ConstraintId { get; init; }

    public required CandidateConstraintInputState InputState { get; init; }

    public required CandidateConstraintPredicate Predicate { get; init; }

    public required string SourceAssertionId { get; init; }

    public required string SourceObservationId { get; init; }

    public required string SourceGroupId { get; init; }

    public IReadOnlyList<string> SourceAncestryIds { get; init; } = [];

    public required string DerivationRuleId { get; init; }

    public IReadOnlyList<string> PublicInputIds { get; init; } = [];

    public IReadOnlyList<InteractionProvenance> Provenance { get; init; } = [];
}

public sealed record CandidateGenerationConstraintGroupAudit
{
    public required string GroupId { get; init; }

    public required CandidateConstraintLogicalRole LogicalRole { get; init; }

    public IReadOnlyList<CandidateGenerationConstraintComponentAudit> Components { get; init; } = [];

    public required int ActivePredicateCount { get; init; }

    public required int DuplicateConstraintsSuppressed { get; init; }

    public required int AlternativeBranchesEvaluated { get; init; }

    public required bool Applied { get; init; }
}

public sealed record CandidateGenerationConstraintCompositionAudit
{
    public required string CompositionId { get; init; }

    public required CandidateConstraintCompositionStatus Status { get; init; }

    public required CandidateConstraintCompositionFailureReason FailureReason { get; init; }

    public required string TargetEntityId { get; init; }

    public required string TargetStateContextId { get; init; }

    public required string CandidateDomainId { get; init; }

    public required PastStateEvidenceUsageRole UsageRole { get; init; }

    public IReadOnlyList<CandidateGenerationConstraintGroupAudit> Groups { get; init; } = [];

    public IReadOnlyList<string> ComponentConstraintIds { get; init; } = [];

    public IReadOnlyList<string> DerivationRuleIds { get; init; } = [];

    public IReadOnlyList<string> SourceObservationIds { get; init; } = [];

    public IReadOnlyList<string> SourceGroupIds { get; init; } = [];

    public IReadOnlyList<string> SourceAncestryIds { get; init; } = [];

    public required int DistinctSourceGroupCount { get; init; }

    public required int InputComponentsReceived { get; init; }

    public required int ConstraintsReceived { get; init; }

    public required int PredicatesApplied { get; init; }

    public required int CandidatePredicateChecks { get; init; }

    public required int DuplicateConstraintsSuppressed { get; init; }

    public required int AlternativeBranchesEvaluated { get; init; }
}

public sealed record CandidateGenerationConstraintCompositionResult
{
    public required string CompositionId { get; init; }

    public required CandidateConstraintCompositionStatus Status { get; init; }

    public required CandidateConstraintCompositionFailureReason FailureReason { get; init; }

    public required int InitialCandidateCount { get; init; }

    public IReadOnlyList<ObservationDerivedHistoricalCandidate> Candidates { get; init; } = [];

    public required int CandidateGenerationWork { get; init; }

    public required CandidateGenerationConstraintCompositionAudit Audit { get; init; }
}

/// <summary>
/// Composes already-justified public CandidateGeneration predicates. Source grouping is audited
/// but never selects AND/OR behavior; the declared logical role alone controls evaluation.
/// </summary>
public sealed class CandidateGenerationConstraintComposer
{
    public CandidateGenerationConstraintCompositionResult Compose(
        CandidateGenerationConstraintCompositionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.ScenarioId);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.TargetEntityId);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.TargetStateContextId);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.CandidateDomainId);
        if (request.EvidenceAvailabilityCutoff < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(request.EvidenceAvailabilityCutoff));
        }

        var groups = request.ConstraintGroups
            .DistinctBy(item => item.GroupId, StringComparer.Ordinal)
            .OrderBy(item => item.GroupId, StringComparer.Ordinal)
            .ToArray();
        var components = groups
            .SelectMany(item => item.Components)
            .DistinctBy(item => item.ComponentId, StringComparer.Ordinal)
            .OrderBy(item => item.ComponentId, StringComparer.Ordinal)
            .ToArray();
        var compositionId = InteractionStableIdentifier.Create(
            "cgcx",
            request.ScenarioId,
            InteractionStableIdentifier.Seed(request.Seed),
            request.TargetEntityId,
            request.TargetStateContextId,
            request.CandidateDomainId,
            request.UsageRole.ToString(),
            InteractionStableIdentifier.Tick(request.EvidenceAvailabilityCutoff),
            groups.Length == 0
                ? "no-constraint-groups"
                : string.Join("|", groups.Select(item => $"{item.LogicalRole}:{item.GroupId}")));
        var initial = new ObservationDerivedHistoricalCandidateReconstructor().Reconstruct(
            request.ScenarioId,
            request.Seed,
            request.TargetEntityId,
            request.PublicCandidateStates,
            constraint: null);
        var validationFailure = Validate(request, groups, components);
        if (validationFailure != CandidateConstraintCompositionFailureReason.None)
        {
            return Result(
                CandidateConstraintCompositionStatus.Invalid,
                validationFailure,
                [],
                predicatesApplied: 0,
                candidatePredicateChecks: 0);
        }

        var activeGroups = groups
            .Where(item => item.LogicalRole != CandidateConstraintLogicalRole.NonRestricting)
            .ToArray();
        if (activeGroups.Length == 0)
        {
            return Result(
                CandidateConstraintCompositionStatus.NonRestricting,
                CandidateConstraintCompositionFailureReason.None,
                initial.Candidates,
                predicatesApplied: 0,
                candidatePredicateChecks: 0);
        }

        var normalizedPredicates = activeGroups.ToDictionary(
            item => item.GroupId,
            ActivePredicates,
            StringComparer.Ordinal);
        var predicatesApplied = normalizedPredicates.Values.Sum(item => item.Length);
        var candidates = initial.Candidates
            .Where(candidate => activeGroups.All(group => GroupMatches(
                group.LogicalRole,
                normalizedPredicates[group.GroupId],
                candidate)))
            .ToArray();
        var status = candidates.Length == 0
            ? CandidateConstraintCompositionStatus.Unsatisfied
            : CandidateConstraintCompositionStatus.Applied;
        return Result(
            status,
            status == CandidateConstraintCompositionStatus.Unsatisfied
                ? CandidateConstraintCompositionFailureReason.UnsatisfiedJointRequirements
                : CandidateConstraintCompositionFailureReason.None,
            candidates,
            predicatesApplied,
            initial.Candidates.Count * predicatesApplied);

        CandidateGenerationConstraintCompositionResult Result(
            CandidateConstraintCompositionStatus status,
            CandidateConstraintCompositionFailureReason failureReason,
            IReadOnlyList<ObservationDerivedHistoricalCandidate> candidates,
            int predicatesApplied,
            int candidatePredicateChecks)
        {
            var groupAudits = groups.Select(group =>
            {
                var active = group.Components
                    .Where(item => item.InputState == CandidateConstraintInputState.Applicable)
                    .ToArray();
                var duplicateSuppressed = group.DuplicateReferencesSuppressed +
                    (group.LogicalRole == CandidateConstraintLogicalRole.EquivalentSupport
                        ? Math.Max(0, active.Length - 1)
                        : 0);
                return new CandidateGenerationConstraintGroupAudit
                {
                    GroupId = group.GroupId,
                    LogicalRole = group.LogicalRole,
                    Components = group.Components.Select(ComponentAudit).ToArray(),
                    ActivePredicateCount = group.LogicalRole switch
                    {
                        CandidateConstraintLogicalRole.EquivalentSupport when active.Length > 0 => 1,
                        CandidateConstraintLogicalRole.NonRestricting => 0,
                        _ => active.Length
                    },
                    DuplicateConstraintsSuppressed = duplicateSuppressed,
                    AlternativeBranchesEvaluated =
                        group.LogicalRole == CandidateConstraintLogicalRole.Alternative
                            ? active.Length
                            : 0,
                    Applied =
                        (status is CandidateConstraintCompositionStatus.Applied or
                            CandidateConstraintCompositionStatus.Unsatisfied) &&
                        group.LogicalRole != CandidateConstraintLogicalRole.NonRestricting
                };
            }).ToArray();
            var audit = new CandidateGenerationConstraintCompositionAudit
            {
                CompositionId = compositionId,
                Status = status,
                FailureReason = failureReason,
                TargetEntityId = request.TargetEntityId,
                TargetStateContextId = request.TargetStateContextId,
                CandidateDomainId = request.CandidateDomainId,
                UsageRole = request.UsageRole,
                Groups = groupAudits,
                ComponentConstraintIds = components
                    .Select(item => item.ConstraintId)
                    .Where(item => item is not null)
                    .Select(item => item!)
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(item => item, StringComparer.Ordinal)
                    .ToArray(),
                DerivationRuleIds = components
                    .Select(item => item.DerivationRuleId)
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(item => item, StringComparer.Ordinal)
                    .ToArray(),
                SourceObservationIds = components
                    .Select(item => item.SourceObservationId)
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(item => item, StringComparer.Ordinal)
                    .ToArray(),
                SourceGroupIds = components
                    .Select(item => item.SourceGroupId)
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(item => item, StringComparer.Ordinal)
                    .ToArray(),
                SourceAncestryIds = components
                    .SelectMany(item => item.SourceAncestryIds)
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(item => item, StringComparer.Ordinal)
                    .ToArray(),
                DistinctSourceGroupCount = components
                    .Select(item => item.SourceGroupId)
                    .Distinct(StringComparer.Ordinal)
                    .Count(),
                InputComponentsReceived = components.Length,
                ConstraintsReceived = components.Count(item =>
                    item.InputState == CandidateConstraintInputState.Applicable),
                PredicatesApplied = predicatesApplied,
                CandidatePredicateChecks = candidatePredicateChecks,
                DuplicateConstraintsSuppressed = groupAudits.Sum(item =>
                    item.DuplicateConstraintsSuppressed),
                AlternativeBranchesEvaluated = groupAudits.Sum(item =>
                    item.AlternativeBranchesEvaluated)
            };
            return new CandidateGenerationConstraintCompositionResult
            {
                CompositionId = compositionId,
                Status = status,
                FailureReason = failureReason,
                InitialCandidateCount = initial.InitialCandidateCount,
                Candidates = candidates,
                CandidateGenerationWork = initial.CandidateGenerationWork + candidatePredicateChecks,
                Audit = audit
            };
        }
    }

    private static CandidateConstraintCompositionFailureReason Validate(
        CandidateGenerationConstraintCompositionRequest request,
        IReadOnlyList<CandidateGenerationConstraintGroup> groups,
        IReadOnlyList<CandidateGenerationConstraintComponent> components)
    {
        if (request.UsageRole != PastStateEvidenceUsageRole.CandidateGeneration)
        {
            return CandidateConstraintCompositionFailureReason.UnsupportedUsageRole;
        }

        foreach (var component in components)
        {
            if (!string.Equals(component.ScenarioId, request.ScenarioId, StringComparison.Ordinal))
            {
                return CandidateConstraintCompositionFailureReason.ScenarioMismatch;
            }

            if (component.Seed != request.Seed)
            {
                return CandidateConstraintCompositionFailureReason.SeedMismatch;
            }

            if (!string.Equals(component.TargetEntityId, request.TargetEntityId, StringComparison.Ordinal))
            {
                return CandidateConstraintCompositionFailureReason.TargetMismatch;
            }

            if (!string.Equals(
                component.TargetStateContextId,
                request.TargetStateContextId,
                StringComparison.Ordinal))
            {
                return CandidateConstraintCompositionFailureReason.TargetStateContextMismatch;
            }

            if (!string.Equals(
                component.CandidateDomainId,
                request.CandidateDomainId,
                StringComparison.Ordinal))
            {
                return CandidateConstraintCompositionFailureReason.CandidateDomainMismatch;
            }

            if (component.UsageRole != PastStateEvidenceUsageRole.CandidateGeneration)
            {
                return CandidateConstraintCompositionFailureReason.UnsupportedUsageRole;
            }

            if (component.Visibility != InteractionVisibility.PublicEvidence)
            {
                return CandidateConstraintCompositionFailureReason.PrivateInput;
            }

            if (component.InputState == CandidateConstraintInputState.Applicable)
            {
                if (component.EvidenceBoundaryState != EvidenceState.Confirmed)
                {
                    return CandidateConstraintCompositionFailureReason.UnsupportedEvidenceState;
                }

                if (component.AvailableAtTick > request.EvidenceAvailabilityCutoff)
                {
                    return CandidateConstraintCompositionFailureReason.ApplicableInputUnavailable;
                }

                if (component.Predicate.IsNonRestricting || component.ConstraintId is null)
                {
                    return CandidateConstraintCompositionFailureReason.InvalidPredicateState;
                }
            }
            else
            {
                if (!component.Predicate.IsNonRestricting || component.ConstraintId is not null)
                {
                    return CandidateConstraintCompositionFailureReason.InvalidPredicateState;
                }

                if (component.InputState == CandidateConstraintInputState.Unavailable &&
                    component.AvailableAtTick <= request.EvidenceAvailabilityCutoff)
                {
                    return CandidateConstraintCompositionFailureReason.UnavailableStateMismatch;
                }
            }
        }

        foreach (var group in groups)
        {
            var applicable = group.Components
                .Where(item => item.InputState == CandidateConstraintInputState.Applicable)
                .ToArray();
            if (group.Components.Count == 0 ||
                group.LogicalRole == CandidateConstraintLogicalRole.NonRestricting &&
                applicable.Length != 0 ||
                group.LogicalRole != CandidateConstraintLogicalRole.NonRestricting &&
                applicable.Length != group.Components.Count ||
                group.LogicalRole == CandidateConstraintLogicalRole.Alternative &&
                applicable.Length < 2 ||
                group.LogicalRole == CandidateConstraintLogicalRole.EquivalentSupport &&
                applicable.Length < 2)
            {
                return CandidateConstraintCompositionFailureReason.InvalidLogicalGroup;
            }

            if (group.LogicalRole == CandidateConstraintLogicalRole.EquivalentSupport &&
                applicable.Select(item => item.Predicate.StableIdentity())
                    .Distinct(StringComparer.Ordinal)
                    .Count() != 1)
            {
                return CandidateConstraintCompositionFailureReason.EquivalentPredicateMismatch;
            }
        }

        return CandidateConstraintCompositionFailureReason.None;
    }

    private static CandidateConstraintPredicate[] ActivePredicates(
        CandidateGenerationConstraintGroup group)
    {
        var predicates = group.Components
            .Where(item => item.InputState == CandidateConstraintInputState.Applicable)
            .Select(item => item.Predicate)
            .ToArray();
        return group.LogicalRole == CandidateConstraintLogicalRole.EquivalentSupport &&
            predicates.Length > 0
                ? [predicates[0]]
                : predicates;
    }

    private static bool GroupMatches(
        CandidateConstraintLogicalRole logicalRole,
        IReadOnlyList<CandidateConstraintPredicate> predicates,
        ObservationDerivedHistoricalCandidate candidate) => logicalRole switch
        {
            CandidateConstraintLogicalRole.JointRequirement =>
                predicates.All(predicate => predicate.Matches(candidate)),
            CandidateConstraintLogicalRole.Alternative =>
                predicates.Any(predicate => predicate.Matches(candidate)),
            CandidateConstraintLogicalRole.EquivalentSupport => predicates[0].Matches(candidate),
            CandidateConstraintLogicalRole.NonRestricting => true,
            _ => throw new ArgumentOutOfRangeException(nameof(logicalRole))
        };

    private static CandidateGenerationConstraintComponentAudit ComponentAudit(
        CandidateGenerationConstraintComponent component) => new()
        {
            ComponentId = component.ComponentId,
            ConstraintId = component.ConstraintId,
            InputState = component.InputState,
            Predicate = component.Predicate,
            SourceAssertionId = component.SourceAssertionId,
            SourceObservationId = component.SourceObservationId,
            SourceGroupId = component.SourceGroupId,
            SourceAncestryIds = component.SourceAncestryIds,
            DerivationRuleId = component.DerivationRuleId,
            PublicInputIds = component.PublicInputIds,
            Provenance = component.Provenance
        };
}
