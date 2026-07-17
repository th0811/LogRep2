using FFXI_LogAnalyzer.Core;

namespace FFXI_LogAnalyzer.Tests;

public class DamageStatisticsTests
{
    [Fact]
    public void Constructor_CalculatesMaxMinAverage()
    {
        var statistics = new DamageStatistics([100, 0, 300]);

        Assert.Equal(400, statistics.TotalDamage);
        Assert.Equal(300, statistics.MaxDamage);
        Assert.Equal(0, statistics.MinDamage);
        Assert.Equal(400.0 / 3, statistics.AverageDamage);
    }

    [Fact]
    public void Constructor_ReturnsNullStatsWhenNoDamage()
    {
        var statistics = new DamageStatistics([]);

        Assert.Equal(0, statistics.TotalDamage);
        Assert.Null(statistics.MaxDamage);
        Assert.Null(statistics.MinDamage);
        Assert.Null(statistics.AverageDamage);
    }
}
