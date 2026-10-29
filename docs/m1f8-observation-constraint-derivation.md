# M1-F8 minimal observation-to-constraint derivation

## 1. Background and outcome

M1-F7 found that S5 usually declares an observation and its historical constraint payload
separately. Location had one experiment-side copy rule, but observed time did not determine the
declared historical window. Participant and relation values likewise had no S5 narrowing dispatch.

M1-F8 adds one executable vertical slice:

```text
public later-event observation
        +
public predecessor dependency with a duration bound
        |
        v
bounded-duration-predecessor-window-v1
        |
        v
observation-derived CandidateGeneration constraint
        |
        v
historical public candidate-state reconstruction
        |
        v
post-reconstruction truth evaluation
```

The outcome is successful. Changing only the observed occurrence-time content changes the computed
past window and the historical candidate result through one named public rule. This is a contract
and traceability result, not a claim of improved real-world performance or general temporal
reasoning.

The starting point is M1-F7 commit
`56aa81e92ef29e44ccf74b8da455517bae0a99c0`. S5 and `later-trace-a` are unchanged.

## 2. Contract scope

The implementation deliberately does not introduce a rule engine. It contains:

- `PublicEventObservation`, whose event existence remains observed while occurrence time is either
  a known uncertainty window or unknown;
- `PublicBoundedDurationDependency`, one explicit predecessor-to-later-event edge with public
  duration bounds and predecessor Region scope;
- `BoundedDurationObservationConstraintDeriver`, the only executable rule;
- `ObservationDerivedConstraint`, a truth-free, usage-bound historical constraint;
- `ObservationConstraintDerivationAudit`, the machine-readable input, applicability, provenance,
  success, and failure record; and
- `ObservationDerivedHistoricalCandidateReconstructor`, a small CandidateGeneration-only adapter
  over existing public candidate-warp state definitions.

The output constraint and audit shapes can be reused by future rules. The bounded-duration input
and interval arithmetic remain rule-specific. No dynamic dispatch, rule registration, graph
search, path composition, participant ambiguity, or relation taxonomy is added.

## 3. Explicit inputs and applicability

The rule declares these required input categories:

| Required input | Fixture value or meaning |
| --- | --- |
| source observation | deterministic public event observation ID |
| occurrence-time window | known closed epistemic window, or explicit unknown |
| public dependency | `predecessor-to-later-checkpoint` |
| duration bounds | inclusive integer ticks `[4,5]` |
| target entity | `person-a` |
| availability | observation must be public and available by cutoff |
| evidence boundary | `Confirmed`; no calibrated truth claim is implied |
| source ancestry | one observation family plus its direct evidence source |
| usage role | `CandidateGeneration` only |

Applicability also requires matching scenario, target entity, and later-event kind. The derived
window must remain non-negative. A failed condition returns no constraint and a typed failure
reason. It does not substitute truth, a default time, or a different usage role.

The only evidence state supported by this first slice is `Confirmed`. Supporting conservative
generation from the other evidence states is a future semantic decision, not silently guessed in
this fixture.

## 4. Time-window semantics

`ObservationTimeWindowKind.OccurrenceTimeUncertainty` means:

> The event occurrence time is known only to be one tick in the closed interval `[a,b]`.

A point observation uses the degenerate interval `[t,t]`. A wider interval is not event duration,
state persistence, observation-act duration, or evidence-availability duration. The model adds no
such meanings.

`OccurrenceTimeKnowledge.Unknown` means that the later event observation exists but its occurrence
time is unavailable. It is not an absent event. The source observation and its one source group
remain auditable even though no time-derived constraint is emitted.

## 5. Public dependency and duration

The fixture's dependency explicitly identifies:

```text
predecessor-state
    -- predecessor-to-later-checkpoint,
       public duration source public-transition-duration-bound,
       duration in [4,5] ticks -->
later-checkpoint-event
```

It also declares the public predecessor Region scope. This scope is the same four Regions for all
profiles, so location is fixed and only the time content varies. The dependency's stable public
input ID includes its endpoints, target, duration bounds, duration source, and Region scope.

This is one edge, not a general dependency graph. No realized trajectory, truth parent, or hidden
path is read.

## 6. Bounded-duration rule and interval formula

Let the possible later-event occurrence time be

```text
O = [o_min, o_max]
```

and let the public dependency duration be

```text
D = [d_min, d_max].
```

