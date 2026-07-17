using FFXI_LogAnalyzer.Core;

namespace FFXI_LogAnalyzer.Tests;

public class MarkerExtractorTests
{
    [Fact]
    public void Extract_ReturnsMarkerRecords()
    {
        var records = new[]
        {
            CreateRecord(1, "通常ログ"),
            CreateMarker(2, "#start", "開始"),
            CreateRecord(3, "通常ログ2")
        };

        var markers = new MarkerExtractor().Extract(records);

        var marker = Assert.Single(markers);
        Assert.Equal(2, marker.Order);
        Assert.Equal("#start", marker.MarkerKeyword);
        Assert.Equal("開始", marker.VisibleText);
        Assert.Equal("[12:00]", marker.MessageTimeText);
        Assert.Equal(DateTimeOffset.Parse("2026-06-23T12:00:00+09:00"), marker.FirstSeenAt);
    }

    [Fact]
    public void Extract_TreatsSameMarkerKeywordAsSeparateCandidates()
    {
        var records = new[]
        {
            CreateMarker(10, "#phase", "1回目"),
            CreateMarker(20, "#phase", "2回目")
        };

        var markers = new MarkerExtractor().Extract(records);

        Assert.Equal(2, markers.Count);
        Assert.Equal(["1回目", "2回目"], markers.Select(marker => marker.VisibleText));
        Assert.All(markers, marker => Assert.Equal("#phase", marker.MarkerKeyword));
    }

    private static CanonicalRecord CreateRecord(long order, string visibleText)
    {
        return new CanonicalRecord
        {
            Order = order,
            VisibleText = visibleText
        };
    }

    private static CanonicalRecord CreateMarker(long order, string keyword, string visibleText)
    {
        return new CanonicalRecord
        {
            Order = order,
            IsMarker = true,
            MarkerKeyword = keyword,
            VisibleText = visibleText,
            MessageTimeText = "[12:00]",
            FirstSeenAt = DateTimeOffset.Parse("2026-06-23T12:00:00+09:00")
        };
    }
}
