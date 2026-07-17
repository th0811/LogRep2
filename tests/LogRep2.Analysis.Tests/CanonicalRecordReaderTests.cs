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
    public void Read_LoadsMultipleJsonlLines()
    {
        var path = CreateJsonlFile(
            CreateRecordJson("record-001", 1, "1行目"),
            CreateRecordJson("record-002", 2, "2行目"));

        var result = new CanonicalRecordReader().Read(path);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.LineErrors);
        Assert.Equal(2, result.Records.Count);
        Assert.Equal("record-001", result.Records[0].CanonicalRecordId);
        Assert.Equal("record-002", result.Records[1].CanonicalRecordId);
    }

    [Fact]
    public void Read_SortsByOrderAscending()
    {
        var path = CreateJsonlFile(
            CreateRecordJson("record-003", 30, "3行目"),
            CreateRecordJson("record-001", 10, "1行目"),
            CreateRecordJson("record-002", 20, "2行目"));

        var result = new CanonicalRecordReader().Read(path);

        Assert.Equal(["record-001", "record-002", "record-003"], result.Records.Select(record => record.CanonicalRecordId));
    }

    [Fact]
    public void Read_UsesReadOrderWhenOrderIsSameOrMissing()
    {
        var path = CreateJsonlFile(
            CreateRecordJson("record-002", 2, "2行目"),
            CreateRecordJson("record-001", 1, "1行目"),
            CreateRecordJson("record-002b", 2, "2行目その2"),
            CreateRecordJsonWithoutOrder("record-no-order-1", "orderなし1"),
            CreateRecordJsonWithoutOrder("record-no-order-2", "orderなし2"));

        var result = new CanonicalRecordReader().Read(path);

        Assert.Equal(
            ["record-001", "record-002", "record-002b", "record-no-order-1", "record-no-order-2"],
            result.Records.Select(record => record.CanonicalRecordId));
    }

    [Fact]
    public void Read_IgnoresBlankLines()
    {
        var path = CreateJsonlFile(
            CreateRecordJson("record-001", 1, "1行目"),
            "",
            "   ",
            CreateRecordJson("record-002", 2, "2行目"));

        var result = new CanonicalRecordReader().Read(path);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.LineErrors);
        Assert.Equal(2, result.Records.Count);
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

    [Fact]
    public void Read_AcceptsNumericFieldsAsStrings()
    {
        var path = CreateJsonlFile("""
            {
              "schema_version": "1.0",
              "canonical_record_id": "record-001",
              "session_id": "session-001",
              "order": "10",
              "event_group": "event-001",
              "sequence_hint_min": "00000009",
              "sequence_hint_max": "00000010",
              "visible_text": "文字列数値を含むログ",
              "is_marker": false
            }
            """);

        var result = new CanonicalRecordReader().Read(path);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.LineErrors);
        var record = Assert.Single(result.Records);
        Assert.Equal(10, record.Order);
        Assert.Equal(9, record.SequenceHintMin);
        Assert.Equal(10, record.SequenceHintMax);
    }

    [Fact]
    public void Read_AcceptsHexSequenceHintStrings()
    {
        var path = CreateJsonlFile("""
            {
              "schema_version": "1.0",
              "canonical_record_id": "record-001",
              "session_id": "session-001",
              "order": 10,
              "event_group": "event-001",
              "sequence_hint_min": "001ad3c0",
              "sequence_hint_max": "001ad3c1",
              "visible_text": "16進sequence hintを含むログ",
              "is_marker": false
            }
            """);

        var result = new CanonicalRecordReader().Read(path);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.LineErrors);
        var record = Assert.Single(result.Records);
        Assert.Equal(0x001ad3c0, record.SequenceHintMin);
        Assert.Equal(0x001ad3c1, record.SequenceHintMax);
    }

    [Fact]
    public void Read_TreatsBrokenNumericStringsAsNull()
    {
        var path = CreateJsonlFile("""
            {
              "schema_version": "1.0",
              "canonical_record_id": "record-001",
              "session_id": "session-001",
              "order": "broken",
              "event_group": "event-001",
              "sequence_hint_min": "\u001e\u0001\u001e\u0001",
              "sequence_hint_max": "",
              "visible_text": "壊れた数値文字列を含むログ",
              "is_marker": false
            }
            """);

        var result = new CanonicalRecordReader().Read(path);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.LineErrors);
        var record = Assert.Single(result.Records);
        Assert.Null(record.Order);
        Assert.Null(record.SequenceHintMin);
        Assert.Null(record.SequenceHintMax);
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

    private static string CreateRecordJsonWithoutOrder(string id, string visibleText)
    {
        return $$"""
            {
              "schema_version": "1.0",
              "canonical_record_id": "{{id}}",
              "session_id": "session-001",
              "visible_text": "{{visibleText}}",
              "is_marker": false
            }
            """;
    }
}
