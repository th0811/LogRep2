using System.Windows;
using System.Windows.Forms;

namespace FFXI_LogAnalyzer.App;

public sealed class DialogService
{
    public string? SelectSessionFolder()
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = "セッションフォルダを選択してください。",
            UseDescriptionForTitle = true
        };

        return dialog.ShowDialog() == DialogResult.OK
            ? dialog.SelectedPath
            : null;
    }

    public string? SelectSessionsRootFolder()
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = "セッション出力先フォルダを選択してください。",
            UseDescriptionForTitle = true
        };

        return dialog.ShowDialog() == DialogResult.OK
            ? dialog.SelectedPath
            : null;
    }

    public bool ConfirmWarnings(IReadOnlyList<string> warnings)
    {
        var message = string.Join(Environment.NewLine, warnings) +
            Environment.NewLine +
            Environment.NewLine +
            "このセッションを読み込みますか？";

        return System.Windows.MessageBox.Show(
            message,
            "セッション警告",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning) == MessageBoxResult.Yes;
    }

    public void ShowError(string message)
    {
        System.Windows.MessageBox.Show(
            message,
            "読み込みエラー",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
    }

    public void ShowInformation(string message)
    {
        System.Windows.MessageBox.Show(
            message,
            "情報",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }
}
