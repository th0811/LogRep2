using System.Text;
using System.Text.Json;

namespace FFXI_LogAnalyzer.Core;

public sealed class CanonicalRecordReader
{
    private static readonly UTF8Encoding Utf8Encoding = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    public CanonicalRecordLoadResult Read(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return new CanonicalRecordLoadResult([], [], ["canonical_records.jsonl のパスが指定されていません。"]);
        }

        if (!File.Exists(path))
        {
            return new CanonicalRecordLoadResult([], [], [$"canonical_records.jsonl が見つかりません: {path}"]);
        }

        try
        {
            var loadedRecords = new List<LoadedCanonicalRecord>();
            var lineErrors = new List<JsonlLineReadError>();
            var lineNumber = 0;

            using var reader = new StreamReader(path, Utf8Encoding);
            while (reader.ReadLine() is { } line)
            {
                lineNumber++;
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                try
                {
                    var record = JsonSerializer.Deserialize<CanonicalRecord>(line, JsonOptions);
                    if (record is null)
                    {
                        lineErrors.Add(new JsonlLineReadError(lineNumber, line, "JSON行が空です。"));
                        continue;
                    }

                    loadedRecords.Add(new LoadedCanonicalRecord(record, loadedRecords.Count));
                }
                catch (JsonException ex)
                {
                    lineErrors.Add(new JsonlLineReadError(lineNumber, line, $"JSON行の読み込みに失敗しました: {ex.Message}"));
                }
            }

            var records = loadedRecords
                .OrderBy(loadedRecord => loadedRecord.Record.Order ?? long.MaxValue)
                .ThenBy(loadedRecord => loadedRecord.ReadIndex)
                .Select(loadedRecord => loadedRecord.Record)
                .ToArray();

            return new CanonicalRecordLoadResult(records, lineErrors, []);
        }
        catch (DecoderFallbackException ex)
        {
            return new CanonicalRecordLoadResult([], [], [$"canonical_records.jsonl はUTF-8として読み込めません: {ex.Message}"]);
        }
        catch (IOException ex)
        {
            return new CanonicalRecordLoadResult([], [], [$"canonical_records.jsonl の読み込みに失敗しました: {ex.Message}"]);
        }
        catch (UnauthorizedAccessException ex)
        {
            return new CanonicalRecordLoadResult([], [], [$"canonical_records.jsonl へのアクセス権限がありません: {ex.Message}"]);
        }
    }

    private sealed record LoadedCanonicalRecord(CanonicalRecord Record, int ReadIndex);
}
