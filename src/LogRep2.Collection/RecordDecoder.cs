using System.Text;

namespace FfxiTempLogCollector.Core;

public sealed class RecordDecoder
{
    private static readonly Encoding Cp932Encoding = CreateCp932Encoding();

    private readonly TextNormalizer _textNormalizer;

    public RecordDecoder(TextNormalizer? textNormalizer = null)
    {
        _textNormalizer = textNormalizer ?? new TextNormalizer();
    }

    public DecodedLogMessage Decode(TempLogRawRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);

        return Decode(record.RawMessageBytes);
    }

    public DecodedLogMessage Decode(byte[] rawMessageBytes)
    {
        ArgumentNullException.ThrowIfNull(rawMessageBytes);

        return new DecodedLogMessage
        {
            RawMessageHex = Convert.ToHexString(rawMessageBytes),
            DecodedText = Cp932Encoding.GetString(rawMessageBytes),
            VisibleText = _textNormalizer.Normalize(
                rawMessageBytes,
                Cp932Encoding),
        };
    }

    private static Encoding CreateCp932Encoding()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

        return Encoding.GetEncoding(
            932,
            EncoderFallback.ReplacementFallback,
            new DecoderReplacementFallback("\uFFFD"));
    }
}
