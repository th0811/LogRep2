using System.IO;
using FfxiTempLogCollector.Core;

namespace FfxiTempLogCollector.App;

public sealed class ConfigEditService
{
    private readonly ConfigStore _configStore;
    private readonly CollectorService _collectorService;
    private readonly string? _configPath;
    private readonly Action<CollectorConfig>? _saveConfig;

    public ConfigEditService(
        ConfigStore configStore,
        CollectorService collectorService,
        string? configPath = null,
        Action<CollectorConfig>? saveConfig = null)
    {
        _configStore = configStore
            ?? throw new ArgumentNullException(nameof(configStore));
        _collectorService = collectorService
            ?? throw new ArgumentNullException(nameof(collectorService));
        _configPath = configPath;
        _saveConfig = saveConfig;
    }

    public ConfigEditResult Save(
        CollectorConfig current,
        CollectorConfig edited)
    {
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(edited);

        var normalized = Clone(edited);
        var configDirectory = Path.GetDirectoryName(
                Path.GetFullPath(_configPath ?? _configStore.DefaultPath))
            ?? AppContext.BaseDirectory;
        normalized.TempDir = ConfigLoader.ExpandPath(
            normalized.TempDir);
        normalized.OutputDir = ConfigLoader.ResolveOutputDirectory(
            normalized.OutputDir,
            configDirectory);
        var validation = Validate(normalized);

        if (!validation.Success)
        {
            return validation;
        }

        var isCollecting = _collectorService.GetStatus().Status
            is CollectorStatus.Starting
            or CollectorStatus.Running
            or CollectorStatus.Stopping;
        var requiresNextCollection = isCollecting
            && HasDeferredChanges(current, normalized);

        try
        {
            if (_saveConfig is null)
            {
                _configStore.Save(normalized, _configPath);
            }
            else
            {
                _saveConfig(normalized);
            }
            CopyConfig(normalized, current);
            _collectorService.UpdatePollingInterval(
                normalized.PollingIntervalMs);
            _collectorService.UpdateLogLevel(normalized.LogLevel);
        }
        catch (Exception exception)
        {
            return new ConfigEditResult
            {
                Message = "設定を保存できませんでした。"
                    + Environment.NewLine
                    + exception.Message,
            };
        }

        return new ConfigEditResult
        {
            Success = true,
            HasWarning = validation.HasWarning,
            RequiresNextCollection = requiresNextCollection,
            Message = requiresNextCollection
                ? "この設定は現在の収集停止後、次回収集開始時に反映されます。"
                : validation.Message,
        };
    }

    public ConfigEditResult Validate(CollectorConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);

        if (config.PollingIntervalMs
            is < PollingOptions.MinimumIntervalMs
            or > PollingOptions.MaximumIntervalMs)
        {
            return new ConfigEditResult
            {
                Message = $"ポーリング間隔は"
                    + $"{PollingOptions.MinimumIntervalMs}～"
                    + $"{PollingOptions.MaximumIntervalMs}ms"
                    + "で指定してください。",
            };
        }

        if (string.IsNullOrWhiteSpace(config.OutputDir))
        {
            return new ConfigEditResult
            {
                Message = "出力先フォルダーを指定してください。",
            };
        }

        if (config.MarkerDetection
            && string.IsNullOrWhiteSpace(config.MarkerPrefix))
        {
            return new ConfigEditResult
            {
                Message = "マーカー文字列を指定してください。",
            };
        }

        try
        {
            Directory.CreateDirectory(
                ConfigLoader.ExpandPath(config.OutputDir));
        }
        catch (Exception exception)
        {
            return new ConfigEditResult
            {
                Message = $"出力先フォルダーを作成できません。"
                    + Environment.NewLine
                    + exception.Message,
            };
        }

        if (string.IsNullOrWhiteSpace(config.TempDir))
        {
            return new ConfigEditResult
            {
                Success = true,
                HasWarning = true,
                Message = "TEMPフォルダーが空です。"
                    + "収集開始前に設定してください。",
            };
        }

        return new ConfigEditResult { Success = true };
    }

    private static bool HasDeferredChanges(
        CollectorConfig current,
        CollectorConfig edited)
    {
        return !StringComparer.OrdinalIgnoreCase.Equals(
                current.TempDir,
                edited.TempDir)
            || !StringComparer.OrdinalIgnoreCase.Equals(
                current.OutputDir,
                edited.OutputDir)
            || current.WatchWindow1 != edited.WatchWindow1
            || current.WatchWindow2 != edited.WatchWindow2
            || current.RawOutput != edited.RawOutput
            || current.CanonicalOutput != edited.CanonicalOutput
            || current.MarkerDetection != edited.MarkerDetection
            || !StringComparer.Ordinal.Equals(
                current.MarkerPrefix,
                edited.MarkerPrefix);
    }

    public static CollectorConfig Clone(CollectorConfig source)
    {
        ArgumentNullException.ThrowIfNull(source);

        var clone = new CollectorConfig();
        CopyConfig(source, clone);
        return clone;
    }

    public static void CopyConfig(
        CollectorConfig source,
        CollectorConfig destination)
    {
        destination.TempDir = source.TempDir;
        destination.OutputDir = source.OutputDir;
        destination.Encoding = source.Encoding;
        destination.PollingIntervalMs = source.PollingIntervalMs;
        destination.WatchWindow1 = source.WatchWindow1;
        destination.WatchWindow2 = source.WatchWindow2;
        destination.RotationSlots = source.RotationSlots;
        destination.RawOutput = source.RawOutput;
        destination.CanonicalOutput = source.CanonicalOutput;
        destination.DedupeRaw = source.DedupeRaw;
        destination.DedupeCanonical = source.DedupeCanonical;
        destination.MarkerDetection = source.MarkerDetection;
        destination.MarkerPrefix = source.MarkerPrefix;
        destination.Timezone = source.Timezone;
        destination.FlushIntervalMs = source.FlushIntervalMs;
        destination.HashAlgorithm = source.HashAlgorithm;
        destination.LogLevel = source.LogLevel;
        destination.AutoStartCollectionOnLaunch =
            source.AutoStartCollectionOnLaunch;
        destination.MinimizeToTrayWhileCollecting =
            source.MinimizeToTrayWhileCollecting;
        destination.MinimizeButtonBehavior =
            source.MinimizeButtonBehavior;
        destination.CloseButtonBehavior =
            source.CloseButtonBehavior;
        destination.ShowTrayNotifications =
            source.ShowTrayNotifications;
    }
}
