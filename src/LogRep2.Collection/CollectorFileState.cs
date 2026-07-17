namespace FfxiTempLogCollector.Core;

public sealed class CollectorFileState
{
    public bool Exists { get; set; }

    public DateTimeOffset? LastWriteTime { get; set; }

    public long FileSize { get; set; }

    public string FileHash { get; set; } = string.Empty;
}
