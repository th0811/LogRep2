using System.Text.Json;
using FfxiTempLogCollector.Core;
using LogRep2.Infrastructure;

namespace FfxiTempLogCollector.Tests;

public sealed class LogRep2SettingsStoreTests
{
    [Fact]
    public void Save_オーバーレイ設定を安全範囲へ補正して保存する()
    {
        using var directory = new TemporaryDirectory();
        var store = new LogRep2SettingsStore(directory.Path);
        var settings = new LogRep2Settings();
        settings.Overlay.Opacity = 0.01;
        settings.Overlay.Width = 1;
        settings.Overlay.Height = 1;
        settings.Overlay.FontSize = 99;
        settings.Overlay.DisplayRowCount = 99;
        settings.Overlay.MonitorDeviceName = "DISPLAY2";

        store.Save(settings);
        var actual = store.Load().Overlay;

        Assert.Equal(0.25, actual.Opacity);
        Assert.Equal(280, actual.Width);
        Assert.Equal(180, actual.Height);
        Assert.Equal(40, actual.FontSize);
        Assert.Equal(30, actual.DisplayRowCount);
        Assert.Equal("DISPLAY2", actual.MonitorDeviceName);
    }

    [Fact]
    public void 初回読込でexe相当フォルダーへ統合設定を作成する()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var store = new LogRep2SettingsStore(temporaryDirectory.Path);

        var result = store.LoadOrMigrate();

        Assert.True(result.Created);
        Assert.Equal(
            temporaryDirectory.GetPath(LogRep2SettingsStore.FileName),
            store.SettingsPath);
        Assert.True(File.Exists(store.SettingsPath));
        Assert.Equal("sessions", result.Settings.Collection.OutputDirectory);
    }

    [Fact]
    public void 旧収集設定と旧分析設定を統合設定へ移行する()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var oldCollection = new CollectorConfig
        {
            TempDir = "TEMP",
            OutputDir = "old-sessions",
            PollingIntervalMs = 750,
            LogLevel = "debug",
        };
        new ConfigStore(temporaryDirectory.Path).Save(oldCollection);
        File.WriteAllText(
            temporaryDirectory.GetPath(
                LogRep2SettingsStore.LegacyAnalysisFileName),
            """
            {
              "sessions_root_folder_path": "ignored-sessions",
              "known_pc_names": ["Xitra"],
              "known_npc_names": ["Goblin"]
            }
            """);
        var store = new LogRep2SettingsStore(temporaryDirectory.Path);

        var result = store.LoadOrMigrate();
        var config = result.Settings.CreateCollectorConfig(
            temporaryDirectory.Path);

        Assert.True(result.MigratedCollectionSettings);
        Assert.True(result.MigratedAnalysisSettings);
        Assert.Empty(result.Warnings);
        Assert.Equal(750, config.PollingIntervalMs);
        Assert.Equal("debug", config.LogLevel);
        Assert.Equal(
            temporaryDirectory.GetPath("old-sessions"),
            config.OutputDir);
        Assert.Equal(["Xitra"], result.Settings.Analysis.KnownPcNames);
        Assert.Equal(["Goblin"], result.Settings.Analysis.KnownNpcNames);
        Assert.True(File.Exists(
            temporaryDirectory.GetPath(
                LogRep2SettingsStore.LegacyCollectionFileName)));
        Assert.True(File.Exists(
            temporaryDirectory.GetPath(
                LogRep2SettingsStore.LegacyAnalysisFileName)));
    }

    [Fact]
    public void 統合設定があれば旧設定で上書きしない()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var store = new LogRep2SettingsStore(temporaryDirectory.Path);
        var current = new LogRep2Settings();
        current.Collection.OutputDirectory = "current-sessions";
        store.Save(current);
        new ConfigStore(temporaryDirectory.Path).Save(
            new CollectorConfig { OutputDir = "legacy-sessions" });

        var result = store.LoadOrMigrate();

        Assert.False(result.Created);
        Assert.False(result.MigratedCollectionSettings);
        Assert.Equal(
            "current-sessions",
            result.Settings.Collection.OutputDirectory);
    }

    [Fact]
    public void 収集設定保存時に分析設定を維持する()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var store = new LogRep2SettingsStore(temporaryDirectory.Path);
        var settings = new LogRep2Settings();
        settings.Analysis.KnownPcNames = ["Xitra"];
        store.Save(settings);
        var config = store.LoadCollectorConfig();
        config.PollingIntervalMs = 500;

        store.SaveCollectorConfig(config);
        var reloaded = store.Load();

        Assert.Equal(500, reloaded.Collection.PollingIntervalMs);
        Assert.Equal(["Xitra"], reloaded.Analysis.KnownPcNames);
    }

    [Fact]
    public void 不正な統合設定を黙って初期化しない()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var store = new LogRep2SettingsStore(temporaryDirectory.Path);
        File.WriteAllText(store.SettingsPath, "{ invalid json");

        var exception = Assert.Throws<InvalidDataException>(store.Load);

        Assert.Contains("統合設定ファイルを読み込めません", exception.Message);
        Assert.Equal("{ invalid json", File.ReadAllText(store.SettingsPath));
    }

    [Fact]
    public void 統合設定はsnake_caseで保存する()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var store = new LogRep2SettingsStore(temporaryDirectory.Path);

        store.Save(new LogRep2Settings());
        using var document = JsonDocument.Parse(
            File.ReadAllText(store.SettingsPath));

        Assert.True(document.RootElement.TryGetProperty(
            "schema_version",
            out _));
        Assert.True(document.RootElement
            .GetProperty("collection")
            .TryGetProperty("output_directory", out _));
        Assert.True(document.RootElement
            .GetProperty("analysis")
            .TryGetProperty("realtime_refresh_interval_ms", out _));
    }

    [Fact]
    public void 保存不能時は対象パスを含む日本語エラーを返す()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var fileAsDirectory = temporaryDirectory.GetPath("not-directory");
        File.WriteAllText(fileAsDirectory, "file");
        var store = new LogRep2SettingsStore(fileAsDirectory);

        var exception = Assert.Throws<IOException>(
            () => store.Save(new LogRep2Settings()));

        Assert.Contains("統合設定を実行ファイルと同じフォルダーへ", exception.Message);
        Assert.Contains(store.SettingsPath, exception.Message);
    }
}
