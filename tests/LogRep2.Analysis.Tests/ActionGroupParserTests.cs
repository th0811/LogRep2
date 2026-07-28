using FFXI_LogAnalyzer.Core;

namespace FFXI_LogAnalyzer.Tests;

public class ActionGroupParserTests
{
    [Fact]
    public void ParseGroup_DamageValueIsHit()
    {
        var result = Parse(
            "Xitraの攻撃。",
            "→Gurfurlur the Menacingに、123ダメージ。");

        Assert.True(result.IsParsed);
        Assert.Equal("Xitra", result.Parsed!.Actor);
        Assert.Equal("通常攻撃", result.Parsed.ActionName);
        Assert.Equal(ActionType.NormalAttack, result.Parsed.ActionType);
        Assert.Equal(123, result.Parsed.Damage.Damage);
        Assert.Equal(HitStatus.Hit, result.Parsed.HitStatus);
    }

    [Fact]
    public void ParseGroup_ZeroDamageIsHit()
    {
        var result = Parse(
            "Xitraの攻撃。",
            "→Gurfurlur the Menacingに、0ダメージ。");

        Assert.True(result.IsParsed);
        Assert.Equal(0, result.Parsed!.Damage.Damage);
        Assert.Equal(HitStatus.Hit, result.Parsed.HitStatus);
    }

    [Fact]
    public void ParseGroup_MissIsMiss()
    {
        var result = Parse("Xitraの攻撃。ミス。");

        Assert.True(result.IsParsed);
        Assert.Equal(HitStatus.Miss, result.Parsed!.HitStatus);
    }

    [Fact]
    public void ParseGroup_NoEffectIsMiss()
    {
        var result = Parse(
            "Xitraの攻撃。",
            "効果なし。");

        Assert.True(result.IsParsed);
        Assert.Equal(HitStatus.Miss, result.Parsed!.HitStatus);
    }

    [Fact]
    public void ParseGroup_ResistedIsMiss()
    {
        var result = Parse(
            "Xitraは、ファイアを唱えた。",
            "レジストされた！");

        Assert.True(result.IsParsed);
        Assert.Equal("Xitra", result.Parsed!.Actor);
        Assert.Equal("ファイア", result.Parsed.ActionName);
        Assert.Equal(ActionType.Magic, result.Parsed.ActionType);
        Assert.Equal(HitStatus.Excluded, result.Parsed.HitStatus);
    }

    [Fact]
    public void ParseGroup_InterruptedIsExcluded()
    {
        var result = Parse(
            "Xitraは、ファイアを唱えた。",
            "詠唱中断。");

        Assert.True(result.IsParsed);
        Assert.Equal(HitStatus.Excluded, result.Parsed!.HitStatus);
    }

    [Fact]
    public void ParseGroup_UnknownHitStatusWhenNoRuleMatches()
    {
        var result = Parse("Xitraの攻撃。");

        Assert.True(result.IsParsed);
        Assert.Equal(HitStatus.Unknown, result.Parsed!.HitStatus);
    }

    [Fact]
    public void ParseGroup_UnknownActorOrActionBecomesUnparsed()
    {
        var result = Parse("誰が何をしたか分からないログ。");

        Assert.False(result.IsParsed);
        Assert.NotNull(result.Unparsed);
        Assert.Contains("actorまたはaction", result.Unparsed!.Reason);
        Assert.Equal(ActionType.Unknown, result.Unparsed.ParsedAction.ActionType);
    }

    [Fact]
    public void ParseGroup_SkillExecutionCanBeParsed()
    {
        var result = Parse(
            "Xitraは、レッドロータスを実行。",
            "→敵に、321ダメージ。");

        Assert.True(result.IsParsed);
        Assert.Equal("Xitra", result.Parsed!.Actor);
        Assert.Equal("レッドロータス", result.Parsed.ActionName);
        Assert.Equal(ActionType.Skill, result.Parsed.ActionType);
        Assert.Equal(321, result.Parsed.Damage.Damage);
    }

