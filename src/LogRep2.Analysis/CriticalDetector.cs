namespace FFXI_LogAnalyzer.Core;

public sealed class CriticalDetector
{
    private static readonly string[] CriticalKeywords =
    [
        "クリティカル"
    ];

    public bool IsCritical(ActionGroup group)
    {
        return group.VisibleTexts.Any(IsCriticalText);
    }

    public bool IsCriticalText(string? text)
    {
        return !string.IsNullOrWhiteSpace(text) &&
            CriticalKeywords.Any(keyword => text.Contains(keyword, StringComparison.Ordinal));
    }
}
