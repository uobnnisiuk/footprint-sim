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

すべてのscenario、seed 0..19、全4戦略をまとめて比較する例です。

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

出力は、1 strategy/run につき1行の `runs.jsonl` と `runs.csv`、scenario・strategy 別の `aggregate.json` と `aggregate.csv`、入力条件と schema を固定する `manifest.json` です。`GroundTruthContainment` は真の person region を候補に保持できたか、`SearchSpaceFraction` は全 scenario region に対する候補比率、`FalseExclusion` は真値を候補から落としたかを示します。M1-Bのschema version 2では、最終時点の有用性、持続率、最長連続tick、後退検出、candidate provenance、合成truth outcome分布も追加されます。算出不能値は `available: false` と `null` で表し、実測値 0 と区別します。

同じ入力と source 状態では5ファイルすべてが byte-for-byte で再現されます。M1-Aの評価定義は [M1-A evaluation harness](docs/m1a-evaluation.md)、確率伝播、公開境界、追加metricは [M1-B stochastic hazard](docs/m1b-stochastic-hazard.md) を参照してください。数値は合成scenario内の比較値であり、現実の災害・救助性能を示しません。

## 構成

- `src/FootprintSim.Core`: world/event/observation/inference/evaluation の契約と決定論エンジン
- `src/FootprintSim.Cli`: 単一 run と複数 run 評価の入出力
- `tests/FootprintSim.Core.Tests`: 決定性、truth 境界、S0/S1回帰、S2、評価 schema の自動テスト
- `scenarios`: 小さく明示的な合成scenario
- `docs`: アーキテクチャとモデル
- `scripts`: SDK の準備・実測

詳細は [architecture](docs/architecture.md) と [model](docs/model.md) を参照してください。

## 現段階の制限

S0/S1 の経路はscenarioに明示した抽象イベントです。S2の重みも研究用の合成値であり、校正済み確率ではありません。流体・土砂・建物倒壊の物理解析、観測チャネル校正、実時間処理、GUI、完全なEBPC、救助優先順位ポリシーは実装していません。特定の実災害を再現するものでも、現実の位置・生存・危険度を予測できるものでもありません。

## 次のマイルストーン

M1-Aは複数runの比較・集計基盤、M1-Bは安定PRNGによる合成hazard伝播、S2、公開envelope、region-level provenance、guidance持続性までを追加しました。M1-C以降では観測チャネルの校正、公開情報の時間変化、より広いscenario fixtureを検討します。完全なEBPC adapterは別工程であり、`ObservationSet` / `IRescueInference` 境界を維持したまま接続します。
