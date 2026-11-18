using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;

namespace FFXI_LogAnalyzer.App;

public partial class LogRangeWindow : Window
{
    public LogRangeWindow(LogRangeViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        viewModel.UseRequested += OnUse;
        viewModel.DeleteRequested += OnDelete;
        Closed += (_, _) =>
        {
            viewModel.UseRequested -= OnUse;
            viewModel.DeleteRequested -= OnDelete;
        };
        InputBindings.Add(new KeyBinding(new RelayCommand(FocusSearch), Key.F, ModifierKeys.Control));
        SearchBox.PreviewKeyDown += OnSearchKeyDown;
    }

    private void OnUse() => DialogResult = true;
    private void OnDelete()
    {
        if (DataContext is not LogRangeViewModel viewModel || viewModel.SelectedSavedRange is not { } range) return;
        if (System.Windows.MessageBox.Show(this, $"保存区間「{range.Name}」を削除しますか？元ログは削除されません。",
            "保存区間の削除", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            viewModel.DeleteSelectedRange();
    }
    private void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (LogsGrid.SelectedItem is { } row) LogsGrid.ScrollIntoView(row);
    }
    private void FocusSearch() { SearchBox.Focus(); SearchBox.SelectAll(); }
    private void OnClearSearchClick(object sender, RoutedEventArgs e) => ClearSearch();
    private void ClearSearch()
    {
        if (DataContext is not LogRangeViewModel viewModel) return;
        viewModel.SearchText = string.Empty;
        viewModel.SearchCommand.Execute(null);
        SearchBox.Focus();
    }
    private void OnSearchKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape || SearchBox.Text.Length == 0) return;
        e.Handled = true;
        ClearSearch();
    }
}
