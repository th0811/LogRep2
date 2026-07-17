using System.ComponentModel;
using System.Runtime.CompilerServices;
using FFXI_LogAnalyzer.Core;
using MediaBrushes = System.Windows.Media.Brushes;

namespace FFXI_LogAnalyzer.App;

public sealed class ActorVisibilityViewModel : INotifyPropertyChanged
{
    private readonly Action<string> _registerAsPc;
    private readonly Action<string> _registerAsNpc;
    private readonly Action<string> _clearRegistration;
    private bool _isVisible;
    private ActorNameKind _nameKind;

    public ActorVisibilityViewModel(
        ActorSummaryViewModel summary,
        ActorNameKind nameKind,
        bool isVisible,
        Action<string> registerAsPc,
        Action<string> registerAsNpc,
        Action<string> clearRegistration)
    {
        ArgumentNullException.ThrowIfNull(summary);

        Actor = summary.Actor;
        TotalDamage = summary.TotalDamage;
        ActionCount = summary.TotalUseCount;
        _nameKind = nameKind;
        _isVisible = isVisible;
        _registerAsPc = registerAsPc;
        _registerAsNpc = registerAsNpc;
        _clearRegistration = clearRegistration;
        RegisterAsPcCommand = new RelayCommand(
            () => _registerAsPc(Actor));
        RegisterAsNpcCommand = new RelayCommand(
            () => _registerAsNpc(Actor));
        ClearRegistrationCommand = new RelayCommand(
            () => _clearRegistration(Actor));
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Actor { get; }

    public string TotalDamage { get; }

    public string ActionCount { get; }

    public RelayCommand RegisterAsPcCommand { get; }

    public RelayCommand RegisterAsNpcCommand { get; }

    public RelayCommand ClearRegistrationCommand { get; }

    public ActorNameKind NameKind
    {
        get => _nameKind;
        private set
        {
            if (_nameKind == value)
            {
                return;
            }

            _nameKind = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(ClassificationLabel));
            OnPropertyChanged(nameof(ClassificationBackground));
            OnPropertyChanged(nameof(ClassificationForeground));
        }
    }

    public string ClassificationLabel
    {
        get
        {
            return NameKind switch
            {
                ActorNameKind.RegisteredNpc => "NPC登録",
                ActorNameKind.RegisteredPc => "PC登録",
                ActorNameKind.PcCandidate => "PC候補",
                _ => "未分類"
            };
        }
    }

    public System.Windows.Media.Brush ClassificationBackground
    {
        get
        {
            return NameKind switch
            {
                ActorNameKind.RegisteredPc => MediaBrushes.RoyalBlue,
                ActorNameKind.PcCandidate => MediaBrushes.LightSkyBlue,
                ActorNameKind.RegisteredNpc => MediaBrushes.Firebrick,
                _ => MediaBrushes.LightGray
            };
        }
    }

    public System.Windows.Media.Brush ClassificationForeground
    {
        get
        {
            return NameKind switch
            {
                ActorNameKind.PcCandidate => MediaBrushes.MidnightBlue,
                ActorNameKind.Unknown => MediaBrushes.Black,
                _ => MediaBrushes.White
            };
        }
    }

    public bool IsVisible
    {
        get => _isVisible;
        set
        {
            if (_isVisible == value)
            {
                return;
            }

            _isVisible = value;
            OnPropertyChanged();
        }
    }

    public void UpdateClassification(ActorNameKind nameKind)
    {
        NameKind = nameKind;
    }

    private void OnPropertyChanged(
        [CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(
            this,
            new PropertyChangedEventArgs(propertyName));
    }
}
