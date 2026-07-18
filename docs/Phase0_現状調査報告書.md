# LogRep2 フェーズ0 現状調査報告書

## 1. 調査概要

### 1.1 目的

[LogRep2 改修指示書](LogRep2_改修指示書.md) の「フェーズ0: 現状基準の確立」に従い、LogRep2の実装開始前に既存2アプリの機能、設定、データモデル、スキーマ、テスト資産、基準データ、文字コードを確認した。

### 1.2 実施日

2026年7月17日

### 1.3 調査対象

- `logRep_r`
- `logAnalyzer`
- `AssistantTool`
- `docs/specification.md`
- `docs/analyzer_specification.md`
- `docs/実装指示書.md`
- `docs/analyzer_実装指示書.md`
- Git履歴上の削除済みテスト資産
- `sample_logfiles`および既存の生成済みセッション

### 1.4 結論

フェーズ1へ進むための既存機能、設定、モデル差異、回帰基準を特定できた。テストソースは現在のHEADから削除されているが、Git履歴の削除直前コミットから復元可能である。ビルド済みテストDLLでは収集109件、分析112件がすべて成功した。

既存のソース、XAML、Markdown、JSONなど525ファイルはすべて有効なUTF-8であり、実ファイルの文字化けは検出されなかった。BOMなしUTF-8をWindows PowerShellの既定文字コードで読み取ると表示が崩れるため、以後はUTF-8を明示して扱う必要がある。

## 2. 既存構成

| アプリ | 技術 | 構成 | 主な責務 |
| --- | --- | --- | --- |
| `logRep_r` | .NET 8 / WPF / Windows Forms併用 | App、Core、Ipc | TEMP監視、raw/canonical生成、セッション保存、GUI、CLI、トレイ |
| `logAnalyzer` | .NET 8 / WPF / Windows Forms併用 | App、Core | セッション読込、分析範囲、集計、結果表示、PC/NPC登録 |
| `AssistantTool` | HTML/CSS/JavaScript | 静的Web | 保存済みログ再現、補助確認 |

両Coreプロジェクトは`net8.0`、両Appプロジェクトは`net8.0-windows`とWPFを使用しており、技術基盤を変更せず統合可能である。

## 3. 既存機能一覧

### 3.1 logRep_r: 収集Core

| 分類 | 機能 | 主な実装 |
| --- | --- | --- |
| 設定 | JSON設定の読込、保存、相対パス解決、環境変数展開 | `ConfigLoader`、`ConfigStore`、`JsonFileSerializer` |
| 監視対象 | `1_N.log`、`2_N.log`の対象構築 | `TempLogWatchTargetBuilder`、`TempLogFileNameParser` |
| 変更検出 | 存在、更新日時、サイズ、SHA-1ハッシュによる変更検出 | `TempLogPoller`、`FileChangeDetector`、`FileSnapshotStore` |
| 読込 | TEMPファイル読込、ヘッダー・レコード解析 | `TempLogFileReader`、`TempLogFileParser` |
| デコード | 制御コード処理、表示文字列生成 | `RecordDecoder`、`TextNormalizer` |
| 補助情報 | 本文時刻、マーカー検出 | `TimestampExtractor`、`MarkerDetector` |
| raw生成 | 出所、ハッシュ、メタ情報、本文を含むrawレコード生成 | `RawRecordFactory` |
| raw重複排除 | raw record IDによる重複排除 | `RawDeduplicator` |
| canonical生成 | rawからcanonical生成、ウィンドウ間統合、順序付け | `CanonicalRecordFactory`、`CanonicalDeduplicator` |
| 保存 | raw追記、canonical全件保存 | `RawRecordJsonlWriter`、`CanonicalRecordJsonlWriter` |
| セッション | active/completed/aborted状態、開始・終了時刻 | `SessionManager` |
| 状態・統計 | ファイル状態、既知ID、件数、警告・エラー数 | `StateStore`、`StatsStore` |
| 実行 | 一回収集、継続ポーリング | `OnceCollectionRunner`、`PollingCollectionRunner` |
| サービス | 非同期開始・停止、状態スナップショット通知 | `CollectorService`、`CollectorEvents` |

### 3.2 logRep_r: GUI・常駐・CLI

