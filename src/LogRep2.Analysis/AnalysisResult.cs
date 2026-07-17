namespace FFXI_LogAnalyzer.Core;

public sealed record AnalysisResult(
    IReadOnlyList<ActorSummary> ActorSummaries,
    IReadOnlyList<ActionSummary> ActionSummaries,
    IReadOnlyList<UnparsedActionGroup> UnparsedActionGroups,
    AnalysisTimeResult AnalysisTime)
{
    public IReadOnlyList<ParsedActionGroup> UnknownActionGroups { get; init; } = [];

    public IReadOnlyList<LevelingPointSummary> LevelingPointSummaries { get; init; } = [];
}
