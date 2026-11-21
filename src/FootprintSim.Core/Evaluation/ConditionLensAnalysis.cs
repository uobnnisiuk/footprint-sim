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
    IReadOnlyList<ConditionLensModeResult> Modes,
    IReadOnlyList<ConditionLensRootRemoval> RootRemovals);
public sealed record ConditionLensMethodDifference(
    string Method, IReadOnlyList<string> BeforeOuter, IReadOnlyList<string> AfterOuter,
    IReadOnlyList<string> RemovedOuter, IReadOnlyList<string> AddedOuter,
    IReadOnlyList<string>? BeforeUncertain, IReadOnlyList<string>? AfterUncertain,
    IReadOnlyList<string>? BeforeConflicted, IReadOnlyList<string>? AfterConflicted,
    int BeforeAvailableSourceGroups, int AfterAvailableSourceGroups);
// A counterfactual diagnostic, never a change to the rescuer's actual received evidence.
public sealed record ConditionLensRootRemovalMethod(
    string Method, IReadOnlyList<string> BeforeOuter, IReadOnlyList<string> AfterOuter,
    IReadOnlyList<string> AddedOuter, IReadOnlyList<string> RemovedOuter,
    IReadOnlyList<string>? BeforePreferred, IReadOnlyList<string>? AfterPreferred,
    IReadOnlyList<string>? BeforeUncertain, IReadOnlyList<string>? AfterUncertain,
    IReadOnlyList<string>? BeforeConflicted, IReadOnlyList<string>? AfterConflicted,
    int BeforeAvailableSourceGroups, int AfterAvailableSourceGroups);
