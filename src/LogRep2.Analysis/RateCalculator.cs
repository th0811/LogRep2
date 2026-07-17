namespace FFXI_LogAnalyzer.Core;

public static class RateCalculator
{
    public static double? CalculateHitRate(int hitCount, int missCount)
    {
        var denominator = hitCount + missCount;
        return denominator <= 0
            ? null
            : (double)hitCount / denominator;
    }

    public static double? CalculateCriticalRate(int normalAttackCriticalHitCount, int normalAttackHitCount)
    {
        return normalAttackHitCount <= 0
            ? null
            : (double)normalAttackCriticalHitCount / normalAttackHitCount;
    }

    public static double? CalculateDps(int totalDamage, AnalysisTimeResult analysisTime)
    {
        if (analysisTime.Confidence == TimeConfidence.Unknown || !analysisTime.CanCalculateDps)
        {
            return null;
        }

        return totalDamage / analysisTime.DurationSeconds!.Value;
    }
}
