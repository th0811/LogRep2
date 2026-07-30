using System.Globalization;
using System.Windows.Data;

namespace FfxiTempLogCollector.App;

/// <summary>
/// ItemsControl.AlternationIndex は 0 始まりのため、表示用に 1 始まりへ直す。
/// </summary>
public sealed class OneBasedIndexConverter : IValueConverter
{
    public object Convert(
        object value,
        Type targetType,
        object parameter,
        CultureInfo culture)
    {
        return value is int index
            ? (index + 1).ToString(culture)
            : string.Empty;
    }

    public object ConvertBack(
        object value,
        Type targetType,
        object parameter,
        CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
