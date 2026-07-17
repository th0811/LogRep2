namespace FfxiTempLogCollector.Core;

public sealed class CollectorStats
{
    public long RawRecordsWritten { get; set; }

    public long CanonicalRecordsWritten { get; set; }

    public long DuplicateRawRecordsSkipped { get; set; }

    public long DuplicateCanonicalRecordsSkipped { get; set; }

    public long ParseErrors { get; set; }

    public long DecodeErrors { get; set; }

    public long GapWarnings { get; set; }

    public DateTimeOffset? LastSeenAt { get; set; }
}
