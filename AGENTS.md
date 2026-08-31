# AGENTS.md

## Repository intent

This is a private research simulator, not an operational rescue or hazard-prediction system. Do not push, publish, create releases, or add a license unless the owner explicitly requests it.

## Working rules

- Target C# / `net8.0`; respect `global.json`.
- Keep ground truth (`SimulationEvent`, final entity state) out of inference inputs and observation JSON.
- Preserve deterministic ordering and output for identical scenario bytes and seed.
- Pass truth to `PerRunEvaluator` only after `ISearchStrategy` has returned; never add truth types to strategy input.
- Keep evaluation JSON/CSV free of current time, absolute paths, random IDs, and unordered collections.
- Treat `Person` and `Device` as independent physical entities after separation.
- Strategies may consume `PublicHazardEnvelope`; never pass a realized transition, terminal, or PRNG draw through it.
- Keep SplitMix64 test vectors and entity/purpose-derived random streams stable.
- Keep truth-side `InteractionOccurrence` out of observation, constraint, inference, and strategy contracts; an interaction observation or derived constraint is not an occurrence.
- Apply cross-thread constraints to matching historical candidate-warp ticks/windows, then propagate with the public model; never intersect an encounter region directly with final candidates.
- Preserve exact-tick versus interval semantics, relation/source-observation metadata, uncertain outer alternatives, and conflicts in CandidateWarp audits.
- Keep S3 comparisons paired: generate truth once per scenario/seed, and allow only `cross-thread-constrained` to consume weft constraints.
- Treat `candidateRegionIds` as the outer-safe schema-v2-compatible set; report preferred candidates separately and never count leaving preferred as hard exclusion while outer plausibility remains.
- Keep neutral telemetry opt-in and split private truth, public/evidence, and evaluation-transition records; telemetry-off evaluation files must remain byte-identical.
- Do not add interpretation scores to telemetry, and preserve exact-point/interval, relation, role, ancestry, access, and candidate before/after metadata with deterministic ordering.
- Keep Operational Causal Tracing on known public structure: canonicalize multiple constraints, group shared source ancestry, preserve support/conflict alternatives, and never use realized parents for backward tracing.
- Treat missing public transitions as unavailable knowledge rather than absence; keep known-relation traversal distinct from candidate reconstruction.
- For past-state reconstruction, keep query formation, public evidence selection, candidate construction, deterministic addressing, independent verification, and optional local traversal separate; exclude future/private evidence and generation ancestry from independent verification.
- Keep scenario changes explicit and testable; do not imply real-world predictive accuracy.
- Do not commit generated `results/`, `bin/`, `obj/`, or SDK artifacts.

## Before handing off

Run `scripts/check-environment.sh`, `dotnet build FootprintSim.sln`, `dotnet test FootprintSim.sln`, `dotnet format --verify-no-changes`, all sample scenarios, and the relevant README evaluation command. Review `git diff` for truth leakage and unrelated changes.
