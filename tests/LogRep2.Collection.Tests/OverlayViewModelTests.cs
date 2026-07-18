using FFXI_LogAnalyzer.Core;
using FfxiTempLogCollector.App;
using LogRep2.Infrastructure;

namespace FfxiTempLogCollector.Tests;

public sealed class OverlayViewModelTests
{
    [Fact]
    public void Apply_PTメンバーを登録順で個人別表示へ変換する()
    {
        var settings = new OverlaySettings();
        var viewModel = new OverlayViewModel(
            settings,
            ["Bob", "Alice", "未登場"],
            () => { },
            () => { });
        var result = new AnalysisResult(
            [CreateActor("Alice", 100, 10, 8, 2), CreateActor("Bob", 200, 20, 9, 1)],
            [],
            [],
            new AnalysisTimeResult(TimeConfidence.Exact, 10, DateTimeOffset.Now, DateTimeOffset.Now.AddSeconds(10), []));

        viewModel.Apply(new RealtimeAnalysisSnapshot(
            RealtimeAnalysisState.Running,
            result,
            2,
            2,
            TimeSpan.FromMilliseconds(10),
            0,
            100,
            DateTimeOffset.Now,
            null));

        Assert.Equal(
            [
                new PartyMemberMetric("Bob", "20.00", "90.0%"),
                new PartyMemberMetric("Alice", "10.00", "80.0%"),
                new PartyMemberMetric("未登場", "-", "-"),
            ],
            viewModel.PartyMembers);
    }

    [Fact]
    public void Opacity_操作不能にならない最低値へ制限する()
    {
        var settings = new OverlaySettings();
        var viewModel = new OverlayViewModel(settings, [], () => { }, () => { });

        viewModel.OverlayOpacity = 0;

        Assert.Equal(0.25, settings.Opacity);
    }

    private static ActorSummary CreateActor(
        string actor,
        int damage,
        double dps,
        int hit,
        int miss)
    {
        return new ActorSummary(
            actor,
            damage,
            dps,
            TimeConfidence.Exact,
            hit / (double)(hit + miss),
            0,
            hit + miss,
            hit,
            miss,
            0,
            new NormalAttackSummary(hit, miss, 0, hit / (double)(hit + miss), 0),
            []);
    }
}
