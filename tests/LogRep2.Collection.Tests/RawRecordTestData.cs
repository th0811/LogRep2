using FfxiTempLogCollector.Core;

namespace FfxiTempLogCollector.Tests;

internal static class RawRecordTestData
{
    internal static RawRecord Create(
        string rawRecordId = "raw-1",
        int windowId = 1,
        string sourceFile = "1_0.log",
        string? sequenceHint = "10",
        DateTimeOffset? firstSeenAt = null,
        string visibleText = "テストメッセージ",
        string? eventGroup = "event",
        string? messageTokenCount = "token-count")
    {
        return new RawRecord
        {
            RawRecordId = rawRecordId,
            SessionId = "20260623-213000",
            FirstSeenAt = firstSeenAt ?? new DateTimeOffset(
                2026,
                6,
                23,
                21,
                30,
                0,
                TimeSpan.FromHours(9)),
            SourceFile = sourceFile,
            WindowId = windowId,
            RotationIndex = 0,
            FileMtime = new DateTimeOffset(
                2026,
                6,
                23,
                21,
                29,
                59,
                TimeSpan.FromHours(9)),
            FileSize = 1234,
            FileHash = "file-hash",
            RecordIndex = 3,
            RecordOffset = 100,
            RawRecordHash = "record-hash",
            MetaFields = ["a", "b"],
            EventGroup = eventGroup,
            SequenceHint = sequenceHint,
            MessageTokenCount = messageTokenCount,
            RawMessageHex = "82A000",
            VisibleText = visibleText,
            MessageTimeText = "21:30",
            MessageTimePrecision = "minute",
            IsMarker = false,
            ParseStatus = ParseStatus.Success,
        };
    }
}
