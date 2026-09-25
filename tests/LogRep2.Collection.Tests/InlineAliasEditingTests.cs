using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Input;
using System.Windows.Threading;
using FFXI_LogAnalyzer.App;
using FFXI_LogAnalyzer.Core;
using LogRep2.Infrastructure;

namespace FfxiTempLogCollector.Tests;

public sealed class InlineAliasEditingTests
{
    [Fact]
    public void 変更なしと取消は書き込まず保存成功と失敗を区別する()
    {
        RunSta(() =>
        {
            using var directory = new TemporaryDirectory();
            var session = LogExclusionTests.CreateSession(directory.Path);
            session.SaveAlias("元の値");
            var path = directory.GetPath(SessionAnnotationsStore.FileName);
            var timestamp = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            File.SetLastWriteTimeUtc(path, timestamp);
            session.BeginAliasEdit();
            session.EditingAlias = " 元の値 ";
            Assert.True(session.TryCommitAliasEdit());
            Assert.False(session.IsAliasJustSaved);
            Assert.Equal(timestamp, File.GetLastWriteTimeUtc(path));
            session.BeginAliasEdit();
            session.EditingAlias = "取消する値";
            session.CancelAliasEdit();
            Assert.Equal("元の値", session.EditingAlias);
            Assert.Equal(timestamp, File.GetLastWriteTimeUtc(path));

            session.BeginAliasEdit();
            session.EditingAlias = "保持する入力";
            using (File.Open(path, FileMode.Open, FileAccess.Read, FileShare.None))
                Assert.False(session.TryCommitAliasEdit());
            Assert.True(session.IsAliasEditing);
            Assert.True(session.HasAliasEditError);
            Assert.Equal("保持する入力", session.EditingAlias);
            Assert.Equal("元の値", session.Alias);
            session.CancelAliasEdit();
            Assert.False(session.HasAliasEditError);

            session.BeginAliasEdit();
            session.EditingAlias = string.Empty;
            Assert.True(session.TryCommitAliasEdit());
            Assert.False(session.HasAlias);
            Assert.True(session.IsAliasJustSaved);
            PumpUntil(() => !session.IsAliasJustSaved);
            session.BeginAliasEdit();
            session.EditingAlias = "再保存";
            Assert.True(session.TryCommitAliasEdit());
            Assert.True(session.IsAliasJustSaved);
        });
    }