For `later = predecessor + duration`, the predecessor-time set is represented by the inclusive
interval hull

```text
DerivedPast(O, D) = [o_min - d_max, o_max - d_min].
```

The scenario declares `O` and `D`; it cannot declare `DerivedPast`. The fixture loader rejects
unmapped fields, including an attempted direct output-window field. A negative result or arithmetic
overflow is a typed derivation failure rather than a clipped or defaulted interval.

## 7. Conservative uncertainty propagation

The formula is conservative for the independent closed intervals represented by this rule. If
`O1` is contained in `O2`, then:

```text
DerivedPast(O1, D) is contained in DerivedPast(O2, D).
```

Widening the duration bound is monotone for the same reason: decreasing `d_min` can only increase
the upper predecessor bound, and increasing `d_max` can only decrease the lower predecessor bound.

Tests cover both properties. The fixture also checks adjacent profile candidate sets; every more
precise set is contained in the next coarser set. There are zero monotonicity violations.

The single interval is a hull. Correlations, disjoint duration alternatives, and path-conditioned
window sets are intentionally not represented.

## 8. Availability and truth boundaries

`AvailableAtTick` states when the observation may be used at the query cutoff. It is checked only
for access. It is not an operand in the interval formula. Changing it between usable values leaves
the derived past window unchanged. Unknown occurrence time is not filled from availability.

The deriver accepts only:

- `PublicEventObservation`;
- `PublicBoundedDurationDependency`;
- a public evidence cutoff; and
- an evidence usage role.

The reconstructor accepts only public candidate-state definitions and an optional derived
constraint. Neither API accepts truth events, realized transitions, realized parents, simulation
runs, scored Regions, or evaluation results. `TruthRegionId` is read only after reconstruction to
compute containment and false exclusion. Changing truth leaves every observation ID, dependency
ID, constraint ID, derived window, candidate ID, and candidate count unchanged.

## 9. Provenance and source independence

Each successful constraint can be traced to:

- the source observation ID and direct evidence source;
- `bounded-duration-predecessor-window-v1`;
- the public dependency ID and its stable public-input ID; and
- `public-transition-duration-bound`, including `[4,5]` in the audit.

Constraint IDs are deterministic hashes of public inputs and computed output. They contain no
truth value, absolute path, current time, or random identifier.

The observation source group is derived from common source ancestry, not from the number of time
possibilities or derived constraints. Deriving different public duration alternatives from the
same observation produces different constraint IDs but the same source-group ID. One observation
therefore never becomes multiple independent evidence sources.

## 10. Evidence usage boundary

The rule supports `PastStateEvidenceUsageRole.CandidateGeneration` only. A request to emit a
verification or traversal constraint fails with `UnsupportedUsageRole`. The fixture reconstructor
performs no verification or traversal and accepts no such evidence inputs.

This preserves the M1-E1 boundary:

| Role | M1-F8 behavior |
| --- | --- |
| CandidateGeneration | supported and explicit |
| CandidateVerification | not supported; no implicit promotion |
| Traversal | not supported; no implicit promotion |
| EvaluationOnly | private truth is used only after candidate generation returns |

## 11. Acceptance fixture and preregistered profiles

The deterministic fixture has four public predecessor event-point candidates:

| Candidate Region | Candidate tick | Truth |
| --- | ---: | --- |
| `predecessor-10` | 10 | yes |
| `predecessor-11` | 11 | no |
| `predecessor-12` | 12 | no |
| `predecessor-13` | 13 | no |

The public duration is fixed at `[4,5]`, availability and cutoff are both tick 20, target and
source ancestry are fixed, and the profile boundaries are declared in the fixture before runtime.

| Profile | Observed occurrence content | Boundary reason |
| --- | --- | --- |
| exact | `[15,15]` | reaches predecessor ticks `[10,11]` |
| medium | `[15,16]` | adds predecessor tick 12 |
| broad | `[15,17]` | adds tick 13 and reaches the candidate-layer ceiling |
| unknown | no occurrence-time window | retains the event observation but emits no time constraint |

There is no randomness in this acceptance fixture, so repeating 200 seed labels would produce 200
copies of the same world and add no evidence.

## 12. Acceptance results

