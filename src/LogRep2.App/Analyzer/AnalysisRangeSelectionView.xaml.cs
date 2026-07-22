namespace FFXI_LogAnalyzer.App;

using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

public partial class AnalysisRangeSelectionView : System.Windows.Controls.UserControl
{
    public AnalysisRangeSelectionView()
    {
        InitializeComponent();
    }

    private void OnAreaSegmentDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is not DataGrid dataGrid
            || ItemsControl.ContainerFromElement(dataGrid, e.OriginalSource as DependencyObject) is not DataGridRow
            || DataContext is not AnalysisRangeViewModel viewModel
            || viewModel.SelectedAreaSegment is null
            || !viewModel.RunAnalysisCommand.CanExecute(null))
        {
            return;
        }

        viewModel.RunAnalysisCommand.Execute(null);
        e.Handled = true;
    }
}
