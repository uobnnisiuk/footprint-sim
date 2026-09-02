# Footprint Sim

Footprint Sim は、災害前・災害中に分散して残る証拠から、救助側が行方不明者の捜索範囲をどこまで絞れるかを再現可能に評価する、ヘッドレスな研究用シミュレーターです。既存の実端末プロジェクト `footprint` の姉妹プロジェクトであり、実端末アプリを置き換えるものではありません。

M0 は建物内に限定しません。建物、道路、斜面、河川、屋外、地下を共通の region/connection モデルで表し、自力移動、乗り物による移動、災害による受動輸送、堆積・拘束、二次輸送を区別します。

Person と Device は別の物理エンティティです。端末の落下、流出、回収後も両者が同じ位置にいると仮定すると捜索範囲を誤るため、分離後は独立した位置履歴を持ちます。

## 必要環境

- Linux/macOS/Windows 上の .NET 8 SDK（Runtime だけでは不足）
- このリポジトリは `global.json` により 8.0 feature band 内の利用可能な SDK を選択します

確認:

```bash
./scripts/check-environment.sh
```

.NET 8 SDK がない Linux/macOS 環境では、Microsoft 公式 `dotnet-install.sh` を使う冪等なスクリプトを source してください。SDK は既定で `$HOME/.dotnet` に入り、リポジトリには入りません。

```bash
source scripts/setup-dotnet.sh
./scripts/check-environment.sh
```

Windows では Microsoft 公式 `dotnet-install.ps1` または公式インストーラーで .NET 8 SDK を導入してください。

## Build and test

```bash
dotnet restore FootprintSim.sln
dotnet build FootprintSim.sln --no-restore
dotnet test FootprintSim.sln --no-build
```

## シナリオ実行

S0（徒歩ベースライン）:

```bash
dotnet run --project src/FootprintSim.Cli -- \
  --scenario scenarios/s0_walking_baseline/scenario.json \
  --seed 42 \
  --output results/s0
```

S1（土砂による輸送と person/device 分離）:

```bash
dotnet run --project src/FootprintSim.Cli -- \
  --scenario scenarios/s1_debris_transport/scenario.json \
  --seed 42 \
  --output results/s1
```

S2（合成的な確率分岐・二次輸送）:

```bash
dotnet run --project src/FootprintSim.Cli -- \
  --scenario scenarios/s2_branching_debris_transport/scenario.json \
  --seed 42 \
  --output results/s2
```

各出力ディレクトリには次が作られます。

- `ground-truth.events.json`: シミュレーターだけが保持する真値イベント
- `observations.json`: 救助側へ渡せる観測契約だけのログ
- `search-zone.summary.json`: 最終確認位置、通常移動範囲、災害輸送 envelope、根拠付き優先候補領域の要約
- `run.metadata.json`: scenario ID、seed、入力ハッシュ、件数

同じ scenario ファイルと seed の出力は byte-for-byte で再現できます。たとえば別ディレクトリへ再実行して比較できます。

```bash
dotnet run --project src/FootprintSim.Cli -- \
  --scenario scenarios/s1_debris_transport/scenario.json \
  --seed 42 --output results/s1-repeat
diff -r results/s1 results/s1-repeat
```

`results/` は生成物なので Git 管理外です。

## 複数 run 評価（M1-A / M1-B）

すべてのscenario、seed 0..19、全8戦略をまとめて比較する例です。

```bash
dotnet run --project src/FootprintSim.Cli -- evaluate \
  --scenarios scenarios \
  --seeds 0..19 \
  --strategies all \
  --output results/evaluation
```

`--scenarios` には単一の scenario JSON または `scenario.json` を再帰検索するディレクトリ、`--seeds` には単一値・包含範囲（`0..19`）・カンマ区切り、`--strategies` には `all` または strategy ID のカンマ区切りを指定できます。

S2だけを200 seedで評価する例です。`--scenario` は単一ファイル用の別名です。

