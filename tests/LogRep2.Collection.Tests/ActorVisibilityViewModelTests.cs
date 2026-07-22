using FFXI_LogAnalyzer.App;
using FFXI_LogAnalyzer.Core;

namespace FfxiTempLogCollector.Tests;

public sealed class ActorVisibilityViewModelTests
{
    [Fact]
    public void 登録状態に応じて登録メニューの操作可否を更新する()
    {
        var viewModel = CreateViewModel(ActorNameKind.PcCandidate);

        Assert.True(viewModel.RegisterAsPcCommand.CanExecute(null));
        Assert.True(viewModel.RegisterAsNpcCommand.CanExecute(null));
        Assert.False(viewModel.ClearRegistrationCommand.CanExecute(null));

        viewModel.UpdateClassification(ActorNameKind.RegisteredPc);

        Assert.False(viewModel.RegisterAsPcCommand.CanExecute(null));
        Assert.True(viewModel.RegisterAsNpcCommand.CanExecute(null));
        Assert.True(viewModel.ClearRegistrationCommand.CanExecute(null));
    }

    private static ActorVisibilityViewModel CreateViewModel(
        ActorNameKind nameKind)
    {
        var summary = new ActorSummaryViewModel(new ActorSummary(
            "Alice",
            100,
            10,
            TimeConfidence.Exact,
            1,
            0,
            1,
            1,
            0,
            0,
            new NormalAttackSummary(1, 0, 0, 1, 0),
            []));
        return new ActorVisibilityViewModel(
            summary,
            nameKind,
            true,
            _ => { },
            _ => { },
            _ => { });
    }
}
