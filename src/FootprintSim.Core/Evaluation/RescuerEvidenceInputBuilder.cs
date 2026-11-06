using FootprintSim.Evidence;
using FootprintSim.Inference;
using FootprintSim.Interactions;
using FootprintSim.Interactions.Tracing;

namespace FootprintSim.Evaluation;

public sealed record RescuerEvidenceInput
{
    internal RescuerEvidenceInput(
        string rescuerId,
        IReadOnlyList<ReceivedInteractionRecord> receivedRecords,
        SearchStrategyInput input)
    {
        RescuerId = rescuerId;
        ReceivedRecords = receivedRecords;
        Input = input;
    }

    public string RescuerId { get; }

    public IReadOnlyList<ReceivedInteractionRecord> ReceivedRecords { get; }

    public SearchStrategyInput Input { get; }
}

/// <summary>
/// Connects one device-held record to the existing inference contracts only after
/// receipt by the specified rescuer. It does not publish to a shared evidence store.
/// </summary>
public sealed class RescuerEvidenceInputBuilder
{
    public RescuerEvidenceInput Build(
        RescueContext publicContext,
        int seed,
        long currentTick,
        string rescuerId,
        DeviceHeldInteractionRecord heldRecord,
        IEnumerable<InteractionRecordReceipt> receipts,
        IReadOnlyList<string>? publicSearchDomainRegionIds = null)
    {
        ArgumentNullException.ThrowIfNull(publicContext);
        ArgumentNullException.ThrowIfNull(heldRecord);
        ArgumentNullException.ThrowIfNull(receipts);
        var normalizedRescuer = InteractionNormalization.RequireIdentifier(rescuerId, nameof(rescuerId));
        if (currentTick < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(currentTick));
        }

        var original = heldRecord.Observation;
        if (!string.Equals(publicContext.ScenarioId, original.ScenarioId, StringComparison.Ordinal) ||
            seed != original.Seed)
        {
            throw new ArgumentException("The held record and rescue query belong to different runs.");
        }

        if (!publicContext.PublicDeviceIds.Contains(heldRecord.HolderDeviceId, StringComparer.Ordinal))
        {
            throw new ArgumentException("The holder must be an explicitly declared public device.");
        }

        // A ledger can contain other recipients, runs, records and future deliveries.
        // Those entries cannot establish access for this query and are ignored.
        var matching = receipts
            .Where(receipt =>
                receipt.ReceivedAtTick <= currentTick &&
                receipt.Seed == seed &&
                string.Equals(receipt.ScenarioId, original.ScenarioId, StringComparison.Ordinal) &&
                string.Equals(receipt.OriginalRecordId, heldRecord.OriginalRecordId, StringComparison.Ordinal) &&
                string.Equals(receipt.HolderDeviceId, heldRecord.HolderDeviceId, StringComparison.Ordinal) &&
                string.Equals(receipt.RescuerId, normalizedRescuer, StringComparison.Ordinal))
            .OrderBy(receipt => receipt.ReceivedAtTick)
            .ToArray();
        if (matching.Any(receipt =>
            receipt.ReceivedAtTick < heldRecord.StoredAtTick ||
            receipt.ReceivedAtTick < original.AvailableAtTick ||
            receipt.ReceivedAtTick < original.ObservedTimeWindow.EndTick))
        {
            throw new ArgumentException(
                "A matching receipt cannot precede storage, original availability or observation.",
                nameof(receipts));
        }

        var firstReceipt = matching.FirstOrDefault();
        IReadOnlyList<ReceivedInteractionRecord> received = firstReceipt is null
            ? []
            : [new ReceivedInteractionRecord(heldRecord, normalizedRescuer, firstReceipt.ReceivedAtTick)];
        IReadOnlyList<CrossThreadConstraint> crossThread = [];
        IReadOnlyList<CausalTracingConstraint> tracing = [];
        if (firstReceipt is not null &&
            CrossThreadConstraintDeriver.DerivePersonEncounterCoLocation(
                original,
                publicContext.TargetPersonId) is { } derived)
        {
            var accessible = ForReceiver(derived, normalizedRescuer, firstReceipt.ReceivedAtTick);
            crossThread = [accessible];
            tracing = [ForTracing(accessible)];
        }

