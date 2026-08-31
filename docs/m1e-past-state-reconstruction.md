# M1-E: Past-state reconstruction

## Status and purpose

This document is the preregistration and design record for M1-E. The adopted baseline is
footprint-research-concept-baseline-v0.2.1.md. M1-E asks whether candidate historical states can
be constructed from public, search-usable evidence distributed across the current query context
without using a realized truth path or repeatedly walking that path backward.

The implementation uses ordinary descriptive and established computing terms. Index Access,
present-state bundle, Jump, Causal Nexus, and causal sandwich are not promoted to final concepts.
Where this document says present evidence set, it means a query-time collection of observations,
relations, constraints, source ancestry, and public model information. It is an analysis input,
not a new world-side entity.

The baseline's `FIXED`, `HYPOTHESIS`, `DEFERRED`, `UNRESOLVED`, and `SUPERSEDED` labels are retained
as status boundaries. The implementation also checked the M1-D and M1-T0 documents and the supplied
research archive, full causal-index synthesis, inventory/gap analysis, session outcomes, and the
archive section containing the earlier Causal Nexus resolution. The standalone research files are
inputs to this decision record and are not modified by M1-E.

## Boundary from M1-D

M1-D Operational Causal Tracing follows already-known public structure forward or backward and
applies public cross-thread constraints to an already-supplied candidate warp. M1-E reuses that
tracer for two comparison methods. It adds a separate operation that constructs candidates for a
specified historical window from a query and a public evidence set.

Tracing and reconstruction are therefore not synonyms:

- tracing follows known public relations;
- reconstruction infers candidate states that are not directly supplied as observed truth;
- addressing gives each constructed candidate a stable machine-readable identity;
- verification checks a candidate with eligible evidence lineage not used to generate it; and
- local traversal optionally follows public relations only near an already-constructed candidate.

The legacy causal-sandwich label remains in the M1-D historical artifact. M1-E treats the underlying
operation as separate upstream, local, and downstream relations joined through an intermediate
candidate state. It does not introduce that label into the new public API.

## Preregistration

This section was fixed before M1-E result evaluation.

### Research question

For the synthetic S5 scenario, can a target historical-window candidate set be constructed from
public evidence available by the query cutoff, while preserving source ancestry, evidence state,
conflict, person/device separation, and multiple plausible states? How does that operation differ
from public backward traversal, and what changes when public local traversal is applied only after
candidate construction?

### Baseline methods

1. Backward traversal baseline: use the existing M1-D OperationalCausalTracer over known public
   candidate states and public transitions. Do not use a realized parent or truth transition.
2. Constraint-based reconstruction: select public evidence at the cutoff, group shared ancestry,
   evaluate condition bundles directly at the target window, and construct deterministic candidates.
3. Reconstruction plus local traversal: seed a small public candidate warp from the reconstructed
   target candidates and use the M1-D tracer only for the target-neighbourhood states.

The same scenario, seed, query, cutoff, public hazard envelope, observations, constraints, and public
candidate-state model are supplied to all applicable methods. Truth is passed only to the evaluator
after the three method results exist.

### Primary comparison

The primary comparison is candidate containment, candidate count/reduction, and public work for the
three methods over seeds 0 through 199. Addressability and Verifiability are reported separately;
neither is interpreted as likelihood or truth.

### Primary metrics

- reconstruction quality: initial candidate count, generated count, containment, false exclusion,
  reduction, unresolved count, and conflicted count;
- addressability: addressable count, deterministic identity success, and whether the truth-scored
  target state is represented;
- verifiability: independent support, independent refutation, unresolved, shared-ancestry-only,
  unavailable, conflicting verification, and ancestry overlap;
- work: backward traversal steps, reconstruction steps, local traversal steps, public relation
  records inspected, evidence records inspected, and source groups inspected.

### Failure conditions

Any of the following is a valid negative result and must not be tuned away after inspection:

- reconstruction does not reduce the candidate count;
- reconstruction excludes the truth-scored historical state;
- reconstruction inspects more relations or evidence than backward traversal;
- candidates are addressable but lack eligible independent verification;
- only shared-ancestry verification exists;
- conflicting evidence widens or leaves unresolved the candidate set;
- independent verification is unavailable by the cutoff;
- local traversal adds work without improving containment or candidate count; or
- deterministic or byte-reproducibility checks fail.

