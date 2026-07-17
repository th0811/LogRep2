namespace FfxiTempLogCollector.Core;

public sealed class CollectorService : IAsyncDisposable
{
    private readonly PollingCollectionRunner _pollingRunner;
    private readonly OnceCollectionRunner _onceRunner;
    private readonly SemaphoreSlim _operationLock = new(1, 1);
    private readonly object _stateLock = new();

    private CollectorStatus _status = CollectorStatus.Stopped;
    private PollingOptions _pollingOptions = new();
    private CancellationTokenSource? _collectionCancellation;
    private Task<PollingCollectionResult>? _collectionTask;
    private string? _sessionId;
    private string? _sessionDirectory;
    private CollectorStats _stats = new();
    private List<string> _errors = [];
    private string _logLevel = "info";
    private bool _disposed;

    public CollectorService(
        PollingCollectionRunner? pollingRunner = null,
        OnceCollectionRunner? onceRunner = null)
    {
        _pollingRunner = pollingRunner ?? new PollingCollectionRunner();
        _onceRunner = onceRunner ?? new OnceCollectionRunner();
    }

    public CollectorEvents Events { get; } = new();

    public async Task<bool> StartAsync(
        CollectorStartRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ThrowIfDisposed();

        await _operationLock.WaitAsync(cancellationToken)
            .ConfigureAwait(false);

        try
        {
            if (GetStatus().Status is CollectorStatus.Starting
                or CollectorStatus.Running
                or CollectorStatus.Stopping)
            {
                return false;
            }

            if (!TryValidateConfig(request.Config, out var error))
            {
                SetError(error);
                return false;
            }

            var started = new TaskCompletionSource(
                TaskCreationOptions.RunContinuationsAsynchronously);
            var cancellation = new CancellationTokenSource();
            var options = new PollingOptions
            {
                IntervalMs = request.Config.PollingIntervalMs,
            };

            lock (_stateLock)
            {
                _status = CollectorStatus.Starting;
                _sessionId = null;
                _sessionDirectory = null;
                _stats = new CollectorStats();
                _errors = [];
                _logLevel = request.Config.LogLevel;
                _pollingOptions = options;
                _collectionCancellation = cancellation;
            }

            RaiseStatusChanged();

            var collectionTask = RunPollingAsync(
                request.Config,
                options,
                cancellation,
                started);
            _collectionTask = collectionTask;

            try
            {
                await started.Task.WaitAsync(cancellationToken)
                    .ConfigureAwait(false);
                return true;
            }
            catch
            {
                cancellation.Cancel();

                try
                {
                    await collectionTask.ConfigureAwait(false);
                }
                catch
                {
                }

                return false;
            }
        }
        finally
        {
            _operationLock.Release();
        }
    }

    public async Task StopAsync(
        CollectorStopRequest? request = null,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        await _operationLock.WaitAsync(cancellationToken)
            .ConfigureAwait(false);

        try
        {
            CancellationTokenSource? cancellation;
            Task<PollingCollectionResult>? collectionTask;

            lock (_stateLock)
            {
                if (_status == CollectorStatus.Stopped)
                {
                    return;
                }

                if (_status == CollectorStatus.Error
                    && _collectionTask is null)
                {
                    return;
                }

                _status = CollectorStatus.Stopping;
                cancellation = _collectionCancellation;
                collectionTask = _collectionTask;
            }

            RaiseStatusChanged();
            cancellation?.Cancel();

            if (collectionTask is not null)
            {
                await collectionTask.WaitAsync(cancellationToken)
                    .ConfigureAwait(false);
            }
        }
        finally
        {
            _operationLock.Release();
        }
    }

    public CollectionResult RunOnce(CollectorStartRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ThrowIfDisposed();
        _operationLock.Wait();

        try
        {
            if (!TryValidateConfig(request.Config, out var error))
            {
                SetError(error);
                throw new InvalidOperationException(error);
            }

            lock (_stateLock)
            {
                if (_status is CollectorStatus.Starting
                    or CollectorStatus.Running
                    or CollectorStatus.Stopping)
                {
                    throw new InvalidOperationException(
                        "ログ収集中はonce実行できません。");
                }

                _status = CollectorStatus.Starting;
                _logLevel = request.Config.LogLevel;
                _errors = [];
                _stats = new CollectorStats();
            }

            RaiseStatusChanged();

            try
            {
                var result = _onceRunner.Run(request.Config);

                lock (_stateLock)
                {
                    _sessionId = result.SessionId;
                    _sessionDirectory = result.SessionDirectory;
                    _stats = new StatsStore().Load(
                        result.SessionDirectory);
                    _errors = [.. result.Errors];
                    _status = CollectorStatus.Stopped;
                }

                RaiseStatusChanged();
                return result;
            }
            catch (Exception exception)
            {
                SetError(exception.Message);
                throw;
            }
        }
        finally
        {
            _operationLock.Release();
        }
    }

