# Shared uncertain weft history: public contract boundary (FP #148)

**Status:** bounded reference tests and contract inventory; **not** a production joint-history solver, an efficiency ranking, or field validation. Scope: [#148](https://github.com/uobnnisiuk/footprint-sim/issues/148), [#142 mathematical note](weft-mathematical-foundations.md), [AR-37](https://github.com/uobnnisiuk/footprint-sim/issues/142#issuecomment-6820601968). This document is not a change to the conservative candidate-exclusion rules.

## Question and fixed semantics

When the subject Person P has missing history, can two *distinct, legitimately received* P–Q encounters constrain P more if they both refer to **one uncertain history of the same other Person Q**? A unary per-tick region set is not automatically an equivalent encoding of that shared-history condition. Do not treat an unknown relationship as absent; conversely, do not invent a shared-history relation because the observations mention the same entity.

The following are separate: Person P / Person Q / a held Device, observed time `k`, first receipt time `r`, rescuer query cutoff `c`, and target time `t`. The synthetic truth is used **only for checking** reference outputs; it is not an inference input. Both controls assume a declared finite region transition relation M, candidate histories consistent with M, a public mapping `b→U1,d→U2`, and no undeclared movement, occupancy or relation promoted to evidence. A cutoff admits a record only after the rescuer has received it, even if the record's observation was earlier.

## Negative control: unary constraints suffice in the finite reference model

This is a *reference computation*, not a claim about current `CrossThreadConstraintApplicator`'s completeness.

At successive discrete ticks 0,1,2, allowed region layers are `{a,c}`, `{b,d}`, `{e,f}`. The published edges are `a→b,a→d,c→d,b→e,d→f`. The two unary conditions on **P** are `P@0=a` and `P@2=e`. Enumerating whole P paths and retaining those satisfying both yields only `a→b→e`, hence the exact region at tick 1 is `{b}` (U1).

Forward reachability from the first constraint at tick 1 is `{b,d}`; backward continuability from the last constraint at tick 1 is `{b}`. Their intersection is `{b}`, identical to full-history enumeration. This is a standard Markov-path dynamic-programming identity *under finite, exact, unary and fully declared edges*, not a new theorem. If the actual model includes an incomplete movement catalog, interval uncertainty, undocumented dwell, or a constraint spanning several histories jointly, the premise has changed. The current applicator performs region intersection plus **forward** public-model propagation; this control does **not** assert it implements the backward pass or returns the reference answer on all inputs.

## Positive control: one shared Q-history selection (reference only)

Assume Q has *one* uncertain history chosen from `H_Q={(a,b),(c,d)}`, with the pairs at ticks k1 and k2. P's public movement M admits `(a,b),(a,d),(c,b),(c,d)`. A distinct, confirmed Person sighting of P at k1 says `P@k1=a`. Two separately warranted, received Person–Person co-location propositions say `P@k1=Q@k1` and `P@k2=Q@k2`. **Only if** these propositions legitimately bind to the *same Q history* can they be applied jointly.

- **Shared latent Q history / exact reference:** `P@k1=a` selects Q history `(a,b)` and forces P@k2 to `{b}` / U1. The synthetic true `P@k2=b` remains included; synthetic false exclusions: 0.
- **Unary marginal relaxation / exact reference:** first encounter sees Q's `{a,c}`, second sees Q's `{b,d}`, with no cross-time shared choice. With P@k1=a and the same M, P@k2 remains `{b,d}` / `{U1,U2}`. A spurious combination (Q at a under one history, at d under another) is retained.
- **Existing runtime:** there is no demonstrated public input path for passing a shared Q history variable together with both P–Q encounter propositions to a production Joint solver. **Not measured and not comparable as two methods given identical expressible runtime inputs.** The reference gap is about expressiveness, not measured superiority of a deployed algorithm.

The reference test in `RescuerEvidenceBoundaryTests.cs` directly enumerates these finite pairs. It **does not** call a production joint-history inference API or invent a new received record encoding the latent shared choice. This differs from the four-history/two-unit projection-order example in #142: no unit-first “straw man” baseline is portrayed as an implemented strategy.

## Actual code/receipt contract audit

| Boundary in present main | Supported public data / call | Shared Q-history limitation |
| --- | --- | --- |
| `RescuerEvidenceInputBuilder.Build` / `BuildDistinctOriginals` | Original private device-held observation becomes public rescuer evidence at the first eligible receipt. Availability is gated at the query cutoff, redelivery does not create an independent original. | Receipt of two observations alone is not proof they assert a single correlated latent Q history. |
| `CrossThreadConstraintDeriver.DerivePersonEncounterCoLocation(observation,targetEntityId)` | Requires `PersonPersonEncounter`, a Person target, `IsPersonEvidence`, and non-device-only observed regions; returns `CrossThreadConstraint`. | Converts `ObservedRegionIds` to a **target-P unary** `AllowedRegionIds`; relation/participants are metadata, not a Q-history join key. |
| `CrossThreadConstraint` | `TargetEntityId`, `ConstrainedTimeWindow`, `AllowedRegionIds`, `EvidenceBoundaryState`, `AvailableAtTick`, `SourceObservationId`, participants and provenance. | No field/solver contract identifying one Q-history alternative selected consistently across two P–Q encounters. |
| `CrossThreadConstraintApplicator.Apply/BuildAppliedWarp` | Filters by target, visibility, receipt tick and source observation; intersects historical **region** candidates, propagates using the public model, records standing/conflict/outer audit. | Not a complete joint P/Q solver. No unit-first projection, and no guaranteed exact backward filter. |
| `PastStateReconstructor.BuildEvidenceSet/Reconstruct` | Cutoff-scoped public evidence; generation/verification source selection and direct-source groups; region constraints over target time window. | Does not retain and quantify over the same Q latent history for two distinct P observations. |
| F14 logical roles / F20 search-unit projection / M1-E / #132 | Logical roles and same-source conjunction have contracts; partial/many-to-one unit maps and direct-P observation gates exist. | Logical conjunction **is not** statistical independence, and none demonstrates this particular cross-person shared-history solve. |

No new call, field, SDK, runtime model, or data source is added here. The contract test checks a real received `PersonPersonEncounter`: cutoff before reception has no usable constraint; after reception the result contains unary target/time/region and source/participants; duplicate deliveries are idempotent; uncertain Person evidence remains uncertain; a Device-only observation cannot be promoted to confirmed Person constraint. Existing tests cover broader source-ancestry grouping, alternatives and outer retention; this limited audit does not weaken them.

## Decision table / what is and is not measured

| Case | Inputs and same-E comparability | Candidate P@k2 region / public units | False exclusion | Unknown / conflict / outer | Work and status |
| --- | --- | --- | --- | --- | --- |
| Negative: enumerated P unary paths | Same declared M and two unary predicates | `{b}` / U1 at tick1 | None for its admitted finite paths | No unknown under complete reference assumptions | Finite enumerator; **reference** |
| Negative: forward ∩ backward | Exactly same M/predicates as preceding row | `{b}` / U1 at tick1 | None under the same assumptions | No unknown under complete reference assumptions | Finite set computation; **reference**, not current runtime |
| Positive: exact P–Q shared-history requirement | Full *hypothetical* legitimate Joint input | `{b}` / U1 | 0 for synthetic truth b | If warrant/receipt/role fails, no hard U2 exclusion | Finite enumerator; **reference only** |
| Positive: Q time marginals | Relaxed representation, drops Q shared choice | `{b,d}` / U1,U2 | 0 for synthetic truth b | Wider outer; unsupported correlation remains unresolved | Finite enumerator; **reference only** |
| Existing received public contract | Real `Build` plus `DerivePersonEncounterCoLocation`; shared Joint not expressible | Per-observation `AllowedRegionIds`, **not** a legitimate runtime Joint P@k2 answer | Not evaluable as Joint | Keep `outer/unknown/conflict` and public partial/unmapped units; never hard-drop U2 based on reference | Real API contract test; no same-input Joint head-to-head or workload benchmark |

Region counts are not interchangeable with unit counts; an incomplete mapping requires an unresolved marker rather than claiming a physical area was removed. The synthetic example gives no rescue minutes, costs or survival-rate estimates. No general theorem, efficiency bound or real-world model calibration follows from these finite tests.

## Safety / falsification conditions

A reference-only joint shrink is **withheld** if either encounter was not received by cutoff, is private/future, has ambiguous `Alternative` instead of a warranted joint conjunction, describes only a Device, has an unknown or contradictory Q-history link, or relies on unpublished movement/dwell. Two copies of one proposition with the same source ancestry add no new fact; two genuinely different valid propositions with one origin may logically both hold, without implying statistical independence. Contradictions remain conflicts, not evidence of absence. True paths and unobserved alternatives must not be removed from `outer` because a reference solver assumes more than the real API received. Unknown relations are neither established nor negated.

## Single bounded next discriminating simulation proposal

Before implementing a Joint engine, define **one** synthetic paired fixture in a future, separately reviewed scope: same public P/Q transition catalog, cutoff, exact receipt ledger, entity/time semantics, unit mapping, and synthetic truth; compare (1) the **same-input** unary P region constraint + accurate forward/backward reference and (2) a properly specified, received and source-audited P–Q shared-history constraint. Vary only whether the legitimate relation *actually conveys* a common Q history, and add a null control where the link is absent. Falsify any “information gain” if the disclosed evidence cannot distinguish the Q histories, if both methods coincide once given equivalent permitted information, or if the truth is excluded. Report candidate regions, unit set, unresolved/outer, logical entailment and inspected states separately. This is a proposal, **not** an approved API change or an experiment performed here.

For real-world follow-up, independently test whether identities can be resolved as Person rather than Device, whether repeated encounters can be linked to the same individual's history, whether observation/receipt timestamps and provenance are reliable, and whether a usable regional transition catalog exists. None of these assumptions is established by the synthetic reference.

Related unfinished work: AR-39 cyclic path responsiveness (separate, non-blocking to this contract study), AR-32/33, unknown relations/24縁. Keep #145's merged finite result, #142's math note and F22/UOB-5 preserved; do not silently reopen them.
