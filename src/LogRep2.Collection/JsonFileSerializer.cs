using System.Text.Json;
using System.Text.Json.Serialization;

namespace FfxiTempLogCollector.Core;

internal static class JsonFileSerializer
{
    internal static JsonSerializerOptions Options { get; } = CreateOptions();

    internal static T Load<T>(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        try
        {
            using var stream = File.OpenRead(path);
            return JsonSerializer.Deserialize<T>(stream, Options)
                ?? throw new InvalidDataException($"JSONファイルの内容が空です: {path}");
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException($"JSONファイルの形式が不正です: {path}", exception);
        }
    }

    internal static void Save<T>(string path, T value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(value);

        var fullPath = Path.GetFullPath(path);
        var directory = Path.GetDirectoryName(fullPath)
            ?? throw new InvalidOperationException($"保存先ディレクトリを特定できません: {path}");

        Directory.CreateDirectory(directory);

        var temporaryPath = $"{fullPath}.{Guid.NewGuid():N}.tmp";

        try
        {
            using (var stream = new FileStream(
                temporaryPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None))
            {
                JsonSerializer.Serialize(stream, value, Options);
                stream.Flush(flushToDisk: true);
            }

            File.Move(temporaryPath, fullPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            PropertyNameCaseInsensitive = true,
            WriteIndented = true,
        };

        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower));

        return options;
    }
}
