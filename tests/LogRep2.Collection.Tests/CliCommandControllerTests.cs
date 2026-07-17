using FfxiTempLogCollector.App;
using FfxiTempLogCollector.Core;
using FfxiTempLogCollector.Ipc;

namespace FfxiTempLogCollector.Tests;

public sealed class CliCommandControllerTests
{
    [Fact]
    public async Task ConfigSetとGetが動作する()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var configPath = temporaryDirectory.GetPath("config.json");
        var output = new StringWriter();
        var error = new StringWriter();
        await using var controller = CreateController(
            temporaryDirectory,
            output,
            error);

        var setExitCode = await controller.ExecuteAsync(
            new CliCommand
            {
                Kind = CliCommandKind.ConfigSet,
                ConfigPath = configPath,
                ConfigKey = "polling_interval_ms",
                ConfigValue = "500",
            });
        var getExitCode = await controller.ExecuteAsync(
            new CliCommand
            {
                Kind = CliCommandKind.ConfigGet,
                ConfigPath = configPath,
                ConfigKey = "polling_interval_ms",
            });

        Assert.Equal(CliExitCode.Success, setExitCode);
        Assert.Equal(CliExitCode.Success, getExitCode);
        Assert.Contains("500", output.ToString());
        Assert.Equal(string.Empty, error.ToString());
    }

    [Fact]
    public async Task ConfigPathが指定パスを表示する()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var configPath = temporaryDirectory.GetPath("custom.json");
        var output = new StringWriter();
        await using var controller = CreateController(
            temporaryDirectory,
            output,
            new StringWriter());

        var exitCode = await controller.ExecuteAsync(
            new CliCommand
            {
                Kind = CliCommandKind.ConfigPath,
                ConfigPath = configPath,
            });

        Assert.Equal(CliExitCode.Success, exitCode);
        Assert.Contains(
            Path.GetFullPath(configPath),
            output.ToString());
    }

    [Fact]
    public async Task ConfigPath未指定なら実行ディレクトリ直下を表示する()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var output = new StringWriter();
        await using var controller = CreateController(
            temporaryDirectory,
            output,
            new StringWriter());

        var exitCode = await controller.ExecuteAsync(
            new CliCommand { Kind = CliCommandKind.ConfigPath });

        Assert.Equal(CliExitCode.Success, exitCode);
        Assert.Contains(
            temporaryDirectory.GetPath(
                Path.Combine("application", "config.json")),
            output.ToString());
    }

    [Fact]
    public async Task ConfigSet未指定なら実行ディレクトリ直下へ保存する()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        await using var controller = CreateController(
            temporaryDirectory,
            new StringWriter(),
            new StringWriter());

        var exitCode = await controller.ExecuteAsync(
            new CliCommand
            {
                Kind = CliCommandKind.ConfigSet,
                ConfigKey = "polling_interval_ms",
                ConfigValue = "500",
            });
        var configPath = temporaryDirectory.GetPath(
            Path.Combine("application", "config.json"));

        Assert.Equal(CliExitCode.Success, exitCode);
        Assert.True(File.Exists(configPath));
        Assert.Contains(
            "\"output_dir\": \"sessions\"",
            File.ReadAllText(configPath));
    }

    [Fact]
    public async Task OnceでCompletedセッションを作成する()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var tempDirectory = temporaryDirectory.GetPath("TEMP");
        var outputDirectory =
            temporaryDirectory.GetPath("sessions");
        Directory.CreateDirectory(tempDirectory);
        File.WriteAllBytes(
            Path.Combine(tempDirectory, "1_0.log"),
            TempLogTestFileBuilder.Create("CLI onceテスト"));
        var output = new StringWriter();
        await using var controller = CreateController(
            temporaryDirectory,
            output,
            new StringWriter());

        var exitCode = await controller.ExecuteAsync(
            new CliCommand
            {
                Kind = CliCommandKind.Once,
                TempDirectory = tempDirectory,
                OutputDirectory = outputDirectory,
            });

        Assert.Equal(CliExitCode.Success, exitCode);
        var sessionDirectory =
            Directory.GetDirectories(outputDirectory).Single();
        var session = new SessionManager().Load(sessionDirectory);
        Assert.Equal(SessionStatus.Completed, session.Status);
        Assert.Contains("raw_records_written=1", output.ToString());
    }

    [Fact]
    public async Task 不正な設定値は非0終了コードを返す()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        await using var controller = CreateController(
            temporaryDirectory,
            new StringWriter(),
            new StringWriter());

        var exitCode = await controller.ExecuteAsync(
            new CliCommand
            {
                Kind = CliCommandKind.ConfigSet,
                ConfigPath =
                    temporaryDirectory.GetPath("config.json"),
                ConfigKey = "polling_interval_ms",
                ConfigValue = "100",
            });

        Assert.Equal(CliExitCode.InvalidArguments, exitCode);
    }

    [Fact]
    public async Task Statusは起動中GuiへIpc要求を送る()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var pipeName =
            $"FfxiTempLogCollector.Tests.{Guid.NewGuid():N}";
        await using var server = new NamedPipeCommandServer(
            (command, _) => Task.FromResult(
                IpcResponse.Ok(
                    data: new Dictionary<string, string>
                    {
                        ["status"] = command.Name == "status"
                            ? "running"
                            : "unexpected",
                    })),
            pipeName);
        server.Start();
        var output = new StringWriter();
        await using var controller = CreateController(
            temporaryDirectory,
            output,
            new StringWriter(),
            new NamedPipeCommandClient(pipeName));

        var exitCode = await controller.ExecuteAsync(
            new CliCommand { Kind = CliCommandKind.Status });

        Assert.Equal(CliExitCode.Success, exitCode);
        Assert.Contains("status=running", output.ToString());
    }

    [Fact]
    public async Task 即時反映設定の保存後にConfigUpdatedを送る()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var pipeName =
            $"FfxiTempLogCollector.Tests.{Guid.NewGuid():N}";
        IpcCommand? received = null;
        await using var server = new NamedPipeCommandServer(
            (command, _) =>
            {
                received = command;
                return Task.FromResult(IpcResponse.Ok());
            },
            pipeName);
        server.Start();
        var output = new StringWriter();
        var configPath = temporaryDirectory.GetPath("config.json");
        await using var controller = CreateController(
            temporaryDirectory,
            output,
            new StringWriter(),
            new NamedPipeCommandClient(pipeName));

        var exitCode = await controller.ExecuteAsync(
            new CliCommand
            {
                Kind = CliCommandKind.ConfigSet,
                ConfigPath = configPath,
                ConfigKey = "log_level",
                ConfigValue = "debug",
            });

        Assert.Equal(CliExitCode.Success, exitCode);
        Assert.NotNull(received);
        Assert.Equal("config-updated", received.Name);
        Assert.Equal(configPath, received.Arguments["config_path"]);
        Assert.Contains(
            "起動中GUIへ設定を反映しました。",
            output.ToString());
    }

    private static CliCommandController CreateController(
        TemporaryDirectory temporaryDirectory,
        TextWriter output,
        TextWriter error,
        NamedPipeCommandClient? ipcClient = null)
    {
        var applicationDirectory =
            temporaryDirectory.GetPath("application");
        var store = new ConfigStore(applicationDirectory);
        var loader = new ConfigLoader(
            store,
            applicationDirectory);

        return new CliCommandController(
            store,
            loader,
            new CollectorService(),
            new CliOutputWriter(output, error),
            ipcClient);
    }
}
