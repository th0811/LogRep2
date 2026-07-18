# LogRep2 フェーズ1 作業報告書

## 1. 概要

### 1.1 実施日

2026年7月17日

### 1.2 目的

[LogRep2 改修指示書](LogRep2_改修指示書.md)の「フェーズ1: ソリューション構築と既存Core移植」および[フェーズ0 現状調査報告書](Phase0_現状調査報告書.md)の引継事項に従い、LogRep2の新規ソリューションを構築し、既存収集Core、分析Core、回帰テスト、分析フィクスチャを移植した。

## 2. 実施内容

### 2.1 ソリューション構築

次のソリューションを作成した。

```text
logRep2/LogRep2.sln
```

フェーズ1完了時点の構成:

```text
logRep2/
├─ LogRep2.sln
├─ Directory.Build.props
├─ global.json
├─ README.md
├─ LogRep2_改修指示書.md
├─ Phase0_現状調査報告書.md
├─ Phase1_作業報告書.md
├─ baseline/
│  ├─ README.md
│  └─ expected_analysis.json
├─ src/
│  ├─ LogRep2.Collection/
│  └─ LogRep2.Analysis/
└─ tests/
   ├─ LogRep2.Collection.Tests/
   └─ LogRep2.Analysis.Tests/
```

`LogRep2.App`、`LogRep2.Infrastructure`、`LogRep2.Ipc`は、対応する実装フェーズで追加する。空プロジェクトだけを先行作成して依存関係を固定することは避けた。

### 2.2 共通ビルド設定

`Directory.Build.props`へ次を設定した。

- Nullable有効
- ImplicitUsings有効
- 警告をエラーとして扱う
- Release時のデバッグシンボル出力抑制

SDKは既存`logRep_r`と同じ`.NET SDK 8.0.422`を`global.json`で指定した。

### 2.3 Collection Core移植

移植元:

```text
logRep_r/src/FfxiTempLogCollector.Core/
```

移植先:

```text
logRep2/src/LogRep2.Collection/
```

結果:

- C# 59ファイルを移植
- ターゲットフレームワーク`net8.0`
- AssemblyName`LogRep2.Collection`
- 移植元と移植先の全C#ファイルについてSHA-256差異0件

フェーズ1では既存挙動を変えないことを優先し、型、処理、コメント、既存名前空間`FfxiTempLogCollector.Core`を変更していない。

### 2.4 Analysis Core移植

移植元:

```text
logAnalyzer/src/FFXI_LogAnalyzer.Core/
```

移植先:

```text
logRep2/src/LogRep2.Analysis/
```

結果:

- C# 65ファイルを移植
- ターゲットフレームワーク`net8.0`
- AssemblyName`LogRep2.Analysis`
- 移植元と移植先の全C#ファイルについてSHA-256差異0件

フェーズ1では既存挙動を変えないことを優先し、型、処理、コメント、既存名前空間`FFXI_LogAnalyzer.Core`を変更していない。

### 2.5 名前空間の扱い

プロジェクトのRootNamespaceは`LogRep2.Collection`および`LogRep2.Analysis`としたが、移植済み型の宣言名前空間は旧名称を維持した。

理由:

- フェーズ1の目的は無改変移植と回帰基準確立である。
- 名前空間変更とロジック移植を同時に行うと差分が大きくなる。
- 既存テストをそのまま利用し、挙動差を検出しやすくする。

後続フェーズで新しく作成する型は`LogRep2.*`名前空間を使用する。既存型の名前空間一括変更は、App統合時に必要性と影響範囲を確認して別作業として行う。

### 2.6 テスト移植

現在のHEADから削除されているテストを、Git履歴の削除直前`a2743f2^`から取得した。

#### Collectionテスト

移植先:

```text
logRep2/tests/LogRep2.Collection.Tests/
```

- Core対象のC# 28ファイルを移植
- `sample_logfiles/TEMP/Xitra`の4ファイルをテスト出力へリンク
- 旧App/IPCへ依存する次の5ファイルはフェーズ1対象外
  - `CliCommandControllerTests.cs`
  - `CliCommandParserTests.cs`
  - `ConfigEditServiceTests.cs`
  - `IpcTests.cs`
  - `WindowCloseBehaviorControllerTests.cs`

除外したテストは削除扱いではない。`LogRep2.App`と`LogRep2.Ipc`の構築時に、LogRep2の確定仕様へ合わせて移植する。

#### Analysisテスト

移植先:

```text
logRep2/tests/LogRep2.Analysis.Tests/
```

- C# 23ファイルを移植
- `sample_session`フィクスチャ3ファイルを移植
- `SampleSessionIntegrationTests`を含む分析パイプライン全体をテスト対象化

### 2.7 `.gitignore`修正

既存の一般規則`tests/`が`logRep2/tests`も除外していたため、LogRep2の回帰テストだけをソース管理対象へ戻す例外を追加した。

