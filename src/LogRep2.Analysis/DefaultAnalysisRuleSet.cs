namespace FFXI_LogAnalyzer.Core;

public sealed partial class DefaultAnalysisRuleSet : AnalysisRuleSet
{
    public DefaultAnalysisRuleSet()
        : this(
            new ActorExtractor(),
            new ActionNameExtractor(),
            new DamageParser(),
            new HitStatusClassifier())
    {
    }

    private DefaultAnalysisRuleSet(
        IActorExtractor actorExtractor,
        IActionNameExtractor actionNameExtractor,
        IDamageParser damageParser,
        IHitStatusClassifier hitStatusClassifier)
        : base(
            new ActionGroupParser(actorExtractor, actionNameExtractor, damageParser, hitStatusClassifier),
            actorExtractor,
            actionNameExtractor,
            damageParser,
            hitStatusClassifier)
    {
    }
}
