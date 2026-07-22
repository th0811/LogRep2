using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using FFXI_LogAnalyzer.Core;

namespace FFXI_LogAnalyzer.App;

public sealed class AnalysisResultViewModel : INotifyPropertyChanged
{
    private readonly ActorNameClassifier _actorNameClassifier = new();
    private readonly ActorFilterService _actorFilterService = new();
    private readonly AnalyzerSettingsStore _settingsStore;
    private readonly CsvExportService _csvExportService;
    private readonly List<ActorSummaryViewModel> _allActorSummaries = [];
    private readonly List<ActionSummaryViewModel> _allActionSummaries = [];
    private AnalyzerSettings _settings;
    private bool _hasResult;
    private string _actorFilterText = string.Empty;
    private bool _showRegisteredPcActors = true;
    private bool _showPcCandidateActors = true;
    private bool _showRegisteredNpcActors = true;
    private bool _showUnknownActors = true;
    private AnalysisTimeResult _analysisTime = AnalysisTimeResult.Unknown([]);
    private DateTimeOffset? _fallbackSessionTime;
    private string _rangeName = "指定範囲";
    private string _statusMessage = "分析結果はまだありません。";

    public AnalysisResultViewModel()
        : this(new AnalyzerSettingsStore())
    {
    }

    public AnalysisResultViewModel(AnalyzerSettingsStore settingsStore)
        : this(settingsStore, new CsvExportService())
    {
    }

