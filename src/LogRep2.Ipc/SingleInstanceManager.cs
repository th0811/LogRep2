namespace FfxiTempLogCollector.Ipc;

public sealed class SingleInstanceManager : IDisposable
{
    private readonly Mutex _mutex;
    private bool _ownsMutex;
    private bool _disposed;

    public SingleInstanceManager(string? mutexName = null)
    {
        _mutex = new Mutex(
            initiallyOwned: false,
            mutexName ?? IpcModule.MutexName);
    }

    public bool TryAcquire()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_ownsMutex)
        {
            return true;
        }

        try
        {
            _ownsMutex = _mutex.WaitOne(TimeSpan.Zero);
        }
        catch (AbandonedMutexException)
        {
            _ownsMutex = true;
        }

        return _ownsMutex;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        if (_ownsMutex)
        {
            _mutex.ReleaseMutex();
            _ownsMutex = false;
        }

        _mutex.Dispose();
    }
}