| Profile | Derived past window | Derivation | Candidates | Containment | False exclusion | Reduction | Generation work |
| --- | --- | --- | ---: | ---: | ---: | ---: | ---: |
| exact | `[10,11]` | success | 2 | 1 | 0 | 0.50 | 5 |
| medium | `[10,12]` | success | 3 | 1 | 0 | 0.25 | 5 |
| broad | `[10,13]` | success | 4 | 1 | 0 | 0.00 | 5 |
| unknown | none | `UnknownOccurrenceTime` | 4 | 1 | 0 | 0.00 | 4 |

Generation work is four public candidate inspections plus one constraint application when a
constraint exists. The unknown profile inspects the four candidates and applies no time constraint.
These counts are fixture diagnostics, not calibrated operational cost.

The success criterion is met: changing the observation's time content changes the computed
constraint and the candidate result. Performance improvement was not required.

## 13. Fixed failure conditions

| Prohibited failure | Enforcement |
| --- | --- |
| truth leakage | public-only API shape plus truth-invariance tests |
| arbitrary time offset | only the documented interval subtraction is implemented |
| direct output declaration | JSON loader rejects unmapped output fields |
| availability-time substitution | availability is an applicability check, never a formula input |
| wider uncertainty gives a stronger constraint | window and candidate monotonicity tests |
| unknown becomes absent | source observation remains present; typed unknown failure emits no constraint |
| missing provenance | audit and provenance completeness tests |
| usage leakage | CandidateGeneration-only applicability and reconstructor validation |
| alternatives become independent sources | stable ancestry-derived source group test |
| result-dependent tuning | fixed fixture windows, duration, and boundary rationales |
| nondeterministic output | stable hashes, deterministic ordering, and byte comparison |

## 14. Why S5 is not migrated

S5 still declares `later-trace-a` at `[24,24]` and its historical constraint window at `[13,14]`.
It has no public dependency or duration bound that justifies that mapping. M1-F8 does not invent
one, reinterpret its relation label, or copy the new formula into S5.

Migrating S5 now would turn an unproven scenario offset into production semantics. The existing S5
scenario, F2/F3 experiments, reconstructor, and validator remain unchanged.

## 15. Reuse and remaining limitations

Reusable pieces are the derived-constraint envelope, audit fields, stable public-input and
constraint addressing, provenance chain, source-group identity, failure result, and usage-role
boundary. Future rules may produce the same constraint type from location sets, participant
alternatives, or relation alternatives.

That structural reuse is not evidence that those axes are already supported:

- location would need its own explicit rule instance and typed unknown semantics;
- participant still needs finite identity/kind alternatives and conservative merge semantics;
- relation still needs justified alternatives or taxonomy and per-relation dispatch;
- time supports one closed occurrence-time interval and one duration edge only;
- non-confirmed evidence behavior is not defined by this slice;
- dependency chains, concurrent conditions, sequence alternatives, disjoint windows, and temporal
  correlations are absent; and
- the acceptance candidate world is deliberately tiny and deterministic.

No Buddhist or Abhidhamma term is introduced into production naming. A future methodology
comparison may compare already-defined primitives such as dependency, duration, sequence, support,
and alternatives; it must not identify this one transition with a specific *paccaya* by name alone.

## 16. Next recommendation

**Recommend A**, with one condition: do not run 200 identical copies of this deterministic fixture.
The next single implementation should add a preregistered, seed-varying synthetic candidate-world
generator around the same public bounded-duration rule, then run exact/medium/broad/unknown as
paired profiles over 200 seeds. This directly tests whether the now-connected time information
changes containment, reduction, and work across worlds without altering the derivation semantics.

Recommend B comes later as a generality check after this time slice has one nontrivial paired
performance result. Recommend C remains blocked because S5 lacks a justifying public dependency.
Recommend D is not selected: the minimal contract satisfies this fixture's correctness, provenance,
unknown, monotonicity, usage, and determinism requirements, although it is intentionally not a
general engine.

What M1-F8 establishes:

- one public observation-to-constraint computation path is executable and auditable;
- bounded occurrence-time uncertainty propagates conservatively into a past window;
- candidate results can expose that precision difference when the candidate layer contains time
  alternatives; and
- unknown occurrence time can preserve event existence while declining derivation.

What M1-F8 does not establish:

- the validity of S5's historical window;
- real-world duration calibration or rescue performance;
- correctness for multiple dependencies, paths, participants, or relations;
- benefits across stochastic worlds or 200 seeds; or
- a general temporal, causal, or rule-engine architecture.
