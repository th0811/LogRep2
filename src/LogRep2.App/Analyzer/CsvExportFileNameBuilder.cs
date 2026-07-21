using System.IO;
using FFXI_LogAnalyzer.Core;

namespace FFXI_LogAnalyzer.App;

internal static class CsvExportFileNameBuilder
{
    private const int MaxComponentLength = 40;

    public static string Build(
        string outputType,
        AnalysisTimeResult analysisTime,
        DateTimeOffset? fallbackSessionTime,
        string rangeName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputType);
        ArgumentNullException.ThrowIfNull(analysisTime);

        var components = new List<string>
        {
            "LogRep2",
            SanitizeComponent(outputType, "分析結果"),
        };
        var timeRange = BuildTimeRange(analysisTime, fallbackSessionTime);
        if (timeRange is not null)
        {
            components.Add(timeRange);
        }

        components.Add(SanitizeComponent(rangeName, "指定範囲"));
        return $"{string.Join('_', components)}.csv";
    }

    internal static string SanitizeComponent(string? value, string fallback)
    {
        var invalidCharacters = Path.GetInvalidFileNameChars()
            .Concat(['<', '>', ':', '"', '/', '\\', '|', '?', '*'])
            .ToHashSet();
        var sanitized = new string((value ?? string.Empty)
            .Trim()
            .Select(character =>
                character < ' ' || invalidCharacters.Contains(character)
                    ? '_'
                    : character)
            .ToArray())
            .Trim(' ', '.');
        if (string.IsNullOrWhiteSpace(sanitized))
        {
            sanitized = fallback;
        }

        return sanitized.Length <= MaxComponentLength
            ? sanitized
            : sanitized[..MaxComponentLength].TrimEnd(' ', '.');
    }

    private static string? BuildTimeRange(
        AnalysisTimeResult analysisTime,
        DateTimeOffset? fallbackSessionTime)
    {
        var start = analysisTime.StartTime;
        var end = analysisTime.EndTime;
        if (start is not null && end is not null)
        {
            return start.Value.Date == end.Value.Date
                ? $"{start:yyyyMMdd}_{start:HHmmss}-{end:HHmmss}"
                : $"{start:yyyyMMdd-HHmmss}_{end:yyyyMMdd-HHmmss}";
        }

        var singleTime = start ?? end;
        if (singleTime is not null)
        {
            return singleTime.Value.ToString("yyyyMMdd_HHmmss");
        }

        return fallbackSessionTime?.ToString("yyyyMMdd");
    }
}
