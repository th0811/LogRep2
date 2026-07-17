using FfxiTempLogCollector.Core;

namespace FfxiTempLogCollector.Tests;

public sealed class CanonicalDeduplicatorTests
{
    [Fact]
    public void 同じCanonicalKeyなら出所と範囲を統合する()
    {
        var firstSeenAt = new DateTimeOffset(
            2026,
            6,
            23,
            21,
            30,
            0,
            TimeSpan.FromHours(9));
        var deduplicator = new CanonicalDeduplicator();
        var first = RawRecordTestData.Create(
            rawRecordId: "raw-1",
            windowId: 1,
            sourceFile: "1_0.log",
            sequenceHint: "10",
            firstSeenAt: firstSeenAt);
        var second = RawRecordTestData.Create(
            rawRecordId: "raw-2",
            windowId: 2,
            sourceFile: "2_0.log",
            sequenceHint: "20",
            firstSeenAt: firstSeenAt.AddSeconds(1));

        var initial = deduplicator.AddOrMerge(first);
        var merged = deduplicator.AddOrMerge(second);

        Assert.Same(initial, merged);
        Assert.Single(deduplicator.Records);
        Assert.Equal([1, 2], merged.SourceWindows);
        Assert.Equal(["1_0.log", "2_0.log"], merged.SourceFiles);
        Assert.Equal(["raw-1", "raw-2"], merged.SourceRawRecordIds);
        Assert.Equal(firstSeenAt.AddSeconds(1), merged.LastSeenAt);
        Assert.Equal("10", merged.SequenceHintMin);
        Assert.Equal("20", merged.SequenceHintMax);
        Assert.Equal(1, merged.Order);
    }

    [Fact]
    public void 新規CanonicalRecordのOrderが単調増加する()
    {
        var deduplicator = new CanonicalDeduplicator(lastOrder: 5);

        var first = deduplicator.AddOrMerge(
            RawRecordTestData.Create(
                rawRecordId: "raw-1",
                visibleText: "メッセージ1"));
        var second = deduplicator.AddOrMerge(
            RawRecordTestData.Create(
                rawRecordId: "raw-2",
                visibleText: "メッセージ2"));

        Assert.Equal(6, first.Order);
        Assert.Equal(7, second.Order);
        Assert.Equal(7, deduplicator.LastOrder);
    }

    [Fact]
    public void 数値SequenceHintを数値として比較する()
    {
        var deduplicator = new CanonicalDeduplicator();
        deduplicator.AddOrMerge(
            RawRecordTestData.Create(
                rawRecordId: "raw-1",
                sequenceHint: "9"));
        var merged = deduplicator.AddOrMerge(
            RawRecordTestData.Create(
                rawRecordId: "raw-2",
                sequenceHint: "10"));

        Assert.Equal("9", merged.SequenceHintMin);
        Assert.Equal("10", merged.SequenceHintMax);
    }

    [Fact]
    public void SequenceHintが片方だけ存在する場合は既知値を保持する()
    {
        var deduplicator = new CanonicalDeduplicator();
        deduplicator.AddOrMerge(
            RawRecordTestData.Create(
                rawRecordId: "raw-1",
                sequenceHint: null));
        var merged = deduplicator.AddOrMerge(
            RawRecordTestData.Create(
                rawRecordId: "raw-2",
                sequenceHint: "15"));

        Assert.Equal("15", merged.SequenceHintMin);
        Assert.Equal("15", merged.SequenceHintMax);
    }
}
