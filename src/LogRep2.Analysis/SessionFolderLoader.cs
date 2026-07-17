using System.Text.Json;
using System.Text.Json.Serialization;

namespace FFXI_LogAnalyzer.Core;

public sealed class SessionFolderLoader
{
    private const string SessionFileName = "session.json";
    private const string CanonicalRecordsFileName = "canonical_records.jsonl";
    private const string StatsFileName = "stats.json";
    private const string RawRecordsFileName = "raw_records.jsonl";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        Converters =
        {
            new JsonStringEnumConverter(JsonNamingPolicy.CamelCase)
        }
    };

    public LoadSessionResult Load(string folderPath)
    {
        if (string.IsNullOrWhiteSpace(folderPath))
        {
            return LoadSessionResult.Failure(["セッションフォルダが指定されていません。"]);
        }

        if (!Directory.Exists(folderPath))
        {
            return LoadSessionResult.Failure([$"セッションフォルダが存在しません: {folderPath}"]);
        }

        var normalizedFolderPath = Path.GetFullPath(folderPath);
        var sessionJsonPath = Path.Combine(normalizedFolderPath, SessionFileName);
        var canonicalRecordsPath = Path.Combine(normalizedFolderPath, CanonicalRecordsFileName);
        var statsJsonPath = Path.Combine(normalizedFolderPath, StatsFileName);
        var rawRecordsPath = Path.Combine(normalizedFolderPath, RawRecordsFileName);

        var missingFiles = GetMissingRequiredFiles(sessionJsonPath, canonicalRecordsPath, statsJsonPath);
        if (missingFiles.Count > 0)
        {
            return LoadSessionResult.Failure(missingFiles.Select(file => $"必須ファイルが見つかりません: {file}").ToArray());
        }

        try
        {
            var sessionInfo = ReadJsonFile<SessionInfo>(sessionJsonPath);
            var statsInfo = ReadJsonFile<StatsInfo>(statsJsonPath);
            var warnings = BuildWarnings(sessionInfo);
            var session = new AnalyzerInputSession(
                normalizedFolderPath,
                sessionJsonPath,
                canonicalRecordsPath,
                statsJsonPath,
                File.Exists(rawRecordsPath) ? rawRecordsPath : null,
                sessionInfo,
                statsInfo);

            return LoadSessionResult.Success(session, warnings);
        }
        catch (JsonException ex)
        {
            return LoadSessionResult.Failure([$"JSONの読み込みに失敗しました: {ex.Message}"]);
        }
        catch (IOException ex)
        {
            return LoadSessionResult.Failure([$"セッションファイルの読み込みに失敗しました: {ex.Message}"]);
        }
        catch (UnauthorizedAccessException ex)
        {
            return LoadSessionResult.Failure([$"セッションファイルへのアクセス権限がありません: {ex.Message}"]);
        }
    }

    private static IReadOnlyList<string> GetMissingRequiredFiles(params string[] paths)
    {
        return paths
            .Where(path => !File.Exists(path))
            .Select(path => Path.GetFileName(path) ?? path)
            .ToArray();
    }

    private static T ReadJsonFile<T>(string path)
    {
        var json = File.ReadAllText(path);
        var value = JsonSerializer.Deserialize<T>(json, JsonOptions);
        if (value is null)
        {
            throw new JsonException($"JSONが空です: {Path.GetFileName(path)}");
        }

        return value;
    }

    private static IReadOnlyList<string> BuildWarnings(SessionInfo sessionInfo)
    {
        if (sessionInfo.Status == SessionStatus.Completed)
        {
            return [];
        }

        return [$"このセッションは正常完了していない可能性があります。status={sessionInfo.Status}"];
    }
}
