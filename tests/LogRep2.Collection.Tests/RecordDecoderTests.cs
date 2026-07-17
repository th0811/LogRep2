using System.Buffers.Binary;
using System.Text;
using FfxiTempLogCollector.Core;

namespace FfxiTempLogCollector.Tests;

public sealed class RecordDecoderTests
{
    private readonly RecordDecoder _decoder = new();

    [Fact]
    public void Cp932の日本語を復元できる()
    {
        var rawBytes = GetCp932Encoding().GetBytes("日本語ログ");

        var actual = _decoder.Decode(rawBytes);

        Assert.Equal("日本語ログ", actual.DecodedText);
        Assert.Equal("日本語ログ", actual.VisibleText);
    }

    [Fact]
    public void RawMessageHexに元バイト列を保持する()
    {
        byte[] rawBytes = [0x82, 0xA0, 0x00, 0x1E, 0x01];

        var actual = _decoder.Decode(rawBytes);

        Assert.Equal("82A0001E01", actual.RawMessageHex);
    }

    [Fact]
    public void Nulと制御コードをVisibleTextから除去する()
    {
        var encoding = GetCp932Encoding();
        var first = encoding.GetBytes("攻撃");
        var second = encoding.GetBytes("成功");
        var rawBytes = new byte[
            first.Length
            + 2
            + second.Length
            + 1];
        first.CopyTo(rawBytes, 0);
        rawBytes[first.Length] = 0x1F;
        rawBytes[first.Length + 1] = 0x02;
        second.CopyTo(rawBytes, first.Length + 2);

        var actual = _decoder.Decode(rawBytes);

        Assert.Equal("攻撃成功", actual.VisibleText);
        Assert.EndsWith("00", actual.RawMessageHex, StringComparison.Ordinal);
    }

    [Fact]
    public void 不正バイト列でも例外終了せず置換文字を使用する()
    {
        byte[] rawBytes = [0x82];

        var exception = Record.Exception(() => _decoder.Decode(rawBytes));
        var actual = _decoder.Decode(rawBytes);

        Assert.Null(exception);
        Assert.Contains('\uFFFD', actual.DecodedText);
        Assert.Equal("82", actual.RawMessageHex);
    }

    [Fact]
    public void TempLogFileParserの抽出結果をデコードできる()
    {
        var encoding = GetCp932Encoding();
        var messageBytes = encoding.GetBytes("連携テスト");
        var fields = Enumerable.Range(0, 21)
            .Select(index => $"field-{index}")
            .ToArray();
        var metaBytes = Encoding.ASCII.GetBytes(
            $"{string.Join(',', fields)},");
        var recordBytes = new byte[
            metaBytes.Length
            + messageBytes.Length
            + 1];
        metaBytes.CopyTo(recordBytes, 0);
        messageBytes.CopyTo(recordBytes, metaBytes.Length);

        var fileBytes = new byte[
            TempLogFileParser.HeaderLength
            + recordBytes.Length];
        BinaryPrimitives.WriteUInt16LittleEndian(
            fileBytes.AsSpan(0, sizeof(ushort)),
            TempLogFileParser.HeaderLength);
        recordBytes.CopyTo(
            fileBytes,
            TempLogFileParser.HeaderLength);

        var parsedFile = new TempLogFileParser().Parse(fileBytes);
        var parsedRecord = Assert.Single(parsedFile.Records);
        var actual = _decoder.Decode(parsedRecord);

        Assert.Equal("連携テスト", actual.VisibleText);
    }

    private static Encoding GetCp932Encoding()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        return Encoding.GetEncoding(932);
    }
}
