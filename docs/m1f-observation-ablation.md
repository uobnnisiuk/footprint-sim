# M1-F observation-family ablation

## Research question and boundary

M1-F asks how past-state reconstruction, public-relation traversal, and candidate verification change
when only the availability of existing S5 public observation families is varied. It is the first
synthetic step toward asking which real observations may reduce later search work and how much
information may be sufficient. `M1-F` and “observation-family ablation” are descriptive work labels,
not new causal concepts or final research terminology.

This stage does not add or modify an inference rule, a verification rule, a traversal rule, a public
edge, a truth probability, a target window, or an evidence-state meaning. It does not implement a
rescuer, search action, sensor calibration, continuous space, physical realism, or active probing.
The E2 gate remains **READY WITH EXPLICIT LIMITATIONS**.

## E2 gate and fixed synthetic assumptions

The baseline is E2 commit `49795e502bdd5c202a4a80825f6c82a274b9c85b`. The experiment retains:

- scenario-declared observation-to-constraint transformations and allowed regions;
- `Confirmed` as an asserted evidence-boundary record state, not an accuracy score;
- synthetic raw source records;
- the artificially complete west/east public topology;
- integer ticks and closed intervals;
- aggregate chronological public candidate-state layers;
- the existing target query, cutoff, local neighborhood, candidate identity, truth scoring, and
  source-independence rules; and
- no learned or calibrated transformation from observation payload to a constraint.

Consequently, a zero contribution in this experiment may mean that S5's coarse Region/tick
representation cannot express the added information. It is not a representation-independent claim
that the corresponding real observation is useless.

## Fixed and manipulated variables

The sole manipulated variable is the set of experiment-listed public observation families available
to the query. For every seed, the simulator generates the truth world exactly once. Every profile
shares that same truth realization, person/device trajectory, hazard realization, evidence condition,
public topology, region set, target query, cutoff, and random draw sequence. Evaluation records a
truth-world SHA-256 and a public-topology SHA-256 so the pairing can be checked.

The implementation uses an external experiment configuration that refers to unchanged S5. This is
Option B in operational form: it avoids changing S5 default bytes and behavior while adding an
experiment-only availability profile. Profiles are applied after the single simulation run, so a
profile cannot alter random realization.

Public topology is never ablated. Removing a traversal observation removes its observation and
derived traversal constraint; it does not remove any published hazard branch. “Full” means all
currently public, cutoff-available experiment families, not truth information, future evidence, or
private evidence.

## Observation-family inventory

These are ordinary descriptive groupings of current S5 sources. They are not asserted as a general
taxonomy of observations.

| Family | Raw source and subject | Information carried | Usage | Independence / ancestry | Active S5 conditions |
| --- | --- | --- | --- | --- | --- |
| `later-trace-a` | synthetic interaction observation; person-a with person-b; tick 24, available 25 | who, later time, fork east/west set, later-trace relation | generation | `source-a`; `record-family-a`; two same-source derivations count once for independence | all six |
| `later-trace-b` | synthetic interaction observation; person-a with person-c; tick 26, available 27 | who, later time, west alternative, uncertain state | generation | `source-b`; `record-family-b` | all six |
| `later-trace-conflict` | synthetic interaction observation; person-a with person-c; tick 25, available 26 | alternate-route conflict | generation | `source-conflict`; `record-family-conflict` | conflicting verification |
| `known-later-public-state` | public record about person-a held by person-b; tick/available 27 | coarse terminal-state set and later-state relation | traversal | `source-known-later-state`; `later-state-record` | all six |
| `public-environment-west` | truth-independent public record about person-a held by person-b; tick/available 27 | west assertion and verification relation | verification only | independent `environment-record-west` | distributed and conflicting verification |
| `shared-record-check` | public record about person-a held by person-b; tick/available 27 | fork candidate set | verification only | shares `record-family-a`; not independent of `later-trace-a` | shared-ancestry verification |
| `independent-conflicting-check` | public record about person-a held by person-c; tick/available 27 | east conflicting assertion | verification only | independent `independent-record-conflict` | conflicting verification |
| `device-fix` | public record about device-d; tick 26, available 27 | device terminal-west assertion, uncertain state | verification only, device-bound | `device-record`; never promoted to person evidence | device-only evidence |

