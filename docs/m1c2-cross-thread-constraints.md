# M1-C2 cross-thread constraint application

## Purpose and research boundary

M1-C2 applies an available, public `CrossThreadConstraint` to a candidate person's historical warp state and then derives later candidates with the public hazard model. It establishes constraint-application semantics; it does not claim that cross-thread evidence improves search accuracy. The formal S3 comparison belongs to M1-C3.

The adopted research baseline is `footprint-research-concept-baseline-v0.2.1.md`. Design also considered `causal-weave-session-note-nexus-resolution.md`, whose Causal Nexus and Causal Nexus Resolution ideas remain hypotheses and deferred research. M1-C2 does not define a Nexus schema or resolution score.

## Relation resolution, not event density

The Nexus session note proposes:

```text
Event Density != Relation Resolution
```

More event points do not necessarily preserve more causal structure. Temporal order, exact time versus interval, relation kind, participants, evidence state, and provenance may matter even when candidate regions are identical. M1-C2 therefore keeps this metadata through application and audit instead of reducing an interaction to a region filter.

This is a preservation rule for the current milestone, not a fixed Causal Nexus formalism.

## Public-only CandidateWarp

`PublicCandidateWarpFactory` constructs `CandidateWarpSet` from explicit public candidate-state definitions. Its public API cannot receive `SimulationRun`, `SimulationEvent`, or truth-side `InteractionOccurrence`.

The small model contains:

- `CandidateWarpSet`: deterministic alternatives for one target entity;
- `CandidateWarp`: ordered temporal states and `Preferred`, `OuterPlausible`, or `Conflicted` standing;
- `CandidateWarpState`: inclusive time window, `EventPoint` or `StateInterval` semantics, ordered regions, evidence state, and ordered provenance; and
- `CandidateWarpProvenance`: public-model/evidence source, region set, availability, person/device classification, optional relation/observation/window metadata, participants, and underlying interaction provenance.

It is not a Causal Weave graph, state machine, belief network, or Causal Nexus production type.

## Historical tick and interval semantics

Constraint windows and candidate state windows are inclusive.

- An exact tick has equal start and end ticks and is recorded as `ExactTick` in the application audit.
- A multi-tick constraint is recorded as `InclusiveInterval`.
- A state interval is split at the overlap boundary so only its overlapping historical portion is narrowed.
- No temporal overlap produces `NoOp`; absence of a represented state is not an absence claim.
- `EventPoint` and `StateInterval` remain distinct even when both refer to the same region.

M1-C2 does not infer duration, continuity, interruption, overlap between arbitrary relations, or transition modes. It only avoids destroying the temporal structure already supplied.

## Constraint application flow

M1-C2 explicitly avoids intersecting a historical encounter region with final regions.

```text
available public cross-thread constraint at historical tick/window
        ↓
intersect the matching historical CandidateWarpState
        ↓
retain or branch alternatives according to evidence state
        ↓
discard stale future states from that branch
        ↓
propagate from the constrained historical state with PublicHazardEnvelope
        ↓
derive the candidate regions at the evaluation tick
```

`PublicCandidateWarpPropagator` traverses only published `PublicHazardBranch` values. It has no selected transition, realized terminal, ground-truth trajectory, or random draw. When no public branch applies, the public candidate remains at its source region rather than being removed.

## Evidence-state semantics

The application result uses machine-readable dispositions:

- `Narrow`
- `RetainAlternative`
- `Branch`
- `MarkConflict`
- `NoOp`

Initial rules are:

| Evidence state | M1-C2 behavior |
|---|---|
| `Confirmed` | Narrow the overlapping historical state. An empty region intersection retains the candidate as `Conflicted` with `MarkConflict`; it never deletes all candidates. |
| `SupportedButUncertain` | Create a constrained `Preferred` branch and retain a publicly propagated `OuterPlausible` alternative. An empty intersection retains the outer candidate. |
| `Conflicting` | Keep the candidate, mark it `Conflicted`, and record `MarkConflict`; no hard exclusion. |
| `Indeterminate` | `NoOp`. |
| `UnsupportedForQuery` | `NoOp`. |
| unavailable/private | Excluded from the public search context before application; no application audit is emitted. |

