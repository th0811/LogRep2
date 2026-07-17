namespace FFXI_LogAnalyzer.Core;

public sealed record NormalAttackParseResult(
    string Actor,
    string ActionName,
    ActionType ActionType);
