using FfxiTempLogCollector.Core;

namespace FfxiTempLogCollector.Tests;

public sealed class CollectorServiceTests
{
    [Fact]
    public async Task 手動完了は過去のみ変更し現在の収集と開始停止処理を保護する()
    {
        using var directory = new TemporaryDirectory();
        var config = CreateConfig(directory);
        Directory.CreateDirectory(config.TempDir);
        var past = directory.GetPath("past");
        Directory.CreateDirectory(past);
        var path = Path.Combine(past, "session.json");
        File.WriteAllText(path, "{\"status\":\"active\",\"ended_at\":null,\"custom\":42}");
        await using var service = new CollectorService();
        var protectedStarting = false;
        var protectedStopping = false;
        service.Events.StatusChanged += (_, state) =>
        {
            if (state.Status == CollectorStatus.Starting)
                protectedStarting = !service.CanCompleteSessionManually(past);
            if (state.Status == CollectorStatus.Stopping)
                protectedStopping = !service.CanCompleteSessionManually(state.SessionDirectory!);
        };
        Assert.True(await service.StartAsync(new CollectorStartRequest { Config = config }));
        var current = service.GetStatus().SessionDirectory!;
        var original = File.ReadAllText(Path.Combine(current, "session.json"));
        Assert.False(service.CanCompleteSessionManually(Path.Combine(current, ".")));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CompleteSessionManuallyAsync(current));
        Assert.Equal(original, File.ReadAllText(Path.Combine(current, "session.json")));
        Assert.True(service.CanCompleteSessionManually(past));
        await service.CompleteSessionManuallyAsync(past);
        var json = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(path))!;
        Assert.Equal("completed", json["status"]!.GetValue<string>());
        Assert.Null(json["ended_at"]);
        Assert.Equal(42, json["custom"]!.GetValue<int>());
        Assert.NotNull(json["manually_completed_at"]);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CompleteSessionManuallyAsync(past));
        Assert.Equal(CollectorStatus.Running, service.GetStatus().Status);
        await service.StopAsync();
        Assert.True(protectedStarting);
        Assert.True(protectedStopping);
    }

    [Fact]
    public async Task 手動完了は終了時刻を保持し不正ファイルを変更しない()
    {
        using var directory = new TemporaryDirectory();
        var path = directory.GetPath("session.json");
        await using var service = new CollectorService();
        File.WriteAllText(path, "{\"status\":\"active\",\"ended_at\":\"2026-01-01T00:00:00+09:00\"}");
        await service.CompleteSessionManuallyAsync(directory.Path);
        Assert.Contains("2026-01-01T00:00:00", File.ReadAllText(path));
        File.WriteAllText(path, "不正なJSON");
        await Assert.ThrowsAsync<InvalidDataException>(() => service.CompleteSessionManuallyAsync(directory.Path));
        Assert.Equal("不正なJSON", File.ReadAllText(path));
    }

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
