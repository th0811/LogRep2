using System.Text.Json;
using FFXI_LogAnalyzer.Core;
using FfxiTempLogCollector.Core;

namespace FfxiTempLogCollector.Tests;

public sealed class Phase6CompatibilityTests
{
    private static readonly string[] AssistantToolBaseProperties =
    [
        "schema_version",
        "raw_record_id",
        "session_id",
        "first_seen_at",
        "source_file",
        "window_id",
        "rotation_index",
        "file_mtime",
        "file_size",
        "file_hash",
        "record_index",
        "record_offset",
        "raw_record_hash",
        "meta_fields",
        "event_group",
        "sequence_hint",
        "message_token_count",
        "display",
        "raw_message_hex",
        "visible_text",
        "message_time_text",
        "message_time_precision",
        "is_marker",
        "marker_keyword",
        "parse_status",
        "parse_error",
    ];

    private static readonly string[] AlwaysSerializedProperties =
    [
        "schema_version",
        "raw_record_id",
        "session_id",
        "first_seen_at",
        "source_file",
        "window_id",
        "rotation_index",
        "file_mtime",
        "file_size",
        "file_hash",
        "record_index",
        "record_offset",
        "raw_record_hash",
        "meta_fields",
        "raw_message_hex",
        "visible_text",
        "is_marker",
        "parse_status",
    ];

    [Fact]
    public void LogRep2生成セッションを過去ログ分析で読み込める()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var result = GenerateSession(temporaryDirectory);

        var loadResult = new SessionFolderLoader().Load(result.SessionDirectory);
        var recordResult = new CanonicalRecordReader().Read(
            Path.Combine(result.SessionDirectory, CanonicalRecordJsonlWriter.FileName));

        Assert.True(loadResult.IsSuccess, string.Join(Environment.NewLine, loadResult.Errors));
        Assert.NotNull(loadResult.Session);
        Assert.Equal("completed", loadResult.Session.SessionInfo.Status.ToString().ToLowerInvariant());
        Assert.Empty(recordResult.Errors);
        Assert.Equal(result.CanonicalRecordsWritten, recordResult.Records.Count);
        Assert.NotEmpty(new ActionGroupBuilder().Build(recordResult.Records));
    }

    [Fact]
    public void LogRep2生成RawJsonlはAssistantToolの列契約を維持する()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var result = GenerateSession(temporaryDirectory);
        var rawPath = Path.Combine(result.SessionDirectory, RawRecordJsonlWriter.FileName);
        var firstLine = File.ReadLines(rawPath).First();
        using var document = JsonDocument.Parse(firstLine);
        var root = document.RootElement;

        foreach (var propertyName in AlwaysSerializedProperties)
        {
            Assert.True(root.TryGetProperty(propertyName, out _), $"不足プロパティ: {propertyName}");
        }

        Assert.Equal(JsonValueKind.Array, root.GetProperty("meta_fields").ValueKind);

        using var displayDocument = JsonDocument.Parse(File.ReadLines(rawPath).First(
            line => line.Contains("\"display\"", StringComparison.Ordinal)));
        Assert.Equal(JsonValueKind.Object, displayDocument.RootElement.GetProperty("display").ValueKind);
        Assert.True(displayDocument.RootElement.GetProperty("display").TryGetProperty("color_code", out _));

        var appScript = File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory,
            "compatibility",
            "AssistantTool",
            "app.js"));
        foreach (var propertyName in AssistantToolBaseProperties.Where(
                     name => name is not "meta_fields" and not "display"))
        {
            Assert.Contains($"\"{propertyName}\"", appScript, StringComparison.Ordinal);
        }
        Assert.Contains("display.color_code", appScript, StringComparison.Ordinal);
        Assert.Contains("meta_fields", appScript, StringComparison.Ordinal);
    }

    private static CollectionResult GenerateSession(TemporaryDirectory temporaryDirectory)
    {
        return new OnceCollectionRunner().Run(new CollectorConfig
        {
            TempDir = Path.Combine(AppContext.BaseDirectory, "fixtures", "temp_logs"),
            OutputDir = temporaryDirectory.GetPath("sessions"),
            WatchWindow1 = true,
            WatchWindow2 = true,
            RotationSlots = 2,
        });
    }
}
