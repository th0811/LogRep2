using System.ComponentModel;
using System.Runtime.CompilerServices;
using FFXI_LogAnalyzer.Core;

namespace FFXI_LogAnalyzer.App;

public sealed class SessionSelectionViewModel : INotifyPropertyChanged
{
    private bool _isEnabled = true;

    public SessionSelectionViewModel(
        AnalyzerInputSession session,
        IReadOnlyList<CanonicalRecord> records,
        IReadOnlyList<string> warnings)
    {
        Session = session;
        Records = records;
        Warnings = warnings;
        FolderPath = session.FolderPath;
        SessionId = string.IsNullOrWhiteSpace(session.SessionInfo.SessionId)
            ? "-"
            : session.SessionInfo.SessionId;
        StartedAt = AnalysisDisplayText.ToDateTimeText(session.SessionInfo.StartedAt);
        EndedAt = AnalysisDisplayText.ToDateTimeText(session.SessionInfo.EndedAt);
        RecordCount = records.Count.ToString("N0");
        MarkerCount = records.Count(record => record.IsMarker).ToString("N0");
        Status = AnalysisDisplayText.ToText(session.SessionInfo.Status);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public AnalyzerInputSession Session { get; }

    public IReadOnlyList<CanonicalRecord> Records { get; }

    public IReadOnlyList<string> Warnings { get; }

    public bool IsEnabled
    {
        get => _isEnabled;
        set
        {
            if (_isEnabled == value)
            {
                return;
            }

            _isEnabled = value;
            OnPropertyChanged();
        }
    }

    public string SessionId { get; }

    public string StartedAt { get; }

    public string EndedAt { get; }

    public string RecordCount { get; }

    public string MarkerCount { get; }

    public string Status { get; }

    public string FolderPath { get; }

    private void OnPropertyChanged(
        [CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(
            this,
            new PropertyChangedEventArgs(propertyName));
    }
}
