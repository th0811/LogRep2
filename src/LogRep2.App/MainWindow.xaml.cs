using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using MessageBox = System.Windows.MessageBox;

namespace FfxiTempLogCollector.App;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;
    private readonly GitHubReleaseUpdateService _updateService;
    private readonly bool _checkForUpdatesOnLaunch;
    private bool _shutdownCompleted;
    private bool _updateCheckStarted;

    internal MainWindow(
        MainViewModel viewModel,
        GitHubReleaseUpdateService updateService,
        bool checkForUpdatesOnLaunch)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        ArgumentNullException.ThrowIfNull(updateService);

        InitializeComponent();
        _viewModel = viewModel;
        _updateService = updateService;
        _checkForUpdatesOnLaunch = checkForUpdatesOnLaunch;
        DataContext = viewModel;
    }

    private async void OnLoaded(
        object sender,
        RoutedEventArgs eventArgs)
    {
        await _viewModel.InitializeAsync();
        if (_checkForUpdatesOnLaunch)
        {
            await CheckForUpdateAsync();
        }
    }

    private async Task CheckForUpdateAsync()
    {
        if (_updateCheckStarted)
        {
            return;
        }

        _updateCheckStarted = true;
        try
        {
            var result = await _updateService.CheckAsync();
            if (result is null || !result.IsUpdateAvailable)
            {
                return;
            }

            var answer = MessageBox.Show(
                this,
                $"新しいバージョン {result.LatestVersion.ToString(3)}"
                + " が公開されています。"
                + Environment.NewLine
                + $"現在のバージョン: {result.CurrentVersion.ToString(3)}"
                + Environment.NewLine
                + Environment.NewLine
                + "GitHub Releasesを開いて更新内容を確認しますか？",
                "LogRep2 アップデート",
                MessageBoxButton.YesNo,
                MessageBoxImage.Information);

            if (answer == MessageBoxResult.Yes)
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = result.ReleaseUri.AbsoluteUri,
                    UseShellExecute = true,
                });
            }
        }
        catch (Exception exception)
        {
            DiagnosticLogService.Write(
                "アップデート確認エラー",
                exception);
        }
    }

    private void OnStateChanged(
        object? sender,
        EventArgs eventArgs)
    {
        _viewModel.HandleWindowStateChanged();
    }

    private async void OnClosing(
        object? sender,
        CancelEventArgs eventArgs)
    {
        if (_shutdownCompleted)
        {
            return;
        }

        var closeAction = _viewModel.GetCloseAction();

        if (closeAction == WindowCloseAction.MoveToTray)
        {
            eventArgs.Cancel = true;
            _viewModel.MinimizeToTray();
            return;
        }

        if (closeAction == WindowCloseAction.Cancel)
        {
            eventArgs.Cancel = true;
            return;
        }

        eventArgs.Cancel = true;
        _viewModel.BeginShutdown();
        Cursor = System.Windows.Input.Cursors.Wait;
        await Dispatcher.Yield(DispatcherPriority.Render);
        IsEnabled = false;

        try
        {
            await _viewModel.ShutdownAsync();
        }
        catch (Exception exception)
        {
            MessageBox.Show(
                this,
                $"終了処理中にエラーが発生しました。\n{exception.Message}",
                "終了エラー",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        finally
        {
            Cursor = null;
            _shutdownCompleted = true;
            Close();
        }
    }
}
