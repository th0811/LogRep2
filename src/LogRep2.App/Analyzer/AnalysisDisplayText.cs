using FFXI_LogAnalyzer.Core;

namespace FFXI_LogAnalyzer.App;

internal static class AnalysisDisplayText
{
    public static string ToDateTimeText(DateTimeOffset? value)
    {
        return value?.ToString("yyyy-MM-dd HH:mm:ss") ?? "-";
    }

    public static string ToText(TimeConfidence confidence) => confidence switch
    {
        TimeConfidence.Exact => "秒単位",
        TimeConfidence.Minute => "分単位",
        TimeConfidence.Estimated => "推定",
        TimeConfidence.Unknown => "不明",
        _ => confidence.ToString(),
    };

    public static string ToText(ActionType actionType) => actionType switch
    {
        ActionType.NormalAttack => "通常攻撃",
        ActionType.NormalAttackCritical => "通常攻撃（クリティカル）",
        ActionType.Skill => "技",
        ActionType.Magic => "魔法",
        ActionType.Unknown => "未分類",
        _ => actionType.ToString(),
    };

    public static string ToText(SessionStatus status) => status switch
    {
        SessionStatus.Active => "収集中",
        SessionStatus.Completed => "完了",
        SessionStatus.Aborted => "中断",
        SessionStatus.Unknown => "不明",
        _ => status.ToString(),
    };
}
