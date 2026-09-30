using System.Net;
using FFXI_LogAnalyzer.App;
using FFXI_LogAnalyzer.Core;

namespace FfxiTempLogCollector.Tests;

public sealed class PartyTimelineExportTests
{
    [Fact]
    public void 被攻撃のみのPCにも被ダメージ統計と回避率を表示する()
    {
        var records = new[]
        {
            Record(1, "hit", "Bossの攻撃。"), Record(2, "hit", "→Aliceに、100ダメージ。"),
            Record(3, "magic", "Bossのファイアが発動。"), Record(4, "magic", "→Aliceに、300ダメージ。"),
            Record(5, "miss", "Bossの攻撃。"), Record(6, "miss", "Aliceは攻撃をかわした。"),
        };
        var result = new RealtimeAnalysisEngine().Analyze(records, 0, records.Length).Result;
        var html = PartyTimelineHtmlExporter.Build(new PartyTimelineBuilder().Build(records), ["Alice"], "戦闘", "全区間", 0, result.ActorSummaries);
        Assert.Contains("<th>被ダメージ合計</th><th>被ダメージ最大</th><th>被ダメージ最小</th><th>被ダメージ平均</th><th>回避率</th>", html);
        Assert.Contains("<td>400</td><td>300</td><td>100</td><td>200.00</td><td>50.00%</td>", html);
        var viewModel = new ActorSummaryViewModel(result.ActorSummaries.Single(item => item.Actor == "Alice"));
        Assert.Equal("400", viewModel.IncomingDamage);
        Assert.Equal("50％(1/2)", viewModel.EvasionRate);
    }

    [Fact]
    public void PCサマリは通常攻撃とWSの命中率を合算しWS専用統計を表示する()
    {
        var records = new List<CanonicalRecord>();
        void Add(string group, params string[] lines)
        {
            foreach (var line in lines) records.Add(Record(records.Count + 1, group, line));
        }
        Add("normal-hit", "Aliceの攻撃。", "→Bossに、1000ダメージ。");
        Add("normal-miss", "Aliceの攻撃。ミス。");
        Add("normal-unknown", "Aliceの攻撃。");
        Add("ws-hit", "Aliceは、レッドロータスを実行。", "→Bossに、100ダメージ。", "→Otherに、200ダメージ。");
        Add("ws-zero", "Aliceは、レッドロータスを実行。", "→Bossに、0ダメージ。");
        Add("ws-miss", "Aliceは、レッドロータスを実行。", "ミス。");
        Add("ws-unknown", "Aliceは、レッドロータスを実行。");
        Add("magic", "Aliceのファイアが発動。", "→Bossに、9000ダメージ。");
        Add("ability", "Aliceのジャンプ！", "→Bossに、10ダメージ。");
        Add("other-player", "Boroの攻撃。", "→Bossに、50ダメージ。");
        var parser = new ActionGroupParser(new DefaultAnalysisRuleSet());
        var parsed = new ActionGroupBuilder().Build(records).Select(group => parser.ParseGroup(group).Parsed!).ToArray();
        var summaries = new AnalysisAggregator().Aggregate(parsed, AnalysisTimeResult.Unknown([])).ActorSummaries;
        var html = PartyTimelineHtmlExporter.Build(new PartyTimelineBuilder().Build(records), ["Alice", "Boro", "Carol"], "戦闘", "1–30", 0, summaries);
        var section = System.Text.RegularExpressions.Regex.Match(html, "<section id=\"numbers\">(.*?)</section>",
            System.Text.RegularExpressions.RegexOptions.Singleline).Value;
        Assert.Contains("<th>PC</th><th>ダメージ合計</th><th>攻撃命中率</th><th>WS回数</th><th>WSダメージ平均</th><th>WSダメージ最大</th><th>WSダメージ最小</th>", section);
        Assert.Contains("<td>Alice</td><td>9,310</td><td>60.00%</td><td>4</td><td>150.00</td><td>300</td><td>0</td>", section);
        Assert.Contains("<td>Boro</td><td>—</td><td>100.00%</td><td>0</td><td>—</td><td>—</td><td>—</td>", section);
        Assert.Contains("<td>Carol</td><td>—</td><td>—</td><td>0</td><td>—</td><td>—</td><td>—</td>", section);
        Assert.DoesNotContain("<th>種別</th>", section);
        Assert.DoesNotContain("<th>使用回数</th>", section);
    }

