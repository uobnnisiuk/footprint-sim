using System.Text.Json;
using FootprintSim.Evidence;
using FootprintSim.Hazards;
using FootprintSim.Inference;
using FootprintSim.Interactions;
using FootprintSim.Interactions.CandidateWarps;
using FootprintSim.Model;

namespace FootprintSim.Evaluation;

// The original 24 paccaya are a source index, NOT physical relation types.
public sealed record ConditionLensIndexEntry(string Id, string PaliName, string Source, string Status, string Mapping);
public sealed record ConditionLensQuestion(
    string QuestionId, IReadOnlyList<string> LensIds, string ReasonCode,
    string TargetPersonId, long TargetTick, long ReceiptCutoffTick,
    IReadOnlyList<string> ReceivedOriginalIds,
    IReadOnlyList<string> ReceivedObservationIds,
    IReadOnlyList<string> SourceAncestryIds,
    string RequiredInformation, string Explanation);
public sealed record ConditionLensModeResult(
    string Mode, IReadOnlyList<ConditionLensQuestion> Questions,
    RescuerEvidenceMethodQueryRow MethodComparison);
public sealed record ConditionLensCaseResult(
    string Id, string Group, string AnswerCondition,
    IReadOnlyList<string> ReceivedOriginalIds,
    RescuerMissingDeclaredPathReport ExistingDeclaredPathDiagnostic,
    IReadOnlyList<ConditionLensModeResult> Modes);
public sealed record ConditionLensMethodDifference(
    string Method, IReadOnlyList<string> BeforeOuter, IReadOnlyList<string> AfterOuter,
    IReadOnlyList<string> RemovedOuter, IReadOnlyList<string> AddedOuter,
    IReadOnlyList<string>? BeforeUncertain, IReadOnlyList<string>? AfterUncertain,
    IReadOnlyList<string>? BeforeConflicted, IReadOnlyList<string>? AfterConflicted,
    int BeforeAvailableSourceGroups, int AfterAvailableSourceGroups);
public sealed record ConditionLensAnswerDifference(
    string Group, string FromCase, string ToCase, string AnswerCondition,
    string Interpretation, IReadOnlyList<ConditionLensMethodDifference> Methods,
    IReadOnlyList<string> ResolvedQuestionIds, IReadOnlyList<string> NewQuestionIds);
public sealed record ConditionLensReport(
    int SchemaVersion, string ScenarioId, int Seed, string TargetPersonId,
    long TargetTick, long ReceiptCutoffTick,
    IReadOnlyList<ConditionLensIndexEntry> Index,
    IReadOnlyList<ConditionLensCaseResult> Cases,
    IReadOnlyList<ConditionLensAnswerDifference> AnswerDifferences);

// This is a finite public synthetic input; held originals are not rescuer knowledge.
public sealed record ConditionLensFixture(
    int SchemaVersion, string ScenarioId, int Seed, string TargetPersonId,
    string RescuerId, long TargetTick, long ReceiptCutoffTick,
    IReadOnlyList<string> PublicRegionIds, IReadOnlyList<ConditionLensCaseSpec> Cases);
public sealed record ConditionLensCaseSpec(
    string Id, string Group, string AnswerCondition,
    IReadOnlyList<ConditionLensOriginalSpec> Originals, bool DuplicateFirstReceipt = false);
public sealed record ConditionLensOriginalSpec(
    string Id, string HolderDeviceId, string EvidenceKind, long ObservedTick,
    IReadOnlyList<string> RegionIds, string SourceAncestryId, long? ReceivedAtTick);

/// <summary>
/// Two bounded, source-backed question lenses on the existing receipt bridge.
/// Neither a lens nor a proposed answer is an additional inference constraint.
/// Every reported candidate comes from the unmodified M1-D/E comparison.
/// </summary>
public static class ConditionLensAnalysis
{
    private const string PaliSource = "https://www.aimwell.org/patthana.html";
    private static readonly string[] Names =
    [
        "hetu", "ārammaṇa", "adhipati", "anantara", "samanantara", "sahajāta",
        "aññamañña", "nissaya", "upanissaya", "purejāta", "pacchājāta",
        "āsevana", "kamma", "vipāka", "āhāra", "indriya", "jhāna", "magga",
        "sampayutta", "vippayutta", "atthi", "natthi", "vigata", "avigata"
    ];

    public static IReadOnlyList<ConditionLensIndexEntry> Index { get; } =
        Names.Select((name, index) => new ConditionLensIndexEntry(
            (index + 1).ToString("D2", System.Globalization.CultureInfo.InvariantCulture),
            name, PaliSource,
            index is 1 or 5 ? "limited-analogy-implemented" : "unimplemented-unassessed",
            index == 1 ? "received-device-subject-check" :
            index == 5 ? "received-same-tick-person-evidence-check" :
            "no-validated-footprint-mapping")).ToArray();

