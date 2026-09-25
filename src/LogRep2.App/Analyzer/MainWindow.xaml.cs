using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using TextBox = System.Windows.Controls.TextBox;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;

namespace FFXI_LogAnalyzer.App;

public partial class MainWindow : Window
{
    private bool _endingAliasEdit;
    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainViewModel(
            new SessionOpenService(),
            new DialogService());
        ((MainViewModel)DataContext).LogExclusionsRequested += editor =>
            new LogExclusionWindow(editor) { Owner = this }.ShowDialog();

        // エイリアスセルは未選択の行でも1クリックで編集を始めます。
        SessionGrid.PreviewMouseLeftButtonDown += OnSessionGridPreviewMouseLeftButtonDown;
        SessionGrid.PreviewMouseRightButtonDown += (_, e) =>
        {
            var row = FindAncestor<DataGridRow>(e.OriginalSource as DependencyObject);
            if (row is not null) row.IsSelected = true;
        };
        SessionGrid.PreviewKeyDown += (_, e) =>
        {
            if (e.Key != Key.F2) return;
            e.Handled = true;
            OnEditAliasMenuClick(this, e);
        };
        Closing += (_, e) =>
        {
            if (SessionGrid.Items.OfType<SessionSelectionViewModel>().Any(session => session.IsAliasEditing))
                e.Cancel = !SessionGrid.CommitEdit(DataGridEditingUnit.Cell, true);
        };
    }

    // ---- 編集開始 ----------------------------------------------------------

    // エイリアス列のセルは 1 クリックで編集開始（未選択行でも）。
    private void OnSessionGridPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        var cell = FindAncestor<DataGridCell>(e.OriginalSource as DependencyObject);
        if (cell is null || cell.IsEditing || cell.IsReadOnly) return;
        if (cell.Column is DataGridCheckBoxColumn && cell.DataContext is SessionSelectionViewModel target)
        {
            e.Handled = true;
            if (!SessionGrid.CommitEdit(DataGridEditingUnit.Cell, true)) return;
            SessionGrid.CommitEdit(DataGridEditingUnit.Row, true);
            SessionGrid.SelectedItem = target;
            target.IsEnabled = !target.IsEnabled;
            return;
        }
        if (!IsAliasColumn(cell.Column)) return;
        if (cell.DataContext is not SessionSelectionViewModel { CanEditAlias: true }) return;

        if (!cell.IsFocused) cell.Focus();
        var row = FindAncestor<DataGridRow>(cell);
        if (row is not null && !row.IsSelected) row.IsSelected = true;
        SessionGrid.CurrentCell = new DataGridCellInfo(cell);
        SessionGrid.BeginEdit(e);
        e.Handled = true;
    }

    private void OnEditAliasMenuClick(object sender, RoutedEventArgs e)
    {
        if (SessionGrid.SelectedItem is null) return;
        var aliasColumn = SessionGrid.Columns.FirstOrDefault(IsAliasColumn);
        if (aliasColumn is null) return;
        SessionGrid.CurrentCell = new DataGridCellInfo(SessionGrid.SelectedItem, aliasColumn);
        SessionGrid.BeginEdit();
    }

    // F2 / ダブルクリック / 上記いずれの経路でもここを通る
    private void OnSessionGridBeginningEdit(object? sender, DataGridBeginningEditEventArgs e)
    {
        if (!IsAliasColumn(e.Column)) return;
        if (e.Row.Item is not SessionSelectionViewModel session || !session.CanEditAlias)
        {
            e.Cancel = true;
            return;
        }
        session.BeginAliasEdit();
    }

    private void OnAliasEditorLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is not TextBox box) return;
        box.Focus();
        box.SelectAll();
    }

    // ---- 確定 / 取消 --------------------------------------------------------

    // Enter: 保存して編集終了。DataGrid 既定の「次の行へ移動」を抑止する。
    // Esc  : セルの編集を取り消し、元の値に戻す。
    private void OnAliasEditorPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            e.Handled = true;
            SessionGrid.CancelEdit(DataGridEditingUnit.Cell);
            SessionGrid.CancelEdit(DataGridEditingUnit.Row);
            return;
        }
        if (e.Key != Key.Enter) return;
        e.Handled = true;
        if (SessionGrid.CommitEdit(DataGridEditingUnit.Cell, exitEditingMode: true))
            SessionGrid.CommitEdit(DataGridEditingUnit.Row, true);
    }

    private void OnAliasEditorLostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (sender is not TextBox box || _endingAliasEdit) return;
        Dispatcher.BeginInvoke(new Action(() =>
        {
            if (!box.IsKeyboardFocusWithin && FindAncestor<DataGridCell>(box) is { IsEditing: true }
                && box.DataContext is SessionSelectionViewModel { IsAliasEditing: true })
            {
                if (SessionGrid.CommitEdit(DataGridEditingUnit.Cell, true))
                    SessionGrid.CommitEdit(DataGridEditingUnit.Row, true);
            }
        }), DispatcherPriority.Background);
    }

    private void OnSessionGridCellEditEnding(object? sender, DataGridCellEditEndingEventArgs e)
    {
        if (!IsAliasColumn(e.Column)) return;
        if (e.Row.Item is not SessionSelectionViewModel session) return;

        if (e.EditAction == DataGridEditAction.Cancel)
        {
            session.CancelAliasEdit();
            return;
        }

        // フォーカス外れ・Enter のどちらもここに来る
        _endingAliasEdit = true;
        try
        {
            if (!session.TryCommitAliasEdit())
            {
                // 保存失敗時は入力を維持し、フォーカスを編集欄に戻します。
                e.Cancel = true;
                Dispatcher.BeginInvoke(new Action(() => FindDescendant<TextBox>(e.EditingElement)?.Focus()), DispatcherPriority.Input);
            }
            else
            {
                // 検索結果の更新より先に行の編集トランザクションも終了します。
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    if (!SessionGrid.Items.OfType<SessionSelectionViewModel>().Any(item => item.IsAliasEditing))
                        SessionGrid.CommitEdit(DataGridEditingUnit.Row, true);
                }), DispatcherPriority.Input);
            }
        }
        finally { _endingAliasEdit = false; }
    }

    // ---- 補助処理 ----------------------------------------------------------

    private static bool IsAliasColumn(DataGridColumn? column) =>
        column is DataGridTemplateColumn { SortMemberPath: nameof(SessionSelectionViewModel.Alias) };

    private static T? FindAncestor<T>(DependencyObject? current) where T : DependencyObject
    {
        while (current is not null)
        {
            if (current is T match) return match;
            current = current is Visual or System.Windows.Media.Media3D.Visual3D
                ? VisualTreeHelper.GetParent(current)
                : LogicalTreeHelper.GetParent(current);
        }
        return null;
    }

    private static T? FindDescendant<T>(DependencyObject current) where T : DependencyObject
    {
        if (current is T match) return match;
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(current); i++)
            if (FindDescendant<T>(VisualTreeHelper.GetChild(current, i)) is { } child) return child;
        return null;
    }
}
