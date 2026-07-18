using FfxiTempLogCollector.App;

namespace FfxiTempLogCollector.Tests;

public sealed class PartyMemberManagerViewModelTests
{
    [Fact]
    public void 候補追加と並べ替えを保存する()
    {
        IReadOnlyList<string> saved = [];
        var viewModel = new PartyMemberManagerViewModel(
            ["Alice"],
            ["Bob"],
            members => saved = members);

        viewModel.SelectedCandidate = "Bob";
        viewModel.AddCandidateCommand.Execute(null);
        viewModel.SelectedMember = "Bob";
        viewModel.MoveUpCommand.Execute(null);

        Assert.Equal(["Bob", "Alice"], saved);
        Assert.Equal("現在のPTメンバー（2 / 6）", viewModel.CountText);
    }

    [Fact]
    public void PTメンバーは最大6名に制限する()
    {
        var viewModel = new PartyMemberManagerViewModel(
            ["A", "B", "C", "D", "E", "F"],
            ["G"],
            _ => { });

        viewModel.SelectedCandidate = "G";

        Assert.False(viewModel.AddCandidateCommand.CanExecute(null));
    }
}
