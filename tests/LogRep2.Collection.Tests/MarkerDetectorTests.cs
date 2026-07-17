using FfxiTempLogCollector.Core;

namespace FfxiTempLogCollector.Tests;

public sealed class MarkerDetectorTests
{
    private readonly MarkerDetector _detector = new();

    [Theory]
    [InlineData("/echo ###start:aminon", "start:aminon")]
    [InlineData("[21:35] ###end:aminon", "end:aminon")]
    [InlineData("/echo ###aminon_start", "aminon_start")]
    [InlineData("/echo ###aminon_end", "aminon_end")]
    [InlineData("/echo ###test", "test")]
    [InlineData("/echo ### test", "test")]
    [InlineData("[20:29] ### mark1", "mark1")]
    [InlineData("/echo ###　test", "test")]
    [InlineData("日本語 ###開始", "開始")]
    public void マーカーを検出できる(
        string visibleText,
        string expectedKeyword)
    {
        var actual = _detector.Detect(visibleText);

        Assert.NotNull(actual);
        Assert.True(actual.IsMarker);
        Assert.Equal(expectedKeyword, actual.Keyword);
    }

    [Theory]
    [InlineData("マーカーなし")]
    [InlineData("#")]
    [InlineData("#test")]
    [InlineData("#　")]
    [InlineData("")]
    public void マーカーがなければNullを返す(string visibleText)
    {
        var actual = _detector.Detect(visibleText);

        Assert.Null(actual);
    }

    [Fact]
    public void 時刻とマーカーを個別に検出できる()
    {
        const string visibleText = "[21:35] ###end:aminon";

        var timestamp = new TimestampExtractor().Extract(visibleText);
        var marker = _detector.Detect(visibleText);

        Assert.NotNull(timestamp);
        Assert.Equal("21:35", timestamp.TimeText);
        Assert.NotNull(marker);
        Assert.Equal("end:aminon", marker.Keyword);
    }

    [Fact]
    public void 任意のマーカー文字列を使える()
    {
        const string visibleText = "[21:35] @@@ end:aminon";

        var marker = _detector.Detect(visibleText, "@@@");

        Assert.NotNull(marker);
        Assert.Equal("end:aminon", marker.Keyword);
    }
}
