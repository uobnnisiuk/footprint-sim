# M1-D: Operational Causal Tracing

## Purpose and research boundary

M1-D asks whether several cross-thread evidence constraints can be combined without collapsing their evidence state, temporal structure, source ancestry, alternatives, or conflicts. The implementation is a minimum operational form of 因果追い / Causal Tracing over already-known public structure. It is not a full Causal Weave graph, past reconstruction, Index Access, a belief engine, or a Causal Nexus formalism.

The adopted research baseline is `footprint-research-concept-baseline-v0.2.1.md`. The Causal Nexus note and the full Causal Weave/Index session archives informed the requirement to preserve relation and interval metadata. Their exploratory numerical results are not treated as reproduced results or acceptance thresholds.

## Traversal versus reconstruction

Operational tracing has three explicit operations:

- **Forward**: follow public transitions from an earlier candidate state to compatible later states.
- **Backward**: use the reverse view of the same public transitions to retain compatible earlier states. It never follows a realized truth parent.
- **CrossThread**: apply evidence-derived constraints at their historical exact tick or inclusive interval.

Combined tracing alternates public forward and backward consistency checks after applying cross-thread constraints. It traverses a candidate warp already supplied by a public model. It does not create a past index from a present-state bundle. `Reconstruct → Jump → Traverse`, Addressability/Verifiability experiments, and dynamic Index Access remain M1-E work.

A missing public transition is treated as missing knowledge, not proof of absence. It therefore causes no hard exclusion. This preserves `NotObserved(X) != Absent(X)`.

### AR-1: open-world published-path repair (#96)

Before #96, `OperationalCausalTracer.ApplyDirection` hard-intersected outer with the destinations reachable in the published *subset* whenever any branch was present. The same synthetic records and receipts showed false exclusion with zero received records, loss of regions compatible with Confirmed evidence, and later artificial widening. These pre-repair results are preserved as historical measurements in `docs/rescuer-receipt-boundary.md`, not as safe expected behavior.

Adopted candidate A: successful published paths may support **preferred** alternatives and are recorded in the deterministic transition audit; a missing path is unavailable knowledge, **not** negative evidence. Public path failure alone does not hard-narrow `OuterPlausible` or create a conflict in forward/backward or point/window tracing. Actually received Confirmed cross-thread evidence may still legitimately intersect outer at its applicable historical tick/window. Unknown edges are never fabricated, and even a fully declared envelope is not presumed complete.

Alternative B (hard intersect only with independently established completeness) remains on hold because `PublicHazardEnvelope` does not certify completeness. Alternative C (guessing unknown physical edges) is excluded as a new model assumption. Revisit A if it drops legitimate Confirmed exclusion, misrepresents preferred or audit lineage, or fails a fixed partial-public counterexample; a verified public-model completeness contract would also require review. This narrow repair does **not** resolve AR-6 in the separate candidate propagator, discover missing causal links, validate the 24-lens idea, or demonstrate rescue benefit.

AR-7 follow-up: preserving an outer-possible region does **not** mean every other-region observation supports that region. Candidate-level support lineage now carries received source groups and constraints only when the evidence is locally applicable or can traverse a *positive published path* to that region; E/F without a published path from A remain unknown, not g1-supported. State-level warp provenance is deliberately conservative: a propagated constraint is attached to a multi-region state only if it supports **every** listed region; the per-region support lineage and cross-thread step audit retain more specific evidence. Conflicting source groups continue to propagate on positive public paths as competing context, never as proof to delete an outer region. Lineage propagation requires additional positive-path checks, which are **not included** in the existing tracing-step work counter. A convergence-specific guard prevents an observation limited to region A at its historical time from being passed through A→D and then backwards through B→D to falsely support B. Each propagated constraint retains its own evidence time and allowed-region boundary: outside that window it can travel towards earlier or later states, but cannot reverse direction after leaving the window; within the window it cannot support a disallowed region. This is a finite-model provenance correction, not discovery of unpublished relations or a calibrated rescue claim. Source-group counts, lineage claims, and the model's processing work must not be read as calibrated rescue outcomes.

## Multiple wefts and source independence

`CausalTracingConstraint` preserves direct source, source observation, ordered source ancestry, target entity, relation kind, participants and roles, observed/constrained windows, allowed regions, available tick, evidence state, visibility, derivation provenance, and the query-relative `Upstream`, `Local`, or `Downstream` role. These roles describe a constraint's role in one tracing query; they are not a fixed relation taxonomy.

Constraints are canonically ordered before application. Source families are connected components of source-observation, direct-source, and ancestry identifiers. Multiple constraints derived from one family remain visible as separate derivations but count as one structurally independent source group. No probability, confidence, witness-reliability, or independence score is inferred.

Surviving candidates retain supporting source groups, constraint IDs, relation kinds, constrained windows, and evidence states. Conflicts retain competing source groups. Counts are structural telemetry, not truth scores.

