using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Threading;
using FfxiTempLogCollector.Core;

namespace FfxiTempLogCollector.App;

public sealed class MainViewModel : INotifyPropertyChanged
{
    private readonly GuiCommandController _controller;
    private readonly Dispatcher _dispatcher;
    private readonly RealtimeAnalysisController _realtimeAnalysis;

    private string _statusText = "停止中";
    private string _statusForeground = "#FFFFFF";
    private string _statusBackground = "#162331";
    private string _sessionId = "-";
    private long _rawRecordsWritten;
    private long _canonicalRecordsWritten;
    private string _lastSeenAt = "-";
    private long _warningCount;
    private string _lastError = "-";
    private bool _isShuttingDown;
    private string _realtimeStateText = "停止中";
    private int _realtimeTargetCount;
    private int _realtimeCanonicalCount;
    private long _realtimeTotalDamage;
    private string _realtimeDps = "-";
    private string _realtimeAggregationTime = "-";
    private string _realtimeMemory = "-";
    private long _realtimeDiscardedCount;
    private string _realtimeLastUpdated = "-";

    public MainViewModel(
        GuiCommandController controller,
        Dispatcher dispatcher,
        RealtimeAnalysisController realtimeAnalysis)
    {
        _controller = controller
            ?? throw new ArgumentNullException(nameof(controller));
        _dispatcher = dispatcher
            ?? throw new ArgumentNullException(nameof(dispatcher));
        _realtimeAnalysis = realtimeAnalysis
            ?? throw new ArgumentNullException(nameof(realtimeAnalysis));

        StartCommand = new AsyncRelayCommand(
            StartAsync,
            () => !IsShuttingDown
                && (Status is CollectorStatus.Stopped
                    or CollectorStatus.Error));
        StopCommand = new AsyncRelayCommand(
            StopAsync,
            () => !IsShuttingDown
                && (Status is CollectorStatus.Starting
                    or CollectorStatus.Running));
        SettingsCommand = new RelayCommand(
            _controller.ShowSettings,
            () => !IsShuttingDown);
        PartyMemberSettingsCommand = new RelayCommand(
            ShowPartyMemberSettings,
            () => !IsShuttingDown);
        ChangeTempDirectoryCommand = new RelayCommand(
            () => _controller.ShowSettings(SettingsFocusTarget.TempDirectory),
            () => !IsShuttingDown);
        ChangeOutputDirectoryCommand = new RelayCommand(
            () => _controller.ShowSettings(SettingsFocusTarget.OutputDirectory),
            () => !IsShuttingDown);
        AnalysisCommand = new RelayCommand(
            _controller.ShowAnalysis,
            () => !IsShuttingDown);
        OpenOutputCommand = new RelayCommand(
            _controller.OpenOutputDirectory,
            () => !IsShuttingDown);
        OpenTempCommand = new RelayCommand(
            _controller.OpenTempDirectory,
            () => !IsShuttingDown);
        MinimizeCommand = new RelayCommand(
            _controller.MinimizeToTray,
            () => !IsShuttingDown);
        ExitCommand = new RelayCommand(
            _controller.Exit,
            () => !IsShuttingDown);
        StartRealtimeAnalysisCommand = new RelayCommand(
            _realtimeAnalysis.Start,
            () => !IsShuttingDown
                && RealtimeState != RealtimeAnalysisState.Running);
        StopRealtimeAnalysisCommand = new RelayCommand(
            _realtimeAnalysis.Stop,
            () => !IsShuttingDown
                && RealtimeState == RealtimeAnalysisState.Running);
        ResetRealtimeAnalysisCommand = new RelayCommand(
            _realtimeAnalysis.Reset,
            () => !IsShuttingDown
                && RealtimeState == RealtimeAnalysisState.Running);
        ToggleOverlayCommand = new RelayCommand(
            _controller.ToggleOverlay,
            () => !IsShuttingDown);
        ResetOverlayPositionCommand = new RelayCommand(
            _controller.ResetOverlayPosition,
            () => !IsShuttingDown);

        _controller.Events.StatusChanged += OnStatusChanged;
        _controller.ConfigChanged += OnConfigChanged;
        _controller.PartyMembersChanged += OnPartyMembersChanged;
        _realtimeAnalysis.Updated += OnRealtimeAnalysisUpdated;
        _controller.OverlayVisibilityChanged += OnOverlayVisibilityChanged;
        ApplyStatus(_controller.GetStatus());
        ApplyRealtimeAnalysis(_realtimeAnalysis.Current);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public CollectorStatus Status { get; private set; }

    public RealtimeAnalysisState RealtimeState { get; private set; }

    public string RealtimeStateText { get => _realtimeStateText; private set => SetProperty(ref _realtimeStateText, value); }

    public int RealtimeTargetCount { get => _realtimeTargetCount; private set => SetProperty(ref _realtimeTargetCount, value); }

    public int RealtimeCanonicalCount { get => _realtimeCanonicalCount; private set => SetProperty(ref _realtimeCanonicalCount, value); }

    public long RealtimeTotalDamage { get => _realtimeTotalDamage; private set => SetProperty(ref _realtimeTotalDamage, value); }

    public string RealtimeDps { get => _realtimeDps; private set => SetProperty(ref _realtimeDps, value); }

    public string RealtimeAggregationTime { get => _realtimeAggregationTime; private set => SetProperty(ref _realtimeAggregationTime, value); }

    public string RealtimeMemory { get => _realtimeMemory; private set => SetProperty(ref _realtimeMemory, value); }

    public long RealtimeDiscardedCount { get => _realtimeDiscardedCount; private set => SetProperty(ref _realtimeDiscardedCount, value); }

    public string RealtimeLastUpdated { get => _realtimeLastUpdated; private set => SetProperty(ref _realtimeLastUpdated, value); }

    public string StatusText
    {
        get => _statusText;
        private set => SetProperty(ref _statusText, value);
    }

    public string StatusForeground
    {
        get => _statusForeground;
        private set => SetProperty(ref _statusForeground, value);
    }

    public string StatusBackground
    {
        get => _statusBackground;
        private set => SetProperty(ref _statusBackground, value);
    }

    public bool IsShuttingDown
    {
        get => _isShuttingDown;
        private set => SetProperty(ref _isShuttingDown, value);
    }

    public string TempDirectory => _controller.Config.TempDir;

    public string OutputDirectory => _controller.Config.OutputDir;

    public string PartyMemberSummary
    {
        get
        {
            var members = _controller.GetPartyMembers();
            return members.Count == 0
                ? "未登録"
                : $"{members.Count}名: {string.Join(" / ", members)}";
        }
    }

    public string SessionId
    {
        get => _sessionId;
        private set => SetProperty(ref _sessionId, value);
    }

    public long RawRecordsWritten
    {
        get => _rawRecordsWritten;
        private set => SetProperty(ref _rawRecordsWritten, value);
    }

    public long CanonicalRecordsWritten
    {
        get => _canonicalRecordsWritten;
        private set => SetProperty(
            ref _canonicalRecordsWritten,
            value);
    }

    public string LastSeenAt
    {
        get => _lastSeenAt;
        private set => SetProperty(ref _lastSeenAt, value);
    }

    public long WarningCount
    {
        get => _warningCount;
        private set => SetProperty(ref _warningCount, value);
    }

    public string LastError
    {
        get => _lastError;
        private set => SetProperty(ref _lastError, value);
    }

    public AsyncRelayCommand StartCommand { get; }

    public AsyncRelayCommand StopCommand { get; }

    public RelayCommand SettingsCommand { get; }

    public RelayCommand PartyMemberSettingsCommand { get; }

    public RelayCommand ChangeTempDirectoryCommand { get; }

    public RelayCommand ChangeOutputDirectoryCommand { get; }

    public RelayCommand AnalysisCommand { get; }

    public RelayCommand OpenOutputCommand { get; }

    public RelayCommand OpenTempCommand { get; }

    public RelayCommand MinimizeCommand { get; }

    public RelayCommand ExitCommand { get; }

    public RelayCommand StartRealtimeAnalysisCommand { get; }

    public RelayCommand StopRealtimeAnalysisCommand { get; }

    public RelayCommand ResetRealtimeAnalysisCommand { get; }

    public RelayCommand ToggleOverlayCommand { get; }

    public RelayCommand ResetOverlayPositionCommand { get; }

    public string OverlayButtonText => _controller.IsOverlayVisible
        ? "オーバーレイ非表示"
        : "オーバーレイ表示";

    public async Task InitializeAsync()
    {
        _controller.RestoreOverlayVisibility();
        if (_controller.Config.AutoStartCollectionOnLaunch
            && (Status is CollectorStatus.Stopped
                or CollectorStatus.Error))
        {
            await StartAsync();
        }
    }

    public async Task ShutdownAsync()
    {
        _controller.Events.StatusChanged -= OnStatusChanged;
        _controller.ConfigChanged -= OnConfigChanged;
        _controller.PartyMembersChanged -= OnPartyMembersChanged;
        _realtimeAnalysis.Updated -= OnRealtimeAnalysisUpdated;
        _controller.OverlayVisibilityChanged -= OnOverlayVisibilityChanged;
        await _realtimeAnalysis.DisposeAsync();
        await _controller.DisposeAsync();
    }

    public WindowCloseAction GetCloseAction()
    {
        return _controller.GetCloseAction();
    }

    public void BeginShutdown()
    {
        if (IsShuttingDown)
        {
            return;
        }

        IsShuttingDown = true;
        StatusText = "終了しています...";
        StatusForeground = "#FFFFFF";
        StatusBackground = "#B45309";
        RaiseCommandCanExecuteChanged();
    }

    public void MinimizeToTray()
    {
        _controller.MinimizeToTray();
    }

    public void HandleWindowStateChanged()
    {
        _controller.HandleWindowStateChanged();
    }

    private async Task StartAsync()
    {
        try
        {
            var started = await _controller.StartAsync();

            if (started
                && _controller.Config.MinimizeToTrayWhileCollecting)
            {
                _controller.MinimizeToTray();
            }
        }
        catch (Exception exception)
        {
            LastError = exception.Message;
        }
    }

    private async Task StopAsync()
    {
        try
        {
            await _controller.StopAsync();
        }
        catch (Exception exception)
        {
            LastError = exception.Message;
        }
    }

    private void OnStatusChanged(
        object? sender,
        CollectorStatusSnapshot snapshot)
    {
        _dispatcher.InvokeAsync(() => ApplyStatus(snapshot));
    }

    private void OnConfigChanged(object? sender, EventArgs eventArgs)
    {
        OnPropertyChanged(nameof(TempDirectory));
        OnPropertyChanged(nameof(OutputDirectory));
    }

    private void OnPartyMembersChanged(object? sender, EventArgs eventArgs)
    {
        _dispatcher.InvokeAsync(() => OnPropertyChanged(nameof(PartyMemberSummary)));
    }

    private void ShowPartyMemberSettings()
    {
        var actors = _realtimeAnalysis.Current.Result?.ActorSummaries
            .Select(actor => actor.Actor)
            ?? [];
        _controller.ShowPartyMemberSettings(actors);
    }

    private void OnRealtimeAnalysisUpdated(
        object? sender,
        RealtimeAnalysisSnapshot snapshot)
    {
        _dispatcher.InvokeAsync(() => ApplyRealtimeAnalysis(snapshot));
    }

    private void OnOverlayVisibilityChanged(object? sender, EventArgs eventArgs)
    {
        _dispatcher.InvokeAsync(() => OnPropertyChanged(nameof(OverlayButtonText)));
    }

    private void ApplyRealtimeAnalysis(RealtimeAnalysisSnapshot snapshot)
    {
        RealtimeState = snapshot.State;
        RealtimeStateText = snapshot.State switch
        {
            RealtimeAnalysisState.Running => "分析中",
            RealtimeAnalysisState.Completed => "分析終了",
            _ => "停止中",
        };
        RealtimeTargetCount = snapshot.TargetRecordCount;
        RealtimeCanonicalCount = snapshot.CanonicalRecordCount;
        RealtimeTotalDamage = snapshot.Result?.ActorSummaries.Sum(actor => (long)actor.TotalDamage) ?? 0;
        var dpsValues = snapshot.Result?.ActorSummaries
            .Where(actor => actor.Dps is not null)
            .Select(actor => actor.Dps!.Value)
            .ToArray() ?? [];
        RealtimeDps = dpsValues.Length == 0 ? "-" : dpsValues.Sum().ToString("N2");
        RealtimeAggregationTime = $"{snapshot.LastAggregationTime.TotalMilliseconds:N1} ms";
        RealtimeMemory = $"{snapshot.ProcessMemoryBytes / 1024d / 1024d:N1} MB";
        RealtimeDiscardedCount = snapshot.DiscardedAggregationCount;
        RealtimeLastUpdated = snapshot.LastUpdatedAt?.ToLocalTime().ToString("HH:mm:ss.fff") ?? "-";
        if (!string.IsNullOrWhiteSpace(snapshot.ErrorMessage))
        {
            LastError = snapshot.ErrorMessage;
        }

        RaiseCommandCanExecuteChanged();
    }

    private void ApplyStatus(CollectorStatusSnapshot snapshot)
    {
        if (IsShuttingDown)
        {
            return;
        }

        Status = snapshot.Status;
        StatusText = GetStatusText(snapshot.Status);
        StatusForeground = GetStatusForeground(snapshot.Status);
        StatusBackground = GetStatusBackground(snapshot.Status);
        SessionId = string.IsNullOrWhiteSpace(snapshot.SessionId)
            ? "-"
            : snapshot.SessionId;
        RawRecordsWritten = snapshot.RawRecordsWritten;
        CanonicalRecordsWritten =
            snapshot.CanonicalRecordsWritten;
        LastSeenAt = snapshot.LastSeenAt?.ToLocalTime()
            .ToString("yyyy/MM/dd HH:mm:ss")
            ?? "-";
        WarningCount = snapshot.WarningCount;
        LastError = string.IsNullOrWhiteSpace(snapshot.LastError)
            ? "-"
            : snapshot.LastError;

        RaiseCommandCanExecuteChanged();
    }

    private static string GetStatusText(CollectorStatus status)
    {
        return status switch
        {
            CollectorStatus.Stopped => "停止中",
            CollectorStatus.Starting => "開始処理中",
            CollectorStatus.Running => "収集中",
            CollectorStatus.Stopping => "停止処理中",
            CollectorStatus.Error => "エラー",
            _ => "不明",
        };
    }

    private static string GetStatusForeground(CollectorStatus status)
    {
        return status switch
        {
            CollectorStatus.Running => "#ECFDF5",
            CollectorStatus.Starting => "#FFFBEB",
            CollectorStatus.Stopping => "#FFFBEB",
            CollectorStatus.Error => "#FEF2F2",
            _ => "#FFFFFF",
        };
    }

    private static string GetStatusBackground(CollectorStatus status)
    {
        return status switch
        {
            CollectorStatus.Running => "#047857",
            CollectorStatus.Starting => "#B45309",
            CollectorStatus.Stopping => "#B45309",
            CollectorStatus.Error => "#B42318",
            _ => "#162331",
        };
    }

    private void RaiseCommandCanExecuteChanged()
    {
        StartCommand.RaiseCanExecuteChanged();
        StopCommand.RaiseCanExecuteChanged();
        SettingsCommand.RaiseCanExecuteChanged();
        PartyMemberSettingsCommand.RaiseCanExecuteChanged();
        ChangeTempDirectoryCommand.RaiseCanExecuteChanged();
        ChangeOutputDirectoryCommand.RaiseCanExecuteChanged();
        AnalysisCommand.RaiseCanExecuteChanged();
        OpenOutputCommand.RaiseCanExecuteChanged();
        OpenTempCommand.RaiseCanExecuteChanged();
        MinimizeCommand.RaiseCanExecuteChanged();
        ExitCommand.RaiseCanExecuteChanged();
        StartRealtimeAnalysisCommand.RaiseCanExecuteChanged();
        StopRealtimeAnalysisCommand.RaiseCanExecuteChanged();
        ResetRealtimeAnalysisCommand.RaiseCanExecuteChanged();
        ToggleOverlayCommand.RaiseCanExecuteChanged();
        ResetOverlayPositionCommand.RaiseCanExecuteChanged();
    }

    private void SetProperty<T>(
        ref T field,
        T value,
        [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return;
        }

        field = value;
        OnPropertyChanged(propertyName);
    }

    private void OnPropertyChanged(
        [CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(
            this,
            new PropertyChangedEventArgs(propertyName));
    }
}
