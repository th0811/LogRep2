using System.Windows;

namespace FfxiTempLogCollector.Tests;

public sealed class IntegratedWindowTests
{
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
}
