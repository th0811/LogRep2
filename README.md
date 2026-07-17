# LogRep2

`logRep_r`のログ収集機能と`logAnalyzer`のログ分析機能を統合したWindowsアプリである。ログ収集、過去ログ分析、リアルタイム分析、分析結果オーバーレイを一つの`LogRep2.exe`で利用できる。

## 対応環境

- Windows 10 64bit
- Windows 11 64bit
- 配布対象アーキテクチャ: x64
- FFXIのウィンドウモード、枠なしウィンドウモード

x64自己完結型配布物には.NETランタイムが含まれるため、利用PCへ.NET 8 Desktop Runtimeを別途インストールする必要はない。

## 導入方法

1. `LogRep2-win-x64.zip`を任意の書込み可能なフォルダーへ展開する。
2. `LogRep2.exe`を起動する。
3. 「設定」でFFXI TEMPフォルダーとセッション出力先を確認する。
4. 「収集開始」を押してログ収集を開始する。

設定はポータブル方式で、`LogRep2.exe`と同じフォルダーの`LogRep2.settings.json`へ保存する。`Program Files`など一般ユーザーが書き込めないフォルダーは避けること。書込みに失敗してもAppDataなどへ暗黙に切り替えない。

ユーザーホーム、`%APPDATA%`、`%LOCALAPPDATA%`、レジストリへ設定を保存しない。

## 旧アプリからの移行

初回移行では、新しい`LogRep2.exe`を旧`logRep_r`と`logAnalyzer`の設定ファイルがあるフォルダーへ配置して起動する方法を推奨する。

同じフォルダーに次のファイルがある場合、初回起動時に検出する。

- `config.json`: 収集設定
- `analyzer_settings.json`: PC名、NPC名などの分析設定

移行結果は`LogRep2.settings.json`へ保存する。旧設定ファイルは削除・上書きしない。新設定が既にある場合は旧設定で上書きしない。セッション出力先は`config.json`の値を優先し、過去ログ分析の既定セッションルートにも使用する。

旧アプリ名の互換ランチャーは提供しない。移行確認後も旧アプリを残す場合、同じTEMPログを同時収集しないこと。

## 更新方法

1. 収集を停止し、LogRep2を終了する。
2. 現在の`LogRep2.settings.json`をバックアップする。
3. 新しい配布物を同じフォルダーへ上書き展開する。
4. `LogRep2.settings.json`が保持されていることを確認して起動する。
5. 設定、収集出力先、過去ログ一覧、オーバーレイ位置を確認する。

配布物の`config.example.json`は設定例であり、既存`LogRep2.settings.json`を置き換えるファイルではない。

## GUI機能

### ログ収集

- 収集開始・停止
- TEMPログのデコード、正規化、重複排除
- raw、canonical、セッション状態、統計情報の保存
- タスクトレイ格納と単一起動制御

セッションフォルダーには次を出力する。

```text
session.json
raw_records.jsonl
canonical_records.jsonl
state.json
stats.json
```

### 過去ログ分析

メイン画面またはタスクトレイの「過去ログ分析」から開く。収集出力先を既定セッションルートとして使用する。一時的に別フォルダーを開いても収集出力先は変更しない。

旧`logRep_r`が出力したschema version 1.0のセッションを読み込み可能である。

### リアルタイム分析

- 「分析開始」以後のcanonicalレコードを対象とする。
- 「分析終了」時点で範囲を固定する。
- 「分析リセット」で結果を消去し、リセット後のレコードから分析を継続する。
- 初版はメモリ上の対象snapshot全件再集計方式で、差分集計は行わない。
- 更新要求を250～1,000msでまとめ、古い結果を破棄して最新結果を優先する。

メイン画面では合計ダメージ、DPS、対象件数、再集計時間、破棄回数、プロセスメモリなどを確認できる。

### オーバーレイ

メイン画面またはタスクトレイから表示を切り替える。

- 合計ダメージ、DPS、命中率、actor別順位、分析状態を表示
- 透明度、文字サイズ、常に最前面を変更可能
- 編集状態で移動・サイズ変更
- 固定状態で意図しない移動・サイズ変更を防止
- 位置リセット、モニター切断時の画面内復元
- 非表示時はリアルタイム描画更新を停止

## AssistantTool

HTMLベースのログ再現ツールはLogRep2へ統合していない。配布物の`AssistantTool/index.html`をEdgeなどで開き、LogRep2が出力した`raw_records.jsonl`を従来どおりドラッグ＆ドロップして使用する。

詳細は`AssistantTool/README.md`を参照すること。

## CLI確定仕様

