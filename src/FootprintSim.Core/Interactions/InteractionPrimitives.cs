using System.Buffers.Binary;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace FootprintSim.Interactions;

public enum InteractionRelationKind
{
    PersonPersonEncounter
}

public enum InteractionEntityKind
{
    Person,
    Device
}

public enum InteractionParticipantRole
{
    EncounterParticipant
}

public enum InteractionVisibility
{
    PrivateTruth,
    PublicEvidence,
    PrivateEvidence
}

public enum InteractionProvenanceKind
{
    WorldEvent,
    EvidenceRecord,
    InteractionObservation,
    DerivationRule,
    ExternalSource
}

public sealed record InteractionTimeWindow
{
    public InteractionTimeWindow(long startTick, long endTick)
    {
        if (startTick < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(startTick));
        }

        if (endTick < startTick)
        {
            throw new ArgumentOutOfRangeException(nameof(endTick));
        }

        StartTick = startTick;
        EndTick = endTick;
    }

    public long StartTick { get; }

    public long EndTick { get; }

    public static InteractionTimeWindow At(long tick) => new(tick, tick);
}

public sealed record InteractionParticipant
{
    public InteractionParticipant(
        string entityId,
        InteractionEntityKind entityKind,
        InteractionParticipantRole role)
    {
        EntityId = InteractionNormalization.RequireIdentifier(entityId, nameof(entityId));
        EntityKind = entityKind;
        Role = role;
    }

    public string EntityId { get; }

    public InteractionEntityKind EntityKind { get; }

    public InteractionParticipantRole Role { get; }
}

public sealed record InteractionProvenance
{
    public InteractionProvenance(
        InteractionProvenanceKind kind,
        string sourceIdentifier)
    {
        Kind = kind;
        SourceIdentifier = InteractionNormalization.RequireIdentifier(
            sourceIdentifier,
            nameof(sourceIdentifier));
    }

    public InteractionProvenanceKind Kind { get; }

    public string SourceIdentifier { get; }
}

internal static class InteractionNormalization
{
    public static string RequireIdentifier(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Identifier cannot be empty.", parameterName);
        }

        return value;
    }

    public static IReadOnlyList<InteractionParticipant> Participants(
        IEnumerable<InteractionParticipant> participants)
    {
        ArgumentNullException.ThrowIfNull(participants);
        var ordered = participants
            .Distinct()
            .OrderBy(item => item.Role)
            .ThenBy(item => item.EntityKind)
            .ThenBy(item => item.EntityId, StringComparer.Ordinal)
            .ToArray();
        if (ordered.Length == 0)
        {
            throw new ArgumentException("At least one interaction participant is required.");
        }

        var duplicateEntity = ordered
            .GroupBy(item => item.EntityId, StringComparer.Ordinal)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicateEntity is not null)
        {
            throw new ArgumentException(
                $"Interaction participant '{duplicateEntity.Key}' is duplicated.");
        }

        return ordered;
    }

    public static IReadOnlyList<string> RegionIds(
        IEnumerable<string> regionIds,
        bool requireAtLeastOne)
    {
        ArgumentNullException.ThrowIfNull(regionIds);
        var ordered = regionIds
            .Select(regionId => RequireIdentifier(regionId, nameof(regionIds)))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(regionId => regionId, StringComparer.Ordinal)
            .ToArray();
        if (requireAtLeastOne && ordered.Length == 0)
        {
            throw new ArgumentException("At least one interaction region is required.");
        }

        return ordered;
    }

    public static IReadOnlyList<InteractionProvenance> Provenance(
        IEnumerable<InteractionProvenance> provenance)
    {
        ArgumentNullException.ThrowIfNull(provenance);
        var ordered = provenance
            .Distinct()
            .OrderBy(item => item.Kind)
            .ThenBy(item => item.SourceIdentifier, StringComparer.Ordinal)
            .ToArray();
        if (ordered.Length == 0)
        {
            throw new ArgumentException("At least one provenance reference is required.");
        }

        return ordered;
    }
}

internal static class InteractionStableIdentifier
{
    public static string Create(string prefix, params string[] components)
    {
        InteractionNormalization.RequireIdentifier(prefix, nameof(prefix));
        ArgumentNullException.ThrowIfNull(components);
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        Append(hash, prefix);
        foreach (var component in components)
        {
            Append(hash, InteractionNormalization.RequireIdentifier(
                component,
                nameof(components)));
        }

        var digest = hash.GetHashAndReset();
        return $"{prefix}-{Convert.ToHexString(digest.AsSpan(0, 8)).ToLowerInvariant()}";
    }

    public static string Tick(long tick) => tick.ToString(CultureInfo.InvariantCulture);

    public static string Seed(int seed) => seed.ToString(CultureInfo.InvariantCulture);

    private static void Append(IncrementalHash hash, string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        Span<byte> length = stackalloc byte[sizeof(int)];
        BinaryPrimitives.WriteInt32LittleEndian(length, bytes.Length);
        hash.AppendData(length);
        hash.AppendData(bytes);
    }
}
