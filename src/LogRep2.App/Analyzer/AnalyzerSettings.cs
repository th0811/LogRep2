namespace FFXI_LogAnalyzer.App;

public sealed class AnalyzerSettings
{
    public string? SessionsRootFolderPath { get; set; }

    public List<string> KnownPcNames { get; set; } = [];

    public List<string> KnownNpcNames { get; set; } = [];
}