    public static ConditionLensReport Run(byte[] fixtureBytes)
    {
        ArgumentNullException.ThrowIfNull(fixtureBytes);
        var fixture = JsonSerializer.Deserialize<ConditionLensFixture>(
            fixtureBytes, new JsonSerializerOptions(JsonSerializerDefaults.Web))
            ?? throw new JsonException("Condition-lens fixture is null.");
        if (fixture.SchemaVersion != 1 || fixture.TargetTick < 0 ||
            fixture.ReceiptCutoffTick < fixture.TargetTick ||
            string.IsNullOrWhiteSpace(fixture.ScenarioId) ||
            string.IsNullOrWhiteSpace(fixture.RescuerId) ||
            string.IsNullOrWhiteSpace(fixture.TargetPersonId) ||
            fixture.PublicRegionIds is null || fixture.PublicRegionIds.Count == 0 ||
            fixture.PublicRegionIds.Any(string.IsNullOrWhiteSpace) ||
            fixture.PublicRegionIds.Distinct(StringComparer.Ordinal).Count() != fixture.PublicRegionIds.Count ||
            fixture.Cases is null || fixture.Cases.Count == 0 ||
            fixture.Cases.Any(c => c is null || string.IsNullOrWhiteSpace(c.Id) ||
                string.IsNullOrWhiteSpace(c.Group) ||
                c.AnswerCondition is not ("none" or "eligible" or "future" or "other-tick" or "duplicate") ||
                c.Originals is null || c.Originals.Count != 2) ||
            fixture.Cases.Select(c => c.Id).Distinct(StringComparer.Ordinal).Count() != fixture.Cases.Count)
        {
            throw new ArgumentException("Invalid finite condition-lens fixture.");
        }

        var regions = fixture.PublicRegionIds.OrderBy(x => x, StringComparer.Ordinal).ToArray();
        var publicWorld = new RescueContext
        {
            ScenarioId = fixture.ScenarioId,
            TargetPersonId = fixture.TargetPersonId,
            NormalWalkingRadius = 0,
            PublicDeviceIds = fixture.Cases.SelectMany(c => c.Originals)
                .Select(o => o.HolderDeviceId).Distinct(StringComparer.Ordinal)
                .OrderBy(x => x, StringComparer.Ordinal).ToArray(),
            PublicRegions = regions.Select((id, n) => new RegionDefinition
            {
                Id = id,
                Type = RegionType.Outdoor,
                Center = new Position(n, 0, 0, id)
            }).ToArray(),
            PublicCausalTracingModel = new PublicCausalTracingModel
            {
                CandidateStates =
                [
                    new PublicCandidateWarpStateDefinition
                    {
                        TimeWindow = InteractionTimeWindow.At(fixture.TargetTick),
                        TemporalKind = CandidateWarpTemporalKind.EventPoint,
                        RegionIds = regions,
                        EvidenceBoundaryState = EvidenceState.SupportedButUncertain,
                        Provenance =
                        [
                            CandidateWarpProvenance.Create(
                                CandidateWarpSourceKind.PublicCausalModel,
                                "condition-lens-public-prior", 0, false, false,
                                EvidenceState.SupportedButUncertain, regions)
                        ]
                    }
                ]
            }
        };
        var builder = new RescuerEvidenceInputBuilder();
        var results = new List<ConditionLensCaseResult>();
        foreach (var item in fixture.Cases.OrderBy(c => c.Id, StringComparer.Ordinal))
        {
            if (item.Originals.Any(o => o is null || string.IsNullOrWhiteSpace(o.Id) ||
                    string.IsNullOrWhiteSpace(o.HolderDeviceId) ||
                    string.IsNullOrWhiteSpace(o.SourceAncestryId) ||
                    o.ObservedTick < 0 || o.ObservedTick > fixture.ReceiptCutoffTick ||
                    o.EvidenceKind is not ("person" or "device") ||
                    o.RegionIds is null || o.RegionIds.Count == 0 ||
                    o.RegionIds.Any(r => !regions.Contains(r, StringComparer.Ordinal)) ||
                    o.ReceivedAtTick < o.ObservedTick) ||
                item.Originals.Select(o => o.Id).Distinct(StringComparer.Ordinal).Count() != 2)
            {
                throw new ArgumentException("Invalid or out-of-domain synthetic original.");
            }
            var held = item.Originals.OrderBy(o => o.Id, StringComparer.Ordinal)
                .Select(o =>
                {
                    var person = o.EvidenceKind == "person";
                    var observation = InteractionObservation.Create(
                        fixture.ScenarioId, fixture.Seed, o.Id, null,
                        InteractionRelationKind.PersonPersonEncounter,
                        [
                            new InteractionParticipant(
                                person ? fixture.TargetPersonId : o.HolderDeviceId,
                                person ? InteractionEntityKind.Person : InteractionEntityKind.Device,
                                InteractionParticipantRole.EncounterParticipant),
                            new InteractionParticipant("synthetic-peer", InteractionEntityKind.Person,
                                InteractionParticipantRole.EncounterParticipant)
                        ],
                        InteractionTimeWindow.At(o.ObservedTick),
                        o.RegionIds, o.ObservedTick, EvidenceState.Confirmed,
                        isPersonEvidence: person, isDeviceOnlyEvidence: !person,
                        InteractionVisibility.PrivateEvidence,
                        [new InteractionProvenance(
                            InteractionProvenanceKind.ExternalSource, o.SourceAncestryId)]);
                    return new DeviceHeldInteractionRecord(
                        o.HolderDeviceId, o.ObservedTick, observation);
                }).ToArray();
            var receipts = item.Originals.Where(o => o.ReceivedAtTick is not null)
                .Select(o => new InteractionRecordReceipt(
                    fixture.ScenarioId, fixture.Seed, o.Id, o.HolderDeviceId,
                    fixture.RescuerId, o.ReceivedAtTick!.Value)).ToList();
            if (item.DuplicateFirstReceipt)
            {
                // A retransmission is the SAME original and first receipt, not a new root.
                var first = receipts.OrderBy(r => r.OriginalRecordId, StringComparer.Ordinal).FirstOrDefault()
                    ?? throw new ArgumentException("A duplicate receipt needs an actual receipt.");
                receipts.Add(first);
            }
            var received = builder.BuildDistinctOriginals(
                publicWorld, fixture.Seed, fixture.ReceiptCutoffTick,
                fixture.RescuerId, held, receipts, regions);
            var window = InteractionTimeWindow.At(fixture.TargetTick);
            var comparison = RescuerEvidenceMethodComparison.Compare(received, window);
            var existing = RescuerEvidenceMethodComparison.AnalyzeMissingDeclaredPathQuestions(
                received, window);
            var detected = Questions(received, comparison, fixture.TargetTick);
            var modes = new[]
            {
                new ConditionLensModeResult("none", [], comparison),
                new ConditionLensModeResult("02", detected.Where(q => q.LensIds.Contains("02")).ToArray(), comparison),
                new ConditionLensModeResult("06", detected.Where(q => q.LensIds.Contains("06")).ToArray(), comparison),
                new ConditionLensModeResult("combined", detected, comparison)
            };
            results.Add(new ConditionLensCaseResult(
                item.Id, item.Group, item.AnswerCondition,
                received.ReceivedRecords.Select(r => r.OriginalRecordId)
                    .OrderBy(x => x, StringComparer.Ordinal).ToArray(),
                existing, modes));
        }

        var differences = new List<ConditionLensAnswerDifference>();
        foreach (var group in results.GroupBy(c => c.Group, StringComparer.Ordinal))
        {
            var baseline = group.SingleOrDefault(c => c.AnswerCondition == "none");
            if (baseline is null) continue;
            foreach (var condition in group.Where(c => c != baseline)
                .OrderBy(c => c.Id, StringComparer.Ordinal))
            {
                var before = baseline.Modes[0].MethodComparison.Methods;
                var after = condition.Modes[0].MethodComparison.Methods;
                var methods = before.Select(b =>
                {
                    var a = after.Single(m => m.Method == b.Method);
                    return new ConditionLensMethodDifference(
                        b.Method, b.OuterCandidateRegionIds, a.OuterCandidateRegionIds,
                        b.OuterCandidateRegionIds.Except(a.OuterCandidateRegionIds, StringComparer.Ordinal)
                            .OrderBy(x => x, StringComparer.Ordinal).ToArray(),
                        a.OuterCandidateRegionIds.Except(b.OuterCandidateRegionIds, StringComparer.Ordinal)
                            .OrderBy(x => x, StringComparer.Ordinal).ToArray(),
                        b.UncertainCandidateRegionIds, a.UncertainCandidateRegionIds,
                        b.ConflictedCandidateRegionIds, a.ConflictedCandidateRegionIds,
                        b.AvailableSourceGroupCount, a.AvailableSourceGroupCount);
                }).ToArray();
                var priorQuestions = baseline.Modes[3].Questions.Select(q => q.QuestionId).ToArray();
                var nextQuestions = condition.Modes[3].Questions.Select(q => q.QuestionId).ToArray();
                differences.Add(new ConditionLensAnswerDifference(
                    baseline.Group, baseline.Id, condition.Id, condition.AnswerCondition,
                    "different-received-information-not-method-superiority",
                    methods,
                    priorQuestions.Except(nextQuestions, StringComparer.Ordinal)
                        .OrderBy(x => x, StringComparer.Ordinal).ToArray(),
                    nextQuestions.Except(priorQuestions, StringComparer.Ordinal)
                        .OrderBy(x => x, StringComparer.Ordinal).ToArray()));
            }
        }
        return new ConditionLensReport(
            1, fixture.ScenarioId, fixture.Seed, fixture.TargetPersonId,
            fixture.TargetTick, fixture.ReceiptCutoffTick, Index, results, differences);
    }

