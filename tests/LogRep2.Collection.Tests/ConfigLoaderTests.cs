using FfxiTempLogCollector.Core;

namespace FfxiTempLogCollector.Tests;

public sealed class ConfigLoaderTests
{
    [Fact]
    public void 環境変数を含むパスを展開できる()
    {
        var userProfile = Environment.GetEnvironmentVariable("USERPROFILE");

        Assert.False(string.IsNullOrWhiteSpace(userProfile));

        var actual = ConfigLoader.ExpandPath(
            @"%USERPROFILE%\Documents\FFXI_LogRep_r\sessions");

        Assert.Equal(
            Path.Combine(
                userProfile!,
                "Documents",
                "FFXI_LogRep_r",
                "sessions"),
            actual);
    }

    [Fact]
    public void 相対出力先を実行ディレクトリ基準で展開できる()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var applicationDirectory = temporaryDirectory.GetPath("application");

        var actual = ConfigLoader.ResolveOutputDirectory(
            "sessions",
            applicationDirectory);

        Assert.Equal(
            Path.Combine(applicationDirectory, "sessions"),
            actual);
    }

    [Fact]
    public void 明示パスを最優先で読み込める()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var applicationDirectory = temporaryDirectory.GetPath("application");
        var explicitPath = temporaryDirectory.GetPath("explicit.json");
        Directory.CreateDirectory(applicationDirectory);

        var store = new ConfigStore(applicationDirectory);
        store.Save(new CollectorConfig { LogLevel = "debug" });
        store.Save(new CollectorConfig { LogLevel = "error" }, explicitPath);

        var loader = new ConfigLoader(store, applicationDirectory);
        var actual = loader.Load(explicitPath);

        Assert.Equal("error", actual.LogLevel);
    }

    [Fact]
    public void 実行ディレクトリの設定を読み込める()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var applicationDirectory = temporaryDirectory.GetPath("application");
        Directory.CreateDirectory(applicationDirectory);

        var store = new ConfigStore(applicationDirectory);
        store.Save(new CollectorConfig { LogLevel = "debug" });

        var loader = new ConfigLoader(store, applicationDirectory);
        var actual = loader.Load();

        Assert.Equal("debug", actual.LogLevel);
    }

    [Fact]
    public void 設定ファイルがなければ展開済みデフォルト設定を返す()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var applicationDirectory = temporaryDirectory.GetPath("application");
        var store = new ConfigStore(applicationDirectory);
        var loader = new ConfigLoader(store, applicationDirectory);

        var actual = loader.Load();

        Assert.DoesNotContain("%USERPROFILE%", actual.OutputDir);
        Assert.Equal(
            Path.Combine(applicationDirectory, "sessions"),
            actual.OutputDir);
        Assert.Equal("cp932", actual.Encoding);
    }
}
