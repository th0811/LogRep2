using System.IO.Pipes;
using System.Text;
using System.Text.Json;

namespace FfxiTempLogCollector.Ipc;

public sealed class NamedPipeCommandClient
{
    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web);

    private readonly string _pipeName;

    public NamedPipeCommandClient(string? pipeName = null)
    {
        _pipeName = pipeName ?? IpcModule.PipeName;
    }

    public async Task<IpcResponse?> TrySendAsync(
        IpcCommand command,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        using var timeoutCancellation =
            CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken);
        timeoutCancellation.CancelAfter(
            timeout ?? TimeSpan.FromSeconds(2));

        try
        {
            await using var pipe = new NamedPipeClientStream(
                ".",
                _pipeName,
                PipeDirection.InOut,
                PipeOptions.Asynchronous);
            await pipe.ConnectAsync(timeoutCancellation.Token)
                .ConfigureAwait(false);
            using var writer = new StreamWriter(
                pipe,
                new UTF8Encoding(false),
                leaveOpen: true)
            {
                AutoFlush = true,
            };
            using var reader = new StreamReader(
                pipe,
                Encoding.UTF8,
                leaveOpen: true);

            var requestJson = JsonSerializer.Serialize(
                command,
                JsonOptions);
            await writer.WriteLineAsync(requestJson)
                .ConfigureAwait(false);
            var responseJson = await reader.ReadLineAsync(
                    timeoutCancellation.Token)
                .ConfigureAwait(false);

            return string.IsNullOrWhiteSpace(responseJson)
                ? null
                : JsonSerializer.Deserialize<IpcResponse>(
                    responseJson,
                    JsonOptions);
        }
        catch (OperationCanceledException)
            when (!cancellationToken.IsCancellationRequested)
        {
            return null;
        }
        catch (IOException)
        {
            return null;
        }
    }
}
