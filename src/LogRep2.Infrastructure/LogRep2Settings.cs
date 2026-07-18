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
            Encoding = Collection.Encoding,
            PollingIntervalMs = Collection.PollingIntervalMs,
            WatchWindow1 = Collection.WatchWindow1,
            WatchWindow2 = Collection.WatchWindow2,
            RotationSlots = Collection.RotationSlots,
            RawOutput = Collection.RawOutput,
            CanonicalOutput = Collection.CanonicalOutput,
            DedupeRaw = Collection.DedupeRaw,
            DedupeCanonical = Collection.DedupeCanonical,
            MarkerDetection = Collection.MarkerDetection,
            MarkerPrefix = Collection.MarkerPrefix,
            Timezone = Collection.Timezone,
            FlushIntervalMs = Collection.FlushIntervalMs,
            HashAlgorithm = Collection.HashAlgorithm,
            LogLevel = Application.LogLevel,
            AutoStartCollectionOnLaunch =
                Application.AutoStartCollectionOnLaunch,
            MinimizeToTrayWhileCollecting =
                Application.MinimizeToTrayWhileCollecting,
            MinimizeButtonBehavior =
                Application.MinimizeButtonBehavior,
            CloseButtonBehavior = Application.CloseButtonBehavior,
            ShowTrayNotifications = Application.ShowTrayNotifications,
        };
    }

    public void UpdateFromCollectorConfig(CollectorConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);

        Collection.TempDirectory = config.TempDir;
        Collection.OutputDirectory = config.OutputDir;
        Collection.Encoding = config.Encoding;
        Collection.PollingIntervalMs = config.PollingIntervalMs;
        Collection.WatchWindow1 = config.WatchWindow1;
        Collection.WatchWindow2 = config.WatchWindow2;
        Collection.RotationSlots = config.RotationSlots;
        Collection.RawOutput = config.RawOutput;
        Collection.CanonicalOutput = config.CanonicalOutput;
        Collection.DedupeRaw = config.DedupeRaw;
        Collection.DedupeCanonical = config.DedupeCanonical;
        Collection.MarkerDetection = config.MarkerDetection;
        Collection.MarkerPrefix = config.MarkerPrefix;
        Collection.Timezone = config.Timezone;
        Collection.FlushIntervalMs = config.FlushIntervalMs;
        Collection.HashAlgorithm = config.HashAlgorithm;
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
    }
}

public sealed class CollectionSettings
{
    public string TempDirectory { get; set; } = string.Empty;

    public string OutputDirectory { get; set; } =
        CollectorConfig.DefaultOutputDirectory;

    public string Encoding { get; set; } = "cp932";

    public int PollingIntervalMs { get; set; } = 1000;

    public bool WatchWindow1 { get; set; } = true;

    public bool WatchWindow2 { get; set; } = true;

    public int RotationSlots { get; set; } = 20;

    public bool RawOutput { get; set; } = true;

    public bool CanonicalOutput { get; set; } = true;

    public bool DedupeRaw { get; set; } = true;

    public bool DedupeCanonical { get; set; } = true;

    public bool MarkerDetection { get; set; } = true;

    public string MarkerPrefix { get; set; } =
        CollectorConfig.DefaultMarkerPrefix;

    public string Timezone { get; set; } = "Asia/Tokyo";

    public int FlushIntervalMs { get; set; } = 1000;

    public string HashAlgorithm { get; set; } = "sha1";
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

    public double Opacity { get; set; } = 0.8;

    public bool Topmost { get; set; } = true;

    public double Left { get; set; } = 100;

    public double Top { get; set; } = 100;

    public double Width { get; set; } = 420;

    public double Height { get; set; } = 300;

    public bool PositionLocked { get; set; }

    public string? MonitorDeviceName { get; set; }

    public double FontSize { get; set; } = 16;

    public int DisplayRowCount { get; set; } = 10;

    public List<string> DisplayItems { get; set; } =
    [
        "total_damage",
        "dps",
        "hit_rate",
        "actor_ranking",
        "analysis_state",
    ];
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
}
