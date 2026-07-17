namespace FfxiTempLogCollector.Core;

public sealed class OnceCollectionRunner
{
    private const string CollectorVersion = "1.0.0";

    private readonly TempLogWatchTargetBuilder _targetBuilder;
    private readonly TempLogFileReader _fileReader;
    private readonly CollectorPipeline _pipeline;
    private readonly SessionManager _sessionManager;
    private readonly StateStore _stateStore;
    private readonly StatsStore _statsStore;
    private readonly CanonicalRecordJsonlWriter _canonicalWriter;
    private readonly Func<DateTimeOffset> _clock;

    public OnceCollectionRunner(
        TempLogWatchTargetBuilder? targetBuilder = null,
        TempLogFileReader? fileReader = null,
        CollectorPipeline? pipeline = null,
        SessionManager? sessionManager = null,
        StateStore? stateStore = null,
        StatsStore? statsStore = null,
        CanonicalRecordJsonlWriter? canonicalWriter = null,
        Func<DateTimeOffset>? clock = null)
    {
        _targetBuilder = targetBuilder ?? new TempLogWatchTargetBuilder();
        _fileReader = fileReader ?? new TempLogFileReader();
        _pipeline = pipeline ?? new CollectorPipeline();
        _sessionManager = sessionManager ?? new SessionManager();
        _stateStore = stateStore ?? new StateStore();
        _statsStore = statsStore ?? new StatsStore();
        _canonicalWriter = canonicalWriter
            ?? new CanonicalRecordJsonlWriter();
        _clock = clock ?? (() => DateTimeOffset.Now);
    }

    public CollectionResult Run(CollectorConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentException.ThrowIfNullOrWhiteSpace(config.TempDir);
        ArgumentException.ThrowIfNullOrWhiteSpace(config.OutputDir);

        var startedAt = _clock();
        var targets = _targetBuilder.Build(config.TempDir, config);
        var session = _sessionManager.Create(
            config,
            targets.Select(
                target => Path.GetFileName(target)
                    ?? throw new InvalidOperationException(
                        $"監視対象のファイル名を取得できません: {target}")),
            CollectorVersion,
            startedAt);
        var sessionDirectory = Path.Combine(
            config.OutputDir,
            session.SessionId);
        var result = new CollectionResult
        {
            SessionId = session.SessionId,
            SessionDirectory = sessionDirectory,
            TargetFiles = targets.Count,
        };

        Directory.CreateDirectory(sessionDirectory);
        _sessionManager.Save(sessionDirectory, session);

        var state = new CollectorState
        {
            SessionId = session.SessionId,
            UpdatedAt = startedAt,
        };
        var stats = new CollectorStats();
        var rawDeduplicator = new RawDeduplicator(
            state.SeenRawRecordIds);
        var canonicalDeduplicator = new CanonicalDeduplicator(
            lastOrder: state.LastOrder);
        var processingTargets = OrderTargetsByLastWriteTime(targets);

        try
        {
            foreach (var target in processingTargets)
            {
                ProcessTarget(
                    target,
                    config,
                    sessionDirectory,
                    session.SessionId,
                    rawDeduplicator,
                    canonicalDeduplicator,
                    state,
                    stats,
                    result);
            }

            if (config.CanonicalOutput)
            {
                _canonicalWriter.WriteAll(
                    sessionDirectory,
                    canonicalDeduplicator.Records);
                stats.CanonicalRecordsWritten =
                    canonicalDeduplicator.Records.Count;
            }

            state.SeenRawRecordIds =
            [
                .. rawDeduplicator.SeenRawRecordIds,
            ];
            state.SeenCanonicalKeys =
            [
                .. canonicalDeduplicator.Records.Select(
                    record => record.CanonicalKey),
            ];
            state.LastOrder = canonicalDeduplicator.LastOrder;
            state.UpdatedAt = _clock();

            _stateStore.Save(sessionDirectory, state);
            _statsStore.Save(sessionDirectory, stats);
            _sessionManager.Complete(sessionDirectory, _clock());

            result.RawRecordsWritten = stats.RawRecordsWritten;
            result.CanonicalRecordsWritten =
                stats.CanonicalRecordsWritten;
            result.ParseErrors = stats.ParseErrors;

            return result;
        }
        catch
        {
            _sessionManager.Abort(sessionDirectory, _clock());
            throw;
        }
    }

    private static IReadOnlyList<string> OrderTargetsByLastWriteTime(
        IEnumerable<string> targets)
    {
        return
        [
            .. targets
                .Select(
                    target => new
                    {
                        Path = target,
                        LastWriteTime = GetLastWriteTime(target),
                    })
                .OrderBy(item => item.LastWriteTime)
                .ThenBy(
                    item => Path.GetFileName(item.Path),
                    StringComparer.Ordinal)
                .Select(item => item.Path),
        ];
    }

    private static DateTimeOffset GetLastWriteTime(string path)
    {
        try
        {
            return File.Exists(path)
                ? File.GetLastWriteTimeUtc(path)
                : DateTimeOffset.MaxValue;
        }
        catch
        {
            // 読み取りエラーは既存のファイル読込処理で記録します。
            return DateTimeOffset.MaxValue;
        }
    }

    private void ProcessTarget(
        string target,
        CollectorConfig config,
        string sessionDirectory,
        string sessionId,
        RawDeduplicator rawDeduplicator,
        CanonicalDeduplicator canonicalDeduplicator,
        CollectorState state,
        CollectorStats stats,
        CollectionResult result)
    {
        var readResult = _fileReader.Read(target);
        var fileName = Path.GetFileName(target);

        if (!readResult.Exists)
        {
            result.MissingFiles++;
            state.Files[fileName] = new CollectorFileState();
            return;
        }

        if (readResult.Error is not null
            || readResult.Snapshot is null)
        {
            result.FileReadErrors++;
            result.Errors.Add(
                readResult.Error
                ?? $"ログファイルの読込結果が不正です: {target}");
            state.Files[fileName] = new CollectorFileState
            {
                Exists = true,
            };
            return;
        }

        var snapshot = readResult.Snapshot;
        result.FilesRead++;
        state.Files[fileName] = new CollectorFileState
        {
            Exists = true,
            LastWriteTime = snapshot.LastWriteTime,
            FileSize = snapshot.FileSize,
            FileHash = snapshot.FileHash,
        };

        _pipeline.Process(
            snapshot,
            sessionId,
            sessionDirectory,
            config,
            rawDeduplicator,
            canonicalDeduplicator,
            stats,
            _clock());
    }
}
