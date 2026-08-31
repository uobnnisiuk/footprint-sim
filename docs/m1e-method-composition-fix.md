# M1-E method-composition correction

## Purpose and status

This document records a narrow correction to the M1-E comparison methods. It does not add a new
inference rule. The correction separates evidence use for candidate generation, candidate
verification, public-transition traversal, and evaluation. These are ordinary operational roles,
not causal relations or new research terminology.

The corrected 200-seed S5 comparison defined below is preregistered before its full evaluation. The
original M1-E result remains part of the research history.

## Original observed result and defect analysis

The original S5 run reported 94.5% containment and 11/200 false exclusions for reconstruction with
local traversal. The affected seeds were 24, 36, 78, 90, 96, 120, 132, 138, 150, 162, and 180. In
every case the truth-scored historical region was `fork-east`, reconstruction produced
`fork-east|fork-west`, and the former local method produced only `fork-west`.

Two method-composition defects make that result unsuitable as a measure of pure local traversal:

1. A verification-only, Confirmed, west-limited constraint was passed to the M1-D tracer. Its
   intersection changed the candidate set from east|west to west even though M1-E defines
   verification as a separate assessment of an already-generated candidate.
2. The one-tick padding selected only the target state in S5. The local tracing session therefore
   performed no forward or backward public-transition traversal. Its recorded steps were constraint
   applications, not transition traversal.

The public model contains east-consistent paths for all eleven seeds. Missing east topology was not
the direct cause.

## Evidence-use boundary

Every available constraint is classified for the current query as one or more of:

- candidate generation;
- candidate verification;
- traversal; or
- evaluation only.

S5 declares three disjoint definition lists. A constraint is not made available to every operation
merely because it is present in the public evidence set. Candidate verification remains informative:
an east candidate may be recorded as independently refuted without being deleted from the generated
candidate collection or silently passed into traversal pruning.

The current scenario contract also rejects a direct source reused across these lists. No S5 source
requires multiple roles. Supporting legitimate multi-role use would require an explicit
per-constraint declaration and rationale; this correction does not infer permission from source
identity or add a general policy system.

## Corrected comparison preregistration

The following definitions are fixed before the corrected 200-seed result is inspected.

### Method A: backward traversal

Method A uses the existing M1-D tracer over the public candidate-state model, public hazard envelope,
and explicitly declared traversal constraints outside the target window. It uses no truth parent,
realized transition, private trajectory, generation-only constraint, or verification-only constraint.

### Method B: past-state reconstruction

Method B constructs candidates at the target window from the explicitly declared generation
constraints. It then reports verification dispositions from the separately declared verification
constraints. Verification does not retroactively change the generated candidate identity or region
set.

### Method C: reconstruction with local traversal

Method C starts with Method B's target candidates. Its local state set is defined structurally as:

1. every public candidate state overlapping the target window;
2. every public candidate state overlapping the configured padding window; and
3. the immediate preceding and immediate following public candidate state in temporal order, when
   present.

The third rule prevents a named traversal method from degenerating to a one-state constraint pass
when public states are sparsely sampled. It is independent of the observed truth and does not tune a
tick distance to the S5 result. In S5 it selects the preregistered intervals 8–10, 13–14, and 18–32.

Method C supplies only explicitly declared traversal constraints to the tracer. Verification-only
evidence is not supplied. The existing M1-D public-transition consistency rule may remove a target
candidate only when the selected public local states and a published public transition path make it
inconsistent. Missing public transitions remain unavailable knowledge rather than proof of
impossibility. Both forward and backward directions are applied within the selected state set.

The existing `Steps` field is retained for compatibility. New work fields separately count candidate
generation, candidate verification, actual forward transition traversal, and actual backward
transition traversal. A directional step counts as actual transition traversal only when at least one
public transition reference was used. Transition references are deterministically derived from public
branch metadata; realized truth transition identifiers are not inputs.
Generation work counts setup, inspected generation source groups, and constructed candidates;
verification work counts distinct eligible verification groups. These are deterministic operation
counts, not elapsed time or evidence quality scores.

### Expected audit behavior, not a success target

- The west-only verification record must remain visible in each candidate's verification disposition.
- It must not appear as a Method C traversal constraint.
- S5 Method C must select more than the target state and record at least one actual public transition.
- The eleven former exclusions must not recur for the same verification-reuse reason.
- No containment percentage, candidate count, reduction, or step advantage is required. Any remaining
  false exclusion or added work is reported as observed.