No numerical success threshold is preregistered. Results are descriptive for this synthetic model.

### Claim boundary

M1-E may report only how the three methods differ in S5. It does not establish that a Buddhist text
describes the algorithm, that anumāna and reconstruction are identical, that the twenty-four paccaya
form a complete causal basis, that Index Access or a general Causal Weave theory is proven, or that
past positions can be reconstructed in real Search and Rescue operations.

## Query semantics

The query identifies a target entity, inclusive target tick/window, query-context identifier, and
evidence-availability cutoff. The scope is explicit and deterministic. The runtime does not record a
fabricated analyst intention or a reason for choosing that scope. Evidence with AvailableAtTick later
than the cutoff is excluded before candidate construction and verification.

## Present evidence set

The query-time input is decomposed into:

- available public observations;
- available public relation constraints;
- structural source groups and source ancestry;
- target-window public candidate-state definitions;
- the public hazard envelope and its published transitions; and
- query/access context.

Private evidence, truth InteractionOccurrence, realized transitions, final truth entity state,
random draws, and truth causal parents are forbidden inputs. A missing record remains unavailable
knowledge and is not converted into an Absent claim.

## Candidate construction and representation

Each candidate keeps its query ID, target entity, target window, region/state identifier, generation
evidence identifiers, generation source groups, source ancestry, applied/unresolved/conflicting
constraints, public relation references, and a deterministic ordering. Truth status and truth IDs are
not candidate fields.

Confirmed person evidence may intersect public base candidates when its condition bundle is
consistent. Supported-but-uncertain evidence may support a candidate but cannot hard-exclude the
outer candidate set. Conflicting evidence is retained. Indeterminate, unsupported-for-query,
device-only, private, future-available, and temporally irrelevant records do not become confirmed
person evidence.

## Candidate addressing

A candidate ID is derived from the normalized query, target, window, candidate state, and public
generation basis. It does not use a realized event ID, truth parent, final state, or random identifier.
The operational Addressability measure counts candidates with such stable identities. It is not a
truth-likelihood score.

## Candidate verification

Verification uses a separately configured public evidence subset. A verification source group is
independent only when its ancestry has no overlap with the candidate's generation ancestry. Repeated
derivations from one source family remain one structural group. Shared ancestry is reported and never
counted as independent support. Verification can be independently supported, independently refuted,
conflicting, unresolved, shared-ancestry-only, or unavailable.

Generation and verification constraints are grouped within their respective subsets before ancestry
comparison. A shared verification record therefore cannot change the candidate's generation group
identifier, generation ancestry, or candidate address; only the overlap audit records the shared
lineage.

Candidate construction and candidate verification are evaluated independently. Candidate generation
does not imply verification, and verification does not retroactively change candidate identity.

## Evidence-state and paired-boundary rules

World-side relation existence is not an EvidenceState. Confirmed, supported-but-uncertain,
conflicting, indeterminate, unsupported-for-query, and unavailable remain distinct. They are not
compressed to a Boolean.

The tests evaluate each proposition and its converse separately for observation/occurrence,
person/device, evidence/claim, trace/inferred candidate, cause/consequence, and candidate
generation/verification. In particular:

- observation of X does not expose a truth occurrence object;
- occurrence of X does not imply a public observation by the cutoff;
- a device state is not a person state;
- a trace may support a candidate without being that candidate;
- a public consequence may suggest possible conditions without identifying one true cause; and
- constructing an addressable candidate does not establish independent verification.

These are boundary tests, not a newly named inference engine. In the implementation, a backward
tracing result contains region identifiers but does not create an addressed `PastStateCandidate`;
reconstruction can create candidates without a tracing session; and verification evidence remains
outside the generation basis. This separately exercises trace/inferred-candidate and
support/converse boundaries.

## Buddhist methodological comparison

The comparison below concerns limited methodological analogies after decomposition. It does not make
the modern program and Buddhist soteriological, Abhidhamma, or logical projects identical.

