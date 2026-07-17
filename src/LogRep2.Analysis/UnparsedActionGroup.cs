namespace FFXI_LogAnalyzer.Core;

public sealed record UnparsedActionGroup(
    ActionGroup Group,
    ParsedAction ParsedAction,
    string Reason);
