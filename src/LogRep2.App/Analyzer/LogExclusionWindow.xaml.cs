using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Button = System.Windows.Controls.Button;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;

namespace FFXI_LogAnalyzer.App;

public partial class LogExclusionWindow : Window
{
    public LogExclusionWindow(LogExclusionViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;

        // Ctrl+F で検索欄へ、Esc（検索欄内）でクリア
        InputBindings.Add(new KeyBinding(new RelayCommand(FocusSearch), Key.F, ModifierKeys.Control));
        SearchBox.PreviewKeyDown += OnSearchBoxPreviewKeyDown;
        Loaded += (_, _) => FocusSearch();
    }

    private void OnEditClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is not LogExclusionViewModel viewModel
            || sender is not Button { CommandParameter: string tag }) return;
        viewModel.Apply(Enum.Parse<LogExclusionOperation>(tag), MainLogs.SelectedItems.Cast<LogExclusionRow>().ToArray());
    }

    private void OnMainSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (DataContext is LogExclusionViewModel viewModel)
            viewModel.UpdateSelection(MainLogs.SelectedItems.Cast<LogExclusionRow>());

        if (MainLogs.SelectedItem is { } selected)
            MainLogs.ScrollIntoView(selected);
    }

    private void OnClearSearchClick(object sender, RoutedEventArgs e) => ClearSearch();

    private void OnSearchBoxPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape || string.IsNullOrEmpty(SearchBox.Text)) return;
        // 検索語があるときの Esc はクリアに使い、ウィンドウを閉じない（IsCancel より優先）
        e.Handled = true;
        ClearSearch();
    }

    private void ClearSearch()
    {
        if (DataContext is not LogExclusionViewModel viewModel) return;
        viewModel.SearchText = string.Empty;
        viewModel.SearchCommand.Execute(null);
        SearchBox.Focus();
    }

    private void FocusSearch()
    {
        SearchBox.Focus();
        SearchBox.SelectAll();
    }
}
