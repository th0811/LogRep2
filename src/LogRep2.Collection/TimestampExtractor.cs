using System.Globalization;
using System.Text.RegularExpressions;

namespace FfxiTempLogCollector.Core;

public sealed partial class TimestampExtractor
{
    public ExtractedTimestamp? Extract(string visibleText)
    {
        ArgumentNullException.ThrowIfNull(visibleText);

        foreach (Match match in TimestampRegex().Matches(visibleText))
        {
            var hour = int.Parse(
                match.Groups["hour"].Value,
                CultureInfo.InvariantCulture);
            var minute = int.Parse(
                match.Groups["minute"].Value,
                CultureInfo.InvariantCulture);
            var secondGroup = match.Groups["second"];
            var second = secondGroup.Success
                ? int.Parse(
                    secondGroup.Value,
                    CultureInfo.InvariantCulture)
                : 0;

            if (hour > 23 || minute > 59 || second > 59)
            {
                continue;
            }

            return new ExtractedTimestamp
            {
                TimeText = match.Groups["time"].Value,
                Precision = secondGroup.Success ? "second" : "minute",
                Time = new TimeOnly(hour, minute, second),
            };
        }

        return null;
    }

    [GeneratedRegex(
        @"\[(?<time>(?<hour>\d{1,2}):(?<minute>\d{2})(?::(?<second>\d{2}))?)\]",
        RegexOptions.CultureInvariant)]
    private static partial Regex TimestampRegex();
}
