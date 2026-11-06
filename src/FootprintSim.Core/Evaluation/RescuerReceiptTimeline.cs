using System.Security.Cryptography;
using System.Text.Json;
using FootprintSim.Evidence;
using FootprintSim.Hazards;
using FootprintSim.Inference;
using FootprintSim.Interactions;
using FootprintSim.Interactions.CandidateWarps;
using FootprintSim.Interactions.Tracing;
using FootprintSim.Model;

namespace FootprintSim.Evaluation;

public sealed record RescuerReceiptTimelineReport(
    int SchemaVersion,
    string FixtureSha256,
    string ScenarioId,
    int Seed,
    string RescuerId,
    IReadOnlyList<RescuerReceiptTimelineRow> Queries);

public sealed record RescuerReceiptTimelineRow(
    long QueryTick,
    int ReceivedOriginalCount,
    int CrossThreadConstraintCount,
    int CausalTracingConstraintCount,
    int SourceGroupCount,
    IReadOnlyList<RescuerReceiptOriginal> ReceivedOriginals,
    IReadOnlyList<TracingSourceGroup> SourceGroups,
    SearchStrategyResult SearchResult);

public sealed record RescuerReceiptOriginal(
    string OriginalRecordId,
    string OriginalObservationId,
    long FirstReceivedAtTick);

