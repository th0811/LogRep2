namespace FfxiTempLogCollector.Core;

public sealed class FileSnapshotState
{
    public bool Exists { get; init; }

    public DateTimeOffset? LastWriteTime { get; init; }

    public long FileSize { get; init; }

    public string FileHash { get; init; } = string.Empty;
}
