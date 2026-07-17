using FFXI_LogAnalyzer.Core;

namespace FFXI_LogAnalyzer.Tests;

public class SequenceHintComparerTests
{
    [Fact]
    public void Compare_UsesSequenceHintWhenValid()
    {
        var records = new[]
        {
            CreateActionGroupRecord("second", order: 1, sequenceHintMin: 20, readIndex: 0),
            CreateActionGroupRecord("first", order: 2, sequenceHintMin: 10, readIndex: 1)
        };

        var sorted = records.Order(new SequenceHintComparer()).ToArray();

        Assert.Equal(["first", "second"], sorted.Select(record => record.Record.CanonicalRecordId));
    }

    [Fact]
    public void Compare_FallsBackToOrderWhenSequenceHintIsMissing()
    {
        var records = new[]
        {
            CreateActionGroupRecord("second", order: 20, sequenceHintMin: null, readIndex: 0),
            CreateActionGroupRecord("first", order: 10, sequenceHintMin: null, readIndex: 1)
        };

        var sorted = records.Order(new SequenceHintComparer()).ToArray();

        Assert.Equal(["first", "second"], sorted.Select(record => record.Record.CanonicalRecordId));
    }

    [Fact]
    public void Compare_FallsBackToOrderWhenSequenceHintIsInvalid()
    {
        var records = new[]
        {
            CreateActionGroupRecord("second", order: 20, sequenceHintMin: -1, readIndex: 0),
            CreateActionGroupRecord("first", order: 10, sequenceHintMin: -5, readIndex: 1)
        };

        var sorted = records.Order(new SequenceHintComparer()).ToArray();

        Assert.Equal(["first", "second"], sorted.Select(record => record.Record.CanonicalRecordId));
    }

    [Fact]
    public void Compare_UsesReadIndexWhenSortKeysAreSame()
    {
        var records = new[]
        {
            CreateActionGroupRecord("first", order: 10, sequenceHintMin: 1, readIndex: 0),
            CreateActionGroupRecord("second", order: 20, sequenceHintMin: 1, readIndex: 1)
        };

        var sorted = records.Order(new SequenceHintComparer()).ToArray();

        Assert.Equal(["first", "second"], sorted.Select(record => record.Record.CanonicalRecordId));
    }

    private static ActionGroupRecord CreateActionGroupRecord(
        string id,
        long order,
        long? sequenceHintMin,
        int readIndex)
    {
        return new ActionGroupRecord(
            new CanonicalRecord
            {
                CanonicalRecordId = id,
                Order = order,
                SequenceHintMin = sequenceHintMin
            },
            readIndex);
    }
}
