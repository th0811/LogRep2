namespace FFXI_LogAnalyzer.Core;

public sealed record AnalysisResult(
    IReadOnlyList<ActorSummary> ActorSummaries,
    IReadOnlyList<ActionSummary> ActionSummaries,
    IReadOnlyList<UnparsedActionGroup> UnparsedActionGroups,
    AnalysisTimeResult AnalysisTime)
{
    public int ExcludedRecordCount { get; init; }

    public int ExcludedGroupCount { get; init; }

    public IReadOnlyList<ParsedActionGroup> UnknownActionGroups { get; init; } = [];

    public IReadOnlyList<LevelingPointSummary> LevelingPointSummaries { get; init; } = [];
}
