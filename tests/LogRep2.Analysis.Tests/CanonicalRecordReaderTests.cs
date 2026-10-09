using FFXI_LogAnalyzer.Core;

namespace FFXI_LogAnalyzer.Tests;

public sealed class CanonicalRecordReaderTests : IDisposable
{
    private readonly string _tempRoot;

    public CanonicalRecordReaderTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), "FFXI_LogAnalyzer.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempRoot);
    }

    [Fact]
    public void 複数行をOrder順に読み込み空行を無視する()
    {
        var path = CreateJsonlFile(
            CreateRecordJson("record-002", 2, "2行目"),
            "",
            "   ",
            CreateRecordJson("record-001", 1, "1行目"));

        var result = new CanonicalRecordReader().Read(path);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.LineErrors);
        Assert.Equal(["record-001", "record-002"], result.Records.Select(record => record.CanonicalRecordId));
        Assert.Equal(["1行目", "2行目"], result.Records.Select(record => record.VisibleText));
    }

    [Fact]
    public void Read_KeepsBrokenJsonLineAsLineError()
    {
        var path = CreateJsonlFile(
            CreateRecordJson("record-001", 1, "1行目"),
            "{壊れたJSON",
            CreateRecordJson("record-002", 2, "2行目"));

        var result = new CanonicalRecordReader().Read(path);

        Assert.True(result.IsSuccess);
        Assert.True(result.HasLineErrors);
        var lineError = Assert.Single(result.LineErrors);
        Assert.Equal(2, lineError.LineNumber);
        Assert.Contains("読み込みに失敗", lineError.Message);
        Assert.Equal(2, result.Records.Count);
    }

    [Fact]
    public void Read_LoadsMarkerFields()
    {
        var path = CreateJsonlFile("""
            {
              "schema_version": "1.0",
              "canonical_record_id": "marker-001",
              "session_id": "session-001",
              "order": 10,
              "first_seen_at": "2026-06-23T12:00:00+09:00",
              "last_seen_at": "2026-06-23T12:00:01+09:00",
              "source_windows": [1, 2],
              "source_files": ["1_0.log", "2_0.log"],
              "source_raw_record_ids": ["raw-001", "raw-002"],
              "event_group": "event-001",
              "sequence_hint_min": 100,
              "sequence_hint_max": 101,
              "visible_text": "#start",
              "message_time_text": "[12:00:00]",
              "message_time_precision": "second",
              "is_marker": true,
              "marker_keyword": "#start",
              "canonical_key": "key-001"
            }
            """);

        var result = new CanonicalRecordReader().Read(path);

        var record = Assert.Single(result.Records);
        Assert.Equal("[12:00:00]", record.MessageTimeText);
        Assert.True(record.IsMarker);
        Assert.Equal("#start", record.MarkerKeyword);
        Assert.Equal([1, 2], record.SourceWindows);
        Assert.Equal(["1_0.log", "2_0.log"], record.SourceFiles);
        Assert.Equal(["raw-001", "raw-002"], record.SourceRawRecordIds);
        Assert.Equal(100, record.SequenceHintMin);
        Assert.Equal(101, record.SequenceHintMax);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempRoot))
        {
            Directory.Delete(_tempRoot, recursive: true);
        }
    }

    private string CreateJsonlFile(params string[] lines)
    {
        var path = Path.Combine(_tempRoot, "canonical_records.jsonl");
        File.WriteAllLines(path, lines.Select(NormalizeJsonlLine));
        return path;
    }

    private static string NormalizeJsonlLine(string line)
    {
        return string.IsNullOrWhiteSpace(line)
            ? line
            : line.Replace("\r", " ").Replace("\n", " ");
    }

    private static string CreateRecordJson(string id, long order, string visibleText)
    {
        return $$"""
            {
              "schema_version": "1.0",
              "canonical_record_id": "{{id}}",
              "session_id": "session-001",
              "order": {{order}},
              "first_seen_at": "2026-06-23T12:00:00+09:00",
              "last_seen_at": "2026-06-23T12:00:01+09:00",
              "source_windows": [1],
              "source_files": ["1_0.log"],
              "source_raw_record_ids": ["raw-001"],
              "event_group": "event-001",
              "sequence_hint_min": null,
              "sequence_hint_max": null,
              "visible_text": "{{visibleText}}",
              "message_time_text": "[12:00]",
              "message_time_precision": "minute",
              "is_marker": false,
              "marker_keyword": null,
              "canonical_key": "key-{{id}}"
            }
            """;
    }

}
