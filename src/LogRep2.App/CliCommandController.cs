using System.Globalization;
using System.IO;
using FfxiTempLogCollector.Core;
using FfxiTempLogCollector.Ipc;
using LogRep2.Infrastructure;

namespace FfxiTempLogCollector.App;

public sealed class CliCommandController : IAsyncDisposable
{
    private readonly ConfigStore _configStore;
    private readonly ConfigLoader _configLoader;
    private readonly CollectorService _collectorService;
    private readonly CliOutputWriter _output;
    private readonly NamedPipeCommandClient? _ipcClient;
    private readonly LogRep2SettingsStore? _unifiedSettingsStore;

    public CliCommandController(
        ConfigStore configStore,
        ConfigLoader configLoader,
        CollectorService collectorService,
        CliOutputWriter output,
        NamedPipeCommandClient? ipcClient = null,
        LogRep2SettingsStore? unifiedSettingsStore = null)
    {
        _configStore = configStore
            ?? throw new ArgumentNullException(nameof(configStore));
        _configLoader = configLoader
            ?? throw new ArgumentNullException(nameof(configLoader));
        _collectorService = collectorService
            ?? throw new ArgumentNullException(nameof(collectorService));
        _output = output
            ?? throw new ArgumentNullException(nameof(output));
        _ipcClient = ipcClient;
        _unifiedSettingsStore = unifiedSettingsStore;
    }