```bash
dotnet run --project src/FootprintSim.Cli -- evaluate \
  --scenario scenarios/s2_branching_debris_transport/scenario.json \
  --seeds 0..199 \
  --strategies all \
  --output results/m1b-s2
```

出力は、1 strategy/run につき1行の `runs.jsonl` と `runs.csv`、scenario・strategy 別の `aggregate.json` と `aggregate.csv`、condition別の`conditions.csv`、paired deltaの`paired.csv`、入力条件とschemaを固定する`manifest.json`です。`GroundTruthContainment`、`SearchSpaceFraction`、`FalseExclusion`などschema version 2までの意味は維持します。M1-C3のschema version 3はpreferred/outer plausible、condition、cross-thread audit、paired deltaを別fieldとして追加します。算出不能値は `available: false` と `null` で表し、実測値 0 と区別します。

同じ入力とsource状態では既存7ファイルすべてがbyte-for-byteで再現されます。S4では追加の`causal-tracing.json`と`causal-tracing.csv`、S5では`past-state-reconstruction.json`と`past-state-reconstruction.csv`も再現性対象です。M1-Aの評価定義は [M1-A evaluation harness](docs/m1a-evaluation.md)、確率伝播と公開境界は [M1-B stochastic hazard](docs/m1b-stochastic-hazard.md)、S3比較は [M1-C3 weft evaluation](docs/m1c3-weft-evaluation.md) を参照してください。数値は合成scenario内の比較値であり、現実の災害・救助性能を示しません。

## Cross-entity interaction（M1-C1）

M1-C1は因果の横糸を、private truthの`InteractionOccurrence`、観測されたevidenceの`InteractionObservation`、観測だけから導出される`CrossThreadConstraint`へ分離します。最初のrelationはperson-person encounterだけです。constraintはまだsearch strategyへ渡さず、candidate regionや既存評価結果を変更しません。型、available-at境界、provenance、決定的IDの詳細は [M1-C1 interaction model](docs/m1c1-interaction-model.md) を参照してください。

## Historical cross-thread constraint（M1-C2）

M1-C2は、publicに利用可能になった`CrossThreadConstraint`を最終regionへ直接intersectionせず、`CandidateWarp`の対応するhistorical tickまたはinclusive intervalへ適用します。`Confirmed`、uncertain alternative、conflict、NoOpを区別し、生き残った候補は`PublicHazardEnvelope`だけで評価tickへ前方伝播します。exact tick、interval、relation kind、participant、source observation、provenanceはapplication auditへ保持します。

これはconstraint意味論のfixture実装であり、M1-C2時点の既存4 strategyや評価schemaは変更しません。Causal Nexus / Causal Nexus Resolutionは将来仮説であり、M1-C2はformal schemaやscoreを実装しません。詳細は [M1-C2 cross-thread constraints](docs/m1c2-cross-thread-constraints.md) を参照してください。

## Paired weft evaluation（M1-C3）

M1-C3は正式な合成S3を追加し、同じscenario/seed/truthに対して`own-evidence-constrained`と`cross-thread-constrained`をpaired比較します。後者だけが利用可能なcross-thread constraintをhistorical warpへ適用します。preferred縮約とouter-safe containment、遅延、uncertain alternative、conflict、unavailable、device-only境界をschema version 3で別々に評価します。

```bash
dotnet run --project src/FootprintSim.Cli -- evaluate \
  --scenario scenarios/s3_cross_thread_encounter/scenario.json \
  --seeds 0..199 \
  --strategies all \
  --output results/m1c3-s3
```

## Neutral telemetry（M1-T0）