public sealed record ConditionLensRootRemoval(
    string SourceRootId, IReadOnlyList<string> RemovedReceivedOriginalIds,
    IReadOnlyList<ConditionLensRootRemovalMethod> Methods);
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
/// Four bounded, source-backed question lenses on the existing receipt bridge.
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
            index is 1 or 3 or 5 or 8 ? "limited-analogy-implemented" : "unimplemented-unassessed",
            index == 1 ? "received-device-subject-check" :
            index == 3 ? "received-adjacent-tick-person-check" :
            index == 5 ? "received-same-tick-person-evidence-check" :
            index == 8 ? "received-person-source-root-removal-dependence" :
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
            // Only roots exposed by already received, eligible target-tick Person records
            // are candidates for diagnostic removal. No held-only ID is consulted here.
            var rootGroups = received.ReceivedRecords
                .Where(r => r.OriginalObservation.IsPersonEvidence &&
                    !r.OriginalObservation.IsDeviceOnlyEvidence &&
                    r.OriginalObservation.EvidenceBoundaryState == EvidenceState.Confirmed &&
                    r.OriginalObservation.ObservedTimeWindow.StartTick == fixture.TargetTick &&
                    r.OriginalObservation.ObservedTimeWindow.EndTick == fixture.TargetTick &&
                    r.OriginalObservation.ObservedParticipants.Any(p =>
                        p.EntityId == fixture.TargetPersonId &&
                        p.EntityKind == InteractionEntityKind.Person))
                .Select(r => new
                {
                    Record = r,
                    Roots = r.OriginalObservation.Provenance
                        .Where(p => p.Kind == InteractionProvenanceKind.ExternalSource)
                        .Select(p => p.SourceIdentifier)
                        .Distinct(StringComparer.Ordinal).ToArray()
                })
                .Where(x => x.Roots.Length == 1)
                .GroupBy(x => x.Roots[0], StringComparer.Ordinal)
                .OrderBy(g => g.Key, StringComparer.Ordinal).ToArray();

            var rootRemovals = new List<ConditionLensRootRemoval>();
            foreach (var rootGroup in rootGroups)
            {
                // Determine the eligible Person roots from received Person records,
                // but remove ALL already-received records sharing that same source
                // root, including Device records. One root is one evidence source.
                var removedIds = received.ReceivedRecords
                    .Where(r => r.OriginalObservation.Provenance.Any(p =>
                        p.Kind == InteractionProvenanceKind.ExternalSource &&
                        p.SourceIdentifier == rootGroup.Key))
                    .Select(r => r.OriginalRecordId)
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(x => x, StringComparer.Ordinal).ToArray();
                var remainingReceipts = receipts
                    .Where(r => !removedIds.Contains(r.OriginalRecordId, StringComparer.Ordinal))
                    .ToArray();
                // Keep the same two held originals, public world, seed, cutoff and query.
                // Remove all receipts of the chosen root, not just one original.
                var counterfactual = builder.BuildDistinctOriginals(
                    publicWorld, fixture.Seed, fixture.ReceiptCutoffTick,
                    fixture.RescuerId, held, remainingReceipts, regions);
                var withoutRoot = RescuerEvidenceMethodComparison.Compare(counterfactual, window);
                var methodResults = comparison.Methods.Select(before =>
                {
                    var after = withoutRoot.Methods.Single(m => m.Method == before.Method);
                    return new ConditionLensRootRemovalMethod(
                        before.Method, before.OuterCandidateRegionIds, after.OuterCandidateRegionIds,
                        after.OuterCandidateRegionIds.Except(before.OuterCandidateRegionIds, StringComparer.Ordinal)
                            .OrderBy(x => x, StringComparer.Ordinal).ToArray(),
                        before.OuterCandidateRegionIds.Except(after.OuterCandidateRegionIds, StringComparer.Ordinal)
                            .OrderBy(x => x, StringComparer.Ordinal).ToArray(),
                        before.PreferredCandidateRegionIds, after.PreferredCandidateRegionIds,
                        before.UncertainCandidateRegionIds, after.UncertainCandidateRegionIds,
                        before.ConflictedCandidateRegionIds, after.ConflictedCandidateRegionIds,
                        before.AvailableSourceGroupCount, after.AvailableSourceGroupCount);
                }).ToArray();
                rootRemovals.Add(new ConditionLensRootRemoval(
                    rootGroup.Key, removedIds, methodResults));
            }
            var detected = Questions(received, fixture.TargetTick, rootRemovals);
            var modes = new[]
            {
                new ConditionLensModeResult("none", [], comparison),
                new ConditionLensModeResult("02", detected.Where(q => q.LensIds.Contains("02")).ToArray(), comparison),
                new ConditionLensModeResult("04", detected.Where(q => q.LensIds.Contains("04")).ToArray(), comparison),
                new ConditionLensModeResult("06", detected.Where(q => q.LensIds.Contains("06")).ToArray(), comparison),
                new ConditionLensModeResult("09", detected.Where(q => q.LensIds.Contains("09")).ToArray(), comparison),
                new ConditionLensModeResult("combined", detected, comparison)
            };
            results.Add(new ConditionLensCaseResult(
                item.Id, item.Group, item.AnswerCondition,
                received.ReceivedRecords.Select(r => r.OriginalRecordId)
                    .OrderBy(x => x, StringComparer.Ordinal).ToArray(),
                existing, modes, rootRemovals));
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
                var priorQuestions = baseline.Modes.Single(m => m.Mode == "combined").Questions.Select(q => q.QuestionId).ToArray();
                var nextQuestions = condition.Modes.Single(m => m.Mode == "combined").Questions.Select(q => q.QuestionId).ToArray();
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
            2, fixture.ScenarioId, fixture.Seed, fixture.TargetPersonId,
            fixture.TargetTick, fixture.ReceiptCutoffTick, Index, results, differences);
    }

    private static IReadOnlyList<ConditionLensQuestion> Questions(
        RescuerEvidenceInput received, long tick,
        IReadOnlyList<ConditionLensRootRemoval> rootRemovals)
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
            // Person evidence can answer the missing-Person question, but it does not
            // itself establish that this Device belongs to or locates the Person.
            var hasPersonEvidence = persons.Length > 0;
            IEnumerable<ReceivedInteractionRecord> relevant = hasPersonEvidence
                ? devices.Concat(persons) : devices;
            questions.Add(new ConditionLensQuestion(
                hasPersonEvidence ? "subject-device-person-link:" + tick
                    : "subject-person-verification:" + tick,
                ["02"],
                hasPersonEvidence ? "unverified-device-person-association" : "device-is-not-person",
                target, tick, cutoff, RecordIds(relevant), ObservationIds(relevant),
                Origins(relevant),
                hasPersonEvidence ? "independently-established-device-person-association"
                    : "separately-received-confirmed-person-evidence",
                hasPersonEvidence
                    ? "Separate Person evidence is received, but does not establish that this Device is associated with the target Person."
                    : "Received Device evidence cannot establish target Person; Person evidence is missing."));
        }
        var distinctPersonAncestry = persons.Select(r =>
            string.Join("|", r.OriginalObservation.Provenance
                .Where(p => p.Kind == InteractionProvenanceKind.ExternalSource)
                .Select(p => p.SourceIdentifier).OrderBy(x => x, StringComparer.Ordinal)))
            .Distinct(StringComparer.Ordinal).Count();
        if (persons.Length > 0 && distinctPersonAncestry == 1)
        {
            questions.Add(new ConditionLensQuestion(
                "same-tick-person-corroboration:" + tick, ["06"],
                "unconfirmed-independent-same-tick-person-evidence",
                target, tick, cutoff, RecordIds(persons), ObservationIds(persons),
                Origins(persons), "additional-independent-received-person-evidence-at-the-same-tick",
                "A second independently sourced Person constraint at this tick is not established; mere coincidence or a copied receipt is insufficient."));
        }

        // 04 is a question about missing direct evidence at t, not a reverse
        // movement rule. Read only received, eligible Person originals at t+1.
        if (persons.Length == 0 && tick < long.MaxValue)
        {
            var adjacent = received.ReceivedRecords.Where(r =>
                r.OriginalObservation.ObservedTimeWindow.StartTick == tick + 1 &&
                r.OriginalObservation.ObservedTimeWindow.EndTick == tick + 1 &&
                r.OriginalObservation.IsPersonEvidence &&
                !r.OriginalObservation.IsDeviceOnlyEvidence &&
                r.OriginalObservation.EvidenceBoundaryState == EvidenceState.Confirmed &&
                r.OriginalObservation.ObservedParticipants.Any(p =>
                    p.EntityId == target && p.EntityKind == InteractionEntityKind.Person))
                .OrderBy(r => r.OriginalRecordId, StringComparer.Ordinal).ToArray();
            if (adjacent.Length > 0)
            {
                questions.Add(new ConditionLensQuestion(
                    "adjacent-followup-person-evidence:" + tick, ["04"],
                    "received-adjacent-tick-not-target-tick",
                    target, tick, cutoff, RecordIds(adjacent), ObservationIds(adjacent),
                    Origins(adjacent),
                    "received-person-evidence-at-target-tick-or-declared-public-transition",
                    "Person evidence was received for the following tick, but does not establish the target tick. Verify direct target-tick Person evidence or a declared public transition."));
            }
        }
        // 09 asks a different question from 06: even when two independent sources
        // exist, does the conclusion depend on one of them? A strict widening
        // after removing one source is evidence of model-internal dependence only.
        if (rootRemovals.Count >= 2 && rootRemovals.Any(removal =>
            removal.Methods.Any(m => m.AddedOuter.Count > 0 && m.RemovedOuter.Count == 0)))
        {
            var dependentRoots = rootRemovals.Where(removal => removal.Methods.Any(m =>
                m.AddedOuter.Count > 0 && m.RemovedOuter.Count == 0)).ToArray();
            var relevant = persons.Where(p => dependentRoots.Any(root =>
                root.RemovedReceivedOriginalIds.Contains(p.OriginalRecordId, StringComparer.Ordinal)))
                .ToArray();
            questions.Add(new ConditionLensQuestion(
                "independent-person-root-dependence:" + tick, ["09"],
                "outer-depends-on-received-independent-person-root",
                target, tick, cutoff, RecordIds(relevant), ObservationIds(relevant),
                Origins(relevant),
                "independent-confirmation-of-root-dependent-person-constraint",
                "Removing a received independent Person source root widens an existing method's outer candidates; verify the root-dependent constraint with independently received evidence. This is a diagnostic, not a rejection of the received source."));
        }
        return questions.OrderBy(q => q.QuestionId, StringComparer.Ordinal).ToArray();
    }
}
