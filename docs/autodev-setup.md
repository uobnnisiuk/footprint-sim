# AutoDev MVP v2: Issue → Codex → 検証 → Draft PR

対象: `uobnnisiuk/footprint-sim`。関連: [Issue #1](https://github.com/uobnnisiuk/footprint-sim/issues/1)。

所有者が作成した小さなIssueに、所有者自身が `autodev` ラベルを付けると、一度のパッチ生成、独立した検証、ドラフトPR作成を行います。マージは所有者が手動で行います。

## 初回セットアップ

1. この導入PRをレビューし、所有者が `main` にマージします。既定ブランチは `main` としてください。`issues` イベントはワークフローが既定ブランチにある場合のみ発火し、CIの `workflow_dispatch` も既定ブランチにワークフローが必要です。導入ブランチだけでは自動開発は開始しません。
2. OpenAI APIのプロジェクトで専用キーを発行し、Repository Settings → Secrets and variables → Actions に `OPENAI_API_KEY` を登録します。APIの課金・利用可能なモデルはChatGPTの契約とは別です。利用上限・アラートはAPI側で設定してください。ワークフローの時間制限は金額の厳密な上限ではありません。
3. Settings → Actions → General で、使用するActionsを許可し、「Allow GitHub Actions to create and approve pull requests」を有効にします。組織・リポジトリのポリシーがジョブ単位の `contents: write`、`pull-requests: write`、`actions: write` を許容する必要があります。実装はPR承認・マージ操作を行いません。
4. `autodev` ラベルを作成します。この導入作業は秘密情報や設定を登録・変更しません。
5. 下記の小さなテストIssueを所有者アカウントで作成し、本文を確定してから `autodev` を付けます。Issue #1は基盤導入用なので起動対象にしません。
6. Actionsの「AutoDev - issue to draft PR」を確認します。候補の検証が成功した場合、`autodev/issue-<番号>` から `main` 向けのDraft PRが作られ、「Footprint Sim CI」が明示的に起動します。
7. CIの対象コミットがPRのheadと一致し、差分が意図どおりであることを確認してから、所有者が手動でマージします。

現行のGitHub仕様では、`GITHUB_TOKEN` によるPRイベントは承認待ちの実行を作る場合があります。AutoDevはそれに依存せず `workflow_dispatch` を送ります。CIは `github-actions[bot]` が起こした `pull_request` 実行をスキップし、明示dispatchで実行します。所有者が後から同じPRを更新した場合は通常のPR CIを実行します。botによるpushやCI完了からAutoDevを再起動する仕組みはありません。

## 境界とZIPからの修正

| ジョブ | 権限・秘密情報 | 処理 |
| --- | --- | --- |
| develop | contents/pull-requests: read、APIキーはCodex Actionの保護されたproxyのみ | 所有者・イベント・既存PRを検査し、専用の非特権ユーザーと読み取り専用sandboxでパッチ本文を生成 |
| validate | contents: read、APIキーなし、Git書き込み認証なし | fresh runnerでパッチを検査・適用し、restore/build/test/format/S0/S1/評価を実行 |
| publish | contents/pull-requests/actions: write、APIキーなし | 別のfresh runnerで同じパッチを再検査・適用、commit/push/Draft PR/CI dispatch。候補コードは実行しない |

- 全ジョブがイベントの同じ不変commit SHAをcheckoutします。検証中に `main` が進んでも、異なる基準へ黙ってパッチを適用しません。
- ZIPの「AIが作業ツリーを書き換え、その中のスクリプトでexportする」方式を、**読み取り専用のAIがraw unified diffを返す方式**へ変更しました。AIはガード、Git設定、後続スクリプトを書き換えられません。通常のコード編集形式よりdiff生成が失敗しやすい制約はありますが、不正な出力は失敗として停止します。
- APIキーはAction入力だけに渡し、テスト用環境には渡しません。Codexにはネットワーク、sudo、書き込み権限を与えず、コード・テストの実行も指示しません。変更された候補コードを実行するのはvalidateとCIだけです。
- agent出力は信用しません。応答末尾の改行欠落だけを補い、形式・サイズ・パスを検査し、独立した一時Git repository/indexで適用した結果のblobとmodeまで検査してから、実checkoutに適用します。
- 許可範囲は `src/`、`tests/`、`docs/`、`scenarios/`、`README.md`。ASCII英数字で始まるパス要素に限定します。日本語等の既存ファイル名を扱う作業は手動対応です。
- 範囲内でも、入れ子の `AGENTS.md`、隠しパス、SDK・MSBuild・NuGet・package設定、project/solutionファイル、生成物は変更できません。`.github/`、`scripts/`、ルートのSDK設定等も保護します。新しい依存関係やprojectの追加は手動対応です。
- シンボリックリンクとその配下、gitlink、実行ビット、binary/NUL/non-UTF-8、rename/copy、不正なhunkや余分な末尾テキストを拒否します。30ファイル、patch 400,000 bytes、各ファイル180,000 bytesまでです。
- Issueは所有者作成でも信頼しません。長さ・発火条件を検証し、不可視制御文字を拒否、HTMLコメントを除去、JSONとしてプロンプトへ埋め込みます。これだけでプロンプト注入を完全に防げるわけではなく、外部のOS権限・sandbox・パッチ検査が実際の制約です。
- Actionはcommit SHA、Codex CLIとAPI proxyはバージョンを固定します。Codex Actionは出力中のworkflow commandを無効化する上流修正#187を含む版を使用します。更新時は入力仕様と権限を再確認してください。

## 起動・重複・失敗時の扱い

- ownerによるowner作成・open Issueの `labeled: autodev` だけが起点です。fork、PR、他ユーザー、Actionsの「Re-run jobs」は対象外です。
- Issue単位のconcurrencyで同時起動を抑え、既存の専用ブランチまたはPRがある場合は再作成しません。定期実行、自動修復ループ、自動再試行はありません。
- パッチなし、不正なパッチ、範囲外変更、テスト失敗ではpublishに進みません。Actionsログで失敗理由を確認してください。
- **publishより前に失敗**したIssueは、所有者が内容を修正しラベルを外して付け直すことで新しい試行を開始できます。実行中に本文を書き換える場合は先に実行をキャンセルしてください。
- push/PR作成/dispatchは外部操作のため一括トランザクションではありません。公開処理の途中失敗ではブランチやDraft PRが残ることがあります。ActionsログとGitHub上の状態を確認し、所有者が回復してください。自動で上書き・force pushしません。
- PR作成済みでCI dispatchだけ失敗した場合は、所有者がCIの「Run workflow」でその `autodev/issue-<番号>` を選び、対象head SHAを確認します。
- publishはmainへのpush、自動承認、自動マージ、リリース、secret変更を行いません。なおGitHubのwrite token自体は特定ブランチ専用ではないため、追加の強制が必要なら所有者がmainのruleset/branch protectionを設定してください。

## 最初のテストIssue

タイトル: `Add a regression test for an existing deterministic behavior`

本文例:

> READMEに記載された既存の決定的な挙動を一つ選び、tests/に有用な回帰テストを追加してください。既存の研究仕様、production code、project設定は変更しないでください。ground truthをinference入力に渡さず、同じ入力・seedで同じ結果が得られることを検証してください。

## ローカル検証と残る実機確認

導入時に .NET SDK 8.0.424 で実施:

- 環境確認、restore、Release build: 成功（警告・エラー0）。
- .NET test: 25件成功、失敗・スキップ0。
- `dotnet format FootprintSim.sln --verify-no-changes --no-restore`: 成功。
- S0/S1（seed 42）: 成功、再実行時の各4ファイルがbyte-for-byte一致。
- READMEの評価（2 scenarios × 20 seeds × 4 strategies = 160 runs）: 成功、再実行時の全5ファイルが一致。
- Python構文（4ファイル）、差分保護・プロンプト回帰テスト25件: 成功。
- Actions YAML構文、actionlint 1.7.12、全15 runスクリプトのbash構文: 成功。Actionの固定SHAと入力名は上流のaction.ymlと照合済み。ShellCheckはローカル未導入のため未実行。

再検証コマンド:

```bash
PYTHONDONTWRITEBYTECODE=1 python3 -m unittest discover -s .github/scripts -p 'test_*.py' -v
bash scripts/check-environment.sh
dotnet restore FootprintSim.sln
dotnet build FootprintSim.sln --no-restore -c Release
dotnet test FootprintSim.sln --no-build --no-restore -c Release
dotnet format FootprintSim.sln --verify-no-changes --no-restore
```

導入PRのCIと、APIを使ったIssue→Draft PRの実行は別の確認です。後者にはmainへの手動マージ、API secret、Actions設定、初回Issueが必要です。API実行がまだ未確認の場合、それを成功扱いにしないでください。モデルは固定しておらず、固定CLIの既定値を利用します。アカウントで利用可能かは初回実行で確認し、必要なら所有者がActionの `model` を明示してください。

## 確認した一次資料

- [OpenAI Codex GitHub Action](https://learn.chatgpt.com/docs/github-action)
- [Codex Actionの固定版と入力仕様](https://github.com/openai/codex-action/blob/c05cfd74ad95c626cdf05c6685e62becc3bf7813/action.yml)
- [GitHub: ワークフローからの起動とGITHUB_TOKEN](https://docs.github.com/en/actions/how-tos/write-workflows/choose-when-workflows-run/trigger-a-workflow)
- [GitHub: issues / workflow_dispatchの発火条件](https://docs.github.com/en/actions/reference/workflows-and-actions/events-that-trigger-workflows)

対象範囲はMVPです。自動修復、複数Issueの依存管理、複数リポジトリ運用は含めません。
