using FfxiTempLogCollector.App;

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

}
