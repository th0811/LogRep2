using System.Text;
using FFXI_LogAnalyzer.App;
using FFXI_LogAnalyzer.Core;

namespace FfxiTempLogCollector.Tests;

public sealed class CsvExportServiceTests
{
    [Fact]
    public void 分析日時と区間名を含むCSVファイル名を生成する()
    {
        var time = new AnalysisTimeResult(
            TimeConfidence.Exact,
            630,
            new DateTimeOffset(2026, 7, 21, 20, 30, 15, TimeSpan.FromHours(9)),
            new DateTimeOffset(2026, 7, 21, 20, 40, 45, TimeSpan.FromHours(9)),
            []);

        var fileName = CsvExportFileNameBuilder.Build(
            "キャラクター別",
            time,
            null,
            "西ロンフォール");

        Assert.Equal(
            "LogRep2_キャラクター別_20260721_203015-204045_西ロンフォール.csv",
            fileName);
    }

    [Fact]
    public void 分析時刻不明時はセッション開始日を使用して禁止文字を置換する()
    {
        var fileName = CsvExportFileNameBuilder.Build(
            "アクション別",
            AnalysisTimeResult.Unknown([]),
            new DateTimeOffset(2026, 7, 21, 10, 0, 0, TimeSpan.FromHours(9)),
            "エリア:テスト/滞在2");

        Assert.Equal(
            "LogRep2_アクション別_20260721_エリア_テスト_滞在2.csv",
            fileName);
    }

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
