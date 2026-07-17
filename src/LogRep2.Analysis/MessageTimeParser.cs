using System.Globalization;
using System.Text.RegularExpressions;

namespace FFXI_LogAnalyzer.Core;

public sealed partial class MessageTimeParser
{
    public bool TryParse(string? text, out MessageTime messageTime)
    {
        messageTime = null!;

        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var match = MessageTimeRegex().Match(text.Trim());
        if (!match.Success)
        {
            return false;
        }

        var hour = int.Parse(match.Groups["hour"].Value, CultureInfo.InvariantCulture);
        var minute = int.Parse(match.Groups["minute"].Value, CultureInfo.InvariantCulture);
        var hasSecond = match.Groups["second"].Success;
        var second = hasSecond
            ? int.Parse(match.Groups["second"].Value, CultureInfo.InvariantCulture)
            : 0;

        if (hour > 23 || minute > 59 || second > 59)
        {
            return false;
        }

        messageTime = new MessageTime(
            hour,
            minute,
            second,
            hasSecond ? MessageTimePrecision.Second : MessageTimePrecision.Minute);
        return true;
    }

    [GeneratedRegex(@"^\[(?<hour>\d{2}):(?<minute>\d{2})(?::(?<second>\d{2}))?\]$")]
    private static partial Regex MessageTimeRegex();
}
