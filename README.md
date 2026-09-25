# LogRep2

`logRep_r`のログ収集機能と`logAnalyzer`のログ分析機能を統合したWindowsアプリです。  
ログ収集、過去ログ分析、リアルタイム分析、分析結果オーバーレイを一つのアプリで利用できます。

ビルド・実行には .NET 8 SDKが必要です。  
https://dotnet.microsoft.com/ja-jp/download/dotnet/8.0

ビルド済み実行ファイルは [Releases](https://github.com/th0811/LogRep2/releases) で公開しています。

.NET 8 SDKがインストールされていない端末で実行したい場合は  
[Releases](https://github.com/th0811/LogRep2/releases) より、 `self-contained` バージョンをダウンロードしてください。

## publish（exeファイルの生成）

### ランタイムなし版

```powershell
dotnet publish src/LogRep2.App/LogRep2.App.csproj `
  -c Release `
  -r win-x64 `
  --self-contained false `
  -p:PublishProfile=win-x64 `
  -o publish/
```

### 自己完結式（ランタイム同梱版）

.NET 8 Desktop RuntimeがインストールされていないPCでも実行可能ですが、容量が肥大化します。

```powershell
dotnet publish src/LogRep2.App/LogRep2.App.csproj `
  -c Release `
  -r win-x64 `
  --self-contained true `
  -p:PublishProfile=win-x64 `
  -o publish/
```

### 配布用ZIP
.NET 8 SDK とWindows環境が必要です。

配布用ZIPとSHA-256ファイルを作成する場合は、次のスクリプトを使用します。全テストが成功した場合だけ、`artifacts`フォルダーへ標準のランタイム非同梱版を作成します。

```powershell
.\scripts\Publish-Release.ps1 -Version 0.2.0
```

```text
artifacts/
├─ LogRep2-0.2.0-win-x64/
├─ LogRep2-0.2.0-win-x64.zip
└─ LogRep2-0.2.0-win-x64.zip.sha256
```

ランタイム同梱版も追加で作成する場合は、`-IncludeSelfContained`を指定します。

```powershell
.\scripts\Publish-Release.ps1 -Version 0.2.0 -IncludeSelfContained
```

ランタイム同梱版には、ファイル名の末尾に`-self-contained`が付きます。

```text
artifacts/
├─ LogRep2-0.2.0-win-x64-self-contained/
├─ LogRep2-0.2.0-win-x64-self-contained.zip
└─ LogRep2-0.2.0-win-x64-self-contained.zip.sha256
```

## 基本的な使い方

1. LogRep2を起動する。
2. 「収集設定」でFFXIのTEMPフォルダーとログの出力先を指定する。
3. 「収集開始」を押す。
4. 必要に応じて「PTメンバー設定...」でメンバーを登録する。
5. 「分析開始」を押すと、リアルタイム分析とオーバーレイを利用できる。
6. 保存済みログを調べる場合は「過去ログ分析」を開く。

設定はリポジトリまたはexeと同じフォルダーの`LogRep2.settings.json`に保存されます。

### TEMPログを収集する

画面上部の「TEMPログ収集」から開始・停止します。収集したログは、収集設定で指定した出力先へセッション単位で保存されます。

### リアルタイム分析を使う

「リアルタイム分析」から分析を開始します。PTメンバーを登録すると、メンバーごとのDPSと通常攻撃命中率がオーバーレイに表示されます。

- 「分析開始」以後のログが集計対象になる。
- 既定では「分析開始」時にオーバーレイも表示する。設定から自動表示を無効にできる。
- リアルタイム分析はTEMPログ収集中のみ開始できる。分析中に収集が終了した場合は、結果を保持して分析も終了する。
- 「分析停止」で現在の分析範囲を確定する。
- 「分析リセット」で結果を消去し、リセット後のログから再集計する。
- オーバーレイは常時ドラッグ移動できる。

### 過去ログを分析する

「過去ログ分析」を開き、保存済みセッションと分析範囲を選択します。手動マーカーのほか、エリアチェンジから生成された区間も選択できます。

### AssistantToolを使う

過去ログ分析画面でセッションを選択し、「ゲーム内ログを表示」を押すと、ボタンを押した時点の`raw_records.jsonl`を既定のブラウザで表示します。収集中のセッションも表示できます。

AssistantToolを単体で使う場合は、`AssistantTool/index.html`をEdgeなどで開き、LogRep2が出力した`raw_records.jsonl`をドラッグ＆ドロップします。

詳しい使い方は`AssistantTool/README.md`を参照してください。

## 配布版を使う

1. `LogRep2-バージョン-win-x64.zip`を任意の書き込み可能なフォルダーへ展開する。
2. `LogRep2.exe`を起動する。
3. TEMPフォルダーと出力先を設定する。
4. 「収集開始」を押す。

標準のランタイム非同梱版を利用するには、利用PCへ.NET 8 Desktop Runtimeを別途インストールする必要があります。ファイル名に`-self-contained`が付くランタイム同梱版では、別途インストールする必要はありません。

設定はポータブル方式です。`Program Files`など一般ユーザーが書き込めないフォルダーは避けてください。設定をAppDataやレジストリへ保存することはありません。

予期しないエラーと操作エラーは、exeと同じ場所の`logs`フォルダーへ日単位で記録されます。メイン画面の「診断ログ」からフォルダーを開けます。診断ログは30日を超えると起動時に削除されます。

## GUI機能の詳細

### ログ収集

- TEMPログのデコード、正規化、重複排除
- raw、canonical、セッション状態、統計情報の保存
- タスクトレイ格納と単一起動制御
- 入力仕様はFFXI向けのCP932、Asia/Tokyo、SHA-1、canonical重複排除に固定
- canonical、状態、統計は変更中に5秒間隔でチェックポイント保存し、収集終了時に確定

セッションフォルダーには次のファイルを出力します。

```text
session.json
raw_records.jsonl
canonical_records.jsonl
state.json
stats.json
```

### 過去ログ分析

収集出力先をセッションルートとして使用します。出力先の変更はメイン画面の設定から行い、過去ログ分析画面から現在の出力先をエクスプローラーで開けます。

旧`logRep_r`が出力したschema version 1.0のセッションも読み込めます。

セッションの読込、複数セッションの結合、分析はバックグラウンドで実行され、処理中にキャンセルできます。

- 選択したセッションフォルダーをエクスプローラーで開ける
- 一覧の全セッションをまとめて分析対象にする、または分析対象から外せる
- 分析対象のチェック状態は変更時に保存され、次回起動時に復元される（新しいセッションは選択状態で追加）
- 一覧のエイリアスセルをクリック、F2、または右クリックの「エイリアスを編集」で直接入力できる（100文字以内、空欄で解除、重複可）。Enterまたはフォーカス移動で保存、Escで取消。保存失敗時は入力内容を保ってエラーを表示し、設定読込エラーのある行は編集できない
- エイリアス・セッションIDの部分一致検索で一覧を絞り込める。非表示のセッションの分析対象チェックは維持され、一括操作は非表示分にも適用される
- エイリアスは各セッションの`session_annotations.json`に保存し、セッションID・ログ本体・選択状態・除外設定は変更しない。収集中も編集可能で、フォルダーごとコピーするとエイリアスも引き継がれる。読み込みに失敗した場合は警告とID表示で分析を続行し、設定を修復して「更新」するまでエイリアスの上書きを停止する
- 「ログ確認・除外」で本文を検索し、ログ一覧から行単位・グループ単位で分析対象から除外／復元できる
- 収集終了済みセッションを確認後にごみ箱へ移動できる
- 操作手順はタブ見出しの完了マークで確認でき、分析対象と出力先は上部の1行に表示する
- キャラクター別、アクション別、レベル上げ集計をUTF-8 BOM付きCSVで出力できる（既定ファイル名には分析日時と区間名を付与）
- DPS・平均ダメージは桁区切り付き小数2桁、最大・最小ダメージは整数で表示・CSV出力する
- キャラクター一覧の「登録…」または右クリックからPC・NPC分類を変更できる

「ログ確認・除外」では、例えば「トアクリーバの構え」を検索して対象行を選べます。ログ一覧はCtrl／Shiftで複数行を選択できます。「グループを除外」は検索に出ていない行を含む同一セッション・同一event_groupの全行に適用します。構えとダメージが別グループの場合は、検索条件を変更するか空欄で検索して、対象のダメージ行を選択してください。「グループを戻す」は、そのグループ内の行単位の除外も解除します。

除外設定は各セッションの`analysis_exclusions.json`へ操作ごとに保存し、次回読込時にも反映します。元ログは変更しません。同一グループへ後から追記された行も、セッションを更新するとグループ除外の対象になります。行IDがない旧ログは行単位での操作ができませんが、セッションID・event_groupがあればグループ操作が可能です。

除外後は元の分析区間で再分析してください。分析時間・マーカー・エリア区間は元ログから求め、ダメージ・回数・レベル上げ集計には除外後のログを使います。DPSは除外後のダメージを元の分析時間で割ります。ダメージ行だけを除くと、残った行が使用回数や判定不明として集計される場合があるため、行動全体を外す用途にはグループ除外を推奨します。手動除外は過去ログ分析とそのCSV出力に適用され、リアルタイム分析・オーバーレイには適用されません。

除外設定を読み込めない場合は、警告を表示して該当セッションを含む分析と除外設定の編集を停止します。設定ファイルを修復して「更新」するか、該当セッションを分析対象から外してください。

分析範囲は手動マーカーに加え、`=== エリア名 ===`形式のログから生成したエリア滞在区間でも指定できます。エリア滞在区間はcanonicalのorderで区切られ、エリアチェンジ行の直後から次のエリアチェンジ行の直前までが対象です。同名エリアは訪問回数、通番、order範囲、ログ件数で区別します。

### リアルタイム分析

- PTメンバーは最大6名まで登録可能
- ログから検出したPC候補からも登録可能
- PTメンバー設定は保存され、オーバーレイへ即時反映
- メイン画面に合計ダメージ、DPS、対象件数、再集計時間、破棄回数、プロセスメモリを表示
- 更新要求を250～1,000msでまとめ、古い結果を破棄して最新結果を優先
- canonical件数が増えていない重複更新は再分析しない
- 分析停止時など同一範囲の結果は再集計せず再利用

初版はメモリ上の対象snapshotを全件再集計する方式です。

### オーバーレイ

- PTメンバーごとのDPSと通常攻撃命中率を登録順で表示
- 登録状態にかかわらず、オーバーレイ上部の「PT設定」からメンバー設定を開ける
- 透明度、文字サイズ、常に最前面を変更可能
- 常時ドラッグ移動・サイズ変更
- モニター切断時に画面内へ復元
- 非表示時はリアルタイム描画更新を停止

## 旧アプリからの移行

初回移行では、新しい`LogRep2.exe`を旧`logRep_r`と`logAnalyzer`の設定ファイルがあるフォルダーへ配置して起動する方法を推奨します。

同じフォルダーに次のファイルがある場合、初回起動時に検出します。

- `config.json`: 収集設定
- `analyzer_settings.json`: PC名、NPC名などの分析設定

移行結果は`LogRep2.settings.json`へ保存します。旧設定ファイルは削除・上書きしません。新設定が既にある場合も旧設定で上書きしません。

`LogRep2.settings.json`を読み込めない場合は、確認後に`LogRep2.settings.invalid-日時.json`へバックアップし、初期設定で起動できます。

旧アプリを残す場合、同じTEMPログを旧アプリとLogRep2で同時収集しないでください。

## 更新方法

1. 収集を停止してLogRep2を終了する。
2. `LogRep2.settings.json`をバックアップする。
3. 新しい配布物を同じフォルダーへ上書き展開する。
4. 設定ファイルが保持されていることを確認して起動する。
5. 収集設定、過去ログ一覧、オーバーレイ位置を確認する。

`config.example.json`は開発時の参照用としてリポジトリにのみ保持し、リリース成果物には含めません。

GitHub Releasesを使った開発者向けの公開手順と、利用者向けの詳しい更新手順は、[LogRep2のバージョンアップと更新手順](GITHUB_RELEASE_UPDATE_GUIDE.md)を参照してください。

## CLI

引数なしで起動するとGUIを開始します。引数を指定するとCLIとして動作します。

まずコマンド一覧を確認する場合:

```powershell
LogRep2.exe help
```

### コマンド一覧

```text
LogRep2.exe help
LogRep2.exe start [--temp-dir PATH] [--output-dir PATH]
LogRep2.exe stop
LogRep2.exe status
LogRep2.exe once [--temp-dir PATH] [--output-dir PATH]
LogRep2.exe config path
LogRep2.exe config get KEY
LogRep2.exe config set KEY VALUE
```

共通オプション:

```text
--config PATH
```

`--config`はコマンド位置の前後どちらでも指定できます。省略時はexeと同じフォルダーの`LogRep2.settings.json`を使用します。

### コマンド動作

| コマンド | 動作 |
| --- | --- |
| `help` | 使用方法を表示 |
| `start` | GUI起動中ならGUIの収集を開始。それ以外はCtrl+CまでCLIで収集 |
| `stop` | GUI起動中の収集を停止 |
| `status` | 収集状態を表示 |
| `once` | TEMPログを一度だけ読み取り、completedセッションを作成 |
| `config path` | 使用する設定ファイルのパスを表示 |
| `config get` | 設定値を表示 |
| `config set` | 設定値を検証して保存 |

リアルタイム分析とオーバーレイのCLI操作は提供していません。

### 設定キー

文字列:

```text
temp_dir
output_dir
marker_prefix
log_level
minimize_button_behavior
close_button_behavior
```

整数:

```text
polling_interval_ms
rotation_slots
```

真偽値（`true`または`false`）:

```text
watch_window1
watch_window2
raw_output
canonical_output
dedupe_raw
marker_detection
auto_start_collection_on_launch
minimize_to_tray_while_collecting
show_tray_notifications
```

### 終了コード

| コード | 意味 |
| ---: | --- |
| 0 | 成功 |
| 2 | 引数または設定キー・値が不正 |
| 3 | 設定の読み込み・保存エラー |
| 4 | 収集またはIPC操作エラー |
| 5 | 停止対象が実行されていない |
| 10 | 予期しないエラー |

## 制限事項

- オーバーレイは背後のゲームへクリックを渡さない。
- オーバーレイはウィンドウモードと枠なしウィンドウモードを対象とする。
- 排他的フルスクリーンは保証対象外。
- リアルタイム分析は差分集計ではなく全件再集計。
- 旧CLIとの完全互換は保証しない。
- 旧アプリ名exeの互換ランチャーは提供しない。
- AssistantToolの既知の表示制限は`AssistantTool/README.md`を参照すること。

## トラブルシューティング

- 設定を保存できない: LogRep2を一般ユーザーが書き込めるフォルダーへ移動する。
- TEMPログを収集できない: TEMPフォルダー、監視ウィンドウ、ローテーションスロット数を確認する。
- 過去ログが表示されない: 収集出力先とセッション内の`session.json`、`canonical_records.jsonl`を確認する。
- オーバーレイが画面外にある: アプリを再起動すると、接続中モニターの作業領域内へ自動補正される。
- 排他的フルスクリーン上に表示されない: ウィンドウモードまたは枠なしウィンドウモードへ切り替える。

## 文字コード

- ソース、設定、JSON、JSONLはUTF-8。
- FFXI TEMPログの入力文字コード`cp932`とは区別する。
- Windows PowerShellでBOMなしUTF-8を読む場合は`-Encoding UTF8`を明示する。

## 開発用コマンド

### Debugのビルドとテスト

```powershell
dotnet restore LogRep2.sln
dotnet build LogRep2.sln --no-restore --configuration Debug
dotnet test LogRep2.sln --no-build --no-restore --configuration Debug
```

### Releaseのビルドとテスト

```powershell
dotnet build LogRep2.sln --no-restore --configuration Release
dotnet test LogRep2.sln --no-build --no-restore --configuration Release
```

### フォーマット確認

```powershell
dotnet format LogRep2.sln --verify-no-changes --no-restore
```
