using System.Buffers.Binary;
using System.Text;

namespace FfxiTempLogCollector.Core;

public sealed class TempLogFileParser
{
    public const int HeaderLength = 100;

    public const int HeaderOffsetCount = 50;

    public const int MetaFieldCount = 21;

    public const int MinimumMetaFieldCount = MetaFieldCount;

    public TempLogParsedFile Parse(byte[] fileBytes)
    {
        ArgumentNullException.ThrowIfNull(fileBytes);

        if (fileBytes.Length < HeaderLength)
        {
            return new TempLogParsedFile
            {
                ParseStatus = ParseStatus.Error,
                ParseError = $"ファイルサイズがヘッダ長{HeaderLength}バイト未満です。",
            };
        }

        var headerOffsets = ReadHeaderOffsets(fileBytes);
        var records = new List<TempLogRawRecord>();
        var processedRecordStarts = new HashSet<int>();

        for (var recordIndex = 0; recordIndex < headerOffsets.Count; recordIndex++)
        {
            var recordOffset = headerOffsets[recordIndex];

            if (!IsValidRecordOffset(recordOffset, fileBytes.Length))
            {
                continue;
            }

            var recordStart = FindRecordStart(fileBytes, recordOffset);
            if (!IsValidRecordOffset(recordStart, fileBytes.Length)
                || !processedRecordStarts.Add(recordStart))
            {
                continue;
            }

            records.Add(
                ParseRecord(
                    fileBytes,
                    recordIndex,
                    recordOffset,
                    recordStart));
        }

        return new TempLogParsedFile
        {
            HeaderOffsets = headerOffsets,
            Records = records,
            ParseStatus = ParseStatus.Success,
        };
    }

    private static IReadOnlyList<ushort> ReadHeaderOffsets(byte[] fileBytes)
    {
        var offsets = new ushort[HeaderOffsetCount];

        for (var index = 0; index < HeaderOffsetCount; index++)
        {
            var headerPosition = index * sizeof(ushort);
            offsets[index] = BinaryPrimitives.ReadUInt16LittleEndian(
                fileBytes.AsSpan(headerPosition, sizeof(ushort)));
        }

        return offsets;
    }

    private static bool IsValidRecordOffset(int offset, int fileLength)
    {
        return offset >= HeaderLength && offset < fileLength;
    }

    private static int FindRecordStart(byte[] fileBytes, ushort recordOffset)
    {
        for (var index = recordOffset - 1; index >= HeaderLength; index--)
        {
            if (fileBytes[index] == 0)
            {
                return index + 1;
            }
        }

        return HeaderLength;
    }

    private static TempLogRawRecord ParseRecord(
        byte[] fileBytes,
        int recordIndex,
        ushort recordOffset,
        int recordStart)
    {
        var remainingBytes = fileBytes.AsSpan(recordStart);
        var nullIndex = remainingBytes.IndexOf((byte)0);
        var hasNullTerminator = nullIndex >= 0;
        var recordLength = hasNullTerminator
            ? nullIndex + 1
            : remainingBytes.Length;
        var rawRecordBytes = remainingBytes[..recordLength].ToArray();

        if (!TrySplitRecord(
                rawRecordBytes,
                out var metaFields,
                out var rawMessageBytes))
        {
            return new TempLogRawRecord
            {
                RecordIndex = recordIndex,
                RecordOffset = recordOffset,
                RawRecordBytes = rawRecordBytes,
                MetaFields = metaFields,
                RawMessageBytes = rawMessageBytes,
                ParseStatus = ParseStatus.Error,
                ParseError =
                    $"メタフィールドが{MinimumMetaFieldCount}個未満です。",
            };
        }

        if (!hasNullTerminator)
        {
            return new TempLogRawRecord
            {
                RecordIndex = recordIndex,
                RecordOffset = recordOffset,
                RawRecordBytes = rawRecordBytes,
                MetaFields = metaFields,
                RawMessageBytes = rawMessageBytes,
                ParseStatus = ParseStatus.Error,
                ParseError = "レコードのNUL終端が見つかりません。",
            };
        }

        return new TempLogRawRecord
        {
            RecordIndex = recordIndex,
            RecordOffset = recordOffset,
            RawRecordBytes = rawRecordBytes,
            MetaFields = metaFields,
            RawMessageBytes = rawMessageBytes,
            ParseStatus = ParseStatus.Success,
        };
    }

    private static bool TrySplitRecord(
        byte[] rawRecordBytes,
        out TempLogMetaFields metaFields,
        out byte[] rawMessageBytes)
    {
        var fields = new List<string>(MetaFieldCount);
        var fieldStart = 0;

        for (var index = 0; index < rawRecordBytes.Length; index++)
        {
            if (rawRecordBytes[index] != (byte)',')
            {
                continue;
            }

            fields.Add(
                Encoding.ASCII.GetString(
                    rawRecordBytes,
                    fieldStart,
                    index - fieldStart));
            fieldStart = index + 1;

            if (fields.Count == MetaFieldCount)
            {
                metaFields = CreateMetaFields(fields);
                rawMessageBytes = rawRecordBytes[fieldStart..];
                return true;
            }
        }

        metaFields = CreateMetaFields(fields);
        rawMessageBytes = [];
        return false;
    }

    private static TempLogMetaFields CreateMetaFields(
        IReadOnlyList<string> fields)
    {
        return new TempLogMetaFields
        {
            Fields = fields,
            ColorCode = GetFieldOrNull(fields, 3),
            EventGroup = GetFieldOrNull(fields, 4),
            SequenceHint = GetFieldOrNull(fields, 5),
            MessageTokenCount = GetFieldOrNull(fields, 6),
            MessageUnixTimeHint = GetFieldOrNull(fields, 19),
        };
    }

    private static string? GetFieldOrNull(
        IReadOnlyList<string> fields,
        int index)
    {
        return index < fields.Count
            ? fields[index]
            : null;
    }
}
