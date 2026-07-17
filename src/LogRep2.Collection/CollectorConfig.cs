namespace FfxiTempLogCollector.Core;

public sealed class CollectorConfig
{
    public const string DefaultMarkerPrefix = "###";

    public const string DefaultOutputDirectory = "sessions";

    public string TempDir { get; set; } = string.Empty;

    public string OutputDir { get; set; } = DefaultOutputDirectory;

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

    public string MarkerPrefix { get; set; } = DefaultMarkerPrefix;

    public string Timezone { get; set; } = "Asia/Tokyo";

    public int FlushIntervalMs { get; set; } = 1000;

    public string HashAlgorithm { get; set; } = "sha1";

    public string LogLevel { get; set; } = "info";

    public bool AutoStartCollectionOnLaunch { get; set; }

    public bool MinimizeToTrayWhileCollecting { get; set; }

    public string MinimizeButtonBehavior { get; set; } = "tray";

    public string CloseButtonBehavior { get; set; } = "tray_when_collecting";

    public bool ShowTrayNotifications { get; set; } = true;
}
