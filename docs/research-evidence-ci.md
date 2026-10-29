# Fixed F19 research evidence CI

The `Footprint F19 research evidence` workflow provides a bounded execution and artifact-retention route for the registered F19 study. It is defined in [research-f19.yml](../.github/workflows/research-f19.yml) and invokes [run_f19_research_validation.py](../.github/scripts/run_f19_research_validation.py). It does not generate code, call a model, consume an Issue-provided command, or start the next research study. Ordinary build/test CI and independent review remain separate requirements.

## Fixed scientific scope

The workflow checks out the exact PR head SHA, or the event commit on main/manual dispatch, and separately checks out the completed F18 baseline `77611bc52976178857ac442be6e07a9caa1a2a9d`. The current source must descend from F19 registration `9039417314df4c11fe5bc088825bf5f25eaf4d98`. Both source checkouts must match their expected commits and remain clean before and after validation. Current and baseline outputs are kept outside both checkouts.

All experiment commands use the registered seed set `0..199`. The helper has no seed, arm, profile, experiment-path, command-text, model, or credential override. Its only arguments identify the baseline checkout, exact expected current source SHA, and empty output directory.

| Stage | Fixed operation |
|---|---|
| Environment and regressions | Environment check; Python automation/verifier tests; current and baseline Release builds; current .NET tests and format verification |
| F16 reference replay | `evaluate-common-world-field-combination` using the registered F16 experiment, on baseline and current source |
| F17 reference replay | `audit-common-world-plateau-domain` using the registered F17 protocol and F16 baseline experiment, on both sources |
| F18 reference replay | `evaluate-candidate-interval-width` using the registered F18 protocol and F16 baseline experiment, on both sources |
| F19 repeatability | `evaluate-candidate-density` using the registered F19 protocol and F16 baseline experiment, twice on the same current clean source |
| Independent validation | The independent Python candidate-density verifier, using the current F16/F18 reference rows and the first F19 run |

The reference comparison requires exactly 18 artifacts for each source: eight F16, four F17, and six F18 artifacts. Missing, extra, renamed, non-file, or symlinked reference artifacts and unregistered campaigns fail validation. The known immutable F16 and F18 row hashes are checked on both sides, so matching but altered reference rows do not establish compatibility. Each reference manifest must identify its exact expected execution SHA and `dirtyWorkingTree: false` before metadata normalization; missing, incorrect, or dirty provenance fails. Scientific output must then remain byte-identical apart from the validated execution-source value; unrelated JSON whitespace or serialization differences are not erased by parsing and re-encoding the entire document.

F19 comprises 200 paired worlds, three arms (`point-baseline`, `half-missing`, `full-support`), and eight profiles: 4,800 primary evaluations per full run. The second run is a reproducibility check of the same worlds. F16/F17/F18 references and F19 reference/control calculations are not additional independent samples or additional primary worlds. Both the main report and independent verifier must establish the exact registered counts; partial, inflated, noninteger, or missing counts fail.

The following six F19 files must match byte for byte across runs `a` and `b`, including their manifests because both runs use the same source:

- `candidate-density.json`
- `candidate-density-runs.jsonl`
- `candidate-density-profiles.csv`
- `candidate-density-conditionals.csv`
- `candidate-density-crosswalk.csv`
- `candidate-density-manifest.json`

Each F19 run must contain exactly this six-file set of regular files. Extra, missing, renamed, or symlinked entries fail; evidence-hash collection also rejects symlinks and nonregular entries. The independent verifier must report both `allVerified` and `cleanSourceManifestVerified`. The expected execution SHA is supplied explicitly. A process finishing, a green unit-test count, or the presence of output files alone is insufficient.

## Execution boundary

