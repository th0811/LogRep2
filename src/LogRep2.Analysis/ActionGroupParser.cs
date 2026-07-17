namespace FFXI_LogAnalyzer.Core;

public sealed class ActionGroupParser : IActionParser
{
    private readonly IActorExtractor _actorExtractor;
    private readonly IActionNameExtractor _actionNameExtractor;
    private readonly IDamageParser _damageParser;
    private readonly IHitStatusClassifier _hitStatusClassifier;
    private readonly MagicLogClassifier _magicLogClassifier = new();

    public ActionGroupParser(AnalysisRuleSet ruleSet)
        : this(
            ruleSet.ActorExtractor,
            ruleSet.ActionNameExtractor,
            ruleSet.DamageParser,
            ruleSet.HitStatusClassifier)
    {
    }

    public ActionGroupParser(
        IActorExtractor actorExtractor,
        IActionNameExtractor actionNameExtractor,
        IDamageParser damageParser,
        IHitStatusClassifier hitStatusClassifier)
    {
        _actorExtractor = actorExtractor;
        _actionNameExtractor = actionNameExtractor;
        _damageParser = damageParser;
        _hitStatusClassifier = hitStatusClassifier;
    }

    public ParsedAction Parse(ActionGroup group)
    {
        var parseResult = ParseGroup(group);
        return parseResult.Parsed?.ParsedAction ?? parseResult.Unparsed!.ParsedAction;
    }

    public ActionGroupParseResult ParseGroup(ActionGroup group)
    {
        if (_magicLogClassifier.TryParseCastStart(
                group,
                out var castActor,
                out var castActionName))
        {
            var excludedDamage = ParsedDamageResult.None;
            var excludedAction = new ParsedAction(
                castActor,
                castActionName,
                ActionType.Magic,
                excludedDamage,
                HitStatus.Excluded);

            return ActionGroupParseResult.FromParsed(new ParsedActionGroup(
                group,
                castActor,
                castActionName,
                ActionType.Magic,
                excludedDamage,
                HitStatus.Excluded,
                excludedAction));
        }

        var actor = _actorExtractor.ExtractActor(group);
        var actionName = _actionNameExtractor.ExtractActionName(group);
        var actionType = ResolveActionType(group, _actionNameExtractor);
        var damage = _damageParser.ParseDamage(group);
        var hitStatus = _hitStatusClassifier.Classify(group, damage);
        var parsedAction = new ParsedAction(actor, actionName, actionType, damage, hitStatus);

        if (string.IsNullOrWhiteSpace(actor) || string.IsNullOrWhiteSpace(actionName))
        {
            return ActionGroupParseResult.FromUnparsed(new UnparsedActionGroup(
                group,
                parsedAction,
                "actorまたはactionを特定できません。"));
        }

        return ActionGroupParseResult.FromParsed(new ParsedActionGroup(
            group,
            actor,
            actionName,
            actionType,
            damage,
            hitStatus,
            parsedAction));
    }

    private static ActionType ResolveActionType(ActionGroup group, IActionNameExtractor actionNameExtractor)
    {
        return actionNameExtractor is ActionNameExtractor defaultExtractor
            ? defaultExtractor.ExtractActionType(group)
            : ActionType.Unknown;
    }
}
