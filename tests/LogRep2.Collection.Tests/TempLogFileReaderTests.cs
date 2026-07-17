using FfxiTempLogCollector.Core;

namespace FfxiTempLogCollector.Tests;

public sealed class TempLogFileReaderTests
{
    [Fact]
    public void 存在しないファイルはエラーにしない()
    {
        using var temporaryDirectory = new TemporaryDirectory();

        var actual = new TempLogFileReader().Read(
            temporaryDirectory.GetPath("1_0.log"));

        Assert.False(actual.Exists);
        Assert.Null(actual.Snapshot);
        Assert.Null(actual.Error);
    }

    [Fact]
    public void 書込中のファイルを共有読込できる()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var path = temporaryDirectory.GetPath("1_0.log");
        var bytes = TempLogTestFileBuilder.Create("共有読込");
        File.WriteAllBytes(path, bytes);

        using var writer = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Write,
            FileShare.ReadWrite | FileShare.Delete);

        var actual = new TempLogFileReader().Read(path);

        Assert.True(actual.Exists);
        Assert.Null(actual.Error);
        Assert.NotNull(actual.Snapshot);
        Assert.Equal(bytes, actual.Snapshot.Content);
        Assert.Equal(bytes.LongLength, actual.Snapshot.FileSize);
        Assert.Equal(HashUtil.ComputeSha1(bytes), actual.Snapshot.FileHash);
    }
}
