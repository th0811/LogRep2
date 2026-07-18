using System.Diagnostics;
using System.IO;
using System.Windows;
using FfxiTempLogCollector.Core;
using FfxiTempLogCollector.Ipc;
using LogRep2.Infrastructure;
using MessageBox = System.Windows.MessageBox;

namespace FfxiTempLogCollector.App;

public sealed class GuiCommandController : IAsyncDisposable
{
    private readonly CollectorService _collectorService;
    private readonly ConfigEditService _configEditService;
    private readonly FolderPickerService _folderPickerService;
    private readonly ConfigLoader _configLoader;
    private readonly LogRep2SettingsStore? _unifiedSettingsStore;
    private Window? _window;
    private TrayMenuController? _trayMenuController;
    private TrayIconController? _trayIconController;
    private WindowCloseBehaviorController? _closeBehaviorController;
    private Window? _analysisWindow;
    private OverlayManager? _overlayManager;
    private bool _disposed;
    private bool _exitRequested;

    public GuiCommandController(
        CollectorService collectorService,
        CollectorConfig config,
        ConfigEditService configEditService,
        ConfigLoader configLoader,
        FolderPickerService? folderPickerService = null,
        LogRep2SettingsStore? unifiedSettingsStore = null)
    {
        _collectorService = collectorService
            ?? throw new ArgumentNullException(nameof(collectorService));
        Config = config
            ?? throw new ArgumentNullException(nameof(config));
        _configEditService = configEditService
            ?? throw new ArgumentNullException(
                nameof(configEditService));
        _configLoader = configLoader
            ?? throw new ArgumentNullException(nameof(configLoader));
        _folderPickerService = folderPickerService
            ?? new FolderPickerService();
        _unifiedSettingsStore = unifiedSettingsStore;
    }

    public CollectorConfig Config { get; }

    public CollectorEvents Events => _collectorService.Events;

    public event EventHandler? ConfigChanged;

    public event EventHandler? PartyMembersChanged;

    public void AttachWindow(Window window)
    {
        ArgumentNullException.ThrowIfNull(window);

        if (_window is not null)
        {
            throw new InvalidOperationException(
                "メインウィンドウは既に設定されています。");
        }

        _window = window;
        _trayMenuController = new TrayMenuController(this);
        _trayIconController = new TrayIconController(
            window,
            _collectorService,
            () => Config,
            _trayMenuController);
        _closeBehaviorController =
            new WindowCloseBehaviorController(
                window,
                () => Config,
                GetStatus);
    }

    public CollectorStatusSnapshot GetStatus()
    {
        return _collectorService.GetStatus();
    }

    public async Task<IpcResponse> HandleIpcCommandAsync(
        IpcCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        return command.Name.ToLowerInvariant() switch
        {
            "status" => CreateStatusResponse(),
            "start" => await HandleStartIpcAsync(cancellationToken),
            "stop" => await HandleStopIpcAsync(cancellationToken),
            "show" => HandleShowIpc(),
            "config-updated" => HandleConfigUpdatedIpc(command),
            _ => IpcResponse.Error(
                $"未対応のIPCコマンドです: {command.Name}"),
        };
    }

    public Task<bool> StartAsync(
        CancellationToken cancellationToken = default)
    {
        return _collectorService.StartAsync(
            new CollectorStartRequest
            {
                Config = ConfigEditService.Clone(Config),
            },
            cancellationToken);
    }

    public Task StopAsync(
        CancellationToken cancellationToken = default)
    {
        return _collectorService.StopAsync(
            new CollectorStopRequest(),
            cancellationToken);
    }

    public void ShowSettings()
    {
        ShowSettings(SettingsFocusTarget.None);
    }

    public void ShowSettings(SettingsFocusTarget focusTarget)
    {
        var settingsWindow = new SettingsWindow(focusTarget)
        {
            Owner = GetWindow(),
        };
        var viewModel = new SettingsViewModel(
            settingsWindow,
            Config,
            _configEditService,
            _folderPickerService);
        viewModel.ConfigSaved += (_, _) =>
        {
            ConfigChanged?.Invoke(this, EventArgs.Empty);
            RefreshAnalysisSettings();
        };
        settingsWindow.DataContext = viewModel;
        settingsWindow.ShowDialog();
    }

    public IReadOnlyList<string> GetPartyMembers()
    {
        return _unifiedSettingsStore?.Load().Analysis.RealtimePartyMembers ?? [];
    }

