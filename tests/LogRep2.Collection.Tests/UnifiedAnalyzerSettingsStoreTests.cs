using FFXI_LogAnalyzer.App;
using LogRep2.Infrastructure;

namespace FfxiTempLogCollector.Tests;

public sealed class UnifiedAnalyzerSettingsStoreTests
{
    [Fact]
    public void 選択状態を再読込でき別セッションの状態と他設定を保持する()
    {
        using var directory = new TemporaryDirectory();
        var store = new AnalyzerSettingsStore(directory.Path);
        var first = directory.GetPath("sessions/first");
        var second = directory.GetPath("sessions/second");
        store.SaveSessionSelections([
            new() { FolderPath = first, SessionId = "1", IsEnabled = false },
            new() { FolderPath = second, SessionId = "2", IsEnabled = false },
        ]);
        store.Save(new AnalyzerSettings { KnownPcNames = ["Xitra"] });
        store.SaveSessionSelections([
            new() { FolderPath = first.ToUpperInvariant() + "\\", SessionId = "1", IsEnabled = true },
        ]);

        var actual = new AnalyzerSettingsStore(directory.Path).Load();

        Assert.Equal(["Xitra"], actual.KnownPcNames);
        Assert.Equal(2, actual.SessionSelections.Count);
        Assert.True(actual.SessionSelections.Single(state => state.Matches(first, "1")).IsEnabled);
        Assert.False(actual.SessionSelections.Single(state => state.Matches(second, "2")).IsEnabled);
        Assert.DoesNotContain(actual.SessionSelections, state => state.Matches(first, "別のID"));
        Assert.DoesNotContain(actual.SessionSelections, state => state.Matches(second, "1"));
    }

    [Fact]
    public void 全解除と空の更新で保存済み状態を失わない()
    {
        using var directory = new TemporaryDirectory();
        var store = new AnalyzerSettingsStore(directory.Path);
        store.SaveSessionSelections([
            new() { FolderPath = directory.GetPath("first"), SessionId = "1", IsEnabled = false },
            new() { FolderPath = directory.GetPath("second"), SessionId = "2", IsEnabled = false },
        ]);
        store.SaveSessionSelections([]);

        var actual = new AnalyzerSettingsStore(directory.Path).Load();

        Assert.Equal(2, actual.SessionSelections.Count);
        Assert.All(actual.SessionSelections, state => Assert.False(state.IsEnabled));
    }

    [Fact]
    public void 選択状態がない既存設定を読み込める()
    {
        using var directory = new TemporaryDirectory();
        File.WriteAllText(directory.GetPath(LogRep2SettingsStore.FileName),
            """{"schema_version":1,"analysis":{"known_pc_names":["Xitra"]}}""");

        var actual = new AnalyzerSettingsStore(directory.Path).Load();

        Assert.Empty(actual.SessionSelections);
        Assert.Equal(["Xitra"], actual.KnownPcNames);
    }

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
                KnownNpcNames = ["gOBLIN"],
            });
        var reloaded = unifiedStore.Load();

        Assert.Equal(
            "shared-sessions",
            reloaded.Collection.OutputDirectory);
        Assert.Equal(["Xitra"], reloaded.Analysis.KnownPcNames);
        Assert.Equal(["Goblin"], reloaded.Analysis.KnownNpcNames);
    }
}

