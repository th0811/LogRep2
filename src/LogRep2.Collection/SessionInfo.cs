namespace FfxiTempLogCollector.Core;

public sealed class SessionInfo
{
    public string SchemaVersion { get; set; } = SchemaVersions.Session;

    public string RawSchemaVersion { get; set; } = SchemaVersions.RawRecord;

    public string CanonicalSchemaVersion { get; set; } = SchemaVersions.CanonicalRecord;

    public string CollectorVersion { get; set; } = "1.0.0";

    public string SessionId { get; set; } = string.Empty;

    public SessionStatus Status { get; set; } = SessionStatus.Unknown;

    public DateTimeOffset StartedAt { get; set; }

    public DateTimeOffset? EndedAt { get; set; }

    public string TempDir { get; set; } = string.Empty;

    public string OutputDir { get; set; } = string.Empty;

    public string Encoding { get; set; } = "cp932";

    public string Timezone { get; set; } = "Asia/Tokyo";

    public List<string> WatchFiles { get; set; } = [];
}
