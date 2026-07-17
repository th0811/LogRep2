using FFXI_LogAnalyzer.Core;

namespace FFXI_LogAnalyzer.Tests;

public class AnalysisRangeBuilderTests
{
    [Fact]
    public void Build_ExcludesSelectedMarkerRows()
    {
        var records = new[]
        {
            CreateRecord("before", 5),
            CreateMarkerRecord("start-marker", 10),
            CreateRecord("inside-1", 11),
            CreateRecord("inside-2", 19),
            CreateMarkerRecord("end-marker", 20),
            CreateRecord("after", 21)
        };
        var markers = new MarkerExtractor().Extract(records);
        var selection = new AnalysisRangeSelection(
            AnalysisEndpoint.FromMarker(markers[0]),
            AnalysisEndpoint.FromMarker(markers[1]));

        var range = new AnalysisRangeBuilder().Build(records, selection);

        Assert.Equal(["inside-1", "inside-2"], range.Select(record => record.CanonicalRecordId));
        Assert.DoesNotContain(range, record => record.IsMarker);
    }

    [Fact]
    public void Build_ExcludesMarkerRowsInsideRange()
    {
        var records = new[]
        {
            CreateMarkerRecord("start-marker", 10),
            CreateRecord("inside-1", 11),
            CreateMarkerRecord("middle-marker", 15),
            CreateRecord("inside-2", 16),
            CreateMarkerRecord("end-marker", 20)
        };
        var markers = new MarkerExtractor().Extract(records);
        var selection = new AnalysisRangeSelection(
            AnalysisEndpoint.FromMarker(markers[0]),
            AnalysisEndpoint.FromMarker(markers[2]));

        var range = new AnalysisRangeBuilder().Build(records, selection);

        Assert.Equal(["inside-1", "inside-2"], range.Select(record => record.CanonicalRecordId));
    }

    [Fact]
    public void Build_LogStartAndLogEndIncludesEdgeRecords()
    {
        var records = new[]
        {
            CreateRecord("first", 1),
            CreateRecord("middle", 2),
            CreateRecord("last", 3)
        };
        var selection = new AnalysisRangeSelection(
            AnalysisEndpoint.LogStart,
            AnalysisEndpoint.LogEnd);

        var range = new AnalysisRangeBuilder().Build(records, selection);

        Assert.Equal(["first", "middle", "last"], range.Select(record => record.CanonicalRecordId));
    }

    [Fact]
    public void Build_ThrowsWhenSelectionIsInvalid()
    {
        var records = new[]
        {
            CreateMarkerRecord("start-marker", 20),
            CreateMarkerRecord("end-marker", 10)
        };
        var markers = new MarkerExtractor().Extract(records);
        var selection = new AnalysisRangeSelection(
            AnalysisEndpoint.FromMarker(markers[1]),
            AnalysisEndpoint.FromMarker(markers[0]));

        var exception = Assert.Throws<ArgumentException>(() => new AnalysisRangeBuilder().Build(records, selection));

        Assert.Contains("分析区間が不正", exception.Message);
    }

    private static CanonicalRecord CreateRecord(string id, long order)
    {
        return new CanonicalRecord
        {
            CanonicalRecordId = id,
            Order = order,
            VisibleText = id
        };
    }

    private static CanonicalRecord CreateMarkerRecord(string id, long order)
    {
        return new CanonicalRecord
        {
            CanonicalRecordId = id,
            Order = order,
            IsMarker = true,
            MarkerKeyword = id,
            VisibleText = id
        };
    }
}
