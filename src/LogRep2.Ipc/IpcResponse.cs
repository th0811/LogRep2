namespace FfxiTempLogCollector.Ipc;

public sealed class IpcResponse
{
    public bool Success { get; init; }

    public string Message { get; init; } = string.Empty;

    public Dictionary<string, string> Data { get; init; } = [];

    public static IpcResponse Ok(
        string message = "",
        Dictionary<string, string>? data = null)
    {
        return new IpcResponse
        {
            Success = true,
            Message = message,
            Data = data ?? [],
        };
    }

    public static IpcResponse Error(string message)
    {
        return new IpcResponse { Message = message };
    }
}
