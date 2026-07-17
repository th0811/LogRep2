namespace FfxiTempLogCollector.App;

public enum CliCommandKind
{
    Help,
    Start,
    Stop,
    Status,
    Once,
    ConfigGet,
    ConfigSet,
    ConfigPath,
}

public sealed class CliCommand
{
    public CliCommandKind Kind { get; init; }

    public string? ConfigPath { get; init; }

    public string? TempDirectory { get; init; }

    public string? OutputDirectory { get; init; }

    public bool Minimized { get; init; }

    public string? ConfigKey { get; init; }

    public string? ConfigValue { get; init; }
}
