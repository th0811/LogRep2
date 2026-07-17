using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace FfxiTempLogCollector.Core;

public sealed class JsonlWriterOptions
{
    public Encoding Encoding { get; init; } = new UTF8Encoding(
        encoderShouldEmitUTF8Identifier: false);

    public string NewLine { get; init; } = "\n";

    public bool FlushAfterWrite { get; init; } = true;

    internal JsonSerializerOptions SerializerOptions { get; } =
        CreateSerializerOptions();

    private static JsonSerializerOptions CreateSerializerOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            WriteIndented = false,
        };
        options.Converters.Add(
            new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower));

        return options;
    }
}
