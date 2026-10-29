# M1-F20 public location and action-footprint mapping

Tracking: #52. Baseline: `282b6e993d0ccbfa5fba774266d5af7e989e5fea`.
This document is committed before the F20 implementation and its new cases are executed.
F19 is complete; its protocol, input hashes, results and handoff remain unchanged.

## Question and first-stage scope

Can a public-only contract carry historical candidate uncertainty through declared temporal
transitions to possible current regions, spatial search units and action footprints without
confusing candidate count, location count, coverage and detection?

This first stage is a deterministic contract/vertical-slice validation using analytic cases,
not a new population experiment or a calibrated search-performance study. Expected set results
are algebraic design requirements known before execution, not empirical discoveries.
No F19 seed sweep is reused as an independent sample or repeated to fill an implementation queue.

## Public input contract

1. Query identity, target entity identity, target tick and evidence-availability cutoff.
   The cutoff is nonnegative and no later than the target tick. Candidate historical windows
   must end no later than the target tick. This does not claim a historical candidate was
   available as evidence at its historical time: it is a public reconstruction supplied by
   the caller at the query cutoff.
2. Existing `PastStateCandidate` records, all from that query and the same target entity.
   Retain candidate IDs, historical region/window, disposition and generation/source metadata.
   No hidden truth, realized trajectory, ground-truth evaluator or detected-location input.
3. Declared public region IDs and spatial search-unit IDs. These are distinct namespaces.
   Units may represent synthetic buildings/floors/rooms/areas, but no calibrated geometry is
   introduced. Many candidates may map to one unit and one region may map to multiple units.
4. Public transition rows keyed by `(historical region, start tick, end tick, target tick)`.
   Each lists possible target regions, a source reference, availability tick and `IsComplete`.
   A row refers to the ENTIRE inclusive historical window, not just a selected endpoint.
   Rows are explicit conservative model declarations, not a new propagation algorithm.
   An old region is not silently carried forward: even identity/stay is a declared row.
5. Public unit-mapping rows keyed by `(current region, target tick)`, with known possible unit
   IDs, source reference, availability tick and `IsComplete`.
6. Declared actions at exactly the query target tick. Each has an ID, a known footprint of
   unit IDs, source reference and availability tick. An action is not an observation and is
   not executed by this component.

Rows must be available by cutoff; reject future rows rather than silently consuming them.
Reject malformed identifiers, duplicate candidate IDs, duplicate rule keys/action IDs,
unknown referenced regions/units, reversed/negative windows, or foreign query/entity candidates.
Complete rows must have at least one alternative. Empty incomplete rows represent unknown,
not a proof that a candidate has no possible location. Shared source IDs are permitted and
never counted as independent evidence. Incomplete or contradictory scientific assumptions
cannot be repaired by reading private truth. Input lists and original candidates are not mutated.

## Projection and uncertainty semantics

For each candidate select the exact-window/target-tick transition row. With no matching row,
retain the candidate and mark `transition-unmapped`. With a partial row, retain known regions
AND `transition-incomplete`. Map every known region through the exact target-tick unit row;
missing or incomplete rows add unresolved location alternatives rather than deleting the region.
Return all candidate identities even if all locations are unresolved or dispositions conflict.

Known region and unit unions are sorted/distinct; candidate records are sorted by ordinal ID.
Distinct candidate count, known-region count, known-unit count and unresolved-candidate IDs are
separate output concepts. An empty known set plus unresolved alternatives is NOT zero search space.
Preserve the contributing transition/mapping source references and existing source ancestry;
never interpret merging duplicate spatial units as merging independent evidence or certainty.

For an action, report:
- Candidate IDs whose known unit support intersects its declared footprint.
- Candidate IDs whose ENTIRE represented support is inside that footprint, only when the
  candidate has nonempty known units and no unresolved alternatives.
- Known intersecting units, separately from both candidate lists.

The second relation is a conditional set-inclusion claim under the declared model, NOT guaranteed
presence, detection, successful rescue, or a negative-observation exclusion rule. Unknown alternatives
may lie inside OR outside an action footprint; report them as unresolved, not outside. Different actions
may overlap. Do not rank actions, infer detection probabilities, costs, personnel or travel time.

