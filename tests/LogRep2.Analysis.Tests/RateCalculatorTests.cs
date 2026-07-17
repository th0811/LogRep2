using FFXI_LogAnalyzer.Core;

namespace FFXI_LogAnalyzer.Tests;

public class RateCalculatorTests
{
    [Fact]
    public void CalculateHitRate_ReturnsHitOverHitAndMiss()
    {
        Assert.Equal(0.75, RateCalculator.CalculateHitRate(hitCount: 3, missCount: 1));
    }

    [Fact]
    public void CalculateHitRate_ReturnsNullWhenDenominatorIsZero()
    {
        Assert.Null(RateCalculator.CalculateHitRate(hitCount: 0, missCount: 0));
    }

    [Fact]
    public void CalculateCriticalRate_ReturnsCriticalHitOverNormalAttackHit()
    {
        Assert.Equal(0.25, RateCalculator.CalculateCriticalRate(normalAttackCriticalHitCount: 1, normalAttackHitCount: 4));
    }

    [Fact]
    public void CalculateDps_ReturnsNullWhenTimeIsUnknown()
    {
        var analysisTime = AnalysisTimeResult.Unknown(["時刻不明"]);

        Assert.Null(RateCalculator.CalculateDps(100, analysisTime));
    }
}
