using System.Text.RegularExpressions;

namespace FFXI_LogAnalyzer.Core;

public sealed partial class ActorExtractor : IActorExtractor
{
    private readonly MagicLogClassifier _magicLogClassifier = new();
    private readonly NormalAttackParser _normalAttackParser;

    public ActorExtractor()
        : this(new NormalAttackParser())
    {
    }

    public ActorExtractor(NormalAttackParser normalAttackParser)
    {
        _normalAttackParser = normalAttackParser;
    }

    public string? ExtractActor(ActionGroup group)
    {
        if (_normalAttackParser.TryParse(group, out var normalAttack))
        {
            return normalAttack.Actor;
        }

        if (_magicLogClassifier.TryParseActivation(
                group,
                out var activationActor,
                out _))
        {
            return activationActor;
        }

        foreach (var text in group.VisibleTexts)
        {
            var actor = ExtractActor(text);
            if (!string.IsNullOrWhiteSpace(actor))
            {
                return actor;
            }
        }

        return null;
    }

    private static string? ExtractActor(string text)
    {
        var normalAttack = NormalAttackActorRegex().Match(text);
        if (normalAttack.Success)
        {
            return normalAttack.Groups["actor"].Value;
        }

        var castOrUse = CastOrUseActorRegex().Match(text);
        if (castOrUse.Success)
        {
            return castOrUse.Groups["actor"].Value;
        }

        var activated = ActivatedActorRegex().Match(text);
        return activated.Success ? activated.Groups["actor"].Value : null;
    }

    [GeneratedRegex(@"^(?<actor>.+?)の攻撃")]
    private static partial Regex NormalAttackActorRegex();

    [GeneratedRegex(@"^(?<actor>.+?)は、.+?(?:を実行|を唱えた)")]
    private static partial Regex CastOrUseActorRegex();

    [GeneratedRegex(@"^(?<actor>.+?)の.+?が発動")]
    private static partial Regex ActivatedActorRegex();
}
