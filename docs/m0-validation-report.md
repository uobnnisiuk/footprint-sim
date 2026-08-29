# M0 Validation Report

## Scope and environment

- Validation time: `2026-08-29T14:07:57+09:00`
- OS: Ubuntu 24.04.3 LTS on WSL2
- Architecture: `x86_64` (`linux-x64`)
- .NET SDK: `8.0.424`
- Runtime: `Microsoft.NETCore.App 8.0.30`
- Scenario seed: `42`

This audit fixes M0 as a reproducible baseline. It does not add M1 behavior.

## Build, test, and format

| Check | Result |
|---|---|
| `dotnet --info` / `dotnet --list-sdks` | .NET SDK 8.0.424 selected |
| `dotnet build` | Passed; 0 warnings, 0 errors |
| `dotnet test` | Passed; 12 passed, 0 failed, 0 skipped |
| `dotnet format --verify-no-changes` | Passed |
| JSON syntax validation | All S0 and both S1 runs passed |

The tests cover deterministic event and observation JSON, stable tick/sequence ordering, independent person/device positions, hazard transport beyond the normal walking range, observation-boundary leakage, all evidence-state JSON values, inference outcomes for indeterminate/unsupported/conflicting evidence, scenario loading, and S0/S1 search summaries.

## Scenario execution and determinism

| Scenario | Ground-truth events | Observations |
|---|---:|---:|
| S0 walking baseline | 12 | 3 |
| S1 debris transport | 15 | 4 |

Two independent S1 output directories were generated from the same scenario bytes and seed. Each corresponding pair was byte-identical.

| S1 output | SHA-256 |
|---|---|
| `ground-truth.events.json` | `8524ccb723dfd470d68da0aa31a78143ff14538e7cd13769ef4e409454a72f62` |
| `observations.json` | `06956fd9dba75ede0901936982de5475d24c81ca0845cec3f222f9edc22afd6a` |
| `search-zone.summary.json` | `434f5e24220485b792f990f60154dcd817ac10b12168e348c3e7cea64299e476` |
| `run.metadata.json` | `9533ff477c2b1398f986a2ad0cfd42b85d8603307c976e805fb4501b194b73ee` |

Run metadata has no wall-clock field or output-directory path. Event/observation IDs, ordering, payload dictionaries, region lists, and priority zones are deterministic for identical scenario bytes and seed.

## S1 semantic review

### Ground truth

- Before hazard transport, person and device are at `(0, 0, 42)` in `hillside-building` at tick 3.
- Hazard-driven transport starts at tick 10 in `slope-channel`, then reaches `river-bend` at tick 12.
- Person/device separation occurs at tick 13. The person is at `(94, -18, 11)` in `river-bend`; the device is independently placed at `(70, -12, 17)` in `slope-channel`.
- The person is deposited at tick 16 and trapped at tick 18 at `(126, -16, 9)` in `deposition-zone`.
- The device alone undergoes secondary transport at tick 17 and ends at `(148, -32, 7)` in `downstream-bank`.
- Transport, separation, deposition, trapping, and observation lifecycle events carry cause-event references to their source events. The transport start and initial confirmed event are roots and therefore have no cause.
- Ticks are nondecreasing and sequences are contiguous from 0 through 14. Same-tick ordering is stable.

### Observation boundary

- A building access record for the person is confirmed at tick 3 and available at tick 3.
- A coarse person-presence signal at `slope-channel` is observed at tick 10, available at tick 12, and marked `SupportedButUncertain`.
- The person's deposition/trapping location is intentionally absent: its deposition observation is dropped at tick 16 and exists only as a private lifecycle event.
- The device radio fix is observed at tick 17 and available at tick 18. It produces conflicting device-only reports for `downstream-bank` and `river-bend`.
- Truth-only branch, speed, separation mechanism and positions, deposition probability, trapping state, cause IDs, and truth lifecycle details do not appear in observation JSON.
- No observation reports the person's true final `deposition-zone` position. The downstream radio fixes identify `device-mika-phone`, not the person.

The inference interface accepts only public rescue context and `ObservationSet`; it does not accept the simulation run, truth events, or final entity state.

### Inference and search zones

- Last confirmed person location: `hillside-building`, state `Confirmed`.
- Last reported person location: `slope-channel`, state `SupportedButUncertain`.
- Overall latest person evidence state: `SupportedButUncertain`.
- The 30-unit normal-movement baseline contains `hillside-building` and `mountain-road` only. It does not contain the person's true final `deposition-zone`.
- The declared hazard envelope contains `slope-channel`, `river-bend`, `deposition-zone`, and `downstream-bank`, so it includes the true final person region outside the normal baseline.
- Priority 1 is the last reported person region; priority 2 is the declared hazard envelope; priority 3 is the normal-movement baseline from the last confirmed position. Each is labeled with its basis and evidence state.
- Device-only conflicting fixes do not replace the person's last reported location or assert that the person is downstream.
- `Confirmed`, `SupportedButUncertain`, `Conflicting`, `Indeterminate`, and `UnsupportedForQuery` all round-trip through JSON. Tests also exercise the latter three as inference results.
- The summary labels zones as candidates, makes no point-location claim, and does not declare unlisted regions excluded.

S1 therefore demonstrates the intended M0 result: a normal-movement-only search misses the person's truth region, the hazard envelope retains that region as a candidate, and the separately transported device is not treated as proof of the person's position.

## Repository hygiene and boundary review

- SDK files, installer downloads, generated results, build outputs, test outputs, IDE files, and package artifacts are ignored or absent from the commit candidates.
- No credential/token patterns or user-specific absolute paths were found in commit candidates.
- No license was added.
- Observation fields are created only from explicitly public scenario observation fields; truth subjects, positions, kinds, payloads, and cause IDs are not copied implicitly.
- Person and device state diverge at the separation event and remain independent.
- No clock, random GUID, current directory, or output path affects JSON content. SplitMix64 and the provided seed drive stochastic observation choices.
- The implementation remains a small M0 Core/CLI/test solution; no M1 engine or speculative abstraction was added.

## What M0 establishes

- Reproducible integer-tick event logs and observation logs.
- Explicit world regions, connections, movement categories, hazards, persons, devices, carriers, and rescuers.
- A testable person/device separation and secondary-transport case.
- A hard contract boundary between truth data and rescue inference inputs.
- A baseline comparison between normal movement and a declared hazard envelope.
- Evidence-labeled candidate search priorities without unsupported exclusion claims.

## What M0 does not establish

- Calibrated fluid, debris, avalanche, collapse, mobility, or survival physics.
- Accuracy for a real incident, terrain, sensor, person, device, or rescue operation.
- A complete EBPC implementation or validated probabilistic inference.
- Operational search ordering, resource allocation, safety advice, or exclusion decisions.
- Statistical performance across populations, seeds, or observation-channel conditions.

This simulator does not predict real disasters, real locations, survival probability, or operational safety.

## Concrete topics for M1

1. Replace explicit hazard steps with a seed-controlled graph propagator that applies route branches, speed ranges, and stopping probabilities.
2. Calibrate delay, loss, conflict, and identification error separately for each observation channel.
3. Implement an EBPC adapter behind `IRescueInference` without exposing truth types.
4. Add multi-run metrics for search-area reduction, coverage, false exclusion, and sensitivity to seed/model parameters.
5. Replace geometric normal-range selection with topology-aware reachability and documented passability semantics.
6. Define and validate a domain-backed policy for search-priority ranking; M0 priorities are explanatory candidates only.