- 収集開始、収集停止
- TEMPフォルダーと出力フォルダーの表示・選択・オープン
- 収集状態、セッションID、最終検出時刻、保存件数、警告、直近エラー表示
- 設定画面
- 起動時自動収集
- 収集開始後のトレイ格納
- 最小化ボタンのトレイ動作
- 閉じるボタンの終了確認、収集中のみ格納、常時格納
- タスクトレイメニューと通知
- 単一起動
- 名前付きパイプIPC
- CLI `help`、`start`、`stop`、`status`、`once`
- CLI `config get`、`config set`、`config path`
- CLI `--config`、`--temp-dir`、`--output-dir`

### 3.3 logAnalyzer: 読込・分析Core

| 分類 | 機能 | 主な実装 |
| --- | --- | --- |
| セッション読込 | `session.json`、`stats.json`の検証、active/aborted警告 | `SessionFolderLoader` |
| canonical読込 | UTF-8 JSONL行単位読込、行エラー分離、nullable変換 | `CanonicalRecordReader` |
| 複数セッション | セッション単位の読込と選択 | `AnalyzerInputSession`、App ViewModel |
| マーカー | マーカー抽出、開始・終了候補 | `MarkerExtractor` |
| 分析範囲 | 先頭、末尾、マーカーによる範囲構築・検証 | `AnalysisRangeBuilder`、`AnalysisRangeValidator` |
| 分析時間 | 秒、分精度、推定、日付またぎ、信頼度 | `AnalysisTimeResolver` |
| グループ化 | session IDとevent groupを中心としたaction group構築 | `ActionGroupBuilder` |
| 解析 | 通常攻撃、技、魔法、複数ダメージ、失敗分類 | `ActionGroupParser`、`NormalAttackParser` |
| actor | actor抽出、PC候補・NPC分類、正規化 | `ActorExtractor`、`ActorNameClassifier` |
| action | action名抽出、action type分類 | `ActionNameExtractor`、`ActionType` |
| 命中 | hit/miss/unknown分類 | `HitStatusClassifier` |
| ダメージ | ダメージ値抽出、合計、最大、最小、平均 | `DamageParser`、`DamageStatistics` |
| クリティカル | 通常攻撃クリティカル検出 | `CriticalDetector` |
| 集計 | actor別、action別、DPS、命中率、クリティカル率 | `AnalysisAggregator`、`RateCalculator` |
| レベル上げ | 経験値等のポイント、最大チェーン、時給 | `LevelingPointAggregator` |
| 未解析 | 未解析group、unknown groupの保持 | `UnparsedActionGroup`、`ParsedActionGroup` |
| 拡張性 | 解析ルールの差替え | `AnalysisRuleSet`、`DefaultAnalysisRuleSet`、各interface |

### 3.4 logAnalyzer: GUI

- セッション出力先選択
- セッション一覧更新
- 単一セッション追加、フォルダー内セッション一括追加
- 選択セッション解除、全解除
- completed以外のセッションに対する警告
- セッション情報、警告表示
- 分析開始・終了点選択
- actor表示選択
- PC候補選択、登録済みPC選択
- PC名・NPC名登録、解除、登録名管理
- キャラクター別サマリ
- 技・魔法・通常攻撃別集計
- レベル上げ集計
- 未解析ログ表示
- 分析時間、信頼度、DPS状態表示

## 4. 既存設定項目の全件棚卸し

### 4.1 logRep_r `config.json`

JSONはsnake_caseで保存される。デフォルトの保存先は既存exeと同じフォルダーである。

