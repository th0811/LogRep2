using FFXI_LogAnalyzer.Core;

namespace FFXI_LogAnalyzer.Tests;

public class AnalysisAggregatorTests
{
    [Fact]
    public void Aggregate_CalculatesActorTotalDamage()
    {
        var result = Aggregate(
            Action("Xitra", "通常攻撃", ActionType.NormalAttack, HitStatus.Hit, [100]),
            Action("Xitra", "ファイア", ActionType.Magic, HitStatus.Hit, [200]));

        var actor = Assert.Single(result.ActorSummaries);
        Assert.Equal("Xitra", actor.Actor);
        Assert.Equal(300, actor.TotalDamage);
    }

    [Fact]
    public void Aggregate_CalculatesActorDps()
    {
        var result = AggregateWithTime(
            new AnalysisTimeResult(TimeConfidence.Exact, 10, null, null, []),
            Action("Xitra", "通常攻撃", ActionType.NormalAttack, HitStatus.Hit, [100]));

        var actor = Assert.Single(result.ActorSummaries);
        Assert.Equal(10, actor.Dps);
        Assert.Equal(TimeConfidence.Exact, actor.DpsTimeConfidence);
    }

    [Fact]
    public void Aggregate_HandlesUnavailableDps()
    {
        var result = AggregateWithTime(
            AnalysisTimeResult.Unknown(["時刻不明"]),
            Action("Xitra", "通常攻撃", ActionType.NormalAttack, HitStatus.Hit, [100]));

        var actor = Assert.Single(result.ActorSummaries);
        Assert.Null(actor.Dps);
        Assert.Equal(TimeConfidence.Unknown, actor.DpsTimeConfidence);
    }

    [Fact]
    public void Aggregate_CountsActionUseByActionGroup()
    {
        var result = Aggregate(
            Action("Xitra", "ファイア", ActionType.Magic, HitStatus.Hit, [100]),
            Action("Xitra", "ファイア", ActionType.Magic, HitStatus.Hit, [200]));

        var action = Assert.Single(result.ActionSummaries);
        Assert.Equal(2, action.UseCount);
    }

    [Fact]
    public void Aggregate_MultipleTargetsInOneActionGroupCountsAsOneUse()
    {
        var result = Aggregate(
            Action("Xitra", "ファイア", ActionType.Magic, HitStatus.Hit, [100, 200]));

        var action = Assert.Single(result.ActionSummaries);
        Assert.Equal(1, action.UseCount);
        Assert.Equal(300, action.Damage.TotalDamage);
    }

    [Fact]
    public void Aggregate_CalculatesDamageStatistics()
    {
        var result = Aggregate(
            Action("Xitra", "ファイア", ActionType.Magic, HitStatus.Hit, [100, 300]),
            Action("Xitra", "ファイア", ActionType.Magic, HitStatus.Hit, [200]));

        var action = Assert.Single(result.ActionSummaries);
        Assert.Equal(300, action.Damage.MaxDamage);
        Assert.Equal(100, action.Damage.MinDamage);
        Assert.Equal(200, action.Damage.AverageDamage);
    }

    [Fact]
    public void Aggregate_IncludesZeroDamageInMinimumDamage()
    {
        var result = Aggregate(
            Action("Xitra", "通常攻撃", ActionType.NormalAttack, HitStatus.Hit, [0]),
            Action("Xitra", "通常攻撃", ActionType.NormalAttack, HitStatus.Hit, [100]));

        var action = Assert.Single(result.ActionSummaries);
        Assert.Equal(0, action.Damage.MinDamage);
    }

    [Fact]
    public void Aggregate_ExcludesMissFromDamageStatistics()
    {
        var result = Aggregate(
            Action("Xitra", "通常攻撃", ActionType.NormalAttack, HitStatus.Miss, []),
            Action("Xitra", "通常攻撃", ActionType.NormalAttack, HitStatus.Hit, [100]));

        var action = Assert.Single(result.ActionSummaries);
        Assert.Equal(100, action.Damage.TotalDamage);
        Assert.Equal(100, action.Damage.MinDamage);
    }

    [Fact]
    public void Aggregate_CalculatesHitRate()
    {
        var result = Aggregate(
            Action("Xitra", "通常攻撃", ActionType.NormalAttack, HitStatus.Hit, [100]),
            Action("Xitra", "通常攻撃", ActionType.NormalAttack, HitStatus.Miss, []));

        var action = Assert.Single(result.ActionSummaries);
        Assert.Equal(0.5, action.HitRate);
    }

    [Fact]
    public void Aggregate_ExcludesUnknownFromHitRateDenominator()
    {
        var result = Aggregate(
            Action("Xitra", "通常攻撃", ActionType.NormalAttack, HitStatus.Hit, [100]),
            Action("Xitra", "通常攻撃", ActionType.NormalAttack, HitStatus.Unknown, []));

        var action = Assert.Single(result.ActionSummaries);
        Assert.Equal(1.0, action.HitRate);
        Assert.Equal(1, action.UnknownCount);
    }

    [Fact]
    public void Aggregate_CalculatesNormalAttackCriticalRate()
    {
        var result = Aggregate(
            Action("Xitra", "通常攻撃", ActionType.NormalAttackCritical, HitStatus.Hit, [200]),
            Action("Xitra", "通常攻撃", ActionType.NormalAttack, HitStatus.Hit, [100]),
            Action("Xitra", "通常攻撃", ActionType.NormalAttack, HitStatus.Miss, []));

        var actor = Assert.Single(result.ActorSummaries);
        Assert.Equal(2, actor.NormalAttackSummary.HitCount);
        Assert.Equal(1, actor.NormalAttackSummary.MissCount);
        Assert.Equal(0.5, actor.NormalAttackCriticalRate);
        Assert.Equal(2.0 / 3, actor.NormalAttackHitRate);
    }

    [Fact]
    public void Aggregate_ExcludedActionsDoNotCountAsUseOrHitRateDenominator()
    {
        var result = Aggregate(
            Action("Xitra", "ファイア", ActionType.Magic, HitStatus.Excluded, []));

        var action = Assert.Single(result.ActionSummaries);
        var actor = Assert.Single(result.ActorSummaries);
        Assert.Equal(0, action.UseCount);
        Assert.Null(action.HitRate);
        Assert.Equal(0, actor.TotalUseCount);
    }

    private static AnalysisResult Aggregate(params ParsedActionGroup[] actions)
    {
        return AggregateWithTime(new AnalysisTimeResult(TimeConfidence.Exact, 100, null, null, []), actions);
    }

    private static AnalysisResult AggregateWithTime(
        AnalysisTimeResult analysisTime,
        params ParsedActionGroup[] actions)
    {
        return new AnalysisAggregator().Aggregate(actions, analysisTime);
    }

    private static ParsedActionGroup Action(
        string actor,
        string actionName,
        ActionType actionType,
        HitStatus hitStatus,
        IReadOnlyList<int> damageValues)
    {
        var damage = ParsedDamageResult.FromDamages(damageValues);
        var group = TestActionGroupFactory.Create($"{actor}:{actionName}:{Guid.NewGuid():N}");
        var parsedAction = new ParsedAction(actor, actionName, actionType, damage, hitStatus);
        return new ParsedActionGroup(
            group,
            actor,
            actionName,
            actionType,
            damage,
            hitStatus,
            parsedAction);
    }
}
