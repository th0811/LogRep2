using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using FFXI_LogAnalyzer.Core;
using Microsoft.VisualBasic.FileIO;

namespace FFXI_LogAnalyzer.App;

public sealed class MainViewModel : INotifyPropertyChanged
{
    private readonly SessionOpenService _sessionOpenService;
    private readonly DialogService _dialogService;
    private readonly AnalyzerSettingsStore _settingsStore;
    private readonly AssistantToolLauncher _assistantToolLauncher;
    private readonly CanonicalRecordReader _canonicalRecordReader = new();
    private AnalyzerSettings _settings;
    private SessionSelectionViewModel? _selectedSession;
    private string _statusMessage = "セッション出力先が設定されていません。メイン画面の設定で指定してください。";
    private string _sessionRootFolderPath = "未選択";
    private string _selectedFolderPath = "未選択";
    private int _selectedTabIndex;
    private CancellationTokenSource? _loadCancellation;
    private bool _isBusy;
    private string _busyText = string.Empty;

    public MainViewModel(SessionOpenService sessionOpenService, DialogService dialogService)
        : this(
            sessionOpenService,
            dialogService,
            new AnalyzerSettingsStore(),
            new AssistantToolLauncher())
    {
    }

    public MainViewModel(
        SessionOpenService sessionOpenService,
        DialogService dialogService,
        AnalyzerSettingsStore settingsStore,
        AssistantToolLauncher? assistantToolLauncher = null)
    {
        _sessionOpenService = sessionOpenService
            ?? throw new ArgumentNullException(nameof(sessionOpenService));
        _dialogService = dialogService
            ?? throw new ArgumentNullException(nameof(dialogService));
        _settingsStore = settingsStore
            ?? throw new ArgumentNullException(nameof(settingsStore));
        _assistantToolLauncher = assistantToolLauncher
            ?? new AssistantToolLauncher();
        _settings = _settingsStore.Load();
        AnalysisResult = new AnalysisResultViewModel(_settingsStore);
        AnalysisRange.AnalysisCompleted += OnAnalysisCompleted;
        AnalysisResult.PropertyChanged += OnAnalysisResultPropertyChanged;
        GoToNextStepCommand = new RelayCommand(
            () => SelectedTabIndex = 1,
            () => HasSession);
        OpenSessionRootFolderCommand = new RelayCommand(
            OpenSessionRootFolder,
            () => HasSessionRootFolder && !IsBusy);
        RefreshSessionsCommand = new FfxiTempLogCollector.App.AsyncRelayCommand(
            RefreshSessionsAsync,
            () => HasSessionRootFolder && !IsBusy);
        CancelLoadingCommand = new RelayCommand(
            CancelLoading,
            () => IsBusy);
        OpenSelectedSessionFolderCommand = new RelayCommand(
            OpenSelectedSessionFolder,
            () => SelectedSession is not null && !IsBusy);
        OpenGameLogCommand = new FfxiTempLogCollector.App.AsyncRelayCommand(
            OpenGameLogAsync,
            () => SelectedSession is not null && !IsBusy);
        DeleteSelectedSessionCommand = new FfxiTempLogCollector.App.AsyncRelayCommand(
            DeleteSelectedSessionAsync,
            () => SelectedSession is not null && !IsBusy);
        EnableAllSessionsCommand = new FfxiTempLogCollector.App.AsyncRelayCommand(
            () => SetAllSessionsEnabledAsync(true),
            () => Sessions.Count > 0 && !IsBusy);
        DisableAllSessionsCommand = new FfxiTempLogCollector.App.AsyncRelayCommand(
            () => SetAllSessionsEnabledAsync(false),
            () => Sessions.Count > 0 && !IsBusy);
        _ = LoadConfiguredSessionRootAsync();
        RefreshNavigationState();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public RelayCommand OpenSessionRootFolderCommand { get; }

    public FfxiTempLogCollector.App.AsyncRelayCommand RefreshSessionsCommand { get; }

    public RelayCommand CancelLoadingCommand { get; }

    public RelayCommand OpenSelectedSessionFolderCommand { get; }

    public FfxiTempLogCollector.App.AsyncRelayCommand OpenGameLogCommand { get; }

    public FfxiTempLogCollector.App.AsyncRelayCommand DeleteSelectedSessionCommand { get; }

    public FfxiTempLogCollector.App.AsyncRelayCommand EnableAllSessionsCommand { get; }

    public FfxiTempLogCollector.App.AsyncRelayCommand DisableAllSessionsCommand { get; }

    public RelayCommand GoToNextStepCommand { get; }

    public ObservableCollection<SessionSelectionViewModel> Sessions { get; } = [];

    public ObservableCollection<SessionInfoRow> SessionInfoRows { get; } = [];

    public ObservableCollection<string> Warnings { get; } = [];

    public AnalysisRangeViewModel AnalysisRange { get; } = new();

    public AnalysisResultViewModel AnalysisResult { get; }

    public SessionSelectionViewModel? SelectedSession
    {
        get => _selectedSession;
        set
        {
            if (SetProperty(ref _selectedSession, value))
            {
                OpenSelectedSessionFolderCommand.RaiseCanExecuteChanged();
                OpenGameLogCommand.RaiseCanExecuteChanged();
                DeleteSelectedSessionCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public string SessionRootFolderPath
    {
        get => _sessionRootFolderPath;
        private set => SetProperty(ref _sessionRootFolderPath, value);
    }

    public string SelectedFolderPath
    {
        get => _selectedFolderPath;
        private set => SetProperty(ref _selectedFolderPath, value);
    }

    public int SelectedTabIndex
    {
        get => _selectedTabIndex;
        set => SetProperty(ref _selectedTabIndex, value);
    }

    public string NextStepText => HasSession
        ? "セッションを選択しました。次は分析区間を選びます。"
        : "分析するセッションの「分析対象」列にチェックを入れてください。";

    public bool HasSession => Sessions.Any(session => session.IsEnabled);

    public bool HasSessionRootFolder => !string.IsNullOrWhiteSpace(_settings.SessionsRootFolderPath);

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
            {
                OnPropertyChanged(nameof(IsNotBusy));
                RefreshCommandStates();
                OpenSessionRootFolderCommand.RaiseCanExecuteChanged();
                CancelLoadingCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public string AnalysisTargetSummary
    {
        get
        {
            var enabledCount = Sessions.Count(session => session.IsEnabled);
            return enabledCount == 0
                ? "なし"
                : $"{enabledCount:N0}件のセッション";
        }
    }

    public string SessionTabHeader => HasSession
        ? "① セッション選択  ✓"
        : "① セッション選択";

    public string RangeTabHeader => AnalysisResult.HasResult
        ? "② 分析区間  ✓"
        : "② 分析区間";

    public string ResultTabHeader => AnalysisResult.HasResult
        ? "③ 分析結果  ✓"
        : "③ 分析結果";

    public bool IsNotBusy => !IsBusy;

    public string BusyText
    {
        get => _busyText;
        private set => SetProperty(ref _busyText, value);
    }

    private void OpenSessionRootFolder()
    {
        if (string.IsNullOrWhiteSpace(_settings.SessionsRootFolderPath))
        {
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = _settings.SessionsRootFolderPath,
                UseShellExecute = true,
            });
        }
        catch (Exception exception)
        {
            StatusMessage = $"セッション出力先を開けませんでした: {exception.Message}";
        }
    }

    public void ReloadSharedSessionRoot()
    {
        _settings = _settingsStore.Load();
        _ = LoadConfiguredSessionRootAsync();
    }

    private Task RefreshSessionsAsync()
    {
        return ReloadSessionsFromRootAsync(preserveEnabledStates: true);
    }

    private async Task LoadConfiguredSessionRootAsync()
    {
        if (string.IsNullOrWhiteSpace(_settings.SessionsRootFolderPath))
        {
            SessionRootFolderPath = "未選択";
            StatusMessage = "セッション出力先が設定されていません。メイン画面の設定で指定してください。";
            RefreshCommandStates();
            return;
        }

        SessionRootFolderPath = _settings.SessionsRootFolderPath;
        await ReloadSessionsFromRootAsync(preserveEnabledStates: true);
    }

    private async Task ReloadSessionsFromRootAsync(bool preserveEnabledStates)
    {
        if (string.IsNullOrWhiteSpace(_settings.SessionsRootFolderPath))
        {
            ClearSessionsInternal();
            ClearCombinedSession();
            StatusMessage = "セッション出力先が設定されていません。メイン画面の設定で指定してください。";
            return;
        }

        var rootFolderPath = _settings.SessionsRootFolderPath;
        SessionRootFolderPath = rootFolderPath;
        if (!Directory.Exists(rootFolderPath))
        {
            ClearSessionsInternal();
            ClearCombinedSession();
            StatusMessage = "設定済みセッションフォルダが見つかりません";
            return;
        }

        IReadOnlyDictionary<string, bool> enabledStates = preserveEnabledStates
            ? Sessions.ToDictionary(
                session => session.FolderPath,
                session => session.IsEnabled,
                StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        ClearSessionsInternal();

        var sessionFolders = GetSessionFolders(rootFolderPath);

        if (sessionFolders.Length == 0)
        {
            ClearCombinedSession();
            StatusMessage = "追加可能なセッションフォルダが見つかりませんでした。";
            return;
        }

        _loadCancellation?.Cancel();
        _loadCancellation?.Dispose();
        _loadCancellation = new CancellationTokenSource();
        var cancellationToken = _loadCancellation.Token;
        IsBusy = true;
        var added = 0;
        try
        {
            for (var index = 0; index < sessionFolders.Length; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                BusyText = $"セッションを読み込み中... {index + 1:N0}/{sessionFolders.Length:N0}";
                StatusMessage = BusyText;
                var sessionFolder = sessionFolders[index];
                var attempt = await Task.Run(
                    () => LoadSessionFromPath(sessionFolder),
                    cancellationToken);
                if (attempt.Session is null)
                {
                    AddWarning(sessionFolder, attempt.Errors);
                    continue;
                }

                attempt.Session.IsEnabled = enabledStates.TryGetValue(
                    sessionFolder,
                    out var previousIsEnabled)
                        ? previousIsEnabled
                        : true;
                attempt.Session.PropertyChanged += OnSessionSelectionChanged;
                Sessions.Add(attempt.Session);
                SelectedSession = attempt.Session;
                added++;
            }

            BusyText = "分析対象を準備中...";
            await RefreshCombinedSessionAsync(cancellationToken);
            StatusMessage = $"{added:N0} 件のセッションを読み込みました。";
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "セッションの読み込みをキャンセルしました。";
            await RefreshCombinedSessionAsync(CancellationToken.None);
        }
        catch (Exception exception)
        {
            StatusMessage = $"セッションの読み込みに失敗しました: {exception.Message}";
        }
        finally
        {
            BusyText = string.Empty;
            IsBusy = false;
        }
    }

    private static string[] GetSessionFolders(string rootFolderPath)
    {
        try
        {
            return Directory
                .EnumerateDirectories(rootFolderPath)
                .Where(directory => File.Exists(Path.Combine(directory, "session.json")))
                .Order(StringComparer.Ordinal)
                .ToArray();
        }
        catch (Exception)
        {
            return [];
        }
    }

    private SessionLoadAttempt LoadSessionFromPath(string folderPath)
    {
        var normalizedFolderPath = Path.GetFullPath(folderPath);
        var result = _sessionOpenService.Open(normalizedFolderPath);
        if (!result.IsSuccess || result.Session is null)
        {
            return new SessionLoadAttempt(null, result.Errors);
        }

        var canonicalRecords = _canonicalRecordReader.Read(result.Session.CanonicalRecordsPath);
        if (!canonicalRecords.IsSuccess)
        {
            return new SessionLoadAttempt(null, canonicalRecords.Errors);
        }

        var warnings = result.Warnings
            .Concat(canonicalRecords.LineErrors.Select(
                error => $"canonical_records.jsonl {error.LineNumber}行目: {error.Message}"))
            .ToArray();
        return new SessionLoadAttempt(
            new SessionSelectionViewModel(
                result.Session,
                canonicalRecords.Records,
                warnings),
            []);
    }

    private async Task SetAllSessionsEnabledAsync(bool isEnabled)
    {
        IsBusy = true;
        BusyText = "分析対象を更新中...";
        try
        {
            foreach (var session in Sessions)
            {
                session.PropertyChanged -= OnSessionSelectionChanged;
            }

            try
            {
                foreach (var session in Sessions)
                {
                    session.IsEnabled = isEnabled;
                }
            }
            finally
            {
                foreach (var session in Sessions)
                {
                    session.PropertyChanged += OnSessionSelectionChanged;
                }
            }

            await RefreshCombinedSessionAsync(CancellationToken.None);
            StatusMessage = isEnabled
                ? "すべてのセッションを分析対象にしました。"
                : "すべてのセッションを分析対象から外しました。";
        }
        catch (Exception exception)
        {
            StatusMessage = $"分析対象を更新できませんでした: {exception.Message}";
        }
        finally
        {
            BusyText = string.Empty;
            IsBusy = false;
        }
    }

    private void ClearSessionsInternal()
    {
        foreach (var session in Sessions)
        {
            session.PropertyChanged -= OnSessionSelectionChanged;
        }

        Sessions.Clear();
        SelectedSession = null;
    }

    private void OpenSelectedSessionFolder()
    {
        if (SelectedSession is null)
        {
            return;
        }

        try
        {
            Process.Start(
                new ProcessStartInfo
                {
                    FileName = SelectedSession.FolderPath,
                    UseShellExecute = true,
                });
        }
        catch (Exception exception)
        {
            StatusMessage = $"セッションフォルダーを開けませんでした: {exception.Message}";
        }
    }

    private async Task OpenGameLogAsync()
    {
        var session = SelectedSession;
        if (session is null)
        {
            return;
        }

        IsBusy = true;
        BusyText = "ゲーム内ログのスナップショットを作成しています...";

        try
        {
            var result = await Task.Run(
                () => _assistantToolLauncher.Launch(session.FolderPath));
            StatusMessage = result.Message;
        }
        finally
        {
            BusyText = string.Empty;
            IsBusy = false;
        }
    }

    private async Task DeleteSelectedSessionAsync()
    {
        var session = SelectedSession;
        if (session is null)
        {
            return;
        }

        if (session.Session.SessionInfo.Status == SessionStatus.Active)
        {
            _dialogService.ShowInformation(
                "収集中のセッションは削除できません。収集を停止してから再度実行してください。");
            return;
        }

        if (!IsDirectChildOfSessionRoot(
                _settings.SessionsRootFolderPath,
                session.FolderPath))
        {
            _dialogService.ShowError(
                "セッション出力先の直下ではないため、安全のため削除を中止しました。");
            return;
        }

        if (!_dialogService.ConfirmSessionDeletion(session.SessionId))
        {
            StatusMessage = "セッションの削除をキャンセルしました。";
            return;
        }

        try
        {
            await Task.Run(() => FileSystem.DeleteDirectory(
                session.FolderPath,
                UIOption.OnlyErrorDialogs,
                RecycleOption.SendToRecycleBin));
            session.PropertyChanged -= OnSessionSelectionChanged;
            Sessions.Remove(session);
            SelectedSession = Sessions.LastOrDefault();
            await RefreshCombinedSessionAsync(CancellationToken.None);
            StatusMessage = $"セッション「{session.SessionId}」をごみ箱へ移動しました。";
        }
        catch (Exception exception)
        {
            StatusMessage = $"セッションを削除できませんでした: {exception.Message}";
        }
    }

    internal static bool IsDirectChildOfSessionRoot(
        string? sessionRootFolderPath,
        string folderPath)
    {
        if (string.IsNullOrWhiteSpace(sessionRootFolderPath))
        {
            return false;
        }

        var root = Path.TrimEndingDirectorySeparator(
            Path.GetFullPath(sessionRootFolderPath));
        var target = Path.TrimEndingDirectorySeparator(
            Path.GetFullPath(folderPath));
        var parent = Directory.GetParent(target)?.FullName;
        return !string.Equals(
                root,
                target,
                StringComparison.OrdinalIgnoreCase)
            && string.Equals(
                root,
                Path.TrimEndingDirectorySeparator(parent ?? string.Empty),
                StringComparison.OrdinalIgnoreCase);
    }

    private async Task RefreshCombinedSessionAsync(
        CancellationToken cancellationToken)
    {
        var enabledSessions = Sessions
            .Where(session => session.IsEnabled)
            .ToArray();

        SessionInfoRows.Clear();
        Warnings.Clear();
        foreach (var warning in enabledSessions.SelectMany(session => session.Warnings))
        {
            Warnings.Add(warning);
        }

        if (enabledSessions.Length == 0)
        {
            ClearCombinedSession();
            return;
        }

        foreach (var row in BuildSessionRows(enabledSessions))
        {
            SessionInfoRows.Add(row);
        }

        SelectedFolderPath = enabledSessions.Length == 1
            ? enabledSessions[0].FolderPath
            : $"{enabledSessions.Length:N0} 件のセッションを結合";

        var records = await Task.Run(
            () => BuildCombinedRecords(enabledSessions),
            cancellationToken);
        await AnalysisRange.LoadRecordsAsync(records, cancellationToken);
        AnalysisResult.Clear();
        RefreshCommandStates();
    }

    private void ClearCombinedSession()
    {
        SessionInfoRows.Clear();
        Warnings.Clear();
        AnalysisRange.Clear();
        AnalysisResult.Clear();
        SelectedFolderPath = "未選択";
        RefreshCommandStates();
    }

    private void CancelLoading()
    {
        _loadCancellation?.Cancel();
    }

    private static IReadOnlyList<CanonicalRecord> BuildCombinedRecords(
        IReadOnlyList<SessionSelectionViewModel> enabledSessions)
    {
        return enabledSessions
            .SelectMany(session => session.Records)
            .OrderBy(record => record.FirstSeenAt ?? DateTimeOffset.MaxValue)
            .ThenBy(record => record.SessionId, StringComparer.Ordinal)
            .ThenBy(record => record.Order ?? long.MaxValue)
            .Select((record, index) => CloneWithCombinedOrder(record, index + 1))
            .ToArray();
    }

    private static CanonicalRecord CloneWithCombinedOrder(
        CanonicalRecord record,
        long combinedOrder)
    {
        return new CanonicalRecord
        {
            SchemaVersion = record.SchemaVersion,
            CanonicalRecordId = record.CanonicalRecordId,
            SessionId = record.SessionId,
            Order = combinedOrder,
            FirstSeenAt = record.FirstSeenAt,
            LastSeenAt = record.LastSeenAt,
            SourceWindows = record.SourceWindows,
            SourceFiles = record.SourceFiles,
            SourceRawRecordIds = record.SourceRawRecordIds,
            EventGroup = record.EventGroup,
            SequenceHintMin = record.SequenceHintMin,
            SequenceHintMax = record.SequenceHintMax,
            VisibleText = record.VisibleText,
            MessageTimeText = record.MessageTimeText,
            MessageTimePrecision = record.MessageTimePrecision,
            IsMarker = record.IsMarker,
            MarkerKeyword = record.MarkerKeyword,
            CanonicalKey = record.CanonicalKey
        };
    }

    private async void OnSessionSelectionChanged(
        object? sender,
        PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SessionSelectionViewModel.IsEnabled))
        {
            try
            {
                await RefreshCombinedSessionAsync(CancellationToken.None);
            }
            catch (Exception exception)
            {
                StatusMessage = $"分析対象を更新できませんでした: {exception.Message}";
            }
        }
    }

    private void OnAnalysisCompleted(AnalysisResult result)
    {
        var fallbackSessionTime = Sessions
            .Where(session => session.IsEnabled)
            .Select(session => session.Session.SessionInfo.StartedAt)
            .Where(startedAt => startedAt.HasValue)
            .OrderBy(startedAt => startedAt)
            .FirstOrDefault();
        AnalysisResult.Load(
            result,
            SessionInfoRows.ToArray(),
            fallbackSessionTime,
            AnalysisRange.LastCompletedRangeName);
        StatusMessage = "分析結果を表示しました。";
    }

    private static IReadOnlyList<SessionInfoRow> BuildSessionRows(
        IReadOnlyList<SessionSelectionViewModel> sessions)
    {
        if (sessions.Count == 1)
        {
            return BuildSingleSessionRows(sessions[0].Session);
        }

        return
        [
            new SessionInfoRow("選択セッション数", sessions.Count.ToString("N0")),
            new SessionInfoRow("セッションID", string.Join(", ", sessions.Select(session => session.SessionId))),
            new SessionInfoRow("最も早い開始時刻", ToDisplay(sessions.Min(session => session.Session.SessionInfo.StartedAt))),
            new SessionInfoRow("最も遅い終了時刻", ToDisplay(sessions.Max(session => session.Session.SessionInfo.EndedAt))),
            new SessionInfoRow("正規化ログ件数", sessions.Sum(session => session.Records.Count).ToString("N0")),
            new SessionInfoRow("マーカー件数", sessions.Sum(session => session.Records.Count(record => record.IsMarker)).ToString("N0")),
            new SessionInfoRow("欠落警告数", sessions.Sum(session => session.Session.StatsInfo.GapWarnings).ToString("N0")),
            new SessionInfoRow("解析エラー数", sessions.Sum(session => session.Session.StatsInfo.ParseErrors).ToString("N0")),
            new SessionInfoRow("デコードエラー数", sessions.Sum(session => session.Session.StatsInfo.DecodeErrors).ToString("N0"))
        ];
    }

    private static IReadOnlyList<SessionInfoRow> BuildSingleSessionRows(
        AnalyzerInputSession session)
    {
        var info = session.SessionInfo;
        var stats = session.StatsInfo;
        return
        [
            new SessionInfoRow("セッションID", ToDisplay(info.SessionId)),
            new SessionInfoRow("状態", AnalysisDisplayText.ToText(info.Status)),
            new SessionInfoRow("開始時刻", ToDisplay(info.StartedAt)),
            new SessionInfoRow("終了時刻", ToDisplay(info.EndedAt)),
            new SessionInfoRow("収集機能バージョン", ToDisplay(info.CollectorVersion)),
            new SessionInfoRow("セッション形式バージョン", ToDisplay(info.SchemaVersion)),
            new SessionInfoRow("元ログ形式バージョン", ToDisplay(info.RawSchemaVersion)),
            new SessionInfoRow("正規化ログ形式バージョン", ToDisplay(info.CanonicalSchemaVersion)),
            new SessionInfoRow("正規化ログ件数", stats.CanonicalRecordsWritten.ToString("N0")),
            new SessionInfoRow("欠落警告数", stats.GapWarnings.ToString("N0")),
            new SessionInfoRow("解析エラー数", stats.ParseErrors.ToString("N0")),
            new SessionInfoRow("デコードエラー数", stats.DecodeErrors.ToString("N0"))
        ];
    }

    private void AddWarning(string source, IEnumerable<string> warnings)
    {
        foreach (var warning in warnings)
        {
            Warnings.Add($"{source}: {warning}");
        }
    }

    private sealed record SessionLoadAttempt(
        SessionSelectionViewModel? Session,
        IReadOnlyList<string> Errors);

    private void RefreshCommandStates()
    {
        OnPropertyChanged(nameof(HasSession));
        OnPropertyChanged(nameof(HasSessionRootFolder));
        OnPropertyChanged(nameof(AnalysisTargetSummary));
        OnPropertyChanged(nameof(SessionTabHeader));
        OnPropertyChanged(nameof(RangeTabHeader));
        OnPropertyChanged(nameof(ResultTabHeader));
        RefreshSessionsCommand.RaiseCanExecuteChanged();
        OpenSessionRootFolderCommand.RaiseCanExecuteChanged();
        OpenSelectedSessionFolderCommand.RaiseCanExecuteChanged();
        OpenGameLogCommand.RaiseCanExecuteChanged();
        DeleteSelectedSessionCommand.RaiseCanExecuteChanged();
        EnableAllSessionsCommand.RaiseCanExecuteChanged();
        DisableAllSessionsCommand.RaiseCanExecuteChanged();
        RefreshNavigationState();
    }

    private void RefreshNavigationState()
    {
        OnPropertyChanged(nameof(AnalysisTargetSummary));
        OnPropertyChanged(nameof(SessionTabHeader));
        OnPropertyChanged(nameof(RangeTabHeader));
        OnPropertyChanged(nameof(ResultTabHeader));
        OnPropertyChanged(nameof(NextStepText));
        GoToNextStepCommand.RaiseCanExecuteChanged();
    }

    private void OnAnalysisResultPropertyChanged(
        object? sender,
        PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(AnalysisResultViewModel.HasResult))
        {
            return;
        }

        // 分析完了時は結果タブへ自動的に進めて、手順の流れを分かりやすくする。
        if (AnalysisResult.HasResult)
        {
            SelectedTabIndex = 2;
        }

        RefreshNavigationState();
    }

    private static string ToDisplay(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? "-" : value;
    }

    private static string ToDisplay(DateTimeOffset? value)
    {
        return value?.ToString("yyyy-MM-dd HH:mm:ss zzz") ?? "-";
    }

    private bool SetProperty<T>(
        ref T field,
        T value,
        [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    private void OnPropertyChanged(
        [CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(
            this,
            new PropertyChangedEventArgs(propertyName));
    }
}
