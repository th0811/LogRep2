namespace FfxiTempLogCollector.Core;

public sealed class RawDeduplicator
{
    private readonly HashSet<string> _seenRawRecordIds;

    public RawDeduplicator(IEnumerable<string>? seenRawRecordIds = null)
    {
        _seenRawRecordIds = seenRawRecordIds is null
            ? []
            : new HashSet<string>(
                seenRawRecordIds,
                StringComparer.Ordinal);
    }

    public IReadOnlySet<string> SeenRawRecordIds => _seenRawRecordIds;

    public bool TryAdd(RawRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);

        return _seenRawRecordIds.Add(record.RawRecordId);
    }
}
