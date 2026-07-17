using FFXI_LogAnalyzer.Core;
using LogRep2.Infrastructure;

namespace FFXI_LogAnalyzer.App;

public sealed class AnalyzerSettingsStore
{
    private readonly LogRep2SettingsStore _settingsStore;

    public AnalyzerSettingsStore(string? applicationDirectory = null)
    {
        _settingsStore = new LogRep2SettingsStore(applicationDirectory);
        SettingsPath = _settingsStore.SettingsPath;
    }

    public string SettingsPath { get; }

    public AnalyzerSettings Load()
    {
        var settings = _settingsStore.LoadOrMigrate().Settings;
        var collectorConfig = settings.CreateCollectorConfig(
            _settingsStore.ApplicationDirectory);
        return Normalize(
            new AnalyzerSettings
            {
                SessionsRootFolderPath = collectorConfig.OutputDir,
                KnownPcNames = settings.Analysis.KnownPcNames,
                KnownNpcNames = settings.Analysis.KnownNpcNames,
            });
    }

    public void Save(AnalyzerSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var normalized = Normalize(settings);
        var unified = _settingsStore.LoadOrMigrate().Settings;
        unified.Analysis.KnownPcNames = normalized.KnownPcNames;
        unified.Analysis.KnownNpcNames = normalized.KnownNpcNames;
        _settingsStore.Save(unified);
    }

    private static AnalyzerSettings Normalize(AnalyzerSettings settings)
    {
        return new AnalyzerSettings
        {
            SessionsRootFolderPath = NormalizeFolderPath(settings.SessionsRootFolderPath),
            KnownPcNames = NormalizePcNames(settings.KnownPcNames),
            KnownNpcNames = NormalizeNpcNames(settings.KnownNpcNames)
        };
    }

    private static string? NormalizeFolderPath(string? folderPath)
    {
        return string.IsNullOrWhiteSpace(folderPath)
            ? null
            : folderPath.Trim();
    }

    private static List<string> NormalizePcNames(IEnumerable<string> names)
    {
        return names
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(ActorNameClassifier.NormalizePcName)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static List<string> NormalizeNpcNames(IEnumerable<string> names)
    {
        return names
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => name.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
