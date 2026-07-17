namespace FfxiTempLogCollector.App;

public sealed class CliParseResult
{
    public bool Success { get; init; }

    public CliCommand? Command { get; init; }

    public string Error { get; init; } = string.Empty;
}
