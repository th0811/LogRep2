using FFXI_LogAnalyzer.Core;

namespace FFXI_LogAnalyzer.App;

public sealed class AreaStaySegmentListViewModel
{
    public AreaStaySegmentListViewModel(AreaStaySegment segment)
    {
        Segment = segment;
        var endOrder = segment.End?.Order.ToString() ?? "ログ末尾";
        var firstTime = string.IsNullOrWhiteSpace(segment.FirstMessageTimeText)
            ? "時刻なし"
            : $"最初の時刻 {segment.FirstMessageTimeText}";
        DisplayText = $"#{segment.Sequence} {segment.AreaName}"
            + $"（{segment.AreaOccurrence}回目）"
            + $" / order {segment.Start.Order}～{endOrder}"
            + $" / {segment.RecordCount:N0}件 / {firstTime}";
    }

    public AreaStaySegment Segment { get; }

    public string DisplayText { get; }

    public int Sequence => Segment.Sequence;

    public string AreaName => Segment.AreaName;

    public string OccurrenceText => $"{Segment.AreaOccurrence}回目";

    public string FirstMessageTimeText => string.IsNullOrWhiteSpace(Segment.FirstMessageTimeText)
        ? "時刻なし"
        : Segment.FirstMessageTimeText;

    public int RecordCount => Segment.RecordCount;

    public string OrderRangeText => $"{Segment.Start.Order}～{Segment.End?.Order.ToString() ?? "ログ末尾"}";

    public string SelectionSummary => $"選択中: {AreaName}（{OccurrenceText}） / {RecordCount:N0}件 / ログ順 {OrderRangeText}";
}
