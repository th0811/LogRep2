using FFXI_LogAnalyzer.Core;

namespace FFXI_LogAnalyzer.Tests;

public class HitStatusClassifierTests
{
    [Fact]
    public void Classify_DamageValueIsHit()
    {
        var status = Classify(
            CreateGroup("→敵に、123ダメージ。"),
            ParsedDamageResult.FromDamage(123));

        Assert.Equal(HitStatus.Hit, status);
    }

    [Fact]
    public void Classify_ZeroDamageIsHit()
    {
        var status = Classify(
            CreateGroup("→敵に、0ダメージ。"),
            ParsedDamageResult.FromDamage(0));

        Assert.Equal(HitStatus.Hit, status);
    }

    [Theory]
    [InlineData("Xitraの攻撃。ミス。")]
    [InlineData("敵は攻撃を回避した。")]
    [InlineData("敵は攻撃をかわした。")]
    [InlineData("効果なし。")]
    [InlineData("効果がなかった。")]
    [InlineData("レジストされた！")]
    public void Classify_MissKeywordsAreMiss(string visibleText)
    {
        var status = Classify(CreateGroup(visibleText), ParsedDamageResult.None);

        Assert.Equal(HitStatus.Miss, status);
    }

    [Theory]
    [InlineData("詠唱中断。")]
    [InlineData("発動失敗。")]
    [InlineData("使用失敗。")]
    public void Classify_ExcludedKeywordsAreExcluded(string visibleText)
    {
        var status = Classify(CreateGroup(visibleText), ParsedDamageResult.None);

        Assert.Equal(HitStatus.Excluded, status);
    }

    [Fact]
    public void Classify_UnknownWhenNoRuleMatches()
    {
        var status = Classify(CreateGroup("判定できないログ。"), ParsedDamageResult.None);

        Assert.Equal(HitStatus.Unknown, status);
    }

    private static HitStatus Classify(ActionGroup group, ParsedDamageResult damage)
    {
        return new HitStatusClassifier().Classify(group, damage);
    }

    private static ActionGroup CreateGroup(params string[] visibleTexts)
    {
        return TestActionGroupFactory.Create(visibleTexts);
    }
}
