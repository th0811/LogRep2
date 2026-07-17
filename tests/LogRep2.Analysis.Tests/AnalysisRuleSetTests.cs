using FFXI_LogAnalyzer.Core;

namespace FFXI_LogAnalyzer.Tests;

public class AnalysisRuleSetTests
{
    [Fact]
    public void DefaultAnalysisRuleSet_CanBeCreated()
    {
        var ruleSet = new DefaultAnalysisRuleSet();

        Assert.NotNull(ruleSet.ActionParser);
        Assert.NotNull(ruleSet.ActorExtractor);
        Assert.NotNull(ruleSet.ActionNameExtractor);
        Assert.NotNull(ruleSet.DamageParser);
        Assert.NotNull(ruleSet.HitStatusClassifier);
    }

    [Fact]
    public void DefaultAnalysisRuleSet_ExposesParsersThroughInterfaces()
    {
        AnalysisRuleSet ruleSet = new DefaultAnalysisRuleSet();
        var group = CreateGroup(
            "Xitraの攻撃。クリティカル！",
            "→Gurfurlur the Menacingに、573ダメージ。");

        var parsedAction = ruleSet.ActionParser.Parse(group);
        var actor = ruleSet.ActorExtractor.ExtractActor(group);
        var actionName = ruleSet.ActionNameExtractor.ExtractActionName(group);
        var damage = ruleSet.DamageParser.ParseDamage(group);
        var hitStatus = ruleSet.HitStatusClassifier.Classify(group, damage);

        Assert.Equal("Xitra", actor);
        Assert.Equal("通常攻撃", actionName);
        Assert.True(damage.HasDamage);
        Assert.Equal(573, damage.Damage);
        Assert.Equal(HitStatus.Hit, hitStatus);
        Assert.Equal(ActionType.NormalAttackCritical, parsedAction.ActionType);
    }

    [Fact]
    public void AnalysisRuleSet_CanBeReplaced()
    {
        AnalysisRuleSet ruleSet = new AnalysisRuleSet(
            new StubActionParser(),
            new StubActorExtractor(),
            new StubActionNameExtractor(),
            new StubDamageParser(),
            new StubHitStatusClassifier());
        var group = CreateGroup("任意のログ");

        var parsedAction = ruleSet.ActionParser.Parse(group);

        Assert.Equal("差し替えActor", ruleSet.ActorExtractor.ExtractActor(group));
        Assert.Equal("差し替えAction", ruleSet.ActionNameExtractor.ExtractActionName(group));
        Assert.Equal(999, ruleSet.DamageParser.ParseDamage(group).Damage);
        Assert.Equal(HitStatus.Excluded, ruleSet.HitStatusClassifier.Classify(group, ParsedDamageResult.None));
        Assert.Equal(ActionType.Magic, parsedAction.ActionType);
    }

    private static ActionGroup CreateGroup(params string[] visibleTexts)
    {
        var records = visibleTexts
            .Select((text, index) => new ActionGroupRecord(
                new CanonicalRecord
                {
                    SessionId = "session-1",
                    EventGroup = "event-1",
                    Order = index + 1,
                    VisibleText = text
                },
                index))
            .ToArray();

        return new ActionGroup(new ActionGroupKey("session-1", "event-1"), records);
    }

    private sealed class StubActionParser : IActionParser
    {
        public ParsedAction Parse(ActionGroup group)
        {
            return new ParsedAction(
                "差し替えActor",
                "差し替えAction",
                ActionType.Magic,
                ParsedDamageResult.FromDamage(999),
                HitStatus.Excluded);
        }
    }

    private sealed class StubActorExtractor : IActorExtractor
    {
        public string? ExtractActor(ActionGroup group)
        {
            return "差し替えActor";
        }
    }

    private sealed class StubActionNameExtractor : IActionNameExtractor
    {
        public string? ExtractActionName(ActionGroup group)
        {
            return "差し替えAction";
        }
    }

    private sealed class StubDamageParser : IDamageParser
    {
        public ParsedDamageResult ParseDamage(ActionGroup group)
        {
            return ParsedDamageResult.FromDamage(999);
        }
    }

    private sealed class StubHitStatusClassifier : IHitStatusClassifier
    {
        public HitStatus Classify(ActionGroup group, ParsedDamageResult damage)
        {
            return HitStatus.Excluded;
        }
    }
}
