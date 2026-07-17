# LogRep2 フェーズ3 作業報告書

## 1. 概要

### 1.1 実施日

2026年7月17日

### 1.2 目的

[LogRep2 改修指示書](LogRep2_改修指示書.md)の「フェーズ3: 設定一本化」と、フェーズ0～2の各報告書に従い、収集設定と分析設定をポータブル統合設定`LogRep2.settings.json`へ一本化した。

設定ファイルは`LogRep2.exe`と同じフォルダーだけに作成し、ユーザーホーム、`AppData`、レジストリへ保存しない。

## 2. 実施内容

### 2.1 Infrastructureプロジェクト

次のプロジェクトを追加した。

```text
src/LogRep2.Infrastructure/
```

責務:

- 統合設定モデル
- UTF-8 JSON読込・保存
- snake_case変換
- スキーマバージョン検証
- 旧設定移行
- 収集設定との相互変換
- 同一フォルダー内の一時ファイルを使った保存
- 設定保存エラーの日本語化

`LogRep2.App`と`LogRep2.Collection.Tests`から参照する。Infrastructureは`LogRep2.Collection`だけを参照し、WPFには依存しない。

### 2.2 統合設定ファイル

ファイル名:

```text
LogRep2.settings.json
```

保存先:

```text
LogRep2.exeと同じフォルダー
```

保存場所を次へ暗黙に切り替える処理は実装していない。

- ユーザーホーム
- `%LOCALAPPDATA%`
- `%APPDATA%`
- レジストリ

exeと同じフォルダーへ保存できない場合、対象となる`LogRep2.settings.json`の絶対パスを含む日本語エラーを返す。

### 2.3 設定構造

設定は次のグループに分割した。

```json
{
  "schema_version": 1,
  "collection": {},
  "analysis": {},
  "overlay": {},
  "application": {}
}
```

#### collection

- TEMPフォルダー
- セッション出力先
- 入力文字コード
- ポーリング間隔
- ウィンドウ1・2監視
- ローテーション数
- raw、canonical出力
- raw、canonical重複排除
- マーカー検出とプレフィックス
- タイムゾーン
- flush間隔
- ハッシュアルゴリズム

#### analysis

- PC登録名
- NPC登録名
- リアルタイム分析更新間隔、初期値500ms

#### overlay

後続フェーズ用として次を保存可能にした。

- 表示有無
- 透過度
- 常に最前面
- 左、上、幅、高さ
- 位置固定
- モニター識別名
- フォントサイズ
- 表示行数
- 表示項目

#### application

- ログレベル
- 起動時自動収集
- 収集開始後のトレイ格納
- 最小化ボタン動作
- 閉じるボタン動作
- トレイ通知

既存24設定項目をすべて移行対象に含めた。

### 2.4 設定スキーマ

初版スキーマバージョンは整数`1`とした。

- 対応外スキーマを黙って読み込まない。
- 不正JSONを黙って初期値へ置き換えない。
- 読込エラー時に既存設定を上書きしない。
- JSONプロパティはsnake_caseで保存する。
- nullの設定グループは読込時に既定グループへ補完する。
- 名前リストは空白除去、重複排除、安定ソートを行う。

### 2.5 保存方式

同じフォルダーに次の一時ファイルを書き、flush完了後に置換する。

```text
LogRep2.settings.json.tmp
```

保存成功後は一時ファイルを残さない。保存失敗時は一時ファイル削除を試み、元の保存例外を優先して日本語IOExceptionを返す。

別フォルダーへのフォールバックは行わない。

### 2.6 旧設定移行

初回読込で統合設定が存在しない場合、exeと同じフォルダーから次を検出する。

```text
config.json
analyzer_settings.json
```

移行規則:

- `config.json`の全収集・アプリ設定を移行する。
- `analyzer_settings.json`のPC名、NPC名を移行する。
- 分析側の旧セッションルートより、収集側`output_dir`を優先する。
- 旧設定を削除しない。
- 旧設定を上書きしない。
- 新設定が存在する場合は旧設定を再移行しない。
- 片方の旧設定が不正でも、もう片方の移行を継続する。
- 移行できなかった旧設定と理由を警告として返す。
- GUIでは移行警告を日本語MessageBoxで表示する。
- CLIでは移行警告を標準エラーへ表示する。

