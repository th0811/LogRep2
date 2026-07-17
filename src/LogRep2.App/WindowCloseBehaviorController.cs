using System.Windows;
using FfxiTempLogCollector.Core;
using MessageBox = System.Windows.MessageBox;

namespace FfxiTempLogCollector.App;

public enum WindowCloseAction
{
    Exit,
    MoveToTray,
    Cancel,
}

public sealed class WindowCloseBehaviorController
{
    private readonly Window _window;
    private readonly Func<CollectorConfig> _configProvider;
    private readonly Func<CollectorStatusSnapshot> _statusProvider;

    public WindowCloseBehaviorController(
        Window window,
        Func<CollectorConfig> configProvider,
        Func<CollectorStatusSnapshot> statusProvider)
    {
        _window = window
            ?? throw new ArgumentNullException(nameof(window));
        _configProvider = configProvider
            ?? throw new ArgumentNullException(nameof(configProvider));
        _statusProvider = statusProvider
            ?? throw new ArgumentNullException(nameof(statusProvider));
    }

    public WindowCloseAction GetCloseAction(bool exitRequested)
    {
        if (exitRequested)
        {
            return WindowCloseAction.Exit;
        }

        var behavior = NormalizeCloseBehavior(
            _configProvider().CloseButtonBehavior);

        return behavior switch
        {
            "always_tray" => WindowCloseAction.MoveToTray,
            "tray_when_collecting" => IsCollecting(
                _statusProvider().Status)
                ? WindowCloseAction.MoveToTray
                : WindowCloseAction.Exit,
            "confirm_exit" => ConfirmExit()
                ? WindowCloseAction.Exit
                : WindowCloseAction.Cancel,
            _ => WindowCloseAction.Exit,
        };
    }

    public bool ShouldMoveMinimizedWindowToTray()
    {
        return NormalizeMinimizeBehavior(
            _configProvider().MinimizeButtonBehavior) == "tray";
    }

    public static string NormalizeMinimizeBehavior(string behavior)
    {
        return behavior switch
        {
            "tray" => "tray",
            _ => "normal",
        };
    }

    public static string NormalizeCloseBehavior(string behavior)
    {
        return behavior switch
        {
            "tray" => "always_tray",
            "always_tray" => "always_tray",
            "exit" => "confirm_exit",
            "confirm_exit" => "confirm_exit",
            _ => "tray_when_collecting",
        };
    }

    private bool ConfirmExit()
    {
        return MessageBox.Show(
            _window,
            "ログ収集アプリを終了しますか？",
            "終了確認",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question) == MessageBoxResult.Yes;
    }

    private static bool IsCollecting(CollectorStatus status)
    {
        return status is CollectorStatus.Starting
            or CollectorStatus.Running
            or CollectorStatus.Stopping;
    }
}
