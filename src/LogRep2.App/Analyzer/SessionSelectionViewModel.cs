using System.ComponentModel;
using System.Windows.Threading;
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

    public AnalysisExclusions Exclusions { get; private set; } = new();

    public string? ExclusionsLoadError { get; private set; }

    public void LoadExclusions()
    {
        try
        {
            Exclusions = new AnalysisExclusionsStore().Load(FolderPath);
            ExclusionsLoadError = null;
        }
        catch (Exception exception)
        {
            ExclusionsLoadError = exception.Message;
        }
    }

    public void SaveExclusions(AnalysisExclusions exclusions)
    {
        if (ExclusionsLoadError is not null)
        {
            throw new InvalidOperationException("除外設定を読み込めていないため保存できません。設定ファイルを確認してセッションを更新してください。");
        }

        new AnalysisExclusionsStore().Save(FolderPath, exclusions);
        Exclusions = exclusions;
    }

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

    public string Alias { get; private set; } = string.Empty;

    public string DisplayName => string.IsNullOrEmpty(Alias) ? SessionId : $"{Alias}（{SessionId}）";

    public string? AliasLoadError { get; private set; }

    public void LoadAlias()
    {
        try
        {
            Alias = new SessionAnnotationsStore().Load(FolderPath, SessionId);
            AliasLoadError = null;
        }
        catch (Exception exception)
        {
            Alias = string.Empty;
            AliasLoadError = $"エイリアスを読み込めません: {FolderPath}。{exception.Message}";
        }
        OnPropertyChanged(nameof(Alias));
        OnPropertyChanged(nameof(DisplayName));
        OnPropertyChanged(nameof(HasAlias));
        OnPropertyChanged(nameof(CanEditAlias));
    }

    public void SaveAlias(string alias)
    {
        if (AliasLoadError is not null)
            throw new InvalidOperationException("エイリアス設定を確認できないため保存できません。設定ファイルを修復してセッションを更新してください。");
        var normalized = SessionAnnotationsStore.NormalizeAlias(alias);
        new SessionAnnotationsStore().Save(FolderPath, SessionId, normalized);
        Alias = normalized;
        OnPropertyChanged(nameof(Alias));
        OnPropertyChanged(nameof(DisplayName));
        OnPropertyChanged(nameof(HasAlias));
    }

    private string _editingAlias = string.Empty;
    private string? _aliasEditError;
    private bool _isAliasJustSaved;
    private DispatcherTimer? _savedTimer;

    public bool HasAlias => !string.IsNullOrEmpty(Alias);

    public bool CanEditAlias => AliasLoadError is null;

    private bool _isAliasEditing;
    public bool IsAliasEditing
    {
        get => _isAliasEditing;
        private set
        {
            if (_isAliasEditing == value) return;
            _isAliasEditing = value;
            OnPropertyChanged();
        }
    }

    /// <summary>編集中の一時値。確定するまで Alias は変わらない。</summary>
    public string EditingAlias
    {
        get => _editingAlias;
        set
        {
            if (_editingAlias == value) return;
            _editingAlias = value;
            OnPropertyChanged();
            if (_aliasEditError is not null) AliasEditError = null; // 入力し直したらエラーを消す
        }
    }

    public string? AliasEditError
    {
        get => _aliasEditError;
        private set
        {
            if (_aliasEditError == value) return;
            _aliasEditError = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasAliasEditError));
        }
    }

    public bool HasAliasEditError => _aliasEditError is not null;

    /// <summary>保存直後 2 秒間 true。「✓ 保存しました」ピルの表示に使う。</summary>
    public bool IsAliasJustSaved
    {
        get => _isAliasJustSaved;
        private set
        {
            if (_isAliasJustSaved == value) return;
            _isAliasJustSaved = value;
            OnPropertyChanged();
        }
    }

    public void BeginAliasEdit()
    {
        if (!CanEditAlias) return;
        IsAliasEditing = true;
        _savedTimer?.Stop();
        IsAliasJustSaved = false;
        _editingAlias = Alias;
        OnPropertyChanged(nameof(EditingAlias));
        AliasEditError = null;
    }

    public void CancelAliasEdit()
    {
        IsAliasEditing = false;
        _editingAlias = Alias;
        OnPropertyChanged(nameof(EditingAlias));
        AliasEditError = null;
    }

    /// <returns>編集を終了してよければ true。保存失敗時は false（編集継続）。</returns>
    public bool TryCommitAliasEdit()
    {
        if (!CanEditAlias) return false;
        string normalized;
        try
        {
            normalized = SessionAnnotationsStore.NormalizeAlias(EditingAlias);
        }
        catch (ArgumentException exception)
        {
            AliasEditError = exception.Message;
            return false;
        }

        if (normalized == Alias)
        {
            IsAliasEditing = false;
            AliasEditError = null;
            return true; // 変更なし: 書き込まない・保存表示もしない
        }

        try
        {
            SaveAlias(normalized);
        }
        catch (Exception exception)
        {
            AliasEditError = $"エイリアスを保存できません。{exception.Message}";
            return false;
        }

        AliasEditError = null;
        IsAliasEditing = false;
        ShowSavedFeedback();
        return true;
    }

    private void ShowSavedFeedback()
    {
        IsAliasJustSaved = true;
        _savedTimer ??= new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        _savedTimer.Stop();
        _savedTimer.Tick -= OnSavedTimerTick;
        _savedTimer.Tick += OnSavedTimerTick;
        _savedTimer.Start();
    }

    private void OnSavedTimerTick(object? sender, EventArgs e)
    {
        _savedTimer?.Stop();
        IsAliasJustSaved = false;
    }


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
