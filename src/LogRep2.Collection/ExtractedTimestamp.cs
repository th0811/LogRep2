namespace FfxiTempLogCollector.Core;

public sealed class ExtractedTimestamp
{
    public string TimeText { get; init; } = string.Empty;

    public string Precision { get; init; } = string.Empty;

    public TimeOnly Time { get; init; }
}
