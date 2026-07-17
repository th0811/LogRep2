namespace FFXI_LogAnalyzer.Core;

public interface IActionParser
{
    ParsedAction Parse(ActionGroup group);
}
