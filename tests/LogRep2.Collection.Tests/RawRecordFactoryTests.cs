using FfxiTempLogCollector.Core;

namespace FfxiTempLogCollector.Tests;

public sealed class RawRecordFactoryTests
{
    [Fact]
    public void パース結果からRawRecordを生成できる()
    {
        byte[] rawRecordBytes = [0x01, 0x02, 0x00];
        var parsedRecord = new TempLogRawRecord
        {
            RecordIndex = 4,
            RecordOffset = 120,
            RawRecordBytes = rawRecordBytes,
            RawMessageBytes = [0x82, 0xA0, 0x00],
            MetaFields = new TempLogMetaFields
            {
                Fields = ["0", "1", "2", "3", "event", "12", "template"],
                ColorCode = "3",
                EventGroup = "event",
                SequenceHint = "12",
                MessageTokenCount = "template",
                MessageUnixTimeHint = "65A61380",
            },
            ParseStatus = ParseStatus.Success,
        };
        var context = new RawRecordContext
        {
            SessionId = "20260623-213000",
            FirstSeenAt = new DateTimeOffset(
                2026,
                6,
                23,
                21,
                30,
                0,
                TimeSpan.FromHours(9)),
            SourceFile = "1_0.log",
            WindowId = 1,
            RotationIndex = 0,
            FileMtime = new DateTimeOffset(
                2026,
                6,
                23,
                21,
                29,
                59,
                TimeSpan.FromHours(9)),
            FileSize = 456,
            FileHash = "file-hash",
        };
        var decoded = new DecodedLogMessage
        {
            RawMessageHex = "82A000",
            VisibleText = "[21:30] #test",
        };
        var timestamp = new ExtractedTimestamp
        {
            TimeText = "21:30",
            Precision = "minute",
            Time = new TimeOnly(21, 30),
        };
        var marker = new DetectedMarker
        {
            Keyword = "test",
        };

        var actual = new RawRecordFactory().Create(
            context,
            parsedRecord,
            decoded,
            timestamp,
            marker);

        Assert.Equal(SchemaVersions.RawRecord, actual.SchemaVersion);
        Assert.Equal("20260623-213000", actual.SessionId);
        Assert.Equal("1_0.log", actual.SourceFile);
        Assert.Equal(4, actual.RecordIndex);
        Assert.Equal(120, actual.RecordOffset);
        Assert.Equal("event", actual.EventGroup);
        Assert.Equal("12", actual.SequenceHint);
        Assert.Equal("template", actual.MessageTokenCount);
        Assert.NotNull(actual.Display);
        Assert.Equal("3", actual.Display.ColorCode);
        Assert.Equal("82A000", actual.RawMessageHex);
        Assert.Equal("[21:30] #test", actual.VisibleText);
        Assert.Equal("21:30", actual.MessageTimeText);
        Assert.Equal("minute", actual.MessageTimePrecision);
        Assert.Equal(0x65A61380, actual.MessageUnixTimeHint);
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(0x65A61380), actual.MessageTimeAt);
        Assert.True(actual.IsMarker);
        Assert.Equal("test", actual.MarkerKeyword);
        Assert.Equal(40, actual.RawRecordId.Length);
        Assert.Equal(HashUtil.ComputeSha1(rawRecordBytes), actual.RawRecordHash);
    }

    [Fact]
    public void 元レコードバイト列が変わればRawRecordIdも変わる()
    {
        var factory = new RawRecordFactory();
        var context = CreateContext();
        var first = factory.Create(
            context,
            CreateParsedRecord([0x01]),
            new DecodedLogMessage());
        var second = factory.Create(
            context,
            CreateParsedRecord([0x02]),
            new DecodedLogMessage());

        Assert.NotEqual(first.RawRecordId, second.RawRecordId);
    }

    private static RawRecordContext CreateContext()
    {
        return new RawRecordContext
        {
            SessionId = "session",
            SourceFile = "1_0.log",
        };
    }

    private static TempLogRawRecord CreateParsedRecord(byte[] bytes)
    {
        return new TempLogRawRecord
        {
            RecordIndex = 0,
            RawRecordBytes = bytes,
            MetaFields = new TempLogMetaFields(),
        };
    }
}
