namespace FFXI_LogAnalyzer.Core;

public class AnalysisRuleSet
{
    public AnalysisRuleSet(
        IActionParser actionParser,
        IActorExtractor actorExtractor,
        IActionNameExtractor actionNameExtractor,
        IDamageParser damageParser,
        IHitStatusClassifier hitStatusClassifier)
    {
        ActionParser = actionParser;
        ActorExtractor = actorExtractor;
        ActionNameExtractor = actionNameExtractor;
        DamageParser = damageParser;
        HitStatusClassifier = hitStatusClassifier;
    }

    public IActionParser ActionParser { get; }

    public IActorExtractor ActorExtractor { get; }

    public IActionNameExtractor ActionNameExtractor { get; }

    public IDamageParser DamageParser { get; }

    public IHitStatusClassifier HitStatusClassifier { get; }
}
