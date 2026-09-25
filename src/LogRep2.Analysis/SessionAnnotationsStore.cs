using System.Globalization;
using System.Text.Json;

namespace FFXI_LogAnalyzer.Core;

public sealed class SessionAnnotationsStore
{
    public const string FileName = "session_annotations.json";
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = true,
    };

    public string Load(string folder, string sessionId)
    {
        try
        {
            using var stream = File.OpenRead(Path.Combine(folder, FileName));
            var data = JsonSerializer.Deserialize<Annotations>(stream, Options);
            if (data is null || data.SchemaVersion != 1 || data.SessionId != sessionId)
                throw new InvalidDataException("エイリアス設定の形式・バージョンまたはセッションIDが一致しません。");
            return NormalizeAlias(data.Alias);
        }
        catch (FileNotFoundException)
        {
            return string.Empty;
        }
    }

    public void Save(string folder, string sessionId, string alias)
    {
        var data = new Annotations { SessionId = sessionId, Alias = NormalizeAlias(alias) };
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
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new IOException($"エイリアス設定を保存できません: {path}", exception);
        }
        finally
        {
            try { File.Delete(temporaryPath); }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                // 保存時の例外を優先します。
            }
        }
    }

    public static string NormalizeAlias(string? alias)
    {
        alias ??= string.Empty;
        if (alias.Any(char.IsControl) || alias.Contains('\u2028') || alias.Contains('\u2029'))
            throw new ArgumentException("エイリアスに改行・制御文字は使用できません。");
        var normalized = alias.Trim();
        if (new StringInfo(normalized).LengthInTextElements > 100)
            throw new ArgumentException("エイリアスは100文字以内で入力してください。");
        return normalized;
    }

    private sealed class Annotations
    {
        public int SchemaVersion { get; set; } = 1;
        public string SessionId { get; set; } = string.Empty;
        public string Alias { get; set; } = string.Empty;
    }
}
