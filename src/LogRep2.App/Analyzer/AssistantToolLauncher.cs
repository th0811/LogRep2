using System.Diagnostics;
using System.IO;
using System.Text;

namespace FFXI_LogAnalyzer.App;

public sealed class AssistantToolLauncher
{
    private const string RawRecordsFileName = "raw_records.jsonl";
    private const string AppScriptTag = """<script src="app.js"></script>""";
    private readonly string _assistantToolDirectory;
    private readonly Action<string> _openBrowser;

    public AssistantToolLauncher()
        : this(
            Path.Combine(AppContext.BaseDirectory, "AssistantTool"),
            OpenWithDefaultBrowser)
    {
    }

    internal AssistantToolLauncher(
        string assistantToolDirectory,
        Action<string> openBrowser)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(assistantToolDirectory);
        ArgumentNullException.ThrowIfNull(openBrowser);
        _assistantToolDirectory = assistantToolDirectory;
        _openBrowser = openBrowser;
    }

    public AssistantToolLaunchResult Launch(string sessionFolderPath)
    {
        if (string.IsNullOrWhiteSpace(sessionFolderPath))
        {
            return AssistantToolLaunchResult.Failure(
                "セッションフォルダーが選択されていません。");
        }

        var rawRecordsPath = Path.Combine(sessionFolderPath, RawRecordsFileName);
        if (!File.Exists(rawRecordsPath))
        {
            return AssistantToolLaunchResult.Failure(
                $"ゲーム内ログを表示できません。{RawRecordsFileName}が見つかりません: {rawRecordsPath}");
        }

        var templatePath = Path.Combine(_assistantToolDirectory, "index.html");
        if (!File.Exists(templatePath))
        {
            return AssistantToolLaunchResult.Failure(
                $"AssistantToolが見つかりません。配布ファイルを確認してください: {templatePath}");
        }

        var launchPath = Path.Combine(_assistantToolDirectory, "launch.html");

        try
        {
            var template = File.ReadAllText(templatePath, Encoding.UTF8);
            if (!template.Contains(AppScriptTag, StringComparison.Ordinal))
            {
                return AssistantToolLaunchResult.Failure(
                    $"AssistantToolのテンプレート形式を確認できませんでした: {templatePath}");
            }

            var embeddedData = Convert.ToBase64String(ReadSnapshot(rawRecordsPath));
            var dataElement =
                $"""
                 <script id="logrep2EmbeddedData"
                         type="application/octet-stream"
                         data-file-name="{RawRecordsFileName}">{embeddedData}</script>
                   {AppScriptTag}
                 """;
            var launchHtml = template.Replace(
                AppScriptTag,
                dataElement,
                StringComparison.Ordinal);

            File.WriteAllText(
                launchPath,
                launchHtml,
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            _openBrowser(launchPath);
            return AssistantToolLaunchResult.Success(launchPath);
        }
        catch (UnauthorizedAccessException exception)
        {
            return AssistantToolLaunchResult.Failure(
                $"AssistantToolの起動ファイルを作成できませんでした。"
                + $"書き込み先: {launchPath} "
                + $"AssistantToolフォルダーへの書き込み権限を確認してください。"
                + $"詳細: {exception.Message}");
        }
        catch (IOException exception)
        {
            return AssistantToolLaunchResult.Failure(
                $"ゲーム内ログのスナップショットを作成できませんでした。"
                + $"対象: {rawRecordsPath} 詳細: {exception.Message}");
        }
        catch (Exception exception)
        {
            return AssistantToolLaunchResult.Failure(
                $"AssistantToolを起動できませんでした。詳細: {exception.Message}");
        }
    }

    private static byte[] ReadSnapshot(string path)
    {
        using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete);
        using var memory = new MemoryStream();
        stream.CopyTo(memory);
        return memory.ToArray();
    }

    private static void OpenWithDefaultBrowser(string path)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = path,
            UseShellExecute = true,
        });
    }
}

public sealed record AssistantToolLaunchResult(
    bool IsSuccess,
    string Message,
    string? LaunchPath)
{
    public static AssistantToolLaunchResult Success(string launchPath)
    {
        return new AssistantToolLaunchResult(
            true,
            "ゲーム内ログを既定のブラウザで開きました。ボタンを押した時点のスナップショットを表示しています。",
            launchPath);
    }

    public static AssistantToolLaunchResult Failure(string message)
    {
        return new AssistantToolLaunchResult(false, message, null);
    }
}
