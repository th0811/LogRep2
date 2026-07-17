using System.Text;
using FfxiTempLogCollector.Core;

namespace FfxiTempLogCollector.Tests;

public sealed class TextNormalizerTests
{
    private readonly TextNormalizer _normalizer = new();

    [Fact]
    public void Nulと前後空白と連続空白を除去できる()
    {
        var actual = _normalizer.Normalize(
            " \0  FFXI\tTEMP\r\nログ　 収集 \0 ");

        Assert.Equal("FFXI TEMP ログ 収集", actual);
    }

    [Theory]
    [InlineData(0x1E)]
    [InlineData(0x1F)]
    [InlineData(0x7F)]
    [InlineData(0xEF)]
    public void 制御コードと後続1バイトを除去できる(byte controlCode)
    {
        var encoding = GetCp932Encoding();
        var prefix = encoding.GetBytes("前");
        var suffix = encoding.GetBytes("後");
        var rawBytes = new byte[
            prefix.Length
            + 2
            + suffix.Length
            + 1];
        prefix.CopyTo(rawBytes, 0);
        rawBytes[prefix.Length] = controlCode;
        rawBytes[prefix.Length + 1] = 0x01;
        suffix.CopyTo(rawBytes, prefix.Length + 2);

        var actual = _normalizer.Normalize(rawBytes, encoding);

        Assert.Equal("前後", actual);
    }

    private static Encoding GetCp932Encoding()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        return Encoding.GetEncoding(932);
    }
}
