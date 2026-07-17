using FfxiTempLogCollector.Core;

namespace FfxiTempLogCollector.Tests;

public sealed class PollingCollectionRunnerTests
{
    [Fact]
    public async Task CancellationTokenで停止してSessionをCompletedにする()
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
        Assert.Equal(1, actual.RawRecordsWritten);
        Assert.Equal(1, actual.CanonicalRecordsWritten);

        var session = new SessionManager().Load(
            actual.SessionDirectory);
        Assert.Equal(SessionStatus.Completed, session.Status);
    }

    [Fact]
    public async Task 変更されたファイルだけ処理して上書き後の新規レコードを保存する()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var tempDirectory = temporaryDirectory.GetPath("TEMP");
        var outputDirectory = temporaryDirectory.GetPath("sessions");
        Directory.CreateDirectory(tempDirectory);
        var logPath = Path.Combine(tempDirectory, "1_0.log");
        File.WriteAllBytes(
            logPath,
            TempLogTestFileBuilder.Create("ローテーション前"));
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
        await WaitUntilAsync(
            () => File.Exists(rawPath)
                && File.ReadAllLines(rawPath).Length == 1);

        File.WriteAllBytes(
            logPath,
            TempLogTestFileBuilder.Create(
                "ローテーション後の別レコード"));
        File.SetLastWriteTimeUtc(
            logPath,
            DateTime.UtcNow.AddSeconds(2));

        await WaitUntilAsync(
            () => File.ReadAllLines(rawPath).Length == 2);
        cancellation.Cancel();

        var actual = await runnerTask;

        Assert.Equal(2, actual.FilesProcessed);
        Assert.Equal(2, actual.RawRecordsWritten);
        Assert.Equal(2, File.ReadAllLines(rawPath).Length);
        Assert.Empty(actual.Errors);
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
}
