using FFXI_LogAnalyzer.Core;
using FfxiTempLogCollector.App;
using LogRep2.Infrastructure;

namespace FfxiTempLogCollector.Tests;

public sealed class OverlayViewModelTests
{
    [Fact]
    public void Apply_安定した分析項目を表示用に変換する()
    {
        var settings = new OverlaySettings { DisplayRowCount = 1 };
        var viewModel = new OverlayViewModel(settings, () => { }, () => { }, () => { });
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

        Assert.Equal("分析中", viewModel.StateText);
        Assert.Equal(300, viewModel.TotalDamage);
        Assert.Equal("30.00", viewModel.Dps);
        Assert.Equal("85.0%", viewModel.HitRate);
        Assert.Equal(["1. Bob  200"], viewModel.ActorRankings);
    }

    [Fact]
    public void ToggleEditing_固定設定を反転して保存通知する()
    {
        var saveCount = 0;
        var settings = new OverlaySettings { PositionLocked = true };
        var viewModel = new OverlayViewModel(settings, () => { }, () => { }, () => saveCount++);

        viewModel.ToggleEditingCommand.Execute(null);

        Assert.True(viewModel.IsEditing);
        Assert.False(settings.PositionLocked);
        Assert.Equal(1, saveCount);
    }

    [Fact]
    public void Opacity_操作不能にならない最低値へ制限する()
    {
        var settings = new OverlaySettings();
        var viewModel = new OverlayViewModel(settings, () => { }, () => { }, () => { });

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
