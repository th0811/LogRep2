namespace FFXI_LogAnalyzer.Core;

public interface IActorExtractor
{
    string? ExtractActor(ActionGroup group);
}
