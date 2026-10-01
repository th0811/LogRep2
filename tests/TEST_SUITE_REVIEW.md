# テスト整理記録（2026-10-01）

本番コード、プロジェクト参照、xUnit/.NET 8、ディレクトリ構成は変更していない。
判断基準は「削除すると現実的な重大な退行を見逃すか」。カバレッジ率は維持目標にしていない。
旧ログ形式の互換保証は不要、オーバーレイの表示順はDPS降順という指定を反映した。
旧設定の移行と破損保護は旧ログ互換とは別なので維持した。

## 集計

| 項目 | 整理前 | 整理後 |
| --- | ---: | ---: |
| テストファイル（C#・JavaScript） | 82 | 74 |
| テスト定義（Fact・Theory・node test） | 378 | 295 |
| 実行ケース（Theoryの各行を含む） | 508 | 422 |
| テスト・helperソースファイル | 87 | 79 |
| テスト・helperの物理行数 | 9,077 | 7,821 |

実行ケースは .NET が 506 → 420（分析 190 → 139、収集・アプリ 316 → 281）、
JavaScriptは2 → 2。コードは1,256行（約13.8%）、ケースは86件（約16.9%）削減。
行数は tests 配下の .cs/.cjs を対象に空行・コメント・共通helperを含む。
bin/obj、TestResults、fixtureデータ、プロジェクト定義、この記録は集計対象外。
共通helper5ファイルとサンプルfixtureは残存テストが使うため維持した。

## 分類と対応

1 = 必須なので残す、2 = 他のテストへ統合する、3 = 価値が低いため削除する、
4 = 判断が難しいため今回は残す。複数分類のファイルでは、下記の対象以外は維持した。
表の件数は実行ケース数ではなくテスト定義数。

