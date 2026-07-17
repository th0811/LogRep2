namespace FfxiTempLogCollector.Core;

public sealed class PollingResult
{
    public List<FileSnapshot> ChangedFiles { get; } = [];

    public List<string> MissingFiles { get; } = [];

    public List<string> Errors { get; } = [];
}
