namespace FfxiTempLogCollector.Core;

public sealed class RawRecordContext
{
    public string SessionId { get; init; } = string.Empty;

    public DateTimeOffset FirstSeenAt { get; init; }

    public string SourceFile { get; init; } = string.Empty;

    public int WindowId { get; init; }

    public int RotationIndex { get; init; }

    public DateTimeOffset FileMtime { get; init; }

    public long FileSize { get; init; }

    public string FileHash { get; init; } = string.Empty;
}