| 整理前のファイル | 定義数の前後 | 分類・保証する振る舞い・判断理由 |
| --- | ---: | --- |
| 分析 / ActionGroupBuilderTests.cs | 6 → 3 | 2: 比較器単体を吸収し、4行のTheoryで順序・元ログ・order範囲を確認。セッション境界は維持 |
| 分析 / ActionGroupParserTests.cs | 22 → 17 | 2/3: ミスの完全重複を削除。支援魔法を6行に統合し、失敗文型を上位の解析器で検証 |
| 分析 / ActorNameClassifierTests.cs | 6 → 6 | 1: PC候補の文字種・長さとNPC登録の優先順位 |
| 分析 / AnalysisAggregatorTests.cs | 13 → 3 | 2: 基本集計10定義をサンプル統合テストへ集約。時刻不明・吸収・除外と空統計は維持 |
| 分析 / AnalysisRangeBuilderTests.cs | 4 → 4 | 1: マーカー行の除去、区間境界、不正な範囲 |
| 分析 / AnalysisRangeValidatorTests.cs | 3 → 3 | 1: 逆転した範囲の拒否と端点の許可 |
| 分析 / AnalysisRuleSetTests.cs | 3 → 0 | 3: 非null・インターフェース公開・単なる差し替え保持の確認を削除。5つのstubも削除 |
| 分析 / AnalysisTimeResolverTests.cs | 9 → 9 | 1: 時刻精度、分単位の計算、時刻欠損、日跨ぎ、非正の時間 |
| 分析 / ApplicationInfoTests.cs | 1 → 0 | 3: 固定のアプリ名文字列の比較を削除 |
| 分析 / AreaStaySegmentBuilderTests.cs | 3 → 3 | 1: 再訪・エリア境界とログ形式 |
| 分析 / CanonicalRecordReaderTests.cs | 9 → 3 | 2/3: 正常読込・順序・空行を統合。旧形式のorder欠損/文字列と不正数値文字列を削除。現行のsequence hint読込はJsonlWriterTestsへ統合。破損JSON行とマーカーは維持 |
| 分析 / CriticalDetectorTests.cs | 2 → 0 | 2: ActionGroupParserTestsとサンプル統合テストのクリティカル判定へ集約 |
| 分析 / DamageParserTests.cs | 2 → 2 | 1: ダメージの文型と複数対象の計算。入力変換なので維持 |
| 分析 / DamageStatisticsTests.cs | 2 → 0 | 2: 数値統計はサンプル統合テスト、空統計はAnalysisAggregatorTestsへ移動 |
| 分析 / HitStatusClassifierTests.cs | 6 → 0 | 2: 命中・ミス・除外・支援効果をActionGroupParserTestsへ集約 |
| 分析 / IncomingDamageTests.cs | 2 → 2 | 1: 対象別被ダメージと魔法・連携を回避率の分母に混ぜないこと |
| 分析 / LevelingPointAggregatorTests.cs | 4 → 4 | 1: 経験値等の種類別合計と時給、時刻不明、空入力 |
| 分析 / MarkerExtractorTests.cs | 2 → 2 | 1: マーカー抽出と同名マーカーの位置区別 |
| 分析 / MessageTimeParserTests.cs | 3 → 3 | 1: 時刻の精度と実際にあり得る無効入力 |
| 分析 / NormalAttackParserTests.cs | 4 → 0 | 2: 上位解析器とサンプルの通常攻撃・クリティカル・一行形式・WS分類へ集約 |
| 分析 / PartyTimelineTests.cs | 13 → 13 | 1: 実ログの文型、誤分類、グループ境界、曖昧な宣言と不明/ゼロの区別 |
| 分析 / RateCalculatorTests.cs | 4 → 0 | 2: 比率はサンプル、時刻不明とゼロ分母はAnalysisAggregatorTestsへ集約 |
| 分析 / RealtimeAnalysisEngineTests.cs | 3 → 3 | 4/3: 範囲・通常分析との一致・1万件集計を維持。プロセス全体のメモリ閾値と計測setupは削除 |
| 分析 / SampleSessionIntegrationTests.cs | 1 → 1 | 1/2: 読込→区間→時間→解析→集計の基準。行動別命中率のassertを追加 |
| 分析 / SequenceHintComparerTests.cs | 4 → 0 | 2: 比較器の直接確認をActionGroupBuilderTestsの順序Theoryへ集約 |
| 分析 / SessionFolderLoaderTests.cs | 6 → 3 | 2: 正常系4定義を1件に統合。未完了警告・必須ファイル欠落は維持 |
| 分析 / WeaponSkillCatalogTests.cs | 2 → 2 | 1: WS辞書の完全一致と全登録名の解析、他文型を維持 |
| 収集・アプリ / ActorRegistrationManagerViewModelTests.cs | 1 → 1 | 1: NPC名の正規化と登録保存 |
| 収集・アプリ / ActorVisibilityViewModelTests.cs | 1 → 1 | 1: 登録変更に伴う操作可否 |
| 収集・アプリ / AnalysisExclusionIntegrationTests.cs | 8 → 8 | 1: 除外の保存復元、元ログ保持、分析時間・使用回数とセッションの分離 |
| 収集・アプリ / AnalysisRangeViewModelTests.cs | 2 → 1 | 3/1: 単純な初期値を削除。読込後の区間更新は維持 |
| 収集・アプリ / AssistantToolLauncherTests.cs | 3 → 3 | 1: 日本語JSONLの安全な埋込と不足ファイルのエラー |
| 収集・アプリ / CanonicalDeduplicatorTests.cs | 4 → 4 | 1: 出所統合、order単調増加、数値順と欠損値の保持 |
| 収集・アプリ / CanonicalRecordFactoryTests.cs | 3 → 3 | 1: 永続化フィールドと出所・トークン数に依存しない重複キー |
| 収集・アプリ / CliCommandControllerTests.cs | 8 → 8 | 1: CLI保存・終了コード・収集完了・GUIへのIPC連携 |
| 収集・アプリ / CliCommandParserTests.cs | 4 → 4 | 1: 公開CLIの引数と不明コマンド拒否 |
| 収集・アプリ / CollectorServiceTests.cs | 9 → 9 | 1: 収集状態、手動完了の保護、失敗、再開始と設定即時反映 |
| 収集・アプリ / ConfigEditServiceTests.cs | 5 → 5 | 1: 実際の不正設定、保存、収集中の反映区別 |
| 収集・アプリ / ConfigLoaderTests.cs | 5 → 5 | 1: Windows環境変数・相対パスと設定ファイルの優先順位 |
| 収集・アプリ / ConfigStoreTests.cs | 3 → 1 | 3/1: 既定値の羅列とパスgetterを削除。JSON保存読込は維持。CLI側の保存先確認は残る |
| 収集・アプリ / CsvExportServiceTests.cs | 4 → 4 | 1: CSVのBOM・引用符・ファイル名と削除対象パスの限定 |
| 収集・アプリ / DiagnosticLogServiceTests.cs | 10 → 1 | 3/1: 表示文字列・数値書式9定義を削除。診断ログの日時・カテゴリ・本文は維持 |
| 収集・アプリ / FileChangeDetectorTests.cs | 3 → 3 | 1: 更新時刻・サイズ・ハッシュによる収集対象判定 |
| 収集・アプリ / GitHubReleaseUpdateServiceTests.cs | 5 → 5 | 1: 外部応答の解析、安定版判定と不要な通知の抑止 |
| 収集・アプリ / InlineAliasEditingTests.cs | 2 → 2 | 4: WPF確定・取消・保存失敗と検索の連携を維持。viewmodelだけではUI確定を保証できない |
| 収集・アプリ / IntegratedWindowTests.cs | 7 → 4 | 2/3: オーバーレイ3定義を1回の初期化に統合。スタイル同一性中心のPT選択テストは削除。操作連携は維持 |
| 収集・アプリ / IpcTests.cs | 2 → 2 | 1: NamedPipeの送受信とWindowsの単一起動Mutex |
| 収集・アプリ / JsonlWriterTests.cs | 2 → 2 | 1/2: raw追記とcanonicalの順序・全体置換。現行writerから分析readerへ読み戻し、文字列sequence hintの変換も確認 |
| 収集・アプリ / LogExclusionTests.cs | 12 → 10 | 3/1: 通知名/表示文言中心の確認とIDなし旧ログの専用確認を削除。不正IDの操作保護は別ケースに残し、検索・保存失敗・破損保護は維持 |
| 収集・アプリ / LogRep2SettingsStoreTests.cs | 15 → 13 | 3/1: 単純な既定bool2定義を削除。保存、旧設定互換、移行、破損バックアップと上書き保護は維持 |
| 収集・アプリ / MainViewModelRealtimeAnalysisTests.cs | 2 → 2 | 1: 収集中だけ分析でき、停止/エラーで分析も終了する連携 |
| 収集・アプリ / MarkerDetectorTests.cs | 4 → 4 | 1: echo・時刻付き・日本語・全角空白・カスタム接頭辞 |
| 収集・アプリ / OnceCollectionRunnerTests.cs | 6 → 6 | 1: 収集から保存までの統合、別ウィンドウ重複統合、更新日時順と同時刻の順序 |
| 収集・アプリ / OverlayManagerTests.cs | 1 → 1 | 1: 表示/非表示に連動した更新購読 |
| 収集・アプリ / OverlayPlacementServiceTests.cs | 4 → 4 | 1: モニター切断・DPI・サイズと透明度の安全範囲 |
| 収集・アプリ / OverlayViewModelTests.cs | 4 → 4 | 1: 登録順とDPS順の異なる入力でDPS降順を確認。PT指標、空状態、設定画面導線と操作可能な透明度も維持 |
| 収集・アプリ / PartyMemberManagerViewModelTests.cs | 11 → 6 | 2/3: 追加/削除後の選択を各Theoryへ統合。初期選択・単純な出現数書式・容量getterは削除。保存順/上限/候補復元は維持 |
| 収集・アプリ / PartyTimelineExportTests.cs | 9 → 9 | 1: 外部HTMLの数値、順序、メンバー限定、エスケープと除外適用 |
| 収集・アプリ / PartyTimelinePanel.test.cjs | 2 → 2 | 1: 数値ソート・欠損値と詳細パネルの本文/スクロール復元 |
| 収集・アプリ / PollingCollectionRunnerTests.cs | 5 → 5 | 1: 開始前・停止中ログの除外、回転スロット重複とチェックポイント |
| 収集・アプリ / PollingOptionsTests.cs | 2 → 1 | 3/1: 単純な既定値を削除。実際の間隔設定範囲は維持 |
| 収集・アプリ / RawDeduplicatorTests.cs | 2 → 2 | 1: 重複ログ抑止と再開時の既知ID |
| 収集・アプリ / RawRecordFactoryTests.cs | 2 → 2 | 1: raw出力フィールド、元バイト・ハッシュと時刻ヒント |
| 収集・アプリ / ReadmeDocumentFormatterTests.cs | 2 → 2 | 1: 表示変換と埋込HTMLの無効化 |
| 収集・アプリ / RealtimeAnalysisControllerTests.cs | 7 → 7 | 4: 開始/リセット/停止と非同期更新、時刻更新の再計算を維持。実行回数確認は公開の再利用/抑制仕様に関係する |
| 収集・アプリ / RecordDecoderTests.cs | 5 → 5 | 1: CP932、制御コード、元バイト保持と壊れたバイトへの対応 |
| 収集・アプリ / SessionAliasTests.cs | 5 → 5 | 1: 日本語保存復元、上限・保存失敗・破損保護と検索連携 |
| 収集・アプリ / SessionManagerTests.cs | 3 → 3 | 1: セッションID、session.jsonと完了状態の保存 |
| 収集・アプリ / SessionSelectionPersistenceTests.cs | 2 → 2 | 1: 再起動後の選択復元、新規・破損セッションの扱い |
| 収集・アプリ / StateStoreTests.cs | 1 → 1 | 1: 再開に必要な収集状態の永続化 |
| 収集・アプリ / StatsStoreTests.cs | 1 → 1 | 1: 出力統計の保存読込とJSON形式 |
| 収集・アプリ / TempLogFileNameParserTests.cs | 2 → 2 | 1: FFXIの対象ウィンドウ/スロットと対象外ファイル拒否 |
| 収集・アプリ / TempLogFileParserTests.cs | 8 → 8 | 1: バイナリ形式、NUL、破損データ保持と実ログの途中offset復元 |
| 収集・アプリ / TempLogFileReaderTests.cs | 2 → 2 | 1: 欠落とWindowsで書込中の共有読込 |
| 収集・アプリ / TempLogPollerTests.cs | 6 → 3 | 2/1: 欠落の重複確認と順序2定義を削除。Onceの出力順テストへ集約。初回・変更なし・更新は維持 |
| 収集・アプリ / TempLogWatchTargetBuilderTests.cs | 5 → 5 | 1: ウィンドウ設定・スロット数による監視対象と設定範囲 |
| 収集・アプリ / TextNormalizerTests.cs | 2 → 2 | 1: CP932の制御コード除去と正規化 |
| 収集・アプリ / TimelineMemberSelectionTests.cs | 2 → 2 | 1: 候補更新時の選択維持と候補ゼロからの復帰 |
| 収集・アプリ / TimestampExtractorTests.cs | 6 → 4 | 2: 分/秒/一桁時をTheoryへ統合。不正時刻と後続有効時刻は維持 |
| 収集・アプリ / UnifiedAnalyzerSettingsStoreTests.cs | 5 → 5 | 1: セッション選択保存と収集/分析設定を相互に失わないこと |
| 収集・アプリ / UnifiedCliSettingsTests.cs | 1 → 1 | 1: CLIが統合設定を使い旧設定を作らないこと |
| 収集・アプリ / WindowCloseBehaviorControllerTests.cs | 2 → 2 | 1: 旧設定値との互換と未知値の安全なフォールバック |

