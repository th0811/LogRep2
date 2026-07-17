using System.Globalization;

namespace FfxiTempLogCollector.Core;

public sealed class SessionManager
{
    public const string SessionFileName = "session.json";

    public static string CreateSessionId(DateTimeOffset startedAt)
    {
        return startedAt.ToString(
            "yyyyMMdd-HHmmss",
            CultureInfo.InvariantCulture);
    }

    public SessionInfo Create(
        CollectorConfig config,
        IEnumerable<string> watchFiles,
        string collectorVersion,
        DateTimeOffset? startedAt = null)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(watchFiles);
        ArgumentException.ThrowIfNullOrWhiteSpace(collectorVersion);

        var actualStartedAt = startedAt ?? DateTimeOffset.Now;
        var sessionId = CreateSessionId(actualStartedAt);

        return new SessionInfo
        {
            CollectorVersion = collectorVersion,
            SessionId = sessionId,
            Status = SessionStatus.Active,
            StartedAt = actualStartedAt,
            TempDir = config.TempDir,
            OutputDir = config.OutputDir,
            Encoding = config.Encoding,
            Timezone = config.Timezone,
            WatchFiles = [.. watchFiles],
        };
    }

    public SessionInfo Load(string sessionDirectory)
    {
        return JsonFileSerializer.Load<SessionInfo>(
            GetSessionPath(sessionDirectory));
    }

    public void Save(string sessionDirectory, SessionInfo session)
    {
        ArgumentNullException.ThrowIfNull(session);

        JsonFileSerializer.Save(GetSessionPath(sessionDirectory), session);
    }

    public SessionInfo Complete(
        string sessionDirectory,
        DateTimeOffset? endedAt = null)
    {
        var session = Load(sessionDirectory);
        session.Status = SessionStatus.Completed;
        session.EndedAt = endedAt ?? DateTimeOffset.Now;
        Save(sessionDirectory, session);

        return session;
    }

    public SessionInfo Abort(
        string sessionDirectory,
        DateTimeOffset? endedAt = null)
    {
        var session = Load(sessionDirectory);
        session.Status = SessionStatus.Aborted;
        session.EndedAt = endedAt ?? DateTimeOffset.Now;
        Save(sessionDirectory, session);

        return session;
    }

    private static string GetSessionPath(string sessionDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionDirectory);

        return Path.Combine(sessionDirectory, SessionFileName);
    }
}
