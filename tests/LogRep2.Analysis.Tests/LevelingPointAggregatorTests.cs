using FFXI_LogAnalyzer.Core;

namespace FFXI_LogAnalyzer.Tests;

public sealed class LevelingPointAggregatorTests
{
    [Fact]
    public void Aggregate_CalculatesTotalPointsByType()
    {
        var summaries = Aggregate(
            new AnalysisTimeResult(TimeConfidence.Exact, 1800, null, null, []),
            "Xitraは、1024経験値を獲得した。",
            "Xitraは、200リミットポイントを獲得した。",
            "Xitraは、300エクゼンプラーポイントを獲得した。",
            "Xitraは、24経験値を獲得した。");

        Assert.Equal(1048, Find(summaries, "経験値").TotalPoints);
        Assert.Equal(200, Find(summaries, "リミットポイント").TotalPoints);
        Assert.Equal(300, Find(summaries, "エクゼンプラーポイント").TotalPoints);
    }

    [Fact]
    public void Aggregate_CalculatesPointsPerHour()
    {
        var summaries = Aggregate(
            new AnalysisTimeResult(TimeConfidence.Exact, 1800, null, null, []),
            "Xitraは、1024経験値を獲得した。");

        Assert.Equal(2048, Find(summaries, "経験値").PointsPerHour);
    }

    [Fact]
    public void Aggregate_UnknownTimeDoesNotCalculatePointsPerHour()
    {
        var summaries = Aggregate(
            AnalysisTimeResult.Unknown(["時刻不明"]),
            "Xitraは、1024経験値を獲得した。");

        Assert.Null(Find(summaries, "経験値").PointsPerHour);
    }

    [Fact]
    public void Aggregate_ReturnsAllPointTypesWhenNoPointLogs()
    {
        var summaries = Aggregate(
            new AnalysisTimeResult(TimeConfidence.Exact, 1800, null, null, []),
            "ポイントではないログ");

        Assert.Equal(3, summaries.Count);
        Assert.All(summaries, summary => Assert.Equal(0, summary.TotalPoints));
    }

    private static IReadOnlyList<LevelingPointSummary> Aggregate(
        AnalysisTimeResult analysisTime,
        params string[] visibleTexts)
    {
        var records = visibleTexts
            .Select(text => new CanonicalRecord { VisibleText = text })
            .ToArray();

        return new LevelingPointAggregator().Aggregate(records, analysisTime);
    }

    private static LevelingPointSummary Find(
        IEnumerable<LevelingPointSummary> summaries,
        string pointName)
    {
        return summaries.Single(summary => summary.PointName == pointName);
    }
}
