using FFXI_LogAnalyzer.Core;

namespace FFXI_LogAnalyzer.Tests;

public class DamageParserTests
{
    [Theory]
    [InlineData("→敵に、123ダメージ。", 123)]
    [InlineData("敵は、456ダメージを受けた。", 456)]
    [InlineData("789ダメージ。", 789)]
    [InlineData("→敵に、0ダメージ。", 0)]
    [InlineData("→Nostos Maridから、2042HP吸収。", 2042)]
    public void ParseDamage_ExtractsDamageValue(string visibleText, int expectedDamage)
    {
        var damage = new DamageParser().ParseDamage(CreateGroup(visibleText));

        Assert.True(damage.HasDamage);
        Assert.Equal(expectedDamage, damage.Damage);
        Assert.Equal([expectedDamage], damage.DamageValues);
    }

    [Fact]
    public void ParseDamage_AddsMultipleDamageValues()
    {
        var damage = new DamageParser().ParseDamage(CreateGroup(
            "→敵Aに、100ダメージ。",
            "→敵Bに、200ダメージ。"));

        Assert.True(damage.HasDamage);
        Assert.Equal(300, damage.Damage);
        Assert.Equal([100, 200], damage.DamageValues);
    }

    private static ActionGroup CreateGroup(params string[] visibleTexts)
    {
        return TestActionGroupFactory.Create(visibleTexts);
    }
}
