namespace FFXI_LogAnalyzer.Core;

public sealed record LevelingPointSummary(
    string PointName,
    long TotalPoints,
    int MaxChainCount,
    double? PointsPerHour);
