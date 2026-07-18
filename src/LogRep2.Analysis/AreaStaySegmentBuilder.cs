using LogRep2.Contracts;

namespace FFXI_LogAnalyzer.Core;

public sealed class AreaStaySegmentBuilder
{
    public IReadOnlyList<AreaStaySegment> Build(IEnumerable<ICanonicalRecord> records)
    {
        ArgumentNullException.ThrowIfNull(records);

        var orderedRecords = records
            .Where(record => record.Order is not null)
            .OrderBy(record => record.Order)
            .ToArray();
        var changes = new AreaChangeExtractor().Extract(orderedRecords);
        var result = new List<AreaStaySegment>(changes.Count);

        for (var index = 0; index < changes.Count; index++)
        {
            var current = changes[index];
            var next = index + 1 < changes.Count ? changes[index + 1] : null;
            var segmentRecords = orderedRecords
                .Where(record => record.Order > current.Order)
                .Where(record => next is null || record.Order < next.Order)
                .Where(record => !record.IsMarker)
                .Where(record => !AreaChangeExtractor.IsAreaChange(record.VisibleText))
                .ToArray();
            var firstTimedRecord = segmentRecords.FirstOrDefault(record =>
                !string.IsNullOrWhiteSpace(record.MessageTimeText));

            result.Add(new AreaStaySegment(
                current.Sequence,
                current.AreaName,
                current.AreaOccurrence,
                current,
                next,
                segmentRecords.Length,
                firstTimedRecord?.MessageTimeText));
        }

        return result;
    }
}

public sealed record AreaStaySegment(
    int Sequence,
    string AreaName,
    int AreaOccurrence,
    AreaChangeRecord Start,
    AreaChangeRecord? End,
    int RecordCount,
    string? FirstMessageTimeText)
{
    public AnalysisRangeSelection CreateSelection()
    {
        var start = AnalysisEndpoint.FromMarker(CreateBoundary(Start));
        var end = End is null
            ? AnalysisEndpoint.LogEnd
            : AnalysisEndpoint.FromMarker(CreateBoundary(End));
        return new AnalysisRangeSelection(start, end);
    }

    private static MarkerRecord CreateBoundary(AreaChangeRecord change) =>
        new(
            change.Order,
            $"area:{change.AreaName}",
            change.SourceRecord.VisibleText,
            change.SourceRecord.MessageTimeText,
            change.SourceRecord.FirstSeenAt,
            change.SourceRecord);
}
