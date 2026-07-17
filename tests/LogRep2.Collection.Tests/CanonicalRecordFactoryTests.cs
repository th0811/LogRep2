using FfxiTempLogCollector.Core;

namespace FfxiTempLogCollector.Tests;

public sealed class CanonicalRecordFactoryTests
{
    [Fact]
    public void RawRecordからCanonicalRecordを生成できる()
    {
        var rawRecord = RawRecordTestData.Create();

        var actual = new CanonicalRecordFactory().Create(rawRecord, 1);

        Assert.Equal(SchemaVersions.CanonicalRecord, actual.SchemaVersion);
        Assert.Equal(1, actual.Order);
        Assert.Equal(rawRecord.FirstSeenAt, actual.FirstSeenAt);
        Assert.Equal(rawRecord.FirstSeenAt, actual.LastSeenAt);
        Assert.Equal([1], actual.SourceWindows);
        Assert.Equal(["1_0.log"], actual.SourceFiles);
        Assert.Equal(["raw-1"], actual.SourceRawRecordIds);
        Assert.Equal("10", actual.SequenceHintMin);
        Assert.Equal("10", actual.SequenceHintMax);
        Assert.Equal(actual.CanonicalKey, actual.CanonicalRecordId);
        Assert.Equal(40, actual.CanonicalKey.Length);
    }

    [Fact]
    public void 出所が違っても本文とイベント情報が同じならKeyが一致する()
    {
        var factory = new CanonicalRecordFactory();
        var window1 = RawRecordTestData.Create(
            rawRecordId: "raw-1",
            windowId: 1,
            sourceFile: "1_0.log");
        var window2 = RawRecordTestData.Create(
            rawRecordId: "raw-2",
            windowId: 2,
            sourceFile: "2_19.log");

        var firstKey = factory.CreateCanonicalKey(window1);
        var secondKey = factory.CreateCanonicalKey(window2);

        Assert.Equal(firstKey, secondKey);
    }

    [Fact]
    public void MessageTokenCountが違ってもKeyは一致する()
    {
        var factory = new CanonicalRecordFactory();
        var first = RawRecordTestData.Create(messageTokenCount: "00000005");
        var second = RawRecordTestData.Create(messageTokenCount: "00000006");

        var firstKey = factory.CreateCanonicalKey(first);
        var secondKey = factory.CreateCanonicalKey(second);

        Assert.Equal(firstKey, secondKey);
    }
}
