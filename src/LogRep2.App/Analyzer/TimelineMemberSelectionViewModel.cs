namespace FFXI_LogAnalyzer.App;

public sealed class TimelineMemberSelectionViewModel
{
    private readonly Func<IReadOnlyList<(string Name, string Classification)>> _getCandidates;
    private readonly HashSet<string> _selected = new(StringComparer.Ordinal);

    public TimelineMemberSelectionViewModel(Func<IReadOnlyList<(string Name, string Classification)>> getCandidates)
    {
        _getCandidates = getCandidates;
        Refresh();
    }

    public IReadOnlyList<(string Name, string Classification)> Candidates { get; private set; } = [];
    public IReadOnlyList<string> SelectedMembers => Candidates.Where(item => _selected.Contains(item.Name)).Select(item => item.Name).ToArray();

    public void Refresh()
    {
        Candidates = _getCandidates();
        _selected.IntersectWith(Candidates.Select(item => item.Name));
    }

    public void SetSelected(string name, bool selected)
    {
        if (selected && Candidates.Any(item => item.Name == name)) _selected.Add(name);
        else _selected.Remove(name);
    }

    public void SelectWhere(Func<string, bool> predicate)
    {
        _selected.Clear();
        foreach (var candidate in Candidates.Where(item => predicate(item.Classification))) _selected.Add(candidate.Name);
    }
}
