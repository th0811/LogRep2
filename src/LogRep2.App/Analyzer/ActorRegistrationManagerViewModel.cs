using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using FFXI_LogAnalyzer.Core;

namespace FFXI_LogAnalyzer.App;

public sealed class ActorRegistrationManagerViewModel : INotifyPropertyChanged
{
    private readonly Action<AnalyzerSettings> _save;
    private readonly string? _sessionsRootFolderPath;
    private string _pcNameInput = string.Empty;
    private string _npcNameInput = string.Empty;
    private string? _selectedPcName;
    private string? _selectedNpcName;

    public ActorRegistrationManagerViewModel(
        AnalyzerSettings settings,
        Action<AnalyzerSettings> save)
    {
        ArgumentNullException.ThrowIfNull(settings);
        _save = save ?? throw new ArgumentNullException(nameof(save));
        _sessionsRootFolderPath = settings.SessionsRootFolderPath;

        foreach (var name in settings.KnownPcNames)
        {
            PcNames.Add(name);
        }

        foreach (var name in settings.KnownNpcNames)
        {
            NpcNames.Add(name);
        }

        AddPcNameCommand = new RelayCommand(AddPcName);
        AddNpcNameCommand = new RelayCommand(AddNpcName);
        RemovePcNameCommand = new RelayCommand(
            RemovePcName,
            () => !string.IsNullOrWhiteSpace(SelectedPcName));
        RemoveNpcNameCommand = new RelayCommand(
            RemoveNpcName,
            () => !string.IsNullOrWhiteSpace(SelectedNpcName));
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<string> PcNames { get; } = [];

    public ObservableCollection<string> NpcNames { get; } = [];

    public RelayCommand AddPcNameCommand { get; }

    public RelayCommand AddNpcNameCommand { get; }

    public RelayCommand RemovePcNameCommand { get; }

    public RelayCommand RemoveNpcNameCommand { get; }

    public string PcNameInput
    {
        get => _pcNameInput;
        set => SetProperty(ref _pcNameInput, value);
    }

    public string NpcNameInput
    {
        get => _npcNameInput;
        set => SetProperty(ref _npcNameInput, value);
    }

    public string? SelectedPcName
    {
        get => _selectedPcName;
        set
        {
            if (SetProperty(ref _selectedPcName, value))
            {
                RemovePcNameCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public string? SelectedNpcName
    {
        get => _selectedNpcName;
        set
        {
            if (SetProperty(ref _selectedNpcName, value))
            {
                RemoveNpcNameCommand.RaiseCanExecuteChanged();
            }
        }
    }

    private void AddPcName()
    {
        var normalized = NormalizeInput(PcNameInput);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return;
        }

        RemoveName(NpcNames, normalized);
        AddName(PcNames, normalized);
        PcNameInput = string.Empty;
        Save();
    }

    private void AddNpcName()
    {
        var normalized = NpcNameInput.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return;
        }

        RemoveName(
            PcNames,
            ActorNameClassifier.NormalizePcName(normalized));
        AddName(NpcNames, normalized);
        NpcNameInput = string.Empty;
        Save();
    }

    private void RemovePcName()
    {
        if (!string.IsNullOrWhiteSpace(SelectedPcName))
        {
            RemoveName(PcNames, SelectedPcName);
            Save();
        }
    }

    private void RemoveNpcName()
    {
        if (!string.IsNullOrWhiteSpace(SelectedNpcName))
        {
            RemoveName(NpcNames, SelectedNpcName);
            Save();
        }
    }

    private void Save()
    {
        _save(
            new AnalyzerSettings
            {
                SessionsRootFolderPath = _sessionsRootFolderPath,
                KnownPcNames = [.. PcNames],
                KnownNpcNames = [.. NpcNames]
            });
    }

    private static string NormalizeInput(string input)
    {
        return string.IsNullOrWhiteSpace(input)
            ? string.Empty
            : ActorNameClassifier.NormalizePcName(input);
    }

    private static void AddName(
        ObservableCollection<string> names,
        string name)
    {
        if (names.Any(
                current => string.Equals(
                    current,
                    name,
                    StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        names.Add(name);
        Sort(names);
    }

    private static void RemoveName(
        ObservableCollection<string> names,
        string name)
    {
        var existing = names.FirstOrDefault(
            current => string.Equals(
                current,
                name,
                StringComparison.OrdinalIgnoreCase));
        if (existing is not null)
        {
            names.Remove(existing);
        }
    }

    private static void Sort(ObservableCollection<string> names)
    {
        var sorted = names
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        names.Clear();
        foreach (var name in sorted)
        {
            names.Add(name);
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
        PropertyChanged?.Invoke(
            this,
            new PropertyChangedEventArgs(propertyName));
        return true;
    }
}