引数なしで起動するとGUIを開始する。引数を指定するとCLIとして動作する。

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

`--config`はコマンド位置の前後どちらでも指定できる。省略時はexeと同じフォルダーの`LogRep2.settings.json`を使う。明示時は旧CLI互換のフラットな収集設定JSONとして扱い、統合設定は変更しない。

### コマンド動作

| コマンド | 動作 |
| --- | --- |
| `help` | 使用方法を表示 |
| `start` | GUI起動中ならIPCでGUI収集を開始。それ以外はCLIプロセスで収集し、Ctrl+Cまで継続 |
| `stop` | GUI起動中ならIPCでGUI収集を停止。GUIがなければ非実行を通知 |
| `status` | GUI起動中ならIPCで状態取得。GUIがなければCLIローカル状態を表示 |
| `once` | TEMPログを一度だけ読み取り、completedセッションを作成 |
| `config path` | 実際に使用する設定ファイルパスを表示 |
| `config get` | 収集・アプリ設定値を表示 |
| `config set` | 設定値を検証して保存。一部設定は起動中GUIへ即時通知 |

リアルタイム分析とオーバーレイのCLI操作は初版では提供しない。

### 設定キー

文字列:

```text
temp_dir
output_dir
encoding
marker_prefix
timezone
hash_algorithm
log_level
minimize_button_behavior
close_button_behavior
```

整数:

```text
polling_interval_ms
rotation_slots
flush_interval_ms
```

真偽値（`true`または`false`）:

```text
watch_window1
watch_window2
raw_output
canonical_output
dedupe_raw
dedupe_canonical
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
| 3 | 設定読込・保存エラー |
| 4 | 収集またはIPC操作エラー |
| 5 | 停止対象が実行されていない |
| 10 | 予期しないエラー |

## 制限事項

- オーバーレイのクリック透過は初版対象外。背後のゲームへクリックを渡さない。
- オーバーレイはウィンドウモードと枠なしウィンドウモードを対象とする。
- 排他的フルスクリーンは保証対象外。DirectX描画挿入は行わない。
- 初版のリアルタイム分析は差分集計ではなく全件再集計。
- 旧CLIとの完全互換は保証しない。
- 旧アプリ名exeの互換ランチャーは提供しない。
- `AssistantTool`の既知の表示制限は同梱の`AssistantTool/README.md`を参照すること。

## トラブルシューティング

- 設定を保存できない: LogRep2フォルダーへの書込み権限を確認し、書込み可能なフォルダーへ配布物全体を移動する。
- TEMPログを収集できない: TEMPフォルダー、監視ウィンドウ、ローテーションスロット数を確認する。
- 過去ログが表示されない: 収集出力先とセッション内の`session.json`、`canonical_records.jsonl`を確認する。
- オーバーレイが画面外にある: メイン画面から表示後、オーバーレイの「位置リセット」を実行する。
- 排他的フルスクリーン上に表示されない: ウィンドウモードまたは枠なしウィンドウモードへ切り替える。

## 開発

開発時は次の文書を基準とする。

1. [LogRep2 改修指示書](LogRep2_改修指示書.md)
2. [フェーズ0 現状調査報告書](Phase0_現状調査報告書.md)
3. [フェーズ1 作業報告書](Phase1_作業報告書.md)
4. [フェーズ2 作業報告書](Phase2_作業報告書.md)
5. [フェーズ3 作業報告書](Phase3_作業報告書.md)
6. [フェーズ4 作業報告書](Phase4_作業報告書.md)
7. [フェーズ5 作業報告書](Phase5_作業報告書.md)
8. [フェーズ6 作業報告書](Phase6_作業報告書.md)
9. [ビルド手順書](ビルド手順書.md)

ビルドとテスト:

```powershell
dotnet restore LogRep2.sln
dotnet build LogRep2.sln --no-restore -c Debug
dotnet test LogRep2.sln --no-build --no-restore -c Debug
dotnet build LogRep2.sln --no-restore -c Release
dotnet test LogRep2.sln --no-build --no-restore -c Release
dotnet format LogRep2.sln --verify-no-changes --no-restore
```

x64配布物:

```powershell
dotnet publish src/LogRep2.App/LogRep2.App.csproj `
  --no-restore `
  -p:PublishProfile=win-x64 `
  -o artifacts/LogRep2-win-x64
```

## 文字コード

- ソース、設定、JSON、JSONLはUTF-8。
- FFXI TEMPログの入力文字コード`cp932`とは区別する。
- Windows PowerShellでBOMなしUTF-8を読む場合は`-Encoding UTF8`を明示する。
