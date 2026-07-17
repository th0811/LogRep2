namespace FFXI_LogAnalyzer.Core;

public sealed record ParsedAction(
    string? Actor,
    string? ActionName,
    ActionType ActionType,
    ParsedDamageResult Damage,
    HitStatus HitStatus)
{
    public static ParsedAction Unknown { get; } = new(
        null,
        null,
        ActionType.Unknown,
        ParsedDamageResult.None,
        HitStatus.Unknown);
}
