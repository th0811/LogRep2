using System.Text;
using System.Text.RegularExpressions;

namespace FfxiTempLogCollector.Core;

public sealed partial class TextNormalizer
{
    private static readonly HashSet<byte> ControlCodePrefixes =
    [
        0x1E,
        0x1F,
        0x7F,
        0xEF,
    ];

    public string Normalize(string decodedText)
    {
        ArgumentNullException.ThrowIfNull(decodedText);

        var withoutNull = decodedText.Replace("\0", string.Empty);
        return ConsecutiveWhitespaceRegex()
            .Replace(withoutNull, " ")
            .Trim();
    }

    public string Normalize(
        ReadOnlySpan<byte> rawMessageBytes,
        Encoding encoding)
    {
        ArgumentNullException.ThrowIfNull(encoding);

        var visibleBytes = RemoveControlCodesAndNulls(rawMessageBytes);
        return Normalize(encoding.GetString(visibleBytes));
    }

    private static byte[] RemoveControlCodesAndNulls(
        ReadOnlySpan<byte> rawMessageBytes)
    {
        var visibleBytes = new List<byte>(rawMessageBytes.Length);

        for (var index = 0; index < rawMessageBytes.Length; index++)
        {
            var currentByte = rawMessageBytes[index];

            if (currentByte == 0)
            {
                continue;
            }

            if (ControlCodePrefixes.Contains(currentByte))
            {
                if (index + 1 < rawMessageBytes.Length)
                {
                    index++;
                }

                continue;
            }

            visibleBytes.Add(currentByte);
        }

        return [.. visibleBytes];
    }

    [GeneratedRegex(@"[\s　]+", RegexOptions.CultureInvariant)]
    private static partial Regex ConsecutiveWhitespaceRegex();
}
