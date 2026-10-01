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

    [Theory]
    [InlineData(200L, 100L, "first", "second")]
    [InlineData(null, null, "first", "second")]
    [InlineData(-1L, -5L, "first", "second")]
    [InlineData(1L, 1L, "second", "first")]
    public void 行動内の並び順と元ログとOrder範囲を保持する(
        long? secondHint, long? firstHint, string expectedFirst, string expectedLast)
    {
        var records = new[]
        {
            CreateRecord("second", "session-1", "event-1", order: 20, sequenceHintMin: secondHint),
            CreateRecord("first", "session-1", "event-1", order: 10, sequenceHintMin: firstHint),
        };

        var group = Assert.Single(new ActionGroupBuilder().Build(records));

        Assert.Equal([expectedFirst, expectedLast], group.Records.Select(record => record.Record.CanonicalRecordId));
        Assert.Equal([expectedFirst, expectedLast], group.VisibleTexts);
        Assert.Equal(10, group.OrderMin);
        Assert.Equal(20, group.OrderMax);
    }

    private static CanonicalRecord CreateRecord(
        string id,
        string sessionId,
        string eventGroup,
        long order,
        long? sequenceHintMin = null)
    {
        return new CanonicalRecord
        {
            CanonicalRecordId = id,
            SessionId = sessionId,
            EventGroup = eventGroup,
            Order = order,
            SequenceHintMin = sequenceHintMin,
            VisibleText = id
        };
    }
}
