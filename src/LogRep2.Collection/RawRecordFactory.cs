using System.Globalization;

namespace FfxiTempLogCollector.Core;

public sealed class RawRecordFactory
{
    public RawRecord Create(
        RawRecordContext context,
        TempLogRawRecord parsedRecord,
        DecodedLogMessage decodedMessage,
        ExtractedTimestamp? timestamp = null,
        DetectedMarker? marker = null)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(parsedRecord);
        ArgumentNullException.ThrowIfNull(decodedMessage);

        ArgumentException.ThrowIfNullOrWhiteSpace(context.SessionId);
        ArgumentException.ThrowIfNullOrWhiteSpace(context.SourceFile);

        var rawRecordIdPrefix = string.Concat(
            context.SourceFile,
            parsedRecord.RecordIndex.ToString(CultureInfo.InvariantCulture));

        var messageUnixTimeHint = ParseMetaNumber(
            parsedRecord.MetaFields.MessageUnixTimeHint);

        return new RawRecord
        {
            RawRecordId = HashUtil.ComputeSha1(
                rawRecordIdPrefix,
                parsedRecord.RawRecordBytes),
            SessionId = context.SessionId,
            FirstSeenAt = context.FirstSeenAt,
            SourceFile = context.SourceFile,
            WindowId = context.WindowId,
            RotationIndex = context.RotationIndex,
            FileMtime = context.FileMtime,
            FileSize = context.FileSize,
            FileHash = context.FileHash,
            RecordIndex = parsedRecord.RecordIndex,
            RecordOffset = parsedRecord.RecordOffset,
            RawRecordHash = HashUtil.ComputeSha1(parsedRecord.RawRecordBytes),
            MetaFields = [.. parsedRecord.MetaFields.Fields],
            EventGroup = parsedRecord.MetaFields.EventGroup,
            SequenceHint = parsedRecord.MetaFields.SequenceHint,
            MessageTokenCount = parsedRecord.MetaFields.MessageTokenCount,
            Display = CreateDisplay(parsedRecord.MetaFields.ColorCode),
            RawMessageHex = decodedMessage.RawMessageHex,
            VisibleText = decodedMessage.VisibleText,
            MessageTimeText = timestamp?.TimeText,
            MessageTimePrecision = timestamp?.Precision,
            MessageUnixTimeHint = messageUnixTimeHint,
            MessageTimeAt = messageUnixTimeHint is > 0
                ? DateTimeOffset.FromUnixTimeSeconds(messageUnixTimeHint.Value)
                : null,
            IsMarker = marker?.IsMarker ?? false,
            MarkerKeyword = marker?.Keyword,
            ParseStatus = parsedRecord.ParseStatus,
            ParseError = parsedRecord.ParseError,
        };
    }

    private static RawRecordDisplay? CreateDisplay(string? colorCode)
    {
        return string.IsNullOrWhiteSpace(colorCode)
            ? null
            : new RawRecordDisplay { ColorCode = colorCode };
    }

    private static long? ParseMetaNumber(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var hasHexLetter = value.Any(character =>
            (character >= 'a' && character <= 'f') ||
            (character >= 'A' && character <= 'F'));
        var numberStyle = hasHexLetter || value.Length <= 8
            ? NumberStyles.HexNumber
            : NumberStyles.Integer;

        return long.TryParse(value, numberStyle, CultureInfo.InvariantCulture, out var number)
            && number != 0
            ? number
            : null;
    }
}
