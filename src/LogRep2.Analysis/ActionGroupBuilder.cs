using LogRep2.Contracts;

namespace FFXI_LogAnalyzer.Core;

public sealed class ActionGroupBuilder
{
    private readonly SequenceHintComparer _sequenceHintComparer;

    public ActionGroupBuilder()
        : this(new SequenceHintComparer())
    {
    }

    public ActionGroupBuilder(SequenceHintComparer sequenceHintComparer)
    {
        _sequenceHintComparer = sequenceHintComparer;
    }

    public IReadOnlyList<ActionGroup> Build(IEnumerable<ICanonicalRecord> records)
    {
        ArgumentNullException.ThrowIfNull(records);

        return records
            .Select((record, index) => new ActionGroupRecord(record, index))
            .Where(record => TryCreateKey(record.Record, out _))
            .GroupBy(record =>
            {
                TryCreateKey(record.Record, out var key);
                return key!;
            })
            .Select(group =>
            {
                var sortedRecords = group
                    .Order(_sequenceHintComparer)
                    .ToArray();
                return new ActionGroup(group.Key, sortedRecords);
            })
            .OrderBy(group => group.OrderMin ?? long.MaxValue)
            .ThenBy(group => group.ActionGroupKey, StringComparer.Ordinal)
            .ToArray();
    }

    private static bool TryCreateKey(ICanonicalRecord record, out ActionGroupKey? key)
    {
        key = null;
        if (string.IsNullOrWhiteSpace(record.SessionId) ||
            string.IsNullOrWhiteSpace(record.EventGroup))
        {
            return false;
        }

        key = new ActionGroupKey(record.SessionId, record.EventGroup);
        return true;
    }
}