/// <summary>
/// Runs an explicit, evidence-only receipt timeline against the existing bridge
/// and public causal-tracing strategy. No simulation truth or delivery model enters.
/// </summary>
public static class RescuerReceiptTimeline
{
    public static RescuerReceiptTimelineReport Run(byte[] fixtureBytes)
    {
        ArgumentNullException.ThrowIfNull(fixtureBytes);
        using var document = JsonDocument.Parse(fixtureBytes);
        var root = document.RootElement;
        CheckObject(root,
            "schemaVersion", "scenarioId", "seed", "targetPersonId",
            "holderDeviceId", "rescuerId", "publicModelId", "publicSourceRegionId",
            "publicRegions", "publicStates", "publicTransitions", "observation",
            "storedAtTick", "receipts", "queryTicks");

        if (Integer(root, "schemaVersion") != 1)
        {
            throw new JsonException("Unsupported receipt timeline schemaVersion.");
        }

        var scenario = Identifier(root, "scenarioId");
        var seed = Integer(root, "seed");
        var target = Identifier(root, "targetPersonId");
        var holder = Identifier(root, "holderDeviceId");
        var rescuer = Identifier(root, "rescuerId");
        var modelId = Identifier(root, "publicModelId");
        var source = Identifier(root, "publicSourceRegionId");

        var publicRegions = Identifiers(root, "publicRegions", requireNonEmpty: true);
        var publicRegionSet = new HashSet<string>(publicRegions, StringComparer.Ordinal);
        if (!publicRegionSet.Contains(source))
        {
            throw new ArgumentException("Public model source is outside the declared public regions.");
        }

        var queries = Array(root, "queryTicks", item =>
        {
            if (item.ValueKind != JsonValueKind.Number ||
                !item.TryGetInt64(out var tick) || tick < 0)
            {
                throw new JsonException("Query ticks must be nonnegative integers.");
            }

            return tick;
        });
        if (queries.Length == 0 || queries.Distinct().Count() != queries.Length)
        {
            throw new ArgumentException("Query ticks must be nonempty and unique.");
        }

        var states = Array(root, "publicStates", item =>
        {
            CheckObject(item, "timeWindow", "temporalKind", "regionIds", "evidenceState");
            var window = Window(Required(item, "timeWindow"));
            var temporal = EnumValue<CandidateWarpTemporalKind>(item, "temporalKind");
            if ((temporal == CandidateWarpTemporalKind.EventPoint) !=
                (window.StartTick == window.EndTick))
            {
                throw new ArgumentException("Public state temporal kind and window disagree.");
            }

            var regions = Identifiers(item, "regionIds", requireNonEmpty: true);
            RequireRegions(regions, publicRegionSet);
            var evidenceState = EnumValue<EvidenceState>(item, "evidenceState");
            return new PublicCandidateWarpStateDefinition
            {
                TimeWindow = window,
                TemporalKind = temporal,
                RegionIds = regions,
                EvidenceBoundaryState = evidenceState,
                Provenance =
                [
                    CandidateWarpProvenance.Create(
                        CandidateWarpSourceKind.PublicCausalModel,
                        modelId, 0, false, false, evidenceState, regions)
                ]
            };
        });
        if (states.Length == 0)
        {
            throw new ArgumentException("At least one public candidate state is required.");
        }

        var branches = Array(root, "publicTransitions", item =>
        {
            CheckObject(item,
                "sourceRegionId", "destinationRegionId", "minimumTravelTicks",
                "maximumTravelTicks", "transportType", "provenanceId", "publishedAtTick");
            var from = Identifier(item, "sourceRegionId");
            var to = Identifier(item, "destinationRegionId");
            RequireRegions([from, to], publicRegionSet);
            var minimum = Integer(item, "minimumTravelTicks");
            var maximum = Integer(item, "maximumTravelTicks");
            if (minimum < 0 || maximum < minimum)
            {
                throw new ArgumentException("Invalid public transition travel window.");
            }

            return new PublicHazardBranch
            {
                SourceRegionId = from,
                DestinationRegionId = to,
                MinimumTravelTicks = minimum,
                MaximumTravelTicks = maximum,
                TransportType = EnumValue<HazardTransportType>(item, "transportType"),
                ProvenanceId = Identifier(item, "provenanceId"),
                PublishedAtTick = Tick(item, "publishedAtTick")
            };
        });

        var observationJson = Required(root, "observation");
        CheckObject(observationJson,
            "sourceEvidenceIdentifier", "referencedInteractionId",
            "relationKind", "participants", "timeWindow", "regionIds",
            "availableAtTick", "evidenceState", "isPersonEvidence",
            "isDeviceOnlyEvidence", "provenance");
        string? reference = null;
        if (observationJson.TryGetProperty("referencedInteractionId", out var referenceValue) &&
            referenceValue.ValueKind != JsonValueKind.Null)
        {
            if (referenceValue.ValueKind != JsonValueKind.String)
            {
                throw new JsonException("referencedInteractionId must be a string or null.");
            }

            reference = referenceValue.GetString();
            if (string.IsNullOrWhiteSpace(reference))
            {
                throw new ArgumentException("Empty interaction reference is not permitted.");
            }
        }

        var participants = Array(observationJson, "participants", item =>
        {
            CheckObject(item, "entityId", "entityKind", "role");
            return new InteractionParticipant(
                Identifier(item, "entityId"),
                EnumValue<InteractionEntityKind>(item, "entityKind"),
                EnumValue<InteractionParticipantRole>(item, "role"));
        });
        var regionsForObservation = Identifiers(
            observationJson, "regionIds", requireNonEmpty: false);
        RequireRegions(regionsForObservation, publicRegionSet);

        var provenance = Array(observationJson, "provenance", item =>
        {
            CheckObject(item, "kind", "sourceIdentifier");
            var kind = EnumValue<InteractionProvenanceKind>(item, "kind");
            if (kind is not (
                InteractionProvenanceKind.EvidenceRecord or
                InteractionProvenanceKind.InteractionObservation or
                InteractionProvenanceKind.ExternalSource))
            {
                throw new ArgumentException("Only evidence-side provenance is accepted.");
            }

            return new InteractionProvenance(kind, Identifier(item, "sourceIdentifier"));
        });

        // Construct once. Neither receipt nor query can change the original ID.
        var original = InteractionObservation.Create(
            scenario, seed,
            Identifier(observationJson, "sourceEvidenceIdentifier"), reference,
            EnumValue<InteractionRelationKind>(observationJson, "relationKind"),
            participants, Window(Required(observationJson, "timeWindow")),
            regionsForObservation, Tick(observationJson, "availableAtTick"),
            EnumValue<EvidenceState>(observationJson, "evidenceState"),
            Boolean(observationJson, "isPersonEvidence"),
            Boolean(observationJson, "isDeviceOnlyEvidence"),
            InteractionVisibility.PrivateEvidence, provenance);
        var held = new DeviceHeldInteractionRecord(holder, Tick(root, "storedAtTick"), original);
        var receipts = Array(root, "receipts", item =>
        {
            CheckObject(item,
                "scenarioId", "seed", "originalRecordId", "holderDeviceId",
                "rescuerId", "receivedAtTick");
            return new InteractionRecordReceipt(
                Identifier(item, "scenarioId"), Integer(item, "seed"),
                Identifier(item, "originalRecordId"),
                Identifier(item, "holderDeviceId"),
                Identifier(item, "rescuerId"),
                Tick(item, "receivedAtTick"));
        });

        var publicContext = new RescueContext
        {
            ScenarioId = scenario,
            TargetPersonId = target,
            NormalWalkingRadius = 0,
            PublicDeviceIds = [holder],
            // Coordinates serve only as public map labels here. The tracer uses
            // explicit public states and branches, not geometric distances.
            PublicRegions = publicRegions.Select((id, index) => new RegionDefinition
            {
                Id = id,
                Type = RegionType.Outdoor,
                Center = new Position(index, 0, 0, id)
            }).ToArray(),
            PublicCausalTracingModel = new PublicCausalTracingModel
            {
                CandidateStates = states
            },
            PublicHazardEnvelope = new PublicHazardEnvelope
            {
                HazardId = modelId,
                SourceRegionId = source,
                PublishedAtTick = 0,
                Branches = branches
            }
        };

        var builder = new RescuerEvidenceInputBuilder();
        var strategy = new MultiWeftTracingStrategy();
        var rows = new List<RescuerReceiptTimelineRow>();
        foreach (var tick in queries.OrderBy(item => item))
        {
            var received = builder.Build(
                publicContext, seed, tick, rescuer, held, receipts);
            var searchResult = strategy.Evaluate(received.Input);
            var trace = searchResult.CausalTracingResult
                ?? throw new InvalidOperationException("Public causal tracing did not run.");

            rows.Add(new RescuerReceiptTimelineRow(
                tick,
                received.ReceivedRecords.Count,
                received.Input.AvailableCrossThreadConstraints?.Constraints.Count ?? 0,
                received.Input.AvailableCausalTracingConstraints?.Constraints.Count ?? 0,
                trace.SourceGroups.Count,
                received.ReceivedRecords.Select(item => new RescuerReceiptOriginal(
                    item.OriginalRecordId,
                    item.OriginalObservationId,
                    item.FirstReceivedAtTick)).ToArray(),
                trace.SourceGroups,
                searchResult));
        }

        return new RescuerReceiptTimelineReport(
            1,
            Convert.ToHexString(SHA256.HashData(fixtureBytes)).ToLowerInvariant(),
            scenario, seed, rescuer, rows);
    }

