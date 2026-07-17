using FfxiTempLogCollector.App;
using FfxiTempLogCollector.Core;

namespace FfxiTempLogCollector.Tests;

public sealed class RealtimeAnalysisControllerTests
{
    [Fact]
    public async Task Start_開始時点より後のレコードだけを対象にする()
    {
        var events = new CollectorEvents();
        await using var controller = new RealtimeAnalysisController(events, 250);
        controller.AcceptSnapshot(CreateSnapshot(2));
        controller.Start();
        controller.AcceptSnapshot(CreateSnapshot(4));

        var result = await WaitForResultAsync(controller, 2);

        Assert.Equal(RealtimeAnalysisState.Running, result.State);
        Assert.Equal(2, result.TargetRecordCount);
    }

    [Fact]
    public async Task Reset_分析を継続してリセット後だけを対象にする()
    {
        var events = new CollectorEvents();
        await using var controller = new RealtimeAnalysisController(events, 250);
        controller.Start();
        controller.AcceptSnapshot(CreateSnapshot(2));
        await WaitForResultAsync(controller, 2);

        controller.Reset();
        controller.AcceptSnapshot(CreateSnapshot(5));
        var result = await WaitForResultAsync(controller, 3);

        Assert.Equal(RealtimeAnalysisState.Running, result.State);
        Assert.Equal(3, result.TargetRecordCount);
    }

    [Fact]
    public async Task Stop_終了時点より後のレコードを反映しない()
    {
        var events = new CollectorEvents();
        await using var controller = new RealtimeAnalysisController(events, 250);
        controller.Start();
        controller.AcceptSnapshot(CreateSnapshot(3));
        controller.Stop();
        controller.AcceptSnapshot(CreateSnapshot(5));

        var result = await WaitForResultAsync(controller, 3);

        Assert.Equal(RealtimeAnalysisState.Completed, result.State);
        Assert.Equal(3, result.TargetRecordCount);
    }

    [Fact]
    public async Task 更新集中時_古い要求を破棄して最新結果を採用する()
    {
        var events = new CollectorEvents();
        await using var controller = new RealtimeAnalysisController(events, 250);
        controller.Start();
        controller.AcceptSnapshot(CreateSnapshot(1));
        controller.AcceptSnapshot(CreateSnapshot(2));
        controller.AcceptSnapshot(CreateSnapshot(4));

        var result = await WaitForResultAsync(controller, 4);

        Assert.True(result.DiscardedAggregationCount >= 2);
        Assert.Equal(4, result.TargetRecordCount);
    }

    private static CanonicalSnapshot CreateSnapshot(int count)
    {
        var records = Enumerable.Range(1, count)
            .Select(index => new CanonicalRecord
            {
                CanonicalRecordId = $"record-{index}",
                SessionId = "session-1",
                Order = index,
                EventGroup = $"event-{index}",
                VisibleText = "Aliceの攻撃→Goblinに10ダメージ。",
                MessageTimeText = $"12:00:{index:00}",
                FirstSeenAt = DateTimeOffset.Now.AddSeconds(index),
                LastSeenAt = DateTimeOffset.Now.AddSeconds(index),
            })
            .ToArray();
        return new CanonicalSnapshot("session-1", records, DateTimeOffset.Now);
    }

    private static async Task<RealtimeAnalysisSnapshot> WaitForResultAsync(
        RealtimeAnalysisController controller,
        int expectedCount)
    {
        var timeout = DateTimeOffset.UtcNow.AddSeconds(5);
        while (DateTimeOffset.UtcNow < timeout)
        {
            var current = controller.Current;
            if (current.TargetRecordCount == expectedCount && current.Result is not null)
            {
                return current;
            }

            await Task.Delay(25);
        }

        throw new TimeoutException("リアルタイム分析結果の更新を確認できませんでした。");
    }
}
