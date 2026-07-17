using System.Text.RegularExpressions;

namespace FFXI_LogAnalyzer.Core;

public sealed partial class MagicLogClassifier
{
    public bool TryParseCastStart(
        ActionGroup group,
        out string actor,
        out string actionName)
    {
        foreach (var text in group.VisibleTexts)
        {
            if (TryParseCastStart(text, out actor, out actionName))
            {
                return true;
            }
        }

        actor = string.Empty;
        actionName = string.Empty;
        return false;
    }

    public bool TryParseActivation(
        ActionGroup group,
        out string actor,
        out string actionName)
    {
        foreach (var text in group.VisibleTexts)
        {
            var match = ActivationRegex().Match(text);
            if (match.Success)
            {
                actor = match.Groups["actor"].Value;
                actionName = match.Groups["action"].Value;
                return true;
            }
        }

        actor = string.Empty;
        actionName = string.Empty;
        return false;
    }

    public bool HasActivation(ActionGroup group)
    {
        return group.VisibleTexts.Any(text => ActivationRegex().IsMatch(text));
    }

    public bool HasSuccessfulNonDamageEffect(ActionGroup group)
    {
        return group.VisibleTexts.Any(
            text => EffectRegex().IsMatch(text)
                || StatusRegex().IsMatch(text));
    }

    private static bool TryParseCastStart(
        string text,
        out string actor,
        out string actionName)
    {
        var withTarget = CastStartWithTargetRegex().Match(text);
        if (withTarget.Success)
        {
            actor = withTarget.Groups["actor"].Value;
            actionName = withTarget.Groups["action"].Value;
            return true;
        }

        var withoutTarget = CastStartRegex().Match(text);
        if (withoutTarget.Success)
        {
            actor = withoutTarget.Groups["actor"].Value;
            actionName = withoutTarget.Groups["action"].Value;
            return true;
        }

        actor = string.Empty;
        actionName = string.Empty;
        return false;
    }

    [GeneratedRegex(@"^(?<actor>.+?)は、.+?に(?<action>.+?)を唱えた。?$")]
    private static partial Regex CastStartWithTargetRegex();

    [GeneratedRegex(@"^(?<actor>.+?)は、(?<action>.+?)を唱えた。?$")]
    private static partial Regex CastStartRegex();

    [GeneratedRegex(@"^(?<actor>.+?)の(?<action>.+?)が発動。?$")]
    private static partial Regex ActivationRegex();

    [GeneratedRegex(@"^→?.+?は、.+?の効果。?$")]
    private static partial Regex EffectRegex();

    [GeneratedRegex(@"^→?.+?は、.+?の状態になった！?$")]
    private static partial Regex StatusRegex();
}
