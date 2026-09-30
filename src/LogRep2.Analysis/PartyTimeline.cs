using LogRep2.Contracts;

namespace FFXI_LogAnalyzer.Core;

public sealed record TimelineTargetResult(string? Target, int Damage);

public sealed record PartyTimelineEvent(
    string SessionId,
    string EventGroup,
    long? Order,
    string Actor,
    string ActionName,
    ActionType ActionType,
    string Status,
    bool IsExecuted,
    string? ReferenceTime,
    long? ReferenceTimeOrder,
    IReadOnlyList<TimelineTargetResult> Results,
    IReadOnlyList<ICanonicalRecord> SourceRecords)
{
    public long? Damage => Results.Count == 0 ? null : Results.Sum(result => (long)result.Damage);
}

public sealed record PartyTimeline(
    IReadOnlyList<PartyTimelineEvent> Events,
    IReadOnlyList<string> Warnings)
{
    public static PartyTimeline Empty { get; } = new([], []);
}
