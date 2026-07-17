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
    private readonly Action _resetPosition;
    private readonly Action _settingsChanged;
    private bool _isEditing;
    private string _stateText = "停止中";
    private long _totalDamage;
    private string _dps = "-";
    private string _hitRate = "-";
    private string _lastUpdated = "-";

    public OverlayViewModel(
        OverlaySettings settings,
        Action hide,
        Action resetPosition,
        Action settingsChanged)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _hide = hide ?? throw new ArgumentNullException(nameof(hide));
        _resetPosition = resetPosition ?? throw new ArgumentNullException(nameof(resetPosition));
        _settingsChanged = settingsChanged ?? throw new ArgumentNullException(nameof(settingsChanged));
        _isEditing = !_settings.PositionLocked;
        ToggleEditingCommand = new RelayCommand(ToggleEditing);
        ResetPositionCommand = new RelayCommand(_resetPosition);
        HideCommand = new RelayCommand(_hide);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public event EventHandler? EditingChanged;

    public RelayCommand ToggleEditingCommand { get; }

    public RelayCommand ResetPositionCommand { get; }

    public RelayCommand HideCommand { get; }

    public ObservableCollection<string> ActorRankings { get; } = [];

    public bool ShowAnalysisState => Shows("analysis_state");

    public bool ShowTotalDamage => Shows("total_damage");

    public bool ShowDpsOrHitRate => Shows("dps") || Shows("hit_rate");

    public bool ShowActorRanking => Shows("actor_ranking");

    public bool IsEditing
    {
        get => _isEditing;
        private set
        {
            if (SetProperty(ref _isEditing, value))
            {
                OnPropertyChanged(nameof(EditButtonText));
                EditingChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    public string EditButtonText => IsEditing ? "固定" : "編集";

    public string StateText { get => _stateText; private set => SetProperty(ref _stateText, value); }

    public long TotalDamage { get => _totalDamage; private set => SetProperty(ref _totalDamage, value); }

    public string Dps { get => _dps; private set => SetProperty(ref _dps, value); }

    public string HitRate { get => _hitRate; private set => SetProperty(ref _hitRate, value); }

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
        StateText = snapshot.State switch
        {
            RealtimeAnalysisState.Running => "分析中",
            RealtimeAnalysisState.Completed => "分析終了",
            _ => "停止中",
        };
        TotalDamage = snapshot.Result?.ActorSummaries.Sum(actor => (long)actor.TotalDamage) ?? 0;
        var actorsWithDps = snapshot.Result?.ActorSummaries
            .Where(actor => actor.Dps is not null)
            .ToArray() ?? [];
        Dps = actorsWithDps.Length == 0
            ? "-"
            : actorsWithDps.Sum(actor => actor.Dps!.Value).ToString("N2");
        var hit = snapshot.Result?.ActorSummaries.Sum(actor => actor.TotalHitCount) ?? 0;
        var miss = snapshot.Result?.ActorSummaries.Sum(actor => actor.TotalMissCount) ?? 0;
        HitRate = hit + miss == 0 ? "-" : $"{hit * 100d / (hit + miss):N1}%";
        LastUpdated = snapshot.LastUpdatedAt?.ToLocalTime().ToString("HH:mm:ss") ?? "-";
        UpdateActorRankings(snapshot.Result);
    }

    private void UpdateActorRankings(AnalysisResult? result)
    {
        ActorRankings.Clear();
        if (result is null)
        {
            return;
        }

        var rank = 1;
        foreach (var actor in result.ActorSummaries
                     .OrderByDescending(actor => actor.TotalDamage)
                     .ThenBy(actor => actor.Actor, StringComparer.Ordinal)
                     .Take(_settings.DisplayRowCount))
        {
            ActorRankings.Add($"{rank}. {actor.Actor}  {actor.TotalDamage:N0}");
            rank++;
        }
    }

    private bool Shows(string item)
    {
        return _settings.DisplayItems.Contains(item, StringComparer.OrdinalIgnoreCase);
    }

    private void ToggleEditing()
    {
        IsEditing = !IsEditing;
        _settings.PositionLocked = !IsEditing;
        _settingsChanged();
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
