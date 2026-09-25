using System.ComponentModel;
using System.Runtime.CompilerServices;
using FFXI_LogAnalyzer.Core;

namespace FFXI_LogAnalyzer.App;

public enum LogExclusionOperation
{
    ExcludeRows,
    RestoreRows,
    ExcludeGroups,
    RestoreGroups,
}

public sealed class LogExclusionViewModel : INotifyPropertyChanged
{
    private SessionSelectionViewModel? _selectedSession;
    private LogExclusionRow? _selectedRow;
    private string _searchText = string.Empty;
    private bool _excludedOnly;
    private bool _showGroupOnly;
    private string _statusMessage = "本文を検索し、前後ログまたは同一グループを確認して除外してください。";
    private LogExclusionRow[] _allRows = [];
    private IReadOnlyList<LogExclusionRow> _visibleRows = [];
    private IReadOnlyList<LogExclusionRow> _contextRows = [];

    public LogExclusionViewModel(IReadOnlyList<SessionSelectionViewModel> sessions, SessionSelectionViewModel? selected = null)
    {
        Sessions = sessions;
        SearchCommand = new RelayCommand(RefreshRows);
        PreviousMatchCommand = new RelayCommand(() => MoveMatch(-1));
        NextMatchCommand = new RelayCommand(() => MoveMatch(1));
        SelectedSession = selected ?? sessions.FirstOrDefault();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public IReadOnlyList<SessionSelectionViewModel> Sessions { get; }
    public RelayCommand SearchCommand { get; }
    public RelayCommand PreviousMatchCommand { get; }
    public RelayCommand NextMatchCommand { get; }
    public bool HasChanges { get; private set; }
    public bool CanEdit => SelectedSession is { ExclusionsLoadError: null };
    public string Summary => $"表示 {VisibleRows.Count:N0} / 全 {_allRows.Length:N0}行　除外 {_allRows.Count(row => row.IsExcluded):N0}行";

    public SessionSelectionViewModel? SelectedSession
    {
        get => _selectedSession;
        set
        {
            if (!Set(ref _selectedSession, value)) return;
            _allRows = value?.Records.Select((record, index) => new LogExclusionRow(value, record, index)).ToArray() ?? [];
            SelectedRow = null;
            StatusMessage = value?.ExclusionsLoadError ?? "除外・解除は操作ごとに保存されます。元ログは変更しません。";
            OnPropertyChanged(nameof(CanEdit));
            RefreshRows();
        }
    }

    public string SearchText { get => _searchText; set => Set(ref _searchText, value); }
    public string StatusMessage { get => _statusMessage; private set => Set(ref _statusMessage, value); }
    public IReadOnlyList<LogExclusionRow> VisibleRows { get => _visibleRows; private set => Set(ref _visibleRows, value); }
    public IReadOnlyList<LogExclusionRow> ContextRows { get => _contextRows; private set => Set(ref _contextRows, value); }

    public bool ExcludedOnly
    {
        get => _excludedOnly;
        set { if (Set(ref _excludedOnly, value)) RefreshRows(); }
    }

    public bool ShowGroupOnly
    {
        get => _showGroupOnly;
        set { if (Set(ref _showGroupOnly, value)) RefreshContext(); }
    }

    public LogExclusionRow? SelectedRow
    {
        get => _selectedRow;
        set { if (Set(ref _selectedRow, value)) RefreshContext(); }
    }

    private void RefreshRows()
    {
        var searchText = SearchText.Trim();
        VisibleRows = _allRows.Where(row =>
            (!ExcludedOnly || row.IsExcluded)
            && row.Text.Contains(searchText, StringComparison.OrdinalIgnoreCase)).ToArray();
        if (SelectedRow is null || !VisibleRows.Contains(SelectedRow))
        {
            SelectedRow = VisibleRows.FirstOrDefault();
        }

        RefreshContext();
        OnPropertyChanged(nameof(Summary));
    }

    private void RefreshContext()
    {
        if (SelectedRow is not { } selected)
        {
            ContextRows = [];
            return;
        }

        var key = AnalysisExclusions.TryGetGroupKey(selected.Record);
        var start = Math.Max(0, selected.Index - 10);
        ContextRows = ShowGroupOnly
            ? key is null ? [selected] : _allRows.Where(row => AnalysisExclusions.TryGetGroupKey(row.Record) == key).ToArray()
            : _allRows.Skip(start).Take(selected.Index + 11 - start).ToArray();
    }

    private void MoveMatch(int direction)
    {
        if (VisibleRows.Count == 0) return;
        var index = SelectedRow is null ? -1 : Array.IndexOf(VisibleRows.ToArray(), SelectedRow);
        SelectedRow = VisibleRows[(index + direction + VisibleRows.Count) % VisibleRows.Count];
    }

    public void Apply(LogExclusionOperation operation, IReadOnlyList<LogExclusionRow> rows)
    {
        if (!CanEdit || SelectedSession is not { } session) return;
        if (rows.Count == 0)
        {
            StatusMessage = "操作するログ行を選択してください。";
            return;
        }

        if (rows.Any(row => !ReferenceEquals(row.Session, session)))
        {
            StatusMessage = "現在のセッションのログ行を選択してください。";
            return;
        }

        var updated = session.Exclusions.Clone();
        if (operation is LogExclusionOperation.ExcludeRows or LogExclusionOperation.RestoreRows)
        {
            if (rows.Any(row => AnalysisExclusions.TryGetRecordKey(row.Record) is null))
            {
                StatusMessage = "行IDまたはセッションIDがない行は、行単位で除外・解除できません。グループ単位の操作を使用してください。";
                return;
            }

            if (rows.Any(row => updated.IsGroupExcluded(row.Record)))
            {
                StatusMessage = "グループ全体が除外されています。「グループを戻す」で解除してください。";
                return;
            }

            foreach (var row in rows)
            {
                var key = AnalysisExclusions.TryGetRecordKey(row.Record)!;
                if (operation == LogExclusionOperation.ExcludeRows) updated.Records.Add(key);
                else updated.Records.Remove(key);
            }
        }
        else
        {
            if (rows.Any(row => AnalysisExclusions.TryGetGroupKey(row.Record) is null))
            {
                StatusMessage = "グループIDまたはセッションIDがない行は、グループ単位で除外・解除できません。";
                return;
            }

            var groups = rows.Select(row => AnalysisExclusions.TryGetGroupKey(row.Record)!).ToHashSet();
            foreach (var group in groups)
            {
                if (operation == LogExclusionOperation.ExcludeGroups) updated.Groups.Add(group);
                else updated.Groups.Remove(group);
            }

            // グループ操作は、検索で隠れている行を含むグループ全体に適用します。
            foreach (var row in _allRows.Where(row => AnalysisExclusions.TryGetGroupKey(row.Record) is { } key && groups.Contains(key)))
            {
                if (AnalysisExclusions.TryGetRecordKey(row.Record) is { } key) updated.Records.Remove(key);
            }
        }

        if (updated.Records.SetEquals(session.Exclusions.Records) && updated.Groups.SetEquals(session.Exclusions.Groups))
        {
            StatusMessage = "選択したログはすでに指定の状態です。";
            return;
        }

        try
        {
            session.SaveExclusions(updated);
            HasChanges = true;
            foreach (var row in _allRows) row.Refresh();
            RefreshRows();
            StatusMessage = "除外設定を保存しました。閉じた後、同じ分析区間で再分析してください。";
        }
        catch (Exception exception)
        {
            StatusMessage = $"変更を適用できませんでした。除外状態は変更していません。{exception.Message}";
        }
    }

    private bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(name);
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

public sealed class LogExclusionRow(SessionSelectionViewModel session, CanonicalRecord record, int index) : INotifyPropertyChanged
{
    public SessionSelectionViewModel Session { get; } = session;
    public CanonicalRecord Record { get; } = record;
    public int Index { get; } = index;
    public long? Order => Record.Order;
    public string? Time => Record.MessageTimeText;
    public string Text => Record.VisibleText ?? string.Empty;
    public string? EventGroup => Record.EventGroup;
    public bool IsExcluded => Session.Exclusions.IsExcluded(Record);
    public string State => Session.ExclusionsLoadError is not null ? "設定読込エラー"
        : Session.Exclusions.IsGroupExcluded(Record) ? "グループ除外"
        : Session.Exclusions.IsRecordExcluded(Record) ? "行除外" : "対象";
    public event PropertyChangedEventHandler? PropertyChanged;
    public void Refresh() => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(null));
}
