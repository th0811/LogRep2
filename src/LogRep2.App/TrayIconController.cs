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
        _notifyIcon = new Forms.NotifyIcon
        {
            Text = ApplicationName,
            Icon = SystemIcons.Application,
            ContextMenuStrip = menuController.ContextMenu,
            Visible = true,
        };
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
