using FFXI_LogAnalyzer.Core;

namespace FFXI_LogAnalyzer.Tests;

internal static class TestActionGroupFactory
{
    public static ActionGroup Create(params string[] visibleTexts)
    {
        var records = visibleTexts
            .Select((text, index) => new ActionGroupRecord(
                new CanonicalRecord
                {
                    SessionId = "session-1",
                    EventGroup = "event-1",
                    Order = index + 1,
                    VisibleText = text
                },
                index))
            .ToArray();

        return new ActionGroup(new ActionGroupKey("session-1", "event-1"), records);
    }
}
