using System.Collections.Concurrent;
using System.Text.RegularExpressions;

namespace FfxiTempLogCollector.Core;

public sealed class MarkerDetector
{
    private static readonly ConcurrentDictionary<string, Regex> RegexCache =
        new(StringComparer.Ordinal);

    public DetectedMarker? Detect(string visibleText)
    {
        return Detect(visibleText, CollectorConfig.DefaultMarkerPrefix);
    }

    public DetectedMarker? Detect(string visibleText, string markerPrefix)
    {
        ArgumentNullException.ThrowIfNull(visibleText);

        if (string.IsNullOrWhiteSpace(markerPrefix))
        {
            return null;
        }

        var match = CreateMarkerRegex(markerPrefix).Match(visibleText);

        if (!match.Success)
        {
            return null;
        }

        return new DetectedMarker
        {
            Keyword = match.Groups["keyword"].Value,
        };
    }

    private static Regex CreateMarkerRegex(string markerPrefix)
    {
        return RegexCache.GetOrAdd(
            markerPrefix,
            static prefix => new Regex(
                Regex.Escape(prefix)
                + @"[\s\u3000]*"
                + @"(?<keyword>[A-Za-z0-9_\-:.]+|[^\s\u3000]+)",
                RegexOptions.CultureInvariant));
    }
}
