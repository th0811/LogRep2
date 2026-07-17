using System.Text.Json.Serialization;

namespace FFXI_LogAnalyzer.Core;

public sealed class StatsInfo
{
    [JsonPropertyName("raw_records_written")]
    public long RawRecordsWritten { get; init; }

    [JsonPropertyName("canonical_records_written")]
    public long CanonicalRecordsWritten { get; init; }

    [JsonPropertyName("duplicate_raw_records_skipped")]
    public long DuplicateRawRecordsSkipped { get; init; }

    [JsonPropertyName("duplicate_canonical_records_skipped")]
    public long DuplicateCanonicalRecordsSkipped { get; init; }

    [JsonPropertyName("parse_errors")]
    public long ParseErrors { get; init; }

    [JsonPropertyName("decode_errors")]
    public long DecodeErrors { get; init; }

    [JsonPropertyName("gap_warnings")]
    public long GapWarnings { get; init; }

    [JsonPropertyName("last_seen_at")]
    public DateTimeOffset? LastSeenAt { get; init; }
}
