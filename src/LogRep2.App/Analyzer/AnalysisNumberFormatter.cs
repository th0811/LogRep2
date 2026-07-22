using System.Globalization;

namespace FFXI_LogAnalyzer.App;

internal static class AnalysisNumberFormatter
{
    public static string FormatDecimal(double? value)
    {
        return value is null
            ? "-"
            : value.Value.ToString("N2", CultureInfo.InvariantCulture);
    }

    public static string FormatInteger(int? value)
    {
        return value is null
            ? "-"
            : value.Value.ToString("N0", CultureInfo.InvariantCulture);
    }
}
