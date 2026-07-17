namespace FFXI_LogAnalyzer.Core;

public sealed record NormalAttackSummary(
    int HitCount,
    int MissCount,
    int CriticalHitCount,
    double? HitRate,
    double? CriticalRate);