    [Fact]
    public void ParseGroup_CriticalNormalAttackCanBeParsed()
    {
        var result = Parse(
            "Xitraの攻撃。クリティカル！",
            "→Gurfurlur the Menacingに、573ダメージ。");

        Assert.True(result.IsParsed);
        Assert.Equal("Xitra", result.Parsed!.Actor);
        Assert.Equal("通常攻撃", result.Parsed.ActionName);
        Assert.Equal(ActionType.NormalAttackCritical, result.Parsed.ActionType);
        Assert.Equal(573, result.Parsed.Damage.Damage);
        Assert.Equal(HitStatus.Hit, result.Parsed.HitStatus);
    }

    [Fact]
    public void ParseGroup_SingleLineNormalAttackCanBeParsed()
    {
        var result = Parse("Xitraの攻撃→Gurfurlur the Menacingに、505ダメージ。");

        Assert.True(result.IsParsed);
        Assert.Equal("Xitra", result.Parsed!.Actor);
        Assert.Equal("通常攻撃", result.Parsed.ActionName);
        Assert.Equal(ActionType.NormalAttack, result.Parsed.ActionType);
        Assert.Equal(505, result.Parsed.Damage.Damage);
        Assert.Equal(HitStatus.Hit, result.Parsed.HitStatus);
    }

    [Fact]
    public void ParseGroup_NormalAttackMissCanBeParsed()
    {
        var result = Parse("Xitraの攻撃。ミス。");

        Assert.True(result.IsParsed);
        Assert.Equal("Xitra", result.Parsed!.Actor);
        Assert.Equal(ActionType.NormalAttack, result.Parsed.ActionType);
        Assert.Equal(HitStatus.Miss, result.Parsed.HitStatus);
    }

    [Fact]
    public void ParseGroup_CastStartIsExcluded()
    {
        var result = Parse("Xitraは、Gurfurlur the Menacingにファイアを唱えた。");

        Assert.True(result.IsParsed);
        Assert.Equal("Xitra", result.Parsed!.Actor);
        Assert.Equal("ファイア", result.Parsed.ActionName);
        Assert.Equal(ActionType.Magic, result.Parsed.ActionType);
        Assert.Equal(HitStatus.Excluded, result.Parsed.HitStatus);
    }

    [Fact]
    public void ParseGroup_MagicDamageActivationCanBeParsed()
    {
        var result = Parse(
            "Xitraのファイアが発動。",
            "→Gurfurlur the Menacingに、123ダメージ。");

        Assert.True(result.IsParsed);
        Assert.Equal("Xitra", result.Parsed!.Actor);
        Assert.Equal("ファイア", result.Parsed.ActionName);
        Assert.Equal(ActionType.Magic, result.Parsed.ActionType);
        Assert.Equal(123, result.Parsed.Damage.Damage);
        Assert.Equal(HitStatus.Hit, result.Parsed.HitStatus);
    }

    [Fact]
    public void ParseGroup_HpAbsorbDamageCanBeParsed()
    {
        var result = Parse(
            "Xitraのドレインが発動。",
            "→Nostos Maridから、2042HP吸収。");

        Assert.True(result.IsParsed);
        Assert.Equal("Xitra", result.Parsed!.Actor);
        Assert.Equal("ドレイン", result.Parsed.ActionName);
        Assert.Equal(ActionType.Magic, result.Parsed.ActionType);
        Assert.Equal(2042, result.Parsed.Damage.Damage);
        Assert.Equal(HitStatus.Hit, result.Parsed.HitStatus);
    }

