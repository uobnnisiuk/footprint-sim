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