## Registered analytic validation cases

- Two different historical candidates with complete transitions to one current region/unit:
  two candidate IDs, one known unit; the containing action lists both without merging IDs.
- One candidate with a complete two-region/two-unit image: a one-unit action intersects but
  does not contain all possibilities; a two-unit action does.
- Candidate with no matching transition, wrong target tick or partial transition:
  retain it, preserve any known support, and never claim whole-support containment.
- One known current region with missing/partial unit mapping: keep both known units and
  unresolved alternatives; all-unknown is not an empty successful search result.
- Candidate historical interval `[1,3]`: a row for `[1,1]` must not apply; an exact `[1,3]` row may.
- Permute candidates, rules, catalogues, action footprints and ancestry: serialized output
  is identical. Repeat identical input with identical output and verify input preservation.
- Reject future rows, foreign query/entity candidates, duplicate/invalid IDs, unknown
  references, invalid temporal ranges and a complete row with no alternatives.
- Empty candidate input produces empty support/coverage without vacuous positive containment.
- The code's public signature cannot accept truth contexts. Private-truth changes outside
  that signature cannot influence projection. Existing F16-F19 source/outputs are untouched.

CI runs the existing build/test/format/scenario checks plus these new focused tests. This initial
stage is complete only after the production projector exists, tests actually run, and a separate
reviewer checks its full diff and exact-head CI. Report unexecuted checks as unexecuted. There is
no required empirical success percentage or opportunity to tune a dataset after inspection.

## Follow-on work within this stage

The initial projector consumes explicit public transition rows. After review, add an adapter from
existing public past-state results/transition envelopes, preserving their temporal/lineage semantics,
and a deterministic evaluation/report route for the mapping examples. These are real implementation
steps; passing projector unit tests alone does not complete them. An adapter must not certify a
partial envelope as complete or silently discard unmapped alternatives. Existing public-input types
must be reused rather than replaced with truth-bearing objects.

Only reviewed capabilities may be used by the unattended runner. It currently edits existing files
only, so the first source/test/doc files are bootstrapped through a separately reviewed cloud PR.
No placeholder files, unrestricted write access or guard weakening is introduced. Later bounded
existing-file changes can use the normal approved queue. F19 state remains `f19_complete`; this
separate F20 document/Issue records successor progress rather than falsifying the old handoff.

## Interpretation boundary

These checks establish a representation contract, not correctness of an arbitrary supplied public
model. Sound outer coverage in reality additionally requires justified transition and spatial models.
A model can declare completeness incorrectly; this component cannot validate that from identifiers.
Action-generated observations, posterior updates, resource/cost calibration, operational dispatch,
real geographic coverage and real-data validation remain outside this stage.

## Execution status

Preregistered design. Implementation, executable validation and separate review are pending.

## Explicit-declaration adapter design resolution

The adapter source is caller-supplied `PublicRegionProjection` declarations for the entire exact
inclusive historical window. Query identity, entity and evidence cutoff come from the supplied
`PastStateReconstructionResult.Query`; all candidates retain their identities and metadata.
Declarations retain their source, availability and completeness flags. Missing or partial maps
remain unresolved, and the existing projector validates all rows before the adapter snapshots them.
No implicit stay transition or automatic completeness declaration is introduced.

The proposed focused test connects an actual `PastStateReconstructor.Compare` result from a
public two-region model with empty observation/constraint sets to the adapter and projector.
It checks many-to-one support, declaration-list snapshots and basic query/time rejection.
This is a limited integration case, not the complete registered acceptance suite: a connected
nonempty-evidence case through the CLI and broader adapter permutation/metadata checks remain.
No scripts, build, tests, format checks, scenarios or CLI commands were executed while preparing
this read-only proposal. Separate review and executable validation remain pending.

An automatic exact-window completeness producer from `PublicHazardEnvelope` remains unavailable.
Synthetic completeness must be explicitly justified by the declared model; this adapter cannot
certify arbitrary caller declarations, real geographic coverage or rescue performance.
The original preregistration and F16–F19 results remain unchanged.
