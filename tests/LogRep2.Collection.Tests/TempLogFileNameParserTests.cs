using FfxiTempLogCollector.Core;

namespace FfxiTempLogCollector.Tests;

public sealed class TempLogFileNameParserTests
{
    private readonly TempLogFileNameParser _parser = new();

    [Theory]
    [InlineData("1_0.log", 1, 0)]
    [InlineData("1_19.log", 1, 19)]
    [InlineData("2_0.log", 2, 0)]
    [InlineData("2_19.log", 2, 19)]
    public void 有効なファイル名をパースできる(
        string fileName,
        int expectedWindowId,
        int expectedRotationIndex)
    {
        var succeeded = _parser.TryParse(fileName, out var actual);

        Assert.True(succeeded);
        Assert.NotNull(actual);
        Assert.Equal(fileName, actual.FileName);
        Assert.Equal(expectedWindowId, actual.WindowId);
        Assert.Equal(expectedRotationIndex, actual.RotationIndex);
    }

    [Theory]
    [InlineData("1_20.log")]
    [InlineData("3_0.log")]
    [InlineData("2_8(1).log")]
    [InlineData("1_a.log")]
    [InlineData("1_00.log")]
    [InlineData("1_0.LOG")]
    [InlineData(@"TEMP\1_0.log")]
    [InlineData("")]
    [InlineData(null)]
    public void 無効なファイル名を拒否できる(string? fileName)
    {
        var succeeded = _parser.TryParse(fileName, out var actual);

        Assert.False(succeeded);
        Assert.Null(actual);
    }
}
