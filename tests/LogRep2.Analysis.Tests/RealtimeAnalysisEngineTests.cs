using FFXI_LogAnalyzer.Core;
using System.Diagnostics;
using Xunit.Abstractions;

namespace FFXI_LogAnalyzer.Tests;

public sealed class RealtimeAnalysisEngineTests
{
    private readonly ITestOutputHelper _output;

    public RealtimeAnalysisEngineTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void Analyze_指定されたインデックス範囲だけを集計する()
    {
        var records = new[]
        {
            CreateRecord(1, "event-1", "Aliceの攻撃→Goblinに10ダメージ。", "12:00:00"),
            CreateRecord(2, "event-2", "Aliceの攻撃→Goblinに20ダメージ。", "12:00:01"),
            CreateRecord(3, "event-3", "Aliceの攻撃→Goblinに30ダメージ。", "12:00:02"),
        };

        var actual = new RealtimeAnalysisEngine().Analyze(records, 1, 3);

        Assert.Equal(2, actual.TargetRecordCount);
        Assert.Equal(50, actual.Result.ActorSummaries.Sum(actor => actor.TotalDamage));
    }

    [Fact]
    public void Analyze_同じ範囲なら通常分析と結果が一致する()
    {
        var records = Enumerable.Range(1, 20)
            .Select(index => CreateRecord(
                index,
                $"event-{index}",
                $"Aliceの攻撃→Goblinに{index}ダメージ。",
                $"12:00:{index:00}"))
            .ToArray();
        var realtime = new RealtimeAnalysisEngine().Analyze(records, 0, records.Length);

        var selection = new AnalysisRangeSelection(AnalysisEndpoint.LogStart, AnalysisEndpoint.LogEnd);
        var range = new AnalysisRangeBuilder().Build(records, selection);
        var time = new AnalysisTimeResolver().Resolve(selection, range);
        var parser = new ActionGroupParser(new DefaultAnalysisRuleSet());
        var parseResults = new ActionGroupBuilder().Build(range).Select(parser.ParseGroup).ToArray();
        var expected = new AnalysisAggregator().Aggregate(
            parseResults.Where(result => result.Parsed is not null).Select(result => result.Parsed!),
            time,
            parseResults.Where(result => result.Unparsed is not null).Select(result => result.Unparsed!));

        Assert.Equal(
            expected.ActorSummaries.Select(actor => (actor.Actor, actor.TotalDamage, actor.Dps)),
            realtime.Result.ActorSummaries.Select(actor => (actor.Actor, actor.TotalDamage, actor.Dps)));
        Assert.Equal(
            expected.ActionSummaries.Select(action => (action.Actor, action.ActionName, action.ActionType, action.Damage.TotalDamage)),
            realtime.Result.ActionSummaries.Select(action => (action.Actor, action.ActionName, action.ActionType, action.Damage.TotalDamage)));
        Assert.Equal(expected.AnalysisTime.Confidence, realtime.Result.AnalysisTime.Confidence);
        Assert.Equal(expected.AnalysisTime.DurationSeconds, realtime.Result.AnalysisTime.DurationSeconds);
        Assert.Equal(expected.AnalysisTime.StartTime, realtime.Result.AnalysisTime.StartTime);
        Assert.Equal(expected.AnalysisTime.EndTime, realtime.Result.AnalysisTime.EndTime);
    }

    [Fact]
    public void Analyze_一万件を全件再集計できる()
    {
        var records = Enumerable.Range(1, 10_000)
            .Select(index => CreateRecord(
                index,
                $"event-{index}",
                "Aliceの攻撃→Goblinに10ダメージ。",
                $"12:{index / 60 % 60:00}:{index % 60:00}"))
            .ToArray();

        GC.Collect();
        GC.WaitForPendingFinalizers();
        var allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
        var actual = new RealtimeAnalysisEngine().Analyze(records, 0, records.Length);
        var allocatedBytes = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
        var workingSetBytes = Process.GetCurrentProcess().WorkingSet64;

        Assert.Equal(10_000, actual.TargetRecordCount);
        Assert.Equal(100_000, actual.Result.ActorSummaries.Sum(actor => actor.TotalDamage));
        Assert.True(workingSetBytes < 300L * 1024 * 1024);
        _output.WriteLine($"再集計時間: {actual.Elapsed.TotalMilliseconds:N1} ms");
        _output.WriteLine($"再集計割当量: {allocatedBytes / 1024d / 1024d:N1} MB");
        _output.WriteLine($"テストプロセスWorking Set: {workingSetBytes / 1024d / 1024d:N1} MB");
    }

    private static CanonicalRecord CreateRecord(
        int order,
        string eventGroup,
        string text,
        string time)
    {
        return new CanonicalRecord
        {
            CanonicalRecordId = $"record-{order}",
            SessionId = "session-1",
            Order = order,
            EventGroup = eventGroup,
            VisibleText = text,
            MessageTimeText = time,
            FirstSeenAt = DateTimeOffset.Parse("2026-07-17T12:00:00+09:00").AddSeconds(order),
            LastSeenAt = DateTimeOffset.Parse("2026-07-17T12:00:00+09:00").AddSeconds(order),
        };
    }
}
