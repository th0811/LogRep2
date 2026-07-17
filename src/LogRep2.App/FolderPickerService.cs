using Microsoft.Win32;

namespace FfxiTempLogCollector.App;

public sealed class FolderPickerService
{
    public string? SelectFolder(
        string title,
        string? initialDirectory = null)
    {
        var dialog = new OpenFolderDialog
        {
            Title = title,
            Multiselect = false,
        };

        if (!string.IsNullOrWhiteSpace(initialDirectory))
        {
            dialog.InitialDirectory = initialDirectory;
        }

        return dialog.ShowDialog() == true
            ? dialog.FolderName
            : null;
    }
}
