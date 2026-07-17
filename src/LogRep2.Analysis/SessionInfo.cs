using System.Text.Json.Serialization;

namespace FFXI_LogAnalyzer.Core;

public sealed class SessionInfo
{
    [JsonPropertyName("schema_version")]
    public string? SchemaVersion { get; init; }

    [JsonPropertyName("raw_schema_version")]
    public string? RawSchemaVersion { get; init; }

    [JsonPropertyName("canonical_schema_version")]
    public string? CanonicalSchemaVersion { get; init; }

    [JsonPropertyName("collector_version")]
    public string? CollectorVersion { get; init; }

    [JsonPropertyName("session_id")]
    public string? SessionId { get; init; }

    [JsonPropertyName("status")]
    public SessionStatus Status { get; init; } = SessionStatus.Unknown;

    [JsonPropertyName("started_at")]
    public DateTimeOffset? StartedAt { get; init; }

    [JsonPropertyName("ended_at")]
    public DateTimeOffset? EndedAt { get; init; }

    [JsonPropertyName("temp_dir")]
    public string? TempDir { get; init; }

    [JsonPropertyName("output_dir")]
    public string? OutputDir { get; init; }

    [JsonPropertyName("encoding")]
    public string? Encoding { get; init; }

    [JsonPropertyName("timezone")]
    public string? Timezone { get; init; }

    [JsonPropertyName("watch_files")]
    public IReadOnlyList<string> WatchFiles { get; init; } = [];

    public SchemaVersionInfo SchemaVersions => new(SchemaVersion, RawSchemaVersion, CanonicalSchemaVersion);
}
