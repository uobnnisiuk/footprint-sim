# M1-A evaluation harness

## Purpose and scope

M1-A measures synthetic M0 search outcomes across multiple scenarios, seeds, and strategies. It answers whether a strategy retained the simulated person's evaluation position, how much of the declared search universe it returned, whether it made an unsupported device-to-person promotion, and when it first produced useful guidance. It does not add a probabilistic hazard propagator, calibrated observation channels, a complete EBPC adapter, a search-priority policy, or a user interface.

The values produced by this harness describe only the checked-in abstract scenarios and the current deterministic M0 engine. They are not measurements or predictions of real disaster response, location, safety, survivability, or rescue performance.

## Strategy boundary

All strategies implement `ISearchStrategy` and receive one `SearchStrategyInput`. The input contains only:

- scenario ID and seed identity;
- the current integer simulation tick;
- `RescueContext`: target identity, public region topology, normal walking radius, and the scenario's declared public hazard envelope;
- observations whose `AvailableTick` is no later than the current tick.

`SearchStrategyInput` cannot carry `SimulationRun`, truth events, final entity states, or `GroundTruthEvaluationContext`. A strategy returns `SearchStrategyResult` before evaluation. Only `PerRunEvaluator`, in the `FootprintSim.Evaluation.Truth` namespace, receives the completed result together with the private truth position. This type boundary prevents strategy code from selecting candidates from the expected answer.

Every result has a stable strategy/scenario/seed identity, production tick, ordered candidate region IDs, evidence-boundary state, machine-readable rationale codes, historically confirmed-person evidence regions, and any explicit exclusions. Confirmed-person evidence regions are separate from current candidates: a known earlier location need not remain a current candidate, and the separate field lets device-only promotion be measured without parsing prose.

## Compared strategies

- `last-confirmed-only`: returns only the most recent `Confirmed` observation region for the target person. It returns no candidate before such an observation is available.
- `voluntary-reachability`: returns the current M0 `NormalMovementRegionIds`, reusing `BasicRescueInference` rather than reimplementing walking-range semantics. With confirmed evidence, the M0 range is the passable regions whose centers are within `NormalWalkingRadius` of the last confirmed position.
- `hazard-aware`: uses a target-person observation inside the declared public hazard envelope as the trigger to return that envelope. Without that evidence, it uses the normal-movement result. A device observation is neither a trigger nor a confirmed person position.
- `evidence-aware-current`: adapts the ordered `SearchPriorityZones` produced by the current M0 `BasicRescueInference`. It combines their region IDs in priority order and preserves the existing evidence state. This is an adapter for the current baseline only; it does not claim EBPC conformance.

Candidate order is deterministic. Priority-bearing M0 output retains priority order; sets without priority are sorted ordinally. Strategy and rationale identifiers are data contracts and are not derived by parsing human-readable explanations.

## Per-run metric definitions

A run is one scenario, seed, and strategy combination. The final strategy result is produced at the later of the maximum ground-truth event tick and the maximum observation availability tick, so delayed evidence is not silently omitted. `TimeToUsefulGuidance` additionally evaluates the same strategy at each integer tick from zero through that final tick, with only observations available at that tick.

`searchable regions` is the set of all distinct region IDs declared in the scenario, sorted ordinally. Passability and connection direction do not remove a region from this evaluation universe: they affect strategy reachability, while the denominator represents every region where the synthetic truth model can place an entity. A strategy result containing a region outside this universe is rejected.

- `GroundTruthContainment`: available when the true final person region is in the searchable universe. Its boolean value is whether that region is a candidate; numerator is 1 or 0 and denominator is 1.
- `CandidateRegionCount`: number of distinct candidate region IDs in the final result.
- `SearchSpaceFraction`: candidate count divided by searchable region count. Numerator and denominator are emitted explicitly.
- `SearchSpaceReduction`: `(searchable region count - candidate count) / searchable region count`. This is always reduction against the complete searchable universe, never against another strategy.
- `FalseExclusion`: the inverse of containment: true when the known searchable truth region is absent from the candidates. It measures outcome exclusion even when the strategy did not assert an exclusion.
- `ExplicitFalseExclusion`: true only when the strategy's machine-readable explicit exclusion list contains the truth region. Current strategies make no explicit exclusions, so this remains distinct from `FalseExclusion`.
- `DeviceOnlyPromotionViolation`: number of device-only observation records available by the result's production tick whose reported region the result marks as a confirmed person region. The observation must name a configured device and not the target person. Candidate membership alone is not a promotion.
- `TimeToUsefulGuidance`: the first tick at which candidates both contain the truth region and are fewer than all searchable regions. If no tick satisfies both conditions, it is unavailable rather than zero.
- `EvidenceBoundaryState`: the final result's `Confirmed`, `SupportedButUncertain`, `Conflicting`, `Indeterminate`, or `UnsupportedForQuery` state. Aggregation counts every state explicitly.

Metric records carry `available`, nullable `value`, and, where a ratio is involved, nullable `numerator` and `denominator`. An unavailable metric also carries a stable reason code. Thus a measured zero is not interchangeable with an uncomputable value. For example, a `TimeToUsefulGuidance` value of `0` means the two conditions really held at tick zero; `available: false, value: null` means they never held or truth was unavailable.