`delayed-check` and `private-check` are not manipulable families. They remain, respectively, future
and private sources unavailable to the query under every profile. The tick-1
`syntheticAccessRecord` remains a fixed public-base observation without an M1-E constraint usage
role.

### Primitive information dimensions

The inventory retains subject, entity kind, observed time, available time, location/state set,
relation or declared transformation, evidence state, visibility, operation usage, direct source, and
source ancestry. Phase one switches existing source families as units. It does not independently
remove `who`, `where`, `when`, or relation fields from one record; such field-level experiments would
need a separate representation contract. In particular, current S5 cannot express continuous
position precision, observation range, calibrated error, floor/height, area/volume, channel
reliability, or realistic observation transport.

## Raw observation and derived-constraint coupling

For a disabled family the experiment removes, as one availability unit:

```text
raw ObservationRecord or InteractionObservation
  -> source-observation availability
  -> derived public constraint
```

For an enabled family, a derived constraint is usable only if its source observation exists, is
public, is available by the inclusive query cutoff, and remains query-usable. No constraint is
created from a disabled source. Disabling a source produces unavailable knowledge, not an `Absent`
world proposition. Verification families remain verification-only and cannot mutate generation or
traversal. Device evidence remains device evidence. Multiple derivations with one ancestry remain one
source group for independence.

## Preregistered profiles

All following conditions are core conditions and use seeds 0 through 199:

1. `full`;
2. `none`, retaining only fixed public-base records;
3. one `without-<family>` profile for each of the eight families;
4. one `only-<family>` profile for each of the eight families; and
5. three selected combinations fixed before the full result is inspected:
   - `pair-generation-and-traversal`: `later-trace-a` plus `known-later-public-state`, selected to
     separate generation information from fixed-topology traversal;
   - `pair-generation-and-independent-verification`: `later-trace-a` plus
     `public-environment-west`, selected to preserve generation with an independent verification-only
     record; and
   - `pair-generation-and-shared-ancestry-verification`: `later-trace-a` plus
     `shared-record-check`, selected to audit the no-double-counting boundary.

There are 21 core profiles, 4,200 paired profile runs, and 12,600 method runs. There are no
exploratory profiles in this first experiment. The full 256-subset space is not searched because this
stage prioritizes single-family removal/presence and three structure-motivated pairs over an
uninterpreted exponential search.

## Metrics fixed before result inspection

Safety is truth containment and false exclusion. Search-space behavior is historical candidate-state
count and reduction from the three-region target layer. Evidence behavior includes query-usable raw
records and constraints, grouped source counts after shared-ancestry grouping, redundant same-source
derivations, conflicting/uncertain/unavailable evidence, unresolved/conflicted candidates, and all six
candidate-verification dispositions. Work is separately reported for candidate generation,
candidate verification, forward traversal, backward traversal, public relations inspected, evidence
records inspected, and source groups inspected.

The current provisional search-burden proxy is the historical candidate-region count. It is exactly
the method's candidate count in this Region-level world. The six declared searchable regions are also
recorded as context; no time, people, dog, area, volume, or rescue-cost value is invented.

Every profile is compared with Full by paired seed. Deltas are Full-relative for containment, false
exclusion, mean candidates, reduction, each work component, unresolved candidates, and conflicted
candidates. A discrete threshold is reported only if the observed profile results establish one; no
knee statistic is forced.

### Candidate minimal subset rule

A profile is baseline-equivalent only if, for every paired seed:

- every method has the same candidate-region set, containment value, and false-exclusion value as
  Full; and
- reconstruction candidate dispositions, unresolved/conflicted candidate counts, and
  candidate-verification dispositions match Full.

Work may differ and is reported separately. Candidate minimal profiles are the baseline-equivalent
profiles with the fewest enabled families among the 21 preregistered profiles. This is deliberately
stricter than “same containment”. It is not an exhaustive or Footprint-general minimum observation
set. If several tested profiles tie, all are retained.

## Preregistered failure conditions

