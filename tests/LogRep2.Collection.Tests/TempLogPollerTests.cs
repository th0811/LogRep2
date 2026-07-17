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

    [Fact]
    public void 存在しないファイルでも例外終了しない()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var path = temporaryDirectory.GetPath("1_0.log");
        var poller = new TempLogPoller();

        var exception = Record.Exception(() => poller.Poll([path]));
        var actual = poller.Poll([path]);

        Assert.Null(exception);
        Assert.Empty(actual.ChangedFiles);
        Assert.Empty(actual.Errors);
    }

    [Fact]
    public void 同一ポーリング内の変更ファイルを更新日時順で返す()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var newerPath = temporaryDirectory.GetPath("1_0.log");
        var olderPath = temporaryDirectory.GetPath("1_1.log");
        File.WriteAllBytes(
            newerPath,
            TempLogTestFileBuilder.Create("新しいログ"));
        File.WriteAllBytes(
            olderPath,
            TempLogTestFileBuilder.Create("古いログ"));
        File.SetLastWriteTimeUtc(
            olderPath,
            new DateTime(2026, 6, 23, 10, 0, 0, DateTimeKind.Utc));
        File.SetLastWriteTimeUtc(
            newerPath,
            new DateTime(2026, 6, 23, 11, 0, 0, DateTimeKind.Utc));
        var poller = new TempLogPoller();

        var actual = poller.Poll([newerPath, olderPath]);

        Assert.Equal(
            ["1_1.log", "1_0.log"],
            actual.ChangedFiles.Select(file => file.FileName));
    }

    [Fact]
    public void 更新日時が同じ変更ファイルをファイル名順で返す()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var secondPath = temporaryDirectory.GetPath("1_1.log");
        var firstPath = temporaryDirectory.GetPath("1_0.log");
        File.WriteAllBytes(
            secondPath,
            TempLogTestFileBuilder.Create("次"));
        File.WriteAllBytes(
            firstPath,
            TempLogTestFileBuilder.Create("最初"));
        var sameTime =
            new DateTime(2026, 6, 23, 10, 0, 0, DateTimeKind.Utc);
        File.SetLastWriteTimeUtc(firstPath, sameTime);
        File.SetLastWriteTimeUtc(secondPath, sameTime);
        var poller = new TempLogPoller();

        var actual = poller.Poll([secondPath, firstPath]);

        Assert.Equal(
            ["1_0.log", "1_1.log"],
            actual.ChangedFiles.Select(file => file.FileName));
    }
}
