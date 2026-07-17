using System.Globalization;
using LogRep2.Contracts;

namespace FfxiTempLogCollector.Core;

public sealed class CanonicalRecord : ICanonicalRecord
{
    public string SchemaVersion { get; init; } =
        SchemaVersions.CanonicalRecord;

    public string CanonicalRecordId { get; init; } = string.Empty;

    public string SessionId { get; init; } = string.Empty;

    public long Order { get; init; }

    public DateTimeOffset FirstSeenAt { get; init; }

    public DateTimeOffset LastSeenAt { get; set; }

    public List<int> SourceWindows { get; init; } = [];

    public List<string> SourceFiles { get; init; } = [];

    public List<string> SourceRawRecordIds { get; init; } = [];

    public string? EventGroup { get; init; }

    public string? SequenceHintMin { get; set; }

    public string? SequenceHintMax { get; set; }

    public string VisibleText { get; init; } = string.Empty;

    public string? MessageTimeText { get; init; }

    public string? MessageTimePrecision { get; init; }

    public bool IsMarker { get; init; }

    public string? MarkerKeyword { get; init; }

    public string CanonicalKey { get; init; } = string.Empty;

    long? ICanonicalRecord.Order => Order;

    DateTimeOffset? ICanonicalRecord.FirstSeenAt => FirstSeenAt;

    DateTimeOffset? ICanonicalRecord.LastSeenAt => LastSeenAt;

    long? ICanonicalRecord.SequenceHintMin => ParseSequence(SequenceHintMin);

    long? ICanonicalRecord.SequenceHintMax => ParseSequence(SequenceHintMax);

    private static long? ParseSequence(string? value)
    {
        return long.TryParse(
            value,
            NumberStyles.Integer,
            CultureInfo.InvariantCulture,
            out var parsed)
                ? parsed
                : null;
    }
}
