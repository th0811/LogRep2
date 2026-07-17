namespace FfxiTempLogCollector.Core;

public sealed class DetectedMarker
{
    public bool IsMarker { get; init; } = true;

    public string Keyword { get; init; } = string.Empty;
}
