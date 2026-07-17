using FFXI_LogAnalyzer.Core;

namespace FFXI_LogAnalyzer.App;

public sealed class ActionSummaryViewModel
{
    public ActionSummaryViewModel(ActionSummary summary)
    {
        Actor = summary.Actor;
        ActionName = summary.ActionName;
        ActionType = summary.ActionType.ToString();
        UseCount = summary.UseCount.ToString("N0");
        HitCount = summary.HitCount.ToString("N0");
        MissCount = summary.MissCount.ToString("N0");
        UnknownCount = summary.UnknownCount.ToString("N0");
        HitRate = FormatRate(summary.HitRate);
        TotalDamage = summary.Damage.TotalDamage.ToString("N0");
        MaxDamage = FormatNullable(summary.Damage.MaxDamage);
        MinDamage = FormatNullable(summary.Damage.MinDamage);
        AverageDamage = FormatNullable(summary.Damage.AverageDamage);
    }

    public string Actor { get; }

    public string ActionName { get; }

    public string ActionType { get; }

    public string UseCount { get; }

    public string HitCount { get; }

    public string MissCount { get; }

    public string UnknownCount { get; }

    public string HitRate { get; }

    public string TotalDamage { get; }

    public string MaxDamage { get; }

    public string MinDamage { get; }

    public string AverageDamage { get; }

    private static string FormatRate(double? rate)
    {
        return rate is null ? "-" : $"{rate:P1}";
    }

    private static string FormatNullable(double? value)
    {
        return value is null ? "-" : value.Value.ToString("0.###");
    }
}
