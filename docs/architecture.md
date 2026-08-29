# Architecture

M0 は単一 Core assembly 内で型と依存方向を分けます。分割の目的は小さな初期基盤を保ちながら、後で world engine や EBPC adapter を別 assembly に移せるようにすることです。

```text
Scenario + seed
      |
      v
1. World and hazard engine -- private truth events/final state
      |
      v
2. Observation generator ---- observation-only contract
      |
      v
3. Footprint/EBPC boundary --- IRescueInference
      |
      v
4. Rescue view/evaluator ---- search-zone summary
```

## 1. World and hazard engine

`SimulationEngine` は整数 tick と scenario 内の入力順を用いてイベントを安定整列します。イベントIDと観測IDは scenario ID、seed、step identity から SHA-256 で決定し、乱数は実装を固定した SplitMix64 を用います。この層だけが ground truth event と全 entity の最終位置を保持します。

World は region と connection のグラフです。region は平面座標、高さ、種別、passability を持つため、屋内外を同じ抽象で扱えます。M0 の hazard は route、速度範囲、分岐、停止確率を記述しますが、scenario の明示イベントを物理計算で置換はしません。

## 2. Observation generator

各 truth step の `observation` 方針から、seed に従って発生、欠落、遅延、衝突を決めます。発生過程は truth ログの lifecycle event に残ります。一方、外部へ出す `ObservationRecord` は観測方針に明記した evidence type、reported subject、reported position/region、根拠状態、`publicPayload` だけで構築します。truth event の subject、position、kind、payload、cause event、最終 entity state は暗黙コピーしません。

## 3. Footprint/EBPC inference boundary

`IRescueInference.Infer` が受け取れるのは `RescueContext` と `ObservationSet` だけです。`SimulationRun` や `SimulationEvent` を引数にしないため、推論実装が隠れた正解を参照する必要はありません。

Evidence state は `Confirmed`、`SupportedButUncertain`、`Conflicting`、`Indeterminate`、`UnsupportedForQuery` を契約として JSON に保持します。現在の `BasicRescueInference` は小さな基準実装であり、この interface の先に EBPC adapter を追加できます。

## 4. Rescue view and evaluator

CLI は最終確認位置を anchor にした通常歩行半径と、事前に宣言された hazard transport envelope を別々に出力します。検索優先領域には順位、根拠、evidence stateを付け、候補領域であって一点予測や他領域の確定除外ではないことも明記します。S1 では端末観測を person 観測へ暗黙変換しません。将来の evaluator は truth を採点に使用できますが、推論の入力には渡さず、結果生成後に比較する構成にします。

## Trust boundary

`ground-truth.events.json` と in-memory final state は private truth です。`observations.json` が推論境界を越えられるデータです。新しい観測フィールドは、真値由来であるだけの属性を公開していないかレビューし、必要な provenance は観測側で独立した識別子として表します。
