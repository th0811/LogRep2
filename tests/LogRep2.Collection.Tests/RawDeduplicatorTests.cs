using FfxiTempLogCollector.Core;

namespace FfxiTempLogCollector.Tests;

public sealed class RawDeduplicatorTests
{
    [Fact]
    public void 同一RawRecordIdを重複追加しない()
    {
        var deduplicator = new RawDeduplicator();
        var first = RawRecordTestData.Create(rawRecordId: "same-id");
        var second = RawRecordTestData.Create(
            rawRecordId: "same-id",
            windowId: 2);

        Assert.True(deduplicator.TryAdd(first));
        Assert.False(deduplicator.TryAdd(second));
        Assert.Single(deduplicator.SeenRawRecordIds);
    }

    [Fact]
    public void 既存Idを初期値として利用できる()
    {
        var deduplicator = new RawDeduplicator(["existing"]);

        var added = deduplicator.TryAdd(
            RawRecordTestData.Create(rawRecordId: "existing"));

        Assert.False(added);
    }
}
