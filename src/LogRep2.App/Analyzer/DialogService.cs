using System.Windows;

namespace FFXI_LogAnalyzer.App;

public sealed class DialogService
{
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

    public string? SelectCsvOutputPath(string defaultFileName)
    {
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Title = "CSVファイルの保存先を選択してください。",
            Filter = "CSVファイル (*.csv)|*.csv",
            DefaultExt = ".csv",
            AddExtension = true,
            FileName = defaultFileName,
        };

        return dialog.ShowDialog() == true
            ? dialog.FileName
            : null;
    }

    public bool ConfirmSessionDeletion(string sessionId)
    {
        return System.Windows.MessageBox.Show(
            $"セッション「{sessionId}」を削除しますか？"
            + Environment.NewLine
            + "削除したフォルダーはごみ箱へ移動します。",
            "セッションの削除",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning) == MessageBoxResult.Yes;
    }
}
