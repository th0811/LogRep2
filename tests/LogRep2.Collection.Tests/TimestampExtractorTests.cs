using FfxiTempLogCollector.Core;

namespace FfxiTempLogCollector.Tests;

public sealed class TimestampExtractorTests
{
    private readonly TimestampExtractor _extractor = new();

    [Theory]
    [InlineData("21:35", "minute", 21, 35, 0)]
    [InlineData("21:35:12", "second", 21, 35, 12)]
    [InlineData("9:05", "minute", 9, 5, 0)]
    public void 時刻と精度を抽出できる(string text, string precision, int hour, int minute, int second)
    {
        var actual = _extractor.Extract($"[{text}] 本文");

        Assert.NotNull(actual);
        Assert.Equal(text, actual.TimeText);
        Assert.Equal(precision, actual.Precision);
        Assert.Equal(new TimeOnly(hour, minute, second), actual.Time);
    }

    [Fact]
    public void 時刻がなければNullを返す()
    {
        var actual = _extractor.Extract("text without time");

        Assert.Null(actual);
    }

    [Theory]
    [InlineData("[24:00] text")]
    [InlineData("[12:60] text")]
    [InlineData("[12:30:60] text")]
    [InlineData("[1:5] text")]
    public void 範囲外または形式不正の時刻を拒否する(string visibleText)
    {
        var actual = _extractor.Extract(visibleText);

        Assert.Null(actual);
    }

    [Fact]
    public void 範囲外の時刻に続く有効な時刻を抽出できる()
    {
        var actual = _extractor.Extract("[25:00] invalid [23:59] valid");

        Assert.NotNull(actual);
        Assert.Equal("23:59", actual.TimeText);
    }
}