旧設定が一つもない場合も、初回起動時に既定値の統合設定をexeと同じフォルダーへ作成する。

### 2.7 収集GUI設定

既存の収集設定画面は維持し、保存先だけを統合設定へ切り替えた。

- 既存の入力検証を維持
- 相対出力先を設定ファイルのあるフォルダー基準で解決
- 収集中の次回反映警告を維持
- 即時反映可能なポーリング間隔とログレベルを維持
- 保存時にanalysis、overlay設定を失わない

既存`ConfigEditService`には保存コールバックを追加し、旧テストで利用する`ConfigStore`保存経路も互換維持した。

### 2.8 CLI設定

`--config`を指定しない場合、次のコマンドは統合設定を利用する。

- `config get`
- `config set`
- `config path`
- `start`
- `once`

`config path`は`LogRep2.settings.json`の絶対パスを返す。

`--config PATH`を明示した場合は、既存のフラットな`CollectorConfig`形式を一時的な互換経路として引き続き利用できる。明示設定は統合設定を上書きしない。

起動中GUIへ`config-updated`を送る場合、統合設定利用時は空の明示パスを送り、GUI側が統合設定を再読込する。明示`--config`利用時は従来どおりそのパスを送る。

### 2.9 分析設定

既存`AnalyzerSettingsStore`を統合設定アダプターへ変更した。

- PC名とNPC名は`analysis`グループへ保存する。
- 既定セッションルートは常に`collection.output_directory`から取得する。
- 分析画面で別フォルダーを選択した場合は、そのWindowを開いている間だけの一時選択とする。
- 一時選択を保存しても収集出力先を変更しない。
- 収集設定画面で出力先を変更した場合、開いている分析Windowの既定ルートを再読込する。
- 分析設定保存時にcollection、overlay、application設定を失わない。

### 2.10 設定例

`config.example.json`を統合形式へ更新した。実行時ファイル名は`LogRep2.settings.json`であり、exampleは配布時の参照用である。

## 3. テスト追加

### 3.1 Infrastructure

次をテストした。

- 初回読込時に指定したexe相当フォルダーへ統合設定を作成
- 旧収集・分析設定の移行
- 旧設定ファイルを残す
- 新設定がある場合は旧設定で上書きしない
- 収集設定保存時に分析設定を維持
- 不正JSONを黙って初期化しない
- snake_case保存
- 保存不能時の対象パス付き日本語エラー

### 3.2 分析アダプター

次をテストした。

- 収集出力先を分析の既定ルートとして利用
- 分析側の一時ルートを保存しても収集出力先を変更しない
- PC名を既存ルールで正規化
- PC/NPC名を統合設定へ保存

### 3.3 CLI

次をテストした。

- 既定の`config set`が統合設定を更新
- 旧`config.json`を新規作成しない
- 既定の`config get`が統合設定を読む
- `config path`が統合設定パスを返す
- 明示`--config`の既存テストを維持

## 4. 検証結果

### 4.1 NuGet復元

```powershell
dotnet restore LogRep2.sln
```

結果: 成功。

管理環境のユーザーNuGet.Config読取制限により、承認済みのサンドボックス外実行で復元した。

### 4.2 Debug

```powershell
dotnet build LogRep2.sln --no-restore -c Debug
dotnet test LogRep2.sln --no-build --no-restore -c Debug
```

| 項目 | 結果 |
| --- | --- |
| ビルド | 成功、警告0、エラー0 |
| Collection/App/IPC/Infrastructureテスト | 162件成功 |
| Analysisテスト | 135件成功 |
| 合計 | 297件成功、失敗0 |

### 4.3 Release

```powershell
dotnet build LogRep2.sln --no-restore -c Release
dotnet test LogRep2.sln --no-build --no-restore -c Release
```

| 項目 | 結果 |
| --- | --- |
| ビルド | 成功、警告0、エラー0 |
| Collection/App/IPC/Infrastructureテスト | 162件成功 |
| Analysisテスト | 135件成功 |
| 合計 | 297件成功、失敗0 |

