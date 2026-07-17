using System.Text.Json;
using FfxiTempLogCollector.Core;

namespace FfxiTempLogCollector.Tests;

public sealed class OnceCollectionRunnerTests
{
    private static readonly DateTimeOffset StartedAt =
        new(
            2026,
            6,
            23,
            21,
            30,
            0,
            TimeSpan.FromHours(9));

    [Fact]
    public void 対象ファイルだけ処理して各出力を生成する()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var tempDirectory = temporaryDirectory.GetPath("TEMP");
        var outputDirectory = temporaryDirectory.GetPath("sessions");
        Directory.CreateDirectory(tempDirectory);
        File.WriteAllBytes(
            Path.Combine(tempDirectory, "1_0.log"),
            TempLogTestFileBuilder.Create("[21:30] ###start:test"));
        File.WriteAllBytes(
            Path.Combine(tempDirectory, "ignored.log"),
            TempLogTestFileBuilder.Create("対象外"));
        var config = CreateConfig(tempDirectory, outputDirectory);

        var actual = CreateRunner().Run(config);

        Assert.Equal(1, actual.TargetFiles);
        Assert.Equal(1, actual.FilesRead);
        Assert.Equal(0, actual.MissingFiles);
        Assert.Equal(1, actual.RawRecordsWritten);
        Assert.Equal(1, actual.CanonicalRecordsWritten);

        var rawPath = Path.Combine(
            actual.SessionDirectory,
            RawRecordJsonlWriter.FileName);
        var canonicalPath = Path.Combine(
            actual.SessionDirectory,
            CanonicalRecordJsonlWriter.FileName);
        Assert.True(File.Exists(rawPath));
        Assert.True(File.Exists(canonicalPath));
        Assert.Single(File.ReadAllLines(rawPath));
        Assert.Single(File.ReadAllLines(canonicalPath));

