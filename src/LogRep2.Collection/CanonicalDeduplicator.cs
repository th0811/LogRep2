using System.Globalization;

namespace FfxiTempLogCollector.Core;

public sealed class CanonicalDeduplicator
{
    private readonly CanonicalRecordFactory _factory;
    private readonly Dictionary<string, CanonicalRecord> _records;
    private long _lastOrder;

    public CanonicalDeduplicator(
        CanonicalRecordFactory? factory = null,
        IEnumerable<CanonicalRecord>? existingRecords = null,
        long lastOrder = 0)
    {
        _factory = factory ?? new CanonicalRecordFactory();
        _records = new Dictionary<string, CanonicalRecord>(
            StringComparer.Ordinal);

        if (existingRecords is not null)
        {
            foreach (var record in existingRecords)
            {
                _records[record.CanonicalKey] = record;
                lastOrder = Math.Max(lastOrder, record.Order);
            }
        }

        _lastOrder = lastOrder;
    }

    public long LastOrder => _lastOrder;

    public IReadOnlyList<CanonicalRecord> Records =>
        _records.Values
            .OrderBy(record => record.Order)
            .ToArray();

    public CanonicalRecord AddOrMerge(RawRecord rawRecord)
    {
        ArgumentNullException.ThrowIfNull(rawRecord);

        var canonicalKey = _factory.CreateCanonicalKey(rawRecord);

        if (!_records.TryGetValue(canonicalKey, out var canonicalRecord))
        {
            canonicalRecord = _factory.Create(rawRecord, ++_lastOrder);
            _records.Add(canonicalKey, canonicalRecord);
            return canonicalRecord;
        }

        Merge(canonicalRecord, rawRecord);
        return canonicalRecord;
    }

    private static void Merge(
        CanonicalRecord canonicalRecord,
        RawRecord rawRecord)
    {
        AddUnique(canonicalRecord.SourceWindows, rawRecord.WindowId);
        AddUnique(canonicalRecord.SourceFiles, rawRecord.SourceFile);
        AddUnique(
            canonicalRecord.SourceRawRecordIds,
            rawRecord.RawRecordId);

        canonicalRecord.SourceWindows.Sort();
        canonicalRecord.SourceFiles.Sort(StringComparer.Ordinal);
        canonicalRecord.SourceRawRecordIds.Sort(StringComparer.Ordinal);
        canonicalRecord.LastSeenAt = Max(
            canonicalRecord.LastSeenAt,
            rawRecord.FirstSeenAt);
        canonicalRecord.SequenceHintMin = MinSequence(
            canonicalRecord.SequenceHintMin,
            rawRecord.SequenceHint);
        canonicalRecord.SequenceHintMax = MaxSequence(
            canonicalRecord.SequenceHintMax,
            rawRecord.SequenceHint);
    }

    private static void AddUnique<T>(ICollection<T> values, T value)
    {
        if (!values.Contains(value))
        {
            values.Add(value);
        }
    }

    private static DateTimeOffset Max(
        DateTimeOffset left,
        DateTimeOffset right)
    {
        return left >= right ? left : right;
    }

    private static string? MinSequence(string? left, string? right)
    {
        if (left is null)
        {
            return right;
        }

        if (right is null)
        {
            return left;
        }

        return CompareSequence(left, right) <= 0 ? left : right;
    }

    private static string? MaxSequence(string? left, string? right)
    {
        if (left is null)
        {
            return right;
        }

        if (right is null)
        {
            return left;
        }

        return CompareSequence(left, right) >= 0 ? left : right;
    }

    private static int CompareSequence(string left, string right)
    {
        if (long.TryParse(
                left,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var leftNumber)
            && long.TryParse(
                right,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var rightNumber))
        {
            return leftNumber.CompareTo(rightNumber);
        }

        return string.CompareOrdinal(left, right);
    }
}
