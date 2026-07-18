using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using InputCursors = System.Windows.Input.Cursors;

namespace FfxiTempLogCollector.App;

public partial class OverlayWindow : Window
{
    private bool _allowClose;

    public OverlayWindow()
    {
        InitializeComponent();
        Closing += OnClosing;
        ResizeMode = ResizeMode.CanResizeWithGrip;
        DragSurface.Cursor = InputCursors.Hand;
    }

    public void CloseForShutdown()
    {
        _allowClose = true;
        Close();
    }

    private void OnDragSurfaceMouseLeftButtonDown(
        object sender,
        MouseButtonEventArgs eventArgs)
    {
        if (eventArgs.ButtonState == MouseButtonState.Pressed)
        {
            try
            {
                Mouse.OverrideCursor = InputCursors.SizeAll;
                DragMove();
            }
            finally
            {
                Mouse.OverrideCursor = null;
            }
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
