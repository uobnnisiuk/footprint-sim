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

同じ入力とsource状態では既存7ファイルすべてがbyte-for-byteで再現されます。S4では追加の`causal-tracing.json`と`causal-tracing.csv`も再現性対象です。M1-Aの評価定義は [M1-A evaluation harness](docs/m1a-evaluation.md)、確率伝播と公開境界は [M1-B stochastic hazard](docs/m1b-stochastic-hazard.md)、S3比較は [M1-C3 weft evaluation](docs/m1c3-weft-evaluation.md) を参照してください。数値は合成scenario内の比較値であり、現実の災害・救助性能を示しません。

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

評価へ`--telemetry`を付けると、既存評価ファイルを変えずに、private truth、public/evidence、evaluation transitionを分離した決定的JSONLを`telemetry/`以下へ追加します。telemetry schema version 2はversion 1の意味を維持し、複数weftのtracing constraintとforward/backward/cross-thread stepを追加します。event/interval、relation、support/conflict lineage、candidate before/after、provenanceとsource ancestryを中立記録として保持し、Good/Bad、Causal Shadow、Indexability、Nexus Resolution、救助優先度などのscoreは含めません。

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

S4では同じsynthetic truthに対し、own evidenceのみ、単一weft、複数weftのOperational Causal Tracingを比較します。複数制約はsource ancestryでgroup化し、forward/backward traversal、upstream/local/downstreamのcausal sandwich、support/conflict lineageを保持します。これはknown public structureのtraversalであり、past reconstructionやIndex Accessではありません。

```bash
dotnet run --project src/FootprintSim.Cli -- evaluate \
  --scenario scenarios/s4_multi_weft_tracing/scenario.json \
  --seeds 0..199 \
  --strategies own-evidence-constrained,single-weft,multi-weft-tracing \
  --output results/m1d-s4 \
  --telemetry
```

詳細は [M1-D Operational Causal Tracing](docs/m1d-causal-tracing.md) を参照してください。

## 構成

- `src/FootprintSim.Core`: world/event/observation/interaction/inference/evaluation の契約と決定論エンジン
- `src/FootprintSim.Cli`: 単一 run と複数 run 評価の入出力
- `tests/FootprintSim.Core.Tests`: 決定性、truth 境界、S0/S1回帰、S2、評価 schema の自動テスト
- `scenarios`: 小さく明示的な合成scenario
- `docs`: アーキテクチャとモデル
- `scripts`: SDK の準備・実測

詳細は [architecture](docs/architecture.md) と [model](docs/model.md) を参照してください。

## 現段階の制限

S0/S1 の経路はscenarioに明示した抽象イベントです。S2/S3の重みも研究用の合成値であり、校正済み確率ではありません。M1-C3は境界付きcross-thread evidenceの合成比較であり、formal Causal Weave、Causal Nexus、belief engineではありません。流体・土砂・建物倒壊の物理解析、観測チャネル校正、実時間処理、GUI、完全なEBPC、救助優先順位ポリシーは実装していません。特定の実災害を再現するものでも、現実の位置・生存・危険度を予測できるものでもありません。

## 次のマイルストーン

M1-Aは複数run評価、M1-Bは確率分岐する経糸、M1-C1は横糸の表現境界、M1-C2はhistorical constraint意味論、M1-C3は正式S3 paired比較、M1-T0は意味付け前の中立telemetryです。観測チャネル校正と深いEBPC統合はM2へ送り、`ObservationSet` / `IRescueInference` 境界を維持します。複数weftのoperational tracingはM1-D、dynamic past indexはM1-Eで扱います。
