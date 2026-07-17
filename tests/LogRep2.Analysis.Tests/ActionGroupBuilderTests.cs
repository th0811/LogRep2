using FFXI_LogAnalyzer.Core;

namespace FFXI_LogAnalyzer.Tests;

public class ActionGroupBuilderTests
{
    [Fact]
    public void Build_GroupsBySessionIdAndEventGroup()
    {
        var records = new[]
        {
            CreateRecord("record-1", "session-1", "event-1", order: 1),
            CreateRecord("record-2", "session-1", "event-1", order: 2),
            CreateRecord("record-3", "session-1", "event-2", order: 3)
        };

        var groups = new ActionGroupBuilder().Build(records);

        Assert.Equal(2, groups.Count);
        var firstGroup = Assert.Single(groups, group => group.ActionGroupKey == "session-1:event-1");
        Assert.Equal(["record-1", "record-2"], firstGroup.Records.Select(record => record.Record.CanonicalRecordId));
    }

    [Fact]
    public void Build_SeparatesGroupsWhenSessionIdDiffers()
    {
        var records = new[]
        {
            CreateRecord("record-1", "session-1", "event-1", order: 1),
            CreateRecord("record-2", "session-2", "event-1", order: 2)
        };

        var groups = new ActionGroupBuilder().Build(records);

        Assert.Equal(["session-1:event-1", "session-2:event-1"], groups.Select(group => group.ActionGroupKey));
    }

    [Fact]
    public void Build_SortsRecordsBySequenceHint()
    {
        var records = new[]
        {
            CreateRecord("second", "session-1", "event-1", order: 1, sequenceHintMin: 200),
            CreateRecord("first", "session-1", "event-1", order: 2, sequenceHintMin: 100)
        };

        var group = Assert.Single(new ActionGroupBuilder().Build(records));

        Assert.Equal(["first", "second"], group.Records.Select(record => record.Record.CanonicalRecordId));
    }

    [Fact]
    public void Build_SortsRecordsByOrderWhenSequenceHintIsMissing()
    {
        var records = new[]
        {
            CreateRecord("second", "session-1", "event-1", order: 20),
            CreateRecord("first", "session-1", "event-1", order: 10)
        };

        var group = Assert.Single(new ActionGroupBuilder().Build(records));

        Assert.Equal(["first", "second"], group.Records.Select(record => record.Record.CanonicalRecordId));
    }

    [Fact]
    public void Build_GetsOrderMinAndOrderMax()
    {
        var records = new[]
        {
            CreateRecord("middle", "session-1", "event-1", order: 20),
            CreateRecord("min", "session-1", "event-1", order: 10),
            CreateRecord("max", "session-1", "event-1", order: 30)
        };

        var group = Assert.Single(new ActionGroupBuilder().Build(records));

        Assert.Equal(10, group.OrderMin);
        Assert.Equal(30, group.OrderMax);
    }

    [Fact]
    public void Build_ReturnsVisibleTextsInRecordOrder()
    {
        var records = new[]
        {
            CreateRecord("record-2", "session-1", "event-1", order: 2, visibleText: "結果ログ"),
            CreateRecord("record-1", "session-1", "event-1", order: 1, visibleText: "開始ログ")
        };

        var group = Assert.Single(new ActionGroupBuilder().Build(records));

        Assert.Equal(["開始ログ", "結果ログ"], group.VisibleTexts);
    }

    private static CanonicalRecord CreateRecord(
        string id,
        string sessionId,
        string eventGroup,
        long order,
        long? sequenceHintMin = null,
        string? visibleText = null)
    {
        return new CanonicalRecord
        {
            CanonicalRecordId = id,
            SessionId = sessionId,
            EventGroup = eventGroup,
            Order = order,
            SequenceHintMin = sequenceHintMin,
            VisibleText = visibleText ?? id
        };
    }
}
