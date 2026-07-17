using FFXI_LogAnalyzer.Core;

namespace FFXI_LogAnalyzer.App;

public sealed class MarkerListViewModel
{
    public MarkerListViewModel(MarkerRecord marker)
    {
        Marker = marker;
        SessionId = string.IsNullOrWhiteSpace(marker.SourceRecord.SessionId) ? "-" : marker.SourceRecord.SessionId;
        Order = marker.Order?.ToString() ?? "-";
        MarkerKeyword = string.IsNullOrWhiteSpace(marker.MarkerKeyword) ? "-" : marker.MarkerKeyword;
        VisibleText = string.IsNullOrWhiteSpace(marker.VisibleText) ? "-" : marker.VisibleText;
        MessageTimeText = string.IsNullOrWhiteSpace(marker.MessageTimeText) ? "-" : marker.MessageTimeText;
        FirstSeenAt = marker.FirstSeenAt?.ToString("yyyy-MM-dd HH:mm:ss zzz") ?? "-";
    }

    public MarkerRecord Marker { get; }

    public string SessionId { get; }

    public string Order { get; }

    public string MarkerKeyword { get; }

    public string VisibleText { get; }

    public string MessageTimeText { get; }

    public string FirstSeenAt { get; }
}
