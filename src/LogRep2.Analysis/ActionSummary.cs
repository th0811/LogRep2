namespace FFXI_LogAnalyzer.Core;

public sealed record ActionSummary(
    string Actor,
    string ActionName,
    ActionType ActionType,
    int UseCount,
    int HitCount,
    int MissCount,
    int UnknownCount,
    double? HitRate,
    DamageStatistics Damage);
