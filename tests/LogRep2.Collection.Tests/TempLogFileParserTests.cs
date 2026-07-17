using System.Buffers.Binary;
using System.Text;
using FfxiTempLogCollector.Core;

namespace FfxiTempLogCollector.Tests;

public sealed class TempLogFileParserTests
{
    private readonly TempLogFileParser _parser = new();

    [Fact]
    public void ヘッダをLittleEndianのUint16として読める()
    {
        var fileBytes = CreateFile(
            (0, 100, CreateValidRecord("本文")));

        var actual = _parser.Parse(fileBytes);

        Assert.Equal(50, actual.HeaderOffsets.Count);
        Assert.Equal((ushort)100, actual.HeaderOffsets[0]);
        Assert.Equal(ParseStatus.Success, actual.ParseStatus);
    }

    [Fact]
    public void メタフィールドと本文を分離できる()
    {
        var recordBytes = CreateValidRecord(
            "メッセージ",
            eventGroup: "event-4",
            sequenceHint: "sequence-5",
            messageTokenCount: "token-6");
        var fileBytes = CreateFile((7, 120, recordBytes));

        var actual = _parser.Parse(fileBytes);

        var record = Assert.Single(actual.Records);
        Assert.Equal(7, record.RecordIndex);
        Assert.Equal(120, record.RecordOffset);
        Assert.Equal(ParseStatus.Success, record.ParseStatus);
        Assert.Equal(21, record.MetaFields.Fields.Count);
        Assert.Equal("event-4", record.MetaFields.EventGroup);
        Assert.Equal("sequence-5", record.MetaFields.SequenceHint);
        Assert.Equal("token-6", record.MetaFields.MessageTokenCount);
        Assert.Equal(
            AddNullTerminator(Encoding.UTF8.GetBytes("メッセージ")),
            record.RawMessageBytes);
    }

    [Fact]
    public void Nul終端でレコードを切り出せる()
    {
        var firstRecord = CreateValidRecord("最初");
        var secondRecord = CreateValidRecord("次");
        var secondOffset = 100 + firstRecord.Length;
        var fileBytes = CreateFile(
            (0, 100, firstRecord),
            (1, secondOffset, secondRecord));

        var actual = _parser.Parse(fileBytes);

        Assert.Equal(2, actual.Records.Count);
        Assert.Equal(
            AddNullTerminator(Encoding.UTF8.GetBytes("最初")),
            actual.Records[0].RawMessageBytes);
        Assert.Equal(
            AddNullTerminator(Encoding.UTF8.GetBytes("次")),
            actual.Records[1].RawMessageBytes);
        Assert.Equal((byte)0, actual.Records[0].RawRecordBytes[^1]);
    }

    [Fact]
    public void メタフィールド不足ならエラーにして元バイト列を保持する()
    {
        var invalidRecord = Encoding.ASCII.GetBytes("a,b,c,message\0");
        var fileBytes = CreateFile((0, 100, invalidRecord));

        var actual = _parser.Parse(fileBytes);

        var record = Assert.Single(actual.Records);
        Assert.Equal(ParseStatus.Error, record.ParseStatus);
        Assert.Contains(
            "メタフィールドが21個未満",
            record.ParseError,
            StringComparison.Ordinal);
        Assert.Equal(invalidRecord, record.RawRecordBytes);
        Assert.Equal(3, record.MetaFields.Fields.Count);
    }

    [Fact]
    public void Nul終端がなくても可能な範囲を保持してエラーにする()
    {
        var recordWithoutNull = CreateValidRecord("本文")[..^1];
        var fileBytes = CreateFile((0, 100, recordWithoutNull));

        var actual = _parser.Parse(fileBytes);

        var record = Assert.Single(actual.Records);
        Assert.Equal(ParseStatus.Error, record.ParseStatus);
        Assert.Contains(
            "NUL終端が見つかりません",
            record.ParseError,
            StringComparison.Ordinal);
        Assert.Equal(recordWithoutNull, record.RawRecordBytes);
        Assert.Equal(
            Encoding.UTF8.GetBytes("本文"),
            record.RawMessageBytes);
    }