評価へ`--telemetry`を付けると、既存評価ファイルを変えずに、private truth、public/evidence、evaluation transitionを分離した決定的JSONLを`telemetry/`以下へ追加します。telemetry schema version 4はversion 3までの意味を維持し、M1-E method ID、evidence usage、source-observation resolution、local state、実際のforward/backward transitionとpublic transition referenceを追加します。past-state query、candidate、verification、method workは引き続き別recordです。event/interval、relation、support/conflict lineage、candidate before/after、provenanceとsource ancestryを中立記録として保持し、Good/Bad、Causal Shadow、Indexability、Nexus Resolution、救助優先度などのscoreは含めません。

```bash
dotnet run --project src/FootprintSim.Cli -- evaluate \
  --scenario scenarios/s3_cross_thread_encounter/scenario.json \
  --seeds 0..199 \
  --strategies all \
  --output results/m1t0-s3 \
  --telemetry
```

S0〜S4で利用でき、未指定時は`telemetry/`を作りません。stream境界、schema、再現性、cross-domain SARの将来検証範囲は [M1-T0 neutral telemetry](docs/m1t0-neutral-telemetry.md) を参照してください。

## Operational Causal Tracing（M1-D）

S4では同じsynthetic truthに対し、own evidenceのみ、単一weft、複数weftのOperational Causal Tracingを比較します。複数制約はsource ancestryでgroup化し、forward/backward traversal、upstream/local/downstreamの接続（M1-Dでは`causal sandwich`というlegacy working label）、support/conflict lineageを保持します。これはknown public structureのtraversalであり、past candidate reconstructionではありません。

```bash
dotnet run --project src/FootprintSim.Cli -- evaluate \
  --scenario scenarios/s4_multi_weft_tracing/scenario.json \
  --seeds 0..199 \
  --strategies own-evidence-constrained,single-weft,multi-weft-tracing \
  --output results/m1d-s4 \
  --telemetry
```

詳細は [M1-D Operational Causal Tracing](docs/m1d-causal-tracing.md) を参照してください。

## Past-state reconstruction（M1-E）

S5では、target entity/window/cutoffを明示したqueryに対し、cutoffまでに利用可能なpublic evidenceとconstraintからhistorical candidateを構築します。candidate生成、stable machine-readable IDによるaddressing、別source lineageによるverification、既存M1-D tracerによるbackward/local traversalは別操作です。同じsource ancestryをindependent verificationへ再利用せず、future/private evidence、truth occurrence、realized path、truth parentをpublic comparisonへ渡しません。

```bash
dotnet run --project src/FootprintSim.Cli -- evaluate \
  --scenario scenarios/s5_distributed_past_evidence/scenario.json \
  --seeds 0..199 \
  --strategies all \
  --output results/m1e-s5 \
  --telemetry
```

出力はbackward traversal、constraint-based reconstruction、reconstruction後のlocal traversalを同じseed/query/public inputで比較します。Addressabilityはtruth likelihoodではなく、Verifiabilityはgenerationとは別のlineage検査です。設計、preregistration、仏教方法論との限定的比較、命名監査は [M1-E past-state reconstruction](docs/m1e-past-state-reconstruction.md)、verificationとtraversalの用途分離および修正版比較は [M1-E method-composition correction](docs/m1e-method-composition-fix.md)、S5の観測lineage・時間・局所state・公開topologyの前提監査は [M1-E2 synthetic-world representation validity](docs/m1e2-world-representation-validity.md) を参照してください。

S5 defaultを変えずにpublic observation familyだけを同一truth world上で増減するM1-F評価は、次で実行します。Full、None、single-family removal/presence、事前選定した3組を比較し、raw observationとderived constraintを同時に利用不能化します。

```bash
dotnet run --project src/FootprintSim.Cli -- evaluate-observation-ablation \
  --experiment experiments/m1f_observation_ablation/experiment.json \
  --seeds 0..199 \
  --output results/m1f-s5
```

これは現実の最小観測集合を確定する実験ではありません。設計、事前登録条件、結果と解釈境界は [M1-F observation-family ablation](docs/m1f-observation-ablation.md) を参照してください。