| JSON項目 | C#型 | 初期値 | 用途 | LogRep2移行先 |
| --- | --- | --- | --- | --- |
| `temp_dir` | string | 空 | FFXI TEMPフォルダー | `collection.temp_directory` |
| `output_dir` | string | `sessions` | セッション出力先 | `collection.output_directory` |
| `encoding` | string | `cp932` | TEMPログ文字コード | `collection.encoding` |
| `polling_interval_ms` | int | `1000` | ポーリング間隔 | `collection.polling_interval_ms` |
| `watch_window1` | bool | `true` | ウィンドウ1監視 | `collection.watch_window1` |
| `watch_window2` | bool | `true` | ウィンドウ2監視 | `collection.watch_window2` |
| `rotation_slots` | int | `20` | ローテーション数 | `collection.rotation_slots` |
| `raw_output` | bool | `true` | raw出力 | `collection.raw_output` |
| `canonical_output` | bool | `true` | canonical出力 | `collection.canonical_output` |
| `dedupe_raw` | bool | `true` | raw重複排除 | `collection.dedupe_raw` |
| `dedupe_canonical` | bool | `true` | canonical重複排除 | `collection.dedupe_canonical` |
| `marker_detection` | bool | `true` | マーカー検出 | `collection.marker_detection` |
| `marker_prefix` | string | `###` | マーカープレフィックス | `collection.marker_prefix` |
| `timezone` | string | `Asia/Tokyo` | セッションタイムゾーン | `collection.timezone` |
| `flush_interval_ms` | int | `1000` | 出力flush設定 | `collection.flush_interval_ms` |
| `hash_algorithm` | string | `sha1` | ハッシュアルゴリズム | `collection.hash_algorithm` |
| `log_level` | string | `info` | ログレベル | `application.log_level` |
| `auto_start_collection_on_launch` | bool | `false` | 起動時自動収集 | `application.auto_start_collection_on_launch` |
| `minimize_to_tray_while_collecting` | bool | `false` | 収集開始後トレイ格納 | `application.minimize_to_tray_while_collecting` |
| `minimize_button_behavior` | string | `tray` | 最小化動作 | `application.minimize_button_behavior` |
| `close_button_behavior` | string | `tray_when_collecting` | 閉じる動作 | `application.close_button_behavior` |
| `show_tray_notifications` | bool | `true` | トレイ通知 | `application.show_tray_notifications` |

注意事項:

- `flush_interval_ms`は設定モデルに存在するが、現行のwriter実装で実効的に利用されているかフェーズ1移植時に再確認する。
- `hash_algorithm`は設定可能だが、変更検出側でSHA-1固定箇所がないか再確認する。
- 上記項目は互換移行対象から漏らさない。

### 4.2 logAnalyzer `analyzer_settings.json`

| JSON項目 | C#型 | 初期値 | 用途 | LogRep2移行先 |
| --- | --- | --- | --- | --- |
| `sessions_root_folder_path` | string? | null | セッションルート | 廃止。`collection.output_directory`へ一本化 |
| `known_pc_names` | string[] | 空 | PC登録名 | `analysis.known_pc_names` |
| `known_npc_names` | string[] | 空 | NPC登録名 | `analysis.known_npc_names` |

`sessions_root_folder_path`は旧設定移行時に、収集側`output_dir`が存在しない場合の補助候補としてのみ扱う。競合時は改修指示書どおり収集側`output_dir`を優先する。

### 4.3 LogRep2で新規追加する設定群

実装フェーズで次を追加する。詳細な型と制約は設定実装タスクで確定する。

- `schema_version`
- `analysis.realtime_refresh_interval_ms`、初期値500
- オーバーレイ表示有無
- 常に最前面
- 透過度
- 左、上、幅、高さ
- 位置固定
- モニター復元情報
- フォントサイズ
- 表示行数
- 表示項目

## 5. 永続化スキーマ棚卸し

### 5.1 共通規約

- JSONプロパティ名はsnake_case。
- enumはsnake_case文字列。
- JSONLとJSONはUTF-8。
- 現行スキーマバージョンはsession、raw、canonicalすべて`1.0`。

### 5.2 `session.json`

| 項目 | 収集側 | 分析側 | 差異 |
| --- | --- | --- | --- |
| `schema_version` | string、初期値`1.0` | string? | 分析側は欠損許容 |
| `raw_schema_version` | string、初期値`1.0` | string? | 同上 |
| `canonical_schema_version` | string、初期値`1.0` | string? | 同上 |
| `collector_version` | string | string? | 同上 |
| `session_id` | string | string? | 同上 |
| `status` | enum | 同一enum値 | 実質一致 |
| `started_at` | DateTimeOffset | DateTimeOffset? | 分析側は欠損許容 |
| `ended_at` | DateTimeOffset? | DateTimeOffset? | 一致 |
| `temp_dir` | string | string? | 分析側は欠損許容 |
| `output_dir` | string | string? | 同上 |
| `encoding` | string | string? | 同上 |
| `timezone` | string | string? | 同上 |
| `watch_files` | List<string> | IReadOnlyList<string> | 読み取り専用の差 |

`SessionStatus`は双方とも`unknown`、`active`、`completed`、`aborted`で一致する。

### 5.3 `canonical_records.jsonl`

JSON項目は双方で一致している。

