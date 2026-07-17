# LogRep2 フェーズ2 作業報告書

## 1. 概要

### 1.1 実施日

2026年7月17日

### 1.2 目的

[LogRep2 改修指示書](LogRep2_改修指示書.md)の「フェーズ2: UI統合」、[フェーズ0 現状調査報告書](Phase0_現状調査報告書.md)、[フェーズ1 作業報告書](Phase1_作業報告書.md)に従い、既存の収集WPFアプリ、分析WPFアプリ、タスクトレイ、CLI、単一起動、IPCを一つの`LogRep2.exe`へ統合した。

設定一本化はフェーズ3の対象であるため、本フェーズでは既存設定形式と保存処理を維持した。

## 2. 実施内容

### 2.1 追加プロジェクト

次の2プロジェクトを`LogRep2.sln`へ追加した。

| プロジェクト | ターゲット | 責務 |
| --- | --- | --- |
| `LogRep2.App` | `net8.0-windows` | WPF、トレイ、GUI制御、CLI |
| `LogRep2.Ipc` | `net8.0` | 単一起動、名前付きパイプIPC |

`LogRep2.App`は次を参照する。

- `LogRep2.Collection`
- `LogRep2.Analysis`
- `LogRep2.Ipc`

出力ファイル名は`LogRep2.exe`、Productは`LogRep2`とした。WPFとWindows Formsを有効にし、既存のコンソール兼GUIエントリーポイントを維持している。

### 2.2 収集UI移植

移植元:

```text
logRep_r/src/FfxiTempLogCollector.App/
```

移植先:

```text
logRep2/src/LogRep2.App/
```

次の既存機能を移植した。

- 収集開始、停止
- 収集状態、件数、警告、エラー表示
- TEMPフォルダー、出力フォルダー表示・オープン
- 既存設定画面
- 起動時自動収集
- 最小化、閉じるボタン動作
- タスクトレイ、通知
- CLI
- GUIコントローラー

利用者向け表示を次のように更新した。

- ウィンドウタイトル: `LogRep2`
- 画面ヘッダー: `LogRep2`
- トレイアプリ名: `LogRep2`
- CLIヘルプのexe名: `LogRep2.exe`

### 2.3 分析UI移植

移植元:

```text
logAnalyzer/src/FFXI_LogAnalyzer.App/
```

移植先:

```text
logRep2/src/LogRep2.App/Analyzer/
```

既存の分析画面、ViewModel、UserControl、登録名管理、設定保存処理を移植した。

重複するApplication定義とThemeInfoは収集側ホストへ一本化したため、分析側の次のファイルは移植していない。

- `App.xaml`
- `App.xaml.cs`
- `AssemblyInfo.cs`
- 旧`.csproj`

分析ウィンドウタイトルは`LogRep2 - 過去ログ分析`へ変更した。

### 2.4 UI統合方式

初版の安全な統合として、収集メイン画面をアプリケーションのホストWindowとし、過去ログ分析を同一プロセス内の独立Windowとして開く方式を採用した。

メイン画面へ次のボタンを追加した。

```text
過去ログ分析
```

タスクトレイメニューにも同じ項目を追加した。

分析Windowの管理ルール:

- 同時に一つだけ作成する。
- 既に開いている場合は新規作成せず前面へ戻す。
- 最小化されている場合は通常表示へ戻す。
- 収集メインWindowをOwnerとする。
- 分析Windowを閉じた後は再度作成可能とする。
- アプリ終了時は分析Windowも閉じる。

この方式により、既存分析画面の機能とレイアウトを崩さず、一つのexe・一つのプロセスへ統合した。

### 2.5 IPC識別名

旧アプリと同時に存在しても競合しないよう、移植先だけ次へ変更した。

```text
Mutex: Local\LogRep2.Gui.Singleton
Pipe:  LogRep2.CommandPipe
```

既存`logRep_r`側の識別名は変更していない。

### 2.6 CLI

既存の次のCLIを継続した。

- `help`
- `start`
- `stop`
- `status`
- `once`
- `config get`
- `config set`
- `config path`
- `--config`
- `--temp-dir`
- `--output-dir`

コマンド体系は本フェーズでは変更せず、ヘルプ上の製品名とexe名だけをLogRep2へ更新した。

### 2.7 App/IPCテスト移植

フェーズ1で保留していた次の旧テスト5ファイルを移植した。

- `CliCommandControllerTests.cs`
- `CliCommandParserTests.cs`
- `ConfigEditServiceTests.cs`
- `IpcTests.cs`
- `WindowCloseBehaviorControllerTests.cs`

`LogRep2.Collection.Tests`を`net8.0-windows`へ変更し、AppとIPCへの参照を追加した。

さらに、統合された分析WindowのXAMLとViewModelが同一Appアセンブリ内で初期化できることを確認する`IntegratedWindowTests`を追加した。テストはSTAスレッドでWindowを生成し、タイトルと例外なしの初期化を検証する。

### 2.8 設定ファイル

フェーズ2では既存設定形式を維持する。

- 収集設定: `config.json`
- 分析設定: `analyzer_settings.json`
- どちらもデフォルトでは`LogRep2.exe`と同じフォルダー

`config.example.json`を`logRep2`直下へ追加し、ビルド出力へコピーするよう設定した。

