# F21 bounded transition coverage: results and limits

Related: #52, PR #64. Baseline: cca695801c20e2510e0111f57997639e30a81338.
Preregistration: 93e652c4accaa5751f22d1a2519f66ec6a82682d.
Implementation: b6cb1583c96bf2b9f3403123ac735cebd076112c.
The protocol preceded new implementation/execution. No case or acceptance
threshold was changed after observing the results.

## Interpretation

F20's declared-model projection is reused unchanged. The new finite producer
provides a construction for model-relative coverage, with explicit temporal
semantics and a conservative unknown result when assumptions are insufficient.
This is not a physical completeness certificate.

### Public-envelope non-identifiability

The registered C# witness starts from the existing valid S2 scenario, fixes
separation probability to one in both copies, and changes only fork-to-river
activation from PersonOnly to DeviceOnly. Both scenarios pass the existing
validator. Their actual PublicHazardEnvelopeBuilder outputs serialize identically.
A person can reach the permanent deposition-north terminal in the first model,
but not in the second. The shared source-to-fork trunk is unchanged; positive
weights, zero intermediate deposition and the terminal's lack of release
justify the reachability distinction without interpreting sampled seeds as
exhaustive evidence. Private definitions are used only in this evaluator test.

This proves that the public envelope does not identify exact person support.
It does NOT prove that no sound overapproximation is possible: the union of both
models' possible locations can cover both. Additional closure and dynamics
assumptions are needed to justify a particular finite outer set. Do not confuse
the counterexample with a proof that every conservative envelope algorithm fails.

### Finite coverage construction

For a certified unknown-instant window, the producer unions reachable states from
every integer origin tick. At each step it includes declared waiting, every
declared outgoing edge, and the intermediate transit state of a two-tick move.
Inductively: initial states cover all permitted starting instants; each one-tick
successor covers every permitted transition; union and region projection preserve
coverage. Thus the construction covers the finite model's endpoints when the
closed dynamics and window interpretation assumptions hold. This reasoning does
not certify that those assumptions describe a real place.

The C# recursive oracle independently enumerates full moves and stops in transit
when the target precedes arrival, rather than reusing the producer's one-tick
state-set computation. It checks exact sets for 1,944 registered queries and
independently computes unit unions through unchanged F20 code. A total mapping
uses one unit per node and two per transit region. Missing/partial mappings,
missing assurances, invalid/future declarations, permutations, detached results
and actual CLI JSON output are checked separately.

The producer is intentionally an opt-in bounded model (ticks 0..6, 1..5 nodes,
durations one or two), not an automatic adapter for arbitrary hazard envelopes.
A false closure assurance remains a false premise; identifiers and flags do not
authenticate sources. Unknown-instant intervals must not be substituted for
continuous-residence intervals. Upstream historical candidate coverage is assumed.

## Independent Python reference analysis

Run:
```bash
python3 scripts/analyze-f21-transition-coverage.py
```

Two local executions produced byte-identical JSON. This script does not call or
execute the C# producer; its counts describe the registered reference family.

| Quantity | Count |
|---|---:|
| Graphs | 27 |
| Graph/origin/window/target queries | 1,944 |
| Nonempty proper subsets of the six-region catalogue | 1,924 / 1,944 |
| Full six-region support | 20 / 1,944 |
| Queries including a transit region | 622 / 1,944 |
| Endpoint-only mutant with missing alternatives | 72 / 1,944 |
| Duration-one mutant with missing transit alternatives | 622 / 1,944 |

Reference support size distribution: 1:936, 2:329, 3:389, 4:164, 5:106, 6:20.
Queries are dependent exhaustive cases, not independent empirical samples.
These diagnostics measure representation effects, not rescue performance.

The dedicated endpoint witness uses a->b duration two, window [0,1], target 2.
Correct support is {a,ab,b}; using only tick 1 yields {a,ab} and loses b.
At target 1 from tick 0 the correct support is {a,ab}; instant travel substitutes
b for ab. The closed c-origin positive control returns {c}, showing the producer
does not achieve safety solely by returning all regions or unknown.

## What information is required

| Public information | Role and unresolved external assumption |
|---|---|
| Entity and admissible movement modes | Defines which moves apply; not recoverable in full from the old envelope |
| Closed domain and all allowed dynamics | Makes absent transitions meaningful within this model; physical closure remains unverified |
| Validity horizon, travel and waiting semantics | Determines positions at a particular time, including transit |
| Meaning of the historical interval | Specifies which starting states/times must be covered |
| Spatial realization of nodes/transit into units | Transfers region coverage to units; partial maps retain uncertainty |
| Publication cutoff and source/version identity | Prevents use of unavailable input and identifies the model; does not prove source correctness |

These are sufficient conditions for this construction. The study does not prove
that this exact field list or waiting-at-every-node rule is universally necessary
or minimal. Other conservative models may use different sufficient contracts.

## Decision

The bounded results support developing explicitly scoped public model producers.
They do not authorize real-geographic ingestion or operational search evaluation.
A future geographic study should first select one movement mode and one bounded
area, then evaluate time/transit/domain assumptions against independent held-out
movement records. Detection and resource calibration remain separate later work.
F16-F20 algorithms/results, existing scenarios, CI and automation are unchanged.

## Execution evidence and review status

Implementation-head normal CI:
https://github.com/uobnnisiuk/footprint-sim/actions/runs/41184833162
completed successfully: 550 passed, zero failed/skipped, build/format/environment,
automation checks, S0/S1 and the all-scenario evaluation command.
This includes 19 new test cases; the exhaustive test loops over 1,944 queries.
No local C# execution is claimed (the authoring host has no .NET SDK).

Implementation-head F19 evidence run:
https://github.com/uobnnisiuk/footprint-sim/actions/runs/41184833161
was still running when this result note was authored. Its status is not assumed.
Final-head CI and independent review evidence are tracked in PR #64; this note
does not claim review acceptance or merge.

Python script SHA-256:
4515c77e156297028951deae0f068086d1912ce75f7bd26bb754f180399e0556
Repeated reference JSON SHA-256:
264cc4aee116c9a8549fb08cea45cbbfd053600716cb629d21995350bfe7a6de

Local checks: independent reference executed twice and compared byte-for-byte,
Python syntax compilation, and whitespace diff checks. GitHub comparison confirms
only new isolated files; no baseline files were modified. No implementation or
protocol correction was needed for the first successful normal CI.
