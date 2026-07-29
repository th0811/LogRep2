using FfxiTempLogCollector.App;
using FFXI_LogAnalyzer.App;
using FFXI_LogAnalyzer.Core;

namespace FfxiTempLogCollector.Tests;

public sealed class DiagnosticLogServiceTests
{
    [Fact]
    public void 診断ログに日時カテゴリと本文を出力する()
    {
        var timestamp = new DateTimeOffset(
            2026,
            7,
            21,
            20,
            30,
            15,
            123,
            TimeSpan.FromHours(9));

        var entry = DiagnosticLogService.BuildEntry(
            timestamp,
            "操作エラー",
            "テスト本文");

        Assert.Contains(
            "[2026-07-21 20:30:15.123 +09:00] [操作エラー]",
            entry);
        Assert.Contains("テスト本文", entry);
    }

    [Fact]
    public void 表示用バージョンにバージョン番号を含む()
    {
        Assert.StartsWith("バージョン ", DiagnosticLogService.VersionText);
        Assert.DoesNotContain('+', DiagnosticLogService.VersionText);
    }

    [Theory]
    [InlineData(TimeConfidence.Exact, "秒単位")]
    [InlineData(TimeConfidence.Minute, "分単位")]
    [InlineData(TimeConfidence.Estimated, "推定")]
    [InlineData(TimeConfidence.Unknown, "不明")]
    public void 時刻精度を日本語表示に変換する(
        TimeConfidence confidence,
        string expected)
    {
        Assert.Equal(expected, AnalysisDisplayText.ToText(confidence));
    }

    [Fact]
    public void 分析画面の日時表示からタイムゾーンを除外する()
    {
        var timestamp = new DateTimeOffset(
            2026,
            7,
            29,
            14,
            30,
            0,
            TimeSpan.FromHours(9));

        Assert.Equal(
            "2026-07-29 14:30:00",
            AnalysisDisplayText.ToDateTimeText(timestamp));
    }

    [Fact]
    public void 分析画面の日時がない場合はハイフンを表示する()
    {
        Assert.Equal("-", AnalysisDisplayText.ToDateTimeText(null));
    }

    [Theory]
    [InlineData(ActionType.NormalAttack, "通常攻撃")]
    [InlineData(ActionType.NormalAttackCritical, "通常攻撃（クリティカル）")]
    [InlineData(ActionType.Skill, "技")]
    [InlineData(ActionType.Magic, "魔法")]
    [InlineData(ActionType.Unknown, "未分類")]
    public void アクション種別を日本語表示に変換する(
        ActionType actionType,
        string expected)
    {
        Assert.Equal(expected, AnalysisDisplayText.ToText(actionType));
    }

    [Theory]
    [InlineData(1234.567, "1,234.57")]
    [InlineData(12.5, "12.50")]
    public void 分析結果の小数を桁区切り付き2桁で表示する(
        double value,
        string expected)
    {
        Assert.Equal(expected, AnalysisNumberFormatter.FormatDecimal(value));
    }

    [Fact]
    public void 値がない小数はハイフンで表示する()
    {
        Assert.Equal("-", AnalysisNumberFormatter.FormatDecimal(null));
    }

    [Theory]
    [InlineData(1234, "1,234")]
    [InlineData(0, "0")]
    public void 最大最小ダメージを桁区切り付き整数で表示する(
        int value,
        string expected)
    {
        Assert.Equal(expected, AnalysisNumberFormatter.FormatInteger(value));
    }

    [Fact]
    public void 値がない整数はハイフンで表示する()
    {
        Assert.Equal("-", AnalysisNumberFormatter.FormatInteger(null));
    }
}
