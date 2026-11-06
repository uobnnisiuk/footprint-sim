using FootprintSim.Interactions;

namespace FootprintSim.Evidence;

/// <summary>
/// One explicitly supplied evidence record held by one device. Possession is not
/// receipt by a rescuer, and this contract accepts no simulation truth objects.
/// </summary>
public sealed record DeviceHeldInteractionRecord
{
    public DeviceHeldInteractionRecord(
        string holderDeviceId,
        long storedAtTick,
        InteractionObservation observation)
    {
        HolderDeviceId = InteractionNormalization.RequireIdentifier(
            holderDeviceId,
            nameof(holderDeviceId));
        ArgumentNullException.ThrowIfNull(observation);
        if (observation.Visibility != InteractionVisibility.PrivateEvidence)
        {
            throw new ArgumentException(
                "A device-held original must remain private evidence.",
                nameof(observation));
        }

        if (observation.Provenance.Any(item => item.Kind == InteractionProvenanceKind.WorldEvent))
        {
            throw new ArgumentException(
                "A device-held evidence record cannot carry truth-event provenance.",
                nameof(observation));
        }

        if (storedAtTick < observation.AvailableAtTick)
        {
            throw new ArgumentOutOfRangeException(
                nameof(storedAtTick),
                "A device cannot hold the record before the original evidence is available.");
        }

        StoredAtTick = storedAtTick;
        Observation = observation;
    }

    public string HolderDeviceId { get; }

    public long StoredAtTick { get; }

    public string OriginalRecordId => Observation.SourceEvidenceIdentifier;

    public InteractionObservation Observation { get; }
}

/// <summary>
/// An explicit receipt assertion, not a simulated communication or a new evidence source.
/// Multiple receipts may refer to the same original record.
/// </summary>
public sealed record InteractionRecordReceipt
{
    public InteractionRecordReceipt(
        string scenarioId,
        int seed,
        string originalRecordId,
        string holderDeviceId,
        string rescuerId,
        long receivedAtTick)
    {
        ScenarioId = InteractionNormalization.RequireIdentifier(scenarioId, nameof(scenarioId));
        Seed = seed;
        OriginalRecordId = InteractionNormalization.RequireIdentifier(
            originalRecordId,
            nameof(originalRecordId));
        HolderDeviceId = InteractionNormalization.RequireIdentifier(
            holderDeviceId,
            nameof(holderDeviceId));
        RescuerId = InteractionNormalization.RequireIdentifier(rescuerId, nameof(rescuerId));
        if (receivedAtTick < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(receivedAtTick));
        }

        ReceivedAtTick = receivedAtTick;
    }

    public string ScenarioId { get; }

    public int Seed { get; }

    public string OriginalRecordId { get; }

    public string HolderDeviceId { get; }

    public string RescuerId { get; }

    public long ReceivedAtTick { get; }
}

/// <summary>
/// Access metadata for one rescuer. The original observation and its identity
/// remain unchanged; availability for this rescuer is a separate fact.
/// </summary>
public sealed record ReceivedInteractionRecord
{
    internal ReceivedInteractionRecord(
        DeviceHeldInteractionRecord record,
        string rescuerId,
        long firstReceivedAtTick)
    {
        HolderDeviceId = record.HolderDeviceId;
        RescuerId = rescuerId;
        FirstReceivedAtTick = firstReceivedAtTick;
        OriginalObservation = record.Observation;
    }

    public string HolderDeviceId { get; }

    public string RescuerId { get; }

    public long FirstReceivedAtTick { get; }

    public string OriginalRecordId => OriginalObservation.SourceEvidenceIdentifier;

    public string OriginalObservationId => OriginalObservation.ObservationId;

    public InteractionObservation OriginalObservation { get; }
}
