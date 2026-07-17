namespace FFXI_LogAnalyzer.Core;

public sealed class HitStatusClassifier : IHitStatusClassifier
{
    private readonly MagicLogClassifier _magicLogClassifier = new();

    private static readonly string[] ExcludedKeywords =
    [
        "詠唱中断",
        "発動失敗",
        "使用失敗"
    ];

    private static readonly string[] MissKeywords =
    [
        "ミス",
        "回避",
        "かわした",
        "効果なし",
        "効果がなかった",
        "レジストされた"
    ];

    public HitStatus Classify(ActionGroup group, ParsedDamageResult damage)
    {
        if (ContainsAnyKeyword(group, ExcludedKeywords))
        {
            return HitStatus.Excluded;
        }

        if (damage.HasDamage)
        {
            return HitStatus.Hit;
        }

        if (_magicLogClassifier.HasSuccessfulNonDamageEffect(group))
        {
            return HitStatus.Hit;
        }

        return ContainsAnyKeyword(group, MissKeywords)
            ? HitStatus.Miss
            : HitStatus.Unknown;
    }

    private static bool ContainsAnyKeyword(ActionGroup group, IEnumerable<string> keywords)
    {
        return group.VisibleTexts.Any(text =>
            keywords.Any(keyword => text.Contains(keyword, StringComparison.Ordinal)));
    }
}
