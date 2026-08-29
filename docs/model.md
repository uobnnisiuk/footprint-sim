# Model and scenario contract

## Spatial model

`Position` は `x`、`y`、`elevation`、`regionId` を持ちます。`RegionDefinition` は `RegionType`、0〜1 の `passability`、region 間 connection を持ちます。connection の `passable` は通常移動に対する属性で、災害輸送が通るかどうかとは別です。

Person、Device、Carrier、Rescuer、Hazard は別 entity として定義します。Person/Device の association は同一物ではなく関係イベントです。`PersonDeviceSeparated` は person の `position` と device の `relatedPosition` を別々に更新し、その後はいずれも独立に輸送できます。

## Movement categories

イベントの `movementKind` は次を区別します。

1. `voluntary`: 徒歩、走行、避難
2. `carrier`: 車、バス、船に乗った移動
3. `hazardDriven`: 土砂、洪水、河川、雪崩、崩落による受動輸送
4. `depositionOrTrapping`: 堆積、埋没、漂着、障害物による停止
5. `secondaryTransport`: 二次氾濫や河道閉塞決壊などによる再移動

## Event and time

Scenario step は一意な `id`、非負整数 `tick`、`kind`、subject/related entity、任意の cause step、position/region、payload を持ちます。実行後の truth event は安定した `sequence`、決定論的 event ID、cause event ID を持ちます。同 tick は scenario の step 順で処理され、lifecycle event に予約した sub-order を使います。

観測方針は公開する evidence type、reported subject/position/region、発生確率、欠落確率、最小/最大遅延、conflict 確率と offset、evidence state、`publicPayload` です。公開項目は truth event から暗黙コピーされません。乱数選択は固定 seed に依存します。入力ファイルの step 順や seed を変える操作は意味のある simulation input 変更です。

## S0 and S1

S0 は複数人の encounter/evidence exchange と屋外から建物への通常移動を表し、最後の confirmed position と通常歩行半径を出します。

S1 は建物、道路、斜面、河川、堆積域をつなぎます。person は通常歩行半径外へ災害輸送され、device は分離後に二次輸送で別 region へ達します。person の堆積観測は明示的に欠落し、device の衝突観測は person の現在地として使用しません。

検索要約のpriority zoneは、最新のperson観測、宣言済みhazard envelope、通常移動baselineを根拠別に並べます。各zoneはevidence stateを持つ候補であり、真値位置の断定や一覧外regionの確定除外を意味しません。

これらは境界と再現性を検証する合成 fixture であり、現実の災害の再現・予測モデルではありません。
