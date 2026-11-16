# Footprint Sim

[日本語](README.md) | **English**

**When a person's trail goes cold, how far can the available evidence safely narrow down where to look?**

Footprint Sim is a research simulator exploring this question through **mathematical analysis, reproducible simulation, and validation of real-world assumptions**. Using missing-person searches during disasters as a motivating problem, it investigates the conditions under which evidence from encounters with other people, device observations, and environmental changes can help narrow possible locations even when the person's own history is incomplete.

**This is not an operational search-and-rescue system.** It is a research platform based on synthetic data. It does not predict or guarantee a person's real-world location, exposure to danger, likelihood of survival, or rescue outcomes. It also does not replace the separate `footprint` project that works with physical devices.

## The problem

Knowing a person's last confirmed location does not necessarily reveal where they are now. Finding their phone does not resolve the uncertainty either: if the person and device became separated, the device's location must not be treated as the person's location.

Other evidence can sometimes constrain the missing part of that history. An encounter reported by another person, or an observation received later, may rule out some possible histories.

Footprint uses the weaving metaphor of **warp and weft**:

- **Warp**: the person's own trajectory over time.
- **Weft**: evidence relating that trajectory to other people, devices, and the environment.

The research is not simply about producing a smaller list of candidate locations. It asks four distinct questions:

- **What evidence was available?** Inference uses only evidence received and available by the query cutoff. It cannot use future observations or ground truth known only to the simulator.
- **What can the evidence distinguish?** If multiple histories are consistent with the same received evidence and public model, a method must not choose one without justification.
- **What can be safely excluded?** Preferred places to check and the broader set of still-plausible locations must remain distinct. Unknown, unresolved, or conflicting information does not mean there are no candidates.
- **Does a method make a meaningful difference?** Methods are compared on the same available information, with false exclusion, candidate reduction, observation delay, and computational work assessed separately.

```text
Synthetic world (actual movement and events)
    |
    +-- Observed and received evidence --> Public model and constraints
    |                                          |
    |                                          v
    |                                    Inference method --> Candidate locations
    |                                                               |
    +-- Ground truth (never passed to inference) ----------------> Evaluation
```

## What has been studied, and what remains uncertain

| Area | Current research result | Important limitation |
| --- | --- | --- |
| **Synthetic observations and movement** | Reproducible scenarios model walking, hazard-driven transport, person/device separation, encounters, and observation delays. | Their synthetic conditions are not calibrated representations of actual disasters. |
| **Historical reasoning with weft evidence** | Implemented and compared ways to apply received constraints to historical candidates, trace public relations, reconstruct past-state candidates, and check evidence provenance. | This is not a complete system for discovering unknown relations or reconstructing an entire causal network. |
| **Mapping evidence to places to check** | Limited models make the distinction between candidates, regions, and physical checking units explicit, including unmapped or uncertain alternatives. | Candidate counts do not directly establish real search time or cost. |
| **Reachability within a finite model** | F21 compared 1,944 queries over a small, declared closed model against an independent reference computation. | This does not prove that a real-world movement network is complete. |
| **Mathematical information value** | A research note examines indistinguishable worlds, conditional safe reductions, and complementary evidence. | Its arguments are conditional; they do not prove superiority of the current implementation or real-world effectiveness. |
| **Comparison with walking records** | A bounded research track examines evaluation design and the assumptions required for external walking data. | Real-disaster safety, search efficiency, and effects on survival remain unverified. |

**Evidence may become useful only when combined.** A mathematical example considers four possible histories mapped to two checking units, `{U1, U2}`. Evidence `e1` alone does not reduce the units; neither does `e2`. When both are justified as simultaneously applicable constraints on the same history, the set becomes `{U1}`. This is a **hand-worked theoretical example**, not evidence that the current implementation or a field deployment has achieved that improvement.