    private static IReadOnlyList<ConditionLensQuestion> Questions(
        RescuerEvidenceInput received, RescuerEvidenceMethodQueryRow comparison, long tick)
    {
        // Never inspect held-but-unreceived observations or the synthetic answer label.
        var visible = received.ReceivedRecords
            .Where(r => r.OriginalObservation.ObservedTimeWindow.StartTick == tick &&
                r.OriginalObservation.ObservedTimeWindow.EndTick == tick)
            .OrderBy(r => r.OriginalRecordId, StringComparer.Ordinal).ToArray();
        static string[] Origins(IEnumerable<ReceivedInteractionRecord> records) =>
            records.SelectMany(r => r.OriginalObservation.Provenance
                .Where(p => p.Kind == InteractionProvenanceKind.ExternalSource)
                .Select(p => p.SourceIdentifier))
                .Distinct(StringComparer.Ordinal).OrderBy(x => x, StringComparer.Ordinal).ToArray();
        static string[] RecordIds(IEnumerable<ReceivedInteractionRecord> records) =>
            records.Select(r => r.OriginalRecordId)
                .Distinct(StringComparer.Ordinal).OrderBy(x => x, StringComparer.Ordinal).ToArray();
        static string[] ObservationIds(IEnumerable<ReceivedInteractionRecord> records) =>
            records.Select(r => r.OriginalObservationId)
                .Distinct(StringComparer.Ordinal).OrderBy(x => x, StringComparer.Ordinal).ToArray();

        var target = received.Input.PublicContext.TargetPersonId;
        var cutoff = received.Input.CurrentTick;
        var questions = new List<ConditionLensQuestion>();
        var devices = visible.Where(r => r.OriginalObservation.IsDeviceOnlyEvidence).ToArray();
        var persons = visible.Where(r =>
            r.OriginalObservation.IsPersonEvidence &&
            !r.OriginalObservation.IsDeviceOnlyEvidence &&
            r.OriginalObservation.EvidenceBoundaryState == EvidenceState.Confirmed &&
            r.OriginalObservation.ObservedParticipants.Any(p =>
                p.EntityId == target && p.EntityKind == InteractionEntityKind.Person)).ToArray();

        if (devices.Length > 0)
        {
            questions.Add(new ConditionLensQuestion(
                "subject-person-verification:" + tick, ["02"], "device-is-not-person",
                target, tick, cutoff, RecordIds(devices), ObservationIds(devices),
                Origins(devices), "separately-received-confirmed-person-evidence",
                persons.Length == 0
                    ? "Received device evidence cannot establish target Person; Person evidence is missing."
                    : "Received device evidence cannot be promoted to target Person even when separate Person evidence exists."));
        }
        var distinctPersonAncestry = persons.Select(r =>
            string.Join("|", r.OriginalObservation.Provenance
                .Where(p => p.Kind == InteractionProvenanceKind.ExternalSource)
                .Select(p => p.SourceIdentifier).OrderBy(x => x, StringComparer.Ordinal)))
            .Distinct(StringComparer.Ordinal).Count();
        var candidate = comparison.Methods.Single(m =>
            m.Method == "distributed-reconstruction");
        if (persons.Length > 0 && distinctPersonAncestry == 1 && candidate.CandidateCount > 1)
        {
            questions.Add(new ConditionLensQuestion(
                "same-tick-person-corroboration:" + tick, ["06"],
                "unconfirmed-independent-same-tick-person-evidence",
                target, tick, cutoff, RecordIds(persons), ObservationIds(persons),
                Origins(persons), "additional-independent-received-person-evidence-at-the-same-tick",
                "A second independently sourced Person constraint at this tick is not established; mere coincidence or a copied receipt is insufficient."));
        }
        return questions.OrderBy(q => q.QuestionId, StringComparer.Ordinal).ToArray();
    }
}
