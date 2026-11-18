using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using FFXI_LogAnalyzer.Core;

namespace FFXI_LogAnalyzer.App;

public sealed class LogRangeViewModel : INotifyPropertyChanged
{
    private readonly SavedAnalysisRangesStore _store = new();
    private SessionSelectionViewModel? _selectedSession;
    private RangeLogRow[] _rows = [];
    private IReadOnlyList<RangeLogRow> _visibleRows = [];
    private RangeLogRow? _selectedRow;
    private RangeLogRow? _start;
    private RangeLogRow? _end;
    private SavedAnalysisRange? _selectedSavedRange;
    private string _searchText = string.Empty;
    private bool _includeMatchingGroups;
    private string _rangeName = string.Empty;
    private string _statusMessage = string.Empty;
    private string? _loadError;

    public LogRangeViewModel(IReadOnlyList<SessionSelectionViewModel> sessions, SessionSelectionViewModel? selected = null)
    {
        Sessions = sessions;
        SearchCommand = new RelayCommand(RefreshRows);
        SetStartCommand = new RelayCommand(() => SetBoundary(true), () => SelectedRow?.Record.Order is not null);
        SetEndCommand = new RelayCommand(() => SetBoundary(false), () => SelectedRow?.Record.Order is not null);
        AlignGroupsCommand = new RelayCommand(AlignGroups, () => CanUseRange);
        SaveRangeCommand = new RelayCommand(SaveRange, () => CanSaveRange);
        DeleteRangeCommand = new RelayCommand(() => DeleteRequested?.Invoke(), () => _loadError is null && SelectedSavedRange is not null);
        UseRangeCommand = new RelayCommand(() => UseRequested?.Invoke(), () => CanUseRange);
        SelectedSession = selected is not null && sessions.Contains(selected) ? selected : sessions.FirstOrDefault();
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    public event Action? UseRequested;
    public event Action? DeleteRequested;
    public IReadOnlyList<SessionSelectionViewModel> Sessions { get; }
    public ObservableCollection<SavedAnalysisRange> SavedRanges { get; } = [];
    public RelayCommand SearchCommand { get; }
    public RelayCommand SetStartCommand { get; }
    public RelayCommand SetEndCommand { get; }
    public RelayCommand AlignGroupsCommand { get; }
    public RelayCommand SaveRangeCommand { get; }
    public RelayCommand DeleteRangeCommand { get; }
    public RelayCommand UseRangeCommand { get; }

    public SessionSelectionViewModel? SelectedSession
    {
        get => _selectedSession;
        set
        {
            if (!Set(ref _selectedSession, value)) return;
            _start = _end = null;
            _selectedSavedRange = null;
            _selectedRow = null;
            _rangeName = string.Empty;
            _loadError = null;
            SavedRanges.Clear();
            _rows = value?.Records.Select(record => new RangeLogRow(record)).OrderBy(row => row.Record.Order ?? long.MaxValue).ToArray() ?? [];
            try
            {
                if (value is not null)
                    foreach (var range in _store.Load(value.FolderPath, value.SessionId).Ranges) SavedRanges.Add(range);
                StatusMessage = "開始行・終了行を選び「この区間を使う」を押してください。元ログは変更しません。";
            }
            catch (Exception exception)
            {
                _loadError = $"保存区間を読み込めません。保存・削除はできませんが、一時的な区間指定はできます。{exception.Message}";
                ReportError(_loadError, exception);
            }
            OnPropertyChanged(nameof(SelectedSavedRange));
            OnPropertyChanged(nameof(RangeName));
            OnPropertyChanged(nameof(LoadError));
            RefreshRows();
            RefreshBoundaries();
        }
    }

    public string? LoadError => _loadError;
    public IReadOnlyList<RangeLogRow> VisibleRows { get => _visibleRows; private set => Set(ref _visibleRows, value); }
    public RangeLogRow? SelectedRow
    {
        get => _selectedRow;
        set { if (Set(ref _selectedRow, value)) RefreshCommands(); }
    }
    public RangeLogRow? StartRow => _start;
    public RangeLogRow? EndRow => _end;
    public string SearchText { get => _searchText; set => Set(ref _searchText, value); }
    public bool IncludeMatchingGroups
    {
        get => _includeMatchingGroups;
        set { if (Set(ref _includeMatchingGroups, value)) RefreshRows(); }
    }
    public string RangeName
    {
        get => _rangeName;
        set { if (Set(ref _rangeName, value)) RefreshCommands(); }
    }
    public string StatusMessage { get => _statusMessage; private set => Set(ref _statusMessage, value); }
    public string SearchSummary => $"表示 {VisibleRows.Count:N0} / 全 {_rows.Length:N0}行";
    public string StartText => Describe(_start);
    public string EndText => Describe(_end);
    public string RangeSummary => CanUseRange
        ? $"区間内 {_rows.Count(row => row.Record.Order >= _start!.Record.Order && row.Record.Order <= _end!.Record.Order):N0}行（開始・終了行と検索で非表示の行を含みます）"
        : "開始・終了行を指定してください。終了行は開始行以降に指定します。";
    public string GroupWarning
    {
        get
        {
            if (!CanUseRange) return string.Empty;
            var groups = _rows.Where(row => row.Record.Order >= _start!.Record.Order && row.Record.Order <= _end!.Record.Order)
                .Select(row => AnalysisExclusions.TryGetGroupKey(row.Record)).Where(key => key is not null).ToHashSet();
            return _rows.Any(row => (row.Record.Order < _start!.Record.Order || row.Record.Order > _end!.Record.Order)
                && AnalysisExclusions.TryGetGroupKey(row.Record) is { } key && groups.Contains(key))
                ? "イベントグループが区間の外まで続いています。必要に応じて「グループ全体に合わせる」を押してください。"
                : string.Empty;
        }
    }
    public bool CanUseRange => _start?.Record.Order is { } start && _end?.Record.Order is { } end && start <= end
        && _rows.Count(row => row.Record.Order == start) == 1 && _rows.Count(row => row.Record.Order == end) == 1;
    public bool CanSaveRange => CanUseRange && _loadError is null && !string.IsNullOrWhiteSpace(RangeName)
        && HasStableId(_start!) && HasStableId(_end!);
    public string SaveHint => CanUseRange && (!HasStableId(_start!) || !HasStableId(_end!))
        ? "開始・終了行に一意なセッションID・行IDがないため保存できません。この区間を今回だけ使うことはできます。"
        : "名前を付けて保存すると、次回も保存区間から選び直せます（100文字以内）。";

    public SavedAnalysisRange? SelectedSavedRange
    {
        get => _selectedSavedRange;
        set
        {
            if (!Set(ref _selectedSavedRange, value)) return;
            if (value is not null && SelectedSession is { } session)
            {
                _start = _end = null;
                RangeName = value.Name;
                try
                {
                    var resolved = SavedAnalysisRangesStore.Resolve(session.Records, session.SessionId, value);
                    _start = _rows.Single(row => ReferenceEquals(row.Record, resolved.Start));
                    _end = _rows.Single(row => ReferenceEquals(row.Record, resolved.End));
                    StatusMessage = "保存区間を復元しました。「この区間を使う」で分析区間へ反映します。";
                }
                catch (Exception exception) { ReportError(exception.Message, exception); }
                RefreshBoundaries();
            }
            RefreshCommands();
        }
    }

    private bool HasStableId(RangeLogRow row) => SelectedSession is { } session
        && !string.IsNullOrWhiteSpace(row.Record.SessionId) && row.Record.SessionId == session.SessionId
        && !string.IsNullOrWhiteSpace(row.Record.CanonicalRecordId)
        && _rows.Count(item => item.Record.SessionId == row.Record.SessionId
            && item.Record.CanonicalRecordId == row.Record.CanonicalRecordId) == 1;

    private void RefreshRows()
    {
        var query = SearchText.Trim();
        var groups = IncludeMatchingGroups && query.Length > 0
            ? _rows.Where(row => row.Text.Contains(query, StringComparison.OrdinalIgnoreCase))
                .Select(row => AnalysisExclusions.TryGetGroupKey(row.Record)).Where(key => key is not null).ToHashSet()
            : null;
        VisibleRows = _rows.Where(row => row.Text.Contains(query, StringComparison.OrdinalIgnoreCase)
            || (groups is not null && AnalysisExclusions.TryGetGroupKey(row.Record) is { } key && groups.Contains(key))).ToArray();
        if (SelectedRow is null || !VisibleRows.Contains(SelectedRow)) SelectedRow = VisibleRows.FirstOrDefault();
        OnPropertyChanged(nameof(SearchSummary));
    }

    private void SetBoundary(bool start)
    {
        if (SelectedRow is null) return;
        if (start) _start = SelectedRow;
        else _end = SelectedRow;
        SelectedSavedRange = null;
        RefreshBoundaries();
    }

    private void AlignGroups()
    {
        if (!CanUseRange) return;
        // 境界を広げた先で別のグループを切らないよう、範囲が安定するまで拡張します。
        while (true)
        {
            var groups = _rows.Where(row => row.Record.Order >= _start!.Record.Order && row.Record.Order <= _end!.Record.Order)
                .Select(row => AnalysisExclusions.TryGetGroupKey(row.Record)).Where(key => key is not null).ToHashSet();
            var included = _rows.Where(row => row.Record.Order is not null
                && (ReferenceEquals(row, _start) || ReferenceEquals(row, _end)
                    || (AnalysisExclusions.TryGetGroupKey(row.Record) is { } key && groups.Contains(key)))).ToArray();
            var start = included[0];
            var end = included[^1];
            if (ReferenceEquals(start, _start) && ReferenceEquals(end, _end)) break;
            _start = start;
            _end = end;
        }
        SelectedSavedRange = null;
        RefreshBoundaries();
    }

    private void SaveRange()
    {
        if (!CanSaveRange || SelectedSession is not { } session) return;
        try
        {
            var name = SessionAnnotationsStore.NormalizeAlias(RangeName);
            if (SavedRanges.Any(range => range.Name == name)) throw new InvalidOperationException("同じ名前の区間があります。別の名前を入力してください。");
            var saved = new SavedAnalysisRange(Guid.NewGuid().ToString("N"), name, _start!.Record.CanonicalRecordId!, _end!.Record.CanonicalRecordId!);
            var data = _store.Load(session.FolderPath, session.SessionId);
            if (data.Ranges.Any(range => range.Name == name)) throw new InvalidOperationException("同じ名前の区間が保存されています。別の名前を入力してください。");
            data.Ranges.Add(saved);
            _store.Save(session.FolderPath, data);
            SavedRanges.Clear();
            foreach (var range in data.Ranges) SavedRanges.Add(range);
            SelectedSavedRange = saved;
            StatusMessage = "区間を保存しました。元ログは変更していません。";
        }
        catch (Exception exception) { ReportError($"区間を保存できませんでした。{exception.Message}", exception); }
    }

    public void DeleteSelectedRange()
    {
        if (!DeleteRangeCommand.CanExecute(null) || SelectedSession is not { } session || SelectedSavedRange is not { } selected) return;
        try
        {
            var data = _store.Load(session.FolderPath, session.SessionId);
            data.Ranges.RemoveAll(range => range.Id == selected.Id);
            _store.Save(session.FolderPath, data);
            SelectedSavedRange = null;
            SavedRanges.Clear();
            foreach (var range in data.Ranges) SavedRanges.Add(range);
            StatusMessage = "保存区間を削除しました。元ログと現在の開始・終了位置は変更していません。";
        }
        catch (Exception exception) { ReportError($"保存区間を削除できませんでした。{exception.Message}", exception); }
    }

    private void RefreshBoundaries()
    {
        var canUse = CanUseRange;
        foreach (var row in _rows)
            row.UpdateBoundary(ReferenceEquals(row, _start), ReferenceEquals(row, _end),
                canUse && row.Record.Order >= _start!.Record.Order && row.Record.Order <= _end!.Record.Order);
        foreach (var name in new[] { nameof(StartRow), nameof(EndRow), nameof(StartText), nameof(EndText), nameof(RangeSummary), nameof(GroupWarning), nameof(SaveHint) })
            OnPropertyChanged(name);
        RefreshCommands();
    }
    private void RefreshCommands()
    {
        foreach (var command in new[] { SetStartCommand, SetEndCommand, AlignGroupsCommand, SaveRangeCommand, DeleteRangeCommand, UseRangeCommand })
            command.RaiseCanExecuteChanged();
    }
    private static string Describe(RangeLogRow? row) => row is null ? "未指定" : $"ログ順 {row.Order:N0}　{row.Time}　{row.Text}";
    private void ReportError(string message, Exception exception)
    {
        StatusMessage = message;
        FfxiTempLogCollector.App.DiagnosticLogService.Write("分析区間の編集エラー", exception);
    }
    private bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(name);
        return true;
    }
    private void OnPropertyChanged([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new(name));
}

public sealed class RangeLogRow(CanonicalRecord record) : INotifyPropertyChanged
{
    public CanonicalRecord Record { get; } = record;
    public long? Order => Record.Order;
    public string Text => Record.VisibleText ?? string.Empty;
    public string? Time => Record.MessageTimeText;
    public string? EventGroup => Record.EventGroup;
    public string Boundary { get; private set; } = string.Empty;
    public bool IsInRange { get; private set; }
    public event PropertyChangedEventHandler? PropertyChanged;
    public void UpdateBoundary(bool start, bool end, bool included)
    {
        var boundary = start && end ? "開始・終了" : start ? "開始" : end ? "終了" : string.Empty;
        if (Boundary == boundary && IsInRange == included) return;
        Boundary = boundary;
        IsInRange = included;
        PropertyChanged?.Invoke(this, new(null));
    }
}