```text
schema_version
canonical_record_id
session_id
order
first_seen_at
last_seen_at
source_windows
source_files
source_raw_record_ids
event_group
sequence_hint_min
sequence_hint_max
visible_text
message_time_text
message_time_precision
is_marker
marker_keyword
canonical_key
```

重要な型差異:

- 収集側の`Order`は`long`、分析側は`long?`。
- 収集側の`SequenceHintMin/Max`は`string?`、分析側は柔軟なJSON変換付き`long?`。
- 収集側の主要ID、本文、日時は必須型、分析側は旧版・欠損行に耐えるnullable型。
- 収集側リストは変更可能、分析側は`IReadOnlyList`。

移植方針:

- 保存時の厳格なモデルと、外部・旧データ読込時の寛容性を両方維持する。
- `sequence_hint_min/max`は既存JSON上で数値として保存されるケースがあり、文字列・数値双方の読込互換性を保持する。
- canonicalモデルの共有は可能だが、nullable化だけで収集不変条件を失わせない。単一モデル化する場合は生成時検証またはファクトリーで保証する。

### 5.4 `raw_records.jsonl`

主なフィールドは次のとおり。

```text
schema_version, raw_record_id, session_id, first_seen_at,
source_file, window_id, rotation_index, file_mtime, file_size,
file_hash, record_index, record_offset, raw_record_hash,
meta_fields, event_group, sequence_hint, message_token_count,
display, raw_message_hex, visible_text, message_time_text,
message_time_precision, message_unix_time_hint, message_time_at,
is_marker, marker_keyword, parse_status, parse_error
```

`display`には現在`color_code`がある。rawは再解析・調査用の一次情報であり、LogRep2でも削減しない。

### 5.5 `stats.json`

収集側`CollectorStats`と分析側`StatsInfo`の項目は一致する。

```text
raw_records_written
canonical_records_written
duplicate_raw_records_skipped
duplicate_canonical_records_skipped
parse_errors
decode_errors
gap_warnings
last_seen_at
```

差異は収集側が更新可能、分析側が読込専用である点のみ。

### 5.6 `state.json`

収集継続状態として次を保存する。

- `session_id`
- `updated_at`
- ファイルごとの存在、更新日時、サイズ、ハッシュ
- 処理済みraw record ID集合
- 処理済みcanonical key集合
- 最終order

分析側は`state.json`へ依存していない。LogRep2でも分析契約へ持ち込まず、Collection内部の復旧・診断データとして維持する。

## 6. 重複モデルと統合判断

| モデル | 重複状況 | 判断 |
| --- | --- | --- |
| `CanonicalRecord` | 両Coreに存在 | JSON契約は共通。保存側の厳格性と読込側の寛容性を保つ設計が必要 |
| `SessionInfo` | 両Coreに存在 | JSON契約は共通。外部読込だけnullable耐性が必要 |
| `SessionStatus` | 両Coreに存在 | 値が一致するため共通化候補 |
| `CollectorStats` / `StatsInfo` | 名称違いで項目一致 | 共通契約化候補。更新用と読込用の責務差を考慮 |
| schema version | 収集側定数、分析側表示用record | バージョン定数をInfrastructureまたは共通契約へ集約候補 |
| `RelayCommand` | 両Appに存在 | LogRep2.Appで一本化候補 |
| セッション選択用ViewModel | 分析App固有 | Appへ移植しCoreへ入れない |

現時点では共通契約専用プロジェクトを追加せず、改修指示書の5プロジェクト構成内で依存方向を保てるかフェーズ1で判断する。必要な場合は`LogRep2.Contracts`追加案を提示してから変更する。

## 7. 基準データと期待結果

### 7.1 分析基準

Git履歴上の`SampleSessionIntegrationTests`と14件のcanonicalフィクスチャを基準とした。詳細は[baseline/README.md](baseline/README.md)と[baseline/expected_analysis.json](baseline/expected_analysis.json)に固定した。

主要値:

- canonical 14件
- マーカー`#start`、`#end`
- 分析対象12件
- 分析時間30秒、信頼度exact
- action group 8件、解析成功7件、未解析1件
- Xitra: 総ダメージ1117、DPS約37.2333
- Boro: 総ダメージ200、DPS約6.6667

### 7.2 収集基準

既存実ログ処理結果では、代表セッションがraw 1,960件、canonical 1,002件である。ファイルサイズとSHA-256を`baseline/README.md`へ記録した。

