# UIガイド

目的は、機能追加後も既存利用者が同じ操作感で使えることです。画面全体の再設計は機能修正と分けて提案してください。

## 基準にする実装

| 対象 | 参照先 |
| --- | --- |
| 色・文字・ボタン・入力・カード | [Theme/Styles.xaml](../src/LogRep2.App/Theme/Styles.xaml)、[App.xaml](../src/LogRep2.App/App.xaml) |
| メイン画面の状態と操作 | [MainWindow.xaml](../src/LogRep2.App/MainWindow.xaml) |
| 設定の分類・入力・説明 | [SettingsWindow.xaml](../src/LogRep2.App/SettingsWindow.xaml) |
| 過去ログの操作フロー・結果 | [Analyzer/MainWindow.xaml](../src/LogRep2.App/Analyzer/MainWindow.xaml)、[AnalysisResultView.xaml](../src/LogRep2.App/Analyzer/AnalysisResultView.xaml) |
| オーバーレイ | [OverlayWindow.xaml](../src/LogRep2.App/OverlayWindow.xaml) |
| ブラウザ閲覧・HTML出力 | [AssistantTool/style.css](../AssistantTool/style.css)、[PartyTimelineHtmlExporter.cs](../src/LogRep2.App/Analyzer/PartyTimelineHtmlExporter.cs) |

## 見た目

- WPFでは共通ResourceDictionaryのStyle・Brush・FontFamilyを優先する。既存の色・文字サイズを独自の値で複製しない。画面固有のStyleは既存の共通Styleを基にする。
- 通常画面は既存の明るい背景、カード、紺のヘッダー、青の主要操作を基準にする。成功・警告・危険は既存の色トークンと文言を併用し、色だけで伝えない。
- フォントは既存の `UiFontFamily`、等幅表示は `MonoFontFamily` を使う。文字サイズ、行高、余白、角丸、ボタン寸法は同種の既存画面に合わせる。一律の新寸法を導入しない。
- 共通スタイルを変える場合は全利用画面への影響を確認する。新トークンは既存で表せない意味がある場合に追加する。
- オーバーレイの透明度・文字サイズ・最前面・移動・サイズ変更の操作を保つ。ブラウザ画面はそれぞれのCSS・HTMLを基準にし、WPFの外観を無理に移植しない。

## 文言と操作

- 日本語と既存の用語を使う。「収集」「分析」「セッション」「分析対象」「除外」「エイリアス」の意味を混同しない。ボタンには実行する動作を記す。
- 関連する入力・説明・操作を近くに配置し、主要操作と補助操作を既存のスタイルで区別する。無関係な新しいタブやダイアログを増やさない。
- 処理中・空の結果・入力エラー・保存失敗の表示を用意する。操作できない状態は理由が分かるようにし、長い処理には既存の進捗・キャンセル操作を使う。
- 保存に失敗した入力は修正・再試行できるよう保持する。破壊的操作には対象と影響を示す確認を設ける。通常の可逆操作に確認を増やさない。
- Tab移動、フォーカス表示、既存のEnter・Esc・F2・Ctrl/Shift操作を保つ。新規画面のキーボード操作は同種画面に合わせる。
- 数値の表示・CSVは既存のフォーマッターを再利用する。DPS・平均ダメージは桁区切り付き小数2桁、最大・最小は整数という既存仕様に従う。

## PRで確認すること

変更した画面をWindowsで操作し、変更前後を同じ条件で撮影してPRに添付してください。

- 通常状態に加え、変更に関係する処理中・空・エラー・無効状態。
- 最小サイズ付近と通常サイズ、Windows表示倍率100%・150%での欠け・重なり・スクロール。
- キーボード操作、長い名前・パス・日本語、画面を閉じて開き直した場合。
- 共通スタイル変更なら他の利用画面、オーバーレイ変更なら位置復元と文字サイズ設定。

実機条件を用意できない場合は未確認条件をPRへ明記し、メンテナーの確認を受けてください。スクリーンショットの個人名・パスは匿名化してください。
