namespace FfxiTempLogCollector.Core;

public sealed class ConfigStore
{
    public const string ConfigFileName = "config.json";

    public ConfigStore(string? applicationDirectory = null)
    {
        var baseDirectory = Path.GetFullPath(
            applicationDirectory ?? AppContext.BaseDirectory);

        DefaultPath = Path.Combine(baseDirectory, ConfigFileName);
    }

    public string DefaultPath { get; }

    public CollectorConfig Load(string? path = null)
    {
        return JsonFileSerializer.Load<CollectorConfig>(path ?? DefaultPath);
    }

    public void Save(CollectorConfig config, string? path = null)
    {
        JsonFileSerializer.Save(path ?? DefaultPath, config);
    }
}
