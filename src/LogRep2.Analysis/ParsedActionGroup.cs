namespace FFXI_LogAnalyzer.Core;

public sealed record ParsedActionGroup(
    ActionGroup Group,
    string Actor,
    string ActionName,
    ActionType ActionType,
    ParsedDamageResult Damage,
    HitStatus HitStatus,
    ParsedAction ParsedAction);
