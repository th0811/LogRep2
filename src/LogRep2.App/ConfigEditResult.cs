namespace FfxiTempLogCollector.App;

public sealed class ConfigEditResult
{
    public bool Success { get; init; }

    public bool HasWarning { get; init; }

    public bool RequiresNextCollection { get; init; }

    public string Message { get; init; } = string.Empty;
}
