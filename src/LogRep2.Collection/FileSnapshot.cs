namespace FfxiTempLogCollector.Core;

public sealed class FileSnapshot
{
    public string Path { get; init; } = string.Empty;

    public string FileName { get; init; } = string.Empty;

    public DateTimeOffset LastWriteTime { get; init; }

    public long FileSize { get; init; }

    public string FileHash { get; init; } = string.Empty;

    public byte[] Content { get; init; } = [];
}
