using FfxiTempLogCollector.App;

namespace FfxiTempLogCollector.Tests;

public sealed class WindowCloseBehaviorControllerTests
{
    [Theory]
    [InlineData("tray", "always_tray")]
    [InlineData("always_tray", "always_tray")]
    [InlineData("exit", "confirm_exit")]
    [InlineData("confirm_exit", "confirm_exit")]
    [InlineData("tray_when_collecting", "tray_when_collecting")]
    [InlineData("unknown", "tray_when_collecting")]
    public void 閉じる動作の設定値を正規化できる(
        string value,
        string expected)
    {
        var actual =
            WindowCloseBehaviorController.NormalizeCloseBehavior(
                value);

        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData("tray", "tray")]
    [InlineData("normal", "normal")]
    [InlineData("minimize", "normal")]
    [InlineData("unknown", "normal")]
    public void 最小化動作の設定値を正規化できる(
        string value,
        string expected)
    {
        var actual =
            WindowCloseBehaviorController.NormalizeMinimizeBehavior(
                value);

        Assert.Equal(expected, actual);
    }
}
