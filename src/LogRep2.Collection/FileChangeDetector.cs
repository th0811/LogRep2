namespace FfxiTempLogCollector.Core;

public sealed class FileChangeDetector
{
    public bool HasChanged(
        FileSnapshotState? previous,
        FileSnapshotState current)
    {
        ArgumentNullException.ThrowIfNull(current);

        if (previous is null)
        {
            return current.Exists;
        }

        if (previous.Exists != current.Exists)
        {
            return true;
        }

        if (!current.Exists)
        {
            return false;
        }

        return previous.LastWriteTime != current.LastWriteTime
            || previous.FileSize != current.FileSize
            || (!string.IsNullOrEmpty(current.FileHash)
                && !string.Equals(
                    previous.FileHash,
                    current.FileHash,
                    StringComparison.Ordinal));
    }
}