    public CollectorStatusSnapshot GetStatus()
    {
        lock (_stateLock)
        {
            return CreateSnapshot();
        }
    }

    public void UpdatePollingInterval(int intervalMs)
    {
        ThrowIfDisposed();
        _pollingOptions.IntervalMs = intervalMs;
        RaiseStatusChanged();
    }

    public void UpdateLogLevel(string logLevel)
    {
        ThrowIfDisposed();

        if (string.IsNullOrWhiteSpace(logLevel))
        {
            throw new ArgumentException(
                "ログレベルを指定してください。",
                nameof(logLevel));
        }

        lock (_stateLock)
        {
            _logLevel = logLevel;
        }

        Events.RaiseLogLevelChanged(logLevel);
        RaiseStatusChanged();
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        await StopAsync().ConfigureAwait(false);
        _disposed = true;
        _collectionCancellation?.Dispose();
        _operationLock.Dispose();
    }

    private async Task<PollingCollectionResult> RunPollingAsync(
        CollectorConfig config,
        PollingOptions options,
        CancellationTokenSource cancellation,
        TaskCompletionSource started)
    {
        try
        {
            var result = await _pollingRunner.RunAsync(
                    config,
                    options,
                    (sessionId, sessionDirectory) =>
                    {
                        lock (_stateLock)
                        {
                            _sessionId = sessionId;
                            _sessionDirectory = sessionDirectory;
                            _status = CollectorStatus.Running;
                        }

                        RaiseStatusChanged();
                        started.TrySetResult();
                    },
                    UpdateProgress,
                    Events.RaiseCanonicalSnapshotChanged,
                    cancellation.Token)
                .ConfigureAwait(false);

            lock (_stateLock)
            {
                _errors = [.. result.Errors];
                _status = CollectorStatus.Stopped;
                _collectionTask = null;
                _collectionCancellation = null;
            }

            RaiseStatusChanged();
            return result;
        }
        catch (Exception exception)
        {
            started.TrySetException(exception);
            SetError(exception.Message);
            throw;
        }
        finally
        {
            cancellation.Dispose();
        }
    }

    private void UpdateProgress(
        CollectorStats stats,
        IReadOnlyList<string> errors)
    {
        lock (_stateLock)
        {
            _stats = stats;
            _errors = [.. errors];
        }

        RaiseStatusChanged();
    }

    private void SetError(string error)
    {
        lock (_stateLock)
        {
            _status = CollectorStatus.Error;
            _errors.Add(error);
            _collectionTask = null;
            _collectionCancellation = null;
        }

        RaiseStatusChanged();
    }

    private void RaiseStatusChanged()
    {
        Events.RaiseStatusChanged(GetStatus());
    }

    private CollectorStatusSnapshot CreateSnapshot()
    {
        return new CollectorStatusSnapshot
        {
            Status = _status,
            SessionId = _sessionId,
            SessionDirectory = _sessionDirectory,
            RawRecordsWritten = _stats.RawRecordsWritten,
            CanonicalRecordsWritten = _stats.CanonicalRecordsWritten,
            LastSeenAt = _stats.LastSeenAt,
            WarningCount = _stats.GapWarnings,
            ErrorCount = _stats.ParseErrors
                + _stats.DecodeErrors
                + _errors.Count,
            LastError = _errors.LastOrDefault(),
            PollingIntervalMs = _pollingOptions.IntervalMs,
            LogLevel = _logLevel,
        };
    }

    private static bool TryValidateConfig(
        CollectorConfig config,
        out string error)
    {
        if (config is null)
        {
            error = "設定が指定されていません。";
            return false;
        }

        if (string.IsNullOrWhiteSpace(config.TempDir)
            || !Directory.Exists(config.TempDir))
        {
            error = "TEMPフォルダーが存在しません。";
            return false;
        }

        if (string.IsNullOrWhiteSpace(config.OutputDir))
        {
            error = "出力先フォルダーを指定してください。";
            return false;
        }

        if (!config.WatchWindow1 && !config.WatchWindow2)
        {
            error = "監視するログウィンドウを1つ以上選択してください。";
            return false;
        }

        if (config.RotationSlots is < 1 or > 20)
        {
            error = "ローテーションスロット数は1から20で指定してください。";
            return false;
        }

        if (config.PollingIntervalMs
            is < PollingOptions.MinimumIntervalMs
            or > PollingOptions.MaximumIntervalMs)
        {
            error = $"ポーリング間隔は{PollingOptions.MinimumIntervalMs}から"
                + $"{PollingOptions.MaximumIntervalMs}ミリ秒で指定してください。";
            return false;
        }

        error = string.Empty;
        return true;
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }
}
