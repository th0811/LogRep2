using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using FFXI_LogAnalyzer.Core;
using LogRep2.Infrastructure;

namespace FfxiTempLogCollector.App;

public sealed class OverlayViewModel : INotifyPropertyChanged
{
    private readonly OverlaySettings _settings;
    private readonly Action _hide;
    private readonly Action _settingsChanged;
    private readonly Action _openPartyMemberSettings;
    private List<string> _partyMemberNames;
    private string _lastUpdated = "-";

    public OverlayViewModel(
        OverlaySettings settings,
        IEnumerable<string> partyMemberNames,
        Action hide,
        Action settingsChanged,
        Action? openPartyMemberSettings = null)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _partyMemberNames = [.. partyMemberNames];
        _hide = hide ?? throw new ArgumentNullException(nameof(hide));
        _settingsChanged = settingsChanged ?? throw new ArgumentNullException(nameof(settingsChanged));
        _openPartyMemberSettings = openPartyMemberSettings ?? (() => { });
        HideCommand = new RelayCommand(_hide);
        OpenPartyMemberSettingsCommand = new RelayCommand(_openPartyMemberSettings);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public RelayCommand HideCommand { get; }

    public RelayCommand OpenPartyMemberSettingsCommand { get; }

    public ObservableCollection<PartyMemberMetric> PartyMembers { get; } = [];

    public bool HasPartyMembers => _partyMemberNames.Count > 0;

    public bool ShowEmptyPartyState => !HasPartyMembers;

    public string LastUpdated { get => _lastUpdated; private set => SetProperty(ref _lastUpdated, value); }

    public double OverlayOpacity
    {
        get => _settings.Opacity;
        set
        {
            var normalized = Math.Clamp(value, 0.25, 1.0);
            if (Math.Abs(_settings.Opacity - normalized) < 0.001)
            {
                return;
            }

            _settings.Opacity = normalized;
            OnPropertyChanged();
            _settingsChanged();
        }
    }

    public double FontSize
    {
        get => _settings.FontSize;
        set
        {
            var normalized = Math.Clamp(value, 10, 40);
            if (Math.Abs(_settings.FontSize - normalized) < 0.001)
            {
                return;
            }

            _settings.FontSize = normalized;
            OnPropertyChanged();
            _settingsChanged();
        }
    }

    public bool Topmost
    {
        get => _settings.Topmost;
        set
        {
            if (_settings.Topmost == value)
            {
                return;
            }

            _settings.Topmost = value;
            OnPropertyChanged();
            _settingsChanged();
        }
    }

    public void Apply(RealtimeAnalysisSnapshot snapshot)
    {
        LastUpdated = snapshot.LastUpdatedAt?.ToLocalTime().ToString("HH:mm:ss") ?? "-";
        UpdatePartyMembers(snapshot.Result);
    }

    public void SetPartyMembers(IEnumerable<string> partyMemberNames)
    {
        _partyMemberNames = [.. partyMemberNames];
        OnPropertyChanged(nameof(HasPartyMembers));
        OnPropertyChanged(nameof(ShowEmptyPartyState));
    }

    private void UpdatePartyMembers(AnalysisResult? result)
    {
        PartyMembers.Clear();
        foreach (var name in _partyMemberNames)
        {
            var actor = result?.ActorSummaries.FirstOrDefault(summary =>
                string.Equals(summary.Actor, name, StringComparison.OrdinalIgnoreCase));
            PartyMembers.Add(new PartyMemberMetric(
                name,
                actor?.Dps is null ? "-" : actor.Dps.Value.ToString("N2"),
                actor?.NormalAttackHitRate is null
                    ? "-"
                    : $"{actor.NormalAttackHitRate.Value * 100:N1}%"));
        }
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

public sealed record PartyMemberMetric(string Name, string Dps, string HitRate);
