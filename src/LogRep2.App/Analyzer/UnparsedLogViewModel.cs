using FFXI_LogAnalyzer.Core;

namespace FFXI_LogAnalyzer.App;

public sealed class UnparsedLogViewModel
{
    public UnparsedLogViewModel(UnparsedActionGroup unparsedActionGroup)
        : this(
            unparsedActionGroup.Group,
            unparsedActionGroup.Reason)
    {
    }

    public UnparsedLogViewModel(ParsedActionGroup unknownActionGroup)
        : this(
            unknownActionGroup.Group,
            CreateUnknownReason(unknownActionGroup))
    {
    }

    private UnparsedLogViewModel(ActionGroup group, string reason)
    {
        ActionGroupKey = group.ActionGroupKey;
        OrderRange = FormatOrderRange(group.OrderMin, group.OrderMax);
        EventGroup = group.EventGroup;
        VisibleTexts = group.VisibleTexts.Count == 0
            ? "-"
            : string.Join(Environment.NewLine, group.VisibleTexts);
        Reason = reason;
    }

    public string ActionGroupKey { get; }

    public string OrderRange { get; }

    public string EventGroup { get; }

    public string VisibleTexts { get; }

    public string Reason { get; }

    private static string CreateUnknownReason(ParsedActionGroup unknownActionGroup)
    {
        return $"unknown判定です。action_type={unknownActionGroup.ActionType}, hit_status={unknownActionGroup.HitStatus}";
    }

    private static string FormatOrderRange(long? orderMin, long? orderMax)
    {
        if (orderMin is null && orderMax is null)
        {
            return "-";
        }

        if (orderMin == orderMax)
        {
            return orderMin?.ToString() ?? "-";
        }

        return $"{orderMin?.ToString() ?? "-"}-{orderMax?.ToString() ?? "-"}";
    }
}
