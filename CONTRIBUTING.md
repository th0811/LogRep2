# コントリビューションガイド

LogRep2はWindows向けのログ収集・分析アプリです。AIを使った開発も歓迎します。提出者が変更の目的・理由・検証結果を説明できることを重視します。

## 実装前

[基本設計](docs/architecture.md)、[UIガイド](docs/ui-guidelines.md)、[AGENTS.md](AGENTS.md)を読み、関連コードとテストを確認してください。利用者向け仕様は[README](README.md)を参照してください。

誤字修正や局所的な不具合修正は直接PRを提出できます。次はIssueまたはDraft PRで「問題・変更後の動作・変更箇所・互換性への影響」を示し、メンテナーと方針を合わせてから実装します。

- 新しい画面・操作フロー、集計方法・ログ解釈の変更。
- 保存形式、設定の保存先、プロジェクトの依存方向の変更。
- ライブラリ追加、共通スタイル変更、大規模なリファクタリング。

UI変更には画面案を添えてください。既存仕様が矛盾している場合は、確認結果を共有し、独断で互換性の範囲を広げたり狭めたりしないでください。

## PRの作成

1. 最新の `dev` から作業ブランチを作り、`base: dev` でPRを提出する。外部コントリビューターは元リポジトリの最新devを起点にForkから提出できる。ブランチ名は `fix/対象`、`feature/対象`、`docs/対象`など目的が分かる名前にする。Codexが作成する場合は `codex/` を使う。
2. 1つの目的に絞って変更する。無関係な整形・依存更新は分ける。
3. 下記の検証を行い、PRテンプレートへ結果を記載する。大きな変更は途中でもDraft PRで共有する。
4. 指摘への修正後は影響する検証を再実行する。競合解消や最新devの取り込み後も影響範囲を確認する。

`dev` はレビュー済みの機能を統合して実機確認する常設ブランチ、`main` はリリース対象を確定するブランチです。通常の変更は作業ブランチ → dev、公開準備が整ったらdev → mainのリリースPRとします。両ブランチへの通常の直pushは避けます。

「1つのPRは1つの目的」は機能・修正・文書PRに適用します。dev → mainは確認済みの複数PRを公開版へ昇格させる1つの目的として扱い、含まれるPR一覧、バージョン、統合確認結果を記載します。共同基盤導入時の今回のPRには、メンテナーが許可した例外理由も記載します。

実ログ、個人名・個人パス、秘密情報、ローカル設定、ビルド成果物は追加しないでください。必要な入力例は最小限の合成・匿名化データにし、第三者のコードや画像には出所と利用条件を記載してください。AIの会話全文の提出は不要です。

## 検証

Windowsと `global.json` に指定された.NET 8 SDKを使用します。PowerShellでリポジトリのルートから実行してください。

```powershell
dotnet restore LogRep2.sln
dotnet build LogRep2.sln --no-restore --configuration Debug
dotnet test LogRep2.sln --no-build --no-restore --configuration Debug
dotnet build LogRep2.sln --no-restore --configuration Release
dotnet test LogRep2.sln --no-build --no-restore --configuration Release
dotnet format LogRep2.sln --verify-no-changes --no-restore
```

各コマンドの成功を確認してから次へ進んでください。ビルドは型チェックを含み、`Directory.Build.props` で警告をエラーとして扱います。フォーマット確認は既存設定による静的チェックとして使います。現状、独立したlintコマンドは定義していません。

PT行動HTMLのJavaScriptを変更した場合はNode.jsのテストも実行します。

```powershell
node --test tests/LogRep2.Collection.Tests/PartyTimelinePanel.test.cjs
```

動作変更には、不具合の再発や重要な仕様違反を検出できるテストを追加・更新します。実装をそのままなぞるテストや文書・単純な見た目の変更だけのためのテストは不要です。[テスト整理記録](tests/TEST_SUITE_REVIEW.md)も参考にしてください。テストの削除・期待値変更には仕様上の理由を記載します。

UI変更はWindows実機で変更前後のスクリーンショットと操作結果を示します。保存処理なら保存失敗・再起動、非同期処理ならキャンセル・停止、集計なら代表入力と境界条件を確認してください。実行できない確認や既存の失敗は、コマンド・理由・影響を明記します。無関係な既存不具合を黙って修正しないでください。

文書だけの変更はリンク・記述と実装の整合を中心に確認し、ビルド等を省略した場合はその理由を記載できます。

## GitHub Actionsでの自動検証

[CIワークフロー](.github/workflows/ci.yml)は、すべてのPRの作成・更新時と `main`・`dev` へのpush時に実行します。Actions画面から手動実行もできます。文書だけのPRも実行対象です。

Windows Server 2022で、`global.json` の.NET SDKとNode.js 24を使用します。チェック名は `Windows Debug`、`Windows Release`、`Windows Format`、`Windows JavaScript` です。Debug・Releaseはそれぞれビルドと全.NETテスト、Formatはフォーマット確認、JavaScriptは既存のNode.jsテストを実行します。WPF生成ファイルの競合を避けるため、各ジョブは独立した環境で実行します。

同じPRに新しいコミットを追加すると古いCIを取り消し、最新の変更を検証します。ForkからのPRも読み取り権限で検証し、Secretsは使用しません。GitHub側で外部PRの実行承認が必要な場合は、メンテナーが変更内容を確認して承認します。

GitHubへ反映した後は、試しのPRで4件のチェックが成功することを確認してください。ブランチ保護で必須にする場合は、この4件を登録します。既定ブランチ名を変更する場合はワークフローの `push.branches` も更新してください。CIは実機での画面操作や配布物の確認を代替しません。

## レビューとマージ

当面、コントリビューターのPRはメンテナーが最終確認してマージします。AIレビューは補助として利用し、AIの「問題なし」だけで承認しません。

- 目的、責務分担、操作の統一、データ保護、依存追加、テスト変更の妥当性を確認する。
- 必要な検証が成功し、重要な指摘が解消され、未確認事項をメンテナーが把握していることをマージ条件とする。
- 作業ブランチ → devはSquash mergeでPR単位の履歴を残す。最後の修正後の差分を確認し、他の変更を取り込んだ場合は再検証する。次の作業は最新devから新しいブランチで開始する。
- dev → mainは通常の `Create a merge commit` を使い、常設devの履歴を保持する。mainのlinear historyはOFF、マージ方式にmerge commitを許可する必要がある。
- devへのマージ後にCIと収集・分析・画面・オーバーレイの統合動作を確認する。バージョン更新はdev起点の公開準備ブランチからdevへPRで取り込んでから、dev → mainを提出する。
- mainへのマージ後もCIを確認し、その確定コミットから配布ZIPとタグを作成する。mainのマージ結果は `git merge origin/main` でdevへ取り込む同期PRを作り、merge commitでマージする。同期PRは履歴を取り込む必要があるためSquashしない。devにもmerge commitを許可し、linear historyをOFFにする。

main・devはPR必須、4件のCI必須、未解決会話の解消、最新の対象ブランチへの追従、削除・force push禁止を推奨します。承認件数は当面0件でメンテナーが最終確認し、相互レビューが定着したら1件へ変更します。メンテナー向けの詳細な公開手順書 `GITHUB_RELEASE_UPDATE_GUIDE.md` はリポジトリへ含めません。

この文書は運用ルールです。GitHubのCI・必須チェック・承認条件を自動設定するものではありません。
