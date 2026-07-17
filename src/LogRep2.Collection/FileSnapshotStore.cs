namespace FfxiTempLogCollector.Core;

public sealed class FileSnapshotStore
{
    private readonly Dictionary<string, FileSnapshotState> _states =
        new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyDictionary<string, FileSnapshotState> States =>
        _states;

    public bool TryGet(
        string path,
        out FileSnapshotState? state)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        return _states.TryGetValue(Path.GetFullPath(path), out state);
    }

    public void Set(string path, FileSnapshotState state)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(state);

        _states[Path.GetFullPath(path)] = state;
    }
}
