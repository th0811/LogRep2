using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using FFXI_LogAnalyzer.Core;

namespace FFXI_LogAnalyzer.App;

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
        SelectSessionRootFolderCommand = new RelayCommand(SelectSessionRootFolder);
        RefreshSessionsCommand = new RelayCommand(
            RefreshSessions,
            () => HasSessionRootFolder);
        RemoveSelectedSessionCommand = new RelayCommand(RemoveSelectedSession, () => SelectedSession is not null);
        ClearSessionsCommand = new RelayCommand(ClearSessions, () => Sessions.Count > 0);
        LoadConfiguredSessionRoot();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public RelayCommand SelectSessionRootFolderCommand { get; }

    public RelayCommand RefreshSessionsCommand { get; }

    public RelayCommand RemoveSelectedSessionCommand { get; }

    public RelayCommand ClearSessionsCommand { get; }

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

    public bool HasSession => Sessions.Any(session => session.IsEnabled);

    public bool HasSessionRootFolder => !string.IsNullOrWhiteSpace(_settings.SessionsRootFolderPath);

    private void SelectSessionRootFolder()
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
        ReloadSessionsFromRoot(preserveEnabledStates: true);
    }

    public void ReloadSharedSessionRoot()
    {
        _settings = _settingsStore.Load();
        LoadConfiguredSessionRoot();
    }

    private void RefreshSessions()
    {
        ReloadSessionsFromRoot(preserveEnabledStates: true);
    }

    private void LoadConfiguredSessionRoot()
    {
        if (string.IsNullOrWhiteSpace(_settings.SessionsRootFolderPath))
        {
            SessionRootFolderPath = "未選択";
            StatusMessage = "セッション出力先フォルダを選択してください。";
            RefreshCommandStates();
            return;
        }

        SessionRootFolderPath = _settings.SessionsRootFolderPath;
        ReloadSessionsFromRoot(preserveEnabledStates: true);
    }

    private void ReloadSessionsFromRoot(bool preserveEnabledStates)
    {
        if (string.IsNullOrWhiteSpace(_settings.SessionsRootFolderPath))
        {
            ClearSessionsInternal();
            StatusMessage = "セッション出力先フォルダを選択してください。";
            return;
        }

        var rootFolderPath = _settings.SessionsRootFolderPath;
        SessionRootFolderPath = rootFolderPath;
        if (!Directory.Exists(rootFolderPath))
        {
            ClearSessionsInternal();
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
            StatusMessage = "追加可能なセッションフォルダが見つかりませんでした。";
            return;
        }

        var added = 0;
        foreach (var sessionFolder in sessionFolders)
        {
            var isEnabled = enabledStates.TryGetValue(sessionFolder, out var previousIsEnabled)
                ? previousIsEnabled
                : true;
            if (AddSessionFromPath(sessionFolder, showError: false, isEnabled))
            {
                added++;
            }
        }

        RefreshCombinedSession();
        StatusMessage = $"{added:N0} 件のセッションを読み込みました。";
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

    private bool AddSessionFromPath(
        string folderPath,
        bool showError,
        bool isEnabled)
    {
        var normalizedFolderPath = Path.GetFullPath(folderPath);
        if (Sessions.Any(session => string.Equals(
                session.FolderPath,
                normalizedFolderPath,
                StringComparison.OrdinalIgnoreCase)))
        {
            StatusMessage = "選択済みのセッションはスキップしました。";
            return false;
        }

        var result = _sessionOpenService.Open(normalizedFolderPath);
        if (!result.IsSuccess || result.Session is null)
        {
            if (showError)
            {
                _dialogService.ShowError(string.Join(Environment.NewLine, result.Errors));
            }

            AddWarning(normalizedFolderPath, result.Errors);
            StatusMessage = "セッションの読み込みに失敗しました。";
            return false;
        }

        if (result.Warnings.Count > 0
            && showError
            && !_dialogService.ConfirmWarnings(result.Warnings))
        {
            StatusMessage = "警告があるセッションの追加をキャンセルしました。";
            return false;
        }

        var canonicalRecords = _canonicalRecordReader.Read(result.Session.CanonicalRecordsPath);
        if (!canonicalRecords.IsSuccess)
        {
            if (showError)
            {
                _dialogService.ShowError(string.Join(Environment.NewLine, canonicalRecords.Errors));
            }

            AddWarning(normalizedFolderPath, canonicalRecords.Errors);
            StatusMessage = "canonical_records.jsonl の読み込みに失敗しました。";
            return false;
        }

        var warnings = result.Warnings
            .Concat(canonicalRecords.LineErrors.Select(
                error => $"canonical_records.jsonl {error.LineNumber}行目: {error.Message}"))
            .ToArray();
        var session = new SessionSelectionViewModel(
            result.Session,
            canonicalRecords.Records,
            warnings);
        session.IsEnabled = isEnabled;
        session.PropertyChanged += OnSessionSelectionChanged;
        Sessions.Add(session);
        SelectedSession = session;
        StatusMessage = "セッションを追加しました。";
        return true;
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
        RefreshCombinedSession();
        StatusMessage = "選択中のセッションを解除しました。";
    }

    private void ClearSessions()
    {
        ClearSessionsInternal();
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
        RefreshCombinedSession();
    }

    private void RefreshCombinedSession()
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
            AnalysisRange.Clear();
            AnalysisResult.Clear();
            SelectedFolderPath = "未選択";
            RefreshCommandStates();
            return;
        }

        foreach (var row in BuildSessionRows(enabledSessions))
        {
            SessionInfoRows.Add(row);
        }

        SelectedFolderPath = enabledSessions.Length == 1
            ? enabledSessions[0].FolderPath
            : $"{enabledSessions.Length:N0} 件のセッションを結合";

        AnalysisRange.LoadRecords(BuildCombinedRecords(enabledSessions));
        AnalysisResult.Clear();
        RefreshCommandStates();
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

    private void OnSessionSelectionChanged(
        object? sender,
        PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SessionSelectionViewModel.IsEnabled))
        {
            RefreshCombinedSession();
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

    private void RefreshCommandStates()
    {
        OnPropertyChanged(nameof(HasSession));
        OnPropertyChanged(nameof(HasSessionRootFolder));
        RefreshSessionsCommand.RaiseCanExecuteChanged();
        RemoveSelectedSessionCommand.RaiseCanExecuteChanged();
        ClearSessionsCommand.RaiseCanExecuteChanged();
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
