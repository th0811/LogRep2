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
    private string _validationMessage = "セッションを読み込むと分析区間を選択できます。";
    private string _rangeSummary = "-";

    public AnalysisRangeViewModel()
    {
        RunAnalysisCommand = new RelayCommand(UpdateRangeSummary, CanRunAnalysis);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public event Action<AnalysisResult>? AnalysisCompleted;

    public ObservableCollection<MarkerListViewModel> Markers { get; } = [];

    public ObservableCollection<MarkerListViewModel> EndMarkerCandidates { get; } = [];

    public RelayCommand RunAnalysisCommand { get; }

    public bool HasMarkers => Markers.Count > 0;

    public bool HasRecords => _records.Count > 0;

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

    public void LoadRecords(IReadOnlyList<CanonicalRecord> records)
    {
        _records = records;
        Markers.Clear();
        foreach (var marker in new MarkerExtractor().Extract(records))
        {
            Markers.Add(new MarkerListViewModel(marker));
        }

        _isStartLogStart = true;
        _isEndLogEnd = true;
        _selectedStartMarker = null;
        _selectedEndMarker = null;
        OnPropertyChanged(nameof(IsStartLogStart));
        OnPropertyChanged(nameof(IsStartMarker));
        OnPropertyChanged(nameof(IsEndLogEnd));
        OnPropertyChanged(nameof(IsEndMarker));
        OnPropertyChanged(nameof(SelectedStartMarker));
        OnPropertyChanged(nameof(SelectedEndMarker));
        OnPropertyChanged(nameof(HasMarkers));
        OnPropertyChanged(nameof(HasRecords));
        RefreshEndMarkerCandidates();
        RefreshValidation();
    }

    public void Clear()
    {
        _records = [];
        Markers.Clear();
        EndMarkerCandidates.Clear();
        SelectedStartMarker = null;
        SelectedEndMarker = null;
        RangeSummary = "-";
        ValidationMessage = "セッションを読み込むと分析区間を選択できます。";
        OnPropertyChanged(nameof(HasMarkers));
        OnPropertyChanged(nameof(HasRecords));
        RunAnalysisCommand.RaiseCanExecuteChanged();
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

        RunAnalysisCommand.RaiseCanExecuteChanged();
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
            RunAnalysisCommand.RaiseCanExecuteChanged();
            return;
        }

        var selection = CreateSelection();
        if (selection is null)
        {
            ValidationMessage = "開始markerまたは終了markerを選択してください。";
            RunAnalysisCommand.RaiseCanExecuteChanged();
            return;
        }

        var errors = _rangeValidator.Validate(selection);
        ValidationMessage = errors.Count == 0
            ? "分析区間を選択できます。marker行自体は集計対象外です。"
            : string.Join(Environment.NewLine, errors);
        RunAnalysisCommand.RaiseCanExecuteChanged();
    }

    private bool CanRunAnalysis()
    {
        var selection = CreateSelection();
        return selection is not null && _rangeValidator.IsValid(selection);
    }

    private void UpdateRangeSummary()
    {
        var selection = CreateSelection();
        if (selection is null)
        {
            return;
        }

        var range = _rangeBuilder.Build(_records, selection);
        var time = _timeResolver.Resolve(selection, range);
        var parseResults = _actionGroupBuilder
            .Build(range)
            .Select(group => _actionGroupParser.ParseGroup(group))
            .ToArray();
        var parsed = parseResults
            .Where(result => result.Parsed is not null)
            .Select(result => result.Parsed!)
            .ToArray();
        var unparsed = parseResults
            .Where(result => result.Unparsed is not null)
            .Select(result => result.Unparsed!)
            .ToArray();
        var levelingPointSummaries = _levelingPointAggregator.Aggregate(range, time);
        var analysisResult = _analysisAggregator.Aggregate(parsed, time, unparsed) with
        {
            LevelingPointSummaries = levelingPointSummaries
        };

        RangeSummary = $"対象レコード: {range.Count} 件 / time_confidence: {time.Confidence} / duration_seconds: {ToDurationText(time.DurationSeconds)}";
        AnalysisCompleted?.Invoke(analysisResult);
    }

    private AnalysisRangeSelection? CreateSelection()
    {
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
