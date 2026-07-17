using FfxiTempLogCollector.Ipc;

namespace FfxiTempLogCollector.Tests;

public sealed class IpcTests
{
    [Fact]
    public async Task NamedPipeで要求と応答を送受信できる()
    {
        var pipeName = $"FfxiTempLogCollector.Tests.{Guid.NewGuid():N}";
        await using var server = new NamedPipeCommandServer(
            (command, _) => Task.FromResult(
                IpcResponse.Ok(
                    "受信しました。",
                    new Dictionary<string, string>
                    {
                        ["command"] = command.Name,
                    })),
            pipeName);
        server.Start();
        var client = new NamedPipeCommandClient(pipeName);

        var response = await client.TrySendAsync(
            new IpcCommand { Name = "status" },
            TimeSpan.FromSeconds(2));

        Assert.NotNull(response);
        Assert.True(response.Success);
        Assert.Equal("受信しました。", response.Message);
        Assert.Equal("status", response.Data["command"]);
    }

    [Fact]
    public void 同じMutexを別スレッドから二重取得できない()
    {
        var mutexName =
            $@"Local\FfxiTempLogCollector.Tests.{Guid.NewGuid():N}";
        using var first = new SingleInstanceManager(mutexName);

        Assert.True(first.TryAcquire());

        var secondResult = true;
        var thread = new Thread(
            () =>
            {
                using var second =
                    new SingleInstanceManager(mutexName);
                secondResult = second.TryAcquire();
            });
        thread.Start();
        thread.Join();

        Assert.False(secondResult);
    }
}
