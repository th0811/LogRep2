using System.Text;
using FFXI_LogAnalyzer.App;

namespace FfxiTempLogCollector.Tests;

public sealed class AssistantToolLauncherTests
{
    [Fact]
    public void JSONLを埋め込んだ固定名の起動HTMLを生成する()
    {
        using var directory = new TemporaryDirectory();
        var toolDirectory = directory.GetPath("AssistantTool");
        var sessionDirectory = directory.GetPath("session");
        Directory.CreateDirectory(toolDirectory);
        Directory.CreateDirectory(sessionDirectory);
        File.WriteAllText(
            Path.Combine(toolDirectory, "index.html"),
            """
            <!doctype html>
            <script src="app.js"></script>
            """,
            Encoding.UTF8);
        var jsonl = """
                    {"visible_text":"日本語</script>を含むログ"}

                    """;
        File.WriteAllText(
            Path.Combine(sessionDirectory, "raw_records.jsonl"),
            jsonl,
            new UTF8Encoding(false));
        string? openedPath = null;
        var launcher = new AssistantToolLauncher(
            toolDirectory,
            path => openedPath = path);

        var result = launcher.Launch(sessionDirectory);

        var launchPath = Path.Combine(toolDirectory, "launch.html");
        Assert.True(result.IsSuccess);
        Assert.Equal(launchPath, openedPath);
        Assert.Equal(launchPath, result.LaunchPath);
        var html = File.ReadAllText(launchPath, Encoding.UTF8);
        var marker = """data-file-name="raw_records.jsonl">""";
        var base64Start = html.IndexOf(marker, StringComparison.Ordinal);
        Assert.True(base64Start >= 0);
        base64Start += marker.Length;
        var base64End = html.IndexOf(
            "</script>",
            base64Start,
            StringComparison.Ordinal);
        var decoded = Encoding.UTF8.GetString(
            Convert.FromBase64String(html[base64Start..base64End]));
        Assert.Equal(jsonl, decoded);
    }

    [Fact]
    public void RawRecordsがない場合は分かりやすいエラーを返す()
    {
        using var directory = new TemporaryDirectory();
        var toolDirectory = directory.GetPath("AssistantTool");
        var sessionDirectory = directory.GetPath("session");
        Directory.CreateDirectory(toolDirectory);
        Directory.CreateDirectory(sessionDirectory);
        var launcher = new AssistantToolLauncher(toolDirectory, _ => { });

        var result = launcher.Launch(sessionDirectory);

        Assert.False(result.IsSuccess);
        Assert.Contains("raw_records.jsonlが見つかりません", result.Message);
    }

    [Fact]
    public void AssistantToolがない場合は配布ファイルの確認を促す()
    {
        using var directory = new TemporaryDirectory();
        var toolDirectory = directory.GetPath("AssistantTool");
        var sessionDirectory = directory.GetPath("session");
        Directory.CreateDirectory(sessionDirectory);
        File.WriteAllText(
            Path.Combine(sessionDirectory, "raw_records.jsonl"),
            "{}\n");
        var launcher = new AssistantToolLauncher(toolDirectory, _ => { });

        var result = launcher.Launch(sessionDirectory);

        Assert.False(result.IsSuccess);
        Assert.Contains("AssistantToolが見つかりません", result.Message);
    }
}
