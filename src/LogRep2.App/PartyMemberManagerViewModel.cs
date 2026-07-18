using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using FFXI_LogAnalyzer.Core;

namespace FfxiTempLogCollector.App;

public sealed class PartyMemberManagerViewModel : INotifyPropertyChanged
{
    public const int MaximumPartyMembers = 6;

    private readonly Action<IReadOnlyList<string>> _save;
    private string _nameInput = string.Empty;
    private string? _selectedMember;
    private string? _selectedCandidate;

    public PartyMemberManagerViewModel(
        IEnumerable<string> members,
        IEnumerable<string> candidates,
        Action<IReadOnlyList<string>> save)
    {
        _save = save ?? throw new ArgumentNullException(nameof(save));
        foreach (var member in members.Take(MaximumPartyMembers))
        {
            Members.Add(member);
        }

        foreach (var candidate in candidates
                     .Where(candidate => !Contains(Members, candidate))
                     .OrderBy(candidate => candidate, StringComparer.OrdinalIgnoreCase))
        {
            Candidates.Add(candidate);
        }

        AddNameCommand = new RelayCommand(AddName);
        AddCandidateCommand = new RelayCommand(AddCandidate, CanAddCandidate);
        RemoveCommand = new RelayCommand(Remove, () => SelectedMember is not null);
        MoveUpCommand = new RelayCommand(MoveUp, CanMoveUp);
        MoveDownCommand = new RelayCommand(MoveDown, CanMoveDown);
        ClearCommand = new RelayCommand(Clear, () => Members.Count > 0);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<string> Members { get; } = [];
    public ObservableCollection<string> Candidates { get; } = [];
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

    public string? SelectedCandidate
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

    private void AddName()
    {
        Add(ActorNameClassifier.NormalizePcName(NameInput));
        NameInput = string.Empty;
    }

    private void AddCandidate()
    {
        if (SelectedCandidate is not null)
        {
            Add(SelectedCandidate);
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
            string.Equals(item, name, StringComparison.OrdinalIgnoreCase));
        if (candidate is not null)
        {
            Candidates.Remove(candidate);
            SelectedCandidate = null;
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
        Members.Remove(removed);
        if (!Contains(Candidates, removed))
        {
            Candidates.Add(removed);
        }

        SelectedMember = null;
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
            if (!Contains(Candidates, member))
            {
                Candidates.Add(member);
            }
        }

        Members.Clear();
        SelectedMember = null;
        Save();
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
