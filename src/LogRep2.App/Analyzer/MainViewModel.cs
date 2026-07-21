using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using FFXI_LogAnalyzer.Core;

namespace FFXI_LogAnalyzer.App;

// ステッパー（手順バー）の各ステップの状態。
public enum StepState
{
    // 未着手。
    Pending,

    // 現在このステップを操作中。
    Active,

    // 完了済み。
    Completed
}

public sealed class MainViewModel : INotifyPropertyChanged
{
    private readonly SessionOpenService _sessionOpenService;
    private readonly DialogService _dialogService;
    private readonly AnalyzerSettingsStore _settingsStore;
    private readonly CanonicalRecordReader _canonicalRecordReader = new();
    private AnalyzerSettings _settings;
    private SessionSelectionViewModel? _selectedSession;
    private string _statusMessage = "セッション出力先フォルダを選択してください。";
    private string _sessionRootFolderPath = "未選択";
    private string _selectedFolderPath = "未選択";
    private int _selectedTabIndex;
    private StepState _step1State = StepState.Active;
    private StepState _step2State = StepState.Pending;
    private StepState _step3State = StepState.Pending;
    private string _nextStepText = string.Empty;
    private string _nextStepButtonText = "分析区間へ進む";
    private bool _showNextStepButton = true;
    private int _nextStepTargetTabIndex = 1;
    private bool _canGoToNextStep;
    private CancellationTokenSource? _loadCancellation;
    private bool _isBusy;
    private string _busyText = string.Empty;

    public MainViewModel(SessionOpenService sessionOpenService, DialogService dialogService)
        : this(
            sessionOpenService,
            dialogService,
            new AnalyzerSettingsStore())
    {
    }

