namespace FfxiTempLogCollector.Ipc;

public sealed class IpcCommand
{
    public string Name { get; init; } = string.Empty;

    public Dictionary<string, string> Arguments { get; init; } = [];
}
