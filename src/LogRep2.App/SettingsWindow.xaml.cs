using System.Windows;
using System.Windows.Input;

namespace FfxiTempLogCollector.App;

public enum SettingsFocusTarget
{
    None,
    TempDirectory,
    OutputDirectory,
}

public partial class SettingsWindow : Window
{
    private readonly SettingsFocusTarget _focusTarget;

    public SettingsWindow(SettingsFocusTarget focusTarget = SettingsFocusTarget.None)
    {
        _focusTarget = focusTarget;
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs eventArgs)
    {
        var textBox = _focusTarget switch
        {
            SettingsFocusTarget.TempDirectory => TempDirectoryTextBox,
            SettingsFocusTarget.OutputDirectory => OutputDirectoryTextBox,
            _ => null,
        };

        if (textBox is null)
        {
            return;
        }

        textBox.Focus();
        textBox.SelectAll();
    }

    private void OnPreviewKeyDown(
        object sender,
        System.Windows.Input.KeyEventArgs eventArgs)
    {
        if (eventArgs.Key != Key.Escape)
        {
            return;
        }

        eventArgs.Handled = true;
        Close();
    }
}