## Batch CLI

Evaluate all checked-in scenarios for seeds 0 through 19 with every strategy:

```bash
dotnet run --project src/FootprintSim.Cli -- evaluate \
  --scenarios scenarios \
  --seeds 0..19 \
  --strategies all \
  --output results/evaluation
```

`--scenarios` accepts one scenario JSON file or a directory searched recursively for `scenario.json`. `--seeds` accepts a single integer, an inclusive range such as `0..19`, or comma-separated mixtures such as `0,2,10..12`. `--strategies` accepts `all` or a comma-separated list of strategy identifiers. Descending or malformed ranges, unknown strategies, missing scenarios, and unknown options return a non-zero exit code with an error message.

## Output schemas

M1-A introduced output schema version `1`; M1-B extended it to `2` without reusing field meanings. The integrated research evaluator and batch manifest use schema `3`, including later candidate-standing and tracing metadata. Scenario schemas are separate and currently support versions 1–5. See [M1-B stochastic hazard](m1b-stochastic-hazard.md) and the later research contracts. The output directory contains:

- `runs.jsonl`: one compact, valid JSON object for each scenario/seed/strategy run;
- `runs.csv`: the same run identities, result fields, and metrics in a fixed analysis-oriented column order;
- `aggregate.json`: stable scenario/strategy groups with rates and distributions;
- `aggregate.csv`: the principal aggregate fields in a fixed column order;
- `manifest.json`: schema version, sorted scenario IDs, seeds and strategy IDs, run count, source commit SHA, dirty-tree flag, and scenario content SHA-256 values.

Aggregate groups contain run and metric-available counts; containment and false-exclusion numerator, denominator, and rate; total device-only promotion violations; mean, median, minimum, and maximum for candidate count, search-space fraction, search-space reduction, and available time-to-guidance values; all evidence-state counts; and unique outcome count. `metricCompleteRunCount` counts runs where every non-time core metric is available; time-to-guidance has its own available count because failure to satisfy its condition is a valid unavailable outcome. Median is the middle value for odd samples and the arithmetic mean of the two middle values for even samples.

JSON property order follows declared record property order. Scenario, seed, strategy, manifest, group, candidate, and set-like region orders are normalized or otherwise explicitly stable. CSV uses invariant culture, LF line endings, UTF-8 without a byte-order mark, a fixed column order, and `.` as the decimal separator. Current time, absolute paths, and random run IDs are not written. With identical scenario bytes, source state, selected inputs, SDK-compatible implementation, and command options, all five files are byte-identical.

`results/` remains ignored by Git. The manifest records the current commit and dirty flag so a result from modified source is not mistaken for a clean baseline.

## Seed sensitivity and current limits

M0 uses the seed for observation emission, delay, and conflict choices, but its checked-in truth steps are explicit. The outcome fingerprint covers candidates, confirmed-person evidence regions, explicit exclusions, evidence state, containment, search-space fraction/reduction, device-only promotion count, and time-to-guidance. Across two or more selected seeds, if all fingerprints for an aggregate group match, `uniqueOutcomeCount` is `1` and the report states that the current M0 scenario is seed-insensitive for that strategy result. With fewer than two distinct seeds, sensitivity is explicitly unavailable. The harness does not invent variation. When observation availability changes time-to-guidance, the unique outcome count records that timing sensitivity.

M1-B and later work should address probabilistic hazard propagation, calibrated per-channel observation behavior, explicit public/private hazard-policy configuration, broader scenario fixtures with seed-sensitive truth outcomes, and richer validation of evidence promotion. Complete EBPC mapping, real-data ingestion, survival estimation, map presentation, and operational rescue prioritization remain outside M1-A.

## M1-B synthetic S2 observation loss

S2 (`s2-intermittent-person-evidence`) preserves S1's topology, physical steps, and independent person/device trajectories. Only the transport-start target-person signal changes: emission probability is 1.0 and drop probability is 0.5. The deposition-person signal remains always dropped; device-only fixes remain separate evidence. Seeds affect observation loss and timing, not the physical trajectory. This is an abstract fixture, not a calibrated disaster model.

```bash
dotnet run --project src/FootprintSim.Cli -- evaluate \
  --scenarios scenarios/s2_intermittent_person_evidence/scenario.json \
  --seeds 0..19 \
  --strategies all \
  --output results/s2-evaluation
```

The integration test checks both signal forms across these seeds, byte-identical observation JSON and evaluation results for repeated inputs, unchanged physical truth, and no device-only promotion for all eight registered strategies. For `hazard-aware`, retained person evidence selects the four-region hazard envelope; missing evidence selects the two-region normal baseline. Containment and candidate/search metrics therefore vary, and aggregate containment is checked against the observed signal-presence count rather than an assumed random split.

The separate research fixture `s2-branching-debris-transport` lives at
`scenarios/s2_branching_debris_transport/scenario.json`. It uses stochastic physical
branching and person/device separation (scenario schema 2). It is not the
intermittent-person-evidence fixture above. Both registered IDs and input files
are retained unchanged; reports identify them by full ID rather than the short
historical name “S2”.
