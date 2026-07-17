using FfxiTempLogCollector.Core;

namespace FfxiTempLogCollector.Tests;

public sealed class PollingOptionsTests
{
    [Fact]
    public void デフォルト間隔は1000ミリ秒になる()
    {
        var options = new PollingOptions();

        Assert.Equal(1000, options.IntervalMs);
    }

    [Theory]
    [InlineData(249)]
    [InlineData(5001)]
    public void 設定範囲外の間隔を拒否する(int intervalMs)
    {
        var options = new PollingOptions();

        var exception = Assert.Throws<ArgumentOutOfRangeException>(
            () => options.IntervalMs = intervalMs);

        Assert.Contains(
            "ポーリング間隔は250から5000ミリ秒",
            exception.Message,
            StringComparison.Ordinal);
    }
}
