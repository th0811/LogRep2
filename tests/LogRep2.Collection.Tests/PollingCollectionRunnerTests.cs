using FfxiTempLogCollector.Core;

namespace FfxiTempLogCollector.Tests;

public sealed class PollingCollectionRunnerTests
{
    [Theory]
    [InlineData(false, 10, false)]
    [InlineData(true, 4, false)]
    [InlineData(true, 5, true)]
    [InlineData(true, 10, true)]
    public void 変更がある場合だけ5秒間隔でチェックポイント保存する(
        bool hasChanges,
        int elapsedSeconds,
        bool expected)
    {
        var startedAt = new DateTimeOffset(
            2026,
            1,
            1,
            0,
            0,
            0,
            TimeSpan.Zero);

        var actual = PollingCollectionRunner.ShouldSaveCheckpoint(
            hasChanges,
            startedAt,
            startedAt.AddSeconds(elapsedSeconds));

        Assert.Equal(expected, actual);
    }

    [Fact]
    public async Task 開始前のログを除外してSessionをCompletedにする()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var tempDirectory = temporaryDirectory.GetPath("TEMP");
        var outputDirectory = temporaryDirectory.GetPath("sessions");
        Directory.CreateDirectory(tempDirectory);
        File.WriteAllBytes(
            Path.Combine(tempDirectory, "1_0.log"),
            TempLogTestFileBuilder.Create("停止テスト"));
        var config = CreateConfig(tempDirectory, outputDirectory);
        using var cancellation = new CancellationTokenSource();
        cancellation.CancelAfter(TimeSpan.FromMilliseconds(400));

        var actual = await new PollingCollectionRunner().RunAsync(
            config,
            new PollingOptions { IntervalMs = 250 },
            cancellation.Token);

        Assert.True(actual.PollCount >= 1);
        Assert.Equal(0, actual.RawRecordsWritten);
        Assert.Equal(0, actual.CanonicalRecordsWritten);

        var session = new SessionManager().Load(
            actual.SessionDirectory);
        Assert.Equal(SessionStatus.Completed, session.Status);
    }

    [Fact]
    public async Task 開始後に更新されたファイルの新規レコードだけ保存する()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var tempDirectory = temporaryDirectory.GetPath("TEMP");
        var outputDirectory = temporaryDirectory.GetPath("sessions");
        Directory.CreateDirectory(tempDirectory);
        var logPath = Path.Combine(tempDirectory, "1_0.log");
        File.WriteAllBytes(
            logPath,
            TempLogTestFileBuilder.CreateMany("開始前の既存レコード"));
        var config = CreateConfig(tempDirectory, outputDirectory);
        using var cancellation = new CancellationTokenSource();
        var runnerTask = new PollingCollectionRunner().RunAsync(
            config,
            new PollingOptions { IntervalMs = 250 },
            cancellation.Token);

        var sessionDirectory = await WaitForSessionDirectoryAsync(
            outputDirectory);
        var rawPath = Path.Combine(
            sessionDirectory,
            RawRecordJsonlWriter.FileName);

        File.WriteAllBytes(
            logPath,
            TempLogTestFileBuilder.CreateMany(
                "開始前の既存レコード",
                "開始後の新規レコード"));
        File.SetLastWriteTimeUtc(
            logPath,
            DateTime.UtcNow.AddSeconds(2));

        await WaitUntilAsync(
            () => File.Exists(rawPath)
                && File.ReadAllLines(rawPath).Length == 1);
        cancellation.Cancel();

        var actual = await runnerTask;

        Assert.Equal(1, actual.FilesProcessed);
        Assert.Equal(1, actual.RawRecordsWritten);
        Assert.Single(File.ReadAllLines(rawPath));
        Assert.Empty(actual.Errors);
    }

    [Fact]
    public async Task 停止中に更新されたログは再開後のSessionへ出力しない()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var tempDirectory = temporaryDirectory.GetPath("TEMP");
        Directory.CreateDirectory(tempDirectory);
        var logPath = Path.Combine(tempDirectory, "1_0.log");
        File.WriteAllBytes(
            logPath,
            TempLogTestFileBuilder.Create("開始前"));
        var runner = new PollingCollectionRunner();

        var first = await RunBrieflyAsync(
            runner,
            CreateConfig(
                tempDirectory,
                temporaryDirectory.GetPath("sessions-1")));
        Assert.Equal(0, first.RawRecordsWritten);

        File.WriteAllBytes(
            logPath,
            TempLogTestFileBuilder.Create("停止中の更新"));
        File.SetLastWriteTimeUtc(
            logPath,
            DateTime.UtcNow.AddSeconds(2));

        var second = await RunBrieflyAsync(
            runner,
            CreateConfig(
                tempDirectory,
                temporaryDirectory.GetPath("sessions-2")));

        Assert.Equal(0, second.RawRecordsWritten);
        Assert.Equal(0, second.CanonicalRecordsWritten);
    }

    [Fact]
    public async Task ベースラインと同じレコードが別スロットへ移動しても出力しない()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var tempDirectory = temporaryDirectory.GetPath("TEMP");
        var outputDirectory = temporaryDirectory.GetPath("sessions");
        Directory.CreateDirectory(tempDirectory);
        var bytes = TempLogTestFileBuilder.Create("ローテーション済み");
        File.WriteAllBytes(
            Path.Combine(tempDirectory, "1_0.log"),
            bytes);
        var config = CreateConfig(tempDirectory, outputDirectory);
        config.RotationSlots = 2;
        using var cancellation = new CancellationTokenSource();
        var runnerTask = new PollingCollectionRunner().RunAsync(
            config,
            new PollingOptions { IntervalMs = 250 },
            cancellation.Token);

        await WaitForSessionDirectoryAsync(outputDirectory);
        File.WriteAllBytes(
            Path.Combine(tempDirectory, "1_1.log"),
            bytes);
        await Task.Delay(400);
        cancellation.Cancel();

        var actual = await runnerTask;

        Assert.Equal(0, actual.RawRecordsWritten);
        Assert.Equal(0, actual.CanonicalRecordsWritten);
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
            PollingIntervalMs = 250,
        };
    }

    private static async Task<string> WaitForSessionDirectoryAsync(
        string outputDirectory)
    {
        string? sessionDirectory = null;
        await WaitUntilAsync(
            () =>
            {
                sessionDirectory = Directory.Exists(outputDirectory)
                    ? Directory.GetDirectories(outputDirectory)
                        .SingleOrDefault()
                    : null;
                return sessionDirectory is not null;
            });

        return sessionDirectory!;
    }

    private static async Task WaitUntilAsync(
        Func<bool> condition,
        int timeoutMs = 5000)
    {
        var startedAt = DateTime.UtcNow;

        while (!condition())
        {
            if ((DateTime.UtcNow - startedAt).TotalMilliseconds > timeoutMs)
            {
                throw new TimeoutException(
                    "テスト条件が期限内に成立しませんでした。");
            }

            await Task.Delay(25);
        }
    }

    private static async Task<PollingCollectionResult> RunBrieflyAsync(
        PollingCollectionRunner runner,
        CollectorConfig config)
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.CancelAfter(TimeSpan.FromMilliseconds(400));
        return await runner.RunAsync(
            config,
            new PollingOptions { IntervalMs = 250 },
            cancellation.Token);
    }
}