    public async Task<CliExitCode> ExecuteAsync(
        CliCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        try
        {
            return command.Kind switch
            {
                CliCommandKind.Help => ShowHelp(),
                CliCommandKind.Start => await StartAsync(
                    command,
                    cancellationToken),
                CliCommandKind.Stop => await StopAsync(
                    cancellationToken),
                CliCommandKind.Status => await ShowStatusAsync(
                    cancellationToken),
                CliCommandKind.Once => RunOnce(command),
                CliCommandKind.ConfigGet => GetConfig(command),
                CliCommandKind.ConfigSet => await SetConfigAsync(
                    command,
                    cancellationToken),
                CliCommandKind.ConfigPath => ShowConfigPath(command),
                _ => CliExitCode.InvalidArguments,
            };
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            return CliExitCode.Success;
        }
        catch (InvalidDataException exception)
        {
            _output.WriteError(exception.Message);
            return CliExitCode.ConfigError;
        }
        catch (IOException exception)
        {
            _output.WriteError(exception.Message);
            return CliExitCode.ConfigError;
        }
        catch (UnauthorizedAccessException exception)
        {
            _output.WriteError(exception.Message);
            return CliExitCode.ConfigError;
        }
        catch (Exception exception)
        {
            _output.WriteError($"予期しないエラー: {exception.Message}");
            return CliExitCode.UnexpectedError;
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _collectorService.DisposeAsync();
    }

    private CliExitCode ShowHelp()
    {
        _output.WriteLine(
            """
            LogRep2

            使用方法:
              LogRep2.exe help
              LogRep2.exe start [--temp-dir PATH] [--output-dir PATH]
              LogRep2.exe stop
              LogRep2.exe status
              LogRep2.exe once [--temp-dir PATH] [--output-dir PATH]
              LogRep2.exe config get KEY
              LogRep2.exe config set KEY VALUE
              LogRep2.exe config path

            共通オプション:
              --config PATH    使用する設定ファイルを指定

            主な設定キー:
              temp_dir, output_dir, polling_interval_ms, log_level,
              watch_window1, watch_window2, raw_output, canonical_output
            """);
        return CliExitCode.Success;
    }

    private async Task<CliExitCode> StartAsync(
        CliCommand command,
        CancellationToken cancellationToken)
    {
        var ipcResponse = await TrySendIpcAsync(
            new IpcCommand { Name = "start" },
            cancellationToken);

        if (ipcResponse is not null)
        {
            return WriteIpcResponse(ipcResponse);
        }

        var config = LoadConfig(command.ConfigPath);
        ApplyCollectionOverrides(config, command);
        var started = await _collectorService.StartAsync(
            new CollectorStartRequest { Config = config },
            cancellationToken);

        if (!started)
        {
            var status = _collectorService.GetStatus();
            _output.WriteError(
                status.LastError ?? "ログ収集を開始できませんでした。");
            return CliExitCode.CollectionError;
        }

        _output.WriteLine("ログ収集を開始しました。Ctrl+Cで停止します。");

        try
        {
            await Task.Delay(
                Timeout.InfiniteTimeSpan,
                cancellationToken);
        }
        finally
        {
            await _collectorService.StopAsync(
                cancellationToken: CancellationToken.None);
            _output.WriteLine("ログ収集を停止しました。");
        }

        return CliExitCode.Success;
    }

    private async Task<CliExitCode> StopAsync(
        CancellationToken cancellationToken)
    {
        var ipcResponse = await TrySendIpcAsync(
            new IpcCommand { Name = "stop" },
            cancellationToken);

        if (ipcResponse is not null)
        {
            return WriteIpcResponse(ipcResponse);
        }

        var status = _collectorService.GetStatus();

        if (status.Status is CollectorStatus.Stopped
            or CollectorStatus.Error)
        {
            _output.WriteLine(
                "このCLIプロセスではログ収集は実行されていません。"
                + "起動中のLogRep2 GUIも見つかりませんでした。");
            return CliExitCode.NotRunning;
        }

        await _collectorService.StopAsync(
            cancellationToken: cancellationToken);
        _output.WriteLine("ログ収集を停止しました。");
        return CliExitCode.Success;
    }

    private async Task<CliExitCode> ShowStatusAsync(
        CancellationToken cancellationToken)
    {
        var ipcResponse = await TrySendIpcAsync(
            new IpcCommand { Name = "status" },
            cancellationToken);

        if (ipcResponse is not null)
        {
            return WriteIpcResponse(ipcResponse);
        }

        var status = _collectorService.GetStatus();
        _output.WriteLine(
            $"status={status.Status.ToString().ToLowerInvariant()}");
        _output.WriteLine($"session_id={status.SessionId ?? string.Empty}");
        _output.WriteLine(
            $"raw_records_written={status.RawRecordsWritten}");
        _output.WriteLine(
            $"canonical_records_written={status.CanonicalRecordsWritten}");

        if (!string.IsNullOrWhiteSpace(status.LastError))
        {
            _output.WriteLine($"last_error={status.LastError}");
        }

        return CliExitCode.Success;
    }

    private CliExitCode RunOnce(CliCommand command)
    {
        var config = LoadConfig(command.ConfigPath);
        ApplyCollectionOverrides(config, command);

        try
        {
            var result = _collectorService.RunOnce(
                new CollectorStartRequest { Config = config });
            _output.WriteLine($"session_id={result.SessionId}");
            _output.WriteLine(
                $"session_directory={result.SessionDirectory}");
            _output.WriteLine(
                $"raw_records_written={result.RawRecordsWritten}");
            _output.WriteLine(
                $"canonical_records_written="
                + result.CanonicalRecordsWritten);
            return CliExitCode.Success;
        }
        catch (Exception exception)
        {
            _output.WriteError(
                $"一度読み取りに失敗しました: {exception.Message}");
            return CliExitCode.CollectionError;
        }
    }

    private CliExitCode GetConfig(CliCommand command)
    {
        var config = LoadConfig(command.ConfigPath);

        if (!TryGetConfigValue(
                config,
                command.ConfigKey!,
                out var value))
        {
            _output.WriteError(
                $"不明な設定キーです: {command.ConfigKey}");
            return CliExitCode.InvalidArguments;
        }

        _output.WriteLine(value);
        return CliExitCode.Success;
    }

    private async Task<CliExitCode> SetConfigAsync(
        CliCommand command,
        CancellationToken cancellationToken)
    {
        var path = GetWritableConfigPath(command.ConfigPath);
        var config = LoadWritableConfig(command.ConfigPath, path);

        if (!TrySetConfigValue(
                config,
                command.ConfigKey!,
                command.ConfigValue!,
                out var error))
        {
            _output.WriteError(error);
            return CliExitCode.InvalidArguments;
        }

        SaveConfig(config, command.ConfigPath, path);
        _output.WriteLine($"設定を保存しました: {path}");

        if (IsImmediatelyApplicableKey(command.ConfigKey!))
        {
            var response = await TrySendIpcAsync(
                new IpcCommand
                {
                    Name = "config-updated",
                    Arguments = new Dictionary<string, string>
                    {
                        ["config_path"] = command.ConfigPath ?? string.Empty,
                    },
                },
                cancellationToken);

            if (response is not null)
            {
                if (!response.Success)
                {
                    _output.WriteError(response.Message);
                    return CliExitCode.ConfigError;
                }

                _output.WriteLine("起動中GUIへ設定を反映しました。");
            }
        }

        return CliExitCode.Success;
    }

    private CliExitCode ShowConfigPath(CliCommand command)
    {
        _output.WriteLine(GetWritableConfigPath(command.ConfigPath));
        return CliExitCode.Success;
    }

    private CollectorConfig LoadConfig(string? explicitPath)
    {
        if (string.IsNullOrWhiteSpace(explicitPath)
            && _unifiedSettingsStore is not null)
        {
            return _unifiedSettingsStore.LoadCollectorConfig();
        }

        return _configLoader.Load(explicitPath);
    }

    private CollectorConfig LoadWritableConfig(
        string? explicitPath,
        string resolvedPath)
    {
        if (string.IsNullOrWhiteSpace(explicitPath)
            && _unifiedSettingsStore is not null)
        {
            return _unifiedSettingsStore.LoadCollectorConfig();
        }

        return File.Exists(resolvedPath)
            ? _configLoader.Load(resolvedPath)
            : CreateDefaultConfig();
    }

    private string GetWritableConfigPath(string? explicitPath)
    {
        if (!string.IsNullOrWhiteSpace(explicitPath))
        {
            return Path.GetFullPath(
                ConfigLoader.ExpandPath(explicitPath));
        }

        return _unifiedSettingsStore?.SettingsPath
            ?? _configStore.DefaultPath;
    }

    private void SaveConfig(
        CollectorConfig config,
        string? explicitPath,
        string resolvedPath)
    {
        if (string.IsNullOrWhiteSpace(explicitPath)
            && _unifiedSettingsStore is not null)
        {
            _unifiedSettingsStore.SaveCollectorConfig(config);
            return;
        }

        _configStore.Save(config, resolvedPath);
    }

    private static CollectorConfig CreateDefaultConfig()
    {
        var config = new CollectorConfig();
        config.TempDir = ConfigLoader.ExpandPath(config.TempDir);
        return config;
    }

    private static void ApplyCollectionOverrides(
        CollectorConfig config,
        CliCommand command)
    {
        if (!string.IsNullOrWhiteSpace(command.TempDirectory))
        {
            config.TempDir = ConfigLoader.ExpandPath(
                command.TempDirectory);
        }

        if (!string.IsNullOrWhiteSpace(command.OutputDirectory))
        {
            config.OutputDir = ConfigLoader.ExpandPath(
                command.OutputDirectory);
        }
    }

    private static bool TryGetConfigValue(
        CollectorConfig config,
        string key,
        out string value)
    {
        object? rawValue = NormalizeKey(key) switch
        {
            "temp_dir" => config.TempDir,
            "output_dir" => config.OutputDir,
            "encoding" => config.Encoding,
            "polling_interval_ms" => config.PollingIntervalMs,
            "watch_window1" => config.WatchWindow1,
            "watch_window2" => config.WatchWindow2,
            "rotation_slots" => config.RotationSlots,
            "raw_output" => config.RawOutput,
            "canonical_output" => config.CanonicalOutput,
            "dedupe_raw" => config.DedupeRaw,
            "dedupe_canonical" => config.DedupeCanonical,
            "marker_detection" => config.MarkerDetection,
            "marker_prefix" => config.MarkerPrefix,
            "timezone" => config.Timezone,
            "flush_interval_ms" => config.FlushIntervalMs,
            "hash_algorithm" => config.HashAlgorithm,
            "log_level" => config.LogLevel,
            "auto_start_collection_on_launch" =>
                config.AutoStartCollectionOnLaunch,
            "minimize_to_tray_while_collecting" =>
                config.MinimizeToTrayWhileCollecting,
            "minimize_button_behavior" =>
                config.MinimizeButtonBehavior,
            "close_button_behavior" => config.CloseButtonBehavior,
            "show_tray_notifications" =>
                config.ShowTrayNotifications,
            _ => null,
        };

        if (rawValue is null)
        {
            value = string.Empty;
            return false;
        }

        value = rawValue switch
        {
            bool boolean => boolean.ToString().ToLowerInvariant(),
            IFormattable formattable => formattable.ToString(
                null,
                CultureInfo.InvariantCulture),
            _ => rawValue.ToString() ?? string.Empty,
        };
        return true;
    }

    private static bool TrySetConfigValue(
        CollectorConfig config,
        string key,
        string value,
        out string error)
    {
        var normalizedKey = NormalizeKey(key);

        if (TrySetString(config, normalizedKey, value))
        {
            error = ValidateConfigValue(config, normalizedKey);
            return error.Length == 0;
        }

        if (IsBooleanKey(normalizedKey))
        {
            if (!TrySetBoolean(
                    config,
                    normalizedKey,
                    value,
                    out error))
            {
                return false;
            }

            error = ValidateConfigValue(config, normalizedKey);
            return error.Length == 0;
        }

        if (IsIntegerKey(normalizedKey))
        {
            if (!TrySetInteger(
                    config,
                    normalizedKey,
                    value,
                    out error))
            {
                return false;
            }

            error = ValidateConfigValue(config, normalizedKey);
            return error.Length == 0;
        }

        error = $"不明な設定キーです: {key}";
        return false;
    }

    private static bool TrySetString(
        CollectorConfig config,
        string key,
        string value)
    {
        switch (key)
        {
            case "temp_dir":
                config.TempDir = value;
                return true;
            case "output_dir":
                config.OutputDir = value;
                return true;
            case "encoding":
                config.Encoding = value;
                return true;
            case "marker_prefix":
                config.MarkerPrefix = value;
                return true;
            case "timezone":
                config.Timezone = value;
                return true;
            case "hash_algorithm":
                config.HashAlgorithm = value;
                return true;
            case "log_level":
                config.LogLevel = value.ToLowerInvariant();
                return true;
            case "minimize_button_behavior":
                config.MinimizeButtonBehavior =
                    value.ToLowerInvariant();
                return true;
            case "close_button_behavior":
                config.CloseButtonBehavior =
                    value.ToLowerInvariant();
                return true;
            default:
                return false;
        }
    }

    private static bool TrySetBoolean(
        CollectorConfig config,
        string key,
        string value,
        out string error)
    {
        if (!bool.TryParse(value, out var parsed))
        {
            error = $"{key} は true または false で指定してください。";
            return false;
        }

        switch (key)
        {
            case "watch_window1":
                config.WatchWindow1 = parsed;
                break;
            case "watch_window2":
                config.WatchWindow2 = parsed;
                break;
            case "raw_output":
                config.RawOutput = parsed;
                break;
            case "canonical_output":
                config.CanonicalOutput = parsed;
                break;
            case "dedupe_raw":
                config.DedupeRaw = parsed;
                break;
            case "dedupe_canonical":
                config.DedupeCanonical = parsed;
                break;
            case "marker_detection":
                config.MarkerDetection = parsed;
                break;
            case "auto_start_collection_on_launch":
                config.AutoStartCollectionOnLaunch = parsed;
                break;
            case "minimize_to_tray_while_collecting":
                config.MinimizeToTrayWhileCollecting = parsed;
                break;
            case "show_tray_notifications":
                config.ShowTrayNotifications = parsed;
                break;
        }

        error = string.Empty;
        return true;
    }

    private static bool TrySetInteger(
        CollectorConfig config,
        string key,
        string value,
        out string error)
    {
        if (!int.TryParse(
                value,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var parsed))
        {
            error = $"{key} は整数で指定してください。";
            return false;
        }

        switch (key)
        {
            case "polling_interval_ms":
                config.PollingIntervalMs = parsed;
                break;
            case "rotation_slots":
                config.RotationSlots = parsed;
                break;
            case "flush_interval_ms":
                config.FlushIntervalMs = parsed;
                break;
        }

        error = string.Empty;
        return true;
    }

    private static string ValidateConfigValue(
        CollectorConfig config,
        string key)
    {
        return key switch
        {
            "polling_interval_ms"
                when config.PollingIntervalMs
                    is < PollingOptions.MinimumIntervalMs
                    or > PollingOptions.MaximumIntervalMs =>
                $"polling_interval_ms は"
                + $"{PollingOptions.MinimumIntervalMs}～"
                + $"{PollingOptions.MaximumIntervalMs}で指定してください。",
            "rotation_slots"
                when config.RotationSlots is < 1 or > 20 =>
                "rotation_slots は1～20で指定してください。",
            "log_level"
                when config.LogLevel is not (
                    "debug" or "info" or "warning" or "error") =>
                "log_level は debug、info、warning、error の"
                + "いずれかを指定してください。",
            "minimize_button_behavior"
                when config.MinimizeButtonBehavior is not (
                    "normal" or "tray") =>
                "minimize_button_behavior は normal または tray を"
                + "指定してください。",
            "close_button_behavior"
                when config.CloseButtonBehavior is not (
                    "confirm_exit"
                    or "tray_when_collecting"
                    or "always_tray") =>
                "close_button_behavior は confirm_exit、"
                + "tray_when_collecting、always_tray の"
                + "いずれかを指定してください。",
            _ => string.Empty,
        };
    }

    private static string NormalizeKey(string key)
    {
        return key.Trim().ToLowerInvariant().Replace('-', '_');
    }

    private static bool IsBooleanKey(string key)
    {
        return key is "watch_window1"
            or "watch_window2"
            or "raw_output"
            or "canonical_output"
            or "dedupe_raw"
            or "dedupe_canonical"
            or "marker_detection"
            or "auto_start_collection_on_launch"
            or "minimize_to_tray_while_collecting"
            or "show_tray_notifications";
    }

    private static bool IsIntegerKey(string key)
    {
        return key is "polling_interval_ms"
            or "rotation_slots"
            or "flush_interval_ms";
    }

    private async Task<IpcResponse?> TrySendIpcAsync(
        IpcCommand command,
        CancellationToken cancellationToken)
    {
        return _ipcClient is null
            ? null
            : await _ipcClient.TrySendAsync(
                command,
                cancellationToken: cancellationToken);
    }

    private CliExitCode WriteIpcResponse(IpcResponse response)
    {
        if (!response.Success)
        {
            _output.WriteError(response.Message);
            return CliExitCode.CollectionError;
        }

        if (!string.IsNullOrWhiteSpace(response.Message))
        {
            _output.WriteLine(response.Message);
        }

        foreach (var pair in response.Data)
        {
            _output.WriteLine($"{pair.Key}={pair.Value}");
        }

        return CliExitCode.Success;
    }

    private static bool IsImmediatelyApplicableKey(string key)
    {
        return NormalizeKey(key) is "polling_interval_ms"
            or "log_level"
            or "minimize_to_tray_while_collecting"
            or "minimize_button_behavior"
            or "close_button_behavior"
            or "show_tray_notifications";
    }
}
