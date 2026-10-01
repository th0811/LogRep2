using FFXI_LogAnalyzer.Core;

namespace FFXI_LogAnalyzer.Tests;

public sealed class SessionFolderLoaderTests : IDisposable
{
    private readonly string _tempRoot;

    public SessionFolderLoaderTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), "FFXI_LogAnalyzer.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempRoot);
    }

    [Fact]
    public void 完了セッションの情報と統計とログパスを読み込める()
    {
        var folderPath = CreateSessionFolder("completed");
        var result = new SessionFolderLoader().Load(folderPath);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Warnings);
        Assert.NotNull(result.Session);
        var session = result.Session;
        Assert.Equal("session-001", session.SessionInfo.SessionId);
        Assert.Equal(SessionStatus.Completed, session.SessionInfo.Status);
        Assert.Equal("1.0", session.SessionInfo.SchemaVersions.SchemaVersion);
        Assert.Equal("utf-8", session.SessionInfo.Encoding);
        Assert.Equal(["1_0.log", "2_0.log"], session.SessionInfo.WatchFiles);
        Assert.Equal(10, session.StatsInfo.RawRecordsWritten);
        Assert.Equal(8, session.StatsInfo.CanonicalRecordsWritten);
        Assert.Equal(2, session.StatsInfo.ParseErrors);
        Assert.Equal(3, session.StatsInfo.DecodeErrors);
        Assert.Equal(4, session.StatsInfo.GapWarnings);
        Assert.Equal(DateTimeOffset.Parse("2026-06-23T12:34:56+09:00"), session.StatsInfo.LastSeenAt);
        Assert.Equal(Path.Combine(folderPath, "canonical_records.jsonl"), session.CanonicalRecordsPath);
        Assert.True(File.Exists(session.CanonicalRecordsPath));
    }

    [Theory]
    [InlineData("active")]
    [InlineData("aborted")]
    [InlineData("unknown")]
    public void Load_IncompleteStatus_HasWarning(string status)
    {
        var folderPath = CreateSessionFolder(status);
        var result = new SessionFolderLoader().Load(folderPath);

        Assert.True(result.IsSuccess);
        Assert.Single(result.Warnings);
        Assert.Contains("正常完了していない可能性", result.Warnings[0]);
    }

    [Theory]
    [InlineData("session.json")]
    [InlineData("canonical_records.jsonl")]
    [InlineData("stats.json")]
    public void Load_MissingRequiredFile_ReturnsError(string missingFileName)
    {
        var folderPath = CreateSessionFolder("completed");
        File.Delete(Path.Combine(folderPath, missingFileName));

        var result = new SessionFolderLoader().Load(folderPath);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Session);
        Assert.Contains(result.Errors, error => error.Contains(missingFileName));
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempRoot))
        {
            Directory.Delete(_tempRoot, recursive: true);
        }
    }

    private string CreateSessionFolder(string status)
    {
        var folderPath = Path.Combine(_tempRoot, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folderPath);

        File.WriteAllText(Path.Combine(folderPath, "session.json"), CreateSessionJson(status));
        File.WriteAllText(Path.Combine(folderPath, "stats.json"), CreateStatsJson());
        File.WriteAllText(Path.Combine(folderPath, "canonical_records.jsonl"), string.Empty);

        return folderPath;
    }

    private static string CreateSessionJson(string status)
    {
        return $$"""
            {
              "schema_version": "1.0",
              "raw_schema_version": "1.0",
              "canonical_schema_version": "1.0",
              "collector_version": "0.1.0",
              "session_id": "session-001",
              "status": "{{status}}",
              "started_at": "2026-06-23T12:00:00+09:00",
              "ended_at": "2026-06-23T12:30:00+09:00",
              "temp_dir": "C:\\FFXI\\TEMP",
              "output_dir": "C:\\logs\\session-001",
              "encoding": "utf-8",
              "timezone": "Asia/Tokyo",
              "watch_files": [
                "1_0.log",
                "2_0.log"
              ]
            }
            """;
    }

    private static string CreateStatsJson()
    {
        return """
            {
              "raw_records_written": 10,
              "canonical_records_written": 8,
              "duplicate_raw_records_skipped": 1,
              "duplicate_canonical_records_skipped": 1,
              "parse_errors": 2,
              "decode_errors": 3,
              "gap_warnings": 4,
              "last_seen_at": "2026-06-23T12:34:56+09:00"
            }
            """;
    }
}
