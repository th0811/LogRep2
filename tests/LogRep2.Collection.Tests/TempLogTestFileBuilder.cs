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
}
