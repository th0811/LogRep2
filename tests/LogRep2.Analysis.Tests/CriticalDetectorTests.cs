using FFXI_LogAnalyzer.Core;

namespace FFXI_LogAnalyzer.Tests;

public class CriticalDetectorTests
{
    [Fact]
    public void IsCritical_ReturnsTrueWhenGroupContainsCriticalText()
    {
        var group = TestActionGroupFactory.Create(
            "Xitraの攻撃。",
            "クリティカル！");

        Assert.True(new CriticalDetector().IsCritical(group));
    }

    [Fact]
    public void IsCritical_ReturnsFalseWhenGroupDoesNotContainCriticalText()
    {
        var group = TestActionGroupFactory.Create("Xitraの攻撃。");

        Assert.False(new CriticalDetector().IsCritical(group));
    }
}
