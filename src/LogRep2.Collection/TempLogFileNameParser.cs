using System.Globalization;
using System.Text.RegularExpressions;

namespace FfxiTempLogCollector.Core;

public sealed partial class TempLogFileNameParser
{
    public bool TryParse(
        string? fileName,
        out TempLogFileName? parsedFileName)
    {
        parsedFileName = null;

        if (string.IsNullOrEmpty(fileName))
        {
            return false;
        }

        var match = TargetFileNameRegex().Match(fileName);

        if (!match.Success)
        {
            return false;
        }

        parsedFileName = new TempLogFileName(
            fileName,
            int.Parse(
                match.Groups["window"].Value,
                CultureInfo.InvariantCulture),
            int.Parse(
                match.Groups["rotation"].Value,
                CultureInfo.InvariantCulture));

        return true;
    }

    [GeneratedRegex(
        @"^(?<window>[12])_(?<rotation>[0-9]|1[0-9])\.log$",
        RegexOptions.CultureInvariant)]
    private static partial Regex TargetFileNameRegex();
}
