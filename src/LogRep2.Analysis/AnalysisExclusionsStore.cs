using System.Text.Json;

namespace FFXI_LogAnalyzer.Core;

public sealed class AnalysisExclusionsStore
{
    public const string FileName = "analysis_exclusions.json";
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = true,
    };

    public AnalysisExclusions Load(string sessionFolder)
    {
        var path = Path.Combine(sessionFolder, FileName);
        try
        {
            using var stream = File.OpenRead(path);
            var exclusions = JsonSerializer.Deserialize<AnalysisExclusions>(stream, JsonOptions)
                ?? throw new InvalidDataException("除外設定の内容が空です。");
            Validate(exclusions);
            return exclusions;
        }
        catch (FileNotFoundException)
        {
            return new AnalysisExclusions();
        }
        catch (Exception exception) when (exception is JsonException or IOException or UnauthorizedAccessException)
        {
            throw new InvalidDataException($"除外設定を読み込めません: {path}。{exception.Message}", exception);
        }
    }

    public void Save(string sessionFolder, AnalysisExclusions exclusions)
    {
        Validate(exclusions);
        var path = Path.Combine(sessionFolder, FileName);
        var temporaryPath = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var stream = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                JsonSerializer.Serialize(stream, exclusions, JsonOptions);
                stream.Flush(flushToDisk: true);
            }

            File.Move(temporaryPath, path, overwrite: true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new IOException($"除外設定を保存できません: {path}。{exception.Message}", exception);
        }
        finally
        {
            try
            {
                File.Delete(temporaryPath);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                // 元の保存エラーを優先します。
            }
        }
    }

    private static void Validate(AnalysisExclusions exclusions)
    {
        if (exclusions.SchemaVersion != 1 || exclusions.Records is null || exclusions.Groups is null
            || exclusions.Records.Any(key => key is null || string.IsNullOrWhiteSpace(key.SessionId)
                || string.IsNullOrWhiteSpace(key.CanonicalRecordId))
            || exclusions.Groups.Any(key => key is null || string.IsNullOrWhiteSpace(key.SessionId)
                || string.IsNullOrWhiteSpace(key.EventGroup)))
        {
            throw new InvalidDataException("除外設定の形式またはバージョンが不正です。");
        }
    }
}