設定一本化、`LogRep2.settings.json`、旧設定移行はフェーズ3で実施する。

## 3. 検証結果

### 3.1 NuGet復元

```powershell
dotnet restore LogRep2.sln
```

結果: 成功。

管理環境のユーザーNuGet.Config読取制限により、承認済みのサンドボックス外実行で復元した。

### 3.2 Debugビルド

```powershell
dotnet build LogRep2.sln --no-restore -c Debug
```

結果:

- 成功
- 警告0
- エラー0
- `LogRep2.exe`生成確認

### 3.3 Debugテスト

```powershell
dotnet test LogRep2.sln --no-build --no-restore -c Debug
```

| テストプロジェクト | 合格 | 失敗 | スキップ |
| --- | ---: | ---: | ---: |
| `LogRep2.Collection.Tests` | 152 | 0 | 0 |
| `LogRep2.Analysis.Tests` | 135 | 0 | 0 |
| 合計 | 287 | 0 | 0 |

### 3.4 Releaseビルド

```powershell
dotnet build LogRep2.sln --no-restore -c Release
```

結果:

- 成功
- 警告0
- エラー0

### 3.5 Releaseテスト

```powershell
dotnet test LogRep2.sln --no-build --no-restore -c Release
```

| テストプロジェクト | 合格 | 失敗 | スキップ |
| --- | ---: | ---: | ---: |
| `LogRep2.Collection.Tests` | 152 | 0 | 0 |
| `LogRep2.Analysis.Tests` | 135 | 0 | 0 |
| 合計 | 287 | 0 | 0 |

### 3.6 CLIスモークテスト

```powershell
LogRep2.exe help
```

結果:

- 終了コード0
- 製品名`LogRep2`を表示
- コマンド例が`LogRep2.exe`へ更新済み

### 3.7 GUI起動スモークテスト

Debug版`LogRep2.exe`を非表示で起動し、5秒後にプロセスが稼働中であることを確認した。その後、検証用プロセスを終了した。

結果:

- 起動成功
- 起動直後の異常終了なし

### 3.8 分析Window初期化テスト

STAスレッドで`FFXI_LogAnalyzer.App.MainWindow`を初期化した。

結果:

- 例外なし
- タイトル`LogRep2 - 過去ログ分析`
- Window終了成功

### 3.9 書式検証

```powershell
dotnet format LogRep2.sln --no-restore --verify-no-changes
```

結果: 成功。変更要求なし。

## 4. 変更理由

- 収集と過去ログ分析を一つのWindowsアプリから利用可能にするため。
- 既存機能を維持しながら、設定統合とリアルタイム分析の土台を作るため。
- 旧アプリとLogRep2の単一起動・IPC識別名が競合しないようにするため。
- フェーズ1で保留したApp/IPCテストを復元するため。

## 5. 影響範囲

### 5.1 新規・変更

- `LogRep2.App`
- `LogRep2.Ipc`
- `LogRep2.Collection.Tests`
- `LogRep2.sln`
- `logRep2/config.example.json`
- `logRep2/README.md`
- 本報告書

### 5.2 変更していないもの

- 既存`logRep_r`
- 既存`logAnalyzer`
- `AssistantTool`
- 既存セッションJSONスキーマ
- 既存収集・分析Coreロジック
- ユーザーホーム以下への設定保存

## 6. 既知の制限

- 収集設定と分析設定はまだ別ファイルである。
- 収集出力先と分析セッションルートはまだ自動一本化されていない。
- 収集と分析は同一プロセスだが、canonicalレコードはまだメモリ連携していない。
- リアルタイム分析は未実装。
- オーバーレイは未実装。
- Appと分析UIの旧名前空間は互換移植のため残っている。
- GUIスモークテストは起動確認であり、人間による全画面操作確認ではない。

## 7. 次に確認すべき点

フェーズ3の設定一本化では次を実施する。

1. `LogRep2.Infrastructure`を追加する。
2. `LogRep2.settings.json`をexeと同じフォルダーへ保存する。
3. `config.json`と`analyzer_settings.json`を初回移行する。
4. 収集出力先を分析のデフォルトセッションルートとして共用する。
5. 新設定が存在する場合は旧設定で上書きしない。
6. 書き込み不能時に別場所へ暗黙保存しない。
7. 旧設定を削除、上書きしない。

フェーズ3で設定経路が変わるため、現在の`ConfigStore`、`AnalyzerSettingsStore`を直接全面改修せず、移行と互換アダプターを用意すること。

## 8. フェーズ2完了判定

| 指示事項 | 結果 |
| --- | --- |
| `LogRep2.App`構築 | 完了 |
| `LogRep2.Ipc`構築 | 完了 |
| 収集画面移植 | 完了 |
| 過去ログ分析画面移植 | 完了 |
| 単一アプリから両機能を起動 | 完了 |
| タスクトレイ統合 | 完了 |
| 単一起動・IPC統合 | 完了 |
| App/IPCテスト移植 | 完了 |
| Debug/Releaseビルド | 完了、警告0、エラー0 |
| Debug/Releaseテスト | 完了、各287件成功 |
| CLI・GUIスモーク確認 | 完了 |
| 作業結果の文書化 | 完了、本書 |

フェーズ2は完了と判定する。

