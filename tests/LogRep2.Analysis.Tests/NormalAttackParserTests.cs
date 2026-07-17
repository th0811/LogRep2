using FFXI_LogAnalyzer.Core;

namespace FFXI_LogAnalyzer.Tests;

public class NormalAttackParserTests
{
    [Fact]
    public void TryParse_DetectsNormalAttack()
    {
        var parsed = new NormalAttackParser().TryParse(
            TestActionGroupFactory.Create("Xitraの攻撃。"),
            out var result);

        Assert.True(parsed);
        Assert.Equal("Xitra", result.Actor);
        Assert.Equal("通常攻撃", result.ActionName);
        Assert.Equal(ActionType.NormalAttack, result.ActionType);
    }

    [Fact]
    public void TryParse_DetectsCriticalNormalAttack()
    {
        var parsed = new NormalAttackParser().TryParse(
            TestActionGroupFactory.Create(
                "Xitraの攻撃。クリティカル！",
                "→Gurfurlur the Menacingに、573ダメージ。"),
            out var result);

        Assert.True(parsed);
        Assert.Equal("Xitra", result.Actor);
        Assert.Equal("通常攻撃", result.ActionName);
        Assert.Equal(ActionType.NormalAttackCritical, result.ActionType);
    }

    [Fact]
    public void TryParse_DetectsSingleLineNormalAttack()
    {
        var parsed = new NormalAttackParser().TryParse(
            TestActionGroupFactory.Create("Xitraの攻撃→Gurfurlur the Menacingに、505ダメージ。"),
            out var result);

        Assert.True(parsed);
        Assert.Equal("Xitra", result.Actor);
        Assert.Equal(ActionType.NormalAttack, result.ActionType);
    }

    [Fact]
    public void TryParse_ReturnsFalseForNonNormalAttack()
    {
        var parsed = new NormalAttackParser().TryParse(
            TestActionGroupFactory.Create("Xitraは、レッドロータスを実行。"),
            out _);

        Assert.False(parsed);
    }
}
