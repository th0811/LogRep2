# LogRep2 フェーズ0 基準データ

## 目的

LogRep2への移植前後で、既存の分析結果とセッション出力互換性を比較するための基準値を固定する。

## 基準A: 分析パイプライン

既存Git履歴の削除直前コミット `a2743f2^` に含まれる次のフィクスチャとテストを基準とする。

```text
logAnalyzer/tests/FFXI_LogAnalyzer.Tests/fixtures/sample_session/
logAnalyzer/tests/FFXI_LogAnalyzer.Tests/SampleSessionIntegrationTests.cs
```

フィクスチャの識別値は次のとおり。

| ファイル | バイト数 | レコード数 | SHA-256 |
| --- | ---: | ---: | --- |
| `session.json` | 442 | 1 | `00ac8a1b3b8e8788f6be81631af4a67d4358486927aff14c6f792e8fef40a638` |
| `stats.json` | 260 | 1 | `4a9406025fa06f039b3bfdf1668dcaf7675ef29f8b68aaced9bf46bc320949b8` |
| `canonical_records.jsonl` | 6,531 | 14 | `068bd18cc3ce811df2e61fdfd579f57a7c877d03f34e45b38f5ce1fa7368e822` |

期待する主要分析結果は [expected_analysis.json](expected_analysis.json) に記録する。

## 基準B: 実TEMPログ収集

既存の実ログ処理結果から、次のセッションを大容量回帰基準候補とする。

```text
logRep_r/.tmp/real-sample-parse/20260624-160838/
```

| ファイル | バイト数 | レコード数 | SHA-256 |
| --- | ---: | ---: | --- |
| `session.json` | 1,209 | 1 | `ffbe4444cf38f902059ea3334b1270f1776212aa76bff4d96ff5e02dfdfe7c9f` |
| `stats.json` | 282 | 1 | `1e6466461d6e971f7e59a88561bfe141c26df466aa2b0ed5b45339bae605df21` |
| `raw_records.jsonl` | 2,076,858 | 1,960 | `6fa4636cae8a27bac3f382b6931de3cc1cd8876a2a50ecb9a55e9b3a60656439` |
| `canonical_records.jsonl` | 778,377 | 1,002 | `537461894262956d600c9bbd1458fa9ace6393e3212f116b151df72d44e79bf3` |

このセッションは作業用 `.tmp` 配下にあるため、恒久フィクスチャではない。フェーズ1のテストプロジェクト構築時に、元の `sample_logfiles` から決定論的に再生成するテストへ置き換えること。セッションID、収集日時、ファイル更新日時を含むJSON全体のハッシュ一致を必須にせず、レコード内容、件数、状態、スキーマ、分析結果を比較すること。

## 利用規則

- 基準値を変更する場合は、仕様変更理由と旧値との差分を記録する。
- LogRep2の都合だけで期待値を更新しない。
- 時刻、セッションID、ファイルパスなど実行環境依存値は正規化して比較する。
- 互換性確認では既存`AssistantTool`による読込も実施する。

