namespace FFXI_LogAnalyzer.Core;

public sealed record ActorSummary(
    string Actor,
    int TotalDamage,
    double? Dps,
    TimeConfidence DpsTimeConfidence,
    double? NormalAttackHitRate,
    double? NormalAttackCriticalRate,
    int TotalUseCount,
    int TotalHitCount,
    int TotalMissCount,
    int UnknownCount,
    NormalAttackSummary NormalAttackSummary,
    IReadOnlyList<ActionSummary> ActionSummaries);
