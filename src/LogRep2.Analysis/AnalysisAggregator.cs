namespace FFXI_LogAnalyzer.Core;

public sealed class AnalysisAggregator
{
    public AnalysisResult Aggregate(
        IEnumerable<ParsedActionGroup> parsedActionGroups,
        AnalysisTimeResult analysisTime,
        IEnumerable<UnparsedActionGroup>? unparsedActionGroups = null)
    {
        ArgumentNullException.ThrowIfNull(parsedActionGroups);
        ArgumentNullException.ThrowIfNull(analysisTime);

        var parsedActions = parsedActionGroups.ToArray();
        var unparsed = unparsedActionGroups?.ToArray() ?? [];
        var unknownActions = parsedActions
            .Where(action => action.ActionType == ActionType.Unknown || action.HitStatus == HitStatus.Unknown)
            .ToArray();
        var actionSummaries = parsedActions
            .GroupBy(action => new ActionSummaryKey(action.Actor, action.ActionName, action.ActionType))
            .Select(group => BuildActionSummary(group.Key, group))
            .OrderBy(summary => summary.Actor, StringComparer.Ordinal)
            .ThenBy(summary => summary.ActionName, StringComparer.Ordinal)
            .ThenBy(summary => summary.ActionType)
            .ToArray();
        var actorSummaries = parsedActions
            .GroupBy(action => action.Actor)
            .Select(group => BuildActorSummary(group.Key, group, actionSummaries, analysisTime))
            .OrderBy(summary => summary.Actor, StringComparer.Ordinal)
            .ToArray();

        return new AnalysisResult(actorSummaries, actionSummaries, unparsed, analysisTime)
        {
            UnknownActionGroups = unknownActions,
        };
    }

    private static ActorSummary BuildActorSummary(
        string actor,
        IEnumerable<ParsedActionGroup> actions,
        IReadOnlyList<ActionSummary> allActionSummaries,
        AnalysisTimeResult analysisTime)
    {
        var actionArray = actions.ToArray();
        var damage = DamageStatistics.FromParsedActions(actionArray);
        var totalHitCount = CountByStatus(actionArray, HitStatus.Hit);
        var totalMissCount = CountByStatus(actionArray, HitStatus.Miss);
        var normalAttackSummary = BuildNormalAttackSummary(actionArray);
        var actorActionSummaries = allActionSummaries
            .Where(summary => summary.Actor == actor)
            .ToArray();

        return new ActorSummary(
            actor,
            damage.TotalDamage,
            RateCalculator.CalculateDps(damage.TotalDamage, analysisTime),
            analysisTime.Confidence,
            normalAttackSummary.HitRate,
            normalAttackSummary.CriticalRate,
            actionArray.Count(IsUseCountTarget),
            totalHitCount,
            totalMissCount,
            CountByStatus(actionArray, HitStatus.Unknown),
            normalAttackSummary,
            actorActionSummaries);
    }

    private static ActionSummary BuildActionSummary(
        ActionSummaryKey key,
        IEnumerable<ParsedActionGroup> actions)
    {
        var actionArray = actions.ToArray();
        var hitCount = CountByStatus(actionArray, HitStatus.Hit);
        var missCount = CountByStatus(actionArray, HitStatus.Miss);

        return new ActionSummary(
            key.Actor,
            key.ActionName,
            key.ActionType,
            actionArray.Count(IsUseCountTarget),
            hitCount,
            missCount,
            CountByStatus(actionArray, HitStatus.Unknown),
            RateCalculator.CalculateHitRate(hitCount, missCount),
            DamageStatistics.FromParsedActions(actionArray));
    }

    private static NormalAttackSummary BuildNormalAttackSummary(IReadOnlyList<ParsedActionGroup> actions)
    {
        var normalAttacks = actions
            .Where(action => action.ActionType is ActionType.NormalAttack or ActionType.NormalAttackCritical)
            .ToArray();
        var hitCount = normalAttacks.Count(action => action.HitStatus == HitStatus.Hit);
        var missCount = normalAttacks.Count(action => action.HitStatus == HitStatus.Miss);
        var criticalHitCount = normalAttacks.Count(action =>
            action.ActionType == ActionType.NormalAttackCritical &&
            action.HitStatus == HitStatus.Hit);

        return new NormalAttackSummary(
            hitCount,
            missCount,
            criticalHitCount,
            RateCalculator.CalculateHitRate(hitCount, missCount),
            RateCalculator.CalculateCriticalRate(criticalHitCount, hitCount));
    }

    private static int CountByStatus(IEnumerable<ParsedActionGroup> actions, HitStatus hitStatus)
    {
        return actions.Count(action => action.HitStatus == hitStatus);
    }

    private static bool IsUseCountTarget(ParsedActionGroup action)
    {
        return action.HitStatus != HitStatus.Excluded;
    }

    private sealed record ActionSummaryKey(
        string Actor,
        string ActionName,
        ActionType ActionType);
}
