namespace FfxiTempLogCollector.Core;

public sealed class TempLogRawRecord
{
    public int RecordIndex { get; init; }

    public int RecordOffset { get; init; }

    public byte[] RawRecordBytes { get; init; } = [];

    public TempLogMetaFields MetaFields { get; init; } = new();

    public byte[] RawMessageBytes { get; init; } = [];

    public ParseStatus ParseStatus { get; init; }

    public string? ParseError { get; init; }
}
