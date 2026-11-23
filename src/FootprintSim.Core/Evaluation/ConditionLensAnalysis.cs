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
    IReadOnlyList<ConditionLensRootRemoval> RootRemovals,
    ConditionLensSahajataSourceDiagnostic SahajataSourceDiagnostic);
// This diagnostic reports CLAIMED source signatures, not independent generation.
public sealed record ConditionLensSahajataSourceDiagnostic(
    IReadOnlyList<string> EligibleReceivedPersonOriginalIds,
    int DistinctClaimedSourceSignatureCount,
    IReadOnlyList<string> ClaimedExternalSourceRootIds,
    bool QuestionEmitted, string QuestionStateReason,
    string UpstreamObservationIndependenceStatus,
    string DoctrinalCorrespondenceStatus);
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
// Explicitly separates an implemented question from a verified conditional relation.
public sealed record ConditionLensCorrespondenceAudit(
    string LensId, string QuestionImplementationStatus,
    string DoctrinalCorrespondenceStatus, string SearchEffectStatus,
    string ConditioningPhenomena, string ConditionedPhenomena,
    string ConditioningDirection, string TemporalRelation,
    string DoctrinalSource, string EngineeringQuestionTrigger,
    IReadOnlyList<string> ReceivedEngineeringInputs,
    IReadOnlyList<string> UnrepresentedDoctrinalRelations);
