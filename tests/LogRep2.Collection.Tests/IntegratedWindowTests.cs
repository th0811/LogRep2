using System.Windows;
using InputCursors = System.Windows.Input.Cursors;

namespace FfxiTempLogCollector.Tests;

public sealed class IntegratedWindowTests
{
    [Fact]
    public void HTML候補ゼロから登録管理で復帰しNPC登録で選択を解除する()
    {
        Exception? captured = null;
        var thread = new Thread(() =>
        {
            try
            {
                WpfTestApplication.Ensure();
                (string Name, string Classification)[] candidates = [];
                System.Windows.Window? managerOwner = null;
                var model = new FFXI_LogAnalyzer.App.TimelineMemberSelectionViewModel(() => candidates);
                var window = new FFXI_LogAnalyzer.App.TimelineMemberSelectionWindow(model,
                    owner => { managerOwner = owner; candidates = [("Alice", "PC登録")]; },
                    _ => { }, _ => candidates = [], _ => { });
                try
                {
                    var accept = (System.Windows.Controls.Button)window.FindName("AcceptButton");
                    var manage = (System.Windows.Controls.Button)window.FindName("ManageButton");
                    var empty = (System.Windows.Controls.TextBlock)window.FindName("EmptyMessage");
                    Assert.False(accept.IsEnabled);
                    Assert.True(manage.IsEnabled);
                    Assert.Equal(Visibility.Visible, empty.Visibility);
                    manage.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
                    Assert.Same(window, managerOwner);
                    Assert.Equal(Visibility.Collapsed, empty.Visibility);
                    var list = (System.Windows.Controls.StackPanel)window.FindName("MemberList");
                    var check = (System.Windows.Controls.CheckBox)list.Children[0];
                    check.IsChecked = true;
                    Assert.True(accept.IsEnabled);
                    var label = (System.Windows.Controls.DockPanel)check.Content;
                    var registration = label.Children.OfType<System.Windows.Controls.Button>().Single();
                    var npc = (System.Windows.Controls.MenuItem)registration.ContextMenu.Items[1];
                    npc.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.MenuItem.ClickEvent));
                    Assert.Empty(window.SelectedMembers);
                    Assert.False(accept.IsEnabled);
                    Assert.Equal(Visibility.Visible, empty.Visibility);
                }
                finally { window.Close(); }
            }
            catch (Exception exception) { captured = exception; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(10)));
        Assert.Null(captured);
    }

    [Fact]
    public void ログ除外ウィンドウを初期化してログを表示できる()
    {
        Exception? captured = null;
        var thread = new Thread(() =>
        {
            try
            {
                WpfTestApplication.Ensure();
                using var directory = new TemporaryDirectory();
                var viewModel = new FFXI_LogAnalyzer.App.LogExclusionViewModel([LogExclusionTests.CreateSession(directory.Path)]);
                var window = new FFXI_LogAnalyzer.App.LogExclusionWindow(viewModel);
                window.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
                window.Measure(new Size(1250, 850));
                window.Arrange(new Rect(0, 0, 1250, 850));
                window.UpdateLayout();
                var grid = (System.Windows.Controls.DataGrid)window.FindName("MainLogs");
                Assert.Equal(5, grid.Items.Count);
                window.Close();
            }
            catch (Exception exception)
            {
                captured = exception;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(10)));
        Assert.Null(captured);
    }

    [Fact]
    public void 過去ログ分析ウィンドウを初期化できる()
    {
        Exception? capturedException = null;
        string? title = null;
        var thread = new Thread(
            () =>
            {
                try
                {
                    WpfTestApplication.Ensure();
                    var window = new FFXI_LogAnalyzer.App.MainWindow();
                    title = window.Title;
                    window.Close();
                }
                catch (Exception exception)
                {
                    capturedException = exception;
                }
            });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(10)));

        Assert.Null(capturedException);
        Assert.Equal("LogRep2 - 過去ログ分析", title);
    }

    [Fact]
    public void オーバーレイを初期化してPT設定と移動サイズ変更を利用できる()
    {
        Exception? capturedException = null;
        var thread = new Thread(() =>
        {
            try
            {
                WpfTestApplication.Ensure();
                var window = new FfxiTempLogCollector.App.OverlayWindow();
                try
                {
                    Assert.True(window.AllowsTransparency);
                    Assert.Equal(WindowStyle.None, window.WindowStyle);
                    Assert.Equal(ResizeMode.CanResizeWithGrip, window.ResizeMode);
                    var dragSurface = (FrameworkElement)window.FindName("DragSurface");
                    Assert.Equal(InputCursors.Hand, dragSurface.Cursor);
                    var button = (System.Windows.Controls.Button)window.FindName("PartyMemberSettingsButton");
                    Assert.Equal("PTメンバー設定を開く",
                        System.Windows.Automation.AutomationProperties.GetName(button));
                }
                finally { window.CloseForShutdown(); }
            }
            catch (Exception exception) { capturedException = exception; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(10)), "画面の初期化が制限時間内に完了しませんでした。");
        Assert.Null(capturedException);
    }
}
