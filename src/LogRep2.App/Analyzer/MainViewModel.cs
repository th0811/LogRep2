using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using FFXI_LogAnalyzer.Core;
using LogRep2.Infrastructure;
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
    private CombinedRecord[] _combinedRecords = [];
    private string _sessionSearchText = string.Empty;
    private IReadOnlyList<SessionSelectionViewModel> _filteredSessions = [];
    private bool _aliasRefreshPending;

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
        AnalysisRange.PropertyChanged += OnAnalysisRangePropertyChanged;
        AnalysisRange.GoBackRequested += () => SelectedTabIndex = 0;
        AnalysisResult.PropertyChanged += OnAnalysisResultPropertyChanged;
        AnalysisResult.GoBackRequested += () => SelectedTabIndex = 1;
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
        OpenLogExclusionsCommand = new RelayCommand(
            OpenLogExclusions,
            () => Sessions.Count > 0 && !IsBusy && !AnalysisRange.IsBusy);
        DeleteSelectedSessionCommand = new FfxiTempLogCollector.App.AsyncRelayCommand(
            DeleteSelectedSessionAsync,
            () => SelectedSession is not null && !IsBusy);
        EnableAllSessionsCommand = new FfxiTempLogCollector.App.AsyncRelayCommand(
            () => SetAllSessionsEnabledAsync(true),
            () => Sessions.Count > 0 && !IsBusy);
        DisableAllSessionsCommand = new FfxiTempLogCollector.App.AsyncRelayCommand(
            () => SetAllSessionsEnabledAsync(false),
            () => Sessions.Count > 0 && !IsBusy);
        Sessions.CollectionChanged += (_, _) => RefreshSessionFilter();
        _ = LoadConfiguredSessionRootAsync();
        RefreshNavigationState();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public IReadOnlyList<SessionSelectionViewModel> FilteredSessions => _filteredSessions;
    public string SessionSearchSummary => $"表示 {_filteredSessions.Count:N0} / 全 {Sessions.Count:N0}件・対象 {Sessions.Count(session => session.IsEnabled):N0}件（非表示分を含む）";
    public string SessionSearchText
    {
        get => _sessionSearchText;
        set { if (SetProperty(ref _sessionSearchText, value)) RefreshSessionFilter(); }
    }

    private void RefreshSessionFilter()
    {
        if (Sessions.Any(session => session.IsAliasEditing))
        {
            _aliasRefreshPending = true;
            return;
        }
        var selected = SelectedSession;
        var search = SessionSearchText.Trim();
        _filteredSessions = Sessions.Where(session =>
            session.Alias.Contains(search, StringComparison.OrdinalIgnoreCase)
            || session.SessionId.Contains(search, StringComparison.OrdinalIgnoreCase)).ToArray();
        OnPropertyChanged(nameof(FilteredSessions));
        OnPropertyChanged(nameof(SessionSearchSummary));
        SelectedSession = selected is not null && _filteredSessions.Contains(selected) ? selected : null;
    }

    public RelayCommand OpenLogExclusionsCommand { get; }

    public event Action<LogExclusionViewModel>? LogExclusionsRequested;

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
        set
        {
            if (SetProperty(ref _selectedTabIndex, value))
            {
                RefreshStepState();
            }
        }
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

    // ステップヘッダーの丸バッジ。完了済みで、かつ今そのステップを開いていない場合だけ ✓ にする。
    public string SessionStepBadge => StepBadge("1", HasSession, stepIndex: 0);

    public string RangeStepBadge => StepBadge("2", AnalysisResult.HasResult, stepIndex: 1);

    public string ResultStepBadge => StepBadge("3", AnalysisResult.HasResult, stepIndex: 2);

    /// <summary>ステップ①の確定内容。例「2件 / 34,551」。</summary>
    public string SessionStepSummary
    {
        get
        {
            var enabledSessions = Sessions
                .Where(session => session.IsEnabled)
                .ToArray();
            if (enabledSessions.Length == 0)
            {
                return string.Empty;
            }

            var recordCount = enabledSessions.Sum(session => session.Records.Count);
            return $"{enabledSessions.Length:N0}件 / {recordCount:N0}";
        }
    }

    /// <summary>ステップ②の確定内容。例「Ceizak Battlegrounds」。</summary>
    public string RangeStepSummary
    {
        get
        {
            if (AnalysisResult.HasResult)
            {
                return AnalysisRange.LastCompletedRangeName;
            }

            return AnalysisRange.SelectedAreaSegment?.AreaName ?? string.Empty;
        }
    }

    /// <summary>セッション未選択の間は②を開けない。</summary>
    public bool IsRangeStepEnabled => HasSession;

    /// <summary>分析未実行の間は③を開けない。</summary>
    public bool IsResultStepEnabled => AnalysisResult.HasResult;

    // ②③ は各ビューが自前のフッターを持つため、ウィンドウ側フッターは① のときだけ出す。
    public bool IsSessionStepSelected => SelectedTabIndex == 0;

    private string StepBadge(string number, bool isCompleted, int stepIndex) =>
        isCompleted && SelectedTabIndex != stepIndex
            ? "✓"
            : number;

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

        var enabledStates = preserveEnabledStates
            ? Sessions.Select(CreateSelectionState).ToArray()
            : [];
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

                attempt.Session.IsEnabled = enabledStates.LastOrDefault(state =>
                    state.Matches(sessionFolder, attempt.Session.SessionId))?.IsEnabled
                    ?? _settings.SessionSelections.LastOrDefault(state =>
                        state.Matches(sessionFolder, attempt.Session.SessionId))?.IsEnabled
                    ?? true;
                attempt.Session.PropertyChanged += OnSessionSelectionChanged;
                Sessions.Add(attempt.Session);
                if (FilteredSessions.Contains(attempt.Session)) SelectedSession = attempt.Session;
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
        var session = new SessionSelectionViewModel(
                result.Session,
                canonicalRecords.Records,
                warnings);
        session.LoadExclusions();
        session.LoadAlias();
        return new SessionLoadAttempt(session, []);
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

            var saveError = SaveSessionSelections(Sessions);
            await RefreshCombinedSessionAsync(CancellationToken.None);
            StatusMessage = saveError ?? (isEnabled
                ? "すべてのセッションを分析対象にしました。"
                : "すべてのセッションを分析対象から外しました。");
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

        if (!_dialogService.ConfirmSessionDeletion(session.DisplayName))
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
            SelectedSession = FilteredSessions.LastOrDefault();
            await RefreshCombinedSessionAsync(CancellationToken.None);
            StatusMessage = $"セッション「{session.DisplayName}」をごみ箱へ移動しました。";
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

        foreach (var session in enabledSessions.Where(session => session.ExclusionsLoadError is not null))
        {
            Warnings.Add(session.ExclusionsLoadError!);
        }

        foreach (var session in Sessions.Where(session => session.AliasLoadError is not null))
            Warnings.Add(session.AliasLoadError!);

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

        _combinedRecords = await Task.Run(
            () => BuildCombinedRecords(enabledSessions),
            cancellationToken);
        await AnalysisRange.LoadRecordsAsync(_combinedRecords.Select(item => item.Record).ToArray(), cancellationToken);
        ApplyExclusions();
        AnalysisResult.Clear();
        RefreshCommandStates();
    }

    private void ClearCombinedSession()
    {
        _combinedRecords = [];
        SessionInfoRows.Clear();
        Warnings.Clear();
        foreach (var session in Sessions.Where(session => session.AliasLoadError is not null))
            Warnings.Add(session.AliasLoadError!);
        AnalysisRange.Clear();
        AnalysisResult.Clear();
        SelectedFolderPath = "未選択";
        RefreshCommandStates();
    }

    private void CancelLoading()
    {
        _loadCancellation?.Cancel();
    }

    private static CombinedRecord[] BuildCombinedRecords(
        IReadOnlyList<SessionSelectionViewModel> enabledSessions)
    {
        return enabledSessions
            .SelectMany(session => session.Records.Select(record => (Session: session, Record: record)))
            .OrderBy(item => item.Record.FirstSeenAt ?? DateTimeOffset.MaxValue)
            .ThenBy(item => item.Record.SessionId, StringComparer.Ordinal)
            .ThenBy(item => item.Record.Order ?? long.MaxValue)
            .Select((item, index) => new CombinedRecord(
                CloneWithCombinedOrder(item.Record, index + 1), item.Session))
            .ToArray();
    }

    private sealed record CombinedRecord(CanonicalRecord Record, SessionSelectionViewModel Session);

    private void ApplyExclusions()
    {
        var errors = Sessions.Where(session => session.IsEnabled && session.ExclusionsLoadError is not null)
            .Select(session => session.ExclusionsLoadError).ToArray();
        AnalysisRange.SetExcludedRecords(
            _combinedRecords.Where(item => item.Session.Exclusions.IsExcluded(item.Record)).Select(item => item.Record),
            errors.Length == 0 ? null : "除外設定を確認できないため分析できません。" + Environment.NewLine + string.Join(Environment.NewLine, errors));
    }

    private void OpenLogExclusions()
    {
        var editor = new LogExclusionViewModel(Sessions.ToArray(), SelectedSession);
        LogExclusionsRequested?.Invoke(editor);
        if (editor.HasChanges)
        {
            ApplyExclusions();
            AnalysisResult.Clear();
            if (SelectedTabIndex == 2)
            {
                SelectedTabIndex = 1;
            }

            StatusMessage = "除外設定を保存しました。同じ分析区間で再分析してください。";
        }
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

    private void RefreshAliasDisplay()
    {
        if (Sessions.Any(session => session.IsAliasEditing))
        {
            _aliasRefreshPending = true;
            return;
        }
        _aliasRefreshPending = false;
        RefreshSessionFilter();
        SessionInfoRows.Clear();
        var enabled = Sessions.Where(session => session.IsEnabled).ToArray();
        if (enabled.Length > 0)
            foreach (var row in BuildSessionRows(enabled)) SessionInfoRows.Add(row);
    }

    private async void OnSessionSelectionChanged(
        object? sender,
        PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SessionSelectionViewModel.IsAliasEditing)
            && sender is SessionSelectionViewModel { IsAliasEditing: false } && _aliasRefreshPending)
        {
            _ = System.Windows.Threading.Dispatcher.CurrentDispatcher.BeginInvoke(
                new Action(RefreshAliasDisplay), System.Windows.Threading.DispatcherPriority.Background);
        }
        if (e.PropertyName == nameof(SessionSelectionViewModel.Alias))
        {
            if (sender is SessionSelectionViewModel { IsAliasEditing: true })
            {
                _aliasRefreshPending = true;
                // DataGridの確定処理中にItemsSourceを差し替えないよう、編集終了後に更新します。
                _ = System.Windows.Threading.Dispatcher.CurrentDispatcher.BeginInvoke(
                    new Action(RefreshAliasDisplay), System.Windows.Threading.DispatcherPriority.Background);
            }
            else RefreshAliasDisplay();
        }

        if (e.PropertyName == nameof(SessionSelectionViewModel.IsEnabled))
        {
            OnPropertyChanged(nameof(SessionSearchSummary));
            try
            {
                var saveError = sender is SessionSelectionViewModel session
                    ? SaveSessionSelections([session])
                    : null;
                await RefreshCombinedSessionAsync(CancellationToken.None);
                if (saveError is not null)
                {
                    StatusMessage = saveError;
                }
            }
            catch (Exception exception)
            {
                StatusMessage = $"分析対象を更新できませんでした: {exception.Message}";
            }
        }
    }

    private static SessionSelectionState CreateSelectionState(SessionSelectionViewModel session)
    {
        return new SessionSelectionState
        {
            FolderPath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(session.FolderPath)),
            SessionId = session.SessionId,
            IsEnabled = session.IsEnabled,
        };
    }

    private string? SaveSessionSelections(IEnumerable<SessionSelectionViewModel> sessions)
    {
        var selections = sessions.Select(CreateSelectionState).ToArray();
        foreach (var selection in selections)
        {
            _settings.SessionSelections.RemoveAll(state =>
                state.Matches(selection.FolderPath, selection.SessionId));
            _settings.SessionSelections.Add(selection);
        }

        try
        {
            _settingsStore.SaveSessionSelections(selections);
            return null;
        }
        catch (Exception exception)
        {
            return $"セッションの選択状態を保存できませんでした。次回起動時に反映されません: {exception.Message}";
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
            AnalysisRange.LastCompletedRangeName,
            AnalysisRange.LastCompletedOrderRangeText,
            AnalysisRange.LastCompletedRecordCount);
        StatusMessage = "分析結果を表示しました。";
    }

    private static IReadOnlyList<SessionInfoRow> BuildSessionRows(
        IReadOnlyList<SessionSelectionViewModel> sessions)
    {
        if (sessions.Count == 1)
        {
            return [new SessionInfoRow("エイリアス", ToDisplay(sessions[0].Alias)), .. BuildSingleSessionRows(sessions[0].Session)];
        }

        return
        [
            new SessionInfoRow("選択セッション数", sessions.Count.ToString("N0")),
            new SessionInfoRow("セッション", string.Join(", ", sessions.Select(session => session.DisplayName))),
            new SessionInfoRow("セッションID", string.Join(", ", sessions.Select(session => session.SessionId))),
            new SessionInfoRow("最も早い開始時刻", AnalysisDisplayText.ToDateTimeText(sessions.Min(session => session.Session.SessionInfo.StartedAt))),
            new SessionInfoRow("最も遅い終了時刻", AnalysisDisplayText.ToDateTimeText(sessions.Max(session => session.Session.SessionInfo.EndedAt))),
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
            new SessionInfoRow("開始時刻", AnalysisDisplayText.ToDateTimeText(info.StartedAt)),
            new SessionInfoRow("終了時刻", AnalysisDisplayText.ToDateTimeText(info.EndedAt)),
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
        OnPropertyChanged(nameof(SessionSearchSummary));
        OpenLogExclusionsCommand.RaiseCanExecuteChanged();
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
        RefreshStepState();
        GoToNextStepCommand.RaiseCanExecuteChanged();
    }

    private void RefreshStepState()
    {
        OnPropertyChanged(nameof(SessionStepBadge));
        OnPropertyChanged(nameof(RangeStepBadge));
        OnPropertyChanged(nameof(ResultStepBadge));
        OnPropertyChanged(nameof(SessionStepSummary));
        OnPropertyChanged(nameof(RangeStepSummary));
        OnPropertyChanged(nameof(IsRangeStepEnabled));
        OnPropertyChanged(nameof(IsResultStepEnabled));
        OnPropertyChanged(nameof(IsSessionStepSelected));
    }

    private void OnAnalysisRangePropertyChanged(
        object? sender,
        PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(AnalysisRangeViewModel.IsBusy))
        {
            OpenLogExclusionsCommand.RaiseCanExecuteChanged();
        }

        if (e.PropertyName == nameof(AnalysisRangeViewModel.SelectedAreaSegment))
        {
            OnPropertyChanged(nameof(RangeStepSummary));
        }
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
