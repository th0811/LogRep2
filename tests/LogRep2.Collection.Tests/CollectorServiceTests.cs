using FfxiTempLogCollector.Core;

namespace FfxiTempLogCollector.Tests;

public sealed class CollectorServiceTests
{
    [Fact]
    public async Task StartでRunningになりStopでStoppedになる()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var config = CreateConfig(temporaryDirectory);
        Directory.CreateDirectory(config.TempDir);
        await using var service = new CollectorService();

        var started = await service.StartAsync(
            new CollectorStartRequest { Config = config });

        Assert.True(started);
        Assert.Equal(
            CollectorStatus.Running,
            service.GetStatus().Status);

        await service.StopAsync(new CollectorStopRequest());

        Assert.Equal(
            CollectorStatus.Stopped,
            service.GetStatus().Status);
    }

    [Fact]
    public async Task Stop時にSessionがCompletedになる()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var config = CreateConfig(temporaryDirectory);
        Directory.CreateDirectory(config.TempDir);
        await using var service = new CollectorService();
        Assert.True(await service.StartAsync(
            new CollectorStartRequest { Config = config }));
        var sessionDirectory = service.GetStatus().SessionDirectory;

        await service.StopAsync();

        Assert.NotNull(sessionDirectory);
        var session = new SessionManager().Load(sessionDirectory);
        Assert.Equal(SessionStatus.Completed, session.Status);
    }

    [Fact]
    public async Task 二重Startを拒否する()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var config = CreateConfig(temporaryDirectory);
        Directory.CreateDirectory(config.TempDir);
        await using var service = new CollectorService();

        Assert.True(await service.StartAsync(
            new CollectorStartRequest { Config = config }));
        Assert.False(await service.StartAsync(
            new CollectorStartRequest { Config = config }));
    }

    [Fact]
    public async Task Stopped状態でStopしても落ちない()
    {
        await using var service = new CollectorService();

        await service.StopAsync();

        Assert.Equal(
            CollectorStatus.Stopped,
            service.GetStatus().Status);
    }

    [Fact]
    public async Task 設定不備時はErrorと失敗結果を返す()
    {
        await using var service = new CollectorService();

        var started = await service.StartAsync(
            new CollectorStartRequest
            {
                Config = new CollectorConfig(),
            });

        Assert.False(started);
        var status = service.GetStatus();
        Assert.Equal(CollectorStatus.Error, status.Status);
        Assert.NotNull(status.LastError);
    }

    [Fact]
    public async Task ポーリング間隔とログレベルを即時変更できる()
    {
        await using var service = new CollectorService();
        string? changedLogLevel = null;
        service.Events.LogLevelChanged +=
            (_, logLevel) => changedLogLevel = logLevel;

        service.UpdatePollingInterval(500);
        service.UpdateLogLevel("debug");

        var status = service.GetStatus();
        Assert.Equal(500, status.PollingIntervalMs);
        Assert.Equal("debug", status.LogLevel);
        Assert.Equal("debug", changedLogLevel);
    }

    [Fact]
    public async Task Once実行で収集してStoppedに戻る()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var config = CreateConfig(temporaryDirectory);
        Directory.CreateDirectory(config.TempDir);
        File.WriteAllBytes(
            Path.Combine(config.TempDir, "1_0.log"),
            TempLogTestFileBuilder.Create("onceテスト"));
        await using var service = new CollectorService();

        var result = service.RunOnce(
            new CollectorStartRequest { Config = config });

        Assert.Equal(1, result.RawRecordsWritten);
        var status = service.GetStatus();
        Assert.Equal(CollectorStatus.Stopped, status.Status);
        Assert.Equal(result.SessionId, status.SessionId);
        Assert.Equal(1, status.RawRecordsWritten);
    }

    private static CollectorConfig CreateConfig(
        TemporaryDirectory temporaryDirectory)
    {
        return new CollectorConfig
        {
            TempDir = temporaryDirectory.GetPath("TEMP"),
            OutputDir = temporaryDirectory.GetPath("sessions"),
            WatchWindow1 = true,
            WatchWindow2 = false,
            RotationSlots = 1,
            PollingIntervalMs = 250,
        };
    }
}
