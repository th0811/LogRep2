using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using MessageBox = System.Windows.MessageBox;

namespace FfxiTempLogCollector.App;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;
    private bool _shutdownCompleted;

    public MainWindow(MainViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);

        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
    }

    private async void OnLoaded(
        object sender,
        RoutedEventArgs eventArgs)
    {
        await _viewModel.InitializeAsync();
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
