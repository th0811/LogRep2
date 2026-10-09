using FfxiTempLogCollector.Core;

namespace FfxiTempLogCollector.Tests;

public sealed class TempLogPollerTests
{
    [Fact]
    public void 初回は存在するファイルだけ処理対象になる()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var existingPath = temporaryDirectory.GetPath("1_0.log");
        var missingPath = temporaryDirectory.GetPath("1_1.log");
        File.WriteAllBytes(
            existingPath,
            TempLogTestFileBuilder.Create("初回"));
        var poller = new TempLogPoller();

        var actual = poller.Poll([existingPath, missingPath]);

        var snapshot = Assert.Single(actual.ChangedFiles);
        Assert.Equal("1_0.log", snapshot.FileName);
        Assert.Empty(actual.Errors);
    }

    [Fact]
    public void 変更されていないファイルを再処理しない()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var path = temporaryDirectory.GetPath("1_0.log");
        File.WriteAllBytes(
            path,
            TempLogTestFileBuilder.Create("変更なし"));
        var poller = new TempLogPoller();

        var first = poller.Poll([path]);
        var second = poller.Poll([path]);

        Assert.Single(first.ChangedFiles);
        Assert.Empty(second.ChangedFiles);
    }

    [Fact]
    public void 内容が更新されたファイルを再処理する()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var path = temporaryDirectory.GetPath("1_0.log");
        File.WriteAllBytes(
            path,
            TempLogTestFileBuilder.Create("更新前"));
        var poller = new TempLogPoller();
        var first = poller.Poll([path]);

        File.WriteAllBytes(
            path,
            TempLogTestFileBuilder.Create("更新後の長いメッセージ"));
        File.SetLastWriteTimeUtc(
            path,
            DateTime.UtcNow.AddSeconds(2));
        var second = poller.Poll([path]);

        Assert.Single(first.ChangedFiles);
        Assert.Single(second.ChangedFiles);
        Assert.NotEqual(
            first.ChangedFiles[0].FileHash,
            second.ChangedFiles[0].FileHash);
    }

}
