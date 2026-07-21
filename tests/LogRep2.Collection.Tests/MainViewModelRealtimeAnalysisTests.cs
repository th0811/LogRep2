using FfxiTempLogCollector.App;
using FfxiTempLogCollector.Core;

namespace FfxiTempLogCollector.Tests;

public sealed class MainViewModelRealtimeAnalysisTests
{
    [Theory]
    [InlineData(CollectorStatus.Stopped, false)]
    [InlineData(CollectorStatus.Starting, false)]
    [InlineData(CollectorStatus.Running, true)]
    [InlineData(CollectorStatus.Stopping, false)]
    [InlineData(CollectorStatus.Error, false)]
    public void リアルタイム分析は収集中だけ開始できる(
        CollectorStatus status,
        bool expected)
    {
        Assert.Equal(
            expected,
            MainViewModel.CanStartRealtimeAnalysis(status));
    }

    [Theory]
    [InlineData(CollectorStatus.Stopped, true)]
    [InlineData(CollectorStatus.Starting, false)]
    [InlineData(CollectorStatus.Running, false)]
    [InlineData(CollectorStatus.Stopping, false)]
    [InlineData(CollectorStatus.Error, true)]
    public void 収集停止またはエラーでリアルタイム分析を終了する(
        CollectorStatus status,
        bool expected)
    {
        Assert.Equal(
            expected,
            MainViewModel.ShouldStopRealtimeAnalysis(status));
    }
}