    public void ShowPartyMemberSettings(IEnumerable<string> detectedActors)
    {
        var store = _unifiedSettingsStore
            ?? throw new InvalidOperationException("統合設定を利用できません。");
        var settings = store.Load();
        var classifier = new FFXI_LogAnalyzer.Core.ActorNameClassifier();
        var candidates = detectedActors
            .Where(actor => classifier.Classify(
                actor,
                settings.Analysis.KnownPcNames,
                settings.Analysis.KnownNpcNames)
                is FFXI_LogAnalyzer.Core.ActorNameKind.RegisteredPc
                    or FFXI_LogAnalyzer.Core.ActorNameKind.PcCandidate)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var window = new PartyMemberManagerWindow
        {
            Owner = GetWindow(),
        };
        window.DataContext = new PartyMemberManagerViewModel(
            settings.Analysis.RealtimePartyMembers,
            candidates,
            members => SavePartyMembers(store, members));
        window.ShowDialog();
    }

    private void SavePartyMembers(
        LogRep2SettingsStore store,
        IReadOnlyList<string> members)
    {
        var settings = store.Load();
        settings.Analysis.RealtimePartyMembers = [.. members];
        foreach (var member in members)
        {
            if (!settings.Analysis.KnownPcNames.Contains(
                    member,
                    StringComparer.OrdinalIgnoreCase))
            {
                settings.Analysis.KnownPcNames.Add(member);
            }
        }

        store.Save(settings);
        _overlayManager?.UpdatePartyMembers(settings.Analysis.RealtimePartyMembers);
        PartyMembersChanged?.Invoke(this, EventArgs.Empty);
    }

    public void AttachOverlayManager(OverlayManager overlayManager)
    {
        if (_overlayManager is not null)
        {
            throw new InvalidOperationException("オーバーレイ管理は既に設定されています。");
        }

        _overlayManager = overlayManager ?? throw new ArgumentNullException(nameof(overlayManager));
    }

    public bool IsOverlayVisible => _overlayManager?.IsVisible == true;

    public event EventHandler? OverlayVisibilityChanged;

    public void ToggleOverlay()
    {
        GetOverlayManager().ToggleVisibility();
    }

    public void HideOverlay()
    {
        if (_overlayManager?.IsVisible == true)
        {
            _overlayManager.Hide();
        }
    }

    public void ResetOverlayPosition()
    {
        GetOverlayManager().ResetPosition();
    }

    public void RestoreOverlayVisibility()
    {
        GetOverlayManager().RestoreConfiguredVisibility();
    }

    public void ShowAnalysis()
    {
        if (_analysisWindow is not null)
        {
            if (_analysisWindow.WindowState == WindowState.Minimized)
            {
                _analysisWindow.WindowState = WindowState.Normal;
            }

            _analysisWindow.Show();
            _analysisWindow.Activate();
            return;
        }

        var analysisWindow = new FFXI_LogAnalyzer.App.MainWindow
        {
            Owner = GetWindow(),
        };
        analysisWindow.Closed += (_, _) => _analysisWindow = null;
        _analysisWindow = analysisWindow;
        analysisWindow.Show();
    }

    public void OpenOutputDirectory()
    {
        OpenDirectory(Config.OutputDir, "出力先フォルダー");
    }

    public void OpenTempDirectory()
    {
        OpenDirectory(Config.TempDir, "TEMPフォルダー");
    }

    public void MinimizeToTray()
    {
        GetTrayIconController().MoveWindowToTray();
    }

    public void ShowWindow()
    {
        GetTrayIconController().ShowWindow();
    }

    public void HandleWindowStateChanged()
    {
        if (GetWindow().WindowState == WindowState.Minimized
            && GetCloseBehaviorController()
                .ShouldMoveMinimizedWindowToTray())
        {
            MinimizeToTray();
        }
    }

    public void Exit()
    {
        _exitRequested = true;
        GetWindow().Close();
    }

    public WindowCloseAction GetCloseAction()
    {
        return GetCloseBehaviorController()
            .GetCloseAction(_exitRequested);
    }

