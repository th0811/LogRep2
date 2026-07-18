using System.Text.RegularExpressions;
using LogRep2.Contracts;

namespace FFXI_LogAnalyzer.Core;

public sealed partial class AreaChangeExtractor
{
    public IReadOnlyList<AreaChangeRecord> Extract(IEnumerable<ICanonicalRecord> records)
    {
        ArgumentNullException.ThrowIfNull(records);

        var occurrences = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var result = new List<AreaChangeRecord>();
        foreach (var record in records
                     .Where(record => record.Order is not null)
                     .OrderBy(record => record.Order))
        {
            if (!TryExtractAreaName(record.VisibleText, out var areaName))
            {
                continue;
            }

            occurrences.TryGetValue(areaName, out var occurrence);
            occurrence++;
            occurrences[areaName] = occurrence;
            result.Add(new AreaChangeRecord(
                result.Count + 1,
                areaName,
                occurrence,
                record.Order!.Value,
                record));
        }

        return result;
    }

    public static bool IsAreaChange(string? visibleText) =>
        TryExtractAreaName(visibleText, out _);

    private static bool TryExtractAreaName(string? visibleText, out string areaName)
    {
        var match = AreaChangePattern().Match(visibleText?.Trim() ?? string.Empty);
        areaName = match.Success ? match.Groups["area"].Value.Trim() : string.Empty;
        return areaName.Length > 0;
    }

    [GeneratedRegex(@"^===\s*(?<area>.+?)\s*===$", RegexOptions.CultureInvariant)]
    private static partial Regex AreaChangePattern();
}

public sealed record AreaChangeRecord(
    int Sequence,
    string AreaName,
    int AreaOccurrence,
    long Order,
    ICanonicalRecord SourceRecord);