    [Fact]
    public void 不正オフセットと重複オフセットを無視する()
    {
        var validRecord = CreateValidRecord("本文");
        var fileBytes = CreateFile(
            (0, 99, []),
            (1, 100, validRecord),
            (2, 100, validRecord));
        BinaryPrimitives.WriteUInt16LittleEndian(
            fileBytes.AsSpan(6, sizeof(ushort)),
            (ushort)fileBytes.Length);

        var actual = _parser.Parse(fileBytes);

        var record = Assert.Single(actual.Records);
        Assert.Equal(1, record.RecordIndex);
        Assert.Equal(100, record.RecordOffset);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(50)]
    [InlineData(99)]
    public void ヘッダ未満のファイルでも例外終了しない(int fileLength)
    {
        var exception = Record.Exception(
            () => _parser.Parse(new byte[fileLength]));

        Assert.Null(exception);

        var actual = _parser.Parse(new byte[fileLength]);
        Assert.Equal(ParseStatus.Error, actual.ParseStatus);
        Assert.Empty(actual.Records);
        Assert.Contains(
            "ヘッダ長100バイト未満",
            actual.ParseError,
            StringComparison.Ordinal);
    }

    [Fact]
    public void HeaderOffsetがレコード途中を指してもレコード全体を復元できる()
    {
        var recordBytes = CreateValidRecord(
            "実ログ",
            eventGroup: "event-4",
            sequenceHint: "sequence-5",
            messageTokenCount: "token-6");
        var headerOffset = 100 + 120;
        var fileBytes = CreateFileWithRecordStart(
            (0, headerOffset, 100, recordBytes));

        var actual = _parser.Parse(fileBytes);

        var record = Assert.Single(actual.Records);
        Assert.Equal(0, record.RecordIndex);
        Assert.Equal(headerOffset, record.RecordOffset);
        Assert.Equal(ParseStatus.Success, record.ParseStatus);
        Assert.Equal(21, record.MetaFields.Fields.Count);
        Assert.Equal("event-4", record.MetaFields.EventGroup);
        Assert.Equal("sequence-5", record.MetaFields.SequenceHint);
        Assert.Equal("token-6", record.MetaFields.MessageTokenCount);
        Assert.Equal(recordBytes, record.RawRecordBytes);
        Assert.Equal(
            AddNullTerminator(Encoding.UTF8.GetBytes("実ログ")),
            record.RawMessageBytes);
    }

    private static byte[] CreateValidRecord(
        string message,
        string eventGroup = "4",
        string sequenceHint = "5",
        string messageTokenCount = "6")
    {
        var fields = Enumerable.Range(0, 21)
            .Select(index => $"field-{index}")
            .ToArray();
        fields[4] = eventGroup;
        fields[5] = sequenceHint;
        fields[6] = messageTokenCount;

        var metaBytes = Encoding.ASCII.GetBytes(
            $"{string.Join(',', fields)},");
        var messageBytes = Encoding.UTF8.GetBytes(message);
        var recordBytes = new byte[
            metaBytes.Length
            + messageBytes.Length
            + 1];

        metaBytes.CopyTo(recordBytes, 0);
        messageBytes.CopyTo(recordBytes, metaBytes.Length);

        return recordBytes;
    }

    private static byte[] AddNullTerminator(byte[] bytes)
    {
        return [.. bytes, 0];
    }

    private static byte[] CreateFile(
        params (int HeaderIndex, int Offset, byte[] RecordBytes)[] records)
    {
        var fileLength = Math.Max(
            TempLogFileParser.HeaderLength,
            records.Select(
                    record => record.Offset + record.RecordBytes.Length)
                .DefaultIfEmpty(TempLogFileParser.HeaderLength)
                .Max());
        var fileBytes = new byte[fileLength];

        foreach (var record in records)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(
                fileBytes.AsSpan(
                    record.HeaderIndex * sizeof(ushort),
                    sizeof(ushort)),
                checked((ushort)record.Offset));

            if (record.RecordBytes.Length > 0)
            {
                record.RecordBytes.CopyTo(fileBytes, record.Offset);
            }
        }

        return fileBytes;
    }

    private static byte[] CreateFileWithRecordStart(
        params (int HeaderIndex, int HeaderOffset, int RecordStart, byte[] RecordBytes)[] records)
    {
        var fileLength = Math.Max(
            TempLogFileParser.HeaderLength,
            records.Select(
                    record => record.RecordStart + record.RecordBytes.Length)
                .DefaultIfEmpty(TempLogFileParser.HeaderLength)
                .Max());
        var fileBytes = new byte[fileLength];

        foreach (var record in records)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(
                fileBytes.AsSpan(
                    record.HeaderIndex * sizeof(ushort),
                    sizeof(ushort)),
                checked((ushort)record.HeaderOffset));

            if (record.RecordBytes.Length > 0)
            {
                record.RecordBytes.CopyTo(fileBytes, record.RecordStart);
            }
        }

        return fileBytes;
    }
}
