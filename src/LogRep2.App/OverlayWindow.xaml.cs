using System.ComponentModel;
using System.Windows;
using System.Windows.Input;

namespace FfxiTempLogCollector.App;

public partial class OverlayWindow : Window
{
    private bool _allowClose;

    public OverlayWindow()
    {
        InitializeComponent();
        Closing += OnClosing;
    }

    public void ApplyEditingState(bool isEditing)
    {
        ResizeMode = isEditing
            ? ResizeMode.CanResizeWithGrip
            : ResizeMode.NoResize;
    }

    public void CloseForShutdown()
    {
        _allowClose = true;
        Close();
    }

    private void OnDragAreaMouseLeftButtonDown(
        object sender,
        MouseButtonEventArgs eventArgs)
    {
        if (eventArgs.ButtonState == MouseButtonState.Pressed
            && DataContext is OverlayViewModel { IsEditing: true })
        {
            DragMove();
        }
    }

    private void OnClosing(object? sender, CancelEventArgs eventArgs)
    {
        if (!_allowClose)
        {
            eventArgs.Cancel = true;
            Hide();
        }
    }
}