## 主な削除・統合の判断

- サンプルセッションの統合テストが、複数キャラクター、DPS、使用回数、複数対象、
  最大/最小/平均、ゼロダメージ、ミス、判定不明、クリティカルを一連の入力で検証する。
  AnalysisAggregatorTestsの対応する10定義は削除し、行動別命中率のassertを統合先へ追加した。
- 通常攻撃とクリティカルの単体解析器、命中分類器、計算器の重複テストを削除。
  独自の失敗文型は上位のActionGroupParserTestsへ移し、空統計とゼロ分母は
  AnalysisAggregatorTestsへ移した。単なる行数削減のために重要な境界を消していない。
- 比較器単体の順序確認はActionGroupBuilderTestsへ統合し、
  有効hint・欠損hint・負値hint・同じhintの入力順を確認する。
  orderで並べ替えるケースは入力順を逆にして、並べ替えを省略した退行も検出する。
- 支援魔法、時刻精度、PTメンバー追加/削除後の選択はパラメータ化した。
  パラメータ化したケースは残るため、定義数と実行ケース数は異なる。
- オーバーレイの外観設定・PT設定ボタン・移動/サイズ変更は同じSTA初期化で確認する。
  スタイルオブジェクトの同一性を中心にした別のPT選択テストは削除したが、
  実画面で登録管理→候補更新→選択解除する連携は残した。
