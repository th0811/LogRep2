using System.Diagnostics;
using FFXI_LogAnalyzer.Core;
using FfxiTempLogCollector.Core;

namespace FfxiTempLogCollector.App;

public enum RealtimeAnalysisState
{
    Stopped,
    Running,
    Completed,
}

public sealed record RealtimeAnalysisSnapshot(
    RealtimeAnalysisState State,
    AnalysisResult? Result,
    int CanonicalRecordCount,
    int TargetRecordCount,
    TimeSpan LastAggregationTime,
    long DiscardedAggregationCount,
    long ProcessMemoryBytes,
    DateTimeOffset? LastUpdatedAt,
    string? ErrorMessage);

public sealed class RealtimeAnalysisController : IAsyncDisposable
{
    private readonly CollectorEvents _events;
    private readonly RealtimeAnalysisEngine _engine;
    private readonly TimeSpan _refreshInterval;
    private readonly object _sync = new();
    private CanonicalSnapshot? _latestSnapshot;
    private CancellationTokenSource? _debounceCancellation;
    private Task _pendingTask = Task.CompletedTask;
    private RealtimeAnalysisState _state;
    private string? _sessionId;
    private int _startIndex;
    private int? _endIndex;
    private long _generation;
    private long _discardedCount;
    private RealtimeAnalysisSnapshot _current;
    private bool _disposed;

    public RealtimeAnalysisController(
        CollectorEvents events,
        int refreshIntervalMs,
        RealtimeAnalysisEngine? engine = null)
    {
        _events = events ?? throw new ArgumentNullException(nameof(events));
        _engine = engine ?? new RealtimeAnalysisEngine();
        _refreshInterval = TimeSpan.FromMilliseconds(
            Math.Clamp(refreshIntervalMs, 250, 1000));
        _current = CreateSnapshot(null, 0, TimeSpan.Zero, null, null);
        _events.CanonicalSnapshotChanged += OnCanonicalSnapshotChanged;
    }

    public event EventHandler<RealtimeAnalysisSnapshot>? Updated;

    public RealtimeAnalysisSnapshot Current
    {
        get { lock (_sync) { return _current; } }
    }

    public void Start()
    {
        lock (_sync)
        {
            ThrowIfDisposed();
            _state = RealtimeAnalysisState.Running;
            _sessionId = _latestSnapshot?.SessionId;
            _startIndex = _latestSnapshot?.Records.Count ?? 0;
            _endIndex = null;
            _generation++;
            CancelPendingLocked(countAsDiscarded: false);
            _current = CreateSnapshot(null, 0, TimeSpan.Zero, DateTimeOffset.Now, null);
        }

        RaiseUpdated();
    }

    public void Stop()
    {
        lock (_sync)
        {
            ThrowIfDisposed();
            if (_state != RealtimeAnalysisState.Running)
            {
                return;
            }

            _state = RealtimeAnalysisState.Completed;
            _endIndex = GetApplicableRecordCountLocked();
            ScheduleLocked(TimeSpan.Zero);
        }

        RaiseUpdated();
    }

    public void Reset()
    {
        lock (_sync)
        {
            ThrowIfDisposed();
            if (_state != RealtimeAnalysisState.Running)
            {
                return;
            }

            _startIndex = GetApplicableRecordCountLocked();
            _endIndex = null;
            _generation++;
            CancelPendingLocked(countAsDiscarded: true);
            _current = CreateSnapshot(null, 0, TimeSpan.Zero, DateTimeOffset.Now, null);
        }

        RaiseUpdated();
    }

    public async ValueTask DisposeAsync()
    {
        Task pending;
        lock (_sync)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _events.CanonicalSnapshotChanged -= OnCanonicalSnapshotChanged;
            CancelPendingLocked(countAsDiscarded: false);
            pending = _pendingTask;
        }

        try { await pending.ConfigureAwait(false); }
        catch (OperationCanceledException) { }
    }

    private void OnCanonicalSnapshotChanged(object? sender, CanonicalSnapshot snapshot)
    {
        AcceptSnapshot(snapshot);
    }

    internal void AcceptSnapshot(CanonicalSnapshot snapshot)
    {
        lock (_sync)
        {
            if (_disposed)
            {
                return;
            }

            _latestSnapshot = snapshot;
            if (_state != RealtimeAnalysisState.Running)
            {
                _current = CreateSnapshot(
                    _current.Result,
                    _current.TargetRecordCount,
                    _current.LastAggregationTime,
                    _current.LastUpdatedAt,
                    _current.ErrorMessage);
                return;
            }

            if (_sessionId is null)
            {
                _sessionId = snapshot.SessionId;
                _startIndex = 0;
            }
            else if (!string.Equals(_sessionId, snapshot.SessionId, StringComparison.Ordinal))
            {
                _sessionId = snapshot.SessionId;
                _startIndex = 0;
                _generation++;
            }

            ScheduleLocked(_refreshInterval);
        }
    }

    private void ScheduleLocked(TimeSpan delay)
    {
        CancelPendingLocked(countAsDiscarded: true);
        var cancellation = new CancellationTokenSource();
        _debounceCancellation = cancellation;
        var generation = ++_generation;
        _pendingTask = RunAnalysisAsync(delay, generation, cancellation.Token);
    }

    private async Task RunAnalysisAsync(
        TimeSpan delay,
        long generation,
        CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
            CanonicalSnapshot? source;
            int start;
            int end;
            lock (_sync)
            {
                source = _latestSnapshot;
                start = _startIndex;
                end = _endIndex ?? GetApplicableRecordCountLocked();
            }

            if (source is null)
            {
                return;
            }

            var result = await Task.Run(
                () => _engine.Analyze(source.Records, start, end),
                cancellationToken).ConfigureAwait(false);
            lock (_sync)
            {
                if (_disposed || generation != _generation)
                {
                    _discardedCount++;
                    return;
                }

                _current = CreateSnapshot(
                    result.Result,
                    result.TargetRecordCount,
                    result.Elapsed,
                    DateTimeOffset.Now,
                    null);
            }

            RaiseUpdated();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            lock (_sync)
            {
                _current = CreateSnapshot(
                    _current.Result,
                    _current.TargetRecordCount,
                    _current.LastAggregationTime,
                    DateTimeOffset.Now,
                    $"リアルタイム分析に失敗しました: {exception.Message}");
            }

            RaiseUpdated();
        }
    }

    private int GetApplicableRecordCountLocked()
    {
        return _latestSnapshot is not null
            && string.Equals(_sessionId, _latestSnapshot.SessionId, StringComparison.Ordinal)
                ? _latestSnapshot.Records.Count
                : 0;
    }

    private void CancelPendingLocked(bool countAsDiscarded)
    {
        if (_debounceCancellation is not null)
        {
            if (!_debounceCancellation.IsCancellationRequested && countAsDiscarded)
            {
                _discardedCount++;
            }

            _debounceCancellation.Cancel();
            _debounceCancellation.Dispose();
            _debounceCancellation = null;
        }
    }

    private RealtimeAnalysisSnapshot CreateSnapshot(
        AnalysisResult? result,
        int targetCount,
        TimeSpan elapsed,
        DateTimeOffset? updatedAt,
        string? error)
    {
        return new RealtimeAnalysisSnapshot(
            _state,
            result,
            _latestSnapshot?.Records.Count ?? 0,
            targetCount,
            elapsed,
            _discardedCount,
            Process.GetCurrentProcess().WorkingSet64,
            updatedAt,
            error);
    }

    private void RaiseUpdated()
    {
        Updated?.Invoke(this, Current);
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }
}
