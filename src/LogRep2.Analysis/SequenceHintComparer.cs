namespace FFXI_LogAnalyzer.Core;

public sealed class SequenceHintComparer : IComparer<ActionGroupRecord>
{
    public int Compare(ActionGroupRecord? x, ActionGroupRecord? y)
    {
        if (ReferenceEquals(x, y))
        {
            return 0;
        }

        if (x is null)
        {
            return -1;
        }

        if (y is null)
        {
            return 1;
        }

        var xSortKey = GetSortKey(x);
        var ySortKey = GetSortKey(y);

        var sortValueComparison = xSortKey.Value.CompareTo(ySortKey.Value);
        if (sortValueComparison != 0)
        {
            return sortValueComparison;
        }

        var sourceComparison = xSortKey.Source.CompareTo(ySortKey.Source);
        if (sourceComparison != 0)
        {
            return sourceComparison;
        }

        return x.ReadIndex.CompareTo(y.ReadIndex);
    }

    private static SortKey GetSortKey(ActionGroupRecord record)
    {
        if (record.EffectiveSequenceHint is { } sequenceHint)
        {
            return new SortKey(sequenceHint, SortKeySource.SequenceHint);
        }

        if (record.Order is { } order)
        {
            return new SortKey(order, SortKeySource.Order);
        }

        return new SortKey(long.MaxValue, SortKeySource.ReadIndex);
    }

    private readonly record struct SortKey(long Value, SortKeySource Source);

    private enum SortKeySource
    {
        SequenceHint,
        Order,
        ReadIndex
    }
}