The experiment is invalid if disabling a family leaves its derived constraint usable; a constraint
appears without its source; truth or evaluation-only data enters a method; verification becomes
generation or traversal; device evidence becomes person evidence; missing becomes absence; shared
ancestry is counted as independent; a profile removes public topology; one seed produces different
truth, trajectory, hazard, or random realization across profiles; candidate identity depends on
truth; or output is non-deterministic.

An apparent improvement after removing an observation is not interpreted as evidence that fewer
real observations are preferable. Redundancy, a conflicting synthetic fixture, declared
transformations, and coarse representation remain competing explanations.

## Results

The following result was obtained only after the definitions and failure conditions above were
fixed. All 21 profiles completed the same 200 seeds: 4,200 profile runs and 12,600 method runs. Every
seed had exactly one truth-world identifier and one public-topology identifier across its profiles.

### Full baseline

| Method | Mean candidates | Containment | False exclusions | Reduction | Generation work | Verification work | Forward | Backward |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| backward traversal | 2.000 | 100% | 0/200 | 33.33% | 0 | 0 | 4.000 | 4.000 |
| past-state reconstruction | 2.165 | 100% | 0/200 | 27.83% | 5.330 | 0.835 | 0 | 0 |
| reconstruction with local traversal | 2.000 | 100% | 0/200 | 33.33% | 5.330 | 0 | 4.000 | 4.000 |

Full contained a mean 8.165 recorded/public/cutoff-available observations, of which 7.165 belonged
to experiment families; a mean 4.000 distinct observation IDs were actually used as constraint
sources. Mean query-usable constraints were 3.165 generation, 0.835 verification, and 1 traversal.
Shared-ancestry grouping produced a mean 3.830 source groups and retained 1.170
same-ancestry derived-record excess. Mean conflicting, uncertain, and unavailable evidence counts
were 0.330, 1.165, and 0.330.

Full verification dispositions totaled 34 independently supported, 34 independently refuted, 99
conflicting, 66 unresolved, 68 shared-ancestry-only, and 132 unavailable candidate results. All used
source observations resolved. The Full method outputs reproduce the E2 200-seed candidate,
containment, reduction, and traversal result; M1-F changes only experiment-side availability.

### Single-family removals

All removals retained 100% containment and zero false exclusions in all three methods. Backward and
local candidate counts stayed 2.000, and local forward/backward traversal stayed 4/4, because the
public topology was fixed. The table therefore shows the reconstruction and work effects relative
to Full.

| Removed family | Δ mean reconstruction candidates | Δ reduction | Δ generation work | Δ verification work | Principal observed effect |
| --- | ---: | ---: | ---: | ---: | --- |
| `device-fix` | 0 | 0 | 0 | -0.165 | device-only unresolved results became unavailable; no person candidate changed |
| `independent-conflicting-check` | 0 | 0 | 0 | -0.165 | removed the 99 conflicting verification dispositions in its 33 active seeds |
| `known-later-public-state` | 0 | 0 | 0 | 0 | removed one traversal evidence/relation inspection per run; actual topology traversal remained 4/4 |
| `later-trace-a` | +0.835 | -27.83 points | -0.165 | 0 | lost broad confirmed generation support; target layer remained all three regions; shared verification became independent because the overlapping generation ancestry was absent |
| `later-trace-b` | 0 | 0 | -1.000 | 0 | candidate regions/dispositions unchanged, but mean unresolved candidate count fell by 1.165 |
| `later-trace-conflict` | -0.165 | +5.50 points | -0.330 | 0 | removed the conflict-driven alternate candidate in 33 seeds; no containment loss in this truth distribution |
| `public-environment-west` | 0 | 0 | 0 | -0.335 | removed 34 independent support and 34 independent refutation dispositions; 68 became unavailable |
| `shared-record-check` | 0 | 0 | 0 | -0.170 | removed 68 shared-ancestry-only dispositions; 68 became unavailable |

The apparent improvement from removing `later-trace-conflict` is a scenario/rule interaction, not a
recommendation to suppress conflicting real observations. Current reconstruction intentionally
retains alternatives in the presence of conflict. No family removal improved reduction while
hurting containment; containment did not vary at all.