    [Theory]
    [InlineData("MP")]
    [InlineData("TP")]
    public void ParseGroup_MpOrTpAbsorbIsHitWithoutDamage(string resource)
    {
        var result = Parse(
            "Alegreのアブゾタックが発動。",
            $"→Skomoraから、30{resource}吸収。");

        Assert.True(result.IsParsed);
        Assert.Equal("Alegre", result.Parsed!.Actor);
        Assert.Equal("アブゾタック", result.Parsed.ActionName);
        Assert.Equal(ActionType.Magic, result.Parsed.ActionType);
        Assert.False(result.Parsed.Damage.HasDamage);
        Assert.Equal(HitStatus.Hit, result.Parsed.HitStatus);
    }

    [Fact]
    public void ParseGroup_MagicRecoveryIsHitWithoutDamage()
    {
        var result = Parse(
            "TaechinzのケアルIVが発動。",
            "→AlegreのHPが、100回復。");

        Assert.True(result.IsParsed);
        Assert.Equal(ActionType.Magic, result.Parsed!.ActionType);
        Assert.False(result.Parsed.Damage.HasDamage);
        Assert.Equal(HitStatus.Hit, result.Parsed.HitStatus);
    }

    [Fact]
    public void ParseGroup_MagicStatusRemovalIsHitWithoutDamage()
    {
        var result = Parse(
            "Taechinzのイレースが発動。",
            "→Alegreのバイオの効果を消し去った！");

        Assert.True(result.IsParsed);
        Assert.Equal(ActionType.Magic, result.Parsed!.ActionType);
        Assert.False(result.Parsed.Damage.HasDamage);
        Assert.Equal(HitStatus.Hit, result.Parsed.HitStatus);
    }

    [Fact]
    public void ParseGroup_MagicResistIsMiss()
    {
        var result = Parse(
            "Alegreのフラッシュが発動。",
            "→Degeiは、魔法効果をレジストした！");

        Assert.True(result.IsParsed);
        Assert.Equal(ActionType.Magic, result.Parsed!.ActionType);
        Assert.False(result.Parsed.Damage.HasDamage);
        Assert.Equal(HitStatus.Miss, result.Parsed.HitStatus);
    }

    [Fact]
    public void ParseGroup_NormalAttackAbsorptionIsHitWithoutDamage()
    {
        var result = Parse("Leshonnの攻撃→AlegreのHPが、101回復！");

        Assert.True(result.IsParsed);
        Assert.Equal("Leshonn", result.Parsed!.Actor);
        Assert.Equal(ActionType.NormalAttack, result.Parsed.ActionType);
        Assert.False(result.Parsed.Damage.HasDamage);
        Assert.Equal(HitStatus.Hit, result.Parsed.HitStatus);
    }

    [Fact]
    public void ParseGroup_MagicEffectActivationCanBeParsed()
    {
        var result = Parse(
            "Xitraのプロテスが発動。",
            "→Xitraは、プロテスの効果。");

        Assert.True(result.IsParsed);
        Assert.Equal("Xitra", result.Parsed!.Actor);
        Assert.Equal("プロテス", result.Parsed.ActionName);
        Assert.Equal(ActionType.Magic, result.Parsed.ActionType);
        Assert.False(result.Parsed.Damage.HasDamage);
        Assert.Equal(HitStatus.Hit, result.Parsed.HitStatus);
    }

    [Fact]
    public void ParseGroup_MagicStatusActivationCanBeParsed()
    {
        var result = Parse(
            "Xitraのスリプルが発動。",
            "→Gurfurlur the Menacingは、睡眠の状態になった！");

        Assert.True(result.IsParsed);
        Assert.Equal("Xitra", result.Parsed!.Actor);
        Assert.Equal("スリプル", result.Parsed.ActionName);
        Assert.Equal(ActionType.Magic, result.Parsed.ActionType);
        Assert.False(result.Parsed.Damage.HasDamage);
        Assert.Equal(HitStatus.Hit, result.Parsed.HitStatus);
    }

    private static ActionGroupParseResult Parse(params string[] visibleTexts)
    {
        return new ActionGroupParser(new DefaultAnalysisRuleSet()).ParseGroup(TestActionGroupFactory.Create(visibleTexts));
    }
}
