namespace FFXI_LogAnalyzer.Core;

public sealed class ActionGroup
{
    public ActionGroup(
        ActionGroupKey key,
        IReadOnlyList<ActionGroupRecord> records)
    {
        Key = key;
        Records = records;
        OrderMin = records
            .Select(record => record.Order)
            .Where(order => order is not null)
            .Min();
        OrderMax = records
            .Select(record => record.Order)
            .Where(order => order is not null)
            .Max();
        VisibleTexts = records
            .Select(record => record.VisibleText)
            .Where(text => !string.IsNullOrEmpty(text))
            .Select(text => text!)
            .ToArray();
    }

    public ActionGroupKey Key { get; }

    public string ActionGroupKey => Key.ToString();

    public string SessionId => Key.SessionId;

    public string EventGroup => Key.EventGroup;

    public IReadOnlyList<ActionGroupRecord> Records { get; }

    public long? OrderMin { get; }

    public long? OrderMax { get; }

    public IReadOnlyList<string> VisibleTexts { get; }
}
