using LogRep2.Contracts;

namespace FFXI_LogAnalyzer.Core;

public sealed class ActionGroupRecord
{
    public ActionGroupRecord(ICanonicalRecord record, int readIndex)
    {
        Record = record;
        ReadIndex = readIndex;
    }

    public ICanonicalRecord Record { get; }

    public int ReadIndex { get; }

    public long? EffectiveSequenceHint => IsValidSequenceHint(Record.SequenceHintMin)
        ? Record.SequenceHintMin
        : null;

    public long? Order => Record.Order;

    public string? VisibleText => Record.VisibleText;

    private static bool IsValidSequenceHint(long? sequenceHint)
    {
        return sequenceHint is >= 0;
    }
}
