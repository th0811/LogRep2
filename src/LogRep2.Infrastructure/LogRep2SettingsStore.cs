using System.Text.Json;
using System.Text.Json.Serialization;
using FfxiTempLogCollector.Core;

namespace LogRep2.Infrastructure;

public sealed class LogRep2SettingsStore
{
    public const string FileName = "LogRep2.settings.json";
    public const string LegacyCollectionFileName = "config.json";
    public const string LegacyAnalysisFileName =
        "analyzer_settings.json";

    private static readonly JsonSerializerOptions JsonOptions =
        CreateJsonOptions();

    public LogRep2SettingsStore(string? applicationDirectory = null)
    {
        ApplicationDirectory = Path.GetFullPath(
            applicationDirectory ?? AppContext.BaseDirectory);
        SettingsPath = Path.Combine(ApplicationDirectory, FileName);
    }

    public string ApplicationDirectory { get; }

    public string SettingsPath { get; }

    public LogRep2Settings Load()
    {
        if (!File.Exists(SettingsPath))
        {
            throw new FileNotFoundException(
                $"統合設定ファイルが見つかりません: {SettingsPath}",
                SettingsPath);
        }

        try
        {
            using var stream = File.OpenRead(SettingsPath);
            var settings = JsonSerializer.Deserialize<LogRep2Settings>(
                    stream,
                    JsonOptions)
                ?? throw new InvalidDataException(
                    "統合設定ファイルの内容が空です。");
            ValidateSchemaVersion(settings);
            Normalize(settings);
            return settings;
        }
        catch (Exception exception)
            when (exception is JsonException
                or NotSupportedException)
        {
            throw new InvalidDataException(
                $"統合設定ファイルを読み込めません: {SettingsPath}",
                exception);
        }
    }

    public SettingsMigrationResult LoadOrMigrate()
    {
        if (File.Exists(SettingsPath))
        {
            return new SettingsMigrationResult(
                Load(),
                Created: false,
                MigratedCollectionSettings: false,
                MigratedAnalysisSettings: false,
                Warnings: []);
        }

        var settings = new LogRep2Settings();
        var warnings = new List<string>();
        var collectionMigrated = TryMigrateCollection(
            settings,
            warnings);
        var analysisMigrated = TryMigrateAnalysis(
            settings,
            warnings);
        Normalize(settings);
        Save(settings);

        return new SettingsMigrationResult(
            settings,
            Created: true,
            MigratedCollectionSettings: collectionMigrated,
            MigratedAnalysisSettings: analysisMigrated,
            Warnings: warnings);
    }

    public void Save(LogRep2Settings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ValidateSchemaVersion(settings);
        Normalize(settings);

        var temporaryPath = SettingsPath + ".tmp";

        try
        {
            Directory.CreateDirectory(ApplicationDirectory);
            using (var stream = new FileStream(
                       temporaryPath,
                       FileMode.Create,
                       FileAccess.Write,
                       FileShare.None))
            {
                JsonSerializer.Serialize(stream, settings, JsonOptions);
                stream.Flush(flushToDisk: true);
            }

            File.Move(temporaryPath, SettingsPath, overwrite: true);
        }
        catch (Exception exception)
        {
            TryDeleteTemporaryFile(temporaryPath);
            throw new IOException(
                "統合設定を実行ファイルと同じフォルダーへ"
                + $"保存できません: {SettingsPath}",
                exception);
        }
    }

    public CollectorConfig LoadCollectorConfig()
    {
        return Load().CreateCollectorConfig(ApplicationDirectory);
    }

    public void SaveCollectorConfig(CollectorConfig config)
    {
        var settings = Load();
        settings.UpdateFromCollectorConfig(config);
        Save(settings);
    }

    private bool TryMigrateCollection(
        LogRep2Settings settings,
        ICollection<string> warnings)
    {
        var path = Path.Combine(
            ApplicationDirectory,
            LegacyCollectionFileName);

        if (!File.Exists(path))
        {
            return false;
        }

        try
        {
            var legacy = new ConfigStore(ApplicationDirectory).Load(path);
            settings.UpdateFromCollectorConfig(legacy);
            return true;
        }
        catch (Exception exception)
        {
            warnings.Add(
                $"旧収集設定を移行できませんでした: {path}"
                + Environment.NewLine
                + exception.Message);
            return false;
        }
    }

    private bool TryMigrateAnalysis(
        LogRep2Settings settings,
        ICollection<string> warnings)
    {
        var path = Path.Combine(
            ApplicationDirectory,
            LegacyAnalysisFileName);

        if (!File.Exists(path))
        {
            return false;
        }

        try
        {
            using var stream = File.OpenRead(path);
            var legacy = JsonSerializer.Deserialize<LegacyAnalysisSettings>(
                    stream,
                    JsonOptions)
                ?? new LegacyAnalysisSettings();
            settings.Analysis.KnownPcNames = legacy.KnownPcNames;
            settings.Analysis.KnownNpcNames = legacy.KnownNpcNames;
            return true;
        }
        catch (Exception exception)
        {
            warnings.Add(
                $"旧分析設定を移行できませんでした: {path}"
                + Environment.NewLine
                + exception.Message);
            return false;
        }
    }

    private static void ValidateSchemaVersion(LogRep2Settings settings)
    {
        if (settings.SchemaVersion != 1)
        {
            throw new InvalidDataException(
                "対応していない統合設定スキーマです: "
                + settings.SchemaVersion);
        }
    }

    private static void Normalize(LogRep2Settings settings)
    {
        settings.Collection ??= new CollectionSettings();
        settings.Analysis ??= new AnalysisSettings();
        settings.Overlay ??= new OverlaySettings();
        settings.Application ??= new ApplicationSettings();
        settings.Analysis.KnownPcNames = NormalizeNames(
            settings.Analysis.KnownPcNames);
        settings.Analysis.KnownNpcNames = NormalizeNames(
            settings.Analysis.KnownNpcNames);
        settings.Analysis.RealtimePartyMembers = NormalizeOrderedNames(
            settings.Analysis.RealtimePartyMembers,
            6);
        settings.Overlay.DisplayItems = NormalizeNames(
            settings.Overlay.DisplayItems);
        settings.Overlay.Opacity = Math.Clamp(settings.Overlay.Opacity, 0.25, 1.0);
        settings.Overlay.Width = Math.Max(settings.Overlay.Width, 280);
        settings.Overlay.Height = Math.Max(settings.Overlay.Height, 180);
        settings.Overlay.FontSize = Math.Clamp(settings.Overlay.FontSize, 10, 40);
        settings.Overlay.DisplayRowCount = Math.Clamp(settings.Overlay.DisplayRowCount, 1, 30);
    }

    private static List<string> NormalizeNames(
        IEnumerable<string>? names)
    {
        return (names ?? [])
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => name.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static List<string> NormalizeOrderedNames(
        IEnumerable<string>? names,
        int maximumCount)
    {
        return (names ?? [])
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => name.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(maximumCount)
            .ToList();
    }

    private static void TryDeleteTemporaryFile(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch
        {
            // 元の保存エラーを優先します。
        }
    }

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            PropertyNameCaseInsensitive = true,
        };
        options.Converters.Add(
            new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower));
        return options;
    }

    private sealed class LegacyAnalysisSettings
    {
        public string? SessionsRootFolderPath { get; set; }

        public List<string> KnownPcNames { get; set; } = [];

        public List<string> KnownNpcNames { get; set; } = [];
    }
}