    [Fact]
    public void サマリ直後に図表を置き詳細には元ログ本文だけを保持する()
    {
        var records = new CanonicalRecord[]
        {
            new() { SessionId = "内部セッション", EventGroup = "内部グループ", CanonicalRecordId = "内部ID", Order = 987,
                VisibleText = "Aliceのバーサク！" },
            new() { SessionId = "内部セッション", EventGroup = "内部グループ", Order = 988,
                VisibleText = "→Aliceは、バーサクの効果。" },
        };
        var html = PartyTimelineHtmlExporter.Build(new PartyTimelineBuilder().Build(records), ["Alice"], "戦闘", "987–988", 0);
        Assert.True(html.IndexOf("id=\"numbers\"", StringComparison.Ordinal) < html.IndexOf("id=\"timeline\"", StringComparison.Ordinal));
        Assert.True(html.IndexOf("id=\"timeline\"", StringComparison.Ordinal) < html.IndexOf("id=\"action-summary\"", StringComparison.Ordinal));
        Assert.Contains("<section id=\"action-summary\">", html);
        Assert.Equal(10, System.Text.RegularExpressions.Regex.Matches(html, "class=\"sort-button\"").Count);
        Assert.Equal(7, System.Text.RegularExpressions.Regex.Matches(html, "data-type=\"number\"").Count);
        var source = System.Text.RegularExpressions.Regex.Match(html, "<template id=\"event-1\">(.*?)</template>",
            System.Text.RegularExpressions.RegexOptions.Singleline).Groups[1].Value;
        Assert.Equal(string.Join("\n", records.Select(record => record.VisibleText)) + "\n", WebUtility.HtmlDecode(source));
        Assert.DoesNotContain("内部ID", html);
        Assert.DoesNotContain("内部グループ", html);
        Assert.DoesNotContain("個々の行動", html);
        Assert.Contains("aria-controls=\"log-dialog\"", html);
    }

    [Fact]
    public void 連続行動を詰めてもプレイヤー順序と全件へのリンクを維持する()
    {
        var actors = new[] { "Alice", "Boro", "Alice", "Carol", "Boro", "Alice" };
        var timeline = new PartyTimelineBuilder().Build(actors.Select((actor, index) =>
            Record(index + 1, $"g{index}", $"{actor}のバーサク！")).ToArray());
        var rows = PartyTimelineLayout.BuildRows(timeline.Events);
        Assert.Equal(3, rows.Count);
        Assert.Equal(new[] { 0, 1 }, rows[0]);
        Assert.Equal(new[] { 2, 3, 4 }, rows[1]);
        Assert.Equal(new[] { 5 }, rows[2]);
        var html = PartyTimelineHtmlExporter.Build(timeline, ["Alice", "Boro", "Carol"], "戦闘", "1–6", 0);
        Assert.Equal(3, System.Text.RegularExpressions.Regex.Matches(html, "class=\"action-row\"").Count);
        for (var number = 1; number <= actors.Length; number++)
        {
            Assert.Contains($"id=\"row-{number}\"", html);
            Assert.Contains($"<template id=\"event-{number}\"", html);
            Assert.Contains($"data-log=\"event-{number}\"", html);
        }
    }

    [Fact]
    public void 最大三件とセッションと参考時刻を境界にし同じ時刻の別ログはまとめる()
    {
        var template = new PartyTimelineBuilder().Build([Record(1, "g", "Aliceのバーサク！")]).Events[0];
        var events = new[]
        {
            template,
            template with { Actor = "Boro" },
            template with { Actor = "Carol" },
            template with { Actor = "David" },
            template with { Actor = "Erin", ReferenceTime = "[10:00]", ReferenceTimeOrder = 5 },
            template with { Actor = "Fred", ReferenceTime = "[10:00]", ReferenceTimeOrder = 6 },
            template with { Actor = "Gina", ReferenceTime = "[10:01]" },
            template with { Actor = "Hank", ReferenceTime = "[10:01]", SessionId = "second" },
        };
        var rows = PartyTimelineLayout.BuildRows(events);
        Assert.Equal(new[] { 3, 1, 2, 1, 1 }, rows.Select(row => row.Count));
        Assert.Equal(Enumerable.Range(0, events.Length), rows.SelectMany(row => row));
        Assert.Empty(PartyTimelineLayout.BuildRows([]));
    }

