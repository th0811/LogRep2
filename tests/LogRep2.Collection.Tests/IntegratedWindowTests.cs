using System.Windows;
using InputCursors = System.Windows.Input.Cursors;

namespace FfxiTempLogCollector.Tests;

public sealed class IntegratedWindowTests
{
    [Fact]
    public void PT出力メンバー選択が共通スタイルで初期化でき選択状態を維持する()
    {
        Exception? captured = null;
        var thread = new Thread(() =>
        {
            try
            {
                WpfTestApplication.Ensure();
                var window = new FFXI_LogAnalyzer.App.TimelineMemberSelectionWindow([("Alice", "PC登録"), ("Boro", "PC候補")]);
                try
                {
                    window.Measure(new Size(480, 360));
                    window.Arrange(new Rect(0, 0, 480, 360));
                    window.UpdateLayout();
                    var button = (System.Windows.Controls.Button)window.FindName("AcceptButton");
                    var list = (System.Windows.Controls.StackPanel)window.FindName("MemberList");
                    Assert.Same(window.FindResource("PrimaryButtonStyle"), button.Style);
                    Assert.False(button.IsEnabled);
                    var check = (System.Windows.Controls.CheckBox)list.Children[0];
                    Assert.Same(window.FindResource("AppCheckBoxStyle"), check.Style);
                    check.IsChecked = true;
                    Assert.True(button.IsEnabled);
                    Assert.Equal("Alice", Assert.Single(window.SelectedMembers));
                    check.IsChecked = false;
                    Assert.False(button.IsEnabled);
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
    public void オーバーレイウィンドウを初期化できる()
    {
        Exception? capturedException = null;
        var allowsTransparency = false;
        var windowStyle = WindowStyle.SingleBorderWindow;
        var thread = new Thread(() =>
        {
            try
            {
                WpfTestApplication.Ensure();
                var window = new FfxiTempLogCollector.App.OverlayWindow();
                allowsTransparency = window.AllowsTransparency;
                windowStyle = window.WindowStyle;
                window.CloseForShutdown();
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
        Assert.True(allowsTransparency);
        Assert.Equal(WindowStyle.None, windowStyle);
    }

    [Fact]
    public void オーバーレイにPT設定ボタンを常時配置する()
    {
        Exception? capturedException = null;
        string? accessibleName = null;
        var thread = new Thread(() =>
        {
            try
            {
                WpfTestApplication.Ensure();
                var window = new FfxiTempLogCollector.App.OverlayWindow();
                var button = (System.Windows.Controls.Button)window.FindName(
                    "PartyMemberSettingsButton");
                accessibleName = System.Windows.Automation.AutomationProperties
                    .GetName(button);
                window.CloseForShutdown();
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
        Assert.Equal("PTメンバー設定を開く", accessibleName);
    }

    [Fact]
    public void オーバーレイは常時移動とサイズ変更が可能である()
    {
        Exception? capturedException = null;
        System.Windows.Input.Cursor? cursor = null;
        var resizeMode = ResizeMode.NoResize;
        var thread = new Thread(() =>
        {
            try
            {
                WpfTestApplication.Ensure();
                var window = new FfxiTempLogCollector.App.OverlayWindow();
                var dragSurface = (FrameworkElement)window.FindName("DragSurface");

                cursor = dragSurface.Cursor;
                resizeMode = window.ResizeMode;
                window.CloseForShutdown();
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
        Assert.Equal(InputCursors.Hand, cursor);
        Assert.Equal(ResizeMode.CanResizeWithGrip, resizeMode);
    }
}
