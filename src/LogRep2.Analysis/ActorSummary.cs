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
    IReadOnlyList<ActionSummary> ActionSummaries)
{
    public DamageStatistics IncomingDamage { get; init; } = DamageStatistics.Empty;
    public int IncomingHitCount { get; init; }
    public int EvadeCount { get; init; }
    public double? EvasionRate => RateCalculator.CalculateHitRate(EvadeCount, IncomingHitCount);
}
