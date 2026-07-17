using System.IO.Pipes;
using System.Text;
using System.Text.Json;

namespace FfxiTempLogCollector.Ipc;

public sealed class NamedPipeCommandServer : IAsyncDisposable
{
    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web);

    private readonly string _pipeName;
    private readonly Func<IpcCommand, CancellationToken, Task<IpcResponse>>
        _handler;
    private CancellationTokenSource? _cancellation;
    private Task? _serverTask;

    public NamedPipeCommandServer(
        Func<IpcCommand, CancellationToken, Task<IpcResponse>> handler,
        string? pipeName = null)
    {
        _handler = handler
            ?? throw new ArgumentNullException(nameof(handler));
        _pipeName = pipeName ?? IpcModule.PipeName;
    }

    public void Start()
    {
        if (_serverTask is not null)
        {
            throw new InvalidOperationException(
                "Named Pipeサーバーは既に開始されています。");
        }

        _cancellation = new CancellationTokenSource();
        _serverTask = RunAsync(_cancellation.Token);
    }

    public async ValueTask DisposeAsync()
    {
        if (_serverTask is null)
        {
            return;
        }

        _cancellation!.Cancel();

        try
        {
            await _serverTask.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }

        _cancellation.Dispose();
        _cancellation = null;
        _serverTask = null;
    }

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            await using var pipe = new NamedPipeServerStream(
                _pipeName,
                PipeDirection.InOut,
                1,
                PipeTransmissionMode.Byte,
                PipeOptions.Asynchronous);
            await pipe.WaitForConnectionAsync(cancellationToken)
                .ConfigureAwait(false);

            try
            {
                await ProcessCommandAsync(pipe, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (IOException)
            {
                // クライアント切断時は次の接続を待機します。
            }
        }
    }

    private async Task ProcessCommandAsync(
        Stream pipe,
        CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(
            pipe,
            Encoding.UTF8,
            leaveOpen: true);
        using var writer = new StreamWriter(
            pipe,
            new UTF8Encoding(false),
            leaveOpen: true)
        {
            AutoFlush = true,
        };

        IpcResponse response;

        try
        {
            var requestJson = await reader.ReadLineAsync(
                    cancellationToken)
                .ConfigureAwait(false);
            var command = string.IsNullOrWhiteSpace(requestJson)
                ? null
                : JsonSerializer.Deserialize<IpcCommand>(
                    requestJson,
                    JsonOptions);
            response = command is null
                ? IpcResponse.Error("IPC要求が空です。")
                : await _handler(command, cancellationToken)
                    .ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            response = IpcResponse.Error(
                $"IPC要求の処理に失敗しました: {exception.Message}");
        }

        var responseJson = JsonSerializer.Serialize(
            response,
            JsonOptions);
        await writer.WriteLineAsync(responseJson)
            .ConfigureAwait(false);
    }
}
