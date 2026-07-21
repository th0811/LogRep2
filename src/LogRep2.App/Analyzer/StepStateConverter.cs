using System.Globalization;
using System.Windows;
using System.Windows.Data;
using Binding = System.Windows.Data.Binding;
using Brush = System.Windows.Media.Brush;
using Brushes = System.Windows.Media.Brushes;
using Color = System.Windows.Media.Color;
using ColorConverter = System.Windows.Media.ColorConverter;
using SolidColorBrush = System.Windows.Media.SolidColorBrush;

namespace FFXI_LogAnalyzer.App;

// ステッパー各ステップの StepState を、ConverterParameter に応じて
// 背景色・アクセント色・文字色・アイコン表示などの見た目に変換する。
public sealed class StepStateConverter : IValueConverter
{
    private static readonly Brush ChipCompleted = Freeze("#E1F5EE");
    private static readonly Brush ChipActive = Freeze("#E6F1FB");
    private static readonly Brush ChipPending = Freeze("#F4F5F2");

    private static readonly Brush AccentCompleted = Freeze("#1D9E75");
    private static readonly Brush AccentActive = Freeze("#378ADD");
    private static readonly Brush AccentPending = Freeze("#C7CBD1");

    private static readonly Brush TitleCompleted = Freeze("#085041");
    private static readonly Brush TitleActive = Freeze("#0C447C");
    private static readonly Brush TitlePending = Freeze("#5F5E5A");

    public object Convert(
        object? value,
        Type targetType,
        object? parameter,
        CultureInfo culture)
    {
        var state = value is StepState s ? s : StepState.Pending;
        var role = parameter as string ?? "chip";

        return role switch
        {
            "chip" => state switch
            {
                StepState.Completed => ChipCompleted,
                StepState.Active => ChipActive,
                _ => ChipPending
            },
            "accent" => state switch
            {
                StepState.Completed => AccentCompleted,
                StepState.Active => AccentActive,
                _ => AccentPending
            },
            "title" => state switch
            {
                StepState.Completed => TitleCompleted,
                StepState.Active => TitleActive,
                _ => TitlePending
            },
            "outline" => state == StepState.Active ? AccentActive : Brushes.Transparent,
            "circleForeground" => state == StepState.Pending ? TitlePending : Brushes.White,
            "checkVisibility" => state == StepState.Completed
                ? Visibility.Visible
                : Visibility.Collapsed,
            "numberVisibility" => state == StepState.Completed
                ? Visibility.Collapsed
                : Visibility.Visible,
            "label" => state switch
            {
                StepState.Completed => "完了",
                StepState.Active => "実行中",
                _ => "未着手"
            },
            _ => Binding.DoNothing
        };
    }

    public object ConvertBack(
        object? value,
        Type targetType,
        object? parameter,
        CultureInfo culture)
    {
        throw new NotSupportedException();
    }

    private static Brush Freeze(string hex)
    {
        var brush = new SolidColorBrush(
            (Color)ColorConverter.ConvertFromString(hex));
        brush.Freeze();
        return brush;
    }
}
