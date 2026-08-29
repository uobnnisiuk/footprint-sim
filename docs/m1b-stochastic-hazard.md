# M1-B stochastic hazard propagation

## Purpose and limits

M1-B adds a reproducible, seed-driven synthetic hazard propagator and the S2 branching-debris fixture. It measures whether the four existing search strategies behave differently as synthetic truth outcomes vary, without giving a strategy the selected truth path. The weights, topology, travel windows, trapping, separation, and release values are illustrative research inputs. They are not calibrated probabilities or a model of a particular disaster, terrain, device, human location, survival, or rescue performance.

M1-B does not add observation-channel calibration, BLE propagation, a complete EBPC adapter, real-data ingestion, a survival model, GUI/map output, a rescue-priority policy, external services, or parallel execution.

## Stable random generator and stream separation

`StableRandom` implements SplitMix64 in project code. It accepts a 64-bit seed, has fixed output-vector tests, produces `[0,1)` doubles from the upper 53 bits, and uses rejection sampling for unbiased inclusive integer ranges. It is stable for the same seed and call order across supported .NET implementations; it is not cryptographically secure.

`StableSeed.Derive` length-prefixes purpose strings and derives 64-bit sub-seeds with SHA-256. Hazard propagation uses a shared pre-separation stream, then separate person and device streams. Draws added to one separated entity therefore do not shift the other entity's sequence. Scheduling and wall-clock time are not inputs. PRNG state and raw draws never cross the strategy boundary.

## Hazard model and validation

A schema-version-2 scenario declares one stochastic hazard with:

- nodes: region ID, inclusive hazard-arrival window, deposition/trapping probability, optional release probability and delay, terminal flags, visibility, publication tick, and public provenance;
- directed transitions: stable ID, source/destination, finite positive weight, inclusive travel-tick range, primary or secondary transport type, optional entity/separation activation, visibility, and provenance;
- run controls: source, start tick, optional associated device, separation probability, and a transition safety bound.

Weights are relative weights. Selection normalizes their finite positive sum; callers do not need exact floating-point sums of one. Validation rejects non-finite, non-positive, or non-normalizable weights; probabilities outside `[0,1]`; unknown regions/entities; duplicate IDs; invalid tick windows; impossible destination arrival windows; cycles; unreachable required terminals; missing destinations; inactive release edges; and a public node graph that is inconsistent with the declared envelope. Arrival and travel ranges are inclusive.

The realized ground-truth event chain records selected transition/provenance identifiers and bounded travel ticks, followed by `Deposited` or `Trapped`. A released entity produces `SecondaryTransported` whose cause points to its preceding stop event. Raw random values are not recorded.

The current compact model performs one required `AnyEntity` primary transition from a non-depositing source before the optional person/device separation decision. This represents the shared initial transport segment and is validated explicitly rather than silently ignoring source deposition settings.
Scenario validation also requires transported subjects to be at the source before `startTick` and rejects explicit movement of those subjects at or after hazard start; their later path must come from the propagator rather than a competing fixed step list.

## Realized path versus public envelope

The realized path is private simulation truth: selected branches, selected timings, final person/device regions, secondary transport, and causal events. It is held by `SimulationRun` and is available to the evaluator only after a strategy has returned.

The `PublicHazardEnvelope` contains only the source and publicly visible possible branches/regions, inclusive arrival/travel windows, transport type, publication tick, and public provenance. It does not contain the selected transition, selected terminal, private trajectory, secondary outcome, or random draw. It is constructed from scenario content and is therefore identical across seeds for the same public conditions. Strategies see it only through `RescueContext` and only use regions whose publication tick is at or before the current simulation tick.

## Candidate provenance and evidence boundary

Every built-in strategy result has at least one machine-readable provenance record per candidate. Source kinds are `LastConfirmed`, `VoluntaryReachability`, `PublicHazardEnvelope`, `PersonObservation`, `DeviceOnlyObservation`, `ConflictingObservation`, and `Unsupported`. A record also contains source identifier, availability tick, person-evidence and device-only flags, and evidence-boundary state.

Device-only provenance may explain why an already-supported region is visible, but it never adds that region to `ConfirmedPersonRegionIds`. This metadata is not a complete EBPC mapping and no EBPC conformance is claimed.

## S2 synthetic scenario

`scenarios/s2_branching_debris_transport/scenario.json` contains a building/road approach, a synthetic slope source, a fork, a river path, three direct deposition regions, and a downstream terminal reachable by secondary transport. Person and device may separate after the shared trunk and then choose independently derived paths. All possible public branches are in the initial envelope; only the ground-truth log identifies the realized branch. Multiple seeds produce multiple person and device outcomes. Observed frequencies remain properties of this synthetic fixture, not real probabilities.

## Guidance persistence metrics

For batch evaluation, the strategy is evaluated at every integer tick from `0` through `finalTick`, both inclusive. `finalTick` is the later of the last truth-event tick and last observation-availability tick. At each tick:

`useful = candidates contain the final true person region AND distinct candidate count < searchable region count`

The evaluator, not the strategy, applies the truth clause. Schema version 2 retains `TimeToUsefulGuidance` and adds:

- `FinalGuidanceUseful`: predicate at `finalTick`;
- `UsefulGuidanceTickCount`: number of evaluated ticks where the predicate is true;
- `EvaluatedTickCount`: inclusive timeline size;
- `UsefulGuidancePersistenceFraction`: useful count divided by evaluated count;
- `LongestUsefulGuidanceStreak`: longest consecutive run of useful evaluated ticks;
- `GuidanceRegressedAfterUseful`: whether any non-useful tick occurs after the first useful tick.

If truth is absent or outside the searchable universe, truth-dependent metrics are `available: false` with null values; they are not written as zero. A measured zero remains `available: true`. `TimeToUsefulGuidance` can therefore be tick 0 while final usefulness is false and regression is true.

## Batch output schema version 2

Run S2 for 200 seeds and all strategies:

```bash
dotnet run --project src/FootprintSim.Cli -- evaluate \
  --scenario scenarios/s2_branching_debris_transport/scenario.json \
  --seeds 0..199 \
  --strategies all \
  --output results/m1b-s2
```

The five reproducibility files remain `runs.jsonl`, `runs.csv`, `aggregate.json`, `aggregate.csv`, and `manifest.json`. Version 2 adds candidate provenance, guidance-persistence fields, evaluator-only realized person/device outcome fields, secondary-transport counts, per-strategy provenance-source counts, and scenario truth distributions. Truth distribution fields are output/evaluator data and are never copied into strategy input or result.

JSON declaration and collection order, scenario/seed/strategy/candidate/provenance order, CSV columns, invariant-culture numbers, LF endings, and UTF-8 without BOM are stable. The manifest records scenario hashes, sorted inputs, source commit, and dirty flag. Reproducibility files exclude current time, absolute paths, random run IDs, and runtime-generated identifiers. Byte identity requires identical scenario bytes, selected inputs, source state, and this implementation. `results/` remains ignored.

## Deferred to M1-C and later

Further work should address calibrated and purpose-separated observation streams, time-varying public-envelope updates with observation provenance, sensitivity analysis of synthetic weights, richer malformed-graph fixtures, a formal EBPC mapping, and larger scenario families. Physical debris/fluid simulation, real hazard probabilities, operational interpretation, survivability, and rescue prioritization remain out of scope.
