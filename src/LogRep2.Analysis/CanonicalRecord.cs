using System.Text.Json.Serialization;
using LogRep2.Contracts;

namespace FFXI_LogAnalyzer.Core;

public sealed class CanonicalRecord : ICanonicalRecord
{
    [JsonPropertyName("schema_version")]
    public string? SchemaVersion { get; init; }

    [JsonPropertyName("canonical_record_id")]
    public string? CanonicalRecordId { get; init; }

    [JsonPropertyName("session_id")]
    public string? SessionId { get; init; }

    [JsonPropertyName("order")]
    [JsonConverter(typeof(FlexibleNullableLongJsonConverter))]
    public long? Order { get; init; }

    [JsonPropertyName("first_seen_at")]
    public DateTimeOffset? FirstSeenAt { get; init; }

    [JsonPropertyName("last_seen_at")]
    public DateTimeOffset? LastSeenAt { get; init; }

    [JsonPropertyName("source_windows")]
    public IReadOnlyList<int> SourceWindows { get; init; } = [];

    [JsonPropertyName("source_files")]
    public IReadOnlyList<string> SourceFiles { get; init; } = [];

    [JsonPropertyName("source_raw_record_ids")]
    public IReadOnlyList<string> SourceRawRecordIds { get; init; } = [];

    [JsonPropertyName("event_group")]
    public string? EventGroup { get; init; }

    [JsonPropertyName("sequence_hint_min")]
    [JsonConverter(typeof(FlexibleNullableLongJsonConverter))]
    public long? SequenceHintMin { get; init; }

    [JsonPropertyName("sequence_hint_max")]
    [JsonConverter(typeof(FlexibleNullableLongJsonConverter))]
    public long? SequenceHintMax { get; init; }

    [JsonPropertyName("visible_text")]
    public string? VisibleText { get; init; }

    [JsonPropertyName("message_time_text")]
    public string? MessageTimeText { get; init; }

    [JsonPropertyName("message_time_precision")]
    public string? MessageTimePrecision { get; init; }

    [JsonPropertyName("is_marker")]
    public bool IsMarker { get; init; }

    [JsonPropertyName("marker_keyword")]
    public string? MarkerKeyword { get; init; }

    [JsonPropertyName("canonical_key")]
    public string? CanonicalKey { get; init; }
}
