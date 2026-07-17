namespace FFXI_LogAnalyzer.Core;

public sealed record ParsedDamageResult(
    bool HasDamage,
    int? Damage,
    IReadOnlyList<int> DamageValues)
{
    public static ParsedDamageResult None { get; } = new(false, null, []);

    public static ParsedDamageResult FromDamage(int damage)
    {
        return new ParsedDamageResult(true, damage, [damage]);
    }

    public static ParsedDamageResult FromDamages(IReadOnlyList<int> damages)
    {
        return damages.Count == 0
            ? None
            : new ParsedDamageResult(true, damages.Sum(), damages);
    }
}
