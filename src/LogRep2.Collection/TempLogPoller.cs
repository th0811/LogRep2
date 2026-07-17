namespace FfxiTempLogCollector.Core;

public sealed class TempLogPoller
{
    private readonly TempLogFileReader _fileReader;
    private readonly FileChangeDetector _changeDetector;
    private readonly FileSnapshotStore _snapshotStore;

    public TempLogPoller(
        TempLogFileReader? fileReader = null,
        FileChangeDetector? changeDetector = null,
        FileSnapshotStore? snapshotStore = null)
    {
        _fileReader = fileReader ?? new TempLogFileReader();
        _changeDetector = changeDetector ?? new FileChangeDetector();
        _snapshotStore = snapshotStore ?? new FileSnapshotStore();
    }

    public FileSnapshotStore SnapshotStore => _snapshotStore;

    public PollingResult Poll(IEnumerable<string> targets)
    {
        ArgumentNullException.ThrowIfNull(targets);

        var result = new PollingResult();

        foreach (var target in targets)
        {
            PollTarget(target, result);
        }

        result.ChangedFiles.Sort(CompareSnapshots);
        return result;
    }

    private static int CompareSnapshots(
        FileSnapshot left,
        FileSnapshot right)
    {
        var timeComparison =
            left.LastWriteTime.CompareTo(right.LastWriteTime);

        if (timeComparison != 0)
        {
            return timeComparison;
        }

        var fileNameComparison = StringComparer.Ordinal.Compare(
            left.FileName,
            right.FileName);

        return fileNameComparison != 0
            ? fileNameComparison
            : StringComparer.Ordinal.Compare(left.Path, right.Path);
    }

    private void PollTarget(string target, PollingResult result)
    {
        var currentMetadata = ReadMetadata(target);
        _snapshotStore.TryGet(target, out var previous);

        if (!currentMetadata.Exists)
        {
            if (_changeDetector.HasChanged(previous, currentMetadata))
            {
                result.MissingFiles.Add(target);
            }

            _snapshotStore.Set(target, currentMetadata);
            return;
        }

        if (!_changeDetector.HasChanged(previous, currentMetadata))
        {
            return;
        }

        var readResult = _fileReader.Read(target);

        if (readResult.Error is not null
            || readResult.Snapshot is null)
        {
            result.Errors.Add(
                readResult.Error
                ?? $"ログファイルの読込結果が不正です: {target}");
            return;
        }

        var snapshot = readResult.Snapshot;
        var completedState = new FileSnapshotState
        {
            Exists = true,
            LastWriteTime = snapshot.LastWriteTime,
            FileSize = snapshot.FileSize,
            FileHash = snapshot.FileHash,
        };

        if (_changeDetector.HasChanged(previous, completedState))
        {
            result.ChangedFiles.Add(snapshot);
        }

        _snapshotStore.Set(target, completedState);
    }

    private static FileSnapshotState ReadMetadata(string path)
    {
        if (!File.Exists(path))
        {
            return new FileSnapshotState();
        }

        var fileInfo = new FileInfo(path);

        return new FileSnapshotState
        {
            Exists = true,
            LastWriteTime = fileInfo.LastWriteTimeUtc,
            FileSize = fileInfo.Length,
        };
    }
}
