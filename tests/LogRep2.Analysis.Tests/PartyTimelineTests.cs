using FFXI_LogAnalyzer.Core;

namespace FFXI_LogAnalyzer.Tests;

public sealed class PartyTimelineTests
{
    [Theory]
    [InlineData("サムライロール", 5)]
    [InlineData("ウィザーズロール", 11)]
    public void ロールの合計値を行動名に含めず同じ名前で集計する(string name, int total)
    {
        var declaration = $"Xitraの{name}→合計値が{total}になった！";
        var effect = $"→Xitraに{name}の効果。";
        var parser = new ActionGroupParser(new DefaultAnalysisRuleSet());
        var first = parser.ParseGroup(TestActionGroupFactory.Create(declaration, effect)).Parsed!;
        var second = parser.ParseGroup(TestActionGroupFactory.Create($"Xitraの{name}→合計値が1になった！", effect)).Parsed!;
        Assert.Equal("Xitra", first.Actor);
        Assert.Equal(name, first.ActionName);
        Assert.Equal(ActionType.Ability, first.ActionType);
        var summary = Assert.Single(new AnalysisAggregator().Aggregate([first, second], AnalysisTimeResult.Unknown([])).ActionSummaries);
        Assert.Equal(2, summary.UseCount);
        var item = Assert.Single(Build(R(1, "roll", declaration), R(2, "roll", effect)).Events);
        Assert.Equal(name, item.ActionName);
        Assert.Null(item.Damage);
        Assert.Equal(declaration, item.SourceRecords[0].VisibleText);
    }

    [Fact]
    public void アビリティ宣言を既存集計にも取り込む()
    {
        var group = TestActionGroupFactory.Create("Xitraのバーサク！");
        var parsed = new ActionGroupParser(new DefaultAnalysisRuleSet()).ParseGroup(group).Parsed!;
        Assert.Equal("Xitra", parsed.Actor);
        Assert.Equal("バーサク", parsed.ActionName);
        Assert.Equal(ActionType.Ability, parsed.ActionType);
        var result = new AnalysisAggregator().Aggregate([parsed], AnalysisTimeResult.Unknown([]));
        Assert.Equal(1, Assert.Single(result.ActionSummaries).UseCount);
        Assert.False(parsed.Damage.HasDamage);
    }

    [Theory]
    [InlineData("Xitraのジョブポイントがアップ！")]
    [InlineData("Xitraのジョブポイントがアップ!")]
    public void ジョブポイント通知を既存解析とタイムラインの行動から除外する(string text)
    {
        Assert.False(AbilityLogClassifier.TryParse(text, out _, out _));
        var parser = new ActionGroupParser(new DefaultAnalysisRuleSet());
        Assert.Null(parser.ParseGroup(TestActionGroupFactory.Create(text)).Parsed);
        Assert.Empty(Build(R(1, "notice", text)).Events);
        var group = TestActionGroupFactory.Create(text, "Xitraのバーサク！");
        var parsed = parser.ParseGroup(group).Parsed!;
        Assert.Equal("バーサク", parsed.ActionName);
        Assert.Equal(ActionType.Ability, parsed.ActionType);
    }

    [Theory]
    [InlineData("→XitraのHPが、100回復！")]
    [InlineData("→Xitraのバイオの効果を消し去った！")]
    [InlineData("Xitraは、睡眠の状態になった！")]
    public void 効果結果をアビリティ宣言と誤認しない(string text)
    {
        Assert.False(AbilityLogClassifier.TryParse(text, out _, out _));
    }

    [Fact]
    public void 通常攻撃と連携と実行者不明を除き対象別ダメージを保持する()
    {
        var timeline = Build(
            R(1, "normal", "Xitraの攻撃。"),
            R(2, "normal", "→Bossに、50ダメージ。"),
            R(3, "ws", "Xitraは、レッドロータスを実行。"),
            R(4, "ws", "→Bossに、120ダメージ。"),
            R(5, "ws", "→Otherに、30ダメージ。"),
            R(6, "ws", "技連携・核熱！"),
            R(7, "ws", "→Bossに、300ダメージ。"),
            R(8, "orphan", "→Bossに、999ダメージ。"));
        var item = Assert.Single(timeline.Events);
        Assert.Equal(150L, item.Damage);
        Assert.Equal(new[] { "Boss", "Other" }, item.Results.Select(result => result.Target));
        Assert.Equal(3L, item.Order);
    }

