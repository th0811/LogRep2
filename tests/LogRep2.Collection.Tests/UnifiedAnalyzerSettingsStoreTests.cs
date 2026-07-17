using FFXI_LogAnalyzer.App;
using LogRep2.Infrastructure;

namespace FfxiTempLogCollector.Tests;

public sealed class UnifiedAnalyzerSettingsStoreTests
{
    [Fact]
    public void 分析設定は収集出力先をセッションルートとして使う()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var unifiedStore = new LogRep2SettingsStore(
            temporaryDirectory.Path);
        var unified = new LogRep2Settings();
        unified.Collection.OutputDirectory = "shared-sessions";
        unifiedStore.Save(unified);
        var analyzerStore = new AnalyzerSettingsStore(
            temporaryDirectory.Path);

        var actual = analyzerStore.Load();

        Assert.Equal(
            temporaryDirectory.GetPath("shared-sessions"),
            actual.SessionsRootFolderPath);
    }

    [Fact]
    public void 分析設定保存で収集出力先を変更しない()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var unifiedStore = new LogRep2SettingsStore(
            temporaryDirectory.Path);
        var unified = new LogRep2Settings();
        unified.Collection.OutputDirectory = "shared-sessions";
        unifiedStore.Save(unified);
        var analyzerStore = new AnalyzerSettingsStore(
            temporaryDirectory.Path);

        analyzerStore.Save(
            new AnalyzerSettings
            {
                SessionsRootFolderPath = "temporary-selection",
                KnownPcNames = [" xitra "],
                KnownNpcNames = ["Goblin"],
            });
        var reloaded = unifiedStore.Load();

        Assert.Equal(
            "shared-sessions",
            reloaded.Collection.OutputDirectory);
        Assert.Equal(["Xitra"], reloaded.Analysis.KnownPcNames);
        Assert.Equal(["Goblin"], reloaded.Analysis.KnownNpcNames);
    }
}

