namespace FfxiTempLogCollector.Core;

public sealed class CollectorEvents
{
    public event EventHandler<CollectorStatusSnapshot>? StatusChanged;

    public event EventHandler<string>? LogLevelChanged;

    public event EventHandler<CanonicalSnapshot>? CanonicalSnapshotChanged;

    internal void RaiseStatusChanged(CollectorStatusSnapshot snapshot)
    {
        StatusChanged?.Invoke(this, snapshot);
    }

    internal void RaiseLogLevelChanged(string logLevel)
    {
        LogLevelChanged?.Invoke(this, logLevel);
    }

    internal void RaiseCanonicalSnapshotChanged(CanonicalSnapshot snapshot)
    {
        CanonicalSnapshotChanged?.Invoke(this, snapshot);
    }
}
