using System.Globalization;
using FFXI_LogAnalyzer.Core;

namespace FFXI_LogAnalyzer.App;

public sealed class AreaStaySegmentListViewModel
{
    public AreaStaySegmentListViewModel(
        AreaStaySegment segment,
        string? lastMessageTimeText = null)
    {
        Segment = segment;
        LastMessageTimeText = string.IsNullOrWhiteSpace(lastMessageTimeText)
            ? null
            : lastMessageTimeText;
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

    public string FirstMessageTimeText => TrimBrackets(Segment.FirstMessageTimeText) is { Length: > 0 } text
        ? text
        : "時刻なし";

    /// <summary>区間内で最後に時刻を持っていた行のログ内時刻。無ければ null。</summary>
    public string? LastMessageTimeText { get; }

    public int RecordCount => Segment.RecordCount;

    public string OrderRangeText => $"{Segment.Start.Order}～{Segment.End?.Order.ToString() ?? "ログ末尾"}";

    /// <summary>区間の開始〜終了のログ内時刻。例「22:11:08 – 22:58:41」。</summary>
    public string TimeRangeText
    {
        get
        {
            var start = TrimBrackets(Segment.FirstMessageTimeText);
            if (start.Length == 0)
            {
                return "-";
            }

            var end = TrimBrackets(LastMessageTimeText);
            return end.Length == 0 || end == start
                ? start
                : $"{start} – {end}";
        }
    }

    /// <summary>区間の経過時間。例「47分33秒」。時刻が取れない場合は「-」。</summary>
    public string ElapsedText
    {
        get
        {
            if (!TryParseLogTime(Segment.FirstMessageTimeText, out var start)
                || !TryParseLogTime(LastMessageTimeText, out var end))
            {
                return "-";
            }

            var elapsed = end - start;
            if (elapsed < TimeSpan.Zero)
            {
                // 日をまたいだ区間。ログ内時刻には日付が無いので24時間を足して補正する。
                elapsed += TimeSpan.FromDays(1);
            }

            if (elapsed.TotalHours >= 1)
            {
                return $"{(int)elapsed.TotalHours}時間{elapsed.Minutes}分{elapsed.Seconds}秒";
            }

            return elapsed.TotalMinutes >= 1
                ? $"{elapsed.Minutes}分{elapsed.Seconds}秒"
                : $"{elapsed.Seconds}秒";
        }
    }

    public string SelectionSummary => $"選択中: {AreaName}（{OccurrenceText}） / {RecordCount:N0}件 / ログ順 {OrderRangeText}";

    /// <summary>ログ内時刻は「[21:35:11]」のように角括弧付きで保持されているため外す。</summary>
    private static string TrimBrackets(string? text) =>
        text?.Trim().Trim('[', ']').Trim() ?? string.Empty;

    private static bool TryParseLogTime(string? text, out TimeSpan value)
    {
        value = TimeSpan.Zero;
        var normalized = TrimBrackets(text);
        return normalized.Length > 0
            && TimeSpan.TryParseExact(
                normalized,
                @"hh\:mm\:ss",
                CultureInfo.InvariantCulture,
                out value);
    }
}
