namespace FfxiTempLogCollector.Core;

public sealed class ConfigLoader
{
    private readonly string _applicationDirectory;
    private readonly ConfigStore _configStore;

    public ConfigLoader(
        ConfigStore? configStore = null,
        string? applicationDirectory = null)
    {
        _configStore = configStore ?? new ConfigStore(applicationDirectory);
        _applicationDirectory = Path.GetFullPath(
            applicationDirectory ?? AppContext.BaseDirectory);
    }

    public CollectorConfig Load(string? explicitPath = null)
    {
        var configPath = ResolvePath(explicitPath);

        if (configPath is null)
        {
            return ExpandPaths(new CollectorConfig());
        }

        return ExpandPaths(_configStore.Load(configPath));
    }

    public string? ResolvePath(string? explicitPath = null)
    {
        if (!string.IsNullOrWhiteSpace(explicitPath))
        {
            return Path.GetFullPath(ExpandPath(explicitPath));
        }

        var applicationConfigPath = Path.Combine(
            _applicationDirectory,
            ConfigStore.ConfigFileName);

        if (File.Exists(applicationConfigPath))
        {
            return applicationConfigPath;
        }

        return File.Exists(_configStore.DefaultPath)
            ? _configStore.DefaultPath
            : null;
    }

    public static string ExpandPath(string path)
    {
        ArgumentNullException.ThrowIfNull(path);

        return Environment.ExpandEnvironmentVariables(path);
    }

    public static string ResolveOutputDirectory(
        string outputDirectory,
        string applicationDirectory)
    {
        ArgumentNullException.ThrowIfNull(outputDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(applicationDirectory);

        var expanded = ExpandPath(outputDirectory);

        return Path.IsPathFullyQualified(expanded)
            ? expanded
            : Path.GetFullPath(
                Path.Combine(applicationDirectory, expanded));
    }

    private CollectorConfig ExpandPaths(CollectorConfig config)
    {
        config.TempDir = ExpandPath(config.TempDir);
        config.OutputDir = ResolveOutputDirectory(
            config.OutputDir,
            _applicationDirectory);

        return config;
    }
}
