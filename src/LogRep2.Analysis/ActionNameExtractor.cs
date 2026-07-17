using System.Text.RegularExpressions;

namespace FFXI_LogAnalyzer.Core;

public sealed partial class ActionNameExtractor : IActionNameExtractor
{
    private readonly MagicLogClassifier _magicLogClassifier = new();
    private readonly NormalAttackParser _normalAttackParser;

    public ActionNameExtractor()
        : this(new NormalAttackParser())
    {
    }

    public ActionNameExtractor(NormalAttackParser normalAttackParser)
    {
        _normalAttackParser = normalAttackParser;
    }

    public string? ExtractActionName(ActionGroup group)
    {
        if (_normalAttackParser.TryParse(group, out var normalAttack))
        {
            return normalAttack.ActionName;
        }

        if (_magicLogClassifier.TryParseActivation(
                group,
                out _,
                out var magicActionName))
        {
            return magicActionName;
        }

        foreach (var text in group.VisibleTexts)
        {
            var actionName = ExtractActionName(text);
            if (!string.IsNullOrWhiteSpace(actionName))
            {
                return actionName;
            }
        }

        return null;
    }

    public ActionType ExtractActionType(ActionGroup group)
    {
        if (_normalAttackParser.TryParse(group, out var normalAttack))
        {
            return normalAttack.ActionType;
        }

        if (_magicLogClassifier.HasActivation(group))
        {
            return ActionType.Magic;
        }

        foreach (var text in group.VisibleTexts)
        {
            if (SkillRegex().IsMatch(text))
            {
                return ActionType.Skill;
            }
        }

        return ActionType.Unknown;
    }

    private static string? ExtractActionName(string text)
    {
        if (NormalAttackRegex().IsMatch(text))
        {
            return "通常攻撃";
        }

        var skill = SkillRegex().Match(text);
        return skill.Success ? skill.Groups["action"].Value : null;
    }

    [GeneratedRegex(@"^.+?の攻撃")]
    private static partial Regex NormalAttackRegex();

    [GeneratedRegex(@"^.+?は、(?<action>.+?)を実行")]
    private static partial Regex SkillRegex();
}
