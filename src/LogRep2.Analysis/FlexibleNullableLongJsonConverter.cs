using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace FFXI_LogAnalyzer.Core;

public sealed class FlexibleNullableLongJsonConverter : JsonConverter<long?>
{
    public override long? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return reader.TokenType switch
        {
            JsonTokenType.Null => null,
            JsonTokenType.Number => reader.GetInt64(),
            JsonTokenType.String => ParseStringValue(reader.GetString()),
            _ => throw new JsonException($"数値または数値文字列として読み込めないJSON値です: {reader.TokenType}")
        };
    }

    public override void Write(Utf8JsonWriter writer, long? value, JsonSerializerOptions options)
    {
        if (value is null)
        {
            writer.WriteNullValue();
            return;
        }

        writer.WriteNumberValue(value.Value);
    }

    private static long? ParseStringValue(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var decimalValue))
        {
            return decimalValue;
        }

        if (long.TryParse(value, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var hexValue))
        {
            return hexValue;
        }

        return null;
    }
}
