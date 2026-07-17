namespace FFXI_LogAnalyzer.Core;

public interface IHitStatusClassifier
{
    HitStatus Classify(ActionGroup group, ParsedDamageResult damage);
}
