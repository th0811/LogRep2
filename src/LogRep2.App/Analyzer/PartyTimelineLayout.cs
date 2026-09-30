using FFXI_LogAnalyzer.Core;

namespace FFXI_LogAnalyzer.App;

internal static class PartyTimelineLayout
{
    public static IReadOnlyList<IReadOnlyList<int>> BuildRows(IReadOnlyList<PartyTimelineEvent> events)
    {
        var rows = new List<IReadOnlyList<int>>();
        var row = new List<int>();
        for (var index = 0; index < events.Count; index++)
        {
            var item = events[index];
            if (row.Count > 0)
            {
                var first = events[row[0]];
                if (row.Count == 3
                    || row.Any(previous => events[previous].Actor == item.Actor)
                    || first.SessionId != item.SessionId
                    || first.ReferenceTime != item.ReferenceTime)
                {
                    rows.Add(row);
                    row = [];
                }
            }
            // 過去の行へ戻らず、連続した行動だけを横並びにします。
            row.Add(index);
        }
        if (row.Count > 0) rows.Add(row);
        return rows;
    }
}
