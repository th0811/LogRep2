namespace FFXI_LogAnalyzer.Core;

public sealed record ActionGroupKey(string SessionId, string EventGroup)
{
    public override string ToString()
    {
        return $"{SessionId}:{EventGroup}";
    }
}
