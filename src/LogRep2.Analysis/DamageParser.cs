using System.Globalization;
using System.Text.RegularExpressions;

namespace FFXI_LogAnalyzer.Core;

public sealed partial class DamageParser : IDamageParser
{
    public ParsedDamageResult ParseDamage(ActionGroup group)
    {
        var damages = new List<int>();

        foreach (var text in group.VisibleTexts)
        {
            foreach (Match match in DamageRegex().Matches(text))
            {
                damages.Add(int.Parse(match.Groups["damage"].Value, CultureInfo.InvariantCulture));
            }
        }

        return ParsedDamageResult.FromDamages(damages);
    }

    [GeneratedRegex(@"(?:に、|は、)?(?<damage>\d+)(?:ダメージ|HP吸収)")]
    private static partial Regex DamageRegex();
}