| Modern operation | Buddhist methodological analogue candidate | Match | Boundary |
| --- | --- | --- | --- |
| Query formation and attention scope | yoniso manasikāra | partial semantic/operational analogy | Appropriate attention in MN 2 concerns what is fit for attention in a liberative discipline; it is not a computer query planner. |
| Decomposition of evidence and claims | dhammavicaya / Vibhaṅga | partial operational analogy | Analytical discrimination and classified analysis motivate decomposition, but the M1-E types are not a Buddhist taxonomy. |
| Conditional analysis | Paṭṭhāna / paccaya | partial semantic analogy | Multiple conditions and producing/supporting/maintaining distinctions are relevant; no twenty-four-condition engine or exhaustive mapping is implemented. |
| Paired converse test | Yamaka | operational analogy | Yamaka's paired and converse questioning motivates evaluating implication directions separately; there is no Yamaka engine. |
| Multi-condition concurrence | paccaya-ghaṭanā | unresolved / correspondence not confirmed | The requested term's precise textual scope was not established by the consulted canonical material; candidate condition bundles are not named after it. |
| Forward/reverse tracing | anuloma / paṭiloma | partial operational analogy | Ordered and reverse examination is comparable at a method level; M1-D traversal remains a computer graph/constraint operation. |
| Hidden-state inference from signs | anumāna | partial semantic analogy | Evidence-to-hidden-candidate inference overlaps in function, while addressing, serialization, verification, and traversal do not become anumāna. |
| Evidence validity | pramāṇa / trairūpya | partial future-design analogy | Target case, supporting cases, and contrary cases are retained as an audit direction; a Buddhist logic or valid-cognition engine is not implemented. |
| Cause/result analysis | paṭisambhidā / Paṭisambhidāmagga conditional analysis | partial semantic analogy | Forward cause-to-result analysis is kept distinct from result-to-possible-condition inference; terminology is not transferred to fields. |