public sealed record ConditionLensReport(
    int SchemaVersion, string ScenarioId, int Seed, string TargetPersonId,
    long TargetTick, long ReceiptCutoffTick,
    IReadOnlyList<ConditionLensIndexEntry> Index,
    IReadOnlyList<ConditionLensCaseResult> Cases,
    IReadOnlyList<ConditionLensAnswerDifference> AnswerDifferences,
    IReadOnlyList<ConditionLensCorrespondenceAudit> CorrespondenceAudits);

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
/// Nine bounded, source-backed question lenses on the existing receipt bridge.
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
            index is 1 or 3 or 5 or 7 or 8 or 9 or 11 or 20 or 21 ? "limited-analogy-implemented" : "unimplemented-unassessed",
            index == 1 ? "received-device-subject-check" :
            index == 3 ? "received-adjacent-tick-person-check" :
            index == 5 ? "received-same-tick-person-evidence-check" :
            index == 7 ? "received-distinct-roots-upstream-provenance-check" :
            index == 8 ? "received-person-source-root-removal-dependence" :
            index == 9 ? "received-prior-tick-person-support-check" :
            index == 11 ? "received-adjacent-distinct-person-observations-shared-root-check" :
            index == 20 ? "received-distant-person-observation-temporal-applicability-check" :
            index == 21 ? "received-evidence-absence-is-not-person-absence" :
            "no-validated-footprint-mapping")).ToArray();

    // This source-to-model audit does not read held records or infer a new relation.
    // Even a resolved 06 question does not establish the original sahajata condition.
    private static readonly IReadOnlyList<ConditionLensCorrespondenceAudit> CorrespondenceAudits =
        Index.Select(entry => entry.Id == "06"
            ? new ConditionLensCorrespondenceAudit(
                entry.Id, entry.Status,
                "doctrinal-correspondence-unverified", "search-effect-unverified",
                "co-arising conditioning mental-or-material phenomena",
                "co-arising conditioned mental-or-material phenomena",
                "conditioning-to-conditioned; source examples can be mutual",
                "co-arising",
                entry.Source,
                "one-declared-external-source-root-among-received-confirmed-person-originals-at-target-exact-tick",
                ["target-person", "received-original-id", "received-observation-id",
                 "observed-exact-tick", "received-external-source-root", "receipt-cutoff"],
                ["conditioning-and-conditioned-phenomena-pair",
                 "directed-or-mutual-conditional-relation",
                 "co-arising-conditional-link"])
            : entry.Id == "05"
                ? new ConditionLensCorrespondenceAudit(
                    entry.Id, entry.Status,
                    "doctrinal-correspondence-unassessed", "search-effect-unverified",
                    "preceding mental phenomenon", "immediately succeeding mental phenomenon",
                    "preceding-to-immediately-succeeding", "immediately-successive-mental-moments",
                    entry.Source,
                    "unimplemented; E-prior-none/E-prior-future vs F-followup; received tick adjacency is not mental succession or a new 04/10 question",
                    ["received-person-observation-at-tick-before-target", "received-person-observation-at-tick-after-target", "observed-exact-tick", "receipt-cutoff"],
                    ["preceding-and-succeeding-mental-phenomena-pair", "immediate-mental-succession-conditional-link", "independent-question-distinct-from-04-and-10"])
            : entry.Id == "07"
                ? new ConditionLensCorrespondenceAudit(
                    entry.Id, entry.Status,
                    "doctrinal-correspondence-unassessed", "search-effect-unverified",
                    "mutually conditioning mental aggregates or material elements", "counterpart mental aggregates or material elements in reciprocal dependence",
                    "reciprocal-conditioning-in-both-directions", "simultaneous-mutual-conditioning",
                    entry.Source,
                    "unimplemented; S1/S3/S4/B-eligible; one/copy/two received roots do not establish original mutual condition or a new 06/08 question",
                    ["received-person-observation-at-target-exact-tick", "received-original-id", "received-external-source-root", "receipt-cutoff"],
                    ["two-conditioned-phenomena-with-reciprocal-roles", "bidirectional-mutual-conditional-links", "simultaneous-original-phenomena-relation"])
            : entry.Id == "11"
                ? new ConditionLensCorrespondenceAudit(
                    entry.Id, entry.Status,
                    "doctrinal-correspondence-unassessed", "search-effect-unverified",
                    "later-arisen mental phenomena", "earlier-arisen material body phenomena",
                    "later-mental-to-earlier-material", "later-arising-conditions-earlier-material",
                    entry.Source,
                    "unimplemented; D-none/D-eligible; a later observed or received Person record is not reverse conditioning or a new 04 question",
                    ["received-person-observation-after-target-tick", "observed-exact-tick", "receipt-cutoff"],
                    ["later-arisen-mental-phenomenon", "earlier-material-body-phenomenon", "later-to-earlier-conditional-effect"])
            : entry.Id == "23"
                ? new ConditionLensCorrespondenceAudit(
                    entry.Id, entry.Status,
                    "doctrinal-correspondence-unassessed", "search-effect-unverified",
                    "just-ceased preceding mental phenomenon", "immediately succeeding mental phenomenon",
                    "ceased-predecessor-to-successor", "cessation-just-before-succeeding-mental-moment",
                    entry.Source,
                    "unimplemented; U-none/U-future; no legally received original is not verified cessation or a new 22 question",
                    ["received-original-count", "target-tick", "receipt-cutoff"],
                    ["verified-cessation-of-preceding-mental-phenomenon", "preceding-and-succeeding-mental-phenomena-pair", "cessation-conditioning-link"])
            : entry.Id == "24"
                ? new ConditionLensCorrespondenceAudit(
                    entry.Id, entry.Status,
                    "doctrinal-correspondence-unassessed", "search-effect-unverified",
                    "not-ceased persisting phenomena", "other phenomena conditioned by their continuance",
                    "persisting-to-conditioned", "non-cessation-during-conditioning",
                    entry.Source,
                    "unimplemented; H-none/H-eligible; distant received Person observation is not persistence or a new 21 question",
                    ["received-distant-person-observation", "observed-exact-tick", "receipt-cutoff"],
                    ["verified-persistence-or-non-cessation", "conditioning-and-conditioned-phenomena-pair", "persistence-conditional-link"])

            : entry.Id == "01"
                ? new ConditionLensCorrespondenceAudit(
                    entry.Id, entry.Status,
                    "doctrinal-correspondence-unassessed", "search-effect-unverified",
                    "six mental roots including greed hatred delusion and their opposites",
                    "associated mental phenomena and root-produced material phenomena",
                    "mental-root-to-associated-mental-and-produced-material",
                    "co-arising-root-conditionality",
                    entry.Source,
                    "unimplemented; K4-nested/B-eligible; ExternalSource ancestry roots and root removal show provenance sensitivity, not original mental roots",
                    ["received-confirmed-person-observation", "received-external-source-root",
                     "root-removal-method-differences", "receipt-cutoff"],
                    ["mental-root-and-conditioned-phenomena-pair",
                     "mental-root-classification", "root-to-conditioned-phenomena-conditional-link"])
            : entry.Id == "03"
                ? new ConditionLensCorrespondenceAudit(
                    entry.Id, entry.Status,
                    "doctrinal-correspondence-unassessed", "search-effect-unverified",
                    "predominant desire effort consciousness investigation or object",
                    "mental-factor predominance: associated mental phenomena and the material phenomena they produce; object predominance: consciousness and mental factors arising with that outstanding object",
                    "dominant-mental-factor-to-associated-mental-and-produced-material;predominant-object-to-consciousness-and-mental-factors",
                    "co-arising-or-object-predominance-conditionality",
                    entry.Source,
                    "unimplemented; K4-nested/N-redund/S4; candidate impact or distinct-root redundancy cannot measure mental predominance or evidence trust",
                    ["received-external-source-root", "root-removal-candidate-delta",
                     "received-distinct-source-roots", "receipt-cutoff"],
                    ["dominant-mental-factor-or-object", "conditioned-mental-phenomena",
                     "mental-factor-to-produced-material-conditional-link",
                     "predominant-object-to-mental-phenomena-conditional-link",
                     "predominance-conditional-link"])
            : entry.Id == "19"
                ? new ConditionLensCorrespondenceAudit(
                    entry.Id, entry.Status,
                    "doctrinal-correspondence-unassessed", "search-effect-unverified",
                    "associated immaterial aggregates",
                    "other associated immaterial aggregates",
                    "mutual-association-between-immaterial-aggregates",
                    "co-arising-associated-mental-phenomena",
                    entry.Source,
                    "unimplemented; S1/S4/N-redund; same-tick Person observations or distinct provenance roots do not establish associated mental aggregates",
                    ["received-person-observation-at-target-exact-tick",
                     "received-external-source-root", "receipt-cutoff"],
                    ["mutually-associated-immaterial-aggregates-pair",
                     "mutual-mental-association-links", "original-associated-mental-phenomena"])
            : entry.Id == "20"
                ? new ConditionLensCorrespondenceAudit(
                    entry.Id, entry.Status,
                    "doctrinal-correspondence-unassessed", "search-effect-unverified",
                    "mental or material phenomena as non-associated conditioning side",
                    "opposite material or mental phenomena being conditioned",
                    "mental-to-material-and-material-to-mental-distinct",
                    "context-dependent-mental-material-conditionality",
                    entry.Source,
                    "unimplemented; A-none/A-eligible; Device-only and Person evidence are physical entity kinds, not original mental-material non-association",
                    ["received-device-only-observation", "received-person-observation",
                     "entity-kind-person-device-distinction", "receipt-cutoff"],
                    ["directed-mental-material-conditional-relations",
                     "mental-to-material-conditional-link", "material-to-mental-conditional-link"])

            : entry.Id == "13"
                ? new ConditionLensCorrespondenceAudit(
                    entry.Id, entry.Status,
                    "doctrinal-correspondence-unassessed", "search-effect-unverified",
                    "context-a: wholesome-or-unwholesome-kamma;context-b: associated-volition-cetana",
                    "context-a: resultant-aggregates-and-kamma-produced-material-phenomena;context-b: associated-phenomena-and-material-phenomena-produced-by-them",
                    "context-a-kamma-to-resultant-aggregates-and-kamma-produced-matter;context-b-volition-to-associated-phenomena-and-produced-matter",
                    "context-a-kamma-result-conditioning-not-observation-tick-order;context-b-associated-volition-conditioning-not-receipt-order",
                    entry.Source,
                    "unimplemented; observed and received ticks plus source ancestry cannot establish either kamma causing resultant aggregates or volition conditioning associated mental and produced material phenomena",
                    ["observed-exact-tick", "receipt-tick", "received-external-source-root", "receipt-cutoff"],
                    ["context-a-kamma-conditioning-side", "context-a-resultant-aggregates-and-kamma-born-material-side", "context-a-kamma-to-resultant-conditional-link", "context-b-volition-conditioning-side", "context-b-associated-and-produced-material-side", "context-b-volition-to-associated-conditional-link"])

            : entry.Id == "14"
                ? new ConditionLensCorrespondenceAudit(
                    entry.Id, entry.Status,
                    "doctrinal-correspondence-unassessed", "search-effect-unverified",
                    "mutually conditioning resultant immaterial aggregates",
                    "other resultant immaterial aggregates in reciprocal dependence",
                    "mutual-resultant-immaterial-aggregates-conditioning-in-both-directions",
                    "co-arising-mutual-resultant-conditioning-not-algorithmic-outcome",
                    entry.Source,
                    "unimplemented; B-eligible/K2-conflict method outcomes and outer/preferred/uncertain/conflicted candidate sets are algorithmic results, not resultant aggregates",
                    ["method-comparison-outer", "method-comparison-preferred", "method-comparison-uncertain", "method-comparison-conflicted", "receipt-cutoff"],
                    ["resultant-immaterial-aggregates-on-both-sides", "mutual-resultant-aggregates-conditional-links"])

            : entry.Id == "15"
                ? new ConditionLensCorrespondenceAudit(
                    entry.Id, entry.Status,
                    "doctrinal-correspondence-unassessed", "search-effect-unverified",
                    "context-a: physical-food-nutriment;context-b: immaterial-nutriments",
                    "context-a: bodily-material-phenomena;context-b: associated-phenomena-and-material-phenomena-produced-by-them",
                    "context-a-food-to-bodily-materiality;context-b-immaterial-nutriments-to-associated-phenomena-and-produced-matter",
                    "context-a-material-nutrition;context-b-associated-immaterial-nutrition-not-evidence-receipt",
                    entry.Source,
                    "unimplemented; U-none/U-eligible received record counts and ticks do not show food sustaining bodily matter or immaterial nutriments conditioning associated phenomena",
                    ["received-original-count", "observed-exact-tick", "receipt-cutoff"],
                    ["context-a-food-and-bodily-material-conditioning-link", "context-b-immaterial-nutriment-and-associated-phenomena-link", "context-b-immaterial-nutriment-to-produced-material-link"])

            : entry.Id == "16"
                ? new ConditionLensCorrespondenceAudit(
                    entry.Id, entry.Status,
                    "doctrinal-correspondence-unassessed", "search-effect-unverified",
                    "context-a: five-material-sense-faculties;context-b: material-life-faculty;context-c: immaterial-faculties",
                    "context-a: corresponding-sense-consciousness-and-associated-phenomena;context-b: kamma-produced-material-phenomena;context-c: associated-phenomena-and-material-phenomena-produced-by-them",
                    "context-a-sense-faculties-to-consciousness-and-associated-phenomena;context-b-material-life-faculty-to-kamma-born-matter;context-c-immaterial-faculties-to-associated-phenomena-and-produced-matter",
                    "context-a-corresponding-sense-conditionality;context-b-life-faculty-material-conditionality;context-c-immaterial-faculty-conditionality",
                    entry.Source,
                    "unimplemented; A-none/A-eligible Person/Device entity kinds and source metadata do not identify sense faculties, material-life faculty or immaterial faculties",
                    ["received-device-only-observation", "received-person-observation", "entity-kind-person-device-distinction", "receipt-cutoff"],
                    ["context-a-five-sense-faculties-and-corresponding-consciousness-link", "context-b-material-life-faculty-and-kamma-born-matter-link", "context-c-immaterial-faculties-and-produced-material-link"])

            : entry.Id == "17"
                ? new ConditionLensCorrespondenceAudit(
                    entry.Id, entry.Status,
                    "doctrinal-correspondence-unassessed", "search-effect-unverified",
                    "mental-jhana-factors",
                    "jhana-associated-mental-phenomena-and-material-phenomena-produced-by-them",
                    "jhana-factors-to-associated-phenomena-and-produced-matter",
                    "jhana-factor-conditionality-not-same-tick-source-corroboration",
                    entry.Source,
                    "unimplemented; S1/S4 same-tick received Person roots and root-removal candidate changes do not establish jhana factors or their material results",
                    ["received-person-observation-at-target-exact-tick", "received-external-source-root", "root-removal-candidate-delta", "receipt-cutoff"],
                    ["jhana-factors-conditioner", "jhana-associated-mental-phenomena", "jhana-factors-to-produced-material-conditional-link"])

            : entry.Id == "18"
                ? new ConditionLensCorrespondenceAudit(
                    entry.Id, entry.Status,
                    "doctrinal-correspondence-unassessed", "search-effect-unverified",
                    "mental-path-factors",
                    "path-associated-mental-phenomena-and-material-phenomena-produced-by-them",
                    "mental-path-factors-to-associated-phenomena-and-produced-matter",
                    "path-factor-conditionality-not-public-geographic-path",
                    entry.Source,
                    "unimplemented; existing #106 public-geographic-route question or geographic reachability cannot represent mental path factors",
                    ["public-declared-geographic-path", "public-region-reachability", "received-person-observation", "receipt-cutoff"],
                    ["mental-path-factors-conditioner", "path-associated-mental-phenomena", "path-factors-to-produced-material-conditional-link"])
            : entry.Id == "02"
                ? new ConditionLensCorrespondenceAudit(
                    entry.Id, entry.Status,
                    "doctrinal-correspondence-unassessed", "search-effect-unverified",
                    "context-a: visible-sound-odor-taste-touch-objects;context-b: other-objects-of-mind",
                    "context-a: corresponding-sense-consciousness-and-associated-mental-factors;context-b: mind-consciousness-and-mental-factors-taking-that-object",
                    "context-a-sense-object-to-corresponding-consciousness-and-factors;context-b-mind-object-to-mind-consciousness-and-factors",
                    "object-conditions-cognition-not-device-person-observation-tick",
                    entry.Source,
                    "A-none/A-eligible/A-future/C-none; Device-only receipt prompts Person subject or association check, not sense-object conditioning",
                    ["received-device-only-observation", "received-person-observation", "entity-kind-person-device-distinction", "receipt-cutoff"],
                    ["sense-object-to-consciousness-conditional-link", "object-and-mental-phenomena-pair", "device-record-is-not-sense-object"])

            : entry.Id == "04"
                ? new ConditionLensCorrespondenceAudit(
                    entry.Id, entry.Status,
                    "doctrinal-correspondence-unassessed", "search-effect-unverified",
                    "preceding-consciousness-and-associated-mental-factors",
                    "immediately-succeeding-consciousness-and-associated-mental-factors",
                    "preceding-mental-moment-to-immediately-succeeding-mental-moment",
                    "immediate-mental-succession-not-target-tick-plus-one-observation",
                    entry.Source,
                    "F-followup positive, E-prior-eligible negative; actual engineering question uses received Person at t+1 to ask about t without backward inference",
                    ["received-person-observation-at-tick-after-target", "observed-exact-tick", "receipt-cutoff"],
                    ["preceding-and-succeeding-mental-phenomena-pair", "original-mental-succession-conditional-link", "observation-t-plus-one-is-not-mental-moment"])

            : entry.Id == "08"
                ? new ConditionLensCorrespondenceAudit(
                    entry.Id, entry.Status,
                    "doctrinal-correspondence-unassessed", "search-effect-unverified",
                    "context-a: four-immaterial-aggregates;context-b: four-great-elements;context-c: rebirth-mental-and-material;context-d: consciousness-and-mental-factors;context-e: four-great-elements;context-f: sense-bases-and-material-supports",
                    "context-a: mutually-supporting-immaterial-aggregates;context-b: mutually-supporting-great-elements;context-c: mutually-supporting-mental-and-material;context-d: mind-produced-material-phenomena;context-e: derived-material-phenomena;context-f: corresponding-consciousness-and-associated-mental-factors",
                    "context-a-mutual-immaterial-support;context-b-mutual-material-support;context-c-mutual-rebirth-support;context-d-consciousness-and-mental-factors-to-mind-born-matter;context-e-great-elements-to-derived-matter;context-f-material-bases-to-consciousness-and-factors",
                    "context-a-c-mutual-or-coexisting;context-d-consciousness-produced-materiality;context-e-f-support-material-to-mental-context",
                    entry.Source,
                    "N-redundant/S4 distinct received roots and unchanged four-method outcome; N-overlap/N-same-root are negative, provenance not original support",
                    ["received-confirmed-person-observation-at-target-exact-tick", "received-distinct-source-roots", "root-removal-method-differences", "receipt-cutoff"],
                    ["mutual-mental-material-conditional-links", "consciousness-and-mental-factors-to-mind-born-matter-link", "material-sense-bases-to-consciousness-link", "upstream-generation-not-proven-by-root-labels"])

            : entry.Id == "09"
                ? new ConditionLensCorrespondenceAudit(
                    entry.Id, entry.Status,
                    "doctrinal-correspondence-unassessed", "search-effect-unverified",
                    "context-a: preceding-wholesome-unwholesome-or-neutral-mental-phenomena;context-b: weather-food-persons-and-dwellings-as-conditioners-in-pali",
                    "context-a: later-mental-phenomena-with-non-immediate-qualifications;context-b: conditioned-side-not-stated-in-pali-[to beings]-translator-supplement-only",
                    "context-a-prior-mental-phenomena-to-later-mental-phenomena;context-b-environmental-conditioners-with-no-explicit-pali-conditioned-side",
                    "context-a-prior-to-later-with-non-immediate-qualifications;context-b-environmental-support-not-observation-time-order",
                    entry.Source,
                    "B-eligible/K2-conflict legal received Person root removal changes outer/conflict; B-none/B-future negative, not environmental support",
                    ["received-external-source-root", "received-person-observation", "root-removal-candidate-delta", "receipt-cutoff"],
                    ["prior-mental-phenomena-to-later-mental-phenomena-conditional-link", "environmental-weather-food-person-dwelling-conditioning-link", "context-b-pali-conditioned-side-not-explicit", "translator-[to beings]-not-original-specified-conditioned-side"])

            : entry.Id == "10"
                ? new ConditionLensCorrespondenceAudit(
                    entry.Id, entry.Status,
                    "doctrinal-correspondence-unassessed", "search-effect-unverified",
                    "prior-arisen-material-sense-bases-and-visible-objects",
                    "subsequently-arising-sense-consciousness-and-associated-mental-factors",
                    "prior-material-phenomena-to-later-consciousness-and-associated-factors",
                    "prior-arisen-material-before-later-mental-phenomena;conditioning-applies-in-some-cases-not-others",
                    entry.Source,
                    "E-prior-none/E-prior-future already have received Person at t-1 without target-t confirmation and ask for target-t evidence or declared forward reachability; E-prior-eligible resolves the question, F-followup is not 10",
                    ["received-person-observation-at-tick-before-target", "observed-exact-tick", "receipt-cutoff"],
                    ["prior-material-basis-and-subsequent-consciousness-pair", "material-to-mental-conditional-link", "original-conditional-and-nonconditional-cases"])

            : entry.Id == "12"
                ? new ConditionLensCorrespondenceAudit(
                    entry.Id, entry.Status,
                    "doctrinal-correspondence-unassessed", "search-effect-unverified",
                    "context-a: preceding-wholesome-mental-phenomena;context-b: preceding-unwholesome-mental-phenomena;context-c: preceding-functional-neutral-mental-phenomena",
                    "context-a: following-wholesome-mental-phenomena;context-b: following-unwholesome-mental-phenomena;context-c: following-functional-neutral-mental-phenomena",
                    "context-a-wholesome-to-wholesome;context-b-unwholesome-to-unwholesome;context-c-functional-neutral-to-functional-neutral",
                    "successive-same-kind-mental-repetition-not-record-retransmission",
                    entry.Source,
                    "T-repeat distinct original observations at t-1/t with shared received source root; T-independent/T-copy/T-future-prior negatives, not mental repetition",
                    ["received-distinct-original-observation-ids", "observed-exact-tick", "received-external-source-root", "receipt-cutoff"],
                    ["wholesome-unwholesome-functional-neutral-classification", "successive-mental-phenomena-pair", "same-kind-repetition-conditional-link"])

            : entry.Id == "21"
                ? new ConditionLensCorrespondenceAudit(
                    entry.Id, entry.Status,
                    "doctrinal-correspondence-unassessed", "search-effect-unverified",
                    "context-a: four-immaterial-aggregates;context-b: four-great-elements;context-c: rebirth-mental-and-material;context-d: consciousness-and-mental-factors;context-e: four-great-elements;context-f: material-sense-bases-and-objects",
                    "context-a: mutually-present-immaterial-aggregates;context-b: mutually-present-great-elements;context-c: mutually-present-mental-and-material;context-d: mind-produced-material-phenomena;context-e: derived-material-phenomena;context-f: corresponding-consciousness-and-associated-mental-factors",
                    "context-a-mutual-present-immaterial;context-b-mutual-present-great-elements;context-c-mutual-present-rebirth;context-d-consciousness-and-mental-factors-to-mind-born-matter;context-e-great-elements-to-derived-matter;context-f-present-material-bases-to-consciousness-and-factors",
                    "present-conditioning-phenomena-not-distant-received-observation-or-current-person-presence",
                    entry.Source,
                    "H-none distant received Person observation at tick 7 asks about applicability at target 5; H-eligible resolves, no evidence of presently existing original condition",
                    ["received-distant-person-observation", "observed-exact-tick", "receipt-cutoff"],
                    ["present-mental-and-material-conditional-relations", "consciousness-and-mental-factors-to-mind-born-matter-link", "actual-presence-of-conditioning-phenomena", "past-observation-is-not-present-condition"])

            : entry.Id == "22"
                ? new ConditionLensCorrespondenceAudit(
                    entry.Id, entry.Status,
                    "doctrinal-correspondence-unassessed", "search-effect-unverified",
                    "immediately-ceased-preceding-consciousness-and-mental-factors",
                    "presently-arising-consciousness-and-associated-mental-factors",
                    "ceased-preceding-mental-phenomena-to-present-mental-phenomena",
                    "cessation-immediately-before-present-mental-arising-not-receipt-absence",
                    entry.Source,
                    "U-none/U-future no legally received originals raises availability question, U-eligible/U-device negative for 22; no proof of Person or mental cessation",
                    ["received-original-count", "receipt-cutoff", "entity-kind-person-device-distinction"],
                    ["verified-cessation-of-preceding-consciousness-and-mental-factors", "preceding-and-current-mental-phenomena-pair", "cessation-to-present-conditional-link", "no-received-evidence-is-not-mental-cessation"])
            : new ConditionLensCorrespondenceAudit(
                entry.Id, entry.Status,
                "doctrinal-correspondence-unassessed", "search-effect-unverified",
                "not-assessed", "not-assessed", "not-assessed", "not-assessed",
                entry.Source,
                entry.Status == "limited-analogy-implemented"
                    ? "existing-engineering-question-only"
                    : "not-implemented",
                [], ["doctrinal-conditional-relation-not-assessed"]))
        .ToArray();

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
            // Diagnostic only. Match the existing 06 question's post-receipt
            // Person/Confirmed/exact-tick filter and ExternalSource signature.
            // Different claimed identifiers cannot establish independent
            // original observation generation or the original co-arising relation.
            var eligibleFor06 = received.ReceivedRecords.Where(r =>
                r.OriginalObservation.IsPersonEvidence &&
                !r.OriginalObservation.IsDeviceOnlyEvidence &&
                r.OriginalObservation.EvidenceBoundaryState == EvidenceState.Confirmed &&
                r.OriginalObservation.ObservedTimeWindow.StartTick == fixture.TargetTick &&
                r.OriginalObservation.ObservedTimeWindow.EndTick == fixture.TargetTick &&
                r.OriginalObservation.ObservedParticipants.Any(p =>
                    p.EntityId == fixture.TargetPersonId &&
                    p.EntityKind == InteractionEntityKind.Person))
                .OrderBy(r => r.OriginalRecordId, StringComparer.Ordinal).ToArray();
            var claimedSignatures = eligibleFor06.Select(r => string.Join("|",
                    r.OriginalObservation.Provenance
                        .Where(p => p.Kind == InteractionProvenanceKind.ExternalSource)
                        .Select(p => p.SourceIdentifier)
                        .OrderBy(id => id, StringComparer.Ordinal)))
                .Distinct(StringComparer.Ordinal).Count();
            var question06Emitted = detected.Any(q => q.LensIds.Contains("06"));
            var sahajataDiagnostic = new ConditionLensSahajataSourceDiagnostic(
                eligibleFor06.Select(r => r.OriginalRecordId)
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(id => id, StringComparer.Ordinal).ToArray(),
                claimedSignatures,
                eligibleFor06.SelectMany(r => r.OriginalObservation.Provenance
                        .Where(p => p.Kind == InteractionProvenanceKind.ExternalSource)
                        .Select(p => p.SourceIdentifier))
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(id => id, StringComparer.Ordinal).ToArray(),
                question06Emitted,
                question06Emitted ? "one-claimed-source-signature-question-emitted"
                    : eligibleFor06.Length == 0 ? "no-eligible-received-person-original"
                    : claimedSignatures == 0 ? "no-claimed-source-signature"
                    : "multiple-claimed-source-signatures-question-not-emitted",
                "unknown-not-verifiable-from-received-input",
                "doctrinal-correspondence-unverified");
            var modes = new[]
            {
                new ConditionLensModeResult("none", [], comparison),
                new ConditionLensModeResult("02", detected.Where(q => q.LensIds.Contains("02")).ToArray(), comparison),
                new ConditionLensModeResult("04", detected.Where(q => q.LensIds.Contains("04")).ToArray(), comparison),
                new ConditionLensModeResult("06", detected.Where(q => q.LensIds.Contains("06")).ToArray(), comparison),
                new ConditionLensModeResult("08", detected.Where(q => q.LensIds.Contains("08")).ToArray(), comparison),
                new ConditionLensModeResult("09", detected.Where(q => q.LensIds.Contains("09")).ToArray(), comparison),
                new ConditionLensModeResult("10", detected.Where(q => q.LensIds.Contains("10")).ToArray(), comparison),
                new ConditionLensModeResult("12", detected.Where(q => q.LensIds.Contains("12")).ToArray(), comparison),
                new ConditionLensModeResult("21", detected.Where(q => q.LensIds.Contains("21")).ToArray(), comparison),
                new ConditionLensModeResult("22", detected.Where(q => q.LensIds.Contains("22")).ToArray(), comparison),
                new ConditionLensModeResult("combined", detected, comparison)
            };
            results.Add(new ConditionLensCaseResult(
                item.Id, item.Group, item.AnswerCondition,
                received.ReceivedRecords.Select(r => r.OriginalRecordId)
                    .OrderBy(x => x, StringComparer.Ordinal).ToArray(),
                existing, modes, rootRemovals, sahajataDiagnostic));
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
            13, fixture.ScenarioId, fixture.Seed, fixture.TargetPersonId,
            fixture.TargetTick, fixture.ReceiptCutoffTick, Index, results, differences,
            CorrespondenceAudits);
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
        // 22 is a deliberately weak engineering question about receipt visibility,
        // NOT the Paṭṭhāna's cessation condition or evidence of a Person's absence.
        // Consult only the post-gate received set; never quote held/future originals.
        if (received.ReceivedRecords.Count == 0)
        {
            questions.Add(new ConditionLensQuestion(
                "no-received-records-person-availability:" + tick, ["22"],
                "no-received-evidence-is-not-person-absence",
                target, tick, cutoff, [], [], [],
                "legitimately-received-person-evidence-at-target-tick-or-availability-check",
                "No evidence originals were received by the cutoff; that is not proof that the Person is absent. Ask what Person evidence could legitimately be obtained, without assuming knowledge of held or future records."));
        }
        // AR-90 / Rev13: an off-tick Device receipt can silence all existing
        // questions without providing ANY target-Person observation. This is a
        // missing-information question only, never a Person/Device inference.
        // The builder has already applied the receipt cutoff; inspect no held originals.
        var offTickDeviceOnly = received.ReceivedRecords
            .OrderBy(r => r.OriginalRecordId, StringComparer.Ordinal).ToArray();
        if (offTickDeviceOnly.Length > 0 && offTickDeviceOnly.All(r =>
            r.OriginalObservation.IsDeviceOnlyEvidence &&
            r.OriginalObservation.EvidenceBoundaryState == EvidenceState.Confirmed &&
            r.OriginalObservation.ObservedTimeWindow.StartTick ==
                r.OriginalObservation.ObservedTimeWindow.EndTick &&
            r.OriginalObservation.ObservedTimeWindow.StartTick != tick))
        {
            var observedTicks = offTickDeviceOnly.Select(r =>
                    r.OriginalObservation.ObservedTimeWindow.StartTick)
                .Distinct().OrderBy(t => t)
                .Select(t => t.ToString(System.Globalization.CultureInfo.InvariantCulture));
            questions.Add(new ConditionLensQuestion(
                "off-tick-device-only-person-availability:" + tick, ["22"],
                "received-offtick-device-without-target-person-evidence",
                target, tick, cutoff, RecordIds(offTickDeviceOnly),
                ObservationIds(offTickDeviceOnly), Origins(offTickDeviceOnly),
                "legitimately-received-target-person-evidence-or-availability-check",
                $"Only Confirmed Device-only original(s) at tick(s) {string.Join(", ", observedTicks)} were received by cutoff {cutoff}, not target tick {tick}. These are not Person evidence, and their receipt cannot establish the Person's presence or absence. Ask whether target-Person evidence could legitimately be obtained; infer no location or physical relation."));
        }
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

        // 10 contrasts with 04: received Person evidence at t-1 is NOT direct
        // evidence at target t. Never infer a forward transition from this fact.
        // A target tick of zero has no valid preceding nonnegative tick.
        if (persons.Length == 0 && tick > 0)
        {
            var prior = received.ReceivedRecords.Where(r =>
                r.OriginalObservation.ObservedTimeWindow.StartTick == tick - 1 &&
                r.OriginalObservation.ObservedTimeWindow.EndTick == tick - 1 &&
                r.OriginalObservation.IsPersonEvidence &&
                !r.OriginalObservation.IsDeviceOnlyEvidence &&
                r.OriginalObservation.EvidenceBoundaryState == EvidenceState.Confirmed &&
                r.OriginalObservation.ObservedParticipants.Any(p =>
                    p.EntityId == target && p.EntityKind == InteractionEntityKind.Person))
                .OrderBy(r => r.OriginalRecordId, StringComparer.Ordinal).ToArray();
            if (prior.Length > 0)
            {
                questions.Add(new ConditionLensQuestion(
                    "prior-tick-person-support:" + tick, ["10"],
                    "received-prior-tick-not-target-tick",
                    target, tick, cutoff, RecordIds(prior), ObservationIds(prior),
                    Origins(prior),
                    "received-person-evidence-at-target-tick-or-declared-forward-public-reachability",
                    "Received Person evidence at the preceding tick is not direct evidence at the target tick. Verify target-tick Person evidence or declared forward public reachability; do not presume any transition."));
            }
        }

        // 21 (atthi) is a weak engineering question about temporal applicability,
        // NOT a doctrinal or physical rule of presence or movement. Only received,
        // Confirmed, exact-tick evidence for the target Person is eligible.
        // The receipt gate already excludes held-only and future observations.
        var receivedPersonAtExactTick = received.ReceivedRecords.Where(r =>
            r.OriginalObservation.IsPersonEvidence &&
            !r.OriginalObservation.IsDeviceOnlyEvidence &&
            r.OriginalObservation.EvidenceBoundaryState == EvidenceState.Confirmed &&
            r.OriginalObservation.ObservedTimeWindow.StartTick ==
                r.OriginalObservation.ObservedTimeWindow.EndTick &&
            r.OriginalObservation.ObservedParticipants.Any(p =>
                p.EntityId == target && p.EntityKind == InteractionEntityKind.Person))
            .OrderBy(r => r.OriginalRecordId, StringComparer.Ordinal).ToArray();
        static bool IsAtOrAdjacent(long observedTick, long targetTick) =>
            observedTick < targetTick
                ? targetTick - observedTick <= 1
                : observedTick - targetTick <= 1;
        if (!receivedPersonAtExactTick.Any(r => IsAtOrAdjacent(
            r.OriginalObservation.ObservedTimeWindow.StartTick, tick)))
        {
            var distant = receivedPersonAtExactTick.Where(r =>
                !IsAtOrAdjacent(r.OriginalObservation.ObservedTimeWindow.StartTick, tick))
                .ToArray();
            if (distant.Length > 0)
            {
                var observedTicks = distant
                    .Select(r => r.OriginalObservation.ObservedTimeWindow.StartTick)
                    .Distinct().OrderBy(t => t)
                    .Select(t => t.ToString(System.Globalization.CultureInfo.InvariantCulture));
                questions.Add(new ConditionLensQuestion(
                    "distant-person-temporal-applicability:" + tick, ["21"],
                    "received-distant-person-observation-not-target-tick",
                    target, tick, cutoff, RecordIds(distant), ObservationIds(distant),
                    Origins(distant),
                    "direct-target-tick-person-evidence-or-declared-temporal-applicability",
                    $"Received Confirmed Person evidence at tick(s) {string.Join(", ", observedTicks)} is at least two ticks away from target tick {tick}; receiving it does not establish applicability at the target tick. Ask for legitimately received direct Person evidence or a declared public temporal relation. Neither presence nor absence nor movement is inferred."));
            }
        }

        // 12 is a question about provenance of two DIFFERENT, already-received
        // Person observations on adjacent ticks, not a rule for movement or
        // duplicate suppression. Same ExternalSource does not mean same fact.
        if (tick > 0 && persons.Length > 0)
        {
            var preceding = received.ReceivedRecords.Where(r =>
                r.OriginalObservation.ObservedTimeWindow.StartTick == tick - 1 &&
                r.OriginalObservation.ObservedTimeWindow.EndTick == tick - 1 &&
                r.OriginalObservation.IsPersonEvidence &&
                !r.OriginalObservation.IsDeviceOnlyEvidence &&
                r.OriginalObservation.EvidenceBoundaryState == EvidenceState.Confirmed &&
                r.OriginalObservation.ObservedParticipants.Any(p =>
                    p.EntityId == target && p.EntityKind == InteractionEntityKind.Person))
                .OrderBy(r => r.OriginalRecordId, StringComparer.Ordinal).ToArray();
            foreach (var earlier in preceding)
            {
                var earlierRoots = Origins([earlier]);
                if (earlierRoots.Length != 1)
                    continue;
                foreach (var current in persons)
                {
                    // Two distinct originals and observations, one known source root.
                    // A duplicate receipt of an original is already removed by Builder.
                    if (earlier.OriginalRecordId == current.OriginalRecordId ||
                        earlier.OriginalObservationId == current.OriginalObservationId ||
                        !Origins([current]).SequenceEqual(earlierRoots))
                        continue;
                    var relevant = new[] { earlier, current };
                    questions.Add(new ConditionLensQuestion(
                        "repeated-temporal-person-observations:" + tick, ["12"],
                        "repeated-temporal-person-observations-share-source-root",
                        target, tick, cutoff, RecordIds(relevant),
                        ObservationIds(relevant), earlierRoots,
                        "original-observation-generation-provenance-and-times-for-both-received-person-records",
                        $"Received Person observations at ticks {tick - 1} and {tick} have distinct original and observation IDs but share one source root. Verify whether each is a separate original observation rather than a copy or re-timestamped record; do not infer continuous presence or independent sources."));
                }
            }
        }
        // 08 asks about unknown common upstream provenance of two distinct
        // received, same-tick Person roots whose effects are region-redundant.
        // Source-group changes are reported but are not candidate changes.
        static bool SameRegions(IReadOnlyList<string>? before, IReadOnlyList<string>? after) =>
            before is null ? after is null :
            after is not null && before.SequenceEqual(after, StringComparer.Ordinal);
        if (persons.Length == 2 &&
            RecordIds(persons).Length == 2 &&
            ObservationIds(persons).Length == 2 &&
            persons.All(p => Origins([p]).Length == 1) &&
            Origins(persons).Length == 2 &&
            rootRemovals.Count == 2 &&
            rootRemovals.All(removal => removal.Methods.All(method =>
                SameRegions(method.BeforeOuter, method.AfterOuter) &&
                SameRegions(method.BeforePreferred, method.AfterPreferred) &&
                SameRegions(method.BeforeUncertain, method.AfterUncertain) &&
                SameRegions(method.BeforeConflicted, method.AfterConflicted))))
        {
            questions.Add(new ConditionLensQuestion(
                "apparently-redundant-person-roots-upstream-check:" + tick, ["08"],
                "received-distinct-roots-upstream-provenance-unverified-despite-redundant-outcome",
                target, tick, cutoff, RecordIds(persons), ObservationIds(persons),
                Origins(persons),
                "original-observation-generation-sources-forwarding-paths-and-possible-shared-upstream",
                "Two received same-tick Person observations claim distinct ExternalSource roots, but removing either leaves all methods' outer, preferred, uncertain and conflicted regions unchanged. Verify whether the origins share an upstream observation or derive from separate original observations. Neither independence nor common origin is established; evidence and source groups are not changed."));
        }

        // 09 diagnoses dependence on independently received Person roots.
        // A strict widening is the original positive case. AR-88 also asks when
        // removing one root strictly relieves a conflict and shrinks outer.
        // Neither counterfactual changes the actual received evidence or methods.
        static bool WidensOuter(ConditionLensRootRemovalMethod method) =>
            method.AddedOuter.Count > 0 && method.RemovedOuter.Count == 0;
        static bool RelievesConflict(ConditionLensRootRemovalMethod method) =>
            method.BeforeConflicted is { Count: > 0 } before &&
            method.AfterConflicted is not null &&
            before.Except(method.AfterConflicted, StringComparer.Ordinal).Any() &&
            !method.AfterConflicted.Except(before, StringComparer.Ordinal).Any() &&
            method.RemovedOuter.Count > 0;

        if (rootRemovals.Count >= 2)
        {
            var dependentRoots = rootRemovals.Where(removal =>
                removal.Methods.Any(m => WidensOuter(m) || RelievesConflict(m))).ToArray();
            if (dependentRoots.Length > 0)
            {
                // Preserve the old question ID, reason and explanation whenever
                // widening exists; do not multiply questions for mixed results.
                var hasWidening = dependentRoots.Any(root =>
                    root.Methods.Any(WidensOuter));
                var relevant = persons.Where(p => dependentRoots.Any(root =>
                    root.RemovedReceivedOriginalIds.Contains(
                        p.OriginalRecordId, StringComparer.Ordinal))).ToArray();
                questions.Add(new ConditionLensQuestion(
                    "independent-person-root-dependence:" + tick, ["09"],
                    hasWidening
                        ? "outer-depends-on-received-independent-person-root"
                        : "conflicted-outcome-depends-on-received-independent-person-root",
                    target, tick, cutoff, RecordIds(relevant), ObservationIds(relevant),
                    Origins(relevant),
                    hasWidening
                        ? "independent-confirmation-of-root-dependent-person-constraint"
                        : "independent-confirmation-of-conflicting-person-source-roots",
                    hasWidening
                        ? "Removing a received independent Person source root widens an existing method's outer candidates; verify the root-dependent constraint with independently received evidence. This is a diagnostic, not a rejection of the received source."
                        : "Removing a received independent Person source root strictly reduces conflicting candidates and shrinks outer candidates in an existing method; independently confirm the conflicting roots. This is a diagnostic, not a rejection of any received source."));
            }
        }
        return questions.OrderBy(q => q.QuestionId, StringComparer.Ordinal).ToArray();
    }
}
