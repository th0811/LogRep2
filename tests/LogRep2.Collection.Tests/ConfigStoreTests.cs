using FfxiTempLogCollector.Core;

namespace FfxiTempLogCollector.Tests;

public sealed class ConfigStoreTests
{
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

}