### Single-family presence and selected combinations

With no experiment family, reconstruction retained all three target regions (0% reduction), while
fixed-topology backward/local methods still returned two regions (33.33% reduction). Each
verification-only family by itself and `known-later-public-state`, `later-trace-b`, or
`later-trace-conflict` by itself also left reconstruction at three candidates.

`later-trace-a` alone produced two reconstruction candidates and 33.33% reduction with 100%
containment. Each selected pair containing `later-trace-a` also produced two reconstruction
candidates:

| Profile | Reconstruction candidates | Containment | Reduction | Verification observation |
| --- | ---: | ---: | ---: | --- |
| only `later-trace-a` | 2.000 | 100% | 33.33% | none |
| generation + traversal | 2.000 | 100% | 33.33% | none; traversal observation added inspection work only |
| generation + independent verification | 2.000 | 100% | 33.33% | 67 supported and 67 refuted candidate dispositions across active runs |
| generation + shared-ancestry verification | 2.000 | 100% | 33.33% | 68 shared-ancestry-only dispositions; no independent double count |

An interaction is visible: `shared-record-check` is shared ancestry only while `later-trace-a` is
present, but becomes independent of generation when that family is removed. Also,
`later-trace-conflict` expands rather than narrows the candidate set when combined with confirmed
generation evidence. These effects are retained rather than compressed to an additive family score.

### Threshold and candidate minimal profiles

No smooth threshold or knee can be estimated from these discrete profiles. The appearance of
`later-trace-a` gives a clear S5 rule boundary from three to two reconstruction candidates, but the
conflicting family can broaden the set again; this is not treated as a general observation threshold.

All 21 profiles were safety-equivalent to Full because they retained 100% containment and zero false
exclusions. Under the stricter preregistered baseline-equivalence rule, only `full` and
`without-known-later-public-state` matched candidate sets, candidate dispositions, unresolved and
conflicted counts, and verification dispositions for every seed. The unique fewest-family candidate
among the tested profiles is therefore `without-known-later-public-state` with seven families.

This candidate is not a globally minimal S5 subset: untested combinations may use fewer families.
Its apparent redundancy also depends on the complete synthetic public topology, which performs the
same four forward and four backward transition steps without the later-state traversal constraint.
Under a safety-only rule, even `none` tied Full; candidate reduction and evidence semantics are why
the stricter rule is necessary.

### Contribution summary and search-burden proxy

`later-trace-a` has the strongest positive contribution to reconstruction reduction in the tested
single removals. `known-later-public-state` has zero candidate or verification contribution and only
adds evidence/relation inspection work under the current complete topology. `later-trace-b` changes
unresolved metadata and work but not candidate regions. The four verification families affect
verification dispositions and work without mutating generation, confirming the E1 boundary.

At the current Region granularity, historical candidate count is the provisional search-burden
proxy: Full reconstruction is 2.165, None is 3.000, `later-trace-a` alone is 2.000, and Method C is
2.000 in every profile due to fixed topology. The broader searchable-region context remains six
regions; no physical search cost is inferred.

### Output and schema

The experiment has a dedicated output schema version 1 with machine-readable per-run observation
existence, public visibility, cutoff availability, query-source usage, family presence, source
resolution, candidate/verification dispositions, method work, transition identifiers, paired world
identifiers, aggregates, Full-relative deltas, and family contributions. Neutral telemetry remains
schema version 4 and is not changed by M1-F.

## Limitations and next steps

This stage remains conditional on a complete synthetic public topology and declared transformations.
The next observation-limit stage may decompose families into subject/time/location/relation
dimensions, vary availability timing without changing truth, and repeat the test after separate
representation-validity changes. It must not reuse a result here as a calibrated real observation
requirement.

The future rescue-performance path remains: translate candidate regions into a defensible search
burden, define handoff conditions to existing rescue means, model rescue action separately, then test
action -> new observation -> inference update and case dependence. None is implemented here.

Evidence presence, combinations, independence, conflict, uncertainty, and non-observation may later
be compared with Buddhist methodological sources. This experiment does not establish a semantic
identity and introduces no Buddhist term into production naming.
