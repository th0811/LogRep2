using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using FFXI_LogAnalyzer.Core;

namespace FFXI_LogAnalyzer.App;

public sealed class AnalysisRangeViewModel : INotifyPropertyChanged
{
    private readonly AnalysisRangeBuilder _rangeBuilder = new();
    private readonly AnalysisRangeValidator _rangeValidator = new();
    private readonly AnalysisTimeResolver _timeResolver = new();
    private readonly ActionGroupBuilder _actionGroupBuilder = new();
    private readonly ActionGroupParser _actionGroupParser = new(new DefaultAnalysisRuleSet());
    private readonly AnalysisAggregator _analysisAggregator = new();
    private readonly LevelingPointAggregator _levelingPointAggregator = new();
    private IReadOnlyList<CanonicalRecord> _records = [];
    private bool _isStartLogStart = true;
    private bool _isEndLogEnd = true;
    private MarkerListViewModel? _selectedStartMarker;
    private MarkerListViewModel? _selectedEndMarker;
    private AreaStaySegmentListViewModel? _selectedAreaSegment;
    private bool _isAreaSegmentMode;
    private string _validationMessage = "セッションを読み込むと分析区間を選択できます。";
    private string _rangeSummary = "-";
    private CancellationTokenSource? _analysisCancellation;
    private bool _isBusy;
    private string _lastCompletedRangeName = "指定範囲";

