namespace FFXI_LogAnalyzer.Core;

public interface IActionNameExtractor
{
    string? ExtractActionName(ActionGroup group);
}
