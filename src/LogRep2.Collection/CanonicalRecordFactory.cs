namespace FfxiTempLogCollector.Core;

public sealed class CanonicalRecordFactory
{
    public CanonicalRecord Create(RawRecord rawRecord, long order)
    {
        ArgumentNullException.ThrowIfNull(rawRecord);

        if (order < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(order),
                order,
                "orderは1以上で指定してください。");
        }

        var canonicalKey = CreateCanonicalKey(rawRecord);

        return new CanonicalRecord
        {
            CanonicalRecordId = canonicalKey,
            SessionId = rawRecord.SessionId,
            Order = order,
            FirstSeenAt = rawRecord.FirstSeenAt,
            LastSeenAt = rawRecord.FirstSeenAt,
            SourceWindows = [rawRecord.WindowId],
            SourceFiles = [rawRecord.SourceFile],
            SourceRawRecordIds = [rawRecord.RawRecordId],
            EventGroup = rawRecord.EventGroup,
            SequenceHintMin = rawRecord.SequenceHint,
            SequenceHintMax = rawRecord.SequenceHint,
            VisibleText = rawRecord.VisibleText,
            MessageTimeText = rawRecord.MessageTimeText,
            MessageTimePrecision = rawRecord.MessageTimePrecision,
            IsMarker = rawRecord.IsMarker,
            MarkerKeyword = rawRecord.MarkerKeyword,
            CanonicalKey = canonicalKey,
        };
    }

    public string CreateCanonicalKey(RawRecord rawRecord)
    {
        ArgumentNullException.ThrowIfNull(rawRecord);

        return HashUtil.ComputeSha1(
            string.Concat(
                rawRecord.EventGroup,
                rawRecord.VisibleText));
    }
}
