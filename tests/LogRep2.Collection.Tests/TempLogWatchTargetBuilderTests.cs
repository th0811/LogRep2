using FfxiTempLogCollector.Core;

namespace FfxiTempLogCollector.Tests;

public sealed class TempLogWatchTargetBuilderTests
{
    private readonly TempLogWatchTargetBuilder _builder = new();

    [Fact]
    public void 両方のログウィンドウから40ファイルを生成できる()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var config = new CollectorConfig
        {
            WatchWindow1 = true,
            WatchWindow2 = true,
            RotationSlots = 20,
        };

        var actual = _builder.Build(temporaryDirectory.Path, config);

        Assert.Equal(40, actual.Count);
        Assert.Equal(
            temporaryDirectory.GetPath("1_0.log"),
            actual[0]);
        Assert.Equal(
            temporaryDirectory.GetPath("1_19.log"),
            actual[19]);
        Assert.Equal(
            temporaryDirectory.GetPath("2_0.log"),
            actual[20]);
        Assert.Equal(
            temporaryDirectory.GetPath("2_19.log"),
            actual[39]);
        Assert.DoesNotContain(
            temporaryDirectory.GetPath("2_8(1).log"),
            actual);
    }

    [Theory]
    [InlineData(true, false, "1_0.log", "1_19.log")]
    [InlineData(false, true, "2_0.log", "2_19.log")]
    public void 片方のログウィンドウだけ監視できる(
        bool watchWindow1,
        bool watchWindow2,
        string expectedFirstFile,
        string expectedLastFile)
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var config = new CollectorConfig
        {
            WatchWindow1 = watchWindow1,
            WatchWindow2 = watchWindow2,
            RotationSlots = 20,
        };

        var actual = _builder.Build(temporaryDirectory.Path, config);

        Assert.Equal(20, actual.Count);
        Assert.Equal(
            temporaryDirectory.GetPath(expectedFirstFile),
            actual[0]);
        Assert.Equal(
            temporaryDirectory.GetPath(expectedLastFile),
            actual[19]);
    }

    [Fact]
    public void RotationSlotsを監視対象数に反映できる()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var config = new CollectorConfig
        {
            WatchWindow1 = true,
            WatchWindow2 = false,
            RotationSlots = 5,
        };

        var actual = _builder.Build(temporaryDirectory.Path, config);

        Assert.Equal(5, actual.Count);
        Assert.Equal(
            temporaryDirectory.GetPath("1_4.log"),
            actual[^1]);
    }

    [Fact]
    public void 両方のログウィンドウが無効なら監視対象は空になる()
    {
        var config = new CollectorConfig
        {
            WatchWindow1 = false,
            WatchWindow2 = false,
        };

        var actual = _builder.Build(@"C:\FFXI\TEMP", config);

        Assert.Empty(actual);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(21)]
    public void RotationSlotsの範囲外を拒否する(int rotationSlots)
    {
        var config = new CollectorConfig
        {
            RotationSlots = rotationSlots,
        };

        var exception = Assert.Throws<ArgumentOutOfRangeException>(
            () => _builder.Build(@"C:\FFXI\TEMP", config));

        Assert.Contains(
            "ローテーション数は0から20の範囲",
            exception.Message,
            StringComparison.Ordinal);
    }
}
