using System.Text;
using FFXI_LogAnalyzer.App;

namespace FfxiTempLogCollector.Tests;

public sealed class CsvExportServiceTests
{
    [Fact]
    public void Excel向けUTF8BOM付きCSVを正しく出力する()
    {
        using var directory = new TemporaryDirectory();
        var path = directory.GetPath("result.csv");

        CsvExportService.Write(
            path,
            ["名前", "値"],
            [
                ["Alice", "1,234"],
                ["引用符", "\"テスト\""],
            ]);

        var bytes = File.ReadAllBytes(path);
        var text = File.ReadAllText(path, Encoding.UTF8);
        Assert.Equal([0xEF, 0xBB, 0xBF], bytes[..3]);
        Assert.Contains("Alice,\"1,234\"\r\n", text);
        Assert.Contains("引用符,\"\"\"テスト\"\"\"\r\n", text);
    }

    [Theory]
    [InlineData("C:\\sessions", "C:\\sessions\\session-1", true)]
    [InlineData("C:\\sessions", "C:\\sessions", false)]
    [InlineData("C:\\sessions", "C:\\other\\session-1", false)]
    [InlineData("C:\\sessions", "C:\\sessions\\group\\session-1", false)]
    public void セッション出力先直下だけを削除対象にする(
        string root,
        string target,
        bool expected)
    {
        Assert.Equal(
            expected,
            MainViewModel.IsDirectChildOfSessionRoot(root, target));
    }
}
