# AGENTS.md

## Repository intent

This is a private research simulator, not an operational rescue or hazard-prediction system. Do not push, publish, create releases, or add a license unless the owner explicitly requests it.

## Working rules

- Target C# / `net8.0`; respect `global.json`.
- Keep ground truth (`SimulationEvent`, final entity state) out of inference inputs and observation JSON.
- Preserve deterministic ordering and output for identical scenario bytes and seed.
- Treat `Person` and `Device` as independent physical entities after separation.
- Keep scenario changes explicit and testable; do not imply real-world predictive accuracy.
- Do not commit generated `results/`, `bin/`, `obj/`, or SDK artifacts.

## Before handing off

Run `scripts/check-environment.sh`, `dotnet build FootprintSim.sln`, `dotnet test FootprintSim.sln`, and both sample scenarios from the README. Review `git diff` for truth leakage and unrelated changes.