        using var rawDocument = JsonDocument.Parse(
            File.ReadAllText(rawPath));
        var rawRoot = rawDocument.RootElement;
        Assert.Equal(
            "1_0.log",
            rawRoot.GetProperty("source_file").GetString());
        Assert.Equal(
            "21:30",
            rawRoot.GetProperty("message_time_text").GetString());
        Assert.True(rawRoot.GetProperty("is_marker").GetBoolean());
        Assert.Equal(
            "start:test",
            rawRoot.GetProperty("marker_keyword").GetString());
    }

    [Fact]
    public void 存在しない対象ファイルがあっても完了する()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var tempDirectory = temporaryDirectory.GetPath("TEMP");
        var outputDirectory = temporaryDirectory.GetPath("sessions");
        Directory.CreateDirectory(tempDirectory);
        File.WriteAllBytes(
            Path.Combine(tempDirectory, "1_0.log"),
            TempLogTestFileBuilder.Create("存在するログ"));
        var config = CreateConfig(tempDirectory, outputDirectory);
        config.RotationSlots = 2;

        var actual = CreateRunner().Run(config);

        Assert.Equal(2, actual.TargetFiles);
        Assert.Equal(1, actual.FilesRead);
        Assert.Equal(1, actual.MissingFiles);
        Assert.Empty(actual.Errors);
    }

    [Fact]
    public void SessionをCompletedにしてStateとStatsを保存する()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var tempDirectory = temporaryDirectory.GetPath("TEMP");
        var outputDirectory = temporaryDirectory.GetPath("sessions");
        Directory.CreateDirectory(tempDirectory);
        File.WriteAllBytes(
            Path.Combine(tempDirectory, "1_0.log"),
            TempLogTestFileBuilder.Create("収集テスト"));
        var config = CreateConfig(tempDirectory, outputDirectory);

        var actual = CreateRunner().Run(config);

        var session = new SessionManager().Load(
            actual.SessionDirectory);
        var state = new StateStore().Load(
            actual.SessionDirectory);
        var stats = new StatsStore().Load(
            actual.SessionDirectory);

        Assert.Equal(SessionStatus.Completed, session.Status);
        Assert.NotNull(session.EndedAt);
        Assert.Equal("20260623-213000", session.SessionId);
        Assert.Equal(session.SessionId, state.SessionId);
        Assert.True(state.Files["1_0.log"].Exists);
        Assert.Single(state.SeenRawRecordIds);
        Assert.Single(state.SeenCanonicalKeys);
        Assert.Equal(1, state.LastOrder);
        Assert.Equal(1, stats.RawRecordsWritten);
        Assert.Equal(1, stats.CanonicalRecordsWritten);
        Assert.NotNull(stats.LastSeenAt);
    }

    [Fact]
    public void 同一イベントを別ウィンドウから読んだ場合Canonicalを統合する()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var tempDirectory = temporaryDirectory.GetPath("TEMP");
        var outputDirectory = temporaryDirectory.GetPath("sessions");
        Directory.CreateDirectory(tempDirectory);
        var bytes = TempLogTestFileBuilder.Create("同一イベント");
        File.WriteAllBytes(Path.Combine(tempDirectory, "1_0.log"), bytes);
        File.WriteAllBytes(Path.Combine(tempDirectory, "2_0.log"), bytes);
        var config = CreateConfig(tempDirectory, outputDirectory);
        config.WatchWindow2 = true;

        var actual = CreateRunner().Run(config);

        Assert.Equal(2, actual.RawRecordsWritten);
        Assert.Equal(1, actual.CanonicalRecordsWritten);

        var canonicalPath = Path.Combine(
            actual.SessionDirectory,
            CanonicalRecordJsonlWriter.FileName);
        using var document = JsonDocument.Parse(
            File.ReadAllText(canonicalPath));
        var sourceWindows = document.RootElement
            .GetProperty("source_windows")
            .EnumerateArray()
            .Select(element => element.GetInt32())
            .ToArray();
        Assert.Equal([1, 2], sourceWindows);
    }

    [Fact]
    public void 初回収集は更新日時の古いファイルから処理する()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var tempDirectory = temporaryDirectory.GetPath("TEMP");
        var outputDirectory = temporaryDirectory.GetPath("sessions");
        Directory.CreateDirectory(tempDirectory);
        var newerPath = Path.Combine(tempDirectory, "1_0.log");
        var olderPath = Path.Combine(tempDirectory, "1_1.log");
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
        var config = CreateConfig(tempDirectory, outputDirectory);
        config.RotationSlots = 2;

        var actual = CreateRunner().Run(config);

        var rawPath = Path.Combine(
            actual.SessionDirectory,
            RawRecordJsonlWriter.FileName);
        var sourceFiles = File.ReadLines(rawPath)
            .Select(
                line => JsonDocument.Parse(line)
                    .RootElement.GetProperty("source_file")
                    .GetString())
            .ToArray();

        Assert.Equal(["1_1.log", "1_0.log"], sourceFiles);
    }

    [Fact]
    public void 更新日時が同じ場合はファイル名順で処理する()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var tempDirectory = temporaryDirectory.GetPath("TEMP");
        var outputDirectory = temporaryDirectory.GetPath("sessions");
        Directory.CreateDirectory(tempDirectory);
        var firstPath = Path.Combine(tempDirectory, "1_0.log");
        var secondPath = Path.Combine(tempDirectory, "1_1.log");
        File.WriteAllBytes(
            firstPath,
            TempLogTestFileBuilder.Create("最初"));
        File.WriteAllBytes(
            secondPath,
            TempLogTestFileBuilder.Create("次"));
        var sameTime =
            new DateTime(2026, 6, 23, 10, 0, 0, DateTimeKind.Utc);
        File.SetLastWriteTimeUtc(firstPath, sameTime);
        File.SetLastWriteTimeUtc(secondPath, sameTime);
        var config = CreateConfig(tempDirectory, outputDirectory);
        config.RotationSlots = 2;

        var actual = CreateRunner().Run(config);

        var rawPath = Path.Combine(
            actual.SessionDirectory,
            RawRecordJsonlWriter.FileName);
        var sourceFiles = File.ReadLines(rawPath)
            .Select(
                line => JsonDocument.Parse(line)
                    .RootElement.GetProperty("source_file")
                    .GetString())
            .ToArray();

        Assert.Equal(["1_0.log", "1_1.log"], sourceFiles);
    }

    private static CollectorConfig CreateConfig(
        string tempDirectory,
        string outputDirectory)
    {
        return new CollectorConfig
        {
            TempDir = tempDirectory,
            OutputDir = outputDirectory,
            WatchWindow1 = true,
            WatchWindow2 = false,
            RotationSlots = 1,
        };
    }

    private static OnceCollectionRunner CreateRunner()
    {
        var current = StartedAt;

        return new OnceCollectionRunner(
            clock: () =>
            {
                var value = current;
                current = current.AddSeconds(1);
                return value;
            });
    }
}
