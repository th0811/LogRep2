using System.Text.Json;
using LogRep2.Contracts;

namespace FFXI_LogAnalyzer.Core;

public sealed record SavedAnalysisRange(string Id, string Name, string StartRecordId, string EndRecordId);

public sealed class SavedAnalysisRanges
{
    public int SchemaVersion { get; set; } = 1;
    public string SessionId { get; set; } = string.Empty;
    public List<SavedAnalysisRange> Ranges { get; set; } = [];
}

public sealed class SavedAnalysisRangesStore
{
    public const string FileName = "analysis_ranges.json";
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = true,
    };

    public SavedAnalysisRanges Load(string folder, string sessionId)
    {
        try
        {
            using var stream = File.OpenRead(Path.Combine(folder, FileName));
            var data = JsonSerializer.Deserialize<SavedAnalysisRanges>(stream, Options)
                ?? throw new InvalidDataException("保存区間の内容が空です。");
            Validate(data, sessionId);
            return data;
        }
        catch (FileNotFoundException)
        {
            return new() { SessionId = sessionId };
        }
    }

    public void Save(string folder, SavedAnalysisRanges data)
    {
        Validate(data, data.SessionId);
        // 読込後に破損したファイルも、空の設定として上書きしません。
        _ = Load(folder, data.SessionId);
        var path = Path.Combine(folder, FileName);
        var temporaryPath = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var stream = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                JsonSerializer.Serialize(stream, data, Options);
                stream.Flush(flushToDisk: true);
            }
            File.Move(temporaryPath, path, overwrite: true);
        }
        finally
        {
            try { File.Delete(temporaryPath); }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                // 保存時のエラーを優先します。
            }
        }
    }

    private static void Validate(SavedAnalysisRanges data, string sessionId)
    {
        if (data.SchemaVersion != 1 || string.IsNullOrWhiteSpace(sessionId) || data.SessionId != sessionId
            || data.Ranges is null || data.Ranges.Any(range => range is null
                || string.IsNullOrWhiteSpace(range.Id) || string.IsNullOrWhiteSpace(range.Name)
                || range.Name != SessionAnnotationsStore.NormalizeAlias(range.Name)
                || string.IsNullOrWhiteSpace(range.StartRecordId) || string.IsNullOrWhiteSpace(range.EndRecordId))
            || data.Ranges.Select(range => range.Id).Distinct(StringComparer.Ordinal).Count() != data.Ranges.Count)
            throw new InvalidDataException("保存区間の形式・バージョンまたはセッションIDが不正です。");
    }

    public static (T Start, T End) Resolve<T>(IReadOnlyList<T> records, string sessionId, SavedAnalysisRange range)
        where T : ICanonicalRecord
    {
        var starts = records.Where(record => record.SessionId == sessionId && record.CanonicalRecordId == range.StartRecordId).ToArray();
        var ends = records.Where(record => record.SessionId == sessionId && record.CanonicalRecordId == range.EndRecordId).ToArray();
        if (starts.Length != 1 || ends.Length != 1)
            throw new InvalidDataException("保存区間の開始・終了行が見つからないか、行IDが重複しています。ログから区間を指定し直してください。");
        if (starts[0].Order is not { } start || ends[0].Order is not { } end || start > end)
            throw new InvalidDataException("保存区間の開始・終了行のログ順が不正です。");
        return (starts[0], ends[0]);
    }
}
