using FFXI_LogAnalyzer.Core;

namespace FFXI_LogAnalyzer.Tests;

public class AnalysisTimeResolverTests
{
    [Fact]
    public void Resolve_SecondPrecisionMarkers_ReturnsExact()
    {
        var selection = CreateMarkerSelection(
            CreateMarker(10, "[21:35:10]"),
            CreateMarker(20, "[21:35:40]"));

        var result = new AnalysisTimeResolver().Resolve(selection, []);

        Assert.Equal(TimeConfidence.Exact, result.Confidence);
        Assert.Equal(30, result.DurationSeconds);
        Assert.True(result.CanCalculateDps);
    }

    [Fact]
    public void Resolve_MinutePrecisionMarkers_ReturnsMinute()
    {
        var selection = CreateMarkerSelection(
            CreateMarker(10, "[21:35]"),
            CreateMarker(20, "[21:37]"));

        var result = new AnalysisTimeResolver().Resolve(selection, []);

        Assert.Equal(TimeConfidence.Minute, result.Confidence);
        Assert.Equal(179, result.DurationSeconds);
        Assert.True(result.CanCalculateDps);
    }

    [Fact]
    public void Resolve_SameMinute_Returns59Seconds()
    {
        var selection = CreateMarkerSelection(
            CreateMarker(10, "[21:35]"),
            CreateMarker(20, "[21:35]"));

        var result = new AnalysisTimeResolver().Resolve(selection, []);

        Assert.Equal(TimeConfidence.Minute, result.Confidence);
        Assert.Equal(59, result.DurationSeconds);
    }

    [Fact]
    public void Resolve_NextMinute_Returns119Seconds()
    {
        var selection = CreateMarkerSelection(
            CreateMarker(10, "[21:35]"),
            CreateMarker(20, "[21:36]"));

        var result = new AnalysisTimeResolver().Resolve(selection, []);

        Assert.Equal(TimeConfidence.Minute, result.Confidence);
        Assert.Equal(119, result.DurationSeconds);
    }

    [Fact]
    public void Resolve_LogStartAndLogEnd_ReturnsEstimated()
    {
        var records = new[]
        {
            CreateRecord(1, "[10:00:00]"),
            CreateRecord(2, null),
            CreateRecord(3, "[10:00:20]")
        };
        var selection = new AnalysisRangeSelection(
            AnalysisEndpoint.LogStart,
            AnalysisEndpoint.LogEnd);

        var result = new AnalysisTimeResolver().Resolve(selection, records);

        Assert.Equal(TimeConfidence.Estimated, result.Confidence);
        Assert.Equal(20, result.DurationSeconds);
        Assert.True(result.CanCalculateDps);
    }

    [Fact]
    public void Resolve_LogStartAndLogEnd_UsesFirstSeenAtAndLastSeenAtWhenMessageTimeIsMissing()
    {
        var start = DateTimeOffset.Parse("2026-06-23T10:00:00+09:00");
        var end = DateTimeOffset.Parse("2026-06-23T10:00:30+09:00");
        var records = new[]
        {
            new CanonicalRecord { Order = 1, FirstSeenAt = start },
            new CanonicalRecord { Order = 2, LastSeenAt = end }
        };
        var selection = new AnalysisRangeSelection(
            AnalysisEndpoint.LogStart,
            AnalysisEndpoint.LogEnd);

        var result = new AnalysisTimeResolver().Resolve(selection, records);

        Assert.Equal(TimeConfidence.Estimated, result.Confidence);
        Assert.Equal(30, result.DurationSeconds);
        Assert.True(result.CanCalculateDps);
    }

    [Fact]
    public void Resolve_ReturnsUnknownWhenTimeCannotBeResolved()
    {
        var records = new[]
        {
            new CanonicalRecord { Order = 1, VisibleText = "時刻なし" }
        };
        var selection = new AnalysisRangeSelection(
            AnalysisEndpoint.LogStart,
            AnalysisEndpoint.LogEnd);

        var result = new AnalysisTimeResolver().Resolve(selection, records);

        Assert.Equal(TimeConfidence.Unknown, result.Confidence);
        Assert.Null(result.DurationSeconds);
        Assert.False(result.CanCalculateDps);
        Assert.NotEmpty(result.Warnings);
    }

    [Fact]
    public void Resolve_HandlesDateRollover()
    {
        var selection = CreateMarkerSelection(
            CreateMarker(10, "[23:59]"),
            CreateMarker(20, "[00:01]"));

        var result = new AnalysisTimeResolver().Resolve(selection, []);

        Assert.Equal(TimeConfidence.Minute, result.Confidence);
        Assert.Equal(179, result.DurationSeconds);
        Assert.True(result.CanCalculateDps);
    }

    [Fact]
    public void Resolve_NonPositiveDuration_CannotCalculateDps()
    {
        var selection = CreateMarkerSelection(
            CreateMarker(10, "[12:00:00]"),
            CreateMarker(20, "[12:00:00]"));

        var result = new AnalysisTimeResolver().Resolve(selection, []);

        Assert.Equal(TimeConfidence.Exact, result.Confidence);
        Assert.Equal(0, result.DurationSeconds);
        Assert.False(result.CanCalculateDps);
        Assert.NotEmpty(result.Warnings);
    }

    private static AnalysisRangeSelection CreateMarkerSelection(MarkerRecord start, MarkerRecord end)
    {
        return new AnalysisRangeSelection(
            AnalysisEndpoint.FromMarker(start),
            AnalysisEndpoint.FromMarker(end));
    }

    private static MarkerRecord CreateMarker(long order, string? messageTimeText)
    {
        var record = new CanonicalRecord
        {
            Order = order,
            IsMarker = true,
            MessageTimeText = messageTimeText
        };

        return new MarkerRecord(order, "#marker", "#marker", messageTimeText, null, record);
    }

    private static CanonicalRecord CreateRecord(long order, string? messageTimeText)
    {
        return new CanonicalRecord
        {
            Order = order,
            MessageTimeText = messageTimeText
        };
    }
}