セッションID、収集時刻、絶対パス、ファイル更新日時が実行ごとに変わるため、LogRep2回帰テストではJSONファイル全体のハッシュ完全一致ではなく、環境依存値を除外した内容比較を採用する。

## 8. テスト資産調査

### 8.1 現在の状態

- 現在のHEADでは両テストフォルダーのソースと`.csproj`が削除済み。
- `bin`と`obj`は作業ツリーに残っているが、ソリューションからの`dotnet test`は`.csproj`不在で失敗する。
- 削除コミットは`a2743f2 Remove test directory from repository`。

### 8.2 復元可能性

削除直前の`a2743f2^`から次を復元可能である。

- 収集テスト: C# 33ファイルと`.csproj`
- 分析テスト: C# 23ファイルと`.csproj`
- 分析用sample sessionフィクスチャ3ファイル

フェーズ1では既存フォルダーを復元するのではなく、LogRep2の各責務へ合わせて必要なテストを`logRep2/tests`へ移植する。移植元は削除直前履歴とする。

### 8.3 現行バイナリのテスト結果

2026年7月17日にビルド済みDLLを`dotnet vstest`で実行した。

| 対象 | 合格 | 失敗 | スキップ |
| --- | ---: | ---: | ---: |
| `FfxiTempLogCollector.Tests.dll` | 109 | 0 | 0 |
| `FFXI_LogAnalyzer.Tests.dll` | 112 | 0 | 0 |

これは現在残っているバイナリと同時点のCoreに対する基準であり、現在のソースから再ビルドされた証明ではない。フェーズ1でテストソースを移植し、ソースビルドから再実行する。

## 9. 文字コード調査

### 9.1 調査結果

対象拡張子 `.md`、`.cs`、`.xaml`、`.json`、`.js`、`.css`、`.html`、`.csproj`、`.sln`、`.props`、`.pubxml`の525ファイルを、例外を報告する厳格なUTF-8デコーダで検査した。

| 結果 | 件数 |
| --- | ---: |
| UTF-8 BOMあり | 49 |
| UTF-8 BOMなし | 476 |
| 不正UTF-8 | 0 |
| Unicode置換文字を含むファイル | 0 |
| 典型的な日本語文字化け列を含むファイル | 0 |

### 9.2 原因判断

以前PowerShellで表示された文字化けは、実ファイル破損ではない。BOMなしUTF-8を、Windows PowerShellが既定のANSI系文字コードとして読んだことで発生した表示上の問題である。

### 9.3 開発ルール

- 新規テキストファイルはUTF-8で保存する。
- Windows PowerShellで`Get-Content`を使う場合は`-Encoding UTF8`を明示する。
- JSONLの読書きは既存どおりUTF-8とする。
- FFXI TEMP入力の`cp932`と、アプリ内部・保存ファイルのUTF-8を混同しない。
- フェーズ1で`.editorconfig`を用意し、文字コード方針を固定することを推奨する。

## 10. フェーズ1へ引き継ぐ注意事項

### 10.1 必須

1. テストを削除直前履歴からLogRep2へ段階移植する。
2. canonicalの保存側厳格型と読込側nullable耐性を維持する。
3. `sequence_hint_min/max`の文字列・数値互換をテストする。
4. 既存JSONのsnake_case、enum文字列、schema version 1.0を維持する。
5. `AssistantTool`が依存するcanonical出力を変更しない。
6. UTF-8を明示して文字化けの再発を防ぐ。

### 10.2 実装前にコードで再確認する項目

- `flush_interval_ms`の現行実効性
- `hash_algorithm`の現行実効性
- canonical全件書換え時の原子的置換と分析読込安全性
- `CanonicalDeduplicator.Records`を読み取り専用スナップショットとして公開する境界
- 収集開始・停止と分析開始・終了を分離した状態遷移

## 11. フェーズ0完了判定

| 指示事項 | 結果 |
| --- | --- |
| 既存機能一覧 | 完了。本書3章 |
| 既存設定項目の全件棚卸し | 完了。本書4章 |
| 重複モデルとスキーマ差異 | 完了。本書5～6章 |
| 基準出力ファイルと分析結果 | 完了。`baseline`と本書7章 |
| 既存テスト資産の復元可否 | 完了。復元可能。本書8章 |
| 既存文言と文字コード | 完了。実ファイル破損なし。本書9章 |

フェーズ0は完了と判定する。フェーズ1では、本書と改修指示書を参照し、ソリューション構築と既存Core移植へ進む。

