# M1-T0: Neutral causal telemetry

## Purpose and research boundary

M1-T0 records the causal, evidence, candidate, and constraint-application structure already produced by M1-B through M1-C3. Its purpose is to permit later re-analysis without assigning a research interpretation at collection time. The telemetry contains no Good/Bad, fortune, Causal Shadow, Indexability, Causal Nexus Resolution, survival, or rescue-priority score.

The adopted research baseline is `footprint-research-concept-baseline-v0.2.1.md`. The design also checked the Causal Nexus session note, the full Causal Weave archive, the full-v2 Causal Index synthesis, and the research-material inventory. Their HYPOTHESIS, DEFERRED, and UNRESOLVED concepts remain future research rather than telemetry semantics.

This is a synthetic research simulator. Telemetry records its model execution; it does not validate real disaster probabilities, locations, survival, or rescue performance.

## Schema and stream boundary

Telemetry has an independent `telemetrySchemaVersion`; M1-T0 introduced version `1`, and M1-D writes version `2` with additional tracing-constraint and tracing-step categories. Version-1 field meanings are unchanged. Telemetry versioning does not change evaluation schema version 3; field meanings are never silently repurposed.

Three streams are physically separated under `telemetry/`:

| Stream | Files | Boundary |
| --- | --- | --- |
| private truth/world | `truth/world-events.jsonl`, `truth/interactions.jsonl` | realized world events and truth-side interaction occurrences; never a public strategy input |
| public/evidence | `evidence/observations.jsonl`, `evidence/interaction-observations.jsonl`, `evidence/constraints.jsonl` | observations and evidence-derived constraints only |
| evaluation/search transition | `evaluation/constraint-applications.jsonl`, `evaluation/candidate-transitions.jsonl` | how evidence changed public candidates; contains no truth occurrence object or realized path |

`telemetry-manifest.json` records schema version, source commit, dirty flag, scenario content hashes, seeds, strategies, evaluation run count, record counts, raw constraint count, unique source-observation count, ordering contract, and newline policy. It contains no wall-clock time or absolute path.

All primary records are one valid JSON object per line. Files are UTF-8 without BOM with LF newlines. Records and nested collections use stable scenario, seed, strategy, time, identifier, region, entity, ancestry, and provenance ordering. IDs are existing deterministic simulation/evidence IDs or deterministically derived IDs; random GUIDs are not output.

## Recorded categories

### World events

A world record preserves scenario, seed, stable event ID, sequence, generic event and optional movement kind, exact occurrence tick, involved entity IDs/kinds/roles, region set, causal reference, private-truth visibility, provenance, and the existing scenario payload. Domain-specific information may remain in that optional payload; it is not a required core-schema field.

### Observations

An ordinary observation records the observed entity, evidence kind, observed tick, available-at tick, public region, evidence state, person/device-only flags, evidence visibility, source ancestry, provenance, and the existing public payload. It does not copy hidden truth event fields.

An interaction observation additionally preserves observed relation kind, participant roles, point or inclusive observed window, and an optional opaque referenced-interaction ID supplied by evidence. That ID is not a link or capability for retrieving a truth occurrence.

### Interactions

Truth interactions are restricted to the truth stream. They retain the stable interaction ID, relation kind, participants and roles, occurrence point/interval, regions, authoritative source-event ID, causal references, and occurrence provenance. Public consumers receive only the separate interaction-observation representation.

### Constraints

An evidence-side constraint preserves its stable ID, source observation/evidence IDs, target entity, source relation and participants, source-observed window, constrained window, allowed regions, available tick, evidence and person/device boundary, derivation kind, visibility, source ancestry, and provenance. A constraint is an evidence-derived claim, not truth.

### Constraint applications

Each M1-C2/C3 audit becomes an evaluation record containing strategy, candidate warp, constraint and source observation, relation kind, target, application tick, query/access context, observed and constrained windows, participant roles, evidence state, disposition, reason, before/after counts, retained-alternative and conflict flags, ancestry, and provenance. This retains `Narrow`, `Branch`, `MarkConflict`, and `NoOp` as observed neutral dispositions; it does not assign belief scores.

### Candidate transitions

For every strategy evaluation tick, a candidate transition records the preceding and current candidate-set IDs and regions, preferred regions, outer-plausible regions, conflicted regions, evidence state, trigger references, query/access contexts, ancestry, and region-level provenance. The first record has no preceding set. This is a temporal record of candidate-set change, not a claim that preferred candidates are the only possible truth.

## Event, interval, and relation resolution

