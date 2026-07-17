namespace FfxiTempLogCollector.Core;

public sealed class PollingCollectionRunner
{
    private const string CollectorVersion = "1.0.0";

    private readonly TempLogWatchTargetBuilder _targetBuilder;
    private readonly TempLogPoller _poller;
    private readonly CollectorPipeline _pipeline;
    private readonly SessionManager _sessionManager;
    private readonly StateStore _stateStore;
    private readonly StatsStore _statsStore;
    private readonly CanonicalRecordJsonlWriter _canonicalWriter;
    private readonly Func<DateTimeOffset> _clock;

    public PollingCollectionRunner(
        TempLogWatchTargetBuilder? targetBuilder = null,
        TempLogPoller? poller = null,
        CollectorPipeline? pipeline = null,
        SessionManager? sessionManager = null,
        StateStore? stateStore = null,
        StatsStore? statsStore = null,
        CanonicalRecordJsonlWriter? canonicalWriter = null,
        Func<DateTimeOffset>? clock = null)
    {
        _targetBuilder = targetBuilder ?? new TempLogWatchTargetBuilder();
        _poller = poller ?? new TempLogPoller();
        _pipeline = pipeline ?? new CollectorPipeline();
        _sessionManager = sessionManager ?? new SessionManager();
        _stateStore = stateStore ?? new StateStore();
        _statsStore = statsStore ?? new StatsStore();
        _canonicalWriter = canonicalWriter
            ?? new CanonicalRecordJsonlWriter();
        _clock = clock ?? (() => DateTimeOffset.Now);
    }

    public async Task<PollingCollectionResult> RunAsync(
        CollectorConfig config,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentException.ThrowIfNullOrWhiteSpace(config.TempDir);
        ArgumentException.ThrowIfNullOrWhiteSpace(config.OutputDir);

        var options = new PollingOptions
        {
            IntervalMs = config.PollingIntervalMs,
        };

        return await RunAsync(
            config,
            options,
            cancellationToken).ConfigureAwait(false);
    }

    public async Task<PollingCollectionResult> RunAsync(
        CollectorConfig config,
        PollingOptions options,
        CancellationToken cancellationToken)
    {
        return await RunAsync(
            config,
            options,
            sessionStarted: null,
            progressChanged: null,
            canonicalSnapshotChanged: null,
            cancellationToken).ConfigureAwait(false);
    }

    public async Task<PollingCollectionResult> RunAsync(
        CollectorConfig config,
        PollingOptions options,
        Action<string, string>? sessionStarted,
        Action<CollectorStats, IReadOnlyList<string>>? progressChanged,
        Action<CanonicalSnapshot>? canonicalSnapshotChanged,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(options);
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
        var result = new PollingCollectionResult
        {
            SessionId = session.SessionId,
            SessionDirectory = sessionDirectory,
        };

        Directory.CreateDirectory(sessionDirectory);
        _sessionManager.Save(sessionDirectory, session);
        sessionStarted?.Invoke(session.SessionId, sessionDirectory);

        var state = new CollectorState
        {
            SessionId = session.SessionId,
            UpdatedAt = startedAt,
        };
        var stats = new CollectorStats();
        var rawDeduplicator = new RawDeduplicator();
        var canonicalDeduplicator = new CanonicalDeduplicator();

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var pollingResult = _poller.Poll(targets);
                result.PollCount++;
                result.Errors.AddRange(pollingResult.Errors);

                foreach (var snapshot in pollingResult.ChangedFiles)
                {
                    _pipeline.Process(
                        snapshot,
                        session.SessionId,
                        sessionDirectory,
                        config,
                        rawDeduplicator,
                        canonicalDeduplicator,
                        stats,
                        _clock());
                    result.FilesProcessed++;
                }

                UpdateState(state, rawDeduplicator, canonicalDeduplicator);
                canonicalSnapshotChanged?.Invoke(
                    CreateCanonicalSnapshot(
                        session.SessionId,
                        canonicalDeduplicator));
                SaveProgress(
                    sessionDirectory,
                    config,
                    state,
                    stats,
                    canonicalDeduplicator);
                progressChanged?.Invoke(
                    CopyStats(stats),
                    result.Errors.ToArray());

                await Task.Delay(
                        options.IntervalMs,
                        cancellationToken)
                    .ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
        }
        catch
        {
            _sessionManager.Abort(sessionDirectory, _clock());
            throw;
        }

        UpdateState(state, rawDeduplicator, canonicalDeduplicator);
        canonicalSnapshotChanged?.Invoke(
            CreateCanonicalSnapshot(
                session.SessionId,
                canonicalDeduplicator));
        SaveProgress(
            sessionDirectory,
            config,
            state,
            stats,
            canonicalDeduplicator);
        progressChanged?.Invoke(
            CopyStats(stats),
            result.Errors.ToArray());
        _sessionManager.Complete(sessionDirectory, _clock());

        result.RawRecordsWritten = stats.RawRecordsWritten;
        result.CanonicalRecordsWritten = stats.CanonicalRecordsWritten;

        return result;
    }

    private CanonicalSnapshot CreateCanonicalSnapshot(
        string sessionId,
        CanonicalDeduplicator canonicalDeduplicator)
    {
        return new CanonicalSnapshot(
            sessionId,
            canonicalDeduplicator.Records,
            _clock());
    }

    private static CollectorStats CopyStats(CollectorStats stats)
    {
        return new CollectorStats
        {
            RawRecordsWritten = stats.RawRecordsWritten,
            CanonicalRecordsWritten = stats.CanonicalRecordsWritten,
            DuplicateRawRecordsSkipped =
                stats.DuplicateRawRecordsSkipped,
            DuplicateCanonicalRecordsSkipped =
                stats.DuplicateCanonicalRecordsSkipped,
            ParseErrors = stats.ParseErrors,
            DecodeErrors = stats.DecodeErrors,
            GapWarnings = stats.GapWarnings,
            LastSeenAt = stats.LastSeenAt,
        };
    }

    private void SaveProgress(
        string sessionDirectory,
        CollectorConfig config,
        CollectorState state,
        CollectorStats stats,
        CanonicalDeduplicator canonicalDeduplicator)
    {
        if (config.CanonicalOutput)
        {
            _canonicalWriter.WriteAll(
                sessionDirectory,
                canonicalDeduplicator.Records);
            stats.CanonicalRecordsWritten =
                canonicalDeduplicator.Records.Count;
        }

        CopySnapshotStates(state);
        state.UpdatedAt = _clock();
        _stateStore.Save(sessionDirectory, state);
        _statsStore.Save(sessionDirectory, stats);
    }

    private void CopySnapshotStates(CollectorState state)
    {
        foreach (var pair in _poller.SnapshotStore.States)
        {
            var fileName = Path.GetFileName(pair.Key);
            var snapshotState = pair.Value;
            state.Files[fileName] = new CollectorFileState
            {
                Exists = snapshotState.Exists,
                LastWriteTime = snapshotState.LastWriteTime,
                FileSize = snapshotState.FileSize,
                FileHash = snapshotState.FileHash,
            };
        }
    }

    private static void UpdateState(
        CollectorState state,
        RawDeduplicator rawDeduplicator,
        CanonicalDeduplicator canonicalDeduplicator)
    {
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
    }
}
