using System.Text.Json;
using FfxiTempLogCollector.Core;

namespace FfxiTempLogCollector.Tests;

public sealed class SessionManagerTests
{
    [Fact]
    public void セッションIdが指定形式になる()
    {
        var startedAt = new DateTimeOffset(
            2026,
            6,
            23,
            21,
            30,
            0,
            TimeSpan.FromHours(9));

        var actual = SessionManager.CreateSessionId(startedAt);

        Assert.Equal("20260623-213000", actual);
        Assert.Matches(@"^\d{8}-\d{6}$", actual);
    }

    [Fact]
    public void SessionJsonを保存できる()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var manager = new SessionManager();
        var startedAt = new DateTimeOffset(
            2026,
            6,
            23,
            21,
            30,
            0,
            TimeSpan.FromHours(9));
        var config = new CollectorConfig
        {
            TempDir = @"C:\FFXI\TEMP",
            OutputDir = temporaryDirectory.Path,
        };
        var session = manager.Create(
            config,
            ["1_0.log", "2_0.log"],
            "1.0.0",
            startedAt);

        manager.Save(temporaryDirectory.Path, session);

        var sessionPath = temporaryDirectory.GetPath("session.json");
        Assert.True(File.Exists(sessionPath));

        using var document = JsonDocument.Parse(File.ReadAllText(sessionPath));
        var root = document.RootElement;
        Assert.Equal("1.0", root.GetProperty("schema_version").GetString());
        Assert.Equal("active", root.GetProperty("status").GetString());
        Assert.Equal(2, root.GetProperty("watch_files").GetArrayLength());
    }

    [Fact]
    public void SessionStatusをActiveからCompletedへ更新できる()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var manager = new SessionManager();
        var startedAt = new DateTimeOffset(
            2026,
            6,
            23,
            21,
            30,
            0,
            TimeSpan.FromHours(9));
        var endedAt = startedAt.AddMinutes(40);
        var session = manager.Create(
            new CollectorConfig(),
            [],
            "1.0.0",
            startedAt);
        manager.Save(temporaryDirectory.Path, session);

        var completed = manager.Complete(temporaryDirectory.Path, endedAt);
        var reloaded = manager.Load(temporaryDirectory.Path);

        Assert.Equal(SessionStatus.Completed, completed.Status);
        Assert.Equal(endedAt, completed.EndedAt);
        Assert.Equal(SessionStatus.Completed, reloaded.Status);
        Assert.Equal(endedAt, reloaded.EndedAt);
    }
}