M1-Fで候補削減への寄与が最も明確だった`later-trace-a`について、subject / time / location / relationの情報availabilityを分けて監査するM1-F1評価は次で実行します。現行のscenario-declared transformationは4 fieldを一体として要求するため、不足fieldを隠れたscenario値で補わず、導出不能を結果として記録します。

```bash
dotnet run --project src/FootprintSim.Cli -- evaluate-later-trace-field-ablation \
  --experiment experiments/m1f1_later_trace_field_ablation/experiment.json \
  --seeds 0..199 \
  --output results/m1f1-s5
```

これはfieldごとの現実的価値を確定する実験ではありません。事前登録条件、atomic derivationの境界、結果の識別可能性は [M1-F1 later-trace field ablation](docs/m1f1-later-trace-field-ablation.md) を参照してください。

M1-F2ではsubject/time/relationを固定したまま、`later-trace-a`のlocation Region集合だけを現行fork集合、target-window全候補、S5 searchable全域、unknownへ段階的に粗くします。finite levelではobservation payloadとderived allowed regionsを一致させ、unknownからexact locationを補完しません。

```bash
dotnet run --project src/FootprintSim.Cli -- evaluate-location-resolution \
  --experiment experiments/m1f2_location_resolution/experiment.json \
  --seeds 0..199 \
  --output results/m1f2-s5
```

これは現実距離やsensor精度を決める実験ではありません。location level、partial-observation semantics、事前登録、結果の表現依存性は [M1-F2 location resolution](docs/m1f2-location-resolution.md) を参照してください。

M1-F3ではlocation/subject/relation/availabilityを固定し、`later-trace-a`のobserved-timeだけをS5の既存state境界に沿って段階的に広げます。広いwindowはrelationの持続ではなく、区間内のどこか1 tickで観測されたという不確実性です。現在のscenario-declared historical windowがobserved tickを実際に使用するかも明示的に監査します。

```bash
dotnet run --project src/FootprintSim.Cli -- evaluate-time-resolution \
  --experiment experiments/m1f3_time_resolution/experiment.json \
  --seeds 0..199 \
  --output results/m1f3-s5
```

これは現実の時間精度要件を決める実験ではありません。time level、occurrence uncertainty、既存transformationの境界、事前登録と結果は [M1-F3 time resolution](docs/m1f3-time-resolution.md) を参照してください。

M1-F8ではS5を変更せず、観測されたlater-eventのoccurrence-time windowとpublic dependencyのduration boundから、predecessor windowを計算する最小vertical sliceを実行します。fixtureは決定論的であり、200回の同一seed反復は行いません。

```bash
dotnet run --project src/FootprintSim.Cli -- evaluate-observation-constraint-derivation \
  --fixture experiments/m1f8_observation_constraint_derivation/fixture.json \
  --output results/m1f8-derivation
```

式、provenance、unknown-time failure、CandidateGeneration用途境界、受入結果とS5未移行理由は [M1-F8 observation-to-constraint derivation](docs/m1f8-observation-constraint-derivation.md) を参照してください。

M1-F9ではF8 ruleを変更せず、seedごとにcandidate layer、later tick、duration bound、truth candidateを生成し、Exact / Medium / Broad / Unknownを同一world上でpaired評価します。登録seedは`0..199`に固定されています。

```bash
dotnet run --project src/FootprintSim.Cli -- evaluate-time-resolution-paired \
  --experiment experiments/m1f9_time_resolution_paired/experiment.json \
  --seeds 0..199 \
  --output results/m1f9-time-resolution
```

generator範囲、per-seed set inclusion、candidate plateau、結果と解釈境界は [M1-F9 paired time-resolution evaluation](docs/m1f9-time-resolution-paired-evaluation.md) を参照してください。

M1-F10ではS5を変更せず、exact / finite alternatives / identity unknownのother-participant情報と、各possible personのpublicなsame-time Region evidenceから、targetのencounter Region constraintを保守的unionで生成する最小vertical sliceを実行します。fixtureはPerson-onlyかつ決定論的であり、200-seed評価は次工程へ残します。

