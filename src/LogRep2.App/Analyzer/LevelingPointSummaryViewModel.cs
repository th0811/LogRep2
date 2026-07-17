using FFXI_LogAnalyzer.Core;

namespace FFXI_LogAnalyzer.App;

public sealed class LevelingPointSummaryViewModel
{
    public LevelingPointSummaryViewModel(LevelingPointSummary summary)
    {
        PointName = summary.PointName;
        TotalPoints = summary.TotalPoints.ToString("N0");
        MaxChainCount = summary.MaxChainCount.ToString("N0");
        PointsPerHour = summary.PointsPerHour is null
            ? "-"
            : summary.PointsPerHour.Value.ToString("N0");
    }

    public string PointName { get; }

    public string TotalPoints { get; }

    public string MaxChainCount { get; }

    public string PointsPerHour { get; }
}
