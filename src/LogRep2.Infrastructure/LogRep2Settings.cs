using FfxiTempLogCollector.Core;

namespace LogRep2.Infrastructure;

public sealed class LogRep2Settings
{
    public int SchemaVersion { get; set; } = 1;

    public CollectionSettings Collection { get; set; } = new();

    public AnalysisSettings Analysis { get; set; } = new();

    public OverlaySettings Overlay { get; set; } = new();

    public ApplicationSettings Application { get; set; } = new();

    public CollectorConfig CreateCollectorConfig(string baseDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(baseDirectory);

        return new CollectorConfig
        {
            TempDir = ConfigLoader.ExpandPath(Collection.TempDirectory),
            OutputDir = ConfigLoader.ResolveOutputDirectory(
                Collection.OutputDirectory,
                baseDirectory),
            PollingIntervalMs = Collection.PollingIntervalMs,
            WatchWindow1 = Collection.WatchWindow1,
            WatchWindow2 = Collection.WatchWindow2,
            RotationSlots = Collection.RotationSlots,
            RawOutput = Collection.RawOutput,
            CanonicalOutput = Collection.CanonicalOutput,
            DedupeRaw = Collection.DedupeRaw,
            MarkerDetection = Collection.MarkerDetection,
            MarkerPrefix = Collection.MarkerPrefix,
            LogLevel = Application.LogLevel,
            AutoStartCollectionOnLaunch =
                Application.AutoStartCollectionOnLaunch,
            MinimizeToTrayWhileCollecting =
                Application.MinimizeToTrayWhileCollecting,
            MinimizeButtonBehavior =
                Application.MinimizeButtonBehavior,
            CloseButtonBehavior = Application.CloseButtonBehavior,
            ShowTrayNotifications = Application.ShowTrayNotifications,
            CheckForUpdatesOnLaunch =
                Application.CheckForUpdatesOnLaunch,
        };
    }

    public void UpdateFromCollectorConfig(CollectorConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);

        Collection.TempDirectory = config.TempDir;
        Collection.OutputDirectory = config.OutputDir;
        Collection.PollingIntervalMs = config.PollingIntervalMs;
        Collection.WatchWindow1 = config.WatchWindow1;
        Collection.WatchWindow2 = config.WatchWindow2;
        Collection.RotationSlots = config.RotationSlots;
        Collection.RawOutput = config.RawOutput;
        Collection.CanonicalOutput = config.CanonicalOutput;
        Collection.DedupeRaw = config.DedupeRaw;
        Collection.MarkerDetection = config.MarkerDetection;
        Collection.MarkerPrefix = config.MarkerPrefix;
        Application.LogLevel = config.LogLevel;
        Application.AutoStartCollectionOnLaunch =
            config.AutoStartCollectionOnLaunch;
        Application.MinimizeToTrayWhileCollecting =
            config.MinimizeToTrayWhileCollecting;
        Application.MinimizeButtonBehavior =
            config.MinimizeButtonBehavior;
        Application.CloseButtonBehavior = config.CloseButtonBehavior;
        Application.ShowTrayNotifications =
            config.ShowTrayNotifications;
        Application.CheckForUpdatesOnLaunch =
            config.CheckForUpdatesOnLaunch;
    }
}

public sealed class CollectionSettings
{
    public string TempDirectory { get; set; } = string.Empty;

    public string OutputDirectory { get; set; } =
        CollectorConfig.DefaultOutputDirectory;

    public int PollingIntervalMs { get; set; } = 1000;

    public bool WatchWindow1 { get; set; } = true;

    public bool WatchWindow2 { get; set; } = true;

    public int RotationSlots { get; set; } = 20;

    public bool RawOutput { get; set; } = true;

    public bool CanonicalOutput { get; set; } = true;

    public bool DedupeRaw { get; set; } = true;

    public bool MarkerDetection { get; set; } = true;

    public string MarkerPrefix { get; set; } =
        CollectorConfig.DefaultMarkerPrefix;

}

public sealed class AnalysisSettings
{
    public List<string> KnownPcNames { get; set; } = [];

    public List<string> KnownNpcNames { get; set; } = [];

    public int RealtimeRefreshIntervalMs { get; set; } = 500;

    public List<string> RealtimePartyMembers { get; set; } = [];
}

public sealed class OverlaySettings
{
    public bool Enabled { get; set; }

    public bool ShowOnRealtimeAnalysisStart { get; set; } = true;

    public double Opacity { get; set; } = 0.8;

    public bool Topmost { get; set; } = true;

    public double Left { get; set; } = 100;

    public double Top { get; set; } = 100;

    public double Width { get; set; } = 420;

    public double Height { get; set; } = 300;

    public string? MonitorDeviceName { get; set; }

    public double FontSize { get; set; } = 16;

}

public sealed class ApplicationSettings
{
    public string LogLevel { get; set; } = "info";

    public bool AutoStartCollectionOnLaunch { get; set; }

    public bool MinimizeToTrayWhileCollecting { get; set; }

    public string MinimizeButtonBehavior { get; set; } = "tray";

    public string CloseButtonBehavior { get; set; } =
        "tray_when_collecting";

    public bool ShowTrayNotifications { get; set; } = true;

    public bool CheckForUpdatesOnLaunch { get; set; } = true;
}
