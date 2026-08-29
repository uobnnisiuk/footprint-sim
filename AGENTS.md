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
- Keep scenario changes explicit and testable; do not imply real-world predictive accuracy.
- Do not commit generated `results/`, `bin/`, `obj/`, or SDK artifacts.

## Before handing off

Run `scripts/check-environment.sh`, `dotnet build FootprintSim.sln`, `dotnet test FootprintSim.sln`, `dotnet format --verify-no-changes`, all sample scenarios, and the relevant README evaluation command. Review `git diff` for truth leakage and unrelated changes.