    public AnalysisResultViewModel(
        AnalyzerSettingsStore settingsStore,
        CsvExportService csvExportService)
    {
        _settingsStore = settingsStore
            ?? throw new ArgumentNullException(nameof(settingsStore));
        _csvExportService = csvExportService
            ?? throw new ArgumentNullException(nameof(csvExportService));
        _settings = _settingsStore.Load();
        SelectAllActorsCommand = new RelayCommand(
            SelectAllActors,
            () => HasResult);
        ClearActorSelectionCommand = new RelayCommand(
            ClearActorSelection,
            () => HasResult);
        SelectPcCandidateActorsCommand = new RelayCommand(
            SelectPcCandidateActors,
            () => HasResult);
        SelectRegisteredPcActorsCommand = new RelayCommand(
            SelectRegisteredPcActors,
            () => HasResult);
        OpenActorRegistrationManagerCommand = new RelayCommand(
            OpenActorRegistrationManager,
            () => HasResult);
        ExportActorSummariesCommand = new RelayCommand(
            ExportActorSummaries,
            () => HasResult);
        ExportActionSummariesCommand = new RelayCommand(
            ExportActionSummaries,
            () => HasResult);
        ExportLevelingPointsCommand = new RelayCommand(
            ExportLevelingPoints,
            () => HasResult);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public RelayCommand SelectAllActorsCommand { get; }

    public RelayCommand ClearActorSelectionCommand { get; }

    public RelayCommand SelectPcCandidateActorsCommand { get; }

    public RelayCommand SelectRegisteredPcActorsCommand { get; }

    public RelayCommand OpenActorRegistrationManagerCommand { get; }

    public RelayCommand ExportActorSummariesCommand { get; }

    public RelayCommand ExportActionSummariesCommand { get; }

    public RelayCommand ExportLevelingPointsCommand { get; }

    public ObservableCollection<ActorSummaryViewModel> ActorSummaries { get; } = [];

    public ObservableCollection<ActionSummaryViewModel> ActionSummaries { get; } = [];

    public ObservableCollection<LevelingPointSummaryViewModel> LevelingPointSummaries { get; } = [];

    public ObservableCollection<ActorVisibilityViewModel> ActorVisibilities { get; } = [];

    public ObservableCollection<ActorVisibilityViewModel> FilteredActorVisibilities { get; } = [];

    public ObservableCollection<UnparsedLogViewModel> UnparsedLogs { get; } = [];

    public ObservableCollection<SessionInfoRow> SessionRows { get; } = [];

    public bool HasResult
    {
        get => _hasResult;
        private set
        {
            if (SetProperty(ref _hasResult, value))
            {
                SelectAllActorsCommand.RaiseCanExecuteChanged();
                ClearActorSelectionCommand.RaiseCanExecuteChanged();
                SelectPcCandidateActorsCommand.RaiseCanExecuteChanged();
                SelectRegisteredPcActorsCommand.RaiseCanExecuteChanged();
                OpenActorRegistrationManagerCommand.RaiseCanExecuteChanged();
                ExportActorSummariesCommand.RaiseCanExecuteChanged();
                ExportActionSummariesCommand.RaiseCanExecuteChanged();
                ExportLevelingPointsCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public string ActorFilterText
    {
        get => _actorFilterText;
        set
        {
            if (SetProperty(ref _actorFilterText, value))
            {
                RefreshActorVisibilityFilter();
            }
        }
    }

    public bool ShowRegisteredPcActors
    {
        get => _showRegisteredPcActors;
        set
        {
            if (SetProperty(ref _showRegisteredPcActors, value))
            {
                RefreshActorVisibilityFilter();
            }
        }
    }

    public bool ShowPcCandidateActors
    {
        get => _showPcCandidateActors;
        set
        {
            if (SetProperty(ref _showPcCandidateActors, value))
            {
                RefreshActorVisibilityFilter();
            }
        }
    }

    public bool ShowRegisteredNpcActors
    {
        get => _showRegisteredNpcActors;
        set
        {
            if (SetProperty(ref _showRegisteredNpcActors, value))
            {
                RefreshActorVisibilityFilter();
            }
        }
    }

    public bool ShowUnknownActors
    {
        get => _showUnknownActors;
        set
        {
            if (SetProperty(ref _showUnknownActors, value))
            {
                RefreshActorVisibilityFilter();
            }
        }
    }

    public string ActorSelectionSummary
    {
        get
        {
            var selected = ActorVisibilities.Count(visibility => visibility.IsVisible);
            return $"選択中: {selected:N0} / {ActorVisibilities.Count:N0}";
        }
    }

    public void Load(
        AnalysisResult result,
        IReadOnlyList<SessionInfoRow> sessionRows,
        DateTimeOffset? fallbackSessionTime = null,
        string rangeName = "指定範囲")
    {
        ClearVisibilitySubscriptions();
        _settings = _settingsStore.Load();
        _analysisTime = result.AnalysisTime;
        _fallbackSessionTime = fallbackSessionTime;
        _rangeName = rangeName;

        _allActorSummaries.Clear();
        _allActorSummaries.AddRange(
            result.ActorSummaries.Select(
                summary => new ActorSummaryViewModel(summary)));

        _allActionSummaries.Clear();
        _allActionSummaries.AddRange(
            result.ActionSummaries.Select(
                summary => new ActionSummaryViewModel(summary)));

        LevelingPointSummaries.Clear();
        foreach (var summary in result.LevelingPointSummaries)
        {
            LevelingPointSummaries.Add(new LevelingPointSummaryViewModel(summary));
        }

        ActorVisibilities.Clear();
        foreach (var summary in _allActorSummaries)
        {
            var classification = Classify(summary.Actor);
            var visibility = new ActorVisibilityViewModel(
                summary,
                classification,
                IsPcCandidateSelectionTarget(classification),
                RegisterAsPc,
                RegisterAsNpc,
                ClearRegistration);
            visibility.PropertyChanged += OnActorVisibilityChanged;
            ActorVisibilities.Add(visibility);
        }

        UnparsedLogs.Clear();
        foreach (var unknownActionGroup in result.UnknownActionGroups)
        {
            UnparsedLogs.Add(new UnparsedLogViewModel(unknownActionGroup));
        }

        foreach (var unparsedActionGroup in result.UnparsedActionGroups)
        {
            UnparsedLogs.Add(new UnparsedLogViewModel(unparsedActionGroup));
        }

        SessionRows.Clear();
        foreach (var row in sessionRows)
        {
            SessionRows.Add(row);
        }

        SessionRows.Add(new SessionInfoRow("分析時刻の精度", AnalysisDisplayText.ToText(result.AnalysisTime.Confidence)));
        SessionRows.Add(new SessionInfoRow("分析時間（秒）", AnalysisNumberFormatter.FormatDecimal(result.AnalysisTime.DurationSeconds)));
        SessionRows.Add(new SessionInfoRow("DPS状態", ToDpsStatus(result.AnalysisTime)));
        SessionRows.Add(new SessionInfoRow("未解析ログ件数", UnparsedLogs.Count.ToString("N0")));

        RefreshActorVisibilityFilter();
        RefreshFilteredResults();

        HasResult = true;
        StatusMessage = "分析結果を表示しています。";
    }

    public void Clear()
    {
        ClearVisibilitySubscriptions();
        _allActorSummaries.Clear();
        _allActionSummaries.Clear();
        ActorSummaries.Clear();
        ActionSummaries.Clear();
        LevelingPointSummaries.Clear();
        ActorVisibilities.Clear();
        FilteredActorVisibilities.Clear();
        UnparsedLogs.Clear();
        SessionRows.Clear();
        _analysisTime = AnalysisTimeResult.Unknown([]);
        _fallbackSessionTime = null;
        _rangeName = "指定範囲";
        HasResult = false;
        StatusMessage = "分析結果はまだありません。";
        OnPropertyChanged(nameof(ActorSelectionSummary));
    }

    private static string ToDpsStatus(AnalysisTimeResult analysisTime)
    {
        return analysisTime.CanCalculateDps
            && analysisTime.Confidence != TimeConfidence.Unknown
            ? "DPS計算可能"
            : "DPS計算不可";
    }

    private void SelectAllActors()
    {
        SetActorSelection(_ => true);
    }

    private void ClearActorSelection()
    {
        SetActorSelection(_ => false);
    }

    private void SelectPcCandidateActors()
    {
        SetActorSelection(
            visibility => IsPcCandidateSelectionTarget(
                visibility.NameKind));
    }

    private void SelectRegisteredPcActors()
    {
        SetActorSelection(
            visibility => visibility.NameKind
                == ActorNameKind.RegisteredPc);
    }

    private void SetActorSelection(
        Func<ActorVisibilityViewModel, bool> selector)
    {
        foreach (var visibility in ActorVisibilities)
        {
            visibility.IsVisible = selector(visibility);
        }

        RefreshFilteredResults();
        OnPropertyChanged(nameof(ActorSelectionSummary));
    }

    private void RegisterAsPc(string actor)
    {
        var normalized = ActorNameClassifier.NormalizePcName(actor);
        RemoveName(_settings.KnownNpcNames, normalized);
        AddName(_settings.KnownPcNames, normalized);
        SaveSettingsAndRefreshClassifications();
    }

    private void RegisterAsNpc(string actor)
    {
        var normalized = ActorNameClassifier.NormalizeRegisteredName(actor);
        RemoveName(_settings.KnownPcNames, normalized);
        AddName(_settings.KnownNpcNames, normalized);
        SaveSettingsAndRefreshClassifications();
    }

    private void ClearRegistration(string actor)
    {
        RemoveName(
            _settings.KnownPcNames,
            ActorNameClassifier.NormalizePcName(actor));
        RemoveName(
            _settings.KnownNpcNames,
            ActorNameClassifier.NormalizeRegisteredName(actor));
        SaveSettingsAndRefreshClassifications();
    }

    private void OpenActorRegistrationManager()
    {
        var viewModel = new ActorRegistrationManagerViewModel(
            _settings,
            settings =>
            {
                _settings = settings;
                _settingsStore.Save(_settings);
                RefreshClassifications();
            });
        var window = new ActorRegistrationManagerWindow
        {
            DataContext = viewModel,
            Owner = System.Windows.Application.Current.MainWindow
        };
        window.ShowDialog();
    }

    private void ExportActorSummaries()
    {
        Export(
            BuildExportFileName("キャラクター別"),
            [
                "キャラクター名", "総与ダメージ", "DPS", "DPS時間信頼度",
                "通常攻撃命中率", "通常攻撃クリティカル率", "使用回数",
                "命中回数", "非命中回数", "未分類件数",
            ],
            ActorSummaries.Select(summary => (IReadOnlyList<string>)
            [
                summary.Actor, summary.TotalDamage, summary.Dps,
                summary.DpsTimeConfidence, summary.NormalAttackHitRate,
                summary.NormalAttackCriticalRate, summary.TotalUseCount,
                summary.TotalHitCount, summary.TotalMissCount,
                summary.UnknownCount,
            ]));
    }

    private void ExportActionSummaries()
    {
        Export(
            BuildExportFileName("アクション別"),
            [
                "キャラクター名", "アクション名", "種別", "使用回数",
                "命中回数", "非命中回数", "未分類件数", "命中率",
                "総ダメージ", "最大ダメージ", "最小ダメージ", "平均ダメージ",
            ],
            ActionSummaries.Select(summary => (IReadOnlyList<string>)
            [
                summary.Actor, summary.ActionName, summary.ActionType,
                summary.UseCount, summary.HitCount, summary.MissCount,
                summary.UnknownCount, summary.HitRate, summary.TotalDamage,
                summary.MaxDamage, summary.MinDamage, summary.AverageDamage,
            ]));
    }

    private void ExportLevelingPoints()
    {
        Export(
            BuildExportFileName("レベル上げ"),
            ["ポイント種別", "総合計", "最大チェーン", "時給"],
            LevelingPointSummaries.Select(summary => (IReadOnlyList<string>)
            [
                summary.PointName, summary.TotalPoints,
                summary.MaxChainCount, summary.PointsPerHour,
            ]));
    }

    private string BuildExportFileName(string outputType)
    {
        return CsvExportFileNameBuilder.Build(
            outputType,
            _analysisTime,
            _fallbackSessionTime,
            _rangeName);
    }

    private void Export(
        string defaultFileName,
        IReadOnlyList<string> headers,
        IEnumerable<IReadOnlyList<string>> rows)
    {
        try
        {
            StatusMessage = _csvExportService.Export(
                defaultFileName,
                headers,
                rows)
                    ? "CSVファイルを保存しました。"
                    : "CSV出力をキャンセルしました。";
        }
        catch (Exception exception)
        {
            StatusMessage = $"CSVファイルを保存できませんでした: {exception.Message}";
        }
    }

    private void SaveSettingsAndRefreshClassifications()
    {
        _settingsStore.Save(_settings);
        _settings = _settingsStore.Load();
        RefreshClassifications();
    }

    private void RefreshClassifications()
    {
        foreach (var visibility in ActorVisibilities)
        {
            visibility.UpdateClassification(Classify(visibility.Actor));
        }

        RefreshActorVisibilityFilter();
    }

    private ActorNameKind Classify(string actor)
    {
        return _actorNameClassifier.Classify(
            actor,
            _settings.KnownPcNames,
            _settings.KnownNpcNames);
    }

    private static bool IsPcCandidateSelectionTarget(
        ActorNameKind classification)
    {
        return classification is ActorNameKind.RegisteredPc
            or ActorNameKind.PcCandidate;
    }

    private static void AddName(List<string> names, string name)
    {
        if (!names.Contains(name, StringComparer.OrdinalIgnoreCase))
        {
            names.Add(name);
        }
    }

    private static void RemoveName(List<string> names, string name)
    {
        names.RemoveAll(
            registered => string.Equals(
                registered,
                name,
                StringComparison.OrdinalIgnoreCase));
    }

    private void OnActorVisibilityChanged(
        object? sender,
        PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ActorVisibilityViewModel.IsVisible))
        {
            RefreshFilteredResults();
            OnPropertyChanged(nameof(ActorSelectionSummary));
        }
    }

    private void RefreshFilteredResults()
    {
        ActorSummaries.Clear();
        foreach (var summary in _actorFilterService.FilterActorSummaries(_allActorSummaries, ActorVisibilities))
        {
            ActorSummaries.Add(summary);
        }

        ActionSummaries.Clear();
        foreach (var summary in _actorFilterService.FilterActionSummaries(_allActionSummaries, ActorVisibilities))
        {
            ActionSummaries.Add(summary);
        }
    }

    private void RefreshActorVisibilityFilter()
    {
        FilteredActorVisibilities.Clear();
        foreach (var visibility in ActorVisibilities.Where(MatchesActorVisibilityFilter))
        {
            FilteredActorVisibilities.Add(visibility);
        }
    }

    private bool MatchesActorVisibilityFilter(ActorVisibilityViewModel visibility)
    {
        if (!MatchesActorNameFilter(visibility.Actor))
        {
            return false;
        }

        return visibility.NameKind switch
        {
            ActorNameKind.RegisteredPc => ShowRegisteredPcActors,
            ActorNameKind.PcCandidate => ShowPcCandidateActors,
            ActorNameKind.RegisteredNpc => ShowRegisteredNpcActors,
            _ => ShowUnknownActors
        };
    }

    private bool MatchesActorNameFilter(string actor)
    {
        return string.IsNullOrWhiteSpace(ActorFilterText)
            || actor.Contains(
                ActorFilterText.Trim(),
                StringComparison.OrdinalIgnoreCase);
    }

    private void ClearVisibilitySubscriptions()
    {
        foreach (var visibility in ActorVisibilities)
        {
            visibility.PropertyChanged -= OnActorVisibilityChanged;
        }
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
