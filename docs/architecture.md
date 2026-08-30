# Architecture

M0からM1-C3までは単一Core assembly内で型と依存方向を分けます。分割の目的は小さな基盤を保ちながら、後でworld engineやEBPC adapterを別assemblyに移せるようにすることです。

```text
Scenario + seed
      |
      v
1. World and hazard engine -- private realized path/events/final state
      |
      +---------------- public hazard envelope ----------------+
      v                                                    v
2. Observation generator ---- observation-only contract   RescueContext
      |
      v
3. Footprint/EBPC boundary --- IRescueInference
      |
      v
4. Search strategy ----------- SearchStrategyResult
                                      |
                                      v
5. Private truth ------------> Evaluator/aggregate
```

Private truth enters only at step 5, after a strategy result has been produced.

## 1. World and hazard engine

`SimulationEngine` は整数tickとscenario内の入力順を用いてイベントを安定整列します。イベントIDと観測IDはscenario ID、seed、step identityからSHA-256で決定し、乱数は実装を固定したSplitMix64を用います。この層だけがground truth event、選択されたtransition、全entityの最終位置を保持します。

Worldはregionとconnectionのグラフです。S0/S1は明示イベントを維持します。S2の`StochasticHazardPropagator`はnodeと重み付き有向transitionからrealized path、bounded travel time、堆積・拘束、任意の二次輸送を生成します。person/device分離後はroot seedからentity別sub-seedを導出し、一方のdraw追加が他方のstreamをずらさないようにします。

`PublicHazardEnvelopeBuilder`は同じscenario定義から公開可能なbranch、到達region、到達時間幅、transport type、provenance、公開tickだけを生成します。選択されたbranch、真の終端、乱数drawは含めません。

## 2. Observation generator

各 truth step の `observation` 方針から、seed に従って発生、欠落、遅延、衝突を決めます。発生過程は truth ログの lifecycle event に残ります。一方、外部へ出す `ObservationRecord` は観測方針に明記した evidence type、reported subject、reported position/region、根拠状態、`publicPayload` だけで構築します。truth event の subject、position、kind、payload、cause event、最終 entity state は暗黙コピーしません。

## 3. Causal interaction representation

M1-C1はcross-entity interactionを3型に分離します。

```text
SimulationEvent(Encounter) -- truth projection --> InteractionOccurrence

independent evidence input ---------------------> InteractionObservation
                                                    |
                                                    v
                                           CrossThreadConstraint
```

`InteractionOccurrence`は`FootprintSim.Interactions.Truth`に置き、既存world eventから投影します。`SimulationEvent`が引き続きauthoritative logであり、二つのmutable truthを持ちません。Occurrenceはsource world-event ID、既存causal parent、participant role、time window、region、provenanceを明示するsemantic representationです。

`InteractionObservation`はoccurrence objectを保持せず、任意のinteraction referenceもopaqueなevidence-side stringです。`CrossThreadConstraint`はobservationだけから導出され、truth occurrenceを引数にもpropertyにも持ちません。両者のsetは`AvailableAtTick <= currentTick`のpublic evidenceだけを返せます。

M1-C1ではこれらを`SearchStrategyInput`へ追加せず、candidate regionへ適用しませんでした。最初の正式relationはperson-person encounterだけです。これは完全なCausal WeaveまたはEBPC adapterではありません。

M1-C2は別のpublic-only研究経路として、public modelから作る`CandidateWarpSet`と、利用時点までにpublicになった`CrossThreadConstraint`を`CrossThreadConstraintApplicator`へ渡します。Applicatorはconstraintをhistorical tick/inclusive intervalへ適用し、`Confirmed`のnarrow、uncertain branchとouter alternative、conflict、NoOpを区別します。その後のstateは`PublicHazardEnvelope`だけで前方伝播します。truth event、realized transition、final entity stateはAPIへ入りません。

`CandidateWarpState`はregionだけでなく、`EventPoint`/`StateInterval`、inclusive window、evidence state、relation/source-observationを含むprovenanceを保持します。application auditはrelation kind、participant role、observed/constrained window、application tick、disposition、before/after count、retained alternative、source ancestryを保持します。これはCausal Nexusのformal schemaやResolution metricではなく、将来relation resolutionを研究できる情報を失わないための最小contractです。

M1-C3ではevidence-sideの`CrossThreadConstraintSet`だけをstrategy inputへ追加します。truth-side`InteractionOccurrence`は引き続き到達不能です。S3の`cross-thread-constrained`だけがApplicatorを使い、`own-evidence-constrained`との同一truth paired比較を行います。target-specific denominator、preferred/outer-safe候補、condition別集計、paired deltaをschema version 3で追加し、既存fieldの意味は変更しません。

## 4. Footprint/EBPC inference boundary

`IRescueInference.Infer` が受け取れるのは `RescueContext` と `ObservationSet` だけです。`SimulationRun` や `SimulationEvent` を引数にしないため、推論実装が隠れた正解を参照する必要はありません。

Evidence state は `Confirmed`、`SupportedButUncertain`、`Conflicting`、`Indeterminate`、`UnsupportedForQuery` を契約として JSON に保持します。現在の `BasicRescueInference` は小さな基準実装であり、この interface の先に EBPC adapter を追加できます。

## 5. Rescue view and search strategies

CLI は最終確認位置を anchor にした通常歩行半径と、事前に宣言された hazard transport envelope を別々に出力します。検索優先領域には順位、根拠、evidence stateを付け、候補領域であって一点予測や他領域の確定除外ではないことも明記します。S1 では端末観測を person 観測へ暗黙変換しません。evaluator は truth を採点に使用できますが、推論の入力には渡さず、結果生成後に比較します。

M1-Aの`ISearchStrategy`はpublic context/evidence、clock、run identityを受け取ります。M1-Bでは`RescueContext`にpublic hazard envelopeを追加し、M1-C3では評価tickまでにpublicなderived constraintを追加しましたが、realized pathとtruth occurrenceは追加していません。各candidate regionはsource kind、source identifier、available tick、person/device-only区分、evidence stateを持つprovenanceを返します。現在の`EvidenceAwareCurrent`はM0 summaryのadapterであり、完全なEBPC adapterではありません。

## 6. Evaluator and aggregate

`PerRunEvaluator`だけがstrategy resultの確定後に`GroundTruthEvaluationContext`を受け取ります。既存指標に加え、schema version 3ではpreferred/outer-safe containment、reduction、guidance、constraint audit countを独立fieldとして計算します。`AggregateEvaluator`はscenario・strategy、condition、paired deltaと、evaluator専用truth outcome分布を分離して集計します。再現性対象JSON/JSONL/CSVは時刻や絶対パスを含まず決定的です。

## Trust boundary

`ground-truth.events.json`、in-memory final state、`InteractionOccurrence`はprivate truthです。`observations.json`と`InteractionObservation`は別のevidence表現です。新しい観測フィールドは、真値由来であるだけの属性を公開していないかレビューし、必要なprovenanceは観測側で独立した識別子として表します。`CrossThreadConstraint`もevidence-derived claimであってtruthではありません。評価時の真値は`FootprintSim.Evaluation.Truth`に閉じ、truth型をstrategy inputへ追加しません。
