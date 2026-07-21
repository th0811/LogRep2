using System.Drawing;
using System.Windows;
using FfxiTempLogCollector.Core;
using Forms = System.Windows.Forms;

namespace FfxiTempLogCollector.App;

public sealed class TrayIconController : IDisposable
{
    private const string ApplicationName = "LogRep2";

    private readonly Window _window;
    private readonly CollectorService _collectorService;
    private readonly Func<CollectorConfig> _configProvider;
    private readonly TrayMenuController _menuController;
    private readonly Forms.NotifyIcon _notifyIcon;
    private readonly Icon? _iconStopped;
    private readonly Icon? _iconRunning;
    private readonly Icon? _iconError;
    private readonly System.Windows.Media.ImageSource? _windowIconStopped;
    private readonly System.Windows.Media.ImageSource? _windowIconRunning;
    private readonly System.Windows.Media.ImageSource? _windowIconError;
    private CollectorStatus _previousStatus;
    private bool _disposed;

    public TrayIconController(
        Window window,
        CollectorService collectorService,
        Func<CollectorConfig> configProvider,
        TrayMenuController menuController)
    {
        _window = window
            ?? throw new ArgumentNullException(nameof(window));
        _collectorService = collectorService
            ?? throw new ArgumentNullException(nameof(collectorService));
        _configProvider = configProvider
            ?? throw new ArgumentNullException(nameof(configProvider));
        _menuController = menuController
            ?? throw new ArgumentNullException(nameof(menuController));

        _previousStatus = collectorService.GetStatus().Status;
        _iconStopped = TryLoadIcon("app.ico");
        _iconRunning = TryLoadIcon("app-running.ico");
        _iconError = TryLoadIcon("app-error.ico");
        _windowIconStopped = TryLoadImageSource("app.ico");
        _windowIconRunning = TryLoadImageSource("app-running.ico");
        _windowIconError = TryLoadImageSource("app-error.ico");
        _notifyIcon = new Forms.NotifyIcon
        {
            ContextMenuStrip = menuController.ContextMenu,
        };
        ApplyStatus(_previousStatus);
        _notifyIcon.Visible = true;
        _notifyIcon.DoubleClick += OnDoubleClick;
        _collectorService.Events.StatusChanged += OnStatusChanged;
    }

    public void ShowWindow()
    {
        _window.Show();
        _window.WindowState = WindowState.Normal;
        _window.Activate();
    }

    public void MoveWindowToTray()
    {
        _window.Hide();

        if (_configProvider().ShowTrayNotifications)
        {
            ShowNotification(
                "タスクトレイに格納しました。",
                Forms.ToolTipIcon.Info);
        }
    }

