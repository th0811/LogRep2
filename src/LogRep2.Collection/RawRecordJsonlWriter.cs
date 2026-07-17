using System.Text.Json;

namespace FfxiTempLogCollector.Core;

public sealed class RawRecordJsonlWriter
{
    public const string FileName = "raw_records.jsonl";

    private readonly JsonlWriterOptions _options;

    public RawRecordJsonlWriter(JsonlWriterOptions? options = null)
    {
        _options = options ?? new JsonlWriterOptions();
    }

    public void Append(string sessionDirectory, RawRecord record)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionDirectory);
        ArgumentNullException.ThrowIfNull(record);

        Directory.CreateDirectory(sessionDirectory);
        var path = Path.Combine(sessionDirectory, FileName);
        var json = JsonSerializer.Serialize(
            record,
            _options.SerializerOptions);

        using var writer = new StreamWriter(
            path,
            append: true,
            _options.Encoding);
        writer.NewLine = _options.NewLine;
        writer.WriteLine(json);

        if (_options.FlushAfterWrite)
        {
            writer.Flush();
        }
    }
}
