using LogRep2.Contracts;

namespace FFXI_LogAnalyzer.Core;

public sealed class AnalysisRangeBuilder
{
    private readonly AnalysisRangeValidator _validator;

    public AnalysisRangeBuilder()
        : this(new AnalysisRangeValidator())
    {
    }

    public AnalysisRangeBuilder(AnalysisRangeValidator validator)
    {
        _validator = validator;
    }

    public IReadOnlyList<ICanonicalRecord> Build(
        IEnumerable<ICanonicalRecord> records,
        AnalysisRangeSelection selection)
    {
        ArgumentNullException.ThrowIfNull(records);
        ArgumentNullException.ThrowIfNull(selection);

        var validationErrors = _validator.Validate(selection);
        if (validationErrors.Count > 0)
        {
            throw new ArgumentException($"分析区間が不正です: {string.Join(" ", validationErrors)}", nameof(selection));
        }

        var startExclusiveOrder = selection.Start.Type == AnalysisEndpointType.Marker
            ? selection.Start.Marker?.Order
            : null;
        var endExclusiveOrder = selection.End.Type == AnalysisEndpointType.Marker
            ? selection.End.Marker?.Order
            : null;

        return records
            .Where(record => IsInRange(record, startExclusiveOrder, endExclusiveOrder))
            .Where(record => !record.IsMarker)
            .OrderBy(record => record.Order ?? long.MaxValue)
            .ToArray();
    }

    private static bool IsInRange(ICanonicalRecord record, long? startExclusiveOrder, long? endExclusiveOrder)
    {
        var order = record.Order;
        if ((startExclusiveOrder is not null || endExclusiveOrder is not null) && order is null)
        {
            return false;
        }

        if (startExclusiveOrder is not null && order!.Value <= startExclusiveOrder.Value)
        {
            return false;
        }

        if (endExclusiveOrder is not null && order!.Value >= endExclusiveOrder.Value)
        {
            return false;
        }

        return true;
    }
}