    public void NotifyOutputFailure(string message)
    {
        if (_configProvider().ShowTrayNotifications)
        {
            ShowNotification(message, Forms.ToolTipIcon.Error);
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _collectorService.Events.StatusChanged -= OnStatusChanged;
        _notifyIcon.DoubleClick -= OnDoubleClick;
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        _iconStopped?.Dispose();
        _iconRunning?.Dispose();
        _iconError?.Dispose();
    }

    // 収集状態に応じて、タスクトレイのアイコン/ツールチップと、
    // ウィンドウのアイコン（タイトルバーおよびタスクバーのボタン）を切り替える。
    // アイコンの形状は全状態で共通のため、色だけが手掛かりにならないよう
    // ツールチップにも状態を出して色覚特性に依存せず判別できるようにする。
    private void ApplyStatus(CollectorStatus status)
    {
        // 停止処理中もまだ収集しているため収集中と同じ扱いにする。
        // Starting はまだ収集していないため停止中と同じ扱いにする。
        var icon = status switch
        {
            CollectorStatus.Running or CollectorStatus.Stopping => _iconRunning,
            CollectorStatus.Error => _iconError,
            _ => _iconStopped
        };
        var windowIcon = status switch
        {
            CollectorStatus.Running or CollectorStatus.Stopping => _windowIconRunning,
            CollectorStatus.Error => _windowIconError,
            _ => _windowIconStopped
        };

        _notifyIcon.Icon = icon ?? _iconStopped ?? SystemIcons.Application;
        _notifyIcon.Text = $"{ApplicationName} - {ToStatusText(status)}";

        if (windowIcon is not null)
        {
            _window.Icon = windowIcon;
        }
    }

    private static string ToStatusText(CollectorStatus status)
    {
        return status switch
        {
            CollectorStatus.Running => "収集中",
            CollectorStatus.Starting => "開始中",
            CollectorStatus.Stopping => "停止処理中",
            CollectorStatus.Error => "エラー",
            _ => "停止中"
        };
    }

    // 状態別アイコンをWPFのWindow.Icon用に読み込む。
    // アイコンデコーダ経由で読むことで、タイトルバー(16px)とタスクバー(32px)に
    // 適したフレームをWPF側が選択できるようにする。
    private static System.Windows.Media.ImageSource? TryLoadImageSource(
        string resourceName)
    {
        try
        {
            var frame = System.Windows.Media.Imaging.BitmapFrame.Create(
                new Uri($"pack://application:,,,/LogRep2;component/{resourceName}"));
            frame.Freeze();
            return frame;
        }
        catch
        {
            return null;
        }
    }

    // 状態別アイコンをタスクトレイ用に読み込む。
    // NotifyIconはApplicationIconやWindow.Iconを継承しないため明示的に設定する。
    private static Icon? TryLoadIcon(string resourceName)
    {
        try
        {
            // エントリアセンブリに依存しないようアセンブリ修飾したpack URIを使う。
            var resource = System.Windows.Application.GetResourceStream(
                new Uri($"pack://application:,,,/LogRep2;component/{resourceName}"));
            if (resource is null)
            {
                return null;
            }

            using var stream = resource.Stream;
            // トレイ表示に適した小サイズのフレームを選ばせる（DPIに応じて16/24等）。
            return new Icon(stream, Forms.SystemInformation.SmallIconSize);
        }
        catch
        {
            return null;
        }
    }

    private void OnDoubleClick(object? sender, EventArgs eventArgs)
    {
        _window.Dispatcher.Invoke(ShowWindow);
    }

    private void OnStatusChanged(
        object? sender,
        CollectorStatusSnapshot snapshot)
    {
        _window.Dispatcher.BeginInvoke(
            () =>
            {
                _menuController.UpdateStatus(snapshot);
                ApplyStatus(snapshot.Status);
                ShowStatusNotification(snapshot);
                _previousStatus = snapshot.Status;
            });
    }

    private void ShowStatusNotification(
        CollectorStatusSnapshot snapshot)
    {
        if (!_configProvider().ShowTrayNotifications
            || snapshot.Status == _previousStatus)
        {
            return;
        }

        if (snapshot.Status == CollectorStatus.Running)
        {
            ShowNotification(
                "ログ収集を開始しました。",
                Forms.ToolTipIcon.Info);
        }
        else if (snapshot.Status == CollectorStatus.Stopped
                 && _previousStatus is CollectorStatus.Running
                     or CollectorStatus.Stopping)
        {
            ShowNotification(
                "ログ収集を停止しました。",
                Forms.ToolTipIcon.Info);
        }
        else if (snapshot.Status == CollectorStatus.Error)
        {
            ShowNotification(
                string.IsNullOrWhiteSpace(snapshot.LastError)
                    ? "重大エラーが発生しました。"
                    : snapshot.LastError,
                Forms.ToolTipIcon.Error);
        }
    }

    private void ShowNotification(
        string message,
        Forms.ToolTipIcon icon)
    {
        _notifyIcon.ShowBalloonTip(
            3000,
            ApplicationName,
            message,
            icon);
    }
}
