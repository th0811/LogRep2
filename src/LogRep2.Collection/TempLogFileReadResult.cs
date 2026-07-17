namespace FfxiTempLogCollector.Core;

public sealed class TempLogFileReadResult
{
    public bool Exists { get; init; }

    public FileSnapshot? Snapshot { get; init; }

    public string? Error { get; init; }
}
