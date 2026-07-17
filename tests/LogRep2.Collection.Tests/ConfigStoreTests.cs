using FfxiTempLogCollector.Core;

namespace FfxiTempLogCollector.Tests;

public sealed class ConfigStoreTests
{
    [Fact]
    public void デフォルト設定を生成できる()
    {
        var config = new CollectorConfig();

        Assert.Equal(string.Empty, config.TempDir);
        Assert.Equal("sessions", config.OutputDir);
        Assert.Equal("cp932", config.Encoding);
        Assert.Equal(1000, config.PollingIntervalMs);
        Assert.True(config.WatchWindow1);
        Assert.True(config.WatchWindow2);
        Assert.Equal(20, config.RotationSlots);
        Assert.True(config.RawOutput);
        Assert.True(config.CanonicalOutput);
        Assert.True(config.DedupeRaw);
        Assert.True(config.DedupeCanonical);
        Assert.True(config.MarkerDetection);
        Assert.Equal("###", config.MarkerPrefix);
        Assert.Equal("Asia/Tokyo", config.Timezone);
        Assert.Equal(1000, config.FlushIntervalMs);
        Assert.Equal("sha1", config.HashAlgorithm);
        Assert.Equal("info", config.LogLevel);
        Assert.False(config.AutoStartCollectionOnLaunch);
        Assert.False(config.MinimizeToTrayWhileCollecting);
        Assert.Equal("tray", config.MinimizeButtonBehavior);
        Assert.Equal("tray_when_collecting", config.CloseButtonBehavior);
        Assert.True(config.ShowTrayNotifications);
    }

    [Fact]
    public void ConfigJsonを保存して読み込める()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var path = temporaryDirectory.GetPath("config.json");
        var store = new ConfigStore(temporaryDirectory.Path);
        var expected = new CollectorConfig
        {
            TempDir = @"C:\FFXI\TEMP",
            OutputDir = @"D:\FFXI\Sessions",
            PollingIntervalMs = 500,
            WatchWindow2 = false,
        };

        store.Save(expected, path);
        var actual = store.Load(path);

        Assert.Equal(expected.TempDir, actual.TempDir);
        Assert.Equal(expected.OutputDir, actual.OutputDir);
        Assert.Equal(expected.PollingIntervalMs, actual.PollingIntervalMs);
        Assert.Equal(expected.WatchWindow2, actual.WatchWindow2);

        var json = File.ReadAllText(path);
        Assert.Contains("\"temp_dir\"", json, StringComparison.Ordinal);
        Assert.Contains("\"polling_interval_ms\"", json, StringComparison.Ordinal);
    }

    [Fact]
    public void 標準保存先は実行ディレクトリ直下になる()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var applicationDirectory =
            temporaryDirectory.GetPath("application");
        var store = new ConfigStore(applicationDirectory);

        Assert.Equal(
            Path.Combine(applicationDirectory, "config.json"),
            store.DefaultPath);
    }
}