```gitignore
!logRep2/tests/
!logRep2/tests/*/
!logRep2/tests/*/*.cs
!logRep2/tests/*/*.csproj
!logRep2/tests/*/fixtures/
!logRep2/tests/*/fixtures/**
```

例外はテストソース、プロジェクト、フィクスチャだけに限定した。既存アプリおよびLogRep2の`bin`、`obj`、削除済みテストフォルダーに対する無視方針は変更していない。

## 3. 検証結果

### 3.1 復元

実行:

```powershell
dotnet restore LogRep2.sln
```

結果: 成功。

管理された実行環境ではユーザーNuGet.Configの読取が制限されたため、承認済みのサンドボックス外実行で復元した。プロジェクト自体がユーザーホームへ設定ファイルを作成する仕様ではない。

### 3.2 Debugビルド

```powershell
dotnet build LogRep2.sln --no-restore -c Debug
```

結果:

- 成功
- 警告0
- エラー0

### 3.3 Debugテスト

```powershell
dotnet test LogRep2.sln --no-build --no-restore -c Debug
```

| テストプロジェクト | 合格 | 失敗 | スキップ |
| --- | ---: | ---: | ---: |
| `LogRep2.Collection.Tests` | 121 | 0 | 0 |
| `LogRep2.Analysis.Tests` | 135 | 0 | 0 |
| 合計 | 256 | 0 | 0 |

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
| `LogRep2.Collection.Tests` | 121 | 0 | 0 |
| `LogRep2.Analysis.Tests` | 135 | 0 | 0 |
| 合計 | 256 | 0 | 0 |

### 3.6 書式検証

```powershell
dotnet format LogRep2.sln --no-restore --verify-no-changes
```

結果: 成功。変更要求なし。

### 3.7 元Coreとの差分

全C#ファイルをファイル名とSHA-256で比較した。

| 対象 | 元ファイル数 | 移植ファイル数 | 差異 |
| --- | ---: | ---: | ---: |
| Collection | 59 | 59 | 0 |
| Analysis | 65 | 65 | 0 |

### 3.8 基準分析結果

`SampleSessionIntegrationTests`が成功し、フェーズ0で固定した次の基準を維持している。

- canonical 14件
- 分析対象12件
- 分析時間30秒、信頼度exact
- action group 8件
- Xitra総ダメージ1117
- Boro総ダメージ200

詳細期待値は[baseline/expected_analysis.json](baseline/expected_analysis.json)を参照する。

## 4. 変更理由

- 新アプリの実装を既存2アプリから分離するため。
- 既存Coreを無改変で移植し、統合前後の挙動差を検知できる状態にするため。
- 削除済みだったテストを、新アプリ側でソースから再実行可能にするため。
- 後続のUI統合、設定統合、リアルタイム分析の安全な土台を作るため。

## 5. 影響範囲

### 5.1 新規

- `logRep2`以下のソリューション、Core、テスト、文書

### 5.2 既存ファイル変更

- ルート`.gitignore`へ`logRep2/tests`追跡例外を追加

### 5.3 変更していないもの

- `logRep_r`のソースと動作
- `logAnalyzer`のソースと動作
- `AssistantTool`
- 既存JSONスキーマ
- 既存分析ルール
- ユーザーホーム以下のアプリ設定

## 6. 既知の制限

- WPFアプリはまだ存在しないため、LogRep2.exeは生成されない。
- 統合設定、旧設定移行、リアルタイム分析、オーバーレイは未実装。
- App/IPC依存の旧テスト5ファイルは未移植。
- 移植Coreの宣言名前空間は旧名称のままである。
- `LogRep2.Collection`と`LogRep2.Analysis`の間に直接参照はなく、canonicalデータをメモリ共有する契約は未実装。

## 7. 次に確認すべき点

改修指示書では次工程はフェーズ2のUI統合である。着手時は次を実施する。

1. `LogRep2.App`と`LogRep2.Ipc`を追加する。
2. 既存Appの機能と依存関係を画面・サービス単位に移植する。
3. 収集操作と過去ログ分析を単一WPFアプリから利用可能にする。
4. App/IPC依存の旧テストを新構成へ移植する。
5. 旧名前空間を維持するか段階的に変更するか、差分規模を確認して決定する。

設定一本化はフェーズ3で行うため、フェーズ2では既存設定の保存形式を不用意に変更しない。

## 8. フェーズ1完了判定

| 指示事項 | 結果 |
| --- | --- |
| `LogRep2.sln`作成 | 完了 |
| Collection Core移植 | 完了、59ファイル、差異0 |
| Analysis Core移植 | 完了、65ファイル、差異0 |
| 既存サンプルで分析結果比較 | 完了、統合テスト成功 |
| Core回帰テスト移植 | 完了、合計256件成功 |
| Debug/Releaseビルド | 完了、警告0、エラー0 |
| 作業結果の文書化 | 完了、本書 |

フェーズ1は完了と判定する。
