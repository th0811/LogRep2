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
    private string _totalDps = "-";
    private string _totalHitRate = "-";

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

    /// <summary>合計行に出すPTメンバー合計DPS。</summary>
    public string TotalDps { get => _totalDps; private set => SetProperty(ref _totalDps, value); }

    /// <summary>合計行に出すPTメンバー全体の通常攻撃命中率。</summary>
    public string TotalHitRate { get => _totalHitRate; private set => SetProperty(ref _totalHitRate, value); }

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
        var actors = _partyMemberNames
            .Select(name => (
                Name: name,
                Actor: result?.ActorSummaries.FirstOrDefault(summary =>
                    string.Equals(summary.Actor, name, StringComparison.OrdinalIgnoreCase))))
            .OrderByDescending(entry => entry.Actor?.Dps ?? -1)
            .ToArray();
        var maxDps = actors
            .Select(entry => entry.Actor?.Dps ?? 0)
            .DefaultIfEmpty(0)
            .Max();

        PartyMembers.Clear();
        foreach (var (name, actor) in actors)
        {
            PartyMembers.Add(new PartyMemberMetric(
                name,
                actor?.Dps is null ? "-" : actor.Dps.Value.ToString("N2"),
                actor?.NormalAttackHitRate is null
                    ? "-"
                    : $"{actor.NormalAttackHitRate.Value * 100:N1}%",
                maxDps > 0 ? Math.Clamp((actor?.Dps ?? 0) / maxDps, 0, 1) : 0));
        }

        UpdateTotals(actors.Select(entry => entry.Actor));
    }

    private void UpdateTotals(IEnumerable<ActorSummary?> actors)
    {
        var summaries = actors
            .OfType<ActorSummary>()
            .ToArray();

        if (summaries.Length == 0)
        {
            TotalDps = "-";
            TotalHitRate = "-";
            return;
        }

        var dpsValues = summaries
            .Select(actor => actor.Dps)
            .OfType<double>()
            .ToArray();
        TotalDps = dpsValues.Length == 0
            ? "-"
            : dpsValues.Sum().ToString("N2");

        var hitCount = summaries.Sum(actor => actor.NormalAttackSummary.HitCount);
        var missCount = summaries.Sum(actor => actor.NormalAttackSummary.MissCount);
        var attemptCount = hitCount + missCount;
        TotalHitRate = attemptCount == 0
            ? "-"
            : $"{(double)hitCount / attemptCount * 100:N1}%";
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

/// <summary>
/// オーバーレイ1行分の表示値。<paramref name="DpsRatio"/> は最大DPSを1とした相対量（行背景バー用）。
/// </summary>
public sealed record PartyMemberMetric(
    string Name,
    string Dps,
    string HitRate,
    double DpsRatio = 0);
