# M1-C1 causal interaction model

## Purpose and research boundary

M1-C1 introduces an explicit representation of causal weft: an interaction that crosses entity timelines. M1-B made one entity's warp probabilistically branch while separating the private realized path from the public hazard envelope. M1-C1 does not narrow those candidate warps. It only fixes the representation boundary needed before M1-C2 can test whether cross-entity evidence safely constrains them.

The first formally supported relation is `PersonPersonEncounter`. This is deliberately not an arbitrary relation taxonomy, a 24 Conditions mapping, a full Causal Weave graph engine, or an EBPC-conformance layer.

## Three distinct stages

```text
private world event
    -> InteractionOccurrence

independently reported evidence
    -> InteractionObservation
    -> CrossThreadConstraint
```

The arrows describe representation and derivation, not equality.

- `InteractionOccurrence` is what actually happened in simulation truth.
- `InteractionObservation` is what an evidence source reports about an interaction. It may be delayed, conflicting, unsupported, device-only, or lack a referenced interaction ID.
- `CrossThreadConstraint` is a claim derived only from an observation. It is not truth and cannot resolve a private occurrence.

No M1-C1 type is added to `SearchStrategyInput`, and no built-in strategy consumes a cross-thread constraint. Existing candidate regions, evaluation schema version 2, and M0/M1-A/M1-B output files therefore retain their meaning and bytes.

## InteractionOccurrence

`InteractionOccurrence` lives in `FootprintSim.Interactions.Truth`. A person-person occurrence is projected from an existing `SimulationEvent` whose kind is `Encounter`; it is not stored as a second mutable world state.

It retains:

- stable interaction ID;
- scenario and seed identity;
- `PersonPersonEncounter` relation kind;
- two known person participants and their `EncounterParticipant` roles;
- inclusive occurrence time window;
- ordered region IDs;
- source world-event ID and the existing causal-parent event ID;
- ordered machine-readable provenance;
- `PrivateTruth` visibility.

The existing `SimulationEvent` remains the authoritative world log. The occurrence is a typed semantic projection for cross-thread research.

## InteractionObservation

`InteractionObservation` is an evidence-side type in `FootprintSim.Interactions`. It has no `InteractionOccurrence` property and can be constructed without a truth object. Its optional `ReferencedInteractionId` is only an opaque identifier reported by evidence; no public occurrence catalog or reverse resolver exists.

It retains:

- stable observation ID derived from explicit input identity;
- observed relation kind, participants, roles, time window, and region set;
- `AvailableAtTick`, separate from the observed time;
- the existing `EvidenceState` values;
- explicit person-evidence versus device-only classification;
- public or private evidence visibility;
- ordered machine-readable provenance.

`InteractionObservationSet.PubliclyAvailableAt(tick)` returns only public observations whose `AvailableAtTick` is less than or equal to the requested tick. The boundary is inclusive: evidence available at tick 23 is absent at tick 22 and present at tick 23.

## CrossThreadConstraint

`CrossThreadConstraintDeriver` currently supports one derivation: a person named by a person-person encounter observation may be co-located within the observation's reported region set and observed time window.

The resulting `CrossThreadConstraint` retains:

- stable constraint ID;
- source observation ID;
- target entity ID;
- source relation and derivation kind;
- constrained time window and ordered allowed-region set;
- `AvailableAtTick` copied from the observation;
- unchanged evidence-boundary state;
- observation, evidence-source, and derivation-rule provenance.

The deriver accepts only `InteractionObservation`, never `InteractionOccurrence`. Device-only evidence cannot derive a person co-location constraint, even when the device observation itself is `Confirmed`. Conflicting, indeterminate, and unsupported states are preserved rather than promoted. M1-C1 does not decide how any state changes a candidate set.

`CrossThreadConstraintSet.PubliclyAvailableAt(tick)` provides the same inclusive availability gate as the observation set. The set is not connected to a search strategy in this milestone.

## Participant roles and ordering

Every participant has an entity ID, entity kind, and machine-readable role. Both participants in the initial symmetric encounter use `EncounterParticipant`. A small enum leaves room for later asymmetric relations without defining those relations prematurely.

Participants are ordered by role, entity kind, and ordinal entity ID. Regions are ordinally sorted and deduplicated. Provenance is ordered by provenance kind and source identifier. Observation and constraint sets are ordered by availability, constrained/observed start tick, and stable ID.

## Deterministic identifiers

Interaction, interaction-observation, and cross-thread-constraint IDs use project-owned SHA-256 over length-prefixed UTF-8 components and retain the first eight digest bytes as lowercase hexadecimal, matching the existing stable-ID shape. Inputs include explicit scenario/seed/source/time/relation/participant/region identities as appropriate. Current time, random GUIDs, absolute paths, and thread scheduling are not inputs.

Existing event and observation ID algorithms are not modified, so S0/S1 byte baselines and S2 deterministic behavior remain unchanged.

## Evidence and device boundary

M1-C1 reuses `Confirmed`, `SupportedButUncertain`, `Conflicting`, `Indeterminate`, and `UnsupportedForQuery`. Availability is represented separately by `AvailableAtTick`; unavailable-before-tick is not encoded as measured zero or as a stronger evidence state.

This is an EBPC-compatible boundary rule, not a claim of full EBPC conformance. In particular:

- occurrence is not observation;
- observation is not truth;
- derived constraint is not truth;
- device-only evidence is not person evidence;
- an opaque referenced interaction ID is not a truth lookup capability.

## Deferred work

M1-C2 should decide how eligible cross-thread constraints affect candidate warp histories, including treatment of conflicting and supported-but-uncertain constraints, provenance integrity, and availability gating inside a strategy-facing contract.

M1-C3 should add a formal S3 and paired comparison of last-confirmed, public-hazard, own-evidence, and cross-entity-evidence strategies. S0, S1, and S2 should remain regression baselines.

M1-T0 should add neutral telemetry such as state snapshots and transition observations using stable interaction IDs, participant roles, ticks, regions, causal references, and provenance. M1-C1 does not add telemetry serialization, Good/Bad labels, or a causal-shadow metric.

Observation-channel calibration, missed/noisy encounter generation, and complete EBPC integration remain M2 topics. The current model and fixtures do not measure real-world encounter accuracy, hazard behavior, location, survival, or rescue performance.