    private static JsonElement Required(JsonElement node, string name)
    {
        if (!node.TryGetProperty(name, out var value))
        {
            throw new JsonException($"Required fixture property '{name}' is missing.");
        }

        return value;
    }

    private static void CheckObject(JsonElement node, params string[] allowed)
    {
        if (node.ValueKind != JsonValueKind.Object)
        {
            throw new JsonException("Fixture object was expected.");
        }

        var permitted = new HashSet<string>(allowed, StringComparer.Ordinal);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var property in node.EnumerateObject())
        {
            if (!permitted.Contains(property.Name) || !seen.Add(property.Name))
            {
                throw new JsonException($"Unknown or duplicate property '{property.Name}'.");
            }
        }
    }

    private static T[] Array<T>(
        JsonElement node, string name, Func<JsonElement, T> parser)
    {
        var value = Required(node, name);
        if (value.ValueKind != JsonValueKind.Array)
        {
            throw new JsonException($"'{name}' must be an array.");
        }

        return value.EnumerateArray().Select(parser).ToArray();
    }

    private static string Identifier(JsonElement node, string name)
    {
        var value = Required(node, name);
        if (value.ValueKind != JsonValueKind.String ||
            string.IsNullOrWhiteSpace(value.GetString()))
        {
            throw new JsonException($"'{name}' must be a nonempty identifier.");
        }

        return value.GetString()!;
    }

    private static IReadOnlyList<string> Identifiers(
        JsonElement node, string name, bool requireNonEmpty)
    {
        var values = Array(node, name, item =>
        {
            if (item.ValueKind != JsonValueKind.String ||
                string.IsNullOrWhiteSpace(item.GetString()))
            {
                throw new JsonException($"'{name}' contains an invalid identifier.");
            }

            return item.GetString()!;
        });
        if ((requireNonEmpty && values.Length == 0) ||
            values.Distinct(StringComparer.Ordinal).Count() != values.Length)
        {
            throw new ArgumentException($"'{name}' must contain unique identifiers.");
        }

        return values.OrderBy(item => item, StringComparer.Ordinal).ToArray();
    }

    private static void RequireRegions(
        IEnumerable<string> ids, IReadOnlySet<string> publicRegions)
    {
        if (ids.Any(id => !publicRegions.Contains(id)))
        {
            throw new ArgumentException("An evidence or public region is not in the public map.");
        }
    }

    private static long Tick(JsonElement node, string name)
    {
        var value = Required(node, name);
        if (value.ValueKind != JsonValueKind.Number ||
            !value.TryGetInt64(out var tick) || tick < 0)
        {
            throw new JsonException($"'{name}' must be a nonnegative integer tick.");
        }

        return tick;
    }

    private static int Integer(JsonElement node, string name)
    {
        var value = Required(node, name);
        if (value.ValueKind != JsonValueKind.Number || !value.TryGetInt32(out var number))
        {
            throw new JsonException($"'{name}' must be an integer.");
        }

        return number;
    }

    private static bool Boolean(JsonElement node, string name)
    {
        var value = Required(node, name);
        if (value.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
        {
            throw new JsonException($"'{name}' must be a boolean.");
        }

        return value.GetBoolean();
    }

    private static T EnumValue<T>(JsonElement node, string name) where T : struct, Enum
    {
        var value = Required(node, name);
        if (value.ValueKind != JsonValueKind.String ||
            !Enum.TryParse<T>(value.GetString(), ignoreCase: false, out var result) ||
            !Enum.IsDefined(result) ||
            !Enum.GetNames<T>().Contains(value.GetString(), StringComparer.Ordinal))
        {
            throw new JsonException($"'{name}' contains an unsupported enum name.");
        }

        return result;
    }

    private static InteractionTimeWindow Window(JsonElement node)
    {
        CheckObject(node, "startTick", "endTick");
        return new InteractionTimeWindow(
            Tick(node, "startTick"), Tick(node, "endTick"));
    }
}
