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
    public void Load_ReadsValidSessionJson()
    {
        var folderPath = CreateSessionFolder("completed");
        var result = new SessionFolderLoader().Load(folderPath);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Session);
        Assert.Equal("session-001", result.Session.SessionInfo.SessionId);
        Assert.Equal(SessionStatus.Completed, result.Session.SessionInfo.Status);
        Assert.Equal("1.0", result.Session.SessionInfo.SchemaVersions.SchemaVersion);
        Assert.Equal("utf-8", result.Session.SessionInfo.Encoding);
        Assert.Equal(["1_0.log", "2_0.log"], result.Session.SessionInfo.WatchFiles);
    }

    [Fact]
    public void Load_ReadsStatsJson()
    {
        var folderPath = CreateSessionFolder("completed");
        var result = new SessionFolderLoader().Load(folderPath);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Session);
        Assert.Equal(10, result.Session.StatsInfo.RawRecordsWritten);
        Assert.Equal(8, result.Session.StatsInfo.CanonicalRecordsWritten);
        Assert.Equal(2, result.Session.StatsInfo.ParseErrors);
        Assert.Equal(3, result.Session.StatsInfo.DecodeErrors);
        Assert.Equal(4, result.Session.StatsInfo.GapWarnings);
        Assert.Equal(DateTimeOffset.Parse("2026-06-23T12:34:56+09:00"), result.Session.StatsInfo.LastSeenAt);
    }

    [Fact]
    public void Load_ChecksCanonicalRecordsExists()
    {
        var folderPath = CreateSessionFolder("completed");
        var result = new SessionFolderLoader().Load(folderPath);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Session);
        Assert.EndsWith("canonical_records.jsonl", result.Session.CanonicalRecordsPath);
        Assert.True(File.Exists(result.Session.CanonicalRecordsPath));
    }

    [Fact]
    public void Load_CompletedStatus_HasNoWarnings()
    {
        var folderPath = CreateSessionFolder("completed");
        var result = new SessionFolderLoader().Load(folderPath);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Warnings);
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