`ExactEventPoint` and `InclusiveStateInterval` remain distinct temporal kinds. Existing point events are not promoted to intervals, and existing interaction windows are not collapsed to a single tick. M1-T0 does not infer duration, overlap, continuity, interruption, persistence, or transition mode.

Relation kind, participant role, relation/observation identity, time window, and provenance remain machine readable rather than being reduced to region IDs. This preserves input for possible future relation-resolution and Causal Nexus research without defining a Causal Nexus schema or score. Event density is not used as a substitute for relation resolution.

## Provenance, ancestry, and evidence independence

Evidence provenance and source ancestry are kept separately where the current runtime supplies them. Raw constraint count and unique source-observation count are also separate because relation count is not independent-evidence count. No correlation probability or witness-independence score is inferred.

The current runtime does not provide a complete observation pipeline. M1-T0 therefore does not invent sensor, witness, transformation, relay, storage, aggregation, selection, retrieval, or missingness stages. Future schema versions may add those stages, holder-specific knowledge, transport lineage, or observer health when real data exists.

Discovery provenance is also distinct from evidence provenance. Future extensions may record lens, query, analyst/agent, derivation, source evidence, discovered Aspect, and discovery time/version. M1-T0 does not fabricate those fields or implement 24-lens discovery.

For re-encoding and persistence research, existing parent references, interaction/relation identities, source observations, ancestry, and application triggers are retained. Generation/hop and parent-trace lineage are not generated when the model cannot establish them.

## Evidence access and absence boundary

The schema keeps evidence visibility, `AvailableAtTick`, and evaluation query/access context distinct. Thus evidence existence, public visibility, availability, and query usability are not treated as equivalent. Holder/rescuer-specific acquisition, relay, store-carry-forward, and device failure remain M2 work.

`NotObserved(X) != Absent(X)`. M1-T0 has no synthetic `Absent` field and does not turn a missing observation, unavailable evidence, or `UnsupportedForQuery` into an absence claim. A future absence claim would need explicit observer, scope, interval, detection capability, and observer-health support.

## CLI

Telemetry is opt-in and is added beneath the normal evaluation output directory:

```bash
dotnet run --project src/FootprintSim.Cli -- evaluate \
  --scenario scenarios/s3_cross_thread_encounter/scenario.json \
  --seeds 0..199 \
  --strategies all \
  --output results/m1t0-s3 \
  --telemetry
```

Without `--telemetry`, the existing seven evaluation files are byte-for-byte unchanged and no `telemetry/` directory is created. S0 through S3 are supported; categories absent from a scenario produce zero-line JSONL files and an explicit zero count in the manifest.

## Search and Rescue domain generality

The core contract uses generic entity, relation, event, region, temporal, visibility, ancestry, and provenance concepts. It does not require debris, river, building, mountain, mine, sea, or plant fields. Scenario payloads may retain domain terms without changing this contract.

The long-term scope is Search & Rescue reconstruction under incomplete observation across voluntary movement, passive transport or drift, fall/slide, burial/trapping/confinement, route constraints, environmental branches, secondary transport, encounters, accompaniment/separation, person/device or person/object separation, machine interaction, and distributed observations/records. M1-T0 does not implement new domain physics.

Cross-domain SAR validation is a future milestone. It should test whether the shared warp/weft/evidence-boundary/candidate-reconstruction/guidance structure survives materially different domains, including mountain or wilderness, mine or underground, industrial plant, construction site, desert, and maritime cases.

## Research use and future work

Telemetry-based studies should define their success condition, failure condition, comparison condition, and claim boundary as far as practical before evaluating results. M1-T0 fixes no numerical threshold.

- M1-D: multiple wefts, independent-source conflict, forward/backward Causal Tracing, and multi-relation provenance.
- M1-E: present-state bundles, dynamic past indices, Reconstruct/Jump/Traverse, Addressability, Verifiability, discriminative power, and re-encoding/persistence experiments.
- Causal Nexus research: formal nexus boundaries, relation and temporal resolution, overlap/continuity/interruption, compression, and storage-versus-resolution tradeoffs.
- Causal Shadow research: alternative definitions, non-identifiability, knowledge/model context, and active probing. No shadow label or score is emitted here.
- 吉凶空間論: later analysis of neutral state-space change under an explicitly chosen semantics. No Good/Bad label, utility function, or score is emitted here.

M1-T0 also does not implement Index Access, a 24-lens engine, belief/ATMS semantics, probabilistic independence, holder networks, BLE/GIS/DEM, rescue policy, active probing, visualization, GUI, or parallel execution.
