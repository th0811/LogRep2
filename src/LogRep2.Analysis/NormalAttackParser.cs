using System.Text.RegularExpressions;

namespace FFXI_LogAnalyzer.Core;

public sealed partial class NormalAttackParser
{
    private readonly CriticalDetector _criticalDetector;

    public NormalAttackParser()
        : this(new CriticalDetector())
    {
    }

    public NormalAttackParser(CriticalDetector criticalDetector)
    {
        _criticalDetector = criticalDetector;
    }

    public bool IsNormalAttack(ActionGroup group)
    {
        return TryParse(group, out _);
    }

    public bool TryParse(ActionGroup group, out NormalAttackParseResult result)
    {
        result = null!;

        foreach (var text in group.VisibleTexts)
        {
            var match = NormalAttackRegex().Match(text);
            if (!match.Success)
            {
                continue;
            }

            var actionType = _criticalDetector.IsCritical(group)
                ? ActionType.NormalAttackCritical
                : ActionType.NormalAttack;
            result = new NormalAttackParseResult(
                match.Groups["actor"].Value,
                "通常攻撃",
                actionType);
            return true;
        }

        return false;
    }

    [GeneratedRegex(@"^(?<actor>.+?)の攻撃")]
    private static partial Regex NormalAttackRegex();
}