    [Fact]
    public void セル確定後に検索を更新し読み込み失敗行は編集できない()
    {
        RunSta(() =>
        {
            WpfTestApplication.Ensure();
            using var directory = new TemporaryDirectory();
            var settings = new LogRep2Settings();
            settings.Collection.OutputDirectory = directory.GetPath("sessions");
            new LogRep2SettingsStore(directory.Path).Save(settings);
            var folder = directory.GetPath("sessions/first");
            Directory.CreateDirectory(folder);
            File.WriteAllText(Path.Combine(folder, "session.json"), """{"session_id":"first","status":"completed"}""");
            File.WriteAllText(Path.Combine(folder, "stats.json"), "{}");
            File.WriteAllText(Path.Combine(folder, "canonical_records.jsonl"), string.Empty);
            new SessionAnnotationsStore().Save(folder, "first", "検索対象");
            var main = new MainViewModel(new SessionOpenService(), new DialogService(), new AnalyzerSettingsStore(directory.Path));
            PumpUntil(() => !main.IsBusy);
            main.SessionSearchText = "検索";
            var session = main.Sessions[0];
            var window = new MainWindow { DataContext = main };
            var root = (FrameworkElement)window.Content;
            root.Measure(new Size(1560, 840));
            root.Arrange(new Rect(0, 0, 1560, 840));
            root.UpdateLayout();
            Pump();
            var grid = (DataGrid)window.FindName("SessionGrid");
            grid.UpdateLayout();
            var column = grid.Columns.Single(column => column.SortMemberPath == "Alias");
            grid.CurrentCell = new DataGridCellInfo(session, column);
            Assert.True(grid.BeginEdit());
            grid.UpdateLayout();
            Pump();
            var editor = FindEditor(grid);
            Assert.NotNull(editor);
            Assert.Equal(100, editor.MaxLength);
            editor.Text = "検索から消える前";
            editor.GetBindingExpression(TextBox.TextProperty)!.UpdateSource();
            Assert.Equal("検索対象", session.Alias);
            Assert.Single(main.FilteredSessions);
            RaiseKey(editor, Key.Escape);
            Assert.Equal("検索対象", session.Alias);
            grid.CurrentCell = new DataGridCellInfo(session, column);
            RaiseKey(grid, Key.F2);
            Assert.True(session.IsAliasEditing);
            grid.UpdateLayout();
            Pump();
            editor = FindEditor(grid)!;
            editor.Text = "変更後";
            editor.GetBindingExpression(TextBox.TextProperty)!.UpdateSource();
            RaiseKey(editor, Key.Enter);
            Pump();
            Assert.Empty(main.FilteredSessions);
            Assert.Equal("変更後", new SessionAnnotationsStore().Load(folder, "first"));
            Assert.DoesNotContain("エイリアスを保存", main.StatusMessage);
            main.SessionSearchText = string.Empty;
            Pump();
            grid.CurrentCell = new DataGridCellInfo(session, column);
            grid.UpdateLayout();
            var cell = FindCell(grid, column)!;
            grid.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left)
            {
                RoutedEvent = UIElement.PreviewMouseLeftButtonDownEvent,
                Source = cell,
            });
            Assert.True(session.IsAliasEditing);
            grid.UpdateLayout();
            Pump();
            editor = FindEditor(grid)!;
            editor.Text = "保持する入力";
            editor.GetBindingExpression(TextBox.TextProperty)!.UpdateSource();
            using (File.Open(Path.Combine(folder, SessionAnnotationsStore.FileName), FileMode.Open, FileAccess.Read, FileShare.None))
                Assert.False(grid.CommitEdit(DataGridEditingUnit.Cell, true));
            Assert.True(session.IsAliasEditing);
            Assert.True(session.HasAliasEditError);
            Assert.Equal("保持する入力", editor.Text);
            RaiseKey(editor, Key.Escape);
            Pump();
            grid.CurrentCell = new DataGridCellInfo(session, column);
            Assert.True(grid.BeginEdit());
            grid.UpdateLayout();
            Pump();
            editor = FindEditor(grid)!;
            editor.Text = "フォーカス移動で保存";
            editor.GetBindingExpression(TextBox.TextProperty)!.UpdateSource();
            editor.RaiseEvent(new KeyboardFocusChangedEventArgs(Keyboard.PrimaryDevice, 0, editor, grid)
            { RoutedEvent = Keyboard.LostKeyboardFocusEvent });
            Pump();
            Assert.False(session.IsAliasEditing);
            Assert.Equal("フォーカス移動で保存", session.Alias);
            Assert.Same(session, main.SelectedSession);
            var checkboxCell = FindCell(grid, grid.Columns[0])!;
            grid.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left)
            { RoutedEvent = UIElement.PreviewMouseLeftButtonDownEvent, Source = checkboxCell });
            Assert.False(session.IsEnabled);
            File.WriteAllText(Path.Combine(folder, SessionAnnotationsStore.FileName), "不正");
            session.LoadAlias();
            Pump();
            grid.CurrentCell = new DataGridCellInfo(session, column);
            Assert.False(grid.BeginEdit());
            Assert.False(session.CanEditAlias);
            window.Close();
        });
    }

    private static TextBox? FindEditor(DependencyObject root)
    {
        if (root is TextBox { Name: "AliasEditor" } box) return box;
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
            if (FindEditor(VisualTreeHelper.GetChild(root, i)) is { } child) return child;
        return null;
    }

    private static DataGridCell? FindCell(DependencyObject root, DataGridColumn column)
    {
        if (root is DataGridCell cell && cell.Column == column) return cell;
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
            if (FindCell(VisualTreeHelper.GetChild(root, i), column) is { } child) return child;
        return null;
    }

    private static void RaiseKey(UIElement target, Key key) => target.RaiseEvent(
        new KeyEventArgs(Keyboard.PrimaryDevice, new TestPresentationSource(), 0, key)
        { RoutedEvent = Keyboard.PreviewKeyDownEvent });

    private sealed class TestPresentationSource : PresentationSource
    {
        public override Visual RootVisual { get; set; } = new DrawingVisual();
        public override bool IsDisposed => false;
        protected override CompositionTarget GetCompositionTargetCore() => null!;
    }

    private static void Pump() => Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);

    private static void PumpUntil(Func<bool> condition)
    {
        var timer = System.Diagnostics.Stopwatch.StartNew();
        while (!condition())
        {
            Assert.True(timer.Elapsed < TimeSpan.FromSeconds(10), "画面更新が制限時間内に完了しませんでした。");
            Pump();
            Thread.Sleep(10);
        }
    }

    private static void RunSta(Action action)
    {
        Exception? error = null;
        var thread = new Thread(() => { try { action(); } catch (Exception exception) { error = exception; } });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(20)), "画面テストが制限時間内に完了しませんでした。");
        Assert.Null(error);
    }
}
