namespace FfxiTempLogCollector.Core;

public sealed class DecodedLogMessage
{
    public string RawMessageHex { get; init; } = string.Empty;

    public string DecodedText { get; init; } = string.Empty;

    public string VisibleText { get; init; } = string.Empty;
}