## Evidence-state working semantics

### AR-43: tracing one interval with differently timed observations

Rev1: Exact Person Conflicting evidence also causes an exact-tick partition,
so later Confirmed evidence cannot erase a known earlier conflict. Inclusive
Conflicting evidence still cannot establish a conflict at every tick. M1-E's
interval-query traversal now takes a conservative union of outer-plausible
regions from all tracer states overlapping the query. This is a "possible at
some point in the window" candidate set, **not** a TargetEndTick prediction
or a rescue-time inference; evaluation using end-tick truth has a different
interpretation. A missing published path is never proof that a path is impossible.

A multi-tick `StateInterval` is **not** a stationary location. For public, received,
person-specific exact Confirmed observations, operational tracing now divides the
interval at the observed ticks, retains the original outer possibilities in
unobserved portions, and narrows only the corresponding exact tick. A later
confirmed location never proves the same location at an earlier tick. Two
disjoint locations at the **same** exact tick can still produce a conflict;
different ticks alone cannot.

An inclusive interval observation means *somewhere in the interval*, not
residence at every tick. In an interval that was split by independently received
exact Confirmed observations, other broad or weak witnesses are left as
non-applied audit steps rather than broadcast over every new time slice.
In split windows, an inclusive Conflicting witness retains its source
ancestry and competing-region lineage and emits a non-applied, alternatives-
retained audit for each overlapping slice. It does **not** mark a contradiction
or create a Conflicted candidate at any unverified exact tick. Exact-tick
Conflicting witnesses can still mark that tick as conflicted. Unsplit legacy intervals keep their
existing treatment; the complete semantics of overlapping inclusive evidence
remains an open limitation, not a proof of continuous residence. A positive published path may supply support, but a
missing/unpublished path does not refute a received fact. This is a conservative
finite correction, not complete temporal logic, route reconstruction, or a
general safety proof. The synthetic S4/S5 evaluation and exact-tick regression
checks remain necessary.

Issue #159 separately corrects M1-E generation and verification when evidence has
distinct or partially overlapping times. Its generation-only `[10,12]` query with
the inclusive `[12,13]={A}` witness retains `{A,B}` and does not mark the whole
window as positively supported by that witness. This does not change the Tracer
itself: enabling traversal for an unsplit inclusive StateInterval can still
produce the earlier `{A}` result. That remains an open limitation. The corrected
generation-only B/C cases, the legacy S5 common-window layer, and traversal-enabled
inclusive behavior must not be reported as one fully repaired temporal method.



### AR-82: interval conflicts in time-sliced past-state reconstruction

When confirmed Person observations at different exact ticks split a query,
an overlapping public and received Person **Conflicting** interval must not
disappear from the reconstructed candidates or their independent-verification
audit. Otherwise two point Confirmed observations can erase an interval conflict,
incorrectly narrow the outer set, and even exclude a synthetic truth region.
M1-E keeps the conflict's constraint and source ancestry visible, and restores
the public outer alternatives **within overlapping slices**. Unaffected slices
still permit legitimate exact-tick narrowing. Widening only the time window of
an otherwise identical conflicting witness must not remove an outer possibility
that its point counterpart retains.

This is an *uncertain competing witness*, not a confirmed position, an
all-ticks residence claim, or independent proof of a refutation at every time.
Generation and independently sourced verification remain separate; shared
ancestry cannot be counted as new verification. Private, future-received,
device-only, wrong-Person and out-of-window evidence remain excluded from
this Person inference. Stable candidate IDs, conflict/unresolved/source IDs,
and the distinction between outer and preferred must survive the change.

AR-82's finite P1/P3/P4/P5/P7/P8, G-series, V-series and S5 synthetic
contrasts test candidate recovery and audit separately from end-tick
performance. A reduced candidate count is not a benefit when truth is
incorrectly excluded. Tracer's separately documented inclusive-interval T1
limitation, AR-78/80's missing-own-trace gate, and AR-74/75's historical
evaluation corrections remain outside this limited M1-E fix. None of these
finite checks proves a complete temporal theory or a real rescue outcome.

**Known unresolved safety limit (AR-82 follow-up, outside this patch):**
A received public Person `Confirmed` witness spanning multiple ticks is
still omitted from time-slice conflict logic when exact-tick `Confirmed`
witnesses narrow those slices. If they disagree, outer candidates can be
incorrectly excluded (including a synthetic truth region), while the audit
misses the conflict. The C1/C1p/C2/C3 and S5 `Confirmed`-interval contrasts
are not repaired here; this limitation predates this PR and needs a separately
reviewed scope. Passing the `Conflicting`-interval checks does **not** establish
outer safety for all interval evidence. Do not cite affected runs'
`CandidateReduction` as a validated search benefit.

