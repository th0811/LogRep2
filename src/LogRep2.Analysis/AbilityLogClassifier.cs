using System.Text.RegularExpressions;

namespace FFXI_LogAnalyzer.Core;

public static partial class AbilityLogClassifier
{
    public static bool TryParse(string text, out string actor, out string actionName)
    {
        // ロールの合計値は行動名に含めず、元ログにのみ保持します。
        var match = RollDeclarationRegex().Match(text);
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

    [GeneratedRegex(@"^(?<actor>[A-Za-z][A-Za-z0-9 '._-]*)の(?<action>[^→、。！!\r\n]+ロール)→合計値が[0-9]+になった[！!]$")]
    private static partial Regex RollDeclarationRegex();
}