GitHub execution uses an ephemeral Ubuntu runner, pinned checkout/setup/artifact actions, `contents: read`, and `persist-credentials: false` for both checkouts. The research subprocess starts with a cleared environment and a fresh temporary home. The helper further allows only the environment needed for its tools and disables global/system Git configuration and interactive Git authentication. It receives no owner credentials or configured repository secrets. Restore still uses the existing .NET dependency configuration; this is not an offline build claim.

The helper is **not an OS sandbox**. Clearing environment variables alone does not hide files, sockets, or credentials available to an operating-system account. Local execution therefore requires an outer credential-free filesystem/process environment prepared by the operator. Do not invoke candidate builds, tests, or this helper in the credential-holding owner checkout and assume that its environment filtering provides isolation. The migration's separate isolated validation environment supplies that boundary; the unattended Local AutoDev runner does not run candidate code locally.

The workflow has a 30-minute job timeout. Its concurrency group is per GitHub ref and does not cancel an existing run to begin another. It uses ordinary `pull_request`, main `push`, or `workflow_dispatch` events; dispatch has no user-defined inputs. It does not use `pull_request_target`, write-token publication, generic Issue shell execution, model calls, API fallback, credit top-ups, or permission escalation. This workflow does not schedule additional Windows model slots.

## Evidence and retention

The artifact is named `footprint-f19-<source-sha>-<run-id>-<run-attempt>`. It contains the command logs, test results, both sources' F16/F17/F18 outputs, both F19 runs, independent-verifier outputs, and `evidence.json`. The evidence records source and registration identities, completed command names, reference comparisons, input hashes, repeatability hashes, independent verification, and per-artifact hashes.

`complete` starts as `false` and becomes `true` only after the full validation sequence succeeds. On a normal command failure the helper writes the incomplete evidence in its finalization path. A separate fixed pre-upload check runs with `always()` and rejects symlinks or special files even after validation fails. Upload occurs only when that check succeeds, and the run summary links the immutable artifact and its upload digest. A cancelled or timed-out runner may not finish finalization/upload; absent evidence or a partial artifact must never be promoted to a completed study.

Artifacts are retained for **30 days**, not permanently. Archive required evidence before expiry in an authorized durable location, retaining the download/artifact identity and recorded hashes. The upload archive digest and the helper's individual file hashes describe different objects and should not be conflated. Raw results remain outside Git. A source SHA is an execution identity, not permission to rewrite provenance or describe a dirty execution as clean.

## Automatic implementation and state-file boundary

This change adds a fixed scientific validation route. It does not expand the model runner's existing-file replacement schema, allowed roots, modes, file/patch sizes, one-attempt-per-Issue rule, or eight daily slots. It does not grant model edits to `experiments/`, `scripts/`, `.github/`, `tools/`, or protected configuration, and it adds no new-file/delete/rename capability. Research-code or infrastructure changes still require their applicable review and CI.

The guard protects the basename `research-autodev-state.json` case-insensitively, including an already-existing `docs/research-autodev-state.json`. The structured converter reuses that guard, so a model cannot replace `integrating` with `f19_complete` in the handoff manifest. Regression tests cover the protected path, mixed case, and a proposed promotion of an existing state file. State publication and updates must use the separate reviewed owner-authorized workflow.

Protecting the file does **not** make its phase or research dependencies runtime gates: the existing queue/runner still selects approved Issues using its existing author/label/attempt checks and does not enforce `research-autodev-state.json`. An empty automatic research allowance and the existing cloud-side hold remain necessary while Work owns the scope. No manifest, label, successful CI artifact, or docs update authorizes an unfinished or new research task by itself.

The final state publication follows actual review, main integration, deployment-byte checks, immutable-ledger checks, and queue inspection in a separate docs PR. It must use a real reviewed integration ancestor for `integrated_main_sha`, distinguish implemented CI capability from measured deployed capability, and avoid claiming deployment before verification. `f19_complete` additionally requires the registered full experiment, independent verification, repeatability, evidence-backed interpretation, and its limitations. Resuming the existing Windows task preserves its original times and privileges and does not require a manual production model invocation.