Failure conditions include no actual transition despite the corrected neighborhood, verification
appearing in traversal usage, loss of deterministic output, a truth-dependent public method result,
or a new false exclusion caused by public-transition consistency.

## S5 source-observation lineage

The S5 generation constraints already resolve to interaction-observation records. This correction
adds ordinary observation records for the coarse later-state traversal source and the independent
west verification source, and records whether every constraint's source-observation identifier
resolves. An unresolved reference is reported; it is not automatically treated as invalid or absent.
On the simulation side, the observation lifecycle event retains the source occurrence reference and
the observation identifier; on the public side, the constraint retains that observation identifier.
The truth-side source occurrence is used only to audit the synthetic fixture and is not passed to the
reconstructor or tracer.

The independent west record is a deliberate truth-independent synthetic false-positive fixture used
to exercise verification dispositions. `Confirmed` is the fixture's evidence-boundary label, not a
claim of calibrated accuracy. It can therefore disagree with the simulated branch. This artificial
choice must not be reused as a validated observation model or as input to minimum-observation-set
research. Its realism, error semantics, and calibration remain issues for artificial-world validity
work.

## Telemetry

Telemetry schema version 4 preserves version-3 field meanings and adds method IDs, evidence usage,
source-observation resolution, candidate regions before and after, local state membership, separate
generation/verification/forward/backward work, public transition references, and persisted M1-E
tracing steps. Truth scoring remains outside public method telemetry.

## Corrected synthetic experiment result

After the preregistration above was fixed, S5 seeds 0 through 199 were evaluated with the three
methods (600 method runs; the complete CLI evaluation also ran eight search strategies for 1,600
strategy runs). The corrected method results were:

| Method | Mean candidates | Containment | False exclusions | Mean reduction | Existing `Steps` | Generation work | Verification work | Forward transition work | Backward transition work |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| Backward traversal | 2.000 | 100% | 0/200 | 33.33% | 4.000 | 0 | 0 | 4.000 | 4.000 |
| Past-state reconstruction | 2.165 | 100% | 0/200 | 27.83% | 6.165 | 5.330 | 0.835 | 0 | 0 |
| Reconstruction with local traversal | 2.000 | 100% | 0/200 | 33.33% | 9.000 | 5.330 | 0 | 4.000 | 4.000 |

Method C inspected one traversal evidence record and nine tracer step/relation records per run. Its
four deterministic public transition references cover both release-to-fork and fork-to-terminal
branches. The local traversal removes the topology-inconsistent `alternate-route` candidate in the
conflicting-evidence condition; it does not use verification-only constraints for that reduction.

All eleven formerly failing seeds retained `fork-east|fork-west`, recorded four forward and four
backward transition steps, and contained the truth-scored east region. The east candidate still has
the separately reported `IndependentlyRefuted` disposition under the truth-independent west fixture.
For comparison, seed 0 has truth west but the same east|west public Method C result; truth is not used
to select the output.

Across the 200 query records, 633 available constraint records were used for generation, 167 for
verification, and 200 for traversal. All generation and traversal source-observation references
resolved. One hundred of the verification references resolved; 67 constraint-only synthetic
verification references remain explicitly unresolved and are not silently treated as absent or
invalid. Candidate verification totals were 34 independently supported, 34 independently refuted,
99 conflicting, 66 unresolved, 68 shared-ancestry-only, and 132 unavailable dispositions (counts are
per candidate, not per seed).

The correction removes the established method-composition false exclusions in this synthetic run.
It does not prove that verification refutation should never affect later decisions; such a decision
rule is intentionally not implemented here.

## Interpretation and roadmap

The corrected result remains an uncalibrated synthetic S5 comparison. It does not establish real SAR
performance or a generally correct inference policy. The next stages remain: decide the inference-rule
boundary, validate artificial-world representation and observation lineage, add complexity in
separate representation/observation/causal/physical/operational dimensions, study observation limits
and minimum observation sets, then study search workload, handoff to existing rescue means, rescue
actions, new-observation feedback, and case dependence.

Generation is not verification, implication is not its converse, and missing evidence is not absence.
Those boundaries may inform later Buddhist-methodology comparison, but no Buddhist term is introduced
into production code by this correction.
