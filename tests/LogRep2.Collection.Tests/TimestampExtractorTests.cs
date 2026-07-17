using FfxiTempLogCollector.Core;

namespace FfxiTempLogCollector.Tests;

public sealed class TimestampExtractorTests
{
    private readonly TimestampExtractor _extractor = new();

    [Fact]
    public void 分精度の時刻を抽出できる()
    {
        var actual = _extractor.Extract("[21:35] text");

        Assert.NotNull(actual);
        Assert.Equal("21:35", actual.TimeText);
        Assert.Equal("minute", actual.Precision);
        Assert.Equal(new TimeOnly(21, 35), actual.Time);
    }

    [Fact]
    public void 秒精度の時刻を抽出できる()
    {
        var actual = _extractor.Extract("[21:35:12] text");

        Assert.NotNull(actual);
        Assert.Equal("21:35:12", actual.TimeText);
        Assert.Equal("second", actual.Precision);
        Assert.Equal(new TimeOnly(21, 35, 12), actual.Time);
    }

    [Fact]
    public void 一桁の時を抽出できる()
    {
        var actual = _extractor.Extract("[9:05] text");

        Assert.NotNull(actual);
        Assert.Equal("9:05", actual.TimeText);
        Assert.Equal("minute", actual.Precision);
        Assert.Equal(new TimeOnly(9, 5), actual.Time);
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