- `Confirmed + Confirmed`: narrow when consistent; preserve candidates and mark conflict on empty intersection.
- `Confirmed + SupportedButUncertain`: confirmed evidence may bound the outer set; uncertain evidence may affect preferred candidates without hard outer exclusion.
- `SupportedButUncertain + SupportedButUncertain`: retain alternatives; no hard exclusion.
- `Conflicting`: mark competing source lineage and retain candidates. There is no majority vote or last-writer-wins rule.
- `Indeterminate` and `UnsupportedForQuery`: no-op with audit.
- unavailable or private evidence: excluded from the public search context.
- device-only evidence: no-op for person hard narrowing.

`Preferred`, `OuterPlausible`, and `Conflicted` remain distinct. Leaving `Preferred` does not imply impossibility while the candidate remains in `OuterPlausible`.

## Causal sandwich

The limited causal-sandwich fixture applies query-relative upstream, local, and downstream constraints around an intermediate candidate state:

```text
public upstream relation -> candidate X <- public downstream relation
                              ^
                              |
                       local cross-thread evidence
```

The operation only constrains states on a known public candidate warp. It is not a general reconstruction algorithm. Audit records preserve each role and every forward/backward/cross-thread step.

## S4 synthetic scenario

`scenarios/s4_multi_weft_tracing/scenario.json` is a synthetic, uncalibrated scenario with Person A, independent Person B/C evidence, a device-only source, a downstream public environmental source, branching passive transport, person/device separation, and a secondary route. A target-specific searchable set prevents auxiliary people and their regions from inflating the denominator.

Its ten seed-cycled conditions are two independent historical wefts, two derivations from one source family, confirmed plus uncertain, independent conflict, upstream plus local, local plus downstream, causal sandwich, delayed, unavailable/private, and device-only. One simulation run is generated per scenario/seed. `own-evidence-constrained`, `single-weft`, and `multi-weft-tracing` consume the same truth; only their public constraint ablation differs.

## Evaluation and paired output

Existing evaluation schema version 3 and its field meanings remain unchanged. S4 additionally emits:

- `causal-tracing.json`: per-run structural tracing measures and `multi-weft-tracing - single-weft` paired deltas, overall and by condition;
- `causal-tracing.csv`: stable, invariant-culture per-run tracing table.

The tracing report has schema version 1. It records applied constraint/source-group counts, supporting and conflicting source groups, total/forward/backward steps, retained alternatives, order-invariance declaration, and causal-sandwich use alongside existing preferred/outer containment, count, reduction, and persistence values. Candidate reduction is never interpreted without containment and false-exclusion results. Source-group counts are structural counts, not confidence scores.

## Telemetry

Telemetry schema version 2 adds `evidence/tracing-constraints.jsonl` and `evaluation/causal-tracing-steps.jsonl` without changing version-1 field meanings. They preserve tracing session, direction, query-relative role, exact/interval window, relation, source group and ancestry, applied/skipped constraints, candidate input/output, support/conflict lineage, disposition, alternatives, and provenance. Truth occurrence and realized paths remain in the private truth stream.

More telemetry increases storage volume. M1-D intentionally preserves information rather than optimizing it; relation-resolution retention versus storage cost remains future research.

## Determinism and order invariance

Constraint input/JSON/enumeration order is normalized before grouping and application. Safe cases use set semantics, and output/audit/source/provenance collections use deterministic ordinal or temporal ordering. IDs derive from stable inputs; no current time, random GUID, absolute path, or thread scheduling enters reproducible output.

## Cross-domain SAR scope

The tracing core uses entity, temporal state, relation, candidate warp, constraint, evidence boundary, provenance, source group, and direction. It does not require river, debris, building, mine, mountain, plant, or maritime fields. S4 details stay in the scenario/public model. Future cross-domain validation should cover urban/building, construction, mine/underground, industrial plant, mountain/wilderness, desert, maritime, and compound-disaster settings.

## Limitations and next work

M1-D results describe only this synthetic model. They do not establish real SAR performance, BLE accuracy, location prediction, the 24-lens hypothesis, Index Access, general Causal Weave theory, or Causal Nexus theory.

- **M1-E**: present-state bundle, reconstruction, dynamic past index, `Reconstruct → Jump → Traverse`, Addressability, Verifiability, Addressing Basis, Robust Constraint Spectrum, discriminative power, and re-encoding/persistence experiments.
- **Causal Nexus research**: formal Nexus boundaries, overlap/continuity/interruption/persistence, relation compression and resolution, tracing accuracy, and storage-cost trade-offs.
- **Causal Shadow / 吉凶空間論**: analyze neutral records later without embedding a hidden-cause, Good/Bad, utility, or rescue-priority score now.
- **Later operational work**: calibrated observations, holder-specific distributed record flow, active probing, real topology/physics, rescue action and prioritization.

Future experiments should preregister success, failure, comparison, and claim boundaries where practicable. No numerical threshold is fixed by M1-D.
