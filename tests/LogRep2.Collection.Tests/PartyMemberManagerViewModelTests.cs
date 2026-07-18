using FfxiTempLogCollector.App;

namespace FfxiTempLogCollector.Tests;

public sealed class PartyMemberManagerViewModelTests
{
    [Fact]
    public void 初期表示で先頭候補を選択する()
    {
        var viewModel = new PartyMemberManagerViewModel(
            ["Alice"],
            ["Charlie", "Bob"],
            _ => { });

        Assert.Equal("Bob", viewModel.SelectedCandidate);
    }

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

    [Fact]
    public void 候補追加後は同じ位置の次候補を選択する()
    {
        var viewModel = new PartyMemberManagerViewModel(
            ["Alice"],
            ["Bob", "Charlie", "Dave"],
            _ => { });
        viewModel.SelectedCandidate = "Charlie";

        viewModel.AddCandidateCommand.Execute(null);

        Assert.Equal("Dave", viewModel.SelectedCandidate);
    }

    [Fact]
    public void 末尾候補追加後は一つ前の候補を選択する()
    {
        var viewModel = new PartyMemberManagerViewModel(
            ["Alice"],
            ["Bob", "Charlie"],
            _ => { });
        viewModel.SelectedCandidate = "Charlie";

        viewModel.AddCandidateCommand.Execute(null);

        Assert.Equal("Bob", viewModel.SelectedCandidate);
    }

    [Fact]
    public void メンバー削除後は同じ位置の次メンバーを選択する()
    {
        var viewModel = new PartyMemberManagerViewModel(
            ["Alice", "Bob", "Charlie"],
            [],
            _ => { });
        viewModel.SelectedMember = "Bob";

        viewModel.RemoveCommand.Execute(null);

        Assert.Equal("Charlie", viewModel.SelectedMember);
    }

    [Fact]
    public void 末尾メンバー削除後は一つ前のメンバーを選択する()
    {
        var viewModel = new PartyMemberManagerViewModel(
            ["Alice", "Bob"],
            [],
            _ => { });
        viewModel.SelectedMember = "Bob";

        viewModel.RemoveCommand.Execute(null);

        Assert.Equal("Alice", viewModel.SelectedMember);
    }

    [Fact]
    public void 候補追加で上限到達時は候補選択を解除する()
    {
        var viewModel = new PartyMemberManagerViewModel(
            ["A", "B", "C", "D", "E"],
            ["F", "G"],
            _ => { });

        viewModel.AddCandidateCommand.Execute(null);

        Assert.Null(viewModel.SelectedCandidate);
        Assert.False(viewModel.AddCandidateCommand.CanExecute(null));
    }
}
