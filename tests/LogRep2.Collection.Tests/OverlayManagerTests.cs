using System.Windows.Threading;
using FfxiTempLogCollector.App;
using FfxiTempLogCollector.Core;
using LogRep2.Infrastructure;

namespace FfxiTempLogCollector.Tests;

public sealed class OverlayManagerTests
{
    [Fact]
    public void ShowとHide_表示中だけリアルタイム更新を購読する()
    {
        using var directory = new TemporaryDirectory();
        new LogRep2SettingsStore(directory.Path).Save(new LogRep2Settings());
        Exception? capturedException = null;
        var subscribedWhileVisible = false;
        var subscribedWhileHidden = true;
        var thread = new Thread(() =>
        {
            try
            {
                var events = new CollectorEvents();
                var realtime = new RealtimeAnalysisController(events, 500);
                using var manager = new OverlayManager(
                    new LogRep2SettingsStore(directory.Path),
                    realtime,
                    Dispatcher.CurrentDispatcher,
                    _ => { });

                manager.Show();
                subscribedWhileVisible = manager.IsRealtimeUpdateSubscribed;
                manager.Hide();
                subscribedWhileHidden = manager.IsRealtimeUpdateSubscribed;
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
        Assert.True(subscribedWhileVisible);
        Assert.False(subscribedWhileHidden);
    }
}