These rules are a deliberately small working semantics, not probabilistic belief updating or ATMS.

## Preferred and outer plausible candidates

`Preferred` means selected by the currently applicable evidence rule. `OuterPlausible` means retained because uncertain evidence cannot prove the alternative impossible. Leaving the preferred set is not an exclusion from the outer plausible set.

`Conflicted` preserves a candidate and its unresolved contradiction. No M1-C2 standing predicts truth or survival.

## Knowledge and access boundary

M1-C2 uses a small `CandidateWarpAccessContext` with scenario, seed, target, evaluation tick, and query-context ID. It enforces:

- `AvailableAtTick <= CurrentTick` inclusively;
- public evidence visibility; and
- query target/scenario/seed identity.

It does not model holder-specific relay, reveal, store-carry-forward, device failure, or local knowledge. Those remain M2 work. Public availability is not a claim that every rescuer holds the record.

## Same-source evidence and absence

Constraints are ordered deterministically and grouped by `SourceObservationId`. Only one constraint from a source observation is applied in the current single-derivation model; an additional same-source item is audited as `NoOp` with `DuplicateSourceObservation`. This prevents relation count from becoming independent-evidence count.

A missing, private, unavailable, temporally non-overlapping, indeterminate, or unsupported constraint does not create an absence claim or hard exclusion:

```text
NotObserved(X) != Absent(X)
```

## Person/device boundary

M1-C1's deriver does not create a person co-location constraint from device-only evidence. The M1-C2 applicator also checks the evidence classification before hard narrowing. A device-only source cannot promote a person region to confirmed or remove other person candidates.

## Provenance and application audit

The constrained historical state retains `CrossThreadObservation` provenance with:

- constraint and source-observation identity;
- source evidence identity;
- relation kind;
- source participants and roles;
- source observed window;
- allowed regions;
- evidence state and availability; and
- ordered observation/derivation provenance.

Forward-derived states add `PublicHazardEnvelope` provenance for each used public branch.

Each application audit retains:

- deterministic audit ID and candidate-warp ID;
- constraint ID and source-observation ID;
- relation kind and target entity;
- application tick and query context;
- constrained and source-observed windows;
- exact-tick or inclusive-interval classification;
- participants/roles and evidence state;
- disposition and reason;
- candidate counts before and after;
- alternative-retained and conflict flags;
- source ancestry; and
- ordered provenance.

The audit is in-memory research output in M1-C2. Evaluation schema version 2 and the five existing batch files are unchanged. A general M1-T0 serializer is not added.

## Determinism

Candidate regions, provenance, states, warps, constraints, retained alternatives, and audits use explicit ordinal ordering. IDs use the existing length-prefixed SHA-256 stable-ID helper. Current time, random GUIDs, absolute paths, PRNG state, and thread scheduling are not inputs.

## Causal Nexus non-goals

M1-C2 deliberately does not implement:

- a `CausalNexus` production schema or graph;
- Causal Nexus Resolution or event-density metrics;
- Nexus start/end rules;
- relation or transition-mode taxonomy;
- delay, duration, continuity, overlap, persistence, or interruption inference;
- Nexus compression or resolution/retention trade-off experiments; or
- 24-lens mapping.

The preserved windows, temporal kind, relation kind, participants, provenance, source identity, and application time are intended to keep these questions open rather than settle them by information loss.

## Deferred milestones

- **M1-C3:** formal S3 and paired strategy comparison. CandidateWarp application semantics should be adapted to a comparison strategy without changing the truth boundary.
- **M1-T0:** stable serialization of application audits, source ancestry, candidate before/after, retained alternatives, relation metadata, access context, and neutral state-transition telemetry.
- **M1-D:** multiple wefts, interaction between conflicting constraints, forward/backward Causal Tracing, and richer candidate-warp reconstruction.
- **Future Causal Nexus research:** formal Nexus boundaries, relation-resolution dimensions, resolution measurement, compression, and reconstruction-versus-retention experiments.

The synthetic fixtures do not establish real encounter accuracy, disaster dynamics, location, survival, rescue performance, or Causal Nexus efficacy.
