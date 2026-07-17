using FFXI_LogAnalyzer.Core;

namespace FFXI_LogAnalyzer.App;

public sealed class ActorSummaryViewModel
{
    public ActorSummaryViewModel(ActorSummary summary)
    {
        Actor = summary.Actor;
        TotalDamage = summary.TotalDamage.ToString("N0");
        Dps = FormatNullable(summary.Dps);
        DpsTimeConfidence = summary.DpsTimeConfidence.ToString();
        NormalAttackHitRate = FormatNormalAttackHitRate(summary);
        NormalAttackCriticalRate = FormatNormalAttackCriticalRate(summary);
        TotalUseCount = summary.TotalUseCount.ToString("N0");
        TotalHitCount = summary.TotalHitCount.ToString("N0");
        TotalMissCount = summary.TotalMissCount.ToString("N0");
        UnknownCount = summary.UnknownCount.ToString("N0");
    }

    public string Actor { get; }

    public string TotalDamage { get; }

    public string Dps { get; }

    public string DpsTimeConfidence { get; }

    public string NormalAttackHitRate { get; }

    public string NormalAttackCriticalRate { get; }

    public string TotalUseCount { get; }

    public string TotalHitCount { get; }

    public string TotalMissCount { get; }

    public string UnknownCount { get; }

    private static string FormatNormalAttackHitRate(ActorSummary summary)
    {
        var hitCount = summary.NormalAttackSummary.HitCount;
        var useCount = hitCount + summary.NormalAttackSummary.MissCount;
        return FormatRateWithCount(
            summary.NormalAttackSummary.HitRate,
            hitCount,
            useCount);
    }

    private static string FormatNormalAttackCriticalRate(ActorSummary summary)
    {
        return FormatRateWithCount(
            summary.NormalAttackSummary.CriticalRate,
            summary.NormalAttackSummary.CriticalHitCount,
            summary.NormalAttackSummary.HitCount);
    }

    private static string FormatRateWithCount(double? rate, int numerator, int denominator)
    {
        return rate is null
            ? $"-({numerator:N0}/{denominator:N0})"
            : $"{rate.Value * 100:0.#}％({numerator:N0}/{denominator:N0})";
    }

    private static string FormatNullable(double? value)
    {
        return value is null ? "-" : value.Value.ToString("0.###");
    }
}
