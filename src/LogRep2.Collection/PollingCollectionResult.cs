namespace FfxiTempLogCollector.Core;

public sealed class PollingCollectionResult
{
    public string SessionId { get; init; } = string.Empty;

    public string SessionDirectory { get; init; } = string.Empty;

    public long PollCount { get; internal set; }

    public long FilesProcessed { get; internal set; }

    public long RawRecordsWritten { get; internal set; }

    public long CanonicalRecordsWritten { get; internal set; }

    public List<string> Errors { get; } = [];
}
