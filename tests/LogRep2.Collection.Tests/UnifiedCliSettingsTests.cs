using FfxiTempLogCollector.App;
using FfxiTempLogCollector.Core;
using LogRep2.Infrastructure;

namespace FfxiTempLogCollector.Tests;

public sealed class UnifiedCliSettingsTests
{
    [Fact]
    public async Task CLIの既定設定操作は統合設定を使用する()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var unifiedStore = new LogRep2SettingsStore(
            temporaryDirectory.Path);
        unifiedStore.LoadOrMigrate();
        var legacyStore = new ConfigStore(temporaryDirectory.Path);
        var output = new StringWriter();
        await using var controller = new CliCommandController(
            legacyStore,
            new ConfigLoader(legacyStore, temporaryDirectory.Path),
            new CollectorService(),
            new CliOutputWriter(output, new StringWriter()),
            unifiedSettingsStore: unifiedStore);

        var setResult = await controller.ExecuteAsync(
            new CliCommand
            {
                Kind = CliCommandKind.ConfigSet,
                ConfigKey = "polling_interval_ms",
                ConfigValue = "500",
            });
        var getResult = await controller.ExecuteAsync(
            new CliCommand
            {
                Kind = CliCommandKind.ConfigGet,
                ConfigKey = "polling_interval_ms",
            });
        var pathResult = await controller.ExecuteAsync(
            new CliCommand { Kind = CliCommandKind.ConfigPath });

        Assert.Equal(CliExitCode.Success, setResult);
        Assert.Equal(CliExitCode.Success, getResult);
        Assert.Equal(CliExitCode.Success, pathResult);
        Assert.Equal(
            500,
            unifiedStore.Load().Collection.PollingIntervalMs);
        Assert.False(File.Exists(legacyStore.DefaultPath));
        Assert.Contains("500", output.ToString());
        Assert.Contains(unifiedStore.SettingsPath, output.ToString());
    }
}

