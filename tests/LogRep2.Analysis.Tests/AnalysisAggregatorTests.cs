using FFXI_LogAnalyzer.Core;

namespace FFXI_LogAnalyzer.Tests;

public class AnalysisAggregatorTests
{
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
    public void Aggregate_NormalAttackAbsorptionCountsAsHitWithoutDamage()
    {
        var result = Aggregate(
            Action("Leshonn", "通常攻撃", ActionType.NormalAttack, HitStatus.Hit, []));

        var action = Assert.Single(result.ActionSummaries);
        var actor = Assert.Single(result.ActorSummaries);
        Assert.Equal(1, action.HitCount);
        Assert.Equal(0, action.Damage.TotalDamage);
        Assert.Empty(action.Damage.DamageValues);
        Assert.Null(action.Damage.MaxDamage);
        Assert.Null(action.Damage.MinDamage);
        Assert.Null(action.Damage.AverageDamage);
        Assert.Equal(1, actor.NormalAttackSummary.HitCount);
        Assert.Equal(0, actor.TotalDamage);
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
        Assert.Null(actor.NormalAttackCriticalRate);
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