For the reasoning and boundaries, see the [mathematical research note](docs/weft-mathematical-foundations.md), [F21 finite-model results](docs/m1f21-public-transition-coverage-results.md), and [walking-record research issue #65](https://github.com/uobnnisiuk/footprint-sim/issues/65). Issues and pull requests track subsequent work and verification status.

## Try it

### Requirements

- **.NET 8 SDK** on Windows, Linux, or macOS (the runtime alone is insufficient to build the project)
- An installed SDK compatible with the repository's `global.json`

Check the environment:

```bash
./scripts/check-environment.sh
```

On Linux or macOS, the repository provides a helper script using Microsoft's official SDK installation mechanism if the SDK is missing:

```bash
source scripts/setup-dotnet.sh
./scripts/check-environment.sh
```

On Windows, use Microsoft's official .NET 8 SDK installer or `dotnet-install.ps1`.

### Build and test

```bash
dotnet restore FootprintSim.sln
dotnet build FootprintSim.sln --no-restore
dotnet test FootprintSim.sln --no-build
```

### Run a scenario

This example runs **S1**, a synthetic scenario that includes hazard-driven movement and **separation of a person from their device**:

```bash
dotnet run --project src/FootprintSim.Cli -- \
  --scenario scenarios/s1_debris_transport/scenario.json \
  --seed 42 \
  --output results/s1
```

The main output files are:

| File | Meaning |
| --- | --- |
| `ground-truth.events.json` | Ground truth known to the simulator; **never used as an inference input** |
| `observations.json` | Evidence records that can be made available to the search side |
| `search-zone.summary.json` | A summary of candidate areas based on observations and the public model |
| `run.metadata.json` | Scenario, seed, input hash, and other reproducibility metadata |

Runs are designed to be deterministic for identical scenario bytes and seed. Generated output under `results/` is excluded from version control.

### Compare inference methods

**S3** compares a method using only the target's own evidence with one that additionally uses available cross-person encounter evidence (weft), under the **same synthetic world and seed**:

```bash
dotnet run --project src/FootprintSim.Cli -- evaluate \
  --scenario scenarios/s3_cross_thread_encounter/scenario.json \
  --seeds 0..19 \
  --strategies all \
  --output results/s3-comparison
```

Evaluation produces files including `runs.csv`, `aggregate.csv`, `conditions.csv`, `paired.csv`, and `manifest.json`. Add `--telemetry` to record the observation and inference process separately.

**A smaller candidate set is not sufficient evidence of success.** Check false exclusions, uncertainty, and unavailable evidence too.

## Scenarios and research areas

| Scenario | Research focus |
| --- | --- |
| **S0** | Walking and last-known observations (baseline) |
| **S1** | Hazard transport, device separation, missing and conflicting observations |
| **S2** | Probabilistic branching, deposition or trapping, secondary transport |
| **S3** | Historical constraints from encounters and paired method comparisons |
| **S4** | Tracing public causal relations across multiple wefts |
| **S5** | Reconstructing past-state candidates from distributed evidence |

The M1-F experiments built around S5 investigate changes in observation families, spatial and temporal precision, participant ambiguity, constraint composition, candidate representation, and related assumptions under controlled comparisons. Individual commands, assumptions, and results are recorded in the linked research documents.

### Further reading

- [Architecture and ground-truth isolation](docs/architecture.md) / [Scenario and movement model](docs/model.md)
- [Historical cross-thread constraints](docs/m1c2-cross-thread-constraints.md) / [Multi-weft causal tracing](docs/m1d-causal-tracing.md)
- [Past-state reconstruction](docs/m1e-past-state-reconstruction.md) / [Observation-family ablation](docs/m1f-observation-ablation.md)
- [Evidence ancestry and logical composition](docs/m1f14-constraint-composition-contract.md)
- [Candidate-to-region-to-checking-unit mapping](docs/m1f20-public-search-mapping.md) / [Bounded reachability results](docs/m1f21-public-transition-coverage-results.md)
- [Mathematical note on weft information value](docs/weft-mathematical-foundations.md)
- [Contributor and validation rules](AGENTS.md)

## Design principles and research limitations

- **Keep ground truth out of inference.** The realized path, selected hazard branches, and random draws are never passed to inference methods. Ground truth is used only to evaluate their results afterward.
- **Keep people and devices distinct.** After separation, each has its own location history.
- **Do not turn unknown into impossible.** Unreceived evidence, unpublished routes, unmapped locations, and contradictions have different meanings. Missing information must not be reported as proof that no search candidates exist.
- **Compare on equal terms.** Use the same available public information across methods, and keep scenario/seed outputs reproducible. Synthetic comparisons must not be presented as real-world performance measurements.
- **Do not assume operational readiness.** Disaster-physics modeling, observation-device calibration, real-time field operations, a complete 24-relations engine, and rescue-decision optimization are not complete or validated here.

**Footprint Sim aims to make uncertainty explicit while steadily expanding what can be justified from the evidence that remains.**
