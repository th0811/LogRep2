using FfxiTempLogCollector.App;

namespace FfxiTempLogCollector.Tests;

public sealed class CliCommandParserTests
{
    [Fact]
    public void Helpコマンドを解析できる()
    {
        var result = new CliCommandParser().Parse(["help"]);

        Assert.True(result.Success);
        Assert.Equal(CliCommandKind.Help, result.Command!.Kind);
    }

    [Fact]
    public void Onceのフォルダー指定を解析できる()
    {
        var result = new CliCommandParser().Parse(
        [
            "once",
            "--temp-dir",
            @"C:\FFXI\TEMP",
            "--output-dir",
            @"D:\sessions",
        ]);

        Assert.True(result.Success);
        Assert.Equal(CliCommandKind.Once, result.Command!.Kind);
        Assert.Equal(@"C:\FFXI\TEMP", result.Command.TempDirectory);
        Assert.Equal(@"D:\sessions", result.Command.OutputDirectory);
    }

    [Fact]
    public void ConfigSetを解析できる()
    {
        var result = new CliCommandParser().Parse(
        [
            "config",
            "set",
            "polling_interval_ms",
            "500",
        ]);

        Assert.True(result.Success);
        Assert.Equal(CliCommandKind.ConfigSet, result.Command!.Kind);
        Assert.Equal("polling_interval_ms", result.Command.ConfigKey);
        Assert.Equal("500", result.Command.ConfigValue);
    }

    [Fact]
    public void 不明なコマンドを拒否する()
    {
        var result = new CliCommandParser().Parse(["unknown"]);

        Assert.False(result.Success);
        Assert.Contains("不明なコマンド", result.Error);
    }
}
