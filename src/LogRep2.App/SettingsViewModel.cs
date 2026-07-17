using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using FfxiTempLogCollector.Core;
using MessageBox = System.Windows.MessageBox;

namespace FfxiTempLogCollector.App;

public sealed class SettingsViewModel : INotifyPropertyChanged
{
    private readonly CollectorConfig _currentConfig;
    private readonly ConfigEditService _configEditService;
    private readonly FolderPickerService _folderPickerService;
    private readonly Window _window;

    private string _tempDirectory;
    private string _outputDirectory;
    private string _pollingIntervalText;
    private bool _watchWindow1;
    private bool _watchWindow2;
    private string _markerPrefix;
    private bool _rawOutput;
    private bool _canonicalOutput;
    private bool _autoStartCollectionOnLaunch;
    private bool _minimizeToTrayWhileCollecting;
    private bool _minimizeToTray;
    private bool _showTrayNotifications;
    private string _closeButtonBehavior;
    private string _logLevel;

    public SettingsViewModel(
        Window window,
        CollectorConfig currentConfig,
        ConfigEditService configEditService,
        FolderPickerService folderPickerService)
    {
        _window = window
            ?? throw new ArgumentNullException(nameof(window));
        _currentConfig = currentConfig
            ?? throw new ArgumentNullException(nameof(currentConfig));
        _configEditService = configEditService
            ?? throw new ArgumentNullException(
                nameof(configEditService));
        _folderPickerService = folderPickerService
            ?? throw new ArgumentNullException(
                nameof(folderPickerService));

        var editable = ConfigEditService.Clone(currentConfig);
        _tempDirectory = editable.TempDir;
        _outputDirectory = editable.OutputDir;
        _pollingIntervalText =
            editable.PollingIntervalMs.ToString();
        _watchWindow1 = editable.WatchWindow1;
        _watchWindow2 = editable.WatchWindow2;
        _markerPrefix = editable.MarkerPrefix;
        _rawOutput = editable.RawOutput;
        _canonicalOutput = editable.CanonicalOutput;
        _autoStartCollectionOnLaunch =
            editable.AutoStartCollectionOnLaunch;
        _minimizeToTrayWhileCollecting =
            editable.MinimizeToTrayWhileCollecting;
        _minimizeToTray =
            editable.MinimizeButtonBehavior == "tray";
        _showTrayNotifications = editable.ShowTrayNotifications;
        _closeButtonBehavior =
            WindowCloseBehaviorController.NormalizeCloseBehavior(
                editable.CloseButtonBehavior);
        _logLevel = editable.LogLevel;

        SelectTempDirectoryCommand = new RelayCommand(
            SelectTempDirectory);
        SelectOutputDirectoryCommand = new RelayCommand(
            SelectOutputDirectory);
        SaveCommand = new RelayCommand(Save);
        CancelCommand = new RelayCommand(() => _window.Close());
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public event EventHandler? ConfigSaved;

    public IReadOnlyList<string> CloseButtonBehaviors { get; } =
    [
        "tray_when_collecting",
        "always_tray",
        "confirm_exit",
    ];

    public IReadOnlyList<string> LogLevels { get; } =
    [
        "debug",
        "info",
        "warning",
        "error",
    ];

    public string TempDirectory
    {
        get => _tempDirectory;
        set => SetProperty(ref _tempDirectory, value);
    }

    public string OutputDirectory
    {
        get => _outputDirectory;
        set => SetProperty(ref _outputDirectory, value);
    }

    public string PollingIntervalText
    {
        get => _pollingIntervalText;
        set => SetProperty(ref _pollingIntervalText, value);
    }

    public bool WatchWindow1
    {
        get => _watchWindow1;
        set => SetProperty(ref _watchWindow1, value);
    }

    public bool WatchWindow2
    {
        get => _watchWindow2;
        set => SetProperty(ref _watchWindow2, value);
    }

    public string MarkerPrefix
    {
        get => _markerPrefix;
        set => SetProperty(ref _markerPrefix, value);
    }

    public bool RawOutput
    {
        get => _rawOutput;
        set => SetProperty(ref _rawOutput, value);
    }

    public bool CanonicalOutput
    {
        get => _canonicalOutput;
        set => SetProperty(ref _canonicalOutput, value);
    }

    public bool AutoStartCollectionOnLaunch
    {
        get => _autoStartCollectionOnLaunch;
        set => SetProperty(
            ref _autoStartCollectionOnLaunch,
            value);
    }

    public bool MinimizeToTrayWhileCollecting
    {
        get => _minimizeToTrayWhileCollecting;
        set => SetProperty(
            ref _minimizeToTrayWhileCollecting,
            value);
    }

    public bool MinimizeToTray
    {
        get => _minimizeToTray;
        set => SetProperty(ref _minimizeToTray, value);
    }

    public bool ShowTrayNotifications
    {
        get => _showTrayNotifications;
        set => SetProperty(ref _showTrayNotifications, value);
    }

    public string CloseButtonBehavior
    {
        get => _closeButtonBehavior;
        set => SetProperty(ref _closeButtonBehavior, value);
    }

    public string LogLevel
    {
        get => _logLevel;
        set => SetProperty(ref _logLevel, value);
    }

    public RelayCommand SelectTempDirectoryCommand { get; }

    public RelayCommand SelectOutputDirectoryCommand { get; }

    public RelayCommand SaveCommand { get; }

    public RelayCommand CancelCommand { get; }

    private void SelectTempDirectory()
    {
        var selected = _folderPickerService.SelectFolder(
            "TEMPフォルダーを選択",
            TempDirectory);

        if (selected is not null)
        {
            TempDirectory = selected;
        }
    }

    private void SelectOutputDirectory()
    {
        var selected = _folderPickerService.SelectFolder(
            "出力先フォルダーを選択",
            OutputDirectory);

        if (selected is not null)
        {
            OutputDirectory = selected;
        }
    }

    private void Save()
    {
        if (!int.TryParse(
                PollingIntervalText,
                out var pollingInterval))
        {
            ShowMessage(
                "ポーリング間隔は整数で指定してください。",
                MessageBoxImage.Warning);
            return;
        }

        var edited = ConfigEditService.Clone(_currentConfig);
        edited.TempDir = TempDirectory.Trim();
        edited.OutputDir = OutputDirectory.Trim();
        edited.PollingIntervalMs = pollingInterval;
        edited.WatchWindow1 = WatchWindow1;
        edited.WatchWindow2 = WatchWindow2;
        edited.MarkerPrefix = (MarkerPrefix ?? string.Empty).Trim();
        edited.RawOutput = RawOutput;
        edited.CanonicalOutput = CanonicalOutput;
        edited.AutoStartCollectionOnLaunch =
            AutoStartCollectionOnLaunch;
        edited.MinimizeToTrayWhileCollecting =
            MinimizeToTrayWhileCollecting;
        edited.MinimizeButtonBehavior =
            MinimizeToTray ? "tray" : "normal";
        edited.CloseButtonBehavior = CloseButtonBehavior;
        edited.ShowTrayNotifications = ShowTrayNotifications;
        edited.LogLevel = LogLevel;

        var validation = _configEditService.Validate(edited);

        if (!validation.Success)
        {
            ShowMessage(
                validation.Message,
                MessageBoxImage.Error);
            return;
        }

        if (validation.HasWarning)
        {
            var answer = MessageBox.Show(
                _window,
                validation.Message + Environment.NewLine
                    + "このまま保存しますか？",
                "設定の警告",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (answer != MessageBoxResult.Yes)
            {
                return;
            }
        }

        var result = _configEditService.Save(
            _currentConfig,
            edited);

        if (!result.Success)
        {
            ShowMessage(result.Message, MessageBoxImage.Error);
            return;
        }

        ConfigSaved?.Invoke(this, EventArgs.Empty);

        if (!string.IsNullOrWhiteSpace(result.Message))
        {
            ShowMessage(
                result.Message,
                MessageBoxImage.Information);
        }

        _window.DialogResult = true;
        _window.Close();
    }

    private void ShowMessage(
        string message,
        MessageBoxImage image)
    {
        MessageBox.Show(
            _window,
            message,
            "設定",
            MessageBoxButton.OK,
            image);
    }

    private void SetProperty<T>(
        ref T field,
        T value,
        [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return;
        }

        field = value;
        PropertyChanged?.Invoke(
            this,
            new PropertyChangedEventArgs(propertyName));
    }
}