- 既定値・getter・表示書式・通知プロパティ名の細粒度確認を削除した。
  設定保存復元、画面操作、診断ログ本文、保存失敗・破損保護は残した。
- ルールセットの保持・差し替えだけを確認するテストと5つの専用stubを削除した。
  1万件集計のGC操作・割当量計測・出力helperも不要になった。
- 全プロセスのWorking Setが300MB未満というassertは、並列テストや実行環境の影響を受ける。
  集計件数とダメージ合計の確認は残し、メモリ性能は機能テストから切り離した。

## 判断に迷って残した箇所と次の候補

- 現行writerもsequence hintを文字列で保存するため、前回の「旧ログ互換」という分類を修正した。
  JsonlWriterTestsで現行出力を読み戻す連携へ統合し、破損JSON行の保護は残した。
- InlineAliasEditingTestsとWPF操作連携はコード量が多いが、セル確定・検索更新・保存失敗の
  連携をviewmodelだけでは保証できない。UI操作の自動確認を置き換えられるまでは残す。
- RealtimeAnalysisControllerTestsの回数assertは、内部呼出しだけでなく更新抑制・結果再利用という
  READMEの仕様を検証する。非同期の退行が分かりにくいため今回は残す。
- 1万件集計は小さな入力と異なる量の問題を検出するため残した。性能を管理する場合は
  別途同じ環境での計測にする。
