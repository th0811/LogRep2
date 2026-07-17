namespace FfxiTempLogCollector.Core;

public sealed class CollectionResult
{
    public string SessionId { get; init; } = string.Empty;

    public string SessionDirectory { get; init; } = string.Empty;

    public int TargetFiles { get; internal set; }

    public int FilesRead { get; internal set; }

    public int MissingFiles { get; internal set; }

    public int FileReadErrors { get; internal set; }

    public long RawRecordsWritten { get; internal set; }

    public long CanonicalRecordsWritten { get; internal set; }

    public long ParseErrors { get; internal set; }

    public List<string> Errors { get; } = [];
}
