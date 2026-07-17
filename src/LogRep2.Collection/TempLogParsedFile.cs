namespace FfxiTempLogCollector.Core;

public sealed class TempLogParsedFile
{
    public IReadOnlyList<ushort> HeaderOffsets { get; init; } = [];

    public IReadOnlyList<TempLogRawRecord> Records { get; init; } = [];

    public ParseStatus ParseStatus { get; init; }

    public string? ParseError { get; init; }
}
