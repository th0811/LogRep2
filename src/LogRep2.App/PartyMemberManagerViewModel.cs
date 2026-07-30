using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using FFXI_LogAnalyzer.Core;

namespace FfxiTempLogCollector.App;

public sealed class PartyMemberManagerViewModel : INotifyPropertyChanged
{
    public const int MaximumPartyMembers = 6;

    private readonly Action<IReadOnlyList<string>> _save;
    private readonly Dictionary<string, int> _occurrenceByName =
        new(StringComparer.OrdinalIgnoreCase);
    private string _nameInput = string.Empty;
    private string? _selectedMember;
    private PartyMemberCandidate? _selectedCandidate;

    public PartyMemberManagerViewModel(
        IEnumerable<string> members,
        IEnumerable<PartyMemberCandidate> candidates,
        Action<IReadOnlyList<string>> save)
    {
        _save = save ?? throw new ArgumentNullException(nameof(save));
        foreach (var member in members.Take(MaximumPartyMembers))
        {
            Members.Add(member);
        }

        foreach (var candidate in candidates
                     .OrderBy(candidate => candidate.Name, StringComparer.OrdinalIgnoreCase))
        {
            _occurrenceByName[candidate.Name] = candidate.OccurrenceCount;

            if (!Contains(Members, candidate.Name))
            {
                Candidates.Add(candidate);
            }
        }

        AddNameCommand = new RelayCommand(AddName);
        AddCandidateCommand = new RelayCommand(AddCandidate, CanAddCandidate);
        RemoveCommand = new RelayCommand(Remove, () => SelectedMember is not null);
        MoveUpCommand = new RelayCommand(MoveUp, CanMoveUp);
        MoveDownCommand = new RelayCommand(MoveDown, CanMoveDown);
        ClearCommand = new RelayCommand(Clear, () => Members.Count > 0);

        if (Members.Count < MaximumPartyMembers && Candidates.Count > 0)
        {
            SelectedCandidate = Candidates[0];
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<string> Members { get; } = [];
    public ObservableCollection<PartyMemberCandidate> Candidates { get; } = [];
    public RelayCommand AddNameCommand { get; }
    public RelayCommand AddCandidateCommand { get; }
    public RelayCommand RemoveCommand { get; }
    public RelayCommand MoveUpCommand { get; }
    public RelayCommand MoveDownCommand { get; }
    public RelayCommand ClearCommand { get; }

    public string NameInput
    {
        get => _nameInput;
        set => SetProperty(ref _nameInput, value);
    }

    public string? SelectedMember
    {
        get => _selectedMember;
        set
        {
            if (SetProperty(ref _selectedMember, value))
            {
                RaiseCanExecuteChanged();
            }
        }
    }

    public PartyMemberCandidate? SelectedCandidate
    {
        get => _selectedCandidate;
        set
        {
            if (SetProperty(ref _selectedCandidate, value))
            {
                RaiseCanExecuteChanged();
            }
        }
    }

    public string CountText => $"現在のPTメンバー（{Members.Count} / {MaximumPartyMembers}）";

    /// <summary>人数上限の進捗バー用。</summary>
    public int MemberCount => Members.Count;

    /// <summary>人数上限の進捗バー用。</summary>
    public int MemberCapacity => MaximumPartyMembers;

    private void AddName()
    {
        Add(ActorNameClassifier.NormalizePcName(NameInput));
        NameInput = string.Empty;
    }

    private void AddCandidate()
    {
        if (SelectedCandidate is not null)
        {
            Add(SelectedCandidate.Name);
        }
    }

    private void Add(string name)
    {
        if (string.IsNullOrWhiteSpace(name)
            || Members.Count >= MaximumPartyMembers
            || Contains(Members, name))
        {
            return;
        }

        Members.Add(name);
        var candidate = Candidates.FirstOrDefault(item =>
            string.Equals(item.Name, name, StringComparison.OrdinalIgnoreCase));
        if (candidate is not null)
        {
            var candidateIndex = Candidates.IndexOf(candidate);
            Candidates.RemoveAt(candidateIndex);
            SelectedCandidate = Members.Count >= MaximumPartyMembers
                ? null
                : SelectAtSameIndex(Candidates, candidateIndex);
        }

        Save();
    }

    private void Remove()
    {
        if (SelectedMember is null)
        {
            return;
        }

        var removed = SelectedMember;
        var memberIndex = Members.IndexOf(removed);
        Members.RemoveAt(memberIndex);
        RestoreCandidate(removed);

        SelectedMember = SelectAtSameIndex(Members, memberIndex);
        Save();
    }

    private void MoveUp() => Move(-1);
    private void MoveDown() => Move(1);

    private void Move(int offset)
    {
        if (SelectedMember is null)
        {
            return;
        }

        var index = Members.IndexOf(SelectedMember);
        var target = index + offset;
        if (target < 0 || target >= Members.Count)
        {
            return;
        }

        Members.Move(index, target);
        Save();
        RaiseCanExecuteChanged();
    }

    private void Clear()
    {
        foreach (var member in Members)
        {
            RestoreCandidate(member);
        }

        Members.Clear();
        SelectedMember = null;
        Save();
    }

    /// <summary>
    /// 登録メンバーから外れた名前を候補一覧へ戻す。出現数は初回に受け取った値を再利用する。
    /// </summary>
    private void RestoreCandidate(string name)
    {
        if (Candidates.Any(candidate =>
                string.Equals(candidate.Name, name, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        var occurrence = _occurrenceByName.TryGetValue(name, out var count)
            ? count
            : 0;
        Candidates.Add(new PartyMemberCandidate(name, occurrence));
    }

    private bool CanAddCandidate() =>
        SelectedCandidate is not null && Members.Count < MaximumPartyMembers;

    private bool CanMoveUp() =>
        SelectedMember is not null && Members.IndexOf(SelectedMember) > 0;

    private bool CanMoveDown() =>
        SelectedMember is not null
        && Members.IndexOf(SelectedMember) < Members.Count - 1;

    private void Save()
    {
        _save([.. Members]);
        OnPropertyChanged(nameof(CountText));
        OnPropertyChanged(nameof(MemberCount));
        RaiseCanExecuteChanged();
    }

    private void RaiseCanExecuteChanged()
    {
        AddCandidateCommand.RaiseCanExecuteChanged();
        RemoveCommand.RaiseCanExecuteChanged();
        MoveUpCommand.RaiseCanExecuteChanged();
        MoveDownCommand.RaiseCanExecuteChanged();
        ClearCommand.RaiseCanExecuteChanged();
    }

    private static bool Contains(IEnumerable<string> names, string name) =>
        names.Contains(name, StringComparer.OrdinalIgnoreCase);

    private static T? SelectAtSameIndex<T>(IReadOnlyList<T> items, int previousIndex)
        where T : class =>
        items.Count == 0 ? null : items[Math.Min(previousIndex, items.Count - 1)];

    private bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