```bash
dotnet run --project src/FootprintSim.Cli -- evaluate-participant-ambiguity-derivation \
  --fixture experiments/m1f10_participant_ambiguity_derivation/fixture.json \
  --output results/m1f10-participant-derivation
```

same-time co-locationのsemantic basis、missing-evidence failure、source independence、受入結果とS5未移行理由は [M1-F10 participant ambiguity derivation](docs/m1f10-participant-ambiguity-derivation.md) を参照してください。

M1-F11ではF10 ruleを変更せず、candidate/Region/participant poolとparticipantごとのpublic Region evidenceをseedで変化させます。同一world上のExact / Narrow / Broad / Unknownをpaired評価し、participant→Region→candidateのどの層でprecision差がplateauになるかをper-seed set inclusionで監査します。

```bash
dotnet run --project src/FootprintSim.Cli -- evaluate-participant-ambiguity-paired \
  --experiment experiments/m1f11_participant_ambiguity_paired/experiment.json \
  --seeds 0..199 \
  --output results/m1f11-participant-ambiguity
```

generator事前登録、overlap分類、source independence、200-world結果と解釈境界は [M1-F11 paired participant-ambiguity evaluation](docs/m1f11-participant-ambiguity-paired-evaluation.md) を参照してください。

M1-F12ではS5を変更せず、relation informationをExact `PersonPersonEncounter`と
「relationは存在するがkind不明」に分離します。Exact時のみ既存same-time co-location
ruleを適用し、kind不明時はrelation observationを保持したままrelation-derived constraintを
安全側に保留する決定的vertical sliceです。正当なsecond relation/taxonomyがないため2 profileに
限定します。

```bash
dotnet run --project src/FootprintSim.Cli -- evaluate-relation-dependent-derivation \
  --fixture experiments/m1f12_relation_dependent_derivation/fixture.json \
  --output results/m1f12-relation-derivation
```

formal/free-text relationの区別、applicability dispatch、Exact/Unknown結果、truth/source/usage
境界と次工程判断は [M1-F12 relation-dependent derivation](docs/m1f12-relation-dependent-derivation.md) を参照してください。

## 構成

- `src/FootprintSim.Core`: world/event/observation/interaction/inference/evaluation の契約と決定論エンジン
- `src/FootprintSim.Cli`: 単一 run と複数 run 評価の入出力
- `tests/FootprintSim.Core.Tests`: 決定性、truth 境界、S0/S1回帰、S2、評価 schema の自動テスト
- `scenarios`: 小さく明示的な合成scenario
- `docs`: アーキテクチャとモデル
- `scripts`: SDK の準備・実測

詳細は [architecture](docs/architecture.md) と [model](docs/model.md) を参照してください。

## 現段階の制限

S0/S1 の経路はscenarioに明示した抽象イベントです。S2〜S5の重み・evidence conditionも研究用の合成値であり、校正済み確率ではありません。M1-C3〜M1-Eは境界付きpublic evidenceの合成比較であり、formal Causal Weave、Causal Nexus、24縁engine、belief engineではありません。流体・土砂・建物倒壊の物理解析、観測チャネル校正、実時間処理、GUI、完全なEBPC、救助優先順位ポリシーは実装していません。特定の実災害を再現するものでも、現実の位置・生存・危険度を予測できるものでもありません。

## 次のマイルストーン

M1-Aは複数run評価、M1-Bは確率分岐する経糸、M1-C1は横糸の表現境界、M1-C2はhistorical constraint意味論、M1-C3は正式S3 paired比較、M1-T0は意味付け前の中立telemetryです。複数weftのoperational tracingはM1-D、public evidenceからのpast-state candidate reconstructionはM1-Eです。観測チャネル校正、動的index、24縁とのprimitive relation照合、深いEBPC統合は将来へ送り、`ObservationSet` / `IRescueInference` 境界を維持します。
