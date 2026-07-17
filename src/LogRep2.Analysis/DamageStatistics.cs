namespace FFXI_LogAnalyzer.Core;

public sealed class DamageStatistics
{
    public DamageStatistics(IReadOnlyList<int> damageValues)
    {
        DamageValues = damageValues;
        TotalDamage = damageValues.Sum();
        MaxDamage = damageValues.Count == 0 ? null : damageValues.Max();
        MinDamage = damageValues.Count == 0 ? null : damageValues.Min();
        AverageDamage = damageValues.Count == 0 ? null : damageValues.Average();
    }

    public IReadOnlyList<int> DamageValues { get; }

    public int TotalDamage { get; }

    public int? MaxDamage { get; }

    public int? MinDamage { get; }

    public double? AverageDamage { get; }

    public static DamageStatistics Empty { get; } = new([]);

    public static DamageStatistics FromParsedActions(IEnumerable<ParsedActionGroup> actions)
    {
        return new DamageStatistics(actions.SelectMany(action => action.Damage.DamageValues).ToArray());
    }
}
