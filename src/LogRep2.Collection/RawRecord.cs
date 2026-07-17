namespace FfxiTempLogCollector.Core;

public sealed class RawRecord
{
    public string SchemaVersion { get; init; } = SchemaVersions.RawRecord;

    public string RawRecordId { get; init; } = string.Empty;

    public string SessionId { get; init; } = string.Empty;

    public DateTimeOffset FirstSeenAt { get; init; }

    public string SourceFile { get; init; } = string.Empty;

    public int WindowId { get; init; }

    public int RotationIndex { get; init; }

    public DateTimeOffset FileMtime { get; init; }

    public long FileSize { get; init; }

    public string FileHash { get; init; } = string.Empty;

    public int RecordIndex { get; init; }

    public int RecordOffset { get; init; }

    public string RawRecordHash { get; init; } = string.Empty;

    public IReadOnlyList<string> MetaFields { get; init; } = [];

    public string? EventGroup { get; init; }

    public string? SequenceHint { get; init; }

    public string? MessageTokenCount { get; init; }

    public RawRecordDisplay? Display { get; init; }

    public string RawMessageHex { get; init; } = string.Empty;

    public string VisibleText { get; init; } = string.Empty;

    public string? MessageTimeText { get; init; }

    public string? MessageTimePrecision { get; init; }

    public long? MessageUnixTimeHint { get; init; }

    public DateTimeOffset? MessageTimeAt { get; init; }

    public bool IsMarker { get; init; }

    public string? MarkerKeyword { get; init; }

    public ParseStatus ParseStatus { get; init; }

    public string? ParseError { get; init; }
}

public sealed class RawRecordDisplay
{
    public string ColorCode { get; init; } = string.Empty;
}
