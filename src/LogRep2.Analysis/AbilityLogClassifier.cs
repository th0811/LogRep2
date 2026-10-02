using System.Text.RegularExpressions;

namespace FFXI_LogAnalyzer.Core;

public static partial class AbilityLogClassifier
{
    public static bool TryParse(string text, out string actor, out string actionName)
    {
        // ロールの合計値は行動名に含めません。
        var match = RollDeclarationRegex().Match(text);
        if (!match.Success)
        {
            match = DoubleUpDeclarationRegex().Match(text);
        }
        if (!match.Success)
        {
            match = DeclarationRegex().Match(text);
        }
        actor = match.Groups["actor"].Value;
        actionName = match.Groups["action"].Value;
        // 成長通知はアビリティの宣言と同じ文型ですが、行動として扱いません。
        return match.Success && actionName != "ジョブポイントがアップ";
    }

    [GeneratedRegex(@"^(?<actor>[A-Za-z][A-Za-z0-9 '._-]*)の(?<action>[^、。！!\r\n]+)[！!]$")]
    private static partial Regex DeclarationRegex();

    [GeneratedRegex(@"^(?<actor>[A-Za-z][A-Za-z0-9 '._-]*)の(?<action>[^→、。！!\r\n]+ロール)→合計値が(?<total>[0-9]+)になった[！!]$")]
    private static partial Regex RollDeclarationRegex();

    // ダブルアップだけは、宣言行の末尾に感嘆符がありません。
    [GeneratedRegex(@"^(?<actor>[A-Za-z][A-Za-z0-9 '._-]*)の(?<action>ダブルアップ)$")]
    private static partial Regex DoubleUpDeclarationRegex();

    public static bool TryParseRollTotal(string text, string actor, string actionName, out int total)
    {
        total = 0;
        var match = RollDeclarationRegex().Match(text);
        if (match.Success)
            return match.Groups["actor"].Value == actor && match.Groups["action"].Value == actionName
                && int.TryParse(match.Groups["total"].Value, out total);
        match = DoubleUpResultRegex().Match(text);
        return actionName == "ダブルアップ" && match.Success && int.TryParse(match.Groups["total"].Value, out total);
    }

    [GeneratedRegex(@"^→[^→、。！!\r\n]+ロールの合計値が(?<total>[0-9]+)になった[！!]$")]
    private static partial Regex DoubleUpResultRegex();
}
