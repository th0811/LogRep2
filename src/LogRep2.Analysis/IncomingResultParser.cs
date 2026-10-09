using System.Text.RegularExpressions;

namespace FFXI_LogAnalyzer.Core;

public sealed record IncomingResult(string Target, int? Damage, HitStatus Status);

public static partial class IncomingResultParser
{
    public static IEnumerable<IncomingResult> Parse(ActionGroup group, ActionType type)
    {
        var physical = type is ActionType.NormalAttack or ActionType.NormalAttackCritical or ActionType.WeaponSkill or ActionType.Ability;
        var chain = false;
        foreach (var text in group.VisibleTexts)
        {
            if (text.Contains("連携", StringComparison.Ordinal)) chain = true;
            var damage = DamageRegex().Match(text);
            if (damage.Success && int.TryParse(damage.Groups["damage"].Value, out var value))
            {
                yield return new IncomingResult(damage.Groups["target"].Value.Trim(), value,
                    physical && !chain ? HitStatus.Hit : HitStatus.Unknown);
                continue;
            }
            // 効果なし・レジスト・分身による無効化は回避として数えません。
            var evade = EvadeRegex().Match(text);
            if (!evade.Success) evade = TargetMissRegex().Match(text);
            if (evade.Success)
                yield return new IncomingResult(evade.Groups["target"].Value.Trim(), null,
                    physical && !chain ? HitStatus.Miss : HitStatus.Unknown);
        }
    }

    [GeneratedRegex(@"(?:^|。|→)\s*→?(?<target>[^→、。]+?)(?:に、|は、)(?<damage>\d+)(?:ダメージ|HP吸収)[。！!]?$")]
    private static partial Regex DamageRegex();

    [GeneratedRegex(@"(?:^|。|→)\s*(?:ミス[！!]\s*)?→?(?<target>[^→、。！!]+?)は、?(?:攻撃を)?(?:回避した|かわした)[。！!]?$")]
    private static partial Regex EvadeRegex();

    [GeneratedRegex(@"(?:^|。|→)\s*→?(?<target>[^→、。]+?)に、ミス[。！!]?$")]
    private static partial Regex TargetMissRegex();
}