    [Fact]
    public void 出力前の候補は登録PCとPC候補だけでNPCを再追加しない()
    {
        using var directory = new TemporaryDirectory();
        var store = new AnalyzerSettingsStore(directory.Path);
        store.Save(new AnalyzerSettings { KnownPcNames = ["Registered Player"], KnownNpcNames = ["Goblin"] });
        var records = new[] { "Alice", "Registered Player", "Goblin", "Unknown Enemy" }
            .Select((actor, index) => Record(index + 1, $"g{index}", $"{actor}のバーサク！")).ToArray();
        var parsed = new ActionGroupBuilder().Build(records)
            .Select(group => new ActionGroupParser(new DefaultAnalysisRuleSet()).ParseGroup(group).Parsed!).ToArray();
        var result = new AnalysisAggregator().Aggregate(parsed, AnalysisTimeResult.Unknown([])) with
        {
            Timeline = new PartyTimelineBuilder().Build(records),
        };
        var viewModel = new AnalysisResultViewModel(store);
        viewModel.Load(result, []);
        var candidates = viewModel.GetTimelineMemberCandidates();
        Assert.Equal(new[] { "Alice", "Registered Player" }, candidates.Select(item => item.Name));
        Assert.Equal(new[] { "PC候補", "PC登録" }, candidates.Select(item => item.Classification));
    }

    [Fact]
    public void 選択メンバーだけを数値と図表に出しHTMLをエスケープする()
    {
        var records = new[]
        {
            Record(1, "a", "Xitraの<script>alert(1)</script>！"),
            Record(2, "b", "Boroのウォークライ！"),
        };
        var timeline = new PartyTimelineBuilder().Build(records);
        var html = PartyTimelineHtmlExporter.Build(timeline, ["Xitra"], "<戦闘>", "1–2", 0);
        Assert.DoesNotContain("<script>alert(1)</script>", html);
        Assert.Contains(WebUtility.HtmlEncode("<script>alert(1)</script>"), html);
        Assert.DoesNotContain("Boro", html);
        Assert.DoesNotContain("ウォークライ", html);
        Assert.Contains("<template id=\"event-1\"", html);
        Assert.Contains("data-log=\"event-1\"", html);
        Assert.Contains("縦の間隔は経過時間を表しません", html);
        Assert.DoesNotContain("<script src", html);
    }

    [Fact]
    public async Task 区間境界と手動除外をHTML用イベントにも適用する()
    {
        var records = new[]
        {
            Record(1, "outside", "Xitraのバーサク！"),
            new CanonicalRecord { Order = 2, IsMarker = true, MarkerKeyword = "開始" },
            Record(3, "a", "Xitraは、レッドロータスを実行。"),
            Record(4, "a", "→Bossに、100ダメージ。"),
            Record(5, "b", "Boroのウォークライ！"),
            new CanonicalRecord { Order = 6, IsMarker = true, MarkerKeyword = "終了" },
            Record(7, "outside2", "Xitraのバーサク！"),
        };
        var viewModel = new AnalysisRangeViewModel();
        await viewModel.LoadRecordsAsync(records, CancellationToken.None);
        viewModel.IsManualRangeMode = true;
        viewModel.SelectedStartMarker = viewModel.Markers[0];
        viewModel.SelectedEndMarker = viewModel.Markers[1];
        viewModel.SetExcludedRecords([records[3]]);
        var result = await AnalysisExclusionIntegrationTests.Analyze(viewModel);
        Assert.Equal(2, result.Timeline.Events.Count);
        Assert.All(result.Timeline.Events, item => Assert.Null(item.Damage));
        var html = PartyTimelineHtmlExporter.Build(result.Timeline, ["Xitra", "Boro"], "ボス戦", "3–5", result.ExcludedRecordCount);
        Assert.DoesNotContain("バーサク", html);
        Assert.DoesNotContain("100ダメージ", html);
        Assert.Contains("ウォークライ", html);
    }

    [Fact]
    public void 出力対象がなくても説明とプレイヤー列を出す()
    {
        var html = PartyTimelineHtmlExporter.Build(PartyTimeline.Empty, ["Xitra"], "ボス戦", "—", 0);
        Assert.Contains("選択したメンバーの表示対象行動はありません", html);
        Assert.Contains("<th>Xitra</th>", html);
        Assert.Throws<ArgumentException>(() => PartyTimelineHtmlExporter.Build(PartyTimeline.Empty, [], "ボス戦", "—", 0));
    }

    private static CanonicalRecord Record(long order, string group, string text) => new()
    {
        SessionId = "session",
        Order = order,
        EventGroup = group,
        VisibleText = text,
    };
}
