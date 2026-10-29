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

## Multiple wefts and source independence

`CausalTracingConstraint` preserves direct source, source observation, ordered source ancestry, target entity, relation kind, participants and roles, observed/constrained windows, allowed regions, available tick, evidence state, visibility, derivation provenance, and the query-relative `Upstream`, `Local`, or `Downstream` role. These roles describe a constraint's role in one tracing query; they are not a fixed relation taxonomy.

Constraints are canonically ordered before application. Source families are connected components of source-observation, direct-source, and ancestry identifiers. Multiple constraints derived from one family remain visible as separate derivations but count as one structurally independent source group. No probability, confidence, witness-reliability, or independence score is inferred.

Surviving candidates retain supporting source groups, constraint IDs, relation kinds, constrained windows, and evidence states. Conflicts retain competing source groups. Counts are structural telemetry, not truth scores.

## Evidence-state working semantics

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
