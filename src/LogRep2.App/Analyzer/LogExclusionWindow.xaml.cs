using System.Windows;
using System.Windows.Controls;

namespace FFXI_LogAnalyzer.App;

public partial class LogExclusionWindow : Window
{
    public LogExclusionWindow(LogExclusionViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    private void OnEditClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is not LogExclusionViewModel viewModel
            || sender is not System.Windows.Controls.Button { Tag: string tag }) return;
        viewModel.Apply(Enum.Parse<LogExclusionOperation>(tag), MainLogs.SelectedItems.Cast<LogExclusionRow>().ToArray());
    }

    private void OnMainSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (MainLogs.SelectedItem is { } selected)
        {
            MainLogs.ScrollIntoView(selected);
        }
    }
}