        return new RescuerEvidenceInput(normalizedRescuer, received, new SearchStrategyInput
        {
            ScenarioId = original.ScenarioId,
            Seed = seed,
            CurrentTick = currentTick,
            PublicContext = publicContext,
            PublicSearchDomainRegionIds = publicSearchDomainRegionIds?
                .Distinct(StringComparer.Ordinal)
                .OrderBy(item => item, StringComparer.Ordinal)
                .ToArray(),
            // An interaction/time window is not a point-position ObservationRecord.
            AvailableEvidence = new ObservationSet
            {
                ScenarioId = original.ScenarioId,
                Seed = seed,
                Observations = []
            },
            AvailableCrossThreadConstraints = new CrossThreadConstraintSet(original.ScenarioId, seed, crossThread),
            AvailableCausalTracingConstraints = new CausalTracingConstraintSet(original.ScenarioId, seed, tracing)
        });
    }


    /// <summary>
    /// Normalizes explicit copies of one original held by several devices. Receipt
    /// grants access once, never another evidence root or independent source.
    /// </summary>
    public RescuerEvidenceInput Build(
        RescueContext publicContext,
        int seed,
        long currentTick,
        string rescuerId,
        IEnumerable<DeviceHeldInteractionRecord> heldRecords,
        IEnumerable<InteractionRecordReceipt> receipts,
        IReadOnlyList<string>? publicSearchDomainRegionIds = null)
    {
        ArgumentNullException.ThrowIfNull(publicContext);
        ArgumentNullException.ThrowIfNull(heldRecords);
        ArgumentNullException.ThrowIfNull(receipts);
        var normalizedRescuer = InteractionNormalization.RequireIdentifier(rescuerId, nameof(rescuerId));
        if (currentTick < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(currentTick));
        }

        var copies = heldRecords.ToArray();
        if (copies.Length == 0 || copies.Any(copy => copy is null))
        {
            throw new ArgumentException("At least one non-null held copy is required.", nameof(heldRecords));
        }

        var original = copies[0].Observation;
        if (!string.Equals(publicContext.ScenarioId, original.ScenarioId, StringComparison.Ordinal) ||
            seed != original.Seed ||
            copies.Any(copy =>
                !SameOriginal(original, copy.Observation) ||
                !publicContext.PublicDeviceIds.Contains(copy.HolderDeviceId, StringComparer.Ordinal)))
        {
            throw new ArgumentException(
                "All copies must share one identical original and declare public holder devices.",
                nameof(heldRecords));
        }

        // Identical repeat entries for one holder are harmless; conflicting storage
        // times for that holder are not evidence that can be silently resolved.
        var normalizedCopies = copies
            .GroupBy(copy => copy.HolderDeviceId, StringComparer.Ordinal)
            .Select(group =>
            {
                var first = group.First();
                if (group.Any(copy => copy.StoredAtTick != first.StoredAtTick))
                {
                    throw new ArgumentException(
                        "One holder has conflicting storage times.", nameof(heldRecords));
                }

                return first;
            })
            .OrderBy(copy => copy.HolderDeviceId, StringComparer.Ordinal)
            .ToArray();
        var byHolder = normalizedCopies.ToDictionary(copy => copy.HolderDeviceId, StringComparer.Ordinal);

        // Filter other queries and future deliveries before temporal validation.
        // Check every currently matching assertion, not only the first valid one.
        var matching = receipts
            .Where(receipt =>
                receipt is not null &&
                receipt.ReceivedAtTick <= currentTick &&
                receipt.Seed == seed &&
                string.Equals(receipt.ScenarioId, original.ScenarioId, StringComparison.Ordinal) &&
                string.Equals(receipt.OriginalRecordId, original.SourceEvidenceIdentifier, StringComparison.Ordinal) &&
                string.Equals(receipt.RescuerId, normalizedRescuer, StringComparison.Ordinal) &&
                byHolder.ContainsKey(receipt.HolderDeviceId))
            .OrderBy(receipt => receipt.ReceivedAtTick)
            .ThenBy(receipt => receipt.HolderDeviceId, StringComparer.Ordinal)
            .ToArray();
        if (matching.Any(receipt =>
            receipt.ReceivedAtTick < byHolder[receipt.HolderDeviceId].StoredAtTick ||
            receipt.ReceivedAtTick < original.AvailableAtTick ||
            receipt.ReceivedAtTick < original.ObservedTimeWindow.EndTick))
        {
            throw new ArgumentException(
                "A matching receipt cannot precede storage, original availability or observation.",
                nameof(receipts));
        }

        var firstReceipt = matching.FirstOrDefault();
        var selected = firstReceipt is null
            ? normalizedCopies[0]
            : byHolder[firstReceipt.HolderDeviceId];

        // Reuse the established one-holder projection, including original
        // ancestry, person/device and uncertain/conflict semantics.
        return Build(
            publicContext, seed, currentTick, normalizedRescuer, selected,
            firstReceipt is null ? [] : [firstReceipt],
            publicSearchDomainRegionIds);
    }

    private static bool SameOriginal(InteractionObservation left, InteractionObservation right) =>
        string.Equals(left.ObservationId, right.ObservationId, StringComparison.Ordinal) &&
        string.Equals(left.ScenarioId, right.ScenarioId, StringComparison.Ordinal) &&
        left.Seed == right.Seed &&
        string.Equals(left.SourceEvidenceIdentifier, right.SourceEvidenceIdentifier, StringComparison.Ordinal) &&
        string.Equals(left.ReferencedInteractionId, right.ReferencedInteractionId, StringComparison.Ordinal) &&
        left.ObservedRelationKind == right.ObservedRelationKind &&
        left.ObservedParticipants.SequenceEqual(right.ObservedParticipants) &&
        left.ObservedTimeWindow == right.ObservedTimeWindow &&
        left.ObservedRegionIds.SequenceEqual(right.ObservedRegionIds, StringComparer.Ordinal) &&
        left.AvailableAtTick == right.AvailableAtTick &&
        left.EvidenceBoundaryState == right.EvidenceBoundaryState &&
        left.IsPersonEvidence == right.IsPersonEvidence &&
        left.IsDeviceOnlyEvidence == right.IsDeviceOnlyEvidence &&
        left.Visibility == right.Visibility &&
        left.Provenance.SequenceEqual(right.Provenance);

    private static CrossThreadConstraint ForReceiver(
        CrossThreadConstraint original,
        string rescuerId,
        long receivedAtTick) => new(
            // The view identity includes access context; its source identity does not.
            InteractionStableIdentifier.Create(
                "rctc",
                original.ConstraintId,
                rescuerId,
                InteractionStableIdentifier.Tick(receivedAtTick)),
            original.ScenarioId,
            original.Seed,
            original.SourceObservationId,
            original.SourceEvidenceIdentifier,
            original.TargetEntityId,
            original.SourceRelationKind,
            original.SourceParticipants,
            original.SourceObservedTimeWindow,
            original.ConstrainedTimeWindow,
            original.AllowedRegionIds,
            receivedAtTick,
            original.EvidenceBoundaryState,
            original.IsPersonEvidence,
            original.IsDeviceOnlyEvidence,
            original.DerivationKind,
            InteractionVisibility.PublicEvidence,
            original.Provenance);

    private static CausalTracingConstraint ForTracing(CrossThreadConstraint constraint) =>
        CausalTracingConstraint.Create(
            constraint.ScenarioId,
            constraint.Seed,
            constraint.SourceObservationId,
            constraint.SourceEvidenceIdentifier,
            constraint.Provenance
                .Where(item => item.Kind is
                    InteractionProvenanceKind.EvidenceRecord or
                    InteractionProvenanceKind.InteractionObservation or
                    InteractionProvenanceKind.ExternalSource)
                .Select(item => item.SourceIdentifier),
            constraint.TargetEntityId,
            TracingConstraintSourceKind.CrossThreadObservation,
            constraint.SourceRelationKind.ToString(),
            constraint.SourceParticipants,
            TracingConstraintRole.Local,
            constraint.SourceObservedTimeWindow,
            constraint.ConstrainedTimeWindow,
            constraint.AllowedRegionIds,
            constraint.AvailableAtTick,
            constraint.EvidenceBoundaryState,
            constraint.IsPersonEvidence,
            constraint.IsDeviceOnlyEvidence,
            InteractionVisibility.PublicEvidence,
            CrossThreadConstraintDeriver.PersonEncounterCoLocationRule,
            constraint.Provenance);
}
