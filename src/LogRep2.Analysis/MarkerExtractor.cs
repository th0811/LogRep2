using LogRep2.Contracts;

namespace FFXI_LogAnalyzer.Core;

public sealed class MarkerExtractor
{
    public IReadOnlyList<MarkerRecord> Extract(IEnumerable<ICanonicalRecord> records)
    {
        ArgumentNullException.ThrowIfNull(records);

        return records
            .Select((record, index) => new { Record = record, Index = index })
            .Where(item => item.Record.IsMarker)
            .OrderBy(item => item.Record.Order ?? long.MaxValue)
            .ThenBy(item => item.Index)
            .Select(item => new MarkerRecord(
                item.Record.Order,
                item.Record.MarkerKeyword,
                item.Record.VisibleText,
                item.Record.MessageTimeText,
                item.Record.FirstSeenAt,
                item.Record))
            .ToArray();
    }
}