    [Fact]
    public void 同じグループ名でもセッションをまたいで紐付けない()
    {
        var timeline = Build(R(1, "same", "Xitraは、レッドロータスを実行。"),
            R(2, "same", "→Bossに、999ダメージ。", session: "other"));
        Assert.Null(Assert.Single(timeline.Events).Damage);
    }

    [Fact]
    public void 時刻がなくても実行行の順番で並べる()
    {
        var timeline = Build(R(1, "a", "Xitraは、ファイアを唱えた。"),
            R(2, "b", "Boroのバーサク！"), R(3, "a", "Xitraのファイアが発動。"),
            R(4, "a", "→Bossに、100ダメージ。"));
        Assert.Equal(new[] { "Boro", "Xitra" }, timeline.Events.Select(item => item.Actor));
        Assert.All(timeline.Events, item => Assert.Null(item.ReferenceTime));
        Assert.Equal(100L, timeline.Events[1].Damage);
        var parsed = new ActionGroupParser(new DefaultAnalysisRuleSet()).ParseGroup(
            TestActionGroupFactory.Create("Xitraは、ファイアを唱えた。", "Xitraのファイアが発動。", "→Bossに、100ダメージ。"));
        Assert.Equal(HitStatus.Hit, parsed.Parsed!.HitStatus);
    }

    [Fact]
    public void 開始だけは表示せず中断を使用回数に含めない()
    {
        var timeline = Build(R(1, "a", "Xitraは、ファイアを唱えた。"),
            R(2, "b", "Boroは、ケアルを唱えた。"), R(3, "b", "詠唱中断。"));
        var item = Assert.Single(timeline.Events);
        Assert.Equal("中断", item.Status);
        Assert.False(item.IsExecuted);
        Assert.Equal(3L, item.Order);
    }

    [Fact]
    public void 未確認とゼロダメージとミスを区別する()
    {
        var timeline = Build(R(1, "a", "Xitraのバーサク！"),
            R(2, "b", "Xitraは、レッドロータスを実行。"), R(3, "b", "→Bossに、0ダメージ。"),
            R(4, "c", "Xitraは、レッドロータスを実行。"), R(5, "c", "ミス。"));
        Assert.Null(timeline.Events[0].Damage);
        Assert.Equal(0L, timeline.Events[1].Damage);
        Assert.Null(timeline.Events[2].Damage);
        Assert.Equal("ミス・効果なし", timeline.Events[2].Status);
    }

    [Fact]
    public void 重複する宣言は一回で曖昧な行動候補は警告する()
    {
        var timeline = Build(R(1, "a", "Xitraのバーサク！"), R(2, "a", "Xitraのバーサク！"),
            R(3, "b", "Xitraのウォークライ！"), R(4, "b", "Boroのウォークライ！"));
        Assert.Single(timeline.Events);
        Assert.Single(timeline.Warnings);
    }

    [Fact]
    public void 通常攻撃行の参考時刻を利用するが収集時刻を補間しない()
    {
        var timeline = Build(R(1, "normal", "Xitraの攻撃。", time: "[23:59]"),
            R(2, "a", "Xitraのバーサク！"), R(3, "b", "Xitraのウォークライ！", session: "other"));
        Assert.Equal("[23:59]", timeline.Events[0].ReferenceTime);
        Assert.Equal(1L, timeline.Events[0].ReferenceTimeOrder);
        Assert.Null(timeline.Events[1].ReferenceTime);
    }

    [Fact]
    public void 通常攻撃が混在したグループのダメージをWSに加算しない()
    {
        var timeline = Build(R(1, "a", "Xitraは、レッドロータスを実行。"),
            R(2, "a", "→Bossに、100ダメージ。"), R(3, "a", "Boroの攻撃。"),
            R(4, "a", "→Bossに、50ダメージ。"));
        Assert.Empty(timeline.Events);
        Assert.Single(timeline.Warnings);
    }

    [Fact]
    public void 別グループの結果やグループなしを結合しない()
    {
        var timeline = Build(R(1, "a", "Xitraのファイアが発動。"), R(2, "b", "→Bossに、100ダメージ。"),
            R(3, "", "Xitraのバーサク！"));
        Assert.Null(Assert.Single(timeline.Events).Damage);
        Assert.Single(timeline.Warnings);
    }

    private static PartyTimeline Build(params CanonicalRecord[] records) => new PartyTimelineBuilder().Build(records);
    private static CanonicalRecord R(long order, string group, string text, string session = "session", string? time = null) => new()
    {
        SessionId = session,
        EventGroup = group,
        Order = order,
        VisibleText = text,
        MessageTimeText = time,
        CanonicalRecordId = $"{session}-{order}",
        FirstSeenAt = DateTimeOffset.Now,
    };
}