    public MainViewModel(
        SessionOpenService sessionOpenService,
        DialogService dialogService,
        AnalyzerSettingsStore settingsStore)
    {
        _sessionOpenService = sessionOpenService
            ?? throw new ArgumentNullException(nameof(sessionOpenService));
        _dialogService = dialogService
            ?? throw new ArgumentNullException(nameof(dialogService));
        _settingsStore = settingsStore
            ?? throw new ArgumentNullException(nameof(settingsStore));
        _settings = _settingsStore.Load();
        AnalysisResult = new AnalysisResultViewModel(_settingsStore);
        AnalysisRange.AnalysisCompleted += OnAnalysisCompleted;
        AnalysisRange.PropertyChanged += OnAnalysisRangePropertyChanged;
        AnalysisResult.PropertyChanged += OnAnalysisResultPropertyChanged;
        GoToNextStepCommand = new RelayCommand(GoToNextStep, () => _canGoToNextStep);
        SelectSessionRootFolderCommand = new FfxiTempLogCollector.App.AsyncRelayCommand(
            SelectSessionRootFolderAsync,
            () => !IsBusy);
        RefreshSessionsCommand = new FfxiTempLogCollector.App.AsyncRelayCommand(
            RefreshSessionsAsync,
            () => HasSessionRootFolder && !IsBusy);
        CancelLoadingCommand = new RelayCommand(
            CancelLoading,
            () => IsBusy);
        RemoveSelectedSessionCommand = new RelayCommand(
            RemoveSelectedSession,
            () => SelectedSession is not null && !IsBusy);
        ClearSessionsCommand = new RelayCommand(
            ClearSessions,
            () => Sessions.Count > 0 && !IsBusy);
        _ = LoadConfiguredSessionRootAsync();
        RefreshStepStates();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public FfxiTempLogCollector.App.AsyncRelayCommand SelectSessionRootFolderCommand { get; }

    public FfxiTempLogCollector.App.AsyncRelayCommand RefreshSessionsCommand { get; }

    public RelayCommand CancelLoadingCommand { get; }

    public RelayCommand RemoveSelectedSessionCommand { get; }

    public RelayCommand ClearSessionsCommand { get; }

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
                RemoveSelectedSessionCommand.RaiseCanExecuteChanged();
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

    // TabControl.SelectedIndex とバインドし、ステッパーの「実行中」判定と誘導ボタンの遷移先に使う。
    public int SelectedTabIndex
    {
        get => _selectedTabIndex;
        set
        {
            if (SetProperty(ref _selectedTabIndex, value))
            {
                RefreshStepStates();
            }
        }
    }

    public StepState Step1State
    {
        get => _step1State;
        private set => SetProperty(ref _step1State, value);
    }

    public StepState Step2State
    {
        get => _step2State;
        private set => SetProperty(ref _step2State, value);
    }

    public StepState Step3State
    {
        get => _step3State;
        private set => SetProperty(ref _step3State, value);
    }

    // 現在のステップに応じた「次に何をすべきか」の案内文。
    public string NextStepText
    {
        get => _nextStepText;
        private set => SetProperty(ref _nextStepText, value);
    }

    public string NextStepButtonText
    {
        get => _nextStepButtonText;
        private set => SetProperty(ref _nextStepButtonText, value);
    }

    // 最終ステップ（分析結果）では遷移先がないため誘導ボタンを隠す。
    public bool ShowNextStepButton
    {
        get => _showNextStepButton;
        private set => SetProperty(ref _showNextStepButton, value);
    }

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
                SelectSessionRootFolderCommand.RaiseCanExecuteChanged();
                CancelLoadingCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public bool IsNotBusy => !IsBusy;

    public string BusyText
    {
        get => _busyText;
        private set => SetProperty(ref _busyText, value);
    }

    private async Task SelectSessionRootFolderAsync()
    {
        var folderPath = _dialogService.SelectSessionsRootFolder();
        if (string.IsNullOrWhiteSpace(folderPath))
        {
            StatusMessage = "セッション出力先選択をキャンセルしました。";
            return;
        }

        _settings.SessionsRootFolderPath = Path.GetFullPath(folderPath);
        SessionRootFolderPath = _settings.SessionsRootFolderPath ?? "未選択";
        RefreshCommandStates();
        await ReloadSessionsFromRootAsync(preserveEnabledStates: true);
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
            StatusMessage = "セッション出力先フォルダを選択してください。";
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
            StatusMessage = "セッション出力先フォルダを選択してください。";
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

    private void RemoveSelectedSession()
    {
        if (SelectedSession is null)
        {
            return;
        }

        SelectedSession.PropertyChanged -= OnSessionSelectionChanged;
        Sessions.Remove(SelectedSession);
        SelectedSession = Sessions.LastOrDefault();
        _ = RefreshCombinedSessionAsync(CancellationToken.None);
        StatusMessage = "選択中のセッションを解除しました。";
    }

    private void ClearSessions()
    {
        ClearSessionsInternal();
        ClearCombinedSession();
        StatusMessage = "セッション選択をすべて解除しました。";
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
        AnalysisResult.Load(result, SessionInfoRows.ToArray());
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
            new SessionInfoRow("selected_sessions", sessions.Count.ToString("N0")),
            new SessionInfoRow("session_ids", string.Join(", ", sessions.Select(session => session.SessionId))),
            new SessionInfoRow("started_at_min", ToDisplay(sessions.Min(session => session.Session.SessionInfo.StartedAt))),
            new SessionInfoRow("ended_at_max", ToDisplay(sessions.Max(session => session.Session.SessionInfo.EndedAt))),
            new SessionInfoRow("canonical record件数", sessions.Sum(session => session.Records.Count).ToString("N0")),
            new SessionInfoRow("marker件数", sessions.Sum(session => session.Records.Count(record => record.IsMarker)).ToString("N0")),
            new SessionInfoRow("gap_warnings", sessions.Sum(session => session.Session.StatsInfo.GapWarnings).ToString("N0")),
            new SessionInfoRow("parse_errors", sessions.Sum(session => session.Session.StatsInfo.ParseErrors).ToString("N0")),
            new SessionInfoRow("decode_errors", sessions.Sum(session => session.Session.StatsInfo.DecodeErrors).ToString("N0"))
        ];
    }

    private static IReadOnlyList<SessionInfoRow> BuildSingleSessionRows(
        AnalyzerInputSession session)
    {
        var info = session.SessionInfo;
        var stats = session.StatsInfo;
        return
        [
            new SessionInfoRow("session_id", ToDisplay(info.SessionId)),
            new SessionInfoRow("status", info.Status.ToString()),
            new SessionInfoRow("started_at", ToDisplay(info.StartedAt)),
            new SessionInfoRow("ended_at", ToDisplay(info.EndedAt)),
            new SessionInfoRow("collector_version", ToDisplay(info.CollectorVersion)),
            new SessionInfoRow("schema_version", ToDisplay(info.SchemaVersion)),
            new SessionInfoRow("raw_schema_version", ToDisplay(info.RawSchemaVersion)),
            new SessionInfoRow("canonical_schema_version", ToDisplay(info.CanonicalSchemaVersion)),
            new SessionInfoRow("canonical record件数", stats.CanonicalRecordsWritten.ToString("N0")),
            new SessionInfoRow("gap_warnings", stats.GapWarnings.ToString("N0")),
            new SessionInfoRow("parse_errors", stats.ParseErrors.ToString("N0")),
            new SessionInfoRow("decode_errors", stats.DecodeErrors.ToString("N0"))
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
        RefreshSessionsCommand.RaiseCanExecuteChanged();
        RemoveSelectedSessionCommand.RaiseCanExecuteChanged();
        ClearSessionsCommand.RaiseCanExecuteChanged();
        RefreshStepStates();
    }

    // 3ステップ（セッション選択→分析区間→分析実行）の状態と、次の手順への誘導表示を更新する。
    private void RefreshStepStates()
    {
        // STEP2の完了は「区間が妥当か（IsRangeReady）」では判定しない。
        // デフォルトの「ログ先頭→ログ最後尾」が常に妥当なため、起動直後から
        // 完了扱いになってしまう。分析を実際に実行した時点をSTEP2の完了とみなす。
        Step1State = ResolveStepState(0, HasSession);
        Step2State = ResolveStepState(1, AnalysisResult.HasResult);
        Step3State = ResolveStepState(2, AnalysisResult.HasResult);
        UpdateNextStepGuidance();
    }

    // 対象タブを操作中なら「実行中」、完了条件を満たせば「完了」、それ以外は「未着手」。
    private StepState ResolveStepState(int tabIndex, bool isDone)
    {
        if (SelectedTabIndex == tabIndex && !isDone)
        {
            return StepState.Active;
        }

        return isDone ? StepState.Completed : StepState.Pending;
    }

    private void UpdateNextStepGuidance()
    {
        switch (SelectedTabIndex)
        {
            case 0:
                NextStepText = HasSession
                    ? "セッションを選択しました。次は分析区間を選びます。"
                    : "分析するセッションの「使用」列にチェックを入れてください。";
                NextStepButtonText = "分析区間へ進む";
                ShowNextStepButton = true;
                _nextStepTargetTabIndex = 1;
                _canGoToNextStep = HasSession;
                break;
            case 1:
                NextStepText = AnalysisRange.IsRangeReady
                    ? "分析区間を選択しました。「分析実行」ボタンを押してください。"
                    : "分析区間（開始・終了ポイント）を選択してください。";
                NextStepButtonText = "分析結果へ進む";
                ShowNextStepButton = true;
                _nextStepTargetTabIndex = 2;
                _canGoToNextStep = AnalysisResult.HasResult;
                break;
            default:
                NextStepText = AnalysisResult.HasResult
                    ? "分析が完了しました。結果を確認できます。"
                    : "「分析区間」タブで分析を実行すると結果が表示されます。";
                ShowNextStepButton = false;
                _canGoToNextStep = false;
                break;
        }

        GoToNextStepCommand.RaiseCanExecuteChanged();
    }

    private void GoToNextStep()
    {
        SelectedTabIndex = _nextStepTargetTabIndex;
    }

    private void OnAnalysisRangePropertyChanged(
        object? sender,
        PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(AnalysisRangeViewModel.IsRangeReady))
        {
            RefreshStepStates();
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

        RefreshStepStates();
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