    public void ShowOperationError(
        string message,
        Exception exception)
    {
        var detail = $"{message}\n{exception.Message}";
        MessageBox.Show(
            GetWindow(),
            detail,
            "操作エラー",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
        _trayIconController?.NotifyOutputFailure(detail);
    }

    private IpcResponse CreateStatusResponse()
    {
        var status = GetStatus();
        return IpcResponse.Ok(
            data: new Dictionary<string, string>
            {
                ["status"] =
                    status.Status.ToString().ToLowerInvariant(),
                ["session_id"] = status.SessionId ?? string.Empty,
                ["raw_records_written"] =
                    status.RawRecordsWritten.ToString(),
                ["canonical_records_written"] =
                    status.CanonicalRecordsWritten.ToString(),
                ["last_error"] = status.LastError ?? string.Empty,
            });
    }

    private async Task<IpcResponse> HandleStartIpcAsync(
        CancellationToken cancellationToken)
    {
        var started = await StartAsync(cancellationToken);
        return started
            ? IpcResponse.Ok("ログ収集を開始しました。")
            : IpcResponse.Error(
                GetStatus().LastError
                ?? "ログ収集は既に開始されています。");
    }

    private async Task<IpcResponse> HandleStopIpcAsync(
        CancellationToken cancellationToken)
    {
        await StopAsync(cancellationToken);
        return IpcResponse.Ok("ログ収集を停止しました。");
    }

    private IpcResponse HandleShowIpc()
    {
        GetWindow().Dispatcher.BeginInvoke(ShowWindow);
        return IpcResponse.Ok("メイン画面を表示しました。");
    }

    private IpcResponse HandleConfigUpdatedIpc(IpcCommand command)
    {
        command.Arguments.TryGetValue(
            "config_path",
            out var configPath);
        var updated = string.IsNullOrWhiteSpace(configPath)
            && _unifiedSettingsStore is not null
                ? _unifiedSettingsStore.LoadCollectorConfig()
                : _configLoader.Load(configPath);
        ConfigEditService.CopyConfig(updated, Config);
        _collectorService.UpdatePollingInterval(
            Config.PollingIntervalMs);
        _collectorService.UpdateLogLevel(Config.LogLevel);
        GetWindow().Dispatcher.BeginInvoke(
            () =>
            {
                ConfigChanged?.Invoke(this, EventArgs.Empty);
                RefreshAnalysisSettings();
            });
        return IpcResponse.Ok("設定変更を反映しました。");
    }

    private void RefreshAnalysisSettings()
    {
        if (_analysisWindow?.DataContext
            is FFXI_LogAnalyzer.App.MainViewModel viewModel)
        {
            viewModel.ReloadSharedSessionRoot();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _analysisWindow?.Close();
        _analysisWindow = null;
        if (_overlayManager is not null)
        {
            _overlayManager.VisibilityChanged -= OnOverlayVisibilityChanged;
            _overlayManager.Dispose();
            _overlayManager = null;
        }
        _trayIconController?.Dispose();
        _trayMenuController?.Dispose();
        await _collectorService.DisposeAsync();
    }

    private void OpenDirectory(string path, string displayName)
    {
        try
        {
            Directory.CreateDirectory(path);
            StartShell(path);
        }
        catch (Exception exception)
        {
            ShowOperationError(
                $"{displayName}を開けませんでした。",
                exception);
        }
    }

    private static void StartShell(string path)
    {
        Process.Start(
            new ProcessStartInfo
            {
                FileName = path,
                UseShellExecute = true,
            });
    }

    private Window GetWindow()
    {
        return _window
            ?? throw new InvalidOperationException(
                "メインウィンドウが設定されていません。");
    }

    private TrayIconController GetTrayIconController()
    {
        return _trayIconController
            ?? throw new InvalidOperationException(
                "タスクトレイアイコンが初期化されていません。");
    }

    private WindowCloseBehaviorController GetCloseBehaviorController()
    {
        return _closeBehaviorController
            ?? throw new InvalidOperationException(
                "ウィンドウ動作が初期化されていません。");
    }

    private OverlayManager GetOverlayManager()
    {
        return _overlayManager
            ?? throw new InvalidOperationException("オーバーレイ管理が初期化されていません。");
    }

    public void StartOverlayNotifications()
    {
        GetOverlayManager().VisibilityChanged += OnOverlayVisibilityChanged;
    }

    private void OnOverlayVisibilityChanged(object? sender, EventArgs eventArgs)
    {
        OverlayVisibilityChanged?.Invoke(this, EventArgs.Empty);
    }
}