    public AnalysisRangeViewModel()
    {
        RunAnalysisCommand = new FfxiTempLogCollector.App.AsyncRelayCommand(
            UpdateRangeSummaryAsync,
            CanRunAnalysis);
        CancelAnalysisCommand = new RelayCommand(
            CancelAnalysis,
            () => IsBusy);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public event Action<AnalysisResult>? AnalysisCompleted;

    public ObservableCollection<MarkerListViewModel> Markers { get; } = [];

    public ObservableCollection<MarkerListViewModel> EndMarkerCandidates { get; } = [];

    public ObservableCollection<AreaStaySegmentListViewModel> AreaSegments { get; } = [];

    public FfxiTempLogCollector.App.AsyncRelayCommand RunAnalysisCommand { get; }

    public RelayCommand CancelAnalysisCommand { get; }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
            {
                RunAnalysisCommand.RaiseCanExecuteChanged();
                CancelAnalysisCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public bool HasMarkers => Markers.Count > 0;

    public bool HasAreaSegments => AreaSegments.Count > 0;

    public bool HasRecords => _records.Count > 0;

    public string LastCompletedRangeName => _lastCompletedRangeName;

    // 分析区間（開始・終了ポイント）が確定し、分析実行が可能かどうか。
    // ステッパーのSTEP2完了判定に利用する。
    public bool IsRangeReady => CanRunAnalysis();

    public bool IsManualRangeMode
    {
        get => !IsAreaSegmentMode;
        set
        {
            if (value)
            {
                IsAreaSegmentMode = false;
            }
        }
    }

    public bool IsAreaSegmentMode
    {
        get => _isAreaSegmentMode;
        set
        {
            if (SetProperty(ref _isAreaSegmentMode, value))
            {
                OnPropertyChanged(nameof(IsManualRangeMode));
                RefreshValidation();
            }
        }
    }

    public AreaStaySegmentListViewModel? SelectedAreaSegment
    {
        get => _selectedAreaSegment;
        set
        {
            if (SetProperty(ref _selectedAreaSegment, value))
            {
                if (value is not null)
                {
                    IsAreaSegmentMode = true;
                }

                RefreshValidation();
            }
        }
    }

    public bool IsStartLogStart
    {
        get => _isStartLogStart;
        set
        {
            if (SetProperty(ref _isStartLogStart, value) && value)
            {
                SelectedStartMarker = null;
                OnPropertyChanged(nameof(IsStartMarker));
                RefreshEndMarkerCandidates();
                RefreshValidation();
            }
        }
    }

    public bool IsStartMarker
    {
        get => !IsStartLogStart;
        set
        {
            if (value)
            {
                if (SetProperty(ref _isStartLogStart, false, nameof(IsStartLogStart)))
                {
                    OnPropertyChanged(nameof(IsStartMarker));
                }

                RefreshEndMarkerCandidates();
                RefreshValidation();
            }
        }
    }

    public bool IsEndLogEnd
    {
        get => _isEndLogEnd;
        set
        {
            if (SetProperty(ref _isEndLogEnd, value) && value)
            {
                SelectedEndMarker = null;
                OnPropertyChanged(nameof(IsEndMarker));
                RefreshValidation();
            }
        }
    }

    public bool IsEndMarker
    {
        get => !IsEndLogEnd;
        set
        {
            if (value)
            {
                if (SetProperty(ref _isEndLogEnd, false, nameof(IsEndLogEnd)))
                {
                    OnPropertyChanged(nameof(IsEndMarker));
                }

                RefreshValidation();
            }
        }
    }

    public MarkerListViewModel? SelectedStartMarker
    {
        get => _selectedStartMarker;
        set
        {
            if (SetProperty(ref _selectedStartMarker, value))
            {
                if (value is not null)
                {
                    if (SetProperty(ref _isStartLogStart, false, nameof(IsStartLogStart)))
                    {
                        OnPropertyChanged(nameof(IsStartMarker));
                    }
                }

                RefreshEndMarkerCandidates();
                RefreshValidation();
            }
        }
    }

    public MarkerListViewModel? SelectedEndMarker
    {
        get => _selectedEndMarker;
        set
        {
            if (SetProperty(ref _selectedEndMarker, value))
            {
                if (value is not null)
                {
                    if (SetProperty(ref _isEndLogEnd, false, nameof(IsEndLogEnd)))
                    {
                        OnPropertyChanged(nameof(IsEndMarker));
                    }
                }

                RefreshValidation();
            }
        }
    }

    public string ValidationMessage
    {
        get => _validationMessage;
        private set => SetProperty(ref _validationMessage, value);
    }

    public string RangeSummary
    {
        get => _rangeSummary;
        private set => SetProperty(ref _rangeSummary, value);
    }

    public async Task LoadRecordsAsync(
        IReadOnlyList<CanonicalRecord> records,
        CancellationToken cancellationToken)
    {
        var prepared = await Task.Run(
            () => new PreparedRanges(
                new MarkerExtractor().Extract(records)
                    .Select(marker => new MarkerListViewModel(marker))
                    .ToArray(),
                new AreaStaySegmentBuilder().Build(records)
                    .Select(segment => new AreaStaySegmentListViewModel(segment))
                    .ToArray()),
            cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        _records = records;
        Markers.Clear();
        foreach (var marker in prepared.Markers)
        {
            Markers.Add(marker);
        }

        AreaSegments.Clear();
        foreach (var segment in prepared.AreaSegments)
        {
            AreaSegments.Add(segment);
        }

        _isStartLogStart = true;
        _isEndLogEnd = true;
        _selectedStartMarker = null;
        _selectedEndMarker = null;
        _selectedAreaSegment = null;
        _isAreaSegmentMode = false;
        OnPropertyChanged(nameof(IsStartLogStart));
        OnPropertyChanged(nameof(IsStartMarker));
        OnPropertyChanged(nameof(IsEndLogEnd));
        OnPropertyChanged(nameof(IsEndMarker));
        OnPropertyChanged(nameof(SelectedStartMarker));
        OnPropertyChanged(nameof(SelectedEndMarker));
        OnPropertyChanged(nameof(SelectedAreaSegment));
        OnPropertyChanged(nameof(IsAreaSegmentMode));
        OnPropertyChanged(nameof(IsManualRangeMode));
        OnPropertyChanged(nameof(HasMarkers));
        OnPropertyChanged(nameof(HasAreaSegments));
        OnPropertyChanged(nameof(HasRecords));
        RefreshEndMarkerCandidates();
        RefreshValidation();
    }

    public void Clear()
    {
        _records = [];
        Markers.Clear();
        EndMarkerCandidates.Clear();
        AreaSegments.Clear();
        SelectedStartMarker = null;
        SelectedEndMarker = null;
        SelectedAreaSegment = null;
        RangeSummary = "-";
        ValidationMessage = "セッションを読み込むと分析区間を選択できます。";
        OnPropertyChanged(nameof(HasMarkers));
        OnPropertyChanged(nameof(HasAreaSegments));
        OnPropertyChanged(nameof(HasRecords));
        RaiseRunAnalysisState();
    }

    private void RefreshEndMarkerCandidates()
    {
        var previous = SelectedEndMarker;
        EndMarkerCandidates.Clear();
        foreach (var marker in GetEndMarkerCandidates())
        {
            EndMarkerCandidates.Add(marker);
        }

        if (previous is not null && !EndMarkerCandidates.Contains(previous))
        {
            _selectedEndMarker = null;
            OnPropertyChanged(nameof(SelectedEndMarker));
        }

        RaiseRunAnalysisState();
    }

    private IEnumerable<MarkerListViewModel> GetEndMarkerCandidates()
    {
        if (SelectedStartMarker?.Marker.Order is not { } startOrder)
        {
            return Markers;
        }

        return Markers.Where(marker => marker.Marker.Order > startOrder);
    }

    private void RefreshValidation()
    {
        if (!HasRecords)
        {
            ValidationMessage = "セッションを読み込むと分析区間を選択できます。";
            RaiseRunAnalysisState();
            return;
        }

        var selection = CreateSelection();
        if (selection is null)
        {
            ValidationMessage = IsAreaSegmentMode
                ? "分析するエリアログ区間を選択してください。"
                : "開始markerまたは終了markerを選択してください。";
            RaiseRunAnalysisState();
            return;
        }

        var errors = _rangeValidator.Validate(selection);
        ValidationMessage = errors.Count == 0
            ? IsAreaSegmentMode
                ? "選択したエリアログ区間を分析できます。エリアチェンジ行自体は集計対象外です。"
                : "分析区間を選択できます。marker行自体は集計対象外です。"
            : string.Join(Environment.NewLine, errors);
        RaiseRunAnalysisState();
    }

    private bool CanRunAnalysis()
    {
        var selection = CreateSelection();
        return !IsBusy
            && selection is not null
            && _rangeValidator.IsValid(selection);
    }

    // 分析実行コマンドの実行可否と、ステッパー用のIsRangeReadyをまとめて通知する。
    private void RaiseRunAnalysisState()
    {
        RunAnalysisCommand.RaiseCanExecuteChanged();
        OnPropertyChanged(nameof(IsRangeReady));
    }

    private async Task UpdateRangeSummaryAsync()
    {
        var selection = CreateSelection();
        if (selection is null)
        {
            return;
        }

        var rangeName = CreateExportRangeName();

        _analysisCancellation?.Cancel();
        _analysisCancellation?.Dispose();
        _analysisCancellation = new CancellationTokenSource();
        var cancellationToken = _analysisCancellation.Token;
        IsBusy = true;
        ValidationMessage = "分析中...";
        try
        {
            var calculation = await Task.Run(
                () => Analyze(selection, cancellationToken),
                cancellationToken);
            RangeSummary = $"対象ログ: {calculation.RecordCount:N0}件 / 時刻精度: {AnalysisDisplayText.ToText(calculation.Result.AnalysisTime.Confidence)} / 分析時間: {ToDurationText(calculation.Result.AnalysisTime.DurationSeconds)}秒";
            ValidationMessage = "分析が完了しました。";
            _lastCompletedRangeName = rangeName;
            AnalysisCompleted?.Invoke(calculation.Result);
        }
        catch (OperationCanceledException)
        {
            ValidationMessage = "分析をキャンセルしました。";
        }
        catch (Exception exception)
        {
            ValidationMessage = $"分析に失敗しました: {exception.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private AnalysisCalculation Analyze(
        AnalysisRangeSelection selection,
        CancellationToken cancellationToken)
    {
        var range = _rangeBuilder.Build(_records, selection);
        cancellationToken.ThrowIfCancellationRequested();
        var time = _timeResolver.Resolve(selection, range);
        var parseResults = _actionGroupBuilder
            .Build(range)
            .Select(group =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                return _actionGroupParser.ParseGroup(group);
            })
            .ToArray();
        var parsed = parseResults
            .Where(result => result.Parsed is not null)
            .Select(result => result.Parsed!)
            .ToArray();
        var unparsed = parseResults
            .Where(result => result.Unparsed is not null)
            .Select(result => result.Unparsed!)
            .ToArray();
        var result = _analysisAggregator.Aggregate(parsed, time, unparsed) with
        {
            LevelingPointSummaries =
                _levelingPointAggregator.Aggregate(range, time),
        };
        return new AnalysisCalculation(result, range.Count);
    }

    private void CancelAnalysis()
    {
        _analysisCancellation?.Cancel();
    }

    private AnalysisRangeSelection? CreateSelection()
    {
        if (IsAreaSegmentMode)
        {
            return SelectedAreaSegment?.Segment.CreateSelection();
        }

        var start = IsStartLogStart
            ? AnalysisEndpoint.LogStart
            : SelectedStartMarker is null
                ? null
                : AnalysisEndpoint.FromMarker(SelectedStartMarker.Marker);
        var end = IsEndLogEnd
            ? AnalysisEndpoint.LogEnd
            : SelectedEndMarker is null
                ? null
                : AnalysisEndpoint.FromMarker(SelectedEndMarker.Marker);

        return start is null || end is null
            ? null
            : new AnalysisRangeSelection(start, end);
    }

    private static string ToDurationText(double? durationSeconds)
    {
        return durationSeconds?.ToString("0.###") ?? "-";
    }

    private string CreateExportRangeName()
    {
        if (IsAreaSegmentMode && SelectedAreaSegment is not null)
        {
            var segment = SelectedAreaSegment.Segment;
            return segment.AreaOccurrence > 1
                ? $"{segment.AreaName}_滞在{segment.AreaOccurrence}"
                : segment.AreaName;
        }

        return IsStartLogStart && IsEndLogEnd
            ? "全体"
            : "指定範囲";
    }

    private sealed record PreparedRanges(
        IReadOnlyList<MarkerListViewModel> Markers,
        IReadOnlyList<AreaStaySegmentListViewModel> AreaSegments);

    private sealed record AnalysisCalculation(
        AnalysisResult Result,
        int RecordCount);

    private bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