Consulted text/translation anchors include [MN 2](https://www.accesstoinsight.org/ati/tipitaka/mn/mn.002.than.html)
for fit/unfit attention, the Pali Text Society descriptions of the
[Vibhaṅga](https://palitextsociety.org/product/the-book-of-analysis/) and
[Yamaka](https://palitextsociety.org/product/book-of-pairs-3-volume-set/), the
[Paṭisambhidāmagga Pāli text](https://www.opentipitaka.org/texts/s0517m_mul?lang=en&ui=en), and
Paṭṭhāna conditional-analysis material. The precise canonical use and scope of
`paccaya-ghaṭanā` was not confirmed in the sources consulted, so M1-E records it as unresolved rather
than treating the requested expression as an established name for its condition bundle.
[Dignāga-related inference and three-form material](https://plato.stanford.edu/archives/sum2015/entries/logic-india/)
is treated as a later Buddhist logical tradition, not silently projected into early Pāli texts.

## Relation primitive hypothesis

Future research may test whether sufficiently decomposed complex causal relations can be described
naturally by one or a combination of existing paccaya. Each attempted mapping must be classified as
natural match, partial match, forced match, no match, or further decomposition needed. M1-E does not
claim that the twenty-four paccaya are a complete basis and adds no new relation taxonomy.

The semantic relation between the working Causal Nexus label and paccaya-ghaṭanā remains unresolved.
M1-E neither extends Causal Nexus as a production concept nor renames condition bundles with a
Buddhist term whose scope has not been confirmed.

## S5 synthetic scenario

S5 contains a target person, historical window with incomplete direct history, distributed current
evidence, at least three independent source families, redundant same-source derivations, uncertain
and conflicting evidence, later/downstream traces, person/device separation, private/unavailable
records, and multiple plausible candidate regions. Its topology and evidence generation are synthetic
and uncalibrated. The scenario is not adjusted after evaluation to make reconstruction outperform
traversal.

The six preregistered evidence conditions are distributed public evidence, shared-ancestry
verification, conflicting verification, delayed verification, unavailable/private verification,
and device-only evidence. The direct public history at the target window is incomplete. A later
public state supports the traversal baseline, while separate later traces and environmental records
exercise construction and verification. The constraint definitions, condition cycle, and success
and failure criteria are fixed in source before the 200-seed evaluation.

## Telemetry and evaluation boundary

Telemetry schema version 3 retains every version-2 field meaning and adds four neutral record
categories in the evaluation stream: past-state query/evidence-set membership, generated candidate,
candidate verification, and method work. Query records contain target window, cutoff, public observation/constraint/group
membership, public relation references, and the future-evidence exclusion count. Candidate and
verification records remain in the evaluation stream and contain no truth score. Method records keep
the three operations and optional M1-D tracing session reference separate.

Truth historical state, person/device outcome, containment, and false exclusion are computed only by
the evaluation builder after the public comparison exists. The telemetry collector receives the
public comparison before that evaluation call. Telemetry remains opt-in; disabling it does not change
evaluation files.

## Cross-domain Search and Rescue boundary

The query, evidence, candidate, addressing, verification, and traversal contracts use generic entity,
time, region/state, relation, constraint, ancestry, provenance, and availability fields. They do not
require a river, debris, building, mine, plant, mountain, snow, desert, flood, maritime, rail, aviation,
or event-specific core field. Cross-domain validity remains future work.

## Naming discipline and pre-result audit

Public names introduced by M1-E must be classified before commit as an existing Buddhist term, an
existing general academic/engineering term, an ordinary descriptive term, or a coined term. Buddhist
terms are used in documentation comparisons only. General terms include query, evidence set,
candidate, addressing, verification, reconstruction, traversal, source group, provenance, and
availability cutoff. Ordinary descriptive identifiers are preferred for scenario and telemetry fields.
No unconfirmed coined research term is authorized by this milestone.

### Public-facing naming audit

| Public-facing name | Classification | Decision |
| --- | --- | --- |
| `PastStateQuery`, `QueryContextId`, `EvidenceAvailabilityCutoff` | general computing / ordinary descriptive | Adopted for explicit query scope; no analyst-reason field is fabricated. |
| `PublicEvidenceSet` | general academic/computing | Adopted as a query-time collection, not an ontological bundle. |
| `PastStateCandidate`, `PastStateCandidateDisposition`, `PastStateCandidateIdentity` | general academic / ordinary descriptive | Adopted; truth status is excluded and the identity helper is reproducibly recomputable from public fields. |
| `CandidateVerification`, `CandidateVerificationDisposition` | general academic / ordinary descriptive | Adopted; independent, shared-ancestry, conflict, unresolved, and unavailable are separate. |
| `PastStateReconstructor`, `PastStateReconstructionResult` | general academic/computing | Adopted for the evidence-to-candidate operation only. |
| `PastStateMethod`, `BackwardTraversal`, `ConstraintBasedReconstruction`, `ReconstructionWithLocalTraversal` | established general graph/inference terms | Adopted as comparison labels, not a new high-level concept. |
| `PastStatePublicContext` | ordinary descriptive | Adopted as an input carrier; it is not a world entity. |
| `PastStateReconstructionDefinition`, `PastStateEvidenceCondition` | ordinary descriptive | Adopted as scenario configuration. |
| M1-E evaluation-report and telemetry-record names | ordinary descriptive | Adopted as serialization/reporting containers. |
| `Addressability`, `Verifiability` | existing general academic properties | Retained as operational measures, not promoted to a combined concept or score. |
| S5 `distributed_past_evidence` | ordinary descriptive | Adopted without a research neologism. |

No Buddhist-derived term is used as a system type or field. No coined public research term or coined
algorithm name was added. Consequently there is no new name requiring user confirmation in M1-E.
`Index Access`, `Jump`, `Causal Nexus`, `paccaya-ghaṭanā`, and the legacy causal-sandwich label remain
working, unresolved, or historical expressions only.

## Limitations and intentionally deferred work

M1-E does not implement a twenty-four-condition engine, full Causal Weave, belief/ATMS machinery,
probabilistic source independence, complete observation pipeline, holder-specific record transport,
active probing, Causal Nexus formalism, Causal Shadow metric, Good/Bad State-Space interpretation,
rescue prioritization, real topology/physics, real-world calibration, or predictive validity.

## Subsequent method-composition correction

The original M1-E result for reconstruction with local traversal was 94.5% containment with 11/200
false exclusions. Subsequent analysis found that verification-only evidence had been reused for
candidate pruning and that the S5 local sessions performed zero actual transition traversals. The
historical result is retained, but it cannot be interpreted as pure local-traversal performance.
The preregistered corrected contract, evidence-use audit, and corrected experiment are recorded in
[M1-E method-composition correction](m1e-method-composition-fix.md). Telemetry schema version 4 adds
the corresponding diagnostics without changing version-3 field meanings.