### 4.4 書式

```powershell
dotnet format LogRep2.sln --no-restore --verify-no-changes
```

結果: 成功。変更要求なし。

### 4.5 CLI移行スモーク

Debug出力を一時フォルダーへコピーし、同じフォルダーに旧`config.json`を置いて次を実行した。

```powershell
LogRep2.exe config path
LogRep2.exe config get polling_interval_ms
```

結果:

- 両コマンド終了コード0
- `LogRep2.settings.json`をexeと同じ一時フォルダーへ生成
- 旧`config.json`が残存
- schema version 1
- 旧ポーリング間隔1000を移行
- 旧相対出力先`sessions`を移行
- `config path`が統合設定の絶対パスを表示

### 4.6 GUI設定読込スモーク

上記の一時配置からGUIを非表示起動し、5秒後にプロセスが稼働していることを確認後、検証用プロセスを終了した。

結果:

- 統合設定読込後のGUI起動成功
- 起動直後の異常終了なし

## 5. 変更理由

- 収集と分析で重複していた出力先設定を一本化するため。
- 新機能のリアルタイム分析とオーバーレイ設定を一つのポータブル設定で管理するため。
- アプリ更新や移行時に旧設定を失わないようにするため。
- CLI、GUI、分析画面で異なる設定を参照する状態を解消するため。

## 6. 影響範囲

### 6.1 新規・変更

- `LogRep2.Infrastructure`
- `LogRep2.App`のProgram、GUI設定保存、CLI、IPC再読込
- 分析側`AnalyzerSettingsStore`と一時フォルダー選択
- `LogRep2.Collection.Tests`
- `config.example.json`
- `LogRep2.sln`
- `README.md`
- 本報告書

### 6.2 変更していないもの

- 既存`logRep_r`
- 既存`logAnalyzer`
- `AssistantTool`
- セッション出力JSONスキーマ
- 収集Core
- 分析Coreと集計結果
- 旧設定ファイル

## 7. 既知の制限

- 旧設定ファイル内の一つの値がJSON型不正の場合、その旧ファイル全体を移行せず警告する。もう一方の旧設定移行は継続する。
- 分析画面の一時セッションルートはWindowを閉じると失われる。これは収集出力先を一本化するための意図した仕様である。
- `--config`明示時は互換性のため旧フラット収集設定形式を使う。
- リアルタイム分析は未実装。
- オーバーレイUIは未実装。設定モデルだけを先行追加した。
- GUIの移行警告はMessageBox表示であり、恒久的な診断ログ保存は後続の診断実装対象である。

## 8. 次に確認すべき点

フェーズ4のリアルタイム分析では次を実施する。

1. Collectionから読み取り専用canonicalスナップショットを公開する。
2. レコード本体をAnalysis側で二重保持しない。
3. 分析開始、終了、リセット状態を実装する。
4. `analysis.realtime_refresh_interval_ms`を更新間隔へ利用する。
5. 更新要求のデバウンス、キャンセル、最新結果優先を実装する。
6. 同じ分析範囲の保存後再分析結果と一致することを検証する。
7. canonical 10,000件でメモリと処理時間を測定する。

## 9. フェーズ3完了判定

| 指示事項 | 結果 |
| --- | --- |
| `LogRep2.Infrastructure`追加 | 完了 |
| 統合設定モデル | 完了 |
| exeと同じフォルダーへの保存 | 完了 |
| ユーザーホームへ保存しない | 完了 |
| 旧設定移行 | 完了 |
| 旧設定を削除・上書きしない | 完了 |
| 収集GUI設定接続 | 完了 |
| CLI設定接続 | 完了 |
| 分析設定接続 | 完了 |
| 出力先と分析ルート一本化 | 完了 |
| Debug/Releaseビルド | 完了、警告0、エラー0 |
| Debug/Releaseテスト | 完了、各297件成功 |
| CLI・GUIスモーク | 完了 |
| 作業結果の文書化 | 完了、本書 |

フェーズ3は完了と判定する。

