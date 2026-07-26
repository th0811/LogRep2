using System.Buffers.Binary;
using System.Text;
using FfxiTempLogCollector.Core;

namespace FfxiTempLogCollector.Tests;

internal static class TempLogTestFileBuilder
{
    internal static byte[] Create(
        string message,
        string eventGroup = "event",
        string sequenceHint = "10",
        string messageTokenCount = "token-count")
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        var encoding = Encoding.GetEncoding(932);
        var fields = Enumerable.Range(0, 21)
            .Select(index => $"field-{index}")
            .ToArray();
        fields[4] = eventGroup;
        fields[5] = sequenceHint;
        fields[6] = messageTokenCount;

        var metaBytes = Encoding.ASCII.GetBytes(
            $"{string.Join(',', fields)},");
        var messageBytes = encoding.GetBytes(message);
        var fileBytes = new byte[
            TempLogFileParser.HeaderLength
            + metaBytes.Length
            + messageBytes.Length
            + 1];

        BinaryPrimitives.WriteUInt16LittleEndian(
            fileBytes.AsSpan(0, sizeof(ushort)),
            TempLogFileParser.HeaderLength);
        metaBytes.CopyTo(fileBytes, TempLogFileParser.HeaderLength);
        messageBytes.CopyTo(
            fileBytes,
            TempLogFileParser.HeaderLength + metaBytes.Length);

        return fileBytes;
    }

    internal static byte[] CreateMany(params string[] messages)
    {
        ArgumentNullException.ThrowIfNull(messages);

        if (messages.Length is < 1 or > TempLogFileParser.HeaderOffsetCount)
        {
            throw new ArgumentOutOfRangeException(nameof(messages));
        }

        var records = messages
            .Select(
                (message, index) => Create(
                    message,
                    sequenceHint: (10 + index).ToString()))
            .Select(bytes => bytes[TempLogFileParser.HeaderLength..])
            .ToArray();
        var fileBytes = new byte[
            TempLogFileParser.HeaderLength
            + records.Sum(record => record.Length)];
        var offset = TempLogFileParser.HeaderLength;

        for (var index = 0; index < records.Length; index++)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(
                fileBytes.AsSpan(index * sizeof(ushort), sizeof(ushort)),
                checked((ushort)offset));
            records[index].CopyTo(fileBytes, offset);
            offset += records[index].Length;
        }

        return fileBytes;
    }
}
