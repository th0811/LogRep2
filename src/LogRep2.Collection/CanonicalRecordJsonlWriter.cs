using System.Text.Json;

namespace FfxiTempLogCollector.Core;

public sealed class CanonicalRecordJsonlWriter
{
    public const string FileName = "canonical_records.jsonl";

    private readonly JsonlWriterOptions _options;

    public CanonicalRecordJsonlWriter(JsonlWriterOptions? options = null)
    {
        _options = options ?? new JsonlWriterOptions();
    }

    public void WriteAll(
        string sessionDirectory,
        IEnumerable<CanonicalRecord> records)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionDirectory);
        ArgumentNullException.ThrowIfNull(records);

        Directory.CreateDirectory(sessionDirectory);
        var path = Path.Combine(sessionDirectory, FileName);
        var temporaryPath = $"{path}.{Guid.NewGuid():N}.tmp";

        try
        {
            using (var writer = new StreamWriter(
                temporaryPath,
                append: false,
                _options.Encoding))
            {
                writer.NewLine = _options.NewLine;

                foreach (var record in records.OrderBy(
                             record => record.Order))
                {
                    writer.WriteLine(
                        JsonSerializer.Serialize(
                            record,
                            _options.SerializerOptions));
                }

                if (_options.FlushAfterWrite)
                {
                    writer.Flush();
                }
            }

            File.Move(temporaryPath, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }
}
