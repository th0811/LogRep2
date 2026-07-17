namespace FfxiTempLogCollector.Core;

public sealed class CollectorStatusSnapshot
{
    public CollectorStatus Status { get; init; }

    public string? SessionId { get; init; }

    public string? SessionDirectory { get; init; }

    public long RawRecordsWritten { get; init; }

    public long CanonicalRecordsWritten { get; init; }

    public DateTimeOffset? LastSeenAt { get; init; }

    public long WarningCount { get; init; }

    public long ErrorCount { get; init; }

    public string? LastError { get; init; }

    public int PollingIntervalMs { get; init; }

    public string LogLevel { get; init; } = "info";
}
