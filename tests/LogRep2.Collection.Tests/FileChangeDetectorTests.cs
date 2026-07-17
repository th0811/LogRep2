using FfxiTempLogCollector.Core;

namespace FfxiTempLogCollector.Tests;

public sealed class FileChangeDetectorTests
{
    private readonly FileChangeDetector _detector = new();

    [Fact]
    public void 新規ファイルを変更として検出する()
    {
        var current = CreateState();

        Assert.True(_detector.HasChanged(null, current));
    }

    [Fact]
    public void 更新時刻またはサイズまたはハッシュの変更を検出する()
    {
        var previous = CreateState();

        Assert.True(
            _detector.HasChanged(
                previous,
                CreateState(lastWriteOffsetSeconds: 1)));
        Assert.True(
            _detector.HasChanged(
                previous,
                CreateState(fileSize: 11)));
        Assert.True(
            _detector.HasChanged(
                previous,
                CreateState(fileHash: "changed")));
    }

    [Fact]
    public void 同一状態は変更なしと判定する()
    {
        var previous = CreateState();
        var current = CreateState();

        Assert.False(_detector.HasChanged(previous, current));
    }

    private static FileSnapshotState CreateState(
        int lastWriteOffsetSeconds = 0,
        long fileSize = 10,
        string fileHash = "hash")
    {
        return new FileSnapshotState
        {
            Exists = true,
            LastWriteTime = new DateTimeOffset(
                2026,
                6,
                23,
                21,
                30,
                lastWriteOffsetSeconds,
                TimeSpan.Zero),
            FileSize = fileSize,
            FileHash = fileHash,
        };
    }
}
