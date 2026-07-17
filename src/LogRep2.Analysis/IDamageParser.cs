namespace FFXI_LogAnalyzer.Core;

public interface IDamageParser
{
    ParsedDamageResult ParseDamage(ActionGroup group);
}
