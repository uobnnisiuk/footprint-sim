# 24 paccaya as *question lenses* — bounded prototype

Scope [#160](https://github.com/uobnnisiuk/footprint-sim/issues/160), Work-Key `FP-24-CONDITION-SEARCH-LENSES-V1`. This is **not** a claim that the original conditional relations are physical encounter laws or that all 24 have been implemented. The original `Paṭṭhāna` Paccayuddeso/Paccayaniddeso is [transcribed here](https://www.aimwell.org/patthana.html); compare the [Abhidhammattha Saṅgaha, chapter 8](https://www.ruyimeditation.org/theravada/abhidhamma/paccaya-sangaha/) and [commentary list](https://orthodoxtheravada.org/lists/twenty-four-conditional-relations/). These are source/explanatory references, not validation of any Footprint mapping.

| ID | Pāḷi | Short original meaning/context | Proposed Footprint question | Mapping / implementation |
|---|---|---|---|---|
| 01 | hetu | Root condition | none established | unassessed |
| 02 | ārammaṇa | Object of consciousness | Is this received Device record evidence of Person P? | **limited analogy / executed** |
| 03 | adhipati | Predominance | none established | unassessed |
| 04 | anantara | Contiguity of mental moments | none established | unassessed |
| 05 | samanantara | Immediate succession | none established | unassessed |
| 06 | sahajāta | Co-arising phenomena | Is independent Person evidence applicable at the same tick? | **limited analogy / executed** |
| 07 | aññamañña | Mutual condition | none established | unassessed |
| 08 | nissaya | Support/dependence | none established | unassessed |
| 09 | upanissaya | Decisive support | none established | unassessed |
| 10 | purejāta | Pre-nascence | none established | unassessed |
| 11 | pacchājāta | Post-nascence | none established | unassessed |
| 12 | āsevana | Repetition | none established | unassessed |
| 13 | kamma | Action (kamma) | none established | unassessed |
| 14 | vipāka | Resultant phenomena | none established | unassessed |
| 15 | āhāra | Nutriment | none established | unassessed |
| 16 | indriya | Faculty | none established | unassessed |
| 17 | jhāna | Absorption factors | none established | unassessed |
| 18 | magga | Path factors | none established | unassessed |
| 19 | sampayutta | Association | none established | unassessed |
| 20 | vippayutta | Dissociation | none established | unassessed |
| 21 | atthi | Presence | none established | unassessed |
| 22 | natthi | Absence | none established | unassessed |
| 23 | vigata | Disappearance | none established | unassessed |
| 24 | avigata | Non-disappearance | none established | unassessed |

**02** is a prompt about the subject of the evidence (Person vs Device), not an ontological derivation from ārammaṇa. **06** is a prompt about same-tick eligibility for another genuinely received Person constraint, not a proof of spatial simultaneity or statistical independence from sahajāta. Do not infer that an unknown relation is absent. Neither prompt narrows a candidate set. Unlike #106, neither asks for an unconfirmed public path/branch; unlike #112, neither republishes the B→D fixed relation fixture. The index records the other 22 without turning them into constraints.

## Reproduce the finite synthetic contrast

```bash
dotnet run --project src/FootprintSim.Cli -- evaluate-condition-lenses \
  --fixture experiments/condition_lenses/fixture.json \
  --output results/condition-lenses
```

Output: `results/condition-lenses/condition-lenses.json`. Nine named cases include **A** (Device-only source and later Person receipt), **B** (two distinct same-tick Person originals with expected confirmed intersection `{b}` only when both are received by cutoff), and **C** (both question types, with duplicate receipt control). Point tick 5, cutoff 10, base regions `{a,b,c}`. The fixture includes never-received, eligible receipt 9, future receipt 11, and second observation at tick 6. Only `BuildDistinctOriginals` receipt-projected `ReceivedRecords` may be read for lens questions. Held or future records are *not* rescuer inputs. The case labels are counterfactual evaluator controls, not information available to a rescuer.

Each case exposes the old #106 diagnostic, four question modes (none, 02, 06, both), and the **same unchanged M1-D/E method row** per mode. A separate answer comparison shows changes between the no-answer and newly received conditions by method (outer/uncertain/conflict/ancestry count), with resolved/new question IDs. This explicitly separates information availability from method selection. Non-effects and worsening are not discarded. Candidate narrowing never follows from merely asking a question.

The model has no hazard-transition declarations, and the #106 diagnostic is therefore unavailable rather than evidence of absent relations. Same-tick Person constraint projection is provided by the existing builder/reconstructor. All values are **finite synthetic expectations and method outputs**, not measured rescue benefit, physical causality, proof of 24-paccaya effectiveness, or a validated real-world model. `PersonPersonEncounter` is a declared simulated person-evidence relation; a device-only original is not promoted. Unknown/conflict/outer statuses must be read as defined by the existing method. Legacy inclusive interval and traversal-enabled issues remain outside this point-tick experiment.

No new data/model/SDK, truth-side history or live-device access is involved. Do not commit generated `results/`. A required CI and independent code review remain separate from the scope approval.
