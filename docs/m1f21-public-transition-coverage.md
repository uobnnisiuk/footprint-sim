# F21 bounded public transition coverage study

Status: preregistered, before implementation and execution of new cases.
Owner authorization: 2026-10-31 conversation "進めてください" following the
comparison and bounded-study proposal. Related research parent: #52.
Baseline: cca695801c20e2510e0111f57997639e30a81338. F20 is complete within its declared-model scope.

## Question and claims
Which additional public semantics permit complete location declarations relative
to a finite model, and what cannot be inferred from PublicHazardEnvelope alone?
"Complete" here means no reachable alternative is omitted, not real-world
calibration, detection, exact probability, or authenticity of the model author.
The historical candidate must itself include the actual past state; this study
does not establish upstream candidate coverage.

H1: two valid synthetic hazard definitions can have byte-identical public
envelopes while differing in possible person locations after transport, because
entity activation is omitted. Construct a pair using positive weights and the
same public graph/timing/provenance, changing only a downstream branch's
PersonOnly/DeviceOnly activation. Validate both scenarios with the existing
validator. Private definitions are used only by this counterexample evaluator.
A witness against identifiability is a useful negative finding, not failure.

H2: for the closed finite model below, a public-only declaration producer covers
every possible endpoint at the target tick, including positions during travel.
It generates existing PublicRegionProjection rows; F20 is reused unchanged.
An independent path enumerator must exactly match the generated sets in this
bounded family (stronger than the required subset guarantee).

H3: insufficient closure/time/unit information cannot create a complete
whole-support action claim. Closed positive controls must give nonempty,
nontrivial proper subsets of the declared spatial universe, avoiding an
always-unknown/all-regions solution.

## Registered finite model
One explicitly identified entity per model; no transfer between Person/Device.
Discrete integer ticks, horizon 0..6. Stable public region IDs a,b,c and transit
IDs ab,ac,bc. Each of the three forward edges a->b, a->c, b->c is independently
absent, duration 1, or duration 2: 27 graphs. Waiting one tick at every node is
explicitly allowed. No other dynamics or exits exist *under this model*.
For a duration-two edge, the intermediate tick occupies that edge's transit
region, never its destination. Transit cannot wait or branch.
All six catalogue IDs remain present, including unused transit regions.

For each graph use all 3 start nodes, all 6 inclusive windows [s,e] with
0 <= s <= e <= 2, and targets 3,4,5,6: 1,944 queries.
A historical interval means the entity was at the start node at an unknown
single tick within that interval. Union over ALL starting ticks is required;
continuous residence throughout the interval is explicitly not this semantics.
This interpretation is an opt-in producer contract, not a redefinition of all
PastStateCandidate intervals. Cases not certified to use it must remain unknown.

Public model source identity, entity, validity interval, publication tick,
declared node/edge/transit topology, explicit wait semantics and closed-world
assurance are inputs. Source identity/closure assurance are assumptions about
the supplied model, never independent certification of physical completeness.
Unavailable sources are rejected. Incomplete models emit empty/incomplete
declarations rather than computing a potentially unjustified finite outer set.
No inference reads a hazard definition, simulation realization, PRNG, or truth.

## Independent checks and F20 connection
Production dynamic reachability uses one-tick state sets; independent validation
recursively enumerates complete finite paths, recording intermediate transit.
A separate Python standard-library enumerator produces descriptive family totals
and the paired endpoint-only mutant diagnostic without calling production code.
Its totals are not claims that the C# implementation ran locally.

C# tests compare every registered query with an independent recursive oracle.
Connect declarations via the existing F20 projector and CLI. A total known
region-to-unit relation maps each node to one unit and each transit region to
two units (6 regions -> 9 units). Verify independently computed unit unions,
candidate identity, complete support containment and partial footprints.
Missing/partial unit maps must retain uncertainty. Actual reconstruction -> CLI
was already validated in F20 and is not reimplemented.
Test publication cutoff, exact entity/window/time constraints, unknown
interval interpretation, partial topology, input permutations/snapshots, and
transit handling. No candidate removal or negative-evidence update.

## Preregistered negative controls
- Same public envelope, different entity-activated private possibilities (H1).
- Endpoint-only propagation (use only interval end) must be refuted by a
  dedicated a->b duration-two, window [0,1], target 2 case.
- Treating a duration-two edge as duration one must be refuted.
- Missing closure/interval assurance must not produce IsComplete=true.
- Missing/partial region-unit map must not yield whole-support containment.
A positive control starting at c must return only c, not all catalogue regions.

## Failure and reporting rules
Zero missed reachable regions/units and zero false complete/whole-support claims
are allowed. A single violation fails the producer acceptance.
Any private/future input use fails the information boundary.
Any mismatch between independent oracle and producer fails exact bounded-family
acceptance, even if it is an overapproximation; distinguish this from unsafe loss.
Always-unknown/all-region output on closed positive controls fails usefulness.
Unrepresentable or contradictory semantics must be rejected or unknown, never
silently interpreted as an empty successful search.
Freeze cases and thresholds before execution. Fix implementation defects without
changing the protocol; disclose corrections. If a registered counterexample is
invalid or absent, report it and stop that claim rather than replace it silently.
Report all counts with denominators; 1,944 queries are exhaustive checks in one
registered family, not independent empirical samples. No empirical success
percentage or tuning to observed results.

## Deliverables and boundaries
New isolated producer source, dedicated tests, independent analysis script,
this protocol, and an interpreted result note. Reuse F20 types/projector/CLI.
Do not change F16-F20 algorithms, results, frozen inputs, schemas, dependencies,
CI, AGENTS, runner, scheduler, attempt ledgers, or execution approvals.
No real geography/data, new disaster physics, ranking, detection probability,
rescue recommendations, or negative-observation exclusion.
Existing GitHub CI validates C# where local .NET is unavailable; identify checks
actually run versus unexecuted. Separate review of the final head is required
before merge. This author may create the research branch/PR, not self-approve.
Further research scopes require a new decision.