- DamageParserTestsやMarkerDetectorTestsの代表入力は、上位解析・収集fixtureに組み込めれば
  さらに統合できる。現在の統合fixtureにない文型を先に確保してから削る。
- ログ除外の操作可否テストは似ているが、同数選択の切替・非表示行復元・不正ID・保存失敗は
  異なる退行を検出するため維持した。実際の不具合履歴を確認できれば優先順位をさらに絞れる。

## 検証結果

- 整理前: dotnet test（Debug）506件、node --test 2件、すべて成功。
- 整理後: dotnet test（Debug/Release）420件、node --test 2件、すべて成功。
- dotnet build LogRep2.sln --no-restore --configuration Release: 警告0、エラー0。
  C#の型検査とビルド時アナライザーもこのビルドで確認した。
- dotnet format LogRep2.sln --verify-no-changes --no-restore: 成功。
- git diff --check: 成功。
- 独立したlint/type check用の設定はないため、ビルドとフォーマット確認で実行可能なチェックを行った。
  今回の整理で発生した失敗、未実行のテスト、スキップはない。

## READMEと表示順の修正

オーバーレイの正しい仕様はDPS降順と確認されたため、READMEとテスト名を修正した。
登録は「未登場、Alice、Bob」、DPSはAlice=10・Bob=20という入力に変更し、
期待順を「Bob、Alice、未登場」とした。登録順表示へ退行した場合も検出できる。
READMEの旧ログ専用の操作説明を削除し、旧ログ形式の互換性は保証しないと明記した。
本番コードの並べ替えや読込処理は変更していない。

