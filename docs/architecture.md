# 基本設計

現在の実装を基準とした責務分担と、変更時に守る原則です。利用者向けの詳細仕様は[README](../README.md)、作業手順は[CONTRIBUTING](../CONTRIBUTING.md)を参照してください。

## 構成と依存

矢印はプロジェクト参照先を表します。

```text
LogRep2.App → Collection / Analysis / Infrastructure / Ipc
LogRep2.Infrastructure → Collection
LogRep2.Collection → Contracts
LogRep2.Analysis → Contracts
LogRep2.Contracts / LogRep2.Ipc → 他の本番プロジェクト参照なし
```

| 場所 | 責務・追加する処理 |
| --- | --- |
| `src/LogRep2.Contracts` | 収集・分析が共有する最小限のレコード契約 |
| `src/LogRep2.Collection` | TEMP読取、CP932デコード、正規化、重複排除、セッション・ログ出力、収集制御 |
| `src/LogRep2.Analysis` | canonical読取、行動解釈、分析区間・集計、分析用の除外・注釈保存 |
| `src/LogRep2.Infrastructure` | 統合設定の読取・保存、旧設定の移行 |
| `src/LogRep2.Ipc` | 単一起動と名前付きパイプによるプロセス間の命令・応答 |
| `src/LogRep2.App` | 起動・CLI、WPF画面、ViewModel、サービスの組み合わせ、オーバーレイ、CSV・HTML出力 |
| `AssistantTool` | JSONLをブラウザで閲覧するHTML・JavaScript・CSS |

収集と分析は直接参照し合わず、共有契約を介します。下位プロジェクトへWPFや画面操作を持ち込まないでください。既存の `App/Analyzer` は分析画面と表示・出力処理を担当し、ログ解釈や集計規則は `Analysis` に置きます。既存の名前空間やcode-behindを一括変更して設計を揃える必要はありません。

## データの流れ

TEMP入力 → Collectionでraw・canonical生成 → セッション保存、という流れです。過去ログ分析はAnalysisで保存済みcanonicalを読み、Appが範囲・除外・表示・出力を調整します。リアルタイム分析は収集snapshotを分析し、Appが最新の結果を画面とオーバーレイへ反映します。

リアルタイムは現在、対象snapshotの全件再集計方式です。更新の集約・古い結果の破棄・重複再集計の回避を維持してください。差分集計への変更は別途方針を合わせます。

## 保存と互換性

- 入力はFFXI TEMPのCP932。ソース・JSON・JSONLはUTF-8、CSVは既存のUTF-8 BOM付き出力を維持する。時刻・ハッシュ・重複判定は既存のAsia/Tokyo・SHA-1・canonical重複排除の規則に従う。
- セッションの `session.json`、`raw_records.jsonl`、`canonical_records.jsonl`、`state.json`、`stats.json` のキー・意味・順序識別子を無断で変更しない。
- 手動除外は `analysis_exclusions.json`、エイリアスは `session_annotations.json` に保存する。これらの編集で元ログを変更しない。過去ログの除外をリアルタイム分析へ混入させない。
- 統合設定 `LogRep2.settings.json` はポータブル方式。保存先をAppData・レジストリへ移さない。旧設定の移行、破損時のバックアップ、既存設定を上書きしない保護を維持する。
- 保存では既存の一時ファイルから置換する方式を参考にし、失敗を成功として表示しない。読込失敗した設定を初期値で黙って上書きしない。除外・注釈の読込失敗時の操作制限を維持する。
- 現行の保存データを壊さない。旧ログ形式全般の互換は保証対象に追加しない。現に存在する読込対応の削除やschema変更は、対象・移行方法・検証を事前に説明する。旧設定の移行保証と旧ログ互換を混同しない。

## 実行・エラー処理

- 長い読込・分析でUIスレッドを止めず、既存の非同期処理とキャンセル経路を再利用する。UIにバインドする状態はUIスレッドで更新する。
- 収集開始・停止、分析開始・停止・リセット、終了処理の状態制約を維持する。収集中のファイルやセッションを破壊する操作には既存の保護を適用する。
- イベント購読、タイマー、CancellationTokenSource、IPCやファイルのリソースは終了・画面破棄時に解放する。
- 操作失敗は利用者へ日本語で理由と対処を伝え、既存の診断ログ経路を使う。例外を握りつぶしたりログに秘密情報を出したりしない。
- 新しい外部通信・依存ライブラリは目的と必要性を事前に説明する。HTML出力へログ文字列を埋め込む際は既存のエスケープを維持し、ログをスクリプトとして実行させない。

責務、保存仕様、集計の意味を変更するPRでは、この文書と関連するREADME・テストも同時に更新してください。
