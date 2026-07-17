namespace FfxiTempLogCollector.Core;

public sealed class CollectorStartRequest
{
    public CollectorConfig Config { get; init; } = new();
}
