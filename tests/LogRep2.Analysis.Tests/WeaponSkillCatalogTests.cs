using FFXI_LogAnalyzer.Core;

namespace FFXI_LogAnalyzer.Tests;

public sealed class WeaponSkillCatalogTests
{
    [Theory]
    [InlineData("レッドロータス", ActionType.WeaponSkill)]
    [InlineData("トアクリーバー", ActionType.WeaponSkill)]
    [InlineData("サベッジブレード", ActionType.WeaponSkill)]
    [InlineData("ルースレスストローク", ActionType.WeaponSkill)]
    [InlineData("零之太刀・回天", ActionType.WeaponSkill)]
    [InlineData("レデンサリュート", ActionType.WeaponSkill)]
    [InlineData("挑発", ActionType.Ability)]
    [InlineData("ケアルワルツIII", ActionType.Ability)]
    [InlineData("未登録の行動", ActionType.Ability)]
    [InlineData("サベッジブレード追加", ActionType.Ability)]
    public void 実行文型は辞書の完全一致だけをWSに分類する(string name, ActionType expected)
    {
        var group = TestActionGroupFactory.Create($"Xitraは、{name}を実行。", "→Bossに、100ダメージ。");
        var parsed = new ActionGroupParser(new DefaultAnalysisRuleSet()).ParseGroup(group).Parsed!;
        Assert.Equal(expected, parsed.ActionType);
        Assert.Equal(name, parsed.ActionName);
        Assert.Equal(100, parsed.Damage.Damage);
        var timeline = new PartyTimelineBuilder().Build(group.Records.Select(record => record.Record).ToArray());
        Assert.Equal(expected, Assert.Single(timeline.Events).ActionType);
    }

    [Fact]
    public void 辞書全件が解析でき魔法とアビリティ専用文型を変更しない()
    {
        var parser = new ActionGroupParser(new DefaultAnalysisRuleSet());
        foreach (var name in WeaponSkillCatalog.Names)
        {
            var parsed = parser.ParseGroup(TestActionGroupFactory.Create($"Xitraは、{name}を実行。"));
            Assert.Equal(ActionType.WeaponSkill, parsed.Parsed!.ActionType);
        }
        Assert.False(WeaponSkillCatalog.Contains(null));
        Assert.False(WeaponSkillCatalog.Contains(string.Empty));
        Assert.Equal(ActionType.Ability, parser.Parse(TestActionGroupFactory.Create("Xitraのバーサク！")).ActionType);
        Assert.Equal(ActionType.Magic, parser.Parse(TestActionGroupFactory.Create("Xitraのファイアが発動。")).ActionType);
    }
}
