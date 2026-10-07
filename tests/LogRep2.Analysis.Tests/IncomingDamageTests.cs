using FFXI_LogAnalyzer.Core;

namespace FFXI_LogAnalyzer.Tests;

public sealed class IncomingDamageTests
{
    [Theory]
    [InlineData("Enemyの攻撃→Aliceに、15ダメージ。", 15)]
    [InlineData("Enemyの攻撃→Aliceに、0ダメージ。", 0)]
    [InlineData("Enemyの攻撃。→Aliceに、15ダメージ。", 15)]
    public void 宣言と結果が一行でも被ダメージを対象に集計する(string line, int damage)
    {
        var result = Analyze([line]);
        var target = result.ActorSummaries.Single(item => item.Actor == "Alice");
        Assert.Equal(damage, target.IncomingDamage.TotalDamage);
        Assert.Equal(1, target.IncomingHitCount);
        Assert.Equal(damage, Assert.Single(target.IncomingDamage.DamageValues));
        Assert.Equal(0, result.ActorSummaries.Single(item => item.Actor == "Enemy").IncomingDamage.TotalDamage);
    }

    [Fact]
    public void 一行の魔法や回避も対象を識別し魔法は回避率に含めない()
    {
        var result = Analyze(
            ["Enemyの攻撃→Aliceは攻撃をかわした。"],
            ["Enemyのファイアが発動。→Aliceに、200ダメージ。"]);
        var target = result.ActorSummaries.Single(item => item.Actor == "Alice");
        Assert.Equal(200, target.IncomingDamage.TotalDamage);
        Assert.Equal(0, target.IncomingHitCount);
        Assert.Equal(1, target.EvadeCount);
        Assert.Equal(1d, target.EvasionRate);
    }

    [Fact]
    public void 対象付きミスを回避に数え魔法や効果なしは除外する()
    {
        var result = Analyze(
            ["Enemyの攻撃→Aliceに、ミス。"],
            ["Enemyのファイアが発動→Aliceに、ミス。"],
            ["Enemyの弱体技！", "→Aliceに、効果なし。"]);
        var target = result.ActorSummaries.Single(item => item.Actor == "Alice");
        Assert.Equal(1, target.EvadeCount);
        Assert.Equal(0, target.IncomingHitCount);
        Assert.Equal(0, target.IncomingDamage.TotalDamage);
    }

    [Fact]
    public void 範囲攻撃と魔法を対象別に集計し回避の分母から魔法を除く()
    {
        var result = Analyze(
            ["Enemyは、範囲技を実行。", "→Aliceに、100ダメージ。", "→Boroは攻撃をかわした。"],
            ["Enemyの攻撃。", "→Aliceは、0ダメージ。"],
            ["Enemyの攻撃。", "ミス！Aliceは攻撃をかわした。"],
            ["Enemyのファイアが発動。", "→Aliceに、200ダメージ。"],
            ["Enemyの攻撃。ミス。"],
            ["Enemyの攻撃。", "Aliceの分身が攻撃を受けて消えた。"],
            ["Enemyの弱体技！", "→Aliceに効果なし。"]);
        var alice = result.ActorSummaries.Single(item => item.Actor == "Alice");
        Assert.Equal(0, alice.TotalDamage);
        Assert.Equal(300, alice.IncomingDamage.TotalDamage);
        Assert.Equal(200, alice.IncomingDamage.MaxDamage);
        Assert.Equal(0, alice.IncomingDamage.MinDamage);
        Assert.Equal(100, alice.IncomingDamage.AverageDamage);
        Assert.Equal(2, alice.IncomingHitCount);
        Assert.Equal(1, alice.EvadeCount);
        Assert.Equal(1d / 3, alice.EvasionRate);
        var boro = result.ActorSummaries.Single(item => item.Actor == "Boro");
        Assert.Equal(1d, boro.EvasionRate);
        Assert.Null(boro.IncomingDamage.AverageDamage);
    }

    [Fact]
    public void 連携と実行者不明の被ダメージを数え回避率には混ぜない()
    {
        var result = Analyze(
            ["Enemyは、範囲技を実行。", "→Aliceに、80ダメージ。", "技連携・切断！", "→Aliceに、20ダメージ。"],
            ["→Boroに、50ダメージ。"]);
        var alice = result.ActorSummaries.Single(item => item.Actor == "Alice");
        Assert.Equal(100, alice.IncomingDamage.TotalDamage);
        Assert.Equal(1, alice.IncomingHitCount);
        var boro = result.ActorSummaries.Single(item => item.Actor == "Boro");
        Assert.Equal(50, boro.IncomingDamage.TotalDamage);
        Assert.Null(boro.EvasionRate);
    }

    private static AnalysisResult Analyze(params string[][] groups)
    {
        var records = groups.SelectMany((lines, group) => lines.Select((line, index) => new CanonicalRecord
        {
            SessionId = "session",
            EventGroup = group.ToString(),
            Order = group * 100 + index,
            VisibleText = line,
        })).ToArray();
        return new RealtimeAnalysisEngine().Analyze(records, 0, records.Length).Result;
    }
}
