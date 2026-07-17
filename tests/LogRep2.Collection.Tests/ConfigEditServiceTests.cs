using FfxiTempLogCollector.App;
using FfxiTempLogCollector.Core;

namespace FfxiTempLogCollector.Tests;

public sealed class ConfigEditServiceTests
{
    [Theory]
    [InlineData(249)]
    [InlineData(5001)]
    public async Task ポーリング間隔の範囲外入力を拒否する(
        int intervalMs)
    {
        using var temporaryDirectory = new TemporaryDirectory();
        await using var collectorService = new CollectorService();
        var service = new ConfigEditService(
            new ConfigStore(temporaryDirectory.Path),
            collectorService);
        var config = CreateConfig(temporaryDirectory);
        config.PollingIntervalMs = intervalMs;

        var result = service.Validate(config);

        Assert.False(result.Success);
        Assert.Contains("250～5000ms", result.Message);
    }

    [Fact]
    public async Task 保存した設定をConfigStoreから再読込できる()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var store = new ConfigStore(temporaryDirectory.Path);
        await using var collectorService = new CollectorService();
        var service = new ConfigEditService(
            store,
            collectorService);
        var current = CreateConfig(temporaryDirectory);
        var edited = ConfigEditService.Clone(current);
        edited.PollingIntervalMs = 500;
        edited.LogLevel = "debug";
        edited.WatchWindow2 = true;

        var result = service.Save(current, edited);
        var reloaded = store.Load();

        Assert.True(result.Success);
        Assert.Equal(500, reloaded.PollingIntervalMs);
        Assert.Equal("debug", reloaded.LogLevel);
        Assert.True(reloaded.WatchWindow2);
        Assert.Equal(500, collectorService.GetStatus().PollingIntervalMs);
        Assert.Equal("debug", collectorService.GetStatus().LogLevel);
    }

    [Fact]
    public async Task Tempフォルダーが空の場合は警告にする()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        await using var collectorService = new CollectorService();
        var service = new ConfigEditService(
            new ConfigStore(temporaryDirectory.Path),
            collectorService);
        var config = CreateConfig(temporaryDirectory);
        config.TempDir = string.Empty;

        var result = service.Validate(config);

        Assert.True(result.Success);
        Assert.True(result.HasWarning);
        Assert.Contains("TEMPフォルダーが空です", result.Message);
    }

    [Fact]
    public async Task マーカー文字列が空なら検証エラーにする()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        await using var collectorService = new CollectorService();
        var service = new ConfigEditService(
            new ConfigStore(temporaryDirectory.Path),
            collectorService);
        var config = CreateConfig(temporaryDirectory);
        config.MarkerPrefix = string.Empty;

        var result = service.Validate(config);

        Assert.False(result.Success);
        Assert.Contains("マーカー文字列", result.Message);
    }

    [Fact]
    public async Task 収集中の次回反映設定変更は説明を返す()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var current = CreateConfig(temporaryDirectory);
        Directory.CreateDirectory(current.TempDir);
        var store = new ConfigStore(temporaryDirectory.Path);
        await using var collectorService = new CollectorService();
        var service = new ConfigEditService(
            store,
            collectorService);
        Assert.True(await collectorService.StartAsync(
            new CollectorStartRequest
            {
                Config = ConfigEditService.Clone(current),
            }));
        var edited = ConfigEditService.Clone(current);
        edited.RawOutput = false;
        edited.PollingIntervalMs = 500;

        var result = service.Save(current, edited);

        Assert.True(result.Success);
        Assert.True(result.RequiresNextCollection);
        Assert.Equal(
            "この設定は現在の収集停止後、次回収集開始時に反映されます。",
            result.Message);
        Assert.Equal(
            500,
            collectorService.GetStatus().PollingIntervalMs);
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
            PollingIntervalMs = 1000,
        };
    }
}
